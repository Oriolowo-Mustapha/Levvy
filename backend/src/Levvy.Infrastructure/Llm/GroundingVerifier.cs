using System.Text.Json;
using Levvy.Application.Common.Interfaces;
using Levvy.Domain.Entities;
using Levvy.Infrastructure.Llm.Models;

namespace Levvy.Infrastructure.Llm;

/// <summary>
/// Enforces Levvy's core non-negotiable rule in code: cite-or-say-not-covered.
/// Verifies every returned receipt offset against the real session transcript segments.
/// </summary>
public static class GroundingVerifier
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static (string Status, string Text, IReadOnlyList<long> ReceiptOffsetsMs) ParseAndEnforceGrounding(
        string rawJson,
        IReadOnlyList<TranscriptSegment> transcriptSoFar,
        string? latestTsOverride = null)
    {
        var ordered = transcriptSoFar.OrderBy(s => s.OffsetMs).ToList();
        var latestTs = latestTsOverride ?? (ordered.Count > 0 ? TimestampFormatter.FormatMmSs(ordered[^1].OffsetMs) : "00:00");
        var defaultNotCoveredText = $"The lecturer is at {latestTs}. This topic hasn't come up in the lecture yet.";

        var cleaned = StripMarkdownCodeFence(rawJson);
        if (string.IsNullOrWhiteSpace(cleaned))
            return ("not_covered", defaultNotCoveredText, Array.Empty<long>());

        GroundedAnswerDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GroundedAnswerDto>(cleaned, JsonOpts);
        }
        catch (JsonException)
        {
            return ("not_covered", defaultNotCoveredText, Array.Empty<long>());
        }

        if (dto is null)
            return ("not_covered", defaultNotCoveredText, Array.Empty<long>());

        var normalizedStatus = (dto.Status ?? "").Trim().ToLowerInvariant();
        if (normalizedStatus is "not_covered" or "notcovered" or "unanswered")
        {
            var msg = string.IsNullOrWhiteSpace(dto.Text) ? defaultNotCoveredText : dto.Text.Trim();
            return ("not_covered", msg, Array.Empty<long>());
        }

        var verifiedReceipts = VerifyReceiptsAgainstTranscript(dto.Receipts, ordered);

        if (verifiedReceipts.Count == 0 || string.IsNullOrWhiteSpace(dto.Text))
        {
            return ("not_covered", defaultNotCoveredText, Array.Empty<long>());
        }

        return ("ok", dto.Text.Trim(), verifiedReceipts);
    }

    public static GeneratedStudyPackResult ParseAndValidateStudyPack(
        string rawJson,
        IReadOnlyList<TranscriptSegment> transcript)
    {
        var ordered = transcript.OrderBy(s => s.OffsetMs).ToList();
        var cleaned = StripMarkdownCodeFence(rawJson);
        if (string.IsNullOrWhiteSpace(cleaned) || ordered.Count == 0)
            return new GeneratedStudyPackResult("Study pack unavailable.", Array.Empty<GeneratedFlashcardItem>());

        StudyPackLlmDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<StudyPackLlmDto>(cleaned, JsonOpts);
        }
        catch (JsonException)
        {
            return new GeneratedStudyPackResult("Study pack unavailable.", Array.Empty<GeneratedFlashcardItem>());
        }

        if (dto is null)
            return new GeneratedStudyPackResult("Study pack unavailable.", Array.Empty<GeneratedFlashcardItem>());

        var validCards = new List<GeneratedFlashcardItem>();
        if (dto.Flashcards is not null)
        {
            foreach (var card in dto.Flashcards)
            {
                if (string.IsNullOrWhiteSpace(card.Front) || string.IsNullOrWhiteSpace(card.Back))
                    continue;

                var snapped = SnapToTranscriptOffset(card.SourceOffsetMs, ordered);
                if (snapped.HasValue)
                {
                    validCards.Add(new GeneratedFlashcardItem(
                        card.Front.Trim(),
                        card.Back.Trim(),
                        snapped.Value));
                }
            }
        }

        var summary = string.IsNullOrWhiteSpace(dto.Summary)
            ? $"Lecture covered {ordered.Count} segments through {TimestampFormatter.FormatMmSs(ordered[^1].OffsetMs)}."
            : dto.Summary.Trim();

        return new GeneratedStudyPackResult(summary, validCards);
    }

    public static long? SnapToTranscriptOffset(long candidateOffsetMs, IReadOnlyList<TranscriptSegment> orderedTranscript)
    {
        if (candidateOffsetMs < 0 || orderedTranscript.Count == 0)
            return null;

        foreach (var seg in orderedTranscript)
        {
            if (seg.OffsetMs == candidateOffsetMs)
                return seg.OffsetMs;
        }

        if (candidateOffsetMs > 0 && candidateOffsetMs < 10_000 && orderedTranscript[^1].OffsetMs >= 10_000)
        {
            var asMs = candidateOffsetMs * 1000;
            foreach (var seg in orderedTranscript)
            {
                if (Math.Abs(seg.OffsetMs - asMs) <= 2000)
                    return seg.OffsetMs;
            }
        }

        TranscriptSegment? nearest = null;
        var minDiff = long.MaxValue;
        foreach (var seg in orderedTranscript)
        {
            var diff = Math.Abs(seg.OffsetMs - candidateOffsetMs);
            if (diff < minDiff)
            {
                minDiff = diff;
                nearest = seg;
            }
        }

        return (nearest is not null && minDiff <= 2000) ? nearest.OffsetMs : null;
    }

    private static IReadOnlyList<long> VerifyReceiptsAgainstTranscript(
        List<long>? rawReceipts,
        IReadOnlyList<TranscriptSegment> orderedTranscript)
    {
        if (rawReceipts is null || rawReceipts.Count == 0 || orderedTranscript.Count == 0)
            return Array.Empty<long>();

        var verified = new List<long>();
        var seen = new HashSet<long>();

        foreach (var candidate in rawReceipts)
        {
            var snapped = SnapToTranscriptOffset(candidate, orderedTranscript);
            if (snapped.HasValue && seen.Add(snapped.Value))
            {
                verified.Add(snapped.Value);
            }
        }

        return verified;
    }

    private static string StripMarkdownCodeFence(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
            trimmed = trimmed[(firstNewline + 1)..];

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
            trimmed = trimmed[..^3];

        return trimmed.Trim();
    }
}
