using System.Globalization;
using System.Text;
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

    private const string LlmSystemPrompt =
        """
        Classify each board game manual chunk by role. Valid labels:
        tutorial, rulesreference, narrative, encounter, lore, setup.
        Multi-label allowed. Output ONLY a JSON array of arrays (one inner array per chunk in order).
        Example: [["tutorial","setup"],["rulesreference"],["narrative"]]
        """;

    // D3: LLM fallback for ambiguous chunks (no heading match).
    // System prompt instructs JSON array-of-arrays output; we delegate to
    // ILlmService.GenerateJsonAsync which tolerates prose preamble and markdown
    // code fences (issue #1395). On unrecoverable parse failures the underlying
    // service returns null and we degrade gracefully to RulesReference per chunk
    // (safe default for manuals).
    private async Task<IReadOnlyList<GameBookRole>> ClassifyViaLlmAsync(
        IReadOnlyList<ChunkInput> chunks,
        CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return Array.Empty<GameBookRole>();
        }

        var userPrompt = BuildUserPrompt(chunks);

        try
        {
            var parsed = await _llmService.GenerateJsonAsync<string[][]>(
                LlmSystemPrompt,
                userPrompt,
                RequestSource.RagClassification,
                cancellationToken).ConfigureAwait(false);

            if (parsed is null || parsed.Length == 0)
            {
                _logger.LogWarning(
                    "Role classifier LLM fallback returned no usable JSON; defaulting {Count} chunks to RulesReference",
                    chunks.Count);
                return DefaultAll(chunks.Count);
            }

            return MapParsedToRoles(parsed, chunks.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types — LLM faults must NOT block ingestion; degrade to safe default.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(
                ex,
                "Role classifier LLM fallback failed; defaulting {Count} chunks to RulesReference",
                chunks.Count);
            return DefaultAll(chunks.Count);
        }
    }

    private static string BuildUserPrompt(IReadOnlyList<ChunkInput> chunks)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var bodyPreview = chunks[i].BodyText.Length <= 200
                ? chunks[i].BodyText
                : chunks[i].BodyText.AsSpan(0, 200).ToString();
            sb.Append(CultureInfo.InvariantCulture, $"Chunk {i}: heading='{chunks[i].HeadingPath}' body='{bodyPreview}'");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static IReadOnlyList<GameBookRole> MapParsedToRoles(string[][] parsed, int expectedCount)
    {
        var result = new GameBookRole[expectedCount];
        for (var i = 0; i < expectedCount; i++)
        {
            if (i >= parsed.Length || parsed[i] is null)
            {
                result[i] = GameBookRole.RulesReference;
                continue;
            }

            var roles = GameBookRole.None;
            foreach (var label in parsed[i])
            {
                roles |= ParseLabel(label);
            }
            result[i] = roles == GameBookRole.None ? GameBookRole.RulesReference : roles;
        }
        return result;
    }

    private static GameBookRole ParseLabel(string label) => label?.ToLowerInvariant() switch
    {
        "tutorial" => GameBookRole.Tutorial,
        "rulesreference" or "rules" or "reference" => GameBookRole.RulesReference,
        "narrative" => GameBookRole.Narrative,
        "encounter" => GameBookRole.Encounter,
        "lore" => GameBookRole.Lore,
        "setup" => GameBookRole.Setup,
        _ => GameBookRole.None,
    };

    private static GameBookRole[] DefaultAll(int count) =>
        Enumerable.Repeat(GameBookRole.RulesReference, count).ToArray();
}
