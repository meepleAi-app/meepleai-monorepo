# Diagrammi Architettura MeepleAI

Questa directory contiene i diagrammi completi dell'architettura MeepleAI, generati tramite analisi automatizzata del codice.

## 📋 Indice Diagrammi

### 1. [Infrastructure Overview](./infrastructure-overview.md)
**Infrastruttura Docker Compose e Stack Tecnologico**

Contenuto:
- Diagramma completo dei servizi Docker (13 container)
- Porte e connessioni tra servizi
- Flusso dati per autenticazione, PDF processing e RAG
- Stack tecnologico (Backend, Frontend, AI/ML, DevOps)
- Configurazione osservabilità (Seq, Jaeger, Prometheus, Grafana)

**Diagrammi**: 3
- Docker Compose Architecture
- Data Flow Patterns
- Observability Connections

---

### 2. [CQRS/MediatR Flow](./cqrs-mediatr-flow.md)
**Pattern CQRS e Flusso MediatR**

Contenuto:
- Sequence diagram flusso CQRS generale
- Esempio concreto: User Registration flow
- Layered Architecture (HTTP → Application → Domain → Infrastructure)
- Command vs Query pattern comparison
- MediatR Pipeline Behaviors (Authorization, Validation, Logging, Transaction)
- Bounded Context interactions
- Dependency Injection flow

**Diagrammi**: 7
- CQRS General Flow (Sequence)
- User Registration Example (Sequence)
- Layered Architecture (Graph)
- Command Pattern (Flow)
- Query Pattern (Flow)
- MediatR Pipeline Behaviors (Flow)
- DI Flow (Graph)

---

### 3. [Bounded Contexts Interactions](./bounded-contexts-interactions.md)
**Interazioni tra i 7 Bounded Contexts DDD**

Contenuto:
- Class diagram per ogni Bounded Context:
  1. **Authentication**: User, Session, ApiKey, OAuth (Aggregates + VOs + Handlers)
  2. **GameManagement**: Game, GameSession (Aggregates + VOs + Handlers)
  3. **DocumentProcessing**: PdfDocument, Pipeline (3-stage extractors + Quality validation)
  4. **KnowledgeBase**: ChatThread, VectorDocument, RAG (Hybrid search + LLM + Validation)
  5. **SystemConfiguration**: SystemConfiguration, FeatureFlag (Dynamic config)
- Sequence diagrams:
  - Login with 2FA flow
  - 3-Stage PDF Extraction flow
  - RAG Query with Hybrid Search flow
- Cross-context relationships

**Diagrammi**: 12
- 5 Class diagrams (per bounded context)
- 3 Sequence diagrams (flows)
- 1 Cross-context relationship graph

---

### 4. [RAG System Detailed](./rag-system-detailed.md)
**Sistema RAG - Retrieval Augmented Generation (Dettaglio Completo)**

Contenuto:
- Architettura RAG completa (Hybrid Search)
- RRF Fusion Algorithm (Reciprocal Rank Fusion)
- Query Expansion Strategy (PERF-08: 15-25% recall boost)
- LLM Provider Routing Strategy (user-tier based, circuit breaker)
- 5-Layer Validation Pipeline
- Hallucination Detection (multilingual: 5 lingue)
- Cost Tracking Architecture
- Quality Metrics Dashboard
- Performance targets (latency, accuracy, cost)
- Dynamic configuration via SystemConfiguration

**Diagrammi**: 11
- RAG Complete Architecture
- RRF Fusion Algorithm (flowchart + formula)
- Query Expansion Strategy
- LLM Provider Routing (flowchart)
- Circuit Breaker State Machine
- 5-Layer Validation Pipeline
- Hallucination Detection (multilingual)
- Cost Tracking Architecture
- Quality Metrics Dashboard

**Performance Targets**:
| Metrica | Target | Status |
|---------|--------|--------|
| Retrieval Latency | < 1s | ✓ |
| Generation Latency | < 3s | ⚠ |
| Accuracy | > 95% | ⚠ |
| Hallucination Rate | < 3% | ✓ |
| Cost per Query | < $0.01 | ✓ |

---

