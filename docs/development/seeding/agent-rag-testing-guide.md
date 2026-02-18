# Agent RAG Testing Guide - MeepleAssistant POC

**Status**: ✅ Setup Complete
**Agent ID**: `49365068-d1db-4a66-aff5-f9fadca2763b`
**VectorDoc**: Azul (45 chunks, completed)
**Model**: Claude 3 Haiku (~$0.00025/1K tokens)

---

## Setup Verification

Run verification script to confirm RAG readiness:

```bash
docker exec -i meepleai-postgres psql -U postgres -d meepleai < scripts/verify-agent-setup.sql
```

**Expected Output**: All checks ✓ (6/6 passed)

---

## Manual Testing via Web UI

### Method 1: Web Interface (Easiest)

1. **Navigate to**: http://localhost:3000
2. **Login**: admin@meepleai.dev / Admin123!ChangeMe
3. **Go to AI Lab** or Agents page
4. **Select**: MeepleAssistant POC agent
5. **Test Query**: "How do you score points in Azul?"

**Expected Behavior**:
- ✅ Response uses Azul rulebook context (mentions tiles, patterns, wall, floor)
- ✅ Professional tone maintained
- ✅ Structured answer (Direct → Explanation → Sources)
- ✅ Citation format: [Source: azul_rulebook.pdf] or similar

---

## Manual Testing via Scalar API Docs

### Method 2: API Documentation Interface

1. **Navigate to**: http://localhost:8080/scalar/v1
2. **Find**: `POST /api/v1/agents/{id}/chat`
3. **Authenticate**: Use "Try it" → Login → Get session token
4. **Execute Request**:
   - Agent ID: `49365068-d1db-4a66-aff5-f9fadca2763b`
   - Message: "How do you score points in Azul?"
   - ChatThreadId: null (optional)

**Expected Response** (SSE stream):
```json
{
  "type": "chunk",
  "content": "To score points in Azul, you complete horizontal rows on your wall..."
}
...
{
  "type": "final",
  "answer": "...",
  "retrievedChunks": 5,
  "confidence": 0.85,
  "tokenUsage": {...}
}
```

---

## Manual Testing via cURL

### Method 3: Command Line (Advanced)

#### Step 1: Login and Get Token

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@meepleai.dev",
    "password": "Admin123!ChangeMe"
  }' | jq -r '.sessionToken'
```

Copy the session token from output.

#### Step 2: Query Agent

```bash
# Replace {TOKEN} with your session token
curl -X POST http://localhost:8080/api/v1/agents/49365068-d1db-4a66-aff5-f9fadca2763b/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {TOKEN}" \
  -d '{
    "message": "How do you score points in Azul?",
    "chatThreadId": null
  }'
```

**Expected**: SSE stream with RAG-enhanced responses

---

## Test Scenarios

### Scenario 1: RAG Context Usage ✅

**Query**: "How do you score points in Azul?"

**Verify**:
- [ ] Response mentions specific Azul mechanics (tiles, pattern lines, wall)
- [ ] Cites rulebook or provides source attribution
- [ ] No generic "in tile-laying games..." answers
- [ ] Professional, structured format

### Scenario 2: Professional Tone ✅

**Query**: "What's the best opening strategy?"

**Verify**:
- [ ] Expert, authoritative tone (not casual)
- [ ] Multiple options presented with trade-offs
- [ ] Strategic analysis vocabulary used
- [ ] No emoji, no "lol" or casual language

### Scenario 3: Uncertainty Handling ✅

**Query**: "What are the rules of [obscure expansion]?"

**Verify**:
- [ ] Explicitly states limitation if unknown
- [ ] Doesn't fabricate rules
- [ ] Suggests consulting official resources
- [ ] Provides general principles if applicable

### Scenario 4: Citation Format ✅

**Query**: "How many tiles are in the factory?"

**Verify**:
- [ ] Uses retrieved context from Azul PDF
- [ ] Citation format present: [Source: ...] or "According to rulebook"
- [ ] Specific page/section reference (if available in chunks)

### Scenario 5: Conversational Context ✅

**Query Sequence**:
1. "Tell me about Azul"
2. "What about for 2 players?"

**Verify**:
- [ ] Second question understood as referring to Azul
- [ ] No re-asking "which game?"
- [ ] Maintains conversation thread context

---

## Debugging RAG Issues

### Issue: No chunks retrieved

**Check**:
```sql
SELECT * FROM vector_documents WHERE "Id" = '8b78c72a-b5bc-454e-875b-22754a673c40';
```

**Fix**: Ensure `IndexingStatus = 'completed'` and `ChunkCount > 0`

### Issue: Generic responses (not using RAG)

**Check Configuration**:
```sql
SELECT selected_document_ids_json, is_current
FROM agent_configurations ac
JOIN agents a ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';
```

**Fix**: Ensure `selected_document_ids_json` contains VectorDocument ID and `is_current = true`

### Issue: Context not injected in prompt

**Check Qdrant**:
- Navigate to: http://localhost:6333/dashboard
- Collection: `game-rules` (or configured collection)
- Verify points exist for Azul

**Check Logs**:
```bash
docker logs meepleai-api | grep -A 5 "MeepleAssistant"
docker logs meepleai-api | grep -A 5 "RAG"
```

### Issue: Low confidence or poor answers

**Upgrade Strategy**: From SingleModel to HybridSearch

```sql
UPDATE agents
SET
    "StrategyName" = 'HybridSearch',
    "StrategyParametersJson" = '{"VectorWeight":0.7,"TopK":10,"MinScore":0.55}'
