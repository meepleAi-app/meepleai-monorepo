using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Handles UpdateSetupChecklistCommand.
/// Replaces the entire setup checklist on the session (replace-whole semantics).
/// </summary>
internal class UpdateSetupChecklistCommandHandler : ICommandHandler<UpdateSetupChecklistCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public UpdateSetupChecklistCommandHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task Handle(UpdateSetupChecklistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository
            .GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.UpdateSetupChecklist(command.Checklist);
    }
}
