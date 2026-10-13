using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>
/// La forma di uno scenario. Il set arriva dal corpo di una richiesta e il binding lascia a <c>null</c> ciò
/// che manca: senza questo controllo chi scrive gli scenari riceverebbe un 500 muto invece del nome dello
/// scenario e del campo sbagliato. Un nome di strumento o un tipo di proposta sconosciuti renderebbero lo
/// scenario impossibile da superare, in silenzio.
/// </summary>
internal static class AssistantScenarioRules
{
    private static readonly string[] Languages = ["it", "en"];

    private static readonly string[] Tools =
    [
        SessionAssistantToolset.StatoPartita, SessionAssistantToolset.CercaRegole,
        SessionAssistantToolset.ProponiPunteggio, SessionAssistantToolset.ProponiTurno,
    ];

    /// <summary>Il primo problema di forma dello scenario, o <c>null</c> se è ben formato.</summary>
    public static string? Problem(AssistantScenario? scenario)
    {
        if (scenario is null)
        {
            return "A scenario is null.";
        }

        var problem = HeaderProblem(scenario) ?? StateProblem(scenario.State) ?? ExpectationProblem(scenario.Expected);
        return problem is null ? null : $"Scenario '{(string.IsNullOrWhiteSpace(scenario.Id) ? "(no id)" : scenario.Id)}': {problem}";
    }

    private static string? HeaderProblem(AssistantScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Id))
        {
            return "'id' is required.";
        }

        if (!Languages.Contains(scenario.Language, StringComparer.Ordinal))
        {
            return "'language' must be 'it' or 'en'.";
        }

        if (string.IsNullOrWhiteSpace(scenario.Utterance))
        {
            return "'utterance' is required.";
        }

        return scenario.Passages is null || scenario.Passages.Any(p => p is null || string.IsNullOrWhiteSpace(p.Text))
            ? "'passages' is required (it may be empty), and every passage needs a text."
            : null;
    }

    private static string? StateProblem(AssistantScenarioState? state)
    {
        if (state is null)
        {
            return "'state' is required.";
        }

        if (string.IsNullOrWhiteSpace(state.Game) || string.IsNullOrWhiteSpace(state.Status))
        {
            return "'state.game' and 'state.status' are required.";
        }

        if (state.Players is null || state.Players.Any(string.IsNullOrWhiteSpace))
        {
            return "'state.players' is required, without empty names.";
        }

        if (state.Dimensions is null || state.Dimensions.Any(string.IsNullOrWhiteSpace))
        {
            return "'state.dimensions' is required, without empty names.";
        }

        if (state.Scores is null
            || state.Scores.Any(s => s is null || string.IsNullOrWhiteSpace(s.Player) || string.IsNullOrWhiteSpace(s.Dimension)))
        {
            return "'state.scores' is required (it may be empty), and every score needs a player and a dimension.";
        }

        return state.HouseRules is null ? "'state.houseRules' is required (it may be empty)." : null;
    }

    private static string? ExpectationProblem(AssistantExpectation? expected)
    {
        if (expected is null)
        {
            return "'expected' is required.";
        }

        if (expected.Tools is null || expected.Proposals is null)
        {
            return "'expected.tools' and 'expected.proposals' are required (they may be empty).";
        }

        var unknownTool = expected.Tools.FirstOrDefault(t => !Tools.Contains(t, StringComparer.Ordinal));
        if (unknownTool is not null || expected.Tools.Any(t => t is null))
        {
            return $"unknown tool '{unknownTool}' in 'expected.tools'.";
        }

        return expected.Proposals.Select(ProposalProblem).FirstOrDefault(p => p is not null);
    }

    private static string? ProposalProblem(ExpectedProposal? proposal) => proposal switch
    {
        null => "a proposal in 'expected.proposals' is null.",
        { Kind: ExpectedProposal.AdvanceTurnKind } => null,
        { Kind: ExpectedProposal.ScoreKind } => string.IsNullOrWhiteSpace(proposal.Player)
            || string.IsNullOrWhiteSpace(proposal.Dimension) || proposal.Round is null || proposal.Points is null
                ? "a 'Score' proposal needs player, dimension, round and points."
                : null,
        _ => $"unknown proposal kind '{proposal.Kind}' (use '{ExpectedProposal.ScoreKind}' or '{ExpectedProposal.AdvanceTurnKind}').",
    };
}
