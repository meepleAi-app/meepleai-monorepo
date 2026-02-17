# Implementation Guide

**Practical guide** for implementing TOMAC-RAG system

---

## Quick Start: Phase 0 (Weeks 1-2)

### Prerequisites

- Python 3.11+
- .NET 9 (for C# integration)
- Docker + Docker Compose
- Qdrant vector DB (existing)
- Redis (existing)
- PostgreSQL (existing)

---

### Step 1: Set Up rag-orchestrator Service

```bash
# Create service structure
mkdir -p services/rag-orchestrator
cd services/rag-orchestrator

# Create virtual environment
python -m venv venv
source venv/bin/activate  # or `venv\Scripts\activate` on Windows

# Install dependencies
pip install fastapi uvicorn anthropic openai redis qdrant-client sentence-transformers
```

**Directory Structure**:
```
services/rag-orchestrator/
├── app/
│   ├── routers/
│   │   ├── __init__.py
│   │   ├── routing.py           # Layer 1
│   │   └── models.py            # Dataclasses
│   ├── cache/
│   │   ├── __init__.py
│   │   └── semantic_cache.py    # Layer 2
│   ├── main.py
│   └── config.py
├── requirements.txt
├── Dockerfile
└── docker-compose.yml
```

---

### Step 2: Implement Semantic Cache (Layer 2)

**File**: `app/cache/semantic_cache.py`

```python
import redis.asyncio as redis
import anthropic
from dataclasses import dataclass
import hashlib
import json

@dataclass
class CacheResult:
    hit: bool
    answer: str | None = None
    tokens_used: int = 0

class SemanticCache:
    def __init__(self, redis_url: str, anthropic_api_key: str):
        self.redis = redis.from_url(redis_url)
        self.client = anthropic.AsyncAnthropic(api_key=anthropic_api_key)
        self.threshold = 0.85

    async def lookup(self, query: str) -> CacheResult:
        # Get recent queries
        recent = await self.redis.lrange("rag:recent", 0, 10)
        if not recent:
            return CacheResult(hit=False)

        # Check similarity with LLM
        prompt = f"New: {query}\nCached: {chr(10).join(r.decode() for r in recent)}\nMost similar index (0-{len(recent)-1}) or null:"

        response = await self.client.messages.create(
            model="claude-3-haiku-20240307",
            max_tokens=10,
            messages=[{"role": "user", "content": prompt}]
        )

        try:
            idx = int(response.content[0].text.strip())
            cached_query = recent[idx].decode()
            cache_key = f"rag:{hashlib.md5(cached_query.encode()).hexdigest()}"
            cached_data = await self.redis.get(cache_key)

            if cached_data:
                data = json.loads(cached_data)
                return CacheResult(hit=True, answer=data["answer"], tokens_used=310)
        except:
            pass

        return CacheResult(hit=False, tokens_used=310)

    async def store(self, query: str, answer: str, ttl_hours: int = 24):
        cache_key = f"rag:{hashlib.md5(query.encode()).hexdigest()}"
        data = json.dumps({"query": query, "answer": answer, "ts": time.time()})
        await self.redis.setex(cache_key, ttl_hours * 3600, data)
        await self.redis.lpush("rag:recent", query)
        await self.redis.ltrim("rag:recent", 0, 99)
```

---

### Step 3: API Endpoint

**File**: `app/main.py`

```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from app.cache.semantic_cache import SemanticCache
import os

app = FastAPI(title="TOMAC-RAG Orchestrator")

# Initialize services
cache = SemanticCache(
    redis_url=os.getenv("REDIS_URL", "redis://localhost:6379"),
    anthropic_api_key=os.getenv("ANTHROPIC_API_KEY")
)

class RagRequest(BaseModel):
    query: str
    user_id: str
    game_id: str | None = None

class RagResponse(BaseModel):
    answer: str
    cache_hit: bool
    tokens_consumed: int
    cost_usd: float
    latency_ms: int

@app.post("/api/v1/rag/ask", response_model=RagResponse)
async def ask(request: RagRequest):
    import time
    start = time.time()

    # Layer 2: Check cache
    cache_result = await cache.lookup(request.query)

    if cache_result.hit:
        return RagResponse(
            answer=cache_result.answer,
            cache_hit=True,
            tokens_consumed=cache_result.tokens_used,
            cost_usd=cache_result.tokens_used * 3 / 1_000_000,
            latency_ms=int((time.time() - start) * 1000)
        )

    # Layer 3: Full RAG (placeholder - implement in Phase 1)
    answer = "Placeholder answer from full RAG pipeline"
    tokens = 2000

    # Cache result
    await cache.store(request.query, answer, ttl_hours=24)

    return RagResponse(
        answer=answer,
        cache_hit=False,
        tokens_consumed=tokens,
        cost_usd=tokens * 3 / 1_000_000,
        latency_ms=int((time.time() - start) * 1000)
    )

@app.get("/health")
async def health():
    return {"status": "healthy"}
```

---

### Step 4: Docker Compose Integration

**File**: `docker-compose.yml` (add to existing infra)

```yaml
services:
  rag-orchestrator:
    build: ./services/rag-orchestrator
    ports:
      - "8081:8000"
    environment:
      - REDIS_URL=redis://redis:6379
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - QDRANT_URL=http://qdrant:6333
    env_file:
      - ./infra/secrets/openrouter.secret
    depends_on:
      - redis
      - qdrant
    networks:
      - meepleai-network
```

---

### Step 5: Testing

**Unit Tests**: `tests/test_semantic_cache.py`

```python
import pytest
from app.cache.semantic_cache import SemanticCache

@pytest.mark.asyncio
async def test_cache_hit_for_similar_query():
    cache = SemanticCache(redis_url="redis://localhost:6379", anthropic_api_key=API_KEY)

    # Store original
    await cache.store("How many food tokens?", "5 tokens", ttl_hours=1)

    # Lookup similar query
    result = await cache.lookup("What's the food token count?")

    assert result.hit == True
    assert "5 tokens" in result.answer

@pytest.mark.asyncio
async def test_cache_miss_for_dissimilar():
    cache = SemanticCache(...)

    await cache.store("Setup for Wingspan", "Answer A", ttl_hours=1)
    result = await cache.lookup("Trading in Catan")

    assert result.hit == False
```

**Run Tests**:
```bash
cd services/rag-orchestrator
pytest tests/ -v
```

---

## Phase 1-3 Implementation (Weeks 3-12)

**Week 3-4**: Layer 3 (Retrieval)
- Integrate with existing embedding-service
- Implement FAST retriever (vector-only)
- See [Layer 3 Documentation](04-layer3-retrieval.md)

**Week 5-6**: Layer 4 (CRAG)
- Fine-tune T5-large evaluator (create dataset first!)
- Implement decompose-recompose
- See [Layer 4 Documentation](05-layer4-crag-evaluation.md)

**Week 7-8**: Layer 5 (Generation)
- Template-specific prompts
- Integrate with HybridLlmService (ADR-007)
- See [Layer 5 Documentation](06-layer5-generation.md)

**Week 9-10**: Layer 6 (Validation) + Self-RAG
- Citation validator
- Self-reflection prompts
- See [Layer 6 Documentation](07-layer6-validation.md)

**Week 11-12**: Multi-Agent (PRECISE tier)
- LangGraph setup
- 3-agent orchestration
- See [Multi-Agent Documentation](09-multi-agent-orchestration.md)

---

## Integration with .NET API

**C# Service Wrapper** (`BoundedContexts/KnowledgeBase/Infrastructure/RagOrchestratorClient.cs`):

```csharp
public class RagOrchestratorClient : IRagService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RagOrchestratorClient> _logger;

    public RagOrchestratorClient(HttpClient httpClient, ILogger<RagOrchestratorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RagResponse> AskAsync(string query, Guid? gameId, Guid? userId)
    {
        var request = new {
            query = query,
            user_id = userId?.ToString(),
            game_id = gameId?.ToString()
        };

        var response = await _httpClient.PostAsJsonAsync(
            "http://rag-orchestrator:8000/api/v1/rag/ask",
            request
        );

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RagOrchestratorResponse>();

        return new RagResponse
        {
            Answer = result.Answer,
            Citations = result.Citations,
            Confidence = result.Confidence,
            TokensConsumed = result.TokensConsumed,
            CostUsd = result.CostUsd
        };
    }
}
```

**DI Registration** (`KnowledgeBaseServiceExtensions.cs`):

```csharp
services.AddHttpClient<IRagService, RagOrchestratorClient>(client =>
{
    client.BaseAddress = new Uri(configuration["RagOrchestrator:Url"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## Monitoring Setup

**Grafana Dashboard**:
```json
{
  "panels": [
    {
      "title": "Token Consumption by Strategy",
      "targets": [
        "sum(rag_tokens_total) by (strategy)"
      ]
    },
    {
      "title": "Cache Hit Rate",
      "targets": [
        "rag_cache_hit_rate"
      ]
    },
    {
      "title": "Cost per Query",
      "targets": [
        "rate(rag_cost_usd_total[5m])"
      ]
    }
  ]
}
```

**Prometheus Scrape Config**:
```yaml
scrape_configs:
  - job_name: 'rag-orchestrator'
    static_configs:
      - targets: ['rag-orchestrator:8000']
    metrics_path: '/metrics'
```

---

## Deployment Checklist

**Phase 0 (Week 1-2)**:
- [x] Create service structure
- [x] Implement semantic cache
- [x] Write unit tests (target: 20 tests)
- [x] Deploy to dev environment
- [x] A/B test with 10% Admin traffic

**Phase 1 (Week 3-4)**:
- [ ] Implement FAST retriever
- [ ] Integrate with existing embedding-service
- [ ] Add metadata filtering
- [ ] Integration tests (15 tests)
- [ ] Deploy to staging

**Phase 2 (Week 5-8)**:
- [ ] Fine-tune CRAG evaluator (create dataset!)
- [ ] Implement BALANCED retriever (hybrid search)
- [ ] CRAG pipeline integration
- [ ] Performance benchmarks
- [ ] Expand to 50% Admin traffic

**Phase 3 (Week 9-12)**:
- [ ] Self-RAG implementation
- [ ] Multi-agent orchestration
- [ ] Full integration testing
- [ ] Production deployment (gradual rollout)

---

**Next**: See [Deployment Rollout](13-deployment-rollout.md) for detailed timeline
