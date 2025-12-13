# MeepleAI Documentation Index

**Project**: Italian Board Game Rules AI Assistant
**Version**: 3.0 (Post-Consolidation)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Total Documents**: ~90 (consolidated from 140+)

---

## 📋 Documentation Consolidation (2025-12-08)

**Status**: ✅ Phase 1-4 Complete
**Reduction**: 140+ → ~90 files (-36%)
**Strategy**: Delete obsolete, consolidate duplicates, standardize naming

**New Consolidated Guides**:
- 📚 [Comprehensive Testing Guide](./02-development/testing/comprehensive-testing-guide.md) - All test types in one place
- 🧪 [Test Patterns Reference](./02-development/testing/test-patterns-reference.md) - Quick pattern lookup
- 🐳 [Docker Resources Guide](./02-development/docker-resources-guide.md) - Complete resource configuration
- 🎨 [Design System v2.0](./04-frontend/design-system.md) - Consolidated design guide
- 🔒 [Security Patterns](./06-security/security-patterns.md) - All security patterns
- 🏗️ [Infrastructure Overview](./05-operations/infrastructure-overview.md) - Complete infra guide
- 📊 [Prometheus Setup](./05-operations/monitoring/prometheus-setup.md) - Monitoring & alerts
- ⚙️ [Workflow Automation](./05-operations/workflow-automation.md) - n8n workflows

**Details**: [CONSOLIDATION-FINAL-SUMMARY.md](./CONSOLIDATION-FINAL-SUMMARY.md)

---

## Quick Navigation

**New Team Members** — Read in this order:
1. [Quick Start](./00-getting-started/quick-start.md) - Get up and running in 15 minutes
2. [System Architecture](./01-architecture/overview/system-architecture.md) - Understand the technical design
3. [API Specification](./03-api/board-game-ai-api-specification.md) - Learn the API
4. [Testing Strategy](./02-development/testing/board-game-ai-testing-strategy.md) - Quality standards
5. [Deployment Guide](./05-operations/deployment-guide.md) - Complete deployment workflow with Git strategy **NEW**

