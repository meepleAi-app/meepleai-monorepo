using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - reader, its result and its exception
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (ADR-095 D2): la versione pubblicata del profilo, letta una volta per richiesta. Quella che
/// la pipeline usa è quella che registra: chi la legge una seconda volta a metà richiesta rischia di
/// registrare una versione pubblicata nel frattempo.
/// </summary>
internal interface IPublishedAgentProfileReader
{
    /// <summary>
    /// La versione pubblicata. Lancia <see cref="AgentProfileNotPublishedException"/> se il profilo
    /// non c'è: la pipeline non ricade mai su valori compilati.
    /// </summary>
    Task<PublishedAgentProfile> GetAsync(CancellationToken cancellationToken);
}

/// <summary>La versione pubblicata del profilo: il suo id, il suo numero e il suo contenuto.</summary>
internal sealed record PublishedAgentProfile(Guid VersionId, int VersionNumber, AgentProfileContent Content);

/// <summary>
/// Senza cache: il profilo è una riga e le sue versioni, e una cache chiederebbe un'obsolescenza
/// massima scritta e l'invalidazione alla pubblicazione (fetta 6).
/// </summary>
internal sealed class PublishedAgentProfileReader : IPublishedAgentProfileReader
{
    private readonly IAgentProfileRepository _repository;

    public PublishedAgentProfileReader(IAgentProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PublishedAgentProfile> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await _repository.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new AgentProfileNotPublishedException();
        var published = profile.Published;
        return new PublishedAgentProfile(published.Id, published.VersionNumber, published.Content);
    }
}

/// <summary>
/// Il profilo dell'agente non c'è, per esempio su un database senza il seed: nessuna risposta, e un
/// motivo esplicito invece dei valori compilati.
/// </summary>
internal sealed class AgentProfileNotPublishedException : InvalidOperationException
{
    public const string Code = "AGENT_PROFILE_NOT_PUBLISHED";

    public AgentProfileNotPublishedException()
        : base("The AI agent is not configured: no published agent profile. An administrator must publish one.")
    {
    }
}
