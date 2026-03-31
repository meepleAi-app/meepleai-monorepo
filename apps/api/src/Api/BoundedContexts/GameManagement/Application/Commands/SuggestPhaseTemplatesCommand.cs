using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record SuggestPhaseTemplatesCommand(Guid GameId)
    : IQuery<IReadOnlyList<PhaseTemplateSuggestionDto>>;

public sealed record PhaseTemplateSuggestionDto(
    string PhaseName,
    int PhaseOrder,
    string Description,
    string Rationale);
