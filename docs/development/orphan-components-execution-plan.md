# Orphan Components Execution Plan

> **Meta-plan** consolidating the 6 GitHub issues (meepleAi-app/meepleai-monorepo#290-#295) from `orphan-components-integration-plan.md` into an ordered, gated, and reviewed execution sequence.

**Created:** 2026-04-08
**Review method:** `/sc:spec-panel` critique mode with Wiegers, Fowler, Newman, Nygard, Crispin, Cockburn
**Related plan:** `docs/development/orphan-components-integration-plan.md`
**Related issues:** meepleAi-app/meepleai-monorepo#290-#295

## TL;DR

The original plan suggested order: #290 → #294 → #291 → #292 → #293 → #295.

**Spec panel review revised the plan with 3 changes:**

1. **Add Phase 0 gates** (tests, audit, decisions) before any ticket execution
2. **Reframe #291** from "integrate orphans" to "consolidate tier ecosystem" — 2 orphans are duplicates of already-shipped components
3. **Parallelize #293 + #294** — they touch disjoint code areas

**Revised order:** G0.1+G0.2+G0.3 → #290 → (#294 ∥ #293) → #291 (reframed) → #292 → #295 (optional)

## Pre-flight Code Reality Findings

### 🔴 Tier ecosystem duplication

Three implementations of progress bars, two of upgrade CTAs:

| Orphan (showcase) | Production equivalent | File |
|---|---|---|
| `CollectionProgressBar` | `UsageMeter` | `components/tier/UsageMeter.tsx` |
| `CollectionProgressBar` | `QuotaRow` (inline) | `components/library/UsageWidget.tsx` |
| `UpgradePrompt` | `UpgradeCta` | `components/tier/UpgradeCta.tsx` |

The `components/tier/` ecosystem (UsageMeter, UpgradeCta, UsageSummary, PricingCard) is mature, wired to `useUsage` hook, and in production. The orphans in `ui/feedback/` are **less-developed parallel implementations**.

**Implication:** Ticket #291 cannot be executed as written. It must be **reframed** as a consolidation ticket.

### 🟢 `useUsage` hook already exists

`apps/web/src/hooks/useUsage.ts` — fetches `UsageSnapshot` via `api.tiers.getMyUsage()` with 60s polling. No new hook needed for #291.

### 🟢 Concrete target for RatingStars

`apps/web/src/components/library/GameHeader.tsx:69-70`:
```tsx
<Star className="w-3 h-3 fill-current" />
{rating.toFixed(1)}
```
This is inline `lucide-react Star` + text — the perfect drop-in replacement target for `<RatingStars />`.

## Phase 0 — Unblocking Gates

These gates MUST pass before any ticket execution. Each gate produces a concrete artifact.

### Gate G0.1 — Characterization tests for ChatMessageList (#292 prereq)

**Owner:** TBD (backend/chat engineer)
**Blocks:** #292
**Parallelizable with:** G0.2, G0.3

**Tasks:**
- [ ] Read `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx` and measure coverage
- [ ] If coverage < 80%, add characterization tests for:
  - [ ] Feedback submission: helpful/not-helpful round-trip with `api.knowledgeBase.submitKbFeedback`
  - [ ] Streaming token accumulation (SSE partial messages)
  - [ ] Citation rendering via `RuleSourceCard`
  - [ ] Typing indicator display during streaming
  - [ ] Message window sliding (`WINDOW_SIZE` boundary)
- [ ] E2E Playwright test: chat thread with feedback + citations
- [ ] Document current behavior snapshot in `docs/development/chat-message-list-behavior-baseline.md`

**Done when:** Running tests against the current `ChatMessageList.tsx` passes 100%. Any future refactor that breaks these tests fails loudly.

### Gate G0.2 — Tier ecosystem audit (#291 reframe prereq)

**Owner:** TBD (frontend architect)
**Blocks:** #291
**Parallelizable with:** G0.1, G0.3

**Tasks:**
- [ ] Read and compare side-by-side:
  - `components/tier/UsageMeter.tsx`
  - `components/tier/UpgradeCta.tsx`
  - `components/tier/UsageSummary.tsx`
  - `components/library/UsageWidget.tsx` (including inline `QuotaRow`)
  - `components/ui/feedback/collection-progress-bar.tsx` (orphan)
  - `components/ui/feedback/upgrade-prompt.tsx` (orphan)
- [ ] Document API delta in `docs/development/tier-components-audit.md`:
  - Which features are unique to each implementation
  - Migration path for each current consumer
  - Naming decision: keep `tier/*` names or adopt `ui/feedback/*` names
- [ ] **Decision artifact:** explicit "KEEP / DEPRECATE / MERGE" table for each of the 6 components
- [ ] Update `orphan-components-integration-plan.md` Ticket #291 with new scope
- [ ] Update GitHub issue #291 with reframe comment + labels (`tech-debt` → `+cleanup`)

**Done when:** A single source-of-truth component exists for (a) progress bar and (b) upgrade CTA. Orphans are marked for deprecation.

### Gate G0.3 — Decision documents for P3 tickets

**Owner:** TBD (product + UX motion owner + admin UX owner)
**Blocks:** #293, #294, #295
**Parallelizable with:** G0.1, G0.2

**Tasks:**
- [ ] **Decision doc for #293** (PageTransition):
  - Named owner: who approves/rejects motion UX?
  - Pilot layout choice: `(chat)/layout.tsx` vs `(authenticated)/layout.tsx` vs both
  - Motion variant: `fade` | `slide` | `scale`
  - Go/no-go criteria (acceptable flicker threshold, Lighthouse impact)
  - Save in: `docs/development/page-transition-decision.md`
- [ ] **Decision doc for #294** (AgentStatsDisplay):
  - Target page: `admin/agents/definitions/page.tsx` vs dashboard widget vs both
  - Type mapping: does `AgentDefinition → AgentMetadata` require a mapper?
  - Save in: `docs/development/agent-stats-display-decision.md`
- [ ] **Decision doc for #295** (MobileCardLayout):
  - Product decision: tactile browsing worth adding complexity? (Y/N with rationale)
  - Target mobile surface: library discovery vs session resume vs other
  - Save in: `docs/development/mobile-card-layout-decision.md`
- [ ] If any decision is "NO", **close the corresponding GitHub issue** with rationale and mark orphan component for removal in a follow-up cleanup ticket.

**Done when:** Each of the 3 tickets has either a GO doc or a NO-GO close comment.

## Phase 1 — Warmup: RatingStars in GameHeader

**Issue:** meepleAi-app/meepleai-monorepo#290
**Effort:** S (2h)
**Risk:** Low
**Prerequisites:** None

**Why first:** Concrete target (GameHeader.tsx:69), replaces inline duplication with showcase component, proves the pattern works before touching larger surfaces.

**Files:**
- Modify: `apps/web/src/components/library/GameHeader.tsx`
- Modify: `apps/web/src/components/ui/data-display/rating-stars.tsx` (remove `@status ORPHAN`)
- Modify: `apps/web/src/components/showcase/stories/metadata.ts` (remove `[ORPHAN]` prefix)
- Test: `apps/web/src/components/library/__tests__/GameHeader.test.tsx` (create if missing)

**Steps:**
- [ ] **Step 1: Create characterization test for current GameHeader rating display**
```tsx
// apps/web/src/components/library/__tests__/GameHeader.test.tsx
import { render, screen } from '@testing-library/react';
import { GameHeader } from '../GameHeader';

describe('GameHeader rating display', () => {
  it('renders numeric rating when provided', () => {
    render(<GameHeader title="Catan" rating={7.8} />);
    expect(screen.getByText(/7\.8/)).toBeInTheDocument();
  });

  it('hides rating block when rating is null', () => {
    render(<GameHeader title="Catan" rating={null} />);
    expect(screen.queryByRole('img', { name: /stars?/i })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL (new test, missing assertions match)**
```bash
cd apps/web && pnpm test GameHeader
```

- [ ] **Step 3: Replace inline Star + number with RatingStars**
```tsx
// GameHeader.tsx — replace lines with <Star> inline
import { RatingStars } from '@/components/ui/data-display/rating-stars';

// Inside the component JSX, replace:
//   <Star className="w-3 h-3 fill-current" /> {rating.toFixed(1)}
// with:
{rating != null && (
  <RatingStars rating={rating} maxRating={10} size="sm" showValue />
)}
```

- [ ] **Step 4: Run tests — expect PASS**
```bash
cd apps/web && pnpm test GameHeader
```

- [ ] **Step 5: Remove `@status ORPHAN` JSDoc from `rating-stars.tsx`**

- [ ] **Step 6: Remove `[ORPHAN]` prefix from `showcase/stories/metadata.ts` (rating-stars entry)**

- [ ] **Step 7: Visual check — start dev server and verify GameHeader renders correctly**
```bash
cd apps/web && pnpm dev
# Navigate to a game detail page
```

- [ ] **Step 8: Typecheck + lint**
```bash
cd apps/web && pnpm typecheck && pnpm lint
```

- [ ] **Step 9: Commit**
```bash
git checkout -b feature/issue-290-rating-stars-game-header
git config branch.feature/issue-290-rating-stars-game-header.parent main-dev
git add apps/web/src/components/library/GameHeader.tsx \
        apps/web/src/components/library/__tests__/GameHeader.test.tsx \
        apps/web/src/components/ui/data-display/rating-stars.tsx \
        apps/web/src/components/showcase/stories/metadata.ts
git commit -m "feat(library): use RatingStars in GameHeader (closes #290)"
git push -u origin feature/issue-290-rating-stars-game-header
```

- [ ] **Step 10: Open PR against `main-dev`**

**DoD:**
- [ ] GameHeader consumes RatingStars
- [ ] Unit test for GameHeader rating display
- [ ] `@status ORPHAN` removed from source
- [ ] `[ORPHAN]` removed from showcase metadata
- [ ] Typecheck + lint clean
- [ ] PR opened against parent branch `main-dev`
- [ ] Issue #290 closed by merge

## Phase 2 — Parallel: AgentStatsDisplay + PageTransition

Two independent tickets touching disjoint areas. Can run on parallel branches.

### Phase 2a — #294 AgentStatsDisplay in admin list

**Issue:** meepleAi-app/meepleai-monorepo#294
**Effort:** S (~3h)
**Risk:** Low
**Prerequisites:** Gate G0.3 (agent-stats-display-decision.md)
**Branch:** `feature/issue-294-agent-stats-display`
**Parent:** `main-dev`
**Exclusive scope:** `apps/web/src/app/admin/(dashboard)/agents/definitions/`

**Scope locked:**
- Modify: chosen admin page from G0.3 decision
- Modify: `components/ui/agent/AgentStatsDisplay.tsx` (remove `@status ORPHAN`)
- Modify: `components/showcase/stories/metadata.ts`
- Create (if needed): adapter `mapAgentDefinitionToMetadata()` in `apps/web/src/lib/mappers/agent.ts`

**Steps:** Follow the same TDD + commit pattern as Phase 1. Detailed steps deferred to execution time after G0.3 decides target page.

**Exit criteria:**
- [ ] Admin agent list row renders `<AgentStatsDisplay>` with real data
- [ ] Unit test for mapper (if created)
- [ ] Typecheck + lint clean
- [ ] PR against `main-dev`
- [ ] Issue #294 closed

### Phase 2b — #293 PageTransition pilot

**Issue:** meepleAi-app/meepleai-monorepo#293
**Effort:** S (~3h)
**Risk:** Medium (streaming/Suspense interaction)
**Prerequisites:** Gate G0.3 (page-transition-decision.md)
**Branch:** `feature/issue-293-page-transition-pilot`
**Parent:** `main-dev`
**Exclusive scope:** chosen layout file from G0.3 decision

**Scope locked:**
- Modify: `apps/web/src/app/(chat)/layout.tsx` (or chosen pilot)
- Modify: `components/ui/animations/PageTransition.tsx` (remove `@status ORPHAN`)
- Modify: `components/showcase/stories/metadata.ts`

**Safety clauses (Nygard):**
- [ ] Feature flag the transition (env var `NEXT_PUBLIC_ENABLE_PAGE_TRANSITIONS`)
- [ ] Test in staging before merge — document no flicker, no hydration mismatch
- [ ] Lighthouse performance delta ≤ 5%

**Exit criteria:**
- [ ] Pilot layout wraps `children` in `<PageTransition variant="fade">`
- [ ] Manual test on at least 2 routes in the pilot scope (no flicker)
- [ ] Staging deploy + QA sign-off
- [ ] Typecheck + lint clean
- [ ] PR against `main-dev`
- [ ] Issue #293 closed

### Phase 2 coordination

**Run 2a and 2b in parallel** on separate branches. Both depend only on `main-dev`, neither touches the other's scope. Merge in any order.

## Phase 3 — Tier Kit Consolidation (REFRAMED #291)

**Issue:** meepleAi-app/meepleai-monorepo#291 (to be reframed)
**Effort:** M (~8h) — larger than original estimate due to cleanup scope
**Risk:** Medium (touches production `library/UsageWidget`)
**Prerequisites:** Gate G0.2 (tier-components-audit.md with KEEP/DEPRECATE decisions)
**Branch:** `feature/issue-291-tier-kit-consolidation`
**Parent:** `main-dev`

**Reframed objective:**
> Consolidate the three progress bar implementations and two upgrade CTA implementations in the tier/gate domain into a single canonical component each. Deprecate the orphan versions AND any inline duplications.

**Scope (pending G0.2 decision):**

**Assumption (most likely outcome from audit):**
- Keep `components/tier/UsageMeter.tsx` as canonical progress bar
- Keep `components/tier/UpgradeCta.tsx` as canonical upgrade CTA
- Delete `components/ui/feedback/collection-progress-bar.tsx` (orphan)
- Delete `components/ui/feedback/upgrade-prompt.tsx` (orphan)
- Refactor `components/library/UsageWidget.tsx:QuotaRow` to delegate to `UsageMeter`

**Steps (assuming above):**

- [ ] **Step 1: Delete orphan `collection-progress-bar.tsx` + test + story + metadata entry**
- [ ] **Step 2: Delete orphan `upgrade-prompt.tsx` + test + story + metadata entry**
- [ ] **Step 3: Refactor `UsageWidget.QuotaRow` to use `UsageMeter`**
- [ ] **Step 4: Run all tier-related tests**
```bash
cd apps/web && pnpm test tier UsageWidget
```
- [ ] **Step 5: Typecheck + lint**
- [ ] **Step 6: Verify showcase no longer lists the 2 deleted stories**
- [ ] **Step 7: Commit + PR**

**DoD:**
- [ ] 2 orphan components deleted (files + tests + stories + metadata + `parts/index.ts` exports)
- [ ] `QuotaRow` consolidated into `UsageMeter` usage
- [ ] All existing tier tests pass
- [ ] Typecheck + lint clean
- [ ] PR against `main-dev`
- [ ] Issue #291 closed (with REFRAMED scope documented in final comment)

## Phase 4 — ChatMessageList refactor (#292)

**Issue:** meepleAi-app/meepleai-monorepo#292
**Effort:** L (~16h)
**Risk:** HIGH (production chat pipeline)
**Prerequisites:** Gate G0.1 (characterization test suite passing)
**Branch:** `feature/issue-292-chat-message-list-refactor`
**Parent:** `main-dev`
**Exclusive lock:** no other PR may touch `chat-unified/*` during this phase

**Safety clauses (Nygard + Crispin):**
- [ ] G0.1 characterization tests MUST be green before starting
- [ ] Refactor in small commits: adapter first, then consumer swap, then avatar state
- [ ] Human architect code review required (not auto-approval)
- [ ] Staging smoke test: 5 manual chat exchanges with streaming, citations, feedback

**Sub-tasks (split into commits):**

### 4.1 — Create adapter function
- [ ] Create `apps/web/src/components/chat-unified/utils/toChatMessageProps.ts`
- [ ] TDD: test adapter with ChatMessageItem → ChatMessageProps conversion
- [ ] Commit: `refactor(chat): add ChatMessageItem → ChatMessage adapter`

### 4.2 — Replace inline rendering with `<ChatMessage>`
- [ ] Modify `ChatMessageList.tsx` to use `<ChatMessage {...toChatMessageProps(msg)} />`
- [ ] Keep feedback handler wired via callback prop
- [ ] Run G0.1 characterization tests — MUST all pass
- [ ] Commit: `refactor(chat): replace inline message block with ChatMessage component`

### 4.3 — Wire MeepleAvatar state mapping
- [ ] Map streaming/idle/error states to `MeepleAvatarState`
- [ ] Add unit test for state mapping
- [ ] Commit: `refactor(chat): wire MeepleAvatar state to agent response lifecycle`

### 4.4 — Cleanup orphan annotations
- [ ] Remove `@status ORPHAN` from `chat-message.tsx` + `meeple-avatar.tsx`
- [ ] Remove `[ORPHAN]` from metadata for both stories
- [ ] Commit: `chore(showcase): promote ChatMessage + MeepleAvatar from orphan status`

### 4.5 — Verify and open PR
- [ ] `ChatMessageList.tsx` ≤ 150 lines
- [ ] All G0.1 tests still pass
- [ ] Typecheck + lint clean
- [ ] Staging smoke test checklist complete
- [ ] PR opened against `main-dev`
- [ ] Architect review requested

**DoD:** (mirrors sub-tasks + original ticket DoD)

## Phase 5 — MobileCardLayout adoption (#295, OPTIONAL)

**Issue:** meepleAi-app/meepleai-monorepo#295
**Effort:** M (~12h)
**Risk:** Medium (new mobile surface)
**Prerequisites:** Gate G0.3 — **if NO-GO, skip this phase and close #295**
**Branch:** `feature/issue-295-mobile-card-layout-adoption`

**Conditional execution:** This phase runs only if Gate G0.3 produces a GO decision. If product decides tactile browsing isn't worth the complexity, this ticket is closed with a link to the decision doc, and `MobileCardLayout` is marked for deletion in a follow-up cleanup ticket.

**Steps:** deferred to execution time after G0.3 decides target surface.

## Execution Matrix (Summary)

| Phase | Ticket | Parallel with | Blocks | Effort | Risk |
|---|---|---|---|---|---|
| G0.1 | — (gate) | G0.2, G0.3 | #292 | S | Low |
| G0.2 | — (gate) | G0.1, G0.3 | #291 | S | Low |
| G0.3 | — (gate) | G0.1, G0.2 | #293, #294, #295 | S | Low |
| 1 | #290 | — | — | S | Low |
| 2a | #294 | Phase 2b | — | S | Low |
| 2b | #293 | Phase 2a | — | S | Medium |
| 3 | #291 (reframed) | — | — | M | Medium |
| 4 | #292 | — (exclusive lock) | — | L | **HIGH** |
| 5 | #295 | — | — | M (optional) | Medium |

## Risk Register

| Risk | Mitigation | Owner |
|---|---|---|
| #292 refactor breaks chat production path | G0.1 characterization tests + staging smoke test + architect review | Chat engineer + architect |
| #293 PageTransition breaks streaming/Suspense | Feature flag + staging test + Lighthouse check | Frontend engineer |
| #291 deletion breaks a hidden consumer | Full test suite run + grep for orphan component names before delete | Frontend architect |
| Parallel #293/#294 create merge conflicts | Disjoint scope lock (admin/ vs (chat)/) | PR reviewers |
| G0.3 decisions indefinitely delayed | Park tickets after 7 days without decision; close #293/#294/#295 if needed | Tech lead |

## Definition of Done for the Execution Plan

- [ ] All 3 gates (G0.1, G0.2, G0.3) produced artifacts
- [ ] Phase 1 merged (smallest change, biggest confidence boost)
- [ ] Phases 2a + 2b merged (or their tickets parked per G0.3)
- [ ] Phase 3 merged (2 orphans physically deleted from codebase)
- [ ] Phase 4 merged (ChatMessageList refactor passes characterization tests)
- [ ] Phase 5 either merged (if GO) or parked (if NO-GO)
- [ ] `docs/development/orphan-components-integration-plan.md` updated with final status
- [ ] Showcase metadata: no more `[ORPHAN]` prefixes (or: remaining orphans moved to a single "deprecated" section)

## Self-Review Checklist (writing-plans skill)

- [x] **Spec coverage:** all 6 issues referenced, each mapped to a phase
- [x] **No placeholders:** concrete file paths, concrete code samples in Phase 1, decision gates with explicit owners
- [x] **Type consistency:** `ChatMessageItem`, `ChatMessageProps`, `AgentMetadata` referenced consistently across phases
- [x] **Gate artifacts:** each Phase 0 gate produces a specific `docs/development/<name>.md` file
- [x] **Exit criteria:** each phase has measurable DoD
- [x] **Risk register:** explicit with mitigation + owner

## Execution Handoff

Two paths to execute this plan (writing-plans skill offers a choice):

### Option A — Subagent-Driven (recommended for Phases 1, 2a, 2b)
- Dispatch a fresh subagent per phase
- Review between phases
- Fast iteration, clean context isolation
- **Requires:** `superpowers:subagent-driven-development` skill

### Option B — Inline Execution (recommended for Phase 4)
- Execute in current session with checkpoints
- Better for long-running refactors needing continuous context
- **Requires:** `superpowers:executing-plans` skill

### Option C — Hybrid (actual recommendation)
- **Phases G0.* + 1 + 2a + 2b** → Subagent-driven (short, independent, parallelizable)
- **Phase 3** → Inline (medium complexity, touches production)
- **Phase 4** → Inline with human code review gate
- **Phase 5** → Subagent-driven or parked

## Next Actions

1. **Immediately:** User decides whether to execute Phase 0 gates (required before any ticket work)
2. **After G0.1 green:** Phase 1 (#290) can start
3. **After G0.2 decision:** Update #291 scope, Phase 3 unblocked
4. **After G0.3 decisions:** Phase 2a/2b/5 unblocked or parked
