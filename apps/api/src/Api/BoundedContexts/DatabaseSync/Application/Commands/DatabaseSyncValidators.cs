#pragma warning disable MA0048 // File name must match type name - Contains related validators
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

internal sealed class ApplyMigrationsCommandValidator : AbstractValidator<ApplyMigrationsCommand>
{
    public ApplyMigrationsCommandValidator()
    {
        RuleFor(x => x.Direction)
            .IsInEnum()
            .WithMessage("Direction must be a valid SyncDirection value");

        RuleFor(x => x.Confirmation)
            .NotEmpty()
            .WithMessage("Confirmation is required");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");
    }
}

internal sealed class SyncTableDataCommandValidator : AbstractValidator<SyncTableDataCommand>
{
    public SyncTableDataCommandValidator()
    {
        RuleFor(x => x.TableName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("TableName is required (max 200 chars)");

        RuleFor(x => x.Direction)
            .IsInEnum()
            .WithMessage("Direction must be a valid SyncDirection value");

        RuleFor(x => x.Confirmation)
            .NotEmpty()
            .WithMessage("Confirmation is required");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");
    }
}
