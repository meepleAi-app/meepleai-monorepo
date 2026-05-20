using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Validator for <see cref="EnqueueStorageMigrationCommand"/>.
/// </summary>
internal sealed class EnqueueStorageMigrationCommandValidator : AbstractValidator<EnqueueStorageMigrationCommand>
{
    public EnqueueStorageMigrationCommandValidator()
    {
        RuleFor(c => c.MigrationId)
            .NotEqual(Guid.Empty)
            .WithMessage("MigrationId must be a non-empty GUID.");

        RuleFor(c => c.LegacyPrefix)
            .NotEmpty().WithMessage("LegacyPrefix is required.")
            .Must(p => p.EndsWith('/'))
            .WithMessage("LegacyPrefix must end with '/' (S3 list-objects convention).")
            .Matches(@"^[A-Za-z0-9_\-/]+$")
            .WithMessage("LegacyPrefix may only contain alphanumerics, '-', '_', and '/'.");

        RuleFor(c => c.Category)
            .IsInEnum().WithMessage("Category must be a defined BlobCategory value.");
    }
}
