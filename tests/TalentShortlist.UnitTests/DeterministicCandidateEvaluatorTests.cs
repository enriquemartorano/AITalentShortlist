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
