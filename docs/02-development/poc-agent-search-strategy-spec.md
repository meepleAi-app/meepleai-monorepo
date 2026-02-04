# POC: Agent Chat with Search Strategy Selection & Token Tracking

**Issue**: Agent default behavior definition
**Goal**: Test 3 RAG strategies with full cost/token tracking
**Timeline**: POC implementation (2-3 hours)

---

## Overview

Enable users to compare 3 agent search strategies with transparent cost tracking:

1. **Retrieval-Only** ($0, ~300ms): Return raw code chunks, no LLM
2. **Single Model** ($0-0.0009, ~2-5s): RAG + LLM synthesis (Ollama/OpenRouter)
3. **Multi-Model Consensus** (~$0.027, ~5-10s): RAG + GPT-4 + Claude validation

---

## API Design

### Endpoint
```
POST /api/v1/agents/chat/ask
Content-Type: application/json
Authorization: Bearer {token}
```

### Request
```json
{
  "question": "Come funziona OAuth in MeepleAI?",
  "strategy": "SingleModel",  // RetrievalOnly | SingleModel | MultiModelConsensus
  "sessionId": "optional-session-uuid",
  "boundedContext": "Authentication"  // Optional filter
}
```

### Response
```json
{
  "strategy": "SingleModel",
  "strategyDescription": "RAG + LLM synthesis (80% Ollama free)",

  "answer": "OAuth in MeepleAI uses the Authorization Code Flow with PKCE...",

  "retrievedChunks": [
    {
      "filePath": "Api/BoundedContexts/Authentication/Application/Commands/OAuthCallbackCommand.cs",
      "startLine": 45,
      "endLine": 67,
      "codePreview": "public async Task<Result> Handle(...)",
      "relevanceScore": 0.87,
      "boundedContext": "Authentication"
    }
  ],

  "tokenUsage": {
    "promptTokens": 1500,
    "completionTokens": 300,
    "totalTokens": 1800,
    "embeddingTokens": 50
  },

  "costBreakdown": {
    "embeddingCost": 0.00,
    "vectorSearchCost": 0.00,
    "llmCost": 0.00,  // Ollama free tier
    "totalCost": 0.00,
    "provider": "Ollama",
    "modelUsed": "llama-3.3-70b-versatile"
  },

  "latencyMs": 2340,
  "sessionId": "generated-or-provided-uuid",
  "timestamp": "2025-02-04T14:23:45Z"
}
```

---

## Token Tracking Architecture

### Flow Diagram
```
User Request
    ↓
AskAgentQuestionCommandHandler
    ↓
┌─────────────────────────────────────┐
│ 1. Embed Question (local)           │
│    → Track: embeddingTokens         │
└─────────────────────────────────────┘
    ↓
┌─────────────────────────────────────┐
│ 2. Vector Search (Qdrant)           │
│    → Track: searchLatency           │
└─────────────────────────────────────┘
    ↓
┌─────────────────────────────────────┐
│ 3. Strategy-Based Generation        │
│    ├─ RetrievalOnly: Skip LLM       │
│    ├─ SingleModel: Call LlmService  │
│    │   → Track: promptTokens        │
│    │   → Track: completionTokens    │
│    │   → Track: cost (from response)│
│    └─ MultiModel: 2x LlmService     │
│        → Track: 2x tokens & costs   │
└─────────────────────────────────────┘
    ↓
┌─────────────────────────────────────┐
│ 4. Persist Cost Log (DB)            │
│    → LlmCostLog entity              │
│    → Aggregate metrics              │
└─────────────────────────────────────┘
    ↓
Return AgentChatResponse
```

### Database Schema (Reuse Existing)

**Existing Table**: `LlmCostLog` (KnowledgeBase bounded context)

```sql
-- Already exists, we just add usage tracking
SELECT
    AgentName,
    Strategy,
    PromptTokens,
    CompletionTokens,
    TotalCost,
    COUNT(*) as RequestCount,
    SUM(TotalCost) as TotalSpent
FROM LlmCostLog
WHERE CreatedAt >= '2025-02-01'
GROUP BY AgentName, Strategy;
```

---

## Implementation Steps

### Phase 1: Core Command & Handler (30min)

**Files to Create**:
1. `Application/Commands/AskAgentQuestionCommand.cs`
2. `Application/Commands/AskAgentQuestionCommandHandler.cs`
3. `Application/DTOs/AgentChatResponse.cs`
4. `Domain/Enums/AgentSearchStrategy.cs`

