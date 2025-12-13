# Documentation Consolidation Plan

**Date**: 2025-12-08
**Status**: In Progress
**Goal**: Reduce documentation from 115+ files to ~80 active files with improved organization and navigation

---

## Executive Summary

The MeepleAI documentation has grown organically to over 115 files across 60+ directories. This consolidation plan aims to:

1. **Archive outdated documentation** (completed features, old roadmaps, session logs)
2. **Merge duplicate content** (testing guides, resource docs, design system)
3. **Standardize naming conventions** (kebab-case for general docs, consistent date formats)
4. **Improve navigation** (updated INDEX.md, cross-references, quick-reference cards)

**Expected Outcome**: Better discoverability, reduced redundancy, clearer structure for developers.

---

## Phase 1: Archive Outdated Documentation

### Files to Move to `docs/archive/`

#### Completed Features / Historical Records
- `07-project-management/roadmap/ROADMAP-v19-backup.md` - Old roadmap version
- `07-project-management/completion-reports/sprint-4/*` - Sprint 4 completion reports (historical)
- `04-frontend/improvements/worktree-visual-guide.md` - Completed feature documentation
- `02-development/refactoring/legacy-code-dashboard.md` - DDD migration 100% complete
- `02-development/refactoring/legacy-code-inventory.md` - DDD migration 100% complete
- `02-development/testing/DEMO-USERS-REMOVED.md` - Historical change record
- `docs/issues/test-quality-review-2025-11-20/*` - Completed issue analysis

#### Session Logs / Temporary Docs
- `05-operations/analyzer-cleanup-session-2025-11-30.md` - Session log (should be in memory, not docs)
- `02-development/refactoring/implementation-notes.md` - Temporary implementation notes

**Action**: Move to `docs/archive/YYYY-MM/` organized by month

---

## Phase 2: Merge Duplicate Documentation

### Testing Documentation (High Priority)

**Current State**: Testing docs scattered across 3 locations:
- `02-development/testing/` (50+ files)
- `04-frontend/testing-strategy.md`
- `docs/testing/` (5 files)

**Consolidation**:

1. **Merge Testing Guides**:
   ```
   MERGE:
   - 02-development/testing/test-writing-guide.md
   - 02-development/testing/manual-testing-guide.md
   - 02-development/testing/specialized/manual-testing-guide.md (duplicate)

   INTO: 02-development/testing/comprehensive-testing-guide.md

   STRUCTURE:
   - Introduction
   - Testing Philosophy
   - Unit Testing (Jest, xUnit)
   - Integration Testing (Testcontainers)
   - E2E Testing (Playwright)
   - Manual Testing Procedures
   - Performance Testing (k6)
   - Visual Regression (Chromatic)
   ```

2. **Consolidate Test Pattern Docs**:
   ```
   MERGE:
   - 02-development/testing/test-patterns.md
   - 02-development/testing/e2e-patterns.md
   - 02-development/testing/frontend/testing-react-19-patterns.md

   INTO: 02-development/testing/test-patterns-reference.md
   ```

3. **Move Frontend Testing to Main Testing Dir**:
   ```
   MOVE:
   - 04-frontend/testing-strategy.md → 02-development/testing/frontend/
   ```

### Docker Resource Documentation (Medium Priority)

**Current State**: 3 separate Docker resource docs with overlapping content

**Consolidation**:
```
MERGE:
- 02-development/docker-compose-resource-limits.md
- 02-development/docker-resource-limits-faq.md
- 02-development/docker-resource-limits-quick-reference.md
- 02-development/DOCKER-RESOURCE-LIMITS-SOURCES.md

INTO: 02-development/docker-resources-guide.md

STRUCTURE:
- Overview
- Resource Limits Configuration
- Quick Reference Table
- FAQ
- Troubleshooting
- External Sources
```

### Design System Documentation (High Priority)

**Current State**: Multiple design system docs with versioning confusion

