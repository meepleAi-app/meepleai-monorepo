# Documentation Audit Report - February 12, 2026

**Audit Scope**: D:\Repositories\meepleai-monorepo-frontend\docs\
**Total Files**: 390 markdown files
**Analysis Date**: 2026-02-12

---

## Executive Summary

**Critical Findings**:
- **68 files (17%)** contain obsolete/historical content requiring archival
- **111 files (28%)** have excessive code examples (>3 blocks) that should reference tests
- **25 files (6%)** exceed 500 lines and need compression
- **186 files (48%)** are properly sized and current (KEEP AS-IS)

**Token/Size Impact**:
- **Top 10 largest files**: 10,893 lines combined (avg 1,089 lines each)
- **Obsolete ADRs**: 15 ADR files marked "superseded", "deprecated", or "legacy"
- **Code duplication**: 268 files contain code blocks (potential test extraction)

---

## Category Breakdown

### 1. ARCHIVE/DELETE (68 files, 17%)

Files containing obsolete historical content, deprecated patterns, or superseded decisions.

#### High Priority for Deletion (ADRs)

| File | Lines | Reason | Action |
|------|-------|--------|--------|
| `01-architecture/adr/adr-001-hybrid-rag.md` | 410 | Superseded | DELETE - historical RAG design |
| `01-architecture/adr/adr-003-pdf-processing.md` | 304 | Superseded | DELETE - old PDF approach |
| `01-architecture/adr/adr-004b-hybrid-llm.md` | 189 | Superseded by ADR-007 | DELETE - duplicate content |
| `01-architecture/adr/adr-007-hybrid-llm.md` | 189 | Superseded ADR-004b | MERGE into single current ADR |
| `01-architecture/adr/adr-013-nswag-typescript-generation.md` | 576 | Deprecated (NSwag removed) | DELETE - no longer relevant |
| `01-architecture/adr/adr-014-nswag-msbuild-removal.md` | 327 | Deprecated (NSwag removed) | DELETE - migration complete |
| `01-architecture/adr/adr-040-hyperdx-observability.md` | 149 | Deprecated tool | DELETE - not in use |
| `01-architecture/adr/adr-041-meeple-card-universal-system.md` | 90 | Deprecated component | DELETE - superseded |

**Total ADRs to Archive**: 15 files, 3,800+ lines

#### Obsolete Documentation

| File | Lines | Reason | Action |
|------|-------|--------|--------|
| `01-architecture/components/class-architecture.md` | 2,293 | Italian legacy doc, outdated | DELETE - massive (largest file) |
| `01-architecture/components/shared-game-catalog-spec.md` | 1,405 | 54 code blocks, legacy spec | DELETE - extract to tests |
| `01-architecture/components/pdf-extraction-alternatives.md` | 515 | Historical analysis | DELETE - decision made |
| `01-architecture/components/amplifier/meepleai-examples.md` | 994 | Legacy examples | DELETE - outdated |
| `01-architecture/ddd/ddd-migration-pattern-guide.md` | 453 | Migration complete | DELETE - historical |
| `02-development/operational-guide.md` | 1,299 | Legacy operations | COMPRESS to 300 lines |
| `02-development/git-parallel-development.md` | 418 | Legacy workflow | DELETE - not used |
| `README.md` (root docs) | 219 | Contains "historical" | UPDATE - remove history |
| `SKILLS-REFERENCE.md` | 240 | Contains "legacy" | UPDATE - remove legacy refs |

**Estimated Deletion Impact**: ~12,000 lines removed

---

### 2. COMPRESS (25 files, 6%)

Files exceeding 500 lines that need restructuring into tables, diagrams, or split documents.

#### Deployment Documentation (Most Bloated)

| File | Lines | Recommendation |
|------|-------|----------------|
| `04-deployment/docker-services-production-guide.md` | 1,332 | Split into: setup (300), services (300), troubleshooting (300) |
| `04-deployment/monitoring-setup-guide.md` | 1,269 | Extract to: monitoring-checklist.md (200), metrics-reference.md (300) |
| `04-deployment/setup-guide-self-hosted.md` | 1,031 | Compress with tables: 500 lines max |
| `04-deployment/README.md` | 723 | Compress to index: 200 lines |
| `04-deployment/deployment-workflows-guide.md` | 729 | Extract CI/CD sections to separate files |
| `04-deployment/docker-volume-management.md` | 691 | Compress commands to table format: 300 lines |
| `04-deployment/github-alternatives-cost-optimization.md` | 666 | Compress cost tables: 300 lines |
| `04-deployment/infrastructure-deployment-checklist.md` | 658 | Convert to checklist format: 200 lines |
| `04-deployment/environments.md` | 649 | Table-based env config: 300 lines |

