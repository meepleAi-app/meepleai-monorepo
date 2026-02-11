using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to delete a ledger entry - Level 2 (Issue #3722: Manual Ledger CRUD)
/// Only manual entries can be deleted. Auto entries are protected.
/// </summary>
internal sealed record DeleteLedgerEntryCommand(Guid Id) : ICommand;

internal sealed class DeleteLedgerEntryCommandValidator : AbstractValidator<DeleteLedgerEntryCommand>
{
    public DeleteLedgerEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Ledger entry ID is required");
    }
}
