using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to update a ledger entry - Level 1 edit (Issue #3722: Manual Ledger CRUD)
/// Allows editing description, category, and metadata.
/// </summary>
internal sealed record UpdateLedgerEntryCommand(
    Guid Id,
    string? Description,
    LedgerCategory? Category,
    string? Metadata) : ICommand;

internal sealed class UpdateLedgerEntryCommandValidator : AbstractValidator<UpdateLedgerEntryCommand>
{
    public UpdateLedgerEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Ledger entry ID is required");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.Category)
            .IsInEnum()
            .When(x => x.Category.HasValue)
            .WithMessage("Category must be a valid LedgerCategory");

        RuleFor(x => x.Metadata)
            .MaximumLength(4000)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata cannot exceed 4000 characters");
    }
}
