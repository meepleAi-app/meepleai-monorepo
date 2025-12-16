using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NConfig;

/// <summary>
/// Handles n8n connection testing with state updates.
/// Business logic: Config validation, test result persistence.
/// Infrastructure delegation: HTTP calls, API key decryption, database updates via N8NConfigService.
/// Side effect: Updates config.LastTestedAt and config.LastTestResult in database.
/// </summary>
internal sealed class TestN8NConnectionCommandHandler : ICommandHandler<TestN8NConnectionCommand, N8NTestResult>
{
    private readonly N8NConfigService _configService;
    private readonly ILogger<TestN8NConnectionCommandHandler> _logger;

    public TestN8NConnectionCommandHandler(
        N8NConfigService configService,
        ILogger<TestN8NConnectionCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(configService);
        _configService = configService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<N8NTestResult> Handle(TestN8NConnectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Business logic validation
        if (command.ConfigId == Guid.Empty)
        {
            throw new ArgumentException("Config ID is required", nameof(command));
        }

        _logger.LogInformation("Testing n8n connection for config {ConfigId}", command.ConfigId);

        // Delegate to infrastructure service for:
        // - Database query
        // - API key decryption
        // - HTTP call to n8n
        // - Latency measurement
        // - Database update (LastTestedAt, LastTestResult)
        var result = await _configService.TestConnectionAsync(command.ConfigId.ToString(), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "n8n config {ConfigId} test result: {Success}, Latency: {Latency}ms",
            command.ConfigId, result.Success, result.LatencyMs);

        return result;
    }
}