**Handler Pseudocode**:
```csharp
public class AskAgentQuestionCommandHandler : IRequestHandler<AskAgentQuestionCommand, AgentChatResponse>
{
    private readonly IQdrantVectorStoreAdapter _qdrant;
    private readonly IEmbeddingService _embedding;
    private readonly ILlmService _llmService;
    private readonly ILlmCostLogRepository _costLog;

    public async Task<AgentChatResponse> Handle(AskAgentQuestionCommand request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var tokenUsage = new TokenUsage();
        var costBreakdown = new CostBreakdown();

        // 1. Embed question (local, $0)
        var embedding = await _embedding.GenerateAsync(request.Question, ct);
        tokenUsage.EmbeddingTokens = EstimateTokens(request.Question); // ~word count / 0.75

        // 2. Vector search (Qdrant, $0)
        var chunks = await _qdrant.SearchAsync(
            gameId: Guid.Empty, // Codebase collection
            queryVector: embedding,
            topK: 5,
            minScore: 0.6,
            cancellationToken: ct
        );

        // 3. Strategy-based generation
        string? answer = null;
        switch (request.Strategy)
        {
            case AgentSearchStrategy.RetrievalOnly:
                // No LLM, return chunks only
                break;

            case AgentSearchStrategy.SingleModel:
                var prompt = BuildPrompt(request.Question, chunks);
                var result = await _llmService.GenerateCompletionAsync(
                    systemPrompt: "You are a code assistant...",
                    userPrompt: prompt,
                    user: null,
                    strategy: RagStrategy.CostOptimized, // Force Ollama
                    cancellationToken: ct
                );

                answer = result.Text;
                tokenUsage.PromptTokens = result.Usage.PromptTokens;
                tokenUsage.CompletionTokens = result.Usage.CompletionTokens;
                costBreakdown.LlmCost = result.Cost;
                costBreakdown.Provider = result.Provider;
                costBreakdown.ModelUsed = result.Model;
                break;

            case AgentSearchStrategy.MultiModelConsensus:
                // Call GPT-4
                var gpt4Result = await CallGPT4(...);
                tokenUsage.PromptTokens += gpt4Result.PromptTokens;
                tokenUsage.CompletionTokens += gpt4Result.CompletionTokens;
                costBreakdown.LlmCost += gpt4Result.Cost;

                // Call Claude
                var claudeResult = await CallClaude(...);
                tokenUsage.PromptTokens += claudeResult.PromptTokens;
                tokenUsage.CompletionTokens += claudeResult.CompletionTokens;
                costBreakdown.LlmCost += claudeResult.Cost;

                // Consensus logic
                answer = SynthesizeConsensus(gpt4Result.Text, claudeResult.Text);
                break;
        }

        // 4. Calculate totals
        tokenUsage.TotalTokens = tokenUsage.PromptTokens + tokenUsage.CompletionTokens + tokenUsage.EmbeddingTokens;
        costBreakdown.TotalCost = costBreakdown.EmbeddingCost + costBreakdown.VectorSearchCost + costBreakdown.LlmCost;

        // 5. Persist cost log
        await _costLog.AddAsync(new LlmCostLog
        {
            AgentName = "CodebaseAgent",
            Strategy = request.Strategy.ToString(),
            PromptTokens = tokenUsage.PromptTokens,
            CompletionTokens = tokenUsage.CompletionTokens,
            TotalCost = costBreakdown.TotalCost,
            Provider = costBreakdown.Provider,
            Model = costBreakdown.ModelUsed,
            CreatedAt = DateTime.UtcNow
        }, ct);

        stopwatch.Stop();

        return new AgentChatResponse
        {
            Strategy = request.Strategy,
            StrategyDescription = GetStrategyDescription(request.Strategy),
            Answer = answer,
            RetrievedChunks = MapToCodeChunks(chunks),
            TokenUsage = tokenUsage,
            CostBreakdown = costBreakdown,
            LatencyMs = (int)stopwatch.ElapsedMilliseconds,
            SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow
        };
    }
}
```

---

### Phase 2: Codebase Indexer (30min)

**Command**: `IndexCodebaseCommand`

