# POC Testing Instructions - Agent Search Strategy Comparison

**Date**: 2026-02-04
**Goal**: Compare 3 agent search strategies with full token/cost tracking
**Duration**: ~20 minutes manual testing

---

## Prerequisites

### 1. Services Running

Ensure these services are running:

```bash
# Terminal 1: Start infrastructure
cd infra
docker compose up -d postgres qdrant redis

# Terminal 2: Start API
cd apps/api/src/Api
dotnet run

# Terminal 3: Start Web (optional, for UI testing)
cd apps/web
pnpm dev
```

### 2. Test Data

**Option A: Use existing PDF data**
- If you have PDFs already indexed in Qdrant, skip to testing
- Check: http://localhost:8080/scalar/v1 → `/api/v1/knowledge/...` endpoints

**Option B: Upload test PDF**
1. Navigate to http://localhost:3000
2. Upload a game rules PDF (any board game manual)
3. Wait for indexing to complete (~30s per PDF)

---

## Testing Methods

### Method 1: Bash Script (Automated)

Run the automated test script:

```bash
cd scripts
chmod +x poc-test-agent-strategies.sh
./poc-test-agent-strategies.sh
```

**Output**:
- Console: Real-time progress with colored output
- File: `poc-test-results-{timestamp}.md` with detailed metrics

**What it tests**:
- 5 sample questions × 3 strategies = 15 API calls
- Metrics: Tokens, Cost, Latency, Chunks Retrieved
- Comparison table for manual analysis

---

### Method 2: Manual cURL Testing

Test individual strategies manually:

#### Test 1: Retrieval-Only ($0, ~300ms)

```bash
curl -X POST http://localhost:8080/api/v1/agents/chat/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How do I start the game?",
    "strategy": "RetrievalOnly"
  }' | jq
```

**Expected Output**:
```json
{
  "strategy": "RetrievalOnly",
  "strategyDescription": "Retrieval-Only: No LLM generation, raw chunks ($0, ~300ms)",
  "answer": null,
  "retrievedChunks": [
    {
      "filePath": "game-manual.pdf",
      "startLine": 3,
      "codePreview": "Setup: Each player receives...",
      "relevanceScore": 0.87,
      ...
    }
  ],
  "tokenUsage": {
    "promptTokens": 0,
    "completionTokens": 0,
    "totalTokens": 7,
    "embeddingTokens": 7
  },
  "costBreakdown": {
    "totalCost": 0.00,
    "provider": "Local"
  },
  "latencyMs": 320
}
```

#### Test 2: Single Model ($0-0.0009, ~2-5s)

```bash
curl -X POST http://localhost:8080/api/v1/agents/chat/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How do I start the game?",
    "strategy": "SingleModel"
  }' | jq
```

**Expected Output**:
```json
{
  "strategy": "SingleModel",
  "answer": "To start the game, each player receives 5 cards...",
  "tokenUsage": {
    "totalTokens": 1847
  },
  "costBreakdown": {
    "totalCost": 0.00,
    "provider": "Ollama",
    "modelUsed": "llama-3.3-70b-versatile"
  },
  "latencyMs": 2340
}
```

#### Test 3: Multi-Model Consensus (~$0.027, ~5-10s)

```bash
curl -X POST http://localhost:8080/api/v1/agents/chat/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How do I start the game?",
    "strategy": "MultiModelConsensus"
  }' | jq
```

**Expected Output**:
```json
{
  "strategy": "MultiModelConsensus",
  "answer": "GPT-4 Response:\n...\n\nClaude Response:\n...",
  "tokenUsage": {
    "totalTokens": 3694
  },
  "costBreakdown": {
    "totalCost": 0.027,
    "provider": "Multi-Model (GPT-4 + Claude)"
  },
  "latencyMs": 8120
}
```

---

### Method 3: Web UI Testing (Visual)

1. Navigate to: **http://localhost:3000/poc/agent-chat**

2. Enter a question (e.g., "How do I start the game?")

3. Select strategy via radio buttons

4. Click "Ask Agent"

5. Observe metrics display:
   - **Tokens**: Total count + breakdown
   - **Cost**: USD amount + provider
   - **Latency**: Milliseconds
   - **Chunks**: Retrieved context with relevance scores

6. Compare strategies by asking same question 3 times with different strategies

---

## Sample Questions

Use these questions for testing (adjust based on your PDF content):

1. **Game Setup**: "How do I start the game?"
2. **Rules**: "What are the winning conditions?"
3. **Players**: "How many players can play?"
4. **Components**: "What game components are included?"
5. **Turns**: "How do turns work?"

---

## Evaluation Criteria

### For Each Strategy, Evaluate:

#### 1. Chunk Quality (Retrieval-Only)
- ✅ **Good**: Relevant chunks with high scores (>0.7)
- ⚠️ **Medium**: Some relevant, some not (scores 0.5-0.7)
- ❌ **Poor**: Irrelevant chunks (<0.5)

#### 2. Answer Quality (Single Model / Multi-Model)
- ✅ **Good**: Accurate, complete, based on context
- ⚠️ **Medium**: Partially accurate, some gaps
- ❌ **Poor**: Inaccurate or hallucinated

