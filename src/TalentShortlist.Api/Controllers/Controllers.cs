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
public sealed class ShortlistsController(
    ShortlistService shortlistService,
    ILogger<ShortlistsController> logger) : ControllerBase
{
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateAsync(
        [FromBody] EvaluationRequestDto request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Evaluation requested with mode {EvaluationMode} for {CandidateCount} candidate IDs",
            request.EvaluationMode,
            request.CandidateIds.Count);

        try
        {
            var response = await shortlistService.EvaluateAsync(request, cancellationToken);
            logger.LogInformation(
                "Evaluation completed with evaluator {Evaluator}, {ResultCount} results in {DurationMilliseconds} ms",
                response.Evaluator,
                response.Ranking.Count,
                response.DurationMilliseconds);
            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Evaluation rejected: {Message}", exception.Message);
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Evaluation failed unexpectedly");
            throw;
        }
    }
}
