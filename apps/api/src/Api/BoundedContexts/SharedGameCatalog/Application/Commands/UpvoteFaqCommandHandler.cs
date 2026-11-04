using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for upvoting a FAQ.
/// Issue #2681: Public FAQs endpoints
/// </summary>
/// <remarks>
/// #4557: il voto è pubblico e anonimo, e incrementa il contatore sulla riga della FAQ senza passare
/// dall'aggregato <c>SharedGame</c>. Un update dell'aggregato riscrive tutta la riga del gioco e
/// azzera le colonne che il mapper non porta; vedi <see cref="ISharedGameRepository.IncrementFaqUpvoteAsync"/>.
/// L'incremento è un UPDATE immediato: non c'è nulla da salvare con lo unit of work.
/// <para>
/// #4565: il dettaglio del gioco in cache riporta il contatore (<c>faqs[].upvotes</c>), quindi dopo il
/// voto si evince <c>shared-game:{id}</c>. Il gioco lo dà il repository insieme al contatore: il comando
/// porta solo l'id della FAQ. La lista del catalogo (<c>search-games</c>) non riporta le FAQ, quindi non
/// si tocca. Il voto è anonimo e ogni voto fa rigenerare il dettaglio di quel gioco alla lettura
/// successiva: è il costo di un contatore esatto nel dettaglio. Il limite è la policy di rate limit
/// <c>FaqUpvote</c> (per IP), non questo handler.
/// </para>
/// </remarks>
internal sealed class UpvoteFaqCommandHandler : ICommandHandler<UpvoteFaqCommand, UpvoteFaqResultDto>
{
    private readonly ISharedGameRepository _repository;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<UpvoteFaqCommandHandler> _logger;

    public UpvoteFaqCommandHandler(
        ISharedGameRepository repository,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<UpvoteFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpvoteFaqResultDto> Handle(UpvoteFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Upvoting FAQ: {FaqId}", command.FaqId);

        var upvote = await _repository.IncrementFaqUpvoteAsync(command.FaqId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Faq", command.FaqId.ToString());

        // #4565: dopo l'UPDATE, non prima. Vedi ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailAsync(upvote.SharedGameId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FAQ upvoted successfully: {FaqId}, New count: {UpvoteCount}", command.FaqId, upvote.UpvoteCount);

        return new UpvoteFaqResultDto(command.FaqId, upvote.UpvoteCount);
    }
}
