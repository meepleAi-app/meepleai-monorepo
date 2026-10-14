using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - the two queries of the draft trial and their handlers
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentProfileAdmin;

/// <summary>
/// #4169 (6c-3, ADR-095 D8): la bozza da provare. Si risolve prima di aprire lo stream, così che
/// «nessuna bozza» sia un 409 e non un evento.
/// </summary>
internal sealed record GetAgentProfileDraftTrialQuery : IQuery<AnswerTrial>;

internal sealed class GetAgentProfileDraftTrialQueryHandler : IQueryHandler<GetAgentProfileDraftTrialQuery, AnswerTrial>
{
    /// <summary>Il codice del 409 quando non c'è una bozza da provare.</summary>
    public const string NoDraftCode = "agent_profile_no_draft";

    private readonly IAgentProfileRepository _repository;

    public GetAgentProfileDraftTrialQueryHandler(IAgentProfileRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AnswerTrial> Handle(GetAgentProfileDraftTrialQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var profile = await _repository.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AgentProfile", "system");
        var draft = profile.Draft
            ?? throw new ConflictException(NoDraftCode, "The agent profile has no draft to try: save one first.");
        return new AnswerTrial(draft.Id, draft.VersionNumber, draft.Content);
    }
}

/// <summary>
/// #4169 (6c-3): una domanda alla pipeline delle risposte, con la bozza al posto della versione
/// pubblicata. Chi la manda è un admin, e la domanda non è sua: non conta la quota del piano.
/// </summary>
internal sealed record TryAgentProfileDraftQuery(
    AnswerTrial Trial,
    Guid UserId,
    string UserRole,
    Guid GameId,
    string Question,
    string? AcceptLanguage) : IStreamingQuery<RagStreamingEvent>;

internal sealed class TryAgentProfileDraftQueryHandler : IStreamingQueryHandler<TryAgentProfileDraftQuery, RagStreamingEvent>
{
    private readonly IAnswerPipeline _pipeline;

    public TryAgentProfileDraftQueryHandler(IAnswerPipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    public IAsyncEnumerable<RagStreamingEvent> Handle(TryAgentProfileDraftQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _pipeline.StreamAsync(
            new AnswerRequest(
                GameId: query.GameId.ToString(),
                Query: query.Question,
                UserId: query.UserId,
                UserRole: query.UserRole,
                AcceptLanguage: query.AcceptLanguage,
                Trial: query.Trial),
            cancellationToken);
    }
}