```csharp
public record IndexCodebaseCommand : IRequest<IndexCodebaseResult>
{
    public required List<string> BoundedContexts { get; init; }
    public bool ForceReindex { get; init; } = false;
}

public class IndexCodebaseCommandHandler : IRequestHandler<IndexCodebaseCommand, IndexCodebaseResult>
{
    public async Task<IndexCodebaseResult> Handle(IndexCodebaseCommand request, CancellationToken ct)
    {
        var indexed = 0;
        var errors = new List<string>();

        foreach (var context in request.BoundedContexts)
        {
            // 1. Find all .cs files in bounded context
            var files = Directory.GetFiles(
                Path.Combine("Api", "BoundedContexts", context),
                "*.cs",
                SearchOption.AllDirectories
            );

            foreach (var file in files)
            {
                try
                {
                    // 2. Read and chunk code
                    var code = await File.ReadAllTextAsync(file, ct);
                    var chunks = ChunkCode(code, file); // Function-level chunking

                    // 3. Generate embeddings
                    var texts = chunks.Select(c => c.Code).ToList();
                    var embeddings = await _embedding.GenerateAsync(texts, ct);

                    // 4. Upsert to Qdrant
                    var points = chunks.Zip(embeddings, (chunk, emb) => new
                    {
                        Id = Guid.NewGuid(),
                        Vector = emb,
                        Payload = new
                        {
                            file_path = chunk.FilePath,
                            start_line = chunk.StartLine,
                            end_line = chunk.EndLine,
                            code = chunk.Code,
                            bounded_context = context
                        }
                    });

                    await _qdrant.UpsertAsync("meepleai-codebase", points, ct);
                    indexed++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{file}: {ex.Message}");
                }
            }
        }

        return new IndexCodebaseResult
        {
            FilesIndexed = indexed,
            Errors = errors,
            TotalChunks = indexed * 5 // Avg 5 chunks per file
        };
    }
}
```

---

### Phase 3: Frontend UI (30min)

**React Component**: `AgentChatInterface.tsx`

```tsx
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

type Strategy = 'RetrievalOnly' | 'SingleModel' | 'MultiModelConsensus';

export function AgentChatInterface() {
  const [question, setQuestion] = useState('');
  const [strategy, setStrategy] = useState<Strategy>('RetrievalOnly');
  const [response, setResponse] = useState<any>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    setLoading(true);
    const res = await fetch('/api/v1/agents/chat/ask', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question, strategy })
    });
    const data = await res.json();
    setResponse(data);
    setLoading(false);
  };

  return (
    <div className="space-y-6">
      {/* Question Input */}
      <Textarea
        placeholder="Ask a question about the codebase..."
        value={question}
        onChange={(e) => setQuestion(e.target.value)}
      />

      {/* Strategy Selection */}
      <div className="space-y-3">
        <Label>Search Strategy</Label>
        <RadioGroup value={strategy} onValueChange={(v) => setStrategy(v as Strategy)}>
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="RetrievalOnly" id="retrieval" />
            <Label htmlFor="retrieval">
              Retrieval Only <span className="text-green-600">($0.00, ~300ms)</span>
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="SingleModel" id="single" />
            <Label htmlFor="single">
              Single Model <span className="text-blue-600">($0-0.0009, ~2-5s)</span>
            </Label>
          </div>
          <div className="flex items-center space-x-2">
            <RadioGroupItem value="MultiModelConsensus" id="multi" />
            <Label htmlFor="multi">
              Multi-Model Consensus <span className="text-orange-600">(~$0.027, ~5-10s)</span>
            </Label>
          </div>
        </RadioGroup>
      </div>

      <Button onClick={handleSubmit} disabled={loading}>
        {loading ? 'Processing...' : 'Ask Agent'}
      </Button>

      {/* Response Display */}
      {response && (
        <div className="space-y-4 p-4 border rounded-lg">
          {/* Token & Cost Metrics */}
          <div className="grid grid-cols-3 gap-4 p-3 bg-gray-50 rounded">
            <div>
              <p className="text-sm text-gray-600">Tokens</p>
              <p className="text-lg font-semibold">{response.tokenUsage.totalTokens}</p>
            </div>
            <div>
              <p className="text-sm text-gray-600">Cost</p>
              <p className="text-lg font-semibold">${response.costBreakdown.totalCost.toFixed(4)}</p>
            </div>
            <div>
              <p className="text-sm text-gray-600">Latency</p>
              <p className="text-lg font-semibold">{response.latencyMs}ms</p>
            </div>
          </div>

          {/* Answer (if generated) */}
          {response.answer && (
            <div>
              <h3 className="font-semibold mb-2">Answer</h3>
              <p className="text-sm">{response.answer}</p>
            </div>
          )}

          {/* Code Chunks */}
          <div>
            <h3 className="font-semibold mb-2">Retrieved Code ({response.retrievedChunks.length})</h3>
            {response.retrievedChunks.map((chunk: any, i: number) => (
              <div key={i} className="mb-3 p-3 border rounded">
                <div className="flex justify-between text-sm mb-2">
                  <span className="font-mono text-xs">{chunk.filePath}</span>
                  <span className="text-gray-600">Score: {chunk.relevanceScore.toFixed(2)}</span>
                </div>
                <pre className="text-xs bg-gray-100 p-2 rounded overflow-x-auto">
                  {chunk.codePreview}
                </pre>
              </div>
            ))}
          </div>

          {/* Provider Info */}
          <div className="text-xs text-gray-600">
            Provider: {response.costBreakdown.provider} | Model: {response.costBreakdown.modelUsed || 'N/A'}
          </div>
        </div>
      )}
    </div>
  );
}
```

