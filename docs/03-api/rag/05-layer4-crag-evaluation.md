# Layer 4: CRAG Evaluation (Corrective RAG)

**Purpose**: Quality-gate retrieved documents, trigger web search if needed, filter irrelevant content

**Token Cost**: ~0 LLM tokens (T5 evaluator separate model)
**Context Reduction**: 40-70% through decompose-recompose
**Accuracy Gain**: +10-15% vs no evaluation

---

## CRAG Architecture

```
Retrieved Docs (10 chunks, 5,000 tokens)
    ↓
T5-Large Evaluator (Fine-tuned)
    ↓
├─ Correct (≥0.8 relevance) → 50% of docs
│   └─→ Use as-is: 2,500 tokens
│
├─ Ambiguous (0.5-0.8) → 30% of docs
│   └─→ Trigger Web Search + augment: 2,500 + 2,000 web = 4,500 tokens
│
└─ Incorrect (<0.5) → 20% of docs
    └─→ Discard internal, Web Search only: 2,500 tokens (web)
    ↓
Decompose-Then-Recompose
    ↓
Final Context: 1,800 tokens average (64% reduction!)
```

---

## T5-Large Evaluator

### Model Specification

**Base Model**: `google/t5-large` (770M parameters)
**Fine-tuning Task**: Board game rule relevance classification
**Output**: "correct" | "ambiguous" | "incorrect" + relevance score 0-1

### Training Dataset

**Structure**:
```json
{
  "query": "How many food tokens in Wingspan setup?",
  "document": "Each player receives 5 food tokens and 2 cards... (page 4)",
  "label": "correct",
  "relevance_score": 0.95
}
```

**Dataset Size**: 500-1,000 labeled examples
- 40% correct (highly relevant)
- 30% ambiguous (partially relevant)
- 30% incorrect (irrelevant)

**Sources**:
- Existing MeepleAI query logs (if available)
- Manual labeling by domain experts
- Synthetic generation (LLM-generated query-doc pairs)

### Fine-Tuning Process

```python
from transformers import T5ForConditionalGeneration, T5Tokenizer, Trainer

# Load base model
model = T5ForConditionalGeneration.from_pretrained("t5-large")
tokenizer = T5Tokenizer.from_pretrained("t5-large")

# Prepare dataset
def preprocess(example):
    input_text = f"Query: {example['query']}\nDocument: {example['document']}\nRelevance:"
    target_text = f"{example['label']}|{example['relevance_score']}"

    inputs = tokenizer(input_text, max_length=512, truncation=True, padding="max_length")
    targets = tokenizer(target_text, max_length=20, truncation=True, padding="max_length")

    return {"input_ids": inputs.input_ids, "labels": targets.input_ids}

train_dataset = dataset.map(preprocess)

# Training config
training_args = TrainingArguments(
    output_dir="./models/t5-large-crag-evaluator",
    num_train_epochs=3,
    per_device_train_batch_size=8,
    learning_rate=3e-5,
    warmup_steps=500,
    logging_steps=100,
    save_steps=500,
    evaluation_strategy="steps",
    eval_steps=500
)

trainer = Trainer(model=model, args=training_args, train_dataset=train_dataset)
trainer.train()
```

**Training Time**: 2-3 hours on single GPU (A100)
**Expected Accuracy**: >90% on validation set

**See**: [Appendix D: Fine-Tuning Guide](appendix/D-fine-tuning-crag-evaluator.md)

---

## Decompose-Then-Recompose Algorithm

**Purpose**: Extract only key sentences from correct documents, filter filler text

```python
async def decompose_recompose(documents: list[Document]) -> list[Document]:
    """
    Extract key information, filter irrelevant content.
    Reduces context by 60-70% while preserving accuracy.
    """
    recomposed = []

    for doc in documents:
        # Step 1: Split into sentences
        sentences = split_sentences(doc.content)  # ~20 sentences per doc

        # Step 2: Score each sentence for relevance
        sentence_scores = []
        for sent in sentences:
            score = score_sentence_relevance(sent, doc.metadata, doc.query)
            sentence_scores.append((sent, score))

        # Step 3: Keep top 60% of sentences
        sorted_sentences = sorted(sentence_scores, key=lambda x: x[1], reverse=True)
        threshold_index = int(len(sorted_sentences) * 0.6)
        key_sentences = [s for s, _ in sorted_sentences[:threshold_index]]

        # Step 4: Recompose in original order
        original_order_sentences = [s for s in sentences if s in key_sentences]
        recomposed_content = " ".join(original_order_sentences)

        recomposed.append(Document(
            content=recomposed_content,
            metadata=doc.metadata,
            token_count=len(recomposed_content.split()) * 1.3
        ))

    return recomposed

def score_sentence_relevance(sentence: str, metadata: dict, query: str) -> float:
    """
    Heuristic scoring (can be replaced with ML model).
    Returns 0-1 relevance score.
    """
    score = 0.5  # Baseline

    # Keyword matching (simple but effective)
    query_terms = set(query.lower().split())
    sentence_terms = set(sentence.lower().split())
    overlap = len(query_terms & sentence_terms)
    score += overlap * 0.1  # +0.1 per matching term

    # Metadata signals
    if metadata.get("is_title", False): score += 0.2  # Titles more important
    if metadata.get("contains_numbers", False): score += 0.15  # Numbers often key facts

    # Length penalty (very short/long sentences less useful)
    sent_len = len(sentence.split())
    if sent_len < 5 or sent_len > 50:
        score -= 0.1

    return min(max(score, 0), 1)  # Clamp 0-1
```

