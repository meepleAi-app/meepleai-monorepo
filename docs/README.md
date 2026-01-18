# MeepleAI Documentation

**AI-powered board game assistant** - Complete documentation for developers, DevOps, and architects

---

## 🚀 Quick Start

**New to MeepleAI?** Start here:

1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide (setup, standards, workflows)
2. **[Architecture Overview](./01-architecture/README.md)** - System design and ADRs
3. **[Development Guide](./02-development/README.md)** - Local setup and development workflow

---

## 📂 Documentation Structure

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
├── 07-bounded-contexts/      # DDD Bounded Contexts guides
└── claudedocs/               # Claude AI analysis documents
```

---

## 🔍 Find What You Need

### By Role

**Developer** 👨‍💻
- [Development Guide](./02-development/README.md) - Local setup, coding standards
- [Bounded Contexts](./07-bounded-contexts/README.md) - DDD context guides
- [Testing Guide](./05-testing/README.md) - Testing strategy
- [API Reference](./03-api/README.md) - REST API documentation

**Architect** 🏗️
- [Architecture](./01-architecture/README.md) - System design
- [ADRs](./01-architecture/adr/README.md) - Architecture decisions
- [DDD Guide](./01-architecture/ddd/quick-reference.md) - DDD patterns
- [Diagrams](./01-architecture/diagrams/) - Mermaid architecture diagrams

**DevOps** ⚙️
- [Deployment Guide](./04-deployment/README.md) - Infrastructure deployment
- [NEW-GUIDES-INDEX](./04-deployment/NEW-GUIDES-INDEX.md) - Complete deployment workflow
- [Monitoring Setup](./04-deployment/monitoring-setup-guide.md) - Grafana + Prometheus
- [Secrets Management](./04-deployment/secrets-management.md) - Secret system

**QA/Tester** 🧪
- [Testing Guide](./05-testing/README.md) - Testing philosophy and pyramid
- [Backend Testing](./05-testing/backend/) - xUnit, Testcontainers
- [Frontend Testing](./05-testing/frontend/) - Vitest, Testing Library
- [E2E Testing](./05-testing/e2e/) - Playwright guides

**Security** 🛡️
- [Security Guide](./06-security/README.md) - Security documentation
- [Secrets Management](./04-deployment/secrets-management.md) - Secret system
- [OAuth Testing](./05-testing/backend/oauth-testing.md) - OAuth security

---

### By Topic

**RAG System** 🤖
- [ADR-001: Hybrid RAG](./01-architecture/adr/adr-001-hybrid-rag.md)
- [KnowledgeBase Context](./07-bounded-contexts/knowledge-base.md)
- [RAG Diagram](./01-architecture/diagrams/rag-system-detailed.md)

**PDF Processing** 📄
- [ADR-003b: Unstructured PDF](./01-architecture/adr/adr-003b-unstructured-pdf.md)
- [DocumentProcessing Context](./07-bounded-contexts/document-processing.md)
- [PDF Pipeline Diagram](./01-architecture/diagrams/pdf-pipeline-detailed.md)

**Authentication** 🔐
- [Authentication Context](./07-bounded-contexts/authentication.md)
- [OAuth Testing](./05-testing/backend/oauth-testing.md)
- [Secrets Management](./04-deployment/secrets-management.md)

**Deployment** 🚀
- [Infrastructure Checklist](./04-deployment/infrastructure-deployment-checklist.md)
- [Domain Setup](./04-deployment/domain-setup-guide.md)
- [Cost Summary](./04-deployment/infrastructure-cost-summary.md)

**Testing** 🧪
- [Testing Philosophy](./05-testing/README.md#testing-philosophy)
- [Testcontainers Best Practices](./05-testing/backend/testcontainers-best-practices.md)
- [E2E Guide](./05-testing/e2e/E2E_TEST_GUIDE.md)

---

## 📖 Key Documents

### Must Read for All Developers
1. **[CLAUDE.md](../CLAUDE.md)** - Complete development guide ⭐
2. **[Architecture README](./01-architecture/README.md)** - System architecture
3. **[Development README](./02-development/README.md)** - Development workflow
4. **[Bounded Contexts Overview](./07-bounded-contexts/README.md)** - DDD contexts

### Essential ADRs
1. **[ADR-001: Hybrid RAG](./01-architecture/adr/adr-001-hybrid-rag.md)** - RAG architecture
2. **[ADR-003b: Unstructured PDF](./01-architecture/adr/adr-003b-unstructured-pdf.md)** - PDF processing
3. **[ADR-007: Hybrid LLM](./01-architecture/adr/adr-007-hybrid-llm.md)** - Multi-model consensus
4. **[ADR-008: Streaming CQRS](./01-architecture/adr/adr-008-streaming-cqrs-migration.md)** - CQRS pattern
5. **[ADR-009: Error Handling](./01-architecture/adr/adr-009-centralized-error-handling.md)** - Error handling

### Production Deployment
1. **[NEW-GUIDES-INDEX](./04-deployment/NEW-GUIDES-INDEX.md)** - Complete deployment index
2. **[Infrastructure Checklist](./04-deployment/infrastructure-deployment-checklist.md)** - Step-by-step deployment
3. **[Cost Summary](./04-deployment/infrastructure-cost-summary.md)** - Budget planning
4. **[Monitoring Setup](./04-deployment/monitoring-setup-guide.md)** - Observability

---

## 🎯 Documentation by Phase

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

## 📊 Documentation Statistics

**After Consolidation (2026-01-18)**:

| Section | Files | Description |
|---------|-------|-------------|
| **01-architecture** | ~30 | ADRs, diagrams, DDD docs |
| **02-development** | ~15 | Development guides |
| **03-api** | 3 | API reference |
| **04-deployment** | 16 | Deployment guides |
| **05-testing** | 15 | Testing documentation (consolidated) |
| **06-security** | 1 | Security docs (+ README) |
| **07-bounded-contexts** | 10 | DDD context guides (new) |
| **claudedocs** | 16 | Claude analysis docs (cleaned) |
| **Total** | ~106 | ✅ Well-organized |

**Consolidation Results**:
- ✅ Removed 17 obsolete files (issue reports, session logs)
- ✅ Merged 3 testing directories → 1 (`05-testing/`)
- ✅ Merged 2 deployment directories → 1 (`04-deployment/`)
- ✅ Created 10 new bounded context docs
- ✅ Added READMEs to all sections

---

## 🔄 Maintenance

### Regular Updates

**Weekly**:
- Update test coverage stats in [Testing Guide](./05-testing/README.md)
- Check for broken links (use `markdown-link-check`)

**Monthly**:
- Review ADRs for outdated decisions
- Update deployment guides with lessons learned
- Prune obsolete Claude analysis docs in `claudedocs/`

**Quarterly**:
- Full documentation audit
- Update architecture diagrams
- Review and consolidate duplicate content

### Documentation Health Checks

**Indicators of Good Health**:
- ✅ All README files present and updated
- ✅ No duplicate content across sections
- ✅ Links between documents work correctly
- ✅ Code examples tested and functional
- ✅ Bounded context docs match codebase

**Warning Signs**:
- ❌ Multiple files covering same topic
- ❌ Outdated examples or screenshots
- ❌ Broken cross-references
- ❌ Historical implementation reports accumulating

---

## 📚 Contributing

### Documentation Standards

**File Naming**:
```bash
✅ authentication-guide.md
✅ oauth-testing.md
❌ AuthenticationGuide.md
❌ oauth_testing.md
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

