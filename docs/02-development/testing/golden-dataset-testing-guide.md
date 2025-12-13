# Golden Dataset Testing Guide

**Issue**: #999 (BGAI-059) - Quality test implementation for accuracy validation
**Created**: 2025-11-28
**Status**: Implemented

---

## Overview

This guide explains how to use the golden dataset testing infrastructure to validate RAG system accuracy against known correct answers.

**Purpose**: Measure RAG accuracy against 1000 expert-annotated Q&A pairs to ensure ≥80% accuracy target.

**Dataset Location**: `tests/data/golden_dataset.json`

---

## Components

### 1. Domain Models

**`GoldenDatasetTestCase`** (Value Object)
- Represents a single test case from the golden dataset
- Properties: Question, ExpectedKeywords, ExpectedCitations, ForbiddenKeywords, Difficulty, Category, GameId

**`AccuracyEvaluationResult`** (Value Object)
- Result of evaluating a RAG response against a test case
- Checks: Keyword matching, Citation validity, Forbidden keywords (hallucination)
- Verdict: `IsCorrect` = all checks pass

### 2. Domain Services

**`IGoldenDatasetLoader`**
- Loads and filters test cases from `golden_dataset.json`
- Methods:
  - `LoadAllAsync()`: Load all 1000 test cases
  - `LoadByGameAsync(gameId)`: Filter by specific game
  - `LoadByDifficultyAsync(difficulty)`: Filter by difficulty (easy/medium/hard)
  - `LoadByCategoryAsync(category)`: Filter by category (gameplay/setup/endgame/edge_cases)
  - `SampleAsync(count, stratified)`: Sample N cases (stratified by difficulty)

**`IRagAccuracyEvaluator`**
- Evaluates RAG responses against golden dataset expectations
- Methods:
  - `EvaluateTestCaseAsync(testCase, response)`: Evaluate single case
  - `CalculateAggregatedMetrics(results)`: Calculate overall accuracy
  - `CalculateMetricsByDifficulty/Category/Game(results)`: Group metrics

### 3. Test Suites

**Unit Tests** (`apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/`)
- `GoldenDatasetLoaderTests.cs`: 17 tests (JSON parsing, filtering, sampling)
- `RagAccuracyEvaluatorTests.cs`: 17 tests (evaluation logic, metrics calculation)
- **Execution**: `dotnet test --filter "FullyQualifiedName~GoldenDatasetLoader"`
- **Speed**: <200ms

**Integration Tests** (`apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/`)
- `GoldenDatasetAccuracyIntegrationTests.cs`: 7 tests (end-to-end validation)
- **Scenarios**: Perfect accuracy, threshold accuracy, below threshold, sampling, grouping
- **Execution**: `dotnet test --filter "FullyQualifiedName~GoldenDatasetAccuracyIntegration"`
- **Speed**: <150ms

**Full Evaluation Script** (`tools/run-golden-dataset-evaluation.ps1`)
- Runs all 1000 cases against live RAG API
- Generates detailed accuracy report
- **Cost**: ~$2-5 (OpenRouter API calls)
- **Time**: ~8-10 minutes

---

## Usage

