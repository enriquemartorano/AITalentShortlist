using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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

public sealed class OpenAiCandidateEvaluator(
    ICandidateEvaluator fallbackEvaluator,
    ILogger<OpenAiCandidateEvaluator> logger,
    OpenAiSettings settings) : IAiCandidateEvaluator
{
    public async Task<IReadOnlyList<CandidateAssessment>> EvaluateAsync(
        RankingProfile rankingProfile,
        IReadOnlyCollection<CandidateDocument> candidates,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
        {
            logger.LogWarning(
                "AI evaluation fallback: OpenAI configuration is incomplete (key present: {KeyPresent}, model: {Model})",
                !string.IsNullOrWhiteSpace(settings.ApiKey),
                settings.Model);
            return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
        }

        try
        {
            logger.LogInformation(
                "Starting OpenAI evaluation with model {Model} for {CandidateCount} candidates",
                settings.Model,
                candidates.Count);
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var prompt = BuildPrompt(rankingProfile, candidates);
            var payload = new
            {
                model = settings.Model,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert hiring analyst. Return only valid JSON, no Markdown, no commentary."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            using var response = await httpClient.PostAsJsonAsync(settings.Endpoint, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "AI evaluation fallback: OpenAI returned HTTP {StatusCode}",
                    (int)response.StatusCode);
                return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(cancellationToken: cancellationToken);
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("AI evaluation fallback: OpenAI returned an empty message");
                return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
            }

            var parsed = JsonDocument.Parse(content);
            if (!parsed.RootElement.TryGetProperty("candidates", out var candidatesElement) || candidatesElement.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("AI evaluation fallback: OpenAI response did not contain a candidates array");
                return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
            }

            var mapped = new List<CandidateAssessment>();
            foreach (var candidateElement in candidatesElement.EnumerateArray())
            {
                var candidateName = candidateElement.TryGetProperty("candidateName", out var candidateNameElement)
                    ? candidateNameElement.GetString() ?? string.Empty
                    : string.Empty;
                var totalScore = candidateElement.TryGetProperty("totalScore", out var totalScoreElement) && totalScoreElement.TryGetDecimal(out var scoreValue)
                    ? scoreValue
                    : 0m;
                var recommendationValue = candidateElement.TryGetProperty("recommendation", out var recommendationElement)
                    ? recommendationElement.GetString() ?? "ReviewRequired"
                    : "ReviewRequired";
                var executiveSummary = candidateElement.TryGetProperty("executiveSummary", out var summaryElement)
                    ? summaryElement.GetString() ?? string.Empty
                    : string.Empty;
                var humanReviewRequired = candidateElement.TryGetProperty("humanReviewRequired", out var humanReviewElement)
                    ? humanReviewElement.GetBoolean()
                    : true;
                var strengths = candidateElement.TryGetProperty("strengths", out var strengthsElement) && strengthsElement.ValueKind == JsonValueKind.Array
                    ? strengthsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                    : new List<string>();
                var gaps = candidateElement.TryGetProperty("gaps", out var gapsElement) && gapsElement.ValueKind == JsonValueKind.Array
                    ? gapsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                    : new List<string>();

                mapped.Add(new CandidateAssessment
                {
                    CandidateId = candidates.FirstOrDefault(candidate => candidate.CandidateName == candidateName)?.Id ?? Guid.Empty,
                    CandidateName = candidateName,
                    TotalScore = totalScore,
                    Recommendation = MapRecommendation(recommendationValue),
                    ExecutiveSummary = executiveSummary,
                    MandatoryRequirementsMet = !gaps.Any(item => item.Contains("Mandatory", StringComparison.OrdinalIgnoreCase)),
                    HumanReviewRequired = humanReviewRequired,
                    Strengths = strengths,
                    Gaps = gaps,
                    CriterionScores = [],
                    Evidence = []
                });
            }

            if (mapped.Count == 0)
            {
                logger.LogWarning("AI evaluation fallback: OpenAI response contained no usable candidates");
                return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
            }

            logger.LogInformation("OpenAI evaluation completed with {ResultCount} results", mapped.Count);
            return mapped
                .OrderByDescending(candidate => candidate.TotalScore)
                .ThenBy(candidate => candidate.CandidateName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AI evaluation failed; using heuristic fallback");
            return await fallbackEvaluator.EvaluateAsync(rankingProfile, candidates, cancellationToken);
        }
    }

    private static string BuildPrompt(RankingProfile rankingProfile, IReadOnlyCollection<CandidateDocument> candidates)
    {
        var criteria = string.Join("\n", rankingProfile.Criteria.Select(criteria =>
            $"- {criteria.Name} ({criteria.Weight}%): keywords={string.Join(", ", criteria.Keywords)} mandatory={criteria.IsMandatory}"));

        var candidatesText = string.Join("\n---\n", candidates.Select(candidate =>
            $"Candidate: {candidate.CandidateName}\nFile: {candidate.FileName}\nText: {candidate.ExtractedText}"));

        return $$"""
You are evaluating job applicants for a shortlist. Use the following job ranking profile:
{{criteria}}

Return a JSON object with a top-level property named "candidates". Each candidate must include:
- candidateName: string
- totalScore: number between 0 and 100
- recommendation: one of "StrongMatch", "PotentialMatch", "ReviewRequired", "NotRecommended"
- executiveSummary: string
- humanReviewRequired: boolean
- strengths: [string]
- gaps: [string]

Evaluate the candidates using the supplied evidence only.

Candidates:
{{candidatesText}}
""";
    }

    private static Recommendation MapRecommendation(string value) => value switch
    {
        "StrongMatch" => Recommendation.StrongMatch,
        "PotentialMatch" => Recommendation.PotentialMatch,
        "ReviewRequired" => Recommendation.ReviewRequired,
        "NotRecommended" => Recommendation.NotRecommended,
        _ => Recommendation.ReviewRequired
    };

    private sealed class OpenAiChatCompletionResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
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
