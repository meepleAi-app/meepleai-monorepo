using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record RestartServiceCommand(string ServiceName) : ICommand<RestartResponse>;

internal class RestartServiceCommandHandler
    : ICommandHandler<RestartServiceCommand, RestartResponse>
{
    private readonly IDockerProxyService _dockerProxy;
    private readonly IServiceCooldownRegistry _cooldownRegistry;
    private readonly ILogger<RestartServiceCommandHandler> _logger;

    public RestartServiceCommandHandler(
        IDockerProxyService dockerProxy,
        IServiceCooldownRegistry cooldownRegistry,
        ILogger<RestartServiceCommandHandler> logger)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
        _cooldownRegistry = cooldownRegistry ?? throw new ArgumentNullException(nameof(cooldownRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RestartResponse> Handle(
        RestartServiceCommand command, CancellationToken cancellationToken)
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

        _logger.LogWarning("Restarting service {Service} (container {Container}) by admin request",
            command.ServiceName, container.Id);

        _cooldownRegistry.RecordRestart(command.ServiceName);

        var cooldownExpires = DateTime.UtcNow.AddMinutes(5);
        return new RestartResponse(true, command.ServiceName, cooldownExpires);
    }
}
