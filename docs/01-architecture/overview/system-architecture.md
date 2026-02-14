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

```
┌─────────────────────────────────────────────────────────┐
│                     MEEPLEAI SYSTEM                     │
│                                                         │
│  [Web App] [Mobile PWA] [API Clients]                  │
│         │         │           │                         │
│         └─────────┴───────────┘                         │
│                   │                                     │
│            [API Gateway + Rate Limit]                   │
│                   │                                     │
│     ┌─────────────┼─────────────┐                       │
│     │             │             │                       │
│  [Q&A Service] [Indexing] [Admin Dashboard]            │
│     │             │                                     │
│     └─────────────┘                                     │
│           │                                             │
│  ┌────────┼────────┐                                    │
│  │        │        │                                    │
│ [Vector DB] [LLM APIs] [PostgreSQL]                    │
│ (Qdrant)  (GPT-4+Claude) (FTS+Metadata)                │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Layers

```
Presentation: Web (Next.js) | Mobile (PWA) | Public API (REST)
     ↓
Application: Q&A | Indexing | Validation | Citations | Admin
     ↓
Domain: RAG Pipeline | PDF Processing | Embedding | Multi-Model Validation
     ↓
Infrastructure: Vector DB | Cache | DB | LLM APIs | Monitoring
```

---

## Component Architecture

### 1. Question Answering Pipeline

```python
# Query → Retrieve → Generate → Validate

class QAPipeline:
    def answer(self, question: str, game_id: str) -> Response:
        # 1. Process query
        query = QueryProcessor().process(question, game_id)

        # 2. Retrieve context
        context = RetrievalService().retrieve(query, top_k=10)

        # 3. Generate answer
        generated = GenerationService().generate(query, context)

        # 4. Validate (5 layers)
        validated = ValidationService().validate(query, generated, context)

        return validated
```

**Retrieval** (Hybrid Search in Phase 3+):
```python
vector_results = weaviate.search(embedding, top_k=10)
keyword_results = postgres_fts.search(query_text, top_k=10)
merged = rrf_fusion(vector_results, keyword_results, k=60)  # 70% vector + 30% keyword
```

**Generation** (GPT-4 Turbo):
```python
prompt = f"Sei esperto di {game_name}. Rispondi basandoti su: {context}. Domanda: {question}"
response = openai.chat.completions.create(
    model="gpt-4-turbo-2024-04-09",
    temperature=0.1,
    max_tokens=1024
)
```

**Validation** (Multi-Model):
```python
if 0.70 <= confidence < 0.85:
    claude_response = anthropic.generate(validation_prompt)
    similarity = cosine(embed(gpt_answer), embed(claude_answer))
    consensus = similarity >= 0.90
```

---

### 2. Indexing Pipeline

```
PDF Upload → Extract Text → Chunk → Embed → Index → Metadata

1. PDF Processing (3-stage fallback):
   Unstructured (1.3s, Apache 2.0, RAG-optimized)
   ↓ fail
   SmolDocling (vision-language, GPU)
   ↓ fail
   Docnet.Core (existing, proven)

2. Text Chunking (sentence-aware, 256-768 chars, 20% overlap)

3. Embedding (multilingual-e5-large, 1024 dims)

4. Vector DB (Qdrant, batch insert)

5. Metadata (PostgreSQL: filename, page_count, quality_score)
```

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

```
1. Input: "Posso usare Standard Projects dopo aver passato?" (Terraforming Mars)
2. Process: Normalize → Detect Italian → Enrich with game context
3. Retrieve: Embedding → Qdrant search → Top 10 chunks (similarity ≥0.80)
4. Generate: GPT-4 Turbo (temp=0.1) → Answer + confidence=0.85 + citations
5. Validate:
   ✅ Confidence: 0.85 ≥ 0.70
   ✅ Consensus: Claude validates (similarity=0.93)
   ✅ Citation: Page 8 exists, snippet matches
   ✅ Keywords: None detected
6. Response: Answer + confidence badge + [📄 Pag. 8] + feedback buttons
```

### Indexing Flow

```
1. Upload: "terraforming_mars_italiano.pdf" (2.3MB, 24 pages) → Validate ≤50MB ✅
2. Extract: Unstructured (1.3s) → 23,450 chars (quality=0.92)
3. Chunk: 487 sentences → 89 chunks (avg 263 chars, 20% overlap)
4. Embed: multilingual-e5-large → 89 × 1024 dims (~15s)
5. Index: Qdrant batch insert (~5s)
6. Metadata: PostgreSQL (filename, page_count=24, chunk_count=89)
7. Notify: "Indicizzazione completata! 89 sezioni analizzate."
```

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

```yaml
services:
  api: { build: ./backend, ports: ["8000:8000"], depends_on: [postgres, redis, qdrant] }
  frontend: { build: ./frontend, ports: ["3000:3000"], depends_on: [api] }
  postgres: { image: postgres:16-alpine, volumes: ["postgres_data:/var/lib/postgresql/data"] }
  redis: { image: redis:7-alpine }
  qdrant: { image: semitechnologies/qdrant:1.23.0 }
```

### Phase 2+ (Production) - Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata: { name: meepleai-api }
spec:
  replicas: 3  # HA
  template:
    spec:
      containers:
      - name: api
        image: meepleai/api:v1.2.0
        resources:
          requests: { memory: "512Mi", cpu: "500m" }
          limits: { memory: "2Gi", cpu: "2000m" }
        livenessProbe: { httpGet: { path: /health, port: 8000 } }
```

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

```python
# Application
qa_requests_total = Counter('qa_requests_total', ['game_id', 'language'])
qa_duration_seconds = Histogram('qa_duration_seconds', ['game_id'])
qa_confidence_score = Histogram('qa_confidence_score')
qa_validation_failures = Counter('qa_validation_failures_total', ['layer'])

# LLM
llm_api_calls_total = Counter('llm_api_calls_total', ['provider', 'model'])
llm_api_tokens_total = Counter('llm_api_tokens_total', ['provider', 'type'])

# System
active_users_gauge = Gauge('active_users')
cache_hit_rate = Gauge('cache_hit_rate')
```

### Alerts (Critical)

```yaml
- alert: HighAccuracyDrop
  expr: rate(qa_validation_failures_total[5m]) > 0.10  # >10% failures
  severity: critical

- alert: HighHallucinationRate
  expr: rate(qa_validation_failures{layer="forbidden_keywords"}[1h]) > 0.03
  severity: critical

- alert: LLMAPIDown
  expr: rate(llm_api_errors_total[2m]) > 0.50
  severity: critical
```

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