#### 3. Cost vs Value
- **Retrieval-Only**: Best for quick lookups, fast responses
- **Single Model**: Best for production (80% free tier)
- **Multi-Model**: Best for critical validation only

#### 4. Performance
- **Latency Target**: <500ms (Retrieval), <6s (Single), <12s (Multi)
- **Throughput**: Requests/minute capacity

---

## Results Template

Fill this table after testing:

| Question | Strategy | Tokens | Cost | Latency | Chunks Quality | Answer Quality | Preferred? |
|----------|----------|--------|------|---------|----------------|----------------|------------|
| Game setup | Retrieval | | $0.00 | ms | ⭐⭐⭐⭐ | N/A | ? |
| Game setup | Single | | $0.00 | ms | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ? |
| Game setup | Multi | | $0.027 | ms | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ? |

*(Repeat for all 5 questions)*

---

## Success Criteria

POC is successful if:

- ✅ All 3 strategies return valid responses
- ✅ Token tracking shows correct counts:
  - Retrieval-Only: embeddingTokens only, totalCost = $0
  - SingleModel: prompt + completion tokens, totalCost ≤ $0.0009
  - MultiModel: 2x tokens, totalCost ≤ $0.03
- ✅ Latency within expected ranges
- ✅ Cost logs persisted in database
- ✅ Retrieved chunks are relevant (score ≥ 0.6)

---

## Database Verification

Check cost logs in PostgreSQL:

```sql
SELECT
    "Strategy",
    "Provider",
    "Model",
    "PromptTokens",
    "CompletionTokens",
    "TotalCost",
    "CreatedAt"
FROM "LlmCostLog"
WHERE "AgentName" = 'CodebaseAgent'
ORDER BY "CreatedAt" DESC
LIMIT 20;
```

Expected:
- Retrieval-Only: 0 tokens, $0 cost
- SingleModel: 1500-2000 tokens, $0 cost (Ollama)
- MultiModel: 3000-4000 tokens, ~$0.027 cost

---

## Troubleshooting

### API Not Responding
```bash
# Check if API is running
curl http://localhost:8080/health

# Check logs
cd apps/api/src/Api
dotnet run | grep "AskAgentQuestion"
```

### No PDF Data Available
```bash
# Check Qdrant collection
curl http://localhost:6333/collections

# Upload test PDF via UI or API
curl -X POST http://localhost:8080/api/v1/knowledge/index \
  -F "file=@test-game-rules.pdf" \
  -F "gameId=your-game-id"
```

### Frontend Not Loading
```bash
# Check Next.js build
cd apps/web
pnpm build
pnpm dev
```

### Qdrant Connection Errors
```bash
# Restart Qdrant
cd infra
docker compose restart qdrant
docker compose logs qdrant
```

---

## Next Steps After Testing

### If Retrieval-Only is Sufficient (80%+ cases):
```yaml
recommendation:
  default_strategy: RetrievalOnly
  fallback: Add "Explain with AI" button for on-demand LLM synthesis
  cost: $0/month
  ux: Fast, transparent, user decides when to use LLM
```

### If Single Model is Preferred:
```yaml
recommendation:
  default_strategy: SingleModel
  routing: Ollama (80% free) + OpenRouter fallback (20%)
  cost: ~$0.50-2/month (depending on usage)
  ux: Balanced speed and quality
```

### If Multi-Model is Needed:
```yaml
recommendation:
  default_strategy: SingleModel
  upgrade: User-triggered "Validate with Multi-Model" button
  cost: ~$5-15/month (reserved for critical queries)
  ux: Cost warning before execution
```

---

## Deliverables

After testing, create:

1. **Test Results**: `poc-test-results-{timestamp}.md` (auto-generated by script)
2. **Comparison Analysis**: Your manual evaluation of which strategy works best
3. **Recommendation**: Proposed default behavior for production agents
4. **Cost Projection**: Monthly cost estimate based on expected usage

---

## Files Created for POC

```
apps/api/src/Api/
├── BoundedContexts/KnowledgeBase/
│   ├── Domain/Enums/
│   │   └── AgentSearchStrategy.cs
│   └── Application/
│       ├── Commands/
│       │   ├── AskAgentQuestionCommand.cs
│       │   ├── AskAgentQuestionCommandHandler.cs
│       │   └── AskAgentQuestionCommandValidator.cs
│       └── DTOs/
│           └── AgentChatResponse.cs
└── Routing/
    └── AgentEndpoints.cs (modified)

apps/web/src/
├── components/agent/
│   └── AgentChatPOC.tsx
└── app/(authenticated)/poc/agent-chat/
    └── page.tsx

docs/02-development/
├── poc-agent-search-strategy-spec.md
└── poc-testing-instructions.md (this file)

scripts/
└── poc-test-agent-strategies.sh
```

---

## Support

If you encounter issues:
1. Check API logs: `dotnet run` output
2. Check Qdrant logs: `docker compose logs qdrant`
3. Verify secrets: `cd infra/secrets && pwsh setup-secrets.ps1`
4. Review spec: `docs/02-development/poc-agent-search-strategy-spec.md`
