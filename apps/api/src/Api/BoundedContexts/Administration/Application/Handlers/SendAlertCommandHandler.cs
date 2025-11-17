using Api.BoundedContexts.Administration.Application.Commands;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SendAlertCommand.
/// Delegates to AlertingService (service will be refactored in future iterations).
/// </summary>
public class SendAlertCommandHandler : ICommandHandler<SendAlertCommand, AlertDto>
{
    private readonly IAlertingService _alertingService;

    public SendAlertCommandHandler(IAlertingService alertingService)
    {
        _alertingService = alertingService;
    }

    public async Task<AlertDto> Handle(SendAlertCommand command, CancellationToken cancellationToken)
    {
        return await _alertingService.SendAlertAsync(
            command.AlertType,
            command.Severity,
            command.Message,
            command.Metadata,
            cancellationToken
        );
    }
}
