using System.Text.Json;
using System.Text.Json.Serialization;

using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Immutable snapshot of an approved <see cref="MechanicAnalysis"/> captured at publish time and
/// stored verbatim in the <c>mechanic_cards.content</c> JSONB column (ADR-051 AD-1). The card never
/// dereferences the live claim graph at render time — a revision produces a new card version instead.
/// </summary>
/// <remarks>
/// <see cref="SchemaVersion"/> guards forward evolution of the JSONB shape. Serialized with
/// snake_case keys via <c>[JsonPropertyName]</c> so the on-disk JSON matches the ADR-051 contract.
/// </remarks>
public sealed record MechanicCardContent
{
    /// <summary>Current on-disk JSONB schema version.</summary>
    public const int CurrentSchemaVersion = 2; // #2782: real validations projected; write-only until a card reader (#528) exists.

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Keys are already snake_case via [JsonPropertyName]; keep output compact.
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("snapshot_at")]
    public DateTime SnapshotAt { get; init; }

    [JsonPropertyName("source_analysis_id")]
    public Guid SourceAnalysisId { get; init; }

    [JsonPropertyName("source_prompt_version")]
    public string SourcePromptVersion { get; init; } = string.Empty;

    [JsonPropertyName("claims")]
    public IReadOnlyList<MechanicCardClaimSnapshot> Claims { get; init; } = Array.Empty<MechanicCardClaimSnapshot>();

    [JsonPropertyName("metadata")]
    public MechanicCardMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Builds an immutable snapshot from an approved analysis and its game context. The analysis
    /// MUST already carry the fully materialized claim + citation graph.
    /// </summary>
    public static MechanicCardContent FromAnalysis(
        MechanicAnalysis analysis,
        MechanicCardGameContext gameContext,
        DateTime snapshotAt)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(gameContext);

        var claims = analysis.Claims
            .OrderBy(c => c.Section)
            .ThenBy(c => c.DisplayOrder)
            .Select(c => new MechanicCardClaimSnapshot
            {
                Id = c.Id,
                Section = c.Section.ToString(),
                Ordinal = c.DisplayOrder,
                Claim = c.Text,
                Citations = c.Citations
                    .OrderBy(cit => cit.DisplayOrder)
                    .Select(cit => new MechanicCardCitationSnapshot
                    {
                        // A MechanicAnalysis is scoped to a single source PDF, so every citation
                        // shares the analysis PdfDocumentId (the citation entity carries only the page).
                        PdfId = analysis.PdfDocumentId,
                        PdfPage = cit.PdfPage,
                        Quote = cit.Quote
                    })
                    .ToList(),
                // Down-project each claim's real per-rule validations (#2782 D6). Both "fail" AND
                // "notRun" collapse to Passed=false — the card is a published snapshot of accepted
                // claims, so the 3-state review nuance is intentionally lossy here. Never project up.
                Validations = c.Validations
                    .Select(v => new MechanicCardValidationSnapshot
                    {
                        Rule = v.Rule,
                        Passed = string.Equals(v.Outcome, MechanicClaimValidationOutcomes.Pass, StringComparison.Ordinal),
                        Score = v.Score
                    })
                    .ToList()
            })
            .ToList();

        return new MechanicCardContent
        {
            SchemaVersion = CurrentSchemaVersion,
            SnapshotAt = snapshotAt,
            SourceAnalysisId = analysis.Id,
            SourcePromptVersion = analysis.PromptVersion,
            Claims = claims,
            Metadata = new MechanicCardMetadata
            {
                SharedGameId = gameContext.SharedGameId,
                SharedGameName = gameContext.SharedGameName,
                Publisher = gameContext.Publisher,
                Language = gameContext.Language
            }
        };
    }

    /// <summary>Serializes the snapshot to the JSONB string persisted in <c>mechanic_cards.content</c>.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}

/// <summary>Per-claim snapshot inside <see cref="MechanicCardContent.Claims"/>.</summary>
public sealed record MechanicCardClaimSnapshot
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("section")]
    public string Section { get; init; } = string.Empty;

    [JsonPropertyName("ordinal")]
    public int Ordinal { get; init; }

    [JsonPropertyName("claim")]
    public string Claim { get; init; } = string.Empty;

    [JsonPropertyName("citations")]
    public IReadOnlyList<MechanicCardCitationSnapshot> Citations { get; init; } = Array.Empty<MechanicCardCitationSnapshot>();

    [JsonPropertyName("validations")]
    public IReadOnlyList<MechanicCardValidationSnapshot> Validations { get; init; } = Array.Empty<MechanicCardValidationSnapshot>();
}

/// <summary>Per-citation snapshot (source page + verbatim quote).</summary>
public sealed record MechanicCardCitationSnapshot
{
    [JsonPropertyName("pdf_id")]
    public Guid PdfId { get; init; }

    [JsonPropertyName("pdf_page")]
    public int PdfPage { get; init; }

    [JsonPropertyName("quote")]
    public string Quote { get; init; } = string.Empty;
}

/// <summary>T1-T4 guardrail outcome snapshot, down-projected from <see cref="MechanicClaimValidation"/> (#2782 D6).</summary>
public sealed record MechanicCardValidationSnapshot
{
    [JsonPropertyName("rule")]
    public string Rule { get; init; } = string.Empty;

    [JsonPropertyName("passed")]
    public bool Passed { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }
}

/// <summary>Game-context metadata block of the snapshot.</summary>
public sealed record MechanicCardMetadata
{
    [JsonPropertyName("shared_game_id")]
    public Guid SharedGameId { get; init; }

    [JsonPropertyName("shared_game_name")]
    public string SharedGameName { get; init; } = string.Empty;

    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
}

/// <summary>
/// Cross-aggregate game context resolved by the command handler (from SharedGame + translations)
/// and passed to <see cref="MechanicCardContent.FromAnalysis"/> and
/// <see cref="MechanicCard.PublishFromAnalysis"/>, since it is not available on the analysis itself.
/// </summary>
public sealed record MechanicCardGameContext
{
    public required Guid SharedGameId { get; init; }
    public required string SharedGameName { get; init; }
    public string? Publisher { get; init; }
    public string Language { get; init; } = "en";
}
