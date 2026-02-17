# Query Expansion RAG

**Stats**: 4,110 tokens/query | $0.016/query | +7% accuracy | 500ms-1s latency | **Priority: P2**

## Architecture

```
User Query: "How do I get meeples?"
    ↓
┌─────────────────────────────────────────┐
│ Query Expansion LLM Call                │
│ Add synonyms + related terms:           │
│ "meeples OR workers OR pieces OR        │
│  game tokens - acquisition rules"       │
└─────────────────────────────────────────┘
    ↓
Expanded Retrieval (better recall: 6 chunks vs 5)
    ↓
Generate (3,480 tokens)
```

## How It Works

Query Expansion adds synonyms, related terms, and conceptual variations to the original query, improving retrieval recall by matching terminology mismatches between user language and rulebook text. When a user says "meeples" but the rulebook says "worker tokens," standard search fails; expanded search finds both.

The workflow: (1) LLM analyzes the query and generates synonyms, related terms, and alternative phrasings, (2) expanded query is embedded as a single combined vector (not multiple queries like RAG-Fusion), (3) retrieval finds chunks matching any of the expanded terms, increasing recall, (4) generate answer from broader result set.

This is particularly valuable for board games with inconsistent terminology: "resources" vs "commodities," "VP" vs "victory points" vs "points," "cards" vs "tiles," etc. Expansion ensures retrieval captures all relevant chunks regardless of terminology variance.

The token cost is moderate (2x naive RAG) because expansion adds 30-50% more terms to the query, increasing retrieved chunk count slightly (6 instead of 5) without the overhead of multiple parallel retrievals like RAG-Fusion.

## Token Breakdown

**Expansion Phase**:
- Input: 200 (prompt: "Add synonyms and related terms") + 50 (query) = 250 tokens
- Output: 80 tokens (expanded query with synonyms)

**Expanded Retrieval**:
- Retrieve with expanded query: 6 chunks × 500 = 3,000 tokens (better recall)

**Generation**:
- Input: 400 + 80 (expanded query) + 3,000 = 3,480 tokens
- Output: 300 tokens

**Total Input**: 250 + 3,480 = 3,730 tokens
**Total Output**: 80 + 300 = 380 tokens
**Grand Total**: **4,110 tokens**

## When to Use

- **Terminology mismatches** between user language and documentation (synonyms)
- **Multi-game knowledge bases** with inconsistent terminology across games
- **Recall-critical queries** where missing relevant chunks is worse than extra irrelevant ones

## Code Example

```python
from anthropic import Anthropic

client = Anthropic(api_key="...")

def query_expansion_rag(query: str) -> str:
    """RAG with query expansion for improved recall."""

    # Step 1: Expand query with synonyms and related terms
    expansion = client.messages.create(
        model="claude-3-5-haiku-20241022",
        max_tokens=100,
        messages=[{
            "role": "user",
            "content": f"""Expand this query with synonyms and related board game terms.

Query: {query}

Add:
1. Common synonyms
2. Related game terminology
3. Alternative phrasings

Output as expanded search query (combine with OR):
"""
        }]
    )
    expanded_query = expansion.content[0].text.strip()

    # Step 2: Retrieve with expanded query
    # Embed the expanded query (single vector, not multiple like RAG-Fusion)
    expanded_embedding = get_embedding(expanded_query)
    chunks = vector_search(expanded_embedding, top_k=6)  # Higher top_k for better recall

    # Step 3: Generate with expanded retrieval results
    context = "\n\n".join([c['text'] for c in chunks])

    response = client.messages.create(
        model="claude-3-5-sonnet-20241022",
        max_tokens=300,
        messages=[{
            "role": "user",
            "content": f"""Original question: {query}
Expanded search: {expanded_query}

Context from rulebook:
{context}

Answer the original question based on the context.
"""
        }]
    )

    return response.content[0].text

# Example expansions
"""
Input: "How do I get meeples?"
Expanded: "meeples OR workers OR pieces OR tokens OR miniatures - acquisition rules placement receive gain"

Input: "How do I earn VP?"
Expanded: "VP OR victory points OR points OR scoring - earn gain acquire accumulate"

Input: "Can I discard cards?"
Expanded: "discard OR remove OR trash OR destroy cards tiles tokens - rules conditions"
"""
```

## Integration

Query Expansion integrates as a **pre-retrieval enhancement** in TOMAC-RAG, expanding the query before embedding and retrieval.

**Enhanced Flow**:
1. **Layer 1 (Routing)**: Detect terminology-sensitive query
2. **Layer 2.5** (NEW): Query expansion (THIS LAYER)
3. **Layer 3**: Retrieve with expanded query (higher top_k for recall)
4. **Layer 4**: CRAG evaluation on expanded results
5. **Layer 5**: Generate from broader context
6. **Layer 6**: Validate answer

**Activation Heuristics**:
- Terminology indicators: game-specific jargon, abbreviations (VP, AP, etc.)
- Multi-game context: search across games with different terminology
- Recall priority: queries where missing information is critical

**Optimization Strategies**:
- **Offline Synonym Dictionary**: Pre-build game-specific synonym maps to skip LLM expansion
  - "VP" → ["victory points", "points", "scoring", "VPs"]
  - Saves 250 input + 80 output = 330 tokens
  - **Optimized**: ~3,780 tokens (8% reduction)

**Alternative: Rule-Based Expansion** (0 tokens):
```python
SYNONYM_MAP = {
    "meeples": ["workers", "pieces", "tokens", "miniatures"],
    "VP": ["victory points", "points", "VPs", "scoring"],
    "cards": ["tiles", "tokens", "pieces"],
    # ... game-specific mappings
}

def expand_query_rules(query: str) -> str:
    """Rule-based expansion (0 LLM tokens)."""
    expanded = query
    for term, synonyms in SYNONYM_MAP.items():
        if term.lower() in query.lower():
            expanded += " " + " ".join(synonyms)
    return expanded
```

## Sources

- [Advanced RAG: Query Expansion](https://haystack.deepset.ai/blog/query-expansion)
- [Query Expansion Techniques](https://www.elastic.co/guide/en/elasticsearch/reference/current/query-dsl-multi-match-query.html)
