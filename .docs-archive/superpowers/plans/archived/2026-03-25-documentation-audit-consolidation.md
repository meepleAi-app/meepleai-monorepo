# Documentation Audit & Consolidation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Audit all project documentation, fix broken references, fill documentation gaps for 8 undocumented bounded contexts, clean up ~8MB of dead files, and consolidate the docs structure.

**Architecture:** Systematic documentation audit following 5 phases: cleanup → fix references → fill gaps → consolidate → update indexes. Each phase is independent and produces a commit. BC READMEs follow the existing Italian-language template pattern established by GameManagement and other documented contexts.

**Tech Stack:** Markdown, Git, existing bounded-context-template.md pattern

---

## Audit Summary (2026-03-25)

| Category | Finding | Priority |
|----------|---------|----------|
| Dead files (HTML mockups, old archives) | ~8MB, 50+ files to remove | 🔴 HIGH |
| Missing BC READMEs | 8 of 19 bounded contexts undocumented (42%) | 🔴 HIGH |
| Broken internal links | 5+ broken cross-references | 🟡 MEDIUM |
| Duplicate documentation | Dashboard Hub exists in active + archive | 🟡 MEDIUM |
| Stale path patterns | Old `01-architecture/` prefix in archived docs | 🟢 LOW |
| Missing deprecation markers | GameCard, ADR-004b, Dashboard v1 | 🟢 LOW |

---

### Task 1: Delete Dead HTML Files & Archives

**Files:**
- Delete: `docs/api/rag/diagrams/*.html` (7 files, 184KB)
- Delete: `docs/frontend/mocks/*.html` (2 files)
- Delete: `docs/frontend/mockups/*.html` (2 files)
- Delete: `docs/components/ui/meeple-card-mockup.html` (60KB)
- Delete: `docs/chat-agent-roadmap.html` (36KB)
- Delete: `docs/DEVELOPMENT-ROADMAP.html` (28KB)
- Delete: `docs/development-workflow.html` (80KB)
- Delete: `docs/playground-debug-roadmap.html` (36KB)
- Delete: `docs/archive/epic-4068/` (entire directory, 6.5MB)
- Delete: `docs/blog/` (1 file)
- Delete: `docs/marketing/` (1 file)
- Delete: `docs/video/` (empty after cleanup)

- [ ] **Step 1: Verify files exist before deletion**

```bash
# List all HTML files to delete
find docs/ -name "*.html" -path "*/rag/diagrams/*" -o -name "*.html" -path "*/mocks/*" -o -name "*.html" -path "*/mockups/*" | sort
ls docs/archive/epic-4068/ 2>/dev/null
ls docs/blog/ docs/marketing/ docs/video/ 2>/dev/null
```

- [ ] **Step 2: Delete RAG HTML diagrams**

```bash
git rm docs/api/rag/diagrams/rag-flow-current.html
git rm docs/api/rag/diagrams/strategy-flow-CONSENSUS.html
git rm docs/api/rag/diagrams/strategy-flow-FAST.html
git rm docs/api/rag/diagrams/strategy-flow-PRECISE.html
git rm docs/api/rag/diagrams/strategy-flows-comparison.html
git rm docs/api/rag/diagrams/strategy-flows-meeple-style.html
git rm docs/api/rag/diagrams/strategy-matrix-view.html
```

- [ ] **Step 3: Delete frontend mockup HTMLs**

```bash
git rm docs/frontend/mocks/layout-desktop-mock.html
git rm docs/frontend/mocks/layout-mobile-mock.html
git rm docs/frontend/mockups/chat-card-integrated.html
git rm docs/frontend/mockups/chat-card-mockup.html
git rm docs/components/ui/meeple-card-mockup.html
```

- [ ] **Step 4: Delete roadmap HTMLs and debug artifact**

```bash
git rm docs/chat-agent-roadmap.html
git rm docs/DEVELOPMENT-ROADMAP.html
git rm docs/development-workflow.html
git rm docs/playground-debug-roadmap.html
```

- [ ] **Step 5: Delete epic-4068 archive and empty folders**

```bash
git rm -r docs/archive/epic-4068/
git rm -r docs/blog/ 2>/dev/null
git rm -r docs/marketing/ 2>/dev/null
git rm -r docs/video/ 2>/dev/null
```

- [ ] **Step 6: Clean up unused templates (keep bounded-context-template.md)**

