namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4482: il servizio di embedding non ha dato il vettore della domanda, quindi non si può cercare.
/// </summary>
/// <remarks>
/// Deriva da <see cref="InvalidOperationException"/>, il tipo che si lanciava prima: chi lo intercettava
/// continua a farlo. Serve a chi vuole dire il motivo invece di un 500 (#2568): la sonda di recupero lo
/// traduce in un 503 con <see cref="Code"/>.
/// </remarks>
internal sealed class EmbeddingUnavailableException : InvalidOperationException
{
    public const string Code = "EMBEDDING_UNAVAILABLE";

    public EmbeddingUnavailableException(string message)
        : base(message)
    {
    }
}
