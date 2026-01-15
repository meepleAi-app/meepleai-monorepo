# MeepleAI Documentation

**Welcome to the MeepleAI documentation!** This directory contains comprehensive documentation for the MeepleAI monorepo, organized by audience and purpose.

> **Quick Links**: [Getting Started](./00-getting-started/quick-start.md) | [Architecture Overview](./01-architecture/overview/system-architecture.md) | [API Docs](./03-api/board-game-ai-api-specification.md) | [Testing Guide](./02-development/testing/test-writing-guide.md)

---

## Documentation Structure

The documentation is organized into **numbered folders** for consistent ordering:

```
docs/
|- 00-getting-started/          # Start here! Quick start & onboarding
|- 01-architecture/             # System architecture, ADRs, diagrams
|- 02-development/              # Developer guides, refactoring, testing
|- 03-api/                      # API specification & integration (legacy index; use 04-frontend for FE)
|- 04-frontend/                 # Frontend architecture & guides (active)
|- 05-operations/               # DevOps, deployment, runbooks
|- 06-security/                 # Security audits & best practices
|- 07-project-management/       # Planning, roadmap, organization
|- 08-business/                 # Business plan & strategy
|- 09-research/                 # Research findings & analysis
\\- 10-knowledge-base/           # External references & wiki
```

---


## Quick Start

### I'm a Player (End User)

- **User Guide**: [Guida Utente (IT)](./00-getting-started/user-guide.md) - Complete guide in Italian for players
  - How to upload rulebooks
  - How to ask questions during gameplay
  - Chat interface and advanced features
  - FAQ and support

### New to MeepleAI (Developers)?

1. **[Quick Start Guide](./00-getting-started/quick-start.md)** - Get MeepleAI running locally in 15 minutes
2. **[Executive Summary](./00-getting-started/executive-summary.md)** - High-level overview of the project
3. **[System Architecture](./01-architecture/overview/system-architecture.md)** - Understand the technical design

### I'm a Developer

- **Setup**: [Quick Start](./00-getting-started/quick-start.md)
- **Architecture**: [System Architecture](./01-architecture/overview/system-architecture.md), [Architecture Diagrams](./01-architecture/diagrams/)
- **API**: [API Specification](./03-api/board-game-ai-api-specification.md)
- **Testing**: [Test Writing Guide](./02-development/testing/test-writing-guide.md), [Testing Strategy](./02-development/testing/board-game-ai-testing-strategy.md)
- **Guides**: [LLM Integration](./02-development/guides/llm-integration.md), [Unstructured Setup](./02-development/guides/unstructured-setup.md)
- **Refactoring**: [DDD Status](./02-development/refactoring/legacy-code-dashboard.md), [DDD Quick Reference](./01-architecture/ddd/quick-reference.md)

### I'm an Architect

- **Overview**: [System Architecture](./01-architecture/overview/system-architecture.md)
- **ADRs**: [Architecture Decision Records](./01-architecture/adr/) - All ADRs indexed
- **Diagrams**: [Architecture Diagrams](./01-architecture/diagrams/) - Mermaid diagrams for all major systems
- **Components**: [PDF Processing](./01-architecture/components/pdf-extraction-alternatives.md), [RAG System](./01-architecture/adr/adr-001-hybrid-rag.md)
- **DDD**: [DDD Quick Reference](./01-architecture/ddd/quick-reference.md)

### I'm in DevOps

- **Deployment**: [Deployment Guide](./05-operations/deployment/board-game-ai-deployment-guide.md), [Frontend Deployment](./05-operations/deployment/frontend-deployment.md)
- **Runbooks**: [High Error Rate](./05-operations/runbooks/high-error-rate.md), [Dependency Down](./05-operations/runbooks/dependency-down.md), [Troubleshooting](./05-operations/runbooks/general-troubleshooting.md)
- **Monitoring**: [Logging & Audit](./05-operations/monitoring/logging-and-audit.md)
- **Disaster Recovery**: [DR Plan](./05-operations/deployment/disaster-recovery.md)

