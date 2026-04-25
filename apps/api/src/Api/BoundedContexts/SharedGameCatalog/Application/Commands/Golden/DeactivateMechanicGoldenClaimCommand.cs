using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Command to deactivate (soft-delete) an existing golden claim for the Mechanic Extractor validation
/// pipeline (ADR-051 / Sprint 1 / Task 21). Deactivation is a terminal state: once a claim has been
/// deactivated it cannot be re-activated or further updated; curators must instead create a new
/// claim. Emits no new identifier — the handler returns <see cref="Unit"/>.
/// </summary>
/// <param name="ClaimId">Id of the golden claim to deactivate.</param>
internal sealed record DeactivateMechanicGoldenClaimCommand(
    Guid ClaimId
) : ICommand<Unit>;
