# Layer 5: Adaptive Generation

**Purpose**: Template-specific answer generation

**Token Cost**: 1,900-8,500 tokens
**Models**: Haiku (FAST) → Sonnet (BALANCED) → Opus (PRECISE)

---

## Template Prompts

### Rule Lookup - FAST
```python
prompt = f"""Rules assistant. Extract exact text.
Retrieved: {docs}
Question: {query}
Answer + page citation:"""
# Input: ~1,950t, Output: ~200t
```

### Rule Lookup - BALANCED
```python
prompt = f"""Rules expert. Synthesize from multiple sections.
Retrieved: {docs}
Question: {query}
Provide: Answer with all citations (pages)"""
# Input: ~3,050t, Output: ~300t
```

### Resource Planning - BALANCED
```python
prompt = f"""Strategic advisor. Analyze trade-offs.
Rules: {docs}
Decision: {query}
Analysis: Option A vs B with pros/cons + recommendation"""
# Input: ~3,050t, Output: ~400t, Tools: calculator
```

### Resource Planning - PRECISE (Multi-Agent)
See [Multi-Agent Orchestration](09-multi-agent-orchestration.md)
# Input: ~8,500t, Output: ~1,200t

---

## Tool-Calling

```python
tools = [{
    "name": "calculator",
    "description": "Calculate resource costs",
    "parameters": {"a": "number", "b": "number", "op": "string"}
}]

# Safe calculator (no eval)
def safe_calc(a: float, b: float, op: str) -> float:
    ops = {"+": a + b, "-": a - b, "*": a * b, "/": a / b if b != 0 else 0}
    return ops.get(op, 0)
```

---

**Back**: [Architecture](01-tomac-rag-architecture.md)
