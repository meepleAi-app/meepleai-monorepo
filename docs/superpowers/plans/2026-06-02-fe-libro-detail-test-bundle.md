# Plan — Libro Detail Test Bundle (#1552 + #1553 + #1554)

**Date**: 2026-06-02
**Issues**: #1552 (test LibroGameDetailView + isLibroGame) + #1553 (test CampaignSetupDrawer) + #1554 (a11y focus drawer step)
**Parent epic / cluster**: #1486 libro-detail conformity follow-up
**Branch**: `feature/issue-1552-libro-detail-test-bundle` from `main-dev`
**Effort**: ~3-4h (Tier S+M+XS bundle)
**Type**: FE-only, no BE deps, no schema changes
**Risk**: Low — characterization tests + small a11y polish

---

## Scope summary

Three follow-up issues from libro-detail gap report (`admin-mockups/design_handoff/libro-detail-gap-report.md`):

1. **#1552 (S, ~1h)** — Unit tests for `LibroGameDetailView` (337 LOC, tab state + subcomponents + KB badge variants) and `isLibroGame` utility (24 LOC, allowlist detection).
2. **#1553 (M, ~2h)** — Unit tests for `CampaignSetupDrawer` (515 LOC, 3-step wizard FSM + form validation + preset selection + submit branches).
3. **#1554 (XS, ~30min)** — A11y focus management when `CampaignSetupDrawer` step changes via Next/Back: move keyboard focus to first interactive element of new step.

Single PR `feat(libro-detail)` bundle. Closes #1552, #1553, #1554.

---

## DEC locked (spec-panel critique)

- **DEC-1** — Bundle PR singolo, 2 commit logici: (a) `test(libro-detail)`: tutti i nuovi file test, (b) `chore(libro-detail)`: focus a11y useEffect+ref + estensione test (`chore` per a11y polish — P148 commitlint type-enum).
- **DEC-2** — TDD ordering: T1 utility → T2 presentational → T3 FSM baseline → T4 focus RED → T5 source GREEN → T6 final verify. (T1+T2+T3 sono characterization tests, T4 è genuine TDD).
- **DEC-3** — jest-axe 1× per file su default render. Utility `is-libro-game.test.ts` skip axe (pure function).
- **DEC-4** — Focus management strategy: `useRef<HTMLDivElement>(null)` su step content container, `useEffect([step])` dopo skip-initial-mount guard chiama `ref.current?.querySelector<HTMLElement>('button:not([disabled]), input:not([disabled]), [role="radio"]')` e `.focus()`. Pattern stabilizzato (no dipendenza da external focus-management hook).
- **DEC-5** — NO mock i18n (componenti non usano hooks i18n, strings hardcoded italiano).
- **DEC-6** — Fixture `LibraryGameDetail`: helper `makeLibraryGameDetail()` con minimal valid object + override params per case-specific test. Co-located in test file (non in shared fixture lib, scope locale).
- **DEC-7** — Mock pattern (eredita NewCampaignDialog.test.tsx pattern): `vi.mock('@/lib/api/gamebook-campaigns')` + `vi.mock('next/navigation')` con pushSpy. Drawer test richiede `installMatchMedia(true)` + jsdom PointerEvent/scrollIntoView shims (pattern da `drawer.test.tsx`).

## G/W/T scenarios

### S1 — Utility `isLibroGame` (#1552 part 1)
- **G** `gameTitle = 'Nanolith'`, **W** `isLibroGame({ gameTitle })`, **T** `=== true`
- 4 negativi: `'Catan'`, `''`, `null`, `undefined` → tutti `false`

### S2 — `LibroGameDetailView` default render (#1552 part 2)
- **G** `gameDetail` minimal valid, **W** mount component, **T** tab `'info'` active + `InfoPanel` rendered + 4 `MetaStat` cells + 5 `Pip` chips + KB badge row + jest-axe `0 violations`

