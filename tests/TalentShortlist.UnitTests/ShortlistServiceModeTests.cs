using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.DTOs;
using TalentShortlist.Application.Services;
using TalentShortlist.Domain.Entities;
using TalentShortlist.Domain.Enums;
using TalentShortlist.Domain.ValueObjects;
using TalentShortlist.Infrastructure.Demo;
using TalentShortlist.Infrastructure.Repositories;

namespace TalentShortlist.UnitTests;

public sealed class ShortlistServiceModeTests
{
    [Fact]
    public async Task EvaluateAsync_UsesHeuristicMode_WhenSelected()
    {
        var repo = new InMemoryCandidateRepository();
        var candidate = new CandidateDocument
        {
            CandidateName = "Alpha",
            ExtractedText = "SharePoint Online, Power Platform, Power Automate, Azure and English."
        };
        await repo.AddAsync(candidate, CancellationToken.None);

        var service = new ShortlistService(repo, new DeterministicCandidateEvaluator(), new FakeAiCandidateEvaluator());
        var request = new EvaluationRequestDto
        {
            EvaluationMode = EvaluationMode.Heuristic,
            RankingProfile = new RankingProfile
            {
                Criteria =
                [
                    new() { Name = "Platform", Weight = 70, IsMandatory = true, Keywords = ["SharePoint Online", "Power Platform", "Power Automate"] },
                    new() { Name = "Cloud", Weight = 30, Keywords = ["Azure"] }
                ]
            },
            CandidateIds = [candidate.Id]
        };

        var result = await service.EvaluateAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("DeterministicCandidateEvaluator", result.Evaluator);
    }

    [Fact]
    public async Task EvaluateAsync_UsesAiMode_WhenSelected()
    {
        var repo = new InMemoryCandidateRepository();
        var candidate = new CandidateDocument
        {
            CandidateName = "Beta",
            ExtractedText = "SharePoint Online and Azure experience."
        };
        await repo.AddAsync(candidate, CancellationToken.None);

        var service = new ShortlistService(repo, new DeterministicCandidateEvaluator(), new FakeAiCandidateEvaluator());
        var request = new EvaluationRequestDto
        {
            EvaluationMode = EvaluationMode.Ai,
            RankingProfile = new RankingProfile
            {
                Criteria =
                [
                    new() { Name = "Platform", Weight = 70, IsMandatory = true, Keywords = ["SharePoint Online", "Power Platform", "Power Automate"] },
                    new() { Name = "Cloud", Weight = 30, Keywords = ["Azure"] }
                ]
            },
            CandidateIds = [candidate.Id]
        };

        var result = await service.EvaluateAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("OpenAiCandidateEvaluator", result.Evaluator);
    }

    private sealed class FakeAiCandidateEvaluator : IAiCandidateEvaluator
    {
        public Task<IReadOnlyList<CandidateAssessment>> EvaluateAsync(
            RankingProfile rankingProfile,
            IReadOnlyCollection<CandidateDocument> candidates,
            CancellationToken cancellationToken)
        {
            var result = candidates
                .Select(candidate => new CandidateAssessment
                {
                    CandidateId = candidate.Id,
                    CandidateName = candidate.CandidateName,
                    TotalScore = 95m,
                    Recommendation = Recommendation.StrongMatch,
                    ExecutiveSummary = "AI result",
                    MandatoryRequirementsMet = true,
                    HumanReviewRequired = true,
                    Strengths = ["AI"],
                    Gaps = [],
                    CriterionScores = [],
                    Evidence = []
                })
                .ToArray();

            return Task.FromResult<IReadOnlyList<CandidateAssessment>>(result);
        }
    }
}
