using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve chat history tier limits configuration.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
internal record GetChatHistoryLimitsQuery : IQuery<ChatHistoryLimitsDto>;
