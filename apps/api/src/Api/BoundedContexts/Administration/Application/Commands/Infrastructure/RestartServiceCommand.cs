using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record RestartInfraServiceCommand(string ServiceName) : ICommand<RestartResponse>;

internal class RestartInfraServiceCommandHandler
    : ICommandHandler<RestartInfraServiceCommand, RestartResponse>
{
    private readonly IDockerProxyService _dockerProxy;
    private readonly IServiceCooldownRegistry _cooldownRegistry;
    private readonly ILogger<RestartInfraServiceCommandHandler> _logger;

    public RestartInfraServiceCommandHandler(
        IDockerProxyService dockerProxy,
        IServiceCooldownRegistry cooldownRegistry,
        ILogger<RestartInfraServiceCommandHandler> logger)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
        _cooldownRegistry = cooldownRegistry ?? throw new ArgumentNullException(nameof(cooldownRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RestartResponse> Handle(
        RestartInfraServiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_cooldownRegistry.IsInCooldown(command.ServiceName, out var remainingSeconds))
        {
            throw new ConflictException(
                $"Service '{command.ServiceName}' is in cooldown. {remainingSeconds} seconds remaining.");
        }

        var def = ServiceRegistry.Services[command.ServiceName];
        var containers = await _dockerProxy.GetContainersAsync(cancellationToken).ConfigureAwait(false);
        var container = containers.FirstOrDefault(c =>
            c.Name.Contains(def.ContainerName, StringComparison.OrdinalIgnoreCase));

        if (container == null)
        {
            throw new NotFoundException("Container", def.ContainerName);
        }

        // Container restart via Docker Socket Proxy is not yet available.
        // The docker-socket-proxy (tecnativa/docker-socket-proxy) is configured read-only
        // and does not expose POST endpoints for container lifecycle operations.
        // When write access is enabled, this handler should POST to
        // /v1.43/containers/{id}/restart via the proxy's HttpClient.
        _logger.LogWarning(
            "Restart requested for service {Service} (container {Container}) but container restart " +
            "is not yet available in the current infrastructure setup. Docker socket proxy is read-only.",
            command.ServiceName, container.Id);

        return new RestartResponse(
            false,
            command.ServiceName,
            null,
            "Restart is not yet available. Docker socket proxy is configured as read-only.");
    }
}
