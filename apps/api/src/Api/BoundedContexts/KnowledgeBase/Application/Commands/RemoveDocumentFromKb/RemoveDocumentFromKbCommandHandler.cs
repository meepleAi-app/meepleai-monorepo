using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

/// <summary>
/// Handles RemoveDocumentFromKbCommand.
/// Removes a vector document from the KB, enforcing game ownership.
/// KB-02: Admin per-game KB backend.
/// </summary>
internal sealed class RemoveDocumentFromKbCommandHandler : IRequestHandler<RemoveDocumentFromKbCommand>
{
    private readonly IVectorDocumentRepository _repo;
    private readonly ILogger<RemoveDocumentFromKbCommandHandler> _logger;

    public RemoveDocumentFromKbCommandHandler(
        IVectorDocumentRepository repo,
        ILogger<RemoveDocumentFromKbCommandHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RemoveDocumentFromKbCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var doc = await _repo.GetByIdAsync(command.VectorDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (doc is null || doc.GameId != command.GameId)
        {
            throw new NotFoundException("VectorDocument", command.VectorDocumentId.ToString());
        }

        _logger.LogInformation(
            "Removing VectorDocument {VectorDocumentId} from game {GameId}",
            command.VectorDocumentId,
            command.GameId);

        await _repo.DeleteAsync(command.VectorDocumentId, cancellationToken).ConfigureAwait(false);
    }
}
