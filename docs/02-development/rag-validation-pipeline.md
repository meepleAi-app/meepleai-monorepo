# RAG Validation Pipeline

**Issue**: [#977](https://github.com/DegrassiAaron/meepleai-monorepo/issues/977) - BGAI-035: Wire all 5 validation layers in RAG pipeline
**Status**: ✅ Complete
**Date**: 2025-11-17

---

## Overview

The RAG Validation Pipeline integrates all 5 validation layers into a unified orchestration service that ensures comprehensive quality assurance for AI-generated responses in the MeepleAI board game rules assistant.

## Architecture

### Validation Layers

The pipeline implements **5 distinct validation layers**, each addressing a specific quality dimension:

#### Layer 1: Confidence Validation
- **Service**: `ConfidenceValidationService`
- **Threshold**: ≥0.70
- **Purpose**: Validates that retrieval confidence meets minimum quality threshold
- **Issue**: [#970](https://github.com/DegrassiAaron/meepleai-monorepo/issues/970) - BGAI-028
- **Severity Levels**:
  - Pass: ≥0.70 (acceptable quality)
  - Warning: 0.60-0.70 (below threshold but usable)
  - Critical: <0.60 (unacceptable quality)
  - Unknown: null (no confidence score available)

#### Layer 2: Multi-Model Consensus
- **Service**: `MultiModelValidationService`
- **Threshold**: ≥0.90 similarity
- **Purpose**: Validates response consistency across multiple LLM models (GPT-4 + Claude)
- **Issue**: [#974](https://github.com/DegrassiAaron/meepleai-monorepo/issues/974) - BGAI-032
- **Algorithm**: TF-IDF cosine similarity
- **Models**:
  - Primary: `openai/gpt-4o`
  - Secondary: `anthropic/claude-3.5-sonnet`

#### Layer 3: Citation Validation
- **Service**: `CitationValidationService`
- **Purpose**: Validates that citations reference actual source documents
- **Issue**: [#971](https://github.com/DegrassiAaron/meepleai-monorepo/issues/971) - BGAI-029
- **Checks**:
  - PDF documents exist in database
  - Page numbers are within valid range (1 to pageCount)
  - Source format is correct (`PDF:guid`)
- **Error Types**:
  - DocumentNotFound
  - InvalidPageNumber
  - MalformedSource
  - TextMismatch

#### Layer 4: Hallucination Detection
- **Service**: `HallucinationDetectionService`
- **Purpose**: Detects AI hallucinations via forbidden keyword analysis
- **Issue**: [#972](https://github.com/DegrassiAaron/meepleai-monorepo/issues/972) - BGAI-030
- **Languages**: English, Italian, German, French, Spanish
- **Target**: <3% hallucination rate (ADR-006)
- **Keyword Categories**:
  - Uncertainty: "I don't know", "not sure", "unclear"
  - Admission: "cannot find", "not specified"
  - Hedging: "might be", "possibly", "perhaps"
- **Severity Levels**:
  - None: 0 keywords
  - Low: 1-2 keywords
  - Medium: 3-4 keywords
  - High: 5+ keywords or critical phrases

#### Layer 5: Validation Accuracy Tracking
- **Service**: `ValidationAccuracyTrackingService`
- **Threshold**: ≥80% accuracy baseline
- **Purpose**: Measures how accurately the validation system identifies correct vs. incorrect responses
- **Issue**: [#981](https://github.com/DegrassiAaron/meepleai-monorepo/issues/981) - BGAI-039
- **Metrics**:
  - Precision: True Positives / (True Positives + False Positives)
  - Recall: True Positives / (True Positives + False Negatives)
  - F1-Score: Harmonic mean of precision and recall
  - Accuracy: (True Positives + True Negatives) / Total
  - Matthews Correlation Coefficient (MCC)

### Orchestration Service

**Service**: `RagValidationPipelineService`
**Interface**: `IRagValidationPipelineService`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`

The orchestration service coordinates all 5 validation layers and provides two operation modes:

#### Standard Mode (3 Layers)
Applies layers 1, 3, 4 (Confidence, Citation, Hallucination)

```csharp
Task<RagValidationResult> ValidateResponseAsync(
    QaResponse response,
    string gameId,
    string? language = null,
    CancellationToken cancellationToken = default);
```

**Use Case**: Standard RAG Q&A validation for production traffic

#### Multi-Model Mode (4 Layers + Tracking)
Applies layers 1, 2, 3, 4, 5 (adds Multi-Model Consensus + Accuracy Tracking)

```csharp
Task<RagValidationResult> ValidateWithMultiModelAsync(
    QaResponse response,
    string gameId,
    string systemPrompt,
    string userPrompt,
    string? language = null,
    CancellationToken cancellationToken = default);
```

**Use Case**: High-stakes queries requiring extra validation, quality assurance testing

---

## Data Flow

### Standard Mode Pipeline

```
QaResponse Input
    │
    ├─► Layer 1: Confidence Validation (0.70 threshold)
    │       │
    │       └─► ConfidenceValidationResult
    │
    ├─► Layer 3: Citation Validation
    │       │
    │       ├─► Verify PDF documents exist
    │       ├─► Validate page numbers
    │       └─► CitationValidationResult
    │
    └─► Layer 4: Hallucination Detection
            │
            ├─► Scan for forbidden keywords
            ├─► Language-specific analysis
            └─► HallucinationValidationResult
                    │
                    ▼
            RagValidationResult
            (IsValid, LayersPassed: 3/3, Severity)
```

### Multi-Model Mode Pipeline

```
QaResponse Input + Prompts
    │
    ├─► Layer 1: Confidence Validation
    │       └─► ConfidenceValidationResult
    │
    ├─► Layer 2: Multi-Model Consensus
    │       │
    │       ├─► Query GPT-4 (parallel)
    │       ├─► Query Claude (parallel)
    │       ├─► Calculate TF-IDF cosine similarity
    │       └─► MultiModelConsensusResult
    │               │
    │               ├─► ConsensusResponse (if similarity ≥0.90)
    │               └─► Gpt4Response + ClaudeResponse
    │
    ├─► Layer 3: Citation Validation
    │       └─► CitationValidationResult
    │
    ├─► Layer 4: Hallucination Detection
    │       │
    │       └─► Uses ConsensusResponse (if available)
    │           or original answer as fallback
    │               │
    │               └─► HallucinationValidationResult
    │
    └─► Layer 5: Validation Accuracy Tracking
            │
            └─► ValidationAccuracyMetrics (informational)
                    │
                    ▼
            RagValidationResult
            (IsValid, LayersPassed: 4/4, Severity, Metrics)
```

---

## Integration

### Dependency Injection

The validation pipeline is registered in the DI container as a **Scoped** service:

```csharp
// File: KnowledgeBaseServiceExtensions.cs

services.AddScoped<IRagValidationPipelineService, RagValidationPipelineService>();
```

**Dependencies**:
- `IConfidenceValidationService` (Singleton)
- `IMultiModelValidationService` (Singleton)
- `ICitationValidationService` (Scoped - uses DbContext)
- `IHallucinationDetectionService` (Singleton)
- `ValidationAccuracyTrackingService` (Scoped)

### Usage in RagService (Example)

```csharp
public class RagService : IRagService
{
    private readonly IRagValidationPipelineService _validationPipeline;

    public async Task<QaResponse> AskAsync(
        string gameId,
        string query,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // ... existing RAG logic to generate response ...
        var response = await GenerateRagResponse(gameId, query, language, cancellationToken);

        // Validate response (standard mode)
        var validationResult = await _validationPipeline.ValidateResponseAsync(
            response,
            gameId,
            language,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "RAG response validation failed: {Message} (passed: {Passed}/{Total})",
                validationResult.Message,
                validationResult.LayersPassed,
                validationResult.TotalLayers);

            // Handle validation failure (e.g., return with warning metadata)
            response.metadata ??= new Dictionary<string, string>();
            response.metadata["validation_status"] = "warning";
            response.metadata["validation_message"] = validationResult.Message;
        }

        return response;
    }
}
```

---

## Validation Result Structure

### RagValidationResult

```csharp
public record RagValidationResult
{
    public required bool IsValid { get; init; }
    public required int LayersPassed { get; init; }
    public required int TotalLayers { get; init; }

    // Layer Results
    public required ConfidenceValidationResult ConfidenceValidation { get; init; }
    public MultiModelConsensusResult? MultiModelConsensus { get; init; }
    public required CitationValidationResult CitationValidation { get; init; }
    public required HallucinationValidationResult HallucinationDetection { get; init; }
    public string? ValidationAccuracyMetrics { get; init; }

    // Overall Status
    public required string Message { get; init; }
    public required RagValidationSeverity Severity { get; init; }
    public required long DurationMs { get; init; }
}
```

### Severity Calculation

The overall severity is determined by the most critical individual layer result:

| Condition | Severity |
|-----------|----------|
| All layers pass | `Pass` |
| Confidence = Critical OR Hallucination = High OR Multi-Model = Error | `Critical` |
| Confidence = Warning OR Citation fails OR Hallucination = Medium/Low OR Multi-Model no consensus | `Warning` |

---

## Quality Metrics

### Target Thresholds

| Layer | Metric | Threshold | Target |
|-------|--------|-----------|--------|
| 1 | Confidence Score | ≥0.70 | >95% accuracy |
| 2 | Consensus Similarity | ≥0.90 | >95% agreement |
| 3 | Citation Accuracy | 100% valid | 0 hallucinated sources |
| 4 | Hallucination Rate | <3% | <10% forbidden keywords |
| 5 | Validation Accuracy | ≥80% | Baseline measurement |

### Performance Characteristics

- **Standard Mode**: ~50-150ms overhead (3 layers)
- **Multi-Model Mode**: ~1500-3000ms overhead (4 layers + 2 LLM calls)
- **Parallelization**: Layer 2 executes GPT-4 + Claude queries in parallel
- **Caching**: Individual layer results are not cached (validation must be fresh)

---

## Testing

### Unit Tests

**File**: `RagValidationPipelineServiceTests.cs`
**Coverage**: 14 comprehensive test cases

**Test Scenarios**:
1. All layers pass (standard mode)
2. Confidence fails (returns warning)
3. Citation fails (returns warning)
4. Hallucination detected (returns warning)
5. Null response (throws ArgumentNullException)
6. Empty game ID (throws ArgumentException)
7. Null language (defaults to English)
8. All layers pass (multi-model mode)
9. Multi-model fails (no consensus)
10. Uses consensus response for hallucination check
11. Uses original answer when no consensus
12. Empty system prompt (throws ArgumentException)
13. Empty user prompt (throws ArgumentException)
14. High hallucination severity (returns critical)

**Test Pattern**:
- Arrange: Mock all dependencies, setup expected results
- Act: Call validation pipeline
- Assert: Verify layer results, overall status, severity

---

## Related Documentation

- [ADR-001: Hybrid RAG Architecture](../01-architecture/adr/adr-001-hybrid-rag.md)
- [ADR-006: Multi-Layer Validation](../01-architecture/adr/adr-006-multi-layer-validation.md)
- [Testing Guide](testing/testing-guide.md)
- [API Specification](../03-api/board-game-ai-api-specification.md)

---

## Future Enhancements

1. **Early Exit Optimization**: Stop validation on critical failures to reduce latency
2. **Layer-Specific Caching**: Cache individual layer results for identical inputs
3. **Adaptive Thresholds**: Dynamic threshold adjustment based on historical accuracy
4. **Layer 6: Contextual Coherence**: Validate response coherence with conversation history
5. **Layer 7: Fact Verification**: Cross-reference answers with external knowledge bases
6. **Weighted Scoring**: Assign importance weights to layers based on query type

---

## Changelog

### 2025-11-17 - Initial Implementation
- Created `IRagValidationPipelineService` interface
- Implemented `RagValidationPipelineService` orchestrator
- Integrated all 5 validation layers
- Added standard and multi-model operation modes
- Registered service in DI container
- Wrote 14 comprehensive unit tests
- Documented pipeline architecture and usage

---

**Version**: 1.0
**Last Updated**: 2025-11-17
**Owner**: Engineering Team
