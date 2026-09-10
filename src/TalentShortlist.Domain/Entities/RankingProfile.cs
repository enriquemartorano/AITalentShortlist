namespace TalentShortlist.Domain.Entities;

public sealed class RankingProfile
{
    public string Name { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public bool AnonymizeCandidates { get; init; }
    public List<RankingCriterion> Criteria { get; init; } = [];

    public void Validate()
    {
        if (Criteria.Count == 0)
        {
            throw new ArgumentException("At least one ranking criterion is required.");
        }

        if (Criteria.Sum(criterion => criterion.Weight) != 100)
        {
            throw new ArgumentException("The total weight of criteria must sum to 100.");
        }

        if (Criteria.Any(criterion => criterion.Weight < 0 || criterion.Keywords.Count == 0))
        {
            throw new ArgumentException("Each criterion must have a non-negative weight and at least one keyword.");
        }
    }
}
