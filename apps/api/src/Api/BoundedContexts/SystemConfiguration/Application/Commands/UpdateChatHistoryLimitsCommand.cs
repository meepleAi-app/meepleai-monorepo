using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update chat history tier limits configuration.
/// Creates configurations if they don't exist, updates if they do.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
internal record UpdateChatHistoryLimitsCommand(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    Guid UpdatedByUserId
) : ICommand<ChatHistoryLimitsDto>;
