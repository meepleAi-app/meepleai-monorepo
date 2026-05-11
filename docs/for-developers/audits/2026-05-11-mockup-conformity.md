# Mockup Conformity Audit — Stage 1

| Field | Value |
|---|---|
| **Date** | 2026-05-11 |
| **Stage** | 1 (read-only audit) |
| **Spec** | [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](../specs/2026-05-11-design-system-deversioning.md) |
| **Issue** | #1024 (umbrella #1023) |
| **Branch** | `feature/issue-1024-deversioning-audit` |
| **Companion JSON** | [`2026-05-11-mockup-conformity.json`](./2026-05-11-mockup-conformity.json) |

## Method

Static analysis. The spec proposes Playwright + pixelmatch for visual diff (§3 Stage 1), but for **deterministic and reproducible** outcomes we use code-level signals:

1. **TODO stub detection**: presence of `// TODO: implement per admin-mockups` comment from matrix §11 stub format → `missing`.
2. **`return null` heuristic**: bare `return null` as the only function body → `missing` (cross-checked with TODO marker to exclude legit conditional-render fallbacks like `prefers-reduced-motion`).
3. **Matrix cross-reference**: components marked `done` with PR landed treated as `match` (acceptance criteria gate already enforced pixel/A11y/token compliance at merge time).
4. **Manual review flag**: components not enumerated in matrix or with structural ambiguity (potential primitive vs. composition).

Visual-diff verification of `match` rows is deferred to Stage 3 cluster-by-cluster PRs (per spec §3 Stage 3 AC3.1, `≤ 2% pixel threshold`).

## Outcome legend

| Symbol | Outcome | Stage 2/3 action |
|--------|---------|-------------------|
| OK | `match` | Stage 2 codemod rename |
| ~ | `partial` | Stage 3 targeted refactor |
| X | `diverge` | Stage 3 reimplementation |
| -- | `missing` | Stage 3 implementation |
| ? | `manual-review` | Designer/architect review |

## Summary stats

| Bucket | Count |
|---|---|
| Total v2 entries audited | 167 |
| `match` | 130 |
| `partial` | 2 |
| `diverge` | 0 |
| `missing` | 30 |
| `manual-review` | 5 |
| Total legacy non-prefixed | 36 |
| `keep` (canonical adapter / no shadow) | 27 |
| `merge` (consolidate with v2) | 9 |
| `delete` | 0 |
| Orphan mockups (no implementation) | 24 |

Match-rate: **77.8%** ready for Stage 2 rename. `missing` cluster is concentrated in three high-tier areas: game-nights (8), discover (6), kb-detail (6), toolkit-detail (6) — each a known matrix `pending` row.

## v2 components by cluster

### Cluster: agent-detail (9 entries)

| Status | Component Path | Mockup | Action | Notes |
|---|---|---|---|---|
| -- | `v2/agent-detail/AgentCharacterSheet.tsx` | sp4-agent-detail | implement | Orphan stub — matrix renamed to AgentHero. DELETE candidate. |
| OK | `v2/agent-detail/AgentDangerZone.tsx` | sp4-agent-detail | rename | PR #711 |
| OK | `v2/agent-detail/AgentHero.tsx` | sp4-agent-detail | rename | PR #711 — DetailHero candidate (spec §4) |
| OK | `v2/agent-detail/AgentSettingsForm.tsx` | sp4-agent-detail | rename | PR #711 |
| ? | `v2/agent-detail/AgentTabs.tsx` | sp4-agent-detail | review | Possible DetailTabs primitive promotion |
| OK | `v2/agent-detail/ChatHistoryTimeline.tsx` | sp4-agent-detail | rename | PR #711 |
| OK | `v2/agent-detail/KbDocList.tsx` | sp4-agent-detail | rename | PR #711 |
| OK | `v2/agent-detail/PersonaCard.tsx` | sp4-agent-detail | rename | PR #711 |
| OK | `v2/agent-detail/SystemPromptViewer.tsx` | sp4-agent-detail | rename | PR #711 |

### Cluster: agents (6 entries)

