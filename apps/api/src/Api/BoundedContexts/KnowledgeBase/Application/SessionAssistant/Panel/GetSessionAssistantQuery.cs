using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - the panel query, its handler and what it returns
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Panel;

/// <summary>
/// Se l'assistente risponde e, se no, perché: lo stesso codice che darebbe il turno
/// (<c>assistant_disabled</c>, <c>assistant_model_without_tools</c> o <c>session_state_invalid</c>).
/// </summary>
internal sealed record SessionAssistantAvailabilityDto(bool Enabled, string? Code);

/// <summary>
/// Un messaggio della conversazione: <c>Role</c> è <c>user</c> o <c>assistant</c>. <c>Citations</c> sono le fonti
/// di una risposta che ha consultato il regolamento (#4411), già filtrate per tier.
/// </summary>
internal sealed record SessionAssistantMessageDto(
    Guid Id, string Role, string Content, DateTime CreatedAt, IReadOnlyList<CitationDto>? Citations = null);

/// <summary>
/// Ciò che il pannello dell'assistente mostra al montaggio e dopo un ricaricamento (#4372): disponibilità,
/// conversazione e proposte del thread, nell'ordine in cui sono nate. Una proposta porta il
/// <c>MessageId</c> del messaggio dell'assistente che l'ha prodotta. <c>RulesLoaded</c> (#4729) dice se il gioco
/// della sessione ha un regolamento indicizzato che <c>cerca_regole</c> può consultare: <c>false</c> se no,
/// <c>null</c> se la sessione non ha un gioco.
/// </summary>
internal sealed record SessionAssistantPanelDto(
    SessionAssistantAvailabilityDto Availability,
    IReadOnlyList<SessionAssistantMessageDto> Messages,
    IReadOnlyList<AssistantProposalDto> Proposals,
    bool? RulesLoaded = null);

/// <summary>
/// La lettura del pannello dell'assistente in partita (#4372). Solo il proprietario della sessione, come il
/// turno: il filtro degli endpoint live controlla la partecipazione, il proprietario lo controlla qui.
/// <c>Language</c> è quella dell'interfaccia: decide il prompt, quindi anche se l'assistente risponde.
/// </summary>
internal sealed record GetSessionAssistantQuery(Guid LiveSessionId, Guid UserId, string? Language)
    : IQuery<SessionAssistantPanelDto>;

internal sealed class GetSessionAssistantQueryHandler : IQueryHandler<GetSessionAssistantQuery, SessionAssistantPanelDto>
{
    private readonly IAssistantSessionDirectory _directory;
    private readonly SessionAssistantAvailability _availability;
    private readonly IChatThreadRepository _threads;
    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantRulesReadiness _rules;

    public GetSessionAssistantQueryHandler(
        IAssistantSessionDirectory directory,
        SessionAssistantAvailability availability,
        IChatThreadRepository threads,
        IAssistantProposalRepository proposals,
        IAssistantRulesReadiness rules)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public async Task<SessionAssistantPanelDto> Handle(GetSessionAssistantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _directory.GetAccessAsync(query.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != query.UserId)
        {
            throw new ForbiddenException("Only the session host can read the session assistant.");
        }

        var availability = await AvailabilityAsync(access, query.Language, cancellationToken).ConfigureAwait(false);
        // The notice only means something while the assistant answers: with it off or the game over, the panel
        // shows the reason instead and ignores the flag, so the probe's queries are not spent.
        bool? rulesLoaded = availability.Enabled
            ? await _rules.HasIndexedRulesAsync(access.GameId, cancellationToken).ConfigureAwait(false)
            : null;

        var thread = await _threads.FindByLiveSessionIdAsync(query.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (thread is null)
        {
            return new SessionAssistantPanelDto(availability, [], [], rulesLoaded);
        }

        var messages = thread.Messages
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.SequenceNumber)
            .Select(m => new SessionAssistantMessageDto(m.Id, m.Role, m.Content, m.Timestamp, AssistantCitations.Read(m.CitationsJson)))
            .ToList();
        var proposals = await _proposals.ListByThreadAsync(thread.Id, cancellationToken).ConfigureAwait(false);
        return new SessionAssistantPanelDto(
            availability, messages, proposals.Select(AssistantProposalDto.From).ToList(), rulesLoaded);
    }

    /// <summary>Nell'ordine dei controlli del turno: prima lo stato della sessione, poi il profilo.</summary>
    private async Task<SessionAssistantAvailabilityDto> AvailabilityAsync(
        AssistantSessionAccess access, string? language, CancellationToken cancellationToken)
    {
        if (!access.AcceptsQuestions)
        {
            return new SessionAssistantAvailabilityDto(Enabled: false, AssistantTurnRunner.SessionStateInvalidCode);
        }

        var result = await _availability.CheckAsync(language, cancellationToken).ConfigureAwait(false);
        return new SessionAssistantAvailabilityDto(result.Enabled, result.Code);
    }
}
