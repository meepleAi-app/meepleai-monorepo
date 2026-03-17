# Documentation Cleanup - Quick Action Checklist

**Generated**: 2026-02-12
**Total Impact**: Remove 25,500 lines (42% reduction)
**Time Estimate**: 40 hours over 4 weeks

---

## Week 1: DELETE Obsolete Content (8 hours) 🔴 HIGH PRIORITY

**Impact**: -12,000 lines (31% reduction)

### 1. Delete Superseded ADRs (15 files, -3,800 lines)

```bash
# Delete deprecated ADRs
rm docs/01-architecture/adr/adr-001-hybrid-rag.md                    # superseded
rm docs/01-architecture/adr/adr-003-pdf-processing.md               # superseded
rm docs/01-architecture/adr/adr-004b-hybrid-llm.md                  # duplicate
rm docs/01-architecture/adr/adr-005-cosine-similarity-consensus.md  # historical
rm docs/01-architecture/adr/adr-008-streaming-cqrs-migration.md     # legacy
rm docs/01-architecture/adr/adr-010-security-headers-middleware.md  # legacy
rm docs/01-architecture/adr/adr-013-nswag-typescript-generation.md  # deprecated
rm docs/01-architecture/adr/adr-014-nswag-msbuild-removal.md        # deprecated
rm docs/01-architecture/adr/adr-019-shared-catalog-delete-workflow.md # no longer
rm docs/01-architecture/adr/adr-027-infrastructure-services-policy.md # legacy
rm docs/01-architecture/adr/adr-041-meeple-card-universal-system.md # deprecated

# Update ADR index
nano docs/01-architecture/adr/README.md  # Remove references to deleted ADRs
```

### 2. Delete Massive Legacy Docs (4 files, -5,200 lines)

```bash
# Largest file (2,293 lines, Italian, outdated)
rm docs/01-architecture/components/class-architecture.md

# Legacy spec with 54 code blocks
rm docs/01-architecture/components/shared-game-catalog-spec.md

# Historical analysis (decision made)
rm docs/01-architecture/components/pdf-extraction-alternatives.md

# Outdated examples
rm docs/01-architecture/components/amplifier/meepleai-examples.md
```

### 3. Delete Legacy Dev Docs (3 files, -2,200 lines)

```bash
# Migration complete
rm docs/01-architecture/ddd/ddd-migration-pattern-guide.md

# Not used
rm docs/02-development/git-parallel-development.md

# Compress first, then decide
# docs/02-development/operational-guide.md (1,299 → 300 lines)
```

### 4. Update Root Docs (3 files)

```bash
# Remove "historical" sections
nano docs/README.md
nano docs/SKILLS-REFERENCE.md
nano docs/01-architecture/README.md
```

**Checklist**:
- [ ] Deleted 15 obsolete ADRs
- [ ] Deleted 4 legacy architecture docs
- [ ] Deleted 3 legacy dev docs
- [ ] Updated 3 root docs to remove historical content
- [ ] Verified no broken links (grep for deleted filenames)

---

## Week 2: Remove Code Examples (12 hours) 🟡 MEDIUM PRIORITY

**Impact**: -2,500 lines (eliminate 250+ code blocks)

### 1. Update All Bounded Context Docs (8 files)

**Pattern for Each File**:
```markdown
## Code Examples

**Implementation Examples**: See `tests/Api.Tests/BoundedContexts/<BoundedContext>/` for:
- Command handlers with validation
- Query handlers with caching
- Domain entity examples
- Repository implementations
- Integration test patterns

**Test Coverage**: 90%+ (see test suite for comprehensive examples)
```

**Files to Update**:
```bash
# Authentication (40 blocks → 2)
nano docs/09-bounded-contexts/authentication.md

# Document Processing (23 blocks → 2)
nano docs/09-bounded-contexts/document-processing.md

# User Library (22 blocks → 2)
nano docs/09-bounded-contexts/user-library.md

# Shared Game Catalog (21 blocks → 2)
nano docs/09-bounded-contexts/shared-game-catalog.md

# Administration (19 blocks → 2)
nano docs/09-bounded-contexts/administration.md

# Session Tracking (18 blocks → 2)
nano docs/09-bounded-contexts/session-tracking.md

# Game Management (15 blocks → 2)
nano docs/09-bounded-contexts/game-management.md

# Knowledge Base (14 blocks → 2)
nano docs/09-bounded-contexts/knowledge-base.md
```

