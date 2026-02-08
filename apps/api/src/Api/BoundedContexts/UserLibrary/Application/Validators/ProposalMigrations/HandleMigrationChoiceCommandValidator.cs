using Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.ProposalMigrations;

/// <summary>
/// Validator for HandleMigrationChoiceCommand.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal class HandleMigrationChoiceCommandValidator : AbstractValidator<HandleMigrationChoiceCommand>
{
    public HandleMigrationChoiceCommandValidator()
    {
        RuleFor(x => x.MigrationId)
            .NotEmpty()
            .WithMessage("MigrationId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Choice)
            .Must(choice => choice == PostApprovalMigrationChoice.LinkToCatalog || choice == PostApprovalMigrationChoice.KeepPrivate)
            .WithMessage("Choice must be either LinkToCatalog or KeepPrivate (Pending is not a valid user choice)");
    }
}