```bash
git rm docs/templates/api-reference-template.md
git rm docs/templates/architecture-template.md
git rm docs/templates/development-guide-template.md
git rm docs/templates/TEAM-INSTRUCTIONS.md
git rm docs/templates/testing-guide-template.md
git rm docs/templates/troubleshooting-template.md
```

- [ ] **Step 7: Commit cleanup**

```bash
git add -A docs/
git commit -m "docs: remove dead HTML mockups, archives, and unused templates (~8MB)

Removes:
- 7 RAG strategy HTML diagrams (replaced by markdown)
- 5 frontend mockup HTMLs (layouts implemented)
- 4 roadmap/debug HTMLs (duplicated in .md)
- epic-4068 archive (completed, 6.5MB)
- blog/marketing/video empty dirs
- 6 unused doc templates (kept bounded-context-template.md)"
```

---

### Task 2: Fix Broken Internal Links

**Files:**
- Modify: `docs/api/multi-agent-system.md` (broken link to context-engineering)
- Modify: `docs/bounded-contexts/authentication.md` (old path prefix)
- Modify: `docs/bounded-contexts/knowledge-base.md` (old path prefix)
- Modify: `docs/bounded-contexts/document-processing.md` (old path prefix)

- [ ] **Step 1: Find all broken references with old `01-architecture` prefix**

```bash
grep -r "01-architecture\|02-development\|03-api\|04-deployment\|05-testing" docs/ --include="*.md" -l
```

- [ ] **Step 2: Fix path references in affected files**

Replace all occurrences of old numeric prefixed paths:
- `../01-architecture/` → `../architecture/`
- `../02-development/` → `../development/`
- `../03-api/` → `../api/`
- `../04-deployment/` → `../deployment/`
- `../05-testing/` → `../testing/`

Use search-and-replace across all matched files.

- [ ] **Step 3: Fix multi-agent-system.md broken link**

In `docs/api/multi-agent-system.md`, find the link:
```markdown
[Context Engineering Framework](../01-context-engineering.md)
```
Replace with the correct path:
```markdown
[Context Engineering Framework](../development/agent-architecture/01-context-engineering.md)
```

- [ ] **Step 4: Verify no broken links remain**

```bash
grep -r "01-architecture\|02-development\|03-api\|04-deployment\|05-testing" docs/ --include="*.md" -l
# Expected: no results (or only archive/ files which are historical)
```

- [ ] **Step 5: Commit**

```bash
git add docs/
git commit -m "docs: fix broken internal links and old path prefixes

Updates numeric-prefixed paths (01-architecture → architecture) in
bounded context docs and fixes broken context-engineering link in
multi-agent-system.md."
```

---

### Task 3: Create README for UserLibrary Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/README.md`

UserLibrary is the largest undocumented BC (244 files). It manages user game collections, wishlists, session history, labels, sharing, suggestions, and checklists.

- [ ] **Step 1: Read key domain entities to understand the context**

```bash
# List domain entities and application commands/queries
find apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities -name "*.cs" | sort
find apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands -name "*Command.cs" | sort
find apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries -name "*Query.cs" | sort
```

- [ ] **Step 2: Write the README following existing Italian-language BC pattern**

Create `apps/api/src/Api/BoundedContexts/UserLibrary/README.md` with:
- **Responsabilità**: Collections, wishlist, game history, labels, sharing, suggestions, checklists
- **Funzionalità Principali**: Collection management, wishlist tracking, play session logging, label/tag system, share links, AI game suggestions, game checklists
- **Struttura**: Domain (PrivateGame, UserCollectionEntry, GameSession, GameLabel, LibraryShareLink, GameSuggestion, GameChecklist) → Application (CQRS with MediatR) → Infrastructure (EF Core repos, quota service)
- **Pattern Architetturali**: CQRS, MediatR, DDD, Repository Pattern
- **API Endpoints**: List all endpoints by reading routing files
- **Dipendenze**: GameManagement (game metadata), Authentication (user identity), SharedGameCatalog (community data)

Follow the same structure, depth, and Italian language as `GameManagement/README.md`.

- [ ] **Step 3: Verify README is well-formed**

```bash
# Verify file created and non-empty
wc -l apps/api/src/Api/BoundedContexts/UserLibrary/README.md
# Expected: 80+ lines
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/README.md
git commit -m "docs(user-library): add bounded context README

Documents collections, wishlists, labels, sharing, suggestions,
and checklists. 244 files, largest undocumented BC."
```

