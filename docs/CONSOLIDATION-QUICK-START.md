# Documentation Consolidation Quick Start - UPDATED

**Purpose**: Step-by-step execution guide for documentation consolidation
**Time**: 3 weeks (phased)
**Status**: ✅ Ready to Execute
**Strategy**: ❌ NO ARCHIVE - Delete obsolete files directly

---

## Quick Links

- **Updated Plan**: [CONSOLIDATION-PLAN-UPDATED.md](./CONSOLIDATION-PLAN-UPDATED.md)
- **Original Summary**: [CONSOLIDATION-SUMMARY.md](./CONSOLIDATION-SUMMARY.md)
- **Current INDEX**: [INDEX.md](./INDEX.md)

---

## UPDATED Phase 1: Delete Obsolete Files ⏱️ 1 Day

### Step 1.1: Delete Root Temporary Files
```bash
cd D:/Repositories/meepleai-monorepo

# Delete temporary files
git rm merge-resolution-summary.md
git rm PR-DESCRIPTION.md
git rm test-split-summary.md

# Move game scraper to knowledge base
git mv GAME_SCRAPER.md docs/10-knowledge-base/game-scraper.md
```

### Step 1.2: Delete Old GitHub Templates
```bash
# Delete obsolete phase-specific templates
git rm .github/ISSUE_TEMPLATE/phase2-storybook-coverage.md

# Delete old security templates (if consolidated)
git rm .github/ISSUE_TEMPLATES/csp-nonce-implementation.md
git rm .github/ISSUE_TEMPLATES/csp-reporting-endpoint.md
git rm .github/ISSUE_TEMPLATES/hsts-preload-submission.md
```

### Step 1.3: Delete DDD Refactoring Docs (100% Complete)
```bash
# DDD Migration Complete - Delete refactoring docs
git rm docs/02-development/refactoring/legacy-code-dashboard.md
git rm docs/02-development/refactoring/legacy-code-inventory.md
git rm docs/02-development/refactoring/implementation-notes.md
```

### Step 1.4: Delete Completed Features & Historical Records
```bash
# Completed Features
git rm docs/04-frontend/improvements/worktree-visual-guide.md
git rm docs/02-development/testing/DEMO-USERS-REMOVED.md

# Old Roadmap
git rm docs/07-project-management/roadmap/ROADMAP-v19-backup.md

# Sprint Completion Reports
git rm -r docs/07-project-management/completion-reports/sprint-4/

# Test Quality Reviews
git rm -r docs/issues/test-quality-review-2025-11-20/
```

### Step 1.5: Delete Session Logs
```bash
# Delete temporary session logs
git rm docs/05-operations/analyzer-cleanup-session-2025-11-30.md
```

### Step 1.6: Delete infra/docs/archive
```bash
# Delete entire infra archive (already obsolete)
git rm -r infra/docs/archive/
```

### Step 1.7: Update CLAUDE.md
Remove references to:
- DDD refactoring (now 100% complete)
- Legacy code dashboards
- Old roadmap versions
- Archived documentation

**Commit**: `docs: delete obsolete documentation (Phase 1)`

---

## Phase 2: Consolidate infra/ Documentation ⏱️ 2 Days

### Step 2.1: Infrastructure Overview (Day 1 AM)
```bash
# Create consolidated infrastructure overview
cat > docs/05-operations/infrastructure-overview.md << 'EOF'
# MeepleAI Infrastructure Overview

## Architecture
[... merge content from infra/README.md ...]

## Components
[... merge content from infra/INFRASTRUCTURE.md ...]

## Docker Compose Profiles
- **minimal**: Core services (postgres, redis, qdrant, api, web)
- **dev**: Development + monitoring (prometheus, grafana)
- **observability**: Full monitoring (alertmanager, hyperdx)
- **ai**: AI/ML services (ollama, embedding, unstructured)
- **automation**: Workflow (n8n)
- **full**: Everything (default)

## Component-Specific Documentation
- [Traefik Reverse Proxy](deployment/traefik-guide.md)
- [Prometheus Monitoring](monitoring/prometheus-setup.md)
- [n8n Workflow Automation](workflow-automation.md)
- [Grafana Dashboards](../infra/dashboards/README.md)
- [Environment Configuration](../infra/env/README.md)
- [Initialization Scripts](../infra/init/README.md)
- [Utility Scripts](../infra/scripts/README.md)
- [Secrets Management](../infra/secrets/README.md)
EOF

# Delete originals
git rm infra/README.md
git rm infra/INFRASTRUCTURE.md
```

