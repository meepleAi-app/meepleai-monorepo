# Documentation Reorganization Plan

**Date**: 2025-11-13
**Status**: In Progress
**Reason**: Improve discoverability, reduce clutter, align with audience needs

---

## Objectives

1. **Audience-first structure**: Organize by user role (developers, DevOps, business, architects)
2. **Reduce root clutter**: Move 8 root `.md` files into appropriate categories
3. **Consolidate related topics**: Merge overlapping directories (guide/operations/runbooks)
4. **Standardize naming**: Remove `board-game-ai-*` prefixes, use descriptive folder names
5. **Support DDD migration**: Create dedicated `refactoring/` folder for migration docs

---

## New Structure

```
docs/
├── 00-getting-started/          # Quick start, onboarding (NEW)
├── 01-architecture/              # Architecture documentation (REORGANIZED)
│   ├── overview/                 # High-level architecture docs (NEW)
│   ├── adr/                      # Architecture Decision Records (NEW)
│   ├── diagrams/                 # Architecture diagrams (EXISTING)
│   ├── components/               # Specific components (PDF, RAG, agents) (NEW)
│   └── ddd/                      # Domain-Driven Design reference (NEW)
├── 02-development/               # Developer documentation (NEW)
│   ├── guides/                   # Technical guides (MOVED from guide/)
│   ├── refactoring/              # DDD migration, legacy code (NEW)
│   ├── implementation/           # Implementation notes (EXISTING)
│   └── testing/                  # Testing docs (EXISTING)
├── 03-api/                       # API documentation (EXISTING)
├── 04-frontend/                  # Frontend documentation (EXISTING)
├── 05-operations/                # Operations & DevOps (REORGANIZED)
│   ├── deployment/               # Deployment guides (EXISTING)
│   ├── runbooks/                 # Incident runbooks (EXISTING)
│   └── monitoring/               # Observability, logging (NEW)
├── 06-security/                  # Security documentation (EXISTING)
├── 07-project-management/        # Project planning & organization (NEW)
│   ├── planning/                 # Implementation plans (EXISTING)
│   ├── roadmap/                  # Strategic roadmap (EXISTING)
│   ├── organization/             # Team, roles, sprints (MOVED from org/)
│   └── completion-reports/       # Phase completion reports (MOVED from completion/)
├── 08-business/                  # Business documentation (EXISTING)
├── 09-research/                  # Research & findings (EXISTING)
└── 10-knowledge-base/            # Wiki & external references (REORGANIZED from kb/ + wiki/)
```

---

## File Migration Mapping

### 00-getting-started/ (NEW)

| Source | Destination | Notes |
|--------|-------------|-------|
| `board-game-ai-QUICK-START.md` | `00-getting-started/quick-start.md` | Renamed |
| `board-game-ai-executive-summary.md` | `00-getting-started/executive-summary.md` | Renamed |
| `README.md` | `00-getting-started/overview.md` | Copy, keep root README |

### 01-architecture/

#### overview/
| Source | Destination |
|--------|-------------|
| `architecture/board-game-ai-architecture-overview.md` | `01-architecture/overview/system-architecture.md` |
| `architecture/board-game-ai-consolidation-strategy.md` | `01-architecture/overview/consolidation-strategy.md` |

#### adr/
| Source | Destination |
|--------|-------------|
| `architecture/adr-001-hybrid-rag-architecture.md` | `01-architecture/adr/adr-001-hybrid-rag.md` |
| `architecture/adr-002-multilingual-embedding-strategy.md` | `01-architecture/adr/adr-002-multilingual-embedding.md` |
| `architecture/adr-003-pdf-processing-pipeline.md` | `01-architecture/adr/adr-003-pdf-processing.md` |
| `architecture/adr-003-unstructured-pdf-extraction.md` | `01-architecture/adr/adr-003b-unstructured-pdf.md` |
| `architecture/adr-004-ai-agents-bounded-context.md` | `01-architecture/adr/adr-004-ai-agents.md` |
| `architecture/adr-004-hybrid-llm-architecture.md` | `01-architecture/adr/adr-004b-hybrid-llm.md` |

#### diagrams/
| Source | Destination |
|--------|-------------|
| `architecture/diagrams/*` | `01-architecture/diagrams/*` (unchanged) |

#### components/
| Source | Destination |
|--------|-------------|
| `architecture/pdf-extraction-opensource-alternatives.md` | `01-architecture/components/pdf-extraction-alternatives.md` |
| `architecture/bgai-028-confidence-validation-layer.md` | `01-architecture/components/confidence-validation.md` |
| `architecture/agent-lightning-architecture.md` | `01-architecture/components/agent-lightning/architecture.md` |
| `architecture/agent-lightning-examples.md` | `01-architecture/components/agent-lightning/examples.md` |
| `architecture/agent-lightning-integration-guide.md` | `01-architecture/components/agent-lightning/integration-guide.md` |
| `architecture/agent-lightning-openrouter-guide.md` | `01-architecture/components/agent-lightning/openrouter-guide.md` |
| `architecture/agent-lightning-quickstart.md` | `01-architecture/components/agent-lightning/quickstart.md` |
| `architecture/amplifier-architecture-overview.md` | `01-architecture/components/amplifier/architecture-overview.md` |
| `architecture/amplifier-developer-workflow-guide.md` | `01-architecture/components/amplifier/developer-workflow.md` |
| `architecture/amplifier-meepleai-examples.md` | `01-architecture/components/amplifier/meepleai-examples.md` |

