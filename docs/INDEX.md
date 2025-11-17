# MeepleAI Documentation Index

**Project**: Italian Board Game Rules AI Assistant
**Version**: 2.0
**Last Updated**: 2025-11-15

---

## 🚀 Quick Navigation

**New Team Members** → Read in this order:
1. [Quick Start](./00-getting-started/quick-start.md) - Get up and running in 15 minutes
2. [System Architecture](./01-architecture/overview/system-architecture.md) - Understand the technical design
3. [API Specification](./03-api/board-game-ai-api-specification.md) - Learn the API
4. [Testing Strategy](./02-development/testing/testing-strategy.md) - Quality standards
5. [Deployment Guide](./05-operations/deployment/board-game-ai-deployment-guide.md) - Deploy to production

**Find docs by role** → See [README.md](./README.md#-finding-documentation) for role-based navigation

---

## 📁 Documentation Categories

### [00 - Getting Started](./00-getting-started/)
**Audience**: All users, new team members
**Purpose**: Quick start guides, onboarding, project overview

- [Quick Start](./00-getting-started/quick-start.md) - Local setup in 15 minutes
- [Guida Setup Locale](./00-getting-started/guida-setup-locale.md) - 🇮🇹 Guida completa setup locale e test in italiano
- [Executive Summary](./00-getting-started/executive-summary.md) - Project overview
- [Overview](./00-getting-started/overview.md) - Documentation structure

### [01 - Architecture](./01-architecture/)
**Audience**: Architects, senior developers, technical leadership
**Purpose**: System design, architectural decisions, technical diagrams

#### Subdirectories
- **[overview/](./01-architecture/overview/)** - High-level architecture docs
  - [System Architecture](./01-architecture/overview/system-architecture.md) (60+ pages)
  - [Consolidation Strategy](./01-architecture/overview/consolidation-strategy.md)

- **[adr/](./01-architecture/adr/)** - Architecture Decision Records (6 ADRs)
  - [ADR-001: Hybrid RAG](./01-architecture/adr/adr-001-hybrid-rag.md)
  - [ADR-002: Multilingual Embedding](./01-architecture/adr/adr-002-multilingual-embedding.md)
  - [ADR-003: PDF Processing](./01-architecture/adr/adr-003-pdf-processing.md)
  - [ADR-003b: Unstructured PDF](./01-architecture/adr/adr-003b-unstructured-pdf.md)
  - [ADR-004: AI Agents](./01-architecture/adr/adr-004-ai-agents.md)
  - [ADR-004b: Hybrid LLM](./01-architecture/adr/adr-004b-hybrid-llm.md)

- **[diagrams/](./01-architecture/diagrams/)** - Architecture diagrams (Mermaid)
  - [Bounded Contexts Interactions](./01-architecture/diagrams/bounded-contexts-interactions.md)
  - [CQRS/MediatR Flow](./01-architecture/diagrams/cqrs-mediatr-flow.md)
  - [Infrastructure Overview](./01-architecture/diagrams/infrastructure-overview.md)
  - [PDF Pipeline Detailed](./01-architecture/diagrams/pdf-pipeline-detailed.md)
  - [RAG System Detailed](./01-architecture/diagrams/rag-system-detailed.md)

- **[components/](./01-architecture/components/)** - Component-specific docs
  - [PDF Extraction Alternatives](./01-architecture/components/pdf-extraction-alternatives.md)
  - [Confidence Validation](./01-architecture/components/confidence-validation.md)
  - [agent-lightning/](./01-architecture/components/agent-lightning/) - Agent Lightning docs (5 files)
  - [amplifier/](./01-architecture/components/amplifier/) - Amplifier docs (3 files)

- **[ddd/](./01-architecture/ddd/)** - Domain-Driven Design
  - [DDD Quick Reference](./01-architecture/ddd/quick-reference.md)

### [02 - Development](./02-development/)
**Audience**: Developers, QA engineers, refactoring team
**Purpose**: Development guides, testing, refactoring, implementation notes

#### Subdirectories
- **[guides/](./02-development/guides/)** - Technical guides (6 files)
  - [AI Agents Guide](./02-development/guides/ai-agents-guide.md)
  - [Codebase Maintenance](./02-development/guides/codebase-maintenance.md)
  - [Dependency Management](./02-development/guides/dependency-management.md)
  - [LLM Integration Guide](./02-development/guides/llm-integration-guide.md)
  - [Migration Management](./02-development/guides/migration-management.md)
  - [PDF Processing Guide](./02-development/guides/pdf-processing-guide.md) ⭐

- **[refactoring/](./02-development/refactoring/)** - DDD migration docs (5 files)
  - [Legacy Code Inventory](./02-development/refactoring/legacy-code-inventory.md) (150+ pages)
  - [Legacy Code Dashboard](./02-development/refactoring/legacy-code-dashboard.md)
  - [Implementation Notes](./02-development/refactoring/implementation-notes.md)
  - [Migration Edit 05 Frontend](./02-development/refactoring/migration-edit05-frontend.md)
  - [Next Steps](./02-development/refactoring/next-steps.md)

- **[testing/](./02-development/testing/)** - Testing documentation (9 files)
  - [Testing Guide](./02-development/testing/testing-guide.md) ⭐ - Comprehensive test writing guide (60+ pages)
  - [Testing Strategy](./02-development/testing/testing-strategy.md) ⭐ - Test pyramid, quality gates (30 pages)
  - [Integration Tests Performance Guide](./02-development/testing/integration-tests-performance-guide.md) ⭐ NEW - Optimize integration tests for 4-11x speedup (80+ pages)
  - [Integration Tests Quick Reference](./02-development/testing/integration-tests-quick-reference.md) - Quick cheat sheet for performant integration tests
  - [Testing Specialized](./02-development/testing/testing-specialized.md) - Manual, accessibility, concurrency, API testing
  - [Testing Quick Reference](./02-development/testing/testing-quick-reference.md) - Pattern lookup
  - [Testing React 19 Patterns](./02-development/testing/testing-react-19-patterns.md) - React 19 specific patterns
  - [Testing Checkpoint Guide](./02-development/testing/testing-checkpoint-guide.md) (Italian) - Checkpoint verification guide
  - [Manual Testing Guide](./02-development/testing/manual-testing-guide.md) (Italian) - Full QA procedures

### [03 - API](./03-api/)
**Audience**: API consumers, frontend developers, integrators
**Purpose**: API specification, endpoints, authentication

- [API Specification](./03-api/board-game-ai-api-specification.md) (40 pages) ⭐
- [LLM Cost Tracking API](./03-api/llm-cost-tracking-api.md)

### [04 - Frontend](./04-frontend/)
**Audience**: Frontend developers, UI/UX designers
**Purpose**: Frontend architecture, design system, performance

- [README](./04-frontend/README.md) - Frontend overview
- [Architecture](./04-frontend/architecture.md)
- [Design System](./04-frontend/design-system.md)
- [Accessibility Standards](./04-frontend/accessibility-standards.md)
- [Internationalization Strategy](./04-frontend/internationalization-strategy.md)
- [Performance Requirements](./04-frontend/performance-requirements.md)
- [React 19 & Next.js 16 Best Practices](./04-frontend/react19-nextjs16-best-practices.md)
- [Shadcn/UI Installation](./04-frontend/shadcn-ui-installation.md)
- [Testing Strategy](./04-frontend/testing-strategy.md)
- [Use Cases](./04-frontend/use-cases.md)
- [User Flows](./04-frontend/user-flows.md)

### [05 - Operations](./05-operations/)
**Audience**: DevOps, SRE, on-call engineers
**Purpose**: Deployment, monitoring, incident response

#### Subdirectories
- **[deployment/](./05-operations/deployment/)** - Deployment guides (3 files)
  - [Deployment Guide](./05-operations/deployment/board-game-ai-deployment-guide.md) (35 pages) ⭐
  - [Frontend Deployment](./05-operations/deployment/frontend-deployment.md)
  - [Disaster Recovery](./05-operations/deployment/disaster-recovery.md)

- **[runbooks/](./05-operations/runbooks/)** - Incident runbooks (6 files)
  - [High Error Rate](./05-operations/runbooks/high-error-rate.md)
  - [Dependency Down](./05-operations/runbooks/dependency-down.md)
  - [AI Quality Low](./05-operations/runbooks/ai-quality-low.md)
  - [Error Spike](./05-operations/runbooks/error-spike.md)
  - [Prompt Management Deployment](./05-operations/runbooks/prompt-management-deployment.md)
  - [General Troubleshooting](./05-operations/runbooks/general-troubleshooting.md)

- **[monitoring/](./05-operations/monitoring/)** - Observability (1 file)
  - [Logging & Audit](./05-operations/monitoring/logging-and-audit.md)

### [06 - Security](./06-security/)
**Audience**: Security team, compliance, developers
**Purpose**: Security audits, vulnerability remediation, best practices

- [Code Scanning Remediation](./06-security/code-scanning-remediation-summary.md) ⭐
- [OAuth Security](./06-security/oauth-security.md)
- [Security Issue Audit](./06-security/security-issue-audit.md)
- [Log Forging Prevention](./06-security/log-forging-prevention.md)
- [Disposable Resource Leak Remediation](./06-security/disposable-resource-leak-remediation.md)
- [Generic Catch Analysis](./06-security/generic-catch-analysis.md)
- [Null Reference Remediation](./06-security/null-reference-remediation.md)

### [07 - Project Management](./07-project-management/)
**Audience**: Project managers, product owners, leadership
**Purpose**: Planning, roadmap, team organization, sprint tracking

#### Subdirectories
- **[planning/](./07-project-management/planning/)** - Implementation plans (10 files)
  - [Backend Implementation Plan](./07-project-management/planning/backend-implementation-plan.md)
  - [Frontend Implementation Plan](./07-project-management/planning/frontend-implementation-plan.md)
  - [Executive Summary Roadmap](./07-project-management/planning/executive-summary-development-roadmap.md)
  - [Issue Status Tracker](./07-project-management/planning/issue-status-tracker.md)
  - [Manual Issue Creation Guide](./07-project-management/planning/manual-issue-creation-guide.md)
  - [Sprint 5 Integration Tests Plan](./07-project-management/planning/sprint-5-integration-tests-plan.md)
  - _...and 4 more planning docs_

- **[roadmap/](./07-project-management/roadmap/)** - Strategic roadmap (8 files)
  - [Strategic Roadmap](./07-project-management/roadmap/board-game-ai-strategic-roadmap.md) (50 pages) ⭐
  - [Master Roadmap 2025](./07-project-management/roadmap/master-roadmap-2025.md)
  - [Code Quality Roadmap 2025](./07-project-management/roadmap/ROADMAP-CODE-QUALITY-2025.md) (11-week plan)
  - [Frontend Roadmap 2025](./07-project-management/roadmap/frontend-roadmap-2025.md)
  - [Quick Reference: Top 10 Issues](./07-project-management/roadmap/quick-reference-top-10-issues.md)
  - _...and 3 more roadmap docs_

- **[organization/](./07-project-management/organization/)** - Team & execution (7 files)
  - [Team Organization](./07-project-management/organization/team-organization.md) ⭐ - Team structure, roles, onboarding
  - [Execution Calendar](./07-project-management/organization/board-game-ai-execution-calendar.md)
  - [Sprint Overview](./07-project-management/organization/board-game-ai-sprint-overview.md)
  - [Onboarding Guide](./07-project-management/organization/onboarding_guide.md)
  - [Solo Developer Execution Plan](./07-project-management/organization/solo-developer-execution-plan.md)
  - [Project Prioritization 2025](./07-project-management/organization/project-prioritization-2025.md)
  - [README](./07-project-management/organization/README.md)

- **[tracking/](./07-project-management/tracking/)** - Metrics & baselines (3 files)
  - [Integration Tests Known Issues](./07-project-management/tracking/integration-tests-known-issues.md)
  - [Migration Ordering Issue](./07-project-management/tracking/migration-ordering-issue.md)
  - [Time Provider Services Inventory](./07-project-management/tracking/time-provider-services-inventory.md)

- **[improvement-plans/](./07-project-management/improvement-plans/)** - Improvement initiatives
  - [Backend Improvements](./07-project-management/improvement-plans/backend/) (5 files)

- **[completion-reports/](./07-project-management/completion-reports/)** - Phase reports
  - _See [archive/completion-reports/](./archive/completion-reports/) for historical reports_

### [08 - Business](./08-business/)
**Audience**: Investors, founders, business stakeholders
**Purpose**: Business strategy, revenue model, market analysis

- [Business Plan](./08-business/board-game-ai-business-plan.md) (35 pages) ⭐

### [09 - Research](./09-research/)
**Audience**: Research team, technical leadership
**Purpose**: Research findings, competitive analysis, experiments

- _See [archive/bgai-implementations/](./archive/bgai-implementations/) for BGAI research findings_

### [10 - Knowledge Base](./10-knowledge-base/)
**Audience**: All users
**Purpose**: External references, research papers, wiki content

- [AI Systems for Board Games 2025](./10-knowledge-base/ai-systems-board-games-state-of-art-2025-it.md) (Italian)
- [references/](./10-knowledge-base/references/) - External references
  - [OpenRouter Models Reference](./10-knowledge-base/references/openrouter-models-reference.wiki)

---

## 📦 Archive & Issue-Specific Docs

### [archive/](./archive/)
**Purpose**: Historical documentation, completed reports, archived research

- **[security-audits/](./archive/security-audits/)** - Historical security audits
  - [Security Audit 2025-11-04](./archive/security-audits/security-audit-2025-11-04.md)

- **[completion-reports/](./archive/completion-reports/)** - Completed sprint/phase reports
  - [Phase 1a Completion Report](./archive/completion-reports/phase-1a-completion-report.md)
  - [Sprint 4 Reports](./archive/completion-reports/sprint-4/)

- **[bgai-implementations/](./archive/bgai-implementations/)** - BGAI implementation research (6 files)
  - [BGAI-016: Ollama Quality Findings](./archive/bgai-implementations/bgai-016-ollama-quality-findings.md)
  - [BGAI-023: RAG Service Migration](./archive/bgai-implementations/bgai-023-ragservice-migration.md)
  - [BGAI-024: RAG Backward Compatibility](./archive/bgai-implementations/bgai-024-rag-backward-compatibility-testing.md)
  - [BGAI-025: RAG Performance Baseline](./archive/bgai-implementations/bgai-025-rag-performance-baseline.md)
  - [BGAI-026: Cost Tracking](./archive/bgai-implementations/bgai-026-cost-tracking.md)
  - [BGAI-030: Multilingual Patterns](./archive/bgai-implementations/bgai-030-multilingual-patterns.md)

### [issues/](./issues/)
**Purpose**: Issue-specific documentation, fix reports, implementation summaries

- **[issue-733/](./issues/issue-733/)** - Sensitive Info Exposure Fix
- **[issue-954/](./issues/issue-954/)** - Security Analysis (954 issues)
- **[issue-1089/](./issues/issue-1089/)** - Implementation Summary
- **[issue-1134/](./issues/issue-1134/)** - Session Management UI
- **[issue-1191/](./issues/issue-1191/)** - OAuth Callback CQRS Migration

---

## 🔑 Key Concepts & Glossary

### Technical Terms

**RAG (Retrieval Augmented Generation)**: Architecture combining document retrieval with LLM generation for accurate, cited responses.

**Vector Database**: Specialized database for high-dimensional similarity search (Qdrant). Stores embeddings for semantic search.

**Embedding**: Numerical vector (1024 dimensions) representing text semantics. Similar texts have similar embeddings.

**Hallucination**: AI-generated content that is factually incorrect. Target: <3% hallucination rate.

**Confidence Score**: Numerical measure (0.0-1.0) of AI certainty. Threshold: ≥0.70 required.

**RRF (Reciprocal Rank Fusion)**: Algorithm combining ranked lists from vector + keyword search (70/30 weighting).

**CQRS**: Command Query Responsibility Segregation pattern. Separate read/write models.

**DDD (Domain-Driven Design)**: Software design approach focused on bounded contexts and domain models.

### Business Terms

**MAU (Monthly Active Users)**: Target 10,000 by Phase 4.

**LTV (Lifetime Value)**: Total revenue from a customer over their lifetime.

**CAC (Customer Acquisition Cost)**: Cost to acquire one customer.

**Churn Rate**: % customers canceling per month. Target: <10%.

**NPS (Net Promoter Score)**: Customer satisfaction metric (-100 to +100).

---

## 📊 Documentation Statistics

- **Total Files**: ~140 markdown documents (including archive and issue-specific docs)
- **Total Pages**: ~850 pages (estimated)
- **Categories**: 11 numbered folders (00-10) + archive + issues
- **Largest Docs**:
  - Legacy Code Inventory: 150+ pages
  - Integration Tests Performance Guide: 80+ pages
  - Testing Guide: 60+ pages
  - System Architecture: 60+ pages
  - Strategic Roadmap: 50 pages
  - Testing Checkpoint Guide: 40 pages
  - API Specification: 40 pages

---

## 🗺️ Documentation Roadmap

### Completed (2025-11-17)
- ✅ Major reorganization into numbered folders
- ✅ Updated README with role-based navigation
- ✅ Created comprehensive INDEX
- ✅ Standardized file naming (kebab-case)
- ✅ Created archive/ and issues/ directories for better organization
- ✅ Archived historical security audits and completion reports
- ✅ Organized issue-specific documentation into dedicated folders
- ✅ Consolidated BGAI implementation research docs
- ✅ Cleaned up testing documentation

### Planned (Q1 2025)
- [ ] Add missing ADRs (ADR-005: Vector DB, ADR-006: Caching)
- [ ] Create developer onboarding video walkthrough
- [ ] Add interactive architecture diagrams (clickable)
- [ ] Create API client SDK documentation

### Backlog
- [ ] Translations (English versions of Italian docs)
- [ ] API examples in more languages (Go, Ruby, Java)
- [ ] Video tutorials for complex topics (RAG, DDD)
- [ ] Documentation search tool improvements
- [ ] Automated link checking in CI/CD

---

## 🔗 Related Documentation

### External Resources

**Research Foundation**:
- [AI Systems for Board Games 2025](./10-knowledge-base/ai-systems-board-games-state-of-art-2025-it.md) - Comprehensive landscape analysis (Italian)

**Academic Papers**:
- Mills 2013: "Learning Board Game Rules from an Instruction Manual"
- IEEE CoG 2024: "Grammar-based Game Description Generation using LLMs"

**Community**:
- La Tana dei Goblin: https://www.gdt.it/forum (Italian community)
- BoardGameGeek: https://boardgamegeek.com (global database)

**Technology**:
- Qdrant Docs: https://qdrant.tech/documentation/
- LangChain: https://python.langchain.com
- OpenRouter: https://openrouter.ai/docs

---

## 📝 Document Maintenance

### Review Schedule

**Monthly** (every 30 days):
- Update INDEX with new documents
- Check for broken links
- Update statistics (file count, page count)

**Quarterly** (every 3 months):
- Review Strategic Roadmap, adjust timelines
- Update Architecture Overview with new patterns
- Review API Specification for breaking changes
- Update Business Plan with revenue actuals

**Annual** (every 12 months):
- Comprehensive documentation audit
- Archive outdated documents
- Update competitive landscape
- Review and update ADRs

---

## 🤝 Contributing

See [README.md - Contributing Documentation](./README.md#-contributing-documentation) for full guidelines.

**Quick checklist**:
- [ ] File in correct numbered folder (00-10)
- [ ] Kebab-case naming
- [ ] Added to README and INDEX
- [ ] Internal links tested
- [ ] Metadata updated
- [ ] PR with `[DOCS]` prefix

---

## 📧 Contact

**Documentation Issues**:
- GitHub Issues: Tag with `documentation`

**Technical Questions**:
- Email: engineering@meepleai.dev

**Business Inquiries**:
- Email: business@meepleai.dev

---

**Legend**: ⭐ = Essential reading, highly recommended

**Index Metadata**:
- **Version**: 2.1
- **Maintainer**: Documentation Team
- **Last Updated**: 2025-11-17
- **Next Review**: 2025-12-17