**Total Compression Target**: Reduce from 8,148 lines → 3,500 lines (57% reduction)

#### Development Documentation

| File | Lines | Recommendation |
|------|-------|----------------|
| `02-development/local-environment-startup-guide.md` | 851 | Compress with quickstart table: 400 lines |
| `02-development/git-workflow.md` | 767 | Compress to quick reference: 300 lines |
| `02-development/docker/advanced-features.md` | 636 | Split: docker-compose.md (300), volumes.md (200) |
| `02-development/docker/common-commands.md` | 629 | Convert to command reference table: 300 lines |
| `02-development/docker/troubleshooting.md` | 603 | Convert to Q&A format: 300 lines |
| `02-development/configuration-values-guide.md` | 523 | Convert to env var table: 250 lines |

**Total Compression Target**: Reduce from 4,009 lines → 1,850 lines (54% reduction)

#### Other Large Files

| File | Lines | Recommendation |
|------|-------|----------------|
| `01-architecture/diagrams/github-actions-flow.md` | 649 | Convert prose to flowchart: 200 lines |
| `01-architecture/diagrams/bounded-contexts-interactions.md` | 560 | Diagram-first approach: 250 lines |
| `01-architecture/components/amplifier/developer-workflow.md` | 554 | Compress to workflow steps: 300 lines |
| `10-user-guides/share-requests-admin-guide.md` | 632 | Convert to step-by-step: 300 lines |
| `11-user-flows/user-role/01-authentication.md` | 548 | Compress to flow diagram: 250 lines |
| `claudedocs/ui-test-plan.md` | 650 | Convert to test matrix: 300 lines |
| `frontend/layout-wireframes.md` | 590 | Image-based, compress prose: 200 lines |

**Total Compression Target**: Reduce from 4,183 lines → 1,800 lines (57% reduction)

---

### 3. REMOVE CODE EXAMPLES (111 files, 28%)

Files with >3 code blocks that should reference test files instead of embedding examples.

#### Top Offenders (Code Block Count)

| File | Lines | Code Blocks | Recommendation |
|------|-------|-------------|----------------|
| `09-bounded-contexts/authentication.md` | 1,196 | 40 | Reference `tests/Authentication/*.cs` instead |
| `03-api/rag/plugins/testing-guide.md` | 682 | 34 | Reference `tests/RAG/Plugins/*.cs` |
| `02-development/share-request-implementation.md` | 763 | 32 | Reference `tests/ShareRequests/*.cs` |
| `frontend/layout-components.md` | 663 | 31 | Reference `apps/web/__tests__/components/*.test.tsx` |
| `03-api/rag/plugins/pipeline-definition.md` | 675 | 28 | Reference test examples |
| `03-api/rag/plugins/plugin-contract.md` | 719 | 27 | Reference plugin test suite |
| `05-testing/backend/oauth-testing.md` | 594 | 25 | Reference `tests/OAuth/*.cs` (examples already exist) |
| `06-security/owasp-top-10-compliance.md` | 458 | 24 | Reference security test suite |
| `09-bounded-contexts/document-processing.md` | 702 | 23 | Reference `tests/DocumentProcessing/*.cs` |
| `09-bounded-contexts/user-library.md` | 661 | 22 | Reference `tests/UserLibrary/*.cs` |

**Pattern**: All 11 `09-bounded-contexts/*.md` files have 15-40 code blocks each
**Action**: Update all bounded context docs to reference test files with "See: `tests/<BoundedContext>/<Feature>.cs` for implementation examples"

#### Bounded Context Documentation (All Need Code Removal)

