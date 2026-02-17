# Tutor Agent - Interactive Tutorial System

**Priority 1 (Proof-of-Concept) | Complexity: Medium | Timeline: 6 weeks**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md) | **Framework**: [Context Engineering](./01-context-engineering.md)

## Capabilities

- **Interactive Onboarding**: Step-by-step game setup with component validation
- **Rules QA**: Natural language questions about gameplay mechanics
- **Multi-Turn Dialogue**: Maintain context across conversation turns
- **Explanation Generation**: Context-aware rule explanations

## Architecture

### Tech Stack
```yaml
Embedding: sentence-transformers/all-MiniLM-L6-v2 (existing)
Vector Store: Qdrant (existing, enhanced with hybrid search)
Reranker: cross-encoder/ms-marco-MiniLM-L-6-v2
Orchestration: LangGraph (Python service)
LLM: GPT-4 / Claude (via OpenRouter)
Memory: PostgreSQL (conversation_memory table)
```

### Hybrid Search Pipeline

```
User Query: "Can I move my knight backwards?"
    ↓
Stage 1: Parallel Retrieval (20 results each)
├─ Keyword Search → Exact "knight" + "move" matches
└─ Semantic Search → Conceptually similar rules
    ↓
Stage 2: Reciprocal Rank Fusion (0.4 keyword, 0.6 semantic)
    ↓
Stage 3: Cross-Encoder Reranking (Top-10 → Top-3)
    ↓
Result: Most relevant knight movement rules
```

### LangGraph Dialogue Flow

```python
from langgraph.graph import StateGraph

class TutorState:
    conversation_history: List[Message]
    current_game: GameMetadata
    setup_progress: SetupStep
    retrieved_context: List[Document]

# Nodes
graph = StateGraph(TutorState)
graph.add_node("classify_intent", classify)  # setup | rules | general
graph.add_node("hybrid_search", search)
graph.add_node("generate_response", generate)
graph.add_node("update_memory", memory)

# Flow
graph.set_entry_point("classify_intent")
graph.add_conditional_edges("classify_intent", route_by_intent)
graph.add_edge("hybrid_search", "generate_response")
graph.add_edge("generate_response", "update_memory")

tutor_agent = graph.compile()
```

## Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
- [ ] Integrate cross-encoder reranker into Qdrant pipeline
- [ ] Implement hybrid search fusion (keyword + vector)
- [ ] Create conversation_memory PostgreSQL table
- [ ] Set up LangGraph development environment

### Phase 2: Core Logic (Week 3-4)
- [ ] Implement intent classification (setup/rules/general)
- [ ] Build LangGraph dialogue state machine
- [ ] Create multi-turn conversation handler
- [ ] Integrate hybrid search with context assembly

### Phase 3: Enhancement (Week 5-6)
- [ ] Add setup validation workflows
- [ ] Implement progressive disclosure (beginner/expert modes)
- [ ] Fine-tune reranking weights via A/B testing
- [ ] Deploy as REST API endpoint (`POST /api/v1/agents/tutor/query`)

## Database Schema

```sql
CREATE TABLE conversation_memory (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL,
    user_id UUID NOT NULL,
    game_id UUID REFERENCES games(id),
    message_type VARCHAR(50) NOT NULL,  -- 'user_query' | 'agent_response'
    content TEXT NOT NULL,
    embedding VECTOR(384),  -- For semantic search
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB DEFAULT '{}',

    INDEX idx_session_temporal (session_id, timestamp DESC),
    INDEX idx_embedding USING ivfflat (embedding vector_cosine_ops)
);
```

## API Endpoints

### POST /api/v1/agents/tutor/query
```typescript
Request:
{
  "sessionId": "uuid",
  "gameId": "uuid",
  "query": "Can I move my knight backwards in chess?",
  "context": {
    "setupStep": 3,  // Optional
    "playerLevel": "beginner"  // Optional
  }
}

Response:
{
  "response": "In chess, knights move in an L-shape...",
  "confidence": 0.95,
  "sources": [
    { "title": "Knight Movement Rules", "page": 12 },
    { "title": "Chess Basics FAQ", "section": "Movement" }
  ],
  "sessionId": "uuid"
}
```

## Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Response Time** | <2s (P95) | End-to-end API latency |
| **Context Retention** | 10+ turns | Multi-turn coherence |
| **Retrieval Accuracy** | >90% | Top-3 relevance @human-eval |
| **User Satisfaction** | >4.0/5.0 | Beta testing feedback |

## Testing Strategy

### Unit Tests
```python
@pytest.mark.asyncio
async def test_intent_classification():
    classifier = IntentClassifier()
    query = "How do I set up the game board?"

    intent = await classifier.classify(query)

    assert intent == IntentType.SETUP_QUESTION
    assert intent.confidence > 0.8
```

### Integration Tests
```python
@pytest.mark.integration
async def test_multi_turn_conversation():
    tutor = TutorAgent()
    session_id = uuid4()

    # Turn 1
    r1 = await tutor.handle_query(session_id, "How do I win in Catan?", "catan")
    assert "victory points" in r1.lower()

    # Turn 2 (context-dependent)
    r2 = await tutor.handle_query(session_id, "How many do I need?", "catan")
    assert "10" in r2  # Should resolve from context
```

### Human Evaluation
- Beta test with 50 users
- Metrics: Helpfulness (5-point scale), Response time, Context retention
- Target: >4.0 helpfulness, <2s response

## Cost Analysis

**Per Conversation** (5 turns avg):
```
Prompt cost: 5 × 3K tokens × $0.03 = $0.45
Completion: 5 × 500 tokens × $0.06 = $0.15
Total: $0.60 per conversation
```

**Monthly** (10K users, 3 conversations/month): ~$18,000

**Optimizations**:
- Caching frequent queries (-40%)
- Prompt compression (-30%)
- Fine-tuning (future, self-hosted)

## Security

### Prompt Injection Defense
```python
INJECTION_PATTERNS = [
    r"ignore previous instructions",
    r"disregard all",
    r"system prompt:",
]

def sanitize_input(user_query: str) -> str:
    for pattern in INJECTION_PATTERNS:
        user_query = re.sub(pattern, "", user_query, flags=re.IGNORECASE)
    return user_query
```

### Rate Limiting
- 30 requests/minute per IP
- 100 requests/hour per user

## Next Steps

1. **For Developers**: Start with Phase 1 foundation
2. **For Testing**: See [Testing Strategy](./07-testing.md)
3. **For Integration**: Review [Integration Guide](./06-integration.md)

---

**Related**:
- [Context Engineering](./01-context-engineering.md)
- [LangGraph Orchestration](./05-orchestration.md)
- [Integration Guide](./06-integration.md)
