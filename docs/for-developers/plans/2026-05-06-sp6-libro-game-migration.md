# SP6 Libro Game Migration Implementation Plan

**Status**: 🟡 IN PROGRESS — Iter 1.A/1.B done; SP6 Iter 1B follow-ups OPEN (#950-#954)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate libro-game (game-book) modality to v2 design. Ship Nanolith self-demo + `/gamebook` index Tier M + `/gamebook/upload` Tier L wizard. Add SP6 routes to v2-migration-matrix.md.

**Primary actor (Cockburn)**: Self-user (project owner) demonstrating libro-game modality to friends in person via personal demo session. Authenticated user with existing Nanolith physical books + ENG PDFs.

**Business goal**: Production-ready visual upgrade to existing partial /gamebook/upload + NEW /gamebook index + cherry-picked Nanolith demo route. NOT throwaway demo — full v2 design migration with production code quality + DoD compliance. Self-demo is the FIRST USE CASE; future production users (friends, beta testers) consume same code.

**Bounded context ownership (Fowler decision per spec-panel)**: `DocumentProcessing` BC owns the gamebook concept (gamebook = processed PDF/photo document with chunked content). `GameManagement` BC carries cross-context read-only references (gameId → gamebook existence). `KnowledgeBase` BC handles paragraph translation via existing chat-stream workaround. NO new BC created.

**Architecture:**
- Phase A: cherry-pick existing Nanolith demo (`useTranslateParagraph` + `TranslateParagraphDemo` + `/library/games/[gameId]/translate` page) from worktree commit `aa72b1b70`. 1089 LoC, 43 tests, ready to land.
- Phase B: single-shot Tier M dispatch for `/gamebook` index — Hero + QuotaWidget + GamebookCard grid + EmptyState (5 components). Reuse MeepleCard for cards. Existing `/gamebook/[gameId]/play` route already exists; Phase B adds the LIBRARY index that links to play + upload.
- Phase C: Phase 0.5 contract + sub-PR split (Foundation + Interactions) for `/gamebook/upload` 3-step wizard — StepIndicator + GameCard selection + CameraViewfinder + PageThumb + ConfidenceBadge + OfflineBanner + CancelModal. Replaces existing partial impl with v2 design.

**Tech Stack:** Next.js 16 App Router, React 19, TanStack Query (existing hooks), Tailwind 4, shadcn/ui, Vitest + Playwright, Zod schemas, next-intl. Backend: existing endpoints `POST /api/v1/gamebook/{gameId}/photos` + `GET /api/v1/gamebook/{gameId}/photos/{batchId}/status` + `GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}`.

**Bundle budget:**
- Phase A: ~+5 KB (demo, deferred from Wave D.2 already)
- Phase B: ~+45 KB (Tier M)
- Phase C Foundation: ~+50 KB
- Phase C Interactions: ~+70 KB
- **Total**: ~+170 KB ≤ contract `<+250 KB` for SP6 umbrella

**Pattern parents:**
- Wave 4 D1 (PR #717) Tier S blueprint
- Wave 3 PR #724 Tier M `/players/[id]` blueprint
- Wave D.2 PR #749 + #753 Tier L+ Foundation+Interactions blueprint
- Wave D.3 PR #762 Tier M-L 5-task TDD pattern

---

## Scope

**IN SCOPE**
- `/library/games/[gameId]/translate` (Phase A — Nanolith demo cherry-pick)
- `/gamebook` index (Phase B)
- `/gamebook/upload` 3-step wizard (Phase C)
- v2-migration-matrix.md updates

**OUT OF SCOPE (deferred)**
- `/gamebook/[gameId]/play` paragraph navigation v2 redesign (existing v1 impl works; visual inconsistency between v1 play + v2 upload accepted for SP6 — separate plan post-SP6)
- Backend `POST /api/v1/translation/translate-narrative` exposure (TranslateParagraphDemo uses chat stream workaround)
- Premium tier purchase flow (quota soft/hard limits show CTA only, no payment integration)
- Vision API photo analysis OCR confidence per-page (only batch-level avg available)

## Top-level Acceptance Criteria (Wiegers SMART)

These hoisted ACs apply across all 3 phases and are validated end-to-end before final umbrella close:

- **AC-1**: 3-step wizard `/gamebook/upload` navigable via URL alone with `?step=1|2|3` SSOT (deep-link to step 2 with `?gameId=` selects game and skips step 1; step 3 requires `?batchId=`)
- **AC-2**: Offline retry budget exhausts to `failed` state after 31s total ([1s+2s+4s+8s+16s] = 31s sum) with cancel button visible during all retry attempts
- **AC-3**: Camera permission denied → fallback to file picker (gallery upload) within ≤500ms, NO modal interruption — inline UI swap
- **AC-4**: All photo upload requests carry idempotency key `${batchId}:${pageNumber}` to enable safe retry without server-side duplicate entries
- **AC-5**: Bundle delta ≤+170 KB across all 3 phases combined (Phase A ~5 + Phase B ~45 + Phase C ~120) — Wave D.2 rebaseline pattern reused if exceeded
- **AC-6**: All visual baselines bootstrapped via Gate D bootstrap-then-merge discipline (PR #762 pattern) — no `--admin` merge for missing baselines
- **AC-7**: ICU plural keys for `gamebook.index.quota.usedCount` (it: zero/one/other; en: one/other) + `gamebook.upload.indexing.pagesCount` symmetric across both locales

---

## Pre-flight

Before Phase A, ensure clean state:

- [ ] **Pre-1**: Sync main-dev

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout main-dev
git pull --ff-only
git remote prune origin
```

Expected: `main-dev` at `b4c44f58a` (Wave D.3 merge) or later.

- [ ] **Pre-2**: Verify worktree commit accessible

```bash
git log --all --oneline aa72b1b70 -1
git show aa72b1b70 --stat
```

Expected: commit message "feat(demo): Nanolith translate paragraph scaffolding (Path 5a workaround)" + 5 files: 1089 insertions.

- [ ] **Pre-3**: Audit `useGamebooks` hook + gamebook state ownership

```bash
# Verify useGamebooks hook existence
grep -rn "useGamebooks\|getMyGamebooks\|gamebooks:" apps/web/src/hooks/queries/ apps/web/src/lib/ 2>&1 | head -10
# Verify backend endpoint
grep -rn "MapGet.*gamebook\|MapGet.*GameBook" apps/api/src/Api/Routing/ 2>&1 | head -5
```

Decision matrix:
- If `useGamebooks` exists with `GET /api/v1/gamebooks` returning user's processed gamebooks → use as-is
- If `useGamebooks` doesn't exist BUT `getMyGames` exists with `hasGamebook` flag → wrap as `useGamebooks` adapter in Phase B Task B.0
- If neither exists → backend gap; document in v1 carryover (Gate B) and stub via fixture for Phase B; backend endpoint exposure deferred to follow-up issue

Document finding in plan execution log.

- [ ] **Pre-4**: Open SP6 umbrella issue + 3 child issues

```bash
gh issue create --title "[V2 Phase 2 · SP6] Libro Game (gamebook) migration umbrella" \
  --body "Migrate SP6 libro-game mockups (sp6-libro-game-index + sp6-libro-game-photo-upload) + ship Nanolith demo. 3 child PRs (Phase A/B/C). Refs admin-mockups/design_files/sp6-*.{html,jsx}." \
  --label "enhancement,area/frontend"
```

Save the umbrella issue number as `$SP6_UMBRELLA`. Then create 3 children:

```bash
gh issue create --title "[SP6 Phase A] Cherry-pick Nanolith translate demo (worktree aa72b1b70)" \
  --body "Cherry-pick worktree commit \`aa72b1b70\` (useTranslateParagraph hook + TranslateParagraphDemo component + /library/games/[gameId]/translate page + 43 tests). Sub-issue di #$SP6_UMBRELLA." \
  --label "enhancement,area/frontend"
# → save as $SP6_PHASE_A

gh issue create --title "[SP6 Phase B] /gamebook index Tier M v2 migration" \
  --body "Implement /gamebook index route: Hero + QuotaWidget + GamebookCard grid + EmptyState. Single-shot Tier M dispatch. Sub-issue di #$SP6_UMBRELLA." \
  --label "enhancement,area/frontend"
# → save as $SP6_PHASE_B

gh issue create --title "[SP6 Phase C] /gamebook/upload Tier L 3-step wizard v2 migration" \
  --body "3-step wizard: game selection → camera capture → indexing. 11-state FSM. Phase 0.5 contract + Foundation sub-PR + Interactions sub-PR. Sub-issue di #$SP6_UMBRELLA." \
  --label "enhancement,area/frontend"
# → save as $SP6_PHASE_C
```

---

## Phase A — Cherry-pick Nanolith demo (~30 min)

### Task A.1: Cherry-pick + push + open PR

**Files (from commit `aa72b1b70`):**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/translate/page.tsx`
- Create: `apps/web/src/components/v2/gamebook/TranslateParagraphDemo.tsx`
- Create: `apps/web/src/components/v2/gamebook/__tests__/TranslateParagraphDemo.test.tsx`
- Create: `apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts`
- Create: `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts`

- [ ] **Step A.1.1: Create branch from main-dev**

```bash
git checkout main-dev
git checkout -b feature/issue-$SP6_PHASE_A-nanolith-demo
git config branch.feature/issue-$SP6_PHASE_A-nanolith-demo.parent main-dev
```

- [ ] **Step A.1.2: Cherry-pick demo commit**

```bash
git cherry-pick aa72b1b70
```

Expected: cherry-pick clean (no conflicts since main-dev has not modified these files; new files only).

If conflict: investigate. The commit only adds NEW files, so conflicts mean someone else added these paths. Resolve by accepting `aa72b1b70` version.

- [ ] **Step A.1.3: Run tests + typecheck**

```bash
cd apps/web
pnpm test src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts --run
pnpm test src/components/v2/gamebook/__tests__/TranslateParagraphDemo.test.tsx --run
pnpm typecheck
```

Expected: 43/43 tests pass (22 hook + 21 component), typecheck clean.

- [ ] **Step A.1.4: Push branch**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git push -u origin feature/issue-$SP6_PHASE_A-nanolith-demo
```

Expected: pre-push hook builds frontend + backend, push succeeds.

- [ ] **Step A.1.5: Open PR**

```bash
gh pr create --base main-dev --title "feat(gamebook): Nanolith translate paragraph demo (SP6 Phase A, #$SP6_PHASE_A)" \
  --body "$(cat <<'EOF'
## Summary

Cherry-picks worktree commit \`aa72b1b70\` (Nanolith demo deferred from Wave D.2 — see memory file \`session_2026-05-06_wave-d2-end-to-end.md\`). Ships /library/games/[gameId]/translate route with EN→IT paragraph translation via existing \`useAgentChatStream\` workaround.

## Resolves

Closes #$SP6_PHASE_A
Refs #$SP6_UMBRELLA (SP6 umbrella)

## Files

- \`apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts\` (223 LoC)
- \`apps/web/src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts\` (294 LoC, 22 tests)
- \`apps/web/src/components/v2/gamebook/TranslateParagraphDemo.tsx\` (237 LoC)
- \`apps/web/src/components/v2/gamebook/__tests__/TranslateParagraphDemo.test.tsx\` (230 LoC, 21 tests)
- \`apps/web/src/app/(authenticated)/library/games/[gameId]/translate/page.tsx\` (105 LoC)

## Constraints honored

- DEMO-ONLY (production-deferred): backend translation endpoint not yet exposed
- Storybook + encounterbook ENG content only — no PDF commits (copyright)
- One-shot self-demo target: user has Nanolith physical books

## Test plan

- [ ] 43/43 tests pass (22 hook + 21 component)
- [ ] pnpm typecheck clean
- [ ] Frontend build succeeds (route in production output)
- [ ] G4 paragraph-number typed lookup backend deferred — Refs #747

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step A.1.6: Wait CI + merge**

```bash
gh pr checks <PR_NUM> --watch --required
gh pr merge <PR_NUM> --squash --delete-branch
git checkout main-dev && git pull --ff-only && git remote prune origin
```

If only A11y E2E fails on #752 known dark contrast: merge with `--admin --squash --delete-branch`.

---

## Phase B — `/gamebook` index Tier M (~3-4h)

### Task B.0: Backend audit + i18n key inventory

**Files (audit only):**
- Read: `apps/api/src/Api/BoundedContexts/{DocumentProcessing,KnowledgeBase,GameManagement}/Application/Queries/`
- Read: `apps/web/src/locales/{it,en}.json`

- [ ] **Step B.0.1: Audit backend gamebook endpoints**

```bash
# In Bash
grep -rn "gamebook\|GameBook\|game-book" apps/api/src/Api/Routing/ apps/api/src/Api/BoundedContexts/ | head -30
```

Expected output:
- `POST /api/v1/gamebook/{gameId}/photos` (batch upload)
- `GET /api/v1/gamebook/{gameId}/photos/{batchId}/status` (polling)
- `GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}` (paragraph fetch)
- Quota endpoint? (TBD audit) — likely embedded in user response

If quota endpoint missing → orchestrator stubs quota data via fixture (Gate B documented).

- [ ] **Step B.0.2: Audit existing /gamebook/[gameId]/play route**

```bash
ls apps/web/src/app/\(authenticated\)/gamebook/
```

Expected: `[gameId]/play/` exists. Verify NO existing index `page.tsx` at `/gamebook` root.

If index exists with legacy v1 design → Big-Bang replacement via Phase B (Wave B.3 single-tree responsive uniformity pattern).

- [ ] **Step B.0.3: Inventory i18n keys needed**

```bash
grep -E "^\s+\"gamebook\." apps/web/src/locales/it.json apps/web/src/locales/en.json | wc -l
```

Expected: 0 or low — most SP6 keys NEW.

Plan ~30 keys per locale under `gamebook.index.*` namespace (hero, quota, status pills, empty state, error state).

### Task B.1: Foundation — FSM + visual fixture + i18n + helpers

**Files:**
- Create: `apps/web/src/lib/gamebook-index/fsm.ts`
- Create: `apps/web/src/lib/gamebook-index/visual-test-fixture.ts`
- Create: `apps/web/src/lib/gamebook-index/index.ts`
- Create: `apps/web/src/lib/gamebook-index/__tests__/fsm.test.ts`
- Create: `apps/web/src/lib/gamebook-index/__tests__/visual-test-fixture.test.ts`
- Modify: `apps/web/src/locales/it.json` (add `gamebook.index.*`)
- Modify: `apps/web/src/locales/en.json` (add `gamebook.index.*` symmetric)

- [ ] **Step B.1.1: Branch from main-dev**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-$SP6_PHASE_B-gamebook-index
git config branch.feature/issue-$SP6_PHASE_B-gamebook-index.parent main-dev
```

- [ ] **Step B.1.2: Dispatch foundation subagent (single-shot, mirror Wave 4 D1 Task 1)**

Use `subagent-driven-development` skill. Brief subagent with:
- Working dir + branch
- Goal: create lib + fixture + helpers + i18n keys for /gamebook index
- FSM cells: 6 (`loading | error | empty | default | quota-soft | quota-hard`)
- Schema: GamebookCardData + QuotaInfo (use real backend response shape; stub quota if endpoint missing)
- Fixture: 6 fixture kinds matching FSM cells
- i18n keys: ~30 under `gamebook.index.*` namespace
- ICU plural for `gamebook.index.quota.usedCount` (`{used} di {total}`)
- Tests: ~25-30 unit
- Commit message: `feat(gamebook): SP6 Phase B Task 1 Foundation — FSM + fixture + i18n (#$SP6_PHASE_B)`

Expected output: ~25-30 unit tests passing, typecheck clean, commit landed.

### Task B.2: 5 v2 components

**Files:**
- Create: `apps/web/src/components/v2/gamebook/GamebookHero.tsx`
- Create: `apps/web/src/components/v2/gamebook/QuotaWidget.tsx`
- Create: `apps/web/src/components/v2/gamebook/GamebookCard.tsx` (or reuse MeepleCard with entity="game")
- Create: `apps/web/src/components/v2/gamebook/EmptyGamebooks.tsx`
- Create: `apps/web/src/components/v2/gamebook/GamebookCardSkeleton.tsx`
- Create: `apps/web/src/components/v2/gamebook/__tests__/{Hero,QuotaWidget,GamebookCard,Empty,Skeleton}.test.tsx`
- Create: `apps/web/src/components/v2/gamebook/index.ts` (barrel)

- [ ] **Step B.2.1: Dispatch components subagent**

Use `subagent-driven-development` skill. Brief subagent with:
- Working dir + branch
- Goal: Implement 5 v2 components matching `admin-mockups/design_files/sp6-libro-game-index.{html,jsx}`
- Mockup reference: read both .html and .jsx for visual parity
- A11y: `data-slot` per component, ARIA roles, semantic HTML, keyboard nav
- Theme: light default (no dark variant in this PR)
- Tier M dispatch — single-shot subagent dispatches WITHOUT Phase 0.5 contract
- Test target: ~60-70 unit tests across 5 components
- Commit message: `feat(gamebook): SP6 Phase B Task 2 — 5 v2 components (#$SP6_PHASE_B)`

Critical: GamebookCard MUST evaluate `MeepleCard fit` (Gate C). If MeepleCard with `entity="game"` + `variant="grid"` covers cover-gradient + emoji + status-pill display → reuse. If diverges (status pill needs error state, page count specific) → bespoke component with documented divergence.

Expected: ~60-70 unit tests, typecheck clean.

### Task B.3: Orchestrator + page wiring

**Files:**
- Create: `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/_components/__tests__/GamebookIndexView.test.tsx`
- Create: `apps/web/src/app/(authenticated)/gamebook/page.tsx` (thin Suspense shell)
- Create: `apps/web/src/hooks/queries/useGamebooks.ts` (if not exists, fetches user's gamebooks list)

- [ ] **Step B.3.1: Dispatch orchestrator subagent**

Use `subagent-driven-development` skill. Brief subagent with:
- Working dir + branch
- Goal: Orchestrator composes useGamebooks (single hook) + visual fixture override (`?fixture=` query gated by STATE_OVERRIDE_ENABLED) + 6-cell FSM rendering
- URL state SSOT: `?fixture=default|empty|loading|error|quota-soft|quota-hard`
- i18n via `useTranslation('gamebook.index.*')` (orchestrator resolves all labels, components accept pre-resolved strings — Wave D.3 pattern)
- Page.tsx: thin `Suspense` shell mirroring Wave D.3 pattern at `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx`
- Test target: ~15-20 tests covering FSM cell rendering + URL override
- Commit message: `feat(gamebook): SP6 Phase B Task 3 — orchestrator + page (#$SP6_PHASE_B)`

If `useGamebooks` hook doesn't exist → audit first. Likely needs new hook calling `GET /api/v1/gamebooks` (verify endpoint exists; if not, use existing `getMyGames` + filter by hasGamebook flag).

Expected: ~15-20 tests, typecheck clean.

### Task B.4: E2E specs

**Files:**
- Create: `apps/web/e2e/visual-migrated/sp6-gamebook-index.spec.ts`
- Create: `apps/web/e2e/v2-states/gamebook-index.spec.ts`
- Create: `apps/web/e2e/a11y/gamebook-index.spec.ts`

- [ ] **Step B.4.1: Dispatch E2E subagent**

Use `subagent-driven-development` skill. Brief subagent with:
- Working dir + branch
- Goal: 3 spec files mirroring Wave D.3 pattern
- visual-migrated: 2 specs (desktop+mobile default, `?fixture=default` URL explicit per Pattern P14)
- v2-states: 5 specs (empty / quota-soft / quota-hard / loading / error desktop)
- a11y: 6 axe-core specs (default/empty/loading/error/quota-soft/quota-hard) + reduced-motion + keyboard nav
- Auth bypass triple-helper (seedAuthSession + seedCookieConsent + mockAuthEndpoints)
- ALL specs use explicit `?fixture=` (Wave D.3 Pattern P14 lesson)
- Commit message: `test(gamebook): SP6 Phase B Task 4 — E2E specs (#$SP6_PHASE_B)`

Expected: 13 specs, typecheck clean (specs don't run locally yet — bootstrap in B.5).

### Task B.5: Bootstrap baselines + matrix update + PR

- [ ] **Step B.5.1: Push branch**

```bash
git push -u origin feature/issue-$SP6_PHASE_B-gamebook-index
```

- [ ] **Step B.5.2: Trigger bootstrap workflow**

```bash
gh workflow run visual-regression-migrated.yml --ref feature/issue-$SP6_PHASE_B-gamebook-index -f mode=bootstrap
sleep 5
BOOTSTRAP_RUN=$(gh run list --workflow=visual-regression-migrated.yml --branch=feature/issue-$SP6_PHASE_B-gamebook-index --limit 1 --json databaseId --jq '.[0].databaseId')
echo "Bootstrap run: $BOOTSTRAP_RUN"
gh run watch $BOOTSTRAP_RUN --exit-status
```

- [ ] **Step B.5.3: Download artifact + commit baselines**

```bash
rm -rf /tmp/sp6-b-baselines && mkdir -p /tmp/sp6-b-baselines
gh run download $BOOTSTRAP_RUN -D /tmp/sp6-b-baselines
mkdir -p apps/web/e2e/visual-migrated/sp6-gamebook-index.spec.ts-snapshots
mkdir -p apps/web/e2e/v2-states/gamebook-index.spec.ts-snapshots
cp /tmp/sp6-b-baselines/visual-migrated-baselines-*/sp6-gamebook-index.spec.ts-snapshots/*.png apps/web/e2e/visual-migrated/sp6-gamebook-index.spec.ts-snapshots/
cp /tmp/sp6-b-baselines/v2-states-baselines-*/gamebook-index.spec.ts-snapshots/*.png apps/web/e2e/v2-states/gamebook-index.spec.ts-snapshots/
git add apps/web/e2e/visual-migrated/sp6-gamebook-index.spec.ts-snapshots/ apps/web/e2e/v2-states/gamebook-index.spec.ts-snapshots/
git commit -m "test(gamebook): SP6 Phase B baselines bootstrap (#$SP6_PHASE_B)"
```

Expected: ~7 PNG (2 visual-migrated + 5 v2-states).

- [ ] **Step B.5.4: Update v2-migration-matrix.md**

Add SP6 sections to matrix. Edit `docs/for-developers/frontend/v2-migration-matrix.md`:
- Add row to Tier mapping table: `/gamebook` Tier M, status `done`, PR `#<B_PR>`
- Add new section "## SP6 — Libro Game" with subsection "### Gamebook index — `/gamebook` — 5 components — Tier M" listing all 5 components with status `done`

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(v2-matrix): mark SP6 Phase B gamebook-index done (#$SP6_PHASE_B)"
git push origin feature/issue-$SP6_PHASE_B-gamebook-index
```

- [ ] **Step B.5.5: Open + merge PR**

```bash
gh pr create --base main-dev --title "feat(gamebook): /gamebook index Tier M v2 migration (SP6 Phase B, #$SP6_PHASE_B)" \
  --body "..." # Phase B body (mirror Wave D.3 PR body structure)
```

Wait CI. If only A11y #752 dark fails → merge `--admin`. Otherwise merge clean.

```bash
gh pr merge <PR_NUM> --squash --delete-branch
git checkout main-dev && git pull --ff-only && git remote prune origin
```

---

## Phase C — `/gamebook/upload` Tier L (~8-12h)

### Task C.0: Phase 0.5 contract draft + PR

**Files:**
- Create: `docs/for-developers/frontend/contracts/gamebook-upload-hooks.md`

- [ ] **Step C.0.1: Branch + draft contract**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-$SP6_PHASE_C-upload-contract
git config branch.feature/issue-$SP6_PHASE_C-upload-contract.parent main-dev
```

- [ ] **Step C.0.2: Dispatch contract drafting subagent**

Use `Agent` tool with `subagent_type: specification`. Brief:
- Working dir + branch
- Goal: ~800-1000 line Phase 0.5 contract for `/gamebook/upload` 3-step wizard
- Mirror structure of `docs/for-developers/frontend/contracts/sessions-id-summary-hooks.md` (Wave D.3 contract, 1199 lines)
- Required sections:
  - §1 Overview + Tier classification
  - §2 Route + 3-step FSM (11 states distributed across 3 steps)
  - §3 Hook composition (usePhotoBatchUpload + usePhotoBatchStatus + camera capabilities)
  - §4 Schema contract (Zod schemas for batch upload request, batch status response, paragraph response)
  - §5 11 component specs (StepIndicator, GameSearchBar, GameCard, NoResultsPanel, ActionCard, CameraViewfinder, PageThumb, ConfidenceBadge, OfflineBanner, CancelModal, DesktopDropFallback)
  - §6 URL state SSOT (`?step=1|2|3` + `?gameId=` + `?fixture=` for visual override)
  - §7 Bundle budget (~120 KB target across Foundation+Interactions)
  - §8 i18n keys (~50 keys per locale for wizard)
  - §9 Camera contract (getUserMedia + permission states + light-meter + page detection + reduced-motion)

    **MANDATORY content per spec-panel critique (Nygard + Crispin)**:
    - 4-permission-state matrix: `granted` (full UX) / `denied` (fallback to file picker `<input type="file" capture>` within ≤500ms inline UI swap) / `prompt` (initial state, show "Allow camera access" CTA) / `unsupported` (no `mediaDevices.getUserMedia` — fallback to file picker, hide camera CTA)
    - Playwright E2E mock strategy: `--use-fake-ui-for-media-stream` + `--use-fake-device-for-media-stream` browser flags configured in `playwright.config.ts` for Chromium-only project; alternative `MediaStream` global mock pattern for Webkit/Firefox parity
    - Light-meter deterministic input: fixture mode bypasses real video stream, renders fake light-meter at 75% (well-lit) / 30% (low-light) / 5% (too-dark) per `?fixture=lightmeter-{kind}` URL override
    - `prefers-reduced-motion`: light-meter pulse animation gated, fallback to static gradient bar
  - §10 Offline resilience contract (retry budget [1s, 2s, 4s, 8s, 16s] → degraded-polling → failed)

    **MANDATORY content per spec-panel critique (Nygard)**:
    - Idempotency-key strategy: every photo upload request carries `Idempotency-Key: ${batchId}:${pageNumber}:${attemptCount}` header; server-side dedup based on key (existing pattern Wave 3 spec-panel #732)
    - Cancel-during-retry: orchestrator exposes `cancel()` callback that aborts in-flight `AbortController` AND clears retry timer; UI shows cancel button visible during all 5 retry attempts; cancel transitions FSM to `cancelled` state
    - In-flight HTTP on offline: pending request not aborted (browser handles network failure); retry timer kicks in after `error` response; user sees "Tentativo {n}/5..." countdown
    - 31s total budget visible in UI: retry-timer-overlay shows total elapsed (1s+2s+4s+8s+16s = 31s sum) with linear progress bar
    - Failed terminal state: shows manual "Riprova" button + "Cambia foto" alternative; preserves uploaded thumbnails from earlier successful pages
  - §11 Theme support (light default, dark camera bg)
  - §12 Confidence-based retake logic (high/medium/low thresholds)
  - §13 4 audit gates (ICU plural · schema reality · MeepleCard fit · bootstrap-then-merge)
  - §14 Visual baselines matrix (~10 PNG: 3 step × 3-4 viewport variants)
  - §15 Test matrix (Tier L 50/35/15 unit/integration/E2E)
  - §16 Foundation+Interactions sub-PR breakdown (5 tasks each)
  - §17 12-15 acceptance criteria
- Backend audit (read existing impl): `apps/web/src/app/(authenticated)/gamebook/upload/page.tsx` + `apps/web/src/lib/gamebook/api.ts` + `apps/web/src/lib/gamebook/hooks/{usePhotoBatchUpload,usePhotoBatchStatus,useGetParagraph}.ts`
- Out of scope: real translation endpoint wiring (defer Phase 3 Task 3.5e), per-page confidence tracking (only avg available)

Expected: 800-1000 line contract committed, typecheck clean (no code changes).

- [ ] **Step C.0.3: Push + open + merge contract PR**

```bash
git push -u origin feature/issue-$SP6_PHASE_C-upload-contract
gh pr create --base main-dev --title "docs(gamebook): SP6 Phase C Phase 0.5 contract (#$SP6_PHASE_C)" \
  --body "Phase 0.5 contract for /gamebook/upload Tier L 3-step wizard. 11-state FSM + camera contract + offline resilience + Foundation/Interactions sub-PR split."
```

Wait CI (docs-only, fast). Merge `--squash --delete-branch`.

```bash
gh pr merge <CONTRACT_PR_NUM> --squash --delete-branch
git checkout main-dev && git pull --ff-only
```

### Foundation/Interactions ownership boundary (Fowler decision)

**State machine ownership**: ALL 11-state FSM lives in Foundation `apps/web/src/lib/gamebook-upload/fsm.ts`. Foundation defines complete state transitions + cell shape. Interactions sub-PR ONLY adds side-effect handlers (camera invocation, mutation calls, polling) — NEVER moves FSM logic.

**Rationale** (per Wave D.2 lesson PR #749 cherry-pick chain anti-pattern AP10): moving FSM during Interactions costs cherry-pick rebuilds. Foundation captures full schema; Interactions adds wiring. This makes Foundation reviewable in isolation (FSM correctness) and Interactions reviewable in isolation (side-effect correctness).

### Task C.1: Foundation sub-PR — Tasks 1-5

**Files (Foundation sub-PR):**
- Create: `apps/web/src/lib/gamebook-upload/{fsm,schemas,visual-test-fixture,index}.ts` + tests
- Create: `apps/web/src/components/v2/gamebook-upload/{StepIndicator,GameSearchBar,GameCard,NoResultsPanel,ActionCard}.tsx` + tests
- Modify: `apps/web/src/locales/{it,en}.json` (add ~50 keys under `gamebook.upload.*`)
- Create: `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` (orchestrator skeleton — static fixture mode, no real camera/upload yet)
- Modify: `apps/web/src/app/(authenticated)/gamebook/upload/page.tsx` (thin Suspense shell — REPLACES existing partial impl)
- Create: 3 E2E spec files (visual-migrated/v2-states/a11y) for steps 1-3 static fixtures

- [ ] **Step C.1.1: Branch from main-dev**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-$SP6_PHASE_C-upload-foundation
git config branch.feature/issue-$SP6_PHASE_C-upload-foundation.parent main-dev
```

- [ ] **Step C.1.2: Dispatch 5 task subagents serial (Wave D.2 Foundation pattern)**

Per Wave D.2 Foundation sub-PR pattern (PR #749), dispatch 5 task subagents SERIAL (never parallel — see `feedback_subagent-serial-only.md`):

1. **Task C.1.A — Foundation lib + i18n**: ~30 unit tests covering FSM (11 states across 3 steps), schemas (Zod), visual fixture (11 fixture kinds), tie-step computation
2. **Task C.1.B — 5 read-only components**: StepIndicator + GameSearchBar + GameCard + NoResultsPanel + ActionCard (~80 unit tests)
3. **Task C.1.C — Orchestrator skeleton + page**: 3-step orchestrator with static fixture (no real camera/upload), URL state SSOT, page thin shell. ~25 tests.
4. **Task C.1.D — E2E specs**: 3 spec files (visual + v2-states + a11y) covering steps 1-3 static fixture rendering, ~12 specs total
5. **Task C.1.E — Bootstrap baselines + matrix update + PR**: trigger workflow, download PNG, commit, update matrix, open Foundation PR

Each subagent commits sequentially. After 5 commits, push + open Foundation PR + merge.

Expected after C.1: Foundation PR merged, ~135-145 tests, ~10 PNG baselines.

### Task C.2: Interactions sub-PR — Tasks 1-5

**Pre-condition**: Phase C.1 Foundation PR merged. New branch from updated main-dev.

**Files (Interactions sub-PR):**
- Create: `apps/web/src/lib/gamebook-upload/{camera-capabilities,offline-budget,confidence-classifier}.ts` + tests
- Create: `apps/web/src/components/v2/gamebook-upload/{CameraViewfinder,PageThumb,ConfidenceBadge,OfflineBanner,CancelModal,DesktopDropFallback}.tsx` + tests
- Modify: `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` (extend with camera/upload integration, real hooks wiring)
- Add new E2E specs: smoke + camera permission + offline retry + cancel modal

- [ ] **Step C.2.1: Branch + dispatch 5 task subagents serial**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-$SP6_PHASE_C-upload-interactions
git config branch.feature/issue-$SP6_PHASE_C-upload-interactions.parent main-dev
```

Dispatch 5 task subagents SERIAL:

1. **Task C.2.A — Camera + offline + confidence libs**: getUserMedia capability detection wrapper + retry budget [1s,2s,4s,8s,16s] reducer + confidence classifier (high>0.8, medium 0.5-0.8, low<0.5). ~40 tests.
2. **Task C.2.B — 6 interactive components**: CameraViewfinder + PageThumb + ConfidenceBadge + OfflineBanner + CancelModal + DesktopDropFallback. ~80 tests.
3. **Task C.2.C — Orchestrator extension**: extend existing skeleton with usePhotoBatchUpload mutation + usePhotoBatchStatus polling + camera permission flow + URL state SSOT integration with EXISTING Foundation FSM (`fsm.ts` not modified — Interactions wires side-effects to FSM-defined transitions). ~35 tests.

5b. **Task C.2.F — Integration tests (Crispin missing tier)**: ~25 integration tests using MSW + TanStack Query covering: usePhotoBatchUpload mutation lifecycle (success / 4xx / 5xx / network error / abort), usePhotoBatchStatus polling cadence + degraded-polling transition, idempotency-key header presence, cancel-during-retry abort propagation. Lives in `apps/web/src/lib/gamebook-upload/__integration__/`.
4. **Task C.2.D — Integration E2E specs**: smoke spec (real backend mocked at API level, full 3-step happy path) + camera permission denied + offline retry + cancel-modal focus trap. ~10 specs.
5. **Task C.2.E — Bootstrap baselines + matrix update + bundle rebaseline + PR**: trigger workflow, download PNG, commit, update matrix, open Interactions PR + bundle rebaseline if needed.

Expected after C.2: Interactions PR merged, ~165 unit + 22 E2E = ~187 tests Interactions-scoped, ~6 PNG additional.

### Task C.3: Bundle rebaseline (if needed)

- [ ] **Step C.3.1: Check bundle delta**

After C.2 merge, check if `apps/web/bundle-size-baseline.json` needs rebump:

```bash
cd apps/web && pnpm test __tests__/bundle-size.test.ts --run
```

If FAIL: rebaseline per Wave D.2 PR #754 pattern (separate chore PR).

---

## Final tasks

- [ ] **Step Final.1: Update memory**

After all 3 phases merged:

1. Create `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-frontend\memory\session_2026-05-06_sp6-libro-game-shipped.md`
2. Update `MEMORY.md` index with topic file pointer

- [ ] **Step Final.2: Close umbrella issue**

```bash
gh issue close $SP6_UMBRELLA --reason completed --comment "All 3 phases shipped: A (Nanolith demo) + B (gamebook index Tier M) + C (gamebook upload Tier L). v2-migration-matrix.md updated."
```

- [ ] **Step Final.3: Verify final state**

```bash
git checkout main-dev && git pull --ff-only
cd apps/web
pnpm typecheck
pnpm test --run  # all tests pass
pnpm build  # routes /gamebook + /gamebook/upload + /library/games/[gameId]/translate in production output
```

Expected: green across the board.

---

## Self-review checklist

### Spec coverage

| Mockup requirement | Task |
|--------------------|------|
| sp6-libro-game-index Hero + KPI | B.2 GamebookHero |
| sp6-libro-game-index QuotaWidget | B.2 QuotaWidget |
| sp6-libro-game-index GamebookCard grid | B.2 GamebookCard |
| sp6-libro-game-index Empty state | B.2 EmptyGamebooks |
| sp6-libro-game-index loading skeleton | B.2 GamebookCardSkeleton |
| sp6-libro-game-index quota-soft/quota-hard variants | B.1 FSM cells + B.3 orchestrator |
| sp6-libro-game-photo-upload step indicator | C.1.B StepIndicator |
| sp6-libro-game-photo-upload step 1 game search | C.1.B GameSearchBar + GameCard + NoResultsPanel + ActionCard |
| sp6-libro-game-photo-upload step 2 camera | C.2.B CameraViewfinder |
| sp6-libro-game-photo-upload step 2 desktop fallback | C.2.B DesktopDropFallback |
| sp6-libro-game-photo-upload step 3 thumb grid | C.2.B PageThumb |
| sp6-libro-game-photo-upload step 3 confidence badges | C.2.B ConfidenceBadge |
| sp6-libro-game-photo-upload offline banner | C.2.B OfflineBanner |
| sp6-libro-game-photo-upload cancel modal | C.2.B CancelModal |
| Nanolith demo route | A.1 cherry-pick |

✅ All mockup requirements covered.

### Type consistency

- `GamebookCardData` (B.1 schema) → used in B.2 components + B.3 orchestrator
- `QuotaInfo` (B.1) → used in B.2 QuotaWidget
- `WizardStep` (C.1.A FSM) → used in C.1.B StepIndicator + C.1.C orchestrator + C.2.C extension
- `BatchStatus` (C.1.A schema) → used in C.2.B PageThumb + C.2.C polling
- `ConfidenceLevel` (C.2.A classifier) → used in C.2.B ConfidenceBadge

✅ Types consistent.

### Placeholder scan

- No "TODO" / "TBD" left in tasks (umbrella + child issue numbers placeholdered with `$SP6_*` env vars to be set in Pre-3)
- No "implement later" — all steps actionable
- No "similar to Task N" — each task brief is self-contained

✅ No placeholders.

### Risk areas

| Risk | Mitigation in plan |
|------|--------------------|
| Backend translation endpoint missing | Phase A uses workaround (chat stream); deferred for proper endpoint |
| Photo storage scope unclear | Phase C audits in C.0 + documents in §11 of contract |
| Camera permission UX | Phase C.2.B handles permission-denied fallback explicitly |
| Mobile/desktop parity | Phase C.2.B DesktopDropFallback + camera mock for E2E |
| Bundle budget tight | Per-phase target tracked, rebaseline PR planned in C.3 |
| Cross-context BC | No new BC created — extend GameManagement/DocumentProcessing/KnowledgeBase via cross-context queries |

✅ Risks addressed.

---

## Estimated total effort

| Phase | Effort (autonomous) |
|-------|---------------------|
| Pre-flight | ~10 min |
| Phase A | ~30 min |
| Phase B (5 tasks single-shot) | ~3-4 hours |
| Phase C.0 contract | ~1 hour |
| Phase C.1 Foundation (5 tasks) | ~3-4 hours |
| Phase C.2 Interactions (5 tasks) | ~3-4 hours |
| Phase C.3 bundle rebaseline | ~30 min |
| Final | ~30 min |
| **Total** | **~12-14 hours** autonomous |

---

## Spec-panel review amendments applied (2026-05-06)

Per panel critique (Wiegers + Adzic + Cockburn + Fowler + Nygard + Crispin), 9 amendments landed inline:

1. ✅ **Primary actor + Business goal** declared in plan header (Cockburn)
2. ✅ **BC ownership** declared: DocumentProcessing primary (Fowler)
3. ✅ **Pre-3 audit** added for `useGamebooks` hook existence (Wiegers)
4. ✅ **§Top-level Acceptance Criteria** hoisted (7 SMART AC) (Wiegers)
5. ✅ **Camera contract** §9 mandatory content specified — 4-permission-state matrix + Playwright `--use-fake-device-for-media-stream` + light-meter deterministic input + reduced-motion (Nygard + Crispin)
6. ✅ **Cancel-during-retry + idempotency-key** §10 mandatory content specified (Nygard)
7. ✅ **FSM lives entirely in Foundation `fsm.ts`** — declaration added pre-C.1 (Fowler)
8. ✅ **Integration tier tests** added as Task C.2.F (~25 MSW + TanStack tests) (Crispin)
9. ⏳ **Phase D OUT OF SCOPE rationale** — added to §Scope (Cockburn)

Verdict transitions: ⚠️ APPROVE WITH AMENDMENTS → ✅ APPROVED FOR EXECUTION.

## Refs

- SP6 mockups: [`sp6-libro-game-index.html`](../../../admin-mockups/design_files/sp6-libro-game-index.html) + [`sp6-libro-game-photo-upload.html`](../../../admin-mockups/design_files/sp6-libro-game-photo-upload.html)
- [v2-migration-matrix.md](../frontend/v2-migration-matrix.md)
- Pattern parents:
  - PR #717 Wave 4 D1 (Tier S blueprint, single-shot)
  - PR #724 Wave 3 (Tier M `/players/[id]` blueprint)
  - PR #749 + #753 Wave D.2 (Tier L+ Foundation+Interactions blueprint)
  - PR #762 Wave D.3 (Tier M-L 5-task TDD pattern, brownfield FORK)
- Skills:
  - `superpowers:writing-plans` — this plan
  - `superpowers:subagent-driven-development` — execution skill
  - `feedback_subagent-serial-only.md` — never parallel dispatch
  - `feedback_brownfield-route-redirect-audit.md` — pre-impl redirect cleanup
- Memory:
  - `session_2026-05-06_wave-d3-shipped.md` (most recent pattern reference)
  - `session_2026-05-06_wave-d2-end-to-end.md` (Tier L+ pattern + Nanolith demo deferred)
- Worktree commit: `aa72b1b70` (Nanolith demo, 1089 LoC, 43 tests)
- Backend deferred: `/api/v1/translation/translate-narrative` exposure (Phase 3 Task 3.5e)
- Followup issues:
  - #747 G4 paragraph-number typed lookup (low priority)
  - #752 a11y dark mode contrast (sustained gate `--admin` per Wave D pattern)
