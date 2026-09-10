using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.ValueObjects;

namespace TalentShortlist.Application.Contracts;

public interface ICandidateRepository
{
    Task<IReadOnlyCollection<CandidateDocument>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(CandidateDocument candidate, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public interface ICandidateEvaluator
{
    Task<IReadOnlyList<CandidateAssessment>> EvaluateAsync(
        RankingProfile rankingProfile,
        IReadOnlyCollection<CandidateDocument> candidates,
        CancellationToken cancellationToken);
}

public interface IAiCandidateEvaluator
{
    Task<IReadOnlyList<CandidateAssessment>> EvaluateAsync(
        RankingProfile rankingProfile,
        IReadOnlyCollection<CandidateDocument> candidates,
        CancellationToken cancellationToken);
}

public sealed class OpenAiSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
}

public interface ICvTextExtractor
{
    Task<ExtractedCv> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken);
}

public interface IDemoDataService
{
    Task SeedAsync(CancellationToken cancellationToken);
}