---

## Web Search Augmentation

**Triggered**: When CRAG evaluator returns "ambiguous" or "incorrect" for majority of docs

```python
async def web_search_augment(query: str, category: str) -> list[Document]:
    """
    Search external sources for additional context.
    Triggered in ~30% of BALANCED queries, ~40% of PRECISE.
    """

    # Construct search query
    if category == "ambiguous":
        search_query = f"{query} official rules FAQ errata"
    else:  # incorrect
        search_query = f"{query} board game rules"

    # Use Bing/Google API
    web_results = await bing_search(search_query, max_results=5)

    # Convert to Document format
    web_docs = []
    for result in web_results:
        web_docs.append(Document(
            content=result.snippet,
            metadata={
                "source": "web",
                "url": result.url,
                "title": result.title,
                "relevance_score": result.score
            },
            token_count=len(result.snippet.split()) * 1.3
        ))

    return web_docs
```

**Cost**: ~$0.01 per web search (Bing API)
**Token Impact**: +2,000-2,500 tokens (5 web results)

---

## CRAG Pipeline Integration

```python
async def crag_evaluate_and_filter(
    query: str,
    documents: list[Document],
    strategy: str
) -> CragResult:
    """Complete CRAG pipeline"""

    if strategy == "FAST":
        # Skip CRAG for FAST tier (speed prioritized)
        return CragResult(filtered_docs=documents, web_augmented=False)

    # Step 1: Evaluate each document
    evaluations = []
    for doc in documents:
        eval_result = await crag_evaluator.evaluate(query, doc.content)
        evaluations.append({
            "doc": doc,
            "category": eval_result.category,  # "correct" | "ambiguous" | "incorrect"
            "score": eval_result.score
        })

    # Step 2: Categorize
    correct = [e["doc"] for e in evaluations if e["category"] == "correct"]
    ambiguous = [e["doc"] for e in evaluations if e["category"] == "ambiguous"]
    incorrect = [e["doc"] for e in evaluations if e["category"] == "incorrect"]

    # Step 3: Decide action
    if len(correct) >= 3:
        # Sufficient correct docs, use internal KB only
        final_docs = correct[:5]
        web_augmented = False

    elif len(ambiguous) > 0 or len(correct) > 0:
        # Partial relevance, augment with web
        web_docs = await web_search_augment(query, "ambiguous")
        combined = correct + ambiguous + web_docs
        final_docs = await rerank(combined, query, top_k=5)
        web_augmented = True

    else:
        # All incorrect, use web only
        web_docs = await web_search_augment(query, "incorrect")
        final_docs = await rerank(web_docs, query, top_k=5)
        web_augmented = True

    # Step 4: Decompose-then-recompose
    recomposed_docs = await decompose_recompose(final_docs)

    return CragResult(
        filtered_docs=recomposed_docs,
        web_augmented=web_augmented,
        correct_count=len(correct),
        ambiguous_count=len(ambiguous),
        incorrect_count=len(incorrect),
        original_tokens=sum(d.token_count for d in documents),
        filtered_tokens=sum(d.token_count for d in recomposed_docs)
    )
```

---

## Performance Metrics

**Evaluator Accuracy** (validation set):
- Target: >90% classification accuracy
- Precision (correct): >92%
- Recall (correct): >88%

**Token Reduction**:
- Average: 5,000 → 1,800 tokens (64% reduction)
- Best case (all correct): 5,000 → 1,000 (80% reduction)
- Worst case (web augment): 5,000 → 4,500 (10% reduction)

**Web Search Frequency**:
- BALANCED: ~30% of queries
- PRECISE: ~40% of queries
- Cost impact: +$0.003-0.004 per query average

**Source**: CRAG research papers + implementation guides
