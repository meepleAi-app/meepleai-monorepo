# Graph RAG

## Quick Stats

| Metric | Value |
|--------|-------|
| **Tokens/Query** | 4,180 |
| **Cost/Query** | $0.016 |
| **Accuracy** | +10% above naive |
| **Latency** | 1–5s |
| **Priority** | **P3** - Specialized |

---

## Architecture Diagram

```
Knowledge Base Preparation (One-time)
    ↓
[Entity Extraction] From rulebook
    ├─ Entities: Game rules, mechanics, resources
    └─ Relations: "applies_to", "requires", "enables"
    ↓
[Graph Construction] Build knowledge graph
    ├─ Neo4j or similar
    └─ Cost: ~$1,000 one-time for 100K chunks
    ↓
[Query Time]
    ├─ Router generates Cypher/SPARQL
    ├─ Traversal returns subgraph
    └─ Generation uses graph context
    ↓
Answer
```

---

## How It Works

1. **One-Time Preparation** (amortized):
   - Extract entities from rulebook
   - Build relationships (rule → affects → resource)
   - Create knowledge graph

2. **Query Time**:
   - Router generates graph query (Cypher/SPARQL)
   - Traverse relationships
   - Retrieve subgraph (3-5 hops)
   - Generate answer from context

---

## Token Breakdown

**Graph Construction** (one-time):
- Entity extraction: 50,500 tokens
- Graph building: Internal to model, not counted
- Amortized: ~0.5 tokens per query (if 100K queries)

**Query-Time Routing**:
- Cypher generation: 350 input + 80 output = 430 tokens

**Subgraph Retrieval**: 3,000 tokens (nodes + relationships)

**Generation**:
- Input: 400 + 50 + 3,000 = 3,450 tokens
- Output: 300 tokens

**Total**: 4,180 tokens

**Cost**: $0.016 per query + one-time $1,000 setup

---

## When to Use

✅ **Best For**:
- Relationship-heavy queries ("how do X and Y interact?")
- Rules with cross-references
- Long-term cost (amortization)

❌ **Not For**:
- Simple fact lookups
- One-off queries
- No relationships to model

---

## Code Example

```python
class GraphRag:
    async def prepare_knowledge_graph(self, rulebook: str):
        """Build knowledge graph from rulebook"""

        # Extract entities
        entities_prompt = f"""
        Extract all entities from this rulebook:
        {rulebook}

        Return JSON: {{"entities": ["entity1", "entity2", ...]}}
        """
        entities_response = await self.llm.generate(entities_prompt)
        entities = json.loads(entities_response)["entities"]

        # Extract relationships
        relations_prompt = f"""
        For each entity, identify relationships:
        Format: {{"subject": "entity1", "relation": "affects", "object": "entity2"}}
        """
        relations_response = await self.llm.generate(relations_prompt)
        relations = json.loads(relations_response)

        # Store in Neo4j
        async with self.graph_db.session() as session:
            for entity in entities:
                await session.run(
                    "CREATE (n:Entity {name: $name})",
                    name=entity
                )

            for rel in relations:
                await session.run(
                    """
                    MATCH (a:Entity {name: $subject})
                    MATCH (b:Entity {name: $object})
                    CREATE (a)-[r:{relation}]->(b)
                    """,
                    subject=rel["subject"],
                    relation=rel["relation"],
                    object=rel["object"]
                )

    async def query_graph(self, user_query: str) -> str:
        """Query graph for answer"""

        # Generate Cypher query
        cypher_prompt = f"""
        Convert this question into a Neo4j Cypher query:
        Question: {user_query}

        Return raw Cypher query.
        """
        cypher = await self.llm.generate(cypher_prompt)

        # Execute Cypher
        async with self.graph_db.session() as session:
            results = await session.run(cypher)
            subgraph = results.data()

        # Generate answer
        answer_prompt = f"""
        Based on this graph:
        {subgraph}

        Answer: {user_query}
        """
        return await self.llm.generate(answer_prompt)
```

---

## Pros/Cons

| Aspect | Pros | Cons |
|--------|------|------|
| **Relationship** | Excellent for relationships | Complex setup |
| **Cost** | $0.016/query long-term | $1,000 one-time |
| **Latency** | 1-5s (variable) | Graph traversal overhead |
| **Implementation** | Powerful for structured data | Requires Cypher/SPARQL |

---

## Integration

**Tier Level**: PRECISE tier (specialized scenarios)

**Graph Database**: Neo4j (open-source available)

---

## Research Sources

- [The Rise and Evolution of RAG in 2024](https://ragflow.io/blog/the-rise-and-evolution-of-rag-in-2024-a-year-in-review)

---

**Status**: Partial Production-Ready | **MeepleAI Tier**: P3 Specialized
