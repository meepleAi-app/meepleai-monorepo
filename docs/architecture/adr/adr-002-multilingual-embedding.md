# ADR-002: Multilingual Embedding Strategy (Italian-First)

**Status**: Accepted
**Date**: 2025-01-15
**Deciders**: CTO, ML Engineer
**Context**: Phase 1-4 Embedding Model Selection

---

## Context

MeepleAI targets the Italian market first (completely unserved), with future international expansion (French, German, Spanish). Embedding model choice impacts:
1. **Retrieval accuracy**: Better embeddings → better context retrieval → better answers
2. **Multilingual capability**: Italian quality now + expansion readiness later
3. **Cost**: Embedding generation (one-time per chunk) + storage (ongoing)

**Requirements**:
- Strong Italian language support (core market)
- Multilingual readiness (French, German, Spanish expansion Phase 4)
- Game-specific terminology preservation ("Meeple", "Worker Placement" should stay recognizable)
- Reasonable dimensionality (balance accuracy vs storage cost)

**Research Foundation**:
- Document source (line 105-107): "BoardGameAssistant.ai dichiara multilingual ma no evidence"
- Document source (line 107): "Translation complexity richiede domain expertise (keywords, mechanics, terminology)"
- Document source (line 133): Embedding model comparison table

---

## Decision

Use **multilingual-e5-large** base model with **Italian board game corpus fine-tuning** (Phase 3).

### Model Specifications

**Base Model** (Phase 1-2):
- **Name**: `intfloat/multilingual-e5-large`
- **Dimensions**: 1024 (balance accuracy vs storage)
- **Languages**: 100+ languages including Italian, French, German, Spanish
- **Training**: Multilingual corpus, contrastive learning
- **Performance**: MTEB benchmark (Italian): 0.65-0.70 (good baseline)

**Fine-Tuned Model** (Phase 3+):
- **Training Corpus**: 10,000 Italian board game rulebooks + community FAQ
- **Method**: Contrastive learning on annotated Q&A pairs (1000+ examples)
- **Target Performance**: +5-10 points over base model on Italian retrieval (MTEB: 0.75-0.80)
- **Training Cost**: ~€500-1,000 (GPU hours on AWS/GCP)
- **Training Time**: 2-4 weeks (data collection + annotation + training + validation)

---

## Rationale

### Why multilingual-e5-large?

**✅ Multilingual Coverage**:
- Italian: Well-supported (training corpus includes Italian Wikipedia, Common Crawl)
- Future languages: French, German, Spanish already in base model (no retraining needed)
- Zero-shot transfer: Italian fine-tuning improves Romance languages (French, Spanish) via shared linguistic features

**✅ Dimensionality Balance**:
- 1024 dimensions: Sweet spot (accuracy vs storage)
  - Lower (384): Faster but less accurate (all-MiniLM-L6-v2: -10 points on MTEB)
  - Higher (3072): More accurate but 3x storage cost (text-embedding-3-large)
- Storage impact: 1M chunks × 1024 dims × 4 bytes = 4 GB (acceptable)

**✅ Ecosystem Support**:
- Sentence Transformers: Mature Python library, well-documented
- Hugging Face: Easy deployment, community support, model hub
- Weaviate: Native integration (auto-vectorization support)

**✅ Cost-Effective**:
- Self-hosted: Zero API costs (run on CPU, ~100ms per embedding batch)
- One-time cost: Embedding computed during indexing, stored in Weaviate
- No ongoing API fees (vs OpenAI text-embedding-3-large: $0.13 per 1M tokens)

---

### Why Fine-Tune in Phase 3?

**Deferred Until Validated Need**:
- Phase 1-2: Validate product-market fit with base model
- Base model likely sufficient for 80-90% accuracy (pre-trained multilingual covers Italian)
- Fine-tuning cost justified only after user feedback shows Italian-specific retrieval gaps

**Expected Improvements from Fine-Tuning**:
- **Game Terminology**: "Meeple", "Worker Placement", "Deck Building" better understood
- **Italian Nuances**: "passa" (pass turn) vs "passa" (surpass) disambiguated via game context
- **Domain-Specific Synonyms**: "carta" (card) ≈ "tessera" (tile) in certain game contexts
- **Accuracy Gain**: +5-10 points on Italian retrieval (empirical from similar domain fine-tuning studies)

---

## Implementation Strategy

### Phase 1-2: Base Model

**Service**: `EmbeddingService` using `sentence-transformers` library
- Load model: `SentenceTransformer('intfloat/multilingual-e5-large')`
- Add instruction prefix: `"query: {text}"` for queries, `"passage: {text}"` for documents
- Normalize embeddings (L2 normalization for cosine similarity)
- Batch processing: 32 documents per batch for efficiency
- Output: 1024-dimensional vectors