### I'm a Project Manager

- **Roadmap**: [Strategic Roadmap](./07-project-management/roadmap/board-game-ai-strategic-roadmap.md)
- **Planning**: [Backend Plan](./07-project-management/planning/backend-implementation-plan.md), [Frontend Plan](./07-project-management/planning/frontend-implementation-plan.md)
- **Organization**: [Execution Calendar](./07-project-management/organization/board-game-ai-execution-calendar.md), [Sprint Overview](./07-project-management/organization/board-game-ai-sprint-overview.md)
- **Tracking**: [Issue Tracker](./07-project-management/planning/issue-status-tracker.md)

### I'm a Business Stakeholder

- **Business Plan**: [Complete Business Plan](./08-business/board-game-ai-business-plan.md)
- **Strategy**: [Strategic Roadmap](./07-project-management/roadmap/board-game-ai-strategic-roadmap.md)
- **Executive Summary**: [Executive Summary](./00-getting-started/executive-summary.md)

---

## 📚 Detailed Documentation by Category

### 00 - Getting Started

| Document | Description |
|----------|-------------|
| [User Guide (IT)](./00-getting-started/user-guide.md) | **NEW** Guida completa per giocatori (italiano) |
| [Quick Start](./00-getting-started/quick-start.md) | Step-by-step local setup (Docker, API, Web) |
| [Guida Setup Locale (IT)](./00-getting-started/guida-setup-locale.md) | Setup dettagliato in italiano |
| [Executive Summary](./00-getting-started/executive-summary.md) | High-level project overview |
| [Overview](./00-getting-started/overview.md) | Documentation overview |

### 01 - Architecture

#### Overview
| Document | Description |
|----------|-------------|
| [System Architecture](./01-architecture/overview/system-architecture.md) | Complete system architecture (60+ pages) |
| [Consolidation Strategy](./01-architecture/overview/consolidation-strategy.md) | Architecture consolidation plan |

#### Architecture Decision Records (ADRs)
| ADR | Title |
|-----|-------|
| [ADR-001](./01-architecture/adr/adr-001-hybrid-rag.md) | Hybrid RAG Architecture (Vector + Keyword RRF) |
| [ADR-002](./01-architecture/adr/adr-002-multilingual-embedding.md) | Multilingual Embedding Strategy |
| [ADR-003](./01-architecture/adr/adr-003-pdf-processing.md) | PDF Processing Pipeline (3-stage fallback) |
| [ADR-003b](./01-architecture/adr/adr-003b-unstructured-pdf.md) | Unstructured PDF Extraction |
| [ADR-004](./01-architecture/adr/adr-004-ai-agents.md) | AI Agents Bounded Context |
| [ADR-004b](./01-architecture/adr/adr-004b-hybrid-llm.md) | Hybrid LLM Architecture |

#### Diagrams
| Diagram | Description |
|---------|-------------|
| [Bounded Contexts](./01-architecture/diagrams/bounded-contexts-interactions.md) | DDD Bounded Contexts interactions |
| [CQRS/MediatR Flow](./01-architecture/diagrams/cqrs-mediatr-flow.md) | CQRS command/query flow |
| [GitHub Actions Flow](./01-architecture/diagrams/github-actions-flow.md) | CI/CD workflow visualization |
| [Infrastructure](./01-architecture/diagrams/infrastructure-overview.md) | Complete infrastructure diagram |
| [PDF Pipeline](./01-architecture/diagrams/pdf-pipeline-detailed.md) | 3-stage PDF processing pipeline |
| [RAG System](./01-architecture/diagrams/rag-system-detailed.md) | Hybrid RAG system architecture |

#### Components
| Component | Description |
|-----------|-------------|
| [PDF Extraction Alternatives](./01-architecture/components/pdf-extraction-alternatives.md) | Comparison of PDF extraction tools |
| [Confidence Validation](./01-architecture/components/confidence-validation.md) | 5-layer confidence validation system |
| [**Shared Game Catalog Specification**](./01-architecture/components/shared-game-catalog-spec.md) | Shared catalog architecture and design ⭐ |
| [Agent Lightning](./01-architecture/components/agent-lightning/) | Agent Lightning architecture, examples, guides |
| [Amplifier](./01-architecture/components/amplifier/) | Amplifier architecture, workflow, examples |

