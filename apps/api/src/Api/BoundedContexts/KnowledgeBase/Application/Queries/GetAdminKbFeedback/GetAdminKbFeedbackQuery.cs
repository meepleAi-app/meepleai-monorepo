using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// Query to retrieve paginated user feedback on KB responses for a specific game (admin view).
/// KB-08: Admin Feedback Review backend.
/// </summary>
internal sealed record GetAdminKbFeedbackQuery(
    Guid GameId,
    string? OutcomeFilter,
    DateTime? FromDate,
    int Page,
    int PageSize) : IQuery<AdminKbFeedbackDto>;

/// <summary>
/// Paginated result containing feedback items for a game.
/// </summary>
internal sealed record AdminKbFeedbackDto(
    int Total,
    List<AdminKbFeedbackItemDto> Items);

/// <summary>
/// Single feedback item in the admin feedback list.
/// </summary>
internal sealed record AdminKbFeedbackItemDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid ChatSessionId,
    Guid MessageId,
    string Outcome,
    string? Comment,
    DateTime CreatedAt);
