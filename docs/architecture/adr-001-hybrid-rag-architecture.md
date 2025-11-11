# ADR-001: Hybrid RAG Architecture with Multi-Model Validation

**Status**: Accepted
**Date**: 2025-01-15
**Deciders**: CTO, Engineering Lead, ML Engineer
**Context**: Phase 1 MVP Architecture

---

## Context

MeepleAI requires achieving >95% accuracy on board game rules Q&A, significantly higher than competitor systems (45-75% documented accuracy). The critical constraint is **"one mistake ruins game session"** - users have zero tolerance for fabricated or incorrect rules.

**Problem**: Traditional single-LLM RAG systems suffer from:
1. Hallucinations (inventing non-existent rules when uncertain)
2. Insufficient accuracy (ChatGPT tested at ~73% on War of the Ring)
3. No confidence calibration (cannot distinguish certain vs uncertain answers)

**Research Foundation**:
- Digital Trends testing: ChatGPT invented "multiple Gandalfs", "Will of the West dice" (non-existent)
- Ludomentor user report: 1 error in 20+ queries (95% accuracy insufficient for competitive play)
- Azul AI case study: Single neural network achieved only 45% accuracy

---

## Decision

Implement **Hybrid RAG Architecture with Triple Validation**:

### Architecture Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    QUESTION ANSWERING PIPELINE                  │
└─────────────────────────────────────────────────────────────────┘

1. RETRIEVAL LAYER (Hybrid Search)
   ┌─────────────────────┐     ┌──────────────────────┐
   │  Vector Search      │     │  Keyword Search      │
   │  (Weaviate)         │     │  (PostgreSQL FTS)    │
   │  Similarity ≥0.80   │     │  BM25 ranking        │
   └──────────┬──────────┘     └──────────┬───────────┘
              │                           │
              └───────────┬───────────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │  RRF Fusion           │
              │  (70% vector +        │
              │   30% keyword)        │
              └───────────┬───────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │  Top 10 Chunks        │
              │  (context for LLM)    │
              └───────────┬───────────┘

2. GENERATION LAYER (Primary LLM)
                          │
                          ▼
              ┌───────────────────────┐
              │  GPT-4 Turbo          │
              │  Temperature: 0.1     │
              │  Prompt: Context +    │
              │  Question + Italian   │
              └───────────┬───────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │  Primary Response     │
              │  (answer, confidence, │
              │   citations)          │
              └───────────┬───────────┘

3. VALIDATION LAYER (Triple Check)
                          │
              ┌───────────┴────────────┐
              │                        │
              ▼                        ▼
   ┌──────────────────┐    ┌──────────────────────┐
   │ Layer 1:         │    │ Layer 2:             │
   │ Confidence       │    │ Multi-Model          │
   │ Threshold        │    │ Consensus            │
   │ (≥0.70)          │    │ (GPT-4 + Claude      │
   │                  │    │  similarity ≥0.90)   │
   └────────┬─────────┘    └──────────┬───────────┘
            │                         │
            └───────────┬─────────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │ Layer 3:              │
            │ Citation Verification │
            │ (page + snippet)      │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │ Layer 4:              │
            │ Forbidden Keywords    │
            │ (500+ blocklist)      │
            └───────────┬───────────┘
                        │
                        ▼
            ┌───────────────────────┐
            │ Layer 5:              │
            │ User Feedback         │
            │ (post-response)       │
            └───────────┬───────────┘
                        │
                        ▼
              ┌─────────────────────┐
              │  VALIDATED RESPONSE │
              │  or EXPLICIT        │
              │  UNCERTAINTY        │
              └─────────────────────┘
