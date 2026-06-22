# Stage 2 codemod dry-run report

- **Generated**: 2026-05-11T15:29:46.772Z
- **Mode**: `apply`
- **Audit source**: `docs/for-developers/audits/2026-05-11-mockup-conformity.json`
- **Spec ref**: docs/for-developers/specs/2026-05-11-design-system-deversioning.md §3
- **Issue**: #1025 (umbrella #1023)

## Counts

| Metric | Count |
| --- | --- |
| Renames planned | 322 |
| Deletes planned | 3 |
| Collisions total | 26 |
| Collisions blocker | 0 |
| Collisions warning | 26 |
| Collisions info | 0 |
| Import statements rewritten | 388 |
| Files touched by import fix | 77 |

## Verdict

**safe-to-apply** — zero blocker collisions detected. Phase B may proceed.

## Collisions

### name-watchlist (26)

| Severity | Source | Target | Detail | Recommendation |
| --- | --- | --- | --- | --- |
| warning | `apps/web/src/components/v2/gamebook/ConfidenceBadge.tsx` | `apps/web/src/components/features/gamebook/ConfidenceBadge.tsx` | Basename 'ConfidenceBadge' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/v2/gamebook/StepIndicator.tsx` | `apps/web/src/components/features/gamebook/StepIndicator.tsx` | Basename 'StepIndicator' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/v2/game-chat/ConfidenceBadge.tsx` | `apps/web/src/components/features/game-chat/ConfidenceBadge.tsx` | Basename 'ConfidenceBadge' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/v2/game-chat/TypingIndicator.tsx` | `apps/web/src/components/features/game-chat/TypingIndicator.tsx` | Basename 'TypingIndicator' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/v2/session-summary/SessionDiaryTimeline.tsx` | `apps/web/src/components/features/session-summary/SessionDiaryTimeline.tsx` | Basename 'SessionDiaryTimeline' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/btn/btn.tsx` | `apps/web/src/components/ui/btn/btn.tsx` | Basename 'btn' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/drawer/drawer.tsx` | `apps/web/src/components/ui/drawer/drawer.tsx` | Basename 'drawer' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/input-field/input-field.tsx` | `apps/web/src/components/ui/input-field/input-field.tsx` | Basename 'input-field' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/step-progress/step-progress.tsx` | `apps/web/src/components/ui/step-progress/step-progress.tsx` | Basename 'step-progress' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/toggle-switch/toggle-switch.tsx` | `apps/web/src/components/ui/toggle-switch/toggle-switch.tsx` | Basename 'toggle-switch' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/v2/session-summary/ConnectionBar.tsx` | `apps/web/src/components/features/session-summary/ConnectionBar.tsx` | Basename 'ConnectionBar' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/btn/btn.test.tsx` | `apps/web/src/components/ui/btn/btn.test.tsx` | Basename 'btn.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/btn/index.ts` | `apps/web/src/components/ui/btn/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/drawer/drawer.test.tsx` | `apps/web/src/components/ui/drawer/drawer.test.tsx` | Basename 'drawer.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/drawer/index.ts` | `apps/web/src/components/ui/drawer/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/drawer/use-drawer-breakpoint.test.ts` | `apps/web/src/components/ui/drawer/use-drawer-breakpoint.test.ts` | Basename 'use-drawer-breakpoint.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/drawer/use-drawer-breakpoint.ts` | `apps/web/src/components/ui/drawer/use-drawer-breakpoint.ts` | Basename 'use-drawer-breakpoint' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/entity-card/entity-card.test.tsx` | `apps/web/src/components/ui/entity-card/entity-card.test.tsx` | Basename 'entity-card.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/entity-card/entity-card.tsx` | `apps/web/src/components/ui/entity-card/entity-card.tsx` | Basename 'entity-card' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/entity-card/index.ts` | `apps/web/src/components/ui/entity-card/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/input-field/index.ts` | `apps/web/src/components/ui/input-field/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/input-field/input-field.test.tsx` | `apps/web/src/components/ui/input-field/input-field.test.tsx` | Basename 'input-field.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/step-progress/index.ts` | `apps/web/src/components/ui/step-progress/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/step-progress/step-progress.test.tsx` | `apps/web/src/components/ui/step-progress/step-progress.test.tsx` | Basename 'step-progress.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/toggle-switch/index.ts` | `apps/web/src/components/ui/toggle-switch/index.ts` | Basename 'index' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |
| warning | `apps/web/src/components/ui/v2/toggle-switch/toggle-switch.test.tsx` | `apps/web/src/components/ui/toggle-switch/toggle-switch.test.tsx` | Basename 'toggle-switch.test' matches Stage 2 known-collision watchlist. Different cluster directories prevent FS conflict, but import disambiguation matters. | After rename, audit consumer imports for ambiguous re-exports or aliasing. |

## Deletes planned (3)