### Step 2.2: Traefik Documentation (Day 1 PM)
```bash
# Move Traefik docs to operations/deployment
git mv infra/traefik/README.md docs/05-operations/deployment/traefik-guide.md
git mv infra/traefik/TESTING.md docs/05-operations/deployment/traefik-testing.md
git mv infra/traefik/PRODUCTION-CHECKLIST.md docs/05-operations/deployment/traefik-production.md

# Update internal cross-references in moved files
# (fix relative paths: ../prometheus → ../../monitoring/prometheus-setup.md)
```

### Step 2.3: Monitoring Documentation (Day 2 AM)
```bash
# Consolidate Prometheus and alert documentation
cat > docs/05-operations/monitoring/prometheus-setup.md << 'EOF'
# Prometheus Setup & Configuration

## Overview
[... merge from infra/prometheus/README.md ...]

## Installation & Configuration
- Docker Compose integration
- Service discovery
- Scrape configurations

## Alert Rules
[... merge from infra/prometheus/alerts/README.md ...]

## Grafana Integration
- Dashboard setup
- Data source configuration
- See: [Grafana LLM Cost Dashboard](./grafana-llm-cost-dashboard.md)

## Query Examples
See: [Prometheus LLM Queries](./prometheus-llm-queries.md)
EOF

# Delete originals
git rm infra/prometheus/README.md
git rm infra/prometheus/alerts/README.md
```

### Step 2.4: Workflow Automation (Day 2 PM)
```bash
# Consolidate n8n documentation
cat > docs/05-operations/workflow-automation.md << 'EOF'
# n8n Workflow Automation

## Setup & Configuration
[... merge from infra/n8n/README.md ...]

## Templates
[... merge from infra/n8n/templates/README.md ...]

## Workflows
[... merge from infra/n8n/workflows/README.md ...]

## Integration with MeepleAI
- Document processing workflows
- Alert notification workflows
- Data synchronization

## Troubleshooting
Common issues and solutions
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

#### Create Comprehensive Testing Guide
```bash
# Create new consolidated guide
cat > docs/02-development/testing/comprehensive-testing-guide.md << 'EOF'
# Comprehensive Testing Guide

## Table of Contents
1. Testing Philosophy & Strategy
2. Unit Testing
3. Integration Testing
4. E2E Testing
5. Manual Testing
6. Performance Testing
7. Visual Regression Testing

[... merge content from ...]
- test-writing-guide.md
- manual-testing-guide.md
- specialized/manual-testing-guide.md
EOF

# Remove old files after merge
git rm docs/02-development/testing/manual-testing-guide.md
git rm docs/02-development/testing/specialized/manual-testing-guide.md
```

#### Create Test Patterns Reference
```bash
cat > docs/02-development/testing/test-patterns-reference.md << 'EOF'
# Test Patterns Reference

## Unit Test Patterns
## Integration Test Patterns
## E2E Test Patterns
## Performance Test Patterns

[... merge content from ...]
- test-patterns.md
- e2e-patterns.md
- frontend/testing-react-19-patterns.md
EOF

git rm docs/02-development/testing/e2e-patterns.md
```

#### Move Frontend Testing
```bash
git mv docs/04-frontend/testing-strategy.md docs/02-development/testing/frontend/
```

**Commit**: `docs: consolidate testing documentation (Phase 2.1)`

### 2.2 Docker Resources (Day 4)

```bash
# Create unified Docker resources guide
cat > docs/02-development/docker-resources-guide.md << 'EOF'
# Docker Resources Guide

## Overview
## Configuration Reference
## Quick Reference Table
## FAQ
## Troubleshooting
## External Sources

[... merge content from ...]
- docker-compose-resource-limits.md
- docker-resource-limits-faq.md
- docker-resource-limits-quick-reference.md
- DOCKER-RESOURCE-LIMITS-SOURCES.md
EOF

# Remove old files
git rm docs/02-development/docker-compose-resource-limits.md
git rm docs/02-development/docker-resource-limits-faq.md
git rm docs/02-development/docker-resource-limits-quick-reference.md
git rm docs/02-development/DOCKER-RESOURCE-LIMITS-SOURCES.md
```

**Commit**: `docs: consolidate Docker resources documentation (Phase 2.2)`

### 2.3 Design System (Day 5)

```bash
# Update design-system.md with v2.0 as primary
cat > docs/04-frontend/design-system.md << 'EOF'
# MeepleAI Design System v2.0

## Overview (Current Version)
## Core Principles
## Design Tokens
## Component Library
## Migration Guide (v1 → v2)
## Usage Examples

[... merge content from ...]
- design-system.md (v1 historical)
- design-system-2.0.md (current)
- design-tokens-migration-guide.md
EOF

