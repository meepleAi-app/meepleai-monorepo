using Api.BoundedContexts.UserLibrary.Application.Queries.ProposalMigrations;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.ProposalMigrations;

/// <summary>
/// Validator for GetPendingMigrationsQuery.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal class GetPendingMigrationsQueryValidator : AbstractValidator<GetPendingMigrationsQuery>
{
    public GetPendingMigrationsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
