$ErrorActionPreference = 'Stop'

$root = 'C:\Users\asdur\Repos\AITalentShortlist2'
Set-Location $root

$existingItems = Get-ChildItem -Force | Where-Object { $_.Name -ne 'bootstrap.ps1' }
if ($existingItems) {
    throw "The directory must contain only bootstrap.ps1 before bootstrap runs: $root"
}

function Write-TextFile {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Content
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Set-Content -Path $Path -Value $Content -Encoding utf8
}

dotnet new sln --name AITalentShortlist --format sln

New-Item -ItemType Directory -Force -Path src, tests, samples\cvs | Out-Null

dotnet new classlib --framework net10.0 --name TalentShortlist.Domain --output src\TalentShortlist.Domain
dotnet new classlib --framework net10.0 --name TalentShortlist.Application --output src\TalentShortlist.Application
dotnet new classlib --framework net10.0 --name TalentShortlist.Infrastructure --output src\TalentShortlist.Infrastructure
dotnet new webapi --framework net10.0 --no-https --no-openapi --name TalentShortlist.Api --output src\TalentShortlist.Api
dotnet new xunit --framework net10.0 --name TalentShortlist.UnitTests --output tests\TalentShortlist.UnitTests

dotnet add .\src\TalentShortlist.Application\TalentShortlist.Application.csproj reference `
    .\src\TalentShortlist.Domain\TalentShortlist.Domain.csproj

dotnet add .\src\TalentShortlist.Infrastructure\TalentShortlist.Infrastructure.csproj reference `
    .\src\TalentShortlist.Domain\TalentShortlist.Domain.csproj `
    .\src\TalentShortlist.Application\TalentShortlist.Application.csproj

dotnet add .\src\TalentShortlist.Api\TalentShortlist.Api.csproj reference `
    .\src\TalentShortlist.Domain\TalentShortlist.Domain.csproj `
    .\src\TalentShortlist.Application\TalentShortlist.Application.csproj `
    .\src\TalentShortlist.Infrastructure\TalentShortlist.Infrastructure.csproj

dotnet add .\tests\TalentShortlist.UnitTests\TalentShortlist.UnitTests.csproj reference `
    .\src\TalentShortlist.Domain\TalentShortlist.Domain.csproj `
    .\src\TalentShortlist.Application\TalentShortlist.Application.csproj `
    .\src\TalentShortlist.Infrastructure\TalentShortlist.Infrastructure.csproj

dotnet sln .\AITalentShortlist.sln add `
    .\src\TalentShortlist.Domain\TalentShortlist.Domain.csproj `
    .\src\TalentShortlist.Application\TalentShortlist.Application.csproj `
    .\src\TalentShortlist.Infrastructure\TalentShortlist.Infrastructure.csproj `
    .\src\TalentShortlist.Api\TalentShortlist.Api.csproj `
    .\tests\TalentShortlist.UnitTests\TalentShortlist.UnitTests.csproj

Get-ChildItem .\src, .\tests -Recurse -Include Class1.cs,UnitTest1.cs,WeatherForecast.cs,WeatherForecastController.cs |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-TextFile '.\src\TalentShortlist.Domain\Enums\Recommendation.cs' @'
namespace TalentShortlist.Domain.Enums;

public enum Recommendation
{
    StrongMatch,
    PotentialMatch,
    ReviewRequired,
    NotRecommended
}
'@

Write-TextFile '.\src\TalentShortlist.Domain\Entities\CandidateDocument.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Domain\Entities\JobDescription.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Domain\Entities\RankingCriterion.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Domain\Entities\RankingProfile.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Domain\ValueObjects\AssessmentModels.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Application\Contracts\Contracts.cs' @'
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

public interface ICvTextExtractor
{
    Task<ExtractedCv> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken);
}

public interface IDemoDataService
{
    Task SeedAsync(CancellationToken cancellationToken);
}
'@

Write-TextFile '.\src\TalentShortlist.Application\DTOs\EvaluationModels.cs' @'
using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.ValueObjects;

namespace TalentShortlist.Application.DTOs;

public sealed class EvaluationRequestDto
{
    public JobDescription JobDescription { get; init; } = new();
    public RankingProfile RankingProfile { get; init; } = new();
    public List<Guid> CandidateIds { get; init; } = [];
}

