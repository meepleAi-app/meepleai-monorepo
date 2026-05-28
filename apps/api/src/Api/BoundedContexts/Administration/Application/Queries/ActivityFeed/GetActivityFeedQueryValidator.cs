using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries.ActivityFeed;

/// <summary>
/// Validates <see cref="GetActivityFeedQuery"/> before it reaches the handler.
/// Validation failures surface as HTTP 422 via <c>ApiExceptionHandlerMiddleware</c>.
/// </summary>
internal sealed class GetActivityFeedQueryValidator : AbstractValidator<GetActivityFeedQuery>
{
    public GetActivityFeedQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId must not be empty.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("limit must be between 1 and 100.");
    }
}