---

### Phase 4: Manual Testing (20min)

**Test Script**: `poc-test-agent-strategies.sh`

```bash
#!/bin/bash

API_URL="http://localhost:8080/api/v1/agents/chat/ask"
TOKEN="your-auth-token"

# Test Questions
QUESTIONS=(
  "Come funziona OAuth in MeepleAI?"
  "Dove sono i test per autenticazione?"
  "Come si registra un nuovo utente?"
  "Quali bounded contexts ci sono?"
  "Come funziona il pattern CQRS?"
)

# Test each strategy
for strategy in "RetrievalOnly" "SingleModel" "MultiModelConsensus"; do
  echo "=========================================="
  echo "Testing Strategy: $strategy"
  echo "=========================================="

  for question in "${QUESTIONS[@]}"; do
    echo "Question: $question"

    response=$(curl -s -X POST "$API_URL" \
      -H "Content-Type: application/json" \
      -H "Authorization: Bearer $TOKEN" \
      -d "{\"question\": \"$question\", \"strategy\": \"$strategy\"}")

    echo "Response:"
    echo "$response" | jq '.tokenUsage, .costBreakdown, .latencyMs'
    echo ""
  done
done
```

---

## Success Criteria

### POC is successful if:

1. ✅ All 3 strategies return valid responses
2. ✅ Token tracking shows correct counts for each strategy:
   - RetrievalOnly: embeddingTokens only, totalCost = $0
   - SingleModel: prompt + completion tokens, totalCost ≤ $0.0009
   - MultiModel: 2x tokens, totalCost ≤ $0.03
3. ✅ Latency matches expectations:
   - RetrievalOnly: < 500ms
   - SingleModel: < 6s
   - MultiModel: < 12s
4. ✅ Cost logs persisted in database
5. ✅ UI displays metrics correctly

---

## Comparison Table (Manual Fill After Tests)

| Question | Strategy | Tokens | Cost | Latency | Chunks Quality | Answer Quality | Preferred? |
|----------|----------|--------|------|---------|----------------|----------------|------------|
| OAuth    | Retrieval | 50 | $0 | 320ms | ⭐⭐⭐⭐ | N/A | ? |
| OAuth    | Single | 1800 | $0 | 2.3s | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ? |
| OAuth    | Multi | 3600 | $0.025 | 8.1s | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ? |

---

## Next Steps After POC

### If Retrieval-Only is sufficient (80%+ cases):
- Default to RetrievalOnly
- Add "Explain with AI" button for LLM synthesis on-demand

### If Single Model is preferred:
- Default to SingleModel with Ollama routing
- Monitor free tier hit rate (target: >80%)

### If Multi-Model is needed:
- Reserve for "critical" queries (user-triggered)
- Add cost warning before execution

---

## Files Created

```
Api/BoundedContexts/KnowledgeBase/
├── Application/
│   ├── Commands/
│   │   ├── AskAgentQuestionCommand.cs
│   │   ├── AskAgentQuestionCommandHandler.cs
│   │   ├── IndexCodebaseCommand.cs
│   │   └── IndexCodebaseCommandHandler.cs
│   └── DTOs/
│       └── AgentChatResponse.cs
├── Domain/
│   └── Enums/
│       └── AgentSearchStrategy.cs
└── Infrastructure/
    └── Services/
        └── CodeChunkingService.cs (helper for indexing)

apps/web/src/components/
└── agent/
    └── AgentChatInterface.tsx

docs/02-development/
├── poc-agent-search-strategy-spec.md (this file)
└── poc-test-results.md (to be filled after tests)

scripts/
└── poc-test-agent-strategies.sh
```

---

## Estimated Timeline

- **Phase 1** (Core Command): 30min
- **Phase 2** (Indexer): 30min
- **Phase 3** (Frontend UI): 30min
- **Phase 4** (Manual Testing): 20min
- **Total**: ~2 hours implementation + 20min testing

---

## Cost Budget for POC Testing

```
Test Scenario:
  - 5 questions × 3 strategies = 15 total queries
  - Breakdown:
    * 5 RetrievalOnly = $0
    * 5 SingleModel = $0 (assume 100% Ollama free hit)
    * 5 MultiModel = 5 × $0.027 = $0.135

Total POC Budget: ~$0.15 (acceptable)
```
