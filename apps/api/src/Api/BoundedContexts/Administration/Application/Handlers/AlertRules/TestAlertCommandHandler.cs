using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class TestAlertCommandHandler : IRequestHandler<TestAlertCommand, bool>
{
    private readonly IAlertingService _alertingService;
    private readonly ILogger<TestAlertCommandHandler> _logger;

    public TestAlertCommandHandler(
        IAlertingService alertingService,
        ILogger<TestAlertCommandHandler> logger)
    {
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(TestAlertCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Test alert sent successfully: Type={AlertType}, Channel={Channel}",
                request.AlertType,
                request.Channel);

            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - Test/diagnostic operation failure handling
        // Catches all exceptions during test alert send (network, config, service failures)
        // to return false instead of throwing. Logs error for diagnostics. Test operations
        // should not throw exceptions - returning false indicates test failure.
#pragma warning restore S125
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
#pragma warning restore CA1031
    }
}

