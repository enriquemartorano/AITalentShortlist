namespace TalentShortlist.Domain.Entities;

public sealed class RankingCriterion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Weight { get; init; }
    public bool IsMandatory { get; init; }
    public List<string> Keywords { get; init; } = [];
}
