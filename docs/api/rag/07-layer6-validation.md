# Layer 6: Validation & Auto-Escalation

**Purpose**: Quality assurance, detect hallucinations, auto-escalate

**Token Cost**: 0-4,400 tokens

---

## Validation by Strategy

### FAST: Rule-Based (0 tokens)
```python
def validate_fast(answer: str, template: str):
    if template == "rule_lookup":
        has_citation = bool(re.search(r'\(p(?:age)?\.?\s*\d+\)', answer))
        if not has_citation:
            return ValidationResult(passed=False, action="ESCALATE_TO_BALANCED")

    if len(answer.split()) < 10:
        return ValidationResult(passed=False, action="ESCALATE_TO_BALANCED")

    return ValidationResult(passed=True, confidence=0.75)
```

**Escalation**: ~10-15% queries

---

### BALANCED: Cross-Encoder Alignment (0 LLM tokens)
```python
async def validate_balanced(answer: str, docs: list):
    pairs = [(answer, doc.content) for doc in docs]
    scores = await cross_encoder.score_pairs(pairs)
    avg = sum(scores) / len(scores)

    if avg < 0.6:
        return ValidationResult(
            passed=True,
            confidence=avg,
            action="OFFER_ESCALATE_TO_PRECISE"
        )

    return ValidationResult(passed=True, confidence=avg)
```

**Escalation**: ~5% queries

---

### PRECISE: Self-RAG Reflection (~4,400 tokens)
```python
async def validate_precise(answer: str, docs: list, query: str):
    reflection_prompt = f"""
    Self-critique your answer:
    Answer: {answer}
    Context: {docs}

    Evaluate:
    1. Relevance: Yes/Partial/No
    2. Support: Yes/Partial/No
    3. Usefulness: Yes/Maybe/No
    4. Confidence: 0-1

    JSON: {{"relevance": str, "support": str, "usefulness": str, "confidence": float}}
    """

    reflection = await llm_opus.generate(reflection_prompt, max_tokens=300)
    data = json.loads(reflection.content)

    confidence = calculate_confidence(data)

    if confidence < 0.9 and data.get("re_retrieve"):
        # Re-retrieve with refined query (max 2 loops)
        pass

    return ValidationResult(confidence=confidence, reflection=data)
```

---

## Citation Verification

```python
def verify_citations(answer: str, docs: list) -> bool:
    cited_pages = re.findall(r'page\s+(\d+)', answer)
    for page in cited_pages:
        if not any(int(page) == d.metadata.get("page") for d in docs):
            return False
    return True
```

---

**Back**: [Architecture](01-tomac-rag-architecture.md) | [Self-RAG](variants/self-rag.md)
