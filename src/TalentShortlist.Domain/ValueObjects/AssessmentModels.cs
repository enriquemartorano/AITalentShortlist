using TalentShortlist.Domain.Enums;

namespace TalentShortlist.Domain.ValueObjects;

public sealed class CriterionScore
{
    public Guid CriterionId { get; init; }
    public string CriterionName { get; init; } = string.Empty;
    public int Score { get; init; }
    public decimal WeightedScore { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

public sealed class EvidenceItem
{
    public Guid CriterionId { get; init; }
    public string Excerpt { get; init; } = string.Empty;
    public string SourceFile { get; init; } = string.Empty;
    public double Confidence { get; init; }
}

public sealed class ExtractedCv
{
    public string FileName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public sealed class CandidateAssessment
{
    public Guid CandidateId { get; init; }
    public string CandidateName { get; init; } = string.Empty;
    public decimal TotalScore { get; init; }
    public Recommendation Recommendation { get; init; }
    public string ExecutiveSummary { get; init; } = string.Empty;
    public bool MandatoryRequirementsMet { get; init; }
    public bool HumanReviewRequired { get; init; } = true;
    public List<string> Strengths { get; init; } = [];
    public List<string> Gaps { get; init; } = [];
    public List<CriterionScore> CriterionScores { get; init; } = [];
    public List<EvidenceItem> Evidence { get; init; } = [];
}
