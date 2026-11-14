using Levvy.Domain.Entities;

namespace Levvy.Application.Common.Interfaces;

/// <summary>Session persistence boundary (EF Core in Infrastructure).</summary>
public interface ISessionRepository
{
    Task<Session?> GetByCodeAsync(string code, CancellationToken ct);
    Task<Session?> GetWithTranscriptAsync(Guid sessionId, CancellationToken ct);
    Task AddAsync(Session session, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
