using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

public class TestAlertCommandHandler : IRequestHandler<TestAlertCommand, bool>
{
    private readonly IAlertingService _alertingService;
    private readonly ILogger<TestAlertCommandHandler> _logger;

    public TestAlertCommandHandler(
        IAlertingService alertingService,
        ILogger<TestAlertCommandHandler> logger)
    {
        _alertingService = alertingService;
        _logger = logger;
    }

    public async Task<bool> Handle(TestAlertCommand request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Testing alert: Type={AlertType}, Channel={Channel}",
                request.AlertType,
                request.Channel);

            await _alertingService.SendAlertAsync(
                request.AlertType,
                "Critical",
                $"Test alert for {request.AlertType} via {request.Channel}",
                new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["test"] = true,
                    ["channel"] = request.Channel
                },
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Test alert sent successfully: Type={AlertType}, Channel={Channel}",
                request.AlertType,
                request.Channel);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send test alert: Type={AlertType}, Channel={Channel}, Error={ErrorMessage}",
                request.AlertType,
                request.Channel,
                ex.Message);
            return false;
        }
    }
}
