using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nConfig;

/// <summary>
/// Handles n8n connection testing with state updates.
/// Business logic: Config validation, test result persistence.
/// Infrastructure delegation: HTTP calls, API key decryption, database updates via N8nConfigService.
/// Side effect: Updates config.LastTestedAt and config.LastTestResult in database.
/// </summary>
public sealed class TestN8nConnectionCommandHandler : ICommandHandler<TestN8nConnectionCommand, N8nTestResult>
{
    private readonly N8nConfigService _configService;
    private readonly ILogger<TestN8nConnectionCommandHandler> _logger;

    public TestN8nConnectionCommandHandler(
        N8nConfigService configService,
        ILogger<TestN8nConnectionCommandHandler> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<N8nTestResult> Handle(TestN8nConnectionCommand command, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (command.ConfigId == Guid.Empty)
        {
            throw new ArgumentException("Config ID is required", nameof(command.ConfigId));
        }

        _logger.LogInformation("Testing n8n connection for config {ConfigId}", command.ConfigId);

        // Delegate to infrastructure service for:
        // - Database query
        // - API key decryption
        // - HTTP call to n8n
        // - Latency measurement
        // - Database update (LastTestedAt, LastTestResult)
        var result = await _configService.TestConnectionAsync(command.ConfigId.ToString(), cancellationToken);

        _logger.LogInformation(
            "n8n config {ConfigId} test result: {Success}, Latency: {Latency}ms",
            command.ConfigId, result.Success, result.LatencyMs);

        return result;
    }
}
