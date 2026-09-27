using System.Globalization;
using TalentShortlist.Domain.Entities;

namespace TalentShortlist.Api.JobDescriptions;

public sealed record JobDescriptionSummary(string Id, string Title, string Department, string Location);

public sealed class JobDescriptionCatalog(IWebHostEnvironment environment)
{
    private string DirectoryPath => Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "job-descriptions"));

    public IReadOnlyList<JobDescriptionSummary> List()
    {
        EnsureDirectoryExists();

        return Directory.EnumerateFiles(DirectoryPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Select(path => new { Path = path, Description = Parse(File.ReadAllText(path)) })
            .OrderBy(item => item.Description.Title, StringComparer.OrdinalIgnoreCase)
            .Select(item => new JobDescriptionSummary(
                Path.GetFileNameWithoutExtension(item.Path),
                item.Description.Title,
                item.Description.Department,
                item.Description.Location))
            .ToArray();
    }

    public JobDescription Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid job description id.");
        }

        var path = Path.Combine(DirectoryPath, $"{id}.txt");
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("Job description was not found.", id);
        }

        return Parse(File.ReadAllText(fullPath));
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            throw new DirectoryNotFoundException($"Job description directory was not found: {DirectoryPath}");
        }
    }

    private static JobDescription Parse(string content)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentList = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) && currentList is not null)
            {
                lists[currentList].Add(line[2..].Trim());
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key is "RequiredSkills" or "PreferredSkills" or "RequiredLanguages")
            {
                currentList = key;
                lists[key] = [];
                if (value.Length > 0)
                {
                    lists[key].AddRange(value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }
            else
            {
                currentList = null;
                fields[key] = value;
            }
        }

        return new JobDescription
        {
            Title = Required(fields, "Title"),
            Department = Required(fields, "Department"),
            Location = Required(fields, "Location"),
            Description = Required(fields, "Description"),
            MinimumYearsOfExperience = int.Parse(Required(fields, "MinimumYearsOfExperience"), CultureInfo.InvariantCulture),
            RequiredSkills = GetList(lists, "RequiredSkills"),
            PreferredSkills = GetList(lists, "PreferredSkills"),
            RequiredLanguages = GetList(lists, "RequiredLanguages")
        };
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"Missing required job description field: {key}.");

    private static List<string> GetList(IReadOnlyDictionary<string, List<string>> lists, string key) =>
        lists.TryGetValue(key, out var values) ? values : [];
}