**Find docs by role** — See [README.md](./README.md#-finding-documentation) for role-based navigation

---

## Documentation Categories

### [00 - Getting Started](./00-getting-started/)
**Audience**: All users, new team members, players
**Purpose**: Quick start guides, onboarding, project overview, user documentation

- [User Guide (IT)](./00-getting-started/user-guide.md) ⭐ **NEW** - Guida completa per giocatori (italiano)
- [Quick Start](./00-getting-started/quick-start.md) - Local setup in 15 minutes
- [Guida Setup Locale (IT)](./00-getting-started/guida-setup-locale.md) - Setup dettagliato in italiano
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

- [**Docker Resources Guide**](./02-development/docker-resources-guide.md) ⭐ **CONSOLIDATED** - Complete Docker resource limits and optimization
- [Backend Developer Guide (IT)](./02-development/backend/GUIDA-SVILUPPATORE-BACKEND.md) - Complete backend guide
- [Frontend Developer Guide (IT)](./02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md) - Complete frontend guide
- [Backend Codebase Analysis](./02-development/analysis-summary.md) - Historical analysis (converted from .txt)

#### Subdirectories
- **[guides/](./02-development/guides/)** - Technical guides (~10 files)
  - [LLM Integration](./02-development/guides/llm-integration-guide.md)
  - [PDF Processing Guide](./02-development/guides/pdf-processing-guide.md)
  - [PDF Processing Troubleshooting](./02-development/guides/pdf-processing-troubleshooting.md)
  - [Local Debugging Guide](./02-development/guides/local-debugging-guide.md)
  - [Unstructured Setup](./02-development/guides/unstructured-setup.md)
  - _...and more guides_

- **[refactoring/](./02-development/refactoring/)** - DDD migration docs (4 files)
  - [Legacy Code Inventory](./02-development/refactoring/legacy-code-inventory.md) (150+ pages)
  - [Legacy Code Dashboard](./02-development/refactoring/legacy-code-dashboard.md)
  - [Implementation Notes](./02-development/refactoring/implementation-notes.md)
  - [Next Steps](./02-development/refactoring/next-steps.md)

- **[implementation/](./02-development/implementation/)** - Implementation findings (2 files)
  - [RAG Service Migration](./02-development/implementation/bgai-023-ragservice-migration.md)
  - [Cost Tracking Verification](./02-development/implementation/bgai-026-cost-tracking.md)

- **[code-review/](./02-development/code-review/)** - Code review checklists and reports (5 files)
  - [Backend Comprehensive Review 2025-11-22](./02-development/code-review/BACKEND-COMPREHENSIVE-REVIEW-2025-11-22.md)
  - [Infrastructure Comprehensive Review 2025-11-22](./02-development/code-review/INFRASTRUCTURE-COMPREHENSIVE-REVIEW-2025-11-22.md)
  - [PDF Processing Checklist](./02-development/code-review/pdf-processing-checklist.md)

- **[testing/](./02-development/testing/)** - Testing documentation (~15 files, consolidated)
  - [Comprehensive Testing Guide](./02-development/testing/comprehensive-testing-guide.md) ⭐ **CONSOLIDATED** - All test types (unit, integration, E2E, manual, performance, visual)
  - [Test Patterns Reference](./02-development/testing/test-patterns-reference.md) ⭐ **CONSOLIDATED** - Quick pattern lookup
  - [Golden Dataset Testing Guide](./02-development/testing/golden-dataset-testing-guide.md) - RAG accuracy validation (Issue #999)
  - [UI Element Identification Guide](./02-development/testing/ui-element-identification-guide.md) - Test selector best practices
  - [Testing Strategy](./02-development/testing/core/testing-strategy.md)
  - [Accessibility Testing](./02-development/testing/accessibility-testing-guide.md)
  - [E2E Contribution Guide](./02-development/testing/e2e-contribution-guide.md)
  - [Page Object Model Architecture](./02-development/testing/pom-architecture-design.md)
  - [Visual Testing Guide](./02-development/testing/visual-testing-guide.md) - Chromatic integration
  - _...and more testing docs_

### [03 - API](./03-api/)
**Audience**: API consumers, frontend developers, integrators
**Purpose**: API specification, endpoints, authentication

- [API Specification](./03-api/board-game-ai-api-specification.md) (40 pages) ⭐
- [LLM Cost Tracking API](./03-api/llm-cost-tracking-api.md)

### [04 - Frontend](./04-frontend/)
**Audience**: Frontend developers, UI/UX designers
**Purpose**: Frontend architecture, design system, performance

- [Architecture](./04-frontend/architecture.md)
- [Accessibility Standards](./04-frontend/accessibility-standards.md)
- [Internationalization Strategy](./04-frontend/internationalization-strategy.md)
- [Performance Requirements](./04-frontend/performance-requirements.md)
- [Shadcn/UI Installation](./04-frontend/shadcn-ui-installation.md)
- [Testing Strategy](./04-frontend/testing-strategy.md)
- [Use Cases](./04-frontend/use-cases.md)

### [05 - Operations](./05-operations/)
**Audience**: DevOps, SRE, on-call engineers
**Purpose**: Deployment, monitoring, incident response, infrastructure

- [**Infrastructure Overview**](./05-operations/infrastructure-overview.md) ⭐ **CONSOLIDATED** - Complete infrastructure guide (Docker Compose, profiles, 17 services)
- [**Deployment Guide**](./05-operations/deployment-guide.md) (50+ pages) - Complete workflow with Git/GitHub integration

#### Subdirectories
- **[deployment/](./05-operations/deployment/)** - Deployment guides (6 files)
  - [Multi-Environment Strategy](./05-operations/deployment/multi-environment-strategy.md)
  - [Frontend Deployment](./05-operations/deployment/frontend-deployment.md)
  - [Disaster Recovery](./05-operations/deployment/disaster-recovery.md)
  - [Traefik Guide](./05-operations/deployment/traefik-guide.md) ⭐ **NEW** - Reverse proxy setup (Issue #703)
  - [Traefik Testing](./05-operations/deployment/traefik-testing.md) ⭐ **NEW** - Testing procedures
  - [Traefik Production](./05-operations/deployment/traefik-production.md) ⭐ **NEW** - Production checklist
  - **[infrastructure/](./05-operations/deployment/infrastructure/)** - Infrastructure provider comparison
    - [EU Hosting Providers Comparison](./05-operations/deployment/infrastructure/EU_HOSTING_COMPARISON_2025.md) - Top 10 EU providers

- **[runbooks/](./05-operations/runbooks/)** - Incident runbooks (~10 files)
  - [High Error Rate](./05-operations/runbooks/high-error-rate.md)
  - [Slow Performance](./05-operations/runbooks/slow-performance.md)
  - [High Memory Usage](./05-operations/runbooks/high-memory-usage.md)
  - [Dependency Down](./05-operations/runbooks/dependency-down.md)
  - [Infrastructure Monitoring](./05-operations/runbooks/infrastructure-monitoring.md) ⭐ **NEW** (Issue #705)
  - [RAG Evaluation Pipeline](./05-operations/runbooks/rag-evaluation-pipeline.md)
  - [Prompt Management Deployment](./05-operations/runbooks/prompt-management-deployment.md)
  - _...and more runbooks_

- **[monitoring/](./05-operations/monitoring/)** - Observability (~5 files)
  - [Prometheus Setup](./05-operations/monitoring/prometheus-setup.md) ⭐ **CONSOLIDATED** - Complete monitoring guide (40+ alerts)
  - [Prometheus LLM Queries](./05-operations/monitoring/prometheus-llm-queries.md) - Query examples
  - [Grafana LLM Cost Dashboard](./05-operations/monitoring/grafana-llm-cost-dashboard.md) - Cost tracking
  - [Slack Notifications](./05-operations/monitoring/slack-notifications.md) - Alert routing
  - [Logging & Audit](./05-operations/monitoring/logging-and-audit.md)

- **[backup/](./05-operations/backup/)** - Backup & recovery (2 files) ⭐ **NEW** (Issue #704)
  - [Backup Strategy](./05-operations/backup/backup-strategy.md) - Automated backups
  - [Restore Procedures](./05-operations/backup/restore-procedures.md) - Recovery steps

- **[Workflow Automation](./05-operations/workflow-automation.md)** ⭐ **CONSOLIDATED** - n8n workflows (13 templates + production)

### [06 - Security](./06-security/)
**Audience**: Security team, compliance, developers
**Purpose**: Security audits, vulnerability remediation, best practices

- [**Security Patterns**](./06-security/security-patterns.md) ⭐ **CONSOLIDATED** - All security patterns (resource management, credentials, validation, encoding)
- [Security Audit 2025-11-04](./06-security/security-audit-2025-11-04.md)
- [Environment Variables Production Guide](./06-security/environment-variables-production.md) ⭐ **NEW**
- [Code Scanning Remediation](./06-security/code-scanning-remediation-summary.md)
- [OAuth Security](./06-security/oauth-security.md)
- [Log Forging Prevention](./06-security/log-forging-prevention.md)
- [Disposable Resource Leak Remediation](./06-security/disposable-resource-leak-remediation.md)
- [Generic Catch Analysis](./06-security/generic-catch-analysis.md)
- [Null Reference Remediation](./06-security/null-reference-remediation.md)
- [Sensitive Info Exposure Fix](./06-security/sensitive-info-exposure-fix-733.md)

### [07 - Project Management](./07-project-management/)
**Audience**: Project managers, product owners, leadership
**Purpose**: Planning, roadmap, team organization, sprint tracking

#### Subdirectories
- **[planning/](./07-project-management/planning/)** - Implementation plans (7 files)
  - [Backend Implementation Plan](./07-project-management/planning/backend-implementation-plan.md)
  - [Frontend Implementation Plan](./07-project-management/planning/frontend-implementation-plan.md)
  - [Issue Status Tracker](./07-project-management/planning/issue-status-tracker.md)
  - _...and 4 more planning docs_

- **[roadmap/](./07-project-management/roadmap/)** - Strategic roadmap (1 file)
  - [ROADMAP.md](./07-project-management/roadmap/ROADMAP.md) (50 pages) ⭐ - Active roadmap with 136 open issues

- **[organization/](./07-project-management/organization/)** - Team & execution (12 files)
  - [Execution Calendar](./07-project-management/organization/board-game-ai-execution-calendar.md)
  - [Sprint Overview](./07-project-management/organization/board-game-ai-sprint-overview.md)
  - [Onboarding Guide](./07-project-management/organization/onboarding_guide.md)
  - [Solo Developer Execution Plan](./07-project-management/organization/solo-developer-execution-plan.md)
  - _...and 8 more org docs_

- **[completion-reports/](./07-project-management/completion-reports/)** - Phase reports (1 file)
  - [Phase 1a Completion](./07-project-management/completion-reports/phase-1a-completion-report.md)

### [08 - Business](./08-business/)
**Audience**: Investors, founders, business stakeholders
**Purpose**: Business strategy, revenue model, market analysis

- [Business Plan](./08-business/board-game-ai-business-plan.md) (35 pages) ⭐

#### Subdirectories
- **[cost-analysis/](./08-business/cost-analysis/)** - Infrastructure cost analysis & budget planning ⭐ **NEW**
  - [Cost Analysis 2025](./08-business/cost-analysis/COST_ANALYSIS_2025.md) (600+ pages) - Full-scale analysis (10K+ users)
  - [Cost Analysis Optimized 2025](./08-business/cost-analysis/COST_ANALYSIS_OPTIMIZED_2025.md) (800+ pages) - Right-sized for 2-5K users ⭐ **RECOMMENDED**
  - [Cost Data Export](./08-business/cost-analysis/cost-analysis-data.csv) - CSV export for analysis
  - [Optimized Cost Data Export](./08-business/cost-analysis/cost-analysis-optimized-2025.csv) - CSV export

### [09 - Research](./09-research/)
**Audience**: Research team, technical leadership
**Purpose**: Research findings, competitive analysis, experiments

- [Ollama Quality Findings](./09-research/bgai-016-ollama-quality-findings.md)

### [10 - Knowledge Base](./10-knowledge-base/)
**Audience**: All users
**Purpose**: External references, research papers, wiki content

- [AI Systems for Board Games 2025](./10-knowledge-base/Sistemi%20AI%20per%20arbitrare%20giochi%20da%20tavolo%20%20stato%20dell'arte%202025.md) (Italian)
- [Game Scraper](./10-knowledge-base/game-scraper.md) ⭐ **NEW** - BoardGameGeek scraper documentation
- [references/](./10-knowledge-base/references/) - External references
  - [OpenRouter Models Reference](./10-knowledge-base/references/openrouter-models-reference.wiki)

---

## Key Concepts & Glossary

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

## Documentation Statistics

- **Total Files**: ~95 markdown documents (consolidated from 116)
- **Total Pages**: ~750 pages (estimated)
- **Categories**: 11 numbered folders (00-10)
- **Recent Cleanup**: Removed 20+ duplicate/obsolete files (Nov 2025)
- **Largest Docs**:
  - Legacy Code Inventory: 150+ pages
  - System Architecture: 60+ pages
  - Strategic Roadmap: 50 pages
  - API Specification: 40 pages
  - Business Plan: 35 pages
  - Deployment Guide: 50+ pages
  - Testing Strategy: 30 pages

---

## Documentation Roadmap

### Completed (2025-11-13)
- ✅ Major reorganization into numbered folders
- ✅ Updated README with role-based navigation
- ✅ Created comprehensive INDEX
- ✅ Standardized file naming (kebab-case)

### Planned (Q1 2025)
- [ ] Add missing ADRs (ADR-005: Vector DB, ADR-006: Caching)
- [ ] Create developer onboarding video walkthrough
- [ ] Add interactive architecture diagrams (clickable)
- [ ] Consolidate testing docs (reduce from 21 to ~10 files)
- [ ] Create API client SDK documentation

### Backlog
- [ ] Translations (English versions of Italian docs)
- [ ] API examples in more languages (Go, Ruby, Java)
- [ ] Video tutorials for complex topics (RAG, DDD)
- [ ] Documentation search tool improvements
- [ ] Automated link checking in CI/CD

---

## Related Documentation

### External Resources

**Research Foundation**:
- [AI Systems for Board Games 2025](./10-knowledge-base/Sistemi%20AI%20per%20arbitrare%20giochi%20da%20tavolo%20%20stato%20dell'arte%202025.md) - Comprehensive landscape analysis

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

## Document Maintenance

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

## Contributing

See [README.md - Contributing Documentation](./README.md#-contributing-documentation) for full guidelines.

**Quick checklist**:
- [ ] File in correct numbered folder (00-10)
- [ ] Kebab-case naming
- [ ] Added to README and INDEX
- [ ] Internal links tested
- [ ] Metadata updated
- [ ] PR with `[DOCS]` prefix

---

## Contact

**Documentation Issues**:
- GitHub Issues: Tag with `documentation`

**Technical Questions**:
- Email: engineering@meepleai.dev

**Business Inquiries**:
- Email: business@meepleai.dev

---

**Legend**: ⭐ = Essential reading, highly recommended

**Index Metadata**:
- **Version**: 2.2
- **Maintainer**: Documentation Team
- **Last Updated**: 2025-12-13T10:59:23.970Z
- **Next Review**: 2025-12-24
- **Recent Changes**: Added User Guide (Italian) for end users (BGAI-084)






