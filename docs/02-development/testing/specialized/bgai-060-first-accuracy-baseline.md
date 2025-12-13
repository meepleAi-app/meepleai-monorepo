# BGAI-060: First Accuracy Baseline Measurement

**Status**: ✅ Implemented
**Issue**: #1000
**Date**: 2025-11-30
**Target**: ≥80% accuracy on 50 expert-annotated Q&A pairs

---

## Overview

This document describes the implementation of BGAI-060, which runs the first accuracy baseline measurement on 50 expert-annotated Q&A pairs to establish system quality metrics.

**Goal**: Measure RAG system accuracy against known correct answers to verify ≥80% threshold compliance before wider deployment.

---

## Test Dataset

**50 Expert-Annotated Q&A Pairs**:
- 20 Terraforming Mars (annotator: `expert_human_bgai056`)
- 15 Wingspan (annotator: `expert_wingspan_bgai057`)
- 15 Azul (annotator: `expert_azul_bgai058`)

**Source**: `tests/data/golden_dataset.json`

---

## Implementation Components

### 1. GoldenDatasetLoader Enhancement

**New Method**: `LoadByAnnotatorAsync`

```csharp
Task<IReadOnlyList<GoldenDatasetTestCase>> LoadByAnnotatorAsync(
    string annotator,
    bool exclude = false,
    CancellationToken cancellationToken = default);
```

**Purpose**: Filter test cases by annotator to isolate expert-annotated vs template-generated cases.

**Usage**:
```csharp
// Load only expert-annotated (exclude template-generated)
var expertCases = await loader.LoadByAnnotatorAsync(
    annotator: "template_generator_alpha",
    exclude: true
);
// Returns: 50 expert cases (20 TM + 15 Wingspan + 15 Azul)
```

**Location**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/QualityTracking/GoldenDatasetLoader.cs:244-264`

---

### 2. FirstAccuracyBaselineTest

**Test Class**: `FirstAccuracyBaselineTest`

**Location**: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/FirstAccuracyBaselineTest.cs`

**Purpose**: Manual integration test that calls live RAG API to measure real accuracy.

**Traits**:
- `[Trait("Category", "Manual")]` - Not run in CI (requires live services)
- `[Trait("Issue", "1000")]` - Linked to Issue #1000
- `[Trait("Priority", "P1")]` - High priority baseline measurement

---

## Running the Test

### Prerequisites

1. **Start required services**:
   ```bash
   cd infra
   docker compose up -d postgres qdrant redis
   ```

2. **Start API**:
   ```bash
   cd apps/api/src/Api
   dotnet run
   ```

3. **Verify services**:
   ```bash
   curl http://localhost:8080/health
   # Expected: {"status":"Healthy"}
   ```

4. **Verify OpenRouter API key**:
   ```bash
   # Check environment variable or appsettings.json
   echo $OPENROUTER_API_KEY
   ```

### Execute Test

```bash
cd apps/api/tests/Api.Tests

dotnet test --filter "FullyQualifiedName~FirstAccuracyBaselineTest" --logger "console;verbosity=detailed"
```

**Execution Time**: ~8-10 minutes (50 questions × ~10s per RAG call)
**Cost Estimate**: ~$0.25-0.50 (OpenRouter API calls)

---

## Expected Results

### Success Criteria

**Target**: Accuracy ≥80% (minimum 40/50 correct)

**Metrics Reported**:
- Overall Accuracy
- True Positives / False Negatives / False Positives / True Negatives
- Precision, Recall, F1-Score
- Meets Baseline Threshold (boolean)
- Quality Level (Excellent/VeryGood/Good/Fair/Poor/Critical)

**Breakdowns**:
- Accuracy by Difficulty (easy/medium/hard)
- Accuracy by Game (terraforming-mars/wingspan/azul)
- Accuracy by Category (gameplay/setup/endgame/edge_cases)

---

## Sample Output

```
=== BGAI-060: First Accuracy Baseline Measurement ===
API Base URL: http://localhost:8080
✅ API health check passed

--- Loading Expert-Annotated Test Cases ---
Loaded 50 expert-annotated test cases
  - terraforming-mars: 20 cases
  - wingspan: 15 cases
  - azul: 15 cases

--- Running Accuracy Test on Live RAG API ---

[1/50] Testing: terraforming-mars_expert_001
  Question: Come si gioca una carta azione?
  Game: terraforming-mars | Difficulty: easy | Category: gameplay
  RAG Answer: Per giocare una carta azione, si paga il costo indicato e si applica l'effetto...
  Confidence: 0.85
  ✅ Evaluation: CORRECT
     - Keywords Match: 100%
     - Citations Valid: True
     - No Hallucinations: True

[2/50] Testing: terraforming-mars_expert_002
  ...

=== ACCURACY METRICS ===

Overall Accuracy: 88.00%
True Positives: 44
True Negatives: 0
False Positives: 0
False Negatives: 6
Precision: 100.00%
Recall: 88.00%
F1-Score: 93.62%
Meets Baseline (≥80%): True
Quality Level: Good

--- Accuracy by Difficulty ---
easy: 95.45% (21/22 correct)
medium: 84.21% (16/19 correct)
hard: 77.78% (7/9 correct)

--- Accuracy by Game ---
terraforming-mars: 90.00% (18/20 correct)
wingspan: 86.67% (13/15 correct)
azul: 86.67% (13/15 correct)

--- Accuracy by Category ---
gameplay: 90.00% (27/30 correct)
setup: 83.33% (10/12 correct)
edge_cases: 87.50% (7/8 correct)

=== TEST RESULT ===
✅ PASSED: Accuracy 88.00% meets ≥80% threshold
```