public sealed class EvaluationResponseDto
{
    public required IReadOnlyList<CandidateAssessment> Ranking { get; init; }
    public string Evaluator { get; init; } = "DeterministicCandidateEvaluator";
    public string PromptVersion { get; init; } = "candidate-ranking-v1";
    public bool HumanReviewRequired { get; init; } = true;
    public long DurationMilliseconds { get; init; }
}
'@

Write-TextFile '.\src\TalentShortlist.Application\Services\ShortlistService.cs' @'
using System.Diagnostics;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.DTOs;

namespace TalentShortlist.Application.Services;

public sealed class ShortlistService(
    ICandidateRepository candidateRepository,
    ICandidateEvaluator candidateEvaluator)
{
    public async Task<EvaluationResponseDto> EvaluateAsync(
        EvaluationRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.RankingProfile.Validate();

        if (request.CandidateIds.Count == 0)
        {
            throw new ArgumentException("At least one candidate must be selected.");
        }

        var candidates = await candidateRepository.GetAllAsync(cancellationToken);
        var selectedCandidates = candidates
            .Where(candidate => request.CandidateIds.Contains(candidate.Id))
            .ToArray();

        if (selectedCandidates.Length == 0)
        {
            throw new ArgumentException("No selected candidates were found.");
        }

        var stopwatch = Stopwatch.StartNew();
        var ranking = await candidateEvaluator.EvaluateAsync(
            request.RankingProfile,
            selectedCandidates,
            cancellationToken);
        stopwatch.Stop();

        return new EvaluationResponseDto
        {
            Ranking = ranking,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
'@

Write-TextFile '.\src\TalentShortlist.Infrastructure\Repositories\InMemoryCandidateRepository.cs' @'
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
'@

Write-TextFile '.\src\TalentShortlist.Infrastructure\Demo\DemoServices.cs' @'
using TalentShortlist.Application.Contracts;
using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.Enums;
using TalentShortlist.Domain.ValueObjects;

namespace TalentShortlist.Infrastructure.Demo;

public sealed class DemoCvTextExtractor : ICvTextExtractor
{
    public async Task<ExtractedCv> ExtractAsync(
        Stream document,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(document);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return new ExtractedCv
        {
            FileName = fileName,
            Text = text
        };
    }
}

public sealed class DemoDataService(ICandidateRepository repository) : IDemoDataService
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await repository.ClearAsync(cancellationToken);

        var candidates = new[]
        {
            ("Alex Rivera", "SharePoint Online, Power Platform, Power Automate, Microsoft Graph and Azure specialist. English. Solution architecture, governance and security."),
            ("Casey Park", "Java, Oracle, AWS and Kubernetes specialist with team leadership experience."),
            ("Jordan Lee", "Two years working with SharePoint Online and Power Automate. English."),
            ("Morgan Taylor", "SharePoint Online governance, security, stakeholder leadership and solution architecture."),
            ("Riley Quinn", "Power Platform, Power Automate, Microsoft Graph, Azure and English communication skills."),
            ("Sam Drew", "Entry-level office administration experience with spreadsheets and document management.")
        };

        foreach (var item in candidates)
        {
            await repository.AddAsync(new CandidateDocument
            {
                FileName = item.Item1.Replace(" ", "-").ToLowerInvariant() + ".md",
                CandidateName = item.Item1,
                ExtractedText = item.Item2
            }, cancellationToken);
        }
    }
}

public sealed class DeterministicCandidateEvaluator : ICandidateEvaluator
{
    public Task<IReadOnlyList<CandidateAssessment>> EvaluateAsync(
        RankingProfile rankingProfile,
        IReadOnlyCollection<CandidateDocument> candidates,
        CancellationToken cancellationToken)
    {
        rankingProfile.Validate();

        var assessments = candidates
            .Select(candidate => EvaluateCandidate(rankingProfile, candidate))
            .OrderByDescending(assessment => assessment.TotalScore)
            .ThenBy(assessment => assessment.CandidateName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<CandidateAssessment>>(assessments);
    }

    private static CandidateAssessment EvaluateCandidate(
        RankingProfile profile,
        CandidateDocument candidate)
    {
        var scores = new List<CriterionScore>();
        var evidence = new List<EvidenceItem>();
        var strengths = new List<string>();
        var gaps = new List<string>();
        var mandatoryMet = true;

        foreach (var criterion in profile.Criteria)
        {
            var matches = criterion.Keywords
                .Where(keyword => candidate.ExtractedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var score = (int)Math.Round(matches.Length * 100d / criterion.Keywords.Count);
            var weightedScore = Math.Round(score * criterion.Weight / 100m, 2);

            scores.Add(new CriterionScore
            {
                CriterionId = criterion.Id,
                CriterionName = criterion.Name,
                Score = score,
                WeightedScore = weightedScore,
                Explanation = $"Matched {matches.Length} of {criterion.Keywords.Count} configured keywords."
            });

            if (matches.Length > 0)
            {
                strengths.Add(criterion.Name);
                evidence.Add(new EvidenceItem
                {
                    CriterionId = criterion.Id,
                    Excerpt = matches[0],
                    SourceFile = candidate.FileName,
                    Confidence = score / 100d
                });
            }

            if (criterion.IsMandatory && matches.Length == 0)
            {
                mandatoryMet = false;
                gaps.Add($"Mandatory criterion not evidenced: {criterion.Name}.");
            }
        }

        var totalScore = scores.Sum(score => score.WeightedScore);
        var recommendation = !mandatoryMet
            ? Recommendation.NotRecommended
            : totalScore >= 80 ? Recommendation.StrongMatch
            : totalScore >= 60 ? Recommendation.PotentialMatch
            : Recommendation.ReviewRequired;

        return new CandidateAssessment
        {
            CandidateId = candidate.Id,
            CandidateName = profile.AnonymizeCandidates
                ? $"Candidate {candidate.Id.ToString("N")[..6]}"
                : candidate.CandidateName,
            TotalScore = totalScore,
            Recommendation = recommendation,
            ExecutiveSummary = $"{recommendation}: weighted evidence score {totalScore:0.##}/100.",
            MandatoryRequirementsMet = mandatoryMet,
            HumanReviewRequired = true,
            Strengths = strengths,
            Gaps = gaps,
            CriterionScores = scores,
            Evidence = evidence
        };
    }
}
'@

Write-TextFile '.\src\TalentShortlist.Api\Program.cs' @'
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.Services;
using TalentShortlist.Infrastructure.Demo;
using TalentShortlist.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSingleton<ICandidateRepository, InMemoryCandidateRepository>();
builder.Services.AddSingleton<ICandidateEvaluator, DeterministicCandidateEvaluator>();
builder.Services.AddSingleton<ICvTextExtractor, DemoCvTextExtractor>();
builder.Services.AddSingleton<IDemoDataService, DemoDataService>();
builder.Services.AddScoped<ShortlistService>();

var app = builder.Build();

app.UseCors("LocalFrontend");
app.MapControllers();
app.Run();

public partial class Program;
'@

Write-TextFile '.\src\TalentShortlist.Api\Controllers\Controllers.cs' @'
using Microsoft.AspNetCore.Mvc;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.DTOs;
using TalentShortlist.Application.Services;
using TalentShortlist.Domain.Entities;

namespace TalentShortlist.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        Status = "Healthy",
        Mode = "Demo",
        HumanReviewRequired = true,
        Timestamp = DateTime.UtcNow
    });
}