| Cluster | Source | Reason |
| --- | --- | --- |
| agent-detail | `apps/web/src/components/v2/agent-detail/AgentCharacterSheet.tsx` | TODO stub (return null). Orphan: matrix renamed this entry to AgentHero (PR #711). No canonical successor. |
| agents | `apps/web/src/components/v2/agents/AgentDetailPanel.tsx` | TODO stub. Removed from /agents scope per matrix B.2 review (grid not master-detail). Orphan candidate. |
| agents | `apps/web/src/components/v2/agents/AgentsSidebarList.tsx` | TODO stub. Removed from /agents scope per matrix B.2 review. Orphan. |

## Renames planned (322)

Full list is in the JSON sibling. Sample (first 20):

| Cluster | Source | Target |
| --- | --- | --- |
| agent-detail | `apps/web/src/components/v2/agent-detail/AgentDangerZone.tsx` | `apps/web/src/components/features/agent-detail/AgentDangerZone.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/AgentHero.tsx` | `apps/web/src/components/features/agent-detail/AgentHero.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/AgentSettingsForm.tsx` | `apps/web/src/components/features/agent-detail/AgentSettingsForm.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/ChatHistoryTimeline.tsx` | `apps/web/src/components/features/agent-detail/ChatHistoryTimeline.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/KbDocList.tsx` | `apps/web/src/components/features/agent-detail/KbDocList.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/PersonaCard.tsx` | `apps/web/src/components/features/agent-detail/PersonaCard.tsx` |
| agent-detail | `apps/web/src/components/v2/agent-detail/SystemPromptViewer.tsx` | `apps/web/src/components/features/agent-detail/SystemPromptViewer.tsx` |
| agents | `apps/web/src/components/v2/agents/AgentFilters.tsx` | `apps/web/src/components/features/agents/AgentFilters.tsx` |
| agents | `apps/web/src/components/v2/agents/AgentsResultsGrid.tsx` | `apps/web/src/components/features/agents/AgentsResultsGrid.tsx` |
| agents | `apps/web/src/components/v2/agents/EmptyAgents.tsx` | `apps/web/src/components/features/agents/EmptyAgents.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/ActionCard.tsx` | `apps/web/src/components/features/gamebook/ActionCard.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/CameraViewfinder.tsx` | `apps/web/src/components/features/gamebook/CameraViewfinder.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/CancelModal.tsx` | `apps/web/src/components/features/gamebook/CancelModal.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/ConfidenceBadge.tsx` | `apps/web/src/components/features/gamebook/ConfidenceBadge.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/DesktopDropFallback.tsx` | `apps/web/src/components/features/gamebook/DesktopDropFallback.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/EmptyGamebooks.tsx` | `apps/web/src/components/features/gamebook/EmptyGamebooks.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/GamebookCard.tsx` | `apps/web/src/components/features/gamebook/GamebookCard.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/GamebookCardSkeleton.tsx` | `apps/web/src/components/features/gamebook/GamebookCardSkeleton.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/GamebookHero.tsx` | `apps/web/src/components/features/gamebook/GamebookHero.tsx` |
| gamebook | `apps/web/src/components/v2/gamebook/GamebookPlayShell.tsx` | `apps/web/src/components/features/gamebook/GamebookPlayShell.tsx` |
| … | _302 more in JSON_ | … |

## Import rewrites — sample

First 15 of 388:

| File | Old specifier | New specifier |
| --- | --- | --- |
| `apps/web/src/lib/color-utils.ts` | `@/components/ui/v2/entity-tokens` | `@/components/ui/entity-tokens` |
| `apps/web/src/hooks/queries/useGameChat.ts` | `@/components/v2/game-chat/GameChatHeader` | `@/components/features/game-chat/GameChatHeader` |
| `apps/web/src/app/(authenticated)/notifications/page.tsx` | `@/components/ui/v2/entity-tokens` | `@/components/ui/entity-tokens` |
| `apps/web/src/app/(public)/faq/page.tsx` | `@/components/ui/v2/faq` | `@/components/ui/faq` |
| `apps/web/src/app/(public)/join/page-client.tsx` | `@/components/ui/v2/join` | `@/components/ui/join` |
| `apps/web/src/app/(public)/join/page-client.tsx` | `@/components/ui/v2/join` | `@/components/ui/join` |
| `apps/web/src/app/(public)/pricing/page.tsx` | `@/components/ui/v2/pricing-card/pricing-card` | `@/components/ui/pricing-card/pricing-card` |
| `apps/web/src/app/(public)/shared-games/page-client.tsx` | `@/components/ui/v2/shared-games` | `@/components/ui/shared-games` |
| `apps/web/src/components/library/v2/GameDrawerContent.tsx` | `@/components/ui/v2/entity-tokens` | `@/components/ui/entity-tokens` |
| `apps/web/src/components/library/v2/LibraryDesktop.tsx` | `@/components/ui/v2/entity-card/entity-card` | `@/components/ui/entity-card/entity-card` |
| `apps/web/src/components/library/v2/LibraryMobile.tsx` | `@/components/ui/v2/entity-card/entity-card` | `@/components/ui/entity-card/entity-card` |
| `apps/web/src/components/v2/agent-detail/index.ts` | `./AgentHero` | `@/components/features/agent-detail/AgentHero` |
| `apps/web/src/components/v2/agent-detail/index.ts` | `./AgentTabs` | `@/components/features/agent-detail/AgentTabs` |
| `apps/web/src/components/v2/agent-detail/index.ts` | `./PersonaCard` | `@/components/features/agent-detail/PersonaCard` |
| `apps/web/src/components/v2/agent-detail/index.ts` | `./SystemPromptViewer` | `@/components/features/agent-detail/SystemPromptViewer` |

---

Generated by `scripts/codemod/drop-v2-prefix.ts`. Re-run with `--verbose` for debug output. Phase B applies with `--apply`.
