using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record CreateSessionCheckpointCommand(
    Guid SessionId, Guid RequesterId, string Name
) : IRequest<CreateSessionCheckpointResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

public record CreateSessionCheckpointResult(
    Guid CheckpointId, string Name, DateTime CreatedAt, int DiaryEventCount);

public class CreateSessionCheckpointCommandValidator : AbstractValidator<CreateSessionCheckpointCommand>
{
    public CreateSessionCheckpointCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.RequesterId).NotEmpty().WithMessage("RequesterId is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Checkpoint name is required")
            .MaximumLength(200).WithMessage("Checkpoint name must be 200 characters or fewer");
    }
}
