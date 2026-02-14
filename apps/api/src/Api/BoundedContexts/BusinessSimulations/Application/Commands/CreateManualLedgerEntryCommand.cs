using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to create a manual ledger entry (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed record CreateManualLedgerEntryCommand(
    DateTime Date,
    LedgerEntryType Type,
    LedgerCategory Category,
    decimal Amount,
    string Currency,
    string? Description,
    Guid CreatedByUserId) : ICommand<Guid>;

internal sealed class CreateManualLedgerEntryCommandValidator : AbstractValidator<CreateManualLedgerEntryCommand>
{
    public CreateManualLedgerEntryCommandValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
            .WithMessage("Date cannot be in the future");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid LedgerEntryType");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Category must be a valid LedgerCategory");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code (e.g., EUR, USD)");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required for manual entries");
    }
}
