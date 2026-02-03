# Fusion-in-Decoder

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 3,750 |
| **Cost/Query** | $0.015 |
| **Accuracy** | +8% above naive |
| **Latency** | 1–2s |
| **Priority** | **P3** - Skip |

---

## Architecture Diagram

```
Query Input
    ↓
[Retrieval] Fetch 20 passages
    ↓
[Encoder-Decoder] FiD model (T5/BART)
    ├─ Each passage encoded separately
    ├─ Encoder outputs fused in decoder
    └─ Decoder generates answer
    ↓
Answer
```

---

## How It Works

1. **Multi-Passage Retrieval**: Fetch 20 passages
2. **Encoder Processing**:
   - Each passage encoded independently (no cross-passage interference)
   - Maintains passage-level understanding
3. **Fusion**: Decoder receives fused representations
4. **Generation**: Decoder synthesizes final answer

---

## Token Breakdown

**Retrieval**: 10,000 tokens (20 passages)

**Effective Input** (fused): 400 + 50 + 3,000 = 3,450 tokens

**Output**: 300 tokens

**Total**: 3,750 tokens

---

## When to Use

❌ **Not Recommended**:
- GPT/Claude are decoder-only (not encoder-decoder)
- Requires specific model architecture
- Limited production usage

---

## Code Example

```python
class FusionInDecoder:
    def __init__(self):
        # FiD requires encoder-decoder model
        self.model = T5ForConditionalGeneration.from_pretrained(
            "google/flan-t5-large"
        )

    async def generate_with_fid(
        self,
        query: str,
        passages: list[Document]
    ) -> str:
        """Generate using Fusion-in-Decoder"""

        # Format: "passage: [PASSAGE] question: [QUESTION]"
        inputs = []
        for passage in passages[:20]:
            inputs.append(
                f"passage: {passage.content} question: {query}"
            )

        # T5 handles fusion in decoder
        output = self.model.generate(
            inputs,
            max_length=100
        )

        return output.sequences[0]
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +8% improvement | Limited adoption |
| **Cost** | $0.015 (moderate) | Requires specific models |
| **Latency** | 1-2s acceptable | GPT/Claude incompatible |

---

## Integration

**Not Recommended for MeepleAI**:
- Requires T5/BART/BART-based models
- Claude/GPT-4 are decoder-only
- Use Advanced RAG instead

---

**Status**: Academic | **MeepleAI Tier**: P3 Skip
