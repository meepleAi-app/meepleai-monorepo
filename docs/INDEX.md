# MeepleAI Documentation Index

**Living Documentation System** - Docs auto-generate da codice + ADR manuali

---

## Quick Access

| Resource | URL |
|----------|-----|
| **API Docs (Scalar UI)** | http://localhost:8080/scalar/v1 |
| **OpenAPI Spec** | http://localhost:8080/openapi/v1.json |
| **README** | [../README.md](../README.md) |
| **CLAUDE.md** | [../CLAUDE.md](../CLAUDE.md) |

---

## Documentation Structure

### 01. Architecture (Manual)

**ADR - Architecture Decision Records** (`01-architecture/adr/`)
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

### 02. Development Guide

**[Development Guide](02-development/README.md)** - Complete developer documentation

**Topics Covered**:
- Quick Start & Local Setup
- DDD Bounded Contexts Overview
- Development Workflow (New Features, Migrations)
- Code Standards (Backend C# & Frontend TypeScript)
- Testing Strategy
- Debugging & Performance Optimization
- Common Tasks & Troubleshooting

**Essential Guides**:
- **[Visual Studio Code Setup](02-development/guida-visualcode.md)** - Task automation, Docker workflow, troubleshooting
- [Git Workflow](02-development/git-workflow.md)
- [Local Secrets Setup](02-development/local-secrets-setup.md)
- [Operational Guide](02-development/operational-guide.md)

### 03. API Documentation

**[API Reference](03-api/README.md)** - Complete REST API documentation

**Topics Covered**:
- Authentication (Cookie, API Key, OAuth)
- All API Endpoints Reference
- Request/Response Examples
- Error Handling & Status Codes
- Rate Limiting & Pagination
- Webhooks & Data Models
- Client Libraries & Examples

### 04. Deployment Guide

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

**Cost Optimization**:
- **[GitHub Alternatives & Cost Optimization](04-deployment/github-alternatives-cost-optimization.md)**
  - GitHub Free tier limits analysis (2026 pricing)
  - Zero-cost alternatives: self-hosted runners, GitLab, Forgejo
  - Oracle Always Free setup guide
  - Decision matrix and implementation roadmap

### 05. Testing Documentation

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

### 06. Security Documentation

**[Security Guide](06-security/README.md)** - Security practices and guidelines

### 07. Frontend Documentation

**[Frontend Guide](07-frontend/README.md)** - Frontend architecture and guides

**Topics Covered**:
- Next.js App Router patterns
- React component architecture
- State management (Zustand, React Query)
- Tailwind + shadcn/ui design system
- Performance optimization

### 08. Infrastructure Documentation

**[Infrastructure Guide](08-infrastructure/README.md)** - Infrastructure configuration

**Topics Covered**:
- Docker Compose setup
- Traefik reverse proxy
- Database configuration (PostgreSQL, Redis, Qdrant)
- Service orchestration

### 09. Bounded Contexts

**[Bounded Contexts Guide](09-bounded-contexts/README.md)** - DDD Bounded Contexts documentation

**Available Contexts**:
- [KnowledgeBase](09-bounded-contexts/knowledge-base.md): RAG pipeline, hybrid search, chat
- [Authentication](09-bounded-contexts/authentication.md): OAuth, 2FA, API keys
- [DocumentProcessing](09-bounded-contexts/document-processing.md): PDF extraction
- [GameManagement](09-bounded-contexts/game-management.md): Games catalog
- [SystemConfiguration](09-bounded-contexts/system-configuration.md): Dynamic config
- [Administration](09-bounded-contexts/administration.md): Users, alerts, audit

### 10. User Guides

**[User Guides](10-user-guides/README.md)** - Admin and user documentation

**Admin Guides**:
- **[Shared Games Catalog Management](10-user-guides/admin-shared-games-management.md)** - BGG import, PDF workflows, approval queue
- [Share Requests Admin Guide](10-user-guides/share-requests-admin-guide.md) - User share request management

**User Guides**:
- [Share Game Guide](10-user-guides/share-game-guide.md) - Contributing games to the catalog

**Topics Covered**:
- Administrator guides
- End-user documentation
- Feature walkthroughs

### 11. User Flows

**[User Flows Documentation](11-user-flows/README.md)** - Complete user flow documentation by role

**Topics Covered**:
- User Stories (Gherkin format)
- Sequence Diagrams (Mermaid)
- Screen Flows (ASCII diagrams)
- API Flows (endpoint chains)
- Implementation Status
- Gap Analysis

**User Role** (`11-user-flows/user-role/`):
- [Authentication](11-user-flows/user-role/01-authentication.md): Registration, Login, OAuth, 2FA, API Keys
- [Game Discovery](11-user-flows/user-role/02-game-discovery.md): Browse, Search, Filter, Game Details
- [Library Management](11-user-flows/user-role/03-library-management.md): Tier-based quotas (A games, B/C PDFs, D sessions)
- [AI Chat](11-user-flows/user-role/04-ai-chat.md): RAG questions, Threads, History, Export
- [Game Sessions](11-user-flows/user-role/05-game-sessions.md): Create, Players, State Tracking, Player Mode

**Editor Role** (`11-user-flows/editor-role/`):
- [Game Management](11-user-flows/editor-role/01-game-management.md): Create, Edit, BGG Import, Archive
- [Document Management](11-user-flows/editor-role/02-document-management.md): PDF Upload, Versions, Processing
- [Content Management](11-user-flows/editor-role/03-content-management.md): Quick Questions, FAQ, Errata
- [Publication Workflow](11-user-flows/editor-role/04-publication-workflow.md): Submit, Track, Handle Rejection

**Admin Role** (`11-user-flows/admin-role/`):
- [Approval Workflow](11-user-flows/admin-role/01-approval-workflow.md): Queue, Approve/Reject, Delete
- [User Management](11-user-flows/admin-role/02-user-management.md): CRUD, Roles, Tiers, Bulk Ops
- [System Configuration](11-user-flows/admin-role/03-system-configuration.md): Quotas, Feature Flags, AI Models
- [Monitoring](11-user-flows/admin-role/04-monitoring.md): Health, Services, Alerts, Analytics

**Supporting Documents**:
- [Core Flow Diagrams](11-user-flows/diagrams/core-flows.md): Mermaid sequence diagrams
- [Gap Analysis](11-user-flows/gap-analysis.md): Implementation status & proposed features

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

## Quick Navigation

### For Developers
1. **Getting Started**: [Development Guide](02-development/README.md)
2. **API Reference**: [API Documentation](03-api/README.md)
3. **Testing**: [Testing Guide](05-testing/README.md)
4. **Architecture**: [ADRs](01-architecture/adr/)

### For DevOps
1. **Deployment**: [Deployment Guide](04-deployment/README.md)
2. **Infrastructure**: [Infrastructure Guide](08-infrastructure/README.md)
3. **Monitoring**: [Deployment Guide - Monitoring](04-deployment/README.md#monitoring--observability)
4. **Backups**: [Deployment Guide - Database](04-deployment/README.md#database-management)

### For Product/QA
1. **User Flows**: [User Flows Documentation](11-user-flows/README.md)
2. **Quality Validation**: [Quality Documentation](quality/README.md)
3. **API Testing**: [API Documentation](03-api/README.md)
4. **E2E Testing**: [Testing Guide - E2E](05-testing/README.md#e2e-testing-playwright)
5. **Project Roadmap**: [Roadmap & Epics](roadmap/)
6. **System Overview**: [Architecture](01-architecture/overview/system-architecture.md)

---

**Version**: 1.5
**Last Updated**: 2026-02-04
**Maintainers**: Engineering Team