# Remove old versions
git rm docs/04-frontend/design-system-2.0.md
git rm docs/04-frontend/design-tokens-migration-guide.md
```

**Commit**: `docs: consolidate design system documentation (Phase 2.3)`

### 2.4 Security Patterns (Day 6-7)

```bash
# Update security-patterns.md
cat > docs/06-security/security-patterns.md << 'EOF'
# Security Patterns & Best Practices

## Resource Management Patterns
## Credential Handling Patterns
## Input Validation Patterns
## Output Encoding Patterns
## Error Handling Patterns

[... merge content from ...]
- disposable-resource-leak-remediation.md
- hardcoded-credentials-remediation.md
- null-reference-remediation.md
- log-forging-prevention.md
- incomplete-sanitization-prevention.md
- regex-sanitization-guide.md
EOF

# Remove specific remediation docs (now in patterns)
git rm docs/06-security/disposable-resource-leak-remediation.md
git rm docs/06-security/hardcoded-credentials-remediation.md
git rm docs/06-security/null-reference-remediation.md
git rm docs/06-security/log-forging-prevention.md
git rm docs/06-security/incomplete-sanitization-prevention.md
git rm docs/06-security/regex-sanitization-guide.md

# Keep these separate (operational/comprehensive)
# - codeql-false-positive-management.md
# - oauth-security.md
# - security-testing-strategy.md
```

**Commit**: `docs: consolidate security documentation (Phase 2.4)`

---

## Phase 3: Standardize Naming ⏱️ 3 Days

### Step 3.1: Rename General Docs
```bash
# Convert to kebab-case
git mv docs/02-development/ANALYSIS-SUMMARY.txt docs/02-development/analysis-summary.md
git mv docs/02-development/DOCKER-RESOURCE-LIMITS-INDEX.md docs/02-development/docker-resources-index.md
git mv docs/04-frontend/DOCUMENTATION-SUMMARY.md docs/04-frontend/documentation-index.md
```

### Step 3.2: Convert Text Files to Markdown
```bash
# Convert ANALYSIS-SUMMARY.txt content to proper markdown
# Add headings, lists, code blocks as appropriate
```

### Step 3.3: Standardize Date Formats
```bash
# Find files with date formats
find docs -name "*2025_11_*" -o -name "*2025-11-*"

# Rename to standard YYYY-MM-DD format
# Example: test-fixes_2025_11_20.md → test-fixes-2025-11-20.md
```

**Commit**: `docs: standardize naming conventions (Phase 3)`

---

## Phase 4: Navigation & Cross-References ⏱️ 1 Week

### Step 4.1: Update INDEX.md (Day 1-2)

```bash
# Backup current INDEX
cp docs/INDEX.md docs/INDEX.md.backup

# Update with new structure
cat > docs/INDEX.md << 'EOF'
# MeepleAI Documentation Index

**Total Documents**: ~80 (consolidated)
**Last Updated**: 2025-12-08

## Quick Start
- [Getting Started](00-getting-started/quick-start.md)
- [Local Setup (Italian)](00-getting-started/guida-setup-locale.md)

## For Developers
### Backend
- [Backend Developer Guide](02-development/backend/GUIDA-SVILUPPATORE-BACKEND.md)
- [AI Provider Integration](02-development/ai-provider-integration.md)

### Frontend
- [Frontend Developer Guide](02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md)
- [Design System v2.0](04-frontend/design-system.md)

### Testing
- [Comprehensive Testing Guide](02-development/testing/comprehensive-testing-guide.md)
- [Test Patterns Reference](02-development/testing/test-patterns-reference.md)

[... complete structure ...]
EOF
```

### Step 4.2: Add Cross-References (Day 3-4)

**Add to testing-guide.md**:
```markdown
## Related Documentation
- [Test Patterns Reference](./test-patterns-reference.md)
- [Backend Testing Architecture](./backend/test-architecture.md)
- [E2E Testing Guide](./frontend/e2e/)
- [CI/CD Testing Pipeline](../guides/test-automation-pipeline-guide.md)
```

**Add to security-patterns.md**:
```markdown
## Related Documentation
- [OAuth Security](./oauth-security.md)
- [2FA Security Assessment](./2fa-security-assessment-issue-576.md)
- [Security Testing Strategy](./security-testing-strategy.md)
- [CodeQL Management](./codeql-false-positive-management.md)
```

**Add to docker-resources-guide.md**:
```markdown
## Related Documentation
- [Local Setup Guide](../00-getting-started/guida-setup-locale.md)
- [Deployment Guide](../05-operations/deployment-guide.md)
- [Resource Planning](../05-operations/resource-planning.md)
```

### Step 4.3: Quick Reference Cards (Day 5)

**Add to INDEX.md**:
```markdown
## Quick Reference Cards

