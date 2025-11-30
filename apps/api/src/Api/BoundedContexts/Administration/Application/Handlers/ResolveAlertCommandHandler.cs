using Api.BoundedContexts.Administration.Application.Commands;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for ResolveAlertCommand.
/// Delegates to AlertingService (service will be refactored in future iterations).
/// </summary>
public class ResolveAlertCommandHandler : ICommandHandler<ResolveAlertCommand, bool>
{
    private readonly IAlertingService _alertingService;

    public ResolveAlertCommandHandler(IAlertingService alertingService)
    {
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
    }

    public async Task<bool> Handle(ResolveAlertCommand command, CancellationToken cancellationToken)
    {
        return await _alertingService.ResolveAlertAsync(command.AlertType, cancellationToken).ConfigureAwait(false);
    }
}