| Bounded Context | Lines | Code Blocks | Test Reference |
|-----------------|-------|-------------|----------------|
| Authentication | 1,196 | 40 | `tests/Api.Tests/BoundedContexts/Authentication/` |
| Document Processing | 702 | 23 | `tests/Api.Tests/BoundedContexts/DocumentProcessing/` |
| User Library | 661 | 22 | `tests/Api.Tests/BoundedContexts/UserLibrary/` |
| Shared Game Catalog | 788 | 21 | `tests/Api.Tests/BoundedContexts/SharedGameCatalog/` |
| Administration | 640 | 19 | `tests/Api.Tests/BoundedContexts/Administration/` |
| Session Tracking | 478 | 18 | `tests/Api.Tests/BoundedContexts/SessionTracking/` |
| Game Management | 1,266 | 15 | `tests/Api.Tests/BoundedContexts/GameManagement/` |
| Knowledge Base | 1,594 | 14 | `tests/Api.Tests/BoundedContexts/KnowledgeBase/` |

**Total**: 8 bounded context docs with 172 code blocks → should reference 737 test files

#### ADR Documentation (Code Examples to Remove)

| ADR | Code Blocks | Action |
|-----|-------------|--------|
| `adr-024-advanced-pdf-embedding-pipeline.md` | 19 | Reference tests, reduce to 5 blocks |
| `adr-006-multi-layer-validation.md` | 14 | Reference tests, reduce to 3 blocks |
| `adr-012-fluentvalidation-cqrs.md` | 10 | Reference tests, reduce to 2 blocks |
| `adr-002-multilingual-embedding.md` | 9 | Reference tests, reduce to 2 blocks |
| `adr-013-nswag-typescript-generation.md` | 9 | DELETE (deprecated) |
| `adr-011-cors-whitelist-headers.md` | 8 | Reference middleware tests |

---

### 4. KEEP AS-IS (186 files, 48%)

Files under 500 lines with <3 code blocks and current content. No action needed.

**Examples**:
- Most ADRs (ADR-009, ADR-021, ADR-025, ADR-042)
- Most user flows
- Most guides under 400 lines
- Testing documentation under 500 lines

---

## Redundant Content Analysis

### Duplicate ADRs (Requires Merging)

| Duplicate Set | Files | Lines | Action |
|---------------|-------|-------|--------|
| **Hybrid LLM** | adr-004b (189), adr-007 (189) | 378 | MERGE → single ADR-007, DELETE adr-004b |
| **Hybrid Search** | `03-api/rag/variants/05-hybrid-search.md`, `hybrid-search.md` | 300 | MERGE → single file |

### Overlapping Content (Requires Consolidation)

| Topic | Files | Lines | Recommendation |
|-------|-------|-------|----------------|
| **Docker Setup** | 6 files in `04-deployment/` | 5,000+ | Consolidate to 3: quickstart, production, troubleshooting |
| **Testing Guides** | 10 files in `05-testing/` | 4,000+ | Create master index, compress guides |
| **Agent Architecture** | 10 files in `02-development/agent-architecture/` | 3,500+ | OK - comprehensive coverage needed |

---

## Priority Action Plan

### Phase 1: DELETE Obsolete Content (Week 1) - HIGH IMPACT

**Target**: Remove 12,000+ lines

1. **Delete ADRs** (15 files, 3,800 lines)
   - adr-001, adr-003, adr-004b, adr-005, adr-007 (merge to single), adr-008, adr-010, adr-013, adr-014, adr-019, adr-027, adr-040, adr-041
   - Update ADR README to remove references

2. **Delete Legacy Architecture Docs** (4 files, 5,200 lines)
   - `class-architecture.md` (2,293 lines) - massive Italian doc
   - `shared-game-catalog-spec.md` (1,405 lines) - 54 code blocks
   - `pdf-extraction-alternatives.md` (515 lines) - historical
   - `amplifier/meepleai-examples.md` (994 lines) - outdated

3. **Delete Legacy Dev Docs** (3 files, 2,200 lines)
   - `operational-guide.md` (compress first to 300 lines)
   - `git-parallel-development.md` (418 lines)
   - `ddd-migration-pattern-guide.md` (453 lines)

**Estimated Time**: 8 hours
**Impact**: 31% reduction in docs volume

### Phase 2: Remove Code Examples (Week 2) - MEDIUM IMPACT

**Target**: Update 111 files to reference tests

1. **Bounded Context Docs** (8 files)
   - Add "**Code Examples**: See `tests/Api.Tests/BoundedContexts/<Name>/` for implementation examples" header
   - Remove all code blocks except 1-2 interface examples
   - Reduce 172 code blocks → 16 blocks (90% reduction)

