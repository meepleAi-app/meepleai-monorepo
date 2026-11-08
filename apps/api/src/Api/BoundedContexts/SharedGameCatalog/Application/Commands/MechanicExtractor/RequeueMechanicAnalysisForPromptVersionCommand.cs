using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Re-runs the mechanic extraction for one game with the current prompt version, reusing the
/// PDF the active card was extracted from. Delegates to <see cref="GenerateMechanicAnalysisCommand"/>.
/// </summary>
/// <param name="SharedGameId">Game whose active card is to be re-extracted.</param>
/// <param name="ActorId">Admin user id from the validated session.</param>
/// <param name="CostCapUsd">Cost cap forwarded to the generation pipeline (0 &lt; cap ≤ 10).</param>
internal record RequeueMechanicAnalysisForPromptVersionCommand(
    Guid SharedGameId,
    Guid ActorId,
    decimal CostCapUsd = 2.00m) : ICommand<MechanicAnalysisGenerationResponseDto>;
