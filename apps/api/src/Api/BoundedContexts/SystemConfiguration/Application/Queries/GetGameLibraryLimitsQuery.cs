using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve game library tier limits configuration.
/// </summary>
internal record GetGameLibraryLimitsQuery : IQuery<GameLibraryLimitsDto>;