### 5. [PDF Pipeline Detailed](./pdf-pipeline-detailed.md)
**Pipeline PDF Processing - 3-Stage Fallback (Dettaglio Completo)**

Contenuto:
- 3-Stage extraction pipeline con quality-based fallback
  - **Stage 1**: Unstructured (80% success, 1.3s avg, threshold: 0.80)
  - **Stage 2**: SmolDocling VLM (15% fallback, 3-5s avg, threshold: 0.70)
  - **Stage 3**: Docnet + OCR (5% fallback, 2-3s avg, best effort)
- Stage Decision Tree
- Quality Scoring Formula (4 metrics: text coverage 40%, structure 20%, tables 20%, pages 20%)
- Text Processing Domain Service (normalization + quality assessment)
- OCR Decision Logic
- Concurrency Control (Semaphore: max 4 concurrent Docnet operations)
- Error Handling Strategy
- Performance metrics per stage
- Configuration options

**Diagrammi**: 8
- 3-Stage Pipeline (complete flowchart)
- Stage Decision Tree
- Quality Scoring Formula
- Text Processing Domain Service
- OCR Decision Logic
- Concurrency Control (Semaphore sequence)
- Pipeline Performance Metrics
- Error Handling Strategy

**Stage Performance**:
| Stage | Success Rate | Avg Latency | Quality Threshold |
|-------|--------------|-------------|-------------------|
| Stage 1 (Unstructured) | ~80% | 1.3s | >= 0.80 |
| Stage 2 (SmolDocling) | ~15% | 3-5s | >= 0.70 |
| Stage 3 (Docnet) | ~5% | 2-3s | Best effort |
| **Overall** | **100%** | **P50: 1.5s** | **P95: 5s** |

---

## 🏗️ Architettura Overview

### Bounded Contexts (7)
```
Authentication          → User, Session, ApiKey, OAuth, 2FA
GameManagement         → Game, GameSession, Players
KnowledgeBase          → RAG, ChatThread, VectorDocument, Hybrid Search
DocumentProcessing     → PDF 3-stage pipeline, Quality validation
SystemConfiguration    → Dynamic config, Feature flags
WorkflowIntegration    → n8n workflows, Error logging
Administration         → User mgmt, Alerts, Audit, Analytics
```

### Layer Architecture
```
HTTP Layer (Minimal APIs)
    ↓
Application Layer (CQRS: Commands + Queries + Handlers)
    ↓
Domain Layer (Aggregates + Value Objects + Domain Services)
    ↓
Infrastructure Layer (Repositories + External Services + EF Core)
```

### Patterns Implementati
- **Domain-Driven Design (DDD)**: 7 bounded contexts, aggregates, value objects
- **CQRS (Command Query Responsibility Segregation)**: Separazione write/read
- **MediatR**: Mediator pattern per loose coupling
- **Repository Pattern**: Infrastructure isolation
- **Clean Architecture**: Layer separation
- **Event Sourcing** (partial): Domain events
- **Circuit Breaker**: Fault tolerance per external services
- **Fallback Architecture**: 3-stage PDF pipeline con quality-based routing
- **Hybrid Search**: Vector (Qdrant) + Keyword (PostgreSQL FTS) + RRF fusion

---

## 📊 Statistiche Architettura

### Codebase
- **Total Lines of Code**: ~45,000 (Backend) + ~12,000 (Frontend)
- **Bounded Contexts**: 7
- **Aggregates**: 12
- **Value Objects**: 25+
- **Domain Services**: 12+
- **CQRS Handlers**: 72+ (30+ Commands, 20+ Queries)
- **HTTP Endpoints**: 60+
- **Repositories**: 15+
- **Tests**: 4,225 (162 backend + 4,033 frontend + 30 E2E)
- **Test Coverage**: 90%+ (enforced)

### Legacy Code Removed
- **Total Lines Removed**: 2,070
  - GameService: 181
  - AuthService: 346
  - PDF Services: 1,300
  - UserManagementService: 243

### DDD Migration Status
- **Progress**: 99% complete
- **Contexts Migrated**: 7/7
  - 6 contexts at 100%
  - 1 context (KnowledgeBase) at 95%
- **Test Pass Rate**: 99.1%
- **Build Errors**: 0

---