[ApiController]
[Route("api/demo")]
public sealed class DemoController(IDemoDataService demoDataService) : ControllerBase
{
    [HttpPost("seed")]
    public async Task<IActionResult> SeedAsync(CancellationToken cancellationToken)
    {
        await demoDataService.SeedAsync(cancellationToken);
        return Ok(new { Message = "Six fictional demo candidates loaded." });
    }

    [HttpGet("job-description")]
    public IActionResult GetJobDescription() => Ok(new JobDescription
    {
        Title = "Senior Microsoft 365 & Power Platform Specialist",
        Department = "IT",
        Location = "Hybrid",
        Description = "Lead secure Microsoft 365 and Power Platform solution delivery.",
        RequiredSkills = ["SharePoint Online", "Power Platform", "Power Automate", "Microsoft Graph", "Azure"],
        PreferredSkills = ["Solution Architecture", "Governance", "Security"],
        RequiredLanguages = ["English"],
        MinimumYearsOfExperience = 5
    });

    [HttpGet("ranking-profile")]
    public IActionResult GetRankingProfile() => Ok(new RankingProfile
    {
        Name = "Default ranking strategy",
        Instructions = "Evaluate professional evidence only. Human review is required.",
        Criteria =
        [
            new() { Name = "Microsoft 365 and Power Platform", Weight = 35, IsMandatory = true, Keywords = ["SharePoint Online", "Power Platform", "Power Automate"] },
            new() { Name = "Microsoft Graph and Azure", Weight = 20, IsMandatory = true, Keywords = ["Microsoft Graph", "Azure"] },
            new() { Name = "Architecture and governance", Weight = 20, Keywords = ["Solution architecture", "Governance", "Security"] },
            new() { Name = "Communication", Weight = 15, Keywords = ["English", "Stakeholder"] },
            new() { Name = "Leadership", Weight = 10, Keywords = ["Leadership", "Lead"] }
        ]
    });
}