### 2. Update ADR Code Examples (6 files)

```bash
# Advanced PDF Pipeline (19 → 5 blocks)
nano docs/01-architecture/adr/adr-024-advanced-pdf-embedding-pipeline.md

# Multi-Layer Validation (14 → 3 blocks)
nano docs/01-architecture/adr/adr-006-multi-layer-validation.md

# FluentValidation CQRS (10 → 2 blocks)
nano docs/01-architecture/adr/adr-012-fluentvalidation-cqrs.md

# Multilingual Embedding (9 → 2 blocks)
nano docs/01-architecture/adr/adr-002-multilingual-embedding.md

# CORS Headers (8 → 2 blocks)
nano docs/01-architecture/adr/adr-011-cors-whitelist-headers.md
```

### 3. Update Top API Documentation (10 files)

```bash
# RAG Plugins (34 → 3 blocks each)
nano docs/03-api/rag/plugins/testing-guide.md
nano docs/03-api/rag/plugins/pipeline-definition.md
nano docs/03-api/rag/plugins/plugin-contract.md

# RAG Technical (15 → 3 blocks)
nano docs/03-api/rag/15-technical-reference.md

# API README (16 → 3 blocks)
nano docs/03-api/README.md
```

**Checklist**:
- [ ] Updated 8 bounded context docs (172 → 16 blocks)
- [ ] Updated 6 ADRs (60 → 17 blocks)
- [ ] Updated 10 API docs (80 → 30 blocks)
- [ ] Added test references to all updated files
- [ ] Verified all referenced test paths exist

---

## Week 3: Compress Large Files (16 hours) 🟢 LOW PRIORITY

**Impact**: -10,000 lines (59% reduction in large files)

### Priority 1: Deployment Docs (9 files, 8,148 → 3,500)

```bash
# Docker Services Guide (1,332 → 500)
# Split into: docker-quickstart.md (200), docker-services.md (300)
nano docs/04-deployment/docker-services-production-guide.md

# Monitoring Setup (1,269 → 600)
# Extract: monitoring-checklist.md (200), metrics-reference.md (400)
nano docs/04-deployment/monitoring-setup-guide.md

# Self-Hosted Setup (1,031 → 500)
# Compress with tables and quickstart sections
nano docs/04-deployment/setup-guide-self-hosted.md

# Deployment README (723 → 200)
# Convert to index with links
nano docs/04-deployment/README.md

# Volume Management (691 → 300)
# Convert commands to table format
nano docs/04-deployment/docker-volume-management.md

# Cost Optimization (666 → 300)
# Compress cost tables
nano docs/04-deployment/github-alternatives-cost-optimization.md

# Deployment Checklist (658 → 200)
# Pure checklist format
nano docs/04-deployment/infrastructure-deployment-checklist.md

# Environments (649 → 300)
# Table-based env config
nano docs/04-deployment/environments.md
```

### Priority 2: Development Docs (6 files, 4,009 → 1,850)

```bash
# Local Startup Guide (851 → 400)
# Quickstart table + detailed sections
nano docs/02-development/local-environment-startup-guide.md

# Git Workflow (767 → 300)
# Quick reference format
nano docs/02-development/git-workflow.md

# Docker Advanced (636 → 400)
# Split: docker-compose.md, volumes.md
nano docs/02-development/docker/advanced-features.md

# Docker Commands (629 → 300)
# Command reference table
nano docs/02-development/docker/common-commands.md

# Docker Troubleshooting (603 → 300)
# Q&A format
nano docs/02-development/docker/troubleshooting.md

# Config Values (523 → 250)
# Env var reference table
nano docs/02-development/configuration-values-guide.md
```

### Priority 3: Diagrams & Flows (10 files, 5,000 → 2,200)

```bash
# GitHub Actions Flow (649 → 200)
# Diagram-first, minimal prose
nano docs/01-architecture/diagrams/github-actions-flow.md

# Bounded Contexts (560 → 250)
# Visual diagram focus
nano docs/01-architecture/diagrams/bounded-contexts-interactions.md

# Amplifier Workflow (554 → 300)
# Workflow steps format
nano docs/01-architecture/components/amplifier/developer-workflow.md

# Share Requests Guide (632 → 300)
# Step-by-step format
nano docs/10-user-guides/share-requests-admin-guide.md

# Authentication Flow (548 → 250)
# Flow diagram + minimal text
nano docs/11-user-flows/user-role/01-authentication.md

# UI Test Plan (650 → 300)
# Test matrix format
nano docs/claudedocs/ui-test-plan.md

# Layout Wireframes (590 → 200)
# Image-based, compress prose
nano docs/frontend/layout-wireframes.md
```

