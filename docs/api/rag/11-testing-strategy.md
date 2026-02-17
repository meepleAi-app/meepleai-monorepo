# Testing Strategy

---

## Test Pyramid

**Unit Tests** (70%): ~200 tests
- Router (20), Cache (15), Retriever (30), CRAG (30), Generator (40), Validator (25), Multi-Agent (20), Utils (20)

**Integration Tests** (25%): ~80 tests
- End-to-end flows (30), Strategy escalation (15), Cache integration (10), Model selection (10), Tool-calling (10), Error handling (5)

**Performance Tests** (5%): ~15 tests
- Token budget compliance (5), Latency benchmarks (5), Cost validation (5)

---

## Key Test Cases

### Token Budget Compliance
```python
@pytest.mark.parametrize("user_tier,max_tokens", [
    # Anonymous users cannot access the system - authentication required
    ("User", 3000),
    ("Editor", 5000),
    ("Admin", 15000),
    ("Premium", 20000)
])
async def test_token_budget(user_tier, max_tokens):
    response = await rag.ask(query="Complex strategic query", user_role=user_tier)
    assert response.tokens_consumed <= max_tokens
```

### Accuracy Validation (Labeled Dataset)
```python
async def test_accuracy_on_dataset():
    dataset = load_labeled_dataset("tests/data/rag_eval_100.json")

    results = {"fast": 0, "balanced": 0, "precise": 0}
    for item in dataset:
        for strategy in ["fast", "balanced", "precise"]:
            response = await rag.ask(item.query, strategy_override=strategy)
            if evaluate_answer(response.answer, item.ground_truth):
                results[strategy] += 1

    assert results["fast"] / 100 >= 0.78  # FAST accuracy ≥78%
    assert results["balanced"] / 100 >= 0.85  # BALANCED ≥85%
    assert results["precise"] / 100 >= 0.95  # PRECISE ≥95%
```

---

**Back**: [Implementation](10-implementation-guide.md)