| Status | Component Path | Mockup | Action | Notes |
|---|---|---|---|---|
| -- | `v2/agents/AgentDetailPanel.tsx` | sp4-agents-index | implement | Orphan per matrix B.2; DELETE in Stage 2 |
| OK | `v2/agents/AgentFilters.tsx` | sp4-agents-index | rename | — |
| ~ | `v2/agents/AgentsHero.tsx` | sp4-agents-index | refactor | Spec example: hero padding diverge |
| OK | `v2/agents/AgentsResultsGrid.tsx` | sp4-agents-index | rename | MeepleCard wrapper |
| -- | `v2/agents/AgentsSidebarList.tsx` | sp4-agents-index | implement | Orphan; DELETE |
| OK | `v2/agents/EmptyAgents.tsx` | sp4-agents-index | rename | — |

### Cluster: discover (6 entries — all `missing`)

All six are TODO stubs. Tier L route, Phase 0.5 contract required per matrix before implementation.

`DiscoverHero`, `DiscoverSearchBox`, `EntityFilterPillBar`, `FooterCTA`, `HorizontalRow`, `RowScroller` → mockup `sp4-discover.jsx`.

### Cluster: game-chat (15 entries — all `match`)

All implemented in PR #918 (G1+G5). Mockup refs split between `sp4-game-chat-tab.html` and `sp4-citation-pdf-viewer.html`. Rename target: `components/features/game-chat/`. Two name collisions to handle in codemod:
- `ConfidenceBadge` (also in gamebook/)
- `TypingIndicator` (also in legacy `components/agent/` and `components/game-detail/`)
- `GameChatTabV2` → drop V2 suffix to `GameChatTab` per spec G1.

### Cluster: game-detail (8 entries — all `match`)

All from PR #702 (Wave C.1). Hero/Tabs are candidates for `DetailPageLayout` primitive extraction (spec §4).

`GameDetailAgentsList`, `GameDetailFaqList`, `GameDetailHero`, `GameDetailKbDocList`, `GameDetailKpiCards`, `GameDetailRulesAccordion`, `GameDetailSessionsRail`, `GameDetailTabsAnimated`.

### Cluster: game-nights (8 entries — all `missing`)

All TODO stubs. Tier L, Phase 0.5 required.

`CalendarDayCell`, `CalendarMonthGrid`, `DayDetailDrawer`, `FilterPillBar`, `GameNightListCard`, `GameNightsHeader`, `PlayerAvatars`, `StatusPill` → mockup `sp4-game-nights-index.jsx`.

### Cluster: gamebook (22 entries — 20 `match`, 2 `manual-review`)

