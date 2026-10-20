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
/// </remarks>
internal sealed class UpvoteFaqCommandHandler : ICommandHandler<UpvoteFaqCommand, UpvoteFaqResultDto>
{
    private readonly ISharedGameRepository _repository;
    private readonly ILogger<UpvoteFaqCommandHandler> _logger;

    public UpvoteFaqCommandHandler(
        ISharedGameRepository repository,
        ILogger<UpvoteFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpvoteFaqResultDto> Handle(UpvoteFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Upvoting FAQ: {FaqId}", command.FaqId);

        var newUpvoteCount = await _repository.IncrementFaqUpvoteAsync(command.FaqId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Faq", command.FaqId.ToString());

        _logger.LogInformation("FAQ upvoted successfully: {FaqId}, New count: {UpvoteCount}", command.FaqId, newUpvoteCount);

        return new UpvoteFaqResultDto(command.FaqId, newUpvoteCount);
    }
}