#### Domain-Driven Design
| Document | Description |
|----------|-------------|
| [DDD Quick Reference](./01-architecture/ddd/quick-reference.md) | DDD patterns, bounded contexts, CQRS |
| [**DDD Migration Pattern Guide**](./01-architecture/ddd/ddd-migration-pattern-guide.md) | Migration patterns and best practices ⭐ |

### 02 - Development

#### Setup & Configuration
| Document | Description |
|----------|-------------|
| [**BGG API Token Setup**](./02-development/BGG_API_TOKEN_SETUP.md) | BoardGameGeek API configuration ⭐ |
| [**Azul Test Instructions**](./02-development/AZUL_TEST_INSTRUCTIONS.md) | Azul game testing guide ⭐ |
| [**Docker Services Test URLs**](./02-development/docker-services-test-urls.md) | Local services endpoints ⭐ |
| [**Git Workflow**](./02-development/git-workflow.md) | Git branching and PR process ⭐ |

#### Guides
| Guide | Description |
|-------|-------------|
| [LLM Integration](./02-development/guides/llm-integration.md) | Integrating LLMs (OpenRouter, OpenAI, Claude) |
| [Unstructured Setup](./02-development/guides/unstructured-setup.md) | Setting up Unstructured PDF extraction |

#### Troubleshooting
| Document | Description |
|----------|-------------|
| [**PDF Processing Debug**](./02-development/troubleshooting/pdf-processing-debug-session.md) | PDF extraction troubleshooting ⭐ |
| [**Logger Review**](./02-development/LOGGER-REVIEW-2385.md) | Logging implementation review ⭐ |

#### Refactoring (DDD Migration)
| Document | Description |
|----------|-------------|
| [Legacy Code Inventory](./02-development/refactoring/legacy-code-inventory.md) | Complete inventory of legacy services (150+ pages) |
| [Legacy Code Dashboard](./02-development/refactoring/legacy-code-dashboard.md) | Visual progress tracking dashboard |
| [Implementation Notes](./02-development/refactoring/implementation-notes.md) | DDD implementation notes |
| [Next Steps](./02-development/refactoring/next-steps.md) | Remaining refactoring tasks |

#### Implementation
| Document | Description |
|----------|-------------|
| [RAG Service Migration](./02-development/implementation/bgai-023-ragservice-migration.md) | RAG service migration findings |
| [Cost Tracking Verification](./02-development/implementation/bgai-026-cost-tracking.md) | LLM cost tracking verification |

#### Testing
| Document | Description |
|----------|-------------|
| [Test Writing Guide](./02-development/testing/test-writing-guide.md) | How to write tests (unit, integration, E2E) |
| [Testing Strategy](./02-development/testing/board-game-ai-testing-strategy.md) | Complete testing strategy (30 pages) |
| [Test Patterns](./02-development/testing/test-patterns.md) | Common test patterns (AAA, mocking, etc.) |
| [**Validation Audit**](./05-testing/issue-2424-validation-audit.md) | FluentValidation implementation audit ⭐ |
| [Accessibility Testing](./02-development/testing/accessibility-testing-guide.md) | Accessibility testing with Playwright |
| [E2E Contribution Guide](./02-development/testing/e2e-contribution-guide.md) | Contributing E2E tests |
| [Page Object Model](./02-development/testing/pom-architecture-design.md) | POM architecture for E2E tests |
| _...and 15 more testing docs_ | See [02-development/testing/](./02-development/testing/) |

### 03 - API

| Document | Description |
|----------|-------------|
| [API Specification](./03-api/board-game-ai-api-specification.md) | Complete REST API specification (40 pages) |
| [LLM Cost Tracking API](./03-api/llm-cost-tracking-api.md) | Cost tracking API endpoints |
| [**OpenAPI Audit Report**](./03-api/issue-2425-openapi-audit.md) | OpenAPI specification audit ⭐ |

