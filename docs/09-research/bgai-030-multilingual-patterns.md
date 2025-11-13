# Research: BGAI-030 Implementation - Multilingual Patterns

**Research Date**: 2025-11-12
**Topic**: Multilingual patterns for HallucinationDetectionService (BGAI-030)
**Context**: Implementing forbidden keyword detection with Italian-first multilingual support

---

## Executive Summary

**Finding**: MeepleAI has **comprehensive multilingual infrastructure** already implemented, with Italian-first design and 5-language support (en, it, de, fr, es).

**Key Patterns for BGAI-030**:
1. ✅ Use `language` parameter pattern (string? language = null with "en" default)
2. ✅ Language detection via `ILanguageDetectionService`
3. ✅ Language-specific keyword lists (per-language forbidden words)
4. ✅ Follow existing validation service patterns (Confidence + Citation)

**Recommendation**: Implement `HallucinationDetectionService` with language-specific forbidden keyword dictionaries following established multilingual patterns.

---

## Existing Multilingual Infrastructure

### 1. Language Detection Service

**Interface**: `ILanguageDetectionService`
- Supports: en, it, de, fr, es
- Method: `DetectLanguageAsync(string text) → ISO 639-1 code`
- Default: "en" if unable to detect
- Validation: `IsSupportedLanguage(string code) → bool`

### 2. Language Parameter Pattern (Established Convention)

**Pattern Found in RAG Services**:
```csharp
// Standard pattern across codebase
public async Task<QaResponse> AskAsync(
    string gameId,
    string query,
    string? language = null,  // ← Consistent pattern
    CancellationToken ct = default)
{
    language ??= "en"; // Default to English

    // Language-aware operations...
}
```

**Used in**:
- `RagService.AskAsync()`
- `RagService.ExplainAsync()`
- `RagService.AskWithHybridSearchAsync()`
- `EmbeddingService.GenerateEmbeddingAsync()`
- `QdrantService.SearchAsync()`
- `QueryExpansionService.GenerateQueryVariationsAsync()`

### 3. Embedding Strategy (ADR-002)

**Model**: `intfloat/multilingual-e5-large`
- **Dimensions**: 1024
- **Languages**: 100+ including target 5 (en, it, de, fr, es)
- **Italian Quality**: MTEB benchmark 0.65-0.70 (good baseline)
- **Training**: Multilingual corpus with Italian Wikipedia + Common Crawl

**Language Handling**:
- Single model handles all languages (no switching needed)
- Language parameter used for filtering in vector DB
- Italian-first design with Romance language transfer learning

### 4. Vector Storage Language Filtering

**Qdrant Integration**:
```csharp
// Language filter in vector search
var filter = new Filter
{
    Must = new List<Condition>
    {
        FieldCondition.Match("game_id", gameId),
        FieldCondition.Match("language", language) // ← Language filtering
    }
};
```

**VectorDocument Entity**:
- Has `Language` property (ISO 639-1 code)
- Indexed for fast language filtering
- Enables multilingual document storage

---

## Multilingual Patterns for BGAI-030

### Pattern 1: Language-Specific Forbidden Keywords

**Recommended Structure**:
```csharp
public class HallucinationDetectionService
{
    // Language-specific forbidden keyword dictionaries
    private static readonly Dictionary<string, HashSet<string>> ForbiddenKeywordsByLanguage = new()
    {
        ["en"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "I don't know",
            "I'm not sure",
            "I cannot find",
            "not specified in the rules",
            "unclear",
            "ambiguous"
        },
        ["it"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "non lo so",
            "non sono sicuro",
            "non riesco a trovare",
            "non specificato nelle regole",
            "poco chiaro",
            "ambiguo"
        },
        ["de"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ich weiß nicht",
            "Ich bin nicht sicher",
            "Ich kann nicht finden",
            "nicht in den Regeln angegeben",
            "unklar"
        },
        ["fr"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Je ne sais pas",
            "Je ne suis pas sûr",
            "Je ne trouve pas",
            "pas spécifié dans les règles",
            "peu clair"
        },
        ["es"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "No lo sé",
            "No estoy seguro",
            "No puedo encontrar",
            "no especificado en las reglas",
            "poco claro"
        }
    };

    public HallucinationValidationResult DetectHallucinations(
        string responseText,
        string? language = null)
    {
        language ??= "en"; // Default pattern

        // Get language-specific keywords
        if (!ForbiddenKeywordsByLanguage.TryGetValue(language, out var keywords))
        {
            keywords = ForbiddenKeywordsByLanguage["en"]; // Fallback to English
        }

        // Check for forbidden keywords...
    }
}
```

