

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

internal record RuleAtom(string id, string text, string? section = null, string? page = null, string? line = null);

internal record RuleSpec(string gameId, string version, DateTime createdAt, IReadOnlyList<RuleAtom> rules);