### S3 — `LibroGameDetailView` tab switch (#1552 part 2)
- **G** default render, **W** `userEvent.click(getByRole('tab', { name: 'AI Chat' }))`, **T** `getByText(/Pannello AI Chat · in arrivo/)` visible

### S4 — `LibroGameDetailView` KB badge variants (#1552 part 2)
- 4 case parameterized: `kbStatus='ready'`, `kbStatus='indexing'`, `kbStatus='error'`, fallback `hasRagAccess=true`, fallback `hasRagAccess=false` → assert title text matches

### S5 — `CampaignSetupDrawer` Step 1 (#1553 part 1)
- **G** drawer open step 1, **W** type 2 char in title, **T** Next button disabled + error message `≥ 3 caratteri`
- **W** type 3+ char, **T** Next enabled, error gone
- **W** click preset radio `group-b`, **T** `aria-checked='true'` su radio + altri 2 `aria-checked='false'`

### S6 — `CampaignSetupDrawer` Step 2 + Step 3 (#1553 part 2)
- **G** advance to step 2, **T** 4 player chips visible (Aaron host + 3 guest preset group-a) + agent suggestion card
- **G** advance to step 3, **T** review card with campaign title + preset name + ManaPips list

### S7 — `CampaignSetupDrawer` submit branches (#1553 part 3)
- **G** step 3, **W** click Submit + createCampaign resolves, **T** `pushSpy` called with `/library/g1/play/<campaignId>`
- **G** step 3, **W** click Submit + createCampaign rejects, **T** error message visible
- **G** step 3 mid-mutation, **T** button text `Creazione…` + `disabled`

### S8 — `CampaignSetupDrawer` focus management (#1554)
- **G** drawer open step 1, **W** click Next button, **T** `document.activeElement` matches first focusable in step 2 (host chip area — but step 2 has no interactive elements except disabled "Aggiungi giocatore" button → fallback target: footer Next button? Need to re-verify mockup)
- **G** drawer open step 2, **W** click Next, **T** activeElement matches first focusable in step 3 (Submit button if step 3 is review-only)
- **G** drawer open step 3, **W** click Back, **T** activeElement matches first focusable in step 2
- **NOTE**: Step 2 has only disabled "Aggiungi giocatore" — interactive elements in step 2 are `[role="button"]` chips which are not focusable by default. May need to **add `tabIndex={-1}` to step container** as fallback focus target if no focusable child exists. Decision deferred to T5 source implementation.

---

## Task breakdown

| # | Task | Effort | Files | Mode |
|---|------|--------|-------|------|
| **T1** | `is-libro-game.test.ts` — 5 case + axe skip | XS | NEW `apps/web/src/lib/games/__tests__/is-libro-game.test.ts` | mechanical (haiku) |
| **T2** | `LibroGameDetailView.test.tsx` — ~7 case (S2/S3/S4) | S | NEW `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx` | mechanical (haiku) |
| **T3** | `CampaignSetupDrawer.test.tsx` baseline (S5/S6/S7) — ~12 case | M | NEW `apps/web/src/components/features/gamebook/__tests__/CampaignSetupDrawer.test.tsx` | stateful (sonnet) |
| **T4** | Extend CampaignSetupDrawer.test.tsx with focus assertions (S8) — 3 case + step-2 fallback assertion | XS | EDIT same file as T3 | judgment (sonnet) — **RED expected** |
| **T5** | Implement focus management in CampaignSetupDrawer.tsx | XS | EDIT `apps/web/src/components/features/gamebook/CampaignSetupDrawer.tsx` | source change (sonnet) — **GREEN expected** |
| **T6** | Final verify | XS | run typecheck + lint + test scope | verify (sonnet) |

**Total**: ~35-40 test cases new, 1 source file modified, 3 test files created.

---

## Acceptance criteria traceability

