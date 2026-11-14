using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Levvy.Application.Commands;
using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Llm;
using MediatR;

namespace Levvy.Api.Hubs;

/// <summary>
/// Manages real-time WebSocket connections for live lecture sessions (/ws/{sessionId}?lang=es&amp;role=host|viewer).
/// Hot audio chunks bypass MediatR for &lt;3s latency, emitting CaptionFinalized notifications at chunk boundaries
/// which are persisted asynchronously via Hangfire.
/// </summary>
public sealed class LectureSessionHub(
    IServiceScopeFactory scopeFactory,
    ILogger<LectureSessionHub> logger)
{
    private readonly ConcurrentDictionary<Guid, SessionRoom> _rooms = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task HandleConnectionAsync(
        HttpContext context,
        Guid sessionId,
        WebSocket socket,
        CancellationToken ct)
    {
        var lang = context.Request.Query["lang"].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lang)) lang = "en";

        var role = context.Request.Query["role"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? "viewer";
        var sessionCode = context.Request.Query["code"].FirstOrDefault()?.Trim() ?? sessionId.ToString("N")[..6].ToUpperInvariant();
        var participantId = Guid.TryParse(context.Request.Query["participantId"].FirstOrDefault(), out var pid)
            ? pid
            : Guid.NewGuid();

        var connectionId = Guid.NewGuid();
        var client = new ConnectedClient(connectionId, participantId, role, lang, socket);
        var room = _rooms.GetOrAdd(sessionId, _ => new SessionRoom(sessionId, sessionCode));
        room.Clients[connectionId] = client;

        logger.LogInformation(
            "WS client {ConnectionId} ({Role}, lang={Lang}) joined session {SessionId}",
            connectionId, role, lang, sessionId);

        try
        {
            await ReceiveLoopAsync(room, client, ct);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket closed abruptly for client {ConnectionId}", connectionId);
        }
        finally
        {
            room.Clients.TryRemove(connectionId, out _);
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session closed", CancellationToken.None);
                }
                catch
                {
                    // Ignore close errors
                }
            }
        }
    }

    private async Task ReceiveLoopAsync(SessionRoom room, ConnectedClient client, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        using var messageStream = new MemoryStream();

        while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            messageStream.SetLength(0);
            WebSocketReceiveResult result;

            do
            {
                result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                messageStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            messageStream.Position = 0;

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                await HandleAudioChunkAsync(room, messageStream, ct);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                using var reader = new StreamReader(messageStream, Encoding.UTF8, leaveOpen: true);
                var textPayload = await reader.ReadToEndAsync(ct);
                await HandleTextMessageAsync(room, client, textPayload, ct);
            }
        }
    }

    public async Task HandleAudioChunkAsync(
        SessionRoom room,
        Stream audioChunk,
        CancellationToken ct)
    {
        var chunkOffsetMs = room.ReserveNextChunkOffsetMs(defaultChunkDurationMs: 5000);

        using var scope = scopeFactory.CreateScope();
        var asr = scope.ServiceProvider.GetRequiredService<IAsrStreamer>();
        var translator = scope.ServiceProvider.GetRequiredService<ITranslator>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        IReadOnlyList<TranscriptSegment> segments;
        try
        {
            segments = await asr.TranscribeChunkAsync(audioChunk, chunkOffsetMs, room.SourceLanguage, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ASR chunk transcription failed for session {SessionId} at {OffsetMs}ms", room.SessionId, chunkOffsetMs);
            return;
        }

        if (segments.Count == 0)
            return;

        foreach (var seg in segments)
        {
            seg.SessionId = room.SessionId;
            room.AppendSegment(seg);

            var ts = TimestampFormatter.FormatMmSs(seg.OffsetMs);

            // 1. Broadcast original caption immediately to all viewers
            await BroadcastJsonAsync(room, new
            {
                type = "caption",
                ts,
                offsetMs = seg.OffsetMs,
                text = seg.Text,
                final = true
            }, ct);

            // 2. Publish CaptionFinalized seam (enqueues Hangfire persistence job off the hot loop)
            try
            {
                await mediator.Publish(new CaptionFinalized(room.SessionId, seg.OffsetMs, seg.Text, seg.SourceLanguage), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CaptionFinalized notification handler failed for session {SessionId}", room.SessionId);
            }

            // 3. Translate once per distinct target language among connected viewers
            var targetLanguages = room.Clients.Values
                .Select(c => c.PreferredLanguage)
                .Where(l => !string.IsNullOrWhiteSpace(l) &&
                            !string.Equals(l, seg.SourceLanguage, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetLanguages.Count > 0)
            {
                var translationTasks = targetLanguages.Select(async targetLang =>
                {
                    try
                    {
                        var translated = await translator.TranslateAsync(seg.Text, targetLang, ct);
                        await BroadcastToLanguageAsync(room, targetLang, new
                        {
                            type = "translation",
                            @for = targetLang,
                            ts,
                            offsetMs = seg.OffsetMs,
                            text = translated
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Translation to '{Lang}' failed for session {SessionId}", targetLang, room.SessionId);
                    }
                });

                await Task.WhenAll(translationTasks);
            }
        }
    }

    private async Task HandleTextMessageAsync(
        SessionRoom room,
        ConnectedClient sender,
        string rawJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return;

        InboundWsMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<InboundWsMessage>(rawJson, JsonOpts);
        }
        catch (JsonException)
        {
            return;
        }

        if (msg is null || string.IsNullOrWhiteSpace(msg.Type))
            return;

        var type = msg.Type.Trim().ToLowerInvariant();

        if (type == "chunk_meta")
        {
            if (msg.OffsetMs.HasValue && msg.OffsetMs.Value >= 0)
                room.SetNextChunkOffsetMs(msg.OffsetMs.Value);
            if (!string.IsNullOrWhiteSpace(msg.Lang))
                room.SourceLanguage = msg.Lang.Trim().ToLowerInvariant();
            return;
        }

        if (type == "set_lang" && !string.IsNullOrWhiteSpace(msg.Lang))
        {
            sender.PreferredLanguage = msg.Lang.Trim().ToLowerInvariant();
            return;
        }

        if (type == "ask" && !string.IsNullOrWhiteSpace(msg.Text))
        {
            await HandleAskQuestionAsync(room, sender, msg.Text.Trim(), ct);
        }
    }

    private async Task HandleAskQuestionAsync(
        SessionRoom room,
        ConnectedClient asker,
        string questionText,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var answerer = scope.ServiceProvider.GetRequiredService<ILlmAnswerer>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        string status;
        string answerText;
        IReadOnlyList<long> receiptOffsetsMs;

        try
        {
            var result = await mediator.Send(new AskQuestionCommand(room.SessionCode, asker.ParticipantId, questionText), ct);
            status = result.Status;
            answerText = result.Text;
            receiptOffsetsMs = result.ReceiptOffsetsMs;
        }
        catch
        {
            try
            {
                var snapshot = room.GetTranscriptSnapshot();
                (status, answerText, receiptOffsetsMs) = await answerer.AnswerFromTranscriptAsync(questionText, snapshot, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ask Levvy failed for session {SessionId}", room.SessionId);
                status = "not_covered";
                answerText = "Unable to verify an answer from the lecture transcript right now.";
                receiptOffsetsMs = Array.Empty<long>();
            }
        }

        var receiptSeconds = receiptOffsetsMs.Select(ms => ms / 1000).ToArray();

        await SendJsonAsync(asker.Socket, new
        {
            type = "answer",
            text = answerText,
            receipts = receiptSeconds,
            receiptOffsetsMs,
            status
        }, ct);
    }

    private static async Task BroadcastJsonAsync(SessionRoom room, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = room.Clients.Values
            .Where(c => c.Socket.State == WebSocketState.Open)
            .Select(c => SafeSendAsync(c.Socket, segment, ct));

        await Task.WhenAll(tasks);
    }

    private static async Task BroadcastToLanguageAsync(SessionRoom room, string lang, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = room.Clients.Values
            .Where(c => c.Socket.State == WebSocketState.Open &&
                        string.Equals(c.PreferredLanguage, lang, StringComparison.OrdinalIgnoreCase))
            .Select(c => SafeSendAsync(c.Socket, segment, ct));

        await Task.WhenAll(tasks);
    }

    private static Task SendJsonAsync(WebSocket socket, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        return SafeSendAsync(socket, new ArraySegment<byte>(bytes), ct);
    }

    private static async Task SafeSendAsync(WebSocket socket, ArraySegment<byte> bytes, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            return;

        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        catch
        {
            // Ignore disconnected socket send failure
        }
    }
}
