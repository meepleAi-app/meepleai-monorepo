using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;
using Microsoft.Extensions.AI;

#pragma warning disable MA0048 // File name must match type name - evaluator and its per-scenario result
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

internal sealed record AssistantScenarioResult(
    string Id,
    string Category,
    bool Passed,
    IReadOnlyList<string> ToolsCalled,
    IReadOnlyList<string> MissingTools,
    int ExtraProposals,
    int MissingProposals,
    bool ClaimedActionWithoutProposal,
    int ToolErrors,
    int InvalidArguments,
    int RoundTrips,
    decimal CostUsd,
    long LatencyMs,
    string? ErrorCode,
    string FinalText);

/// <summary>
/// Esegue uno scenario con il ciclo vero (spec §7.1): stessa catena, stessi strumenti e stesso prompt
/// della produzione; cambiano solo il lettore (dallo scenario) e il sink (che ricorda).
/// </summary>
internal sealed class SessionAssistantEvaluator(
    IAssistantChatClientFactory factory, IAssistantPlayerMatcher matcher, ILogger<SessionAssistantEvaluator> logger)
{
    public Task<AssistantScenarioResult> RunAsync(
        AssistantScenario scenario, AssistantModelSettings settings, AssistantPromptSet prompts, CancellationToken cancellationToken) =>
        RunAsync(scenario, settings, prompts, offerRules: true, cancellationToken);

    /// <summary>
    /// Come sopra. Con <paramref name="offerRules"/> falso <c>cerca_regole</c> non viene offerto a nessuno
    /// scenario, come nel turno finché la fetta A5 non lo consegna (#4270): così si misura il testo da
    /// pubblicare con gli strumenti che riceverà davvero.
    /// </summary>
    public async Task<AssistantScenarioResult> RunAsync(
        AssistantScenario scenario,
        AssistantModelSettings settings,
        AssistantPromptSet prompts,
        bool offerRules,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(scenario);
        var turn = new Turn();
        string? errorCode = null;

#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: uno scenario che esplode, nel turno o nel suo giudizio, diventa un risultato con
        // il codice, non la perdita dell'intero report del modello. L'eccezione resta nei log.
        try
        {
            await ExecuteAsync(scenario, settings, prompts, offerRules, turn, cancellationToken).ConfigureAwait(false);
        }
        catch (AssistantGatewayException ex)
        {
            errorCode = ex.ErrorCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Session assistant evaluation: scenario {ScenarioId} failed while running", scenario.Id);
            errorCode = ErrorCodeFor(ex);
        }

        turn.Stopwatch.Stop();
        try
        {
            return Grade(scenario, turn, errorCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Session assistant evaluation: scenario {ScenarioId} could not be graded", scenario.Id);
            return Ungraded(scenario, turn, errorCode ?? ErrorCodeFor(ex));
        }
#pragma warning restore CA1031
    }

    private async Task ExecuteAsync(
        AssistantScenario scenario, AssistantModelSettings settings, AssistantPromptSet prompts, bool offerRules, Turn turn,
        CancellationToken cancellationToken)
    {
        var reader = new ScenarioSessionAssistantReader(scenario);
        var state = await reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        var rules = offerRules && state.GameId is not null ? new ScenarioRuleSource(scenario) : null;
        turn.Toolset = new SessionAssistantToolset(reader, turn.Sink, matcher, rules);
        var messages = AssistantConversation.Build(prompts, scenario.Language, state, [], scenario.Utterance);

        using var chat = factory.Create(settings, turn.Meter);
        turn.Response = await chat.GetResponseAsync(
            messages, new ChatOptions { Tools = turn.Toolset.CreateTools() }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static AssistantScenarioResult Grade(AssistantScenario scenario, Turn turn, string? errorCode)
    {
        // Dal meter e non dai messaggi della risposta: restano anche quando il ciclo si ferma a metà.
        var toolsCalled = turn.Meter.ToolCalls;
        var finalText = turn.Response?.Text ?? string.Empty;
        var (extra, missing) = Compare(scenario.Expected.Proposals, turn.Sink.Proposals);
        var missingTools = scenario.Expected.Tools.Where(t => !toolsCalled.Contains(t, StringComparer.Ordinal)).ToList();
        var claimed = turn.Sink.Proposals.Count == 0
            && (ClaimedActionDetector.ClaimsAction(finalText) || ClaimedActionDetector.ClaimsProposal(finalText));

        return new AssistantScenarioResult(
            scenario.Id,
            scenario.Category,
            Passed: errorCode is null && extra == 0 && missing == 0 && missingTools.Count == 0 && !claimed,
            toolsCalled,
            missingTools,
            extra,
            missing,
            claimed,
            turn.Toolset?.ErrorCount ?? 0,
            turn.Meter.InvalidArguments,
            turn.Meter.RoundTrips,
            turn.Meter.CostUsd,
            turn.Stopwatch.ElapsedMilliseconds,
            errorCode,
            finalText);
    }

    /// <summary>Il risultato di uno scenario che non si è potuto giudicare: restano i contatori del turno.</summary>
    private static AssistantScenarioResult Ungraded(AssistantScenario scenario, Turn turn, string errorCode) => new(
        scenario.Id,
        scenario.Category,
        Passed: false,
        turn.Meter.ToolCalls,
        MissingTools: [],
        ExtraProposals: 0,
        MissingProposals: 0,
        ClaimedActionWithoutProposal: false,
        turn.Toolset?.ErrorCount ?? 0,
        turn.Meter.InvalidArguments,
        turn.Meter.RoundTrips,
        turn.Meter.CostUsd,
        turn.Stopwatch.ElapsedMilliseconds,
        errorCode,
        turn.Response?.Text ?? string.Empty);

    private static string ErrorCodeFor(Exception ex) => $"evaluator_error:{ex.GetType().Name}";

    /// <summary>Confronto senza ordine: ogni proposta attesa consuma al più una proposta fatta.</summary>
    private static (int Extra, int Missing) Compare(IReadOnlyList<ExpectedProposal> expected, IReadOnlyList<ExpectedProposal> actual)
    {
        var remaining = actual.ToList();
        var missing = 0;
        foreach (var wanted in expected)
        {
            var match = remaining.FindIndex(a => Same(wanted, a));
            if (match < 0)
            {
                missing++;
            }
            else
            {
                remaining.RemoveAt(match);
            }
        }

        return (remaining.Count, missing);
    }

    private static bool Same(ExpectedProposal a, ExpectedProposal b) =>
        string.Equals(a.Kind, b.Kind, StringComparison.Ordinal)
        && string.Equals(a.Player, b.Player, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Dimension, b.Dimension, StringComparison.OrdinalIgnoreCase)
        && a.Round == b.Round
        && a.Points == b.Points;

    /// <summary>Ciò che un turno lascia dietro di sé, anche quando si ferma a metà.</summary>
    private sealed class Turn
    {
        public RecordingProposalSink Sink { get; } = new();

        public ChatTurnMeter Meter { get; } = new();

        public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

        public SessionAssistantToolset? Toolset { get; set; }

        public ChatResponse? Response { get; set; }
    }
}
