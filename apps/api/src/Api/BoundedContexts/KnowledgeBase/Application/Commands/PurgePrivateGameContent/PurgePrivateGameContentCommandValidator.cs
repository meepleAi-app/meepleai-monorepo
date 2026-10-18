using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;

/// <summary>
/// Un id vuoto qui non è un errore da poco: <c>pgvector_embeddings.game_id</c> è un id solo, e
/// una cancellazione «per gioco» con l'id sbagliato porta via contenuto che non c'entra.
/// </summary>
internal sealed class PurgePrivateGameContentCommandValidator : AbstractValidator<PurgePrivateGameContentCommand>
{
    public PurgePrivateGameContentCommandValidator()
    {
        RuleFor(x => x.PrivateGameId).NotEmpty();
    }
}