### #1552 AC
- [ ] `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx` created (T2)
- [ ] Covers tab state (default `info`, switch to other tabs) (S3)
- [ ] Covers `MetaStat` rendering (S2)
- [ ] Covers `Pip` rendering (S2)
- [ ] Covers `InfoPanel` with description + KB status (S4)
- [ ] Covers placeholder text "in arrivo con la prossima iter" (S3)
- [ ] `apps/web/src/lib/games/__tests__/is-libro-game.test.ts` created (T1)
- [ ] Covers allowlist `['Nanolith']` → true; other titles → false; empty/undefined → false (S1)
- [ ] jest-axe assertion on default render (S2)
- [ ] `pnpm typecheck && pnpm lint && pnpm test` PASS (T6)

### #1553 AC
- [ ] `apps/web/src/components/features/gamebook/__tests__/CampaignSetupDrawer.test.tsx` created (T3)
- [ ] Covers Step 1: title input rendering, preset radio cards, validation error <3 chars, Next disabled until valid (S5)
- [ ] Covers Step 2: host chip + guest chips + agent suggestion (S6)
- [ ] Covers Step 3: review card with meta + ManaPips (S6)
- [ ] Covers footer: Back/Next/Submit/Cancel handlers (S7)
- [ ] Covers submit branches: `mutation.isPending` → "Creazione…" + disabled, error branch shows message (S7)
- [ ] jest-axe assertion on at least one step (S5 default render = step 1)
- [ ] `pnpm typecheck && pnpm lint && pnpm test` PASS (T6)

### #1554 AC
- [ ] Step 1 → 2 moves focus to first interactive element of step 2 (S8)
- [ ] Step 2 → 3 moves focus to first interactive element of step 3 (S8)
- [ ] Step 3 → 2 (back) moves focus (S8)
- [ ] Unit test asserts `document.activeElement` after setStep (T4)
- [ ] No regression in modal-trap behavior — focus stays inside drawer (verify via Radix focus-trap default + no regressions in existing CampaignSetupDrawer.test.tsx submit flow)

---

## Out of scope (deferred / not addressed)

- i18n migration of hardcoded Italian strings — separate concern, not in #1552/#1553 AC
- Additional KB badge variants beyond 4 documented (no other states in source)
- Wiring of "Aggiungi giocatore" button (disabled by design, per `// Iter 4 — wire-up in iter futuro`)
- Cross-browser focus management verification (jsdom only, real-browser Playwright deferred to e2e)
- Integration test for `/library/[gameId]` page consuming `LibroGameDetailView` — already covered by `apps/web/src/app/(authenticated)/library/[gameId]/__tests__/page.test.tsx` (mocks component, verifies orchestrator wiring)

---

## Pipeline standard (sessione precedente verified)

1. ✅ Branch hygiene (HEAD main-dev, status clean, pull --ff-only)
2. ✅ Feature branch + parent config
3. ✅ Plan doc (this file)
4. T1+T2 subagent haiku mechanical (parallel possibile — independent files)
5. T3 subagent sonnet stateful
6. T4 subagent sonnet judgment (RED expected)
7. T5 subagent sonnet source change (GREEN expected)
8. T6 verify
9. Final code-reviewer feature-dev:code-reviewer sonnet
10. Commit 2× + push + PR open + body
11. CI watch (3 FE Tests shards + FE Static + FE Fast + GitGuardian + CodeQL) + merge normale no-admin (baseline #1773 resolved per memoria)
12. Cleanup branch

## References

- Gap report: `admin-mockups/design_handoff/libro-detail-gap-report.md § 5 (a11y) + § 6 (test coverage) + § 8 F3`
- Pattern test analogo: `apps/web/src/components/features/gamebook/__tests__/NewCampaignDialog.test.tsx`
- Pattern Drawer test: `apps/web/src/components/ui/drawer/drawer.test.tsx` (matchMedia + PointerEvent shims)
- Source: `LibroGameDetailView.tsx` (337 LOC), `CampaignSetupDrawer.tsx` (515 LOC), `is-libro-game.ts` (24 LOC)
