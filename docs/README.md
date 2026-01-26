# MeepleAI Documentation

**AI-powered board game assistant** - Complete documentation for developers, DevOps, and architects

---

## Quick Start

**New to MeepleAI?** Start here:

1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide (setup, standards, workflows)
2. **[Architecture Overview](./01-architecture/README.md)** - System design and ADRs
3. **[Development Guide](./02-development/README.md)** - Local setup and development workflow

---

## Documentation Structure

```
docs/
├── 01-architecture/          # Architecture, ADRs, DDD, diagrams
├── 02-development/           # Developer guides, troubleshooting
├── 03-api/                   # API reference and integration
├── 04-deployment/            # Deployment, infrastructure, secrets
├── 05-testing/               # Testing strategy and guides
│   ├── backend/              # Backend testing (xUnit, Testcontainers)
│   ├── frontend/             # Frontend testing (Vitest)
│   └── e2e/                  # E2E testing (Playwright)
├── 06-security/              # Security documentation
├── 07-frontend/              # Frontend architecture and guides
├── 08-infrastructure/        # Infrastructure configuration
├── 09-bounded-contexts/      # DDD Bounded Contexts guides
├── 10-user-guides/           # Admin and user guides
├── 11-user-flows/            # User flow documentation
└── roadmap/                  # Project roadmap
```

---

## Find What You Need

### By Role

**Developer**
- [Development Guide](./02-development/README.md) - Local setup, coding standards
- [Bounded Contexts](./09-bounded-contexts/README.md) - DDD context guides
- [Testing Guide](./05-testing/README.md) - Testing strategy
- [API Reference](./03-api/README.md) - REST API documentation
- [Frontend Guide](./07-frontend/README.md) - Frontend architecture

**Architect**
- [Architecture](./01-architecture/README.md) - System design
- [ADRs](./01-architecture/adr/README.md) - Architecture decisions
- [DDD Guide](./01-architecture/ddd/quick-reference.md) - DDD patterns
- [Diagrams](./01-architecture/diagrams/) - Mermaid architecture diagrams

**DevOps**
- [Deployment Guide](./04-deployment/README.md) - Infrastructure deployment
- [Infrastructure](./08-infrastructure/README.md) - Infrastructure configuration
- [Monitoring Setup](./04-deployment/monitoring-setup-guide.md) - Grafana + Prometheus
- [Secrets Management](./04-deployment/secrets-management.md) - Secret system

**QA/Tester**
- [Testing Guide](./05-testing/README.md) - Testing philosophy and pyramid
- [Backend Testing](./05-testing/backend/) - xUnit, Testcontainers
- [Frontend Testing](./05-testing/frontend/) - Vitest, Testing Library
- [E2E Testing](./05-testing/e2e/) - Playwright guides

**Security**
- [Security Guide](./06-security/README.md) - Security documentation
- [Secrets Management](./04-deployment/secrets-management.md) - Secret system
- [OAuth Testing](./05-testing/backend/oauth-testing.md) - OAuth security

---

### By Topic

**RAG System**
- [ADR-001: Hybrid RAG](./01-architecture/adr/adr-001-hybrid-rag.md)
- [KnowledgeBase Context](./09-bounded-contexts/knowledge-base.md)
- [RAG Diagram](./01-architecture/diagrams/rag-system-detailed.md)

**PDF Processing**
- [ADR-003b: Unstructured PDF](./01-architecture/adr/adr-003b-unstructured-pdf.md)
- [DocumentProcessing Context](./09-bounded-contexts/document-processing.md)
- [PDF Pipeline Diagram](./01-architecture/diagrams/pdf-pipeline-detailed.md)

**Authentication**
- [Authentication Context](./09-bounded-contexts/authentication.md)
- [OAuth Testing](./05-testing/backend/oauth-testing.md)
- [Secrets Management](./04-deployment/secrets-management.md)

**Deployment**
- [Infrastructure Checklist](./04-deployment/infrastructure-deployment-checklist.md)
- [Domain Setup](./04-deployment/domain-setup-guide.md)
- [Cost Summary](./04-deployment/infrastructure-cost-summary.md)

