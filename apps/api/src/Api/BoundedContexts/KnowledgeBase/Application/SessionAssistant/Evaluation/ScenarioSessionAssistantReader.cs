using System.Security.Cryptography;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Il lettore finto dello spike: lo stato viene dallo scenario (il regolamento da <see cref="ScenarioRuleSource"/>).</summary>
internal sealed class ScenarioSessionAssistantReader(AssistantScenario scenario) : ISessionAssistantReader
{
    private static readonly Guid GameId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    /// <summary>Un id stabile per nome: SHA-256 troncato (MD5 è segnalato dagli analyzer di sicurezza).</summary>
    public static Guid PlayerId(string name) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant())).AsSpan(0, 16));

    public Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken)
    {
        var s = scenario.State;
        var scores = s.Scores.Select(c => new AssistantScoreCell(PlayerId(c.Player), c.Round, c.Dimension, c.Value)).ToList();
        var players = s.Players
            .Select(p => new AssistantPlayer(PlayerId(p), p, scores.Where(c => c.PlayerId == PlayerId(p)).Sum(c => c.Value), true))
            .ToList();
        return Task.FromResult(new AssistantSessionState(
            s.HasRules ? GameId : null,
            s.Game,
            s.Status,
            CanRecordScores: string.Equals(s.Status, "InProgress", StringComparison.Ordinal)
                || string.Equals(s.Status, "Paused", StringComparison.Ordinal),
            CanAdvanceTurn: string.Equals(s.Status, "InProgress", StringComparison.Ordinal),
            players,
            s.CurrentPlayer is null ? null : PlayerId(s.CurrentPlayer),
            s.CurrentTurnIndex,
            AssistantRound.Current(s.CurrentTurnIndex, players.Count),
            s.Phase,
            s.Dimensions,
            scores,
            s.HouseRules));
    }
}