WHERE "Name" = 'MeepleAssistant POC';
```

---

## Performance Benchmarks

### Expected Latency (SingleModel + RAG)

| Component | Time | Notes |
|-----------|------|-------|
| Vector Search | ~100-300ms | Qdrant retrieval |
| LLM Call | ~1-3s | Haiku generation |
| **Total** | ~1.5-3.5s | End-to-end response |

### Expected Costs (Claude 3 Haiku)

| Operation | Input Tokens | Output Tokens | Cost |
|-----------|--------------|---------------|------|
| Simple Query | ~500 | ~150 | ~$0.0002 |
| Complex Query | ~1500 | ~400 | ~$0.0005 |
| **Daily (100 queries)** | - | - | ~$0.03-0.05 |

---

## Upgrade Path

### Phase 1: POC (Current) ✅
- SingleModel strategy
- 1 VectorDocument (Azul)
- Basic RAG context injection
- Professional responses
- **Cost**: ~$0.03/day (100 queries)

### Phase 2: Production Ready
```sql
-- Add more games
UPDATE agent_configurations
SET selected_document_ids_json = '["azul-id","catan-id","wingspan-id"]'
WHERE agent_id = '49365068-d1db-4a66-aff5-f9fadca2763b';

-- Upgrade to HybridSearch
UPDATE agents
SET "StrategyName" = 'HybridSearch',
    "StrategyParametersJson" = '{"VectorWeight":0.7,"TopK":10,"MinScore":0.55}'
WHERE "Id" = '49365068-d1db-4a66-aff5-f9fadca2763b';
```

### Phase 3: Advanced RAG
```sql
-- IterativeRAG for +14% accuracy
UPDATE agents
SET "StrategyName" = 'IterativeRAG',
    "StrategyParametersJson" = '{
      "MaxIterations":3,
      "TopK":5,
      "TopKPerIteration":3,
      "MinScore":0.6,
      "RefinementThreshold":0.7
    }'
WHERE "Id" = '49365068-d1db-4a66-aff5-f9fadca2763b';
```

---

## Quality Metrics

Track agent performance over time:

```sql
-- Invocation stats
SELECT
    "Name",
    "InvocationCount",
    "LastInvokedAt",
    CASE
        WHEN "LastInvokedAt" > NOW() - INTERVAL '24 hours' THEN 'Active'
        WHEN "LastInvokedAt" > NOW() - INTERVAL '7 days' THEN 'Recent'
        ELSE 'Idle'
    END as activity_status
FROM agents
WHERE "Name" = 'MeepleAssistant POC';

-- Token usage (if tracked)
SELECT
    SUM(input_tokens) as total_input,
    SUM(output_tokens) as total_output,
    SUM(estimated_cost) as total_cost,
    COUNT(*) as query_count
FROM llm_cost_logs
WHERE agent_id = '49365068-d1db-4a66-aff5-f9fadca2763b'
  AND created_at > NOW() - INTERVAL '7 days';
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **401 Unauthorized** | Login expired, re-authenticate |
| **Agent not found** | Verify agent ID in database |
| **No RAG context** | Check `selected_document_ids_json` not empty |
| **Generic answers** | Verify VectorDocument indexed (status='completed') |
| **Slow responses** | Check Qdrant/Embedding services health |
| **High costs** | Consider switching to free model (Gemma) or Ollama |

---

**Created**: 2026-02-18
**Status**: Ready for production testing
**Related**: Epic #3687, DefaultAgentSeeder implementation
