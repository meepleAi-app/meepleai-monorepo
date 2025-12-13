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

### Core Architecture

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [001](adr-001-hybrid-rag.md) | Hybrid RAG Architecture with Multi-Model Validation | Accepted | 2025-01-15 | Critical |
| [002](adr-002-multilingual-embedding.md) | Multilingual Embedding Strategy | Accepted | 2025-01-15 | High |
| [003](adr-003-pdf-processing.md) | PDF Processing Pipeline | Accepted | 2025-01-15 | High |
| [003b](adr-003b-unstructured-pdf.md) | Unstructured PDF Extraction | Accepted | 2025-01-16 | High |
| [004](adr-004-ai-agents.md) | AI Agents Architecture | Accepted | 2025-01-15 | Medium |

### Quality & Validation

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [005](adr-005-cosine-similarity-consensus.md) | Cosine Similarity Consensus | Accepted | 2025-01-15 | Medium |
| [006](adr-006-multi-layer-validation.md) | Multi-Layer Validation Framework | Accepted | 2025-01-16 | Critical |

### CQRS & Event-Driven

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [007](adr-007-hybrid-llm.md) | Hybrid LLM Strategy | Accepted | 2025-01-17 | High |
| [008](adr-008-streaming-cqrs-migration.md) | Streaming CQRS Migration | Accepted | 2025-01-17 | High |
| [009](adr-009-centralized-error-handling.md) | Centralized Error Handling | Accepted | 2025-01-18 | Medium |

### Security & Validation (Code Review 2025-01-19)

| ADR | Title | Status | Date | Priority |
|-----|-------|--------|------|----------|
| [010](adr-010-security-headers-middleware.md) | Security Headers Middleware | Proposed | 2025-01-19 | Critical |
| [011](adr-011-cors-whitelist-headers.md) | CORS Header Whitelist Strategy | Proposed | 2025-01-19 | Critical |
| [012](adr-012-fluentvalidation-cqrs.md) | FluentValidation Integration with CQRS | Proposed | 2025-01-19 | High |
| [013](adr-013-nswag-typescript-generation.md) | NSwag TypeScript Client Generation | Proposed | 2025-01-19 | High |

## ADR Lifecycle

### Status Levels

- **Proposed**: Under review, not yet implemented
- **Accepted**: Approved and being implemented
- **Deprecated**: Superseded by a newer ADR
- **Rejected**: Considered but not adopted

### Review Process

1. **Draft**: Author creates ADR with context and proposed solution
2. **Review**: Team reviews and provides feedback
3. **Decision**: Engineering Lead approves or rejects
4. **Implementation**: Issue created and tracked
5. **Retrospective**: Update ADR with lessons learned

## Recent Additions (2025-01-19)

Four new ADRs were created based on the code review of backend-frontend interactions:

### ADR-010: Security Headers Middleware
**Problem**: No HTTP security headers (CSP, HSTS, X-Frame-Options, etc.)
**Solution**: Implement `SecurityHeadersMiddleware` with 7 security headers
**Impact**: Critical security hardening, protects against XSS, clickjacking, MIME sniffing

### ADR-011: CORS Whitelist Headers
**Problem**: `AllowAnyHeader()` in CORS policy creates attack vector
**Solution**: Explicit whitelist of 4 allowed headers
**Impact**: Reduced attack surface, compliance with OWASP guidelines

### ADR-012: FluentValidation CQRS Integration
**Problem**: Inconsistent validation, manual validation code scattered
**Solution**: MediatR pipeline behavior with FluentValidation
**Impact**: Consistent validation, better error messages, reduced code duplication

### ADR-013: NSwag TypeScript Generation
**Problem**: Manual TypeScript types can drift from C# DTOs
**Solution**: Auto-generate TypeScript + Zod from OpenAPI spec
**Impact**: Eliminates type drift, zero manual maintenance

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

## Numbering Convention

- **001-009**: Core architecture (RAG, PDF, AI agents, validation)
- **010-019**: Security & input validation
- **020-029**: Frontend/backend integration
- **030-039**: Performance optimization
- **040-049**: Observability & monitoring
- **050-059**: Infrastructure & deployment

## Related Documentation

- [System Architecture](../overview/system-architecture.md)
- [DDD Bounded Contexts](../../02-development/ddd-bounded-contexts.md)
- [CQRS Pattern](../../02-development/cqrs-pattern.md)
- [Security Guidelines](../../06-security/)

## Contributing

When creating a new ADR:

1. Copy the template above
2. Number sequentially (next available number)
3. Write clear context and decision
4. List alternatives considered
5. Submit PR for team review
6. Update this index table

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Total ADRs**: 13 (9 Accepted, 4 Proposed)

