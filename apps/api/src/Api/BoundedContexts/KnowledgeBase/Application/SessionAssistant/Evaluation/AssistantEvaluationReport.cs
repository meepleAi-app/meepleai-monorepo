namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Le metriche dello spike per un modello (spec §7.1).</summary>
internal sealed record AssistantEvaluationReport(
    string Model,
    int Scenarios,
    int Passed,
    int ScenariosWithExtraProposals,
    int ScenariosWithMissingProposals,
    int ClaimedActionsWithoutProposal,
    int ToolErrors,
    int InvalidArguments,
    int GatewayErrors,
    double AverageRoundTrips,
    decimal AverageCostPerTurnUsd,
    long LatencyP95Ms,
    IReadOnlyList<AssistantScenarioResult> Results)
{
    public static AssistantEvaluationReport From(string model, IReadOnlyList<AssistantScenarioResult> results)
    {
        var latencies = results.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var p95 = latencies.Count == 0 ? 0 : latencies[Math.Max(0, (int)Math.Ceiling(latencies.Count * 0.95) - 1)];
        return new AssistantEvaluationReport(
            model,
            results.Count,
            results.Count(r => r.Passed),
            results.Count(r => r.ExtraProposals > 0),
            results.Count(r => r.MissingProposals > 0),
            results.Count(r => r.ClaimedActionWithoutProposal),
            results.Sum(r => r.ToolErrors),
            results.Sum(r => r.InvalidArguments),
            results.Count(r => r.ErrorCode is not null),
            results.Count == 0 ? 0 : results.Average(r => r.RoundTrips),
            results.Count == 0 ? 0 : results.Average(r => r.CostUsd),
            p95,
            results);
    }
}
