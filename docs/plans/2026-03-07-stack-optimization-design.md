# Stack Optimization Design: Enterprise to MVP

**Date**: 2026-03-07
**Status**: Approved
**Goal**: Reduce from 18+ containers / ~42 GB RAM to 6 containers / ~8.5 GB RAM
**Target hosting**: Hetzner CAX31 (8 ARM cores, 16 GB RAM, EUR 9.30/month)
**Breakeven**: 3 paying users at EUR 4.99-12.99/month

## Architecture

### Target Stack (6 containers)

| Service | Role | RAM |
|---------|------|-----|
| PostgreSQL 16 + pgvector | Relational DB + vector search + FTS | 1.5 GB |
| Redis 7.4 | Cache L1+L2, sessions, rate limiting, token bucket | 0.5 GB |
| .NET 9 API | RAG pipeline, CQRS, PdfPig in-process | 1.5 GB |
| Next.js (standalone) | Frontend SSR+CSR | 0.4 GB |
| Ollama | LLM (Qwen2.5-1.5B) + Embedding (mxbai-embed-large 1024d) | 2.2 GB |
| Caddy | Reverse proxy, auto-TLS | 0.1 GB |
| **Total** | | **~8.5 GB** (7.5 GB headroom on 16 GB) |

### Eliminated Services (12 containers)

| Service | RAM Freed | Replacement |
|---------|-----------|-------------|
| embedding-service (Python, e5-large) | 4 GB | Ollama mxbai-embed-large (1024d, same dimensions) |
| reranker-service (Python, bge-reranker-v2-m3) | 2 GB | Graceful degradation to RRF scores (already in code) |
| unstructured-service (Python, PDF extraction) | 2 GB | PdfPig .NET in-process + offline pre-processing |
| smoldocling-service (Python, VLM PDF) | 4 GB | Claude Vision API on-demand (EUR 0.01/page) |
| orchestration-service (Python, LangGraph) | 4 GB | Removed (not wired to main flow) |
| Qdrant | 4 GB | pgvector HNSW index in PostgreSQL |
| Prometheus + Grafana + AlertManager + cAdvisor + node-exporter | 4.3 GB | Serilog file JSON + UptimeRobot free |
| n8n | 1 GB | Removed (no critical workflows) |
| MinIO | 512 MB | Filesystem local, Cloudflare R2 in future |
| Mailpit | 128 MB | Removed (dev only) |
| Traefik | 256 MB | Caddy (simpler config, auto-TLS) |

## Key Design Decisions

### 1. Embedding: Ollama mxbai-embed-large

- **Dimensions**: 1024 (identical to current e5-large, no DB schema change)
- **Quality**: MTEB 64.7 vs e5-large 61.5 (mxbai is better)
- **RAM**: +0.2 GB on top of existing Ollama (vs +1.7 GB for Python service)
- **Trade-off**: Sequential embedding (no batch), mitigated by async PDF processing
- **Fallback**: If quality insufficient for Italian, add Python e5-large container back

### 2. Vector Search: pgvector replaces Qdrant

- pgvector already enabled in DbContext (UseVector())
- VectorDocumentEntity already has vector(1024) column
- New: PgVectorStoreAdapter implementing same interface as QdrantVectorStoreAdapter
- New: HNSW index (m=16, ef_construction=200) via EF migration
- Performance equivalent for <50K vectors; reintroduce Qdrant if needed at scale

### 3. Reranker: Disabled with Graceful Degradation

- ResilientRetrievalService already handles reranker absence
- Circuit breaker: 3 failures triggers automatic fallback to RRF scores
- Quality impact: -7-11% on ambiguous queries, zero on direct factual Q&A
- Config change only: `EnableReranking: false` in appsettings.json

### 4. PDF Processing: Hybrid Offline + Runtime

- **Offline** (pre-launch): Unstructured CLI on dev machine for top 50-100 games
- **Runtime** (production): PdfPig .NET (UglyToad.PdfPig, MIT) for user uploads
- **Edge cases**: Claude Vision API on-demand for complex scanned PDFs
- No Python service needed in production

### 5. LLM Routing: Unchanged

The HybridLlmService routing logic stays identical:
- Cache L1 (memory) + L2 (Redis): instant response, EUR 0
- FAST/BALANCED: Ollama Qwen2.5-1.5B local, EUR 0
- PRECISE+: DeepSeek/Claude via OpenRouter, EUR 0.008-0.13/query
- Budget alerts and auto-downgrade already implemented

