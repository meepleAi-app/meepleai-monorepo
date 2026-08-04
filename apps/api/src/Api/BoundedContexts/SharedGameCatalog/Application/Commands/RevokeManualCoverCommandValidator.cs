using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Boundary validation for <see cref="RevokeManualCoverCommand"/> — turns bad input into a 4xx
/// client error (422 via the FluentValidation pipeline) instead of a 500. The GameId comes from
/// the route and the AdminId from the session, so both are just identity guards; the idempotent
/// "nothing to revoke" case is handled in the handler as a 204, not here.
/// </summary>
internal sealed class RevokeManualCoverCommandValidator : AbstractValidator<RevokeManualCoverCommand>
{
    public RevokeManualCoverCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");
    }
}
