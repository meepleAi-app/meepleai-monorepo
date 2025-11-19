# MeepleAI Documentation

**Welcome to the MeepleAI documentation!** This directory contains comprehensive documentation for the MeepleAI monorepo, organized by audience and purpose.

> **Quick Links**: [Getting Started](./00-getting-started/quick-start.md) | [Architecture Overview](./01-architecture/overview/system-architecture.md) | [API Docs](./03-api/board-game-ai-api-specification.md) | [Testing Guide](./02-development/testing/core/testing-guide.md)

---

## 📁 Documentation Structure

The documentation is organized into **numbered folders** for consistent ordering:

```
docs/
├── 00-getting-started/          # Start here! Quick start & onboarding
├── 01-architecture/              # System architecture, ADRs, diagrams
├── 02-development/               # Developer guides, refactoring, testing
├── 03-api/                       # API specification & integration
├── 04-frontend/                  # Frontend architecture & guides
├── 05-operations/                # DevOps, deployment, runbooks
├── 06-security/                  # Security audits & best practices
├── 07-project-management/        # Planning, roadmap, organization
├── 08-business/                  # Business plan & strategy
├── 09-research/                  # Research findings & analysis
├── 10-knowledge-base/            # External references & wiki
├── archive/                      # Historical docs, completed reports
└── issues/                       # Issue-specific documentation
```

---

## 🚀 Quick Start

### New to MeepleAI?

1. **[Quick Start Guide](./00-getting-started/quick-start.md)** - Get MeepleAI running locally in 15 minutes
2. **[Executive Summary](./00-getting-started/executive-summary.md)** - High-level overview of the project
3. **[System Architecture](./01-architecture/overview/system-architecture.md)** - Understand the technical design

### I'm a Developer

- **Setup**: [Quick Start](./00-getting-started/quick-start.md), [🇮🇹 Guida Setup Locale](./00-getting-started/guida-setup-locale.md)
- **Architecture**: [System Architecture](./01-architecture/overview/system-architecture.md), [Architecture Diagrams](./01-architecture/diagrams/)
- **API**: [API Specification](./03-api/board-game-ai-api-specification.md)
- **Testing**: [Testing Guide](./02-development/testing/core/testing-guide.md), [Testing Strategy](./02-development/testing/testing-strategy.md)
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
| [Quick Start](./00-getting-started/quick-start.md) | Step-by-step local setup (Docker, API, Web) |
| [Guida Setup Locale](./00-getting-started/guida-setup-locale.md) | 🇮🇹 Guida completa setup locale e test in italiano |
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
| [ADR-007](./01-architecture/adr/adr-007-hybrid-llm.md) | Hybrid LLM Architecture |

#### Diagrams
| Diagram | Description |
|---------|-------------|
| [Bounded Contexts](./01-architecture/diagrams/bounded-contexts-interactions.md) | DDD Bounded Contexts interactions |
| [CQRS/MediatR Flow](./01-architecture/diagrams/cqrs-mediatr-flow.md) | CQRS command/query flow |
| [Infrastructure](./01-architecture/diagrams/infrastructure-overview.md) | Complete infrastructure diagram |
| [PDF Pipeline](./01-architecture/diagrams/pdf-pipeline-detailed.md) | 3-stage PDF processing pipeline |
| [RAG System](./01-architecture/diagrams/rag-system-detailed.md) | Hybrid RAG system architecture |

#### Components
| Component | Description |
|-----------|-------------|
| [PDF Extraction Alternatives](./01-architecture/components/pdf-extraction-alternatives.md) | Comparison of PDF extraction tools |
| [Confidence Validation](./01-architecture/components/confidence-validation.md) | 5-layer confidence validation system |
| [Agent Lightning](./01-architecture/components/agent-lightning/) | Agent Lightning architecture, examples, guides |
| [Amplifier](./01-architecture/components/amplifier/) | Amplifier architecture, workflow, examples |

#### Domain-Driven Design
| Document | Description |
|----------|-------------|
| [DDD Quick Reference](./01-architecture/ddd/quick-reference.md) | DDD patterns, bounded contexts, CQRS |

### 02 - Development

#### Guides
| Guide | Description |
|-------|-------------|
| [LLM Integration](./02-development/guides/llm-integration.md) | Integrating LLMs (OpenRouter, OpenAI, Claude) |
| [Unstructured Setup](./02-development/guides/unstructured-setup.md) | Setting up Unstructured PDF extraction |

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
| [RAG Service Migration](./archive/bgai-implementations/bgai-023-ragservice-migration.md) | RAG service migration findings |
| [Cost Tracking Verification](./archive/bgai-implementations/bgai-026-cost-tracking.md) | LLM cost tracking verification |

#### Testing
| Document | Description |
|----------|-------------|
| [Testing Guide](./02-development/testing/core/testing-guide.md) | Comprehensive test writing guide (60+ pages) |
| [Testing Strategy](./02-development/testing/testing-strategy.md) | Complete testing strategy (30 pages) |
| [Testing Quick Reference](./02-development/testing/testing-quick-reference.md) | Quick reference for testing patterns |
| [Integration Tests Performance](./02-development/testing/integration-tests-performance-guide.md) | Optimize integration tests for 4-11x speedup (80+ pages) |
| [Testing Specialized](./02-development/testing/testing-specialized.md) | Accessibility, concurrency, API testing |
| [Manual Testing Guide](./02-development/testing/manual-testing-guide.md) | 🇮🇹 Full QA procedures (Italian) |
| [Testing Checkpoint Guide](./02-development/testing/testing-checkpoint-guide.md) | 🇮🇹 Checkpoint verification (Italian) |
| _...and 2 more testing docs_ | See [02-development/testing/](./02-development/testing/) |

### 03 - API

| Document | Description |
|----------|-------------|
| [API Specification](./03-api/board-game-ai-api-specification.md) | Complete REST API specification (40 pages) |
| [LLM Cost Tracking API](./03-api/llm-cost-tracking-api.md) | Cost tracking API endpoints |

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

### 05 - Operations

#### Deployment
| Document | Description |
|----------|-------------|
| [Deployment Guide](./05-operations/deployment/board-game-ai-deployment-guide.md) | Docker Compose → Kubernetes (35 pages) |
| [Frontend Deployment](./05-operations/deployment/frontend-deployment.md) | Next.js deployment (Vercel, Docker) |
| [Disaster Recovery](./05-operations/deployment/disaster-recovery.md) | DR plan and procedures |

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
| [Security Audit 2025-11-04](./archive/security-audits/security-audit-2025-11-04.md) | Latest security audit report |
| [Code Scanning Remediation](./06-security/code-scanning-remediation-summary.md) | CodeQL findings remediation |
| [OAuth Security](./06-security/oauth-security.md) | OAuth 2.0 security best practices |
| [Log Forging Prevention](./06-security/log-forging-prevention.md) | Preventing log injection attacks |
| _...and 4 more security docs_ | See [06-security/](./06-security/) |

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
| [Ollama Quality Findings](./archive/bgai-implementations/bgai-016-ollama-quality-findings.md) | Local LLM quality evaluation |

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
- **Be descriptive**: `testing-strategy.md` not `testing.md`
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
- Last Updated: 2025-11-13
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

**Last Updated**: 2025-11-13
**Maintainer**: Documentation Team
**Total Documents**: 115 files
