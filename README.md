# MeepleAI Monorepo

> AI-powered board game assistant — RAG, multi-agent AI, and living documentation.

<!-- snapshot-badge:start -->Snapshot freshness: stale (68 d) 🔴<!-- snapshot-badge:end -->

MeepleAI answers rules questions, assists with strategy, and guides game setup by
combining hybrid retrieval (RAG) over uploaded rulebooks with a multi-agent AI layer.
This repository is a monorepo: a .NET 9 backend, a Next.js frontend, and a set of
Python AI microservices, all orchestrated with Docker Compose.

New here? Start with **[Quick Start](#quick-start)**, then read
**[CLAUDE.md](./CLAUDE.md)** — the single source of truth for the dev workflow.

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0 | Backend (`apps/api`) |
| Node.js | 20+ | Required by Next.js 16 |
| pnpm | latest | Frontend package manager (workspaces) |
| Docker + Compose | latest | Runs Postgres, Redis, AI services, monitoring |
| Git | latest | On Windows, **Git Bash** is required for integration/`make` scripts |

## Quick Start

```bash
git clone <repo-url> && cd meepleai-monorepo

# 1. Install dependencies
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install

# 2. Secrets — generate placeholders, then sync real values from staging (needs SSH)
cd ../../infra && make secrets-setup && make secrets-sync

# 3. Frontend env
cp ../apps/web/.env.development.example ../apps/web/.env.local

# 4. Start the stack (run from infra/)
make dev          # full stack (AI + monitoring + proxy + storage)
# make dev-core   # core only: postgres + redis + api + web
```

> `make secrets-sync` requires SSH access to the staging server. Without it,
> `make secrets-setup` still generates placeholders sufficient to boot the core stack.

Once running:

| Service | URL |
|---------|-----|
| Web app | http://localhost:3000 |
| API + REST explorer (Scalar) | http://localhost:8080/scalar/v1 |
| OpenAPI spec | http://localhost:8080/openapi/v1.json |
| Grafana | http://localhost:3001 |

Common commands (run from `infra/`): `make help` · `make dev-down` · `make logs s=api`.

**Run a single layer without Docker** (infra still needed via `make dev-core`):

```bash
cd apps/api/src/Api && dotnet run   # API  → http://localhost:8080
cd apps/web && pnpm dev             # Web  → http://localhost:3000
```

## Features

- **RAG System** — hybrid retrieval (vector + keyword) with multi-model validation
- **AI Agents** — rules explanation, strategy assistance, setup guidance
- **PDF Processing** — layout analysis + multilingual OCR
- **Game Catalog** — community database with soft-delete + audit trails
- **Real-time** — SSE streaming + SignalR live sessions, CQRS architecture

## Tech Stack

**Backend** (.NET 9)
- ASP.NET Minimal APIs + MediatR (CQRS)
- PostgreSQL 16 + EF Core, with **pgvector** for vector search
- Redis
- FluentValidation
- xUnit + Testcontainers

**Frontend** (Next.js 16)
- App Router + React 19
- Tailwind 4 + shadcn/ui
- Zustand + React Query
- Vitest + Playwright

**AI** (Python microservices)
- sentence-transformers (embeddings)
- cross-encoder (reranking)
- Unstructured + SmolDocling (PDF/document extraction)

**Infrastructure**
- Docker Compose (profiled)
- Reverse proxy: Traefik (dev) / Cloudflare Tunnel (staging)
- Grafana + Prometheus
- GitHub Actions

## Architecture

- **CQRS pattern**: endpoints call `IMediator.Send()` only — no direct service injection.
- **DDD**: 20+ bounded contexts under `apps/api/src/Api/BoundedContexts/`
  (Authentication, KnowledgeBase, GameManagement, DocumentProcessing, …).
- **Layers**: Domain → Application (commands/queries) → Infrastructure.

Full context table, data patterns (soft-delete, audit, concurrency) and the git
branching strategy are documented in **[CLAUDE.md](./CLAUDE.md)**.

## Project Structure

```
apps/
├── api/                  # .NET 9 backend — BoundedContexts/, Routing/, Infrastructure/
├── web/                  # Next.js 16 frontend — src/app/, components/, lib/
├── embedding-service/    # Python: embeddings
├── reranker-service/     # Python: reranking
├── unstructured-service/ # Python: PDF/document extraction
└── smoldocling-service/  # Python: document layout analysis
docs/                     # Documentation (for-users / for-developers / for-claude)
infra/                    # docker-compose, Makefile, secrets/, monitoring/
tests/                    # Test suite — Api.Tests, k6, api-smoke, llm-eval (see tests/README.md)
.github/workflows/        # CI/CD pipelines
```

## Testing

```bash
# Backend (target 90%+)
cd apps/api/src/Api
dotnet test                              # all
dotnet test --filter "Category=Unit"     # unit only
dotnet test /p:CollectCoverage=true      # with coverage

# Frontend (target 85%+)
cd apps/web
pnpm test && pnpm test:coverage          # unit (Vitest)
pnpm test:e2e                            # E2E (Playwright)
pnpm typecheck && pnpm lint              # quality gates
```

Test suite layout: [tests/README.md](./tests/README.md) · patterns: [docs/for-developers/testing/README.md](./docs/for-developers/testing/README.md).

## Documentation

| Audience | Entry point |
|----------|-------------|
| **Setting up / dev workflow** | [CLAUDE.md](./CLAUDE.md) — single source of truth |
| **Contributors** (BE/FE/DevOps/QA) | [docs/for-developers/](./docs/for-developers/README.md) |
| **Architecture & ADRs** | [docs/for-claude/architecture/adr/](./docs/for-claude/architecture/adr/) |
| **End users** | [docs/for-users/](./docs/for-users/README.md) |
| **REST API** | http://localhost:8080/scalar/v1 (live Scalar UI) |

Documentation hub: [docs/README.md](./docs/README.md).

## Contributing

Read [CONTRIBUTING.md](./CONTRIBUTING.md) first. Key rules:

- Branch from the parent (typically `main-dev`): `feature/issue-{n}-{desc}`.
- **PRs target the parent branch, not always `main`.**
- Commits follow Conventional Commits: `feat|fix|docs|refactor|test|chore(scope): description`.

## License

Proprietary