### Pattern 2: Language Detection Integration

**Auto-Detection Pattern**:
```csharp
public async Task<HallucinationValidationResult> DetectHallucinationsAsync(
    string responseText,
    string? language = null,
    CancellationToken ct = default)
{
    // If no language provided, auto-detect
    if (string.IsNullOrWhiteSpace(language))
    {
        language = await _languageDetection.DetectLanguageAsync(responseText);
        _logger.LogDebug("Auto-detected language: {Language}", language);
    }

    // Proceed with language-specific validation...
}
```

### Pattern 3: Multilingual Error Messages

**From Existing Services**:
```csharp
// Validation messages adapt to language
private string GetValidationMessage(string language, ValidationResult result)
{
    return language switch
    {
        "it" => $"Rilevate {result.IssueCount} parole proibite",
        "de" => $"{result.IssueCount} verbotene Wörter erkannt",
        "fr" => $"{result.IssueCount} mots interdits détectés",
        "es" => $"{result.IssueCount} palabras prohibidas detectadas",
        _ => $"{result.IssueCount} forbidden keywords detected"
    };
}
```

---

## Implementation Guidelines for BGAI-030

### Service Architecture (Following BGAI-028, BGAI-029 Pattern)

```csharp
// 1. Interface
public interface IHallucinationDetectionService
{
    Task<HallucinationValidationResult> DetectHallucinationsAsync(
        string responseText,
        string? language = null,
        CancellationToken ct = default);

    int GetForbiddenKeywordCount(string language);
}

// 2. Service Implementation
public class HallucinationDetectionService : IHallucinationDetectionService
{
    private readonly ILanguageDetectionService _languageDetection;
    private readonly ILogger<HallucinationDetectionService> _logger;

    // Language-specific forbidden keywords
    private static readonly Dictionary<string, HashSet<string>> ForbiddenKeywords = ...;

    public HallucinationDetectionService(
        ILanguageDetectionService languageDetection,
        ILogger<HallucinationDetectionService> logger)
    {
        _languageDetection = languageDetection;
        _logger = logger;
    }

    public async Task<HallucinationValidationResult> DetectHallucinationsAsync(
        string responseText,
        string? language = null,
        CancellationToken ct = default)
    {
        // Auto-detect if not provided
        language ??= await _languageDetection.DetectLanguageAsync(responseText);

        // Get language-specific keywords
        var keywords = GetKeywordsForLanguage(language);

        // Detect hallucinations
        var detectedKeywords = new List<string>();
        foreach (var keyword in keywords)
        {
            if (responseText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                detectedKeywords.Add(keyword);
            }
        }

        var isValid = detectedKeywords.Count == 0;

        return new HallucinationValidationResult
        {
            IsValid = isValid,
            DetectedKeywords = detectedKeywords,
            Language = language,
            Message = GetMessage(language, detectedKeywords.Count)
        };
    }
}

// 3. DI Registration (in KnowledgeBaseServiceExtensions)
services.AddSingleton<IHallucinationDetectionService, HallucinationDetectionService>();
```

### Testing Pattern