[ApiController]
[Route("api/candidates")]
public sealed class CandidatesController(ICandidateRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken) =>
        Ok(await repository.GetAllAsync(cancellationToken));

    [HttpDelete]
    public async Task<IActionResult> ClearAsync(CancellationToken cancellationToken)
    {
        await repository.ClearAsync(cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/shortlists")]
public sealed class ShortlistsController(ShortlistService shortlistService) : ControllerBase
{
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateAsync(
        [FromBody] EvaluationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await shortlistService.EvaluateAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
    }
}
'@

Write-TextFile '.\tests\TalentShortlist.UnitTests\DeterministicCandidateEvaluatorTests.cs' @'
using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.Enums;
using TalentShortlist.Infrastructure.Demo;

namespace TalentShortlist.UnitTests;

public sealed class DeterministicCandidateEvaluatorTests
{
    [Fact]
    public async Task Evaluator_OrdersCandidatesAndFlagsMissingMandatoryCriteria()
    {
        var evaluator = new DeterministicCandidateEvaluator();

        var profile = new RankingProfile
        {
            Criteria =
            [
                new() { Name = "Platform", Weight = 70, IsMandatory = true, Keywords = ["SharePoint", "Power Platform"] },
                new() { Name = "Language", Weight = 30, Keywords = ["English"] }
            ]
        };

        var strong = new CandidateDocument
        {
            CandidateName = "Strong",
            ExtractedText = "SharePoint and Power Platform specialist. English."
        };

        var weak = new CandidateDocument
        {
            CandidateName = "Weak",
            ExtractedText = "General administration."
        };

        var results = await evaluator.EvaluateAsync(profile, [weak, strong], CancellationToken.None);

        Assert.Equal("Strong", results[0].CandidateName);
        Assert.True(results[0].TotalScore > results[1].TotalScore);
        Assert.False(results[1].MandatoryRequirementsMet);
        Assert.Equal(Recommendation.NotRecommended, results[1].Recommendation);
        Assert.All(results, result => Assert.True(result.HumanReviewRequired));
    }
}
'@

Write-TextFile '.\AITalentShortlist.code-workspace' @'
{
  "folders": [
    { "path": "." }
  ],
  "settings": {
    "dotnet.defaultSolution": "AITalentShortlist.sln"
  }
}
'@

Write-TextFile '.\README.md' @'
# AI Talent Shortlist

Demo-mode backend for candidate shortlisting.

- In-memory storage only.
- Six fictional candidates can be loaded with `POST /api/demo/seed`.
- Deterministic keyword-based evaluation.
- All recommendations require human review.
- No Azure services, real CVs, or protected-characteristic evaluation.
'@

dotnet restore .\AITalentShortlist.sln
dotnet build .\AITalentShortlist.sln --no-restore
dotnet test .\AITalentShortlist.sln --no-build --no-restore

Write-Host ''
Write-Host 'Bootstrap completed successfully.' -ForegroundColor Green
Write-Host "Open VS Code: code `"$root\AITalentShortlist.code-workspace`""
Write-Host 'Start API: dotnet run --project .\src\TalentShortlist.Api --urls http://localhost:5080'