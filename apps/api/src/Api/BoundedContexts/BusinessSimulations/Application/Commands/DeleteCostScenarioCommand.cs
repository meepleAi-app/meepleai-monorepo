using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to delete a saved cost scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed record DeleteCostScenarioCommand(Guid Id) : ICommand;

internal sealed class DeleteCostScenarioCommandValidator : AbstractValidator<DeleteCostScenarioCommand>
{
    public DeleteCostScenarioCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Cost scenario ID is required");
    }
}
