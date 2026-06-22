using NTextCat;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// NTextCat-based language detection.
/// Loads the Core14 n-gram profile from embedded resource at construction time (singleton-scoped).
/// Detection is CPU-bound, fast (~5-15ms typical), pure-managed (no native binding).
///
/// DEC-1 (#1559 spec-panel): NTextCat chosen over Lingua.NET / CLD3.NET for Linux container
/// friendliness, MIT license, zero native deps.
/// DEC-3: Filters detection to allowlist {EN, FR, DE, ES, IT}; out-of-allowlist → Lang=null.
/// DEC-4: Confidence is raw normalized [0,1] (NTextCat returns distance ratios; we normalize
/// best-vs-top3 using proportional inverse).
///
/// NTextCat v0.3.65 namespace: NTextCat (not IvanAkcheurov.NTextCat.Lib — that was pre-0.3.x).
/// Load signature: RankedLanguageIdentifierFactory.Load(Stream, Func&lt;LanguageModel&lt;string&gt;, bool&gt;).
/// Identify returns: IEnumerable&lt;Tuple&lt;LanguageInfo, double&gt;&gt; where double is distance (lower=better).
/// </summary>
internal sealed class NTextCatLanguageDetectionService : ILanguageDetectionService
{
    /// <summary>
    /// NTextCat ISO 639-3 (3-letter) to ISO 639-1 (2-letter) mapping for Core14 allowlist langs.
    /// Core14 comment in profile XML lists: dan, deu, eng, fra, ita, jpn, kor, nld, nor, por, rus, spa, swe, zho.
    /// We only map the 5 product-supported langs (DEC-3) — this map IS the allowlist projection;
    /// any addition here MUST correspond to a product decision to support a new lang. TryGetValue
    /// failure → null lang per DEC-3 (out-of-allowlist).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Iso639To2Letter =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["eng"] = "EN",
            ["fra"] = "FR",
            ["deu"] = "DE",
            ["ger"] = "DE", // historical alternate ISO 639-2B code
            ["spa"] = "ES",
            ["ita"] = "IT",
        };

    // Matches the LogicalName produced by the EmbeddedResource in Api.csproj:
    // <EmbeddedResource Include="Resources\LanguageDetection\Core14.profile.xml" />
    // Default logical name = AssemblyName + "." + relative path with backslash→dot.
    private const string EmbeddedProfilePath = "Api.Resources.LanguageDetection.Core14.profile.xml";

    private readonly ILogger<NTextCatLanguageDetectionService> _logger;
    private readonly RankedLanguageIdentifier _identifier;

    public NTextCatLanguageDetectionService(ILogger<NTextCatLanguageDetectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        var assembly = typeof(NTextCatLanguageDetectionService).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedProfilePath)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedProfilePath}' not found. " +
                "Check Api.csproj for the <EmbeddedResource> entry and that " +
                "apps/api/src/Api/Resources/LanguageDetection/Core14.profile.xml exists. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        var factory = new RankedLanguageIdentifierFactory();
        // Load(Stream, Func<LanguageModel<string>, bool>) — predicate _ => true loads all 14 models.
        _identifier = factory.Load(stream, _ => true);
    }

    /// <inheritdoc />
    public LanguageDetectionResult Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new LanguageDetectionResult(null, 0.0);

        try
        {
            var ranked = _identifier.Identify(text).ToList();
            if (ranked.Count == 0)
                return new LanguageDetectionResult(null, 0.0);

            var best = ranked[0];
            var bestIso3 = best.Item1.Iso639_3;
            var confidence = NormalizeConfidence(ranked);

            // Iso639To2Letter is the allowlist projection: all values are guaranteed to be in Allowlist
            // (mapping defined per DEC-3 to map ONLY supported langs). TryGetValue is the gate.
            if (Iso639To2Letter.TryGetValue(bestIso3, out var twoLetter))
            {
                return new LanguageDetectionResult(twoLetter, confidence);
            }

            // Out-of-allowlist: return null lang but preserve raw confidence per DEC-4.
            return new LanguageDetectionResult(null, confidence);
        }
        catch (Exception ex)
        {
            // Nygard NEW failure mode: NTextCat may crash on edge-case inputs.
            // Log + return safe default; never throw to caller (interface contract).
            _logger.LogWarning(ex,
                "NTextCat language detection failed on input of length {Length}", text.Length);
            return new LanguageDetectionResult(null, 0.0);
        }
    }

    /// <summary>
    /// Normalizes NTextCat out-of-place distance scores to a confidence value in [0, 1].
    /// NTextCat <c>Identify</c> returns <c>Tuple&lt;LanguageInfo, double&gt;</c> where the double
    /// is an out-of-place distance (lower = better match, typically in the range 3800-4200 for
    /// ~50-word texts with the Core14 profile).
    ///
    /// Formula: <c>tanh((second - best) / best * 200)</c>
    /// Maps the relative gap between winner and runner-up to [0, 1] via a smooth S-curve.
    /// Calibrated empirically against Core14 profile distances:
    ///   - ~1% relative gap  (e.g. gap=40 on best=4000) → confidence ≈ 0.96 (very confident)
    ///   - ~0.5% relative gap (gap=20 on best=4000) → confidence ≈ 0.76 (moderate)
    ///   - ~0.3% relative gap (gap=12 on best=4000) → confidence ≈ 0.54 (borderline)
    ///   - ~0.1% relative gap (gap=4 on best=4000)  → confidence ≈ 0.20 (low)
    /// Short/ambiguous texts yield very small gaps (gap≈1) → confidence near 0.
    ///
    /// DEC-4: FE tiers: confident ≥0.65 / borderline 0.45-0.65 / uncertain &lt;0.45.
    /// When only one candidate exists, confidence = 1.0.
    /// When best == 0.0 (degenerate), confidence = 0.0.
    /// </summary>
    private static double NormalizeConfidence(IReadOnlyList<Tuple<LanguageInfo, double>> ranked)
    {
        if (ranked.Count == 1) return 1.0;

        var bestDistance = ranked[0].Item2;
        var secondDistance = ranked[1].Item2;

        if (bestDistance <= 0.0) return 0.0;

        var relativeGap = (secondDistance - bestDistance) / bestDistance;
        return Math.Tanh(relativeGap * 200.0);
    }
}
