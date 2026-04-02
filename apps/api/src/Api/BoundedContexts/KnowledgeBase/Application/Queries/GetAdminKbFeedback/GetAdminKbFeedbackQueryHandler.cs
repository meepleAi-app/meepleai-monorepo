using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// Handles GetAdminKbFeedbackQuery.
/// Returns paginated user feedback on KB responses for a specific game.
/// KB-08: Admin Feedback Review backend.
/// </summary>
internal sealed class GetAdminKbFeedbackQueryHandler
    : IQueryHandler<GetAdminKbFeedbackQuery, AdminKbFeedbackDto>
{
    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    private readonly IKbUserFeedbackRepository _repo;

    public GetAdminKbFeedbackQueryHandler(IKbUserFeedbackRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<AdminKbFeedbackDto> Handle(
        GetAdminKbFeedbackQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(MinPage, query.Page);
        var pageSize = Math.Clamp(query.PageSize, MinPageSize, MaxPageSize);

        var feedbackList = await _repo.GetByGameIdAsync(
            query.GameId,
            query.OutcomeFilter,
            query.FromDate,
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

        var total = await _repo.CountByGameIdAsync(
            query.GameId,
            query.OutcomeFilter,
            query.FromDate,
            cancellationToken).ConfigureAwait(false);

        var items = feedbackList
            .Select(f => new AdminKbFeedbackItemDto(
                Id: f.Id,
                UserId: f.UserId,
                GameId: f.GameId,
                ChatSessionId: f.ChatSessionId,
                MessageId: f.MessageId,
                Outcome: f.Outcome,
                Comment: f.Comment,
                CreatedAt: f.CreatedAt))
            .ToList();

        return new AdminKbFeedbackDto(total, items);
    }
}
