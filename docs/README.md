# MeepleAI Documentation

**AI-powered board game assistant** - Complete documentation for developers, DevOps, and architects

---

## Quick Start

**New to MeepleAI?** Start here:

1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide (setup, standards, workflows)
2. **[Architecture Overview](./architecture/README.md)** - System design and ADRs
3. **[Development Guide](./development/README.md)** - Local setup and development workflow
4. **[Bounded Contexts](./bounded-contexts/README.md)** - DDD context guides

| Resource | URL |
|----------|-----|
| **API Docs (Scalar UI)** | http://localhost:8080/scalar/v1 |
| **OpenAPI Spec** | http://localhost:8080/openapi/v1.json |

---

## Documentation Structure

```
docs/
├── architecture/             # ADRs, DDD, diagrams, system overview
├── development/              # Developer guides, git workflow, secrets
├── api/                      # API reference, RAG system docs
├── deployment/               # Deployment, infrastructure, secrets, monitoring
├── testing/                  # Testing strategy (backend, frontend, e2e, perf)
├── security/                 # Security documentation, OWASP, vulnerabilities
├── frontend/                 # Frontend architecture, components, dashboard
├── bounded-contexts/         # DDD Bounded Contexts guides
├── user-guides/              # Admin guides, user flows, gap analysis
├── migrations/               # Migration guides (dashboard v1→v2)
├── archive/                  # Historical docs (completed epics, sessions)
├── pdca/                     # Active PDCA cycles
├── roadmap/                  # Project roadmap & execution plans
├── evaluation-reports/       # Quality baselines & metrics
└── templates/                # Documentation templates
```

---

## Find What You Need

### By Role

**Developer**
- [Development Guide](./development/README.md) - Local setup, coding standards
- [Quick Start Guide](./development/quick-start-guide.md) - Fast onboarding
- [Bounded Contexts](./bounded-contexts/README.md) - DDD context guides
- [Testing Guide](./testing/README.md) - Testing strategy
- [API Reference](./api/README.md) - REST API documentation
- [Frontend Guide](./frontend/README.md) - Frontend architecture
- [Git Workflow](./development/git-workflow.md) - Branching & commit strategy

**Architect**
- [Architecture](./architecture/README.md) - System design
- [ADRs](./architecture/adr/README.md) - Architecture decisions
- [DDD Guide](./architecture/ddd/quick-reference.md) - DDD patterns
- [Diagrams](./architecture/diagrams/README.md) - Mermaid architecture diagrams

**DevOps**
- [Deployment Guide](./deployment/README.md) - Infrastructure deployment
- [Operations Manual](./operations/operations-manual.md) - Service management, backup, monitoring, incident response
- [Deployment Workflows](./deployment/deployment-workflows-guide.md) - CI/CD pipeline

**QA/Tester**
- [Testing Guide](./testing/README.md) - Testing philosophy and pyramid
- [Backend Testing](./testing/backend/) - xUnit, Testcontainers
- [Frontend Testing](./testing/frontend/) - Vitest, Testing Library
- [E2E Testing](./testing/e2e/) - Playwright guides

**Security**
- [Security Guide](./security/README.md) - Security documentation
- [OWASP Compliance](./security/owasp-top-10-compliance.md) - OWASP Top 10
- [Operations Manual](./operations/operations-manual.md) - Secrets, incident response, DR
- [OAuth Testing](./testing/backend/oauth-testing.md) - OAuth security

---

### By Topic

**RAG System**
- [RAG Overview](./api/rag/README.md) - RAG architecture and documentation
- [KnowledgeBase Context](./bounded-contexts/knowledge-base.md) - Bounded context
- [RAG Diagram](./architecture/diagrams/rag-system-detailed.md) - Architecture diagram

**PDF Processing**
- [ADR-003b: Unstructured PDF](./architecture/adr/adr-003b-unstructured-pdf.md) - PDF processing pipeline
- [DocumentProcessing Context](./bounded-contexts/document-processing.md) - Bounded context
- [PDF Pipeline Diagram](./architecture/diagrams/pdf-pipeline-detailed.md) - Architecture diagram

**Authentication**
- [Authentication Context](./bounded-contexts/authentication.md) - Bounded context
- [OAuth Testing](./testing/backend/oauth-testing.md) - OAuth security
- [Secrets Management](./deployment/secrets-management.md) - Secret management

**Deployment**
- [Infrastructure Checklist](./deployment/infrastructure-deployment-checklist.md) - Step-by-step
- [Domain Setup](./deployment/domain-setup-guide.md) - DNS & domain config
- [Cost Summary](./deployment/infrastructure-cost-summary.md) - Budget planning

**Testing**
- [Testing Philosophy](./testing/README.md) - Strategy and pyramid
- [Testcontainers Best Practices](./testing/backend/testcontainers-best-practices.md) - Integration testing
- [E2E Guide](./testing/e2e/e2-e-test-guide.md) - Playwright E2E

---

## Key Documents

