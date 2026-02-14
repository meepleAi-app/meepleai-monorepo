# MeepleAI Monorepo

AI-powered board game assistant with RAG, multi-agent architecture, and living documentation.

## Coverage

| Stack | Current | Target | Status |
|-------|---------|--------|--------|
| Backend | 4.30% | 90% | Phase 2 |
| Frontend | 39% | 85% | Phase 2 |

## Features

- **RAG System**: Hybrid retrieval (vector + keyword) with multi-model validation
- **AI Agents**: Rules explanation, strategy assistance, setup guidance
- **PDF Processing**: Layout analysis + multilingual OCR
- **Game Catalog**: Community database with soft-delete + audit trails
- **Real-time**: SSE streaming + CQRS architecture

## Tech Stack

**Backend** (.NET 9)
- ASP.NET Minimal APIs + MediatR
- PostgreSQL 16 + EF Core
- Qdrant + Redis
- FluentValidation
- xUnit + Testcontainers

**Frontend** (Next.js 14)
- App Router + React 18
- Tailwind + shadcn/ui
- Zustand + React Query
- Vitest + Playwright

**AI** (Python)
- sentence-transformers
- cross-encoder
- Unstructured
- SmolDocling

**Infrastructure**
- Docker Compose
- Traefik
- Grafana + Prometheus
- GitHub Actions

## Quick Start

```bash
# Clone & install dependencies
git clone <repo> && cd meepleai-monorepo-dev
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install

# Setup secrets (auto-generates JWT, passwords, API keys)
cd ../../infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated

# Start infrastructure
cd ../../infra && docker compose up -d postgres qdrant redis

# Backend (Terminal 1)
cd ../apps/api/src/Api && dotnet run  # http://localhost:8080

# Frontend (Terminal 2)
cd ../../../web && pnpm dev  # http://localhost:3000
```

## Documentation

- [Developer Guide](./CLAUDE.md)
- [Architecture](./docs/01-architecture/)
- [Development](./docs/02-development/)
- [API Reference](./docs/03-api/)
- [Testing Guide](./docs/05-testing/)

## Project Structure

```
apps/
├── api/              # .NET 9 Backend (9 Bounded Contexts)
├── web/              # Next.js 14 Frontend
├── embedding-service/    # Python embeddings
├── reranker-service/     # Python reranking
└── *-service/            # Python PDF/docs processing

docs/                 # Documentation
infra/                # Docker, Traefik, monitoring
tests/                # Backend test suite
.github/workflows/    # CI/CD pipelines
```

## License

Proprietary
