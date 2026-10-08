using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Maps the domain <see cref="MechanicClaim"/> to <see cref="MechanicClaimDto"/>, shared by the
/// review command handlers so the wire shape stays identical across approve/reject/bulk/structure.
/// </summary>
internal static class MechanicClaimDtoMapper
{
    internal static MechanicClaimDto FromDomain(MechanicClaim claim, Guid analysisId) =>
        new(
            Id: claim.Id,
            AnalysisId: analysisId,
            Section: claim.Section,
            Text: claim.Text,
            DisplayOrder: claim.DisplayOrder,
            Status: claim.Status,
            ReviewedBy: claim.ReviewedBy,
            ReviewedAt: claim.ReviewedAt,
            RejectionNote: claim.RejectionNote,
            ReviewNote: claim.ReviewNote,
            Citations: claim.Citations
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new MechanicCitationDto(
                    Id: c.Id,
                    PdfPage: c.PdfPage,
                    Quote: c.Quote,
                    DisplayOrder: c.DisplayOrder))
                .ToList(),
            Validations: MechanicClaimValidations.FromDomain(claim),
            Kind: claim.Kind,
            Priority: claim.Priority,
            Overrides: claim.Overrides,
            Trigger: MechanicTriggerDto.FromDomain(claim.Trigger));
}
