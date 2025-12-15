using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to update a user's subscription tier.
/// Only admin users can change user tiers.
/// </summary>
internal record UpdateUserTierCommand(
    Guid UserId,
    string NewTier,
    Guid RequesterUserId
) : ICommand<UserDto>;
