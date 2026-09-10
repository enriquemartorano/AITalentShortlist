using System.Collections.Concurrent;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Domain.Entities;

namespace TalentShortlist.Infrastructure.Repositories;

public sealed class InMemoryCandidateRepository : ICandidateRepository
{
    private readonly ConcurrentDictionary<Guid, CandidateDocument> _candidates = new();

    public Task<IReadOnlyCollection<CandidateDocument>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CandidateDocument>>(_candidates.Values.ToArray());

    public Task AddAsync(CandidateDocument candidate, CancellationToken cancellationToken)
    {
        _candidates[candidate.Id] = candidate;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _candidates.Clear();
        return Task.CompletedTask;
    }
}
