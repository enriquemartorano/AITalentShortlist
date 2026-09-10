using System.Diagnostics;
using TalentShortlist.Application.Contracts;
using TalentShortlist.Application.DTOs;
using TalentShortlist.Domain.ValueObjects;

namespace TalentShortlist.Application.Services;

public sealed class ShortlistService(
    ICandidateRepository candidateRepository,
    ICandidateEvaluator candidateEvaluator,
    IAiCandidateEvaluator aiCandidateEvaluator)
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
        IReadOnlyList<CandidateAssessment> ranking;
        var evaluatorName = "DeterministicCandidateEvaluator";

        if (request.EvaluationMode == EvaluationMode.Ai)
        {
            ranking = await aiCandidateEvaluator.EvaluateAsync(
                request.RankingProfile,
                selectedCandidates,
                cancellationToken);
            evaluatorName = "OpenAiCandidateEvaluator";
        }
        else
        {
            ranking = await candidateEvaluator.EvaluateAsync(
                request.RankingProfile,
                selectedCandidates,
                cancellationToken);
        }

        stopwatch.Stop();

        return new EvaluationResponseDto
        {
            Ranking = ranking,
            Evaluator = evaluatorName,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
