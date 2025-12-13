MeepleAI — Documento consolidato (estratto dal codice, 2025-12-13T10:53:57.588Z)

Scopo
- Riepilogo unico e aggiornato della codebase: struttura, avvio locale, servizi chiave, pipeline PDF/RAG, testing, variabili d'ambiente e operazioni.

Panoramica veloce
- AI board game rules assistant: PDF ingestion → embedding → hybrid RAG (Qdrant + PG FTS) → LLM (OpenRouter/GPT/Claude) per Q&A.
- Stack: .NET 9 (API), Next.js 16 + React 19 (Web), PostgreSQL, Qdrant, Redis, Docker Compose.

Struttura del monorepo
- apps/api: backend (DDD, 7 bounded contexts, MediatR/CQRS).
- apps/web: frontend Next.js.
- apps/unstructured-service, apps/smoldocling-service: estrazione PDF.
- infra: docker compose, profili (minimal, dev, observability, ai, automation, full).
- docs: documentazione tecnica organizzata.

Avvio rapido (sviluppo)
1. Avviare i servizi: cd infra && ./start-minimal.sh (o docker compose up -d con profile).
2. Api: cd apps/api/src/Api && dotnet run (porta 8080).
3. Web: cd apps/web && pnpm install && pnpm dev (porta 3000).

Servizi principali
- Core: postgres:5432, qdrant:6333, redis:6379
- AI/ML: ollama, embedding, unstructured, smoldocling, reranker (vari porte in infra)
- Observability: prometheus, grafana, hyperdx opzionale

Pipeline PDF
- 3-stage: Unstructured (stage 1, preferito) → SmolDocling (stage 2) → Docnet (stage 3 fallback).
- Validazione qualità (soglia minima ~0.80), metriche di coverage/structure/table/page.

RAG (retrieval + generation)
- Hybrid: vector (Qdrant) + keyword (Postgres FTS) con fusione RRF (70/30).
- Multi-model generation/validation (obiettivi: confidenza ≥0.70, <3% hallucination target).

Endpoint e pattern
- Tutti gli endpoint usano IMediator.Send() (zero service injection) con pattern Commands/Queries.
- Esempi: POST /api/v1/chat (AskQuestionCommand SSE), GET /api/v1/search (SearchQuery).

Testing e qualità
- Coverage richiesta: frontend 90%+, backend 90%+.
- Suite: frontend (Jest + RTL + Playwright), backend (xUnit + Moq + Testcontainers).
- Script utili: dotnet test, pnpm test, pwsh tools/measure-coverage.ps1.

Variabili d'ambiente importanti
- OPENROUTER_API_KEY, CONNECTIONSTRINGS__POSTGRES, QDRANT_URL, REDIS_URL, NEXT_PUBLIC_API_BASE
- Non commettere file .env; utilizzare i template in infra/env.

CI/CD e manutenzione
- GitHub Actions per ci-web, ci-api, security-scan; Codecov per coverage.
- Script di avvio in infra e strumenti di pulizia in tools/.

Note operative e link utili
- Docs index: docs/INDEX.md
- ADR principali: docs/01-architecture/adr/
- API Spec: docs/03-api/board-game-ai-api-specification.md
- Consolidation artefacts: docs/CONSOLIDATION-FINAL-SUMMARY.md

Contatti
- Owner: Engineering Lead (vedere repo per dettagli). 

---
Questo file è stato generato come riepilogo consolidato basato sul codice e la documentazione presente in repository alla data indicata. Per richieste di approfondimento o per ottenere una versione estesa (ad es. pagine dedicate per deploy, security, runbook), indicare quali sezioni espandere.