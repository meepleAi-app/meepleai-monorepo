using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to delete a saved resource forecast scenario.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed record DeleteResourceForecastCommand(Guid Id) : ICommand;

internal sealed class DeleteResourceForecastCommandValidator : AbstractValidator<DeleteResourceForecastCommand>
{
    public DeleteResourceForecastCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Forecast ID is required");
    }
}
