using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record ListSessionCheckpointsQuery(
    Guid SessionId, Guid RequesterId
) : IRequest<ListSessionCheckpointsResult>;

public record ListSessionCheckpointsResult(IReadOnlyList<SessionCheckpointDto> Checkpoints);

public record SessionCheckpointDto(
    Guid Id, string Name, DateTime CreatedAt, Guid CreatedBy, int DiaryEventCount);

public class ListSessionCheckpointsQueryValidator : AbstractValidator<ListSessionCheckpointsQuery>
{
    public ListSessionCheckpointsQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.RequesterId).NotEmpty().WithMessage("RequesterId is required");
    }
}
