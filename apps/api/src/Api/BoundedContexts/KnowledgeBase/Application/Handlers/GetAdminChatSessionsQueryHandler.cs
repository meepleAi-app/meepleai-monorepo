using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetAdminChatSessionsQuery.
/// Returns paginated chat thread list for admin dashboard.
/// Issue #4917: Admin chat history real data.
/// </summary>
internal class GetAdminChatSessionsQueryHandler
    : IRequestHandler<GetAdminChatSessionsQuery, AdminChatSessionsResult>
{
    private readonly IChatThreadRepository _chatThreadRepository;
    private readonly ILogger<GetAdminChatSessionsQueryHandler> _logger;

    public GetAdminChatSessionsQueryHandler(
        IChatThreadRepository chatThreadRepository,
        ILogger<GetAdminChatSessionsQueryHandler> logger)
    {
        _chatThreadRepository = chatThreadRepository ?? throw new ArgumentNullException(nameof(chatThreadRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminChatSessionsResult> Handle(
        GetAdminChatSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _chatThreadRepository.GetAllFilteredAsync(
            agentType: request.AgentType,
            dateFrom: request.DateFrom,
            dateTo: request.DateTo,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Admin chat sessions query: {Count}/{Total} results (page {Page})",
            items.Count, totalCount, request.Page);

        var sessions = items.Select(summary =>
        {
            var userName = summary.UserDisplayName
                ?? (summary.UserEmail != null ? summary.UserEmail.Split('@')[0] : $"user:{summary.UserId.ToString()[..8]}");

            var durationSeconds = (int)(summary.LastMessageAt - summary.CreatedAt).TotalSeconds;

            var preview = summary.PreviewMessages
                .Select(m => new AdminChatPreviewMessageDto(
                    Role: m.Role,
                    Content: m.Content.Length > 200 ? m.Content[..200] + "..." : m.Content
                ))
                .ToList();

            return new AdminChatSessionDto(
                Id: summary.Id.ToString(),
                UserId: summary.UserId.ToString(),
                UserName: userName,
                Agent: summary.AgentType ?? "auto",
                MessageCount: summary.MessageCount,
                DurationSeconds: durationSeconds,
                Date: summary.LastMessageAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Preview: preview
            );
        }).ToList();

        return new AdminChatSessionsResult(sessions, totalCount, request.Page, request.PageSize);
    }
}
