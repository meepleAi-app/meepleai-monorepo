# Integration Guide

**Connecting Multi-Agent System with Existing MeepleAI Stack**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## Existing Stack Integration Points

### Qdrant Enhancement

**Current**: Vector search only
**Enhancement**: Hybrid search (keyword + vector)

```python
from qdrant_client import QdrantClient
from qdrant_client.models import Filter, FieldCondition

async def hybrid_search(query: str, query_embedding: List[float], game_id: str):
    # Parallel: Keyword + Vector
    keyword, vector = await asyncio.gather(
        client.scroll(
            collection_name="game_rules",
            scroll_filter=Filter(must=[
                FieldCondition(key="game_id", match=MatchValue(value=game_id))
            ]),
            limit=20
        ),
        client.search(
            collection_name="game_rules",
            query_vector=query_embedding,
            query_filter=Filter(must=[
                FieldCondition(key="game_id", match=MatchValue(value=game_id))
            ]),
            limit=20
        )
    )

    # Reciprocal Rank Fusion
    return reciprocal_rank_fusion([keyword[0], vector], weights=[0.4, 0.6])
```

### PostgreSQL Schema Extensions

```sql
-- Conversation Memory (new table)
CREATE TABLE conversation_memory (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL,
    user_id UUID NOT NULL,
    game_id UUID REFERENCES games(id),
    message_type VARCHAR(50) NOT NULL,
    content TEXT NOT NULL,
    embedding VECTOR(384),
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    INDEX idx_session_temporal (session_id, timestamp DESC),
    INDEX idx_embedding USING ivfflat (embedding vector_cosine_ops)
);

-- Game State Snapshots (new table)
CREATE TABLE game_state_snapshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID REFERENCES games(id),
    turn_number INT NOT NULL,
    board_state JSONB NOT NULL,
    active_player UUID,
    embedding VECTOR(384),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    INDEX idx_game_temporal (game_id, turn_number DESC)
);

-- Strategy Patterns (new table)
CREATE TABLE strategy_patterns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    game_id UUID REFERENCES games(id),
    pattern_name VARCHAR(255) NOT NULL,
    description TEXT,
    applicable_phase VARCHAR(50),
    board_conditions JSONB,
    move_sequence JSONB,
    evaluation_score FLOAT,
    embedding VECTOR(384),
    source VARCHAR(100),

    INDEX idx_game_phase (game_id, applicable_phase)
);
```

### Redis Cache Layer

```python
class MeepleAICache:
    """3-tier caching for agents"""

    def __init__(self):
        self.redis = Redis(url=os.getenv("REDIS_URL"))
        self.memory_cache = {}

    async def get_rule(self, rule_id: str) -> Optional[Rule]:
        # Tier 1: In-memory
        if rule_id in self.memory_cache:
            return self.memory_cache[rule_id]

        # Tier 2: Redis
        cached = await self.redis.get(f"rule:{rule_id}")
        if cached:
            rule = Rule.from_json(cached)
            self.memory_cache[rule_id] = rule
            return rule

        # Tier 3: Qdrant
        rule = await self.fetch_from_qdrant(rule_id)
        await self.redis.setex(f"rule:{rule_id}", ttl=3600, value=rule.to_json())
        self.memory_cache[rule_id] = rule

        return rule
```

## API Integration

### REST Endpoints

```csharp
// apps/api/src/Api/Routing/AgentRoutes.cs

public static class AgentRoutes
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var agents = app.MapGroup("/api/v1/agents")
            .WithTags("Agents")
            .RequireAuthorization();

        // Tutor Agent
        agents.MapPost("/tutor/query", async (
            TutorQueryCommand cmd,
            IMediator mediator
        ) => Results.Ok(await mediator.Send(cmd)));

        // Arbitro Agent
        agents.MapPost("/arbitro/validate", async (
            ValidateMoveCommand cmd,
            IMediator mediator
        ) => Results.Ok(await mediator.Send(cmd)));

        // Decisore Agent
        agents.MapPost("/decisore/suggest", async (
            SuggestMoveCommand cmd,
            IMediator mediator
        ) => Results.Ok(await mediator.Send(cmd)));
    }
}
```

### LangGraph Python Service

```yaml
# docker-compose.yml
services:
  orchestration-service:
    build: ./apps/orchestration-service
    environment:
      - POSTGRES_URL=${DATABASE_URL}
      - REDIS_URL=${REDIS_URL}
      - QDRANT_URL=http://qdrant:6333
      - OPENROUTER_API_KEY=${OPENROUTER_API_KEY}
    ports:
      - "8090:8090"
    depends_on:
      - postgres
      - redis
      - qdrant
```

## Event Integration

```csharp
// Message bus for agent coordination
public class AgentEventBus : IAgentEventBus
{
    private readonly IMediator _mediator;
    private readonly IHubContext<AgentHub> _hubContext;

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IAgentEvent
    {
        // MediatR for internal coordination
        await _mediator.Publish(@event);

        // SignalR for real-time UI updates
        await _hubContext.Clients.All.SendAsync("AgentEvent", @event);
    }
}
```

## Monitoring Integration

```csharp
// Prometheus metrics
public class AgentMetrics
{
    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "meepleai_agent_requests_total",
        "Total agent requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "agent_type", "status" }
        }
    );

    public static readonly Histogram RequestDuration = Metrics.CreateHistogram(
        "meepleai_agent_request_duration_seconds",
        "Agent request duration",
        new HistogramConfiguration
        {
            LabelNames = new[] { "agent_type" },
            Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 5.0, 10.0 }
        }
    );
}
```

## Next Steps

1. Extend Qdrant with hybrid search
2. Create PostgreSQL migration for new tables
3. Set up Redis cache layer
4. Deploy LangGraph Python service
5. Integrate with existing API routes

---

**Related**: [Context Engineering](./01-context-engineering.md) | [Testing](./07-testing.md)
