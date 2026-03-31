using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal record GetPhaseTemplatesQuery(Guid GameId) : IQuery<IReadOnlyList<PhaseTemplateDto>>;

public sealed record PhaseTemplateDto(
    Guid Id,
    string PhaseName,
    int PhaseOrder,
    string? Description
);
