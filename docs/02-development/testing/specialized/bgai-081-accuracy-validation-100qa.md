# BGAI-081: Accuracy Validation (80% Target on 100+ Q&A)

**Status**: Implemented
**Issue**: #1019
**Date**: 2025-12-04
**Target**: ≥80% accuracy on 100+ expert-annotated Q&A pairs

---

## Overview

This document describes the implementation of BGAI-081, which validates RAG system accuracy on the complete expert-annotated dataset (110 Q&A pairs) to verify ≥80% threshold compliance.

**Goal**: Confirm system meets quality baseline across all expert-annotated content before Phase 1A completion.

---

## Dataset Composition

### Expert-Annotated Q&A (110 pairs total)

| Game | Count | Annotator | Issue |
|------|-------|-----------|-------|
| Terraforming Mars | 20 | expert_human_bgai056 | BGAI-056 |
| Wingspan | 15 | expert_wingspan_bgai057 | BGAI-057 |
| Azul | 15 | expert_azul_bgai058 | BGAI-058 |
| Catan | 15 | expert_bgai070 | BGAI-070 |
| Ticket to Ride | 15 | expert_bgai070 | BGAI-070 |
| 7 Wonders | 10 | expert_bgai071 | BGAI-071 |
| Agricola | 10 | expert_bgai071 | BGAI-071 |
| Splendor | 10 | expert_bgai071 | BGAI-071 |
| **Total** | **110** | | |

### Difficulty Distribution
- Easy: ~40%
- Medium: ~40%
- Hard: ~20%

### Category Distribution
- Gameplay: ~50%
- Setup: ~20%
- Scoring: ~15%
- Edge Cases: ~15%

---

## Test Implementation

### Test Class

**Location**: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/FirstAccuracyBaselineTest.cs`

**Method**: `RunAccuracyValidation_AllExpertAnnotated_MeetsThreshold`

### Key Features

1. **Loads all expert-annotated cases** (excludes template-generated)
2. **Calls live RAG API** at `/api/v1/knowledge-base/ask`
3. **Evaluates responses** against expected keywords, citations, and hallucination checks
4. **Reports metrics** by game, difficulty, and category
5. **Asserts ≥80% threshold**

### Test Traits

```csharp
[Fact(Timeout = 1200000)] // 20 min timeout
[Trait("Issue", "1019")]
[Trait("Category", "Manual")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Priority", "P1")]
```

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

4. **Ensure indexed games**:
   - At minimum: Azul, Wingspan (for partial validation)
   - Ideally: All 8 games for complete validation

### Execute Test

```bash
cd apps/api/tests/Api.Tests

# Full 110 Q&A validation (BGAI-081)
dotnet test --filter "FullyQualifiedName~RunAccuracyValidation_AllExpertAnnotated_MeetsThreshold" --logger "console;verbosity=detailed"
```

### Execution Metrics

| Metric | Value |
|--------|-------|
| Questions | 110 |
| Execution Time | ~15-20 minutes |
| Cost Estimate | ~$0.50-0.75 (OpenRouter) |
| Timeout | 20 minutes |

---

## Expected Results

### Success Criteria

**Target**: Accuracy ≥80% (minimum 88/110 correct)

### Quality Levels

| Accuracy | Level | Description |
|----------|-------|-------------|
| ≥95% | Excellent | Production-ready quality |
| ≥90% | Very Good | Minor improvements needed |
| ≥85% | Good | Acceptable for MVP |
| ≥80% | Fair | Meets minimum threshold |
| <80% | Poor | Requires investigation |

### Metrics Reported

- Overall Accuracy, Precision, Recall, F1-Score
- Breakdown by Difficulty (easy/medium/hard)
- Breakdown by Game (8 games)
- Breakdown by Category (gameplay/setup/scoring/edge_cases)

---

## Sample Output

```
=== BGAI-081: Accuracy Validation on 100+ Expert Q&A ===
API Base URL: http://localhost:8080
Target: ≥80% accuracy on 110 expert-annotated Q&A pairs
✅ API health check passed

--- Loading Expert-Annotated Test Cases (All 110) ---
Loaded 110 expert-annotated test cases
  - terraforming-mars: 20 cases
  - wingspan: 15 cases
  - azul: 15 cases
  - catan: 15 cases
  - ticket-to-ride: 15 cases
  - 7-wonders: 10 cases
  - agricola: 10 cases
  - splendor: 10 cases

--- Running Accuracy Validation on Live RAG API ---
[1/110] Testing: terraforming-mars_expert_001
  Question: Come si gioca una carta azione?
  ✅ Evaluation: CORRECT
...

=== BGAI-081: ACCURACY VALIDATION RESULTS ===
Total execution time: 18:32

--- Overall Metrics ---
Overall Accuracy: 84.55%
True Positives: 93
False Negatives: 17
Precision: 100.00%
Recall: 84.55%
F1-Score: 91.63%
Meets Baseline (≥80%): True
Quality Level: Good

--- Accuracy by Game ---
azul: 93.33% (14/15 correct)
wingspan: 86.67% (13/15 correct)
catan: 86.67% (13/15 correct)
...

=== BGAI-081 TEST RESULT ===
✅ PASSED: Accuracy 84.55% meets ≥80% threshold
   93/110 questions answered correctly
```

---

## Troubleshooting

### "Loaded fewer than 100 test cases"

**Cause**: Template filtering not working or golden_dataset.json outdated

**Fix**:
1. Verify dataset version: check `metadata.total_test_cases` in golden_dataset.json
2. Verify expert annotators exist in dataset
3. Regenerate dataset if needed

### "Unknown game slug: X"

**Cause**: Game not registered in API database

**Fix**:
1. Add game via admin interface or API
2. Index game rulebook PDF
3. Verify game appears in `/api/v1/games` response

### "Accuracy below 80% threshold"

**Investigation Steps**:
1. Review failed test cases (which games/categories)
2. Check if specific annotator has lower quality
3. Verify rulebook PDFs are indexed correctly
4. Test failed questions manually via Postman
5. Review RAG retrieval logs for missed chunks

---

## Relationship to Other Issues

| Issue | Relationship |
|-------|--------------|
| #1000 (BGAI-060) | Previous baseline (50 Q&A) |
| #1019 (BGAI-081) | This validation (110 Q&A) |
| BGAI-056/057/058 | Expert annotation sources (Phase 1) |
| BGAI-070/071 | Expert annotation sources (Phase 2) |
| ADR-006 | Multi-layer validation architecture |

---

## Next Steps After Validation

1. **If PASSED (≥80%)**:
   - Update Issue #1019 DoD
   - Close issue as resolved
   - Document baseline metrics
   - Schedule weekly re-validation

2. **If FAILED (<80%)**:
   - Analyze failure patterns by game/difficulty
   - Create improvement issues for specific areas
   - Review RAG configuration (retrieval, prompts)
   - Re-run after fixes

---

## CI Integration Note

This test is marked `[Trait("Category", "Manual")]` and does NOT run in CI because:
- Requires live external services (Qdrant, OpenRouter)
- Has non-trivial cost (~$0.50 per run)
- Execution time (~15-20 min) exceeds CI timeout

For CI coverage, use unit tests that mock the RAG API.

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Implemented By**: Engineering Lead (Claude)
**Status**: Ready for Manual Execution

