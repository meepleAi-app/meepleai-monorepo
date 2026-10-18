using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

internal interface IKbUserFeedbackRepository
{
    Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// #4469: i feedback che passano il filtro, dal più recente. Prima il gioco era obbligatorio, e
    /// l'admin doveva digitarne l'UUID.
    /// </summary>
    Task<List<KbUserFeedback>> ListAsync(
        KbFeedbackFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(KbFeedbackFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// #4469: utili e non utili per versione del profilo, una voce per ogni versione con almeno un
    /// feedback. I feedback senza versione hanno una voce propria, con <c>ProfileVersionId</c> nullo.
    /// </summary>
    Task<IReadOnlyList<KbFeedbackVersionCount>> CountOutcomesByVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>#4469: i giochi che hanno almeno un feedback.</summary>
    Task<IReadOnlyList<Guid>> GetGameIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Count feedback rows whose CreatedAt is on or after the given moment.
    /// Issue #1655: KbSubNav count badges.
    /// </summary>
    Task<int> CountSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}

/// <summary>
/// #4469: i filtri della lista admin dei feedback. Ogni filtro assente lascia passare tutto.
/// <see cref="WithoutVersion"/> chiede i feedback senza versione, e allora <see cref="ProfileVersionId"/>
/// è nullo.
/// </summary>
internal sealed record KbFeedbackFilter(
    Guid? GameId = null,
    Guid? ProfileVersionId = null,
    bool WithoutVersion = false,
    string? Outcome = null,
    DateTime? From = null);

/// <summary>#4469: i feedback di una versione del profilo, o di nessuna.</summary>
internal sealed record KbFeedbackVersionCount(Guid? ProfileVersionId, int Helpful, int NotHelpful);
