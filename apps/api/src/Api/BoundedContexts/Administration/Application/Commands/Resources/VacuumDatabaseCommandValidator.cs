using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Validator for VacuumDatabaseCommand.
/// Ensures confirmation is explicitly provided for this operation.
/// Issue #3695: Resources Monitoring - VACUUM validation
/// </summary>
internal class VacuumDatabaseCommandValidator : AbstractValidator<VacuumDatabaseCommand>
{
    public VacuumDatabaseCommandValidator()
    {
        RuleFor(x => x.Confirmed)
            .Equal(true)
            .WithMessage("Confirmation is required to execute VACUUM. This operation will briefly lock tables.");
    }
}
