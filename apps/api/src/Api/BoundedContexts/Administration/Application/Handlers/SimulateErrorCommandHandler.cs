using Api.BoundedContexts.Administration.Application.Commands;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SimulateErrorCommand.
/// Generates test errors for runbook validation and Prometheus alert testing.
///
/// Security:
/// - Admin-only access enforced at endpoint level
/// - Disabled in production via TestEndpoints:Enabled config
/// - Rate limiting applied (Admin: 1000 tokens, 10/sec refill)
///
/// Issue #2004: Enables testing of high-error-rate.md and error-spike.md runbooks.
/// </summary>
public class SimulateErrorCommandHandler : IRequestHandler<SimulateErrorCommand, Unit>
{
    private readonly ILogger<SimulateErrorCommandHandler> _logger;
    private readonly IConfiguration _configuration;

    public SimulateErrorCommandHandler(
        ILogger<SimulateErrorCommandHandler> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<Unit> Handle(SimulateErrorCommand request, CancellationToken cancellationToken)
    {
        // Security: Verify test endpoints are enabled
        var enabled = _configuration.GetValue<bool>("TestEndpoints:Enabled", false);
        if (!enabled)
        {
            _logger.LogWarning("Test endpoints are disabled. Set TestEndpoints:Enabled=true in appsettings");
            throw new InvalidOperationException("Test endpoints are disabled in this environment");
        }

        // Log for Prometheus metrics collection
        _logger.LogWarning("Simulating error type: {ErrorType}", request.ErrorType);

        // Generate error based on type
        return request.ErrorType.ToLowerInvariant() switch
        {
            "500" => throw new InvalidOperationException($"Simulated 500 Internal Server Error (test endpoint)"),
            "400" => throw new ArgumentException($"Simulated 400 Bad Request (test endpoint)"),
            "timeout" => await SimulateTimeoutAsync(cancellationToken).ConfigureAwait(false),
            "exception" => throw new ApplicationException($"Simulated unhandled exception (test endpoint)"),
            _ => throw new ArgumentException($"Invalid error type: {request.ErrorType}. Valid types: 500, 400, timeout, exception")
        };
    }

    private static async Task<Unit> SimulateTimeoutAsync(CancellationToken cancellationToken)
    {
        // Simulate a long-running operation that times out
        await Task.Delay(30000, cancellationToken).ConfigureAwait(false); // 30 seconds - exceeds typical timeout
        throw new TimeoutException("Simulated timeout error (test endpoint)");
    }
}