---

### Task 4: Create README for UserNotifications Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/README.md`

UserNotifications (192 files) handles email queuing, templates, Slack integration, notification preferences, and background jobs.

- [ ] **Step 1: Read key domain entities**

```bash
find apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Entities -name "*.cs" | sort
find apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands -name "*Command.cs" | sort
find apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure -name "*.cs" -type f | head -20
```

- [ ] **Step 2: Write the README**

Create `apps/api/src/Api/BoundedContexts/UserNotifications/README.md` with:
- **Responsabilità**: Notification delivery, email queuing, Slack integration, user preferences
- **Funzionalità Principali**: Email queue with templates, Slack builders (GameNightSlackBuilder, BadgeSlackBuilder, etc.), notification preferences, background job processing
- **Struttura**: Domain (Notification, NotificationPreferences, EmailQueueItem, EmailTemplate, SlackConnection) → Application → Infrastructure (job processors, Slack builders)
- **Pattern Architetturali**: CQRS, background jobs, template engine, multi-channel delivery
- **Dipendenze**: Authentication (user identity), Gamification (badge notifications), SessionTracking (game night notifications)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/README.md
git commit -m "docs(user-notifications): add bounded context README

Documents email queuing, Slack integration, notification preferences,
and background job processing. 192 files."
```

---

### Task 5: Create README for BusinessSimulations Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/BusinessSimulations/README.md`

BusinessSimulations (61 files) manages financial ledger entries, cost scenarios, resource forecasts, user budgets, and growth patterns.

- [ ] **Step 1: Read key domain entities**

```bash
find apps/api/src/Api/BoundedContexts/BusinessSimulations/Domain/Entities -name "*.cs" | sort
find apps/api/src/Api/BoundedContexts/BusinessSimulations/Application -name "*Command.cs" -o -name "*Query.cs" | sort
```

- [ ] **Step 2: Write the README**

Create `apps/api/src/Api/BoundedContexts/BusinessSimulations/README.md` with:
- **Responsabilità**: Financial simulation and budget tracking for board game collections
- **Funzionalità Principali**: Ledger entries (purchases, trades, sales), cost scenarios, resource forecasts, user budgets, growth pattern analysis
- **Struttura**: Domain (CostScenario, LedgerEntry, ResourceForecast, UserBudget) → Application → Infrastructure (LedgerEntryRepository, CostScenarioRepository, LedgerTrackingService)
- **Pattern Architetturali**: CQRS, MediatR, DDD
- **Dipendenze**: UserLibrary (collection data), GameManagement (game pricing)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/BusinessSimulations/README.md
git commit -m "docs(business-simulations): add bounded context README

Documents ledger, cost scenarios, resource forecasts, and budget
tracking. 61 files."
```

---

### Task 6: Create README for Gamification Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Gamification/README.md`

Gamification (20 files) is the smallest undocumented BC. Manages achievements, badges, user unlocks, categories, and rarity levels.

- [ ] **Step 1: Read key domain entities**

```bash
find apps/api/src/Api/BoundedContexts/Gamification/ -name "*.cs" | sort
```

- [ ] **Step 2: Write the README**

Create `apps/api/src/Api/BoundedContexts/Gamification/README.md` with:
- **Responsabilità**: Achievement and badge system for user engagement
- **Funzionalità Principali**: Achievement definitions (categories, rarity), user achievement tracking, unlock triggers, leaderboards
- **Struttura**: Domain (Achievement, UserAchievement, AchievementCategory, AchievementRarity) → Application → Infrastructure (AchievementRepository, UserAchievementRepository)
- **Pattern Architetturali**: CQRS, MediatR, DDD, event-driven unlocks
- **Dipendenze**: Authentication (user identity), UserNotifications (badge notification delivery)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Gamification/README.md
git commit -m "docs(gamification): add bounded context README

Documents achievements, badges, user unlocks, categories, and
rarity system. 20 files."
```

---

### Task 7: Create README for AgentMemory Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/README.md`

AgentMemory (50 files) manages game memory, group memory, player memory, house rules, and memory notes for AI agents.

- [ ] **Step 1: Read key domain entities**

```bash
find apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Entities -name "*.cs" | sort
find apps/api/src/Api/BoundedContexts/AgentMemory/Application -name "*Command.cs" -o -name "*Query.cs" | sort
```

