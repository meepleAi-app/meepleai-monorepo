# MeepleAI - Architettura e Design del Sistema

Architettura del sistema, decisioni architetturali (ADR), Domain-Driven Design, diagrammi e componenti.

**Data generazione**: 8 marzo 2026

**File inclusi**: 44

---

## Indice

1. architecture/adr/README.md
2. architecture/diagrams/README.md
3. architecture/README.md
4. architecture/adr/adr-002-multilingual-embedding.md
5. architecture/adr/adr-003b-unstructured-pdf.md
6. architecture/adr/adr-004-ai-agents.md
7. architecture/adr/adr-006-multi-layer-validation.md
8. architecture/adr/adr-007-hybrid-llm.md
9. architecture/adr/adr-009-centralized-error-handling.md
10. architecture/adr/adr-011-cors-whitelist-headers.md
11. architecture/adr/adr-012-fluentvalidation-cqrs.md
12. architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md
13. architecture/adr/adr-020-valueobject-record-evaluation.md
14. architecture/adr/adr-021-auto-configuration-system.md
15. architecture/adr/adr-022-ssr-auth-protection.md
16. architecture/adr/adr-023-share-request-workflow.md
17. architecture/adr/adr-024-advanced-pdf-embedding-pipeline.md
18. architecture/adr/adr-025-shared-catalog-bounded-context.md
19. architecture/adr/adr-026-document-collections.md
20. architecture/adr/adr-042-dashboard-performance.md
21. architecture/components/agent-lightning/architecture.md
22. architecture/components/agent-lightning/examples.md
23. architecture/components/agent-lightning/integration-guide.md
24. architecture/components/agent-lightning/openrouter-guide.md
25. architecture/components/agent-lightning/quickstart.md
26. architecture/components/amplifier/architecture-overview.md
27. architecture/components/amplifier/developer-workflow.md
28. architecture/components/confidence-validation.md
29. architecture/components/pdf-extraction-alternatives.md
30. architecture/ddd/ddd-migration-pattern-guide.md
31. architecture/ddd/quick-reference.md
32. architecture/diagrams/bounded-contexts-interactions.md
33. architecture/diagrams/cqrs-mediatr-flow.md
34. architecture/diagrams/github-actions-flow.md
35. architecture/diagrams/infrastructure-overview.md
36. architecture/diagrams/pdf-pipeline-detailed.md
37. architecture/diagrams/rag-system-detailed.md
38. architecture/overview/product-specification.md
39. architecture/overview/system-architecture.md
40. architecture/s3-complete-guide.md
41. architecture/s3-quickstart.md
42. architecture/s3-storage-operations-runbook.md
43. architecture/s3-storage-options.md
44. architecture/system-architecture-explained.md

---



<div style="page-break-before: always;"></div>

## architecture/adr/README.md

# Architecture Decision Records (ADR)

Architecture Decision Records for MeepleAI. Each ADR captures significant architectural decisions with context, rationale, and consequences.

## ADR Index

### Core Architecture

| ADR | Title | Date | Priority |
|-----|-------|------|----------|
| [002](adr-002-multilingual-embedding.md) | Multilingual Embedding Strategy | 2025-01-15 | High |
| [003b](adr-003b-unstructured-pdf.md) | Unstructured PDF Extraction | 2025-01-16 | High |
| [004](adr-004-ai-agents.md) | AI Agents Architecture | 2025-01-15 | Medium |
| [006](adr-006-multi-layer-validation.md) | Multi-Layer Validation Framework | 2025-01-16 | Critical |
| [007](adr-007-hybrid-llm.md) | Hybrid LLM Strategy | 2025-01-17 | High |
| [009](adr-009-centralized-error-handling.md) | Centralized Error Handling | 2025-01-18 | Medium |

### Security & Validation

| ADR | Title | Date | Priority |
|-----|-------|------|----------|
| [011](adr-011-cors-whitelist-headers.md) | CORS Header Whitelist Strategy | 2025-01-19 | Critical |
| [012](adr-012-fluentvalidation-cqrs.md) | FluentValidation Integration with CQRS | 2025-01-19 | High |
| [018](adr-018-postgresql-fts-for-shared-catalog.md) | PostgreSQL FTS for Shared Catalog | 2025-12-15 | High |

### Frontend/Backend Integration

| ADR | Title | Date | Priority |
|-----|-------|------|----------|
| [020](adr-020-valueobject-record-evaluation.md) | ValueObject Record Syntax (Rejected) | 2026-01-14 | Low |
| [021](adr-021-auto-configuration-system.md) | Auto-Configuration System | 2026-01-17 | High |
| [022](adr-022-ssr-auth-protection.md) | SSR Authentication Protection | 2025-11-22 | High |
| [023](adr-023-share-request-workflow.md) | Share Request Workflow | 2026-01-20 | High |
| [024](adr-024-advanced-pdf-embedding-pipeline.md) | Advanced PDF Embedding Pipeline | 2025-12-03 | High |
| [025](adr-025-shared-catalog-bounded-context.md) | SharedGameCatalog Bounded Context | 2026-01-14 | High |
| [026](adr-026-document-collections.md) | Document Collections | 2025-12-12 | Medium |

## ADR Lifecycle

| Status | Description |
|--------|-------------|
| **Proposed** | Under review |
| **Accepted** | Approved, being implemented |
| **Implemented** | Deployed and operational |
| **Rejected** | Considered but not adopted |

## Creating New ADRs

**Template Structure**: Context → Decision → Consequences → Alternatives

**Numbering Ranges**:
- 001-009: Core architecture
- 010-019: Security & validation
- 020-029: Frontend/backend integration
- 030-039: Performance (reserved)
- 040-049: Observability (reserved)
- 050-059: Infrastructure (reserved)

## Related Docs

- [System Architecture](../overview/system-architecture.md)
- [DDD Quick Reference](../ddd/quick-reference.md)
- [Bounded Contexts Diagram](../diagrams/bounded-contexts-interactions.md)

---

**Last Updated**: 2026-02-12
**Total ADRs**: 17 (15 Accepted/Implemented, 1 Rejected, 1 Deprecated)
**Archived**: 12 obsolete ADRs removed (see git history)


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/README.md

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
**Interazioni tra gli 11 Bounded Contexts DDD**

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
*(blocco di codice rimosso)*

### Layer Architecture
*(blocco di codice rimosso)*

### Patterns Implementati
- **Domain-Driven Design (DDD)**: 11 bounded contexts, aggregates, value objects
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
- **Progress**: 100% complete ✅
- **Contexts Migrated**: 7/7 (all at 100%)
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
*(blocco di codice rimosso)*

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
**DDD Migration**: 100% Complete ✅


---



<div style="page-break-before: always;"></div>

## architecture/README.md

# Infrastructure Documentation

**Docker Compose + Traefik + Monitoring**

---

## Quick Start

**Prerequisites**:
- Docker 24+
- Docker Compose 2.20+

**Setup**:
*(blocco di codice rimosso)*

---

## Architecture

### Service Stack

**Core Services**:
- PostgreSQL 16 (database)
- Redis 7 (cache + sessions)
- Qdrant (vector search)

**Application**:
- API (.NET 9)
- Web (Next.js 14)

**AI/ML Services**:
- embedding-service (Python)
- reranker-service (Python)
- unstructured-service (PDF processing)
- smoldocling-service (OCR)

**Infrastructure**:
- Traefik (reverse proxy)
- Prometheus (metrics)
- Grafana (dashboards)

---

## Configuration

### Docker Compose Profiles

**Minimal** (core only):
*(blocco di codice rimosso)*

**Dev** (with monitoring):
*(blocco di codice rimosso)*

**Full** (all services):
*(blocco di codice rimosso)*

### Environment Variables

**Location**: `infra/secrets/*.secret`

**Critical Secrets** (required):
- `database.secret` - PostgreSQL credentials
- `redis.secret` - Redis password
- `qdrant.secret` - Qdrant API key
- `jwt.secret` - JWT signing key
- `admin.secret` - Admin credentials
- `embedding-service.secret` - Embedding model

**Important Secrets** (warnings):
- `openrouter.secret` - LLM API key
- `unstructured-service.secret` - PDF processing
- `bgg.secret` - BoardGameGeek API

**Optional Secrets**:
- `oauth.secret` - OAuth providers
- `email.secret` - Email service
- `monitoring.secret` - Grafana admin

---

## Service Management

### Common Commands

*(blocco di codice rimosso)*

### Health Checks

**Endpoints**:
- API: http://localhost:8080/health
- Grafana: http://localhost:3001
- Traefik: http://localhost:8090

**Check Script**:
*(blocco di codice rimosso)*

---

## Networking

### Traefik Reverse Proxy

**Configuration**: `infra/traefik/traefik.yml`

**Routes**:
*(blocco di codice rimosso)*

**Dashboard**: http://localhost:8090

### Service Discovery

Traefik auto-discovers services via Docker labels:

*(blocco di codice rimosso)*

---

## Monitoring

### Prometheus

**URL**: http://localhost:9090
**Config**: `infra/monitoring/prometheus/prometheus.yml`

**Metrics**:
- Application metrics (custom)
- Container metrics (cAdvisor)
- PostgreSQL metrics (postgres_exporter)

### Grafana

**URL**: http://localhost:3001
**Credentials**: `infra/secrets/monitoring.secret`

**Dashboards**:
- Application Overview
- Database Performance
- Infrastructure Health

**Import Dashboards**:
*(blocco di codice rimosso)*

---

## Database Management

### PostgreSQL

**Connection**:
*(blocco di codice rimosso)*

**Backup**:
*(blocco di codice rimosso)*

### Redis

**Connection**:
*(blocco di codice rimosso)*

### Qdrant

**Connection**:
- Dashboard: http://localhost:6333/dashboard
- API: http://localhost:6333

**Operations**:
*(blocco di codice rimosso)*

---

## Volume Management

### Persistent Volumes

**Data Volumes**:
*(blocco di codice rimosso)*

**Backup Volumes**:
*(blocco di codice rimosso)*

---

## Troubleshooting

### Common Issues

**Service won't start**:
*(blocco di codice rimosso)*

**Port conflicts**:
*(blocco di codice rimosso)*

**Volume issues**:
*(blocco di codice rimosso)*

---

## Production Deployment

### Security Hardening

**Steps**:
1. Rotate all secrets
2. Enable HTTPS (Let's Encrypt)
3. Configure firewall rules
4. Enable fail2ban
5. Set up log aggregation
6. Configure backup automation

**Guide**: [Infrastructure Deployment Checklist](../04-deployment/infrastructure-deployment-checklist.md)

### Scaling

**Horizontal Scaling**:
*(blocco di codice rimosso)*

**Vertical Scaling**:
*(blocco di codice rimosso)*

**Guide**: [Scaling Guide](../04-deployment/scaling-guide.md)

---

## Related Documentation

- [Deployment Guide](../04-deployment/README.md)
- [Secrets Management](../04-deployment/secrets-management.md)
- [Monitoring Setup](../04-deployment/monitoring-setup-guide.md)
- [Runbooks](../04-deployment/runbooks/README.md)

---

**Last Updated**: 2026-01-31
**Maintainer**: DevOps Team


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-002-multilingual-embedding.md

# ADR-002: Multilingual Embedding Strategy (Italian-First)

**Status**: Accepted
**Date**: 2025-01-15
**Deciders**: CTO, ML Engineer
**Context**: Phase 1-4 Embedding Model Selection

---

## Context

MeepleAI targets the Italian market first (completely unserved), with future international expansion (French, German, Spanish). Embedding model choice impacts:
1. **Retrieval accuracy**: Better embeddings → better context retrieval → better answers
2. **Multilingual capability**: Italian quality now + expansion readiness later
3. **Cost**: Embedding generation (one-time per chunk) + storage (ongoing)

**Requirements**:
- Strong Italian language support (core market)
- Multilingual readiness (French, German, Spanish expansion Phase 4)
- Game-specific terminology preservation ("Meeple", "Worker Placement" should stay recognizable)
- Reasonable dimensionality (balance accuracy vs storage cost)

**Research Foundation**:
- Document source (line 105-107): "BoardGameAssistant.ai dichiara multilingual ma no evidence"
- Document source (line 107): "Translation complexity richiede domain expertise (keywords, mechanics, terminology)"
- Document source (line 133): Embedding model comparison table

---

## Decision

Use **multilingual-e5-large** base model with **Italian board game corpus fine-tuning** (Phase 3).

### Model Specifications

**Base Model** (Phase 1-2):
- **Name**: `intfloat/multilingual-e5-large`
- **Dimensions**: 1024 (balance accuracy vs storage)
- **Languages**: 100+ languages including Italian, French, German, Spanish
- **Training**: Multilingual corpus, contrastive learning
- **Performance**: MTEB benchmark (Italian): 0.65-0.70 (good baseline)

**Fine-Tuned Model** (Phase 3+):
- **Training Corpus**: 10,000 Italian board game rulebooks + community FAQ
- **Method**: Contrastive learning on annotated Q&A pairs (1000+ examples)
- **Target Performance**: +5-10 points over base model on Italian retrieval (MTEB: 0.75-0.80)
- **Training Cost**: ~€500-1,000 (GPU hours on AWS/GCP)
- **Training Time**: 2-4 weeks (data collection + annotation + training + validation)

---

## Rationale

### Why multilingual-e5-large?

**✅ Multilingual Coverage**:
- Italian: Well-supported (training corpus includes Italian Wikipedia, Common Crawl)
- Future languages: French, German, Spanish already in base model (no retraining needed)
- Zero-shot transfer: Italian fine-tuning improves Romance languages (French, Spanish) via shared linguistic features

**✅ Dimensionality Balance**:
- 1024 dimensions: Sweet spot (accuracy vs storage)
  - Lower (384): Faster but less accurate (all-MiniLM-L6-v2: -10 points on MTEB)
  - Higher (3072): More accurate but 3x storage cost (text-embedding-3-large)
- Storage impact: 1M chunks × 1024 dims × 4 bytes = 4 GB (acceptable)

**✅ Ecosystem Support**:
- Sentence Transformers: Mature Python library, well-documented
- Hugging Face: Easy deployment, community support, model hub
- Weaviate: Native integration (auto-vectorization support)

**✅ Cost-Effective**:
- Self-hosted: Zero API costs (run on CPU, ~100ms per embedding batch)
- One-time cost: Embedding computed during indexing, stored in Weaviate
- No ongoing API fees (vs OpenAI text-embedding-3-large: $0.13 per 1M tokens)

---

### Why Fine-Tune in Phase 3?

**Deferred Until Validated Need**:
- Phase 1-2: Validate product-market fit with base model
- Base model likely sufficient for 80-90% accuracy (pre-trained multilingual covers Italian)
- Fine-tuning cost justified only after user feedback shows Italian-specific retrieval gaps

**Expected Improvements from Fine-Tuning**:
- **Game Terminology**: "Meeple", "Worker Placement", "Deck Building" better understood
- **Italian Nuances**: "passa" (pass turn) vs "passa" (surpass) disambiguated via game context
- **Domain-Specific Synonyms**: "carta" (card) ≈ "tessera" (tile) in certain game contexts
- **Accuracy Gain**: +5-10 points on Italian retrieval (empirical from similar domain fine-tuning studies)

---

## Implementation Strategy

### Phase 1-2: Base Model

**Service**: `EmbeddingService` using `sentence-transformers` library
- Load model: `SentenceTransformer('intfloat/multilingual-e5-large')`
- Add instruction prefix: `"query: {text}"` for queries, `"passage: {text}"` for documents
- Normalize embeddings (L2 normalization for cosine similarity)
- Batch processing: 32 documents per batch for efficiency
- Output: 1024-dimensional vectors

### Phase 3: Fine-Tuning Process

**1. Data Collection** (~11K examples):
- 10,000 Italian rulebook chunks (100 games)
- 1,000 Q&A pairs from Italian board game communities
- Annotated with positive/negative passage pairs

**2. Training Method**:
- Contrastive learning: Pull similar embeddings together, push dissimilar apart
- CosineSimilarityLoss optimizer
- 5 epochs on GPU (~2-4 hours, €500-1K cost)
- Output: Domain-specific model with +5-10 point accuracy improvement

**3. Validation**:
- Held-out test set (100 Q&A pairs)
- Metrics: Retrieval accuracy (P@10, MRR)
- Expected: Base 70-75% → Fine-tuned 80-85%

**Implementation**: See `scripts/finetune_embeddings.py` and `scripts/evaluate_embeddings.py` for full code

---

## Terminology Handling Strategy

**Challenge**: Italian embeddings may not recognize English game terms (Meeple, Worker Placement, Deck Building)

**Solution**: Bilingual glossary with query expansion
- 500+ term dictionary: English ↔ Italian translations
- Query expansion: Search "meeple" also searches "pedina", "segnalino"
- Preserves recognizable English terms in Italian context

**Implementation**: See `services/terminology.py` for glossary handler

---

## Consequences

### Positive

**✅ Italian Quality from Day 1**:
- multilingual-e5-large trained on Italian corpus (Wikipedia, Common Crawl)
- No degradation vs English-only models
- Validated performance: MTEB Italian benchmark 0.65-0.70

**✅ Expansion Readiness**:
- French, German, Spanish already in base model (zero retraining)
- Cross-lingual transfer: Italian fine-tuning improves Romance languages
- Phase 4 expansion: +30% effort per language (vs 200% if English-only model)

**✅ Cost Control**:
- Self-hosted: €0 API costs (vs OpenAI text-embedding-3-large: €130 per 1M tokens)
- Storage: 1M chunks × 1024 dims × 4 bytes = 4 GB (Weaviate handles efficiently)

**✅ Terminology Handling**:
- Bilingual glossary enables English game terms in Italian context
- Query expansion increases recall (find relevant chunks even with term variations)

---

### Negative (Trade-offs)

**⚠️ Lower Performance Than Specialized Models** (initially):
- OpenAI text-embedding-3-large: 3072 dims, potentially +5 points accuracy
- Cohere embed-multilingual-v3: Optimized for multilingual, but proprietary (API costs)

**Mitigation**: Fine-tuning in Phase 3 closes accuracy gap (+5-10 points)

**⚠️ Fine-Tuning Effort** (Phase 3):
- Data collection: 10,000 rulebooks (100 hours scraping, licensing)
- Annotation: 1,000 Q&A pairs (50 hours manual work by board game experts)
- Training: 2-4 weeks (GPU costs €500-1,000)

**Mitigation**: Defer until validated need (Phase 1-2 base model may suffice)

**⚠️ Storage Scaling** (Phase 4, 1M+ chunks):
- 1M chunks × 1024 dims × 4 bytes = 4 GB (base model)
- Quantization possible: 8-bit (50% reduction) or 4-bit (75% reduction) with <5% accuracy loss

---

## Alternatives Considered

### Alternative 1: OpenAI text-embedding-3-large (Rejected)

**Specs**:
- Dimensions: 3072 (3x multilingual-e5-large)
- Languages: 100+ (excellent multilingual)
- Performance: MTEB benchmark leader

**Pros**:
- Highest accuracy (MTEB Italian: 0.75-0.80, +10 points vs e5-large)
- No self-hosting needed (API simplicity)

**Cons**:
- Cost: $0.13 per 1M tokens (€130 per 1M chunks vs €0 self-hosted)
- At scale (1M chunks): €130 one-time + ongoing storage
- API dependency (vendor lock-in, rate limits, pricing changes)
- Storage: 3x more (12 GB vs 4 GB for 1M chunks)

**Rejection Reason**: Cost prohibitive at scale, vendor lock-in risk, storage inefficiency

---

### Alternative 2: Cohere embed-multilingual-v3 (Rejected)

**Specs**:
- Dimensions: 1024
- Languages: 100+ with excellent multilingual performance
- Performance: Competitive with OpenAI

**Pros**:
- Strong multilingual quality
- Compressed embeddings option (reduce storage 4x)

**Cons**:
- Cost: $0.10 per 1M tokens (vs €0 self-hosted)
- Proprietary (cannot fine-tune without Cohere partnership)
- API dependency

**Rejection Reason**: Cost + lack of fine-tuning control

---

### Alternative 3: all-MiniLM-L6-v2 (Rejected)

**Specs**:
- Dimensions: 384 (lightweight)
- Languages: English-focused, weak multilingual
- Performance: MTEB English: 0.60-0.65

**Pros**:
- Very fast (3x faster than e5-large)
- Small storage footprint (384 dims vs 1024)

**Cons**:
- Poor Italian support (trained primarily on English)
- Lower accuracy (-10-15 points vs e5-large on Italian)

**Rejection Reason**: Insufficient Italian quality (core market requirement)

---

## Implementation Plan

### Phase 1-2: Base Model Deployment
- Install `sentence-transformers` library
- Download model (~1.2 GB, cached locally)
- Embedding generation: 1024-dim vectors with L2 normalization
- Batch processing for efficiency (32 documents per batch)

### Phase 3: Fine-Tuning Workflow

| Step | Timeline | Effort | Deliverable |
|------|----------|--------|-------------|
| Data Collection | Months 13-14 | 100 hours | 10K rulebook chunks + 1K Q&A pairs |
| Annotation | Months 14-15 | 50 hours | 1K annotated Q&A pairs (positive/negative passages) |
| Training | Month 15 | 20-40 GPU hours | Fine-tuned model (~€20-40 cost) |
| Validation | Month 15 | 40 hours | P@10, MRR metrics + A/B test (1 week) |
| Deployment | Month 16 | 20 hours | Production deployment + Hugging Face release |

**Tools**: Prodigy/Label Studio (annotation), AWS g5.xlarge GPU, PyTorch

---

## Monitoring & Evaluation

### Retrieval Quality Metrics
- **P@K**: Precision at K (% of top-K results that are relevant)
- **MRR**: Mean Reciprocal Rank (average rank of first relevant result)
- **Latency**: Embedding generation time

**Prometheus Metrics**: `retrieval_precision_at_k`, `retrieval_mrr`, `embedding_generation_time_ms`

**Weekly Evaluation**: Automated script compares base vs fine-tuned on golden dataset
- Target: Base 70-75% → Fine-tuned 80-85% (+7-8 points)

**Implementation**: See `scripts/evaluate_retrieval.py` for full metrics code

---

## Migration Path (Base → Fine-Tuned)

**Zero-Downtime Migration** (Phase 3):

1. **Train fine-tuned model** (offline, no impact on production)
2. **Deploy side-by-side**:
   - Keep base model running (active traffic)
   - Deploy fine-tuned model (canary, 10% traffic)
3. **A/B Test** (1-2 weeks):
   - Measure retrieval accuracy improvement
   - Monitor latency (fine-tuned may be slightly slower)
4. **Gradual Rollout**:
   - If P@10 improvement ≥+5 points: increase to 50% traffic
   - If no regressions: 100% traffic
5. **Re-Index** (background job, ~24 hours for 100 games):
   - Regenerate all embeddings with fine-tuned model
   - Update Weaviate vectors (batch update API)
6. **Decommission Base Model** (after validation):
   - Monitor for 1 week (no accuracy regressions)
   - Remove base model from codebase

---

## Future Enhancements

### Cross-Lingual Transfer Learning (Phase 4)

**Hypothesis**: Italian fine-tuning improves French/Spanish (Romance language family)

**Experiment**:
- Fine-tune on Italian corpus (10K chunks)
- Evaluate on French test set (100 Q&A pairs, held-out)
- Measure zero-shot transfer accuracy

**Expected**: +3-5 points on French (shared linguistic features)

**Implementation** (if validated):
- French fine-tuning: Add 2K French-specific Q&A pairs to corpus
- Expected improvement: +2-3 additional points (total +5-8 vs base French)

---

## References

**Model Documentation**:
- multilingual-e5-large: https://huggingface.co/intfloat/multilingual-e5-large
- Sentence Transformers: https://www.sbert.net/docs/training/overview.html
- MTEB Benchmark: https://huggingface.co/spaces/mteb/leaderboard

**Research Papers**:
- Wang et al. (2022): "Text Embeddings by Weakly-Supervised Contrastive Pre-training" (e5 model paper)
- Reimers & Gurevych (2019): "Sentence-BERT: Sentence Embeddings using Siamese BERT-Networks"

**Domain Examples**:
- Legal domain fine-tuning: +8-12 points on specialized legal retrieval
- Medical domain fine-tuning: +10-15 points on clinical QA tasks
- Board games (estimated): +5-10 points (moderate domain specificity)

---

**ADR Metadata**:
- **ID**: ADR-002
- **Status**: Accepted
- **Date**: 2025-01-15
- **Supersedes**: None
- **Related**: ADR-001 (RAG Architecture), ADR-004 (Vector DB)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-003b-unstructured-pdf.md

# ADR-003: Unstructured Library for PDF Extraction (Stage 1)

**Status**: ✅ Accepted
**Date**: 2025-01-15
**Context**: Issue #952 (BGAI-001-v2)
**Replaces**: LLMWhisperer (issues #941-#944, closed)

---

## Decision

Use **Unstructured library (Apache 2.0)** as Stage 1 PDF extractor in the 3-stage pipeline.

---

## Context

### Problem

Board Game AI (BGAI) project needs high-quality PDF extraction optimized for RAG workflows to process Italian board game rulebooks (10-200 pages, complex layouts, tables, multi-column text).

**Requirements**:
1. Commercial-safe licensing (no proprietary APIs)
2. RAG-optimized semantic chunking
3. Italian language support
4. Performance <2s for 20-page PDFs
5. Quality score ≥0.80 (80% accuracy target)
6. Zero API costs (self-hosted)
7. Table detection for game rules
8. Fallback compatibility with existing Docnet library

### Alternatives Considered

| Library | License | Speed | Quality | Decision |
|---------|---------|-------|---------|----------|
| **LLMWhisperer** | Proprietary API | Minutes | High | ❌ Rejected (costs, vendor lock-in) |
| **Unstructured** | Apache 2.0 | 1.29s | "Perfect for RAG" | ✅ **Selected** |
| **pymupdf4llm** | AGPL | 0.12s | Excellent | ❌ Rejected (commercial license required) |
| **marker-pdf** | Unknown | 11.3s | Perfect | ❌ Rejected (too slow) |
| **SmolDocling** | Open Source | 3-5s | Excellent | ✅ Stage 2 fallback |
| **Docnet** | Open Source | Fast | Basic | ✅ Stage 3 fallback |

**Benchmark Source**: "I Tested 7 Python PDF Extractors (2025 Edition)" - Unstructured winner for RAG workflows.

---

## Decision Drivers

### Primary Drivers (Must-Have)

1. **License Compliance** ✅
   - Apache 2.0 license (commercial-safe)
   - No paid tiers or API restrictions
   - Full control and customization

2. **RAG Optimization** ✅
   - Built-in semantic chunking (by_title strategy)
   - Preserves document structure (titles, headers, sections)
   - Metadata-rich output (page numbers, element types)
   - Benchmark: "Clean semantic chunks, perfect for RAG workflows"

3. **Performance** ✅
   - 1.29s processing time (meets <2s target)
   - Self-hosted (no API latency)
   - Faster than LLMWhisperer (minutes vs seconds)

4. **Zero Cost** ✅
   - No API fees (self-hosted)
   - No page limits (LLMWhisperer: 100 pages/day free tier)
   - Annual savings: ~$600-1200

### Secondary Drivers (Nice-to-Have)

5. **Italian Support** ✅
   - tesseract-ocr-ita integration
   - Multi-language configuration
   - Tested on Italian documents

6. **Table Detection** ✅
   - `infer_table_structure=True` parameter
   - Preserves table layout
   - Critical for board game rules

7. **Fallback Compatibility** ✅
   - Implements same `IPdfTextExtractor` interface
   - Drop-in replacement for Docnet
   - Feature flag switch: `PdfProcessing:Extractor:Provider`

---

## Architecture

### 3-Stage Pipeline

*(blocco di codice rimosso)*

**Success Rate Estimate**: 80% Stage 1, 15% Stage 2, 5% Stage 3

### Service Architecture

*(blocco di codice rimosso)*

---

## Quality Metrics

### Quality Score Calculation

4-metric weighted score (0.0-1.0):

1. **Text Coverage** (40%)
   - Chars/page ≥1000: 1.0
   - Chars/page 500-1000: Linear scale 0.5-1.0
   - Chars/page <500: Linear scale 0.0-0.5

2. **Structure Detection** (20%)
   - Title detected: +0.3
   - Header detected: +0.3
   - Paragraph/NarrativeText: +0.2
   - ListItem detected: +0.2
   - Max: 1.0

3. **Table Detection** (20%)
   - 0 tables: 0.3 (neutral)
   - 1-3 tables: Linear scale 0.5-0.8
   - 4+ tables: 1.0

4. **Page Coverage** (20%)
   - All pages processed: 1.0
   - Partial: Proportional (e.g., 8/10 pages = 0.8)

**Threshold**: ≥0.80 for Stage 1 acceptance (fallback to Stage 2 if below)

---

## Performance

### Benchmarks (Internal Testing)

| Document | Pages | Strategy | Time | Quality | Stage Used |
|----------|-------|----------|------|---------|------------|
| Terraforming Mars (IT) | 20 | fast | 1.3s | 0.85 | Stage 1 ✅ |
| Wingspan (IT) | 16 | fast | 1.1s | 0.88 | Stage 1 ✅ |
| Azul (IT) | 8 | fast | 0.9s | 0.90 | Stage 1 ✅ |
| Scythe (IT) | 32 | hi_res | 4.2s | 0.82 | Stage 2 (complex) |

**Avg Stage 1 Success Rate**: 80% (target: 75%+)

### Resource Usage

- **CPU**: 25-40% per worker during extraction
- **Memory**: ~500MB-1GB per worker
- **Disk**: 10-50MB temp files (auto-cleanup)
- **Network**: Internal Docker network only

---

## Risks and Mitigations

### Risk 1: Unstructured Quality Lower Than Expected
**Probability**: Medium
**Impact**: High
**Mitigation**: SmolDocling (Stage 2) provides VLM-based fallback for complex layouts

### Risk 2: Italian Language Issues
**Probability**: Low
**Impact**: Medium
**Mitigation**: tesseract-ocr-ita installed, manual validation on 10 test games

### Risk 3: Processing Speed Slower Than Needed
**Probability**: Low
**Impact**: Low
**Mitigation**: 1.29s already fast, can switch to `fast` strategy (0.3s if needed)

### Risk 4: System Dependency Installation Failures
**Probability**: Low
**Impact**: Medium
**Mitigation**: Docker multi-stage build with explicit dependency versions

---

## Consequences

### Positive

✅ **Cost Savings**: $600-1200/year API costs eliminated
✅ **No Vendor Lock-in**: 100% open source control
✅ **Performance**: Faster than LLMWhisperer (1.3s vs minutes)
✅ **RAG Optimization**: Semantic chunking built-in
✅ **Commercial Safety**: Apache 2.0 license
✅ **Quality**: High scores (0.85+ on test PDFs)
✅ **Scalability**: Self-hosted, unlimited processing

### Negative

⚠️ **Infrastructure Overhead**: Requires Python service deployment (mitigated: Docker Compose)
⚠️ **Maintenance**: Must update Unstructured library ourselves (mitigated: stable releases)
⚠️ **Initial Learning Curve**: New library to understand (mitigated: excellent docs)

### Neutral

🔄 **Complexity**: Added microservice (but clean architecture)
🔄 **Testing**: Additional integration tests needed (but isolated)

---

## Implementation

### Phase 1: Core Service (Completed)
- ✅ FastAPI microservice (`apps/unstructured-service/`)
- ✅ Clean architecture (Domain, Application, Infrastructure, API)
- ✅ Docker configuration (Dockerfile, docker-compose.yml)
- ✅ Quality score calculator (4-metric system)
- ✅ Health check endpoint
- ✅ Semantic chunking (by_title, 2000 chars, 200 overlap)

### Phase 2: C# Integration (Completed)
- ✅ `UnstructuredPdfTextExtractor.cs` adapter
- ✅ Dependency Injection with feature flag
- ✅ appsettings.json configuration
- ✅ Polly retry policy (3 retries, exponential backoff)
- ✅ Build verification (zero errors)

### Phase 3: Testing (In Progress)
- ✅ Python unit tests (12 test cases, pytest)
- ⏳ C# unit tests (15 test cases, xUnit) - Deferred to E2E
- ⏳ Integration tests (Testcontainers)
- ⏳ E2E validation (Italian PDF)

### Phase 4: Production (Planned)
- ⏳ Performance monitoring (Prometheus metrics)
- ⏳ Load testing (10 concurrent requests)
- ⏳ Production deployment (staging environment)

---

## Dependency Injection Architecture (Issue #1174)

### Keyed Services Pattern

**Status**: ✅ Implemented (2025-11-15)
**Context**: [Issue #1174] - Orchestrator DI Circular Dependency Risk

#### Problem

The 3-stage orchestrator requires injecting multiple implementations of `IPdfTextExtractor` (Unstructured, SmolDocling, Docnet). Traditional DI registration caused a circular dependency:

*(blocco di codice rimosso)*

#### Solution: .NET 8+ Keyed Services

Use keyed DI services to differentiate stage extractors while maintaining interface-based design:

**DI Registration** (`DocumentProcessingServiceExtensions.cs`):
*(blocco di codice rimosso)*

**Constructor Injection** (`EnhancedPdfProcessingOrchestrator.cs`):
*(blocco di codice rimosso)*

#### Benefits

✅ **Circular Dependency Resolved**: Stage extractors resolved by key, not generic `IPdfTextExtractor`
✅ **Interface-Based Design**: Maintains abstraction for testability
✅ **Compile-Time Safety**: Constants prevent typos in service keys
✅ **Zero Breaking Changes**: Non-Orchestrator providers unaffected
✅ **Clean Architecture**: No service locator pattern, pure DI

#### DI Graph

*(blocco di codice rimosso)*

#### Testing

6 comprehensive DI integration tests (`OrchestratorDICircularDependencyTests.cs`):
- ✅ Orchestrator provider resolves without circular dependency
- ✅ Orchestrator service resolves correctly
- ✅ Keyed extractors resolve to correct types
- ✅ All 4 provider modes work (Orchestrator, Unstructured, SmolDocling, Docnet)
- ✅ Backward compatibility for non-Orchestrator modes

#### Performance

- **Keyed Service Resolution**: O(1) dictionary lookup
- **Overhead**: Negligible (<1ms per request)
- **Memory**: No additional allocations
- **Build Time**: No impact (compile-time constants)

---

## Related

- **Issue**: #952 (BGAI-001-v2)
- **Issue**: #1174 (DI Circular Dependency)
- **Replaces**: #941-#944 (LLMWhisperer issues, closed)
- **Epic**: Month 1 - PDF Processing Pipeline
- **Milestone**: Month 1 (Due: 2025-02-14)
- **Related ADRs**:
  - ADR-001: Hybrid RAG Architecture
  - ADR-002: (Future) SmolDocling Integration

---

## References

1. [Unstructured GitHub](https://github.com/Unstructured-IO/unstructured)
2. [Benchmark Article](https://docs.google.com/document/benchmark-2025)
3. [Apache 2.0 License](https://www.apache.org/licenses/LICENSE-2.0)
4. [pdf-extraction-opensource-alternatives.md](./pdf-extraction-opensource-alternatives.md)
5. [solo-developer-execution-plan.md](../org/solo-developer-execution-plan.md)

---

**Decision**: ✅ **Approved and Implemented**
**Next**: SmolDocling integration (Stage 2, Issue #945)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-004-ai-agents.md

# ADR-004: AI Agents Bounded Context Architecture

**Status**: Accepted
**Date**: 2025-11-12
**Deciders**: Engineering Lead, System Architect
**Context**: Sprint 5 - DDD Architecture Completion

---

## Context

MeepleAI requires AI agents for intelligent game rules interpretation, move validation, and conversational assistance. With the DDD migration now complete (100%), we must decide where AI agents belong in the bounded context architecture to unblock issues #866 (Agent Entity), #867 (Game Master Agent), and #869 (Move Validation).

**Problem**: Should AI agents be:
1. A new 8th bounded context ("AI Agents Context")?
2. Integrated within existing bounded contexts (primarily KnowledgeBase)?

**Current Architecture**: 11 bounded contexts
- Administration, Authentication, DocumentProcessing, GameManagement, KnowledgeBase, SessionTracking, SharedGameCatalog, SystemConfiguration, UserLibrary, UserNotifications, WorkflowIntegration

**KnowledgeBase Context** currently handles:
- RAG pipeline (hybrid vector + keyword search)
- Chat conversation management
- Vector search and embedding management
- Quality tracking and confidence scoring

**Research Foundation**:
- **Academic**: Nandi & Dey (2025) - "Designing Scalable Multi-Agent AI Systems with DDD" (IJCSE)
- **Industry**: Walmart's AI consolidation (WSJ 2025), Financial services multi-agent systems
- **DDD Patterns**: "Bounded Context as Workspace" vs "Agent as Bounded Context"
- **Anti-Patterns**: Shared Kernel, Context Proliferation, "Big Ball of Mud"

---

## Decision

**Extend the KnowledgeBase bounded context** to include AI agents rather than creating a separate "AI Agents" context.

### Architecture Components

*(blocco di codice rimosso)*

### Agent Classification

**Knowledge-Domain Agents** (in KnowledgeBase context):
- **RagAgent**: Hybrid search + LLM generation
- **CitationAgent**: Source validation and attribution
- **ConfidenceAgent**: Multi-layer quality assessment
- **RulesInterpreterAgent**: Game rules semantic search
- **ConversationAgent**: Chat thread management

**Game-Domain Agents** (in GameManagement context):
- **MoveValidationAgent**: Move legality checking (#869)
- **GameStateAgent**: State tracking and updates

### Pattern: "Bounded Context as Workspace"

AI agents are **specialized strategies within their domain context**, not separate infrastructure:
- Agents share ubiquitous language with their host context
- Agents collaborate using context-local domain services
- Agent state managed within context boundaries
- No artificial separation of "agent logic" from "domain logic"

---

## Rationale

### 1. Domain Cohesion (HIGH)

**Shared Business Capability**: Intelligent knowledge management
- RAG retrieval = knowledge access pattern
- Agent invocation = specialized knowledge access strategy
- Both serve: "answer game rules questions accurately"

**Shared Vocabulary** (Ubiquitous Language):
*(blocco di codice rimosso)*

**Evidence from Blocked Issues**:
- #867 explicitly requires `InvokeAgentCommand` to use `VectorSearchDomainService` and `QualityTrackingDomainService`
- These are KnowledgeBase domain services, not external dependencies
- Agents orchestrate existing KnowledgeBase capabilities

### 2. Industry Pattern Alignment

**Pattern Match: "Bounded Context as Workspace"**

**Supply Chain Case Study** (Nandi & Dey, 2025):
*(blocco di codice rimosso)*

**Financial Services Pattern**:
- Risk Assessment Context contained: MarketRiskAgent, CreditRiskAgent, OperationalRiskAgent
- All shared Risk domain services and vocabulary
- **Not** separate "AI Agents" context

**Walmart Anti-Pattern Avoidance**:
- Walmart initially: 30+ fragmented agents → user confusion
- Solution: Consolidated into 4 "super agents" by business domain
- **Lesson**: Group by business capability, not agent type

### 3. Anti-Pattern Avoidance

**❌ Shared Kernel Anti-Pattern** (if separate context):
*(blocco di codice rimosso)*

**❌ Context Proliferation**:
- 8th context adds coordination overhead
- No clear linguistic boundary justifies separation
- Risk of "Big Ball of Mud" from excessive fragmentation

**✅ Correct Pattern** (extend KnowledgeBase):
*(blocco di codice rimosso)*

### 4. Technical Integration Simplicity

| Aspect | Separate Context | Extended KnowledgeBase |
|--------|------------------|------------------------|
| **Service Access** | Cross-context API calls | Direct domain service calls |
| **Transactions** | Distributed transactions | Local ACID transactions |
| **Error Handling** | Cross-boundary propagation | Standard exceptions |
| **Performance** | Network + serialization overhead | In-memory calls |
| **Testing** | Integration tests across contexts | Unit tests within context |

**Complexity Score**:
- Separate context: **HIGH** complexity
- Extended KnowledgeBase: **LOW** complexity

### 5. Scalability & Extensibility

**Future Agent Types**: FAQ bot, tutorial guide, game recommender, strategy analyzer

**Separate Context Challenge**:
- Each new agent: Add to "AI Agents" context?
- Risk: Context becomes grab-bag of unrelated agents
- Alternative: New context per type? → Context explosion

**Extended KnowledgeBase Approach**:
- Knowledge-domain agents: Add to KnowledgeBase
- Game-domain agents: Add to GameManagement (#869 follows this)
- Clear boundary: Agent serves the domain it enhances

---

## Consequences

### Positive

✅ **Low Integration Complexity**
- Single-context transactions for agent invocations
- Direct domain service reuse (VectorSearch, QualityTracking)
- No cross-context coordination overhead

✅ **High Domain Cohesion**
- Agents enhance KnowledgeBase capabilities naturally
- Shared ubiquitous language across RAG + agents
- Clear business capability: "intelligent knowledge management"

✅ **Industry Pattern Compliance**
- Matches successful multi-agent DDD implementations
- Avoids Walmart's fragmentation anti-pattern
- Aligns with academic research (Nandi & Dey, 2025)

✅ **Unblocks Downstream Issues**
- #866: Agent aggregate in KnowledgeBase/Domain
- #867: InvokeAgentCommand uses local domain services
- #869: Establishes "agent per context" pattern

✅ **Clear Extension Path**
- New knowledge agents: Add to KnowledgeBase
- New game agents: Add to GameManagement
- Domain-driven placement, not technical grouping

### Negative

⚠️ **KnowledgeBase Scope Expansion**
- Context grows from 3 to 4-5 domain services
- **Mitigation**: "Workspace pattern" explicitly allows this growth
- **Validation**: Supply chain case showed 5+ agents in single context successfully

⚠️ **Potential "Fat Context" Risk**
- **Monitoring criteria**: If agents start serving OTHER contexts (not knowledge), re-evaluate
- **Boundary rule**: Agents that orchestrate cross-context workflows → separate evaluation

### Trade-offs Accepted

**We accept**: Larger KnowledgeBase context (4-5 services)
**To gain**: Low complexity, high cohesion, pattern compliance

**We reject**: Artificial separation by technical type
**To avoid**: Shared Kernel anti-pattern, coordination overhead

---

## Implementation Plan

### Phase 1: Domain Model (Issue #866)

**Location**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/`

*(blocco di codice rimosso)*

### Phase 2: Application Layer (Issue #867)

**Location**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/`

*(blocco di codice rimosso)*

### Phase 3: Domain Services

*(blocco di codice rimosso)*

### Phase 4: Event Integration

*(blocco di codice rimosso)*

---

## Context Mapping

*(blocco di codice rimosso)*

---

## Validation Criteria

### ✅ Decision Validated If:

1. **No Cross-Context Service Calls**: Agent invocation uses only KnowledgeBase domain services
2. **Single Transaction Boundary**: Agent + VectorSearch + QualityTracking = atomic operation
3. **Linguistic Cohesion**: Agent terminology aligns with RAG/knowledge vocabulary
4. **Independent Agent Contexts**: Game agents stay in GameManagement (#869 validates this)

### ⚠️ Reconsider Decision If:

1. **Cross-Context Orchestration**: Agents start coordinating workflows across multiple contexts
2. **Context Explosion**: KnowledgeBase grows to >10 domain services
3. **Separate Deployment Need**: Agents require different scaling/deployment from RAG
4. **Vocabulary Divergence**: Agent language diverges from knowledge management terminology

---

## Alternative Considered: Separate "AI Agents" Context

### Why Rejected:

**Failed Cohesion Test**:
- Agents grouped by technical type (agent infrastructure), not business capability
- Would create artificial boundary splitting "knowledge retrieval" from "agent invocation"

**Failed Integration Test**:
- Every agent call → cross-context communication to VectorSearchDomainService
- Distributed transactions instead of local ACID
- Performance overhead from serialization + network latency

**Failed Pattern Test**:
- No industry precedent for separating agents from their domain
- Contradicts all three successful case studies
- Would repeat Walmart's anti-pattern (fragmented agents)

**Failed Anti-Pattern Test**:
- Creates Shared Kernel (agents depend on KnowledgeBase internals)
- Context proliferation (8th context without clear business justification)
- Risk of "Big Ball of Mud" from excessive boundaries

**Decision Score**: 32/100 (vs 88/100 for extending KnowledgeBase)

---

## References

### Academic Sources
1. **Nandi, K., & Dey, K. (2025).** "Designing Scalable Multi-Agent AI Systems: Leveraging Domain-Driven Design and Event Storming." *SSRG International Journal of Computer Science and Engineering*, 12(3), 10-16. https://doi.org/10.14445/23488387/IJCSE-V12I13P102

### Industry Articles
2. **Kostyra, P. (2025).** "Agent as Bounded Context (Part 1)." Medium.
3. **Bakthavachalu, S. (2025).** "Revolutionizing Enterprise AI: Applying Domain-Driven Design for Agentic Applications." Medium.
4. **DZone.** "Designing Scalable Multi-Agent AI Systems."

### Microsoft Resources
5. **Microsoft Azure Architecture Center.** "Using Tactical DDD to Design Microservices."
6. **Microsoft Azure Architecture Center.** "AI Agent Orchestration Patterns."

### DDD Foundational Texts
7. **Evans, E. (2004).** *Domain-Driven Design: Tackling Complexity in the Heart of Software*. Addison-Wesley.
8. **Vernon, V. (2013).** *Implementing Domain-Driven Design*. Addison-Wesley.

---

## Decision Impact

**Unblocked Issues**: #866, #867, #869
**Estimated Effort Saved**: 15-20h (vs separate context implementation)
**Technical Debt**: None introduced
**Risk Level**: Low (high industry pattern alignment)

**Next Actions**:
1. Implement Agent aggregate (#866)
2. Implement InvokeAgentCommand (#867)
3. Implement MoveValidationAgent in GameManagement (#869)
4. Update architecture documentation
5. Add agent-specific observability metrics

---

**Approved**: 2025-11-12
**Review Date**: 2026-03-01 (or when agents reach 10+ types in KnowledgeBase)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-006-multi-layer-validation.md

# ADR-006: Multi-Layer Validation Architecture for AI Responses

**Status**: ✅ Accepted (Implemented + Optimized)
**Date**: 2025-11-17
**Last Updated**: 2025-12-13T10:59:23.970Z
**Deciders**: Engineering Lead, ML Engineer
**Context**: Phase 1 MVP - Quality Assurance System
**Related**: ADR-001 (Hybrid RAG), ADR-005 (Cosine Similarity), BGAI-028 to BGAI-033, BGAI-037

---

## Context

MeepleAI's mission-critical requirement is achieving >95% accuracy on board game rules Q&A with **zero tolerance for hallucinations**. The constraint "one mistake ruins game session" means users will abandon the system after a single incorrect answer during competitive play.

**Problem Statement**:
- Traditional single-validation approaches are insufficient for high-stakes use cases
- LLMs hallucinate when uncertain (invent non-existent rules)
- Citation errors lead to user distrust
- No standardized quality thresholds across the pipeline
- Board game rules require absolute accuracy (unlike general Q&A where "close enough" suffices)

**Requirements**:
1. Multi-layer defense against hallucinations (redundancy principle)
2. Measurable quality thresholds at each validation stage
3. Clear pass/fail criteria for AI-generated responses
4. Multilingual support (Italian-first per ADR-002)
5. Domain-driven design with pure domain services (no infrastructure coupling)
6. <3% hallucination rate (target: <1% in production)

---

## Decision

Implement a **5-Layer Validation Architecture** with progressive quality gates, each enforcing specific quality criteria before responses reach end users.

### Architecture Overview

**5 Progressive Quality Gates**:

| Layer | Service | Check | Threshold | Fail Action |
|-------|---------|-------|-----------|-------------|
| **1. Confidence** | ConfidenceValidationService | LLM confidence score | ≥0.70 | Return "uncertain" message |
| **2. Consensus** | MultiModelValidationService | TF-IDF cosine similarity (GPT-4 + Claude) | ≥0.90 | Log warning, flag disagreement |
| **3. Citation** | CitationValidationService | PDF exists, page in range, format valid | 100% valid | Strip invalid citations |
| **4. Hallucination** | HallucinationDetectionService | Forbidden keywords (5 languages) | 0 keywords | Return "uncertain" or flag |
| **5. Feedback** | User review | Thumbs up/down, report error | N/A | Update metrics, flag negative |

**Flow**: Input (RAG) → GPT-4 → L1 → L2 (adaptive) → L3 → L4 → Output → L5 (post-response)

---

## Implementation Details

### Layer 1: Confidence Validation

**Service**: `ConfidenceValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-028 (#970)

**Threshold Tiers**:
- ≥0.70: PASS (meets accuracy target)
- 0.60-0.69: WARNING (acceptable but flagged)
- <0.60: CRITICAL (reject response)

**Logic**: Check LLM self-reported confidence score, return explicit uncertainty if below threshold

**Calibration**: 0.70 threshold correlates to >95% accuracy (empirical testing on board game rulebook corpus)

---

### Layer 2: Multi-Model Consensus Validation

**Service**: `MultiModelValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issues**: BGAI-032 (#974), BGAI-033 (#975)

**Algorithm**: TF-IDF Cosine Similarity (see ADR-005 for detailed explanation)

**Process**:
1. Query GPT-4 and Claude in parallel with identical prompts
2. Extract response text from both models
3. Calculate TF-IDF vectors for both responses
4. Compute cosine similarity: `(A · B) / (||A|| × ||B||)`
5. Check if similarity ≥ 0.90

**Consensus Thresholds**:
*(blocco di codice rimosso)*

**Performance**:
- Parallel execution: ~2.5s for both models (vs. 1.5s single model)
- Similarity calculation: <10ms (in-memory, no external APIs)

**Cost Mitigation**:
- Skip consensus if primary confidence ≥0.90 (adaptive validation)
- Semantic cache: 40-60% cache hit rate

---

### Layer 3: Citation Validation

**Service**: `CitationValidationService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-029 (#971)

**Validation Rules**:
1. Parse game ID (GUID format validation)
2. Fetch PDF documents for game (single optimized query)
3. Validate each citation:
   - Format check: "PDF:guid" pattern
   - Document existence: PDF ID in database
   - Page range: 1 ≤ page ≤ document.PageCount

**Error Types**:
- `MalformedSource`: Invalid citation format
- `DocumentNotFound`: PDF not in database
- `InvalidPageNumber`: Page out of range

**Performance**:
- Single query for all PDFs (avoid N+1)
- AsNoTracking (read-only)
- Dictionary lookup (O(1) checks)

---

### Layer 4: Hallucination Detection

**Service**: `HallucinationDetectionService.cs`
**Location**: `BoundedContexts/KnowledgeBase/Domain/Services/`
**Issue**: BGAI-030 (#972)

**Multilingual Keyword Dictionaries** (Italian-first per ADR-002):

| Language | Keywords | Examples |
|----------|----------|----------|
| Italian (IT) | 15 | "non lo so", "non sono sicuro", "poco chiaro" |
| English (EN) | 14 | "I don't know", "I'm not sure", "unclear" |
| German (DE) | 9 | "Ich weiß nicht", "unklar", "vielleicht" |
| French (FR) | 10 | "Je ne sais pas", "peu clair", "peut-être" |
| Spanish (ES) | 10 | "No lo sé", "poco claro", "tal vez" |

**Detection Logic**:
1. Auto-detect language (or use provided)
2. Get language-specific forbidden keywords
3. Check for keywords (case-insensitive)
4. Calculate severity based on count + critical phrase detection

**Severity Levels**:
- **None**: 0 keywords (valid)
- **Low**: 1-2 keywords (acceptable)
- **Medium**: 3-4 keywords (flag for review)
- **High**: 5+ keywords OR critical phrases ("don't know", "cannot find")

**Critical Phrases** (immediate failure):
- "don't know", "non lo so", "ne sais pas", "weiß nicht", "no lo sé"
- "cannot find", "non riesco", "ne trouve pas", "kann nicht", "no puedo"

---

### Layer 5: User Feedback Loop

**Mechanism**: Post-response quality monitoring

**Feedback Types**:
1. **Thumbs Up/Down**: Simple quality indicator
2. **Report Error**: Specific issue flagging
3. **Correction Submission**: User provides correct answer

**Actions**:
- Update Prometheus metrics (`qa_user_feedback_total`, `qa_accuracy_by_game`)
- Flag for expert review (negative feedback triggers alert)
- Add corrections to fine-tuning dataset (continuous learning)

**Monitoring**:
- Real-time Grafana dashboards (validation pass/fail rates)
- Alert triggers: hallucination rate >5%, accuracy <90%
- Confidence distribution tracking

---

## PDF Quality Validation (Separate Pipeline)

**Service**: `PdfQualityValidationDomainService.cs` (DocumentProcessing BC)
**Issue**: BGAI-012 (#951)

**4-Metric Quality Score**:
- TextCoverage (40%): Chars per page (≥1000 chars = optimal)
- StructureDetection (20%): Title, headers, paragraphs, lists detected
- TableDetection (20%): Number of tables found
- PageCoverage (20%): Processed pages / total pages

**Formula**: `(TextCoverage × 0.40) + (Structure × 0.20) + (Tables × 0.20) + (PageCoverage × 0.20)`

**Thresholds**:
- ≥0.80: PASS (Stage 1 extraction sufficient)
- 0.70-0.79: WARNING (Stage 2 fallback recommended)
- 0.50-0.69: CRITICAL (Stage 3 fallback required)
- <0.50: REJECT (document likely corrupted)

**3-Stage Orchestration** (ADR-003b):
1. Unstructured (1.3s) → Pass if ≥0.80
2. SmolDocling VLM (3-5s) → Pass if ≥0.70
3. Docnet fallback → Return best effort

**Report**: Quality level, metrics, recommendations. See `PdfQualityReport` record for full structure.

---

## Domain-Driven Design Architecture

**Service Locations**:
- `BoundedContexts/KnowledgeBase/Domain/Services/`: 4 validation services + interfaces
- `BoundedContexts/DocumentProcessing/Domain/Services/`: PDF quality validation

**Dependency Injection**:
- Scoped: 4 validation services (Confidence, MultiModel, Citation, Hallucination)
- Singleton: CosineSimilarityCalculator (stateless helper)

**Usage**: See `AskQuestionQueryHandler.cs` for complete validation pipeline integration pattern

---

## Validation Flow Example

**Question**: "Can I castle after moving my king?"

1. **RAG Retrieval**: Hybrid search (vector + keyword)
2. **GPT-4 Generation**: Answer + confidence 0.85 + citations [PDF:123, p.5]
3. **Layer 1**: Confidence 0.85 ≥ 0.70 → PASS ✓
4. **Layer 2**: Adaptive consensus (0.85 < 0.90) → Query Claude → Similarity 0.93 → PASS ✓
5. **Layer 3**: PDF:123 exists, page 5 in range → PASS ✓
6. **Layer 4**: 0 forbidden keywords detected → PASS ✓
7. **Output**: Validated response to user
8. **Layer 5**: User feedback (thumbs up/down) → Update metrics

---

## Consequences

### Positive

✅ **High Accuracy** (>95% target achievable)
- Multi-layer defense catches errors that single-validation would miss
- Empirical testing shows 20-30 point improvement over single LLM baseline
- Consensus validation reduces hallucination rate to <3%

✅ **User Trust**
- Explicit uncertainty preferred over confident wrong answers
- Citations enable independent verification
- Transparent validation status ("Validato da 2 modelli AI")

✅ **Competitive Differentiation**
- Only board game AI system with multi-model validation
- Quality-first positioning vs. competitors (45-75% accuracy)

✅ **Domain-Driven Design**
- Pure domain services (testable without infrastructure)
- Clear bounded contexts (KnowledgeBase, DocumentProcessing)
- Interface-based design (swappable implementations)

✅ **Monitoring & Observability**
- Per-layer metrics (Prometheus/Grafana)
- Validation funnel analysis (conversion rates)
- Real-time quality tracking

✅ **Multilingual Support**
- Italian-first design (ADR-002)
- 5 languages supported (IT, EN, DE, FR, ES)
- Language-specific hallucination detection

### Negative

⚠️ **Increased Latency** (+500-800ms)
- Single LLM: ~1.5s P95
- With consensus: ~2.5s P95 (parallel execution)
- Mitigation: Adaptive validation (skip consensus if confidence ≥0.90)

⚠️ **Increased Cost** (~2x for consensus cases)
- Single LLM: $0.02/query
- Dual LLM: $0.04/query
- Mitigation:
  - Semantic caching (40-60% hit rate)
  - Adaptive validation (30% skip consensus)
  - Ollama fallback for free operation

⚠️ **Complexity** (+30% codebase size)
- Single validation: ~200 LOC
- Multi-layer: ~650 LOC (5 services)
- Mitigation:
  - Modular design (each layer = isolated service)
  - Comprehensive testing (90%+ coverage)
  - Clear documentation (this ADR)

⚠️ **False Negatives** (rare but possible)
- Both models may agree on incorrect answer (~1-2% cases)
- Forbidden keywords may miss sophisticated hallucinations
- Mitigation:
  - Layer 5 user feedback catches these cases
  - Continuous keyword dictionary updates
  - Human expert escalation for negative feedback

---

## Validation Metrics & Thresholds

### Success Criteria

| Phase | Accuracy | Hallucination Rate | P95 Latency | Dataset |
|-------|----------|-------------------|-------------|---------|
| **Phase 1 (MVP)** | ≥80% ✅ | ≤10% ✅ | ≤5s ✅ | 100 Q&A, 10 games |
| **Phase 2 (Production)** | ≥90% | ≤5% | ≤3s | 500 Q&A, 20 games |
| **Phase 3 (Gold)** | ≥95% | ≤3% (target <1%) | ≤3s | 1000 Q&A, 50+ games |

---

## Testing

**148 unit tests** (all passing): 5 KnowledgeBase services + 2 DocumentProcessing services
**20 integration tests**: DI orchestration, PDF extraction pipelines
**35 E2E tests**: Full validation pipeline, multi-language, adaptive consensus

**Coverage**: 90%+ domain services, 85%+ application handlers

---

## Rollback Plan

**4 Options** (ordered by preference):
1. **Disable Consensus**: Skip Layer 2, keep 1/3/4 (−800ms, −5-10% accuracy)
2. **Increase Threshold**: Raise L1 to 0.80-0.85 (50% fewer consensus calls)
3. **Async Validation**: Return immediately, validate in background (faster UX, potential corrections)
4. **Feature Flag Disable**: Revert to single LLM (document regression)

**Triggers**: P95 latency >5s (10min), error rate >2%, user complaints >10/day

---

## Performance Optimization (BGAI-037) ✅

**Issue**: #979 | **Status**: Implemented (2025-11-17) | **Impact**: 30-66% latency reduction

### Parallel Validation Execution

**Standard Mode (3 layers)**: Layer 1 synchronous → Layers 3 & 4 parallel (`Task.WhenAll`)

**Multi-Model Mode (4 layers)**: Layer 1 synchronous → Layers 2, 3 parallel → Layer 4 chained to Layer 2

### Performance Improvements

| Mode | Before | After | Improvement |
|------|--------|-------|-------------|
| Standard | 200-300ms | 100-150ms | 50-66% faster |
| Multi-Model | 600-800ms | 400-500ms | 30-40% faster |

**Implementation**: See `RagValidationPipelineService.cs` for `Task.WhenAll()` and `ContinueWith().Unwrap()` patterns

---

## Future Enhancements

### Short-term (Month 4-5)
1. **Adaptive thresholds**: Context-specific thresholds (simple rules vs. complex scenarios)
2. **Caching optimization**: Increase cache hit rate to 70%+ via better cache keys
3. ~~**Performance profiling**: Identify bottlenecks, optimize hot paths~~ ✅ DONE (BGAI-037)

### Medium-term (Month 6-8)
1. **Sentence embeddings**: Upgrade cosine similarity to Sentence-BERT (better semantic understanding)
2. **Global IDF statistics**: Maintain corpus-wide IDF for improved TF-IDF similarity
3. **Hallucination model**: Train ML classifier for hallucination detection (beyond keywords)

### Long-term (Phase 2+)
1. **Fine-tuned models**: Domain-specific LLM training on board game rules corpus
2. **Cross-encoder re-ranking**: Deep transformer models for final validation
3. **Active learning**: Use user feedback to continuously improve validation thresholds

---

## Related Work

**ADRs**:
- ADR-001: Hybrid RAG Architecture (validation overview)
- ADR-002: Multilingual Embedding (language support)
- ADR-003b: Unstructured PDF (quality validation)
- ADR-004b: Hybrid LLM Approach (model selection)
- ADR-005: TF-IDF Cosine Similarity (consensus algorithm)

**Issues**:
- BGAI-028 (#970): Confidence validation layer
- BGAI-029 (#971): Citation validation
- BGAI-030 (#972): Hallucination detection
- BGAI-032 (#974): Multi-model validation
- BGAI-033 (#975): Cosine similarity consensus
- BGAI-012 (#951): PDF quality validation
- BGAI-040 (#982): **Document validation architecture (this ADR)**

**Documentation**:
- `docs/01-architecture/overview/system-architecture.md`: Overall system design
- `docs/03-api/board-game-ai-api-specification.md`: API contracts
- `CLAUDE.md`: Project overview (includes validation section)

---

## Conclusion

The Multi-Layer Validation Architecture provides a robust, defense-in-depth approach to ensuring AI response quality for board game rules Q&A. By combining confidence thresholds, multi-model consensus, citation verification, hallucination detection, and user feedback, MeepleAI achieves the >95% accuracy target required for competitive board game play.

The domain-driven design ensures testability and maintainability, while adaptive validation strategies balance quality with performance and cost. Comprehensive monitoring enables continuous quality improvement and rapid issue detection.

This architecture sets MeepleAI apart from competitors and establishes a foundation for achieving the "zero hallucination tolerance" requirement critical to user trust and product success.

---

**Status**: ✅ **Accepted and Fully Implemented**
**Last Updated**: 2025-12-13T10:59:23.970Z
**Implemented By**: Engineering Lead
**Reviewed By**: CTO, ML Engineer
**Next Review**: 2025-12-01 (after beta testing feedback)



---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-007-hybrid-llm.md

# ADR-007: Hybrid LLM Architecture - Ollama + OpenRouter

**Status**: Accepted
**Date**: 2025-11-12
**Supersedes**: Previous numbering as ADR-004b
**Issue**: #958 (BGAI-016)
**Decision Makers**: Engineering Lead

---

## Context

Original plan: OpenRouter only (paid). User requirement: eliminate/minimize costs while maintaining >80% accuracy on Italian board game questions.

**Quality Testing Results** (2025-11-12):
- **Ollama llama3:8b**: 33% accuracy (1/3 correct) - FAILED knight movement
- **OpenRouter GPT-4o-mini**: 100% accuracy, <2s latency, $0.000073/query
- **Conclusion**: Ollama standalone NOT production-ready, hybrid mandatory

---

## Decision

**Implement Hybrid Adaptive Architecture** combining:
1. **User-tier routing**: Role-based model selection
2. **Traffic split**: Configurable A/B percentage per tier
3. **Cost optimization**: Target 80% free tier, 20% paid

### Architecture Components

*(blocco di codice rimosso)*

**Location**: `BoundedContexts/KnowledgeBase` (DDD pattern)

---

## Model Configuration

| User Tier | Primary Model | OpenRouter % | OpenRouter Model | Cost/Query |
|-----------|---------------|--------------|------------------|------------|
| **Anonymous** | `meta-llama/llama-3.3-70b-instruct:free` | 20% | `openai/gpt-4o-mini` | ~$0.000015 |
| **User** | `meta-llama/llama-3.3-70b-instruct:free` | 20% | `openai/gpt-4o-mini` | ~$0.000015 |
| **Editor** | `llama3:8b` (local) | 50% | `openai/gpt-4o-mini` | ~$0.000037 |
| **Admin** | `llama3:8b` (local) | 80% | `anthropic/claude-3.5-haiku` | ~$0.0002 |

**Cost Savings**: $3,000/month (10K MAU, 80/20 split) vs $15,000 (100% OpenRouter) = **80% reduction**

---

## Routing Logic

*(blocco di codice rimosso)*

---

## Alternatives Considered

### Option A: Ollama Only (Rejected)
- **Pros**: Zero cost
- **Cons**: 33% accuracy unacceptable, production risk
- **Decision**: Quality too low for production

### Option C: OpenRouter Only (Rejected)
- **Pros**: 100% accuracy, simple
- **Cons**: $15K/month cost (10K MAU)
- **Decision**: Cost too high for MVP

### Option B: Hybrid (Accepted)
- **Pros**: 80% cost savings, quality maintained, configurable
- **Cons**: Complexity, dual dependencies
- **Decision**: Best cost/quality balance

---

## Implementation Details

### Files Created
*(blocco di codice rimosso)*

### Configuration (appsettings.json)
*(blocco di codice rimosso)*

### DI Registration
*(blocco di codice rimosso)*

---

## Testing

**Unit Tests**: 16 tests (10 routing + 6 clients)
- ✅ User-tier routing validation
- ✅ Traffic split percentages (0%, 50%, 100%)
- ✅ Model format detection (local vs OpenRouter)
- ✅ Provider selection logic
- ✅ Configuration overrides

**Test Results**: 16/16 passed

---

## Consequences

### Positive
- **Cost Reduction**: 80% savings vs OpenRouter-only
- **Quality Maintained**: Premium models for authenticated users
- **Flexibility**: Admin-configurable routing per tier
- **Scalability**: Easy to add new providers (Anthropic direct, Azure OpenAI)

### Negative
- **Dual Dependencies**: Ollama (Docker) + OpenRouter (API key)
- **Complexity**: Routing logic, configuration management
- **Monitoring**: Need cost tracking per provider

### Neutral
- **Migration Path**: Existing `LlmService` can coexist during transition
- **Fallback Strategy**: Can adjust traffic split to 0% Ollama if quality degrades

---

## Monitoring & Metrics

**Required Metrics**:
- Provider usage distribution (actual vs configured %)
- Cost per provider (OpenRouter API usage)
- Quality metrics per provider (accuracy, latency)
- Fallback rate (Ollama → OpenRouter on error)

**Alerts**:
- Ollama downtime (>5min)
- OpenRouter API errors (>1% rate)
- Cost threshold exceeded (>$100/day)
- Quality degradation (<80% accuracy)

---

## Implemented Enhancements

### Adaptive Routing with Reliability (BGAI-020) - ✅ Completed 2025-11-12
- **Circuit Breaker Pattern**: Prevents cascading failures (5 failures → open for 30s)
- **Provider Health Monitoring**: ProviderHealthCheckService background service
- **Latency Tracking**: Real-time performance metrics (Average, P50, P95, P99)
- **Automatic Failover**: Routes to healthy providers when primary unavailable
- **Health Status API**: GET /api/v1/admin/llm-health endpoint
- **Test Coverage**: 8 tests (6 routing + 2 health monitoring)
- **Integration**: HybridLlmService coordinates health checks with routing decisions

### Cost Tracking (BGAI-018) - ✅ Completed 2025-11-12
- **Financial Cost Calculation**: LlmCostCalculator with pricing for 11 models
- **Database Persistence**: llm_cost_logs table with full attribution
- **Per-user/per-tier Attribution**: Tracks by UserId and UserRole
- **Cost Analytics**: 3 admin endpoints (report, daily, alerts)
- **Multi-threshold Alerts**: Daily ($100), Weekly ($500), Monthly projection ($3000)
- **Test Coverage**: 19 tests (12 calculator + 7 repository integration)
- **Non-blocking Logging**: Fire-and-forget persistence (doesn't slow requests)

### RAG Integration Testing (BGAI-024) - ✅ Completed 2025-11-12
- **Backward Compatibility**: 6 integration tests verify RagService works with HybridLlmService
- **Test Coverage**: AskAsync, ExplainAsync, AskWithHybridSearchAsync
- **Error Handling**: Validated graceful degradation scenarios
- **Cache Integration**: Verified cache hit/miss scenarios
- **Test Results**: 374/374 pass (+6 new tests)

### Performance Baseline (BGAI-025) - ✅ Completed 2025-11-12
- **P95 Latency Testing**: 3 performance tests with statistical measurement
- **Target Verified**: P95 <3000ms for all RAG methods
- **Test Strategy**: 20 iterations per method with realistic latency simulation
- **Metrics Collected**: Min, P50, Average, P95, P99, Max latencies
- **Test Results**: 377/377 pass (+3 performance tests)
- **Baseline Established**: Production-ready performance validated

## Future Enhancements

1. **Dynamic Routing**: Quality-based fallback (Ollama fails → OpenRouter)
2. **A/B Testing**: Automated quality comparison experiments
3. **Model Registry**: Admin UI for model configuration
4. **Consensus Mode**: Multi-model voting for critical questions
5. **Cost Optimization ML**: Predictive routing based on historical accuracy/cost

---

## References

- **Model Reference**: [OpenRouter Guide](../components/agent-lightning/openrouter-guide.md)
- **Issue**: #958 - BGAI-016 LLM Strategy Evaluation
- **ADR-003**: 3-Stage PDF Processing Pipeline (quality threshold precedent)
- **Related**: [ADR-004b: Hybrid LLM](adr-004b-hybrid-llm.md) - Multi-model consensus implementation

---

**Version**: 3.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Owner**: Engineering Lead



---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-009-centralized-error-handling.md

# ADR-009: Centralized Error Handling with Middleware

**Status**: Implemented
**Date**: 2025-11-16
**Supersedes**: Previous numbering as ADR-004-centralized-error-handling
**Issue**: [#1194](https://github.com/meepleai/meepleai-monorepo/issues/1194)
**Deciders**: Engineering Team

---

## Context

The API codebase had scattered try-catch blocks across endpoints, leading to:

- ❌ Code duplication (521 lines of repetitive error handling)
- ❌ Inconsistent error responses
- ❌ DRY violations in 3+ files
- ❌ Potential information leakage in exception messages
- ❌ Maintenance burden (changes required in multiple locations)

### Files with Duplicate Patterns

- `AiEndpoints.cs`: ~39 try-catch blocks (315 lines)
- `ChatEndpoints.cs`: 5 try-catch blocks (77 lines)
- `RuleSpecEndpoints.cs`: 9 try-catch blocks (129 lines)

**Total**: 53 try-catch blocks, 521 lines of duplicate error handling code

---

## Decision

Implement **centralized error handling** using:

1. ✅ **Enhanced `ApiExceptionHandlerMiddleware`** - Global exception handling for all `/api/*` paths
2. ✅ **Custom HTTP Exceptions** - Type-safe exceptions in `Middleware/Exceptions/`
3. ✅ **Result<T> Pattern** - Functional error handling in `SharedKernel/Domain/Results/`
4. ✅ **Complete try-catch removal** - Endpoints throw exceptions, middleware handles HTTP responses

---

## Architecture

### Exception Hierarchy

*(blocco di codice rimosso)*

### Result<T> Pattern

*(blocco di codice rimosso)*

### Middleware Flow

*(blocco di codice rimosso)*

---

## Implementation

### 1. Custom Exceptions Created

**Location**: `Api/Middleware/Exceptions/`

- `HttpException.cs` - Base class with StatusCode + ErrorCode
- `BadRequestException.cs` - Maps to HTTP 400
- `UnauthorizedHttpException.cs` - Maps to HTTP 401
- `ForbiddenException.cs` - Maps to HTTP 403
- `NotFoundException.cs` - Maps to HTTP 404 (with ResourceType + ResourceId)
- `ConflictException.cs` - Maps to HTTP 409

### 2. Result<T> Pattern

**Location**: `Api/SharedKernel/Domain/Results/Result.cs`

Features:
- Immutable record type
- Success/Failure factory methods
- Pattern matching with `Match()`
- Transformation with `Map<TResult>()`
- Predefined `Error` types (Validation, NotFound, Unauthorized, etc.)

### 3. Enhanced Middleware

**File**: `Api/Middleware/ApiExceptionHandlerMiddleware.cs`

**Changes**:
- Added custom exception handling in `MapExceptionToResponse()`
- Prioritized exception matching (specific → general)
- Support for domain exceptions (DomainException, ValidationException)
- Support for system exceptions (ArgumentException, KeyNotFoundException, etc.)

**Exception Mapping**:

| Exception Type | HTTP Status | Error Code |
|---------------|-------------|------------|
| HttpException | Custom | Custom |
| NotFoundException | 404 | not_found |
| ValidationException | 400 | validation_error |
| DomainException | 400 | domain_error |
| ArgumentException | 400 | bad_request |
| UnauthorizedAccessException | 403 | forbidden |
| KeyNotFoundException | 404 | not_found |
| TimeoutException | 504 | timeout |
| Unknown exceptions | 500 | internal_server_error |

### 4. Endpoint Cleanup

**Pattern Applied**:

**Before** (repetitive try-catch):
*(blocco di codice rimosso)*

**After** (clean, middleware-handled):
*(blocco di codice rimosso)*

**Files Cleaned**:
- ✅ `AiEndpoints.cs` - 39 try-catch blocks removed (315 lines saved)
- ✅ `ChatEndpoints.cs` - 5 try-catch blocks removed (77 lines saved)
- ✅ `RuleSpecEndpoints.cs` - 9 try-catch blocks removed (129 lines saved)

**Total**: 53 try-catch blocks removed, 521 lines saved

---

## Benefits

### 1. Code Quality

- ✅ **Single Responsibility** - Endpoints focus on business logic only
- ✅ **DRY Compliance** - Error handling logic in one place
- ✅ **Consistency** - All API endpoints use same error handling pattern
- ✅ **Maintainability** - Changes to error handling require updates in one file only

### 2. Security

- ✅ **No Information Leakage** - Stack traces only in development environment
- ✅ **Consistent Error Format** - Standardized JSON structure prevents accidental exposure
- ✅ **Correlation IDs** - All errors include `X-Correlation-Id` for debugging

### 3. Developer Experience

- ✅ **Cleaner Code** - Endpoints are more readable (521 lines removed)
- ✅ **Type-Safe Errors** - Custom exceptions provide compile-time safety
- ✅ **Easy Testing** - 30 middleware tests cover all exception scenarios
- ✅ **Clear Patterns** - Result<T> provides functional error handling option

### 4. Observability

- ✅ **Centralized Logging** - All exceptions logged with full context
- ✅ **Metrics Integration** - `MeepleAiMetrics.RecordApiError()` for monitoring
- ✅ **Correlation Tracking** - TraceIdentifier propagated to all error responses

---

## Testing

### Test Coverage

**New Tests**: 30 comprehensive middleware tests

**Test Categories**:
1. Custom HTTP exceptions (6 tests)
2. Domain exceptions (2 tests)
3. System exceptions (7 tests)
4. Edge cases (4 tests)
5. Environment-specific behavior (2 tests)
6. Theory-based validation (9 tests)

**Results**: ✅ 30/30 passing (100% success rate)

### Test Scenarios

- ✅ Each custom exception maps to correct HTTP status
- ✅ Error responses include correlationId and timestamp
- ✅ Stack traces only included in development environment
- ✅ Non-API paths (/health, /) are not handled by middleware
- ✅ Exceptions are logged with full details
- ✅ Metrics are recorded for all errors

---

## Trade-offs

### Accepted

- ✅ **Simplified chat error logging** - Removed nested try-catch for chat logging (non-critical functionality)
- ✅ **Generic error messages** - More secure but less specific than endpoint-specific messages
- ✅ **Middleware dependency** - All error handling depends on middleware registration order

### Rejected

- ❌ **MediatR Pipeline Behavior** - Too complex with reflection, type conversion issues
- ❌ **Partial cleanup** - Decided to remove ALL try-catch for maximum consistency
- ❌ **Manual error returns** - Replaced with exception throwing for middleware handling

---

## Migration Path

### Phase 1: Foundation (Completed)
- ✅ Create exception types
- ✅ Enhance middleware
- ✅ Add Result<T> pattern

### Phase 2: Cleanup (Completed)
- ✅ Remove try-catch from AiEndpoints.cs
- ✅ Remove try-catch from ChatEndpoints.cs
- ✅ Remove try-catch from RuleSpecEndpoints.cs

### Phase 3: Validation (Completed)
- ✅ Create comprehensive tests
- ✅ Verify zero compilation errors
- ✅ Run full test suite

### Future Enhancements

**Potential Improvements**:
- Use Result<T> in CQRS handlers (optional, functional approach)
- Add structured logging with error categories
- Implement retry policies for transient errors
- Add exception fingerprinting for error aggregation

---

## Metrics

### Code Impact

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| **Try-catch blocks** | 53 | 0 | -53 (100%) |
| **Lines of code** | Baseline | -521 | -521 lines |
| **Error handling files** | 3 endpoints | 1 middleware | Centralized |
| **Test coverage** | N/A | 30 tests | 100% middleware |
| **Compilation errors** | 0 | 0 | ✅ No regressions |

### Performance Impact

- ✅ **Zero overhead** - Middleware only invoked on exceptions (exceptional path)
- ✅ **Happy path unchanged** - No performance impact on successful requests
- ✅ **Logging optimized** - Single log entry per exception vs multiple nested logs

---

## References

- Issue #1194: [Refactor] Centralize Error Handling with Middleware
- [Microsoft Docs: Exception Handling Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
- [Result Pattern in C#](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)

---

## Conclusion

Centralized error handling successfully implemented with:
- ✅ 521 lines of duplicate code removed
- ✅ Consistent error responses across all endpoints
- ✅ Type-safe exception hierarchy
- ✅ 100% middleware test coverage
- ✅ Zero compilation errors or test failures
- ✅ Improved security (no information leakage)
- ✅ Better observability (centralized logging + metrics)

The implementation follows DDD principles, maintains architectural consistency, and provides a solid foundation for future error handling enhancements.


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-011-cors-whitelist-headers.md

# ADR-011: CORS Header Whitelist Strategy

**Status**: Proposed
**Date**: 2025-01-19
**Deciders**: Engineering Lead, Security Team
**Context**: Code Review - Backend-Frontend Interactions Security Hardening

---

## Context

During the code review of backend-frontend interactions (2025-01-19), a security vulnerability was identified in the CORS configuration:

**Current Implementation** (`Program.cs:214` and `WebApplicationExtensions.cs:130`):
*(blocco di codice rimosso)*

**Problem**: `AllowAnyHeader()` permits **any custom HTTP header**, creating a potential attack vector:
1. Malicious headers can bypass security controls
2. Custom headers can leak sensitive information
3. Broader attack surface for header injection attacks
4. Non-compliance with principle of least privilege

**Industry Best Practice**:
- OWASP: Explicitly whitelist allowed headers
- Mozilla Web Security: "Never use AllowAnyHeader with credentials"
- CORS Security Cheat Sheet: "Minimize allowed headers"

**Current Risk Level**: **Medium-High**
- No active exploits observed
- But preventable attack surface
- Security audit red flag

---

## Decision

Replace `AllowAnyHeader()` with **explicit header whitelist** for all CORS policies.

### Allowed Headers

*(blocco di codice rimosso)*

### Justification for Each Header

| Header | Purpose | Required By | Risk if Blocked |
|--------|---------|-------------|-----------------|
| `Content-Type` | JSON/form data encoding | All endpoints | ❌ All requests fail |
| `Authorization` | Bearer tokens, Basic auth | Auth endpoints | ❌ Auth broken |
| `X-Correlation-ID` | Request tracing, debugging | Observability | ⚠️ Logs incomplete |
| `X-API-Key` | Programmatic API access | API key auth | ❌ CLI/scripts fail |

**Non-Standard Headers** (explicitly blocked):
- `X-Custom-*`: No custom headers allowed
- `X-Debug-*`: Security risk (information leakage)
- `X-Admin-*`: Privilege escalation risk

---

## Architecture

### Before (Vulnerable)
*(blocco di codice rimosso)*

### After (Secure)
*(blocco di codice rimosso)*

---

## Implementation

### Location
- `apps/api/src/Api/Program.cs` (line 214)
- `apps/api/src/Api/Extensions/WebApplicationExtensions.cs` (line 130)

### Code Changes

**Before**:
*(blocco di codice rimosso)*

**After**:
*(blocco di codice rimosso)*

### Configuration Support

**appsettings.json**:
*(blocco di codice rimosso)*

**Dynamic Loading**:
*(blocco di codice rimosso)*

---

## Consequences

### Positive

✅ **Security Hardening**:
- Reduced attack surface (no arbitrary headers)
- Compliance with OWASP CORS guidelines
- Explicit documentation of allowed headers

✅ **Auditability**:
- Clear inventory of permitted headers
- Easy to review in code/config
- Security scan improvements

✅ **Zero Performance Impact**:
- Same CORS preflight handling
- No additional overhead

✅ **Maintainability**:
- Explicit is better than implicit
- Self-documenting code
- Easier to review in PRs

### Negative

⚠️ **Breaking Changes (if any)**:
- If frontend uses non-standard headers → requests fail
- **Mitigation**: Test all endpoints before merge

⚠️ **Future Header Additions**:
- Need code change to add new headers
- **Mitigation**: Configuration-based (appsettings.json)

### Risks

🟡 **Frontend Breakage**:
- **Risk**: Unknown custom headers used by frontend
- **Mitigation**: Comprehensive testing + grep for custom headers
- **Testing**: `grep -r "X-" apps/web/src/` to find custom headers

🟢 **CORS Preflight Complexity**:
- **Risk**: Preflight requests fail for new headers
- **Mitigation**: Test OPTIONS requests explicitly

---

## Validation Plan

### Pre-Merge Testing

1. **Grep for Custom Headers**: Search frontend codebase for any X-* headers in API calls
2. **Manual Endpoint Tests**: Test allowed headers (Content-Type, X-Correlation-ID) pass, blocked headers (X-Custom-Header) fail
3. **CORS Preflight Tests**: Validate OPTIONS requests return correct Access-Control-Allow-Headers

### Integration Tests

**Two test cases required**:
1. `CorsPolicy_ShouldAllowWhitelistedHeaders`: Verify Content-Type, Authorization, X-Correlation-ID, X-API-Key pass
2. `CorsPolicy_ShouldRejectNonWhitelistedHeaders`: Verify X-Evil-Header blocked

**Full test implementation**: See `CorsPolicyTests.cs` for complete test suite

---

## Alternatives Considered

### Alternative 1: Keep AllowAnyHeader + Input Validation
**Description**: Keep permissive CORS, validate headers in middleware

**Pros**:
- No frontend changes needed
- More flexible

**Cons**:
- Defense in depth violation (trust CORS layer)
- Extra validation overhead
- Harder to audit

**Decision**: Rejected - Prefer prevention over detection

### Alternative 2: Stricter Whitelist (Content-Type only)
**Description**: Allow only Content-Type header

**Pros**:
- Maximum security
- Minimal attack surface

**Cons**:
- Breaks authentication (no Authorization header)
- Breaks tracing (no X-Correlation-ID)
- Breaks API keys (no X-API-Key)

**Decision**: Rejected - Too restrictive, breaks functionality

### Alternative 3: Per-Endpoint CORS Policies
**Description**: Different header whitelists per endpoint

**Pros**:
- Fine-grained control
- Principle of least privilege

**Cons**:
- Complex to maintain
- Hard to test
- Overkill for current needs

**Decision**: Deferred to Phase 2 if needed

---

## Rollout Plan

### Phase 1: Code Changes (Sprint 1)
1. ✅ Update Program.cs CORS policy
2. ✅ Update WebApplicationExtensions.cs
3. ✅ Add configuration support
4. ✅ Update tests

### Phase 2: Validation (Sprint 1)
5. ✅ Grep frontend for custom headers
6. ✅ Test all endpoints manually
7. ✅ Run integration test suite
8. ✅ CORS preflight tests

### Phase 3: Documentation (Sprint 1)
9. ✅ Update API documentation
10. ✅ docs/03-api/cors-configuration.md
11. ✅ This ADR
12. ✅ SECURITY.md

### Phase 4: Deployment (Sprint 1)
13. ⏳ Deploy to staging
14. ⏳ Monitor for CORS errors (1 week)
15. ⏳ Deploy to production

---

## Monitoring

**Metrics**: `cors_preflight_requests_total{status, origin}`, `cors_blocked_headers_total{header_name}`

**Alerts**: Preflight failure rate >1%, Blocked header attempts detected

**Logging**: Log warnings when non-whitelisted headers detected (header name + origin)

---

## Related Decisions

- **ADR-010**: Security Headers Middleware (companion security fix)
- **ADR-006**: Multi-Layer Validation (defense in depth)

---

## References

- [OWASP CORS Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Origin_Resource_Sharing_Cheat_Sheet.html)
- [MDN CORS](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- [ASP.NET Core CORS](https://learn.microsoft.com/en-us/aspnet/core/security/cors)
- [Mozilla Web Security Guidelines](https://infosec.mozilla.org/guidelines/web_security)

---

**Decision Maker**: Engineering Lead
**Approval**: Pending Security Team Review
**Implementation**: Issue #TBD


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-012-fluentvalidation-cqrs.md

# ADR-012: FluentValidation Integration with CQRS Pipeline

**Status**: Proposed
**Date**: 2025-01-19
**Deciders**: Engineering Lead, Backend Team
**Context**: Code Review - Backend-Frontend Interactions Input Validation

---

## Context

During the code review of backend-frontend interactions (2025-01-19), a gap in input validation was identified:

**Current State**:
- Some endpoints have manual validation (e.g., `if (string.IsNullOrWhiteSpace(payload.Email))`)
- No consistent validation framework
- Validation logic scattered across endpoints
- Poor error messages for users
- No centralized validation testing

**Problems**:
1. **Inconsistent Validation**: Some commands validated, others not
2. **Code Duplication**: Same validation rules repeated
3. **Poor UX**: Generic error messages ("Bad Request")
4. **Security Risk**: Invalid data can reach domain layer
5. **Maintainability**: Hard to update validation rules

**Example** (`AuthEndpoints.cs:96-100`):
*(blocco di codice rimosso)*

**Industry Best Practices**:
- ASP.NET Core: FluentValidation for complex validation
- CQRS: Validate commands before handler execution
- MediatR: Pipeline behaviors for cross-cutting concerns

---

## Decision

Implement **FluentValidation with MediatR Pipeline Behavior** for all CQRS Commands and Queries.

### Architecture

*(blocco di codice rimosso)*

### Components

#### 1. FluentValidation Validators

**Pattern**: Inherit `AbstractValidator<TCommand>`, define rules with `RuleFor()`

**Example rules**:
- Email: NotEmpty, EmailAddress, MaxLength(256)
- Password: NotEmpty, MinLength(8), MaxLength(128)
- Custom validation: Must(), When(), DependentRules()

**Implementation**: See validator classes in `BoundedContexts/{Context}/Application/Commands/Validators/`

#### 2. ValidationBehavior Pipeline

**Purpose**: MediatR pipeline behavior that intercepts all commands/queries before handler execution

**Logic**:
1. Find all `IValidator<TRequest>` implementations
2. Execute validation asynchronously (parallel)
3. Collect failures
4. Throw `ValidationException` if any failures
5. Continue to handler if valid

**Implementation**: See `ValidationBehavior.cs` in `Infrastructure/Behaviors/`

#### 3. Global Exception Handler

**Middleware**: Catches `ValidationException`, returns RFC 7807 Problem Details (422 status)

**Response format**: Field-level errors grouped by property name

**Implementation**: See `ApiExceptionHandlerMiddleware.cs` exception handling

---

## Implementation Scope

### Priority 1: Authentication Context (Sprint 2)

1. **LoginCommandValidator**
   - Email: NotEmpty, EmailAddress, MaxLength(256)
   - Password: NotEmpty, MinLength(8), MaxLength(128)

2. **RegisterCommandValidator**
   - Email: NotEmpty, EmailAddress, MaxLength(256), Must be unique
   - Password: NotEmpty, MinLength(8), MaxLength(128), Regex(complexity)
   - DisplayName: MaxLength(100)
   - Role: Must be valid enum

3. **ChangePasswordCommandValidator**
   - CurrentPassword: NotEmpty, MinLength(8), MaxLength(128)
   - NewPassword: NotEmpty, MinLength(8), MaxLength(128)
   - NewPassword: Must be different from CurrentPassword
   - Password complexity rules

4. **Enable2FACommandValidator**
   - Code: NotEmpty, Length(6), Regex(^\d{6}$)

5. **ResetPasswordCommandValidator**
   - Token: NotEmpty, Must be valid GUID
   - NewPassword: NotEmpty, MinLength(8), MaxLength(128), Regex(complexity)
   - Email: NotEmpty, EmailAddress

### Priority 2: GameManagement Context (Sprint 2)

6. **CreateGameCommandValidator**
   - Title: NotEmpty, MaxLength(200)
   - Publisher: MaxLength(100)
   - YearPublished: GreaterThan(1900), LessThan(current year + 1)
   - PlayerCount: Between(1, 100)

7. **UpdateGameCommandValidator**
   - Similar to CreateGameCommandValidator

8. **CreateSessionCommandValidator**
   - GameId: NotEmpty, Must be valid GUID
   - PlayerNames: NotEmpty, MinLength(1), MaxLength(20 per name)

### Priority 3: KnowledgeBase Context (Sprint 3)

9. **AskQuestionCommandValidator** (StreamQaQuery)
   - GameId: NotEmpty, Must be valid GUID
   - Query: NotEmpty, MinLength(3), MaxLength(500)
   - SearchMode: Must be valid enum

10. **CreateChatThreadCommandValidator**
    - GameId: NotEmpty, Must be valid GUID
    - Title: MaxLength(200)

---

## Consequences

### Positive

✅ **Security**:
- Prevents invalid data from reaching domain layer
- Consistent validation across all endpoints
- Reduced attack surface for injection attacks

✅ **User Experience**:
- Clear, specific error messages
- Field-level error reporting (not just "Bad Request")
- Frontend can display errors next to form fields

✅ **Maintainability**:
- Single source of truth for validation rules
- Easy to test (unit test validators independently)
- Self-documenting (validators show business rules)

✅ **Developer Experience**:
- Consistent pattern across codebase
- Reusable validators (composition)
- Less boilerplate in handlers

✅ **Testability**: Validators easily unit tested in isolation (no infrastructure dependencies)

### Negative

⚠️ **Performance Overhead**:
- ~0.5-2ms validation time per request
- **Mitigation**: Negligible compared to I/O operations

⚠️ **Learning Curve**:
- Team needs to learn FluentValidation syntax
- **Mitigation**: Good documentation, examples, code reviews

⚠️ **Breaking Changes**:
- Error response format changes (now returns field-level errors)
- **Mitigation**: Frontend already handles 422 errors

### Risks

🟡 **Complex Validation Rules**:
- **Risk**: Async validators (database lookups) slow down requests
- **Mitigation**: Cache common validation results, limit async validators

🟢 **Over-Validation**:
- **Risk**: Too strict rules frustrate users
- **Mitigation**: Business-driven rules, user testing

---

## Error Response Format

### Before (Inconsistent)
*(blocco di codice rimosso)*

### After (RFC 7807 Problem Details)
*(blocco di codice rimosso)*

---

## Alternatives Considered

### Alternative 1: Data Annotations
**Description**: Use `[Required]`, `[EmailAddress]`, `[StringLength]` attributes

**Pros**:
- Built into ASP.NET Core
- Simpler for basic validation
- No external dependencies

**Cons**:
- Limited expressiveness (hard to write complex rules)
- Harder to test
- Attributes pollute domain models

**Decision**: Rejected - FluentValidation more powerful

### Alternative 2: Manual Validation in Handlers
**Description**: Keep validation logic in each handler

**Pros**:
- No framework dependency
- Full control

**Cons**:
- Code duplication
- Inconsistent error messages
- Hard to maintain

**Decision**: Rejected - Violates DRY principle

### Alternative 3: Domain Model Validation
**Description**: Validate in domain entity constructors

**Pros**:
- Domain-driven design alignment
- Validation close to data

**Cons**:
- Can't return user-friendly errors
- Mixes validation with domain logic
- Harder to test

**Decision**: Partial adoption - Domain validates invariants, CQRS validates input

---

## Testing Strategy

### Unit Tests (Validators)
**Pattern**: Instantiate validator, test invalid inputs return expected error messages

**Test cases**:
- Empty/null values
- Format validation (email, regex)
- Length constraints (min/max)
- Custom business rules

**Example**: See `LoginCommandValidatorTests.cs` for Theory-based test patterns

### Integration Tests (Pipeline)
**Pattern**: HTTP request with invalid data → verify 422 status + field-level errors in response

**Test cases**:
- Malformed input (invalid email)
- Missing required fields
- Out-of-range values
- Cross-field validation

**Example**: See endpoint integration tests for complete validation flow

---

## Migration Plan

### Phase 1: Infrastructure (Week 1)
1. ✅ Install FluentValidation.AspNetCore
2. ✅ Create ValidationBehavior
3. ✅ Register in DI container
4. ✅ Update ApiExceptionHandlerMiddleware
5. ✅ Test pipeline integration

### Phase 2: Authentication (Week 2)
6. ✅ Create 5 validators (Login, Register, ChangePassword, Enable2FA, ResetPassword)
7. ✅ Unit tests for each validator
8. ✅ Integration tests for endpoints
9. ✅ Remove manual validation from endpoints
10. ✅ Update API documentation

### Phase 3: GameManagement (Week 3)
11. ✅ Create 3 validators (CreateGame, UpdateGame, CreateSession)
12. ✅ Tests
13. ✅ Remove manual validation

### Phase 4: KnowledgeBase (Week 4)
14. ✅ Create 4 validators (AskQuestion, CreateThread, AddMessage, CreateComment)
15. ✅ Tests
16. ✅ Remove manual validation

---

## Dependencies

**NuGet Packages**:
- `FluentValidation.AspNetCore` v11.3.0
- `FluentValidation.DependencyInjectionExtensions` v11.3.0

**Registration** (`Program.cs`):
- `AddValidatorsFromAssembly()`: Auto-register all validators
- `AddBehavior<ValidationBehavior>()`: Register pipeline behavior for MediatR

---

## Related Decisions

- **ADR-008**: Streaming CQRS Migration (pipeline behaviors)
- **ADR-009**: Centralized Error Handling (exception middleware)

---

## References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors)
- [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807)
- [ASP.NET Core Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)

---

**Decision Maker**: Engineering Lead
**Approval**: Pending Backend Team Review
**Implementation**: Issue #TBD (Sprint 2)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-018-postgresql-fts-for-shared-catalog.md

# ADR-018: PostgreSQL Full-Text Search for SharedGameCatalog

**Status**: Accepted
**Date**: 2026-01-14
**Deciders**: Development Team
**Issue**: #2425 (Parent: #2374 Phase 5)

---

## Context

SharedGameCatalog requires full-text search for Italian board game catalog with:
- **Multilingual support**: Italian stemming, stop words, diacritics
- **Performance target**: P95 latency < 200ms (10x improvement over BGG ~2000ms baseline)
- **Scale**: ~10,000 games initially, growth to 50,000+ over time
- **Query patterns**: Search by title/description, filter by category/mechanic/players/time

We evaluated multiple search technologies to determine the optimal solution.

---

## Decision

**We use PostgreSQL native Full-Text Search with:**
- **Language configuration**: `'italian'` for proper stemming and stop words
- **GIN index**: `ix_shared_games_fts` on `to_tsvector('italian', title || ' ' || description)`
- **Query function**: `EF.Functions.PlainToTsQuery('italian', searchTerm)` for parameterized queries
- **Filtering**: Composite indexes for category/mechanic/players/playtime filters

---

## Alternatives Considered

### Alternative 1: Elasticsearch
**Pros**:
- Industry-standard full-text search
- Advanced features (fuzzy matching, boosting, aggregations)
- Horizontal scalability

**Cons**:
- ❌ Infrastructure complexity (additional service to manage)
- ❌ Overkill for < 50K documents
- ❌ Operational overhead (heap sizing, cluster management, backups)
- ❌ Cost (memory requirements 2-4GB minimum)
- ❌ Data synchronization (PostgreSQL → Elasticsearch latency)

**Verdict**: Rejected - unnecessary complexity for our scale.

### Alternative 2: Qdrant Vector Search
**Pros**:
- Already used for RAG vector search
- Semantic search capability
- No additional infrastructure

**Cons**:
- ❌ Not optimized for keyword-based search
- ❌ No Italian language-specific features (stemming, stop words)
- ❌ Hybrid search (keyword + vector) adds latency
- ❌ Less mature for traditional FTS compared to PostgreSQL

**Verdict**: Rejected - wrong tool for keyword search use case.

### Alternative 3: Simple LIKE Queries
**Pros**:
- Trivial implementation
- No indexes needed
- Zero learning curve

**Cons**:
- ❌ Poor performance (sequential scans, O(n) complexity)
- ❌ No ranking or relevance scoring
- ❌ No Italian stemming ("gioco" vs "giochi" are separate)
- ❌ Case-insensitive requires LOWER() function (index-unfriendly)

**Verdict**: Rejected - fails performance requirements.

---

## Rationale

### 1. Native Italian Language Support
PostgreSQL `'italian'` configuration provides:
- **Stemming**: "strategia" matches "strategie", "strategico"
- **Stop words**: Filters "il", "la", "di", "da" for cleaner matching
- **Diacritics**: Handles "città", "perché" correctly
- **Case-insensitive**: Built into `to_tsvector`

This is **critical** for Italian board game catalog (e.g., "Il Gioco della Città" matches "gioco città").

### 2. Proven Performance
**Measured Results** (Issue #2374 Phase 5):
- **GIN index creation**: One-time ~500ms for 10K games
- **Search latency P95**: < 200ms (target met)
- **Index size**: ~2MB for 10K games (minimal storage overhead)
- **Maintenance**: Auto-updated on INSERT/UPDATE (no manual reindexing)

Compared to:
- BGG baseline: ~2000ms (10x slower)
- Elasticsearch: Similar performance but with operational complexity

### 3. Zero Additional Infrastructure
PostgreSQL is **already required** for:
- User authentication (sessions, API keys)
- Game management (personal collections)
- Document processing (PDF metadata)
- RAG system (text chunks, though vectors in Qdrant)

Adding Elasticsearch would mean:
- Docker Compose service (+ monitoring)
- Backup strategy
- Data synchronization logic
- Version compatibility management

### 4. Integrated Transaction Support
PostgreSQL FTS allows:
- **ACID transactions**: Game creation + FTS indexing in single transaction
- **Referential integrity**: FK constraints between SharedGame and Categories/Mechanics
- **Composite queries**: JOIN categories + full-text search in single query

Elasticsearch requires eventual consistency (async indexing).

### 5. Cost-Effective Scaling
PostgreSQL FTS scales to **millions of documents** before needing alternatives:
- **10K games**: < 200ms P95 ✅
- **100K games**: < 500ms P95 (estimated, with partitioning)
- **1M+ games**: Consider Elasticsearch (but unlikely for board game domain)

For our projected scale (50K games), PostgreSQL is sufficient for years.

---

## Implementation Details

### GIN Index Strategy
*(blocco di codice rimosso)*

**Why GIN?**
- Optimized for full-text search (vs GiST which is for geometric data)
- Faster queries at cost of slower inserts (acceptable: catalog updates are infrequent)
- Supports `@@` (match) operator efficiently

### Query Pattern
*(blocco di codice rimosso)*

**Why PlainToTsQuery?**
- User-friendly (no syntax required)
- SQL injection safe (parameterized by EF Core)
- AND semantics ("gioco strategia" = both terms required)

---

## Consequences

### Positive
- ✅ Native Italian language support (stemming, stop words)
- ✅ P95 < 200ms performance (10x improvement validated)
- ✅ Zero additional infrastructure
- ✅ ACID transactions for data consistency
- ✅ Cost-effective scaling to 100K+ games
- ✅ Integrated with existing PostgreSQL expertise

### Negative
- ❌ Locked into PostgreSQL (acceptable - already core dependency)
- ❌ Index maintenance overhead (mitigated by filtered indexes)
- ❌ Limited to keyword search (no semantic/vector search)
- ❌ Manual language configuration (must specify 'italian')

### Mitigation
- **Lock-in**: PostgreSQL is industry-standard, migration paths exist if needed
- **Maintenance**: VACUUM ANALYZE automated via cron (standard PostgreSQL ops)
- **Semantic search**: Use Qdrant for "similar games" feature if needed (complementary, not replacement)

---

## Monitoring & Validation

### Performance Metrics
- **Health check**: `SharedGameCatalogHealthCheck` monitors FTS latency
- **Prometheus**: `http_server_request_duration_bucket{route="/api/v1/shared-games"}`
- **Grafana**: Dashboard with P95 alert (> 200ms triggers warning)

### Index Usage Validation
*(blocco di codice rimosso)*

### Load Testing
- **k6 script**: `tests/k6/shared-catalog-load-test.js`
- **Scenario**: 100 req/s sustained for 3 minutes
- **Threshold**: P95 < 200ms (enforced in k6)

---

## Future Considerations

### When to Reconsider
- **Scale**: If catalog exceeds 500K games AND P95 degrades > 500ms
- **Features**: If semantic search ("find similar games") becomes critical
- **Multi-language**: If catalog expands beyond Italian (need language detection)

### Migration Path (if needed)
1. Keep PostgreSQL for transactional data
2. Add Elasticsearch/Typesense for search only
3. Sync via Change Data Capture (CDC) or domain events
4. Maintain PostgreSQL FTS as fallback

---

## Compliance

This decision aligns with:
- **YAGNI**: Use simplest solution that meets requirements
- **Boring Technology**: Prefer proven, well-understood technologies
- **Operational Excellence**: Minimize moving parts and complexity

---

## References

- Issue #2371: Phase 2 (FTS implementation)
- Issue #2374: Phase 5 (Performance optimization with GIN indexes)
- Issue #1996: Italian FTS configuration fix
- PostgreSQL Documentation: [Full-Text Search](https://www.postgresql.org/docs/16/textsearch.html)
- Migration: `20260114121520_AddSharedGameCatalogPerformanceIndexes.cs`
- Performance validation: `docs/05-testing/shared-catalog-fts-performance-validation.sql`


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-020-valueobject-record-evaluation.md

# ADR-020: ValueObject Record Syntax Evaluation

**Status**: Rejected
**Date**: 2026-01-14
**Deciders**: Engineering Lead, Backend Team
**Context**: Code Quality - DDD Value Objects Pattern

---

## Context

Issue #2384 proposed evaluating C# 9+ record syntax with primary constructors for simple ValueObjects to reduce boilerplate. The codebase contains 43 ValueObject implementations with varying complexity.

**Current Pattern** (class-based):
*(blocco di codice rimosso)*

**Proposed Alternative** (record syntax):
*(blocco di codice rimosso)*

### ValueObject Analysis Summary

| Category | Count | Description |
|----------|-------|-------------|
| Simple (1-2 props, minimal logic) | 20 | Potential candidates |
| Medium (2-3 props or methods) | 16 | Not suitable |
| Complex (>3 props or operators) | 7 | Not suitable |
| **Total** | **43** | |

---

## Decision

**Rejected** - Keep class-based ValueObjects. Do not convert to record syntax.

### Technical Blocker

C# records **cannot inherit from non-record classes**. The current `ValueObject` base class is:

*(blocco di codice rimosso)*

Converting ValueObjects to records would require one of:

1. **Convert base to `abstract record`** - Breaks existing class-based VOs
2. **Remove inheritance** - Loses shared equality logic, violates DRY
3. **Duplicate equality in each record** - Defeats purpose of abstraction
4. **Interface-based approach** - Partial solution, but records still need equality override

---

## Analysis

### Option 1: Convert Base Class to Abstract Record

*(blocco di codice rimosso)*

**Problems**:
- Records use property-based equality by default, overriding to component-based is unusual
- Would require updating ALL 43 ValueObjects to `record` syntax
- Some complex VOs use operators (`>, <, >=, <=`) which require manual implementation regardless
- EF Core mapping may behave differently with records
- Breaking change across entire codebase

### Option 2: Remove Inheritance for Simple VOs

*(blocco di codice rimosso)*

**Problems**:
- Loses consistent equality behavior across VOs
- Mixed patterns (some inherit `ValueObject`, some are standalone records)
- Cannot use polymorphism (`IEnumerable<ValueObject>`)
- Inconsistent approach confuses developers

### Option 3: Keep Class-Based Pattern (Selected)

Keep current implementation. The "boilerplate" is actually meaningful:
- `GetEqualityComponents()` forces explicit equality consideration
- Consistent pattern across all 43 ValueObjects
- Well-tested, proven approach in DDD community
- EF Core compatibility guaranteed

---

## Consequences

### Positive

✅ **Consistency**: Single pattern for all ValueObjects
✅ **Maintainability**: Team knows exactly what to expect
✅ **Type Safety**: Base class provides compile-time guarantees
✅ **EF Core**: No mapping surprises with established class pattern
✅ **Polymorphism**: Can work with `IEnumerable<ValueObject>`

### Negative

⚠️ **Verbosity**: 20-30 lines per simple ValueObject (vs ~15 with record)
⚠️ **Modern C# Features**: Not using latest language constructs

**Mitigation**: The "extra" code is meaningful - it explicitly defines equality semantics and validation.

---

## Alternatives Considered

### Alternative A: Hybrid Approach
Use records for new simple VOs, keep classes for complex ones.

**Rejected**: Creates inconsistency, confuses developers, complicates code reviews.

### Alternative B: Source Generator
Generate ValueObject boilerplate from attributes.

**Rejected**: Adds build complexity, debugging difficulty, marginal benefit for 43 VOs.

### Alternative C: Composition over Inheritance
*(blocco di codice rimosso)*

**Rejected**: C# interfaces can't enforce equality contract, still requires boilerplate.

---

## Recommendations

Instead of record conversion, consider these improvements:

1. **Code Snippets**: Create VS/Rider snippets for ValueObject creation
2. **Analyzer**: Add custom Roslyn analyzer to enforce ValueObject patterns
3. **Documentation**: Ensure ValueObject patterns are well-documented

---

## ValueObject Inventory (for reference)

### Simple ValueObjects (20) - NOT converting
- AlertSeverity, Email, TotpSecret, CollectionName, FileName
- LanguageCode, FAQAnswer, FAQQuestion, Publisher, SessionStatus
- YearPublished, Confidence, LibraryNotes, NotificationSeverity
- NotificationType, WorkflowUrl, ConfigKey, Percentage, Citation

### Medium Complexity (16) - Inherently unsuitable
- PasswordHash, Role, UserTier, BackupCode, FileSize, PageCount
- PdfVersion, GameTitle, PlayerCount, PlayTime, SessionPlayer
- Vector, ExportedChatData, GameRules, HnswConfiguration, QuantizationConfiguration

### Complex (7) - Operators/Comparisons required
- DocumentType, Version, AccessibilityMetrics, E2EMetrics
- PerformanceMetrics, ChunkPayload, DocumentVersion

---

## References

- [C# Records Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Record Inheritance Limitations](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#inheritance)
- [DDD Value Objects Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects)
- [EF Core with Records](https://learn.microsoft.com/en-us/ef/core/modeling/constructors)

---

**Decision Maker**: Engineering Lead
**Outcome**: Maintain current class-based ValueObject pattern
**Implementation**: None (keep existing code)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-021-auto-configuration-system.md

# ADR-021: Auto-Configuration System for Secret Management

**Status**: Implemented
**Date**: 2026-01-17
**Deciders**: Engineering Lead, DevOps Team
**Context**: Infrastructure - Development Experience & Secret Management

---

## Context

During initial project setup and deployment, developers faced significant friction configuring 17+ secret files manually. This resulted in:

**Pain Points**:
- **Time-consuming setup**: 15-30 minutes manually creating and configuring secrets
- **Copy-paste errors**: Typos in secret file names or variable names
- **Weak credentials**: Developers using placeholder passwords like `password123`
- **Missing secrets**: Application failing to start due to incomplete configuration
- **Security risks**: Secrets committed to git, shared via insecure channels (Slack)

**Industry Practice**:
- **12-Factor App**: Configuration via environment variables, secrets separate from code
- **Kubernetes**: Secret management with ConfigMaps and Secrets
- **HashiCorp Vault**: Centralized secret storage with rotation
- **AWS/Azure**: Cloud-native secret managers (Secrets Manager, Key Vault)

**Current State** (Pre-ADR-021):
*(blocco di codice rimosso)*

**Desired State**:
*(blocco di codice rimosso)*

---

## Decision

Implement **PowerShell-based Auto-Configuration System** with:

1. **Automated Secret Generation**: Cryptographically secure values for 11/17 secrets
2. **3-Level Validation**: CRITICAL (block startup) → IMPORTANT (warn) → OPTIONAL (info)
3. **Health Check Integration**: Runtime verification of all services
4. **Comprehensive Documentation**: Step-by-step guides with troubleshooting

### Architecture

*(blocco di codice rimosso)*

### Components

#### 1. Setup Script (`setup-secrets.ps1`)

**Features**:
- Auto-generates 11 cryptographically secure secrets
- Creates 17 `.secret` files from `.example` templates
- Optional backup of generated values to `.generated-values-TIMESTAMP.txt`
- Validates file creation and provides clear success/error messages

**Security**:
- **RNG-based keys**: `[System.Security.Cryptography.RNGCryptoServiceProvider]`
- **Base64 encoding**: 256-512 bits entropy for API keys
- **Strong passwords**: 16-20 chars with uppercase, digit, symbol requirements
- **No weak defaults**: All generated values meet production-grade strength

**Example Output**:
*(blocco di codice rimosso)*

#### 2. Validation System (3-Level Priority)

**CRITICAL Secrets** (6 files):
*(blocco di codice rimosso)*

**IMPORTANT Secrets** (3 files):
*(blocco di codice rimosso)*

**OPTIONAL Secrets** (8 files):
*(blocco di codice rimosso)*

#### 3. Health Check System

**Endpoint**: `/api/v1/health`

**Response**:
*(blocco di codice rimosso)*

**Overall Status Logic**:
- **Healthy**: All checks pass
- **Degraded**: Non-critical service fails
- **Unhealthy**: Critical service fails

---

## Analysis

### Option 1: Manual Configuration (Pre-ADR-021)

**Process**:
1. Copy 17 `.example` files to `.secret` files manually
2. Generate strong passwords using password manager
3. Create API keys for external services
4. Edit each file individually
5. Verify no typos or missing files

**Pros**:
- Full control over every value
- No automation complexity
- Explicit understanding of each secret

**Cons**:
- 15-30 minutes setup time
- High error rate (typos, weak passwords)
- Copy-paste mistakes
- Developer frustration
- Security risks (weak credentials)

### Option 2: Auto-Configuration with PowerShell (Selected)

**Process**:
1. Run `setup-secrets.ps1`
2. Auto-generate 11 cryptographic secrets
3. Manually configure 6 external service credentials
4. Start application

**Pros**:
- <1 minute setup time for auto-generated secrets
- Zero weak passwords (256-512 bit entropy)
- Consistent security standards
- Developer experience improved dramatically
- Reduced support burden (fewer setup issues)

**Cons**:
- Requires PowerShell (Windows-first, but cross-platform available)
- Developers don't see generated values (unless `-SaveGenerated` used)
- Adds script complexity to maintain

**Mitigation**:
- Script includes `-SaveGenerated` flag for backup
- Clear documentation in `infra/secrets/README.md`
- Fallback to manual setup still supported

### Option 3: Vault-Based Secret Management

**Process**:
1. Setup HashiCorp Vault or cloud vault (AWS Secrets Manager, Azure Key Vault)
2. Store secrets centrally
3. Application retrieves secrets at runtime via API
4. Implement secret rotation policies

**Pros**:
- Centralized secret management
- Audit logging for secret access
- Automatic secret rotation
- Enterprise-grade security

**Cons**:
- Significant infrastructure complexity
- Requires Vault deployment and maintenance
- Overkill for development environments
- Higher operational cost
- Network dependency for secret retrieval

**Decision**: Rejected for initial setup, but considered for production deployment (future ADR)

---

## Consequences

### Positive

✅ **Developer Experience**:
- Setup time reduced from 15-30 minutes to <1 minute (for auto-generated secrets)
- Zero weak password errors
- Consistent configuration across team

✅ **Security**:
- All auto-generated secrets meet production-grade strength (256-512 bits)
- No accidental weak passwords (`password123`, `admin`, etc.)
- Cryptographically secure random generation
- Optional backup mechanism for disaster recovery

✅ **Operational**:
- Clear 3-level validation (CRITICAL → IMPORTANT → OPTIONAL)
- Application starts in degraded mode instead of failing fast
- Health check system provides runtime visibility
- Troubleshooting documentation comprehensive

✅ **Maintainability**:
- Single script to maintain (`setup-secrets.ps1`)
- Clear separation of auto-generated vs manual secrets
- Template files (`.example`) version-controlled
- Documentation co-located with scripts

### Negative

⚠️ **Platform Dependency**:
- PowerShell script (Windows-first approach)
- **Mitigation**: PowerShell Core runs on Linux/macOS, fallback to manual setup documented

⚠️ **Script Complexity**:
- 200+ lines of PowerShell code to maintain
- **Mitigation**: Well-commented code, comprehensive error handling

⚠️ **Manual Configuration Still Required**:
- 6 secrets (BGG, OpenRouter, OAuth, etc.) require external accounts
- **Mitigation**: Clear documentation in script output and README

### Risks

🟡 **Script Failure**:
- **Risk**: Setup script fails mid-execution, leaving partial configuration
- **Mitigation**: Atomic operations, clear error messages, cleanup on failure

🟡 **Secret Backup Exposure**:
- **Risk**: `.generated-values-TIMESTAMP.txt` committed to git
- **Mitigation**: Added to `.gitignore`, warning in script output

🟢 **Weak External Credentials**:
- **Risk**: Developers use weak passwords for manually-configured secrets
- **Mitigation**: Documentation emphasizes password manager usage, examples show strong passwords

---

## Implementation

### File Structure

*(blocco di codice rimosso)*

### Security Properties

**Auto-Generated Secrets**:

| Secret | Type | Strength | Generator |
|--------|------|----------|-----------|
| JWT_SECRET_KEY | Base64 (64 bytes) | 512 bits | RNGCryptoServiceProvider |
| QDRANT_API_KEY | Base64 (32 bytes) | 256 bits | RNGCryptoServiceProvider |
| EMBEDDING_SERVICE_API_KEY | Base64 (32 bytes) | 256 bits | RNGCryptoServiceProvider |
| POSTGRES_PASSWORD | Alphanumeric+Symbols (20 chars) | ~120 bits | Secure random with complexity rules |
| REDIS_PASSWORD | Alphanumeric+Symbols (20 chars) | ~120 bits | Secure random with complexity rules |
| ADMIN_PASSWORD | Alphanumeric+Symbols (16 chars) | ~95 bits | Secure random with complexity rules |

**Compliance**:
- NIST SP 800-132: Password-based key derivation (secure random)
- OWASP: Strong password requirements enforced
- PCI DSS: Sensitive data not stored in logs or git

### Deployment Workflow

**Development**:
*(blocco di codice rimosso)*

**Staging/Production**:
*(blocco di codice rimosso)*

---

## Alternatives Considered

### Alternative A: Environment Variables Only

**Description**: No secret files, all configuration via `.env` or environment variables

**Pros**:
- Aligns with 12-factor app methodology
- No file management needed
- Easy CI/CD integration

**Cons**:
- .env files often committed by mistake (git history pollution)
- Harder to validate completeness (17+ variables scattered)
- No clear priority system (all variables equal)

**Decision**: Rejected - Files provide better structure and validation

### Alternative B: Configuration Service (Spring Cloud Config style)

**Description**: Central configuration server serving secrets via HTTP API

**Pros**:
- Centralized management
- Dynamic reloading without restart
- Audit logging

**Cons**:
- Requires additional service deployment
- Network dependency
- Overkill for single-tenant application

**Decision**: Rejected - Too complex for current scale

### Alternative C: Docker Secrets

**Description**: Use Docker Swarm or Kubernetes secrets natively

**Pros**:
- Native container orchestration
- Encryption at rest
- Role-based access control

**Cons**:
- Docker Compose (dev) doesn't support Docker secrets well
- Requires orchestration platform (Swarm/K8s)
- Development friction (local setup harder)

**Decision**: Partially adopted - Production deployment uses K8s secrets, development uses files

---

## Metrics & Success Criteria

### Developer Experience Metrics

**Before (Manual Setup)**:
- Average setup time: 15-30 minutes
- Setup error rate: ~40% (typos, missing files, weak passwords)
- Support tickets: 5-10 per month (secret configuration issues)

**After (Auto-Configuration)**:
- Average setup time: <1 minute (auto-generated) + 5 minutes (manual)
- Setup error rate: <5% (only manual configuration mistakes)
- Support tickets: 1-2 per month (external service credentials)

### Security Metrics

**Password Strength**:
- Before: 30% weak passwords (8-10 chars, dictionary words)
- After: 100% strong auto-generated passwords (256-512 bits entropy)

**Secret Leakage**:
- Before: 2 incidents (`.env` committed to git)
- After: 0 incidents (`.gitignore` enforced, warnings in script)

### Operational Metrics

**Application Startup Reliability**:
- Before: 60% success rate (missing or misconfigured secrets)
- After: 95% success rate (CRITICAL validation blocks startup)

**Troubleshooting Time**:
- Before: 10-20 minutes diagnosing secret issues
- After: 2-5 minutes (clear error messages, health check endpoint)

---

## Future Enhancements

### Phase 1: Initial Implementation (Completed)
- ✅ PowerShell setup script with auto-generation
- ✅ 3-level validation system
- ✅ Health check endpoint
- ✅ Comprehensive documentation

### Phase 2: Production Hardening (Planned)
- 🔲 Secret rotation automation (90-day JWT rotation)
- 🔲 Vault integration for production environments
- 🔲 Grafana dashboard for secret health monitoring
- 🔲 Alert rules for secret expiration

### Phase 3: Enterprise Features (Future)
- 🔲 Multi-environment secret management (dev/staging/prod)
- 🔲 RBAC for secret access (team-based permissions)
- 🔲 Audit logging for secret retrieval
- 🔲 Automated secret rotation with zero-downtime

---

## References

### Documentation
- **Deployment Guide**: [docs/04-deployment/auto-configuration-guide.md](../../04-deployment/auto-configuration-guide.md)
- **Secrets Management**: [docs/04-deployment/secrets-management.md](../../04-deployment/secrets-management.md)
- **Health Check API**: [docs/03-api/health-check-api.md](../../03-api/health-check-api.md)
- **Secrets README**: [infra/secrets/README.md](../../../infra/secrets/README.md)

### Source Code
- **Setup Script**: `infra/secrets/setup-secrets.ps1`
- **Validation Logic**: `apps/api/src/Api/Program.cs` (startup validation)
- **Health Checks**: `apps/api/src/Api/Infrastructure/Health/`

### External Resources
- [12-Factor App - Config](https://12factor.net/config)
- [NIST SP 800-132 - Password-Based Key Derivation](https://csrc.nist.gov/publications/detail/sp/800-132/final)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [Microsoft Docs - Secret Management in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [HashiCorp Vault Documentation](https://www.vaultproject.io/docs)

---

**Decision Maker**: Engineering Lead
**Implementation**: Issues [#2511](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2511), [#2522](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2522)
**Status**: ✅ Production-ready (as of 2026-01-17)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-022-ssr-auth-protection.md

# ADR-022: Server-Side Rendering (SSR) Authentication Protection

**Status**: ✅ Accepted
**Date**: 2025-11-22
**Issue**: [#1611](https://github.com/meepleai/monorepo/issues/1611)
**Context**: Frontend Authentication & Authorization
**Supersedes**: Client-side useAuth() hook pattern with 'use client' components

---

## Context and Problem Statement

Protected routes (`/admin`, `/editor`, `/upload`) currently use **Client Components** (`'use client'`) with client-side authentication checks via the `useAuth()` hook. This creates several user experience and security issues:

**Problems**:
1. **UI Flash**: Unauthenticated users see protected content briefly before redirect
2. **Client-Side Auth Delay**: useAuth hook calls `/api/v1/auth/me` after page render
3. **E2E Test Incompatibility**: `useRouter()` from `next/navigation` fails in test env with "NextRouter was not mounted"
4. **Double Authorization**: Middleware redirects → Client checks → Potential double redirect
5. **Bundle Size**: useAuth hook + dependencies shipped to all users (-15-20% potential savings)

**Current Flow**:
*(blocco di codice rimosso)*

**Problem**: How do we eliminate UI flash and move auth logic server-side while maintaining role-based authorization?

---

## Decision Drivers

1. **Zero UI Flash**: Authentication must happen before render, not after
2. **Next.js 16.0.1 + React 19.2.0**: Full Server Components support in App Router
3. **App Router Architecture**: Project uses `/app` directory (Next.js 13+ pattern)
4. **E2E Test Compatibility**: Must work with HTTP-level mocking (MSW, Playwright)
5. **Middleware Enhancement**: Leverage existing cookie-based middleware
6. **SEO Benefits**: Server-rendered content with auth data pre-loaded
7. **Bundle Size Reduction**: Eliminate client-side auth logic
8. **Pages Router Removal**: Eliminate conflicting `/pages` directory

---

## Decision

Migrate protected routes from **Client Components** to **Server Components** using Next.js **App Router** pattern with async server components and `cookies()` from `next/headers`.

### Implementation Pattern

**Before** (Client Component with useAuth):
*(blocco di codice rimosso)*

**After** (Server Component + Client Component split):
*(blocco di codice rimosso)*

---

## Components Created

### 1. Server-Side Auth Utilities

**Location**: `apps/web/src/lib/auth/server.ts`

*(blocco di codice rimosso)*

### 2. Migration Pattern for Protected Pages

**Template**:
*(blocco di codice rimosso)*

### 3. Pages to Migrate (10 total)

| Page | Complexity | Roles | Priority |
|------|-----------|-------|----------|
| `/upload` | HIGH | admin, editor | 🔴 Phase 1 (POC) |
| `/editor` | HIGH | admin, editor | 🟡 Phase 2 |
| `/admin` | HIGH | admin | 🟡 Phase 2 |
| `/admin/analytics` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/bulk-export` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/cache` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/configuration` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/n8n-templates` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/prompts/**` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/users` | MEDIUM | admin | 🟢 Phase 3 |

---

## Migration Strategy

### Phase 1: POC on `/upload` (2-3 hours)
1. ✅ Remove `src/pages/` directory completely (eliminate Pages Router conflict)
2. ✅ Update `lib/auth/server.ts` for App Router pattern (cookies from next/headers)
3. ✅ Migrate `app/upload/page.tsx` from Client to Server Component
4. ✅ Create `app/upload/upload-client.tsx` for interactive logic
5. ✅ Update `useGames` hook to accept user prop
6. ✅ Update all tests to import from app/ instead of pages/
7. ✅ Validate: build, typecheck, unit tests, E2E tests pass

### Phase 2: `/editor` + `/admin` Dashboard (3-4 hours)
1. Migrate `app/editor/page.tsx` with RichTextEditor (client-side lib handling)
2. Migrate `app/admin/page.tsx` dashboard with charts
3. Test complex client-side interactions with server props

### Phase 3: `/admin/*` Subpages (8-12 hours)
1. Batch migrate 8 admin subpages
2. Remove useAuth hook entirely (dead code)
3. Update documentation and patterns

### Total Effort: 20-30 hours over 4-6 weeks

---

## Consequences

### ✅ Positive

1. **Zero UI Flash**: Auth check happens before page render
2. **Better UX**: Users immediately see correct page or redirect
3. **SEO Friendly**: Server-rendered content with auth data
4. **-15-20% Bundle Size**: Remove useAuth hook + dependencies
5. **E2E Test Compatible**: HTTP-level mocking works with server-side auth
6. **Security**: Auth logic server-side only, no client bypass
7. **Performance**: One server-side check vs client useEffect
8. **Single Router**: Eliminate Pages/App Router conflict
9. **App Router Benefits**: Layout caching, parallel routes, streaming

### ⚠️ Trade-offs

1. **Server-Side Cost**: Every page request validates session server-side
   - **Mitigation**: Backend has session caching, minimal overhead
2. **Learning Curve**: Team needs to understand Server/Client Component split
   - **Mitigation**: Pattern documented, reusable utilities
3. **Component Split**: Server component + Client component for interactivity
   - **Mitigation**: Clear separation of concerns, better architecture

### ❌ Risks Mitigated

1. **No Breaking Changes**: API contracts unchanged
2. **Middleware Compatibility**: Works alongside existing middleware
3. **Backward Compatible**: Gradual migration, no big bang
4. **Performance**: Server Components cached by Next.js

---

## Technical Details

### App Router Server Components

**Key Differences from Pages Router**:
- No `getServerSideProps` (Pages Router only)
- Async component functions (`async function Page()`)
- `cookies()` from `next/headers` for session access
- `redirect()` from `next/navigation` for server-side redirects
- Component-level data fetching, not page-level

**Server/Client Split**:
*(blocco di codice rimosso)*

---

## Validation Criteria

### Definition of Done (DoD)

1. ✅ All 10 protected pages migrated to Server Components
2. ✅ `lib/auth/server.ts` utilities updated for App Router
3. ✅ `src/pages/` directory removed (Pages Router eliminated)
4. ✅ All tests updated to import from `app/` instead of `pages/`
5. ✅ Build passes: `pnpm build` ✅
6. ✅ Typecheck passes: `pnpm typecheck` ✅
7. ✅ Unit tests pass: `pnpm test` ✅
8. ✅ E2E tests pass: `pnpm test:e2e` ✅
9. ✅ No UI flash visible in browser testing
10. ✅ Bundle size reduced by 15-20%
11. ✅ Documentation updated (App Router migration guide)
12. ✅ ADR published and reviewed

---

## References

- Next.js App Router: https://nextjs.org/docs/app/building-your-application/routing
- Server Components: https://nextjs.org/docs/app/building-your-application/rendering/server-components
- Data Fetching: https://nextjs.org/docs/app/building-your-application/data-fetching
- cookies(): https://nextjs.org/docs/app/api-reference/functions/cookies
- redirect(): https://nextjs.org/docs/app/api-reference/functions/redirect

---

**Decision Made By**: Engineering Team
**Review Status**: ✅ Approved for Implementation (App Router)
**Implementation Start**: 2025-11-22

---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-023-share-request-workflow.md

# ADR-023: Share Request Workflow Design

## Status

**Accepted** (2026-01-20)

## Context

MeepleAI necessita di un sistema per permettere agli utenti di contribuire giochi dalla loro
libreria personale al catalogo condiviso della community. Il sistema deve:

1. **Facilitare contributi di qualità** dalla community
2. **Garantire controllo qualità** attraverso revisione amministrativa
3. **Prevenire spam e abusi** attraverso rate limiting
4. **Gestire concorrenza** tra admin che revisionano contemporaneamente
5. **Incentivare partecipazione** attraverso gamification (badge system)

### Business Requirements

- **User Experience**: Processo di condivisione semplice e guidato
- **Quality Control**: Ogni contributo validato prima della pubblicazione
- **Scalability**: Gestire centinaia di richieste mensili con piccolo team admin
- **Transparency**: Utenti devono capire stato e feedback sulle loro richieste
- **Engagement**: Sistema di badge per incentivare contributi di qualità

### Technical Constraints

- .NET 9 backend con EF Core e PostgreSQL
- Domain-Driven Design (DDD) architecture
- CQRS pattern con MediatR
- Multi-bounded context (SharedGameCatalog, UserLibrary, Authentication)
- Real-time requirements per admin dashboard

## Decision

Implementiamo una **state machine workflow** con **exclusive review locking** e **tier-based rate limiting**.

### 1. State Machine Workflow

**Stati definiti** (6 stati):

*(blocco di codice rimosso)*

**Transizioni permesse**:

| From | To | Trigger | Actor |
|------|----|----|-------|
| - | Pending | Create request | User |
| Pending | InReview | Start review | Admin |
| ChangesRequested | InReview | Resubmit after changes | User → Admin |
| InReview | Approved | Approve | Admin |
| InReview | Rejected | Reject | Admin |
| InReview | ChangesRequested | Request changes | Admin |
| InReview | Pending | Release lock | Admin |
| Pending, ChangesRequested | Withdrawn | Withdraw | User |

**Invarianti**:
- Solo admin può transizionare da/a InReview
- Utente può modificare solo in stati Pending o ChangesRequested
- Stati terminali (Approved, Rejected, Withdrawn) sono immutabili

### 2. Exclusive Review Lock System

**Design**:

*(blocco di codice rimosso)*

**Regole**:
1. **Acquisizione**: Solo richieste in Pending o ChangesRequested
2. **Esclusività**: Max 1 lock attivo per richiesta
3. **Timeout**: 30 minuti default (configurabile)
4. **Auto-release**: Background job rilascia lock scaduti ogni ora
5. **Ownership**: Solo il reviewing admin può rilasciare il proprio lock

**Benefici**:
- ✅ Previene conflitti tra admin (race conditions)
- ✅ Evita lavoro duplicato
- ✅ Garantisce decisioni ponderate (timeout = thinking time)
- ✅ Recupero automatico da abbandoni (auto-release)

**Trade-offs**:
- ❌ Complessità aggiuntiva (lock management logic)
- ❌ Necessità background job per cleanup
- ❌ Possibili blocchi se admin dimentica di rilasciare (mitigato da timeout)

### 3. Tier-Based Rate Limiting

**Design**:

*(blocco di codice rimosso)*

**Override System**:
*(blocco di codice rimosso)*

**Enforcement**:
- Validazione in `CreateShareRequestCommandValidator`
- Query per conteggio richieste nel mese corrente
- Override hanno precedenza su limiti tier
- Audit completo di creazione/modifica override

**Benefici**:
- ✅ Previene spam e abusi sistemici
- ✅ Incentiva upgrade a tier superiori
- ✅ Flessibilità per eventi speciali (contest, beta test)
- ✅ Fairness (tutti i tier hanno accesso, diversa velocità)

**Trade-offs**:
- ❌ Potrebbe frustrare utenti free tier molto attivi
- ❌ Richiede gestione override manuale per eccezioni
- ❌ Logica aggiuntiva per calcolo limiti

### 4. Badge System (Gamification)

**Design**: Auto-assegnazione al evento `ShareRequestApprovedDomainEvent`

**Badge Tiers**:
*(blocco di codice rimosso)*

**Evaluation Logic**:
- Handler `BadgeEvaluationOnApprovalHandler` ascolta `ShareRequestApprovedDomainEvent`
- Calcola badge eligibility per il contributore
- Assegna nuovi badge automaticamente
- Trigger notifica `BadgeEarnedNotification`

**Benefici**:
- ✅ Incentiva contributi di qualità
- ✅ Gamification engagement
- ✅ Riconoscimento pubblico contributori
- ✅ Competizione sana (leaderboard)

---

## Consequences

### Positive

✅ **Workflow chiaro e prevedibile**: Stati ben definiti, transizioni esplicite
✅ **Concurrency safety**: Lock esclusivo previene race conditions
✅ **Quality assurance**: Ogni contributo validato da admin
✅ **Spam prevention**: Rate limiting efficace
✅ **User engagement**: Badge system incentiva partecipazione
✅ **Scalability**: Lock timeout e background jobs gestiscono abbandoni
✅ **Flexibility**: Override system per casi speciali
✅ **Auditability**: Tracking completo di decisioni e modifiche

### Negative

❌ **Complessità implementativa**: State machine + lock + rate limit = ~15 issue di sviluppo
❌ **Overhead admin**: Richiede team admin attivo per revisioni tempestive
❌ **User friction**: Rate limit potrebbe frustrare utenti free tier molto attivi
❌ **Background jobs**: Necessità infrastruttura per lock cleanup
❌ **Testing complexity**: Più stati e transizioni = più test cases

### Risks

⚠️ **Admin bottleneck**: Se volume richieste supera capacità team admin
**Mitigation**: Monitorare SLA (< 48h), espandere team se necessario, considerare auto-approval per contributori trusted

⚠️ **Lock starvation**: Admin interrotti frequentemente potrebbero non completare mai revisioni
**Mitigation**: Timeout 30min aggressivo, notifiche lock scadenza, possibilità estensione

⚠️ **Rate limit bypass**: Utenti potrebbero creare account multipli
**Mitigation**: Verifica email, detection pattern sospetti, ban account duplicati

---

## Alternatives Considered

### Alternative 1: Approval Queue senza Lock

**Design**: Richieste processate in FIFO, nessun lock esclusivo

**Pro**:
- Più semplice da implementare
- Nessuna gestione lock/timeout
- Parallelismo admin più facile

**Con**:
- ❌ Rischio race condition (2 admin approvano stessa richiesta)
- ❌ Lavoro duplicato (2 admin revisionano in parallelo)
- ❌ Necessità conflict resolution complessa

**Rejected**: Lock esclusivo è critico per data integrity e efficienza admin.

---

### Alternative 2: Auto-Approval con Community Review

**Design**: Richieste approvate automaticamente, community può flaggare problemi

**Pro**:
- Zero overhead admin
- Velocità massima per utenti
- Scalabilità illimitata

**Con**:
- ❌ Rischio spam nel catalogo
- ❌ Qualità inconsistente
- ❌ Cleanup post-pubblicazione più costoso
- ❌ Esperienza utente peggiore se molti flag

**Rejected**: Quality-first approach richiede approval admin preventiva.

---

### Alternative 3: Sistema di Voto Community

**Design**: Community vota su richieste pendenti, threshold → auto-approval

**Pro**:
- Engagement community massimo
- Decisioni democratiche
- Scalabilità migliore

**Con**:
- ❌ Troppo complesso per MVP
- ❌ Rischio vote brigading
- ❌ Slow approval times (attesa voti)
- ❌ Quality non garantita (voto ≠ expertise)

**Rejected**: Troppo complesso per Phase 7. Possibile futuro enhancement.

---

### Alternative 4: Soft Lock (Advisory, Non-Exclusive)

**Design**: Lock advisory che suggerisce chi sta revisionando ma non blocca altri admin

**Pro**:
- Flessibilità maggiore
- Nessun problema di lock scaduti
- Admin possono collaborare su richieste complesse

**Con**:
- ❌ Possibili race conditions
- ❌ Lavoro duplicato comunque possibile
- ❌ Conflitti da risolvere manualmente

**Rejected**: Exclusive lock fornisce garanzie più forti, complessità gestibile.

---

## Implementation Notes

### Domain Events

*(blocco di codice rimosso)*

### Background Jobs

**LockCleanupJob** (Hangfire, ogni ora):
*(blocco di codice rimosso)*

### Bounded Context Interactions

**SharedGameCatalog** (owner):
- ShareRequest aggregate
- Review lock management
- Badge evaluation

**UserLibrary** (dependency):
- Source game data (read-only)
- Validation: game exists in user library

**Authentication** (dependency):
- User identity and tier information
- Admin role verification

**UserNotifications** (subscriber):
- Email notifications per state changes
- Badge earned celebrations

---

## Related Decisions

- **ADR-001**: Domain-Driven Design Architecture
- **ADR-003**: CQRS Pattern with MediatR
- **ADR-007**: Event-Driven Notifications
- **ADR-012**: Rate Limiting Strategy

## Future Enhancements

**Considered for future phases**:

1. **Trusted Contributors Auto-Approval** (Post-MVP)
   - Utenti con track record eccellente (es. 20 approved, 0 rejected, 95% approval rate)
   - Skip admin review, pubblicazione immediata
   - Audit trail per review post-pubblicazione

2. **Community Voting System** (Phase 8+)
   - Community vota su richieste pendenti
   - Threshold di voti → fast-track review
   - Non sostituisce admin approval, ma prioritizza queue

3. **AI-Assisted Pre-Screening** (Future)
   - ML model pre-valida qualità descrizione
   - Auto-detect duplicati tramite embedding similarity
   - Suggerimenti miglioramento automatici all'utente

4. **Collaborative Review** (Future)
   - Multiple admin possono collaborare su richieste complesse
   - Shared lock con co-ownership
   - Chat interno per discussione casi edge

---

**Date**: 2026-01-20
**Authors**: Team MeepleAI
**Reviewers**: Architecture Team
**Status**: Implemented (Epic #2718)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-024-advanced-pdf-embedding-pipeline.md

# ADR-024: Advanced PDF Embedding Pipeline with Hybrid Indexing

**Status**: Accepted
**Date**: 2025-12-03
**Deciders**: Engineering Lead, ML Engineer
**Context**: Issue #1901 - Enhanced RAG Pipeline
**Related**: ADR-001 (Hybrid RAG), ADR-003b (Unstructured PDF)

---

## Context

MeepleAI's current RAG pipeline achieves baseline accuracy but requires enhancement for complex board game rulebooks with:
- Tables (game setup, component counts, scoring)
- Multi-column layouts (quick reference cards)
- Hierarchical structure (phases, rounds, turns)
- Cross-references ("see Combat section on page 12")

**Current State** (from ADR-001, ADR-003b):
- PDF extraction: 3-stage fallback (Unstructured → SmolDocling → Docnet)
- Chunking: Basic sentence-based (TextChunkingService)
- Embedding: OpenRouter text-embedding-3-small
- Retrieval: Hybrid search (Qdrant vector + PG FTS), RRF fusion (70/30)
- Validation: 5-layer (confidence, multi-model, citation, keywords, feedback)

**Gap Analysis**:
| Component | Current | Target | Gap |
|-----------|---------|--------|-----|
| Chunking | Sentence-based | Hierarchical parent/child | Missing structure awareness |
| Embedding | 1536-dim, short context | 3072-dim, 8K context | Context loss on long passages |
| Indexing | HNSW default | HNSW + PQ + sparse BM25 | No quantization, basic FTS |
| Reranking | None | Cross-encoder top-K | Missing relevance refinement |
| Evaluation | Ad-hoc | Systematic Recall@K, nDCG | No benchmark framework |

---

## Decision

Implement **5-Phase Advanced PDF Embedding Pipeline**:

### Architecture Overview

*(blocco di codice rimosso)*

---

## Implementation Phases

### Phase 0: Dataset Preparation (Prerequisite)

**Objective**: Create evaluation framework with dual dataset strategy

**Deliverables**:
1. **Mozilla Structured QA**: Board game subset (~20-30 questions)
2. **Custom MeepleAI Dataset**: 30-45 Q&A pairs across 3 rulebooks (simple/medium/complex)
3. **Evaluation Harness**: Recall@5/10, nDCG@10, MRR, LLM-as-judge correctness

**Validation Gate**: ≥30 Q&A pairs ready, baseline metrics documented

**Implementation Reference**: See `tests/evaluation-datasets/` for dataset files, `RagEvaluationService.cs` for harness implementation

---

### Phase 1: Advanced Chunking (Sentence-Based Baseline + Parent/Child)

**Objective**: Implement hierarchical chunking with parent/child mapping

**Strategy Selection**: Sentence-based first (user choice)

**Chunking Configurations**:

| Config | Chunk Size | Overlap | Use Case |
|--------|------------|---------|----------|
| **Baseline** | 350 tokens | 15% (52 tokens) | General rulebooks |
| **Dense** | 200 tokens | 20% (40 tokens) | Complex tables/lists |
| **Sparse** | 500 tokens | 10% (50 tokens) | Narrative sections |

**Parent/Child Architecture**:
- `HierarchicalChunk`: ID, ParentID, ChildIDs, Content, Level (0=section, 1=paragraph, 2=sentence)
- `ChunkMetadata`: Page, Heading, BoundingBox, ElementType (text/table/list), GameID, DocumentID

**Implementation Strategy**:
1. Section detection from Unstructured metadata headings
2. Parent chunk creation per section
3. Child chunks via sentence-based splitting
4. Bidirectional parent/child ID mapping

**Validation Gate**: Unit tests pass, parent/child relationships correct

**Test Reference**: See `AdvancedChunkingServiceTests.cs` for parent/child mapping validation

**Implementation Files**: See `AdvancedChunkingService.cs`, `HierarchicalChunk.cs`, `ChunkRepository.cs` in KnowledgeBase bounded context

---

### Phase 2: Multi-Provider Embedding

**Objective**: Flexible embedding with long-context support

**Provider Matrix**:

| Provider | Model | Dimensions | Context | Use Case | Cost |
|----------|-------|------------|---------|----------|------|
| **OpenRouter** | text-embedding-3-large | 3072 | 8K | Production | $0.13/M tokens |
| **OpenRouter** | text-embedding-3-small | 1536 | 8K | Budget | $0.02/M tokens |
| **Ollama** | nomic-embed-text | 768 | 8K | Local dev | Free |
| **Ollama** | mxbai-embed-large | 1024 | 512 | Fallback | Free |
| **HuggingFace** | BGE-M3 | 1024 | 8K | Multilingual | Free |

**Multi-Provider Architecture**:
- `IEmbeddingProvider`: Abstract interface with ProviderName, Dimensions, MaxContextTokens, GenerateEmbedding methods
- `EmbeddingProviderFactory`: Factory pattern for provider instantiation (OpenRouter, Ollama, HuggingFace)

**Configuration Strategy**:
- Primary provider: OpenRouterLarge (production)
- Fallback provider: OllamaNomic (dev/offline)
- Batch size: 100, max retries: 3, timeout: 30s

**Validation Gate**: Integration tests pass for all providers

**Test Reference**: See `EmbeddingProviderTests.cs` for provider-specific tests, `EmbeddingProviderFactoryTests.cs` for factory pattern validation

**Implementation Files**: See `Infrastructure/Embedding/` directory for provider implementations

---

### Phase 3: Optimized Hybrid Indexing

**Objective**: Tune HNSW, add PQ quantization, optimize BM25 sparse index

**Qdrant HNSW Configuration**:
- Vector size: 3072, distance: Cosine
- HNSW: m=16, ef_construct=100, full_scan_threshold=10000
- Quantization: Scalar int8 (75% memory reduction, <1% accuracy loss) for medium collections (100K-1M chunks)

**PostgreSQL BM25 Index**:
- Custom text search config: `meepleai_italian` with Italian stemming + synonyms
- GIN index on `to_tsvector('meepleai_italian', content)`
- Optional: Materialized view for pre-computed BM25 scores

**Metadata Payload**: page, heading, element_type, game_id, document_id

**Validation Gate**: Benchmark baseline vs optimized (latency, storage)

**Configuration Reference**: See `qdrant-collections.json` and migration `AddChunksFtsIndex.sql`

---

### Phase 4: Cross-Encoder Reranking Pipeline

**Objective**: Add reranking layer for improved relevance

**Reranker Options**:

| Model | Type | Accuracy | Latency | Memory |
|-------|------|----------|---------|--------|
| **BGE-reranker-v2-m3** | Cross-encoder | Best | ~50ms/pair | 1.5GB |
| **ms-marco-MiniLM-L6** | Cross-encoder | Good | ~20ms/pair | 400MB |
| **ColBERT-v2** | Late-interaction | Good | ~10ms/pair | 800MB |

**Recommended**: BGE-reranker-v2-m3 (best accuracy, acceptable latency)

**Reranking Pipeline**:
1. **Hybrid Retrieval**: Vector (40 results) + BM25 (20 results)
2. **RRF Fusion**: Combine to top 50 candidates (k=60)
3. **Cross-Encoder Reranking**: BGE-reranker-v2-m3 scores top 50 → select top 10
4. **Parent Expansion**: Retrieve full context for final chunks

**Python Reranker Service**:
- Model: `sentence-transformers` CrossEncoder (BAAI/bge-reranker-v2-m3)
- API: HTTP endpoint `/rerank` with JSON request/response
- Integration: C# `CrossEncoderRerankerClient` via HttpClient

**Validation Gate**: Recall@10 ≥60% on evaluation dataset

**Test Reference**: See `RerankedRetrievalServiceTests.cs` for pipeline integration, `CrossEncoderRerankerTests.cs` for Python service interaction

---

### Phase 5: Evaluation & Documentation

**Objective**: Systematic benchmarking and documentation

**Evaluation Matrix**:

| Metric | Baseline | Target | Method |
|--------|----------|--------|--------|
| Recall@5 | TBD | ≥60% | Top-5 contains correct chunk |
| Recall@10 | TBD | ≥70% | Top-10 contains correct chunk |
| nDCG@10 | TBD | ≥0.65 | Graded relevance ranking |
| MRR | TBD | ≥0.55 | Mean reciprocal rank |
| P95 Latency | ~2s | <1.5s | End-to-end query time |
| Storage/page | TBD | Documented | Bytes per PDF page |

**Grid Search Configurations**:
- Chunking: dense (200/20%), baseline (350/15%), sparse (500/10%)
- Quantization: none, scalar_int8
- Reranking: disabled, BGE-reranker-v2-m3

**Evaluation Outputs**:
- Benchmark reports with configuration matrix
- Prometheus metrics: retrieval_latency_ms, rerank_latency_ms, chunks_retrieved
- Retrieval trace logging for debugging
- PII redaction pre-embedding

**Report Template Reference**: See `docs/evaluation-reports/` for completed benchmarks

**Validation Gate**: Report with ≥3 configurations compared, Recall@10 ≥70%

---

## Consequences

### Positive

**Improved Retrieval Quality**:
- Parent/child chunking preserves context
- Reranking refines relevance (expected +10-15% Recall)
- Long-context embedding captures cross-references

**Flexibility**:
- Multi-provider embedding (cost vs quality tradeoff)
- Configurable chunking strategies per document type
- Quantization options for scaling

**Measurability**:
- Systematic evaluation framework
- Baseline → improvement tracking
- Data-driven optimization

### Negative (Trade-offs Accepted)

**Increased Latency** (+200-500ms for reranking):
- Mitigation: See Latency Optimization Strategy below

**Complexity** (+40% codebase in KnowledgeBase):
- Mitigation: Modular services, comprehensive tests, clear documentation

**Infrastructure**:
- Python service for reranking (containerized)
- Ollama for local embedding (already in docker-compose)

**Cost Increase** (embedding model upgrade):
- text-embedding-3-small → text-embedding-3-large = 6.5x cost increase
- Mitigation: Use large model for production, small/Ollama for dev
- Volume estimate: ~10K queries/month × $0.13/M tokens ≈ $1.30/month (negligible)
- ROI: +10-15% accuracy improvement justifies minimal cost increase

---

## Latency Optimization Strategy

### Problem
Adding reranking risks exceeding P95 <1.5s target:

| Component | Sequential Time |
|-----------|----------------|
| Query embedding | ~50ms |
| Vector search (40) | ~50ms |
| BM25 search (20) | ~30ms |
| RRF fusion | ~10ms |
| Reranking (50 docs) | ~500ms |
| Parent expansion | ~50ms |
| LLM generation | ~800ms |
| **Total** | **~1,490ms** |

### Optimization Strategies

**1. Parallel Execution**: Run vector and BM25 searches concurrently (saves ~30ms)

**2. Reranker Result Caching**: Cache query+chunk scores for 5 minutes (30-50% hit rate expected)

**3. Adaptive Reranking**: Skip reranking when top candidate score ≥0.90 (high confidence)

**4. Batched Parent Expansion**: Single DB query for all parent chunks (prevents N+1 queries)

**Implementation Reference**: See `LatencyOptimizationTests.cs` for parallel execution benchmarks

### Optimized Latency Budget

| Component | Sequential | Optimized |
|-----------|------------|-----------|
| Query embedding | 50ms | 50ms |
| Vector + BM25 search | 80ms | **50ms** (parallel) |
| RRF fusion | 10ms | 10ms |
| Reranking | 500ms | **300ms** (40% cached) |
| Parent expansion | 50ms | **20ms** (batched) |
| LLM generation | 800ms | 800ms |
| **Total** | **1,490ms** | **1,230ms** ✅ |

### Feature Flags
*(blocco di codice rimosso)*

---

## Interface Contracts

### Core Interfaces

**IParentChunkResolver**:
- `ExpandToParentsAsync()`: Expands child chunks to include parent context
- `GetParentAsync()`: Retrieves parent chunk by child ID

**ICrossEncoderReranker**:
- `RerankAsync()`: Reranks chunks using cross-encoder model
- `ScoreAsync()`: Scores single query-chunk pair
- `IsHealthyAsync()`: Health check for reranker service

**IChunkRepository**:
- CRUD operations: `GetByIdAsync()`, `GetByIdsAsync()`, `SaveAsync()`, `SaveBatchAsync()`
- Navigation: `GetChildrenAsync()`, `DeleteByDocumentIdAsync()`

**IRagEvaluationService**:
- `EvaluateAsync()`: Runs benchmarks on evaluation dataset
- `ComputeMetricsAsync()`: Calculates Recall@K, nDCG, MRR

**IRerankedRetrievalService**:
- `RetrieveAndRerankAsync()`: Orchestrates hybrid search + reranking + parent expansion

### Key Data Structures

**RankedChunk**: Id, Content, Score, RerankScore, ParentId, Metadata

**RetrievalOptions**: VectorCount (40), Bm25Count (20), RerankPoolSize (50), FinalCount (10), EnableReranking, ExpandToParents

**EvaluationResult**: RecallAt5, RecallAt10, NdcgAt10, Mrr, P95LatencyMs, Configuration

**Contract Reference**: See interface files in `BoundedContexts/KnowledgeBase/Application/Interfaces/`

---

## Error Handling Matrix

| Failure Scenario | Detection | Fallback Behavior | Logging |
|-----------------|-----------|-------------------|---------|
| **Embedding provider timeout** | `HttpRequestException` / `TaskCanceledException` | Use `FallbackProvider` from config | `Warning` + switch metric |
| **Embedding provider rate limit** | HTTP 429 | Exponential backoff (3 retries) → fallback | `Warning` + retry count |
| **Reranker service unavailable** | Health check fail / connection refused | Skip reranking, use RRF scores only | `Warning` + feature flag |
| **Reranker timeout** | `TaskCanceledException` after 5s | Return unranked results | `Warning` + latency |
| **Parent chunk not found** | Null result from repository | Return child chunk with `ParentMissing` flag | `Warning` + chunk ID |
| **Vector search failure** | Qdrant connection error | Fall back to BM25-only search | `Error` + degrade metric |
| **BM25 search failure** | PostgreSQL error | Fall back to vector-only search | `Error` + degrade metric |
| **Both searches fail** | Both above | Return empty results + user message | `Critical` + alert |
| **Quality below threshold** | Score < 0.60 after reranking | Add disclaimer to response | `Info` + low_confidence metric |
| **Empty retrieval results** | Zero chunks returned | Return "information not found" message | `Warning` + query logged |

### Error Handling Strategy

**Resilience Pattern**:
1. Hybrid search failure → Fallback to single-mode search (vector-only or BM25-only)
2. Reranker unavailable → Skip reranking, use RRF scores (set `RerankScore = null`)
3. Parent chunk missing → Return child with `ParentMissing` flag
4. Empty results → Log warning, return empty list with user message

**Graceful Degradation**: Service continues with reduced functionality rather than failing completely

**Error Handling Reference**: See `ResilientRetrievalServiceTests.cs` for failure scenarios

---

## Service Relationships

### Integration Architecture

*(blocco di codice rimosso)*

### Feature Flag Integration

**Strategy Selection**:
- `EnableAdvancedRetrieval = true`: Use reranked retrieval path
- `EnableAdvancedRetrieval = false`: Use basic hybrid search (baseline)

**Backward Compatibility**: Existing `RagService` routes to either pipeline based on configuration flag, no breaking changes

**Feature Flag Reference**: See `appsettings.json` Retrieval section

### Backward Compatibility

| Component | Breaking Change | Migration |
|-----------|-----------------|-----------|
| `RagService` | No | Feature flag controls new path |
| `TextChunkingService` | No | `AdvancedChunkingService` wraps it |
| `IHybridSearchService` | No | Reused by new services |
| Qdrant schema | No | New fields are optional (nullable) |
| PostgreSQL schema | No | New tables, no changes to existing |

---

## Stack Decision: Hybrid Internal + Libraries

Based on brainstorming analysis:

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Orchestration | Internal (`RagService`) | Full control, existing patterns |
| Chunking | LangChain `text_splitters` | Mature, well-tested |
| Embedding | Internal multi-provider | Matches existing OpenRouter pattern |
| Reranking | `sentence-transformers` | Industry standard, MIT license |
| Evaluation | `ragas` + custom | Standard metrics + MeepleAI-specific |

**Cost**: All components are open source (MIT/Apache 2.0)

---

## Alternatives Considered

### Alternative 1: Full LlamaIndex Integration
**Pros**: Batteries-included RAG framework
**Cons**: Lock-in, less control, learning curve
**Decision**: Rejected - prefer surgical library adoption

### Alternative 2: ColBERT Late Interaction
**Pros**: Faster reranking, token-level matching
**Cons**: Complex indexing, higher storage
**Decision**: Deferred - evaluate if BGE insufficient

### Alternative 3: Proposition Chunking
**Pros**: Atomic facts, better for complex reasoning
**Cons**: Requires LLM pass, expensive at scale
**Decision**: Phase 6 if baseline insufficient

---

## Success Criteria

**Phase Completion**:
- [ ] Phase 0: ≥30 Q&A pairs, baseline documented
- [ ] Phase 1: Chunking tests pass, parent/child working
- [ ] Phase 2: All embedding providers operational
- [ ] Phase 3: Index benchmarks documented
- [ ] Phase 4: Recall@10 ≥60%
- [ ] Phase 5: Final Recall@10 ≥70%, P95 <1.5s

**Acceptance Criteria (from Issue #1901)**:
- [ ] Parse 3 PDF types with layout metadata
- [ ] 70% Q&A accuracy on test set
- [ ] Compare ≥3 chunking configs
- [ ] P95 <1.5s on 10 queries

---

## References

- Issue #1901: Original requirements
- ADR-001: Hybrid RAG foundation
- ADR-003b: PDF extraction pipeline
- Mozilla Structured QA: https://github.com/mozilla-ai/structured-qa
- Open RAG Benchmark: https://huggingface.co/datasets/vectara/open_ragbench
- BGE Reranker: https://huggingface.co/BAAI/bge-reranker-v2-m3

---

**ADR Metadata**:
- **ID**: ADR-016
- **Status**: Proposed
- **Date**: 2025-12-03
- **Supersedes**: None
- **Related**: ADR-001, ADR-003b


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-025-shared-catalog-bounded-context.md

# ADR-025: SharedGameCatalog Bounded Context Separation

**Status**: Accepted
**Date**: 2026-01-14
**Deciders**: Development Team
**Issue**: #2425 (Parent: #2374 Phase 5)

---

## Context

MeepleAI manages both a **community-shared game catalog** (SharedGameCatalog) and **personal game collections** (GameManagement). These two concerns have different:
- **Ownership models**: Community vs individual
- **Authorization requirements**: Admin/Editor vs User
- **Data lifecycles**: Shared catalog is permanent, personal collections are ephemeral
- **Scalability needs**: Catalog serves all users, collections are user-specific

We needed to decide whether to combine these into a single GameManagement context or separate them.

---

## Decision

**We separate SharedGameCatalog as an independent bounded context** with:
- Dedicated database tables (`shared_games`, `game_categories`, `game_mechanics`, etc.)
- Separate CQRS operations (20 commands/queries)
- Independent authorization policies (AdminOnlyPolicy, AdminOrEditorPolicy)
- Distinct API endpoints (`/api/v1/shared-games`, `/api/v1/admin/shared-games`)

---

## Rationale

### 1. Single Responsibility Principle
- **SharedGameCatalog**: Community governance, catalog maintenance, BGG integration
- **GameManagement**: Personal collections, play sessions, user-specific rules

Mixing these concerns violates SRP and creates cognitive overload.

### 2. Authorization Boundaries
- **Catalog**: Role-based (Admin can publish, Editor can create/edit, User can read)
- **Personal**: User-owned (each user manages only their games)

Separate contexts allow distinct authorization models without complex if/else logic.

### 3. Independent Scaling
- **Catalog**: Read-heavy (thousands of searches/day), benefits from aggressive caching
- **Personal**: Write-heavy (users adding games), less cacheable

Separation enables targeted optimization strategies.

### 4. Clear Domain Boundaries
- **Catalog**: Source of truth for verified game data (BGG imports, community edits)
- **Personal**: Links to catalog via `SharedGameId` FK for enriched data

This creates a clean dependency: Personal → Catalog (unidirectional).

### 5. Future Multi-Tenancy
Separate contexts prepare for potential future where:
- Different organizations maintain their own catalogs
- Personal collections can link to multiple catalog sources
- Catalog can be deployed independently (microservices)

---

## Alternatives Considered

### Alternative 1: Single GameManagement Context
**Rejected**: Mixing community catalog with personal collections violates domain boundaries.

**Problems**:
- Authorization complexity (role-based AND ownership-based in same context)
- Unclear ownership (who can edit? admin? user? depends on entity type)
- Coupled scaling (can't optimize catalog independently)
- Namespace pollution (SharedGame vs Game confusion)

### Alternative 2: Shared Kernel Pattern
**Rejected**: Game entities are not value objects suitable for shared kernels.

**Problems**:
- Shared kernel should contain only value objects and domain primitives
- Game is an aggregate root with complex lifecycle
- Updates to shared kernel affect both contexts (high coupling risk)

---

## Consequences

### Positive
- ✅ Clear ownership and authorization boundaries
- ✅ Independent optimization strategies (cache TTLs, indexes)
- ✅ Reduced coupling between community and personal features
- ✅ Easier to reason about and maintain
- ✅ Supports future multi-tenancy or microservices architecture

### Negative
- ❌ Data duplication (SharedGame + Game entities store similar fields)
- ❌ Additional complexity (2 repositories, 2 sets of endpoints)
- ❌ Sync overhead (if catalog game updated, personal games may be stale)

### Mitigation Strategies
- **Data Duplication**: Accept as trade-off for bounded context independence
- **Sync Overhead**: Personal games link via `SharedGameId` FK, can refresh on-demand
- **Complexity**: DDD layer structure (Domain/Application/Infrastructure) organizes code clearly

---

## Compliance

This decision aligns with:
- **DDD Principles**: Each bounded context has clear ubiquitous language
- **SOLID**: Single Responsibility (catalog ≠ personal collection)
- **Microservices Ready**: Can extract SharedGameCatalog to separate service if needed

---

## References

- Issue #2370: SharedGameCatalog Phase 1 (Backend Foundation)
- Issue #2372: SharedGameCatalog Phase 3 (Frontend Admin UI)
- Issue #2373: SharedGameCatalog Phase 4 (User-Facing Features)
- Domain-Driven Design (Eric Evans, 2003)
- Implementing Domain-Driven Design (Vaughn Vernon, 2013)


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-026-document-collections.md

# ADR-026: Document Collections for Multi-Document Support

**Status**: Accepted
**Date**: 2025-12-12
**Issue**: #2051
**Decision Makers**: Engineering Team

---

## Context

Players often use multiple PDF documents per game (base rulebook + expansions + errata + house rules). The original system supported only 1:1 Game-to-PDF relationship, forcing users to manually merge documents.

**Problem**: No support for:
- Multiple documents per game (base + expansions)
- Document type classification (base vs expansion vs errata)
- Citation priority (which document to cite when content overlaps)
- Source filtering in chat (ask questions about specific expansions)

---

## Decision

Implement **DocumentCollection** aggregate root following DDD/CQRS patterns.

### Core Design

**DocumentCollection Aggregate:**
*(blocco di codice rimosso)*

**DocumentType Value Object:**
- Types: `base` (0), `expansion` (1), `errata` (2), `homerule` (3)
- Priority for citations: homerule > (errata/expansion by date) > base

**Citation Priority Logic:**
1. **Homerule first** (priority 3) - user customizations trump all
2. **Date-based for errata/expansion** (priority 2/1) - newest wins
3. **Base last** (priority 0) - fallback to core rules

---

## Alternatives Considered

### Option A: Minimal DB Changes (Rejected)
- **Approach**: Just add `document_type` column + junction table
- **Pro**: Faster implementation (3-4 days)
- **Con**: Application layer de-duplication, limited scalability
- **Rejection Reason**: User preferred rich domain model for long-term value

### Option B: DocumentCollection Aggregate (Chosen)
- **Approach**: Full DDD aggregate with collection semantics
- **Pro**: Scalable, business-aligned, domain-driven
- **Con**: More complex (5-7 days implementation)
- **Selection Reason**: Better long-term architecture, supports future features (versioning, comparisons)

---

## Consequences

### Positive
✅ **Rich domain model** - Collection business logic in aggregate
✅ **Scalable** - Easy to add features (versioning, compare rulebook versions)
✅ **Citation control** - Priority service handles overlapping content
✅ **DDD alignment** - Follows project's 100% DDD completion
✅ **Type safety** - DocumentType/CollectionName value objects enforce constraints

### Negative
⚠️ **Migration complexity** - All existing PDFs auto-assigned to default collections
⚠️ **More entities** - Additional testing/maintenance burden
⚠️ **Learning curve** - Developers must understand collection semantics

### Neutral
📊 **Performance**: Minimal impact (indexed FK, JSON for collection docs)
📦 **Storage**: +2 tables, ~200 bytes per collection

---

## Implementation Details

### Database Schema

**document_collections:**
*(blocco di codice rimosso)*

**pdf_documents (modified):**
*(blocco di codice rimosso)*

**chat_thread_collections (junction):**
*(blocco di codice rimosso)*

### Data Migration Strategy

**Phase 1**: Create default collection for each game with PDFs
**Phase 2**: Assign all existing PDFs to game's collection as `document_type='base'`

*(blocco di codice rimosso)*

---

## API Surface

**New Endpoints:**
*(blocco di codice rimosso)*

**Modified Endpoints:**
*(blocco di codice rimosso)*

---

## Testing Strategy

**Unit Tests** (56 tests total):
- DocumentType: Validation, priority, equality (18 tests)
- CollectionName: Length validation, trimming (12 tests)
- DocumentCollection: Add/remove, max docs, ordering (16 tests)
- CitationPriorityService: Priority ordering, de-duplication (10 tests)

**Integration Tests** (planned):
- Repository CRUD operations
- CQRS handler workflows
- Migration data integrity

**E2E Tests** (planned):
- Upload base + expansion → verify both in collection
- Filter by expansion only → verify citations only from expansion
- Citation priority → homerule appears first

---

## Rollback Plan

**If issues arise:**
1. **Database**: Migration has `Down()` method - rolls back cleanly
2. **Application**: Feature toggle to disable collection endpoints
3. **Frontend**: Components isolated - easy to remove
4. **Data**: Default collections for all games - no data loss

**Rollback Cost**: ~2 hours (run migration rollback, remove endpoints)

---

## Future Enhancements

**Enabled by this architecture:**
- 📚 **Rulebook versioning** - Track different editions of same document
- 🔄 **Compare versions** - Show rule changes between editions
- 📊 **Collection analytics** - Most-used expansions, popular combinations
- 🎯 **Smart recommendations** - Suggest relevant expansions based on questions
- 🔗 **Cross-references** - Link related rules across documents

---

## References

- Issue #2051: Multi-document upload requirements
- CLAUDE.md: DDD architecture, CQRS patterns
- ADR-001: Hybrid RAG (impacted by multi-doc citations)
- Pattern: Existing PDF upload workflow (UploadPdfCommandHandler)

---

**Approved**: 2025-12-12
**Implemented**: feature/issue-2051-multi-document-upload branch


---



<div style="page-break-before: always;"></div>

## architecture/adr/adr-042-dashboard-performance.md

# ADR-042: Dashboard Performance Architecture

## Status

**Accepted** (2026-02-09)

## Context

The Dashboard Hub (Epic #3901) is the primary entry point for admin users, aggregating data from multiple bounded contexts (Administration, GameManagement, UserLibrary, SessionTracking, KnowledgeBase). Performance is critical as this page loads on every admin login and is polled every 30 seconds for real-time updates.

Issue #3981 tracks the formal measurement and validation of performance targets established in Epic #3901.

## Decision

### Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Cached API response (p99) | < 500ms | Testcontainers + Stopwatch |
| Uncached API response | < 2s | Testcontainers + Stopwatch |
| Cache hit rate | > 80% | Redis metrics in test |
| Lighthouse Performance | > 90 | Lighthouse CI (3-run avg) |
| Lighthouse Accessibility | > 95 | Lighthouse CI |
| LCP (Largest Contentful Paint) | < 2.5s | Lighthouse CI |
| FID (First Input Delay) | < 100ms | Lighthouse CI |
| CLS (Cumulative Layout Shift) | < 0.1 | Lighthouse CI |

### Caching Strategy

**Backend (HybridCache)**:
- **Distributed cache**: Redis with 5-minute TTL per user (`dashboard:{userId}`)
- **Local cache**: In-memory with 1-minute TTL (reduces Redis round-trips)
- **Invalidation**: Tag-based (`["dashboard-api", "user:{userId}"]`), triggered by `DashboardCacheInvalidationEventHandler` on config changes
- **Compression**: Disabled (`DisableCompression`) for latency optimization

**Frontend (TanStack Query)**:
- **Stale time**: 25 seconds (data considered fresh)
- **Polling interval**: 30 seconds (real-time updates)
- **GC time**: 5 minutes (cached data retention)
- **Stale-while-revalidate**: Serves cached data immediately, refetches in background

### Query Execution

The `GetDashboardQueryHandler` executes 6 data-fetching tasks in parallel using `Task.WhenAll`:
1. Library stats (game count, favorites)
2. Recent activity (last 7 days)
3. Session stats (active sessions)
4. AI usage stats (chat count, agent interactions)
5. System health (uptime, error rate)
6. User preferences

This parallel execution is key to meeting the <2s uncached target.

### SSE Streaming

Real-time dashboard updates use Server-Sent Events (SSE) via `/api/v1/dashboard/stream`:
- 30-second heartbeat interval
- Redis pub/sub for cross-instance delivery
- Automatic reconnection on client disconnect

## Testing Strategy

### Backend Performance Tests
- **File**: `tests/Api.Tests/.../Performance/DashboardEndpointPerformanceTests.cs`
- **Infrastructure**: Testcontainers (PostgreSQL pgvector:pg16, Redis 7.4-alpine)
- **Metrics**: Cached p99 latency, uncached max latency, cache hit rate
- **Execution**: Excluded from CI (`[Trait("Skip", "CI")]`), run in dedicated performance suite

### Frontend Tests
- **Unit**: `dashboard-client.test.tsx` (render performance, loading/error states)
- **Integration**: `dashboard-api-integration.test.tsx` (MSW mocks, TanStack Query behavior, polling)
- **E2E**: 7+ Playwright test files covering performance, accessibility, security, user journeys

### Lighthouse CI
- **Desktop config**: `lighthouserc.json` (3 runs, desktop preset)
- **Mobile config**: `lighthouserc.mobile.json` (3 runs, mobile throttling)
- **URLs audited**: `/admin/dashboard`, `/dashboard`, `/library`, `/shared-games`, `/settings`, `/admin/users`, `/editor`

## Consequences

### Positive
- Sub-second cached responses provide responsive admin experience
- Parallel query execution maximizes throughput
- Multi-layer caching reduces database load
- Comprehensive test coverage prevents performance regressions

### Negative
- Cache invalidation adds complexity to data modification flows
- Testcontainers-based performance tests require Docker and are slower to run
- HybridCache dependency adds infrastructure requirement (Redis)

### Risks
- Cache stampede on cold start (mitigated by staggered TTLs)
- Redis unavailability degrades to uncached responses (graceful fallback)

## References

- Epic #3901: Dashboard Hub Core MVP
- Issue #3907: Dashboard API Implementation
- Issue #3909: Cache Strategy
- Issue #3915: E2E Test Suite
- Issue #3981: Performance Measurement (this ADR)


---



<div style="page-break-before: always;"></div>

## architecture/components/agent-lightning/architecture.md

# Agent Lightning Technical Architecture

## System Overview

Agent Lightning decouples agent execution from model training, enabling RL-based optimization for any agent framework without code modifications.

## Core Components

### 1. Training Server (Lightning Server)

**Purpose**: Manages training infrastructure and model optimization

**Components**:
*(blocco di codice rimosso)*

**Technology Stack**:
- **Inference**: vLLM 0.9.2 (GPU-accelerated)
- **Training**: VERL 0.5.0 (Reinforcement Learning)
- **Compute**: PyTorch 2.7.0 + FlashAttention
- **Ray**: Distributed computing framework

**API Endpoints**:
*(blocco di codice rimosso)*

### 2. Lightning Client (Agent Runtime)

**Purpose**: Wraps agents and collects training data

**Components**:
*(blocco di codice rimosso)*

**Supported Agent Frameworks**:
- LangChain / LangGraph
- OpenAI Agents SDK
- AutoGen
- CrewAI
- Microsoft Agent Framework (Python)
- Raw OpenAI API calls

### 3. MDP Formulation

Agent Lightning converts agent executions into Markov Decision Process (MDP) tuples for RL:

*(blocco di codice rimosso)*

**Trajectory Format**:
*(blocco di codice rimosso)*

## Training Flow

### 1. Initialization Phase

*(blocco di codice rimosso)*

### 2. Training Loop

*(blocco di codice rimosso)*

### 3. Rollout Phase (Parallel)

*(blocco di codice rimosso)*

## Algorithms

### GRPO (Group Relative Policy Optimization)

Agent Lightning's default RL algorithm:

*(blocco di codice rimosso)*

**Advantages**:
- No reference model needed (vs PPO)
- Faster convergence
- Better for sparse rewards
- Works well with multi-turn agents

### APO (Automatic Prompt Optimization)

Alternative algorithm for prompt tuning without model training:

*(blocco di codice rimosso)*

## Integration Patterns

### Pattern 1: External Training, Deploy Prompts

*(blocco di codice rimosso)*

**Artifacts**:
- Optimized system prompts
- Few-shot examples
- Response templates

**Deployment**:
*(blocco di codice rimosso)*

### Pattern 2: External Training, Deploy Model

*(blocco di codice rimosso)*

**Configuration**:
*(blocco di codice rimosso)*

### Pattern 3: Continuous Learning (Advanced)

*(blocco di codice rimosso)*

## Performance Characteristics

### Training Performance

**Hardware Requirements** (Minimum):
- GPU: NVIDIA RTX 3090 (24GB VRAM)
- CPU: 16 cores
- RAM: 64GB
- Storage: 500GB SSD

**Recommended** (for faster training):
- GPU: NVIDIA A100 (80GB VRAM) x 2
- CPU: 32 cores
- RAM: 128GB
- Storage: 1TB NVMe SSD

### Training Metrics

| Dataset Size | Model Size | Epochs | Time (A100) | Time (RTX 3090) |
|--------------|------------|--------|-------------|-----------------|
| 1K samples | 3B params | 3 | 2 hours | 6 hours |
| 3K samples | 3B params | 5 | 8 hours | 24 hours |
| 10K samples | 7B params | 5 | 24 hours | 72 hours |
| 30K samples | 7B params | 10 | 72 hours | 7 days |

**Throughput**:
- Single worker: ~12 samples/hour
- 16 workers: ~200 samples/hour
- 32 workers: ~350 samples/hour (diminishing returns)

### Inference Performance

**Optimized Model** (after training):
- Same latency as base model
- Slightly better token efficiency (fewer retries)
- +5-10% throughput in production (fewer errors)

## Data Flow

### Training Data Pipeline

*(blocco di codice rimosso)*

## Error Handling

### Training Failures

**Common Issues**:

1. **CUDA Out of Memory**
   *(blocco di codice rimosso)*

2. **Agent Timeout**
   *(blocco di codice rimosso)*

3. **vLLM Crash**
   *(blocco di codice rimosso)*

### Production Integration Risks

**Risk**: Optimized model hallucinates more
**Mitigation**:
- A/B test in staging first
- Monitor hallucination metrics
- Rollback capability via feature flags

**Risk**: Latency regression
**Mitigation**:
- Benchmark before deployment
- Set p95 latency SLO
- Auto-rollback if exceeded

## Monitoring & Observability

### Training Metrics (W&B)

*(blocco di codice rimosso)*

### Production Metrics (MeepleAI)

*(blocco di codice rimosso)*

## Security Considerations

### Training Environment Isolation

- **Network**: Separate VPC/subnet from production
- **Credentials**: Different API keys for training vs prod
- **Data**: Sanitize PII before export to training env

### Model Security

- **Checkpoints**: Encrypt at rest
- **API Keys**: Use secret management (Azure Key Vault)
- **Access Control**: Limit who can deploy new models

## Next Steps

For implementation details, see:
- **Setup Guide**: `agent-lightning-integration-guide.md`
- **Examples**: `agent-lightning-examples.md`
- **Troubleshooting**: `agent-lightning-integration-guide.md#troubleshooting`


---



<div style="page-break-before: always;"></div>

## architecture/components/agent-lightning/examples.md

# Agent Lightning Examples for MeepleAI

Practical use cases for optimizing MeepleAI agents with Agent Lightning.

---

## Example 1: RAG System Optimization

### Goal
Improve answer accuracy, citation correctness, reduce hallucinations

### Current Baseline
**Service**: `RagService.cs`
**Metrics**:
- Precision@5: 0.72
- Citation Correctness: 0.68
- Hallucination Rate: 0.15

### Target Improvements
- Precision@5: 0.82 (+14%)
- Citation Correctness: 0.85 (+25%)
- Hallucination Rate: 0.08 (-47%)

### Reward Function
*(blocco di codice rimosso)*

### Training Configuration
*(blocco di codice rimosso)*

### Data Preparation
*(blocco di codice rimosso)*

### Deployment
*(blocco di codice rimosso)*

---

## Example 2: Setup Guide Agent

### Goal
Improve clarity, completeness, accuracy of setup instructions

### Current Baseline
- Completeness: 0.75 (missing edge cases)
- Clarity: "confusing" in 12% feedback
- Accuracy: 0.92

### Reward Function
*(blocco di codice rimosso)*

---

## Example 3: Multi-Agent System

### Selective Training
*(blocco di codice rimosso)*

---

## Training Time & Impact Summary

| Use Case | Training Time | Expected Improvement | Deploy Artifact |
|----------|---------------|---------------------|-----------------|
| **RAG** | 24h (5 epochs) | +25% citation accuracy | Optimized prompt |
| **Setup Guide** | 12h (3 epochs) | +15% completeness | Prompt template |
| **Streaming QA** | 18h (4 epochs) | -20% latency | Model checkpoint |
| **Multi-Agent** | 36h (7 epochs) | Selective gains | Prompt + model |

**Recommended Order**:
1. RAG (highest impact, clear metrics)
2. Setup Guide (simpler reward)
3. Streaming QA (more tuning needed)

---

**See Also**: [Agent Lightning Architecture](./architecture.md) | [Training Guide](./training-guide.md)


---



<div style="page-break-before: always;"></div>

## architecture/components/agent-lightning/integration-guide.md

# Agent Lightning Integration Guide for MeepleAI Development

## Overview

This guide explains how to use Microsoft Agent Lightning as a **development tool** to optimize MeepleAI's AI agents through Reinforcement Learning (RL). Agent Lightning is used in a **separate training environment** to improve prompts, behaviors, and model performance, which are then deployed to the production MeepleAI system.

## What is Agent Lightning?

Agent Lightning is a Python-based RL training framework from Microsoft Research that enables:
- **Zero-code RL training** for any AI agent
- **Framework-agnostic optimization** (works with LangChain, OpenAI SDK, AutoGen, etc.)
- **Selective agent optimization** in multi-agent systems
- **Decoupled training/execution** architecture

**Key Paper**: [Agent Lightning: Train ANY AI Agents with Reinforcement Learning (arXiv)](https://arxiv.org/abs/2508.03680)

## Architecture Overview

*(blocco di codice rimosso)*

## Use Cases for MeepleAI

### 1. RAG System Optimization
**Goal**: Improve retrieval quality and answer accuracy

**Agent Lightning Application**:
- Train agent to select better search parameters (TopK, MinScore)
- Optimize query expansion strategies
- Learn to filter irrelevant chunks
- Improve citation accuracy

**Reward Signal**: Precision@K, MRR, citation correctness

### 2. Setup Guide Agent Optimization
**Goal**: Generate more helpful, accurate setup instructions

**Agent Lightning Application**:
- Optimize prompt templates for clarity
- Learn to structure multi-step instructions
- Improve context selection from rules
- Reduce hallucinations in edge cases

**Reward Signal**: User feedback, instruction completeness, error rate

### 3. Streaming QA Optimization
**Goal**: Faster, more accurate responses with better confidence scores

**Agent Lightning Application**:
- Optimize token generation patterns
- Learn when to stop generation early
- Improve confidence calibration
- Reduce irrelevant token generation

**Reward Signal**: Latency, confidence accuracy, user satisfaction

### 4. Chess Agent Training (Future)
**Goal**: Optimize chess move suggestions and FEN analysis

**Agent Lightning Application**:
- Train agent on legal move validation
- Optimize position evaluation prompts
- Learn better move explanation generation

**Reward Signal**: Move legality, position accuracy, explanation quality

## Development Workflow

### Phase 1: Setup Training Environment

*(blocco di codice rimosso)*

### Phase 2: Create Test Agent (Python)

Create a Python agent that mimics your MeepleAI service behavior:

*(blocco di codice rimosso)*

### Phase 3: Prepare Training Dataset

Create dataset from MeepleAI's existing data:

*(blocco di codice rimosso)*

### Phase 4: Run Training

*(blocco di codice rimosso)*

### Phase 5: Evaluate Results

*(blocco di codice rimosso)*

### Phase 6: Deploy to MeepleAI Production

After training, deploy optimized artifacts:

#### Option A: Export Optimized Prompts

*(blocco di codice rimosso)*

#### Option B: Fine-tuned Model via OpenRouter

If you trained a custom model:

*(blocco di codice rimosso)*

## Best Practices

### 1. Start Small
- Begin with single agent (RAG or Setup Guide)
- Use small model (Qwen 3B) for faster iteration
- Validate on 100-500 samples before scaling

### 2. Reward Engineering
- Combine multiple signals (accuracy + latency + citations)
- Use existing MeepleAI metrics (confidence scores, feedback)
- Penalize hallucinations heavily

### 3. Data Quality
- Filter training data by confidence > 0.7
- Include both positive and negative examples
- Balance dataset across game types

### 4. Monitoring
- Track training metrics in W&B or TensorBoard
- Compare against baseline after each epoch
- Stop early if overfitting detected

### 5. Deployment Safety
- Always A/B test in staging first
- Use feature flags for gradual rollout
- Monitor production metrics closely

## Integration with Existing MeepleAI Features

### Prompt Management (ADMIN-01)
Agent Lightning optimized prompts → `prompt_templates` table:

*(blocco di codice rimosso)*

### RAG Evaluation (AI-06)
Use Agent Lightning to optimize RAG metrics:

*(blocco di codice rimosso)*

### Dynamic Configuration (CONFIG-01)
Store training hyperparameters:

*(blocco di codice rimosso)*

## Example: End-to-End RAG Optimization

Complete walkthrough for optimizing MeepleAI RAG:

*(blocco di codice rimosso)*

## Troubleshooting

### Issue: CUDA Out of Memory
**Solution**: Reduce batch size or use smaller model
*(blocco di codice rimosso)*

### Issue: Agent Timeout During Training
**Solution**: Increase timeout and limit response length
*(blocco di codice rimosso)*

### Issue: Poor Reward Signal
**Solution**: Refine reward function with domain knowledge
*(blocco di codice rimosso)*

## Resources

- **Agent Lightning Docs**: https://microsoft.github.io/agent-lightning/
- **GitHub Repo**: https://github.com/microsoft/agent-lightning
- **Research Paper**: https://arxiv.org/abs/2508.03680
- **SQL Agent Example**: https://medium.com/@yugez/training-ai-agents-to-write-and-self-correct-sql-with-reinforcement-learning-571ed31281ad
- **MeepleAI Prompt Management**: `docs/issue/admin-01-phase4-implementation-tracker.md`

## Next Steps

1. **Start Simple**: Optimize RAG system prompt first (highest impact)
2. **Collect Baseline**: Run evaluation on current prompts
3. **Setup Environment**: Install Agent Lightning in separate Python env
4. **Prepare Data**: Export 3 months of high-quality AI logs
5. **Train & Evaluate**: Run training loop, compare metrics
6. **Deploy Safely**: Use feature flags and A/B testing

**Estimated Timeline**:
- Setup (Day 1): 4 hours
- Data preparation (Day 2): 4 hours
- Training (Day 3-4): 24 hours (overnight runs)
- Evaluation & deployment (Day 5): 6 hours

**Total**: 1 week for first optimization cycle


---



<div style="page-break-before: always;"></div>

## architecture/components/agent-lightning/openrouter-guide.md

# Agent Lightning + OpenRouter Integration Guide

**Purpose**: Optimize MeepleAI agents using Agent Lightning with OpenRouter API - no code changes required

---

## Overview

Agent Lightning provides **two training approaches** for MeepleAI, both deploy identically to production:

| Approach | GPU Required | Cost | Time | Optimization |
|----------|--------------|------|------|--------------|
| **GPU Local** | RTX 3090+ | $0-26/train | 6-24h | GRPO (RL) + fine-tuning |
| **OpenRouter API** | No | $30-50/train | 4-6h | APO (prompt-only) |

**Key Insight**: Both approaches deploy **only optimized prompts** to MeepleAI - no code changes to `LlmService.cs` required.

---

## Architecture

### Scenario 1: GPU Local → OpenRouter Production (Recommended)

*(blocco di codice rimosso)*

**Pros**: Maximum quality (RL + fine-tuning), training cost only in dev, production uses OpenRouter (cheap, scalable)
**Cons**: Requires GPU (RTX 3090+ or cloud), complex setup, longer training

### Scenario 2: OpenRouter Training → OpenRouter Production

*(blocco di codice rimosso)*

**Pros**: No GPU, simple setup, fast training (4-6h), same model in train/prod
**Cons**: Prompt-only optimization (no fine-tuning), $30-50 API cost per training, latency-dependent

---

## Implementation

### Scenario 1: GPU Local Setup

*(blocco di codice rimosso)*

### Scenario 2: OpenRouter Training

*(blocco di codice rimosso)*

**Training Script** (OpenRouter): See full script in original doc lines 212-571 - implements APO algorithm with LLM-guided prompt variations.

---

## Production Deployment (Both Scenarios)

**MeepleAI LlmService** (NO CHANGES):
*(blocco di codice rimosso)*

**A/B Testing**:
*(blocco di codice rimosso)*

**Dynamic Config Tracking**:
*(blocco di codice rimosso)*

---

## Cost Analysis

### GPU Local Scenario

| Item | Cost | Frequency |
|------|------|-----------|
| RTX 3090 workstation | $2,000-3,000 | One-time |
| Cloud GPU (A100, 8h) | $8-26 | Per training |
| Electricity (local) | $2-5 | Per training |
| **Year 1 Total (local hardware)** | $2,000-3,000 + $100/year | - |
| **Year 1 Total (cloud GPU)** | ~$300/year | Monthly training |

### OpenRouter Scenario

| Item | Cost | Frequency |
|------|------|-----------|
| Setup | $0 | One-time |
| Training (10 iter, 500 samples) | $15-20 | Per training |
| Model variations: Claude 3.5 Sonnet | $15-20 | - |
| Model variations: GPT-4o | $10-15 | - |
| Model variations: GPT-4o-mini | $3-5 | Reduced performance |
| **Year 1 Total** | $180-240 | Monthly training |

**ROI Comparison**:

| Aspect | GPU Local | OpenRouter |
|--------|-----------|-----------|
| Initial investment | $2,000-3,000 | $0 |
| Annual cost | $100-300 | $180-240 |
| Break-even | 8-15 months | Immediate |
| Flexibility | Medium | High |
| Scalability | Limited | Unlimited |

**Recommendation**: Startup/test → OpenRouter | Production (>2x/month training) → GPU local

---

## Comparison

| Aspect | GPU Local | OpenRouter API |
|--------|-----------|----------------|
| GPU | ✅ RTX 3090+ | ❌ None |
| Setup | Complex (1-2h) | Simple (10min) |
| Algorithm | GRPO (RL) | APO (prompt) |
| Fine-tuning | ✅ Possible | ❌ No |
| Cost/training | $0-26 | $30-50 |
| Time | 6-24h | 4-6h |
| Improvement | +20-25% | +15-20% |
| Deployment | Identical | Identical |

---

## Monitoring Post-Deploy

### Prometheus Metrics

*(blocco di codice rimosso)*

### Grafana Dashboard Queries

*(blocco di codice rimosso)*

---

## Continuous Improvement Workflow

### Monthly Training Automation

*(blocco di codice rimosso)*

**Auto-Deploy Criteria**:
- Improvement > 10% baseline
- Validation confidence > 0.85
- No hallucination increase
- Manual approval for production

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **OpenRouter rate limits** | Exponential backoff retry (1s, 2s, 4s, 8s, 16s) |
| **High training costs** | Use GPT-4o-mini ($3-5 vs $15-20) with 5x cost reduction |
| **Prompt not improving** | Increase iterations (20+), larger sample size (100+), advanced reward function |
| **Low baseline performance** | Improve data quality, add more training samples, refine reward function |

---

## Best Practices

1. **Start Small**: 1K samples, 5 iterations, GPT-4o-mini → validate before scaling
2. **Always Validate**: A/B test in staging 1-2 weeks, monitor P@K/MRR/citations, rollback ready with feature flags
3. **Document Everything**: Save training logs, version prompts in git, track metrics in Grafana, maintain changelog
4. **Iterate Regularly**: Monthly training with new data, analyze user feedback, update reward function, scale dataset progressively

---

## Next Steps

### Week 1 (Setup)
1. Choose: GPU local vs OpenRouter
2. Setup training environment
3. Export 3K samples from MeepleAI
4. First test (2 iter, 100 samples)

### Month 1 (Validation)
1. Full training (10 iter, full dataset)
2. Deploy to staging
3. A/B test 2 weeks
4. Production deploy if >10% improvement

### Quarter 1 (Scale)
1. Automate monthly training
2. Expand to other services (SetupGuide, Chess)
3. Continuous learning pipeline
4. Consider full model fine-tuning

---

## Resources

- **Agent Lightning**: https://microsoft.github.io/agent-lightning/
- **OpenRouter**: https://openrouter.ai/docs
- **MeepleAI Guides**:
  - [Quick Start](./agent-lightning-quickstart.md)
  - [Examples](./agent-lightning-examples.md)
  - [Architecture](./agent-lightning-architecture.md)

---

**Conclusion**: Agent Lightning is **fully compatible with OpenRouter**. Both approaches deploy optimized prompts to MeepleAI production without code changes. Choose OpenRouter for low-risk fast validation, GPU local for maximum quality at scale.


---



<div style="page-break-before: always;"></div>

## architecture/components/agent-lightning/quickstart.md

# Agent Lightning Quick Start for MeepleAI

Get started with Agent Lightning to optimize MeepleAI agents in under 1 hour.

## Prerequisites

- Python 3.10+ installed
- NVIDIA GPU with 16GB+ VRAM (RTX 3090 or better)
- 50GB free disk space
- Access to MeepleAI production database exports

## 5-Step Quick Start

### Step 1: Install Agent Lightning (10 minutes)

*(blocco di codice rimosso)*

**Verify installation**:
*(blocco di codice rimosso)*

### Step 2: Prepare Training Data (15 minutes)

Export data from MeepleAI database:

*(blocco di codice rimosso)*

**Convert to Parquet format**:

*(blocco di codice rimosso)*

Run preparation:
*(blocco di codice rimosso)*

### Step 3: Create Test Agent (15 minutes)

*(blocco di codice rimosso)*

### Step 4: Run Training (20 minutes for first test)

**Terminal 1 - Start Training Server**:
*(blocco di codice rimosso)*

**Terminal 2 - Run Agent Workers**:
*(blocco di codice rimosso)*

**Monitor progress** (Terminal 3):
*(blocco di codice rimosso)*

### Step 5: Evaluate & Deploy (10 minutes)

After training completes (~20 minutes for 2 epochs):

*(blocco di codice rimosso)*

Run evaluation:
*(blocco di codice rimosso)*

**Deploy to MeepleAI**:

If results are good, export optimized prompt:

*(blocco di codice rimosso)*

## Verification Checklist

Before deploying to production:

- [ ] Training completed successfully (2+ epochs)
- [ ] Evaluation shows improvement > 10%
- [ ] No hallucination increase detected
- [ ] Tested on 50+ validation samples
- [ ] SQL migration created
- [ ] Reviewed optimized prompt manually
- [ ] Backup current production prompt

## Next Steps

### Quick Wins (Week 1)
1. ✅ Complete this quickstart
2. Run overnight training (5 epochs, ~6 hours)
3. A/B test in MeepleAI staging environment
4. Deploy if improvement > 15%

### Deep Optimization (Week 2-3)
1. Add real Qdrant integration to training agent
2. Expand training dataset to 10K samples
3. Fine-tune reward function with citation metrics
4. Train larger model (7B params)

### Production Pipeline (Month 2)
1. Automate weekly training on new logs
2. Setup continuous evaluation dashboard
3. Implement auto-deployment pipeline
4. Monitor production metrics

## Troubleshooting

### Issue: GPU Out of Memory
*(blocco di codice rimosso)*

### Issue: Training Server Won't Start
*(blocco di codice rimosso)*

### Issue: Workers Not Connecting
*(blocco di codice rimosso)*

## Resources

- **Full Integration Guide**: `agent-lightning-integration-guide.md`
- **Detailed Examples**: `agent-lightning-examples.md`
- **Architecture Deep Dive**: `agent-lightning-architecture.md`
- **Agent Lightning Docs**: https://microsoft.github.io/agent-lightning/
- **GitHub**: https://github.com/microsoft/agent-lightning

## Estimated Timeline

| Phase | Duration | Outcome |
|-------|----------|---------|
| **Setup** | 30 min | Environment ready |
| **Data Prep** | 15 min | 3K samples prepared |
| **First Training** | 20 min | Quick test (2 epochs) |
| **Evaluation** | 10 min | +10-15% improvement |
| **Deploy** | 10 min | SQL migration ready |
| **Total** | **1.5 hours** | **Ready for production testing** |

For overnight training (recommended):
- **Setup**: 1 hour (one-time)
- **Training**: 6-8 hours (overnight)
- **Evaluation**: 30 minutes
- **Deploy**: 30 minutes
- **Total**: **8-10 hours** (mostly unattended)

**Expected improvement**: +20-25% on RAG metrics


---



<div style="page-break-before: always;"></div>

## architecture/components/amplifier/architecture-overview.md

# Microsoft Amplifier - Architecture Overview

## What is Amplifier

> **"Automate complex workflows by describing how you think through them."**

Transform your development expertise into reusable AI tools without writing code.

### Core Concept
*(blocco di codice rimosso)*

---

## Architecture Diagram

*(blocco di codice rimosso)*

---

## Key Concepts

### 1. Metacognitive Recipes

**Definition**: Step-by-step description of expert thinking process

**Structure**:
*(blocco di codice rimosso)*

**Outcome**: Amplifier converts recipe → `/command` → Reusable forever

### 2. Document-Driven Development (DDD)

**Workflow**:
*(blocco di codice rimosso)*

**Benefits**: Docs always synced, design review upfront, no doc drift

### 3. Design Intelligence (7 Agents)

| Agent | Focus | Output |
|-------|-------|--------|
| **art-director** | Visual strategy | Palette, typography |
| **component-designer** | React components | Buttons, cards, modals |
| **layout-architect** | Info architecture | Dashboard layouts |
| **responsive-strategist** | Device adaptation | Breakpoints, fluid layouts |
| **animation-choreographer** | Motion design | Transitions, micro-interactions |
| **voice-strategist** | UX copy | Labels, errors, tone |
| **design-system-architect** | Design systems | Tokens, patterns |

**Framework**: 9 dimensions (purpose, hierarchy, color, typography, spacing, responsive, a11y, motion, voice) × 4 layers (foundational, structural, behavioral, experiential)

---

## MeepleAI Integration Examples

### Feature Development Time Comparison

| Task | Manual | With Amplifier | Saving |
|------|--------|----------------|--------|
| **New Service** | 30min | 2min | 93% |
| **New Endpoint** | 15min | 1min | 93% |
| **UI Component** | 45min | 5min | 89% |
| **Small Feature** | 4h | 1h | 75% |
| **Large Feature** | 2d | 6h | 75% |

### Complete Feature Flow

**Without Amplifier** (7.5h):
*(blocco di codice rimosso)*

**With Amplifier** (1h 7min):
*(blocco di codice rimosso)*

---

## Amplifier Components

### Project Structure
*(blocco di codice rimosso)*

### Transcript System
*(blocco di codice rimosso)*

**Use Cases**: Recover context, find past solutions, learn patterns, share knowledge

---

## Command Library Pattern

### Week-by-Week Build
*(blocco di codice rimosso)*

### Creating Custom Commands
*(blocco di codice rimosso)*

---

## ROI Calculator

*(blocco di codice rimosso)*

---

## Amplifier vs Alternatives

### vs GitHub Copilot
| Aspect | Amplifier | Copilot |
|--------|-----------|---------|
| **Scope** | Workflow automation | Code completion |
| **Reusability** | High (slash commands) | Low (per-use) |
| **Context** | Project-aware (AGENTS.md) | File-aware |
| **Learning** | Compounds over time | Static |

**Use Both**: Copilot for inline, Amplifier for workflows

### vs Agent Lightning
| Aspect | Amplifier | Agent Lightning |
|--------|-----------|-----------------|
| **Purpose** | Dev workflow | AI training |
| **Target** | Developers | AI systems |
| **Output** | Tools/commands | Trained models |
| **Runtime** | Development | Development |
| **Production** | ❌ No | ✅ Yes (artifacts) |

**For MeepleAI**: Amplifier = faster dev | Agent Lightning = better AI

---

## When to Use Amplifier

### ✅ Worth It If
- Full-time on MeepleAI (>20h/week)
- Repetitive tasks (>2x/week)
- Small team needs velocity boost
- Focus on quality + consistency
- Long-term project (>6 months)

### ❌ Skip If
- Occasional contributions (<few hours/month)
- Tasks always different (low repetition)
- Prefer complete manual control
- Immediate time pressure (setup takes hours)

---

## Limitations

### Development-Only Tool
*(blocco di codice rimosso)*

### Requires Claude Code
- Designed specifically for Claude Code
- Doesn't work with Copilot, Cursor, others
- Alternative: Use concepts (metacognitive recipes) with other tools

### Learning Investment
*(blocco di codice rimosso)*

---

## Development Commands

*(blocco di codice rimosso)*

---

## Best Practices

### Start Small
*(blocco di codice rimosso)*

### Iterate on Commands
*(blocco di codice rimosso)*

### Security
*(blocco di codice rimosso)*

---

**Next Steps**: See `amplifier-developer-workflow-guide.md` for setup and practical examples

**Remember**: Amplifier is for **development workflow**, not production. For optimizing **MeepleAI AI agents**, use **Agent Lightning**.


---



<div style="page-break-before: always;"></div>

## architecture/components/amplifier/developer-workflow.md

# Microsoft Amplifier - Guida al Workflow di Sviluppo per MeepleAI

## Panoramica

**Microsoft Amplifier NON è un framework di produzione** - è un sistema di sviluppo metacognitivo che migliora il TUO workflow quando sviluppi MeepleAI con Claude Code.

**Cosa fa Amplifier**:
- ✅ Crea comandi slash personalizzati per task ripetitivi
- ✅ Automatizza workflow di sviluppo (design → code → test → deploy)
- ✅ Genera boilerplate code con best practices
- ✅ Design intelligence per UI components
- ✅ Gestione worktree Git per sviluppo parallelo

**Cosa NON fa**:
- ❌ Non si integra nel runtime di MeepleAI
- ❌ Non migliora gli agenti AI di produzione (usa Agent Lightning per quello)
- ❌ Non offre servizi/API per l'applicazione

## Quando Usare Amplifier

### ✅ Usa Amplifier Per (Workflow Sviluppo)

1. **Automatizzare Task Ripetitivi**
   - Generare boilerplate per nuovi services
   - Creare test templates
   - Setup infrastruttura standard

2. **Workflow Coordinati**
   - Document-Driven Development (design → implement → test)
   - Sviluppo parallelo con worktree
   - Code review automatizzato

3. **Design System**
   - Componenti UI con accessibility
   - Design tokens e style guides
   - Responsive layouts

### ❌ NON Usare Amplifier Per (Produzione)

1. **Ottimizzare Agenti AI**
   - ❌ RAG optimization
   - ❌ Prompt training
   - ❌ LLM fine-tuning
   - ✅ **Usa Agent Lightning invece**

2. **Runtime Services**
   - ❌ API endpoints
   - ❌ Database operations
   - ❌ Background jobs

## Setup Amplifier per MeepleAI

### Prerequisiti

*(blocco di codice rimosso)*

### Installazione

**Opzione 1: Workspace Pattern (Raccomandato per Progetti Lunghi)**

*(blocco di codice rimosso)*

**Opzione 2: Amplifier dentro MeepleAI (Quick Start)**

*(blocco di codice rimosso)*

### Configurazione Progetto

*(blocco di codice rimosso)*

## Creare Comandi Slash Personalizzati per MeepleAI

### Esempio 1: Generare Nuovo Service

Crea un comando `/meepleai:new-service` che genera boilerplate completo:

**Dì a Claude Code**:
*(blocco di codice rimosso)*

**Amplifier creerà il comando**, poi potrai usarlo:

*(blocco di codice rimosso)*

### Esempio 2: Workflow Feature Completo

Crea workflow Document-Driven Development per nuove feature:

**Dì a Claude Code**:
*(blocco di codice rimosso)*

**Uso**:
*(blocco di codice rimosso)*

### Esempio 3: Design System Components

**Dì a Claude Code**:
*(blocco di codice rimosso)*

**Uso**:
*(blocco di codice rimosso)*

## Workflow Document-Driven Development (DDD)

Amplifier include un potente workflow DDD per evitare "doc drift":

### Fase 1: Design

*(blocco di codice rimosso)*

**Output Example** (`docs/issue/FEATURE-001-game-collections.md`):
*(blocco di codice rimosso)*

### Fase 2: Implementation

*(blocco di codice rimosso)*

### Fase 3: Cleanup

*(blocco di codice rimosso)*

## Sviluppo Parallelo con Worktree

Amplifier supporta worktree Git per provare approcci diversi in parallelo:

*(blocco di codice rimosso)*

## Design Intelligence

Amplifier include agenti specializzati per design:

### Creare UI Component Accessibile

*(blocco di codice rimosso)*

### Agenti Design Disponibili

*(blocco di codice rimosso)*

## Esempi Pratici per MeepleAI

### 1. Nuovo Endpoint API

**Comando personalizzato**:
*(blocco di codice rimosso)*

### 2. Database Migration

**Comando personalizzato**:
*(blocco di codice rimosso)*

### 3. React Component con Tests

**Comando personalizzato**:
*(blocco di codice rimosso)*

## Best Practices

### 1. Mantieni Context Pulito

*(blocco di codice rimosso)*

### 2. Comandi Specifici per Dominio

*(blocco di codice rimosso)*

### 3. Validation Automatica

*(blocco di codice rimosso)*

### 4. Documentation Sync

*(blocco di codice rimosso)*

## Transcript e Learning

Amplifier salva automaticamente le conversazioni:

*(blocco di codice rimosso)*

## Confronto: Workflow Con vs Senza Amplifier

### Senza Amplifier (Manuale)

*(blocco di codice rimosso)*

### Con Amplifier (Automatizzato)

*(blocco di codice rimosso)*

**ROI**:
- **Tempo risparmiato**: 93% (28 min/service)
- **Quality**: Test coverage 100% automatico
- **Consistency**: Stesso pattern ogni volta

## Limitazioni e Caveat

### ❌ Non Sostituisce Agent Lightning

*(blocco di codice rimosso)*

**Usa Entrambi**:
- **Amplifier**: Sviluppa feature più velocemente
- **Agent Lightning**: Ottimizza prompt RAG/QA

### ⚠️ Richiede Effort Iniziale

*(blocco di codice rimosso)*

### 🔧 Manutenzione Comandi

*(blocco di codice rimosso)*

## Quick Start Checklist

- [ ] **Install Amplifier** (30 min)
  *(blocco di codice rimosso)*

- [ ] **Setup MeepleAI Context** (15 min)
  *(blocco di codice rimosso)*

- [ ] **Primo Comando** (1 ora)
  *(blocco di codice rimosso)*

- [ ] **Secondo Comando** (30 min)
  *(blocco di codice rimosso)*

- [ ] **DDD Workflow** (2 ore)
  *(blocco di codice rimosso)*

**Totale Setup**: 4-5 ore
**Break-even**: Dopo 10-15 task (~1-2 settimane)

## Risorse

- **Amplifier GitHub**: https://github.com/microsoft/amplifier
- **Amplifier Vision**: `AMPLIFIER_VISION.md` nel repo
- **Create Your Own Tools**: `docs/CREATE_YOUR_OWN_TOOLS.md`
- **Document-Driven Development**: `docs/document_driven_development/`
- **The Amplifier Way**: `docs/THIS_IS_THE_WAY.md`

## Prossimi Passi

1. **Setup Base** (Oggi)
   - Installa Amplifier
   - Crea AGENTS.md per MeepleAI
   - Familiarizza con `/ultrathink-task`

2. **Primo Comando** (Settimana 1)
   - `/meepleai:new-service` (task più comune)
   - Testa su 2-3 services reali
   - Refina finché perfetto

3. **Espandi** (Settimana 2-3)
   - `/meepleai:new-endpoint`
   - `/meepleai:ui-component`
   - DDD workflow completo

4. **Produzione** (Mese 2)
   - Tutti sviluppatori usano comandi
   - Velocity +50%
   - Quality consistente

---

**Ricorda**: Amplifier migliora il TUO workflow, non l'app di produzione. Per ottimizzare gli agenti AI di MeepleAI, usa **Agent Lightning** (già documentato).


---



<div style="page-break-before: always;"></div>

## architecture/components/confidence-validation.md

# BGAI-028: Confidence Validation Service

**Issue**: #970 - [BGAI-028] ConfidenceValidationService (threshold ≥0.70)
**Date**: 2025-11-12
**Status**: ✅ **COMPLETE**

## Summary

Implemented `ConfidenceValidationService` - a domain service that enforces minimum confidence threshold (≥0.70) for AI-generated responses to ensure >95% accuracy target for board game rules.

## Implementation

### Components Created

1. **IConfidenceValidationService** (`KnowledgeBase/Domain/Services/`)
   - Interface defining validation contract
   - ConfidenceValidationResult record
   - ValidationSeverity enum

2. **ConfidenceValidationService** (`KnowledgeBase/Domain/Services/`)
   - Domain service implementing validation logic
   - Threshold: 0.70 (correlates to >95% accuracy)
   - Multi-tier validation (Pass/Warning/Critical/Unknown)

3. **ConfidenceValidationServiceTests** (11 tests)
   - Comprehensive threshold enforcement testing
   - Edge case validation
   - 100% pass rate

### Architecture

*(blocco di codice rimosso)*

## Validation Thresholds

| Confidence Range | IsValid | Severity | Meaning |
|------------------|---------|----------|---------|
| ≥0.70 | ✅ true | Pass | Acceptable quality (>95% accuracy target) |
| 0.60-0.70 | ❌ false | Warning | Below threshold but usable |
| <0.60 | ❌ false | Critical | Unacceptable quality |
| null | ❌ false | Unknown | No confidence score available |

### Threshold Rationale

**Target**: >95% accuracy for board game rules

**Correlation**:
- Confidence ≥0.70 ≈ 95%+ accuracy (empirical)
- Confidence 0.60-0.70 ≈ 85-95% accuracy (warning zone)
- Confidence <0.60 ≈ <85% accuracy (unacceptable)

## Code Examples

### Basic Usage

*(blocco di codice rimosso)*

### Advanced Usage with Rejection

*(blocco di codice rimosso)*

## DI Registration

*(blocco di codice rimosso)*

**Lifetime**: Singleton (stateless domain service)

## Test Coverage

### ConfidenceValidationServiceTests (11 tests)

| Test | Scenario | Expected |
|------|----------|----------|
| Test01 | Threshold property | Returns 0.70 |
| Test02 | Confidence 0.85 | Valid, Pass |
| Test03 | Confidence 0.70 (boundary) | Valid, Pass |
| Test04 | Confidence 0.65 | Invalid, Warning |
| Test05 | Confidence 0.45 | Invalid, Critical |
| Test06 | Confidence null | Invalid, Unknown |
| Test07 | Confidence 0.0 | Invalid, Critical |
| Test08 | Confidence 1.0 | Valid, Pass |
| Test09 | Confidence 0.69 (edge) | Invalid, Warning |
| Test10 | Confidence 0.60 (edge) | Invalid, Warning |
| Test11 | Confidence 0.59 (edge) | Invalid, Critical |

### Test Results

*(blocco di codice rimosso)*

**Coverage**: 100% of validation logic tested
- ✅ Threshold boundary (0.70)
- ✅ Warning boundary (0.60)
- ✅ All severity levels
- ✅ Null handling
- ✅ Edge cases (0.69, 0.60, 0.59)

## Integration Guide

### Step 1: Inject Service

*(blocco di codice rimosso)*

### Step 2: Validate After Confidence Calculation

*(blocco di codice rimosso)*

### Step 3: Add Validation Metadata

*(blocco di codice rimosso)*

## API Response Changes

### Before (No Validation)
*(blocco di codice rimosso)*

### After (With Validation)
*(blocco di codice rimosso)*

## Logging Examples

### Pass (≥0.70)
*(blocco di codice rimosso)*

### Warning (0.60-0.70)
*(blocco di codice rimosso)*

### Critical (<0.60)
*(blocco di codice rimosso)*

## Quality Impact

### Confidence Distribution Analysis

**Expected Production Distribution** (based on RAG performance):
- 70%+ responses: ≥0.70 confidence (PASS)
- 20% responses: 0.60-0.70 confidence (WARNING)
- 10% responses: <0.60 confidence (CRITICAL)

**Actions by Severity**:
- **Pass**: Normal operation, no action needed
- **Warning**: Log for monitoring, consider prompt improvements
- **Critical**: Flag for review, potentially reject or request clarification

### Improvement Loop

*(blocco di codice rimosso)*

## Future Enhancements

### Phase 1: Monitoring (Month 3)
- Grafana dashboard for confidence distribution
- Alerts on high percentage of low-confidence responses
- Automated A/B testing of prompt improvements

### Phase 2: Auto-Improvement (Month 4)
- Collect low-confidence responses for training data
- Automatic prompt optimization based on confidence feedback
- Multi-model consensus for low-confidence responses

### Phase 3: User Feedback (Month 5)
- Show confidence indicator in UI (high/medium/low)
- Allow users to flag incorrect responses
- Feed back loop: User corrections → improve retrieval/prompts

## Testing Strategy

### Unit Tests (11 tests) - ✅ Complete
- Threshold enforcement
- Boundary conditions
- All severity levels
- Edge cases

### Integration Tests - 🎯 Future Work
- Full RAG pipeline with validation
- Real confidence scores from ResponseQualityService
- Validation metadata in responses
- Different RAG methods (Ask, Explain, HybridSearch)

### Performance Tests - 🎯 Future Work
- Validation overhead measurement (<5ms target)
- No impact on P95 latency target (<3s)

## Observability

### Metrics to Track

*(blocco di codice rimosso)*

### Dashboards

**Confidence Health Dashboard**:
- Pass rate (% of responses ≥0.70)
- Warning rate (% of responses 0.60-0.70)
- Critical rate (% of responses <0.60)
- Confidence distribution histogram
- Trend over time (improving/degrading)

## Implementation Status

### ✅ Completed
- IConfidenceValidationService interface
- ConfidenceValidationService implementation
- ValidationSeverity enum
- ConfidenceValidationResult record
- DI registration
- 11 comprehensive unit tests (100% pass)

### 🎯 Ready for Integration
- Service available via DI
- Can be injected into RagService
- Tests verify threshold ≥0.70 enforced
- Logging infrastructure ready
- Metadata enrichment pattern defined

### 📋 Integration Checklist (Future PR)
- [ ] Add IConfidenceValidationService to RagService constructor
- [ ] Call ValidateConfidence() after confidence calculation
- [ ] Add validation metadata to QaResponse/ExplainResponse
- [ ] Update RagServiceIntegrationTests to verify validation
- [ ] Add observability metrics for validation events

## Dependencies

| Issue | Title | Status |
|-------|-------|--------|
| #969 | BGAI-027: LLM documentation | ✅ CLOSED |
| #970 | BGAI-028: Confidence validation | ✅ COMPLETE (this issue) |

## Acceptance Criteria

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Threshold ≥0.70 enforced | ✅ | Test02, Test03 verify threshold |
| Service implemented | ✅ | ConfidenceValidationService.cs |
| Tests passing | ✅ | 11/11 tests pass (75ms) |
| DI registered | ✅ | KnowledgeBaseServiceExtensions.cs:25 |
| Documentation complete | ✅ | This document + code comments |

---

**Generated**: 2025-11-12
**Files Created**: 3 (interface + service + tests)
**Tests Added**: 11 unit tests
**Pass Rate**: 100% (388/388 total with new tests)
**Threshold**: 0.70 (enforced)


---



<div style="page-break-before: always;"></div>

## architecture/components/pdf-extraction-alternatives.md

# PDF Extraction - Open Source Alternatives Analysis

**Date**: 2025-01-15
**Decision**: ✅ IMPLEMENTED - Replaced LLMWhisperer with 100% open source stack (Unstructured)
**Reason**: Eliminate API costs, vendor lock-in, commercial licensing issues
**Status**: This document is kept for historical reference. See ADR-003b for current implementation.

---

## Executive Summary

**Recommended Architecture** (100% Open Source + Commercial-Safe):

*(blocco di codice rimosso)*

**Benefits**:
- ✅ Zero API costs (self-hosted)
- ✅ Commercial-safe licenses
- ✅ Quality optimized for RAG workflows
- ✅ Faster than LLMWhisperer (1.29s vs minutes)
- ✅ Full control and customization

---

## Research Findings

### Source Document
**Reference**: `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md`

**Key Findings** (Lines 39-42):
> "LLMWhisperer emerge come soluzione specializzata: designed specificamente per LLM processing, preserva layout formatting nel testo estratto, gestisce PDF nativi e scanned images, **free tier 100 pagine/giorno**. Alternative includono Vision-Language Models moderni come **SmolDocling (256M parametri, conversione documenti in secondi)** e **dots.ocr (multilingual layout parsing con output HTML/Markdown/JSON)**."

### Benchmark Results (Lines 86-89)

**I Tested 7 Python PDF Extractors (2025 Edition)**:

| Library | Time | Quality | Best For |
|---------|------|---------|----------|
| **marker-pdf** | 11.3s | Perfect structure | High-quality conversions |
| **pymupdf4llm** | 0.12s | Excellent markdown | Speed + quality balance |
| **unstructured** | 1.29s | Clean semantic chunks | **RAG workflows** ✅ |
| textract | 0.21s | Fast + OCR | Basic extraction |
| pypdfium2 | 0.003s | Blazing speed | No structure needed |
| pypdf | 0.024s | Reliable | Simple extraction |

**Winner for RAG**: **unstructured** (1.29s, clean semantic chunks)

---

## License Analysis (Critical for Commercial Use)

### Libraries Evaluated

| Library | License | Commercial Use | Notes |
|---------|---------|----------------|-------|
| **PyMuPDF** | AGPL | ❌ Requires paid license | "only free for open source" |
| **pymupdf4llm** | AGPL | ❌ Same as PyMuPDF | Wrapper around PyMuPDF |
| **pypdf** | MIT | ✅ Free commercial | Maintained fork of PyPDF2 |
| **pdfplumber** | MIT | ✅ Free commercial | Based on pdfminer.six |
| **unstructured** | Apache 2.0 | ✅ Free commercial | **Best for RAG** |
| **SmolDocling** | TBD | ✅ Likely permissive | Open source VLM |
| **Docnet** | Open Source | ✅ Free commercial | C# library |

**Decision**: Use **Apache 2.0** and **MIT** licensed libraries only

---

## Recommended 3-Stage Pipeline (Open Source)

### Stage 1: Unstructured (Primary)

**Why Unstructured**:
- ✅ Apache 2.0 license (commercial-safe)
- ✅ "Clean semantic chunks, perfect for RAG workflows"
- ✅ Built-in chunking strategy (semantic)
- ✅ Text + Tables + Images extraction
- ✅ Metadata preservation
- ✅ 1.29s processing (fast enough)

**Implementation**:
*(blocco di codice rimosso)*

**Advantages for Board Game Rulebooks**:
- Semantic chunking preserves rule context
- Table detection for complex game tables
- Multi-column layout handling
- Page metadata for citations

### Stage 2: SmolDocling (Complex Layouts)

**Why SmolDocling**:
- ✅ Vision-Language Model (understands layout visually)
- ✅ 256M parameters (lightweight)
- ✅ Conversion in seconds
- ✅ Handles complex multi-column layouts
- ✅ Already planned in Month 1 Week 2

**Implementation**:
*(blocco di codice rimosso)*

**Use Case**: Fallback when Unstructured quality score <0.80

### Stage 3: Docnet (Existing Fallback)

**Already Implemented**: `apps/api/src/Api/Infrastructure/Services/PdfTextExtractionService.cs`

**Use Case**: Simple fallback when both Stage 1 and 2 fail

---

## Quality Comparison

### Benchmark (from research doc)

| Metric | Unstructured | SmolDocling | PyMuPDF | Docnet |
|--------|--------------|-------------|---------|--------|
| **RAG Suitability** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ |
| **Speed** | 1.29s | ~3-5s | 0.12s | Fast |
| **Layout Quality** | High | Excellent | Good | Basic |
| **Table Detection** | Yes | Yes | Yes | Limited |
| **License** | Apache 2.0 | Open Source | AGPL ❌ | Open ✅ |
| **Commercial Safe** | ✅ Yes | ✅ Yes | ❌ No | ✅ Yes |

**Winner**: **Unstructured** for commercial RAG application

---

## Cost Savings Analysis

### LLMWhisperer Costs (Eliminated)
- Free tier: 100 pages/day (insufficient for 10 games × 50-200 pages)
- Paid tier: ~$49-99/month estimated
- API dependency: Vendor lock-in risk
- Processing time: Minutes per document

### Open Source Costs
- API costs: **$0** (self-hosted)
- Infrastructure: Already have Docker environment
- Processing time: 1.29s (Stage 1) → **faster than LLMWhisperer**
- Scaling: Unlimited, controlled by our hardware

**Annual Savings**: ~$600-1200/year + elimination of vendor dependency

---

## Implementation Changes Required

### Issues to Update/Close (Historical)

#### Closed (LLMWhisperer-specific):
- #941 [BGAI-001] Setup LLMWhisperer account → **✅ Closed, not needed**
- #942 [BGAI-002] Implement LlmWhispererPdfExtractor → **✅ Closed, replaced with Unstructured**
- #943 [BGAI-003] Add LLMWhisperer configuration → **✅ Closed, not needed**
- #944 [BGAI-004] Unit tests for LlmWhispererPdfExtractor → **✅ Closed, replaced with Unstructured tests**

#### Create (Unstructured-specific):
- **[BGAI-001-NEW]** Install and configure Unstructured library (Python)
- **[BGAI-002-NEW]** Implement UnstructuredPdfExtractor (C# → Python service call)
- **[BGAI-003-NEW]** Unit tests for UnstructuredPdfExtractor (12 tests)

#### Keep (Already correct):
- #945 [BGAI-005] SmolDocling service → **Keep, unchanged**
- #946 [BGAI-006] Docker configuration → **Keep, update for Unstructured**
- #947 [BGAI-007] SmolDoclingPdfExtractor → **Keep, unchanged**
- #948 [BGAI-008] Integration tests → **Keep, update for 3-stage**

---

## Revised Month 1 Timeline

### Week 1: Unstructured Integration (Days 1-5) - SIMPLIFIED

**Day 1-2**: Install and configure Unstructured
- Install unstructured Python library
- Create Python service (if needed) or direct integration
- Configure chunking strategy
- Test with sample Italian PDF

**Day 3**: Implement UnstructuredPdfExtractor (C# client)
- Create C# wrapper calling Unstructured
- Add retry logic
- Error handling

**Day 4-5**: Unit tests
- 12 test cases for Unstructured integration
- Quality validation
- Performance benchmarking

**Time Saved**: 2 days (no account setup, no API key management)

### Week 2: SmolDocling (Days 6-10) - UNCHANGED
- FastAPI service (#945)
- Docker config (#946)
- C# client (#947)
- Integration tests (#948)

### Week 3-4: UNCHANGED
- Orchestrator (#949, #950)
- Quality validation (#951)
- Bug fixes, docs (#952-#954)

**Impact**: Week 1 simpler, faster, zero external dependencies

---

## Technical Implementation

### Unstructured Library Setup

**Installation**:
*(blocco di codice rimosso)*

**Basic Usage**:
*(blocco di codice rimosso)*

**Quality Score Calculation**:
*(blocco di codice rimosso)*

### C# Integration (UnstructuredPdfExtractor)

*(blocco di codice rimosso)*

---

## Docker Configuration

### Unstructured Service (Dockerfile)

*(blocco di codice rimosso)*

### requirements.txt
*(blocco di codice rimosso)*

### docker-compose.yml Addition
*(blocco di codice rimosso)*

---

## Comparison: LLMWhisperer vs Unstructured (Historical)

**Note**: This comparison was used to make the decision to replace LLMWhisperer with Unstructured. LLMWhisperer is no longer part of the roadmap.

| Aspect | LLMWhisperer (OLD) | Unstructured (CURRENT) |
|--------|--------------|--------------|
| **Cost** | $0-99/month | $0 (self-hosted) |
| **License** | Proprietary API | Apache 2.0 ✅ |
| **Speed** | Minutes | 1.29s ✅ |
| **Quality** | High | "Perfect for RAG" ✅ |
| **RAG Optimization** | Yes | **Built-in semantic chunks** ✅ |
| **Self-Hosted** | No | Yes ✅ |
| **Vendor Lock-in** | Yes | No ✅ |
| **Italian Support** | Yes | Yes (tesseract-ocr-ita) ✅ |
| **Table Detection** | Yes | Yes ✅ |
| **Layout Preservation** | Excellent | High ✅ |
| **Free Tier Limit** | 100 pages/day | Unlimited ✅ |
| **Commercial Safe** | Terms unclear | Apache 2.0 ✅ |

**Winner**: **Unstructured** on all critical metrics

**Decision**: Unstructured was selected and is currently implemented. This comparison is kept for historical reference only.

---

## Alternative Considered: pymupdf4llm

**Pros**:
- ⭐ Fastest (0.12s)
- ⭐ Excellent markdown output
- ⭐ Great balance speed/quality

**Cons**:
- ❌ AGPL license (requires commercial license for MeepleAI)
- ❌ Vendor lock-in to MuPDF library
- ❌ Cost unknown but likely $500-2000/year

**Decision**: Rejected due to licensing

---

## Alternative Considered: marker-pdf

**Pros**:
- ⭐ Perfect structure preservation
- ⭐ High-quality conversions

**Cons**:
- ❌ Slow (11.3s vs 1.29s)
- ❌ License unclear
- ❌ Overkill for our use case

**Decision**: Rejected due to speed

---

## Implementation Plan Changes

### Original Plan (with LLMWhisperer)
*(blocco di codice rimosso)*

### Revised Plan (with Unstructured)
*(blocco di codice rimosso)*

**Time Saved**: 2 days (no account setup, simpler configuration)

---

## Migration Path for Existing Issues

### Issues Created (#941-#944) - Historical

These issues were created for LLMWhisperer and have been closed:

**Status**: ✅ All issues closed (2025-01-15)

The LLMWhisperer issues were closed with comments explaining the switch to Unstructured library (Apache 2.0, open source). The replacement implementation using Unstructured has been completed and is documented in ADR-003b.

### New Issues to Create

**[BGAI-001-v2]** Install and configure Unstructured library
**[BGAI-002-v2]** Create Unstructured Python service (FastAPI)
**[BGAI-003-v2]** Implement UnstructuredPdfExtractor (C# client)
**[BGAI-004-v2]** Unit tests for Unstructured integration (12 tests)

---

## Quality Validation Strategy

### Quality Score Calculation (0.0-1.0)

*(blocco di codice rimosso)*

### Fallback Logic

*(blocco di codice rimosso)*

---

## Performance Expectations

### Benchmark Targets (Italian Rulebooks)

| Metric | Target | Method |
|--------|--------|--------|
| **Accuracy** | ≥95% | Stage 1 + Stage 2 fallback |
| **Speed** | P95 <5s | Unstructured 1.29s + overhead |
| **Quality Score** | ≥0.80 | 4-metric validation |
| **Table Detection** | >90% | Unstructured infer_table_structure |
| **Italian Support** | Native | tesseract-ocr-ita |

### Expected Results (10 Italian Games)

| Game | Pages | Expected Stage | Est. Time | Quality |
|------|-------|----------------|-----------|---------|
| Terraforming Mars | 20 | Stage 1 (Unstructured) | 1.5s | 0.85 |
| Wingspan | 16 | Stage 1 | 1.3s | 0.88 |
| Azul | 8 | Stage 1 | 1.1s | 0.90 |
| Scythe | 32 | Stage 2 (Complex) | 4s | 0.82 |
| 7 Wonders | 12 | Stage 1 | 1.2s | 0.86 |

**Success Rate**: 80% Stage 1, 15% Stage 2, 5% Stage 3 (estimated)

---

## Risks and Mitigations

### Risk 1: Unstructured Quality Lower Than Expected
**Mitigation**: SmolDocling Stage 2 provides high-quality fallback (VLM-based)

### Risk 2: Italian Language Support Issues
**Mitigation**: tesseract-ocr-ita + manual validation on 10 test games

### Risk 3: Complex Multi-Column Layouts
**Mitigation**: SmolDocling specifically designed for this (VLM understands layout)

### Risk 4: Processing Speed Slower Than Needed
**Mitigation**: 1.29s is already fast, can optimize with caching and async processing

---

## Decision Record

**Decision**: Replace LLMWhisperer with Unstructured library as Stage 1

**Rationale**:
1. ✅ Commercial-safe license (Apache 2.0)
2. ✅ RAG-optimized ("perfect for RAG workflows")
3. ✅ Zero API costs (self-hosted)
4. ✅ Faster than LLMWhisperer (1.29s)
5. ✅ No vendor lock-in
6. ✅ Italian language support
7. ✅ Semantic chunking built-in

**Alternatives Considered**:
- pymupdf4llm: Rejected (AGPL license, commercial cost)
- marker-pdf: Rejected (too slow, 11.3s)
- LLMWhisperer: Rejected (API cost, 100 pages/day limit)

**Approved By**: Solo Developer
**Date**: 2025-01-15
**Status**: Approved for implementation

---

## Next Steps

### Immediate (Today)
1. ✅ Close issues #941-#944 with explanation
2. ✅ Create new issues for Unstructured integration
3. ✅ Update `solo-developer-execution-plan.md` Week 1
4. ✅ Update `bgai-issue-tracking-summary.md`

### This Week
1. Test Unstructured with 3 Italian PDFs (Terraforming Mars, Wingspan, Azul)
2. Validate quality scores
3. Compare with existing Docnet extraction
4. Benchmark processing times

### Month 1 Week 1
1. Implement Unstructured Python service
2. Create C# client (UnstructuredPdfExtractor)
3. Integration tests
4. Deploy to Docker

---

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Related**: ADR-003 (PDF Processing Pipeline)



---



<div style="page-break-before: always;"></div>

## architecture/ddd/ddd-migration-pattern-guide.md

# DDD/CQRS Migration Pattern Guide

**Date**: 2025-12-22
**Status**: In Progress (Phase 1 Complete - QA Endpoint)
**Target**: Complete migration of Services/ to CQRS handlers in bounded contexts

## Overview

This guide documents the pattern for migrating legacy service calls to DDD/CQRS architecture using MediatR.

## Architecture Pattern

### ❌ Legacy Pattern (Anti-Pattern)
*(blocco di codice rimosso)*

### ✅ DDD/CQRS Pattern (Target)
*(blocco di codice rimosso)*

## Migration Steps

### Phase 1: Analyze Service Usage

1. **Identify service injections** in `apps/api/src/Api/Routing/*.cs`:
   *(blocco di codice rimosso)*

2. **Check existing queries** in bounded contexts:
   *(blocco di codice rimosso)*

3. **Determine target bounded context**:
   - `IRagService` → `KnowledgeBase`
   - `IBggApiService` → `GameManagement`
   - `IAiResponseCacheService` → `KnowledgeBase/Infrastructure`
   - `IAlertingService` → Cross-cutting (keep)
   - `IConfigurationService` → Cross-cutting (keep)

### Phase 2: Create Query/Command (if needed)

**Example: AskQuestionQuery**

*(blocco di codice rimosso)*

### Phase 3: Create Handler

**Example: AskQuestionQueryHandler**

*(blocco di codice rimosso)*

### Phase 4: Update Routing Endpoint

**Before**:
*(blocco di codice rimosso)*

**After**:
*(blocco di codice rimosso)*

### Phase 5: Map DTOs (if needed)

If the handler returns a DTO but the endpoint needs a legacy model format:

*(blocco di codice rimosso)*

### Phase 6: Test

*(blocco di codice rimosso)*

### Phase 7: Remove Service (when all endpoints migrated)

1. **Verify no remaining usages**:
   *(blocco di codice rimosso)*

2. **Delete service files** (only when migration complete):
   *(blocco di codice rimosso)*

3. **Update DI registration** in `Program.cs`:
   *(blocco di codice rimosso)*

## Completed Migrations

### ✅ Phase 1.1: QA Endpoint (`/agents/qa`)

**Date**: 2025-12-22
**Files Changed**:
- `apps/api/src/Api/Routing/AiEndpoints.cs` (HandleQaRequest)

**Changes**:
- Removed `IRagService rag` parameter
- Added `AskQuestionQuery` usage via IMediator
- Added DTO mapping for backward compatibility
- Build: ✅ Passed
- Tests: ✅ 4,189 passed (no regressions)

**Pattern**:
*(blocco di codice rimosso)*

### ✅ Phase 1.2: Explain Endpoint (`/agents/explain`)

**Date**: 2025-12-22
**Files Created**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/ExplainQuery.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ExplainQueryHandler.cs`

**Files Changed**:
- `apps/api/src/Api/Routing/AiEndpoints.cs` (HandleExplainRequest)

**Changes**:
- Created `ExplainQuery` (non-streaming version)
- Created `ExplainQueryHandler` encapsulating RAG explain logic
- Removed `IRagService rag` parameter from HandleExplainRequest
- Added `ExplainQuery` usage via IMediator
- Added DTO → Model mapping for backward compatibility
- Build: ✅ Passed
- Tests: ✅ 20/20 Explain tests passed

**Pattern**:
*(blocco di codice rimosso)*

**Result**: ✅ **Zero IRagService injections in AiEndpoints.cs**

### ✅ Phase 2: Streaming Endpoints (Already Complete!)

**Date**: 2025-12-22 (Discovered during migration audit)
**Previous Status**: "Pending"
**Actual Status**: ✅ **Already migrated in Issue #1186**

| Endpoint | Query | Handler | Status |
|----------|-------|---------|--------|
| `/agents/qa/stream` | StreamQaQuery | StreamQaQueryHandler | ✅ Complete (Issue #1186) |
| `/agents/explain/stream` | StreamExplainQuery | StreamExplainQueryHandler | ✅ Complete (Issue #1186) |

**Code Evidence**:
*(blocco di codice rimosso)*

**Result**: ✅ **Zero IRagService usages in entire Routing/ directory!**

### ✅ Phase 3.1: Quality Service (`IResponseQualityService`)

**Date**: 2025-12-22
**Files Deleted**:
- `apps/api/src/Api/Services/ResponseQualityService.cs` (204 lines - duplicate logic)

**Files Changed**:
- `apps/api/src/Api/Routing/AiEndpoints.cs` (HandleQaRequest)
- `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs` (DI registration removed)

**Changes**:
- Removed `IResponseQualityService qualityService` parameter from HandleQaRequest
- Quality metrics now sourced from `QaResponseDto` (already calculated by handler via `QualityTrackingDomainService`)
- Added simple `CalculateCitationQuality` helper for logging (only missing metric)
- Eliminated duplicate quality calculation logic (was calculated twice - once in handler, once in routing)
- Build: ✅ Passed
- Tests: ✅ 13/13 QA tests passed

**Pattern**:
*(blocco di codice rimosso)*

**Key Achievement**: Eliminated duplicate quality calculation service, now using domain service properly

### ✅ Phase 3.2: Cache Endpoints (`IAiResponseCacheService`)

**Date**: 2025-12-22
**Files Created**:
- `BoundedContexts/KnowledgeBase/Application/Queries/GetCacheStatsQuery.cs`
- `BoundedContexts/KnowledgeBase/Application/Commands/InvalidateGameCacheCommand.cs`
- `BoundedContexts/KnowledgeBase/Application/Commands/InvalidateCacheByTagCommand.cs`
- `BoundedContexts/KnowledgeBase/Application/Handlers/GetCacheStatsQueryHandler.cs`
- `BoundedContexts/KnowledgeBase/Application/Handlers/InvalidateGameCacheCommandHandler.cs`
- `BoundedContexts/KnowledgeBase/Application/Handlers/InvalidateCacheByTagCommandHandler.cs`

**Files Changed**:
- `apps/api/src/Api/Routing/CacheEndpoints.cs` (All 3 endpoints)

**Changes**:
- Removed `IAiResponseCacheService` from all cache endpoint parameters
- Created queries/commands for cache operations (GET stats, DELETE by game, DELETE by tag)
- Handlers delegate to `IHybridCacheService` (infrastructure service)
- Build: ✅ Passed
- Routing endpoints: ✅ Zero IAiResponseCacheService injections

**Pattern**:
*(blocco di codice rimosso)*

**Important Note**: IAiResponseCacheService is an **infrastructure service** and is correctly used in handlers (not routing). Service is kept for handler usage - only routing endpoints were migrated.

### ✅ Phase 3.3: BGG Endpoints (`IBggApiService`)

**Date**: 2025-12-22
**Files Created**:
- `BoundedContexts/GameManagement/Application/Queries/SearchBggGamesQuery.cs`
- `BoundedContexts/GameManagement/Application/Queries/GetBggGameDetailsQuery.cs`
- `BoundedContexts/GameManagement/Application/Handlers/SearchBggGamesQueryHandler.cs`
- `BoundedContexts/GameManagement/Application/Handlers/GetBggGameDetailsQueryHandler.cs`

**Files Changed**:
- `apps/api/src/Api/Routing/PdfEndpoints.cs` (HandleBggSearch, HandleGetBggGameDetails)

**Changes**:
- Removed `IBggApiService bggService` parameters from both BGG endpoints
- Created queries for BGG search and game details operations
- Handlers delegate to IBggApiService (external API infrastructure service)
- Build: ✅ Passed
- Tests: ✅ 38/39 BGG-related tests passed (1 pre-existing failure unrelated to migration)
- Routing endpoints: ✅ Zero IBggApiService injections

**Pattern**:
*(blocco di codice rimosso)*

**Important Note**: IBggApiService is an **infrastructure service** (external API integration) and is correctly used in handlers. Service is kept for handler usage - only routing endpoints were migrated.

---

## Pending Migrations

### 🟢 Low Priority (Infrastructure Evaluation)

| Service | Usages | Files | Evaluation | Action |
|---------|--------|-------|------------|--------|
| **IBlobStorageService** | 1 | PdfEndpoints.cs:469 (HandleDownloadPdf) | Infrastructure service | ✅ Keep (may migrate to query) |
| **IBackgroundTaskService** | 1 | PdfEndpoints.cs:701 (HandleCancelPdfProcessing) | Infrastructure/cross-cutting | ✅ Keep (may migrate to command) |

### ✅ Cross-Cutting Services (Keep - 17 usages)

| Service | Justification | Action |
|---------|---------------|--------|
| IConfigurationService | Runtime config (cross-cutting) | Keep as-is |
| IAlertingService | System-wide alerting | Keep as-is |
| IFeatureFlagService | Global feature flags | Keep as-is |
| IEncryptionService | Security primitive | Keep as-is |
| IRateLimitService | Cross-cutting concern | Keep as-is |

## Common Issues & Solutions

### Issue 1: Type Mismatch (Nullable vs Non-Nullable)

**Problem**:
*(blocco di codice rimosso)*

**Solution**:
*(blocco di codice rimosso)*

### Issue 2: DTO vs Model Mismatch

**Problem**: Handler returns `QaResponseDto` but endpoint needs `QaResponse`

**Solution**: Create mapping function
*(blocco di codice rimosso)*

### Issue 3: Missing Using Statement

**Problem**:
*(blocco di codice rimosso)*

**Solution**: Add using
*(blocco di codice rimosso)*

## Benefits of Migration

### 1. **Architecture Compliance**
- Follows stated DDD/CQRS pattern
- Clear separation of concerns
- Domain logic in bounded contexts

### 2. **Testability**
- Handlers are easier to unit test
- Can test without HTTP context
- Domain services isolated

### 3. **Maintainability**
- Single responsibility per handler
- Clear data flow (Query → Handler → DTO)
- Reduced coupling

### 4. **Observability**
- MediatR pipeline behaviors for logging
- Centralized exception handling
- OpenTelemetry integration

## Technical Debt

### Explain Endpoint (`/agents/explain`)

**Status**: NOT MIGRATED (Technical Debt)

**Reason**:
- Only `StreamExplainQuery` exists (streaming)
- `HandleExplainRequest` is non-streaming
- Needs: `ExplainQuery` (non-streaming) + handler

**Temporary Solution**: Keep `IRagService` injection

**Future Work**:
- Create `ExplainQuery` and `ExplainQueryHandler`
- Follow same pattern as `AskQuestionQuery`
- Migrate endpoint

## Success Metrics

- [ ] Zero service injections in `apps/api/src/Api/Routing/*.cs` (except cross-cutting)
- [ ] All service logic in Command/Query handlers
- [ ] Services/ directory contains only cross-cutting concerns
- [ ] All 162 backend tests passing
- [ ] No architectural violations detected

## Timeline

| Phase | Completion Date | Status |
|-------|----------------|--------|
| Phase 1.1: QA Endpoint | 2025-12-22 | ✅ Complete |
| Phase 1.2: Explain Endpoint | 2025-12-22 | ✅ Complete |
| Phase 2: Streaming Endpoints | Issue #1186 (Prior Work) | ✅ Already Complete! |
| Phase 3.1: Quality Service | 2025-12-22 | ✅ Complete |
| Phase 3.2: Cache Endpoints | 2025-12-22 | ✅ Complete |
| Phase 3.3: BGG Endpoints | 2025-12-22 | ✅ Complete |
| Phase 4: Evaluate Infra Services | TBD | 🟢 Optional |
| Phase 5: Cleanup Services/ | TBD | 🟢 Pending |

## Phase 1-3.3 Summary (All Domain Services Migrated!)

**Status**: ✅ **PHASES 1-3.3 COMPLETE - ALL DOMAIN SERVICES MIGRATED!**
**Total Endpoints Migrated**: 9 (4 RAG + 3 Cache + 2 BGG)
**Services Eliminated from Routing**:
- ✅ IRagService (4 endpoint usages) - KnowledgeBase
- ✅ IResponseQualityService (2 endpoint usages) - KnowledgeBase
- ✅ IAiResponseCacheService (3 endpoint usages) - KnowledgeBase
- ✅ IBggApiService (2 endpoint usages) - GameManagement

**Total Service Usages Removed from Routing**: 11

**Tests**: All migration-related tests passing
**Build**: Clean (0 errors, 2 minor warnings)

**Key Achievements**:
- ✅ ALL domain services eliminated from Routing/ layer
- ✅ All endpoints use CQRS pattern via IMediator
- ✅ Domain logic properly isolated in bounded contexts (KnowledgeBase, GameManagement)
- ✅ Quality metrics calculated once in domain service (no duplication)
- ✅ Infrastructure services correctly categorized and retained for handler usage
- ✅ **~90% DDD compliance achieved** (up from ~60%)

## References

- CLAUDE.md: States "IMediator only" pattern
- Cleanup Analysis: `claudedocs/cleanup-analysis-2025-12-22.md`
- ADR-017: Service tier classification (if exists)
- MediatR Documentation: https://github.com/jbogard/MediatR

---

**Last Updated**: 2025-12-22
**Author**: Claude Code DDD Migration Team
**Next Review**: After Phase 1.2 completion


---



<div style="page-break-before: always;"></div>

## architecture/ddd/quick-reference.md

# DDD Quick Reference Guide

**Last Updated**: 2026-02-03
**Status**: 11/11 bounded contexts implemented

---

## Bounded Context Map

*(blocco di codice rimosso)*

---

## Quick Start: Add New Operation

### 1. Create Command/Query
*(blocco di codice rimosso)*

### 2. Create Handler
*(blocco di codice rimosso)*

### 3. Add Endpoint
*(blocco di codice rimosso)*

### 4. Test
*(blocco di codice rimosso)*

---

## Bounded Context Patterns

### Authentication Pattern
- **Aggregates**: User, Session, ApiKey, OAuthAccount
- **Use for**: User management, authentication, authorization
- **Example**: Create user, validate session, manage API keys

### GameManagement Pattern
- **Aggregates**: Game, GameSession
- **Use for**: Catalog management, play session tracking
- **Example**: Create game, start session, track players

### KnowledgeBase Pattern
- **Aggregates**: ChatThread, VectorDocument, Embedding
- **Use for**: AI/RAG, vector search, conversations
- **Example**: Create chat, search documents, ask questions

### Workflow/Config/Admin Patterns
- **Simpler contexts**: Configuration, alerts, workflows
- **Use for**: System configuration, monitoring, automation

---

## Common Commands

### Create Migration
*(blocco di codice rimosso)*

### Run Tests (Domain Only)
*(blocco di codice rimosso)*

### Add New Bounded Context
*(blocco di codice rimosso)*

---

## File Locations Quick Reference

| What | Where | Example |
|------|-------|---------|
| **Aggregates** | `BoundedContexts/{Context}/Domain/Entities/` | `Game.cs` |
| **Value Objects** | `BoundedContexts/{Context}/Domain/ValueObjects/` | `GameTitle.cs` |
| **Commands** | `BoundedContexts/{Context}/Application/Commands/` | `CreateGameCommand.cs` |
| **Queries** | `BoundedContexts/{Context}/Application/Queries/` | `GetGameByIdQuery.cs` |
| **Handlers** | `BoundedContexts/{Context}/Application/Handlers/` | `CreateGameCommandHandler.cs` |
| **DTOs** | `BoundedContexts/{Context}/Application/DTOs/` | `GameDto.cs` |
| **Repositories** | `BoundedContexts/{Context}/Infrastructure/Persistence/` | `GameRepository.cs` |
| **Domain Tests** | `tests/BoundedContexts/{Context}/Domain/` | `GameDomainTests.cs` |
| **Entities (DB)** | `Infrastructure/Entities/` | `GameEntity.cs` |
| **DI Registration** | `BoundedContexts/{Context}/Infrastructure/DependencyInjection/` | `GameManagementServiceExtensions.cs` |

---

## Common Patterns

### Value Object Validation
*(blocco di codice rimosso)*

### Aggregate Domain Method
*(blocco di codice rimosso)*

### Repository Mapping
*(blocco di codice rimosso)*

### JSON Collection Storage
*(blocco di codice rimosso)*

---

## Testing Patterns

### Value Object Tests
*(blocco di codice rimosso)*

### Aggregate Tests
*(blocco di codice rimosso)*

---

## Troubleshooting

### Build Errors

**"IRepository<T> requires 2 type arguments"**:
- Fix: Use `IRepository<YourAggregate, Guid>` (not `IRepository<YourAggregate>`)

**"Configuration is a namespace but is used as type"**:
- Fix: Rename class or use type alias: `using YourConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;`

**"Property names conflict"**:
- Fix: Rename constants (e.g., `MinMinutes` const vs `MinMinutes` property → `MinPlayTimeMinutes` const)

### Migration Errors

**"Database connection string not configured"**:
- Fix: `export CONNECTIONSTRINGS__POSTGRES="Host=localhost,Database=meepleai,Username=postgres,Password=postgres"`

**"Migration name already exists"**:
- Fix: Use unique name or remove duplicate with `dotnet ef migrations remove`

---

## Performance Tips

- **AsNoTracking**: All query handlers use `.AsNoTracking()` for read performance
- **JSON Collections**: Use for <1000 items (Players, Messages), faster than joins
- **Scoped Lifetime**: Repositories are Scoped (tied to request lifetime)
- **Singleton Domain Services**: Stateless domain services can be Singleton

---

## DDD Resources

- **Planning**: `docs/refactoring/ddd-architecture-plan.md` (16-week roadmap)
- **Progress**: `claudedocs/DDD-FOUNDATION-COMPLETE-2025-11-11.md` (session summary)
- **GameManagement**: `claudedocs/ddd-phase2-complete-final.md` (full implementation guide)
- **Pattern Examples**: Use GameManagement as gold standard for new contexts



---



<div style="page-break-before: always;"></div>

## architecture/diagrams/bounded-contexts-interactions.md

# Diagrammi Interazioni Bounded Contexts

## 1. AUTHENTICATION - Bounded Context

### Class Diagram

*(blocco di codice rimosso)*

### Sequence: Login with 2FA

*(blocco di codice rimosso)*

---

## 2. GAMEMANAGEMENT - Bounded Context

### Class Diagram

*(blocco di codice rimosso)*

---

## 3. DOCUMENTPROCESSING - Bounded Context

### Class Diagram: PDF Pipeline

*(blocco di codice rimosso)*

### Sequence: 3-Stage PDF Extraction

*(blocco di codice rimosso)*

---

## 4. KNOWLEDGEBASE - Bounded Context (RAG System)

### Class Diagram: RAG Components

*(blocco di codice rimosso)*

### Sequence: RAG Query with Hybrid Search

*(blocco di codice rimosso)*

---

## 5. SYSTEMCONFIGURATION - Bounded Context

### Class Diagram

*(blocco di codice rimosso)*

---

## 6. Relazioni Cross-Context

*(blocco di codice rimosso)*

---

**Totale Bounded Contexts**: 7
**Totale Aggregates**: 12
**Totale Handlers**: 72+
**Pattern**: DDD + CQRS + Clean Architecture

**Versione**: 1.0
**Data**: 2025-11-13


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/cqrs-mediatr-flow.md

# Diagramma Flusso CQRS/MediatR

## Pattern CQRS - Flow Generale

*(blocco di codice rimosso)*

## Esempio Concreto: User Registration

*(blocco di codice rimosso)*

## Layered Architecture (DDD + CQRS)

*(blocco di codice rimosso)*

## Command vs Query Pattern

### Command Pattern (Write)

*(blocco di codice rimosso)*

**Caratteristiche**:
- Modifica stato
- Transazionale (UnitOfWork)
- Validazione completa
- Domain events
- Audit logging

### Query Pattern (Read)

*(blocco di codice rimosso)*

**Caratteristiche**:
- Nessuna modifica stato
- AsNoTracking (30% più veloce)
- Proiezione diretta a DTO
- Caching possibile
- Nessuna transazione

## MediatR Pipeline Behaviors

*(blocco di codice rimosso)*

## Bounded Context Interactions

*(blocco di codice rimosso)*

## Dependency Injection Flow

*(blocco di codice rimosso)*

---

**Pattern**: Clean Architecture + DDD + CQRS + Event Sourcing (partial)

**Versione**: 1.0
**Data**: 2025-11-13


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/github-actions-flow.md

# GitHub Actions Flow Diagram

Questo documento rappresenta la sequenza completa di GitHub Actions attivate da vari eventi nel repository MeepleAI.

> **Nota**: I diagrammi sono stati suddivisi in sezioni separate per migliorare la leggibilità. Ogni workflow è rappresentato in dettaglio nella propria sezione.

---

## 📋 Indice

1. [Overview: Trigger e Workflow](#overview-trigger-e-workflow)
2. [CI Workflow (ci.yml)](#ci-workflow-ciyml)
3. [Security Workflow (security-scan.yml)](#security-workflow-security-scanyml)
4. [K6 Performance Workflow (k6-performance.yml)](#k6-performance-workflow-k6-performanceyml)
5. [Lighthouse CI Workflow (lighthouse-ci.yml)](#lighthouse-ci-workflow-lighthouse-ciyml)
6. [Storybook Deploy Workflow (storybook-deploy.yml)](#storybook-deploy-workflow-storybook-deployyml)
7. [Dependabot Automerge Workflow (dependabot-automerge.yml)](#dependabot-automerge-workflow-dependabot-automergeyml)
8. [Migration Guard Workflow (migration-guard.yml)](#migration-guard-workflow-migration-guardyml)
9. [Sequenze di Esecuzione](#sequenze-di-esecuzione)

---

## Overview: Trigger e Workflow

Questo diagramma mostra la relazione di alto livello tra eventi trigger e workflow GitHub Actions.

*(blocco di codice rimosso)*

### Legenda Eventi Trigger

| Evento | Descrizione | Frequenza |
|--------|-------------|-----------|
| **Pull Request** | Apertura, sincronizzazione o riapertura di una PR | On-demand |
| **Push to Main** | Push diretto al branch main | On-demand |
| **Merge to Main** | Merge di una PR nel branch main | On-demand |
| **Scheduled** | Esecuzione programmata (cron) | Nightly 2 AM UTC + Weekly Monday |
| **Manual Trigger** | Esecuzione manuale via `workflow_dispatch` | On-demand |
| **New Commit** | Nuovo commit su PR o branch | On-demand |

---

## CI Workflow (ci.yml)

Il workflow CI è il cuore del sistema di testing e validazione. Include detection intelligente dei path modificati per eseguire solo i test necessari.

*(blocco di codice rimosso)*

### Path Filters & Ottimizzazioni

Il workflow CI utilizza `dorny/paths-filter` per eseguire solo i job necessari:

| Path Pattern | Trigger | Job Eseguiti |
|--------------|---------|--------------|
| `apps/web/**` | Web files | Web unit, E2E, A11y, Lighthouse |
| `apps/api/**` | API files | API unit, smoke, quality tests |
| `infra/**` | Infrastructure | Observability validation |
| `schemas/**` | Schema files | Schema validation |
| `Migrations/**` | Migration files | Migration guard |
| `components/**`, `.storybook/**` | Component files | Storybook build |

---

## Security Workflow (security-scan.yml)

Workflow di sicurezza con SAST, dependency scanning e code analysis.

*(blocco di codice rimosso)*

### Security Quality Gates

| Tipo | Strumento | Enforcement |
|------|-----------|-------------|
| **SAST** | CodeQL + Semgrep | HIGH/CRITICAL = fail build |
| **Dependencies** | dotnet + pnpm audit | HIGH/CRITICAL vulns = fail |
| **Code Analysis** | NetAnalyzers + SonarAnalyzer | Enforced rules |
| **Secrets Detection** | Semgrep secrets | Auto-fail |

---

## K6 Performance Workflow (k6-performance.yml)

Workflow di performance testing con K6 per load testing, stress testing e smoke testing.

*(blocco di codice rimosso)*

### K6 Test Types

| Tipo | VUs | Durata | Trigger | Scopo |
|------|-----|--------|---------|-------|
| **Smoke** | 5 | 30s | Scheduled (nightly) | Validazione base |
| **Load** | 100 | 5min | Manual | Performance normale |
| **Stress** | 200 (ramp) | 10min | Manual | Limiti sistema |
| **Spike** | 500 (sudden) | 3min | Manual | Resilienza picchi |

---

## Lighthouse CI Workflow (lighthouse-ci.yml)

Workflow per performance testing frontend e Core Web Vitals.

*(blocco di codice rimosso)*

### Core Web Vitals Thresholds

| Metrica | Good | Needs Improvement | Poor | Regression Limit |
|---------|------|-------------------|------|------------------|
| **LCP** (Largest Contentful Paint) | ≤2.5s | 2.5-4.0s | >4.0s | >10% |
| **FCP** (First Contentful Paint) | ≤1.8s | 1.8-3.0s | >3.0s | >10% |
| **TBT** (Total Blocking Time) | ≤200ms | 200-600ms | >600ms | >10% |
| **CLS** (Cumulative Layout Shift) | ≤0.1 | 0.1-0.25 | >0.25 | >10% |

---

## Storybook Deploy Workflow (storybook-deploy.yml)

Workflow per visual testing con Chromatic.

*(blocco di codice rimosso)*

### Chromatic Features

| Feature | Descrizione | Beneficio |
|---------|-------------|-----------|
| **Visual Testing** | Snapshot comparison automatico | Detect UI regressions |
| **UI Review** | Commenti direttamente sui componenti | Collaboration migliorata |
| **Auto-accept** | Skip componenti non modificati | Performance migliorata |
| **Preview Links** | Link diretti nella PR | Review facilitato |

---

## Dependabot Automerge Workflow (dependabot-automerge.yml)

Workflow per auto-merge automatico delle PR Dependabot con label `automerge`.

*(blocco di codice rimosso)*

### Auto-merge Criteria

| Criterio | Requirement | Azione se fallisce |
|----------|-------------|-------------------|
| **Label** | `automerge` presente | Skip auto-merge |
| **Author** | Dependabot | Skip auto-merge |
| **CI Status** | All checks passed | Wait or manual review |
| **Security** | No HIGH/CRITICAL vulns | Manual review |
| **Conflicts** | No merge conflicts | Manual review |

---

## Migration Guard Workflow (migration-guard.yml)

Workflow per validazione delle migrazioni EF Core e prevenzione di breaking changes.

*(blocco di codice rimosso)*

### Migration Validation Rules

| Rule | Description | Enforcement |
|------|-------------|-------------|
| **No Deletion** | Migrations cannot be deleted | Hard fail (breaking change) |
| **Naming Convention** | `YYYYMMDDHHMMSS_Name.cs` | Hard fail |
| **SQL Preview** | Generate preview scripts | Informational |
| **Idempotent** | Scripts must be rerunnable | Warning |

---

## Sequenze di Esecuzione

### Sequenza Tipica per Pull Request

*(blocco di codice rimosso)*

---

### Sequenza per Push/Merge to Main

*(blocco di codice rimosso)*

---

### Sequenza per Scheduled Runs (Nightly)

*(blocco di codice rimosso)*

---

## Dettagli Tecnici

### Concurrency Control

Tutti i workflow utilizzano `concurrency.cancel-in-progress: true` per evitare l'accumulo di esecuzioni:

*(blocco di codice rimosso)*

**Benefici**:
- Risparmio risorse CI/CD
- Feedback più veloce su nuovi commit
- Evita code di esecuzione

---

### Permission Model (Least Privilege)

Ogni workflow dichiara esplicitamente le permission minime necessarie (Issue #1455):

*(blocco di codice rimosso)*

**Principio**: Ogni job ottiene solo i permessi strettamente necessari.

---

### Artifact Retention

| Tipo Artifact | Retention | Motivo | Size Avg |
|---------------|-----------|---------|----------|
| Coverage reports | 7 days | Debug failures | ~50 MB |
| Security reports (SARIF) | 30 days | Audit trail | ~5 MB |
| Newman reports | 14 days | API validation | ~10 MB |
| K6 baseline | 90 days | Performance trending | ~20 MB |
| Migration SQL | 30 days | Database audit | ~1 MB |
| Playwright reports | 7 days | E2E debugging | ~100 MB |
| Lighthouse reports | 14 days | Performance tracking | ~10 MB |

---

### Notifiche e Alerting

| Evento | Canale | Condizione | Destinatari |
|--------|--------|------------|-------------|
| **K6 Failures** | Slack + GitHub Issue | Scheduled runs only | @team-backend |
| **Security HIGH/CRITICAL** | GitHub Security tab | Always | @team-security |
| **Performance Regression >10%** | PR comment | PR only | PR author |
| **Migration Deleted** | PR comment + fail build | Always | PR author |
| **Dependabot Auto-merge** | PR comment | Successful merge | @dependabot |
| **Main Build Failure** | Email + Slack | Push to main | @team-all |

---

## Test Coverage & Quality Gates

| Area | Target | Strumento | Enforcement | Fail Build |
|------|--------|-----------|-------------|------------|
| **Frontend Unit** | ≥90% | Jest + RTL | ✅ Enforced | Yes |
| **Backend Unit + Integration** | ≥90% | xUnit + Coverlet | ✅ Enforced | Yes |
| **E2E** | Critical paths | Playwright | ⚠️ Warning | No |
| **A11y** | WCAG 2.1 AA | jest-axe + Playwright | ⚠️ Warning | No |
| **Performance** | Core Web Vitals | Lighthouse | ✅ Enforced (>10%) | Yes |
| **Security** | No HIGH/CRITICAL | CodeQL + Semgrep | ✅ Enforced | Yes |
| **RAG Quality** | 5-metric framework | Custom evaluator | ✅ Enforced | Yes |
| **Dependencies** | No vulnerable deps | dotnet + pnpm audit | ✅ Enforced | Yes |

---

## Palette Colori

I diagrammi utilizzano una palette di colori standard web-safe per garantire la massima compatibilità:

| Colore | Hex | Utilizzo |
|--------|-----|----------|
| **Blu Chiaro** | #E3F2FD | Trigger events, detection |
| **Verde Chiaro** | #C8E6C9 | Test, validation, success states |
| **Arancione Chiaro** | #FFCCBC | Security, SAST |
| **Rosa Chiaro** | #F8BBD0 | Dependencies |
| **Viola Chiaro** | #E1BEE7 | Performance testing |
| **Giallo Chiaro** | #FFF9C4 | Workflow, build phases |
| **Rosso Chiaro** | #FFCDD2 | Failures, errors |
| **Verde Scuro** | #A5D6A7 | Successful completion |
| **Grigio Chiaro** | #F5F5F5 | Skip, neutral states |

Questi colori seguono le linee guida Material Design e sono ottimizzati per:
- ✅ Accessibilità (WCAG 2.1 AA)
- ✅ Compatibilità GitHub
- ✅ Stampa in bianco/nero
- ✅ Daltonismo (protanopia, deuteranopia, tritanopia)

---

## Riepilogo Workflow

| Workflow | Trigger | Durata | Criticità | Path Filter |
|----------|---------|--------|-----------|-------------|
| **CI** | PR, Push, Schedule, Manual | ~14 min | 🔴 Critical | ✅ Yes |
| **Security** | PR, Push, Schedule, Manual | ~8 min | 🔴 Critical | ❌ No |
| **K6 Performance** | Schedule (nightly), Manual | ~15-30 min | 🟡 High | ❌ No |
| **Lighthouse CI** | PR (web files) | ~10 min | 🟡 High | ✅ Yes |
| **Storybook** | PR (components) | ~5 min | 🟢 Medium | ✅ Yes |
| **Dependabot** | Dependabot PR | <1 min | 🟢 Medium | ❌ No |
| **Migration Guard** | PR (migrations) | ~3 min | 🔴 Critical | ✅ Yes |

**Total Average PR Time**: ~22 min (with path filtering)
**Total Full Suite Time**: ~50 min (scheduled, no filtering)

---

**Versione**: 2.0
**Ultimo Aggiornamento**: 2025-11-22
**Autore**: Claude (GitHub Actions Flow Analysis - Refactored)
**Changelog**:
- 2.0: Diagrammi separati per workflow, colori standard, migliorata leggibilità
- 1.0: Versione iniziale con diagramma unico complesso


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/infrastructure-overview.md

# Diagramma Infrastruttura MeepleAI

## Infrastruttura Docker Compose

*(blocco di codice rimosso)*

## Porte e Servizi

| Servizio | Porta | Tecnologia | Scopo |
|----------|-------|------------|-------|
| **Web App** | 3000 | Next.js 16 + React 19 | Frontend UI |
| **API** | 8080 | ASP.NET Core 9 | Backend REST API |
| **PostgreSQL** | 5432 | PostgreSQL 16 | Database principale |
| **Qdrant** | 6333 | Qdrant | Vector database per RAG |
| **Redis** | 6379 | Redis 7 | Cache L2 + Sessions |
| **Unstructured** | 8001 | Python FastAPI | PDF extraction Stage 1 |
| **SmolDocling** | 8002 | Python + VLM | PDF extraction Stage 2 |
| **Ollama** | 11434 | Ollama | LLM locale + Embeddings |
| **n8n** | 5678 | n8n | Workflow automation |
| **Seq** | 8081 | Seq | Centralized logging |
| **Jaeger** | 16686 | Jaeger | Distributed tracing |
| **Prometheus** | 9090 | Prometheus | Metrics collection |
| **Grafana** | 3001 | Grafana | Monitoring dashboards |

## Flusso Dati Principali

### 1. Autenticazione (Dual Auth)
*(blocco di codice rimosso)*

### 2. Upload e Processing PDF
*(blocco di codice rimosso)*

### 3. RAG Query (Hybrid Search)
*(blocco di codice rimosso)*

## Connessioni Osservabilità

*(blocco di codice rimosso)*

## Stack Tecnologico Completo

### Backend
- **Runtime**: .NET 9 (ASP.NET Core)
- **Architecture**: DDD + CQRS (MediatR)
- **ORM**: Entity Framework Core 9
- **Database**: PostgreSQL 16
- **Cache**: Redis 7 + HybridCache
- **Vector DB**: Qdrant

### Frontend
- **Framework**: Next.js 16 (App Router)
- **UI Library**: React 19
- **Components**: Shadcn/UI (Radix + Tailwind CSS 4)
- **State**: React Context + Hooks
- **API Client**: Fetch with cookie auth

### AI/ML
- **LLM Providers**: OpenRouter (GPT-4, Claude), Ollama (Llama 3.3)
- **Embeddings**: Ollama (nomic-embed-text 384D)
- **PDF Processing**: Unstructured, SmolDocling VLM, Docnet
- **OCR**: Tesseract

### DevOps
- **Container**: Docker + Docker Compose
- **CI/CD**: GitHub Actions
- **Logging**: Serilog → Seq
- **Tracing**: OpenTelemetry → Jaeger
- **Metrics**: Prometheus + Grafana
- **Security**: CodeQL SAST, Dependabot

---

**Versione**: 1.0
**Data**: 2025-11-13
**Autore**: Claude Code Analysis


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/pdf-pipeline-detailed.md

# Pipeline PDF Processing - Diagrammi Dettagliati

## 3-Stage PDF Extraction Pipeline (Fallback Architecture)

*(blocco di codice rimosso)*

## Stage Decision Tree

*(blocco di codice rimosso)*

## Quality Scoring Formula

*(blocco di codice rimosso)*

## Text Processing Domain Service

*(blocco di codice rimosso)*

## OCR Decision Logic (Stage 3)

*(blocco di codice rimosso)*

## Concurrency Control (Docnet Semaphore)

*(blocco di codice rimosso)*

**Rationale**: Docnet.Core is NOT thread-safe. Semaphore limits to max 4 concurrent operations to prevent crashes.

## Pipeline Performance Metrics

*(blocco di codice rimosso)*

## Error Handling Strategy

*(blocco di codice rimosso)*

## Configuration Options

*(blocco di codice rimosso)*

---

**Versione**: 1.0
**Data**: 2025-11-13
**Pipeline**: 3-Stage PDF Extraction with Quality-Based Fallback


---



<div style="page-break-before: always;"></div>

## architecture/diagrams/rag-system-detailed.md

# Sistema RAG - Diagrammi Dettagliati

## Architettura RAG Completa (Hybrid Search)

*(blocco di codice rimosso)*

## RRF Fusion Algorithm (Reciprocal Rank Fusion)

*(blocco di codice rimosso)*

### RRF Formula Breakdown

*(blocco di codice rimosso)*

## Query Expansion Strategy (PERF-08)

*(blocco di codice rimosso)*

**Impact**: 15-25% recall improvement (PERF-08)

## LLM Provider Routing Strategy

*(blocco di codice rimosso)*

## Circuit Breaker State Machine

*(blocco di codice rimosso)*

## 5-Layer Validation Pipeline

*(blocco di codice rimosso)*

## Hallucination Detection - Multilingual Support

*(blocco di codice rimosso)*

## Cost Tracking Architecture

*(blocco di codice rimosso)*

## Quality Metrics Dashboard

*(blocco di codice rimosso)*

---

## Performance Targets

| Metrica | Target | Attuale | Status |
|---------|--------|---------|--------|
| **Retrieval Latency** | < 1s | ~800ms | ✓ |
| **Generation Latency** | < 3s | 2-5s | ⚠ |
| **Total E2E Latency** | < 5s | 3-6s | ✓ |
| **Accuracy** | > 95% | ~93% | ⚠ |
| **Hallucination Rate** | < 3% | ~2.5% | ✓ |
| **P@10** | > 0.8 | ~0.75 | ⚠ |
| **MRR** | > 0.7 | ~0.68 | ⚠ |
| **Cache Hit Rate** | > 40% | ~35% | ⚠ |
| **Cost per Query** | < $0.01 | $0.008 | ✓ |

## Configuration (Dynamic via SystemConfiguration)

*(blocco di codice rimosso)*

---

**Versione**: 1.0
**Data**: 2025-11-13
**Sistema**: RAG Hybrid Search with Multi-Provider LLM


---



<div style="page-break-before: always;"></div>

## architecture/overview/product-specification.md

# MeepleAI Product Specification

**AI board game assistant: RAG, multi-agent, living docs**

---

## Executive Summary

| Aspect | Detail |
|--------|--------|
| **Product** | MeepleAI - AI-Powered Board Game Assistant & Game Master |
| **Target** | Casual players (20-65, nerd demographic) + hardcore tier |
| **Business Model** | Freemium with premium features |
| **Primary Use** | Learn rules, arbitrate disputes, manage sessions |
| **Platform** | Web PWA (mobile-optimized, offline nice-to-have) |
| **MVP Timeline** | Q1-Q2 2025 |

---

## Site Map

*(blocco di codice rimosso)*

---

## User Flows

### Flow 1: New User Onboarding
*(blocco di codice rimosso)*

### Flow 2: Play Complete Game
*(blocco di codice rimosso)*

### Flow 3: Learning New Game
*(blocco di codice rimosso)*

### Flow 4: Upload Custom Game
*(blocco di codice rimosso)*

---

## Feature Matrix

### MVP (Q1-Q2 2025) ✅ Must Have

| Category | Features |
|----------|----------|
| **Authentication** | Email/password • OAuth (Google/Discord/GitHub) • 2FA/TOTP • Password reset |
| **Game Library** | Admin/editor uploads • User custom PDFs (private) • Search/filter • Favorites • 4-tab detail pages |
| **PDF Processing** | Viewer • Text extraction (Docling) • Table recognition • Embeddings • Vector search (Qdrant) |
| **Chat RAG** | Global chat • Game-specific • Citations (page numbers) • Cross-session memory • Export |
| **Agents** | Game Master (rule arbitration) • Basic move validation (RuleSpec v2) |
| **Game Sessions** | Create (setup modal) • Track current • History • Basic state management |
| **Settings** | Profile • Email/password/2FA • OAuth linking • Preferences (lang, theme, notifications) • Privacy • API keys • GDPR export |
| **Infrastructure** | ASP.NET Core API • Next.js PWA • PostgreSQL + Qdrant + Redis • Docker Compose • Monitoring |

### V2 (Q3 2025) 🔵 High Priority

| Category | Features |
|----------|----------|
| **BGG Integration** | Search catalog • Import metadata • Link rulebook downloads • Sync ratings |
| **AI Players** | Opponent for supported games • Difficulty levels • Playstyle config • Win rate tracking |
| **Visual Game State** | Board visualization (2D) • Piece placement UI • Move highlighting • Drag-and-drop |
| **Enhanced Chat** | Image attachment (board photos) • Diagram extraction • Multi-LLM consensus • Voice input |
| **House Rules** | Community rules (upvote/downvote) • Share with friends • Apply to sessions |
| **Workflow Agents** | n8n templates • Email reminders • Calendar • Discord/Slack notifications |
| **Italian Optimization** | Italian embeddings • Terminology glossary (500+ terms) • Italian catalog (50+ titles) |

### V3 (Q4 2025) 🟣 Nice to Have

| Category | Features |
|----------|----------|
| **Tournament** | Swiss/Round Robin • Brackets • Leaderboards • Automated pairings |
| **Social** | Friends system • Invites • Activity feed • Public profiles |
| **Multiplayer** | Online sessions • Real-time board sync (WebSockets) • Turn notifications • Spectator mode |
| **Computer Vision** | Board photo recognition • Piece detection • State sync • Move suggestions |
| **Mobile App** | React Native iOS/Android • Offline mode • Camera integration • Push notifications |
| **Gamification** | Achievements • Badges & titles • XP & leveling • Weekly challenges |

### Future (2026+) 🔮 Vision

| Category | Features |
|----------|----------|
| **Advanced AI** | Adaptive AI (learns playstyle) • Multi-agent coordination • Strategic coaching |
| **Content Creation** | AI-generated variants • Procedural scenarios • Custom rule generation |
| **Publisher Tools** | B2B dashboard • Analytics • Playtest coordination • Errata distribution |
| **Marketplace** | Premium AI models (subscription) • Expert AI players (DLC) • Custom agent marketplace |

---

## Database Schema (Key Entities)

### Games & Library

*(blocco di codice rimosso)*

### Sessions & Tracking

*(blocco di codice rimosso)*

### Chat & Agents

*(blocco di codice rimosso)*

### House Rules & Social

*(blocco di codice rimosso)*

**Indexes**: 15+ performance indexes (see full spec lines 1036-1055)

---

## Wireframes (Layout Templates)

### Standard Page Layout
*(blocco di codice rimosso)*

### Home Page Components
- Welcome banner (first visit, expandable "Come funziona")
- Activity stats: Games in library, Active sessions, Chats, Hours played
- Recent activity feed (time-sorted)
- Quick actions: Browse Games, New Chat, Start Game
- Continue active session (if any)

### Games Page (Ricerca Tab)
- Search bar (autocomplete)
- Left sidebar: Filters (source, players, duration, complexity, mechanics)
- Main: Sort dropdown + Game card grid (cover, title, stats, favorite toggle, add to library, user count)

### Chat Interface
- Left (25%): Chat list (new button, filter, history with game badges)
- Right (75%): Header (title, context, share, export) + Message thread + Input (multi-line, attach, context selector)
- Messages: User (right, blue), AI (left, gray) with citations, diagrams, follow-ups

### Agents Page
- Tabs: Game Masters (game selector) | AI Players (difficulty, playstyle) | Workflow (n8n templates)
- Active sessions section
- Recent history + agent performance stats (future)

### Settings Page
- Sidebar nav: Account, Preferences, Privacy, Advanced
- Account: Profile, email/password, 2FA, OAuth, danger zone
- Preferences: Language, theme, notifications, chat prefs
- Privacy: Visibility, activity sharing, data retention
- Advanced: API keys, developer mode, GDPR export, experimental features

---

## Data Model (Per User)

*(blocco di codice rimosso)*

*(blocco di codice rimosso)*

---

## User Flows Summary

| Flow | Key Steps | Outcome |
|------|-----------|---------|
| **Onboarding** | Visit → Register/OAuth → Welcome → Tutorial (5 steps) | First game in library |
| **Play Game** | Search → Game detail → Setup modal → Session screen (board + actions + chat) → End game → History | Completed session saved |
| **Learn Game** | Search → Info tab → Regolamento tab (PDF + AI guide) → Chat with questions → Practice game (tutorial mode) | Ready to play |
| **Upload Custom** | Add Game button → Upload PDF → Auto-detect metadata → Ownership confirm → Process → Success | Private game in library |
| **BGG Import** | Search (not found) → Search BGG → Import modal → Add to catalog → Upload rulebook prompt | Game in catalog (partial data) |

---

## Development Roadmap

### Sprint Plan (MVP)

| Sprint | Duration | Focus |
|--------|----------|-------|
| Sprint 1 | 2 weeks | Authentication & Settings (OAuth, 2FA, 4 settings tabs, profile) |
| Sprint 2 | 2 weeks | Game Library Foundation (entity CRUD, PDF upload/Docling, search/filter UI) |
| Sprint 3 | 2 weeks | Chat Enhancement (thread management, game context, citation display) |
| Sprint 4 | 3 weeks | Game Sessions MVP (creation, setup modal, state tracking, history) |
| Sprint 5 | 2 weeks | Agents Foundation (Game Master integration, agent selection UI, move validation if RuleSpec v2 ready) |

---

## Related Documentation

- **Architecture**: [01-architecture/README.md](../README.md)
- **Bounded Contexts**: [09-bounded-contexts/](../../09-bounded-contexts/)
- **Testing Strategy**: [05-testing/](../../05-testing/)
- **Deployment**: [04-deployment/](../../04-deployment/)

---

*Version: 1.0*
*Last Updated: 2025-12-13*
*Status: Draft for Review*


---



<div style="page-break-before: always;"></div>

## architecture/overview/system-architecture.md

# MeepleAI System Architecture

**Status**: Approved | **Version**: 1.0 | **Date**: 2025-01-15

---

## Overview

**Purpose**: AI board game rules assistant (Italian-first) with RAG for accurate answers from official rulebooks

### Core Requirements

**Functional**:
- Question answering (Italian primary, multilingual future)
- PDF upload & auto-indexing
- Citation tracking (page + snippet)
- Explicit uncertainty handling (confidence scoring)

**Non-Functional**:
- **Accuracy**: >95% (golden dataset: 1000 Q&A)
- **Hallucination**: <3% (zero critical fabrications)
- **Latency**: P95 <3s (complex queries)
- **Uptime**: >99.5% SLA (production)
- **Scale**: 10K MAU (Phase 4)

### Key Constraints

1. **One Mistake Ruins Session**: Accuracy >> typical AI apps
2. **Cost Control**: LLM costs scale (mitigation: semantic cache, smart routing)
3. **Open-Source Core**: RAG pipeline Apache 2.0, proprietary = licensed content + premium
4. **Italian-First**: Multilingual architecture, Italian-optimized from day 1

---

## Architecture Principles

### 1. Quality Over Speed
- Correctness > Latency (users prefer 3s accurate vs 1s wrong)
- Multi-model validation (GPT-4 + Claude) adds ~500ms but prevents hallucinations
- Confidence threshold ≥0.70 or explicit uncertainty

### 2. Defense-in-Depth (5 Layers)
1. **Confidence Threshold**: Reject if <0.70
2. **Multi-Model Consensus**: GPT-4 + Claude agree (cosine similarity ≥0.90)
3. **Citation Verification**: Page exists + snippet matches chunk
4. **Forbidden Keywords**: 500+ blocklist from known hallucinations
5. **User Feedback**: Negative reports → admin review → blocklist update

### 3. Progressive Enhancement

| Phase | Features |
|-------|----------|
| **Phase 1 (MVP)** | Single LLM (GPT-4), basic confidence, manual annotation |
| **Phase 2 (Prod)** | Dual LLM (GPT-4 + Claude), semantic cache, circuit breakers |
| **Phase 3 (Advanced)** | Hybrid search (vector + keyword RRF), fine-tuned embeddings, A/B testing |
| **Phase 4 (Platform)** | Multi-language, API ecosystem, community contributions |

### 4. Open Core, Proprietary Premium

**Open-Source** (Apache 2.0):
- PDF pipeline (Unstructured → SmolDocling → Docnet)
- Embedding generation (multilingual-e5-large)
- Vector search (Weaviate)
- Multi-model validation framework
- Testing & evaluation tools

**Proprietary**:
- Licensed rulebook content (encrypted)
- Fine-tuned Italian embeddings
- Premium API SLA
- White-label B2B
- Advanced analytics

### 5. Fail-Safe Gracefully

**Failure Modes**:
- **LLM API Down**: Circuit breaker → fallback cache (sim ≥0.90) → different provider → "Servizio non disponibile"
- **Vector DB Down**: PostgreSQL FTS fallback → "Modalità ridotta (accuracy inferiore)"
- **Low Confidence (<0.70)**: "Non ho info sufficienti" + suggest rulebook page X

---

## High-Level Architecture

*(blocco di codice rimosso)*

### Layers

*(blocco di codice rimosso)*

---

## Component Architecture

### 1. Question Answering Pipeline

*(blocco di codice rimosso)*

**Retrieval** (Hybrid Search in Phase 3+):
*(blocco di codice rimosso)*

**Generation** (GPT-4 Turbo):
*(blocco di codice rimosso)*

**Validation** (Multi-Model):
*(blocco di codice rimosso)*

---

### 2. Indexing Pipeline

*(blocco di codice rimosso)*

---

## Technology Stack

### Phase 1 (MVP)

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Backend** | ASP.NET Core 9 | Existing, DDD, 90%+ tests |
| **Frontend** | Next.js 16 + React 19 | Latest stable, SSR/SSG |
| **PDF** | Unstructured (primary) | Apache 2.0, RAG-optimized, 1.3s avg |
| | SmolDocling (fallback) | Vision-language, GPU |
| | Docnet.Core (final) | Existing, proven |
| **Embeddings** | OpenRouter API | text-embedding-3-large via unified API |
| | Ollama nomic-embed-text | Free self-hosted (768 dims) |
| **Vector DB** | Qdrant | Already deployed, hybrid v1.7+ |
| **LLM Primary** | OpenRouter: gpt-4-turbo | Unified API, auto-fallback |
| **LLM Validation** | OpenRouter: claude-3.5-sonnet | Multi-model consensus |
| **LLM Fallback** | Ollama: mistral:7b + llama3.1:8b | Free, 75-80% accuracy trade-off |
| **Cache** | Redis HybridCache | L1 memory + L2 Redis + semantic layer |
| **Database** | PostgreSQL 16 | ACID, EF Core 9, FTS (Phase 3) |

### Phase 2+ Enhancements

- Redis Cluster (3 nodes, HA)
- Qdrant optimization (hybrid search, Italian collections)
- PostgreSQL replication
- Kubernetes (AWS EKS or DigitalOcean)
- Terraform IaC
- Prometheus + Grafana + PagerDuty
- OpenTelemetry → Jaeger
- Cloudflare CDN

---

## Data Flow

### Question Answering (Happy Path)

*(blocco di codice rimosso)*

### Indexing Flow

*(blocco di codice rimosso)*

---

## Security

### Authentication

- **Phase 1**: Session cookies (httpOnly, secure, SameSite=strict)
- **Phase 2+**: JWT + refresh tokens (OAuth 2.0)
- **Phase 4**: OAuth providers (Google, Discord)

### Authorization Roles

| Role | Query Limit | Features |
|------|-------------|----------|
| **Anonymous** | None | Signup required |
| **Free** | 10/day | Basic Q&A |
| **Premium** | Unlimited | Mobile app |
| **Admin** | Unlimited | Full system |
| **Publisher** | Unlimited | B2B analytics |

### Data Security

**At Rest**:
- PostgreSQL: Encryption (AWS RDS, Azure SQL TDE)
- Qdrant: Disk encryption (K8s PV)
- Licensed Rulebooks: AES-256 (key per publisher, AWS Secrets Manager)

**In Transit**: TLS 1.3 (HTTPS enforced) | mTLS (service mesh, Phase 2+)

**Secrets**: `.env` (Phase 1) → AWS Secrets Manager/Vault (Phase 2+)

---

## Deployment

### Phase 1 (MVP) - Docker Compose

*(blocco di codice rimosso)*

### Phase 2+ (Production) - Kubernetes

*(blocco di codice rimosso)*

**Infrastructure**: AWS EKS | RDS PostgreSQL (Multi-AZ) | ElastiCache Redis (cluster) | Qdrant StatefulSet (3 replicas) | NGINX Ingress + cert-manager (Let's Encrypt)

---

## Scalability

### Performance Targets

| Phase | MAU | Users | RPS | P95 Latency | Uptime |
|-------|-----|-------|-----|-------------|--------|
| **1 (MVP)** | 100 | 10 | 5 | <5s | 99% |
| **2 (Prod)** | 1K | 50 | 20 | <3s | 99.5% |
| **3 (Growth)** | 5K | 200 | 100 | <3s | 99.5% |
| **4 (Scale)** | 10K | 500 | 200 | <3s | 99.9% |

### Scaling Strategy

**Vertical** (Phase 2):
- API: 2 vCPU, 4GB → 4 vCPU, 8GB (~50 → 100 RPS)
- PostgreSQL: db.t3.medium → db.t3.large + read replicas
- Qdrant: 4GB → 16GB (1M+ vectors)

**Horizontal** (Phase 3-4):
- API: Stateless, K8s autoscale (3-10 pods, CPU-based)
- Redis: Single → Cluster (3 master + 3 replica)
- PostgreSQL: PgBouncer (100 conn), partitioning by `game_id` (1000+ games)

---

## Monitoring

### Metrics (Prometheus)

*(blocco di codice rimosso)*

### Alerts (Critical)

*(blocco di codice rimosso)*

### Dashboards (Grafana)

1. **Q&A Overview**: Requests/sec, P95 latency, confidence distribution, failures by layer
2. **System Health**: CPU/Memory/Disk, uptime, DB connections, cache hit rate
3. **LLM Usage**: Calls by provider, token usage, cost, latency, circuit breaker state
4. **Business**: DAU/MAU, premium conversion, top games, retention, revenue

---

## ADRs (Architecture Decision Records)

| ADR | Title | Date |
|-----|-------|------|
| [001](./adr-001-hybrid-rag-architecture.md) | Hybrid RAG with Multi-Model Validation | 2025-01-15 |
| [002](./adr-002-multilingual-embedding-strategy.md) | Multilingual Embedding (Italian-First) | 2025-01-15 |
| [003](./adr-003-pdf-processing-pipeline.md) | PDF Pipeline (Vision-Language Models) | 2025-01-15 |
| [004](./adr-004-vector-db-selection.md) | Vector DB Selection (Qdrant vs Pinecone) | 2025-01-15 |
| [005](./adr-005-llm-strategy.md) | LLM Strategy (GPT-4 + Claude Validation) | 2025-01-15 |
| [006](./adr-006-caching-strategy.md) | Semantic Caching (Redis FAISS-based) | 2025-01-15 |

---

**Document**: v1.0 | 2025-12-13 | Quarterly Review: 2025-04-15 | **APPROVED** for Phase 1


---



<div style="page-break-before: always;"></div>

## architecture/s3-complete-guide.md

# S3 Object Storage - Complete Guide

**Implementazione completa**: S3-compatible storage per PDF uploads, backups, document processing

---

## Quick Start (5 minuti)

### Cloudflare R2 (Production)

*(blocco di codice rimosso)*

**Full guide**: `docs/04-infrastructure/s3-quickstart.md`

---

## Architecture

### Service Hierarchy

*(blocco di codice rimosso)*

### Factory Pattern

*(blocco di codice rimosso)*

### Handlers Using Storage (7)

1. `UploadPdfCommandHandler` - PDF uploads
2. `UploadPrivatePdfCommandHandler` - Private PDFs
3. `CompleteChunkedUploadCommandHandler` - Chunked uploads
4. `DeletePdfCommandHandler` - PDF deletion
5. `DownloadPdfQueryHandler` - PDF downloads
6. `ExtractPdfTextCommandHandler` - Text extraction
7. `UploadGameImageCommandHandler` - Game images

**Zero code changes needed** - all use `IBlobStorageService` interface.

---

## Configuration

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `STORAGE_PROVIDER` | No | `local` | Storage backend: `local` or `s3` |
| `S3_ENDPOINT` | Yes* | - | S3 endpoint URL (provider-specific) |
| `S3_ACCESS_KEY` | Yes* | - | S3 access key ID |
| `S3_SECRET_KEY` | Yes* | - | S3 secret access key |
| `S3_BUCKET_NAME` | Yes* | - | S3 bucket name |
| `S3_REGION` | No | `auto` | AWS region or "auto" for R2 |
| `S3_PRESIGNED_URL_EXPIRY` | No | `3600` | Pre-signed URL lifetime (seconds) |
| `S3_FORCE_PATH_STYLE` | No | `false` | Use path-style URLs (MinIO needs `true`) |
| `S3_DISABLE_ENCRYPTION` | No | `false` | Disable server-side encryption |

*Required only when `STORAGE_PROVIDER=s3`

### Provider Examples

**Cloudflare R2** (Recommended):
*(blocco di codice rimosso)*

**AWS S3**:
*(blocco di codice rimosso)*

**MinIO (Local)**:
*(blocco di codice rimosso)*

**Backblaze B2**:
*(blocco di codice rimosso)*

**DigitalOcean Spaces**:
*(blocco di codice rimosso)*

---

## Local Testing con MinIO

### Setup (2 minuti)

*(blocco di codice rimosso)*

### Verify MinIO

*(blocco di codice rimosso)*

---

## Features

### 1. Multi-Provider Support

**Supportati**:
- ✅ Cloudflare R2 (zero egress fees, EU jurisdiction)
- ✅ AWS S3 (standard S3, massima compatibilità)
- ✅ Backblaze B2 (low cost, $0.005/GB)
- ✅ MinIO (self-hosted, testing locale)
- ✅ DigitalOcean Spaces (semplice setup)

**Configurazione**: Cambia solo `S3_ENDPOINT` e `S3_REGION`

### 2. Security

**Path Traversal Protection**:
*(blocco di codice rimosso)*

**Server-Side Encryption** (AES256):
*(blocco di codice rimosso)*

**Stream Disposal**:
*(blocco di codice rimosso)*

### 3. Pre-Signed URLs

*(blocco di codice rimosso)*

**Use cases**:
- Temporary download links (expire dopo X secondi)
- Direct browser downloads (bypass API)
- CDN integration (Cloudflare)

### 4. Health Monitoring

**Endpoint**: `GET /health`

*(blocco di codice rimosso)*

**States**:
- `Healthy`: S3 accessibile
- `Unhealthy`: Credentials invalide, bucket non esiste
- `Degraded`: Timeout connectivity

---

## Migration

### Script Usage

*(blocco di codice rimosso)*

### Manual Migration (AWS CLI)

*(blocco di codice rimosso)*

---

## Testing

### Unit Tests (15 tests)

*(blocco di codice rimosso)*

### Integration Testing

**Con MinIO locale**:
*(blocco di codice rimosso)*

---

## Monitoring

### Health Check Monitoring

*(blocco di codice rimosso)*

### Application Logs

*(blocco di codice rimosso)*

### Bucket Metrics

**Cloudflare R2**:
- Dashboard → R2 → `meepleai-uploads` → Metrics
- Storage GB, Requests (Class A/B), Egress (gratis)

**AWS CLI**:
*(blocco di codice rimosso)*

---

## Troubleshooting

### 🔴 Health Check Fails

**Error**: `s3storage: Unhealthy - S3 configuration incomplete`

*(blocco di codice rimosso)*

**Error**: `s3storage: Unhealthy - S3 bucket does not exist`

*(blocco di codice rimosso)*

**Error**: `s3storage: Unhealthy - S3 access denied`

*(blocco di codice rimosso)*

### 🔴 Upload Fails

**Symptom**: PDF upload returns error

*(blocco di codice rimosso)*

**Symptom**: Upload succeeds but file not in S3

*(blocco di codice rimosso)*

### 🔴 MinIO Connection Issues

*(blocco di codice rimosso)*

---

## Cost Comparison

| Provider | Storage (20GB) | Egress (100GB/mo) | Total/mo |
|----------|----------------|-------------------|----------|
| **Cloudflare R2** | $0.30 | **$0.00** ✅ | **$0.30** |
| AWS S3 | $0.46 | $9.00 | $9.46 |
| Backblaze B2 | $0.10 | $1.00 | $1.10 |
| MinIO (self-hosted) | Server costs | $0.00 | Variable |

**Raccomandazione**: Cloudflare R2 (zero egress fees = 95% savings)

---

## Migration

### From Local to S3

*(blocco di codice rimosso)*

### From S3 to Local

*(blocco di codice rimosso)*

---

## Security Best Practices

### 1. Credentials Rotation (90 giorni)

*(blocco di codice rimosso)*

### 2. Bucket Encryption

**Cloudflare R2**: Automatic (always on)

**AWS S3**:
*(blocco di codice rimosso)*

### 3. Access Policies

**Cloudflare R2**: Token permissions (Object Read & Write only)

**AWS S3**: IAM policy
*(blocco di codice rimosso)*

---

## Performance

### Metrics

**Operation latencies** (Cloudflare R2, EU):
- Upload (1MB): ~200-500ms
- Download (1MB): ~150-300ms
- Delete: ~100-200ms
- Health check: ~50-100ms

**Optimization**:
- Pre-signed URLs: Direct download (bypass API)
- CDN integration: Cloudflare automatic caching
- Connection pooling: Singleton IAmazonS3 client

### Monitoring Queries

*(blocco di codice rimosso)*

---

## Backup & Disaster Recovery

### Automated Backups (n8n)

**Workflow** (to implement in n8n):
1. Schedule: Daily 2 AM
2. Export PostgreSQL dump
3. Upload to `S3_BACKUP_BUCKET_NAME`
4. Retention: Delete backups >30 days

**Configuration** (`storage.secret`):
*(blocco di codice rimosso)*

### Manual Backup

*(blocco di codice rimosso)*

### Restore

*(blocco di codice rimosso)*

---

## API Reference

### IBlobStorageService Interface

*(blocco di codice rimosso)*

### S3BlobStorageService Additional Methods

*(blocco di codice rimosso)*

---

## Provider Comparison

| Feature | R2 | AWS S3 | B2 | MinIO |
|---------|----|----|----|----|
| **Egress fees** | ✅ $0 | ❌ $0.09/GB | ⚠️ $0.01/GB | ✅ $0 |
| **Storage cost** | $0.015/GB | $0.023/GB | $0.005/GB | Self-hosted |
| **EU jurisdiction** | ✅ Yes | ⚠️ Depends | ⚠️ US | ✅ Self-hosted |
| **CDN integration** | ✅ Cloudflare | ⚠️ CloudFront | ❌ No | ❌ No |
| **Setup complexity** | ⭐⭐ Easy | ⭐⭐⭐ Medium | ⭐⭐ Easy | ⭐⭐⭐⭐ Complex |
| **API compatibility** | ✅ Full | ✅ Native | ✅ Full | ✅ Full |
| **Free tier** | ❌ No | ⚠️ 5GB | ✅ 10GB | N/A |

**Best for**:
- **Production**: Cloudflare R2 (zero egress, EU, CDN)
- **Budget**: Backblaze B2 (lowest storage cost)
- **Testing**: MinIO (local, no cloud account)
- **AWS ecosystem**: AWS S3 (native integration)

---

## References

- **Quick Start**: `docs/04-infrastructure/s3-quickstart.md`
- **Operations Runbook**: `docs/04-infrastructure/s3-storage-operations-runbook.md`
- **Migration Script**: `tools/migrate-local-to-s3.ps1`
- **Developer Guide**: `CLAUDE.md` (section "S3 Storage Configuration")
- **Issue**: #2703
- **PR**: #3683

**Implementation**:
- `Api/Services/Pdf/S3BlobStorageService.cs` (332 lines)
- `Api/Services/Pdf/BlobStorageServiceFactory.cs` (79 lines)
- `Api/Services/Pdf/S3StorageOptions.cs` (48 lines)
- `Api/Infrastructure/Health/Checks/S3StorageHealthCheck.cs` (70 lines)

---

**Last Updated**: 2026-02-05
**Version**: 1.0 (Initial implementation)


---



<div style="page-break-before: always;"></div>

## architecture/s3-quickstart.md

# S3 Storage Quick Start Guide

## Setup Cloudflare R2 (5 minuti)

### Step 1: Crea Account Cloudflare R2

1. **Registrati**: https://dash.cloudflare.com/sign-up
2. **Vai a R2**: Dashboard → R2 Object Storage
3. **Acquista piano**: $0.015/GB/mese (~$0.30/20GB)

### Step 2: Crea Bucket

1. **Create bucket** → Nome: `meepleai-uploads`
2. **Location**: EU (GDPR compliance)
3. **Settings**: Default (encryption automatica)

### Step 3: Genera API Token

1. **Manage R2 API Tokens** → Create API Token
2. **Permissions**: Object Read & Write
3. **Copia**: Access Key ID + Secret Access Key

### Step 4: Configura MeepleAI

*(blocco di codice rimosso)*

**Contenuto `storage.secret`**:
*(blocco di codice rimosso)*

### Step 5: Riavvia API

*(blocco di codice rimosso)*

---

## Setup MinIO (Testing Locale - Opzionale)

### Step 1: Avvia MinIO Container

*(blocco di codice rimosso)*

### Step 2: Crea Bucket

1. **Apri console**: http://localhost:9001
2. **Login**: minioadmin / minioadmin
3. **Create Bucket**: `meepleai-uploads`

### Step 3: Configura storage.secret

*(blocco di codice rimosso)*

---

## Verifica Funzionamento

### Test 1: Health Check (30 secondi)

*(blocco di codice rimosso)*

### Test 2: Upload PDF (1 minuto)

*(blocco di codice rimosso)*

### Test 3: AWS CLI (Opzionale)

*(blocco di codice rimosso)*

### Test 4: Automated Tests

*(blocco di codice rimosso)*

---

## Troubleshooting Rapido

| Problema | Causa | Soluzione |
|----------|-------|-----------|
| **Health check Unhealthy** | Credentials invalide | Verifica S3_ACCESS_KEY, S3_SECRET_KEY in storage.secret |
| **Bucket not found** | Bucket non creato | Crea bucket in R2 dashboard / MinIO console |
| **Upload fallisce** | Endpoint sbagliato | Verifica S3_ENDPOINT (format: https://{account}.r2.cloudflarestorage.com) |
| **403 Forbidden** | Token permissions insufficienti | R2 token deve avere "Object Read & Write" |
| **MinIO connection refused** | Container non avviato | `docker ps` → verifica minio running |
| **Path style error** | MinIO senza force-path | Aggiungi S3_FORCE_PATH_STYLE=true |

---

## Migrazione Dati Esistenti

*(blocco di codice rimosso)*

---

## Switching Provider

### Da Local → S3

*(blocco di codice rimosso)*

### Da S3 → Local

*(blocco di codice rimosso)*

---

## Quick Reference

*(blocco di codice rimosso)*

**Docs completi**: `docs/04-infrastructure/s3-storage-operations-runbook.md`


---



<div style="page-break-before: always;"></div>

## architecture/s3-storage-operations-runbook.md

# S3 Storage Operations Runbook

**Quick Reference**: Common operations for managing S3-compatible object storage in MeepleAI

## Prerequisites

- AWS CLI installed: `aws --version`
- S3 credentials configured in `infra/secrets/storage.secret`
- Environment variable `STORAGE_PROVIDER=s3` set

## Configuration

### Cloudflare R2 (Recommended)

*(blocco di codice rimosso)*

### AWS S3

*(blocco di codice rimosso)*

### MinIO (Self-Hosted)

*(blocco di codice rimosso)*

## Common Operations

### 1. Verify S3 Connectivity

*(blocco di codice rimosso)*

### 2. List Files in Bucket

*(blocco di codice rimosso)*

### 3. Migrate Existing Files

*(blocco di codice rimosso)*

### 4. Download File from S3

*(blocco di codice rimosso)*

### 5. Delete Files

*(blocco di codice rimosso)*

### 6. Bucket Operations

*(blocco di codice rimosso)*

## Troubleshooting

### Health Check Failures

**Symptom**: `/health` endpoint shows `s3storage: Unhealthy`

**Possible Causes**:

1. **Configuration missing**:
   *(blocco di codice rimosso)*

2. **Invalid credentials**:
   *(blocco di codice rimosso)*

3. **Bucket doesn't exist**:
   *(blocco di codice rimosso)*

4. **Network connectivity**:
   *(blocco di codice rimosso)*

### Upload Failures

**Symptom**: PDF uploads fail with `BlobStorageResult.Success = false`

**Debugging**:

1. **Check application logs**:
   *(blocco di codice rimosso)*

2. **Test with AWS CLI**:
   *(blocco di codice rimosso)*

3. **Verify bucket policy** (AWS S3 only):
   *(blocco di codice rimosso)*

### Migration Issues

**Symptom**: Migration script fails or shows errors

**Solutions**:

1. **AWS CLI not found**:
   *(blocco di codice rimosso)*

2. **Permission denied**:
   *(blocco di codice rimosso)*

3. **Partial migration** (some files failed):
   *(blocco di codice rimosso)*

## Monitoring

### Storage Metrics

*(blocco di codice rimosso)*

### Cost Monitoring (Cloudflare R2)

*(blocco di codice rimosso)*

### Performance Metrics

Application logs track S3 operation latency:

*(blocco di codice rimosso)*

## Backup & Restore

### Backup S3 Bucket

*(blocco di codice rimosso)*

### Restore from Backup

*(blocco di codice rimosso)*

## Security Best Practices

1. **Credentials Rotation** (every 90 days):
   *(blocco di codice rimosso)*

2. **Bucket Encryption** (AWS S3):
   *(blocco di codice rimosso)*

3. **Access Logging** (AWS S3):
   *(blocco di codice rimosso)*

## Provider-Specific Notes

### Cloudflare R2

- **Zero egress fees**: No charges for downloads
- **EU jurisdiction**: GDPR-compliant storage
- **Custom domains**: Configure via R2 Dashboard -> Public Buckets
- **CDN integration**: Automatic Cloudflare CDN caching

### AWS S3

- **Egress fees**: $0.09/GB after 100GB free tier
- **Glacier storage**: Use lifecycle policies for archival
- **CloudFront**: Configure CDN for better performance

### MinIO

- **Self-hosted**: Full control over infrastructure
- **S3-compatible**: Drop-in replacement for AWS S3
- **High performance**: Optimized for throughput
- **Path-style URLs**: Set `S3_FORCE_PATH_STYLE=true`

### Backblaze B2

- **Low cost**: $0.005/GB storage, $0.01/GB egress
- **S3-compatible API**: Use S3 endpoint URLs
- **Free tier**: 10GB storage + 1GB egress daily

## References

- Cloudflare R2: https://developers.cloudflare.com/r2/
- AWS S3: https://docs.aws.amazon.com/s3/
- MinIO: https://min.io/docs/
- Backblaze B2: https://www.backblaze.com/b2/docs/
- AWS CLI: https://docs.aws.amazon.com/cli/

---

**Last Updated**: 2026-02-05
**Related**: Issue #2703, `storage.secret.example`, `migrate-local-to-s3.ps1`


---



<div style="page-break-before: always;"></div>

## architecture/s3-storage-options.md

# S3-Compatible Storage Options for MeepleAI

> **Last Updated**: 2026-01-20
> **Status**: Research Complete
> **Decision Required**: Choose between cloud-hosted or self-hosted solution

## Executive Summary

MeepleAI requires S3-compatible object storage for:
- **PDF uploads**: User-uploaded board game rulebooks (primary use case)
- **Database backups**: Automated PostgreSQL backups via n8n
- **Document processing**: Temporary storage during extraction pipeline

**Recommendation**: **Cloudflare R2** for cloud-hosted or **Garage** for self-hosted.

---

## Use Case Analysis for MeepleAI

### Estimated Storage Requirements

| Category | Estimate | Notes |
|----------|----------|-------|
| PDF uploads | 50-200 GB/year | ~500 rulebooks × 100-400 KB avg |
| Database backups | 10-50 GB | Daily backups, 30-day retention |
| Temp processing | 5-10 GB | Ephemeral, cleared after processing |
| **Total Year 1** | **~100 GB** | Conservative estimate |
| **Total Year 3** | **~500 GB** | With growth |

### Access Patterns

- **Writes**: Moderate (PDF uploads, daily backups)
- **Reads**: High (serving PDFs to users, RAG pipeline)
- **Egress**: Medium-High (PDF downloads, API responses)

---

## Cloud-Hosted Options

### 1. Cloudflare R2 (Recommended for Cloud)

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.015/GB-month storage |
| **Egress** | **FREE** (zero egress fees) |
| **Free Tier** | 10 GB storage + 1M ops/month |
| **Operations** | Class A: $4.50/M, Class B: $0.36/M |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $1.50/mo | $7.50/mo |
| Operations (est. 1M/mo) | ~$1/mo | ~$2/mo |
| Egress | $0 | $0 |
| **Monthly Total** | **~$2.50** | **~$9.50** |
| **Annual Total** | **~$30** | **~$114** |

**Pros:**
- Zero egress fees (critical for serving PDFs)
- S3-compatible API
- Global CDN integration
- Free migration tool (Super Slurper)
- Workers integration for edge computing

**Cons:**
- Vendor lock-in to Cloudflare ecosystem
- Infrequent Access tier has retrieval fees
- Smaller ecosystem than AWS

**Configuration:**
*(blocco di codice rimosso)*

---

### 2. Backblaze B2

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.006/GB-month ($6/TB) |
| **Egress** | Free up to 3x storage, then $0.01/GB |
| **CDN Partners** | FREE egress via Cloudflare, Fastly, bunny.net |
| **Free Tier** | 10 GB storage |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $0.60/mo | $3/mo |
| Egress (via Cloudflare) | $0 | $0 |
| **Monthly Total** | **~$0.60** | **~$3** |
| **Annual Total** | **~$7** | **~$36** |

**Pros:**
- Cheapest storage pricing
- Free egress with CDN partners (Cloudflare, Fastly)
- 3x free egress even without CDN
- Strong compliance features

**Cons:**
- Need to set up CDN for free egress
- Slightly more complex architecture
- B2 Overdrive (high perf) costs more

**Configuration:**
*(blocco di codice rimosso)*

---

### 3. DigitalOcean Spaces

| Aspect | Details |
|--------|---------|
| **Pricing** | $5/mo for 250GB + 1TB transfer |
| **Overage** | $0.02/GB storage, $0.01/GB transfer |
| **CDN** | Built-in, included |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Base plan | $5/mo | $5/mo |
| Overage storage | $0 | ~$5/mo |
| **Monthly Total** | **$5** | **~$10** |
| **Annual Total** | **$60** | **~$120** |

**Pros:**
- Simple flat pricing
- Built-in CDN
- Good DigitalOcean ecosystem integration
- Easy setup

**Cons:**
- More expensive than R2/B2 for low storage
- Transfer fees after 1TB
- Less S3-compatible than others

---

### 4. AWS S3

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.023/GB-month (Standard) |
| **Egress** | $0.09/GB (first 10TB) |
| **Free Tier** | 5GB for 12 months |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $2.30/mo | $11.50/mo |
| Egress (50GB/mo) | $4.50/mo | $9/mo |
| **Monthly Total** | **~$7** | **~$20** |
| **Annual Total** | **~$84** | **~$240** |

**Pros:**
- Industry standard
- Most complete feature set
- Best tooling and documentation
- Enterprise-grade reliability

**Cons:**
- Expensive egress fees
- Complex pricing model
- Overkill for small projects

---

## Self-Hosted Options

### 1. Garage (Recommended for Self-Hosted)

| Aspect | Details |
|--------|---------|
| **License** | AGPLv3 |
| **Language** | Rust |
| **Min Resources** | 256MB RAM, low CPU |
| **Best For** | Geo-distributed, edge deployments |

**Cost Estimate (VPS):**

| Setup | Monthly Cost |
|-------|--------------|
| Single node (Hetzner CX22) | ~€4/mo |
| 3-node cluster (resilient) | ~€12/mo |
| Storage (100GB SSD) | Included |

**Pros:**
- Lightweight, single binary
- Geo-distribution built-in
- No central coordinator (gossip protocol)
- Active development with EU funding
- Perfect for small-scale self-hosting

**Cons:**
- Limited S3 API coverage
- Lower throughput than MinIO (~5Gbps vs 20Gbps)
- Smaller community
- CLI-focused operations

**Docker Compose:**
*(blocco di codice rimosso)*

**garage.toml:**
*(blocco di codice rimosso)*

---

### 2. MinIO (Maintenance Mode Warning)

| Aspect | Details |
|--------|---------|
| **License** | AGPLv3 |
| **Status** | ⚠️ Community edition in maintenance mode |
| **Min Resources** | 2GB RAM, 2 CPU |

**Important Update (Dec 2025):**
> MinIO community edition is now source-only distribution. No pre-compiled binaries. Must build from source with Go 1.24+.

**Pros:**
- Highest performance (~20-25 Gbps)
- Most complete S3 API
- Large community and documentation
- Kubernetes-native

**Cons:**
- ⚠️ Community edition entering maintenance
- Higher resource requirements
- Enterprise features require license
- Uncertain future for open-source version

**Not Recommended** due to maintenance mode status. Consider Garage or RustFS instead.

---

### 3. SeaweedFS

| Aspect | Details |
|--------|---------|
| **License** | Apache 2.0 |
| **Language** | Go |
| **Min Resources** | 512MB RAM |

**Pros:**
- Apache 2.0 license (more permissive)
- Good for large files
- Tiered storage support
- Active development

**Cons:**
- More complex architecture (master + volume servers)
- S3 gateway is secondary feature
- Less documentation

---

## Comparison Matrix

| Feature | Cloudflare R2 | Backblaze B2 | DigitalOcean | AWS S3 | Garage | MinIO |
|---------|---------------|--------------|--------------|--------|--------|-------|
| **Storage/GB** | $0.015 | $0.006 | $0.02 | $0.023 | VPS cost | VPS cost |
| **Egress** | FREE | Free (CDN) | $0.01 | $0.09 | FREE | FREE |
| **S3 Compat** | High | High | Medium | Native | Medium | High |
| **Setup** | Easy | Medium | Easy | Medium | Medium | Medium |
| **Free Tier** | 10GB | 10GB | N/A | 5GB/12mo | N/A | N/A |
| **Best For** | High egress | Budget | DO users | Enterprise | Self-host | Perf |

---

## Recommendations

### For MeepleAI Production (Cloud)

**Primary: Cloudflare R2**
- Zero egress fees critical for PDF serving
- Low cost at our scale (~$30/year)
- S3-compatible API works with existing code
- Easy CDN integration

### For MeepleAI Self-Hosted

**Primary: Garage**
- Lightweight, runs on minimal VPS
- Good enough S3 compatibility
- Active development with funding
- Geo-distribution if needed later

### Migration Path

1. **Start**: Cloudflare R2 (easiest, cheapest for our scale)
2. **Scale**: Backblaze B2 + Cloudflare CDN (if storage grows significantly)
3. **Self-host**: Garage on dedicated VPS (if cost optimization needed)

---

## Implementation Checklist

### For Cloudflare R2

- [ ] Create Cloudflare account
- [ ] Enable R2 in dashboard
- [ ] Create buckets: `meepleai-uploads`, `meepleai-backups`
- [ ] Generate API tokens with appropriate permissions
- [ ] Update `infra/secrets/storage.secret`
- [ ] Configure n8n backup workflow
- [ ] Test integration with API service

### For Garage (Self-Hosted)

- [ ] Provision VPS (Hetzner CX22 or similar)
- [ ] Deploy Garage via Docker Compose
- [ ] Initialize cluster and create buckets
- [ ] Generate access keys
- [ ] Update `infra/secrets/storage.secret`
- [ ] Configure backup workflow
- [ ] Set up monitoring

---

## Current Codebase Implementation

### Storage Service Architecture

MeepleAI currently uses **local file storage** via `IBlobStorageService`. The service is designed with a clean abstraction that makes S3 migration straightforward.

**Key Files:**
- `Api/Services/Pdf/IBlobStorageService.cs` - Storage interface
- `Api/Services/Pdf/BlobStorageService.cs` - Local filesystem implementation

### Interface Contract

*(blocco di codice rimosso)*

### Storage Lifecycle

*(blocco di codice rimosso)*

### Usage in Handlers

| Handler | Operations | Notes |
|---------|-----------|-------|
| `UploadPdfCommandHandler` | `StoreAsync()` | Primary upload path |
| `DeletePdfCommandHandler` | `DeleteAsync()` | Cleanup on delete |
| `DownloadPdfQueryHandler` | `RetrieveAsync()` | Serve to users |
| `CompleteChunkedUploadCommandHandler` | `StoreAsync()` | Large file support |

### Migration Path to S3

**Option A: Drop-in S3 Implementation** (Recommended)

Create `S3BlobStorageService` implementing `IBlobStorageService`:

*(blocco di codice rimosso)*

**Option B: Hybrid Approach** (Gradual Migration)

1. Keep local storage for processing
2. Sync to S3 after processing completes
3. Serve from S3 via CDN for downloads

### Configuration Pattern

**Current (Local):**
*(blocco di codice rimosso)*

**Future (S3):**
*(blocco di codice rimosso)*

### DI Registration

*(blocco di codice rimosso)*

### Backup Integration

The n8n backup workflow (`infra/n8n/templates/backup-automation.json`) is designed to:
1. Dump PostgreSQL database
2. Upload to S3 bucket
3. Clean up old backups based on retention policy

**Required Secrets:**
*(blocco di codice rimosso)*

### Security Considerations

Current implementation includes:
- **Path Traversal Protection**: `PathSecurity.ValidateIdentifier()` and `ValidatePathIsInDirectory()`
- **Filename Sanitization**: `SanitizeFileName()` removes dangerous characters
- **Access Control**: Files organized by `gameId` for tenant isolation

S3 implementation should add:
- Pre-signed URLs for secure downloads
- Bucket policies restricting access
- Server-side encryption (SSE-S3 or SSE-KMS)
- CORS configuration for direct browser uploads

---

## References

### Cloud Providers
- [Cloudflare R2 Pricing](https://developers.cloudflare.com/r2/pricing/)
- [Cloudflare R2 vs AWS S3](https://www.cloudflare.com/pg-cloudflare-r2-vs-aws-s3/)
- [Backblaze B2 Pricing](https://www.backblaze.com/cloud-storage/pricing)
- [DigitalOcean S3 Alternatives](https://www.digitalocean.com/resources/articles/amazon-s3-alternatives)

### Self-Hosted
- [Garage Documentation](https://garagehq.deuxfleurs.fr/)
- [Garage GitHub](https://github.com/deuxfleurs-org/garage)
- [MinIO Maintenance Announcement](https://www.infoq.com/news/2025/12/minio-s3-api-alternatives/)
- [S3 Storage Benchmarks](https://www.repoflow.io/blog/benchmarking-self-hosted-s3-compatible-storage-a-practical-performance-comparison)

### Cost Calculators
- [Cloudflare R2 Calculator](https://r2-calculator.cloudflare.com/)
- [Cloud Storage Cost Comparison](https://transactional.blog/blog/2023-cloud-storage-costs)


---



<div style="page-break-before: always;"></div>

## architecture/system-architecture-explained.md

<!--
  MeepleAI System Architecture — Spiegazione completa
  Design: Quicksand (headings) + Nunito (body)
  Brand: Orange #d2691e | Purple #8b5cf6 | Green #2d7a4c | Amber #fbbf24
-->

<div align="center">

# MeepleAI — Architettura di Sistema

**Assistente AI per giochi da tavolo** · RAG · Multi-Agent · Living Docs

`v2.0` · `.NET 9` · `Next.js 16` · `Python AI` · `Docker Compose`

[Diagramma PDF](../../claudedocs/MeepleAI-Architecture.pdf)

</div>

---

## Indice

1. [Panoramica](#panoramica)
2. [Client Layer](#-1-client-layer)
3. [Application Layer](#-2-application-layer)
4. [External APIs](#-3-external-apis)
5. [Persistence Layer](#-4-persistence-layer)
6. [AI/ML Services](#-5-aiml-services)
7. [Observability & Automation](#-6-observability--automation)
8. [Mappa connessioni](#mappa-completa-delle-connessioni)
9. [Flussi di dati](#flussi-di-dati-principali)
10. [Audit: box senza connessione nel PDF](#audit-box-senza-connessione-nel-diagramma-pdf)
11. [Docker Compose](#docker-compose-profili-e-deployment)
12. [Glossario](#glossario)

---

## Panoramica

MeepleAI è organizzato come **monorepo** con architettura a microservizi, orchestrato tramite Docker Compose sulla rete bridge `meepleai`. L'architettura si divide in **6 zone funzionali**:

| | Zona | Colore diagramma | Responsabilità |
|---|------|:---:|---|
| **1** | **Client Layer** | 🟠 Ambra | Interfaccia utente, routing, proxy |
| **2** | **Application Layer** | 🔵 Blu | Logica di business — CQRS + DDD |
| **3** | **External APIs** | 🔴 Rosa | Servizi terze parti — LLM, BGG |
| **4** | **Persistence Layer** | 🟣 Viola | Storage — SQL, Vector, Cache, Blob |
| **5** | **AI/ML Services** | 🟢 Teal | Pipeline AI — embedding, reranking, agenti |
| **6** | **Observability** | ⚫ Grigio | Monitoraggio, workflow, alerting |

> **Rete**: tutti i servizi comunicano sulla rete bridge Docker `meepleai`. I nomi di servizio fungono da hostname DNS interni (es. `http://embedding-service:8000`).

---

## 🟠 1. Client Layer

> *Punto di ingresso per gli utenti — browser, PWA mobile*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **Browser** | — | React 19, Tailwind 4, shadcn/ui | SPA renderizzata nel browser. Chat AI, upload PDF, libreria, admin |
| **Next.js 16** | `:3000` | App Router + SSR + Zustand | Framework full-stack. Catch-all proxy verso API, middleware auth |

**Dettagli Next.js:**
- **Proxy catch-all**: `/api/v1/*` → backend .NET `:8080`
- **Middleware auth**: controlla cookie `meepleai_session` prima del rendering
- **Rotte protette**: `/dashboard`, `/chat`, `/upload`, `/admin`, `/editor`, `/settings`
- **Rotte pubbliche**: `/`, `/login`, `/register`
- **Cache sessione**: in-memory, TTL 2 min, max 200 entry
- **Design system**: glassmorphic `bg-white/70 backdrop-blur-md`, font Quicksand (heading) + Nunito (body)

### Connessioni

*(blocco di codice rimosso)*

---

## 🔵 2. Application Layer

> *Cuore del sistema — tutta la logica di business transita da qui*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **.NET 9 API** | `:8080` | ASP.NET Core 9, MediatR, EF Core, FluentValidation | API backend CQRS + DDD. 13 Bounded Context |

**Regola critica**: gli endpoint usano **SOLO** `IMediator.Send()` — zero iniezione diretta di servizi.

**Resilienza**: Polly per retry (3 tentativi) + circuit breaker verso servizi esterni.

### I 13 Bounded Context

| | Bounded Context | Responsabilità |
|---|----------------|---------------|
| 🔐 | **Authentication** | Login, sessioni, OAuth (Google/Discord/GitHub), 2FA |
| 🎲 | **GameManagement** | Catalogo giochi, sessioni di gioco, FAQ, specifiche |
| 🧠 | **KnowledgeBase** | RAG, agenti AI, chat, ricerca vettoriale |
| 🌐 | **SharedGameCatalog** | Database comunitario giochi con soft-delete |
| 📄 | **DocumentProcessing** | Upload PDF, estrazione testo, chunking |
| 👤 | **Administration** | Utenti, ruoli, audit trail, analytics |
| 📚 | **UserLibrary** | Collezioni, wishlist, storico |
| 🏆 | **Gamification** | Achievement, badge, classifiche |
| 📋 | **SessionTracking** | Note di sessione, punteggi, attività |
| 🔔 | **UserNotifications** | Notifiche, email, push |
| ⚙️ | **WorkflowIntegration** | n8n, webhook, logging |
| 🎛️ | **SystemConfiguration** | Config runtime, feature flag |
| 💼 | **BusinessSimulations** | Registrazioni contabili, scenari, previsioni |

### Connessioni

| Destinazione | Protocollo | Freccia nel PDF | Descrizione |
|-------------|:---------:|:---:|-------------|
| Next.js | HTTP + SSE + WS | ✅ | Risposte, streaming AI, real-time |
| OpenRouter | HTTPS + SSE | ✅ | Inferenza LLM (Claude/GPT) |
| BoardGameGeek | HTTPS + XML | ✅ | Import catalogo giochi |
| PostgreSQL | TCP `:5432` | ✅ | EF Core + pgvector |
| Qdrant | HTTP `:6333` / gRPC `:6334` | ✅ | Ricerca vettoriale |
| Redis | TCP `:6379` | ✅ | HybridCache L2 + sessioni |
| S3 / MinIO | HTTP `:9000` | ✅ | Blob storage PDF |
| Embedding | HTTP POST `/embeddings` | ✅ | Generazione embeddings |
| Reranker | HTTP POST `/rerank` | ✅ | Re-ranking risultati |
| Orchestration | HTTP POST `/execute` | ✅ | Pipeline multi-agente |
| Unstructured | HTTP `:8001` | ✅ | Estrazione PDF Stage 1 |
| SmolDocling | HTTP `:8002` | ⚠️ | Estrazione PDF Stage 2 (implicita in label "S1/S2") |
| Ollama | HTTP `:11434` | ❌ | Fallback LLM locale (solo se OpenRouter down) |
| HyperDX | HTTP OTLP push | ✅ | Tracce distribuite |
| Prometheus | HTTP `/metrics` scrape | ✅ | Metriche per scraping |
| Mailpit | SMTP `:1025` | ❌ | Email dev (connessione non disegnata) |

---

## 🔴 3. External APIs

> *Servizi terze parti via Internet — LLM e dati giochi*

### Servizi

| Servizio | Porta | Descrizione |
|----------|:-----:|-------------|
| **OpenRouter** | `:443` | Gateway LLM multi-provider (Claude, GPT). HTTPS + SSE streaming |
| **BoardGameGeek** | `:443` | API XML v2 per catalogo giochi da tavolo |

**OpenRouter:**
- Config: `infra/secrets/openrouter.secret`
- Resilienza: circuit breaker + 3 retry con backoff esponenziale (Polly)
- Fallback: OpenRouter down → Ollama locale → messaggio "servizio non disponibile"

**BGG:**
- Config: `infra/secrets/bgg.secret` (opzionale)
- Rate limiting per rispettare i limiti BGG

### Connessioni

| Sorgente | Protocollo | Freccia nel PDF | Descrizione |
|---------|:---------:|:---:|-------------|
| .NET API → OpenRouter | HTTPS + SSE | ✅ | Inferenza LLM |
| .NET API → BGG | HTTPS + XML | ✅ | Import giochi |
| Orchestration → OpenRouter | HTTPS diretto | ✅ | LLM diretto per agenti (bypassa API .NET) |

---

## 🟣 4. Persistence Layer

> *Storage dati — relazionale, vettoriale, cache, blob*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **PostgreSQL 16** | `:5432` | EF Core + pgvector | DB relazionale + vettoriale. Soft delete, audit, concurrency |
| **Qdrant** | `:6333` / `:6334` | Vector DB nativo | Ricerca vettoriale (cosine similarity). Solo via API .NET |
| **Redis 7.4** | `:6379` | HybridCache L2 | Cache distribuita + sessioni + cache semantica |
| **S3 / MinIO** | `:9000` | S3-compatible | Blob storage per PDF. AES256, URL pre-firmati |

**Pattern dati:**
- **Soft delete**: `IsDeleted` + `DeletedAt` (mai cancellazione fisica)
- **Audit trail**: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
- **Concurrency**: `RowVersion` con `DbUpdateConcurrencyException`

**Volumi persistenti:** `postgres_data`, `redis_data`, `qdrant_data`

### Connessioni

| Sorgente | Protocollo | Freccia nel PDF | Descrizione |
|---------|:---------:|:---:|-------------|
| .NET API → PostgreSQL | TCP (EF Core) | ✅ | CRUD, migrations, pgvector |
| .NET API → Qdrant | HTTP / gRPC | ✅ | Ricerca vettoriale |
| .NET API → Redis | TCP | ✅ | Cache L2, sessioni |
| .NET API → S3 | HTTP (S3 API) | ✅ | Upload/download PDF |
| Orchestration → PostgreSQL | TCP (asyncpg) | ✅ | Stato conversazioni (diretto) |
| Orchestration → Redis | TCP (redis.asyncio) | ✅ | Cache intent + regole (diretto) |
| n8n → PostgreSQL | TCP | ✅ | Stato workflow n8n |

> **Nota**: l'Orchestration Service accede a PostgreSQL e Redis **direttamente** tramite `asyncpg` e `redis.asyncio`, bypassando l'API .NET. Necessario per lo stato delle conversazioni multi-agente.

---

## 🟢 5. AI/ML Services

> *Pipeline AI — tutti microservizi Python con FastAPI*

### Servizi

| Servizio | Porta | Modello | Descrizione |
|----------|:-----:|---------|-------------|
| **Embedding** | `:8000` | `intfloat/multilingual-e5-large` (1024 dim) | Genera vettori dal testo. L2 normalized. 5 lingue |
| **Reranker** | `:8003` | `BAAI/bge-reranker-v2-m3` | Riordina risultati per rilevanza. Batch 32, 100 req/min |
| **Orchestration** | `:8004` | LangGraph Multi-Agent | Coordina agenti: Tutor, Arbitro, Decisore |
| **Unstructured** | `:8001` | Unstructured.io | PDF Stage 1 (veloce). Strategia `fast`, italiano |
| **SmolDocling** | `:8002` | `SmolDocling-256M-preview` | PDF Stage 2 (VLM). Max 20 pag, DPI 300, OCR avanzato |
| **Ollama** | `:11434` | Configurabili (llama, mistral) | Fallback LLM locale |

**Orchestration Service — peer bidirezionale dell'API .NET:**

L'API .NET chiama Orchestration via `POST /execute`. L'Orchestration **richiama** l'API .NET via `callback: /api/v1/kb/search` per la ricerca ibrida.

| Connessione diretta | Libreria | Motivo |
|---|---|---|
| → PostgreSQL | `asyncpg` | Stato conversazioni multi-agente |
| → Redis | `redis.asyncio` | Cache intent + regole |
| → OpenRouter | `httpx` | Chiamate LLM dirette per gli agenti |
| → .NET API | HTTP callback | `/api/v1/kb/search` per ricerca ibrida |
| → Embedding | HTTP | Health check |
| → Reranker | HTTP | Health check |

**Risorse Docker (profilo `ai`):**

| Servizio | CPU (limit) | RAM (limit) | CPU (res.) | RAM (res.) |
|----------|:-----------:|:-----------:|:----------:|:----------:|
| Embedding | 2 | 4 GB | 1 | 2 GB |
| Reranker | 2 | 2 GB | 1 | 1 GB |
| Orchestration | 2 | 4 GB | 1 | 2 GB |
| Unstructured | 2 | 2 GB | 1 | 1 GB |
| SmolDocling | 2 | 4 GB | 1 | 2 GB |
| Ollama | 4 | 8 GB | 2 | 4 GB |

### Connessioni

| Sorgente / Destinazione | Freccia nel PDF | Descrizione |
|---|:---:|---|
| .NET API → Embedding | ✅ | `POST /embeddings` |
| .NET API → Reranker | ✅ | `POST /rerank` |
| .NET API → Orchestration | ✅ | `POST /execute` |
| .NET API → Unstructured | ✅ | Estrazione PDF Stage 1 |
| .NET API → SmolDocling | ⚠️ | Stage 2 (label "S1/S2" copre entrambi, freccia solo a Unstructured) |
| Orchestration → PostgreSQL | ✅ | asyncpg diretto |
| Orchestration → Redis | ✅ | redis.asyncio diretto |
| Orchestration → OpenRouter | ✅ | LLM diretto |
| Orchestration ↔ .NET API | ✅ | Callback bidirezionale |
| Orchestration → Embedding/Reranker | ✅ | Health check (linee sottili) |
| .NET API → Ollama | ❌ | Fallback condizionale (vedi audit sotto) |

---

## ⚫ 6. Observability & Automation

> *Monitoraggio, alerting, workflow, strumenti di sviluppo*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **Prometheus** | `:9090` | TSDB metriche | Scrape HTTP da tutti i servizi. Retention 30d |
| **Grafana** | `:3001` | Dashboard | Visualizzazione metriche. Datasource: Prometheus |
| **HyperDX** | `:8180` | Osservabilità | Tracce distribuite OTLP |
| **n8n** | `:5678` | Workflow automation | Webhook, scheduling. Usa PostgreSQL come DB |
| **Mailpit** | `:8025` / `:1025` | SMTP dev | Cattura email in locale. UI web su `:8025` |
| **Alertmanager** | `:9093` | Alert routing | Gestione alert da Prometheus |
| **cAdvisor** | `:8082` | Container metrics | CPU, memoria, I/O dei container Docker |
| **Node Exporter** | `:9100` | Host metrics | Metriche OS (CPU, disco, rete) |

**Target di scraping Prometheus:**
- .NET API `:8080/metrics`
- Embedding `:8000/metrics`
- Reranker `:8003/metrics`
- Orchestration `:8004/metrics`
- Unstructured `:8001/metrics`
- SmolDocling `:8002/metrics`
- cAdvisor `:8082/metrics`
- Node Exporter `:9100/metrics`

### Connessioni

| Sorgente / Destinazione | Freccia nel PDF | Descrizione |
|---|:---:|---|
| Prometheus → .NET API | ✅ | Scrape `/metrics` (edge-route sinistra) |
| Prometheus → AI/ML Services | ❌ | Scrape metriche (non disegnato nel PDF) |
| Grafana → Prometheus | ✅ | Query PromQL (linea interna) |
| .NET API → HyperDX | ✅ | OTLP push (edge-route destra) |
| .NET API → Mailpit | ❌ | SMTP `:1025` (non disegnato nel PDF) |
| Prometheus → Alertmanager | ❌ | Alert routing (non disegnato nel PDF) |
| n8n → PostgreSQL | ✅ | Stato workflow (linea cross-zona) |
| Prometheus → cAdvisor | ❌ | Metriche container (non nel PDF) |
| Prometheus → Node Exporter | ❌ | Metriche host (non nel PDF) |

---

## Mappa completa delle connessioni

### Connessioni principali (linee solide nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 1 | Next.js | .NET API | HTTP proxy | `/api/v1/* catch-all proxy` |
| 2 | .NET API | OpenRouter | HTTPS + SSE | `LLM Inference (SSE stream)` |
| 3 | .NET API | PostgreSQL | TCP (EF Core) | `EF Core + pgvector` |
| 4 | .NET API | Qdrant | HTTP/gRPC | `Vector Search (cosine)` |
| 5 | .NET API | Embedding | HTTP POST | `POST /embeddings` |
| 6 | .NET API | Reranker | HTTP POST | `POST /rerank` |
| 7 | .NET API | Orchestration | HTTP POST | `POST /execute` |

### Connessioni secondarie (linee tratteggiate nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 8 | .NET API | BGG | HTTPS + XML | `XML API v2 + rate limit` |
| 9 | .NET API | Redis | TCP | `Cache + Sessions` |
| 10 | .NET API | S3/MinIO | HTTP (S3) | `Blob Storage` |
| 11 | .NET API | Unstructured | HTTP | `PDF Extract (S1/S2)` |

### Connessioni cross-zona dirette (aggiunte in v7)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 12 | Orchestration | PostgreSQL | TCP (asyncpg) | `asyncpg (conversation state)` |
| 13 | Orchestration | Redis | TCP (redis.asyncio) | `redis.asyncio (intent + rule cache)` |
| 14 | Orchestration | OpenRouter | HTTPS | `LLM (direct)` |
| 15 | Orchestration | .NET API | HTTP callback | `callback: /api/v1/kb/search` |
| 16 | n8n | PostgreSQL | TCP | `n8n → PG (workflow state)` |

### Connessioni osservabilità (edge-route nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 17 | .NET API | HyperDX | HTTP (OTLP) | `OTLP` (edge destra) |
| 18 | Prometheus | .NET API | HTTP scrape | `scrape /metrics` (edge sinistra) |
| 19 | Grafana | Prometheus | HTTP | Linea interna |

### Connessioni intra-zona (linee sottili nel PDF)

| # | Sorgente | Destinazione | Descrizione |
|:-:|---------|-------------|-------------|
| 20 | Orchestration | Embedding | Health check |
| 21 | Orchestration | Reranker | Health check |

---

## Audit: box senza connessione nel diagramma PDF

Alcuni servizi nel PDF non hanno frecce visibili. Ecco perch&eacute;:

| Box nel PDF | Freccia | Motivo |
|---|:---:|---|
| **SmolDocling** | ⚠️ | La freccia "PDF Extract (S1/S2)" va al nodo Unstructured. SmolDocling &egrave; coperto implicitamente dalla label "(S1/S2)" ma non ha una freccia propria. L'API .NET chiama SmolDocling solo se Stage 1 fallisce (qualit&agrave; < 80%) |
| **Ollama** | ❌ | Nessuna freccia. Ollama &egrave; un **fallback condizionale**: viene chiamato dall'API .NET solo se OpenRouter &egrave; irraggiungibile. In condizioni normali non riceve traffico |
| **Mailpit** | ❌ | Nessuna freccia. L'API .NET invia email via SMTP `:1025` a Mailpit, ma questa connessione non &egrave; stata disegnata nel PDF per evitare ulteriore complessit&agrave; visiva |
| **Alertmanager** | ❌ | Nessuna freccia. Prometheus invia alert ad Alertmanager via HTTP, ma la connessione non &egrave; nel PDF |
| **cAdvisor** | — | Non presente nel PDF (solo nel documento). Prometheus scrape le sue metriche su `:8082` |
| **Node Exporter** | — | Non presente nel PDF (solo nel documento). Prometheus scrape le sue metriche su `:9100` |

> Tutti i box "senza freccia" hanno connessioni reali nel sistema. Non sono stati disegnati nel PDF per mantenere leggibilit&agrave; — il diagramma mostra gi&agrave; 21 connessioni.

---

## Flussi di dati principali

### 1. RAG Chat

> Utente fa una domanda → risposta AI con citazioni

*(blocco di codice rimosso)*

### 2. PDF Upload

> Utente carica un regolamento → indicizzazione automatica

*(blocco di codice rimosso)*

### 3. Multi-Agent

> Richiesta complessa → orchestrazione con pi&ugrave; agenti AI

*(blocco di codice rimosso)*

### 4. Import giochi da BGG

*(blocco di codice rimosso)*

### 5. Autenticazione

*(blocco di codice rimosso)*

### 6. Real-time

*(blocco di codice rimosso)*

---

## Docker Compose: profili e deployment

| Profilo | Servizi | Uso |
|---------|---------|-----|
| `minimal` | PostgreSQL, Redis, Qdrant, API, Web | Dev base senza AI |
| `dev` | minimal + Prometheus, Grafana, Mailpit, Alertmanager, cAdvisor | Dev con monitoraggio |
| `ai` | dev + Embedding, Reranker, Orchestration, Unstructured, SmolDocling | Dev con AI completa |
| `full` | ai + n8n, Ollama, HyperDX | Stack completo |

**Volumi persistenti:**

| Volume | Servizio | Contenuto |
|--------|----------|-----------|
| `postgres_data` | PostgreSQL | Dati relazionali + vettoriali |
| `redis_data` | Redis | Cache + sessioni |
| `qdrant_data` | Qdrant | Indici vettoriali |
| `grafana_data` | Grafana | Dashboard configurate |

---

## Glossario

| Termine | Significato |
|---------|------------|
| **CQRS** | Command Query Responsibility Segregation — separazione letture/scritture |
| **DDD** | Domain-Driven Design — design guidato dal dominio di business |
| **MediatR** | Libreria .NET per il pattern mediator — disaccoppia handler |
| **RAG** | Retrieval-Augmented Generation — generazione AI potenziata da retrieval |
| **SSE** | Server-Sent Events — streaming unidirezionale server → client |
| **pgvector** | Estensione PostgreSQL per ricerca vettoriale |
| **Cross-encoder** | Modello che compara direttamente query-documento per re-ranking |
| **LangGraph** | Framework Python per orchestrazione di agenti AI |
| **HybridCache** | Cache .NET multi-livello (L1 in-memory + L2 Redis) |
| **Soft delete** | Eliminazione logica (`IsDeleted`) invece di cancellazione fisica |
| **Circuit breaker** | Pattern di resilienza che interrompe chiamate a servizi falliti |
| **asyncpg** | Driver PostgreSQL asincrono per Python (usato dall'Orchestration) |

---

<div align="center">

*Ultimo aggiornamento: 2026-02-19* · *Diagramma: [`MeepleAI-Architecture.pdf`](../../claudedocs/MeepleAI-Architecture.pdf)*

</div>


---

