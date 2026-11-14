using Levvy.Application.Common.Interfaces;
using Levvy.Application.Events;
using Levvy.Domain.Entities;
using MediatR;

namespace Levvy.Application.Commands;

public sealed class AskQuestionCommandHandler(
    ISessionRepository repo,
    ILlmAnswerer answerer,
    IPublisher publisher) : IRequestHandler<AskQuestionCommand, AskQuestionResult>
{
    public async Task<AskQuestionResult> Handle(AskQuestionCommand request, CancellationToken ct)
    {
        var session = await repo.GetByCodeAsync(request.CodeOrTrimmed(), ct)
            ?? throw new KeyNotFoundException($"Session '{request.SessionCode}' was not found.");

        var transcriptSoFar = session.Segments
            .OrderBy(s => s.OffsetMs)
            .ToList();

        var (status, answerText, receiptOffsetsMs) = await answerer.AnswerFromTranscriptAsync(
            request.Text,
            transcriptSoFar,
            ct);

        var questionId = Guid.NewGuid();
        var answerId = Guid.NewGuid();

        var citations = receiptOffsetsMs.Select(offset =>
        {
            var matchedSeg = transcriptSoFar.FirstOrDefault(s => s.OffsetMs == offset);
            return new AnswerCitation
            {
                Id = Guid.NewGuid(),
                AnswerId = answerId,
                OffsetMs = offset,
                SegmentText = matchedSeg?.Text ?? string.Empty
            };
        }).ToList();

        var question = new Question
        {
            Id = questionId,
            SessionId = session.Id,
            AskerId = request.AskerId == Guid.Empty ? Guid.NewGuid() : request.AskerId,
            Text = request.Text.Trim(),
            AskedAtUtc = DateTime.UtcNow,
            Answer = new Answer
            {
                Id = answerId,
                QuestionId = questionId,
                Text = answerText,
                Status = status == "ok" ? "answered" : "not_covered",
                Citations = citations,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        session.Questions.Add(question);
        await repo.SaveChangesAsync(ct);

        await publisher.Publish(new QuestionAsked(session.Id, questionId, question.Text), ct);

        return new AskQuestionResult(status, answerText, receiptOffsetsMs);
    }
}

internal static class AskQuestionCommandExtensions
{
    public static string CodeOrTrimmed(this AskQuestionCommand cmd) => cmd.SessionCode.Trim();
}
