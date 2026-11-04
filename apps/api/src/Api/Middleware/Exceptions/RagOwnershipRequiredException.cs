using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// #4155: il cancello RAG (#443, B5) ha respinto la domanda perché l'utente non ha dichiarato di
/// possedere il gioco. Mappata su 403 con codice <see cref="Code"/> e il gioco, così il client
/// può offrire la dichiarazione per quel gioco invece di un «accesso negato» generico.
/// </summary>
/// <remarks>
/// <para>
/// Deriva da <see cref="ForbiddenException"/>: per chi intercetta già i 403 non cambia nulla.
/// Nello stream della chat lo stesso diniego viaggia come evento <c>Error</c> con lo stesso
/// codice, perché lì la risposta è già un 200 quando il cancello scatta.
/// </para>
/// <para>
/// <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService.CanAccessRagAsync"/>
/// risponde con un booleano, quindi questo codice copre ogni diniego per un non-admin. Per un
/// gioco del catalogo la via d'uscita è la dichiarazione; per il gioco privato di un altro
/// utente non ce n'è, e il codice resta vero alla lettera: quel gioco non è tuo.
/// </para>
/// </remarks>
public sealed class RagOwnershipRequiredException : ForbiddenException
{
    /// <summary>Il codice che il client riconosce, nel 403 e nell'evento di stream.</summary>
    public const string Code = "RAG_OWNERSHIP_REQUIRED";

    [SetsRequiredMembers]
    public RagOwnershipRequiredException(Guid gameId)
        : base(Code, "Per consultare il manuale di questo gioco devi dichiarare di possederne una copia.")
    {
        GameId = gameId;
    }

    /// <summary>Il gioco per cui manca la dichiarazione di possesso.</summary>
    public Guid GameId { get; }
}