- [ ] **Step 2: Write the README**

Create `apps/api/src/Api/BoundedContexts/AgentMemory/README.md` with:
- **Responsabilità**: Persistent memory for AI game agents — stores user preferences, group dynamics, player stats, and house rules
- **Funzionalità Principali**: Game memory (per-game context), group memory (play group preferences), player memory (individual stats/preferences), house rules tracking, memory notes
- **Struttura**: Domain (GameMemory, GroupMemory, PlayerMemory, HouseRule, MemoryNote) → Application → Infrastructure (GameMemoryRepository, GroupMemoryRepository, PlayerMemoryRepository)
- **Pattern Architetturali**: CQRS, MediatR, DDD
- **Dipendenze**: KnowledgeBase (AI agent context), GameManagement (game metadata), Authentication (user identity)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/AgentMemory/README.md
git commit -m "docs(agent-memory): add bounded context README

Documents AI agent persistent memory: game, group, player memory,
house rules, and notes. 50 files."
```

---

### Task 8: Create README for DatabaseSync Bounded Context

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DatabaseSync/README.md`

DatabaseSync (39 files) handles SSH tunnel management, schema/data diffing, migration preview and apply between environments.

- [ ] **Step 1: Read key domain entities**

```bash
find apps/api/src/Api/BoundedContexts/DatabaseSync/ -name "*.cs" | sort
```

- [ ] **Step 2: Write the README**

Create `apps/api/src/Api/BoundedContexts/DatabaseSync/README.md` with:
- **Responsabilità**: Cross-environment database synchronization for admin operations
- **Funzionalità Principali**: SSH tunnel management (connect/disconnect/status), schema diff (compare structures), data diff (compare records), migration preview and apply, sync direction control
- **Struttura**: Domain (SyncDirection, TunnelState, MigrationInfo, SchemaDiffResult, DataDiffResult) → Application → Infrastructure (RemoteDatabaseConnector, SshTunnelClient, SchemaDiffEngine, DataDiffEngine)
- **Pattern Architetturali**: CQRS, MediatR, adapter pattern for remote connections
- **Dipendenze**: Administration (admin auth), SystemConfiguration (environment config)
- **⚠️ Security note**: Admin-only operations, SSH credentials managed via secrets

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/README.md
git commit -m "docs(database-sync): add bounded context README

Documents SSH tunneling, schema/data diffing, and migration
management. Admin-only. 39 files."
```

---

### Task 9: Create READMEs for GameToolbox and GameToolkit Bounded Contexts

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/README.md`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/README.md`

These are two related but distinct contexts: GameToolbox (24 files) manages game aid templates/tools, while GameToolkit (38 files) handles AI-assisted toolkit generation with widgets.

- [ ] **Step 1: Read key domain entities for both**

```bash
find apps/api/src/Api/BoundedContexts/GameToolbox/Domain -name "*.cs" | sort
find apps/api/src/Api/BoundedContexts/GameToolkit/Domain -name "*.cs" | sort
```

- [ ] **Step 2: Write GameToolbox README**

Create `apps/api/src/Api/BoundedContexts/GameToolbox/README.md` with:
- **Responsabilità**: Game aid templates and reusable play tools
- **Funzionalità Principali**: Toolbox definitions, toolbox templates, individual tools, phase tracking, card zone management
- **Struttura**: Domain (Toolbox, ToolboxTemplate, ToolboxTool, Phase, CardZone) → Application → Infrastructure (ToolboxRepository, ToolboxTemplateRepository)
- **Pattern Architetturali**: CQRS, MediatR, DDD, template pattern
- **Dipendenze**: GameManagement (game metadata)

- [ ] **Step 3: Write GameToolkit README**

Create `apps/api/src/Api/BoundedContexts/GameToolkit/README.md` with:
- **Responsabilità**: AI-assisted toolkit generation with interactive widgets for game sessions
- **Funzionalità Principali**: AI toolkit generation from game rules, dashboard layout, score tracking widgets, timer widgets, dice roller widgets, custom widget types
- **Struttura**: Domain (GameToolkit, Toolkit, ToolkitWidget, DiceType, ScoreType, TimerType) → Application → Infrastructure (ToolkitRepository, GameToolkitRepository)
- **Pattern Architetturali**: CQRS, MediatR, DDD, AI-assisted generation
- **Dipendenze**: GameManagement (game metadata), KnowledgeBase (AI generation), GameToolbox (template reuse)
- **Note**: Distinguish from GameToolbox — Toolkit is AI-generated and widget-based; Toolbox is template-based and manual

- [ ] **Step 4: Commit both**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/README.md
git add apps/api/src/Api/BoundedContexts/GameToolkit/README.md
git commit -m "docs(game-toolbox,game-toolkit): add bounded context READMEs

GameToolbox: template-based game aid tools (24 files)
GameToolkit: AI-assisted widget toolkit generation (38 files)
Documents the distinction between manual templates and AI generation."
```