SP6 Phase A/B/C implementations (PRs #790/#792/#794/#796/#800). All operational. Two not enumerated in matrix:
- `NanolithCampaignCTA` — verify mockup correspondence
- `NewCampaignDialog` — verify mockup correspondence

Name collisions: `ConfidenceBadge`, `StepIndicator` (latter overlaps with `ui/v2/step-progress` primitive).

### Cluster: games (6 entries)

| Status | Component | Notes |
|---|---|---|
| -- | `AdvancedFiltersDrawer.tsx` | TODO stub |
| OK | `GamesEmptyState.tsx` | Implementation present (matrix marks pending — outdated) |
| OK | `GamesFiltersInline.tsx` | 265 LOC |
| OK | `GamesHero.tsx` | Implementation present |
| OK | `GamesRecentRail.tsx` | PR #907 |
| OK | `GamesResultsGrid.tsx` | MeepleCard wrapper |

**Matrix drift**: matrix marks `pending` for components that are clearly implemented. Update matrix in Stage 2 PR.

### Cluster: kb-detail (6 entries — all `missing`)

All deferred (G4 v3 pivot per matrix — citation-pdf-viewer redesign 2026-05-10). Reconsider scope inclusion before Stage 2.

`ChunkSearchBox`, `KbChunkListPanel`, `KbChunkPreview`, `KbHeader`, `KbProcessingState`, `MarkdownRenderBlock` → `sp4-kb-detail.jsx`.

### Cluster: library (6 entries — 5 `match`, 1 `manual-review`)

PR #574 implementations.

| Status | Component | Notes |
|---|---|---|
| OK | `BulkSelectionBar`, `LibraryHeroDesktop`, `LibraryHybridGrid`, `LibraryTabs`, `RecentActivityRail` | PR #574 |
| ? | `EmptyLibrary.tsx` | Not in matrix Wave A row set |

### Cluster: player-detail (5 entries — all `match`)

PR #724 (Wave 3). Matrix marks `pending` — **outdated**. `PlayerHero` is DetailHero candidate.

### Cluster: players (4 entries — all `match`)

Wave 4 D1 done via PR #717.

### Cluster: session-live (14 entries — all `match`)

Foundation sub-PR + Interactions sub-PR #750 (lazy dialogs `PauseOverlay`, `EndgameDialog`).

### Cluster: sessions (8 entries — all `match`)

Wave done. `OutcomeBadge` returns null on empty-prop fallback (not stub).

### Cluster: session-summary (11 entries — 10 `match`, 1 `partial`)

PR #762 (Wave D.3).

| Status | Component | Notes |
|---|---|---|
| ~ | `ConnectionBar.tsx` | Duplicates canonical `ui/data-display/connection-bar/ConnectionBar.tsx`. Consolidate per spec §4. |
| OK | (10 others) | — |

Name collision: `SessionDiaryTimeline` also exists in legacy `components/session/` — verify canonical.

### Cluster: toolkit-detail (6 entries — all `missing`)

All TODO stubs. Tier M.

`PromptPreviewBlock`, `RatingBreakdown`, `Stars`, `ToolkitIncludesGrid`, `ToolkitSummaryPanel`, `VersionTimeline`. `Stars` is a candidate for `ui/` promotion.

### Cluster: ui-primitives (`ui/v2/*`, 24 entries)

All `match` except `entity-card` (manual-review — overlap with `MeepleCard` canonical). Rename target: drop `v2/` segment → `apps/web/src/components/ui/<primitive>/`.

Key risks at codemod time:
- `btn` → potential collision with existing `ui/button`
- `drawer` → collision with `ui/sheets`
- `input-field` → collision with `ui/forms/input`
- `toggle-switch` → collision with `ui/switch`
- `entity-card` → semantic overlap with `MeepleCard`
- `shared-game-detail/` → spec §4 designates this as **seed for `DetailPageLayout` primitive** (Stage 3 extraction target)
- `step-progress` → collision with `v2/gamebook/StepIndicator` (feature-specific composition)

## Legacy non-prefixed components (36 audited)

### `keep` (27)

Adapters over `MeepleCard` (per CLAUDE.md canonical pattern):

`MeepleGameCard` (games/), `MeepleAgentCard` (agent/), `MeeplePlayerCard` (players/), `MeepleLibraryGameCard` (library/), `MeepleUserLibraryCard` (library/), `MeepleSessionCard` (session/), `MeepleEventCard` (game-night/).

Domain-specific with no v2 shadow:

`BggSearchPanel`, `SimilarGamesCarousel`, `AgentMessage`, `AgentModeSelector`, `AgentSelector`, `AgentSelectorBadge`, `ChatErrorBoundary`, `MultiAgentChat`, `RecentLibraryCard`, `MeepleParticipantCard`, `MeepleResumeSessionCard`, `LiveSessionLayout`, `LiveSessionContextBar`, `Scoreboard` (toolkit-domain), `GameDiscoverDetail`, `AgentChatPanel`, `MeeplePdfReferenceCard`, `GameRelationships`, `SplitViewLayout`, `rulebook-section`.

### `merge` (9)

Legacy components shadowed by a v2 implementation (Stage 3 consolidation):

| Legacy path | v2 shadow |
|---|---|
| `agent/AgentCharacterSheet.tsx` | `v2/agent-detail/AgentHero.tsx` (matrix rename) |
| `agent/AgentTypingIndicator.tsx` | `v2/game-chat/TypingIndicator.tsx` |
| `session/SessionDiaryTimeline.tsx` | `v2/session-summary/SessionDiaryTimeline.tsx` |
| `discover/GameDiscoverHero.tsx` | `v2/discover/DiscoverHero.tsx` (currently stub) |
| `game-detail/GameDetailDesktop.tsx` | `v2/game-detail/*` cluster |
| `game-detail/game-hero-section.tsx` | `v2/game-detail/GameDetailHero.tsx` |
| `game-detail/stats-grid.tsx` | `v2/game-detail/GameDetailKpiCards.tsx` |
| `game-detail/GameTabsPanel.tsx` | `v2/game-detail/GameDetailTabsAnimated.tsx` |
| `game-detail/TypingIndicator.tsx` | `v2/game-chat/TypingIndicator.tsx` |

### `delete` (0)

No outright deletions — all legacy serves either a canonical role (adapter/domain-specific) or a merge target.

## Orphan mockups (24)

Mockups in `admin-mockups/design_files/` without an implementing v2 component:

| Mockup | Expected cluster | Notes |
|---|---|---|
| `sp4-add-game-bgg-step.jsx` | add-game-bgg | No v2/add-game/ |
| `sp4-add-game-pdf-dedup.jsx` | add-game-pdf-dedup | Likely served by library/PdfUploadModal legacy |
| `sp4-upload-wizard-extended.jsx` | upload-wizard | No v2 cluster |
| `sp4-kb-hub.jsx` | kb-hub | Hub route not yet built (spec §6) |
| `sp7-game-night-create.jsx` | game-night-create | Wave 4 pending |
| `sp7-game-night-detail-rsvp.jsx` | game-night-detail | Wave 4 G2 pending |
| `sp6-libro-game-glossary-editor.jsx` | gamebook-glossary | SP6 future |
| `sp6-libro-game-resume-state.jsx` | gamebook-resume | SP6 future |
| `nanolith-runthrough-*.html` (4) | nanolith-runthrough | Standalone; partial via TranslateViewer |
| `onboarding.jsx` | onboarding | Legacy onboarding/ exists |
| `public.jsx` | public-landing | Likely under landing/ |
| `sp3-how-it-works.jsx` | how-it-works | Public marketing |
| `sp3-legal.jsx` | legal | legacy components/legal/ |
| `sp3-library-public.jsx` | library-public | Partial via ui/v2/shared-games |
| `00-hub.html`, `01-screens.html`, `02-desktop-patterns.html`, `03-drawer-variants.html`, `04-design-system.html`, `05-dark-mode.html` | design-system | Reference docs, not feature clusters |
| `mobile-app.jsx` | mobile-app | Out-of-scope per spec §8 |

## Blockers and risks for Stage 2

1. **Name collisions at codemod time** (highest risk): `btn`, `drawer`, `input-field`, `toggle-switch`, `entity-card`, `ConfidenceBadge` (×2), `TypingIndicator` (×3), `StepIndicator`/`step-progress`, `SessionDiaryTimeline`, `ConnectionBar`. The codemod must produce a collision-detection dry-run report before file moves.

2. **Matrix drift**: matrix lists several `pending` rows (`GamesHero`, `GamesEmptyState`, `GamesFiltersInline`, `AgentsHero`, `AgentFilters`, `EmptyAgents`, `AgentsResultsGrid`, full `player-detail/*`) that are actually implemented. The Stage 2 PR should reconcile matrix `Status` column with reality before rename to avoid stale references.

3. **`session-summary/ConnectionBar` duplication** with `ui/data-display/connection-bar/ConnectionBar.tsx`: two parallel implementations risk drift. Spec §4 cites `ConnectionBar` as primitive seed — consolidate before Stage 2 rename, not after.

4. **Orphan stubs in `agents/`** (`AgentDetailPanel`, `AgentsSidebarList`): matrix B.2 review explicitly removed these from scope. They must be **deleted** in Stage 2, not renamed — confirm no orphan consumers via `git grep`.

5. **`kb-detail/` cluster deferred** (G4 v3 pivot): six stubs should be excluded from Stage 3 priority list until citation-pdf-viewer redesign resolves.

## Cluster prioritization (Stage 3 input)

Top 3 clusters with most divergences/gaps (excluding deferred):

| Cluster | Missing | Tier | Phase 0.5 required? |
|---|---|---|---|
| game-nights | 8 | L | Yes |
| toolkit-detail | 6 | M | No |
| discover | 6 | L | Yes |

`agents/AgentsHero` is the single `partial` non-stub gap requiring a targeted Stage 3 fix.

## References

- Spec: [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](../specs/2026-05-11-design-system-deversioning.md)
- Matrix (will be superseded): [`docs/for-developers/frontend/v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)
- Machine-readable counterpart: [`2026-05-11-mockup-conformity.json`](./2026-05-11-mockup-conformity.json)
- Issue umbrella: #1023 / audit issue: #1024