### Must Read for All Developers
1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide
2. **[Architecture README](./architecture/README.md)** - System architecture
3. **[Development README](./development/README.md)** - Development workflow
4. **[Bounded Contexts Overview](./bounded-contexts/README.md)** - DDD contexts

### Essential ADRs
1. **[ADR-003b: Unstructured PDF](./architecture/adr/adr-003b-unstructured-pdf.md)** - PDF processing
2. **[ADR-006: Multi-Layer Validation](./architecture/adr/adr-006-multi-layer-validation.md)** - 5-layer validation
3. **[ADR-007: Hybrid LLM](./architecture/adr/adr-007-hybrid-llm.md)** - Multi-model consensus
4. **[ADR-009: Error Handling](./architecture/adr/adr-009-centralized-error-handling.md)** - Centralized errors
5. **[ADR-012: FluentValidation CQRS](./architecture/adr/adr-012-fluentvalidation-cqrs.md)** - Validation pattern

### Production Deployment
1. **[Deployment Guide](./deployment/README.md)** - Complete deployment index
2. **[Infrastructure Checklist](./deployment/infrastructure-deployment-checklist.md)** - Step-by-step
3. **[Cost Summary](./deployment/infrastructure-cost-summary.md)** - Budget planning
4. **[Monitoring Quickstart](./deployment/monitoring-quickstart.md)** - Observability

---

## Documentation by Phase

### Alpha (Local Development)
- [CLAUDE.md](../CLAUDE.md) - Complete setup guide
- [Development Guide](./development/README.md) - Local dev workflow
- [Testing Guide](./testing/README.md) - Testing strategy

### Beta (Staging Deployment)
- [Infrastructure Checklist](./deployment/infrastructure-deployment-checklist.md) - Deployment steps
- [Domain Setup](./deployment/domain-setup-guide.md) - DNS configuration
- [Monitoring Quickstart](./deployment/monitoring-quickstart.md) - Observability setup

### Release (Production)
- [Cost Summary](./deployment/infrastructure-cost-summary.md) - Budget planning
- [Email & TOTP Services](./deployment/email-totp-services.md) - External services
- [Security Guide](./security/README.md) - Security hardening

---

## Living Documentation (Auto-Generated)

### Backend - Bounded Contexts

Each context has auto-generated README: `apps/api/src/Api/BoundedContexts/{Context}/README.md`

**Template**: [bounded-context-template.md](./templates/bounded-context-template.md)

### API Documentation (Auto-Generated)

- **XML Docs**: `apps/api/src/Api/bin/Debug/net9.0/Api.xml` (generated on build)
- **OpenAPI Spec**: http://localhost:8080/openapi/v1.json
- **Interactive UI**: http://localhost:8080/scalar/v1 (Scalar API Explorer)

### Frontend Documentation

- **JSDoc Comments**: Block comments `/** */` on all exported functions/components
- **TypeDoc Generation**: `npx typedoc --out docs-generated src/lib`

---

## Documentation Guidelines

### Backend (C#) - XML Comments

**Required for all public APIs**:
```csharp
/// <summary>
/// Executes RAG query with hybrid retrieval (vector + keyword)
/// </summary>
/// <param name="request">Query request with question and game context</param>
/// <returns>Answer DTO with confidence score and sources</returns>
public async Task<AnswerDto> HandleAsync(AskQuestionCommand request)
```

### Frontend (TypeScript) - JSDoc Comments

**Required for all exported functions/components**:
```typescript
/**
 * Executes RAG query with streaming response
 * @param question - User question about game rules
 * @param gameId - Game identifier
 * @returns Promise with streaming answer and sources
 */
export async function askQuestion(question: string, gameId: string): Promise<AnswerDto>
```

### Living Documentation Principles

1. **Code is Truth**: Code auto-generates documentation via XML/JSDoc
2. **Manual for Context**: ADRs, system overviews, workflows require manual docs
3. **Keep Minimal**: Remove outdated docs, maintain high signal-to-noise ratio
4. **Single Source**: Avoid duplication, link to canonical sources
5. **Version Control**: All docs in Git, reviewed in PRs

---

## Contributing

### Documentation Standards

**File Naming**: `kebab-case.md` (e.g., `authentication-guide.md`, `oauth-testing.md`)

**Structure**:
- Use headers (##, ###) for hierarchy
- Include table of contents for docs >50 lines
- Add metadata footer (version, date, maintainer)
- Use code fences with language hints

**Cross-References**:
- Use relative paths from current file
- Link to specific sections with anchors
- Verify links work before committing

---

## External Resources

- **API Explorer**: http://localhost:8080/scalar/v1 (interactive API docs)
- **Grafana**: http://localhost:3001 (monitoring dashboards)

---

## Maintenance

### Regular Updates

**Weekly**: Update test coverage stats in [Testing Guide](./testing/README.md)

**Monthly**: Review ADRs for outdated decisions

**Quarterly**: Full documentation audit, update architecture diagrams

---

**Last Updated**: 2026-02-18
**Maintainer**: Documentation Team
