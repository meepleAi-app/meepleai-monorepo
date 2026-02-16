# RAG Evaluation Baseline Report - February 2026

**Phase**: ADR-024 Phase 0 - Dataset Creation
**Date**: 2026-02-16
**Status**: Dataset Ready (Pre-Evaluation Baseline)

## Executive Summary

This report establishes the Phase 0 baseline for MeepleAI's RAG evaluation framework. At this stage, the evaluation datasets have been created and validated but the full pipeline evaluation has not yet been run (requires live RAG service with indexed game rulebooks).

The datasets are ready for baseline metric collection once game documents are ingested into the RAG pipeline.

## Dataset Summary

### Combined Dataset Statistics

| Metric | Value |
|--------|-------|
| Total Q&A Pairs | 60 |
| Mozilla Format Samples | 25 |
| MeepleAI Custom Samples | 35 |
| Unique Games Covered | 8 |
| Difficulty: Easy | 17 (28%) |
| Difficulty: Medium | 29 (48%) |
| Difficulty: Hard | 14 (23%) |
| Validation | PASSED (>= 30 minimum) |

### Game Coverage

| Game | Complexity Tier | Sample Count | Categories |
|------|----------------|--------------|------------|
| Catan | Simple | 15 | setup, gameplay, scoring |
| Ticket to Ride | Simple | 4 | setup, gameplay |
| Pandemic | Medium | 9 | setup, gameplay, scoring |
| Carcassonne | Medium | 3 | gameplay, scoring |
| Azul | Medium | 3 | setup, gameplay |
| 7 Wonders | Medium | 3 | setup, gameplay |
| Terraforming Mars | Medium | 10 | setup, gameplay, scoring, edge_cases |
| Spirit Island | Complex | 17 | setup, gameplay, scoring, edge_cases |

### Difficulty Distribution by Source

| Difficulty | Mozilla | Custom | Total |
|-----------|---------|--------|-------|
| Easy | 7 | 10 | 17 |
| Medium | 14 | 15 | 29 |
| Hard | 4 | 10 | 14 |

## Evaluation Metrics (Pending)

The following metrics will be collected once game rulebooks are indexed:

| Metric | Phase 0 Target | Phase 4 Target | Phase 5 Target |
|--------|---------------|----------------|----------------|
| Recall@5 | Establish baseline | >= 50% | >= 60% |
| Recall@10 | Establish baseline | >= 60% | >= 70% |
| nDCG@10 | Establish baseline | >= 50% | >= 60% |
| MRR | Establish baseline | >= 50% | >= 60% |
| Answer Correctness | Establish baseline | >= 60% | >= 70% |
| P95 Latency | Establish baseline | < 2000ms | < 1500ms |

## Infrastructure Readiness

### Domain Models
- `EvaluationDataset` - Dataset aggregate with JSON serialization, validation, merge
- `EvaluationSample` - Sample record with Mozilla/MeepleAI factory methods
- `EvaluationMetrics` - Metrics value object with phase target validation
- `EvaluationResult` - Complete result with per-difficulty/category/game breakdowns
- `EvaluationSampleResult` - Per-sample results with retrieval metrics

### Services
- `IDatasetEvaluationService` - Interface with Recall@K, nDCG@K, MRR calculation
- `DatasetEvaluationService` - Full implementation with keyword-based answer correctness

### CQRS Handlers
- `RunEvaluationCommand` / `RunEvaluationCommandHandler` - Execute evaluation run
- `GetEvaluationResultsQuery` / `GetEvaluationResultsQueryHandler` - Retrieve results

### Test Coverage
- `EvaluationDatasetTests` - Domain model unit tests (create, validate, filter, merge, JSON)
- `DatasetEvaluationServiceMetricTests` - Metric calculation unit tests (Recall@K, nDCG@K, MRR)

## Dataset Files

| File | Location | Format |
|------|----------|--------|
| Mozilla Dataset | `tests/evaluation-datasets/mozilla-boardgames.json` | Mozilla Structured QA |
| Custom Dataset | `tests/evaluation-datasets/meepleai-custom.json` | MeepleAI Custom |
| Documentation | `tests/evaluation-datasets/README.md` | Markdown |

## Next Steps

1. **Phase 1**: Index game rulebook PDFs for Catan, Ticket to Ride, Pandemic, Carcassonne, Azul, 7 Wonders
2. **Phase 2**: Index Terraforming Mars and Spirit Island rulebooks
3. **Phase 3**: Run baseline evaluation to populate metrics
4. **Phase 4**: Optimize retrieval to meet Recall@10 >= 60% target
5. **Phase 5**: Optimize latency to meet P95 < 1500ms target

## Acceptance Criteria Status

- [x] 30+ Q&A pairs created (60 total)
- [x] Mozilla format subset (25 samples)
- [x] MeepleAI custom format (35 samples)
- [x] Three complexity tiers (Catan/simple, Terraforming Mars/medium, Spirit Island/complex)
- [x] Difficulty distribution (easy: 28%, medium: 48%, hard: 23%)
- [x] Category coverage (setup, gameplay, scoring, edge_cases)
- [x] Dataset validation passes (>= 30 samples)
- [x] JSON round-trip serialization tested
- [x] Unit tests for domain models and metric calculations
- [x] Baseline report created
- [ ] Baseline metrics collected (requires indexed rulebooks - Phase 1+)