**Consolidation**:
```
MERGE:
- 04-frontend/design-system.md
- 04-frontend/design-system-2.0.md
- 04-frontend/design-tokens-migration-guide.md

INTO: 04-frontend/design-system.md (single source of truth)

STRUCTURE:
- Current Design System (v2.0)
- Design Tokens
- Component Library
- Migration Guide (from v1 → v2)
- Usage Examples
```

### Security Documentation (Medium Priority)

**Current State**: Multiple remediation docs for specific CodeQL issues

**Consolidation**:
```
MERGE (into security-patterns.md):
- 06-security/disposable-resource-leak-remediation.md
- 06-security/hardcoded-credentials-remediation.md
- 06-security/null-reference-remediation.md
- 06-security/log-forging-prevention.md
- 06-security/incomplete-sanitization-prevention.md
- 06-security/regex-sanitization-guide.md

INTO: 06-security/security-patterns.md

KEEP SEPARATE:
- 06-security/codeql-false-positive-management.md (operational)
- 06-security/oauth-security.md (comprehensive guide)
- 06-security/security-testing-strategy.md (testing-specific)
```

---

## Phase 3: Standardize Naming Conventions

### Naming Rules

1. **General Documentation**: `kebab-case.md`
   - Example: `testing-guide.md`, `api-specification.md`

2. **Index Files**: `UPPERCASE.md`
   - Example: `README.md`, `INDEX.md`

3. **Issue-Specific Docs**: `issue-NNNN-description.md`
   - Example: `issue-1089-implementation-summary.md`

4. **Dated Documents**: `YYYY-MM-DD-description.md`
   - Example: `2025-11-20-completion-report.md`

5. **Completion Reports**: `completion-YYYY-MM-DD.md` or `sprint-N-completion-report.md`

### Files to Rename

```
RENAME:
- 02-development/ANALYSIS-SUMMARY.txt → 02-development/analysis-summary.md (+ convert to MD)
- 02-development/DOCKER-RESOURCE-LIMITS-INDEX.md → 02-development/docker-resources-index.md
- 04-frontend/DOCUMENTATION-SUMMARY.md → 04-frontend/documentation-index.md
- Various TEST_FIXES_*.md → test-fixes-YYYY-MM-DD.md
```

---

## Phase 4: Improve Navigation & Cross-References

### Updated INDEX.md Structure