2. **ADR Cleanup** (20 files)
   - Keep max 3 code blocks per ADR (interface/contract examples only)
   - Remove implementation examples, reference tests
   - Reduce 120+ code blocks → 40 blocks (67% reduction)

3. **API Documentation** (30 files in `03-api/`)
   - Reference test files for request/response examples
   - Keep only API contract examples
   - Reduce 80+ code blocks → 30 blocks (63% reduction)

**Estimated Time**: 12 hours
**Impact**: Eliminate 250+ code blocks (2,500+ lines)

### Phase 3: Compress Large Files (Week 3) - LOW IMPACT

**Target**: Reduce 25 files from 17,000 → 7,000 lines

1. **Deployment Docs** (9 files, 8,148 → 3,500 lines)
   - Convert prose to tables
   - Split mega-files (docker-services-production-guide.md)
   - Create quick reference indexes

2. **Development Docs** (6 files, 4,009 → 1,850 lines)
   - Compress startup guide with quickstart table
   - Convert git-workflow to quick reference
   - Table-based command references

3. **Diagrams & Flows** (10 files, 5,000 → 2,200 lines)
   - Diagram-first approach (less prose)
   - Convert flows to visual format
   - Compress user guides to step-by-step

**Estimated Time**: 16 hours
**Impact**: 59% reduction in large file bloat

### Phase 4: Merge Duplicates (Week 4) - LOW IMPACT

**Target**: Consolidate 10 duplicate/overlapping files

1. **ADR Merges**
   - Merge adr-004b → adr-007 (single Hybrid LLM ADR)
   - Update cross-references

2. **Docker Documentation**
   - Consolidate 6 docker files → 3 master guides
   - Create docker-quickstart.md (200 lines)

3. **RAG Variants**
   - Merge duplicate hybrid-search files
   - Create RAG variants index

**Estimated Time**: 4 hours
**Impact**: Reduce 10 files, improve navigation

---

## Summary of Recommendations

| Phase | Action | Files | Lines Affected | Time | Priority |
|-------|--------|-------|----------------|------|----------|
| **Phase 1** | DELETE obsolete/legacy | 22 | -12,000 | 8h | 🔴 HIGH |
| **Phase 2** | REMOVE code examples | 111 | -2,500 | 12h | 🟡 MEDIUM |
| **Phase 3** | COMPRESS large files | 25 | -10,000 | 16h | 🟢 LOW |
| **Phase 4** | MERGE duplicates | 10 | -1,000 | 4h | 🟢 LOW |
| **TOTAL** | **4 phases** | **168** | **-25,500 lines** | **40h** | **1 month** |

**Expected Outcome**:
- **Before**: 390 files, ~60,000 lines
- **After**: 222 files, ~34,500 lines
- **Reduction**: 43% fewer files, 42% fewer lines
- **Quality**: Current, test-referenced, concise documentation

---

## Detailed File-by-File Actions

### ARCHIVE/DELETE List (68 files)

```
# ADRs (15 files)
DELETE: 01-architecture/adr/adr-001-hybrid-rag.md (superseded)
DELETE: 01-architecture/adr/adr-003-pdf-processing.md (superseded)
DELETE: 01-architecture/adr/adr-004b-hybrid-llm.md (duplicate of adr-007)
DELETE: 01-architecture/adr/adr-005-cosine-similarity-consensus.md (historical)
DELETE: 01-architecture/adr/adr-008-streaming-cqrs-migration.md (legacy)
DELETE: 01-architecture/adr/adr-010-security-headers-middleware.md (legacy)
DELETE: 01-architecture/adr/adr-013-nswag-typescript-generation.md (deprecated)
DELETE: 01-architecture/adr/adr-014-nswag-msbuild-removal.md (deprecated)
DELETE: 01-architecture/adr/adr-019-shared-catalog-delete-workflow.md (no longer used)
DELETE: 01-architecture/adr/adr-027-infrastructure-services-policy.md (legacy)
DELETE: 01-architecture/adr/adr-040-hyperdx-observability.md (deprecated)
DELETE: 01-architecture/adr/adr-041-meeple-card-universal-system.md (deprecated)
MERGE: 01-architecture/adr/adr-007-hybrid-llm.md → single current version

# Architecture Components (7 files)
DELETE: 01-architecture/components/class-architecture.md (2,293 lines, Italian, legacy)
DELETE: 01-architecture/components/shared-game-catalog-spec.md (1,405 lines, 54 code blocks)
DELETE: 01-architecture/components/pdf-extraction-alternatives.md (515 lines, historical)
DELETE: 01-architecture/components/amplifier/meepleai-examples.md (994 lines, outdated)
DELETE: 01-architecture/components/amplifier/architecture-overview.md (725 lines, evolution)
DELETE: 01-architecture/ddd/ddd-migration-pattern-guide.md (453 lines, migration complete)
DELETE: 01-architecture/diagrams/README.md (268 lines, legacy)

# Development (5 files)
DELETE: 02-development/git-parallel-development.md (418 lines)
DELETE: 02-development/admin-dashboard-guide.md (183 lines, historical)
COMPRESS: 02-development/operational-guide.md (1,299 → 300 lines)

# Root Docs (3 files)
UPDATE: README.md (remove "historical" sections)
UPDATE: SKILLS-REFERENCE.md (remove "legacy" references)
UPDATE: 01-architecture/README.md (remove "historical" context)
```