**Checklist**:
- [ ] Compressed 9 deployment docs (57% reduction)
- [ ] Compressed 6 development docs (54% reduction)
- [ ] Compressed 10 diagram/flow docs (56% reduction)
- [ ] Converted prose to tables/diagrams
- [ ] Created split files where needed

---

## Week 4: Merge Duplicates (4 hours) 🟢 LOW PRIORITY

**Impact**: -1,000 lines (consolidate redundant content)

### 1. Merge Duplicate ADRs

```bash
# Hybrid LLM: Keep adr-007, delete adr-004b (already in Week 1)
# Update cross-references
grep -r "adr-004b" docs/  # Find references
# Update all references to point to adr-007
```

### 2. Consolidate Docker Documentation

```bash
# Create master quickstart
# Merge content from:
# - docker-services-production-guide.md
# - setup-guide-self-hosted.md
# - docker/advanced-features.md

# Output: 3 consolidated files
# 1. docs/04-deployment/docker-quickstart.md (200 lines)
# 2. docs/04-deployment/docker-production.md (400 lines)
# 3. docs/04-deployment/docker-troubleshooting.md (300 lines)
```

### 3. Merge RAG Variants

```bash
# Check for duplicates
ls docs/03-api/rag/variants/ | sort | uniq -c

# Merge duplicate hybrid-search files if found
# Keep numbered version (05-hybrid-search.md)
```

**Checklist**:
- [ ] Merged duplicate Hybrid LLM ADRs
- [ ] Consolidated Docker documentation (6 → 3 files)
- [ ] Merged RAG variant duplicates
- [ ] Updated all cross-references
- [ ] Created navigation indexes

---

## Verification Commands

### Check Progress

```bash
# Count total files
find docs -name "*.md" | wc -l

# Count total lines
find docs -name "*.md" -exec wc -l {} \; | awk '{sum+=$1} END {print sum}'

# Files >500 lines
find docs -name "*.md" -exec wc -l {} \; | awk '$1 > 500 {print $1, $2}' | sort -rn

# Count code blocks
grep -r "^\`\`\`" docs/*.md | wc -l

# Find obsolete terms
grep -r -i "deprecated\|legacy\|superseded\|historical" docs/*.md
```

### Track Metrics Over Time

```bash
# Before cleanup
./analyze-docs.ps1 > docs-before.txt

# After each week
./analyze-docs.ps1 > docs-week1.txt
./analyze-docs.ps1 > docs-week2.txt
./analyze-docs.ps1 > docs-week3.txt
./analyze-docs.ps1 > docs-week4.txt

# Compare
diff docs-before.txt docs-week4.txt
```

---

## Success Metrics

**Target Outcomes**:
- ✅ Total files: 390 → 222 (43% reduction)
- ✅ Total lines: ~60,000 → ~34,500 (42% reduction)
- ✅ Files >500 lines: 25 → 5 (80% reduction)
- ✅ Code blocks: 800+ → 300 (63% reduction)
- ✅ Obsolete ADRs: 15 → 0 (100% cleanup)

**Quality Gates**:
- [ ] No files >1000 lines
- [ ] No ADRs marked "superseded" >30 days old
- [ ] All bounded context docs reference tests
- [ ] Deployment docs use tables >50%
- [ ] No duplicate content across files

---

## Monthly Maintenance (Going Forward)

**Monthly Checklist** (1st of each month):
1. Run `./audit-docs-detailed.ps1`
2. Review files >500 lines
3. Archive ADRs marked "superseded" >30 days
4. Check for code examples that should be test refs
5. Update this report with new findings

**Quarterly Review** (Q1, Q2, Q3, Q4):
- Deep audit of all documentation
- User feedback on docs quality
- Navigation improvements
- Search optimization

---

**Last Updated**: 2026-02-12
**Next Review**: 2026-03-01 (monthly)
**Full Report**: docs/claudedocs/docs-audit-report-2026-02-12.md
