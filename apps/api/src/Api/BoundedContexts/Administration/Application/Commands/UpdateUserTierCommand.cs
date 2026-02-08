using Api.BoundedContexts.Administration.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to update a user's subscription tier.
/// Only admin users can change user tiers.
/// </summary>
[AuditableAction("TierChange", "User", Level = 1)]
internal record UpdateUserTierCommand(
    Guid UserId,
    string NewTier,
    Guid RequesterUserId
) : ICommand<UserDto>;
