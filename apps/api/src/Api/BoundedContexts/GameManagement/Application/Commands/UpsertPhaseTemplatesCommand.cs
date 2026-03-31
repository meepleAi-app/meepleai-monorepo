using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record UpsertPhaseTemplatesCommand(
    Guid GameId,
    string EditorUserId,
    IReadOnlyList<PhaseTemplateInput> Templates
) : ICommand<IReadOnlyList<PhaseTemplateDto>>;

public sealed record PhaseTemplateInput(
    string PhaseName,
    int PhaseOrder,
    string? Description = null
);

internal sealed class UpsertPhaseTemplatesCommandValidator : AbstractValidator<UpsertPhaseTemplatesCommand>
{
    public UpsertPhaseTemplatesCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty().WithMessage("GameId is required");
        RuleFor(x => x.EditorUserId).NotEmpty().WithMessage("EditorUserId is required");
        RuleFor(x => x.Templates).NotNull().WithMessage("Templates list is required");
        RuleForEach(x => x.Templates).ChildRules(t =>
        {
            t.RuleFor(p => p.PhaseName).NotEmpty().MaximumLength(50);
            t.RuleFor(p => p.PhaseOrder).GreaterThan(0);
            t.RuleFor(p => p.Description).MaximumLength(200).When(p => p.Description != null);
        });
    }
}
