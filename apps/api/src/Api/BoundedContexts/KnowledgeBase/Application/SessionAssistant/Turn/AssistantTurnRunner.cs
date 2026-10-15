using System.Threading.Channels;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using DomainChatMessage = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

#pragma warning disable MA0048 // File name must match type name - the runner and the turn it prepares
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>Un turno che ha superato i controlli: chi chiede, con quale versione del profilo, in che lingua.</summary>
internal sealed record PreparedAssistantTurn(
    AskSessionAssistantCommand Command, PublishedAgentProfile Profile, string Language, string SystemPrompt);

/// <summary>
/// Un turno dell'assistente in partita (spec 2026-10-11 §6.2): controlli, ciclo con gli strumenti,
/// salvataggio di messaggi e proposte, eventi. Non scrive mai la partita: gli strumenti ricevono un
/// lettore e un sink di proposte, e questa classe non ha né il mediator né la porta di scrittura. Da A5 (#4411) la
/// risposta del modello passa da <c>FinalizeAsync</c> quando il turno ha consultato il regolamento.
/// </summary>
internal sealed class AssistantTurnRunner
{
    public const int MaxQuestionLength = 2000;

    /// <summary>Un tag di lingua («it», «en-US»): arriva dal client e finisce in ogni messaggio salvato.</summary>
    public const int MaxLanguageLength = 16;

    /// <summary>Sotto il limite di un messaggio del thread (10.000), con il margine per l'avviso in coda.</summary>
    public const int MaxAnswerLength = 9000;

    /// <summary>Quanti messaggi passati tornano al modello: sei scambi.</summary>
    public const int HistoryMessages = 12;

    public const string ThreadAgentType = "session-assistant";
    public const string DisabledCode = SessionAssistantAvailability.DisabledCode;
    public const string SessionStateInvalidCode = "session_state_invalid";
    public const string BudgetPausedCode = "budget_paused";
    public const string UnavailableCode = "assistant_unavailable";
    public const string TimeoutCode = "turn_timeout";

    /// <summary>#4397: il segno della risposta scritta dal turno quando il modello non ha dato testo.</summary>
    public const string NoAnswerCode = "no_answer";

    /// <summary>
    /// #4411: il segno della risposta sostituita dal messaggio di copyright, perché il modello aveva copiato un
    /// passaggio protetto. Il testo l'ha scritto il turno, quindi non torna al modello nella storia (#4397).
    /// </summary>
    public const string CopyrightSanitizedCode = "copyright_sanitized";

    private readonly IAssistantSessionDirectory _directory;
    private readonly SessionAssistantAvailability _availability;
    private readonly IChatThreadRepository _threads;
    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantChatClientFactory _chatClients;
    private readonly IAssistantPlayerMatcher _matcher;
    private readonly IAssistantRuleSourceFactory _ruleSources;
    private readonly IGroundedAnswerService _groundedAnswers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssistantTurnRunner> _logger;

