# KnowledgeBase Bounded Context

## Responsabilità

Gestisce la knowledge base RAG (Retrieval-Augmented Generation), ricerca ibrida, chat intelligente e generazione di risposte.

## Funzionalità Principali

- **Hybrid Search**: Combinazione di ricerca vettoriale (Qdrant) e keyword (PostgreSQL FTS)
- **RAG Pipeline**: Retrieval-Augmented Generation con validazione multi-layer
- **Chat Intelligente**: Conversazioni context-aware con gestione thread
- **Embedding Management**: Gestione vettori e indicizzazione
- **Multi-Model Generation**: Consensus tra GPT-4 e Claude
- **Quality Validation**: 5-layer validation con confidence scoring

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (ChatThread, Message, VectorDocument, RuleSpec)
- **ValueObjects/**: Oggetti valore immutabili (Question, Answer, ConfidenceScore, Citation)
- **Services/**: Domain services per logica complessa (scoring, validation)
- **Events/**: Domain events (QuestionAsked, AnswerGenerated, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - AskQuestion (streaming SSE)
  - CreateChatThread
  - AddMessageToThread
  - IndexDocument
  - UpdateEmbedding
- **Queries/**: Operazioni di lettura
  - SearchQuery (hybrid search)
  - GetChatHistory
  - GetThreadMessages
  - GetRelevantDocuments
- **DTOs/**: Data Transfer Objects per le risposte (ChatResponse, SearchResult, CitationDTO)
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi (IVectorStore, IEmbeddingService, ILLMService)
- **Services/**: Application services per orchestrazione (RagService)

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core e Qdrant
- **Services/**:
  - QdrantVectorStore: Integrazione con Qdrant
  - OpenRouterLLMService: Integrazione con OpenRouter
  - EmbeddingService: Generazione embeddings
  - HybridSearchService: RRF (Reciprocal Rank Fusion)
- **Adapters/**: Adattatori per servizi AI esterni

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati (SQL + Vector DB)

## Hybrid Search (ADR-001)

La ricerca ibrida combina:
- **Vector Search** (Qdrant): Similarity semantica, 70% weight
- **Keyword Search** (PostgreSQL FTS): Exact matches, 30% weight
- **RRF (Reciprocal Rank Fusion)**: Fusione dei risultati

### Quality Metrics
- **P@10** (Precision at 10): >85% target
- **MRR** (Mean Reciprocal Rank): >0.75 target
- **Confidence Score**: ≥0.70 threshold
- **Hallucination Rate**: <3% target

### 5-Layer Validation
1. **Confidence Threshold**: Score ≥0.70
2. **Citation Verification**: Tutte le citazioni verificate nei documenti source
3. **Forbidden Keywords**: Blocco parole come "suppongo", "probabilmente"
4. **Factual Consistency**: Cross-check tra documenti
5. **Query-Answer Relevance**: Semantic similarity

## RAG Pipeline

```
User Question
    ↓
Query Expansion (synonyms, reformulation)
    ↓
Hybrid Search (Vector + Keyword → RRF)
    ↓
Context Retrieval (top-k documents)
    ↓
Multi-Model Generation (GPT-4 + Claude consensus)
    ↓
5-Layer Validation
    ↓
Streaming Response (SSE)
```

## API Endpoints

```
POST   /api/v1/chat                    → AskQuestionCommand (streaming SSE)
GET    /api/v1/search                  → SearchQuery
GET    /api/v1/chat/threads            → GetChatThreadsQuery
GET    /api/v1/chat/threads/{id}       → GetThreadMessagesQuery
POST   /api/v1/chat/threads            → CreateChatThreadCommand
POST   /api/v1/knowledge/index         → IndexDocumentCommand
```

## Database Entities

Vedi `Infrastructure/Entities/KnowledgeBase/`:
- `ChatThread`: Thread di conversazione
- `Message`: Singolo messaggio in un thread
- `VectorDocument`: Documento indicizzato con embedding
- `RuleSpec`: Specifica regola di gioco

## AI/ML Components

### Embedding Service
- **Model**: text-embedding-3-small (OpenAI) o alternativa
- **Dimensioni**: 1536 dimensions
- **Chunking**: Sentence-based chunking (20% migliore performance)

### LLM Service
- **Primary**: GPT-4 (OpenRouter)
- **Secondary**: Claude 3 Sonnet (consensus)
- **Streaming**: Server-Sent Events (SSE)

### Vector Store
- **Database**: Qdrant
- **Collection**: game-rules
- **Distance Metric**: Cosine similarity
- **Index**: HNSW

## Performance Optimizations

- **HybridCache**: L1 (in-memory) + L2 (Redis), 5min TTL
- **Query Expansion**: 15-25% recall boost
- **RRF Fusion**: Better than pure vector or keyword alone
- **Sentence Chunking**: 20% migliore retrieval quality
- **Connection Pooling**: Ottimizzazione Qdrant client

## Testing

- Unit tests per domain logic, scoring, validation
- Integration tests con Testcontainers (PostgreSQL, Qdrant)
- RAG evaluation tests (P@10, MRR, confidence)
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence (PostgreSQL)
- **Qdrant.Client**: Vector database
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **OpenRouter SDK**: LLM access
- **HybridCache**: Caching (Microsoft.Extensions.Caching)

## Note di Migrazione

Questo context è al 95% di completamento nella migrazione DDD/CQRS. Il `RagService` è mantenuto come orchestration service (infrastruttura), ma i principali use case sono gestiti tramite CQRS handlers.

## Related Documentation

- `docs/01-architecture/adr/adr-001-hybrid-rag.md`
- `docs/03-api/ai-provider-configuration.md`
- `docs/02-development/ai-provider-integration.md`
- `docs/04-frontend/chat-streaming-implementation.md`
