# KnowledgeBase Bounded Context

**RAG System, AI Agents, Chat Threads, Vector Search**

---

## 📋 Responsabilità

- Hybrid RAG (Vector + Keyword search con RRF)
- Multi-model LLM consensus (GPT-4 + Claude)
- AI Agent orchestration (rules clarification, strategy, setup)
- Chat thread management e history
- 5-layer confidence validation

---

## 🏗️ Domain Model

### Aggregates

**ChatThread**:
```csharp
public class ChatThread
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? GameId { get; private set; }
    public List<ChatMessage> Messages { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void AddMessage(string content, string role) { }
}
```

**ChatMessage**:
```csharp
public class ChatMessage
{
    public Guid Id { get; private set; }
    public string Content { get; private set; }
    public string Role { get; private set; }          // "user" | "assistant"
    public double? ConfidenceScore { get; private set; }
    public List<Source> Sources { get; private set; }
}
```

**EmbeddingChunk**:
```csharp
public class EmbeddingChunk
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Content { get; private set; }
    public int PageNumber { get; private set; }
    public float[] Embedding { get; private set; }    // 1024-dim BGE-M3 vector
}
```

---

## 📡 Application Layer

### Commands

| Command | Description | Endpoint |
|---------|-------------|----------|
| `AskQuestionCommand` | RAG query con SSE streaming | `POST /api/v1/chat` |
| `DeleteThreadCommand` | Elimina chat thread | `DELETE /api/v1/chat/threads/{id}` |
| `CreateEmbeddingsCommand` | Genera embeddings per chunks | Internal (event-driven) |

### Queries

| Query | Description | Endpoint |
|-------|-------------|----------|
| `GetThreadsQuery` | Lista thread utente | `GET /api/v1/chat/threads` |
| `GetThreadByIdQuery` | Thread con messaggi | `GET /api/v1/chat/threads/{id}` |
| `SearchChunksQuery` | Hybrid search (vector + keyword) | Internal |

---

## 🎯 Hybrid RAG Architecture (ADR-001)

### Search Strategy

**70% Vector Search** (Qdrant):
```csharp
var vectorResults = await _qdrant.SearchAsync(
    collection: "chunks",
    vector: questionEmbedding,
    limit: 20,
    scoreThreshold: 0.6
);
```

**30% Keyword Search** (PostgreSQL FTS):
```csharp
var keywordResults = await _db.Chunks
    .Where(c => EF.Functions.ToTsVector("italian", c.Content)
        .Matches(EF.Functions.ToTsQuery("italian", searchQuery)))
    .OrderByDescending(c => EF.Functions.TsRank(...))
    .Take(20)
    .ToListAsync();
```

**Reciprocal Rank Fusion**:
```csharp
// Merge vector + keyword results
var fusedResults = ReciprocalRankFusion(vectorResults, keywordResults);
// RRF score = Σ(1 / (k + rank_i)) where k=60
```

**Impact**: 15-25% recall improvement, <3% hallucination rate

---

## 🤖 Multi-Model Consensus (ADR-007)

### Validation Pipeline (5 Layers)

**Layer 1: Confidence Threshold**
```csharp
if (confidence < 0.7) return "Insufficient confidence";
```

**Layer 2: Citation Requirement**
```csharp
if (sources.Count == 0) return "No sources found";
```

**Layer 3: Forbidden Keywords**
```csharp
var forbidden = new[] { "I don't know", "I'm not sure", "impossible to answer" };
if (answer.ContainsAny(forbidden)) return "Uncertain answer";
```

**Layer 4: Multi-Model Agreement**
```csharp
var gpt4Answer = await _openRouter.CompleteAsync(prompt, model: "gpt-4");
var claudeAnswer = await _openRouter.CompleteAsync(prompt, model: "claude-3");

var consensus = CalculateConsensus(gpt4Answer, claudeAnswer);
if (consensus < 0.7) return "Models disagree";
```

**Layer 5: Cosine Similarity Consensus**
```csharp
var embedding1 = await _embeddingService.EmbedAsync(gpt4Answer);
var embedding2 = await _embeddingService.EmbedAsync(claudeAnswer);

var similarity = CosineSimilarity(embedding1, embedding2);
// High similarity (>0.85) → High confidence
// Low similarity (<0.70) → Models interpret differently
```

**Result**: <3% hallucination rate (vs. 8-12% single model)

---

## 🧠 AI Agents (ADR-004)

**Agent Types**:
1. **RulesAgent**: Clarify game rules
2. **StrategyAgent**: Provide gameplay tips
3. **SetupAgent**: Guide initial game setup

**Agent Selection**:
```csharp
var agent = question switch
{
    _ when question.Contains("come si gioca") => AgentType.Rules,
    _ when question.Contains("strategia") => AgentType.Strategy,
    _ when question.Contains("setup") => AgentType.Setup,
    _ => AgentType.Rules // default
};
```

---

## 🔄 Integration Points

### Inbound Events

**From DocumentProcessing**:
```csharp
public class DocumentProcessedEventHandler : INotificationHandler<DocumentProcessedEvent>
{
    public async Task Handle(DocumentProcessedEvent evt, CancellationToken ct)
    {
        // Generate embeddings for chunks → Store in Qdrant
    }
}
```

### Outbound Dependencies

**GameManagement**:
- Fetch game metadata for context

**External Services**:
- **OpenRouter API**: LLM completions (GPT-4, Claude)
- **Embedding Service**: BGE-M3 embeddings (Python)
- **Qdrant**: Vector storage and search

---

## 🧪 Testing

**Location**: `tests/Api.Tests/BoundedContexts/KnowledgeBase/`

**Coverage**: 92%

**Key Tests**:
```csharp
// Hybrid RAG
HybridSearch_ShouldMergeVectorAndKeywordResults()
ReciprocalRankFusion_ShouldRankCorrectly()

// Multi-Model
MultiModelConsensus_HighAgreement_ShouldReturnHighConfidence()
CosineSimilarity_LowSimilarity_ShouldReturnLowConfidence()

// Validation
ConfidenceValidation_BelowThreshold_ShouldReject()
CitationValidation_NoSources_ShouldReject()
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/KnowledgeBase/`

**Key Files**:
- `Domain/Entities/ChatThread.cs`
- `Application/Commands/AskQuestionCommand.cs`
- `Application/Handlers/AskQuestionCommandHandler.cs`
- `Infrastructure/Services/HybridSearchService.cs`
- `Infrastructure/Services/MultiModelValidationService.cs`

---

## 📖 Related Documentation

- [ADR-001: Hybrid RAG](../01-architecture/adr/adr-001-hybrid-rag.md)
- [ADR-007: Hybrid LLM](../01-architecture/adr/adr-007-hybrid-llm.md)
- [ADR-005: Cosine Similarity Consensus](../01-architecture/adr/adr-005-cosine-similarity-consensus.md)
- [RAG System Diagram](../01-architecture/diagrams/rag-system-detailed.md)

---

**Last Updated**: 2026-01-18
**Status**: ✅ Production
**Critical Features**: Hybrid RAG, Multi-model validation
