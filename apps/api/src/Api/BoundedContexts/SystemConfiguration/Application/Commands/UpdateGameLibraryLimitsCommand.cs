using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update game library tier limits configuration.
/// Creates configurations if they don't exist, updates if they do.
/// </summary>
internal record UpdateGameLibraryLimitsCommand(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    Guid UpdatedByUserId
) : ICommand<GameLibraryLimitsDto>;
