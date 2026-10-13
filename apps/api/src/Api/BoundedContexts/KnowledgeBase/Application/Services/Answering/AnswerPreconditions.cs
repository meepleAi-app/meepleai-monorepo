using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D3, consegna 5g-a): ciò che va verificato prima di cercare e prima di chiamare
/// il modello. I tre percorsi che rispondono a una domanda — la chat (<see cref="AnswerPipeline"/>),
/// la domanda singola (<c>AskQuestionQueryHandler</c>) e la ricerca fra giochi
/// (<see cref="CrossGameAnswerFlow"/>) — lo eseguono qui, e non ciascuno per conto suo.
/// </summary>
/// <remarks>
/// Oggi è il passo 1, l'accesso. Il passo 2, la quota per piano, arriva qui con la consegna 5g-b:
/// è il motivo per cui il passo sta in un punto solo. <c>AnswerPathsSharePreconditionsArchitectureTests</c>
/// impedisce a un percorso di tornare a chiedere al servizio d'accesso per conto suo.
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
}

/// <inheritdoc cref="IAnswerPreconditions"/>
internal sealed class AnswerPreconditions : IAnswerPreconditions
{
    private readonly IRagAccessService _ragAccessService;

    public AnswerPreconditions(IRagAccessService ragAccessService)
    {
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
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

    /// <summary>
    /// Il ruolo decide il passaggio degli amministratori. Un testo che non è un ruolo vale come
    /// utente: nel dubbio si controlla, non si lascia passare.
    /// </summary>
    private static UserRole RoleOf(string? userRole) =>
        Enum.TryParse<UserRole>(userRole, ignoreCase: true, out var role) ? role : UserRole.User;
}
