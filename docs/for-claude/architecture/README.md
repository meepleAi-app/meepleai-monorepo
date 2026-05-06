# Architecture Documentation

**MeepleAI System Architecture** — ADRs, DDD, diagrammi, overview di sistema

---

## Quick Navigation

| Risorsa | Percorso | Descrizione |
|---------|----------|-------------|
| **System Architecture** | [overview/system-architecture.md](./overview/system-architecture.md) | Overview completo dello stack |
| **ADR Index** | [adr/README.md](./adr/README.md) | Architecture Decision Records |
| **DDD Quick Reference** | [ddd/quick-reference.md](./ddd/quick-reference.md) | Pattern DDD e Bounded Contexts |
| **Diagrams** | [diagrams/README.md](./diagrams/README.md) | Mermaid diagrams (CQRS, RAG, PDF pipeline) |
| **Bounded Contexts** | [../bounded-contexts/README.md](../bounded-contexts/README.md) | 18 BC con responsabilità e pattern |

---

## Stack Tecnologico

**Backend** (.NET 9): ASP.NET Minimal APIs + MediatR | PostgreSQL 16 + EF Core (pgvector) + Redis | FluentValidation

**Frontend** (Next.js 16): App Router + React 19 | Tailwind 4 + shadcn/ui | Zustand + React Query

**AI** (Python): sentence-transformers | cross-encoder | Unstructured | SmolDocling

---

## Pattern Architetturali

| Pattern | Implementazione |
|---------|----------------|
| **DDD** | 18 Bounded Contexts con Domain/Application/Infrastructure |
| **CQRS** | Commands + Queries via MediatR — zero direct service injection negli endpoint |
| **Clean Architecture** | Dipendenze verso l'interno: Infrastructure → Application → Domain |
| **Event-Driven** | Domain Events per comunicazione inter-context |
| **RAG** | Hybrid retrieval (vector pgvector + keyword FTS) con multi-layer validation |

---

## ADR Essenziali

| ADR | Decisione | Importanza |
|-----|-----------|-----------|
| [ADR-006](./adr/adr-006-multi-layer-validation.md) | Multi-Layer Validation (5 layer) | 🔴 Critico |
| [ADR-007](./adr/adr-007-hybrid-llm.md) | Hybrid LLM (Ollama + OpenRouter) | 🟡 Alto |
| [ADR-009](./adr/adr-009-centralized-error-handling.md) | Centralized Error Handling | 🟡 Alto |
| [ADR-012](./adr/adr-012-fluentvalidation-cqrs.md) | FluentValidation con CQRS | 🟡 Alto |
| [ADR-050](./adr/adr-050-pgvector-migration.md) | Migrazione Qdrant → pgvector | 🟡 Alto |

→ [Indice completo ADR](./adr/README.md)

---

## Infrastruttura Docker

Per setup Docker Compose, Traefik e monitoring, vedi:
- **[Deployment Guide](../deployment/README.md)** — guida completa all'infrastruttura
- **[Development Docker](../development/docker/README.md)** — ambiente locale

---

**Last Updated**: 2026-04-05
**Maintainer**: Architecture Team