```markdown
# MeepleAI Documentation Index

**Total Documents**: ~80 (after consolidation)
**Last Updated**: 2025-12-13T10:59:23.970Z

## Quick Start

- [Getting Started Guide](00-getting-started/quick-start.md)
- [Local Setup Guide (Italian)](00-getting-started/guida-setup-locale.md)
- [Executive Summary](00-getting-started/executive-summary.md)

## For Developers

### Backend Development
- **Main Guide**: [Backend Developer Guide (Italian)](02-development/backend/GUIDA-SVILUPPATORE-BACKEND.md)
- [AI Provider Integration](02-development/ai-provider-integration.md)
- [PDF Processing Guide](02-development/guides/pdf-processing-guide.md)
- [RAG Validation Pipeline](02-development/rag-validation-pipeline.md)

### Frontend Development
- **Main Guide**: [Frontend Developer Guide (Italian)](02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md)
- [Design System](04-frontend/design-system.md)
- [Storybook Guide](04-frontend/storybook-guide.md)
- [React 19 Best Practices](04-frontend/react19-nextjs16-best-practices.md)

### Testing
- **Main Guide**: [Comprehensive Testing Guide](02-development/testing/comprehensive-testing-guide.md)
- [Test Patterns Reference](02-development/testing/test-patterns-reference.md)
- [E2E Testing](02-development/testing/frontend/e2e/)
- [Performance Testing (k6)](02-development/testing/performance/k6-performance-testing.md)
- [Visual Regression (Chromatic)](02-development/testing/visual-testing-guide.md)

## Architecture

### Overview
- [System Architecture](01-architecture/overview/system-architecture.md)
- [Product Specification](01-architecture/overview/product-specification.md)
- [Bounded Contexts](01-architecture/diagrams/bounded-contexts-interactions.md)

### Architecture Decision Records (ADRs)
- [ADR Index](01-architecture/adr/README.md)
- [ADR-001: Hybrid RAG](01-architecture/adr/adr-001-hybrid-rag.md)
- [ADR-016: Advanced PDF Embedding Pipeline](01-architecture/adr/adr-016-advanced-pdf-embedding-pipeline.md)

### DDD Implementation
- [DDD Quick Reference](01-architecture/ddd/quick-reference.md)

## API Documentation

- [Board Game AI API Specification](03-api/board-game-ai-api-specification.md)
- [AI Provider Configuration](03-api/ai-provider-configuration.md)
- [PDF Processing API](03-api/pdf-processing-api.md)

## Operations

### Deployment
- [Deployment Guide](05-operations/deployment-guide.md)
- [Multi-Environment Strategy](05-operations/deployment/multi-environment-strategy.md)
- [Disaster Recovery](05-operations/deployment/disaster-recovery.md)

### Monitoring
- [HyperDX Integration](01-architecture/adr/adr-015-hyperdx-observability.md)
- [Prometheus Queries (LLM)](05-operations/monitoring/prometheus-llm-queries.md)
- [Grafana Dashboards](05-operations/monitoring/grafana-llm-cost-dashboard.md)

### Runbooks
- **Index**: [Runbooks Index](05-operations/runbooks/README.md)
- [High Error Rate](05-operations/runbooks/high-error-rate.md)
- [Slow Performance](05-operations/runbooks/slow-performance.md)
- [RAG Evaluation Pipeline](05-operations/runbooks/rag-evaluation-pipeline.md)
- [Infrastructure Monitoring](05-operations/runbooks/infrastructure-monitoring.md)

### Backup & Recovery
- [Backup Strategy](05-operations/backup/backup-strategy.md)
- [Restore Procedures](05-operations/backup/restore-procedures.md)

## Security

- [Security Patterns](06-security/security-patterns.md)
- [OAuth Security](06-security/oauth-security.md)
- [2FA Security Assessment](06-security/2fa-security-assessment-issue-576.md)
- [Environment Variables (Production)](06-security/environment-variables-production.md)
- [CodeQL False Positive Management](06-security/codeql-false-positive-management.md)

## Project Management

### Roadmap & Planning
- [Current Roadmap](07-project-management/roadmap/ROADMAP.md)
- [Quick Reference: Top 10 Issues](07-project-management/roadmap/quick-reference-top-10-issues.md)
- [Issue Execution Plan](07-project-management/roadmap/ISSUE-EXECUTION-PLAN.md)

### Tracking
- [GitHub Issues Consolidation](07-project-management/tracking/github-issues-consolidation-summary.md)
- [Integration Tests Known Issues](07-project-management/tracking/integration-tests-known-issues.md)

## Business & Research

- [Business Plan](08-business/board-game-ai-business-plan.md)
- [Cost Analysis 2025](08-business/cost-analysis/COST_ANALYSIS_OPTIMIZED_2025.md)
- [AI Systems State of Art 2025](10-knowledge-base/ai-systems-board-games-state-of-art-2025-it.md)

## Archive

- [Archived Documentation](archive/) - Completed features, old roadmaps, historical records

---

## Document Categories

| Category | Path | Description |
|----------|------|-------------|
| **Getting Started** | `00-getting-started/` | Quick start, setup guides |
| **Architecture** | `01-architecture/` | ADRs, DDD, diagrams, system design |
| **Development** | `02-development/` | Backend, frontend, testing guides |
| **API** | `03-api/` | API specifications and references |
| **Frontend** | `04-frontend/` | UI/UX, design system, component library |
| **Operations** | `05-operations/` | Deployment, monitoring, runbooks |
| **Security** | `06-security/` | Security patterns, audits, compliance |
| **Project Management** | `07-project-management/` | Roadmaps, tracking, planning |
| **Business** | `08-business/` | Business plans, cost analysis |
| **Research** | `09-research/` | Research findings and experiments |
| **Knowledge Base** | `10-knowledge-base/` | References, PDFs, external resources |

---

## Quick Reference Cards

### Common Development Tasks

| Task | Documentation |
|------|---------------|
| Set up local environment | [Local Setup Guide](00-getting-started/guida-setup-locale.md) |
| Write backend code | [Backend Developer Guide](02-development/backend/GUIDA-SVILUPPATORE-BACKEND.md) |
| Write frontend code | [Frontend Developer Guide](02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md) |
| Add tests | [Testing Guide](02-development/testing/comprehensive-testing-guide.md) |
| Debug PDF processing | [PDF Troubleshooting](02-development/guides/pdf-processing-troubleshooting.md) |
| Configure AI providers | [AI Provider Integration](02-development/ai-provider-integration.md) |

### Operations Tasks

| Task | Documentation |
|------|---------------|
| Deploy to production | [Deployment Guide](05-operations/deployment-guide.md) |
| Investigate errors | [High Error Rate Runbook](05-operations/runbooks/high-error-rate.md) |
| Monitor performance | [Prometheus Queries](05-operations/monitoring/prometheus-llm-queries.md) |
| Backup/restore data | [Backup Strategy](05-operations/backup/backup-strategy.md) |

---

## Maintenance

- **Review Frequency**: Monthly
- **Owner**: Engineering Lead
- **Last Major Update**: 2025-12-08 (Consolidation)
```