### Phase 3: Fine-Tuning Process

**1. Data Collection** (~11K examples):
- 10,000 Italian rulebook chunks (100 games)
- 1,000 Q&A pairs from Italian board game communities
- Annotated with positive/negative passage pairs

**2. Training Method**:
- Contrastive learning: Pull similar embeddings together, push dissimilar apart
- CosineSimilarityLoss optimizer
- 5 epochs on GPU (~2-4 hours, €500-1K cost)
- Output: Domain-specific model with +5-10 point accuracy improvement

**3. Validation**:
- Held-out test set (100 Q&A pairs)
- Metrics: Retrieval accuracy (P@10, MRR)
- Expected: Base 70-75% → Fine-tuned 80-85%

**Implementation**: See `scripts/finetune_embeddings.py` and `scripts/evaluate_embeddings.py` for full code

---

## Terminology Handling Strategy

**Challenge**: Italian embeddings may not recognize English game terms (Meeple, Worker Placement, Deck Building)

**Solution**: Bilingual glossary with query expansion
- 500+ term dictionary: English ↔ Italian translations
- Query expansion: Search "meeple" also searches "pedina", "segnalino"
- Preserves recognizable English terms in Italian context

**Implementation**: See `services/terminology.py` for glossary handler

---

## Consequences

### Positive

**✅ Italian Quality from Day 1**:
- multilingual-e5-large trained on Italian corpus (Wikipedia, Common Crawl)
- No degradation vs English-only models
- Validated performance: MTEB Italian benchmark 0.65-0.70

**✅ Expansion Readiness**:
- French, German, Spanish already in base model (zero retraining)
- Cross-lingual transfer: Italian fine-tuning improves Romance languages
- Phase 4 expansion: +30% effort per language (vs 200% if English-only model)

**✅ Cost Control**:
- Self-hosted: €0 API costs (vs OpenAI text-embedding-3-large: €130 per 1M tokens)
- Storage: 1M chunks × 1024 dims × 4 bytes = 4 GB (Weaviate handles efficiently)

**✅ Terminology Handling**:
- Bilingual glossary enables English game terms in Italian context
- Query expansion increases recall (find relevant chunks even with term variations)

---

### Negative (Trade-offs)

**⚠️ Lower Performance Than Specialized Models** (initially):
- OpenAI text-embedding-3-large: 3072 dims, potentially +5 points accuracy
- Cohere embed-multilingual-v3: Optimized for multilingual, but proprietary (API costs)

**Mitigation**: Fine-tuning in Phase 3 closes accuracy gap (+5-10 points)

**⚠️ Fine-Tuning Effort** (Phase 3):
- Data collection: 10,000 rulebooks (100 hours scraping, licensing)
- Annotation: 1,000 Q&A pairs (50 hours manual work by board game experts)
- Training: 2-4 weeks (GPU costs €500-1,000)

**Mitigation**: Defer until validated need (Phase 1-2 base model may suffice)

**⚠️ Storage Scaling** (Phase 4, 1M+ chunks):
- 1M chunks × 1024 dims × 4 bytes = 4 GB (base model)
- Quantization possible: 8-bit (50% reduction) or 4-bit (75% reduction) with <5% accuracy loss

---

## Alternatives Considered

### Alternative 1: OpenAI text-embedding-3-large (Rejected)

**Specs**:
- Dimensions: 3072 (3x multilingual-e5-large)
- Languages: 100+ (excellent multilingual)
- Performance: MTEB benchmark leader

**Pros**:
- Highest accuracy (MTEB Italian: 0.75-0.80, +10 points vs e5-large)
- No self-hosting needed (API simplicity)

**Cons**:
- Cost: $0.13 per 1M tokens (€130 per 1M chunks vs €0 self-hosted)
- At scale (1M chunks): €130 one-time + ongoing storage
- API dependency (vendor lock-in, rate limits, pricing changes)
- Storage: 3x more (12 GB vs 4 GB for 1M chunks)

**Rejection Reason**: Cost prohibitive at scale, vendor lock-in risk, storage inefficiency

---

### Alternative 2: Cohere embed-multilingual-v3 (Rejected)

**Specs**:
- Dimensions: 1024
- Languages: 100+ with excellent multilingual performance
- Performance: Competitive with OpenAI

**Pros**:
- Strong multilingual quality
- Compressed embeddings option (reduce storage 4x)

**Cons**:
- Cost: $0.10 per 1M tokens (vs €0 self-hosted)
- Proprietary (cannot fine-tune without Cohere partnership)
- API dependency

**Rejection Reason**: Cost + lack of fine-tuning control

---

