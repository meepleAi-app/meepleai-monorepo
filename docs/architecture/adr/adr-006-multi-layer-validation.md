# ADR-006: Multi-Layer Validation Architecture for AI Responses

**Status**: ✅ Accepted (Implemented + Optimized)
**Date**: 2025-11-17
**Last Updated**: 2025-12-13T10:59:23.970Z
**Deciders**: Engineering Lead, ML Engineer
**Context**: Phase 1 MVP - Quality Assurance System
**Related**: ADR-001 (Hybrid RAG), ADR-005 (Cosine Similarity), BGAI-028 to BGAI-033, BGAI-037

---

## Context

MeepleAI's mission-critical requirement is achieving >95% accuracy on board game rules Q&A with **zero tolerance for hallucinations**. The constraint "one mistake ruins game session" means users will abandon the system after a single incorrect answer during competitive play.

**Problem Statement**:
- Traditional single-validation approaches are insufficient for high-stakes use cases
- LLMs hallucinate when uncertain (invent non-existent rules)
- Citation errors lead to user distrust
- No standardized quality thresholds across the pipeline
- Board game rules require absolute accuracy (unlike general Q&A where "close enough" suffices)

**Requirements**:
1. Multi-layer defense against hallucinations (redundancy principle)
2. Measurable quality thresholds at each validation stage
3. Clear pass/fail criteria for AI-generated responses
4. Multilingual support (Italian-first per ADR-002)
5. Domain-driven design with pure domain services (no infrastructure coupling)
6. <3% hallucination rate (target: <1% in production)

---

## Decision

Implement a **5-Layer Validation Architecture** with progressive quality gates, each enforcing specific quality criteria before responses reach end users.

### Architecture Overview

**5 Progressive Quality Gates**:

| Layer | Service | Check | Threshold | Fail Action |
|-------|---------|-------|-----------|-------------|
| **1. Confidence** | ConfidenceValidationService | LLM confidence score | ≥0.70 | Return "uncertain" message |
| **2. Consensus** | MultiModelValidationService | TF-IDF cosine similarity (GPT-4 + Claude) | ≥0.90 | Log warning, flag disagreement |
| **3. Citation** | CitationValidationService | PDF exists, page in range, format valid | 100% valid | Strip invalid citations |
| **4. Hallucination** | HallucinationDetectionService | Forbidden keywords (5 languages) | 0 keywords | Return "uncertain" or flag |
| **5. Feedback** | User review | Thumbs up/down, report error | N/A | Update metrics, flag negative |

**Flow**: Input (RAG) → GPT-4 → L1 → L2 (adaptive) → L3 → L4 → Output → L5 (post-response)

---

## Implementation Details

### Layer 1: Confidence Validation

