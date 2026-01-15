# MeepleAI Documentation Index

**Living Documentation System** - Docs auto-generate da codice + ADR manuali

---

## Quick Access

| Resource | URL |
|----------|-----|
| **API Docs (Scalar UI)** | http://localhost:8080/scalar/v1 |
| **OpenAPI Spec** | http://localhost:8080/openapi/v1.json |
| **Living Docs Guide** | [living-documentation.md](living-documentation.md) |
| **README** | [../README.md](../README.md) |
| **CLAUDE.md** | [../CLAUDE.md](../CLAUDE.md) |

---

## Documentation Structure

### 01. Architecture (Manual)

**ADR - Architecture Decision Records** (`01-architecture/adr/` - 22 files)
- [ADR-001](01-architecture/adr/adr-001-hybrid-rag.md): Hybrid RAG (Vector + Keyword)
- [ADR-003b](01-architecture/adr/adr-003b-unstructured-pdf.md): PDF Processing Pipeline
- [ADR-004b](01-architecture/adr/adr-004b-hybrid-llm.md): Hybrid LLM Strategy
- [ADR-006](01-architecture/adr/adr-006-multi-layer-validation.md): 5-Layer Validation System
- [ADR-008](01-architecture/adr/adr-008-streaming-cqrs-migration.md): SSE Streaming + CQRS
- [ADR-016](01-architecture/adr/adr-016-ddd-bounded-contexts.md): DDD Bounded Contexts
- [All ADRs →](01-architecture/adr/)

**System Overview** (`01-architecture/overview/`)
- [System Architecture](01-architecture/overview/system-architecture.md)
- [Architecture Diagrams](01-architecture/diagrams/)

**Domain-Driven Design** (`01-architecture/ddd/`)
- Domain patterns and bounded context definitions

### 02. Development Guide ⭐ NEW

**[Development Guide](02-development/README.md)** - Complete developer documentation