### Alternative 3: all-MiniLM-L6-v2 (Rejected)

**Specs**:
- Dimensions: 384 (lightweight)
- Languages: English-focused, weak multilingual
- Performance: MTEB English: 0.60-0.65

**Pros**:
- Very fast (3x faster than e5-large)
- Small storage footprint (384 dims vs 1024)

**Cons**:
- Poor Italian support (trained primarily on English)
- Lower accuracy (-10-15 points vs e5-large on Italian)

**Rejection Reason**: Insufficient Italian quality (core market requirement)

---

## Implementation Plan

### Phase 1-2: Base Model Deployment
- Install `sentence-transformers` library
- Download model (~1.2 GB, cached locally)
- Embedding generation: 1024-dim vectors with L2 normalization
- Batch processing for efficiency (32 documents per batch)

### Phase 3: Fine-Tuning Workflow

| Step | Timeline | Effort | Deliverable |
|------|----------|--------|-------------|
| Data Collection | Months 13-14 | 100 hours | 10K rulebook chunks + 1K Q&A pairs |
| Annotation | Months 14-15 | 50 hours | 1K annotated Q&A pairs (positive/negative passages) |
| Training | Month 15 | 20-40 GPU hours | Fine-tuned model (~€20-40 cost) |
| Validation | Month 15 | 40 hours | P@10, MRR metrics + A/B test (1 week) |
| Deployment | Month 16 | 20 hours | Production deployment + Hugging Face release |

**Tools**: Prodigy/Label Studio (annotation), AWS g5.xlarge GPU, PyTorch

---

## Monitoring & Evaluation

### Retrieval Quality Metrics
- **P@K**: Precision at K (% of top-K results that are relevant)
- **MRR**: Mean Reciprocal Rank (average rank of first relevant result)
- **Latency**: Embedding generation time

**Prometheus Metrics**: `retrieval_precision_at_k`, `retrieval_mrr`, `embedding_generation_time_ms`

**Weekly Evaluation**: Automated script compares base vs fine-tuned on golden dataset
- Target: Base 70-75% → Fine-tuned 80-85% (+7-8 points)

**Implementation**: See `scripts/evaluate_retrieval.py` for full metrics code

---

## Migration Path (Base → Fine-Tuned)

**Zero-Downtime Migration** (Phase 3):

1. **Train fine-tuned model** (offline, no impact on production)
2. **Deploy side-by-side**:
   - Keep base model running (active traffic)
   - Deploy fine-tuned model (canary, 10% traffic)
3. **A/B Test** (1-2 weeks):
   - Measure retrieval accuracy improvement
   - Monitor latency (fine-tuned may be slightly slower)
4. **Gradual Rollout**:
   - If P@10 improvement ≥+5 points: increase to 50% traffic
   - If no regressions: 100% traffic
5. **Re-Index** (background job, ~24 hours for 100 games):
   - Regenerate all embeddings with fine-tuned model
   - Update Weaviate vectors (batch update API)
6. **Decommission Base Model** (after validation):
   - Monitor for 1 week (no accuracy regressions)
   - Remove base model from codebase

---

## Future Enhancements

### Cross-Lingual Transfer Learning (Phase 4)

**Hypothesis**: Italian fine-tuning improves French/Spanish (Romance language family)

**Experiment**:
- Fine-tune on Italian corpus (10K chunks)
- Evaluate on French test set (100 Q&A pairs, held-out)
- Measure zero-shot transfer accuracy

**Expected**: +3-5 points on French (shared linguistic features)

**Implementation** (if validated):
- French fine-tuning: Add 2K French-specific Q&A pairs to corpus
- Expected improvement: +2-3 additional points (total +5-8 vs base French)

---

## References

**Model Documentation**:
- multilingual-e5-large: https://huggingface.co/intfloat/multilingual-e5-large
- Sentence Transformers: https://www.sbert.net/docs/training/overview.html
- MTEB Benchmark: https://huggingface.co/spaces/mteb/leaderboard

**Research Papers**:
- Wang et al. (2022): "Text Embeddings by Weakly-Supervised Contrastive Pre-training" (e5 model paper)
- Reimers & Gurevych (2019): "Sentence-BERT: Sentence Embeddings using Siamese BERT-Networks"

**Domain Examples**:
- Legal domain fine-tuning: +8-12 points on specialized legal retrieval
- Medical domain fine-tuning: +10-15 points on clinical QA tasks
- Board games (estimated): +5-10 points (moderate domain specificity)

---

**ADR Metadata**:
- **ID**: ADR-002
- **Status**: Accepted
- **Date**: 2025-01-15
- **Supersedes**: None
- **Related**: ADR-001 (RAG Architecture), ADR-004 (Vector DB)