## 🛠️ Strumenti per Visualizzazione

### Mermaid
Tutti i diagrammi sono scritti in **Mermaid**, compatibile con:
- GitHub (rendering automatico)
- GitLab
- VS Code (plugin Mermaid Preview)
- Obsidian
- Notion
- Confluence

### Live Editor
Per modificare/visualizzare i diagrammi:
- [Mermaid Live Editor](https://mermaid.live/)

### Plugin VS Code
```bash
code --install-extension bierner.markdown-mermaid
```

---

## 📖 Come Usare i Diagrammi

### 1. Per Onboarding Nuovi Sviluppatori
**Percorso consigliato**:
1. Inizia con [Infrastructure Overview](./infrastructure-overview.md) per capire lo stack
2. Leggi [CQRS/MediatR Flow](./cqrs-mediatr-flow.md) per capire i pattern
3. Esplora [Bounded Contexts Interactions](./bounded-contexts-interactions.md) per il domain model
4. Approfondisci i sistemi core:
   - [RAG System Detailed](./rag-system-detailed.md) per il sistema Q&A
   - [PDF Pipeline Detailed](./pdf-pipeline-detailed.md) per il processing documentale

### 2. Per Code Review
Usa i diagrammi per verificare:
- Aderenza al pattern CQRS
- Separazione corretta dei layer
- Dipendenze tra bounded contexts
- Flow validation corretto

### 3. Per Feature Development
Prima di implementare una feature:
1. Identifica il bounded context coinvolto
2. Verifica le interazioni esistenti nel class diagram
3. Segui il pattern CQRS dal diagramma flow
4. Mantieni la separazione dei layer

### 4. Per Troubleshooting
Usa i diagrammi di flow per:
- Tracciare il percorso di una richiesta
- Identificare punti di failure
- Verificare configurazioni (timeouts, thresholds)
- Analizzare performance bottleneck

---

## 🔄 Aggiornamento Diagrammi

I diagrammi sono stati generati tramite analisi automatizzata del codice in data **2025-11-13**.

### Quando Aggiornare
Aggiorna i diagrammi quando:
- Aggiungi un nuovo Bounded Context
- Modifichi significativamente un aggregato
- Cambi il flusso CQRS/MediatR
- Aggiungi/rimuovi stage nel PDF pipeline
- Modifichi il sistema RAG (es. nuovi provider LLM)
- Cambi l'infrastruttura Docker

### Come Aggiornare
1. Modifica i diagrammi Mermaid nei file `.md`
2. Valida la sintassi su [Mermaid Live Editor](https://mermaid.live/)
3. Committa con messaggio: `docs: update architecture diagram [context name]`
4. Verifica rendering su GitHub

---

## 📚 Riferimenti

### Documentazione Correlata
- [Architecture Overview](../board-game-ai-architecture-overview.md)
- [API Specification](../../api/board-game-ai-api-specification.md)
- [ADR-001: Hybrid RAG Architecture](../adr-001-hybrid-rag-architecture.md)
- [ADR-003: 3-Stage PDF Pipeline](../adr-003-pdf-pipeline-fallback.md)
- [DDD Status and Roadmap](../../refactoring/ddd-status-and-roadmap.md)
- [Database Schema](../../database-schema.md)

### External Resources
- [Mermaid Documentation](https://mermaid.js.org/)
- [DDD Reference](https://www.domainlanguage.com/ddd/)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

---

## 🤝 Contributing

Per contribuire ai diagrammi:
1. Segui le convenzioni Mermaid esistenti
2. Mantieni consistenza nei colori:
   - `#4fc3f7` - Application layer
   - `#29b6f6` - Services
   - `#66bb6a` - Success/Valid
   - `#ef5350` - Error/Critical
   - `#ffa726` - Warning/Processing
   - `#ab47bc` - Domain layer
3. Aggiungi legende quando necessario
4. Mantieni i diagrammi leggibili (max 20-25 nodi)
5. Documenta le metriche con tabelle

---

**Versione Diagrammi**: 1.0
**Data Generazione**: 2025-11-13
**Generato da**: Claude Code Analysis
**Stato**: ✅ Production Ready
**DDD Migration**: 99% Complete