### Quick Validation (Unit Tests)

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~GoldenDataset"
```

**Output**: 24 tests in <200ms

### Full Evaluation (Manual Script)

**Prerequisites**:
1. Start required services:
   ```bash
   cd infra && docker compose up postgres qdrant redis
   cd apps/api/src/Api && dotnet run
   ```

2. Verify API is running:
   ```bash
   curl http://localhost:8080/health
   ```

**Run Full Evaluation** (all 1000 cases):
```powershell
powershell.exe -ExecutionPolicy Bypass -File tools/run-golden-dataset-evaluation.ps1
```

**Run Sample Evaluation** (50 cases):
```powershell
powershell.exe -ExecutionPolicy Bypass -File tools/run-golden-dataset-evaluation.ps1 -SampleSize 50
```

**Dry Run** (check dataset without API calls):
```powershell
powershell.exe -ExecutionPolicy Bypass -File tools/run-golden-dataset-evaluation.ps1 -DryRun
```

---

## Expected Results

### Unit Tests (CI)
- **Target**: 100% pass rate
- **Current**: 24/24 passing
- **Coverage**: ≥90% on new code

### Integration Tests (CI)
- **Target**: All scenarios validated
- **Current**: 7/7 passing
- **Validation**: Loader + Evaluator integration

### Full Evaluation (Manual)
- **Target**: Accuracy ≥80% (800/1000 correct)
- **Current**: Not yet run (requires live API)
- **Frequency**: Before each release, weekly QA

---

## Interpreting Results

### Accuracy Tiers

| Tier | Range | Status | Action |
|------|-------|--------|--------|
| **Excellent** | ≥95% | ✅ | Maintain current settings |
| **Very Good** | 90-94% | ✅ | Minor improvements |
| **Good** | 80-89% | ✅ | Review failed cases |
| **Fair** | 70-79% | ⚠️ | Investigation needed |
| **Poor** | 60-69% | ❌ | Major fixes required |
| **Critical** | <60% | ❌ | System unreliable |

### Key Metrics

**Accuracy** = (Correct answers) / (Total test cases)
- Target: ≥0.80 (80%+)
- Measures overall system correctness

**Keyword Match Rate** = (Keywords found) / (Expected keywords)
- Indicates answer completeness
- Low rate → Missing information

**Citation Validity** = (Valid citations) / (Expected citations)
- Verifies source references
- Low rate → Wrong pages or missing snippets

**Forbidden Keywords** = Presence of hallucination indicators
- Any forbidden keyword → Immediate failure
- Example: "invented", "fake", "guarantee"

---

## Troubleshooting

### "Golden dataset file not found"
**Cause**: Repository root not detected correctly
**Fix**: Verify `.git` directory exists in repository root

### "API not available"
**Cause**: MeepleAI API not running
**Fix**: Start API with `cd apps/api/src/Api && dotnet run`

### "Accuracy below threshold"
**Cause**: RAG system returning incorrect answers
**Actions**:
1. Review report for failed cases pattern
2. Check difficulty distribution (are hard questions failing?)
3. Verify validation layers are enabled
4. Review prompt templates
5. Check embedding quality

### "High cost per run"
**Solution**: Use sampling for frequent validations
- Development: 50 cases (~$0.25, 2 min)
- Pre-release: 200 cases (~$1.00, 4 min)
- Release: 1000 cases (~$5.00, 10 min)

---

## Integration with CI/CD

### Current Setup

**CI Pipeline**:
- Unit tests: Always run (fast, free)
- Integration tests: Always run (fast, free)
- Full evaluation: Manual only (slow, costs money)

**Recommended Workflow**:
1. **Development**: Run unit tests after code changes
2. **PR Review**: Verify unit + integration tests pass
3. **Weekly QA**: Run sample evaluation (50-100 cases)
4. **Pre-Release**: Run full evaluation (1000 cases)
5. **Production**: Track accuracy over time (Prometheus metrics)

---

## Extending the Dataset

### Adding New Test Cases

Edit `tests/data/golden_dataset.json`:

```json
{
  "id": "new_001",
  "question": "Your question in Italian?",
  "expected_answer_keywords": ["keyword1", "keyword2"],
  "expected_citations": [
    {
      "page": 5,
      "snippet_contains": "exact text from rulebook"
    }
  ],
  "forbidden_keywords": ["invented", "fake"],
  "difficulty": "easy",
  "category": "gameplay",
  "annotated_by": "expert_name",
  "annotated_at": "2025-11-28T10:00:00Z"
}
```

### Guidelines

**Good Test Case**:
- Clear, specific question
- 2-4 expected keywords (key concepts)
- Precise citation page numbers
- Relevant forbidden keywords (common hallucinations)
- Proper difficulty classification

**Bad Test Case**:
- Vague question
- Too many keywords (>5)
- Missing citations
- No forbidden keywords

---

## Metrics Tracking

### Quality Framework Integration

Golden dataset evaluation feeds into Month 4 quality framework:
- Metric 1: **Accuracy** (from golden dataset)
- Metric 2: **P@10** (from retrieval tests)
- Metric 3: **MRR** (from ranking tests)
- Metric 4: **Confidence** (from validation pipeline)
- Metric 5: **Hallucination Rate** (from forbidden keywords)

### Prometheus Export

Metrics exported to `/metrics` endpoint:
```
meepleai.quality.accuracy{test_type="golden_dataset",difficulty="easy"} 0.95
meepleai.quality.accuracy{test_type="golden_dataset",difficulty="medium"} 0.82
meepleai.quality.accuracy{test_type="golden_dataset",difficulty="hard"} 0.73
```

---

## References

- **Issue #999**: BGAI-059 - Quality test implementation
- **Golden Dataset**: `tests/data/golden_dataset.json`
- **Adversarial Dataset**: `tests/data/adversarial_dataset.json` (hallucination testing)
- **ADR-001**: Hybrid RAG Architecture
- **Test Writing Guide**: `docs/02-development/testing/test-writing-guide.md`

---

## Maintenance

**Weekly**:
- Review accuracy trends
- Add new test cases for discovered edge cases
- Update expected answers if rulebooks change

**Monthly**:
- Full evaluation run
- Compare accuracy over time
- Refine validation thresholds

**Quarterly**:
- Expand dataset (new games, more cases)
- Review and retire obsolete test cases
- Update difficulty classifications

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Maintainer**: QA Team
**Version**: 1.0