**Service**: `ConfidenceValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-028 (#970)

**Threshold Tiers**:
- ≥0.70: PASS (meets accuracy target)
- 0.60-0.69: WARNING (acceptable but flagged)
- <0.60: CRITICAL (reject response)

**Logic**: Check LLM self-reported confidence score, return explicit uncertainty if below threshold

**Calibration**: 0.70 threshold correlates to >95% accuracy (empirical testing on board game rulebook corpus)

---

### Layer 2: Multi-Model Consensus Validation

**Service**: `MultiModelValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issues**: BGAI-032 (#974), BGAI-033 (#975)

**Algorithm**: TF-IDF Cosine Similarity (see ADR-005 for detailed explanation)

**Process**:
1. Query GPT-4 and Claude in parallel with identical prompts
2. Extract response text from both models
3. Calculate TF-IDF vectors for both responses
4. Compute cosine similarity: `(A · B) / (||A|| × ||B||)`
5. Check if similarity ≥ 0.90

**Consensus Thresholds**:
```csharp
public const double MinimumConsensusThreshold = 0.90;  // High consensus
// ≥0.90: High (PASS)
// 0.70-0.89: Moderate (WARNING)
// 0.50-0.69: Low (FAIL)
// <0.50: None (CRITICAL)
```

**Performance**:
- Parallel execution: ~2.5s for both models (vs. 1.5s single model)
- Similarity calculation: <10ms (in-memory, no external APIs)

**Cost Mitigation**:
- Skip consensus if primary confidence ≥0.90 (adaptive validation)
- Semantic cache: 40-60% cache hit rate

---

### Layer 3: Citation Validation

**Service**: `CitationValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-029 (#971)

**Validation Rules**:
1. Parse game ID (GUID format validation)
2. Fetch PDF documents for game (single optimized query)
3. Validate each citation:
   - Format check: "PDF:guid" pattern
   - Document existence: PDF ID in database
   - Page range: 1 ≤ page ≤ document.PageCount

**Error Types**:
- `MalformedSource`: Invalid citation format
- `DocumentNotFound`: PDF not in database
- `InvalidPageNumber`: Page out of range

**Performance**:
- Single query for all PDFs (avoid N+1)
- AsNoTracking (read-only)
- Dictionary lookup (O(1) checks)

---

### Layer 4: Hallucination Detection

**Service**: `HallucinationDetectionService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-030 (#972)

**Multilingual Keyword Dictionaries** (Italian-first per ADR-002):

| Language | Keywords | Examples |
|----------|----------|----------|
| Italian (IT) | 15 | "non lo so", "non sono sicuro", "poco chiaro" |
| English (EN) | 14 | "I don't know", "I'm not sure", "unclear" |
| German (DE) | 9 | "Ich weiß nicht", "unklar", "vielleicht" |
| French (FR) | 10 | "Je ne sais pas", "peu clair", "peut-être" |
| Spanish (ES) | 10 | "No lo sé", "poco claro", "tal vez" |

**Detection Logic**:
1. Auto-detect language (or use provided)
2. Get language-specific forbidden keywords
3. Check for keywords (case-insensitive)
4. Calculate severity based on count + critical phrase detection

**Severity Levels**:
- **None**: 0 keywords (valid)
- **Low**: 1-2 keywords (acceptable)
- **Medium**: 3-4 keywords (flag for review)
- **High**: 5+ keywords OR critical phrases ("don't know", "cannot find")

**Critical Phrases** (immediate failure):
- "don't know", "non lo so", "ne sais pas", "weiß nicht", "no lo sé"
- "cannot find", "non riesco", "ne trouve pas", "kann nicht", "no puedo"

---

### Layer 5: User Feedback Loop

**Mechanism**: Post-response quality monitoring

**Feedback Types**:
1. **Thumbs Up/Down**: Simple quality indicator
2. **Report Error**: Specific issue flagging
3. **Correction Submission**: User provides correct answer

**Actions**:
- Update Prometheus metrics (`qa_user_feedback_total`, `qa_accuracy_by_game`)
- Flag for expert review (negative feedback triggers alert)
- Add corrections to fine-tuning dataset (continuous learning)

**Monitoring**:
- Real-time Grafana dashboards (validation pass/fail rates)
- Alert triggers: hallucination rate >5%, accuracy <90%
- Confidence distribution tracking

---

## PDF Quality Validation (Separate Pipeline)

**Service**: `PdfQualityValidationDomainService.cs` (DocumentProcessing BC)
**Issue**: BGAI-012 (#951)

**4-Metric Quality Score**:
- TextCoverage (40%): Chars per page (≥1000 chars = optimal)
- StructureDetection (20%): Title, headers, paragraphs, lists detected
- TableDetection (20%): Number of tables found
- PageCoverage (20%): Processed pages / total pages

**Formula**: `(TextCoverage × 0.40) + (Structure × 0.20) + (Tables × 0.20) + (PageCoverage × 0.20)`

**Thresholds**:
- ≥0.80: PASS (Stage 1 extraction sufficient)
- 0.70-0.79: WARNING (Stage 2 fallback recommended)
- 0.50-0.69: CRITICAL (Stage 3 fallback required)
- <0.50: REJECT (document likely corrupted)

**3-Stage Orchestration** (ADR-003b):
1. Unstructured (1.3s) → Pass if ≥0.80
2. SmolDocling VLM (3-5s) → Pass if ≥0.70
3. Docnet fallback → Return best effort

**Report**: Quality level, metrics, recommendations. See `PdfQualityReport` record for full structure.

---

## Domain-Driven Design Architecture

**Service Locations**:
- `BoundedContexts/KnowledgeBase/Domain/Services/`: 4 validation services + interfaces
- `BoundedContexts/DocumentProcessing/Domain/Services/`: PDF quality validation

**Dependency Injection**:
- Scoped: 4 validation services (Confidence, MultiModel, Citation, Hallucination)
- Singleton: CosineSimilarityCalculator (stateless helper)

**Usage**: See `AskQuestionQueryHandler.cs` for complete validation pipeline integration pattern

---

## Validation Flow Example

**Question**: "Can I castle after moving my king?"

1. **RAG Retrieval**: Hybrid search (vector + keyword)
2. **GPT-4 Generation**: Answer + confidence 0.85 + citations [PDF:123, p.5]
3. **Layer 1**: Confidence 0.85 ≥ 0.70 → PASS ✓
4. **Layer 2**: Adaptive consensus (0.85 < 0.90) → Query Claude → Similarity 0.93 → PASS ✓
5. **Layer 3**: PDF:123 exists, page 5 in range → PASS ✓
6. **Layer 4**: 0 forbidden keywords detected → PASS ✓
7. **Output**: Validated response to user
8. **Layer 5**: User feedback (thumbs up/down) → Update metrics

---

## Consequences

### Positive

✅ **High Accuracy** (>95% target achievable)
- Multi-layer defense catches errors that single-validation would miss
- Empirical testing shows 20-30 point improvement over single LLM baseline
- Consensus validation reduces hallucination rate to <3%

✅ **User Trust**
- Explicit uncertainty preferred over confident wrong answers
- Citations enable independent verification
- Transparent validation status ("Validato da 2 modelli AI")

✅ **Competitive Differentiation**
- Only board game AI system with multi-model validation
- Quality-first positioning vs. competitors (45-75% accuracy)

✅ **Domain-Driven Design**
- Pure domain services (testable without infrastructure)
- Clear bounded contexts (KnowledgeBase, DocumentProcessing)
- Interface-based design (swappable implementations)

✅ **Monitoring & Observability**
- Per-layer metrics (Prometheus/Grafana)
- Validation funnel analysis (conversion rates)
- Real-time quality tracking

✅ **Multilingual Support**
- Italian-first design (ADR-002)
- 5 languages supported (IT, EN, DE, FR, ES)
- Language-specific hallucination detection

### Negative

⚠️ **Increased Latency** (+500-800ms)
- Single LLM: ~1.5s P95
- With consensus: ~2.5s P95 (parallel execution)
- Mitigation: Adaptive validation (skip consensus if confidence ≥0.90)

⚠️ **Increased Cost** (~2x for consensus cases)
- Single LLM: $0.02/query
- Dual LLM: $0.04/query
- Mitigation:
  - Semantic caching (40-60% hit rate)
  - Adaptive validation (30% skip consensus)
  - Ollama fallback for free operation

⚠️ **Complexity** (+30% codebase size)
- Single validation: ~200 LOC
- Multi-layer: ~650 LOC (5 services)
- Mitigation:
  - Modular design (each layer = isolated service)
  - Comprehensive testing (90%+ coverage)
  - Clear documentation (this ADR)

⚠️ **False Negatives** (rare but possible)
- Both models may agree on incorrect answer (~1-2% cases)
- Forbidden keywords may miss sophisticated hallucinations
- Mitigation:
  - Layer 5 user feedback catches these cases
  - Continuous keyword dictionary updates
  - Human expert escalation for negative feedback

---

## Validation Metrics & Thresholds

### Success Criteria

| Phase | Accuracy | Hallucination Rate | P95 Latency | Dataset |
|-------|----------|-------------------|-------------|---------|
| **Phase 1 (MVP)** | ≥80% ✅ | ≤10% ✅ | ≤5s ✅ | 100 Q&A, 10 games |
| **Phase 2 (Production)** | ≥90% | ≤5% | ≤3s | 500 Q&A, 20 games |
| **Phase 3 (Gold)** | ≥95% | ≤3% (target <1%) | ≤3s | 1000 Q&A, 50+ games |

---

## Testing

**148 unit tests** (all passing): 5 KnowledgeBase services + 2 DocumentProcessing services
**20 integration tests**: DI orchestration, PDF extraction pipelines
**35 E2E tests**: Full validation pipeline, multi-language, adaptive consensus

**Coverage**: 90%+ domain services, 85%+ application handlers

---

## Rollback Plan

**4 Options** (ordered by preference):
1. **Disable Consensus**: Skip Layer 2, keep 1/3/4 (−800ms, −5-10% accuracy)
2. **Increase Threshold**: Raise L1 to 0.80-0.85 (50% fewer consensus calls)
3. **Async Validation**: Return immediately, validate in background (faster UX, potential corrections)
4. **Feature Flag Disable**: Revert to single LLM (document regression)

**Triggers**: P95 latency >5s (10min), error rate >2%, user complaints >10/day

---

## Performance Optimization (BGAI-037) ✅

**Issue**: #979 | **Status**: Implemented (2025-11-17) | **Impact**: 30-66% latency reduction

### Parallel Validation Execution

**Standard Mode (3 layers)**: Layer 1 synchronous → Layers 3 & 4 parallel (`Task.WhenAll`)

**Multi-Model Mode (4 layers)**: Layer 1 synchronous → Layers 2, 3 parallel → Layer 4 chained to Layer 2

### Performance Improvements

| Mode | Before | After | Improvement |
|------|--------|-------|-------------|
| Standard | 200-300ms | 100-150ms | 50-66% faster |
| Multi-Model | 600-800ms | 400-500ms | 30-40% faster |

**Implementation**: See `RagValidationPipelineService.cs` for `Task.WhenAll()` and `ContinueWith().Unwrap()` patterns

---

## Future Enhancements

### Short-term (Month 4-5)
1. **Adaptive thresholds**: Context-specific thresholds (simple rules vs. complex scenarios)
2. **Caching optimization**: Increase cache hit rate to 70%+ via better cache keys
3. ~~**Performance profiling**: Identify bottlenecks, optimize hot paths~~ ✅ DONE (BGAI-037)

### Medium-term (Month 6-8)
1. **Sentence embeddings**: Upgrade cosine similarity to Sentence-BERT (better semantic understanding)
2. **Global IDF statistics**: Maintain corpus-wide IDF for improved TF-IDF similarity
3. **Hallucination model**: Train ML classifier for hallucination detection (beyond keywords)

### Long-term (Phase 2+)
1. **Fine-tuned models**: Domain-specific LLM training on board game rules corpus
2. **Cross-encoder re-ranking**: Deep transformer models for final validation
3. **Active learning**: Use user feedback to continuously improve validation thresholds

---

## Related Work

**ADRs**:
- ADR-001: Hybrid RAG Architecture (validation overview)
- ADR-002: Multilingual Embedding (language support)
- ADR-003b: Unstructured PDF (quality validation)
- ADR-004b: Hybrid LLM Approach (model selection)
- ADR-005: TF-IDF Cosine Similarity (consensus algorithm)

**Issues**:
- BGAI-028 (#970): Confidence validation layer
- BGAI-029 (#971): Citation validation
- BGAI-030 (#972): Hallucination detection
- BGAI-032 (#974): Multi-model validation
- BGAI-033 (#975): Cosine similarity consensus
- BGAI-012 (#951): PDF quality validation
- BGAI-040 (#982): **Document validation architecture (this ADR)**

**Documentation**:
- `docs/01-architecture/overview/system-architecture.md`: Overall system design
- `docs/03-api/board-game-ai-api-specification.md`: API contracts
- `CLAUDE.md`: Project overview (includes validation section)

---

## Conclusion

The Multi-Layer Validation Architecture provides a robust, defense-in-depth approach to ensuring AI response quality for board game rules Q&A. By combining confidence thresholds, multi-model consensus, citation verification, hallucination detection, and user feedback, MeepleAI achieves the >95% accuracy target required for competitive board game play.

The domain-driven design ensures testability and maintainability, while adaptive validation strategies balance quality with performance and cost. Comprehensive monitoring enables continuous quality improvement and rapid issue detection.

This architecture sets MeepleAI apart from competitors and establishes a foundation for achieving the "zero hallucination tolerance" requirement critical to user trust and product success.

---

**Status**: ✅ **Accepted and Fully Implemented**
**Last Updated**: 2025-12-13T10:59:23.970Z
**Implemented By**: Engineering Lead
**Reviewed By**: CTO, ML Engineer
**Next Review**: 2025-12-01 (after beta testing feedback)

