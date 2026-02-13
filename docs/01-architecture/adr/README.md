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