```

---

## Consequences

### Positive

**✅ Hallucination Prevention**:
- Multi-model consensus detects when GPT-4 fabricates rules (Claude disagrees)
- Forbidden keywords catch known hallucination patterns ("multiple Gandalfs")
- Confidence threshold forces explicit uncertainty instead of guessing

**✅ Accuracy Improvement** (+20-30 points over single-LLM):
- GPT-4 alone: ~75% baseline (ChatGPT tested performance)
- + Citation verification: +5-10 points (validates page references)
- + Multi-model consensus: +10-15 points (catches errors)
- **Target**: >95% with full triple validation

**✅ User Trust**:
- Explicit uncertainty > confident wrong answer
- Citations enable independent verification (users can check rulebook)
- Transparent validation status ("Validato da 2 modelli AI")

**✅ Competitive Differentiation**:
- Only system with multi-model validation (competitors use single LLM)
- Quality-first positioning (95%+ vs 45-75% competitors)

---

### Negative (Trade-offs Accepted)

**⚠️ Increased Latency** (+500-800ms):
- Single LLM: ~1.5s P95
- Dual LLM validation: ~2.5s P95 (GPT-4 + Claude sequential)
- Triple validation (complex cases): ~4s P95

**Mitigation**:
- Adaptive validation (skip consensus if confidence ≥0.90, single model sufficient)
- Parallel execution where possible (citation verification || forbidden keywords check)
- Performance budget: 80% queries <2s, 15% <3s, 5% <5s (acceptable distribution)

**⚠️ Increased Cost** (~2x per query):
- Single LLM: $0.02 per query (GPT-4 Turbo input+output tokens)
- Dual LLM: $0.04 per query (GPT-4 + Claude validation call)

**Mitigation**:
- Semantic caching (40-60% cache hit rate → 40-60% cost reduction)
- Smart routing (GPT-3.5 for simple queries → 30% cost reduction on 50% of traffic)
- Validation skip for high-confidence queries (≥0.90)
- Publisher B2B revenue offsets consumer API costs

**⚠️ Complexity** (+30% codebase size):
- Single LLM: ~500 LOC (lines of code)
- Triple validation: ~650 LOC (validation layers, consensus logic, fallback handling)

**Mitigation**:
- Modular design (each validation layer = separate service)
- Comprehensive testing (unit tests for each layer, integration tests for pipeline)
- Clear documentation (ADRs, code comments, API docs)

---

## Implementation Details

### Confidence Threshold Implementation

```python
# services/validation.py
class ConfidenceValidator:
    THRESHOLD = 0.70

    def validate(self, response: GeneratedResponse) -> ValidationResult:
        if response.confidence >= self.THRESHOLD:
            return ValidationResult(
                passed=True,
                layer='confidence_threshold',
                message=f"Confidence {response.confidence:.2f} >= {self.THRESHOLD}"
            )
        else:
            return ValidationResult(
                passed=False,
                layer='confidence_threshold',
                message=f"Confidence {response.confidence:.2f} < {self.THRESHOLD}",
                fallback_action='explicit_uncertainty'
            )
```

### Multi-Model Consensus Implementation

```python
# services/validation.py
class MultiModelConsensusValidator:
    SIMILARITY_THRESHOLD = 0.90

    async def validate(self,
                       query: str,
                       primary_response: GeneratedResponse,
                       context: List[RetrievedChunk]) -> ValidationResult:

        # Build validation prompt
        validation_prompt = self._build_validation_prompt(
            query, primary_response.answer, context
        )

        # Call Claude for validation
        claude_response = await self.claude_client.generate(
            prompt=validation_prompt,
            model="claude-3-5-sonnet-20240620",
            temperature=0.1,
            max_tokens=1024
        )

        # Calculate semantic similarity
        primary_embedding = self.embedder.embed(primary_response.answer)
        validation_embedding = self.embedder.embed(claude_response)
        similarity = self.cosine_similarity(primary_embedding, validation_embedding)

        if similarity >= self.SIMILARITY_THRESHOLD:
            return ValidationResult(
                passed=True,
                layer='multi_model_consensus',
                message=f"Consensus similarity {similarity:.3f} >= {self.SIMILARITY_THRESHOLD}",
                metadata={'claude_response': claude_response, 'similarity': similarity}
            )
        else:
            return ValidationResult(
                passed=False,
                layer='multi_model_consensus',
                message=f"Models disagree (similarity {similarity:.3f})",
                fallback_action='explicit_uncertainty',
                metadata={'claude_response': claude_response, 'similarity': similarity}
            )
```

### Citation Verification Implementation

```python
# services/validation.py
class CitationValidator:
    def validate(self,
                 citations: List[Citation],
                 context_chunks: List[RetrievedChunk]) -> ValidationResult:

        verified_citations = []
        for citation in citations:
            # Check page number exists in context
            page_exists = any(chunk.page == citation.page for chunk in context_chunks)

            if not page_exists:
                return ValidationResult(
                    passed=False,
                    layer='citation_verification',
                    message=f"Citation page {citation.page} not found in retrieved context",
                    fallback_action='remove_invalid_citation'
                )

            # Check snippet matches retrieved text (fuzzy match)
            snippet_match = self._fuzzy_match(
                citation.snippet,
                [chunk.text for chunk in context_chunks if chunk.page == citation.page]
            )

            if not snippet_match:
                return ValidationResult(
                    passed=False,
                    layer='citation_verification',
                    message=f"Citation snippet not found in page {citation.page} context",
                    fallback_action='remove_invalid_citation'
                )

            verified_citations.append(citation)

        return ValidationResult(
            passed=True,
            layer='citation_verification',
            message=f"All {len(verified_citations)} citations verified",
            metadata={'verified_citations': verified_citations}
        )
