using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Sets Kind/Priority/Overrides/Trigger on a single claim of an analysis in review.
/// </summary>
/// <param name="AnalysisId">Parent aggregate id.</param>
/// <param name="ClaimId">Claim id to edit.</param>
/// <param name="ReviewerId">Admin user id from the validated session (never from the body).</param>
/// <param name="Structure">The new structure.</param>
internal record UpdateMechanicClaimStructureCommand(
    Guid AnalysisId,
    Guid ClaimId,
    Guid ReviewerId,
    MechanicClaimStructureDto Structure) : ICommand<MechanicClaimDto>;
