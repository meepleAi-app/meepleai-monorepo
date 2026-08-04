using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Boundary validation for <see cref="RevokeManualCoverCommand"/> — 400 (not 500) on bad input.
/// The GameId comes from the route and the AdminId from the session, so both are just identity
/// guards; the idempotent "nothing to revoke" case is handled in the handler as a 204, not here.
/// </summary>
internal sealed class RevokeManualCoverCommandValidator : AbstractValidator<RevokeManualCoverCommand>
{
    public RevokeManualCoverCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");
    }
}
