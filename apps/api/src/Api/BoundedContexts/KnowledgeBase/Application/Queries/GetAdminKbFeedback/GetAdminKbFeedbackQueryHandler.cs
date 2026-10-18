using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// Handles GetAdminKbFeedbackQuery.
/// Returns paginated user feedback on KB responses.
/// KB-08: Admin Feedback Review backend. #4469: senza UUID del gioco, per versione del profilo, con il
/// nome del gioco e l'inizio della risposta valutata.
/// </summary>
internal sealed class GetAdminKbFeedbackQueryHandler
    : IQueryHandler<GetAdminKbFeedbackQuery, AdminKbFeedbackDto>
{
    /// <summary>Il valore di <see cref="GetAdminKbFeedbackQuery.Version"/> che chiede i feedback senza versione.</summary>
    internal const string WithoutVersion = "none";

    internal const int AnswerExcerptLength = 280;

    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    private readonly IKbUserFeedbackRepository _repo;
    private readonly IAgentProfileRepository _profiles;
    private readonly IChatThreadRepository _threads;
    private readonly MeepleAiDbContext _db;

    public GetAdminKbFeedbackQueryHandler(
        IKbUserFeedbackRepository repo,
        IAgentProfileRepository profiles,
        IChatThreadRepository threads,
        MeepleAiDbContext db)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AdminKbFeedbackDto> Handle(
        GetAdminKbFeedbackQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(MinPage, query.Page);
        var pageSize = Math.Clamp(query.PageSize, MinPageSize, MaxPageSize);
        var filter = ToFilter(query);

        var feedbackList = await _repo.ListAsync(filter, page, pageSize, cancellationToken).ConfigureAwait(false);
        var total = await _repo.CountAsync(filter, cancellationToken).ConfigureAwait(false);

        var versions = await AdminKbFeedbackLabels.VersionsAsync(_profiles, cancellationToken).ConfigureAwait(false);
        var gameNames = await AdminKbFeedbackLabels.GameNamesAsync(
            _db, feedbackList.Select(f => f.GameId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var threads = await _threads.GetByIdsAsync(
            feedbackList.Select(f => f.ChatSessionId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var answers = threads
            .SelectMany(t => t.Messages)
            .Where(m => m.IsAssistantMessage && !m.IsDeleted)
            .GroupBy(m => m.Id)
            .ToDictionary(g => g.Key, g => g.First().Content);

        var items = feedbackList
            .Select(f => new AdminKbFeedbackItemDto(
                Id: f.Id,
                UserId: f.UserId,
                GameId: f.GameId,
                GameName: gameNames.GetValueOrDefault(f.GameId),
                ChatSessionId: f.ChatSessionId,
                MessageId: f.MessageId,
                ProfileVersionId: f.ProfileVersionId,
                VersionNumber: f.ProfileVersionId is { } versionId && versions.TryGetValue(versionId, out var version)
                    ? version.VersionNumber
                    : null,
                Outcome: f.Outcome,
                Comment: f.Comment,
                CreatedAt: f.CreatedAt,
                AnswerExcerpt: answers.TryGetValue(f.MessageId, out var answer) ? Excerpt(answer) : null))
            .ToList();

        return new AdminKbFeedbackDto(total, items);
    }

    private static KbFeedbackFilter ToFilter(GetAdminKbFeedbackQuery query)
    {
        if (query.Version is null)
            return new KbFeedbackFilter(query.GameId, Outcome: query.OutcomeFilter, From: query.FromDate);

        if (string.Equals(query.Version, WithoutVersion, StringComparison.Ordinal))
            return new KbFeedbackFilter(query.GameId, WithoutVersion: true, Outcome: query.OutcomeFilter, From: query.FromDate);

        if (Guid.TryParse(query.Version, out var versionId))
            return new KbFeedbackFilter(query.GameId, versionId, Outcome: query.OutcomeFilter, From: query.FromDate);

        throw new BadRequestException(
            $"version must be the id of a profile version or '{WithoutVersion}', not '{query.Version}'.");
    }

    private static string Excerpt(string answer) =>
        answer.Length <= AnswerExcerptLength ? answer : string.Concat(answer.AsSpan(0, AnswerExcerptLength), "…");
}