### 6. Monitoring: Simplified

- Serilog structured JSON logging (file rotation)
- /admin/health endpoint (already exists)
- AdminBudgetService for LLM cost tracking (already exists)
- UptimeRobot free tier for uptime monitoring
- Reintroduce Prometheus/Grafana when >50 paying users

## Data Migration

### Re-embedding (e5-large to mxbai-embed-large)

Both models produce 1024-dimensional vectors but in different semantic spaces.
All documents must be re-embedded before launch.

- Estimated volume: 50-100 games x ~50 chunks = ~5K vectors
- Estimated time: ~25 minutes via Ollama sequential embedding
- One-time operation, offline, before launch

### Qdrant to pgvector

- Implement PgVectorStoreAdapter (same interface)
- EF Migration: CREATE INDEX USING hnsw
- Re-index all vectors (combined with re-embedding step)
- Remove Qdrant.Client NuGet package

## Quality Impact Matrix

| Functionality | Enterprise Stack | MVP Stack | Quality Delta |
|--------------|-----------------|-----------|:---:|
| Simple Q&A ("how many VP to win?") | Claude Sonnet 4.5 | Cache/Ollama local | 0% |
| Medium Q&A ("port + knight interaction") | DeepSeek + reranker | Ollama + RRF | -5-8% |
| Complex Q&A ("compare strategies") | Claude multi-agent | DeepSeek cloud | -10-15% |
| Vector search (<50K vectors) | Qdrant HNSW | pgvector HNSW | 0% |
| PDF extraction (top games) | Unstructured real-time | Pre-processed offline | 0% |
| PDF upload (user) | Unstructured + SmolDocling | PdfPig + Claude Vision | -10-20% on complex PDFs |
| Embedding quality | e5-large 1024d | mxbai-embed-large 1024d | +3% (mxbai better) |
| Response latency (cache miss) | 1-3s cloud | 7-12s local + streaming | Noticeable |

## Cost Model

### Monthly Costs

| Item | Cost |
|------|------|
| Hetzner CAX31 | EUR 9.30 |
| Domain + DNS | EUR 1.50 |
| OpenRouter (pay-per-use) | EUR 5-15 |
| Cloudflare free tier | EUR 0 |
| UptimeRobot free tier | EUR 0 |
| **Total** | **EUR 16-26** |

### Breakeven

- Starter (EUR 4.99): 4 users
- Pro (EUR 12.99): 2 users
- Realistic mix: **3 paying users**

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|:-:|:-:|------------|
| mxbai quality insufficient for Italian | Low | Medium | Benchmark 50 IT queries pre-launch. Fallback: add Python e5-large |
| PdfPig fails on complex PDFs | Medium | Low | Queue for manual review + Claude Vision API |
| pgvector slow at >50K vectors | Low | Low | Months away. Reintroduce Qdrant when needed |
| Ollama model swapping delay | Medium | Low | OLLAMA_KEEP_ALIVE=24h keeps both in RAM |
| Qwen2.5-1.5B quality too low | Medium | High | Upgrade to 3B (+0.5 GB) or route all to cloud |
| ARM64 compatibility issues | Low | High | .NET 9 + Ollama support ARM64 natively. Test before deploy |

## Monetization: Token Bucket Model

| Plan | Price | Queries/month | Cost per query |
|------|-------|:---:|:---:|
| Free | EUR 0 | 5/day (FAST only) | EUR 0 (local) |
| Starter | EUR 4.99 | 100 | EUR 0.001-0.008 |
| Pro | EUR 12.99 | 500 | EUR 0.001-0.008 |
| Top-up | EUR 2.99 | +50 | Same |

Margin: 83-98% depending on cache hit rate and query tier distribution.

## Implementation Phases

1. **Eliminate non-essential services** (1-2 days) - Remove from docker-compose, verify graceful degradation
2. **Switch embedding to Ollama mxbai** (2-3 days) - Config change, re-embed documents
3. **Disable reranker** (1 day) - Config change, test RAG quality
4. **Replace Qdrant with pgvector** (3-5 days) - New adapter, migration, re-index
5. **PDF processing transition** (2-3 days) - PdfPig integration, offline pre-processing
6. **Deploy on Hetzner CAX31** (1-2 days) - compose.mvp.yml, Caddy, DNS

**Total estimated effort**: ~2 weeks
