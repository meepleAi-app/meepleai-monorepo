using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to auto-save a session checkpoint.
/// Triggered by the AutoSaveSessionJob every 60 seconds for active sessions.
/// </summary>
internal record AutoSaveSessionCommand(Guid SessionId) : IRequest;

/// <summary>
/// Handler for AutoSaveSessionCommand. Creates a lightweight checkpoint
/// for the specified session if it is still active.
/// Silently skips if the session has ended or is not found.
/// </summary>
internal sealed class AutoSaveSessionCommandHandler(
    ISessionCheckpointRepository checkpointRepo,
    ISessionRepository sessionRepo,
    IUnitOfWork unitOfWork
) : IRequestHandler<AutoSaveSessionCommand>
{
    public async Task Handle(AutoSaveSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await sessionRepo.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.Status != SessionStatus.Active)
            return; // Session ended or not found — skip silently

        var checkpoint = SessionCheckpoint.Create(
            command.SessionId,
            $"Auto-save {DateTime.UtcNow:HH:mm}",
            session.UserId,
            "{}",
            0);

        await checkpointRepo.AddAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
