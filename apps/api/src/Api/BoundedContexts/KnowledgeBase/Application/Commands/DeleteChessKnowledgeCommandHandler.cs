using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for DeleteChessKnowledgeCommand.
/// Deletes all chess knowledge from the vector database.
/// NOTE: Qdrant dependency removed — this handler is now a no-op stub.
/// </summary>
internal sealed class DeleteChessKnowledgeCommandHandler
    : IRequestHandler<DeleteChessKnowledgeCommand, bool>
{
    private readonly ILogger<DeleteChessKnowledgeCommandHandler> _logger;

    public DeleteChessKnowledgeCommandHandler(
        ILogger<DeleteChessKnowledgeCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> Handle(
        DeleteChessKnowledgeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("DeleteChessKnowledge called — no-op (Qdrant dependency removed)");

        return Task.FromResult(true);
    }
}