### 04 - Frontend

| Document | Description |
|----------|-------------|
| [Architecture](./04-frontend/architecture.md) | Frontend architecture (Next.js 16, React 19) |
| [Accessibility Standards](./04-frontend/accessibility-standards.md) | WCAG 2.1 AA compliance |
| [Internationalization](./04-frontend/internationalization-strategy.md) | i18n strategy (Italian-first) |
| [Performance Requirements](./04-frontend/performance-requirements.md) | Performance targets (FCP, LCP, CLS) |
| [Shadcn/UI Installation](./04-frontend/shadcn-ui-installation.md) | Shadcn/UI setup guide |
| [Testing Strategy](./04-frontend/testing-strategy.md) | Frontend testing (Jest, Playwright) |
| [Use Cases](./04-frontend/use-cases.md) | User journeys and use cases |
| [**User Flows**](./04-frontend/user-flows.md) | Complete user journeys (v2.0) ⭐ |
| [User Flows - Edge Cases](./04-frontend/user-flows-edge-cases.md) | Error states and recovery |

### 05 - Operations

#### Deployment
| Document | Description |
|----------|-------------|
| [Deployment Guide](./05-operations/deployment/board-game-ai-deployment-guide.md) | Docker Compose → Kubernetes (35 pages) |
| [Frontend Deployment](./05-operations/deployment/frontend-deployment.md) | Next.js deployment (Vercel, Docker) |
| [Disaster Recovery](./05-operations/deployment/disaster-recovery.md) | DR plan and procedures |
| [**Shared Catalog Pre-Deployment**](./04-deployment/shared-catalog-pre-deployment-checklist.md) | Shared catalog deployment checklist ⭐ |
| [**Shared Catalog Environment**](./04-deployment/shared-catalog-environment-config.md) | Environment configuration guide ⭐ |

#### Monitoring & Observability
| Document | Description |
|----------|-------------|
| [**Observability Validation**](./04-deployment/monitoring/observability-validation-report.md) | Observability system validation ⭐ |
| [**Grafana Dashboard Fix**](./04-deployment/monitoring/grafana-dashboard-fix-report.md) | Dashboard configuration fixes ⭐ |
| [**Health Check OAuth**](./04-deployment/monitoring/health-check-oauth-report.md) | OAuth health check report ⭐ |
| [**Health Check Final Report**](./04-deployment/monitoring/final-health-check-report-2026-01-15.md) | Comprehensive health check ⭐ |

#### Secrets Management
| Document | Description |
|----------|-------------|
| [**Infisical POC Setup**](./04-deployment/secrets/infisical-poc-setup-guide.md) | Infisical proof of concept setup ⭐ |
| [**Infisical POC Results**](./04-deployment/secrets/infisical-poc-results.md) | POC evaluation results ⭐ |

#### Validation
| Document | Description |
|----------|-------------|
| [**Production Validation Guide**](./04-deployment/validation/issue-2374-production-validation-guide.md) | Production deployment validation ⭐ |

#### Runbooks
| Runbook | Scenario |
|---------|----------|
| [High Error Rate](./05-operations/runbooks/high-error-rate.md) | Error spike troubleshooting |
| [Dependency Down](./05-operations/runbooks/dependency-down.md) | PG/Qdrant/Redis outage |
| [AI Quality Low](./05-operations/runbooks/ai-quality-low.md) | RAG quality degradation |
| [Error Spike](./05-operations/runbooks/error-spike.md) | Sudden error increase |
| [General Troubleshooting](./05-operations/runbooks/general-troubleshooting.md) | Common issues and fixes |

#### Monitoring
| Document | Description |
|----------|-------------|
| [Logging & Audit](./05-operations/monitoring/logging-and-audit.md) | Serilog, Seq, correlation IDs, audit trails |

### 06 - Security