**Topics Covered**:
- Quick Start & Local Setup
- DDD Bounded Contexts Overview
- Development Workflow (New Features, Migrations)
- Code Standards (Backend C# & Frontend TypeScript)
- Testing Strategy
- Debugging & Performance Optimization
- Common Tasks & Troubleshooting

### 03. API Documentation ⭐ NEW

**[API Reference](03-api/README.md)** - Complete REST API documentation

**Topics Covered**:
- Authentication (Cookie, API Key, OAuth)
- All API Endpoints Reference
- Request/Response Examples
- Error Handling & Status Codes
- Rate Limiting & Pagination
- Webhooks & Data Models
- Client Libraries & Examples

### 04. Deployment Guide ⭐ NEW

**[Deployment Guide](04-deployment/README.md)** - Production deployment documentation

**Topics Covered**:
- Local Development Setup
- Production Deployment (Docker + Traefik)
- Infrastructure Configuration
- Database Management & Backups
- Monitoring & Observability (Grafana, Prometheus)
- Scaling & Performance
- Security Hardening
- CI/CD Pipeline
- Disaster Recovery

### 05. Testing Documentation ⭐ NEW

**[Testing Guide](05-testing/README.md)** - Comprehensive testing strategy

**Topics Covered**:
- Testing Philosophy & Pyramid
- Backend Testing (xUnit + Testcontainers)
- Frontend Testing (Vitest + Testing Library)
- E2E Testing (Playwright)
- Test Coverage (90%+ targets)
- Best Practices & CI/CD Integration
- Performance Testing (k6)
- Accessibility Testing

---

## Living Documentation (Auto-Generated)

### Backend - Bounded Contexts

Each context has auto-generated README: `apps/api/src/Api/BoundedContexts/{Context}/README.md`

**Available Contexts**:
- [KnowledgeBase](../apps/api/src/Api/BoundedContexts/KnowledgeBase/README.md): RAG pipeline, hybrid search, chat
- [Authentication](../apps/api/src/Api/BoundedContexts/Authentication/README.md): OAuth, 2FA, API keys
- [DocumentProcessing](../apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md): PDF extraction
- [GameManagement](../apps/api/src/Api/BoundedContexts/GameManagement/README.md): Games catalog
- [SystemConfiguration](../apps/api/src/Api/BoundedContexts/SystemConfiguration/README.md): Dynamic config
- [Administration](../apps/api/src/Api/BoundedContexts/Administration/README.md): Users, alerts, audit
- [WorkflowIntegration](../apps/api/src/Api/BoundedContexts/WorkflowIntegration/README.md): n8n workflows

**Template**: [README-TEMPLATE.md](../apps/api/src/Api/BoundedContexts/README-TEMPLATE.md)

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
/// <exception cref="ValidationException">Invalid question format</exception>
public async Task<AnswerDto> HandleAsync(AskQuestionCommand request)
{
    // Implementation
}
```

### Frontend (TypeScript) - JSDoc Comments

**Required for all exported functions/components**:
```typescript
/**
 * Executes RAG query with streaming response
 * @param question - User question about game rules
 * @param gameId - Game identifier
 * @returns Promise with streaming answer and sources
 * @throws {ApiError} Network or validation errors
 */
export async function askQuestion(
  question: string,
  gameId: string
): Promise<AnswerDto> {
  // Implementation
}
```

---

## Documentation Maintenance

### When to Update

**Architecture Docs**:
- Create ADR for significant architectural decisions
- Update system diagrams when components change
- Document new bounded contexts with README

**Development Docs**:
- Update when new workflows are established
- Add troubleshooting entries for recurring issues
- Document new development tools or practices

**API Docs**:
- Auto-generated on build (no manual updates)
- Add examples for complex endpoints
- Document breaking changes immediately

**Deployment Docs**:
- Update when infrastructure changes
- Document new deployment procedures
- Add troubleshooting for production issues

**Testing Docs**:
- Update when testing strategy changes
- Add examples for new testing patterns
- Document coverage targets and CI requirements

### Living Documentation Principles

1. **Code is Truth**: Code auto-generates documentation via XML/JSDoc
2. **Manual for Context**: ADRs, system overviews, workflows require manual docs
3. **Keep Minimal**: Remove outdated docs, maintain high signal-to-noise ratio
4. **Single Source**: Avoid duplication, link to canonical sources
5. **Version Control**: All docs in Git, reviewed in PRs

---

## Migration History

### v1.2 (2026-01-15) - Documentation Consolidation

**Removed** (81% reduction: 79 → 1 claudedocs files):
- ❌ `/claudedocs/` (root) → Entire directory removed
- ❌ Issue completion reports (29 files)
- ❌ Week implementation summaries (14 files)
- ❌ Cleanup & migration reports (9 files)
- ❌ Research & planning docs (4 files)
- ❌ Test reports & error analysis (6 files)
- ❌ Miscellaneous completions (2 files)

**Reorganized** (16 files moved to standard structure):
- ✅ Security audits → `docs/06-security/`
- ✅ Configuration guides → `docs/04-deployment/secrets/`
- ✅ Monitoring reports → `docs/04-deployment/monitoring/`
- ✅ Development guides → `docs/01-architecture/ddd/`, `docs/02-development/troubleshooting/`
- ✅ Testing & validation → `docs/05-testing/`, `docs/03-api/`, `docs/04-deployment/validation/`
- ✅ Architecture specs → `docs/01-architecture/components/`

**Backup**: `docs-backup-consolidation-2026-01-15/`

**Purpose**: Remove historical implementation reports, focus on operational documentation

### v1.1 (2026-01-01)

**Added**:
- ✅ `docs/02-development/`: Development guide (new)
- ✅ `docs/03-api/`: API documentation (new)
- ✅ `docs/04-deployment/`: Deployment guide (new)
- ✅ `docs/05-testing/`: Testing documentation (new)

**Purpose**: Restore practical documentation that complements living docs for onboarding and operations

### v1.0 (2024-12-31)

**Removed** (84% reduction: 301 → 47 files):
- ❌ Previous `docs/02-development/` → Replaced by living docs
- ❌ Previous `docs/03-api/` → Replaced by OpenAPI
- ❌ `docs/04-*`, `docs/05-*`, `docs/06-*`, `docs/07-*`, `docs/08-*`, `docs/10-*`

**Backup**: `docs-backup-20241231/`

---

## Quick Navigation

### For Developers
1. **Getting Started**: [Development Guide](02-development/README.md)
2. **API Reference**: [API Documentation](03-api/README.md)
3. **Testing**: [Testing Guide](05-testing/README.md)
4. **Architecture**: [ADRs](01-architecture/adr/)

### For DevOps
1. **Deployment**: [Deployment Guide](04-deployment/README.md)
2. **Monitoring**: [Deployment Guide - Monitoring](04-deployment/README.md#monitoring--observability)
3. **Backups**: [Deployment Guide - Database](04-deployment/README.md#database-management)

### For Product/QA
1. **API Testing**: [API Documentation](03-api/README.md)
2. **E2E Testing**: [Testing Guide - E2E](05-testing/README.md#e2e-testing-playwright)
3. **System Overview**: [Architecture](01-architecture/overview/system-architecture.md)

---

**Version**: 1.2
**Last Updated**: 2026-01-15
**Maintainers**: Engineering Team