### Cross-Reference Strategy

Add "Related Documentation" sections to major documents:

**Example for `02-development/testing/comprehensive-testing-guide.md`**:
```markdown
## Related Documentation

- [Test Patterns Reference](./test-patterns-reference.md) - Common testing patterns
- [Backend Testing](./backend/test-architecture.md) - Backend-specific patterns
- [E2E Testing Guide](./frontend/e2e/README-chat-animations.md) - Playwright patterns
- [CI/CD Pipeline](../guides/test-automation-pipeline-guide.md) - Automated testing
```

---

## Implementation Checklist

### Phase 1: Archive (Immediate)
- [ ] Create `docs/archive/2025-12/` directory
- [ ] Move 8 completed/outdated docs to archive
- [ ] Update CLAUDE.md to remove DDD refactoring references
- [ ] Update INDEX.md to reflect archived docs

### Phase 2: Merge Duplicates (Week 1)
- [ ] Consolidate testing documentation (3 guides → 1)
- [ ] Merge Docker resource docs (4 docs → 1)
- [ ] Consolidate design system docs (3 docs → 1)
- [ ] Merge security remediation docs into patterns

### Phase 3: Standardize Naming (Week 2)
- [ ] Rename 5+ files to follow kebab-case convention
- [ ] Convert ANALYSIS-SUMMARY.txt to markdown
- [ ] Standardize date formats in filenames

### Phase 4: Navigation (Week 2)
- [ ] Update INDEX.md with new structure
- [ ] Add cross-references to 10+ major documents
- [ ] Create quick-reference cards for common tasks
- [ ] Verify all internal links work

### Validation
- [ ] All internal links functional
- [ ] No duplicate content remaining
- [ ] Naming conventions consistent
- [ ] Archive directory organized
- [ ] INDEX.md comprehensive and accurate

---

## Success Metrics

| Metric | Before | Target | Current |
|--------|--------|--------|---------|
| **Total Active Docs** | 115 | 80 | 115 |
| **Duplicate Content** | ~15 files | 0 | 15 |
| **Broken Links** | Unknown | 0 | Unknown |
| **Navigation Clarity** | 6/10 | 9/10 | 6/10 |
| **Time to Find Info** | ~5 min | <2 min | ~5 min |

---

## Notes

- Keep ADRs chronological (don't consolidate)
- Preserve historical context in archive
- Update CLAUDE.md only for structural changes
- Test all internal links after consolidation
- Consider automated link checking in CI

---

**Status**: 📝 Plan created, ready for implementation
**Next Step**: Create archive directory and begin Phase 1

