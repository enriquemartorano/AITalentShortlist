namespace TalentShortlist.Domain.Entities;

public sealed class JobDescription
{
    public string Title { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<string> RequiredSkills { get; init; } = [];
    public List<string> PreferredSkills { get; init; } = [];
    public List<string> RequiredLanguages { get; init; } = [];
    public int MinimumYearsOfExperience { get; init; }
}
