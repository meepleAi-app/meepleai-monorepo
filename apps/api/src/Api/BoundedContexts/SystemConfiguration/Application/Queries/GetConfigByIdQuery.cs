using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

public record GetConfigByIdQuery(Guid ConfigId) : IQuery<ConfigurationDto?>;
