using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for upvoting a FAQ.
/// Issue #2681: Public FAQs endpoints
/// </summary>
internal sealed class UpvoteFaqCommandHandler : ICommandHandler<UpvoteFaqCommand, UpvoteFaqResultDto>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpvoteFaqCommandHandler> _logger;

    public UpvoteFaqCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpvoteFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpvoteFaqResultDto> Handle(UpvoteFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Upvoting FAQ: {FaqId}", command.FaqId);

        var game = await _repository.GetGameByFaqIdAsync(command.FaqId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"FAQ with ID {command.FaqId} not found");

        var newUpvoteCount = game.UpvoteFaq(command.FaqId);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FAQ upvoted successfully: {FaqId}, New count: {UpvoteCount}", command.FaqId, newUpvoteCount);

        return new UpvoteFaqResultDto(command.FaqId, newUpvoteCount);
    }
}
