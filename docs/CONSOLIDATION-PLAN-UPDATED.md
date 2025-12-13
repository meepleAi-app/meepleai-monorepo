# Documentation Consolidation Plan - UPDATED

**Date**: 2025-12-08 (Updated)
**Status**: Ready for Execution
**Strategy**: ELIMINATE obsolete docs (no archive)
**Scope**: ALL .md files in repository

---

## Key Changes from Original Plan

1. ❌ **NO ARCHIVE** - Delete obsolete files directly
2. ✅ **Complete Scope** - Include ALL .md files (root, infra, .github, docs)
3. ✅ **Aggressive Cleanup** - Eliminate temporary/session files
4. ✅ **Consolidate infra/** - Move infra docs to docs/05-operations/

---

## Complete Repository Analysis

### Files Found (by location)

#### 📁 Root Directory (9 files)
```
✅ KEEP:
- README.md (main project readme)
- SECURITY.md (security policy)
- CONTRIBUTING.md (contributor guide)
- CLAUDE.md (AI assistant context)
- .env.README.md (environment variables guide)

❌ DELETE:
- GAME_SCRAPER.md → Move to docs/10-knowledge-base/
- merge-resolution-summary.md (temporary)
- PR-DESCRIPTION.md (temporary template)
- test-split-summary.md (temporary)
```

#### 📁 .github/ (4+ files)
```
✅ KEEP:
- .github/pull_request_template.md (GitHub template)

❌ DELETE OR CONSOLIDATE:
- .github/ISSUE_TEMPLATE/phase2-storybook-coverage.md (old phase)
- .github/ISSUE_TEMPLATES/*.md (consolidate or delete)
  - csp-nonce-implementation.md
  - csp-reporting-endpoint.md
  - hsts-preload-submission.md
```

#### 📁 infra/ (20 files)
```
✅ KEEP & RELOCATE to docs/05-operations/:
- infra/README.md → docs/05-operations/infrastructure-overview.md
- infra/INFRASTRUCTURE.md → merge into above
- infra/traefik/README.md → docs/05-operations/deployment/traefik-guide.md
- infra/traefik/TESTING.md → docs/05-operations/deployment/traefik-testing.md
- infra/traefik/PRODUCTION-CHECKLIST.md → docs/05-operations/deployment/traefik-production.md
- infra/prometheus/README.md → docs/05-operations/monitoring/prometheus-setup.md
- infra/n8n/README.md → docs/05-operations/workflow-automation.md

✅ KEEP IN PLACE (component-specific):
- infra/dashboards/README.md (Grafana dashboards)
- infra/env/README.md (environment config)
- infra/init/README.md (initialization scripts)
- infra/scripts/README.md (utility scripts)
- infra/secrets/README.md (secrets management)

❌ DELETE:
- infra/docs/archive/* (already archived, obsolete)
- infra/experimental/README.md (if empty/outdated)
```

#### 📁 docs/ (115 files - previously analyzed)
```
See Phase 1-4 details below
```

#### 📁 .serena/memories/ (150+ files)
```
⚠️ DO NOT TOUCH - Operational Serena MCP memory
```

---

## UPDATED Phase 1: Delete Obsolete Files ⏱️ 1 Day

### 1.1 Root Directory Cleanup
```bash
# Delete temporary files
git rm merge-resolution-summary.md
git rm PR-DESCRIPTION.md
git rm test-split-summary.md

# Move game scraper to knowledge base
git mv GAME_SCRAPER.md docs/10-knowledge-base/game-scraper.md
```

### 1.2 GitHub Templates Cleanup
```bash
# Review and delete old phase-specific templates
git rm .github/ISSUE_TEMPLATE/phase2-storybook-coverage.md

# Review CSP templates - consolidate or delete
# If still relevant, consolidate into single security template
git rm .github/ISSUE_TEMPLATES/csp-nonce-implementation.md
git rm .github/ISSUE_TEMPLATES/csp-reporting-endpoint.md
git rm .github/ISSUE_TEMPLATES/hsts-preload-submission.md

# Or consolidate:
# cat > .github/ISSUE_TEMPLATE/security-hardening.md
```

### 1.3 docs/ Obsolete Files (NO ARCHIVE)
```bash
# DDD Migration Complete (100%) - DELETE
git rm docs/02-development/refactoring/legacy-code-dashboard.md
git rm docs/02-development/refactoring/legacy-code-inventory.md
git rm docs/02-development/refactoring/implementation-notes.md

# Completed Features - DELETE
git rm docs/04-frontend/improvements/worktree-visual-guide.md
git rm docs/02-development/testing/DEMO-USERS-REMOVED.md

# Old Roadmap - DELETE
git rm docs/07-project-management/roadmap/ROADMAP-v19-backup.md

# Sprint Completion Reports - DELETE
git rm -r docs/07-project-management/completion-reports/sprint-4/

# Test Quality Reviews - DELETE
git rm -r docs/issues/test-quality-review-2025-11-20/

# Session Logs - DELETE
git rm docs/05-operations/analyzer-cleanup-session-2025-11-30.md
```

### 1.4 infra/docs/archive - DELETE
```bash
# Delete entire infra archive (already obsolete)
git rm -r infra/docs/archive/
```

**Commit**: `docs: delete obsolete documentation (Phase 1)`

---

## Phase 2: Consolidate infra/ Documentation ⏱️ 2 Days

### 2.1 Infrastructure Overview
```bash
# Merge infra README and INFRASTRUCTURE.md
cat > docs/05-operations/infrastructure-overview.md << 'EOF'
# MeepleAI Infrastructure Overview

## Architecture
[... merge from infra/README.md ...]

## Components
[... merge from infra/INFRASTRUCTURE.md ...]

## Docker Compose Profiles
[... current profile documentation ...]

## Component-Specific Docs
- [Traefik Reverse Proxy](deployment/traefik-guide.md)
- [Prometheus Monitoring](monitoring/prometheus-setup.md)
- [n8n Workflow Automation](workflow-automation.md)
EOF

# Delete originals
git rm infra/README.md
git rm infra/INFRASTRUCTURE.md
```

### 2.2 Traefik Documentation
```bash
# Move Traefik docs to operations
git mv infra/traefik/README.md docs/05-operations/deployment/traefik-guide.md
git mv infra/traefik/TESTING.md docs/05-operations/deployment/traefik-testing.md
git mv infra/traefik/PRODUCTION-CHECKLIST.md docs/05-operations/deployment/traefik-production.md

# Update cross-references in moved files
```

### 2.3 Monitoring Documentation
```bash
# Consolidate Prometheus docs
cat > docs/05-operations/monitoring/prometheus-setup.md << 'EOF'
# Prometheus Setup & Configuration

## Overview
[... from infra/prometheus/README.md ...]

## Alert Rules
[... from infra/prometheus/alerts/README.md ...]

## Queries
[... link to existing prometheus-llm-queries.md ...]
EOF

# Delete originals
git rm infra/prometheus/README.md
git rm infra/prometheus/alerts/README.md
```

### 2.4 Workflow Automation
```bash
# Consolidate n8n documentation
cat > docs/05-operations/workflow-automation.md << 'EOF'
# n8n Workflow Automation

## Setup
[... from infra/n8n/README.md ...]

## Templates
[... from infra/n8n/templates/README.md ...]

## Workflows
[... from infra/n8n/workflows/README.md ...]
EOF

# Delete originals
git rm infra/n8n/README.md
git rm infra/n8n/templates/README.md
git rm infra/n8n/workflows/README.md
```

**Commit**: `docs: consolidate infra/ documentation into docs/05-operations/ (Phase 2)`

---

## Phase 3: Merge Duplicate docs/ Content ⏱️ 1 Week

### 3.1 Testing Documentation (Day 1-3)
**Same as original plan** - see CONSOLIDATION-QUICK-START.md

### 3.2 Docker Resources (Day 4)
**Same as original plan**

### 3.3 Design System (Day 5)
**Same as original plan**

### 3.4 Security Patterns (Day 6-7)
**Same as original plan**

**Commit**: `docs: consolidate duplicate documentation (Phase 3)`

---

## Phase 4: Standardize Naming ⏱️ 3 Days

**Same as original plan** - see CONSOLIDATION-QUICK-START.md

**Commit**: `docs: standardize naming conventions (Phase 4)`

---

## Phase 5: Navigation & Cross-References ⏱️ 1 Week

### Updated INDEX.md Structure

```markdown
# MeepleAI Documentation Index

**Total Documents**: ~65 (consolidated from 115+)
**Last Updated**: 2025-12-13T10:59:23.970Z

## Quick Start
- [Getting Started](00-getting-started/quick-start.md)
- [Local Setup](00-getting-started/guida-setup-locale.md)
- [Environment Variables](.env.README.md) (root)

## For Developers
### Backend
- [Backend Guide](02-development/backend/GUIDA-SVILUPPATORE-BACKEND.md)
- [AI Provider Integration](02-development/ai-provider-integration.md)

### Frontend
- [Frontend Guide](02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md)
- [Design System v2.0](04-frontend/design-system.md)

### Testing
- [Comprehensive Testing Guide](02-development/testing/comprehensive-testing-guide.md)
- [Test Patterns Reference](02-development/testing/test-patterns-reference.md)

## Architecture
- [System Architecture](01-architecture/overview/system-architecture.md)
- [ADRs](01-architecture/adr/README.md)
- [DDD Quick Reference](01-architecture/ddd/quick-reference.md)

## Operations
### Infrastructure
- [Infrastructure Overview](05-operations/infrastructure-overview.md) ✨ NEW
- [Docker Resources Guide](02-development/docker-resources-guide.md)

### Deployment
- [Deployment Guide](05-operations/deployment-guide.md)
- [Traefik Guide](05-operations/deployment/traefik-guide.md) ✨ NEW
- [Traefik Testing](05-operations/deployment/traefik-testing.md) ✨ NEW
- [Traefik Production](05-operations/deployment/traefik-production.md) ✨ NEW

### Monitoring
- [Prometheus Setup](05-operations/monitoring/prometheus-setup.md) ✨ NEW
- [Grafana Dashboards](05-operations/monitoring/grafana-llm-cost-dashboard.md)
- [HyperDX Integration](01-architecture/adr/adr-015-hyperdx-observability.md)

### Workflow Automation
- [n8n Automation](05-operations/workflow-automation.md) ✨ NEW

### Runbooks
- [Runbooks Index](05-operations/runbooks/README.md)
- [Infrastructure Monitoring](05-operations/runbooks/infrastructure-monitoring.md)

## Security
- [Security Patterns](06-security/security-patterns.md)
- [SECURITY.md (root)](../SECURITY.md)

## Project Management
- [Current Roadmap](07-project-management/roadmap/ROADMAP.md)
- [Issue Execution Plan](07-project-management/roadmap/ISSUE-EXECUTION-PLAN.md)

## Contributing
- [CONTRIBUTING.md (root)](../CONTRIBUTING.md)

## Knowledge Base
- [Game Scraper](10-knowledge-base/game-scraper.md) ✨ NEW
```

**Commit**: `docs: update navigation and cross-references (Phase 5)`

---

## Summary: Files to DELETE (No Archive)

### Root Directory (3 files)
```
✗ merge-resolution-summary.md
✗ PR-DESCRIPTION.md
✗ test-split-summary.md
```

### .github/ (4 files)
```
✗ .github/ISSUE_TEMPLATE/phase2-storybook-coverage.md
✗ .github/ISSUE_TEMPLATES/csp-nonce-implementation.md
✗ .github/ISSUE_TEMPLATES/csp-reporting-endpoint.md
✗ .github/ISSUE_TEMPLATES/hsts-preload-submission.md
```

### infra/ (4 files + 1 directory)
```
✗ infra/README.md (merged into docs)
✗ infra/INFRASTRUCTURE.md (merged into docs)
✗ infra/docs/archive/* (entire directory)
✗ infra/experimental/README.md (if obsolete)
```

### docs/ (25+ files)
```
Refactoring (DDD complete):
✗ docs/02-development/refactoring/legacy-code-dashboard.md
✗ docs/02-development/refactoring/legacy-code-inventory.md
✗ docs/02-development/refactoring/implementation-notes.md

Completed Features:
✗ docs/04-frontend/improvements/worktree-visual-guide.md
✗ docs/02-development/testing/DEMO-USERS-REMOVED.md

Old Roadmaps:
✗ docs/07-project-management/roadmap/ROADMAP-v19-backup.md

Sprint Reports:
✗ docs/07-project-management/completion-reports/sprint-4/ (entire directory)

Test Quality Reviews:
✗ docs/issues/test-quality-review-2025-11-20/ (entire directory)

Session Logs:
✗ docs/05-operations/analyzer-cleanup-session-2025-11-30.md

Duplicates (after merge):
✗ docs/02-development/testing/manual-testing-guide.md
✗ docs/02-development/testing/specialized/manual-testing-guide.md
✗ docs/02-development/testing/e2e-patterns.md
✗ docs/04-frontend/testing-strategy.md (moved)
✗ docs/02-development/docker-compose-resource-limits.md
✗ docs/02-development/docker-resource-limits-faq.md
✗ docs/02-development/docker-resource-limits-quick-reference.md
✗ docs/02-development/DOCKER-RESOURCE-LIMITS-SOURCES.md
✗ docs/04-frontend/design-system-2.0.md
✗ docs/04-frontend/design-tokens-migration-guide.md
✗ docs/06-security/disposable-resource-leak-remediation.md
✗ docs/06-security/hardcoded-credentials-remediation.md
✗ docs/06-security/null-reference-remediation.md
✗ docs/06-security/log-forging-prevention.md
✗ docs/06-security/incomplete-sanitization-prevention.md
✗ docs/06-security/regex-sanitization-guide.md
```

### infra/ Moved to docs/ (13 files)
```
→ infra/traefik/README.md → docs/05-operations/deployment/traefik-guide.md
→ infra/traefik/TESTING.md → docs/05-operations/deployment/traefik-testing.md
→ infra/traefik/PRODUCTION-CHECKLIST.md → docs/05-operations/deployment/traefik-production.md
→ infra/prometheus/README.md → docs/05-operations/monitoring/prometheus-setup.md
→ infra/n8n/README.md → docs/05-operations/workflow-automation.md
→ GAME_SCRAPER.md → docs/10-knowledge-base/game-scraper.md
```

---

## Updated Success Metrics

| Metric | Before | After | Reduction |
|--------|--------|-------|-----------|
| **Total .md Files** | 115+ (docs) + 25 (other) = 140 | ~65 | **-54%** |
| **Root Clutter** | 9 files | 5 files | **-44%** |
| **infra/ Docs** | 20 files | 7 files (component-specific) | **-65%** |
| **Duplicate Content** | ~20 files | 0 files | **-100%** |
| **Obsolete Files** | ~30 files | 0 files | **-100%** |

---

## Implementation Timeline

```
Week 1:
├─ Mon: Phase 1 - Delete obsolete files (root, .github, docs, infra)
├─ Tue: Phase 2.1-2.2 - Consolidate infra/ (infrastructure, traefik)
└─ Wed-Fri: Phase 2.3-2.4 - Consolidate infra/ (monitoring, n8n)

Week 2:
├─ Mon-Wed: Phase 3 - Merge duplicate docs/ content
└─ Thu-Fri: Phase 4 - Standardize naming

Week 3:
├─ Mon-Tue: Phase 5 - Update INDEX.md and navigation
├─ Wed: Validate all links and structure
└─ Thu-Fri: Final review and commit
```

---

## Validation Checklist

```
Phase 1: Delete
☐ All temporary files removed (root)
☐ Old GitHub templates deleted
☐ DDD refactoring docs removed
☐ Old sprint reports deleted
☐ infra/docs/archive/ deleted
☐ No broken references to deleted files

Phase 2: Consolidate infra/
☐ Infrastructure overview created
☐ Traefik docs moved to docs/05-operations/deployment/
☐ Prometheus docs consolidated
☐ n8n docs consolidated
☐ Original infra/ files deleted
☐ All cross-references updated

Phase 3-5: Same as Original Plan
☐ Testing guide comprehensive
☐ Docker resources unified
☐ Design system v2.0 authoritative
☐ Security patterns consolidated
☐ Naming conventions standardized
☐ INDEX.md updated with new structure
☐ Cross-references added
☐ All internal links validated

Final Check
☐ Total files: ~65 (target achieved)
☐ No duplicates
☐ No broken links
☐ All infra/ docs moved or component-specific only
☐ CLAUDE.md updated
☐ README.md references validated
```

---

## Git Commit Strategy

```bash
# Phase 1: Aggressive cleanup
git commit -m "docs: delete obsolete documentation - root, .github, docs, infra (Phase 1)"

# Phase 2: Consolidate infrastructure
git commit -m "docs: consolidate infra/ documentation into docs/05-operations/ (Phase 2)"

# Phase 3-5: Same as original plan
git commit -m "docs: consolidate duplicate documentation (Phase 3)"
git commit -m "docs: standardize naming conventions (Phase 4)"
git commit -m "docs: update navigation and cross-references (Phase 5)"

# Final
git commit -m "docs: consolidation complete - 140 → 65 files (-54%)"
```

---

## Directory Structure After Consolidation

```
meepleai-monorepo/
├── .env.README.md ✅ (environment variables)
├── README.md ✅ (main project)
├── SECURITY.md ✅ (security policy)
├── CLAUDE.md ✅ (AI context)
├── CONTRIBUTING.md ✅ (contributor guide)
├── .github/
│   └── pull_request_template.md ✅
├── infra/
│   ├── dashboards/README.md ✅ (component-specific)
│   ├── env/README.md ✅ (component-specific)
│   ├── init/README.md ✅ (component-specific)
│   ├── scripts/README.md ✅ (component-specific)
│   └── secrets/README.md ✅ (component-specific)
└── docs/ (~65 files)
    ├── INDEX.md ✅ (updated navigation)
    ├── 00-getting-started/ (5 files)
    ├── 01-architecture/ (26 files)
    ├── 02-development/ (~25 files, consolidated)
    ├── 03-api/ (7 files)
    ├── 04-frontend/ (~15 files, consolidated)
    ├── 05-operations/ (~25 files, includes infra/)
    │   ├── infrastructure-overview.md ✨ NEW
    │   ├── deployment/
    │   │   ├── traefik-guide.md ✨ NEW
    │   │   ├── traefik-testing.md ✨ NEW
    │   │   └── traefik-production.md ✨ NEW
    │   ├── monitoring/
    │   │   └── prometheus-setup.md ✨ NEW
    │   └── workflow-automation.md ✨ NEW
    ├── 06-security/ (~8 files, consolidated)
    ├── 07-project-management/ (~15 files, cleaned)
    ├── 08-business/ (5 files)
    ├── 09-research/ (2 files)
    └── 10-knowledge-base/
        └── game-scraper.md ✨ NEW
```

---

**Status**: ✅ Updated Plan Complete
**Key Change**: ELIMINATE (no archive) + Include ALL .md files
**Next Step**: Execute Phase 1 - Delete obsolete files
**Owner**: Engineering Lead
**Timeline**: 3 weeks
**Expected Reduction**: 140 → 65 files (-54%)

