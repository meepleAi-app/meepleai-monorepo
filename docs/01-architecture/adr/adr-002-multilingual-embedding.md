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

```python
# services/embeddings.py
from sentence_transformers import SentenceTransformer

class EmbeddingService:
    def __init__(self):
        self.model = SentenceTransformer('intfloat/multilingual-e5-large')
        self.model.to('cpu')  # CPU sufficient for MVP (<100 embeddings/sec)

    def embed_text(self, text: str) -> np.ndarray:
        # Add instruction prefix (e5 models expect this for retrieval tasks)
        prefixed = f"query: {text}"  # For queries
        # prefixed = f"passage: {text}"  # For documents

        embedding = self.model.encode(
            prefixed,
            normalize_embeddings=True  # L2 normalization for cosine similarity
        )
        return embedding  # Shape: (1024,)

    def embed_batch(self, texts: List[str], is_query: bool = False) -> np.ndarray:
        prefix = "query:" if is_query else "passage:"
        prefixed_texts = [f"{prefix} {text}" for text in texts]

        embeddings = self.model.encode(
            prefixed_texts,
            batch_size=32,
            show_progress_bar=True,
            normalize_embeddings=True
        )
        return embeddings  # Shape: (len(texts), 1024)
```

---

### Phase 3: Fine-Tuning Process

**1. Data Collection**:
```python
# scripts/collect_training_data.py
corpus = []

# Rulebook text (10,000 chunks from 100 games)
for game in games_catalog:
    rulebook_chunks = chunk_rulebook(game.rulebook_path)
    corpus.extend(rulebook_chunks)

# Community FAQ (1,000 Q&A pairs from La Tana dei Goblin)
community_qa = scrape_forum_qa("https://www.gdt.it/forum", min_upvotes=5)
corpus.extend(community_qa)

print(f"Collected {len(corpus)} training examples")
# Expected: ~11,000 examples
```

**2. Annotation** (manually annotate 1000 Q&A pairs):
```json
{
  "query": "Posso usare Standard Projects dopo aver passato?",
  "positive_passages": [
    "Durante il proprio turno, prima di effettuare 1 o 2 azioni, il giocatore può utilizzare qualsiasi numero di Progetti Standard."
  ],
  "negative_passages": [
    "Il giocatore può passare in qualsiasi momento del proprio turno.",
    "Le azioni possono essere effettuate in qualsiasi ordine."
  ],
  "game_id": "terraforming-mars",
  "difficulty": "medium"
}
```

**3. Fine-Tuning Script**:
```python
# scripts/finetune_embeddings.py
from sentence_transformers import SentenceTransformer, losses, InputExample
from torch.utils.data import DataLoader

# Load base model
model = SentenceTransformer('intfloat/multilingual-e5-large')

# Prepare training data (contrastive learning)
train_examples = []
for item in annotated_dataset:
    # Positive pair (query + relevant passage)
    train_examples.append(InputExample(
        texts=[item['query'], item['positive_passages'][0]],
        label=1.0  # Similar
    ))

    # Negative pairs (query + irrelevant passages)
    for neg_passage in item['negative_passages']:
        train_examples.append(InputExample(
            texts=[item['query'], neg_passage],
            label=0.0  # Dissimilar
        ))

train_dataloader = DataLoader(train_examples, batch_size=16, shuffle=True)

# Contrastive loss (pull similar together, push dissimilar apart)
train_loss = losses.CosineSimilarityLoss(model)

# Fine-tune (5 epochs, ~2-4 hours on GPU)
model.fit(
    train_objectives=[(train_dataloader, train_loss)],
    epochs=5,
    warmup_steps=100,
    output_path='./models/italian-boardgame-e5-large'
)

print("Fine-tuning complete. Model saved to ./models/italian-boardgame-e5-large")
```

**4. Validation** (compare base vs fine-tuned):
```python
# scripts/evaluate_embeddings.py
from sklearn.metrics.pairwise import cosine_similarity

base_model = SentenceTransformer('intfloat/multilingual-e5-large')
finetuned_model = SentenceTransformer('./models/italian-boardgame-e5-large')

test_queries = load_test_dataset()  # 100 held-out Q&A pairs

# Evaluate retrieval accuracy
base_accuracy = evaluate_retrieval(base_model, test_queries)
finetuned_accuracy = evaluate_retrieval(finetuned_model, test_queries)

print(f"Base model accuracy: {base_accuracy:.2%}")
print(f"Fine-tuned accuracy: {finetuned_accuracy:.2%}")
print(f"Improvement: +{finetuned_accuracy - base_accuracy:.2%}")

# Expected: Base 70-75%, Fine-tuned 80-85% (+5-10 points)
```

---

## Terminology Handling Strategy

### Game-Specific Keywords Preservation

**Challenge**: Italian embeddings may not recognize English game terms
- "Meeple" (originated from "my people", universally used)
- "Worker Placement" (mechanic name, not translated)
- "Deck Building" (mechanic, Italian = "costruzione mazzo" but English common)