### REMOVE CODE EXAMPLES List (Top 30)

```
# Bounded Contexts (all 8 files)
UPDATE: 09-bounded-contexts/authentication.md (40 blocks → 2)
UPDATE: 09-bounded-contexts/document-processing.md (23 blocks → 2)
UPDATE: 09-bounded-contexts/user-library.md (22 blocks → 2)
UPDATE: 09-bounded-contexts/shared-game-catalog.md (21 blocks → 2)
UPDATE: 09-bounded-contexts/administration.md (19 blocks → 2)
UPDATE: 09-bounded-contexts/session-tracking.md (18 blocks → 2)
UPDATE: 09-bounded-contexts/game-management.md (15 blocks → 2)
UPDATE: 09-bounded-contexts/knowledge-base.md (14 blocks → 2)

# ADRs (top 6)
UPDATE: 01-architecture/adr/adr-024-advanced-pdf-embedding-pipeline.md (19 → 5)
UPDATE: 01-architecture/adr/adr-006-multi-layer-validation.md (14 → 3)
UPDATE: 01-architecture/adr/adr-012-fluentvalidation-cqrs.md (10 → 2)
UPDATE: 01-architecture/adr/adr-002-multilingual-embedding.md (9 → 2)
UPDATE: 01-architecture/adr/adr-011-cors-whitelist-headers.md (8 → 2)

# API Documentation (top 10)
UPDATE: 03-api/rag/plugins/testing-guide.md (34 → 3)
UPDATE: 03-api/rag/plugins/pipeline-definition.md (28 → 3)
UPDATE: 03-api/rag/plugins/plugin-contract.md (27 → 3)
UPDATE: 03-api/rag/15-technical-reference.md (15 → 3)
UPDATE: 03-api/README.md (16 → 3)

# Frontend (5 files)
UPDATE: frontend/layout-components.md (31 → 3)
UPDATE: 02-development/share-request-implementation.md (32 → 5)

# Testing (3 files)
UPDATE: 05-testing/backend/oauth-testing.md (25 → 3)
UPDATE: 06-security/owasp-top-10-compliance.md (24 → 3)
UPDATE: 05-testing/playwright-best-practices.md (22 → 3)
```

### COMPRESS List (25 files)