```

---

## Alternatives Considered

### Alternative 1: Single LLM with High Temperature (Rejected)

**Approach**: Use GPT-4 with temperature 0.7-0.9 for creative, diverse answers

**Pros**:
- Simpler architecture
- Lower latency (~1.5s)
- Lower cost (single API call)

**Cons**:
- Higher hallucination risk (temperature increases randomness)
- No validation mechanism (cannot detect fabricated rules)
- Unreliable for competitive play

**Rejection Reason**: Conflicts with "one mistake ruins session" requirement

---

### Alternative 2: Human-in-the-Loop Validation (Rejected for MVP)

**Approach**: All AI responses reviewed by human expert before sending to user

**Pros**:
- 100% accuracy achievable (human catches all errors)
- No hallucination risk (human filters fabrications)

**Cons**:
- Latency: Hours to days (human review bottleneck)
- Cost: €10-20 per query (human expert hourly rate)
- Scalability: Cannot handle >10 queries/day per expert
- Not real-time (fails "during gameplay" use case)

**Rejection Reason**: Violates latency requirement (<3s P95), cost prohibitive, not scalable

**Future Consideration**: Phase 3+ for edge cases where AI confidence <0.50 (escalate to human)

---

### Alternative 3: Fine-Tuned Single Model (Deferred to Phase 3)

**Approach**: Fine-tune GPT-4 or open-source LLM on Italian board game corpus

**Pros**:
- Potentially higher accuracy (domain-specific training)
- Lower inference cost (smaller model possible)
- Better Italian language handling

**Cons**:
- Requires large training corpus (10K+ rulebooks, expensive to collect)
- Training cost (€5-10K for GPT-4 fine-tuning)
- Validation still needed (fine-tuned models still hallucinate)
- Time investment (3-6 months data collection + training)

**Decision**: Defer to Phase 3. MVP uses pre-trained models + multi-model validation. Fine-tuning additive (improves accuracy +5-10 points but doesn't replace validation).

---

## Metrics & Validation

### Success Criteria

**Phase 1 (MVP)**:
- [ ] Accuracy ≥80% on golden dataset (100 Q&A, 10 games)
- [ ] Hallucination rate ≤10% on adversarial queries (50 synthetic)
- [ ] P95 latency ≤5s (acceptable for MVP, optimize Phase 2)
- [ ] User satisfaction ≥4.0/5.0 (beta survey)

**Phase 2 (Production)**:
- [ ] Accuracy ≥90% on expanded dataset (500 Q&A, 20 games)
- [ ] Hallucination rate ≤5%
- [ ] P95 latency ≤3s (optimized via caching, async processing)

**Phase 3 (Gold Standard)**:
- [ ] Accuracy ≥95% on comprehensive dataset (1000 Q&A, 50+ games)
- [ ] Hallucination rate ≤3%
- [ ] P95 latency ≤3s maintained at scale

### Monitoring

**Prometheus Metrics**:
```python
# Validation layer pass/fail rates
qa_validation_layer_pass_total = Counter('qa_validation_layer_pass_total', ['layer'])
qa_validation_layer_fail_total = Counter('qa_validation_layer_fail_total', ['layer'])

# Multi-model consensus similarity distribution
qa_consensus_similarity = Histogram('qa_consensus_similarity', 'Cosine similarity between models')

# Validation latency breakdown
qa_validation_latency_ms = Histogram('qa_validation_latency_ms', 'Validation time', ['layer'])
```

**Grafana Dashboard**:
- Validation funnel (% passing each layer)
- Consensus similarity distribution (histogram)
- Latency breakdown (stacked bar: retrieval, generation, validation)

---

## Rollback Plan

If validation layers cause unacceptable latency (P95 >5s sustained):

**Option 1: Reduce Validation Scope**
- Skip multi-model consensus for confidence ≥0.90 (only validate marginal cases)
- Expected: 30% of queries skip consensus → latency reduction ~300ms average

**Option 2: Async Validation**
- Return primary response immediately
- Run validation asynchronously
- Update response if validation fails (WebSocket notification to client)
- Trade-off: User sees answer faster, but may need correction

**Option 3: Rollback to Single LLM**
- Disable multi-model consensus entirely
- Keep confidence threshold + citation verification + forbidden keywords
- Fallback to Phase 1 baseline architecture
- Document accuracy regression, plan remediation

---

## References

**Research Sources**:
- `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md` (lines 91-126: accuracy analysis)
- Digital Trends: "I tested ChatGPT on board game rules" (ChatGPT hallucination cases)
- Ludomentor user review: "One mistake negatively impacts session" (App Store review)

**Technical Inspiration**:
- Sam Miller (2024, Medium): "RAG Board Game Guru" (Google Cloud Vertex AI + Gemini)
- Andor FAQ LLM (MaxHalford GitHub): OpenAI + Streamlit + manual annotation

**Academic Foundation**:
- Mills 2013: "Learning Board Game Rules from an Instruction Manual" (F-measure 30%, shows difficulty)
- IEEE CoG 2024: "Grammar-based Game Description Generation" (arXiv:2407.17404)

---

**ADR Metadata**:
- **ID**: ADR-001
- **Status**: Accepted
- **Date**: 2025-01-15
- **Supersedes**: None
- **Superseded By**: None
- **Related**: ADR-002 (Embeddings), ADR-005 (LLM Strategy)