---

### Task 10: Resolve Dashboard Hub Duplicates & Add Deprecation Markers

**Files:**
- Delete: `docs/archive/epics/3901-dashboard-hub/DASHBOARD-HUB-INDEX.md` (duplicate of active)
- Delete: `docs/archive/epics/3901-dashboard-hub/DASHBOARD-HUB-QUICK-REFERENCE.md` (duplicate of active)
- Modify: `docs/migrations/dashboard-v1-to-v2.md` (add DEPRECATED header)

- [ ] **Step 1: Verify active copies exist in docs/frontend/**

```bash
ls -la docs/frontend/DASHBOARD-HUB-INDEX.md
ls -la docs/frontend/DASHBOARD-HUB-QUICK-REFERENCE.md
```
Expected: both files exist in `docs/frontend/`

- [ ] **Step 2: Remove duplicates from archive**

```bash
git rm docs/archive/epics/3901-dashboard-hub/DASHBOARD-HUB-INDEX.md
git rm docs/archive/epics/3901-dashboard-hub/DASHBOARD-HUB-QUICK-REFERENCE.md
```

- [ ] **Step 3: Add deprecation header to dashboard migration doc**

Add to the top of `docs/migrations/dashboard-v1-to-v2.md`:
```markdown
> ⚠️ **DEPRECATED** (2026-03-25): Dashboard v1 components have been fully replaced by the unified layout system. This migration guide is kept for historical reference only. See `docs/superpowers/specs/2026-03-24-layout-redesign-design.md` for current layout architecture.
```

- [ ] **Step 4: Commit**

```bash
git add docs/
git commit -m "docs: resolve Dashboard Hub duplicates, add deprecation markers

Removes duplicate Dashboard Hub docs from archive (active copies in
docs/frontend/). Marks dashboard-v1-to-v2 migration as deprecated."
```

---

### Task 11: Delete docs-review-needed.md and Update Documentation Index

**Files:**
- Delete: `docs-review-needed.md` (all recommendations executed in Task 1)
- Modify: `docs/README.md` (add section for newly documented BCs)

- [ ] **Step 1: Delete the review file (all items addressed)**

```bash
git rm docs-review-needed.md
```

- [ ] **Step 2: Update docs/README.md bounded contexts section**

In the bounded contexts section of `docs/README.md`, ensure all 19 contexts are listed (previously only 10 were documented). Add the 8 new ones with their README paths:
- AgentMemory, BusinessSimulations, DatabaseSync, GameToolbox, GameToolkit, Gamification, UserLibrary, UserNotifications

- [ ] **Step 3: Verify documentation coverage**

```bash
# Count BC READMEs — should now be 18 of 19 (or 19 of 19)
find apps/api/src/Api/BoundedContexts/ -name "README.md" | wc -l
# Expected: 18 (was 10)
```

- [ ] **Step 4: Final commit**

```bash
git add docs-review-needed.md docs/README.md
git commit -m "docs: remove completed review checklist, update docs index

All recommendations from docs-review-needed.md have been executed.
Updates docs/README.md to reflect 18/19 bounded contexts now documented
(was 10/19, 53% → 95% coverage)."
```

---

## Post-Completion Summary

| Metric | Before | After |
|--------|--------|-------|
| BC README coverage | 10/19 (53%) | 18/19 (95%) |
| Dead HTML files | 19 files (~500KB) | 0 |
| Archive bloat | 6.5MB epic-4068 | Removed |
| Unused templates | 7 | 1 (bounded-context-template.md) |
| Broken internal links | 5+ | 0 |
| Duplicate docs | 2 pairs | 0 |
| docs-review-needed.md | 166 lines of debt | Resolved & deleted |

**Total commits**: 11 (one per task)
**Estimated file changes**: ~50 deletions, 8 creations, ~10 modifications
