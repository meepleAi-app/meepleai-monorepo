using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record TriggerHealthCheckCommand(string ServiceName) : ICommand<HealthCheckResponse>;

internal class TriggerHealthCheckCommandHandler
    : ICommandHandler<TriggerHealthCheckCommand, HealthCheckResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly ILogger<TriggerHealthCheckCommandHandler> _logger;

    public TriggerHealthCheckCommandHandler(
        IInfrastructureHealthService healthService,
        ILogger<TriggerHealthCheckCommandHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResponse> Handle(
        TriggerHealthCheckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("On-demand health check triggered for {Service}", command.ServiceName);

        var health = await _healthService
            .GetServiceHealthAsync(command.ServiceName, cancellationToken)
            .ConfigureAwait(false);

        var status = health.State == HealthState.Healthy
            ? ServiceHealthLevel.Healthy
            : health.State == HealthState.Degraded
                ? ServiceHealthLevel.Degraded
                : ServiceHealthLevel.Down;

        return new HealthCheckResponse(
            command.ServiceName,
            status,
            health.ErrorMessage,
            Math.Round(health.ResponseTime.TotalMilliseconds, 1));
    }
}
