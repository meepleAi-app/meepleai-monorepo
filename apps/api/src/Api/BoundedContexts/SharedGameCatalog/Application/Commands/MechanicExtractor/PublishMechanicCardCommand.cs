using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Publishes an approved (<c>Published</c>-lifecycle) <c>MechanicAnalysis</c> into a user-facing
/// <c>MechanicCard</c> (ADR-051 / #527). Publishing is explicit and distinct from approving the
/// analysis lifecycle (AD-2).
/// </summary>
/// <param name="AnalysisId">Analysis to publish. Must be Published and not already published.</param>
/// <param name="Title">Optional admin title (5..200 chars). When null, a default is derived from the game name.</param>
/// <param name="PreviousCardId">
/// Optional id of a suppressed card being superseded (republish / AC-5). When set it MUST reference a
/// suppressed card for this game; the new card gets <c>version = previous.version + 1</c>.
/// </param>
/// <param name="PublisherId">Admin user id, sourced from the validated session (never from the body).</param>
internal record PublishMechanicCardCommand(
    Guid AnalysisId,
    string? Title,
    Guid? PreviousCardId,
    Guid PublisherId) : ICommand<PublishMechanicCardResponseDto>;