### Common Development Tasks
| Task | Documentation |
|------|---------------|
| Set up local environment | [Local Setup](00-getting-started/guida-setup-locale.md) |
| Write backend code | [Backend Guide](02-development/backend/) |
| Write tests | [Testing Guide](02-development/testing/comprehensive-testing-guide.md) |
| Debug PDF processing | [PDF Troubleshooting](02-development/guides/pdf-processing-troubleshooting.md) |

### Operations Tasks
| Task | Documentation |
|------|---------------|
| Deploy to production | [Deployment Guide](05-operations/deployment-guide.md) |
| Investigate errors | [High Error Rate](05-operations/runbooks/high-error-rate.md) |
| Monitor performance | [Prometheus Queries](05-operations/monitoring/prometheus-llm-queries.md) |
```

**Commit**: `docs: enhance navigation and cross-references (Phase 4)`

---

## Validation ⏱️ 2 Days

### Validate Internal Links
```bash
# Install markdown link checker (if not already)
npm install -g markdown-link-check

# Check all markdown files
find docs -name "*.md" -not -path "*/archive/*" | \
  xargs -I {} markdown-link-check {} --config .markdown-link-check.json
```

### Validate Structure
```bash
# Count active docs (should be ~80)
find docs -name "*.md" -not -path "*/archive/*" | wc -l

# Check for duplicates
find docs -name "*.md" -not -path "*/archive/*" -exec basename {} \; | \
  sort | uniq -d

# Check naming convention compliance
find docs -name "*.md" -not -path "*/archive/*" | \
  grep -E '[A-Z]' | grep -v -E '(README|INDEX|ADR-)'
```

### Manual Testing
```
☐ Can find setup guide in <30 seconds
☐ Can navigate from INDEX to any major doc in <2 clicks
☐ Cross-references work bidirectionally
☐ No 404 errors on internal links
☐ Archive is accessible but clearly separated
```

---

## Commit Strategy

```bash
# Good commit messages
git commit -m "docs: archive outdated documentation (Phase 1)"
git commit -m "docs: consolidate testing documentation (Phase 2.1)"
git commit -m "docs: consolidate Docker resources (Phase 2.2)"
git commit -m "docs: consolidate design system (Phase 2.3)"
git commit -m "docs: consolidate security patterns (Phase 2.4)"
git commit -m "docs: standardize naming conventions (Phase 3)"
git commit -m "docs: enhance navigation and cross-references (Phase 4)"
git commit -m "docs: validate and test consolidated structure"
```

---

## Rollback Plan

If issues arise, rollback strategy:

```bash
# Phase-by-phase rollback
git revert <commit-hash>

# Full rollback (if necessary)
git reset --hard <pre-consolidation-commit>

# Restore from backup
cp docs/INDEX.md.backup docs/INDEX.md
```

---

## Success Criteria

### Quantitative
- [x] File count: 115 → ~80 (-30%)
- [ ] Duplicate files: 0
- [ ] Broken links: 0
- [ ] Naming compliance: 100%

### Qualitative
- [ ] Navigation time <2 minutes
- [ ] Developer feedback positive
- [ ] Clear information architecture
- [ ] Easy to maintain

---

## Post-Consolidation

### Update CLAUDE.md
```markdown
**Documentation**: See [docs/INDEX.md](docs/INDEX.md) for complete navigation (115+ docs consolidated to 80)
**Consolidation**: [CONSOLIDATION-SUMMARY.md](docs/CONSOLIDATION-SUMMARY.md)
```

### Announce Completion
- GitHub issue comment
- README update
- Team notification

### Schedule First Review
- **Date**: 2025-12-15 (1 week after completion)
- **Checklist**: Links, duplicates, feedback

---

## Maintenance

**Monthly**:
- Verify no new duplicates
- Check for outdated content
- Validate links

**Quarterly**:
- Archive completed features
- Update navigation
- Review structure

---

## Troubleshooting

### Issue: Broken Links After Move
```bash
# Find and fix broken links
grep -r "\](.*docs/" docs/ --include="*.md" | \
  grep -v archive
```

### Issue: Merge Conflicts
```bash
# Resolve carefully, prefer comprehensive version
git checkout --theirs <file>  # or --ours
```

### Issue: Lost Content
```bash
# Check git history
git log --all --full-history --follow -- <file>

# Restore from archive
cp docs/archive/2025-12/<file> docs/<location>/
```

---

**Status**: ✅ Ready to Execute
**Next**: Start Phase 1 (Archive Outdated Docs)
**Duration**: 2-3 weeks
**Owner**: Engineering Lead
