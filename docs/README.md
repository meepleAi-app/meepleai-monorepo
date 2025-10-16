# MeepleAI Documentation

This directory contains technical documentation for the MeepleAI monorepo, organized into guides, technical designs, and issue resolutions.

## Documentation Structure

- **[guide/](./guide/)** - User guides and how-to documentation
- **[technic/](./technic/)** - Technical design documents and architecture decisions
- **[issue/](./issue/)** - Issue resolution documentation and implementation summaries
- **Root docs/** - Core documentation (schema, roadmap, features, observability)

---

## 📖 User Guides

### Getting Started
- **[Getting Started Guide](./guide/getting-started.md)** - Step-by-step guide for local setup and testing
- **[AI Agents Guide](./guide/agents-guide.md)** - Guide for AI coding assistants (conventions, workflow, prompts)
- **[Testing Guide](./guide/testing-guide.md)** - BDD-style test naming conventions and best practices

### Deployment & Configuration
- **[Deployment Checklist](./guide/deployment-checklist.md)** - Quick checklist for production deployment
- **[n8n Integration Guide](./guide/n8n-integration-guide.md)** - n8n workflow automation integration
- **[n8n Deployment Guide](./guide/n8n-deployment.md)** - Deploying and configuring n8n workflows
- **[n8n User Guide (IT)](./guide/n8n-user-guide-it.md)** - Guida utente n8n in italiano

---

## 🏗️ Technical Documentation

### Architecture & Design
- **[Database Schema](./database-schema.md)** - Complete database schema reference
- **[Observability Guide](./observability.md)** - Health checks, logging, Seq dashboard, correlation IDs (OPS-01)
- **[OpenTelemetry Design](./ops-02-opentelemetry-design.md)** - Distributed tracing & metrics architecture (OPS-02)
- **[SECURITY.md](./SECURITY.md)** - Security policies, secret management, key rotation procedures

### Design Documents (technic/)
- **[Chess UI Design](./technic/chess-05-ui-design.md)** - Chess game UI/UX design specifications
- **[n8n Webhook Chess Design](./technic/n8n-webhook-chess-design.md)** - Chess agent webhook integration design
- **[n8n Webhook Explain Design](./technic/n8n-webhook-explain-design.md)** - Explain agent webhook integration design
- **[PDF Processing Design](./technic/pdf-processing-design.md)** - PDF ingestion and processing architecture
- **[Performance Optimization](./technic/performance-optimization.md)** - System performance optimization strategies

---

## 🔧 Issue Resolutions & Implementation

### Authentication & Security
- **[AUTH-03: Session Migration](./issue/auth-03-session-migration.md)** - User session management migration guide
- **[SEC-04: Security Audit Report](./issue/sec-04-audit-report.md)** - Security audit findings and resolutions

### Feature Implementations
- **[CHESS-03: Knowledge Base](./issue/chess-03-knowledge-base.md)** - Chess knowledge base implementation
- **[CHESS-04: Agent Implementation](./issue/chess-04-agent-implementation.md)** - Chess AI agent implementation details
- **[EDIT-01: Testing Summary](./issue/edit-01-testing-summary.md)** - Editor testing implementation summary
- **[PDF-03: Table Extraction](./issue/pdf-03-table-extraction.md)** - PDF table extraction implementation
- **[RULE-02: Implementation Summary](./issue/rule-02-implementation-summary.md)** - Rule specification implementation

### Performance & UI
- **[PERF-02: Rate Limit Config](./issue/perf-02-rate-limit-config.md)** - Rate limiting configuration and code review
- **[UI: Landing Page Redesign](./issue/ui-landing-page-redesign.md)** - Landing page redesign documentation

---

## 📊 Project Planning & Features

- **[Roadmap](./roadmap.md)** - Epic breakdown and 6-month development roadmap
- **[Features](./features.md)** - Comprehensive feature list and capabilities
- **[Epic Structure](./meepleai_epic_structure.md)** - High-level project organization and epic breakdown

---

## 📈 Testing & Quality

- **[Code Coverage Guide](./code-coverage.md)** - Comprehensive guide for measuring, tracking, and monitoring test coverage
  - How to run coverage locally
  - Coverage tools and formats
  - CI/CD integration options
  - Best practices and troubleshooting

- **[Coverage Baseline Estimate](./coverage-baseline-estimate.md)** - Current test suite analysis and coverage baseline
  - 313 tests across 39 test files
  - Estimated 75-85% line coverage
  - Measurement plan and recommendations

- **[Codecov Setup Guide](./codecov-setup.md)** - Step-by-step instructions for Codecov integration
  - Account creation and repository setup
  - GitHub secrets configuration
  - Coverage badge and monitoring

- **[Security Scanning](./security-scanning.md)** - CI security scanning guide
  - CodeQL SAST configuration
  - Dependency vulnerability scanning
  - .NET security analyzers

---

## 🔍 Reference Documentation

- **[Tenant Reference Audit](./tenant-reference-audit.md)** - Analysis of tenant/partition references in codebase
  - Legacy multi-tenancy review
  - Migration to shared context

---

## Quick Links

### For Developers
- **Getting Started**: See [Getting Started Guide](./guide/getting-started.md) for local setup
- **Architecture**: See [../CLAUDE.md](../CLAUDE.md) for complete development guide
- **Running Tests**: `dotnet test` (API) or `pnpm test` (Web)
- **Measuring Coverage**: `pwsh tools/measure-coverage.ps1`

### For Architects
- **System Overview**: [../CLAUDE.md - Architecture](../CLAUDE.md#architecture)
- **Database Schema**: [database-schema.md](./database-schema.md)
- **Observability**: [observability.md](./observability.md)
- **OpenTelemetry**: [ops-02-opentelemetry-design.md](./ops-02-opentelemetry-design.md)

### For QA/Testing
- **Test Strategy**: [guide/testing-guide.md](./guide/testing-guide.md)
- **Coverage Reports**: [code-coverage.md](./code-coverage.md)
- **CI/CD Pipeline**: [../CLAUDE.md - CI/CD](../CLAUDE.md#cicd)

---

## Contributing Documentation

When adding new documentation:

1. **Choose the right location**:
   - `guide/` - User-facing guides and how-to documentation
   - `technic/` - Technical design documents and architecture decisions
   - `issue/` - Issue resolution documentation and implementation summaries
   - Root `docs/` - Core project documentation (schema, features, roadmap)

2. **Create a descriptive filename** using kebab-case (e.g., `feature-name-guide.md`)
3. **Add an entry** to this README under the appropriate category
4. **Include a clear purpose/summary** for the document
5. **Follow existing documentation structure** and tone
6. **Add cross-references** to related docs where relevant

---

## Documentation Standards

- **Format**: GitHub-flavored Markdown
- **Style**: Clear, concise, with practical examples
- **Code Blocks**: Always specify language for syntax highlighting
- **Structure**: Use hierarchical headings (H1 → H6)
- **Links**: Use relative paths for internal docs
- **Examples**: Include both good and bad examples where helpful
- **File Naming**: Use kebab-case (lowercase with hyphens)

---

## Last Updated

This index was last updated: 2025-10-16