    public AssistantTurnRunner(
        IAssistantSessionDirectory directory,
        SessionAssistantAvailability availability,
        IChatThreadRepository threads,
        IAssistantProposalRepository proposals,
        IAssistantChatClientFactory chatClients,
        IAssistantPlayerMatcher matcher,
        IAssistantRuleSourceFactory ruleSources,
        IGroundedAnswerService groundedAnswers,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<AssistantTurnRunner> logger)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _chatClients = chatClients ?? throw new ArgumentNullException(nameof(chatClients));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _ruleSources = ruleSources ?? throw new ArgumentNullException(nameof(ruleSources));
        _groundedAnswers = groundedAnswers ?? throw new ArgumentNullException(nameof(groundedAnswers));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// I controlli di §6.2, passo 1. Lancia le eccezioni che il middleware traduce in stato HTTP: nessun
    /// evento è ancora partito.
    /// </summary>
    public async Task<PreparedAssistantTurn> PrepareAsync(AskSessionAssistantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Question) || command.Question.Length > MaxQuestionLength)
        {
            throw new ValidationException(
                nameof(command.Question), $"The question is required, up to {MaxQuestionLength} characters.");
        }

        if (command.Language is { Length: > MaxLanguageLength })
        {
            throw new ValidationException(
                nameof(command.Language), $"The language tag is at most {MaxLanguageLength} characters.");
        }

        var access = await _directory.GetAccessAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        if (access.OwnerUserId != command.UserId)
        {
            throw new ForbiddenException("Only the session host can talk to the session assistant.");
        }

        if (!access.AcceptsQuestions)
        {
            throw new ConflictException(
                SessionStateInvalidCode,
                $"The session is {access.Status}: the assistant no longer answers once the game is over.");
        }

        // #4372: la stessa regola che legge il pannello, così non può dire «disponibile» a un turno che
        // risponde 409.
        var availability = await _availability.CheckAsync(command.Language, cancellationToken).ConfigureAwait(false);
        if (availability.Code is { } code)
        {
            throw new ConflictException(code, availability.Problem!);
        }

        return new PreparedAssistantTurn(command, availability.Profile, availability.Language, availability.SystemPrompt!);
    }

    /// <summary>
    /// Esegue il turno e scrive i suoi eventi. Non lancia, se non per annullamento: un errore imprevisto
    /// diventa un evento <c>error</c>, e il canale viene sempre chiuso.
    /// </summary>
    public async Task RunAsync(PreparedAssistantTurn turn, ChannelWriter<AssistantTurnEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(events);
        try
        {
            await ExecuteAsync(turn, events, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: lo stream è già partito, quindi un'eccezione qui troncherebbe la connessione
        // senza dire nulla all'host. Diventa un evento con un codice; la causa resta nei log. Passa solo
        // l'annullamento della richiesta: un OperationCanceledException nato altrove è un errore come gli
        // altri, e lasciarlo uscire chiuderebbe lo stream in silenzio.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Session assistant: the turn failed for live session {LiveSessionId}", turn.Command.LiveSessionId);
            events.TryWrite(AssistantTurnEvent.Error(UnavailableCode));
        }
#pragma warning restore CA1031
        finally
        {
            events.TryComplete();
        }
    }

    private async Task ExecuteAsync(PreparedAssistantTurn turn, ChannelWriter<AssistantTurnEvent> events, CancellationToken cancellationToken)
    {
        var command = turn.Command;
        var content = turn.Profile.Content;
        var assistant = content.SessionAssistant;

        var thread = await _threads.FindByLiveSessionIdAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        var isNewThread = thread is null;
        thread ??= new ChatThread(
            Guid.NewGuid(), command.UserId, gameId: null, title: null, agentId: null, agentType: ThreadAgentType,
            liveSessionId: command.LiveSessionId);

        await ExpireEarlierProposalsAsync(command.LiveSessionId, cancellationToken).ConfigureAwait(false);
        var history = await BuildHistoryAsync(thread, cancellationToken).ConfigureAwait(false);

        var reader = _directory.CreateReader(command.LiveSessionId, command.UserId);
        var state = await reader.GetStateAsync(cancellationToken).ConfigureAwait(false);

        // L'id del messaggio dell'assistente nasce prima del ciclo: le proposte lo citano.
        var messageId = Guid.NewGuid();
        var sink = new RepositoryProposalSink(
            _proposals, command.LiveSessionId, thread.Id, messageId, turn.Profile.VersionId, _timeProvider);
        // Spec §4.2: cerca_regole solo se la sessione ha un gioco. Per chi chiede, con il suo ruolo, e con i numeri
        // di recupero della versione del profilo di questo turno.
        var rules = state.GameId is { } gameId
            ? _ruleSources.Create(new AssistantRuleScope(
                gameId, command.UserId, command.UserRole, content, AssistantLanguage.Code(turn.Language)))
            : null;
        var toolset = new SessionAssistantToolset(reader, sink, _matcher, rules);
        var meter = new ChatTurnMeter { ToolCalled = tool => events.TryWrite(AssistantTurnEvent.Status(tool)) };
        var settings = new AssistantModelSettings(
            content.PrimaryModelId,
            assistant.UseReserveModel ? content.FallbackModelId : null,
            (double)assistant.Temperature,
            assistant.MaxResponseTokens,
            assistant.MaxToolIterations,
            turn.Profile.VersionId,
            RequestSource.SessionAssistant);
        var messages = AssistantConversation.Build(
            new AssistantPromptSet(turn.SystemPrompt, turn.SystemPrompt), turn.Language, state, history, command.Question);

        var outcome = await AskModelAsync(settings, meter, messages, toolset, assistant.TurnTimeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        var composed = await ComposeAnswerAsync(
            outcome, toolset.Citations, sink.Created.Count, turn.Language, command.Question, cancellationToken)
            .ConfigureAwait(false);

        thread.AddUserMessage(command.Question);
        var answer = new DomainChatMessage(composed.Text, DomainChatMessage.AssistantRole, thread.Messages.Count, id: messageId);
        answer.SetAnswerProvenance(turn.Profile.VersionId, turn.Language);
        if (composed.FallbackCode is { } fallbackCode)
        {
            answer.MarkAsFallback(fallbackCode);
        }

        if (composed.Citations.Count > 0)
        {
            answer.SetAgentMetadata(citationsJson: AssistantCitations.Write(composed.Citations));
        }

        thread.AddMessage(answer);
        if (isNewThread)
        {
            await _threads.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _threads.UpdateAsync(thread, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Solo adesso le card: una proposta mostrata prima del salvataggio si potrebbe confermare quando
        // nel database non c'è ancora.
        foreach (var proposal in sink.Created)
        {
            events.TryWrite(AssistantTurnEvent.ForProposal(AssistantProposalDto.From(proposal)));
        }

        events.TryWrite(AssistantTurnEvent.Answer(composed.Text, composed.Citations));
        events.TryWrite(outcome.ErrorCode is null
            ? AssistantTurnEvent.Done(messageId, turn.Profile.VersionId, turn.Profile.VersionNumber)
            : AssistantTurnEvent.Error(outcome.ErrorCode));
    }

    /// <summary>
    /// Il ciclo, con la scadenza del turno. Tetto, provider e scadenza non sono eccezioni per chi chiama:
    /// sono un esito con un codice, perché le proposte già create vanno comunque salvate (spec §6.3).
    /// </summary>
    private async Task<TurnOutcome> AskModelAsync(
        AssistantModelSettings settings,
        ChatTurnMeter meter,
        List<ChatMessage> messages,
        SessionAssistantToolset toolset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        // La scadenza passa dal TimeProvider: nei test è virtuale, e non gareggia con il carico della suite.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds), _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var chat = _chatClients.Create(settings, meter);
            var response = await chat.GetResponseAsync(
                messages, new ChatOptions { Tools = toolset.CreateTools() }, linked.Token)
                .ConfigureAwait(false);

            // L'ultimo messaggio dell'assistente, non la concatenazione: i giri intermedi possono portare
            // un testo accanto alle chiamate a strumenti.
            var last = response.Messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
            return new TurnOutcome(last?.Text, ErrorCode: null);
        }
        catch (AssistantGatewayException ex)
        {
            _logger.LogWarning(ex, "Session assistant: the gateway stopped the turn with {ErrorCode}", ex.ErrorCode);
            var paused = string.Equals(ex.ErrorCode, LlmBudgetAdmission.DailyBudgetErrorCode, StringComparison.Ordinal)
                || string.Equals(ex.ErrorCode, LlmBudgetAdmission.MonthlyBudgetErrorCode, StringComparison.Ordinal);
            return new TurnOutcome(null, paused ? BudgetPausedCode : UnavailableCode);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Session assistant: the turn exceeded its {TimeoutSeconds}s deadline", timeoutSeconds);
            return new TurnOutcome(null, TimeoutCode);
        }
    }

    /// <summary>
    /// Il testo della risposta, il suo segno e le sue fonti. Il testo del modello passa da <c>FinalizeAsync</c> con
    /// le citazioni di tutte le chiamate a cerca_regole del turno (spec §4.2); i testi di ripiego no.
    /// </summary>
    private async Task<ComposedAnswer> ComposeAnswerAsync(
        TurnOutcome outcome, IReadOnlyList<ChunkCitation> citations, int proposals, string language, string question,
        CancellationToken cancellationToken)
    {
        var english = AssistantLanguage.IsEnglish(language);
        if (outcome.ErrorCode is { } errorCode)
        {
            return new ComposedAnswer(AssistantTurnTexts.ForError(errorCode, english, proposals > 0), errorCode, []);
        }

        if (string.IsNullOrWhiteSpace(outcome.Text))
        {
            return new ComposedAnswer(AssistantTurnTexts.NoAnswer(english, proposals > 0), NoAnswerCode, []);
        }

        var text = outcome.Text.Trim();
        if (text.Length > MaxAnswerLength)
        {
            text = text[..MaxAnswerLength];
        }

        IReadOnlyList<CitationDto> sources = [];
        if (citations.Count > 0)
        {
            var grounded = await _groundedAnswers
                .FinalizeAsync(text, citations, question, AssistantLanguage.Code(language), cancellationToken)
                .ConfigureAwait(false);
            if (grounded.LeakMatchCount > 0)
            {
                return new ComposedAnswer(grounded.ResponseText, CopyrightSanitizedCode, grounded.CitationDtos);
            }

            text = grounded.ResponseText;
            sources = grounded.CitationDtos;
        }

        // Spec §6.3: il modello dice di aver registrato, o di aver preparato una proposta, e nel turno non ce n'è
        // nessuna. L'host cercherebbe un punteggio o una card che non esistono.
        var claimed = proposals == 0
            && (ClaimedActionDetector.ClaimsAction(text) || ClaimedActionDetector.ClaimsProposal(text));
        return new ComposedAnswer(claimed ? text + AssistantTurnTexts.NothingRecorded(english) : text, FallbackCode: null, sources);
    }

    /// <summary>
    /// Spec §5.2: una proposta ancora <c>Pending</c> scade quando inizia il turno successivo. Una ancora
    /// <c>Applying</c> a questo punto è una conferma rimasta a metà (§5.3): si segnala nei log.
    /// </summary>
    private async Task ExpireEarlierProposalsAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _proposals.ExpirePendingBySessionAsync(liveSessionId, now, cancellationToken).ConfigureAwait(false);

        var stillOpen = await _proposals.ListOpenBySessionAsync(liveSessionId, cancellationToken).ConfigureAwait(false);
        foreach (var proposal in stillOpen.Where(p => p.Status == AssistantProposalStatus.Applying))
        {
            _logger.LogWarning(
                "Session assistant: proposal {ProposalId} of live session {LiveSessionId} is Applying at the start of a new turn: a confirmation is in flight, or one did not complete",
                proposal.Id, liveSessionId);
        }
    }

    /// <summary>
    /// Dei turni passati tornano al modello solo il testo e gli esiti delle proposte (spec §6.2).
    /// </summary>
    /// <remarks>
    /// #4397: torna solo ciò che ha scritto il modello. Il testo che il turno ha scritto al suo posto resta
    /// nella conversazione che vede l'host, ma non nella storia: il modello vedrebbe sé stesso dire «non sono
    /// disponibile» e potrebbe ripeterlo. Se prima del guasto il turno aveva creato proposte, i loro esiti
    /// sono fatti e restano, con la domanda che le ha chieste; senza proposte il turno fallito sparisce,
    /// domanda compresa. Il filtro precede la finestra, che conta solo i messaggi che arrivano al modello.
    /// </remarks>
    private async Task<IReadOnlyList<AssistantHistoryTurn>> BuildHistoryAsync(ChatThread thread, CancellationToken cancellationToken)
    {
        if (thread.Messages.Count == 0)
        {
            return [];
        }

        var proposals = await _proposals.ListByThreadAsync(thread.Id, cancellationToken).ConfigureAwait(false);
        var byMessage = proposals.ToLookup(p => p.MessageId);
        var turns = new List<AssistantHistoryTurn>();
        foreach (var message in thread.Messages.Where(m => !m.IsDeleted))
        {
            var outcomes = Outcomes(byMessage[message.Id]);
            if (message.FallbackCode is null)
            {
                turns.Add(new AssistantHistoryTurn(
                    message.Role, outcomes.Count == 0 ? message.Content : $"{message.Content}\n{string.Join('\n', outcomes)}"));
            }
            else if (outcomes.Count > 0)
            {
                turns.Add(new AssistantHistoryTurn(message.Role, string.Join('\n', outcomes)));
            }
            else if (turns.Count > 0 && string.Equals(turns[^1].Role, DomainChatMessage.UserRole, StringComparison.Ordinal))
            {
                turns.RemoveAt(turns.Count - 1);
            }
        }

        return turns.TakeLast(HistoryMessages).ToList();
    }

    private static List<string> Outcomes(IEnumerable<AssistantProposal> proposals) => proposals
        .Select(p => $"[{AssistantProposalSummary.For(p)}: {AssistantProposalSummary.Outcome(p.Status)}]")
        .ToList();

    private sealed record TurnOutcome(string? Text, string? ErrorCode);

    private sealed record ComposedAnswer(string Text, string? FallbackCode, IReadOnlyList<CitationDto> Citations);
}
