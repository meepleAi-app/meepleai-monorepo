using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// #4469 (ADR-095 fetta 7a): utili e non utili per versione del profilo, dalla più recente, con i
/// feedback senza versione in fondo e in una voce propria. Da #4168 ogni feedback porta la versione
/// della risposta che valuta (D2), ma nessuno la leggeva.
/// </summary>
internal sealed class GetAdminKbFeedbackSummaryQueryHandler
    : IQueryHandler<GetAdminKbFeedbackSummaryQuery, AdminKbFeedbackSummaryDto>
{
    private readonly IKbUserFeedbackRepository _repo;
    private readonly IAgentProfileRepository _profiles;
    private readonly MeepleAiDbContext _db;

    public GetAdminKbFeedbackSummaryQueryHandler(
        IKbUserFeedbackRepository repo,
        IAgentProfileRepository profiles,
        MeepleAiDbContext db)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AdminKbFeedbackSummaryDto> Handle(
        GetAdminKbFeedbackSummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var counts = await _repo.CountOutcomesByVersionAsync(cancellationToken).ConfigureAwait(false);
        var versions = await AdminKbFeedbackLabels.VersionsAsync(_profiles, cancellationToken).ConfigureAwait(false);

        var byVersion = counts
            .Select(c =>
            {
                var version = c.ProfileVersionId is { } id && versions.TryGetValue(id, out var v) ? v : null;
                return new AdminKbFeedbackVersionDto(
                    c.ProfileVersionId,
                    version?.VersionNumber,
                    version?.Status.ToString(),
                    version?.PublishedAt,
                    c.Helpful,
                    c.NotHelpful);
            })
            // Dalla versione più recente; i feedback senza versione per ultimi.
            .OrderBy(v => v.ProfileVersionId is null)
            .ThenByDescending(v => v.VersionNumber ?? int.MinValue)
            .ToList();

        var gameIds = await _repo.GetGameIdsAsync(cancellationToken).ConfigureAwait(false);
        var names = await AdminKbFeedbackLabels.GameNamesAsync(_db, gameIds, cancellationToken).ConfigureAwait(false);
        var games = gameIds
            .Select(id => new AdminKbFeedbackGameDto(id, names.GetValueOrDefault(id)))
            .OrderBy(g => g.GameName is null)
            .ThenBy(g => g.GameName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdminKbFeedbackSummaryDto(byVersion, games);
    }
}
