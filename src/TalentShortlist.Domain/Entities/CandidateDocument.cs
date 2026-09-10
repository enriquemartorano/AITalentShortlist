namespace TalentShortlist.Domain.Entities;

public sealed class CandidateDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; init; } = string.Empty;
    public string CandidateName { get; init; } = string.Empty;
    public string ExtractedText { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
    public bool IsAnonymized { get; init; }
}
