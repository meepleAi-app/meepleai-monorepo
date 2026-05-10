using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed class ProbeProviderCommandHandler : ICommandHandler<ProbeProviderCommand, ProviderProbeResultDto>
{
    private readonly IProviderProbeService _probeService;

    public ProbeProviderCommandHandler(IProviderProbeService probeService) => _probeService = probeService;

    public Task<ProviderProbeResultDto> Handle(ProbeProviderCommand command, CancellationToken cancellationToken)
        => _probeService.ProbeAsync(command.ProviderName, command.ActorId, command.ExpectedModel, cancellationToken);
}
