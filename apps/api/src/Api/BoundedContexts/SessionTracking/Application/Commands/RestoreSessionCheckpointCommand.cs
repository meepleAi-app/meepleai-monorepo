using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record RestoreSessionCheckpointCommand(
    Guid SessionId, Guid RequesterId, Guid CheckpointId
) : IRequest<RestoreSessionCheckpointResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

public record RestoreSessionCheckpointResult(
    Guid CheckpointId, string Name, DateTime RestoredAt, int WidgetsRestored);

public class RestoreSessionCheckpointCommandValidator : AbstractValidator<RestoreSessionCheckpointCommand>
{
    public RestoreSessionCheckpointCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.RequesterId).NotEmpty().WithMessage("RequesterId is required");
        RuleFor(x => x.CheckpointId).NotEmpty().WithMessage("CheckpointId is required");
    }
}