---

## Troubleshooting

### "API not available at http://localhost:8080"

**Cause**: API not running or services not started

**Fix**:
1. Start Docker services: `cd infra && docker compose up -d postgres qdrant redis`
2. Start API: `cd apps/api/src/Api && dotnet run`
3. Verify health: `curl http://localhost:8080/health`

---

### "Test timeout after 10 minutes"

**Cause**: LLM API slow or rate limiting

**Fix**:
1. Check OpenRouter API status
2. Verify no rate limiting in logs
3. Increase timeout if needed: `[Fact(Timeout = 900000)]` (15 min)

---

### "Accuracy below 80% threshold"

**Possible Causes**:
- Expert annotations incorrect
- RAG system misconfigured
- Vector embeddings not indexed correctly
- Validation layers too strict

**Investigation Steps**:
1. Review failed test cases in output logs
2. Check which games/difficulties failed
3. Manually test failed questions via Postman
4. Review validation layer configuration
5. Check prompt templates

---

### "Could not find golden_dataset.json"

**Cause**: Repository root detection failed

**Fix**:
- Verify `.git` directory exists at repository root
- Check dataset exists: `ls tests/data/golden_dataset.json`
- Run test from repository root directory

---

## Unit Tests (CI-Safe)

Unit tests for `LoadByAnnotatorAsync` run in CI without external dependencies:

```bash
dotnet test --filter "FullyQualifiedName~LoadByAnnotatorAsync"
```

**Tests**:
1. `LoadByAnnotatorAsync_ExcludeTemplateGenerated_ReturnsExpertAnnotatedOnly` - Verifies expert filter
2. `LoadByAnnotatorAsync_IncludeTemplateGenerated_ReturnsTemplateOnly` - Verifies template filter
3. `LoadByAnnotatorAsync_WithEmptyAnnotator_ThrowsArgumentException` - Input validation
4. `LoadByAnnotatorAsync_WithNonExistentAnnotator_ReturnsEmpty` - Edge case handling

**Expected**: 4/4 passing in <500ms

---

## Integration with Validation Framework

The baseline measurement integrates with the existing validation accuracy framework (BGAI-039):

```csharp
// After running FirstAccuracyBaselineTest
var command = new MeasureValidationAccuracyCommand
{
    Context = "First Accuracy Baseline",
    DatasetId = "expert-annotated-50",
    EvaluationId = evaluationResult.Id,
    ExpectedValidCount = 50, // All 50 should be valid
    StoreBaseline = true
};

var baseline = await mediator.Send(command);
// Stores in validation_accuracy_baselines table
```

---

## Files Modified

| File | Changes |
|------|---------|
| `GoldenDatasetLoader.cs` | Added `LoadByAnnotatorAsync` method |
| `GoldenDatasetLoaderTests.cs` | Added 4 unit tests + helper methods |
| `FirstAccuracyBaselineTest.cs` | **NEW** - Manual integration test |

---

## Next Steps

1. **Run Baseline Test** (Manual):
   ```bash
   # Start services
   cd infra && docker compose up -d postgres qdrant redis
   cd apps/api/src/Api && dotnet run

   # Run test (separate terminal)
   cd apps/api/tests/Api.Tests
   dotnet test --filter "FullyQualifiedName~FirstAccuracyBaselineTest"
   ```

2. **Record Baseline Metrics**:
   - Document accuracy percentage
   - Save detailed report
   - Identify improvement areas

3. **Update Issue #1000**:
   - Mark DoD items complete
   - Record baseline results
   - Close issue if ≥80% achieved

4. **Continuous Monitoring**:
   - Run baseline test weekly during development
   - Track accuracy trends over time
   - Re-run after major RAG changes

---

## References

- **Issue #1000**: [P1] [BGAI-060] Run first accuracy test (baseline measurement)
- **BGAI-039**: Validation Accuracy Baseline Framework
- **BGAI-059**: Quality test implementation (golden dataset infrastructure)
- **ADR-006**: Multi-Layer Validation Architecture
- **Golden Dataset Guide**: `docs/02-development/testing/golden-dataset-testing-guide.md`

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Implemented By**: Engineering Lead (Claude)
**Status**: Ready for Manual Execution