**Testing**
- [Testing Philosophy](./05-testing/README.md#testing-philosophy)
- [Testcontainers Best Practices](./05-testing/backend/testcontainers-best-practices.md)
- [E2E Guide](./05-testing/e2e/E2E_TEST_GUIDE.md)

---

## Key Documents

### Must Read for All Developers
1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide
2. **[Architecture README](./01-architecture/README.md)** - System architecture
3. **[Development README](./02-development/README.md)** - Development workflow
4. **[Bounded Contexts Overview](./09-bounded-contexts/README.md)** - DDD contexts

### Essential ADRs
1. **[ADR-001: Hybrid RAG](./01-architecture/adr/adr-001-hybrid-rag.md)** - RAG architecture
2. **[ADR-003b: Unstructured PDF](./01-architecture/adr/adr-003b-unstructured-pdf.md)** - PDF processing
3. **[ADR-007: Hybrid LLM](./01-architecture/adr/adr-007-hybrid-llm.md)** - Multi-model consensus
4. **[ADR-008: Streaming CQRS](./01-architecture/adr/adr-008-streaming-cqrs-migration.md)** - CQRS pattern
5. **[ADR-009: Error Handling](./01-architecture/adr/adr-009-centralized-error-handling.md)** - Error handling

### Production Deployment
1. **[Deployment Guide](./04-deployment/README.md)** - Complete deployment index
2. **[Infrastructure Checklist](./04-deployment/infrastructure-deployment-checklist.md)** - Step-by-step deployment
3. **[Cost Summary](./04-deployment/infrastructure-cost-summary.md)** - Budget planning
4. **[Monitoring Setup](./04-deployment/monitoring-setup-guide.md)** - Observability

---

## Documentation by Phase

### Alpha (Local Development)
- [CLAUDE.md](../CLAUDE.md) - Complete setup guide
- [Development Guide](./02-development/README.md)
- [Testing Guide](./05-testing/README.md)

### Beta (Staging Deployment)
- [Infrastructure Checklist](./04-deployment/infrastructure-deployment-checklist.md)
- [Domain Setup](./04-deployment/domain-setup-guide.md)
- [Monitoring Setup](./04-deployment/monitoring-setup-guide.md)

### Release (Production)
- [Cost Summary](./04-deployment/infrastructure-cost-summary.md)
- [Email & TOTP Services](./04-deployment/email-totp-services.md)
- [Security Guide](./06-security/README.md)

---

## Documentation Statistics

**After Consolidation (2026-01-26)**:

| Section | Description |
|---------|-------------|
| **01-architecture** | ADRs, diagrams, DDD docs |
| **02-development** | Development guides |
| **03-api** | API reference |
| **04-deployment** | Deployment guides |
| **05-testing** | Testing documentation |
| **06-security** | Security docs |
| **07-frontend** | Frontend architecture |
| **08-infrastructure** | Infrastructure configuration |
| **09-bounded-contexts** | DDD context guides |
| **10-user-guides** | Admin and user guides |
| **11-user-flows** | User flow documentation |
| **roadmap** | Project roadmap |

---

## Maintenance

### Regular Updates

**Weekly**:
- Update test coverage stats in [Testing Guide](./05-testing/README.md)
- Check for broken links (use `markdown-link-check`)

**Monthly**:
- Review ADRs for outdated decisions
- Update deployment guides with lessons learned

**Quarterly**:
- Full documentation audit
- Update architecture diagrams
- Review and consolidate duplicate content

### Documentation Health Checks

**Indicators of Good Health**:
- All README files present and updated
- No duplicate content across sections
- Links between documents work correctly
- Code examples tested and functional
- Bounded context docs match codebase

**Warning Signs**:
- Multiple files covering same topic
- Outdated examples or screenshots
- Broken cross-references
- Historical implementation reports accumulating

---

## Contributing

### Documentation Standards

**File Naming**:
```bash
# Correct
authentication-guide.md
oauth-testing.md

# Incorrect
AuthenticationGuide.md
oauth_testing.md
```

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
- **Traefik**: http://localhost:8090 (reverse proxy dashboard)

---

## Consolidation History

### 2026-01-26: Documentation Cleanup

**Actions Taken**:
- Deleted obsolete folders: claudedocs/, issues/, pdca/, token-budget/, design-proposals/, screenshots/
- Deleted orphan root files (session checkpoints, implementation reports)
- Merged api/ into 03-api/
- Merged 07-deployment/ into 04-deployment/
- Renamed folders to sequential numbering (07-frontend, 08-infrastructure, 09-bounded-contexts, 10-user-guides, 11-user-flows)
- Consolidated admin-guides/ + user-guides/ into 10-user-guides/

**Results**:
- Clean, sequential folder numbering
- No duplicate or obsolete content
- Logical organization by domain

### 2026-01-18: Major Documentation Consolidation

**Actions Taken**:
- Removed obsolete files from claudedocs/ (issue reports, session logs)
- Consolidated testing docs into 05-testing/
- Consolidated deployment docs into 04-deployment/
- Created bounded contexts documentation
- Added READMEs to all documentation sections

---

**Last Updated**: 2026-01-26
**Maintainer**: Documentation Team
**Status**: Clean and Organized
