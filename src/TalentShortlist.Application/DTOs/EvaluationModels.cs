using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.ValueObjects;

namespace TalentShortlist.Application.DTOs;

public enum EvaluationMode
{
    Heuristic,
    Ai
}

public sealed class EvaluationRequestDto
{
    public JobDescription JobDescription { get; init; } = new();
    public RankingProfile RankingProfile { get; init; } = new();
    public List<Guid> CandidateIds { get; init; } = [];
    public EvaluationMode EvaluationMode { get; init; } = EvaluationMode.Heuristic;
}

public sealed class EvaluationResponseDto
{
    public required IReadOnlyList<CandidateAssessment> Ranking { get; init; }
    public string Evaluator { get; init; } = "DeterministicCandidateEvaluator";
    public string PromptVersion { get; init; } = "candidate-ranking-v1";
    public bool HumanReviewRequired { get; init; } = true;
    public long DurationMilliseconds { get; init; }
}
