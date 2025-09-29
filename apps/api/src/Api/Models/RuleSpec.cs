namespace Api.Models;

public record RuleAtom(string id, string text, string? section=null, string? page=null, string? line=null);

public record RuleSpec(string gameId, string version, DateTime createdAt, IReadOnlyList<RuleAtom> rules);
