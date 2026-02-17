# Multimodal RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 8,550 |
| **Cost/Query** | $0.034 |
| **Accuracy** | +8% above naive |
| **Latency** | 2–4s |
| **Priority** | **P2** - Visual |

---

## Architecture Diagram

```
Knowledge Base
    ├─ Text Chunks (rules)
    └─ Images (board layouts, cards)
    ↓
[Query Input]
    ↓
[Text + Image Retrieval]
    ├─ Vector search for text: 4 chunks
    ├─ Visual search for images: 3 images
    └─ Total: 2,000 + images
    ↓
[Vision Model Processing]
    ├─ Describe images (Claude 3.5 vision): 4,950 input + 400 output
    ↓
[Text LLM Synthesis]
    ├─ Combine text + image descriptions: 2,850 input + 350 output
    ↓
Answer (with visual context)
```

---

## How It Works

1. **Retrieval**:
   - Text chunks: Standard vector search
   - Images: Visual similarity search or metadata match

2. **Vision Processing**:
   - Claude 3.5 Vision describes images
   - Extract layout, board state, card details
   - Cost: ~1,500 tokens per image × 3 = 4,500 tokens

3. **Synthesis**:
   - Combine text rules + image descriptions
   - Generate answer incorporating visual info

---

## Token Breakdown

**Text Retrieval**: 2,000 tokens (4 chunks)

**Image Retrieval**: 3 images (metadata, not token-counted)

**Vision Processing** (Claude 3.5 Vision):
- Input: 400 (prompt) + 50 (query) + 3×1,500 (3 images) = 4,950 tokens
- Output: 400 tokens (descriptions)

**Text Synthesis**:
- Input: 400 (prompt) + 50 (query) + 2,000 (text) + 400 (descriptions) = 2,850 tokens
- Output: 350 tokens

**Total**: 4,950 + 400 + 2,850 + 350 = **8,550 tokens**

**Cost**: $0.034 per query (4-5x naive RAG)

---

## When to Use

✅ **Best For**:
- Rulebooks with setup diagrams
- Card-based games (visual reference)
- Board layouts (strategic context)
- When visual info essential

❌ **Not For**:
- Text-only rulebooks
- Budget-constrained
- Latency-critical

---

## Code Example

```python
class MultimodalRag:
    async def retrieve_text_and_images(
        self,
        query: str
    ) -> tuple[list[Document], list[Image]]:
        """Retrieve both text and images"""

        # Text retrieval
        text_docs = await self.retrieve_text(query, top_k=4)

        # Image retrieval (by metadata or visual similarity)
        image_query = await self.vision_model.generate_image_query(query)
        images = await self.retrieve_images(image_query, top_k=3)

        return text_docs, images

    async def describe_images(
        self,
        images: list[Image]
    ) -> list[str]:
        """Use vision model to describe images"""

        descriptions = []

        for image in images:
            prompt = f"""
            Describe this game component:
            - What is shown?
            - What layout/state is visible?
            - How does it relate to the rules?
            """

            description = await self.vision_model.generate(
                prompt,
                image=image.content
            )

            descriptions.append(description)

        return descriptions

    async def synthesize_multimodal(
        self,
        query: str,
        text_docs: list[Document],
        images: list[Image]
    ) -> str:
        """Synthesize answer combining text and images"""

        # Get image descriptions
        image_descriptions = await self.describe_images(images)

        # Synthesize
        prompt = f"""
        Answer this question using the text rules and visual context.

        Question: {query}

        Text Rules:
        {self.format_docs(text_docs)}

        Visual Context:
        {chr(10).join(image_descriptions)}

        Provide answer that references both text and visual elements.
        """

        return await self.llm.generate(prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Accuracy** | +8% (visual context) | 8,550 tokens (4-5x) |
| **Context** | Incorporates board/card visuals | Vision model cost |
| **Use Case** | Essential for visual games | Complex image handling |
| **Latency** | 2-4s (manageable) | Multiple model calls |

---

## Integration

**Tier Level**: BALANCED/PRECISE tier (visual games)

**Vision Model**: Claude 3.5 Vision (recommended)

**Image Storage**:
- Store high-quality board/card images
- Tag with relevant rules/sections
- Metadata: game, component_type, page

---

## Research Sources

- [Multimodal RAG Innovations](https://tao-hpu.medium.com/multimodal-rag-innovations-transforming-enterprise-data-intelligence-healthcare-and-legal-745d2e25728d)

---

**Status**: Production-Ready | **MeepleAI Tier**: P2 Visual
