using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries.AdminEvents;

/// <summary>
/// Validates <see cref="GetAdminEventsQuery"/> before it reaches the handler.
/// Validation failures surface as HTTP 422 via <c>ApiExceptionHandlerMiddleware</c>.
///
/// F4.1 issue #1718 — Task 1.1.
/// </summary>
internal sealed class GetAdminEventsQueryValidator : AbstractValidator<GetAdminEventsQuery>
{
    public GetAdminEventsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 1000)
            .WithMessage("Limit must be between 1 and 1000.");

        // Since: optional cursor — no rule needed (null means no cursor).
        // EventTypes / AggregateTypes: optional allow-lists, null and empty are equivalent — no rule.
        // UserId / AggregateId: optional equality filters — no rule.
        //
        // Note: Math.Clamp in the handler is retained as defense-in-depth per codebase policy.
    }
}