```csharp
public class HallucinationDetectionServiceTests
{
    [Theory]
    [InlineData("I don't know the answer", "en", false)] // Contains forbidden
    [InlineData("The game has 4 players", "en", true)]   // Valid
    [InlineData("Non lo so", "it", false)]               // Italian forbidden
    [InlineData("Il gioco ha 4 giocatori", "it", true)]  // Italian valid
    public async Task Test_DetectHallucinations_MultipleLanguages(
        string text, string language, bool expectedValid)
    {
        var result = await _service.DetectHallucinationsAsync(text, language);
        Assert.Equal(expectedValid, result.IsValid);
    }
}
```

---

## Multilingual Forbidden Keywords Research

### Italian (it) - Primary Language
**Common Hallucination Phrases**:
- "non lo so" (I don't know)
- "non sono sicuro/a" (I'm not sure)
- "non riesco a trovare" (I cannot find)
- "non specificato nelle regole" (not specified in the rules)
- "poco chiaro" (unclear)
- "ambiguo" (ambiguous)
- "potrebbe essere" (might be)
- "probabilmente" (probably)
- "forse" (perhaps/maybe)
- "non sono certo/a" (I'm not certain)

### English (en)
**Common Hallucination Phrases**:
- "I don't know"
- "I'm not sure"
- "I cannot find"
- "not specified"
- "unclear"
- "ambiguous"
- "might be"
- "possibly"
- "perhaps"
- "I think"

### German (de)
**Common Hallucination Phrases**:
- "Ich weiß nicht" (I don't know)
- "Ich bin nicht sicher" (I'm not sure)
- "Ich kann nicht finden" (I cannot find)
- "nicht angegeben" (not specified)
- "unklar" (unclear)
- "vielleicht" (perhaps)

### French (fr)
**Common Hallucination Phrases**:
- "Je ne sais pas" (I don't know)
- "Je ne suis pas sûr" (I'm not sure)
- "Je ne trouve pas" (I cannot find)
- "pas spécifié" (not specified)
- "peu clair" (unclear)
- "peut-être" (perhaps)

### Spanish (es)
**Common Hallucination Phrases**:
- "No lo sé" (I don't know)
- "No estoy seguro" (I'm not sure)
- "No puedo encontrar" (I cannot find)
- "no especificado" (not specified)
- "poco claro" (unclear)
- "tal vez" (perhaps)

---

## Integration with Existing Services

### RAG Pipeline Integration

```csharp
public class RagService
{
    private readonly IHallucinationDetectionService _hallucinationDetection;
    private readonly IConfidenceValidationService _confidenceValidation;
    private readonly ICitationValidationService _citationValidation;

    public async Task<QaResponse> AskAsync(
        string gameId,
        string query,
        string? language = null,
        CancellationToken ct = default)
    {
        language ??= "en";

        // ... RAG pipeline ...

        // Validation chain
        var confidence = CalculateConfidence(results);
        var confidenceResult = _confidenceValidation.ValidateConfidence(confidence);

        var citationResult = await _citationValidation.ValidateCitationsAsync(
            snippets, gameId, ct);

        var hallucinationResult = await _hallucinationDetection
            .DetectHallucinationsAsync(answer, language, ct); // ← Language-aware

        // Combine validation results in metadata
        var metadata = new Dictionary<string, string>
        {
            ["confidence_valid"] = confidenceResult.IsValid.ToString(),
            ["citations_valid"] = citationResult.IsValid.ToString(),
            ["hallucination_free"] = hallucinationResult.IsValid.ToString(),
            ["validation_language"] = language
        };

        return new QaResponse(answer, snippets, tokens, confidence, metadata);
    }
}
```

---

## Configuration Pattern

### appsettings.json

```json
{
  "Validation": {
    "ConfidenceThreshold": 0.70,
    "EnableHallucinationDetection": true,
    "SupportedLanguages": ["en", "it", "de", "fr", "es"],
    "DefaultLanguage": "en"
  },

  "HallucinationDetection": {
    "StrictMode": false,  // If true, reject responses with forbidden keywords
    "LogDetections": true,
    "ForbiddenKeywords": {
      "en": ["I don't know", "I'm not sure", "unclear"],
      "it": ["non lo so", "non sono sicuro", "poco chiaro"]
      // ... other languages
    }
  }
}
```

---

## Best Practices from Codebase

### 1. Language Parameter Propagation

**Pattern**: Pass language through the entire call chain
```csharp
// Entry point
public async Task<QaResponse> AskAsync(string query, string? language = null)
{
    language ??= "en";

    // Embedding (language-aware)
    var embedding = await _embeddingService.GenerateEmbeddingAsync(query, language, ct);

    // Vector search (language-filtered)
    var results = await _qdrantService.SearchAsync(gameId, embedding, language, limit, ct);

    // Validation (language-specific)
    var validation = await _hallucinationDetection.DetectHallucinationsAsync(
        answer, language, ct);
}
```

### 2. Language Detection Integration

**Auto-Detection Pattern**:
```csharp
// If language not provided, detect from query
if (string.IsNullOrWhiteSpace(language))
{
    language = await _languageDetection.DetectLanguageAsync(query);
    _logger.LogInformation("Auto-detected language: {Language} for query: {QueryPreview}",
        language, query.Substring(0, Math.Min(50, query.Length)));
}
```

### 3. Fallback Strategy

**Defensive Pattern**:
```csharp
// Always have English fallback
var keywords = ForbiddenKeywordsByLanguage.TryGetValue(language, out var langKeywords)
    ? langKeywords
    : ForbiddenKeywordsByLanguage["en"]; // Fallback to English

_logger.LogWarning("No keywords for language {Language}, using English fallback", language);
```

---

## Code Examples from Existing Services

### Example 1: RagService Language Usage

```csharp
// AI-09: Added language parameter for multilingual support
public async Task<QaResponse> AskAsync(
    string gameId,
    string query,
    string? language = null,
    bool bypassCache = false,
    CancellationToken ct = default)
{
    // AI-09: Default to English if no language specified
    language ??= "en";

    // ... existing code ...

    // AI-09: Include language in cache key
    var cacheKey = $"{_cache.GenerateQaCacheKey(gameId, query)}:lang:{language}";

    // AI-09: Use language-aware embedding service
    var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
        topic, language, ct);

    // AI-09: Filter search by language
    var searchResult = await _qdrantService.SearchAsync(
        gameId, embedding, language, limit, ct);
}
```

### Example 2: QueryExpansionService

```csharp
public async Task<List<string>> GenerateQueryVariationsAsync(
    string query,
    string language,
    CancellationToken ct = default)
{
    var variations = new List<string> { query };

    // Language-specific expansion rules
    if (language == "it")
    {
        // Italian-specific expansions
        if (query.Contains("carta"))
            variations.Add(query.Replace("carta", "tessera"));
    }
    else if (language == "en")
    {
        // English expansions
        if (query.Contains("card"))
            variations.Add(query.Replace("card", "tile"));
    }

    return variations;
}
```

---

## Testing Strategy for Multilingual Features

### Test Pattern from Existing Code

```csharp
[Theory]
[InlineData("en", "How many players?")]
[InlineData("it", "Quanti giocatori?")]
[InlineData("de", "Wie viele Spieler?")]
[InlineData("fr", "Combien de joueurs?")]
[InlineData("es", "¿Cuántos jugadores?")]
public async Task Test_MultilingualSupport_AllLanguages(string lang, string query)
{
    var result = await _service.ProcessAsync(query, language: lang);
    Assert.NotNull(result);
    // Language-specific validations...
}
```

---

## BGAI-030 Implementation Checklist

### ✅ Use Existing Patterns

1. **Service Interface**:
   - `IHallucinationDetectionService` interface
   - `DetectHallucinationsAsync(string text, string? language = null, CT ct)`
   - Returns `HallucinationValidationResult`

2. **Language Support**:
   - Support all 5 languages: en, it, de, fr, es
   - Default to "en" if null
   - Use `ILanguageDetectionService` for auto-detection

3. **Forbidden Keywords**:
   - Dictionary<string, HashSet<string>> by language
   - StringComparer.OrdinalIgnoreCase for matching
   - Comprehensive lists per language (10-15 phrases)

4. **Logging**:
   - LogDebug: Keyword matches found
   - LogWarning: Hallucinations detected
   - LogInformation: Validation summary

5. **DI Registration**:
   - Singleton (stateless, language dictionaries are static)
   - In `KnowledgeBaseServiceExtensions.cs`

6. **Testing**:
   - Theory tests with InlineData for each language
   - Edge cases: null language, unknown language
   - 15+ tests recommended (3 per language + edge cases)

---

## Performance Considerations

### Keyword Matching Optimization

```csharp
// Use HashSet for O(1) lookup instead of List.Contains O(n)
private static readonly Dictionary<string, HashSet<string>> ForbiddenKeywords = ...;

// Case-insensitive matching
new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// Efficient multi-keyword detection
foreach (var keyword in keywords)
{
    if (responseText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
    {
        detected.Add(keyword);
    }
}

// Alternative: Regex for complex patterns (use cautiously)
private static readonly Dictionary<string, Regex> ForbiddenPatterns = new()
{
    ["en"] = new Regex(@"\b(I don't know|not sure|cannot find)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ["it"] = new Regex(@"\b(non lo so|non sono sicuro|non riesco)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)
};
```

---

## Validation Service Comparison

| Service | Validates | Language-Aware | Threshold | Pattern |
|---------|-----------|----------------|-----------|---------|
| **ConfidenceValidationService** | Confidence scores | No | ≥0.70 | Numeric threshold |
| **CitationValidationService** | Source references | Indirectly (gameId) | N/A | DB lookup |
| **HallucinationDetectionService** | Forbidden keywords | **✅ Yes** | Configurable | Keyword matching |

**BGAI-030 Uniqueness**: Only validation service that needs explicit multilingual support

---

## Documentation References

| Document | Relevance | Key Insights |
|----------|-----------|--------------|
| **ADR-002** | ⭐⭐⭐ | Multilingual embedding strategy, Italian-first design |
| **ILanguageDetectionService** | ⭐⭐⭐ | Language detection API and supported languages |
| **RagService** | ⭐⭐ | Language parameter patterns (lines 74, 293, 521) |
| **EmbeddingService** | ⭐⭐ | Language-aware embedding generation |
| **QueryExpansionService** | ⭐⭐ | Language-specific query variations |

---

## Recommended Implementation Steps

### Step 1: Create Interface and Models
- `IHallucinationDetectionService.cs`
- `HallucinationValidationResult` record
- `DetectedHallucination` record
- `HallucinationSeverity` enum

### Step 2: Implement Service
- `HallucinationDetectionService.cs`
- Language-specific keyword dictionaries (5 languages)
- Case-insensitive keyword matching
- Language detection integration

### Step 3: Register in DI
- Add to `KnowledgeBaseServiceExtensions.cs`
- Singleton lifetime (stateless)

### Step 4: Comprehensive Tests (15+ tests)
- Test for each language (5 tests)
- Edge cases (null language, unknown language)
- Mixed content tests
- Performance tests

### Step 5: Documentation
- Architecture document
- Keyword lists documentation
- Integration guide

---

## Confidence & Recommendations

**Confidence**: 95% (comprehensive multilingual infrastructure exists)

**Recommendations for BGAI-030**:
1. ✅ Follow language parameter pattern (`string? language = null`)
2. ✅ Use `ILanguageDetectionService` for auto-detection
3. ✅ Create language-specific keyword dictionaries
4. ✅ Support all 5 languages (en, it, de, fr, es)
5. ✅ Follow validation service patterns (BGAI-028, BGAI-029)
6. ✅ Use Theory tests with InlineData for multilingual testing
7. ✅ Register as Singleton in DI
8. ✅ Add to validation chain in RagService

---

**Research Complete**: Ready to implement BGAI-030 with full multilingual support using established patterns.
