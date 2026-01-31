using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

internal record GetConfigByKeyQuery(
    string Key,
    string? Environment = null,
    bool ActiveOnly = true
) : IQuery<ConfigurationDto?>;