| Document | Description |
|----------|-------------|
| [**Security Audit 2026-Q1**](./06-security/2026-Q1-security-review.md) | Q1 2026 security review ⭐ |
| [**Secrets Audit 2026-01-15**](./06-security/secrets-audit-2026-01-15.md) | Secrets management audit ⭐ |
| [**TOTP Vulnerability Analysis**](./06-security/totp_vulnerability_analysis.md) | 2FA implementation security ⭐ |
| [Code Warnings Remediation](./06-security/code-warnings-remediation-plan.md) | Security analyzer remediation (CA3xxx, S2xxx) |
| [Code Scanning Remediation](./06-security/code-scanning-remediation-summary.md) | CodeQL findings remediation |
| [OAuth Security](./06-security/oauth-security.md) | OAuth 2.0 security best practices |
| [Log Forging Prevention](./06-security/log-forging-prevention.md) | Preventing log injection attacks |

### 07 - Project Management

#### Planning
| Document | Description |
|----------|-------------|
| [Backend Implementation Plan](./07-project-management/planning/backend-implementation-plan.md) | ASP.NET 9 implementation roadmap |
| [Frontend Implementation Plan](./07-project-management/planning/frontend-implementation-plan.md) | Next.js 16 implementation roadmap |
| [Executive Summary Roadmap](./07-project-management/planning/executive-summary-development-roadmap.md) | High-level development roadmap |
| [Issue Status Tracker](./07-project-management/planning/issue-status-tracker.md) | GitHub issues tracking |

#### Roadmap
| Document | Description |
|----------|-------------|
| [Strategic Roadmap](./07-project-management/roadmap/board-game-ai-strategic-roadmap.md) | 4-phase strategic roadmap (50 pages) |

#### Organization
| Document | Description |
|----------|-------------|
| [Execution Calendar](./07-project-management/organization/board-game-ai-execution-calendar.md) | Sprint calendar and milestones |
| [Sprint Overview](./07-project-management/organization/board-game-ai-sprint-overview.md) | Sprint structure and ceremonies |
| [Onboarding Guide](./07-project-management/organization/onboarding_guide.md) | New team member onboarding |
| [Solo Developer Execution Plan](./07-project-management/organization/solo-developer-execution-plan.md) | Solo developer workflow |
| _...and 8 more org docs_ | See [07-project-management/organization/](./07-project-management/organization/) |

#### Completion Reports
| Document | Description |
|----------|-------------|
| [Phase 1a Completion](./07-project-management/completion-reports/phase-1a-completion-report.md) | Phase 1a milestone report |

### 08 - Business

| Document | Description |
|----------|-------------|
| [Business Plan](./08-business/board-game-ai-business-plan.md) | Complete business plan (35 pages) |

### 09 - Research

| Document | Description |
|----------|-------------|
| [Ollama Quality Findings](./09-research/bgai-016-ollama-quality-findings.md) | Local LLM quality evaluation |

### 10 - Knowledge Base

