# Layer 2: Semantic Cache

**Purpose**: Return cached answers for semantically similar queries (handle variations)

**Token Cost**: ~310 tokens (if miss), ~50 tokens (if hit)
**Cache Hit Rate Target**: 80%
**Token Savings**: -65% average vs always running full RAG

---

## Architecture

```
Query → Semantic Similarity Check (LLM)
    ↓
If similarity ≥ 0.85 (60-80% of queries):
    └─→ Return cached answer (50 tokens total)

If similarity < 0.85 (20-40% of queries):
    └─→ Proceed to Layer 3 (full RAG)
```

---

## Implementation

### Redis Cache Structure

```python
# Cache key schema
cache_key = f"rag:semantic:{hash(normalized_query)}"

# Cache value
cache_value = {
    "query": str,  # Original query that was cached
    "query_embedding": list[float],  # For similarity comparison
    "answer": str,
    "citations": list[dict],
    "confidence": float,
    "strategy_used": str,
    "template": str,
    "timestamp": int,
    "access_count": int,  # Popularity tracking
    "ttl_hours": int
}
```

### Semantic Similarity Check

```python
class SemanticCache:
    def __init__(self, redis_client, llm_client, threshold=0.85):
        self.redis = redis_client
        self.llm = llm_client
        self.threshold = threshold

    async def lookup(self, query: str) -> CacheResult:
        # Normalize query
        normalized = self.normalize_query(query)

        # Get candidate cached queries (last 100 for this game)
        candidates = await self.redis.lrange("rag:recent_queries", 0, 100)

        if not candidates:
            return CacheResult(hit=False, tokens_used=0)

        # LLM similarity check (batch for efficiency)
        similarity_prompt = f"""
        Compare this new query to cached queries. Return similarity 0-1.

        New Query: {normalized}

        Cached Queries:
        {chr(10).join(f"{i}. {c}" for i, c in enumerate(candidates[:10]))}

        Output JSON: {{"most_similar_index": int or null, "similarity": float}}
        """

        response = await self.llm.generate(
            prompt=similarity_prompt,
            max_tokens=50,
            model="claude-3-haiku"  # Cheap for cache checks
        )

        result = json.loads(response.content)
        tokens_used = response.tokens_input + response.tokens_output  # ~310

        if result["similarity"] >= self.threshold and result["most_similar_index"] is not None:
            # Cache hit!
            cached_query = candidates[result["most_similar_index"]]
            cache_data = await self.redis.get(f"rag:semantic:{hash(cached_query)}")

            # Update access count
            cache_data["access_count"] += 1
            await self.redis.set(f"rag:semantic:{hash(cached_query)}", cache_data)

            return CacheResult(
                hit=True,
                answer=cache_data["answer"],
                citations=cache_data["citations"],
                confidence=cache_data["confidence"],
                tokens_used=tokens_used + 50,  # Lookup overhead
                cache_age_hours=(time.time() - cache_data["timestamp"]) / 3600
            )
        else:
            # Cache miss
            return CacheResult(hit=False, tokens_used=tokens_used)

    async def store(self, query: str, response: RagResponse, ttl_hours: int):
        normalized = self.normalize_query(query)
        cache_key = f"rag:semantic:{hash(normalized)}"

        cache_data = {
            "query": normalized,
            "answer": response.answer,
            "citations": response.citations,
            "confidence": response.confidence,
            "strategy_used": response.strategy,
            "template": response.template,
            "timestamp": time.time(),
            "access_count": 1,
            "ttl_hours": ttl_hours
        }

        await self.redis.setex(cache_key, ttl_hours * 3600, cache_data)
        await self.redis.lpush("rag:recent_queries", normalized)
        await self.redis.ltrim("rag:recent_queries", 0, 999)  # Keep last 1000

    def normalize_query(self, query: str) -> str:
        """Normalize query for better cache matching"""
        import re
        # Lowercase, remove extra whitespace, strip punctuation
        normalized = query.lower().strip()
        normalized = re.sub(r'\s+', ' ', normalized)
        normalized = re.sub(r'[?.!]+$', '', normalized)
        return normalized
```

---

## Cache Hit Rate Optimization

### TTL Strategy (Time-To-Live)

```python
def get_cache_ttl(user_role: str, query_frequency: str) -> int:
    """Return TTL in hours based on user tier and query popularity"""

    # Note: Anonymous users cannot access the system - authentication required
    base_ttl = {
        "User": 48,        # 2 days
        "Editor": 72,      # 3 days
        "Admin": 168,      # 1 week
        "Premium": 336     # 2 weeks
    }

    # Extend TTL for popular queries
    if query_frequency == "high":  # Accessed >10 times
        return base_ttl[user_role] * 2
    elif query_frequency == "medium":  # Accessed 3-10 times
        return base_ttl[user_role] * 1.5
    else:
        return base_ttl[user_role]
```

### Cache Warming

**Populate cache with common queries** at system startup:

```python
async def warm_cache():
    """Pre-populate cache with FAQ queries"""
    faq_queries = [
        "How many players in Wingspan?",
        "Setup procedure for Catan",
        "Food tokens in Wingspan setup",
        # ... 100+ common queries
    ]

    for query in faq_queries:
        if not await cache.lookup(query).hit:
            # Run full RAG and cache result
            response = await rag_orchestrator.process(query, admin_user)
            await cache.store(query, response, ttl_hours=168)
```

---

## Monitoring

**Prometheus Metrics**:
```python
cache_hit_rate = Gauge('rag_cache_hit_rate', 'Cache hit rate', ['user_tier', 'template'])
cache_tokens_saved = Counter('rag_cache_tokens_saved', 'Tokens saved by cache')
cache_latency = Histogram('rag_cache_lookup_latency_ms', 'Cache lookup time')
```

**Expected Results**:
- Hit rate: 80% (FAQ queries)
- Avg tokens saved: ~1,950 per cached query
- Monthly savings: ~$120 (for 100K queries)

**Source**: Research on semantic caching + LLM similarity
