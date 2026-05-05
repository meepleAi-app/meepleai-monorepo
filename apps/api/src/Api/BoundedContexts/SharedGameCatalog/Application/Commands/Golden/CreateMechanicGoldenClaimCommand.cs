using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Command to create a new golden claim for the Mechanic Extractor validation pipeline
/// (ADR-051 / Sprint 1 / Task 19).
/// </summary>
/// <param name="SharedGameId">The shared game the golden claim belongs to.</param>
/// <param name="Section">Logical rulebook section the claim describes.</param>
/// <param name="Statement">The curator-authored claim statement (1..500 chars).</param>
/// <param name="ExpectedPage">The rulebook page the claim references (>= 1).</param>
/// <param name="SourceQuote">The verbatim source quote from the rulebook (1..1000 chars).</param>
/// <param name="CuratorUserId">The user ID of the admin curator creating the claim.</param>
internal sealed record CreateMechanicGoldenClaimCommand(
    Guid SharedGameId,
    MechanicSection Section,
    string Statement,
    int ExpectedPage,
    string SourceQuote,
    Guid CuratorUserId
) : ICommand<Guid>;
