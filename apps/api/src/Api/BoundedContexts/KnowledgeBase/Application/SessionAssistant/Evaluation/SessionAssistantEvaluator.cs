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
internal sealed class SessionAssistantEvaluator(IAssistantChatClientFactory factory, IAssistantPlayerMatcher matcher)
{
    public async Task<AssistantScenarioResult> RunAsync(
        AssistantScenario scenario, AssistantModelSettings settings, CancellationToken cancellationToken)
    {
        var reader = new ScenarioSessionAssistantReader(scenario);
        var sink = new RecordingProposalSink();
        var toolset = new SessionAssistantToolset(reader, sink, matcher);
        var state = await reader.GetStateAsync(cancellationToken).ConfigureAwait(false);
        var messages = AssistantConversation.Build(scenario.Language, state, [], scenario.Utterance);
        var meter = new ChatTurnMeter();
        var stopwatch = Stopwatch.StartNew();

        ChatResponse? response = null;
        string? errorCode = null;
        try
        {
            using var chat = factory.Create(settings, meter);
            response = await chat.GetResponseAsync(
                messages, new ChatOptions { Tools = toolset.CreateTools(includeRules: state.GameId is not null) }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AssistantGatewayException ex)
        {
            errorCode = ex.ErrorCode;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: uno scenario che esplode diventa un risultato con il codice, non la perdita
        // dell'intero report del modello.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorCode = $"evaluator_error:{ex.GetType().Name}";
        }
#pragma warning restore CA1031

        stopwatch.Stop();
        // Dal meter e non dai messaggi della risposta: restano anche quando il ciclo si ferma a metà.
        var toolsCalled = meter.ToolCalls;
        var finalText = response?.Text ?? string.Empty;
        var (extra, missing) = Compare(scenario.Expected.Proposals, sink.Proposals);
        var missingTools = scenario.Expected.Tools.Where(t => !toolsCalled.Contains(t, StringComparer.Ordinal)).ToList();
        var claimed = sink.Proposals.Count == 0
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
            toolset.ErrorCount,
            meter.InvalidArguments,
            meter.RoundTrips,
            meter.CostUsd,
            stopwatch.ElapsedMilliseconds,
            errorCode,
            finalText);
    }

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
}