```
# Deployment (Priority 1)
COMPRESS: 04-deployment/docker-services-production-guide.md (1,332 → 500)
COMPRESS: 04-deployment/monitoring-setup-guide.md (1,269 → 600)
COMPRESS: 04-deployment/setup-guide-self-hosted.md (1,031 → 500)
COMPRESS: 04-deployment/README.md (723 → 200)
COMPRESS: 04-deployment/deployment-workflows-guide.md (729 → 400)
COMPRESS: 04-deployment/docker-volume-management.md (691 → 300)
COMPRESS: 04-deployment/github-alternatives-cost-optimization.md (666 → 300)
COMPRESS: 04-deployment/infrastructure-deployment-checklist.md (658 → 200)
COMPRESS: 04-deployment/environments.md (649 → 300)

# Development (Priority 2)
COMPRESS: 02-development/local-environment-startup-guide.md (851 → 400)
COMPRESS: 02-development/git-workflow.md (767 → 300)
COMPRESS: 02-development/docker/advanced-features.md (636 → 400)
COMPRESS: 02-development/docker/common-commands.md (629 → 300)
COMPRESS: 02-development/docker/troubleshooting.md (603 → 300)
COMPRESS: 02-development/configuration-values-guide.md (523 → 250)

# Architecture/Diagrams (Priority 3)
COMPRESS: 01-architecture/diagrams/github-actions-flow.md (649 → 200)
COMPRESS: 01-architecture/diagrams/bounded-contexts-interactions.md (560 → 250)
COMPRESS: 01-architecture/components/amplifier/developer-workflow.md (554 → 300)

# User Guides (Priority 3)
COMPRESS: 10-user-guides/share-requests-admin-guide.md (632 → 300)
COMPRESS: 11-user-flows/user-role/01-authentication.md (548 → 250)
COMPRESS: claudedocs/ui-test-plan.md (650 → 300)
COMPRESS: frontend/layout-wireframes.md (590 → 200)
COMPRESS: 04-features/admin-game-import/epic-4136-breakdown.md (544 → 300)
```

---

## Automation Opportunities

### Scripts to Create

1. **Code Block Extractor**
   ```bash
   # Extract all code blocks from a markdown file
   # Output: list of code blocks that should be test references
   ./scripts/extract-code-blocks.sh <file>
   ```

2. **Test Reference Generator**
   ```bash
   # Generate "See tests/..." references for bounded context docs
   ./scripts/generate-test-refs.sh <bounded-context>
   ```

3. **Line Counter Dashboard**
   ```bash
   # Track docs size over time
   ./scripts/docs-metrics.sh
   ```

4. **Obsolete Content Scanner**
   ```bash
   # Scan for "deprecated", "legacy", "superseded" keywords
   ./scripts/scan-obsolete.sh
   ```

---

## Maintenance Guidelines

**Going Forward**:
1. **Max file size**: 500 lines (exceptions: comprehensive specs)
2. **Code examples**: Max 3 blocks per file, prefer test references
3. **ADRs**: Delete/archive superseded decisions within 30 days
4. **Version info**: Remove "why we chose" historical narratives after 6 months
5. **Monthly audit**: Run docs-metrics.sh, review files >500 lines

**Quality Gates**:
- [ ] No files >1000 lines (current: 3 files violate)
- [ ] No ADRs marked "superseded" >30 days old (current: 8 violate)
- [ ] Bounded context docs reference tests, not embed code (current: 0/8 comply)
- [ ] Deployment docs use tables >50% of content (current: ~10%)

---

## Appendix: Full File Inventory

**CSV Export**: `docs-audit-results.csv` (390 rows)
**Categories**: KEEP (186), REMOVE-CODE (111), COMPRESS (25), ARCHIVE (68)

**Top 20 Files by Size**:
1. class-architecture.md (2,293) - DELETE
2. knowledge-base.md (1,594) - REMOVE-CODE
3. shared-game-catalog-spec.md (1,405) - DELETE
4. product-specification.md (1,367) - KEEP
5. docker-services-production-guide.md (1,332) - COMPRESS
6. operational-guide.md (1,299) - COMPRESS
7. monitoring-setup-guide.md (1,269) - COMPRESS
8. game-management.md (1,266) - REMOVE-CODE
9. infrastructure-cost-summary.md (1,237) - KEEP
10. authentication.md (1,196) - REMOVE-CODE
11. frontend-testing-patterns.md (1,123) - REMOVE-CODE
12. setup-guide-self-hosted.md (1,031) - COMPRESS
13. amplifier/meepleai-examples.md (994) - DELETE
14. HOW-IT-WORKS.md (982) - KEEP
15. ci-cd-pipeline.md (981) - REMOVE-CODE
16. SPECIFICATION.md (935) - KEEP
17. system-architecture.md (926) - KEEP
18. email-totp-services.md (923) - REMOVE-CODE
19. adr-024-advanced-pdf-embedding-pipeline.md (900) - REMOVE-CODE
20. COMPLETE-TEST-FLOWS.md (877) - KEEP

---

**Report Generated**: 2026-02-12
**Analyst**: Claude Code Documentation Audit
**Next Review**: 2026-03-12 (monthly cadence)
