using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Heading-rule-based classifier with LLM fallback for ambiguous chunks.
/// Phase D of multi-book gamebook generalization plan (2026-05-19).
/// D2: rule-based heading detection. D3 (next commit): real LLM fallback.
/// </summary>
internal class RoleClassifierService : IRoleClassifierService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<RoleClassifierService> _logger;

    /// <summary>
    /// Ordered heading patterns. Each match contributes one or more flags to the chunk's tag set.
    /// Multi-label allowed (e.g. "Setup > Components" → Tutorial | Setup).
    /// MA0009/MA0023: ExplicitCapture (no capture groups needed for IsMatch) + timeout to prevent ReDoS.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private const RegexOptions HeadingRegexOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture;

    private static readonly (Regex Pattern, GameBookRole Role)[] HeadingRules =
    {
        (
            new Regex(
                @"\b(?:setup|quick\s*start|learn\s*to\s*play|getting\s*started|components|preparazione)\b",
                HeadingRegexOptions,
                RegexTimeout),
            GameBookRole.Tutorial | GameBookRole.Setup),
        (
            new Regex(
                @"\b(?:rules?|reference|combat|magic|phases?|actions?|turn|regole|combattimento)\b",
                HeadingRegexOptions,
                RegexTimeout),
            GameBookRole.RulesReference),
        (
            new Regex(
                @"\b(?:adventures?|chapters?|paragrap?h\s*\d+|§\s*\d+|avventur)",
                HeadingRegexOptions,
                RegexTimeout),
            GameBookRole.Narrative),
        (
            new Regex(
                @"\b(?:encounters?|scenarios?|combat\s*sheet|incontro|scontri)\b",
                HeadingRegexOptions,
                RegexTimeout),
            GameBookRole.Encounter),
        (
            new Regex(
                @"\b(?:lore|background|history|codex|storia)\b",
                HeadingRegexOptions,
                RegexTimeout),
            GameBookRole.Lore),
    };

    public RoleClassifierService(
        ILlmService llmService,
        ILogger<RoleClassifierService> logger)
    {
        ArgumentNullException.ThrowIfNull(llmService);
        ArgumentNullException.ThrowIfNull(logger);
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GameBookRole>> ClassifyAsync(
        IReadOnlyList<ChunkInput> chunks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var results = new GameBookRole[chunks.Count];
        var ambiguous = new List<(int Index, ChunkInput Chunk)>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var role = ApplyRules(chunks[i].HeadingPath);
            if (role != GameBookRole.None)
            {
                results[i] = role;
            }
            else
            {
                ambiguous.Add((i, chunks[i]));
            }
        }

        if (ambiguous.Count > 0)
        {
            _logger.LogDebug(
                "Role classifier: {AmbiguousCount}/{TotalCount} chunks have no heading match; falling back to LLM",
                ambiguous.Count,
                chunks.Count);

            var llmResults = await ClassifyViaLlmAsync(
                ambiguous.Select(a => a.Chunk).ToList(),
                cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < ambiguous.Count; i++)
            {
                results[ambiguous[i].Index] = llmResults[i];
            }
        }

        return results;
    }

    private static GameBookRole ApplyRules(string headingPath)
    {
        if (string.IsNullOrWhiteSpace(headingPath))
        {
            return GameBookRole.None;
        }

        var result = GameBookRole.None;
        foreach (var (pattern, role) in HeadingRules)
        {
            if (pattern.IsMatch(headingPath))
            {
                result |= role;
            }
        }
        return result;
    }

    // STUB — D3 will replace with a real LLM-driven implementation.
    // Returning RulesReference is the safest default because most ambiguous
    // chunks in a board-game manual are rules-related body text under generic headings.
    private Task<IReadOnlyList<GameBookRole>> ClassifyViaLlmAsync(
        IReadOnlyList<ChunkInput> chunks,
        CancellationToken cancellationToken)
    {
        _ = _llmService; // Injected for D3; not yet used in D2 stub.
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<GameBookRole>>(
            chunks.Select(_ => GameBookRole.RulesReference).ToList());
    }
}
