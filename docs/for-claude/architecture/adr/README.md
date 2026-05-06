# Architecture Decision Records (ADR)

Architecture Decision Records for MeepleAI. Each ADR captures significant architectural decisions with context, rationale, and consequences.

## ADR Index

> **Nota sui gap numerici**: i numeri ADR non sono sequenziali per design. Gli ADR obsoleti o sostituiti vengono rimossi dalla cartella ma il numero non viene riassegnato (vedi git history per i numeri 001-019 mancanti). I range sono preassegnati per categoria.

### Core Architecture (001–009)

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [002](adr-002-multilingual-embedding.md) | Multilingual Embedding Strategy | 2025-01-15 | Implemented |
| [003b](adr-003b-unstructured-pdf.md) | Unstructured PDF Extraction | 2025-01-16 | Implemented |
| [004](adr-004-ai-agents.md) | AI Agents Architecture | 2025-01-15 | Implemented |
| [006](adr-006-multi-layer-validation.md) | Multi-Layer Validation Framework | 2025-01-16 | Implemented |
| [007](adr-007-hybrid-llm.md) | Hybrid LLM Strategy | 2025-01-17 | Implemented |
| [009](adr-009-centralized-error-handling.md) | Centralized Error Handling | 2025-01-18 | Implemented |

### Security & Validation (010–019)

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [011](adr-011-cors-whitelist-headers.md) | CORS Header Whitelist Strategy | 2025-01-19 | Implemented |
| [012](adr-012-fluentvalidation-cqrs.md) | FluentValidation Integration with CQRS | 2025-01-19 | Implemented |
| [017](adr-017-usage-aggregation-gdpr.md) | Usage Aggregation GDPR | 2026-01-20 | Implemented |
| [018](adr-018-postgresql-fts-for-shared-catalog.md) | PostgreSQL FTS for Shared Catalog | 2025-12-15 | Implemented |

### Frontend/Backend Integration (020–029)

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [020](adr-020-valueobject-record-evaluation.md) | ValueObject Record Syntax | 2026-01-14 | Rejected |
| [021](adr-021-auto-configuration-system.md) | Auto-Configuration System | 2026-01-17 | Implemented |
| [022](adr-022-ssr-auth-protection.md) | SSR Authentication Protection | 2025-11-22 | Implemented |
| [023](adr-023-share-request-workflow.md) | Share Request Workflow | 2026-01-20 | Implemented |
| [024](adr-024-advanced-pdf-embedding-pipeline.md) | Advanced PDF Embedding Pipeline | 2025-12-03 | Implemented |
| [025](adr-025-shared-catalog-bounded-context.md) | SharedGameCatalog Bounded Context | 2026-01-14 | Implemented |
| [026](adr-026-document-collections.md) | Document Collections | 2025-12-12 | Implemented |

### Observability & Performance (040–049)

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [042](adr-042-dashboard-performance.md) | Dashboard Performance | 2026-02-01 | Implemented |
| [043](adr-043-llm-subsystem-nfr.md) | LLM Subsystem NFR Documentation | 2026-02-10 | Implemented |
| [044](adr-044-self-hosted-arm64-runner.md) | Self-Hosted ARM64 Runner Migration | 2026-03-01 | Implemented |
| [045](adr-045-multi-region-strategy.md) | Multi-Region Scaling Strategy | 2026-03-10 | Accepted |
| [046](adr-046-game-sharedgame-data-ownership.md) | Game vs SharedGame Data Ownership | 2026-03-30 | Accepted |
| [047](adr-047-crossbc-fk-policy.md) | Cross-BC Foreign Key Policy | 2026-03-30 | Accepted |
| [048](adr-048-sharedgame-soft-delete.md) | SharedGame Soft Delete Behavior | 2026-03-30 | Accepted |

### Infrastructure (050–059)

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [050](adr-050-pgvector-migration.md) | Migrazione Qdrant → pgvector | 2026-03-29 | Implemented |

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
- 040-049: Observability & scaling
- 050-059: Infrastructure

## Related Docs

- [System Architecture](../overview/system-architecture.md)
- [DDD Quick Reference](../ddd/quick-reference.md)
- [Bounded Contexts Diagram](../diagrams/bounded-contexts-interactions.md)

---

**Last Updated**: 2026-04-05
**Total ADRs**: 24 (22 Accepted/Implemented, 1 Rejected, 1 Deprecated)
**Archived**: 12 obsolete ADRs removed (see git history)