## 🔗 External Resources

- **API Explorer**: http://localhost:8080/scalar/v1 (interactive API docs)
- **Grafana**: http://localhost:3001 (monitoring dashboards)
- **Traefik**: http://localhost:8090 (reverse proxy dashboard)

---

## 📝 Consolidation History

### 2026-01-18: Major Documentation Consolidation

**Objectives**:
1. ✅ Consolidate documentation based on current codebase state
2. ✅ Organize, reduce duplication, remove historical docs
3. ✅ Add README to every documentation folder

**Actions Taken**:
- Removed 17 obsolete files from `claudedocs/` (issue reports, session logs)
- Consolidated testing docs: `02-development/testing/` + `04-guides/testing/` → `05-testing/`
- Consolidated deployment: `05-deployment/` → `04-deployment/`
- Created `07-bounded-contexts/` with 9 context guides
- Added/updated READMEs in all documentation sections

**Results**:
- **Files Removed**: 17 (obsolete issue/session reports)
- **Directories Consolidated**: 3 → 1 (testing), 2 → 1 (deployment)
- **New Documentation**: 10 files (bounded contexts + READMEs)
- **Structure**: Clean, logical, easy to navigate

**Backup**: Available in git history (commit before consolidation)

---

**Last Updated**: 2026-01-18
**Maintainer**: Documentation Team
**Total Documents**: ~106 files (after consolidation)
**Status**: ✅ Clean and Organized
