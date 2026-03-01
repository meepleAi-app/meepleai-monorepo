using Api.BoundedContexts.EntityRelationships.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Handler for ImportBggExpansionsCommand (Issue #5141).
/// Delegates to IBggExpansionImporter for the actual BGG fetch + EntityLink creation.
/// </summary>
internal sealed class ImportBggExpansionsCommandHandler
    : ICommandHandler<ImportBggExpansionsCommand, int>
{
    private readonly IBggExpansionImporter _importer;
    private readonly ILogger<ImportBggExpansionsCommandHandler> _logger;

    public ImportBggExpansionsCommandHandler(
        IBggExpansionImporter importer,
        ILogger<ImportBggExpansionsCommandHandler> logger)
    {
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(ImportBggExpansionsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Importing BGG expansions for SharedGame {SharedGameId} by admin {AdminUserId}",
            command.SharedGameId,
            command.AdminUserId);

        var created = await _importer
            .ImportExpansionsAsync(command.SharedGameId, command.AdminUserId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "BGG expansion import complete for SharedGame {SharedGameId}: {Count} links created",
            command.SharedGameId,
            created);

        return created;
    }
}
