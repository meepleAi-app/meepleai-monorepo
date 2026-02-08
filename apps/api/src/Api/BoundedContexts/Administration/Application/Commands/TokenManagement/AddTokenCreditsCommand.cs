using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.TokenManagement;

/// <summary>
/// Command to add credits to token balance (Issue #3692)
/// </summary>
public sealed record AddTokenCreditsCommand(
    decimal Amount,
    string Currency,
    string? Note) : ICommand;

public sealed class AddTokenCreditsCommandValidator : AbstractValidator<AddTokenCreditsCommand>
{
    public AddTokenCreditsCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be positive");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("Currency must be 3-letter code (e.g., EUR, USD)");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note != null)
            .WithMessage("Note cannot exceed 500 characters");
    }
}
