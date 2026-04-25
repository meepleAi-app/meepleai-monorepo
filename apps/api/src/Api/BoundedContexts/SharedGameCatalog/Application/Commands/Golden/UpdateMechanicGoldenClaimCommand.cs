using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Command to update an existing golden claim for the Mechanic Extractor validation pipeline
/// (ADR-051 / Sprint 1 / Task 20). The claim's <c>Section</c> is immutable and cannot be changed;
/// if a curator needs to reclassify a claim they must deactivate the existing one and create a new
/// one under the correct section.
/// </summary>
/// <param name="ClaimId">Id of the golden claim to update.</param>
/// <param name="Statement">The curator-authored claim statement (1..500 chars).</param>
/// <param name="ExpectedPage">The rulebook page the claim references (>= 1).</param>
/// <param name="SourceQuote">The verbatim source quote from the rulebook (1..1000 chars).</param>
internal sealed record UpdateMechanicGoldenClaimCommand(
    Guid ClaimId,
    string Statement,
    int ExpectedPage,
    string SourceQuote
) : ICommand<Unit>;
