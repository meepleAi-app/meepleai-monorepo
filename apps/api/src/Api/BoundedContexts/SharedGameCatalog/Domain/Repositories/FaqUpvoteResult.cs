namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// L'esito di <see cref="ISharedGameRepository.IncrementFaqUpvoteAsync"/> (#4565): il contatore dopo
/// il voto e il gioco a cui la FAQ appartiene.
/// </summary>
/// <param name="SharedGameId">
/// Il gioco della FAQ. Il comando di voto porta solo l'id della FAQ, e chi deve invalidare il dettaglio
/// in cache del gioco (<c>shared-game:{id}</c>) non ha altro modo di conoscerlo.
/// </param>
/// <param name="UpvoteCount">Il numero di voti dopo l'incremento.</param>
public sealed record FaqUpvoteResult(Guid SharedGameId, int UpvoteCount);
