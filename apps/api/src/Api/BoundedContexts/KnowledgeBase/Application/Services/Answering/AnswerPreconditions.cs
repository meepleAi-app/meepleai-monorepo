using Api.Infrastructure.Entities;
using Api.Models;
using Api.Observability;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3, consegne 5g-a e 5g-b): ciò che va verificato prima di cercare e prima di
/// chiamare il modello. I tre percorsi che rispondono a una domanda — la chat
/// (<see cref="AnswerPipeline"/>), la domanda singola (<c>AskQuestionQueryHandler</c>) e la ricerca
/// fra giochi (<see cref="CrossGameAnswerFlow"/>) — lo eseguono qui, e non ciascuno per conto suo.
/// </summary>
/// <remarks>
/// Il passo 1 è l'accesso, il passo 2 la quota del piano.
/// <c>AnswerPathsSharePreconditionsArchitectureTests</c> impedisce a un percorso di tornare a
/// chiedere al servizio d'accesso, o a quello dei piani, per conto suo.
/// </remarks>
internal interface IAnswerPreconditions
{
    /// <summary>
    /// Chi chiede può interrogare la KB di questo gioco? Lancia
    /// <see cref="Api.Middleware.Exceptions.RagOwnershipRequiredException"/> se non può.
    /// </summary>
    /// <param name="userId">Chi chiede.</param>
    /// <param name="userRole">Il suo ruolo, come lo riporta la sessione. Un ruolo che non si riconosce vale come utente.</param>
    /// <param name="gameId">Il gioco della domanda, del catalogo o privato.</param>
    /// <param name="cancellationToken">Annullamento della richiesta.</param>
    Task EnsureCanAskAboutGameAsync(Guid userId, string? userRole, Guid gameId, CancellationToken cancellationToken);

    /// <summary>I giochi che chi chiede può interrogare: l'ambito di una domanda fra giochi.</summary>
    Task<IReadOnlyList<Guid>> GamesOpenToAsync(Guid userId, UserRole role, CancellationToken cancellationToken);

    /// <summary>
    /// La quota del piano: quante domande ha già avuto risposta oggi (giorno UTC), e quante ne
    /// prevede il piano. Va letta prima della cache: anche una risposta servita dalla cache è una
    /// domanda. Non lancia: il percorso decide come dirlo (un evento nello stream, un 403 fuori).
    /// </summary>
    Task<AnswerQuota> CheckQuotaAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A risposta consegnata, che venga dalla cache o dal modello. Non per una domanda fermata
    /// prima (possesso, quota, tetto di spesa) né per una risposta interrotta a metà.
    /// </summary>
    Task RecordAnswerDeliveredAsync(AnswerQuota quota, CancellationToken cancellationToken);
}

/// <summary>
/// L'esito del passo 2 per una domanda. Il percorso lo tiene dalla verifica alla consegna.
/// </summary>
/// <param name="UserId">Chi chiede.</param>
/// <param name="Current">Le domande che oggi hanno già avuto risposta.</param>
/// <param name="Max">Quante ne prevede il piano.</param>
/// <param name="Exceeded">La quota è finita: la domanda si ferma qui.</param>
/// <param name="Counts">
/// La risposta va contata. Falso per chi non ha un limite (admin, editor): il loro contatore non
/// cresce.
/// </param>
internal sealed record AnswerQuota(Guid UserId, int Current, int Max, bool Exceeded, bool Counts)
{
    /// <summary>Il codice dell'evento d'errore nello stream. Maiuscolo come i codici <c>AI_BUDGET_*</c>.</summary>
    public const string StreamErrorCode = "TIER_LIMIT_EXCEEDED";

    /// <summary>Il tipo di limite, come lo chiama il 403 <c>tier_limit_exceeded</c>.</summary>
    public const string LimitType = nameof(TierAction.AgentQuery);

    /// <summary>
    /// Fuori dallo stream: il 403 <c>tier_limit_exceeded</c> che gli altri limiti di piano usano già.
    /// </summary>
    public TierLimitExceededException ToException() => new(LimitType, Current, Max);

    /// <summary>
    /// Nello stream: lo stesso contenuto di quel 403, in un evento <c>Error</c>.
    /// </summary>
    public StreamingError ToStreamingError()
    {
        var refusal = ToException();
        return new StreamingError(
            refusal.Message,
            StreamErrorCode,
            limitType: refusal.LimitType,
            current: refusal.Current,
            max: refusal.Max,
            upgradeUrl: refusal.UpgradeUrl);
    }
}

/// <inheritdoc cref="IAnswerPreconditions"/>
internal sealed class AnswerPreconditions : IAnswerPreconditions
{
    private readonly IRagAccessService _ragAccessService;
    private readonly ITierEnforcementService _tiers;
    private readonly ILogger<AnswerPreconditions> _logger;

    public AnswerPreconditions(
        IRagAccessService ragAccessService,
        ITierEnforcementService tiers,
        ILogger<AnswerPreconditions> logger)
    {
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureCanAskAboutGameAsync(
        Guid userId, string? userRole, Guid gameId, CancellationToken cancellationToken)
    {
        await _ragAccessService
            .EnsureRagAccessAsync(userId, gameId, RoleOf(userRole), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Guid>> GamesOpenToAsync(Guid userId, UserRole role, CancellationToken cancellationToken) =>
        _ragAccessService.GetAccessibleGameIdsAsync(userId, role, cancellationToken);

    public async Task<AnswerQuota> CheckQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usage = await _tiers
            .CheckUsageAsync(userId, TierAction.AgentQuery, cancellationToken)
            .ConfigureAwait(false);

        if (!usage.CounterReadable)
        {
            // Redis non risponde. La quota non blocca, ed è voluto: la spesa la protegge il tetto
            // del gateway, che invece blocca (AI_BUDGET_UNAVAILABLE). Ma non passa in silenzio.
            _logger.LogWarning(
                "The plan quota of user {UserId} could not be read (limit {Max}): the question goes through unchecked",
                userId, usage.Max);
            MeepleAiMetrics.RecordAnswerQuotaUnreadable();
        }

        return new AnswerQuota(userId, usage.Current, usage.Max, usage.Exceeded, Counts: !usage.IsUnlimited);
    }

    public async Task RecordAnswerDeliveredAsync(AnswerQuota quota, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quota);
        if (!quota.Counts)
        {
            return;
        }

        await _tiers
            .RecordUsageAsync(quota.UserId, TierAction.AgentQuery, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Il ruolo decide il passaggio degli amministratori. Un testo che non è un ruolo vale come
    /// utente: nel dubbio si controlla, non si lascia passare.
    /// </summary>
    private static UserRole RoleOf(string? userRole) =>
        Enum.TryParse<UserRole>(userRole, ignoreCase: true, out var role) ? role : UserRole.User;
}
