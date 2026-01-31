using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for DeleteChessKnowledgeCommand.
/// Deletes all chess knowledge from the vector database.
/// </summary>
internal sealed class DeleteChessKnowledgeCommandHandler
    : IRequestHandler<DeleteChessKnowledgeCommand, bool>
{
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<DeleteChessKnowledgeCommandHandler> _logger;

    private const string ChessCategory = "chess";

    public DeleteChessKnowledgeCommandHandler(
        IQdrantService qdrantService,
        ILogger<DeleteChessKnowledgeCommandHandler> logger)
    {
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        DeleteChessKnowledgeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            _logger.LogInformation("Deleting all chess knowledge");

            var result = await _qdrantService.DeleteByCategoryAsync(ChessCategory, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Chess knowledge deletion completed: {Success}", result);

            return result;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // ERROR STATE MANAGEMENT: Chess knowledge deletion failures return false
            // Rationale: Deletion failure (typically Qdrant unavailable) should allow the API to
            // respond with a meaningful error. Returning false lets callers check success and display
            // appropriate error messages to administrators. Throwing would cause 500 errors without
            // context about the Qdrant connectivity issue.
            // Context: Deletion failures typically from Qdrant unavailable or network timeout
            _logger.LogError(ex, "Error during chess knowledge deletion");
            return false;
        }
    }
}
