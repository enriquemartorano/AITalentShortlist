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
        var effectiveEvaluator = "DeterministicCandidateEvaluator";
        var fallbackUsed = false;
        string? fallbackReason = null;

        if (request.EvaluationMode == EvaluationMode.Ai)
        {
            try
            {
                ranking = await aiCandidateEvaluator.EvaluateAsync(
                    request.JobDescription,
                    request.RankingProfile,
                    selectedCandidates,
                    cancellationToken);
                effectiveEvaluator = "OpenAI";
            }
            catch (OpenAiEvaluationException exception)
            {
                fallbackUsed = true;
                fallbackReason = exception.Message;
                ranking = await candidateEvaluator.EvaluateAsync(
                    request.RankingProfile,
                    selectedCandidates,
                    cancellationToken);
            }
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
            RequestedMode = request.EvaluationMode,
            EffectiveEvaluator = effectiveEvaluator,
            FallbackUsed = fallbackUsed,
            FallbackReason = fallbackReason,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