**Solution**: Bilingual Glossary Injection

```python
# services/terminology.py
GAME_TERMINOLOGY = {
    # English term: Italian translation + preserve original
    'meeple': ['meeple', 'pedina', 'segnalino'],
    'worker placement': ['worker placement', 'piazzamento lavoratori'],
    'deck building': ['deck building', 'costruzione mazzo'],
    'engine building': ['engine building', 'costruzione motore'],
    'area control': ['area control', 'controllo area'],
    # ... 500+ terms
}

class TerminologyHandler:
    def expand_query(self, query: str, language: str) -> List[str]:
        expanded = [query]

        # Detect English game terms in Italian query
        for en_term, translations in GAME_TERMINOLOGY.items():
            if en_term in query.lower():
                # Add queries with Italian equivalents
                for it_term in translations:
                    if it_term != en_term:
                        expanded.append(query.replace(en_term, it_term))

        return expanded
```

**Usage**: Query expansion at retrieval time (search for "meeple" also searches "pedina", "segnalino")

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

```bash
# Install dependencies
pip install sentence-transformers

# Download model (one-time, ~1.2 GB)
python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('intfloat/multilingual-e5-large')"

# Model cached at: ~/.cache/torch/sentence_transformers/
```

**Embedding Generation** (during indexing):
```python
from services.embeddings import EmbeddingService

embedder = EmbeddingService()

# Single text
embedding = embedder.embed_text("Posso usare Standard Projects?")
print(embedding.shape)  # (1024,)

# Batch (efficient for rulebook chunking)
chunks = ["Chunk 1 text...", "Chunk 2 text...", ...]  # 89 chunks
embeddings = embedder.embed_batch(chunks, is_query=False)
print(embeddings.shape)  # (89, 1024)
```

---

### Phase 3: Fine-Tuning Workflow

**Step 1: Data Collection** (Months 13-14):
- Scrape 100 Italian rulebooks (public domain, publisher-licensed)
- Extract community Q&A from La Tana dei Goblin (via web scraping, API if available)
- Total corpus: ~10,000 text chunks

**Step 2: Annotation** (Months 14-15):
- Manually annotate 1,000 Q&A pairs:
  - Query: "Posso usare X?"
  - Positive passages: [relevant rulebook sections]
  - Negative passages: [irrelevant but similar sections]
- Tools: Prodigy (annotation tool), Label Studio (open-source alternative)
- Team: 2 board game experts, 25 hours each (50 hours total)

**Step 3: Training** (Month 15):
- GPU instance: AWS g5.xlarge (1x NVIDIA A10G, $1.01/hour)
- Training time: 20-40 hours (~$20-40 GPU cost)
- Framework: Sentence Transformers library, PyTorch backend

**Step 4: Validation** (Month 15):
- Held-out test set: 100 Q&A pairs (not used in training)
- Metrics: Retrieval accuracy (P@10, MRR), embedding similarity distribution
- A/B test: Base vs fine-tuned on production traffic (1 week, 10% fine-tuned)

**Step 5: Deployment** (Month 16):
- Model registry: Hugging Face Hub (public release, open-source contribution)
- Production deployment: Update EmbeddingService to load fine-tuned model
- Monitoring: Track retrieval accuracy improvement vs baseline

---

## Monitoring & Evaluation

### Retrieval Quality Metrics

```python
# Precision at K (P@K): % of top-K results that are relevant
def precision_at_k(retrieved_chunks: List[Chunk], relevant_pages: List[int], k: int) -> float:
    top_k = retrieved_chunks[:k]
    relevant_count = sum(1 for chunk in top_k if chunk.page in relevant_pages)
    return relevant_count / k

# Mean Reciprocal Rank (MRR): average rank of first relevant result
def mean_reciprocal_rank(retrieved_chunks: List[Chunk], relevant_pages: List[int]) -> float:
    for rank, chunk in enumerate(retrieved_chunks, start=1):
        if chunk.page in relevant_pages:
            return 1.0 / rank
    return 0.0  # No relevant result found
```

**Prometheus Metrics**:
```python
retrieval_precision_at_k = Histogram('retrieval_precision_at_k', 'P@K metric', ['k'])
retrieval_mrr = Histogram('retrieval_mrr', 'Mean reciprocal rank')
embedding_generation_time_ms = Histogram('embedding_generation_time_ms', 'Embedding latency')
```

**Weekly Evaluation** (automated script):
```bash
# Evaluate base model on golden dataset
python scripts/evaluate_retrieval.py --model base --dataset tests/data/golden_dataset.json

# Compare fine-tuned (Phase 3+)
python scripts/evaluate_retrieval.py --model finetuned --dataset tests/data/golden_dataset.json

# Output:
# Base model P@10: 0.78, MRR: 0.65
# Fine-tuned P@10: 0.85, MRR: 0.73 (+7 points, +8 points)
```

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
