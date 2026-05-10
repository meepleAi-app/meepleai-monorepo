using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed record ProbeProviderCommand(string ProviderName, Guid ActorId) : ICommand<ProviderProbeResultDto>;
