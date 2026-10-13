using System.Threading.Channels;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
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
/// lettore e un sink di proposte, e questa classe non ha né il mediator né la porta di scrittura.
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
    public const string DisabledCode = "assistant_disabled";
    public const string SessionStateInvalidCode = "session_state_invalid";
    public const string BudgetPausedCode = "budget_paused";
    public const string UnavailableCode = "assistant_unavailable";
    public const string TimeoutCode = "turn_timeout";

    private readonly IAssistantSessionDirectory _directory;
    private readonly IPublishedAgentProfileReader _profiles;
    private readonly IToolCallingModelCatalog _catalog;
    private readonly IChatThreadRepository _threads;
    private readonly IAssistantProposalRepository _proposals;
    private readonly IAssistantChatClientFactory _chatClients;
    private readonly IAssistantPlayerMatcher _matcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssistantTurnRunner> _logger;

    public AssistantTurnRunner(
        IAssistantSessionDirectory directory,
        IPublishedAgentProfileReader profiles,
        IToolCallingModelCatalog catalog,
        IChatThreadRepository threads,
        IAssistantProposalRepository proposals,
        IAssistantChatClientFactory chatClients,
        IAssistantPlayerMatcher matcher,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<AssistantTurnRunner> logger)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _threads = threads ?? throw new ArgumentNullException(nameof(threads));
        _proposals = proposals ?? throw new ArgumentNullException(nameof(proposals));
        _chatClients = chatClients ?? throw new ArgumentNullException(nameof(chatClients));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
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

        var profile = await _profiles.GetAsync(cancellationToken).ConfigureAwait(false);
        var content = profile.Content;
        var assistant = content.SessionAssistant;
        var language = string.IsNullOrWhiteSpace(command.Language) ? content.FallbackLanguage : command.Language.Trim();
        var prompt = assistant.PromptFor(language) ?? assistant.PromptFor(content.FallbackLanguage);
        if (!assistant.Enabled || prompt is null)
        {
            throw new ConflictException(DisabledCode, "The session assistant is not enabled in the published agent profile.");
        }

        // La pubblicazione applica la stessa regola, ma una migrazione di dati la scavalca e il catalogo
        // può cambiare dopo: si ricontrolla a ogni turno.
        var toolCallingModels = await _catalog.GetToolCallingModelIdsAsync(cancellationToken).ConfigureAwait(false);
        if (content.SessionAssistantModelProblem(toolCallingModels) is { } problem)
        {
            throw new ConflictException(AgentProfile.AssistantModelWithoutToolsCode, problem);
        }

        return new PreparedAssistantTurn(command, profile, language, prompt);
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
        var toolset = new SessionAssistantToolset(reader, sink, _matcher);
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
        var text = FinalText(outcome, sink.Created.Count, turn.Language);

        thread.AddUserMessage(command.Question);
        var answer = new DomainChatMessage(text, DomainChatMessage.AssistantRole, thread.Messages.Count, id: messageId);
        answer.SetAnswerProvenance(turn.Profile.VersionId, turn.Language);
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

        events.TryWrite(AssistantTurnEvent.Answer(text));
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
            // cerca_regole arriva con la fetta A5: fino ad allora non è offerto.
            var response = await chat.GetResponseAsync(
                messages, new ChatOptions { Tools = toolset.CreateTools(includeRules: false) }, linked.Token)
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

    private static string FinalText(TurnOutcome outcome, int proposals, string language)
    {
        var english = AssistantLanguage.IsEnglish(language);
        if (outcome.ErrorCode is not null)
        {
            return AssistantTurnTexts.ForError(outcome.ErrorCode, english, hasProposals: proposals > 0);
        }

        if (string.IsNullOrWhiteSpace(outcome.Text))
        {
            return AssistantTurnTexts.NoAnswer(english, hasProposals: proposals > 0);
        }

        var text = outcome.Text.Trim();
        if (text.Length > MaxAnswerLength)
        {
            text = text[..MaxAnswerLength];
        }

        // Spec §6.3: il modello dice di aver registrato, o di aver preparato una proposta, e nel turno non
        // ce n'è nessuna. L'host cercherebbe un punteggio o una card che non esistono.
        var claimed = proposals == 0
            && (ClaimedActionDetector.ClaimsAction(text) || ClaimedActionDetector.ClaimsProposal(text));
        return claimed ? text + AssistantTurnTexts.NothingRecorded(english) : text;
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

    /// <summary>Dei turni passati tornano al modello solo il testo e gli esiti delle proposte (spec §6.2).</summary>
    private async Task<IReadOnlyList<AssistantHistoryTurn>> BuildHistoryAsync(ChatThread thread, CancellationToken cancellationToken)
    {
        if (thread.Messages.Count == 0)
        {
            return [];
        }

        var proposals = await _proposals.ListByThreadAsync(thread.Id, cancellationToken).ConfigureAwait(false);
        var byMessage = proposals.ToLookup(p => p.MessageId);
        return thread.Messages
            .Where(m => !m.IsDeleted)
            .TakeLast(HistoryMessages)
            .Select(m => new AssistantHistoryTurn(m.Role, WithOutcomes(m.Content, byMessage[m.Id])))
            .ToList();
    }

    private static string WithOutcomes(string text, IEnumerable<AssistantProposal> proposals)
    {
        var lines = proposals
            .Select(p => $"[{AssistantProposalSummary.For(p)}: {AssistantProposalSummary.Outcome(p.Status)}]")
            .ToList();
        return lines.Count == 0 ? text : $"{text}\n{string.Join('\n', lines)}";
    }

    private sealed record TurnOutcome(string? Text, string? ErrorCode);
}