| Document | Description |
|----------|-------------|
| [AI Systems for Board Games 2025](./10-knowledge-base/Sistemi%20AI%20per%20arbitrare%20giochi%20da%20tavolo%20%20stato%20dell'arte%202025.md) | State of the art research (Italian) |
| [OpenRouter Models Reference](./10-knowledge-base/references/openrouter-models-reference.wiki) | OpenRouter model comparison |

---

## 🔍 Finding Documentation

### By Role

- **Developers** → [02-development/](./02-development/), [01-architecture/](./01-architecture/), [03-api/](./03-api/)
- **Architects** → [01-architecture/](./01-architecture/), [01-architecture/adr/](./01-architecture/adr/)
- **DevOps** → [05-operations/](./05-operations/)
- **PMs** → [07-project-management/](./07-project-management/)
- **Business** → [08-business/](./08-business/), [07-project-management/roadmap/](./07-project-management/roadmap/)
- **QA** → [02-development/testing/](./02-development/testing/)

### By Topic

- **Architecture** → [01-architecture/](./01-architecture/)
- **RAG System** → [ADR-001](./01-architecture/adr/adr-001-hybrid-rag.md), [RAG Diagram](./01-architecture/diagrams/rag-system-detailed.md)
- **PDF Processing** → [ADR-003](./01-architecture/adr/adr-003-pdf-processing.md), [PDF Diagram](./01-architecture/diagrams/pdf-pipeline-detailed.md)
- **DDD Refactoring** → [02-development/refactoring/](./02-development/refactoring/), [DDD Quick Ref](./01-architecture/ddd/quick-reference.md)
- **Testing** → [02-development/testing/](./02-development/testing/)
- **Security** → [06-security/](./06-security/)
- **Deployment** → [05-operations/deployment/](./05-operations/deployment/)
- **API** → [03-api/](./03-api/)

---

## 📖 Documentation Standards

### File Naming

- **Use kebab-case**: `quick-start.md`, not `QuickStart.md` or `quick_start.md`
- **Be descriptive**: `board-game-ai-testing-strategy.md` not `testing.md`
- **Avoid prefixes**: Use folder structure instead of `board-game-ai-*` prefixes

### Structure

```markdown
# Title

**Brief description**

## Table of Contents (optional for long docs)

## Section 1

Content...

## Section 2

Content...

---

**Metadata** (at bottom):
- Version: 1.0
- Last Updated: 2025-12-13T10:59:23.970Z
- Author: Team Name
```

### Cross-References

- **Use relative paths**: `[Architecture](../01-architecture/overview/system-architecture.md)`
- **Link from root**: `[Quick Start](./00-getting-started/quick-start.md)`
- **Be specific**: Link to sections with anchors `[RAG](#rag-pipeline)`

---

## 🤝 Contributing Documentation

### Adding New Documentation

1. **Choose the right folder** based on audience and topic (see structure above)
2. **Create the file** using kebab-case naming
3. **Write the content** following documentation standards
4. **Add to this README** in the appropriate section
5. **Create a PR** with `[DOCS]` prefix: `[DOCS] Add OAuth integration guide`

### Updating Existing Documentation

1. **Read the file first** to understand context
2. **Make focused changes** (avoid large refactors without discussion)
3. **Update "Last Updated" date** in metadata
4. **Test all links** to ensure they still work
5. **Create a PR** with clear description of changes

### Documentation Review Checklist

- [ ] File follows naming conventions (kebab-case)
- [ ] Content is clear and concise
- [ ] Code examples are tested and work
- [ ] All internal links are valid
- [ ] Metadata is updated (version, date, author)
- [ ] Added to this README in appropriate section
- [ ] Grammar and spelling checked

---

## 🔗 External Resources

- **GitHub Repository**: https://github.com/DegrassiAaron/meepleai-monorepo
- **CLAUDE.md**: [Project Guide](../CLAUDE.md) - Complete development guide
- **Tools**: [tools/](../tools/) - Documentation search, generation, standardization

---

## 📝 Reorganization History

- **2025-11-13**: Major reorganization into numbered folders (00-10) for better discoverability
  - See [REORGANIZATION-PLAN.md](./REORGANIZATION-PLAN.md) for details

---

## 📝 Consolidation History

### 2026-01-15: Documentation Consolidation

**Objectives**:
- Remove historical implementation reports and session summaries
- Reorganize essential operational documentation into standard structure
- Reduce documentation debt and improve discoverability

**Results**:
- **Removed**: ~64 obsolete files (issue reports, session summaries, cleanup reports)
- **Reorganized**: 16 essential files moved to standard structure
- **Reduction**: 81% reduction in claudedocs (79 → 1 file)
- **Backup**: `docs-backup-consolidation-2026-01-15/`

**See**: [CONSOLIDATION-PLAN-2026-01-15.md](./claudedocs/CONSOLIDATION-PLAN-2026-01-15.md)

---

**Last Updated**: 2026-01-15T12:50:00Z
**Maintainer**: Documentation Team
**Total Documents**: ~50 files (after consolidation)



