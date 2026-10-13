using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168: all'avvio, un profilo dell'agente che manca si segnala nel log, invece di scoprirlo alla
/// prima domanda. Senza profilo la pipeline rifiuta ogni risposta con
/// <see cref="AgentProfileNotPublishedException.Code"/>: non ricade su valori compilati. È lo stesso
/// principio del controllo su <c>OPENROUTER_DEFAULT_MODEL</c> in <c>Program.cs</c>.
/// </summary>
internal static class AgentProfileStartupCheck
{
    /// <returns><c>true</c> se c'è una versione pubblicata.</returns>
    internal static async Task<bool> LogIfMissingAsync(
        IAgentProfileRepository repository, ILogger logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var profile = await repository.GetAsync(cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                logger.LogError(
                    "No published agent profile: every answer will be refused with {Code} until one is published. "
                    + "The migration AddAgentProfile seeds the first version — check that it ran on this database.",
                    AgentProfileNotPublishedException.Code);
                return false;
            }

            logger.LogInformation(
                "✓ Agent profile: version {Version} published, primary model {Model}",
                profile.Published.VersionNumber, profile.Published.Content.PrimaryModelId);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // L'avvio continua: è un controllo, e la pipeline darà comunque l'errore esplicito.
            logger.LogError(ex, "The agent profile could not be read at startup");
            return false;
        }
    }
}
