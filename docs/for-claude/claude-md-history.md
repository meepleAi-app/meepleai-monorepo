# CLAUDE.md — Historical Archive

Material extracted from `CLAUDE.md` during the 2026-07-31 consolidation to keep the
root guide operational. Nothing here is a live rule — it is **resolved history,
shipped-implementation diaries, and completed migrations**, preserved for traceability.
Live rules that survived (e.g. "don't re-introduce a persistent desktop sidebar",
token/ESLint rules, active BGG freeze) stay in `CLAUDE.md`.

---

## Known Flaky Tests — resolved history

Triage chain: #1349 (closed, Phase 2d carryover) → #1422 (2026-05-21, 12 SharedGameId/PDF cluster resolved) → 2026-05-22 (4 baseline failures cleared; S3Storage entry was stale) → 2026-06-09 (#1887 PdfDocument_SevenStateProgression cleared via PR #2038, baseline empty) → 2026-06-12 (#2270 cleared orphan `asse-b-axe.test.tsx` from PR #2161 layout-shell dedupe) → 2026-06-13 (#2266 cleared 2 nav-test follow-ups from PR #2279 #2190 — `AppTopBar.test.tsx` + `MobileBottomBar.test.tsx` expecting label "Hub" after rename to "Games").

**Resolved 2026-06-12 (#2270)**: `apps/web/__tests__/asse-b-axe.test.tsx` — Frontend Tests shard 2/3 fail with `Failed to resolve import "@/components/layout/main-nav/MainNavList"`. Orphan test left behind by PR [#2161](https://github.com/meepleAi-app/meepleai-monorepo/pull/2161) "refactor(frontend): dedupe layout shell + retire PageHeader legacy", which deleted 8 source files (`MainSidebar.tsx`, `MainNavList.tsx`, `main-nav-config.ts`, `filter-nav-by-permission.ts` + their tests) but missed the asse-B axe smoke test that imported `MainNavList` + `MAIN_NAV_ITEMS` + `WizardModal`. Deleted outright in that PR.

**Resolved 2026-06-09 (#1887)**: `PdfDocument_SevenStateProgression_ShouldAdvanceThroughAllStates` (fix PR [#2038](https://github.com/meepleAi-app/meepleai-monorepo/pull/2038)) — assertion split into `HaveCount(7)` + typed `OfType<>()` (6 `PdfStateChangedEvent` + 1 `KbDocIndexedEvent` raised on the Ready transition for the activity rail, per BE-3 #1590 B2). Extra 7th event is `KbDocIndexedEvent` from `TransitionTo()` (`PdfDocument.cs:433-443`).

**Resolved 2026-05-22**: 3 documented baseline failures fixed + 1 stale entry removed.
- `Should_Fail_When_GameId_Is_Empty` — fixed by adding `Cascade(CascadeMode.Stop)` to `CreateRuleConflictFaqCommandValidator.RuleFor(x => x.GameId)` so the async `GameExists` check is skipped when `NotEmpty()` already failed.
- `Handle_EmptyGuid_ReturnsNull` (in `GetGameByIdQueryHandlerTests`) — fixed by short-circuiting the handler on `Guid.Empty` before constructing `GameRef.Shared(...)`.
- `Handle_WithSearchFilter_ReturnsMatchingGames` — moved to `Integration/GameManagement/GetAllGamesQueryHandlerIntegrationTests.cs` (Testcontainers Postgres), where `EF.Functions.ILike` translates to SQL `ILIKE`.
- `*_S3Storage_*` (2 tests) — entry was stale: all unit tests in `S3BlobStorageServiceTests` pass; the 11 skipped in `S3BlobStorageIntegrationTests` only require Docker.

**Resolved in #1422 (2026-05-21)**: 12 undocumented SharedGameId/PDF cluster failures. Root cause: regression from PR #1345/#1347 (Phase 2d delete `GameEntity` + drop `games` table). Test fixtures still relied on the dropped `pdf_documents.GameId` column → handlers filtering on `SharedGameId` returned 0 items. Resolution: **11 fixed** via fixture drift correction + **1 deleted** (`Handle_WithSharedGameId_ResolvesToActualGameId`).

---

## Design System — completed migrations & freezes

**Design System De-versioning — COMPLETE 2026-05-18** (umbrella #1023 closed, Stage 3 #1026 closed). All 3 stages shipped (Stage 1 audit #1024 → Stage 2 path-migration #1025/PR #1032 → Stage 3 conformity fixes #1026). Canonical paths active: feature compositions → `apps/web/src/components/features/<feature>/`; primitives → `apps/web/src/components/ui/<primitive>/`. Legacy `apps/web/src/components/v2/**` and `ui/v2/**` are empty post-codemod — **do not re-introduce them** (this rule stays live in CLAUDE.md). Stage 3 conformity fixes shipped per cluster + DetailPageLayout primitive (PR #1112). Spec: [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](./../for-developers/specs/2026-05-11-design-system-deversioning.md).

**Visual Gate REMOVED 2026-05-20** — the entire mockup/visual-regression suite (`apps/web/e2e/visual-conformity/`, `visual-migrated/`, `v2-states/`, `visual-mockups/`) retired with its 9 supporting workflows and Playwright projects. False-positive rate outweighed value; replacement = manual designer review on PRs. Issues #1066 / #1069 / #1269 closed by the removal PR.

**SP6 v2 expansion FREEZE — lifted 2026-05-10** (issued 2026-05-06 per #808, tied to A11y audit #807) by PR #876 (token redesign, AA-compliant CSS vars + entity utilities). #807 and #808 CLOSED. **A11y CI restore COMPLETE 2026-05-18** via #1094 Phase D gate flip: `Frontend - A11y E2E` is now blocking (`continue-on-error` removed, in required-jobs). Final v11 axe run: 0 color-contrast + 0 ARIA violations across 96 tests (v4 baseline 103+11 → v11 0). Companion #1015 also CLOSED. Original blocker #752 closed 2026-05-12 via #876. Audit: [`docs/for-developers/audits/a11y-color-contrast-restoration.md`](./../for-developers/audits/a11y-color-contrast-restoration.md). **Live rule (in CLAUDE.md): any axe AA fail = real regression, investigate, don't skip.**

**Token Canonicalization — Tier 1+2+3+4 complete**, 0 project-wide violations (2026-05-12, spec [`2026-05-12-token-canonicalization.md`](./../for-developers/specs/2026-05-12-token-canonicalization.md)). Runtime imports `admin-mockups/design_files/tokens.css` as `apps/web/src/styles/design-tokens-canonical.css`. Legacy `token-bridge.css` removed in DS-16. Residual legacy `var(--*)` literals inventoried in the [MeepleCard/CSS drift audit](./../for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md) § css-layers.

**Mockup DS-17 patterns (detailed history)**:
- **DS-17-2 (#2070) mockup token guard**: `pnpm lint:tokens:mockups` scans `admin-mockups/**` for forbidden CSS literals, writes `audits/2026-06-09-mockup-token-violations.{json,md}`. CI runs `--strict --max-baseline 1500` whitelist-incremental. Spec: `docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md`. Umbrella #2063.
- **DS-17 Phase 2.5+ (#2113) migration pattern**: 1 story per mockup con argTypes matrix (DEC-P3-3), `FrameNN_ShortName` stories, fidelity.json companion. Pilot: Library 9 + GameDetail 3 Desktop frames. Visual pixel-gate DESCOPED FROM CI 2026-07-15 (committed baselines `*-win32.png`, CI on ubuntu → no real signal); retained as local dev tool (`pnpm test:storybook:snapshots`). Fix log: `docs/for-developers/frontend/page-mock-story-pattern.md`.
- **DS-17 Phase B (#2127) mockup audit**: every mockup in `admin-mockups/design_files/` carries `<name>.fidelity.json` with `design_intent` ∈ {current, forward-refactor, forward-refactor-obsolete}. `pnpm lint:fidelity` validates. 5 obsoletes flagged.
- **DS-17-1 (#2069) + sweep (#2084) annotation pattern**: every user-reachable `page.tsx` mapped in `admin-mockups/MOCKUPS_INDEX.md` carries a `@mockup` JSDoc block. `pnpm mockup-annotations:inject --apply`; audit `--denominator mappable --threshold 80` (blocking, 100% / 68 routes).

**Deferred decisions (planned for DS-16)**: `--admin-*` token family; `--mc-*` MeepleCard palette consolidation; `var(--bg-base)` → `var(--bg)` bridge removal; audit of file-level `eslint-disable local/no-hardcoded-color-utility` directives.

**V2 Migration Components (Phase 0)** — pre-stubbed the 46 feature components from SP4 wave 1+2 mockups under `apps/web/src/components/v2/<feature>/`. Superseded by the De-versioning above (v2 trees now empty). Matrix was [`docs/for-developers/frontend/v2-migration-matrix.md`](./../for-developers/frontend/v2-migration-matrix.md).

---

## GameNight / Session (Asse A–D) — shipped implementation diaries

Umbrella #1895. Domain model spec (live reference, still in CLAUDE.md): [`2026-06-04-gamenight-session-domain-model.md`](./../for-developers/specs/2026-06-04-gamenight-session-domain-model.md). Gap report: [`2026-06-04-claude-design-gap-report.md`](./../for-developers/audits/2026-06-04-claude-design-gap-report.md).

**Asse A (#1896)** — shipped 2026-06-05. WP1 (max 1 live) + WP2 (Session.StartedAt + invariant #15 + X-Warning-Code + mapping doc) + WP3 (polymorphic ScoreType 4 strategies + UpdateSessionScoresCommand + IDOR guard) + WP4 audit + WP5 acceptance. ~80 unit tests. 1 HIGH IDOR fixed post-merge in `c1efb4fb6`. Backend semantic mapping: demo "GameNight" ↔ `GameNightEvent`; "tagged player" ↔ `GameNightEvent.PreInvite` (Draft); "invited" ↔ `Publish()` (raises events → email via `GameNightEmailService`); `Session.IsLive` ↔ `StartedAt != null && FinalizedAt == null`. Invariant #10 via `GameNightEvent.StartCurrentSession()` → `MaxLiveSessionsExceededException` (HTTP 409). Invariant #15 via `SessionStartedHandler : INotificationHandler<SessionStartedDomainEvent>`.

**Asse B (#1897)** — shipped 2026-06-05. WP1 tokens + WP2 MainSidebar 8 voci + WP3 cascade-store DrawerStack + WP4 WizardModal primitive + WP5 StatePreview dev-tool + WP6 useNotificationsCounter SSE + WP7 integration. ~120 unit tests. **Note: desktop MainSidebar was rolled back in #1977 + deleted in #2158 — see the live rule in CLAUDE.md (AppTopBar is the single desktop nav source).**

**Asse C (#1898)** — shipped 2026-06-05. WP1 BE FriendsActivity endpoint (`GET /api/v1/dashboard/friends-activity?limit=10`) + WP2 ProssimiSection + WP3 RecentiSection + WP4 SuggestedSection + WP5 FriendsActivitySection + WP6 GameNightDrawerContent + WP7 DashboardClient refactor (5 entity sections → 4 priority sections). ~75 unit tests. Recenti BE endpoint for completed-GN list not yet wired (silent fallback).

**Asse D P1 (#1899 follow-up)** — shipped 2026-06-05. Polymorphic ScoreType editor (Points/BinaryWin/Objectives/Ranking) wiring `UpdateSessionScoresCommand`. `useUpdateSessionScores` hook; wire scores page (Points+non-host → legacy `ScoreBoard`; host or non-Points → `PolymorphicScoreEditor` + 500ms autosave). ~36 unit tests. `MVP_OBJECTIVES_CATALOGUE` placeholder pending game-catalogue wiring.

**Asse D P2 (#1899 follow-up)** — shipped 2026-06-05. `/games` hub multi-tab refactor, Discover default tab (invariant #20). `DiscoverHub` extracted; `/games/page.tsx` → 4-tab hub (discover/catalogo/trending/community); `/discover` standalone preserved. Resolves broken sidebar link Games → `/games?tab=discover`.

**Asse D P3 (#1899 follow-up)** — shipped 2026-06-05. `/onboarding` 3-step generic wizard using `WizardModal` (replaces `OnboardingTourClient`). Reuses `InterestsStep` + `FirstGameStep`; step 3 `InviteFriendComingSoonStep` placeholder. `FirstGameStep` uses internal catalog (BGG user-side blocked per #1903). Invited-user `OnboardingWizard` untouched.

---

## Session live shell (epic #2354) — shipped implementation diaries

- **G1 layout (#2374)** — 2026-06-15, SessionLiveView desktop → 2-col 60/40 grid (LEFT chat+log, RIGHT polymorphic tabs Score/Turn/Widget/Notes). ChatAgentPanel primitive. Legacy URL `?tab=tools|chat|notes` back-compat via `parseLiveTab`. Mobile bottom-sheet (T9). Plan: `docs/superpowers/plans/2026-06-15-issue-2374-session-live-g1-3-col-layout.md`.
- **G5a Block A (#2389, PR #2428)** — merged 2026-06-17. Polymorphic store contract + SignalR `ScoringConfigured` + `useSessionScores` extension + `setScoringConfig` + ESLint `local/no-store-scores-direct` (warn).
- **G5a Block B (#2389)** — 2026-06-19. `SessionLiveView` consumes `scoringType`+`scoreData` via `mapScoreDataToPanelData` adapter, REST hydration race guard, aria-live placeholder.
- **G5a Block B+ (#2430)** — 2026-06-19. `ScoreTabContent` owns polymorphic scoring; Host → `PolymorphicScoreEditor` via `useUpdateSessionScores` (500ms debounce + flush); Player/Spectator → `ScoringPanelRenderer`. 5-class error matrix via sonner; 30s rate-limit deadline in `useLiveSessionStore.rateLimitedUntil`.
- **G5a Block C (#2389)** — 2026-06-19. Removed backward-compat `scores: Record<string, number>` + `updateScore`, legacy `ScoreUpdated` SignalR handler + `sendScore`/`NotifyScoreUpdated`. `ScoreBoard.tsx` refactored to playerId-keyed read-only. ESLint `local/no-store-scores-direct` promoted warn → **error** (live rule). Deferred: BE `NotifyScoreUpdated` removal; guest proposal flow.