#### ddd/
| Source | Destination |
|--------|-------------|
| `ddd-quick-reference.md` | `01-architecture/ddd/quick-reference.md` |

### 02-development/

#### guides/
| Source | Destination |
|--------|-------------|
| `guide/llm-integration-guide.md` | `02-development/guides/llm-integration.md` |
| `guide/unstructured-setup-guide.md` | `02-development/guides/unstructured-setup.md` |

#### refactoring/
| Source | Destination |
|--------|-------------|
| `implementation/legacy-code-inventory-and-removal-plan.md` | `02-development/refactoring/legacy-code-inventory.md` |
| `implementation/legacy-code-removal-dashboard.md` | `02-development/refactoring/legacy-code-dashboard.md` |
| `board-game-ai-IMPLEMENTATION-NOTES.md` | `02-development/refactoring/implementation-notes.md` |
| `board-game-ai-NEXT-STEPS.md` | `02-development/refactoring/next-steps.md` |

#### implementation/
| Source | Destination |
|--------|-------------|
| `implementation/bgai-023-ragservice-migration-findings.md` | `02-development/implementation/bgai-023-ragservice-migration.md` |
| `implementation/bgai-026-cost-tracking-verification.md` | `02-development/implementation/bgai-026-cost-tracking.md` |

#### testing/
| Source | Destination |
|--------|-------------|
| `testing/*` | `02-development/testing/*` (unchanged structure) |

### 03-api/

| Source | Destination |
|--------|-------------|
| `api/*` | `03-api/*` (unchanged) |

### 04-frontend/

| Source | Destination |
|--------|-------------|
| `frontend/*` | `04-frontend/*` (unchanged) |

### 05-operations/

#### deployment/
| Source | Destination |
|--------|-------------|
| `deployment/*` | `05-operations/deployment/*` (unchanged) |

#### runbooks/
| Source | Destination |
|--------|-------------|
| `runbooks/*` | `05-operations/runbooks/*` (unchanged) |
| `troubleshooting.md` | `05-operations/runbooks/general-troubleshooting.md` |

#### monitoring/
| Source | Destination |
|--------|-------------|
| `operations/logging-and-audit-guide.md` | `05-operations/monitoring/logging-and-audit.md` |

### 06-security/

| Source | Destination |
|--------|-------------|
| `security/*` | `06-security/*` (unchanged) |

### 07-project-management/

#### planning/
| Source | Destination |
|--------|-------------|
| `planning/*` | `07-project-management/planning/*` (unchanged) |

#### roadmap/
| Source | Destination |
|--------|-------------|
| `roadmap/*` | `07-project-management/roadmap/*` (unchanged) |

#### organization/
| Source | Destination |
|--------|-------------|
| `org/*` | `07-project-management/organization/*` (all files) |

#### completion-reports/
| Source | Destination |
|--------|-------------|
| `completion/*` | `07-project-management/completion-reports/*` (unchanged) |

### 08-business/

| Source | Destination |
|--------|-------------|
| `business/*` | `08-business/*` (unchanged) |

### 09-research/

| Source | Destination |
|--------|-------------|
| `research/*` | `09-research/*` (unchanged) |

### 10-knowledge-base/

| Source | Destination |
|--------|-------------|
| `kb/*` | `10-knowledge-base/*` (all files) |
| `wiki/*` | `10-knowledge-base/references/*` (all files) |

### Root Files

| Source | Destination | Notes |
|--------|-------------|-------|
| `README.md` | `README.md` | Updated with new structure |
| `board-game-ai-documentation-index.md` | `INDEX.md` | Renamed, updated |

---

## Implementation Steps

1. ✅ Create reorganization plan document
2. ⏳ Create new directory structure
3. ⏳ Move files to new locations (preserve git history with `git mv`)
4. ⏳ Update internal links and cross-references
5. ⏳ Update root README and INDEX
6. ⏳ Commit and push changes

---

## Benefits

### For Developers
- **Clear entry point**: `00-getting-started/` for onboarding
- **Consolidated guides**: All dev docs in `02-development/`
- **Testing in one place**: `02-development/testing/`

### For Architects
- **Organized ADRs**: `01-architecture/adr/` for all decisions
- **Component docs grouped**: Agent Lightning, Amplifier, PDF in `components/`
- **Diagrams accessible**: `01-architecture/diagrams/`

### For DevOps
- **Ops centralized**: `05-operations/` for deployment, runbooks, monitoring
- **Runbooks easily found**: `05-operations/runbooks/`

### For Project Managers
- **Planning unified**: `07-project-management/` for all project docs
- **Roadmap visible**: `07-project-management/roadmap/`
- **Team info accessible**: `07-project-management/organization/`

---

## Rollback Plan

If issues arise:
1. Revert commit: `git revert <commit-hash>`
2. Old structure preserved in git history
3. Files can be restored with `git mv` back to original locations

---

## Validation Checklist

- [ ] All 113 files accounted for
- [ ] No broken internal links
- [ ] README.md updated with new structure
- [ ] INDEX.md updated with new paths
- [ ] Git history preserved (used `git mv`)
- [ ] CI/CD pipeline still passes
- [ ] Documentation still renders correctly

---

## Notes

- **Naming convention**: Use kebab-case, avoid prefixes like `board-game-ai-`
- **Numbering folders**: `00-`, `01-`, etc. ensures consistent ordering
- **Unchanged folders**: api, frontend, business, research kept as-is (already well-organized)
- **New categories**: refactoring, monitoring, project-management created for better organization

---

**Status**: Ready for implementation
**Review**: Engineering Lead, Documentation Team
**Approval**: Pending
