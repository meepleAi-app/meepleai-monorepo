# Architecture Decision Records (ADR)

This directory contains Architecture Decision Records (ADRs) for MeepleAI. Each ADR captures a significant architectural decision, its context, consequences, and alternatives considered.

## What is an ADR?

An Architecture Decision Record (ADR) documents an important architectural decision, explaining:
- **Why** the decision was made (context and problem)
- **What** decision was chosen (solution)
- **How** it will be implemented (technical details)
- **What else** was considered (alternatives)
- **What happens** as a result (consequences)

## ADR Index

### Core Architecture (001-009)

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [001](adr-001-hybrid-rag.md) | Hybrid RAG Architecture with Multi-Model Validation | Accepted | 2025-01-15 | Critical |
| [002](adr-002-multilingual-embedding.md) | Multilingual Embedding Strategy | Accepted | 2025-01-15 | High |
| [003](adr-003-pdf-processing.md) | PDF Processing Pipeline | Accepted | 2025-01-15 | High |
| [003b](adr-003b-unstructured-pdf.md) | Unstructured PDF Extraction | Accepted | 2025-01-16 | High |
| [004](adr-004-ai-agents.md) | AI Agents Architecture | Accepted | 2025-01-15 | Medium |
| [004b](adr-004b-hybrid-llm.md) | Hybrid LLM Architecture | Accepted | 2025-01-15 | High |
| [005](adr-005-cosine-similarity-consensus.md) | Cosine Similarity Consensus | Accepted | 2025-01-15 | Medium |
| [006](adr-006-multi-layer-validation.md) | Multi-Layer Validation Framework | Accepted | 2025-01-16 | Critical |
| [007](adr-007-hybrid-llm.md) | Hybrid LLM Strategy | Accepted | 2025-01-17 | High |
| [008](adr-008-streaming-cqrs-migration.md) | Streaming CQRS Migration | Accepted | 2025-01-17 | High |
| [009](adr-009-centralized-error-handling.md) | Centralized Error Handling | Accepted | 2025-01-18 | Medium |

### Security & Validation (010-019)

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [010](adr-010-security-headers-middleware.md) | Security Headers Middleware | Accepted | 2025-01-19 | Critical |
| [011](adr-011-cors-whitelist-headers.md) | CORS Header Whitelist Strategy | Accepted | 2025-01-19 | Critical |
| [012](adr-012-fluentvalidation-cqrs.md) | FluentValidation Integration with CQRS | Accepted | 2025-01-19 | High |
| [013](adr-013-nswag-typescript-generation.md) | NSwag TypeScript Client Generation | Accepted | 2025-01-19 | High |
| [014](adr-014-nswag-msbuild-removal.md) | NSwag MSBuild Removal | Accepted | 2025-01-20 | Medium |
| [018](adr-018-postgresql-fts-for-shared-catalog.md) | PostgreSQL FTS for Shared Catalog | Accepted | 2025-12-15 | High |
| [019](adr-019-shared-catalog-delete-workflow.md) | Shared Catalog Delete Workflow | Accepted | 2025-12-15 | Medium |

### Frontend/Backend Integration (020-029)

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [020](adr-020-valueobject-record-evaluation.md) | ValueObject Record Syntax Evaluation | Rejected | 2026-01-14 | Low |
| [021](adr-021-auto-configuration-system.md) | Auto-Configuration System for Secret Management | Implemented | 2026-01-17 | High |
| [022](adr-022-ssr-auth-protection.md) | Server-Side Rendering (SSR) Authentication Protection | Accepted | 2025-11-22 | High |
| [023](adr-023-share-request-workflow.md) | Share Request Workflow Design | Accepted | 2026-01-20 | High |
| [024](adr-024-advanced-pdf-embedding-pipeline.md) | Advanced PDF Embedding Pipeline with Hybrid Indexing | Accepted | 2025-12-03 | High |
| [025](adr-025-shared-catalog-bounded-context.md) | SharedGameCatalog Bounded Context Separation | Accepted | 2026-01-14 | High |
| [026](adr-026-document-collections.md) | Document Collections for Multi-Document Support | Accepted | 2025-12-12 | Medium |
| [027](adr-027-infrastructure-services-policy.md) | Infrastructure Services Policy | Accepted | 2025-12-07 | Medium |

### Observability & Monitoring (040-049)

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [040](adr-040-hyperdx-observability.md) | HyperDX for Unified Observability | Accepted | 2025-12-06 | High |

## ADR Lifecycle

### Status Levels

- **Proposed**: Under review, not yet implemented
- **Accepted**: Approved and being implemented
- **Implemented**: Fully deployed and operational
- **Deprecated**: Superseded by a newer ADR
- **Rejected**: Considered but not adopted

### Review Process

1. **Draft**: Author creates ADR with context and proposed solution
2. **Review**: Team reviews and provides feedback
3. **Decision**: Engineering Lead approves or rejects
4. **Implementation**: Issue created and tracked
5. **Retrospective**: Update ADR with lessons learned

## Numbering Convention

- **001-009**: Core architecture (RAG, PDF, AI agents, validation, LLM)
- **010-019**: Security & input validation
- **020-029**: Frontend/backend integration, DDD patterns
- **030-039**: Performance optimization (reserved)
- **040-049**: Observability & monitoring
- **050-059**: Infrastructure & deployment (reserved)

## Creating a New ADR

Use the following template:

```markdown
# ADR-XXX: [Title]

**Status**: [Proposed|Accepted|Deprecated|Rejected]
**Date**: YYYY-MM-DD
**Deciders**: [List of key decision makers]
**Context**: [Brief context/tags]

---

## Context

[Describe the problem and why this decision is needed]

## Decision

[Describe the chosen solution]

## Consequences

### Positive
[Benefits of this decision]

### Negative
[Drawbacks or risks]

## Alternatives Considered

[Other options that were evaluated]

## References

[Links to related documentation]
```

## Related Documentation

- [System Architecture](../overview/system-architecture.md)
- [DDD Quick Reference](../ddd/quick-reference.md)
- [Bounded Contexts Diagram](../diagrams/bounded-contexts-interactions.md)

## Contributing

When creating a new ADR:

1. Copy the template above
2. Use next available number in appropriate range (see Numbering Convention)
3. Write clear context and decision
4. List alternatives considered
5. Submit PR for team review
6. Update this index table

---

**Last Updated**: 2026-02-03
**Total ADRs**: 24 (22 Accepted/Implemented, 1 Rejected, 1 Deprecated)
