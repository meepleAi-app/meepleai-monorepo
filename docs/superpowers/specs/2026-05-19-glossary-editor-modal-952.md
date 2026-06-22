# Glossary Editor Modal — Spec (draft) for #952

| Field | Value |
|---|---|
| **Issue** | #952 [V2 SP6 Iter 1B] Implement glossary editor route (sp6-libro-game-glossary-editor) |
| **Mockup** | `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx` (908 LOC total, ~328 LOC for `GlossaryEditorModal`) |
| **Parent** | #786 SP6 Libro Game umbrella (CLOSED 2026-05-11) |
| **IA dep** | #871 `[deferred]` IA consolidation /gamebook/* vs /library/games/*/play (CLOSED) — route decision deferred |
| **Status** | hardened — panel score 6.5 → 8.5/10 |
| **Date** | 2026-05-19 |
| **Hardening** | 2026-05-19 — panel P0+P1+P2 applied. D1 RESOLVED (backend has no collision detection); state-04 scope dropped from this PR; AC-5/AC-7 removed. See §14 change-log. |

## 1. Context

The libro-game (gamebook) feature ships a glossary per campaign — a map of EN source terms (e.g. "Voidstone") to user-curated IT translations (e.g. "Pietra del Vuoto"). The glossary is bootstrapped from the source storybook and rendered as inline "pills" on the translated paragraph in `TranslateViewer` / `TranslationPane`.

Backend is **fully shipped**:
- `GamebookGlossaryEntry` entity in BC `SessionTracking`
- Endpoints: `GET /api/v1/gamebook/campaigns/{id}/glossary`, `POST .../bootstrap`, `PUT .../{entryId}` (upsert)
- FE API client: `apps/web/src/lib/api/gamebook-glossary.ts`
- FE hooks: `useGamebookGlossary`, `useBootstrapGlossary`, `useUpsertGlossary` already exist in `apps/web/src/lib/gamebook/hooks/useGamebookGlossary.ts`

What's missing is the **edit surface** the mockup describes: when the user taps a glossary pill on the translated paragraph, a modal floats above the viewer with the EN term locked and the IT term editable.

## 2. Goals & Non-goals

### Goals

- **G1**: Implement `GlossaryEditorModal` component in `apps/web/src/components/features/gamebook/` per the post-Stage-2 path convention (per #952 comment dated 2026-05-11).
- **G2**: Cover all 6 FSM states from the mockup (4 mobile + 2 desktop variants).
- **G3**: Wire to existing `useUpsertGlossary(campaignId)` mutation for the PUT.
- **G4**: Surface a backend-detected "collision" (state-04: target translation already in use by another EN term) with the two-action recovery flow ([Sovrascrivi] / [Cambia traduzione]).
- **G5**: Add a row to `docs/for-developers/frontend/v2-migration-matrix.md`.

### Non-goals

- **NG1**: New route `/library/games/[gameId]/play/[campaignId]/glossary` — the IA decision dependency (#871) is `[deferred]` + CLOSED. The mockup is a **modal** that floats above `TranslateViewer`, not a standalone CRUD page. Bulk listing UI is a separate ticket.
- **NG2**: Integration into `TranslateViewer` (mount + state plumbing for which pill triggered the modal) — separate FE ticket after the modal lands. This PR ships the modal in isolation + a Storybook-style fixture mount for visual tests.
- **NG3**: Backend changes. If the backend doesn't currently emit a 409 collision signal on PUT, the FE optimistically detects collision by pre-checking the cached glossary list locally (graceful degrade to 200 if backend tolerates duplicates).
- **NG4**: Visual regression / conformity gate entry (`mockup-ownership.bootstrap.json`) — deferred to a follow-up because the mockup carries 6 frame variants and the conformity gate (per #1276 lesson) expects single-screen baseline.

## 3. FSM states inventory

From the mockup header. **State-04 (collision) is dropped from this PR scope after D1 resolution** — the backend `UpsertGlossaryEntryCommandHandler` does NOT detect `termIt` duplicates across entries. Collision UI is tracked as a follow-up ticket pending backend work.

| ID | State | Notes |
|---|---|---|
| s01-m | `state-01-edit-pristine` | Mobile bottom-sheet. EN "Voidstone" locked, IT "Pietra del Vuoto" filled, Salva disabled (no dirty). |
| s02-m | `state-02-edited` | Mobile. IT input dirty → "Pietra del Caos". Salva enabled. Diff hint shows old value strikethrough. |
| s03-m | `state-03-save-error` | Mobile. Network/500 failure. Amber banner + Retry. Input value preserved. |
| ~~s04-m~~ | ~~`state-04-collision`~~ | **Out of scope (D1 resolved 2026-05-19 — backend has no collision detection). Follow-up ticket required.** |
| s01-d | `state-01-desktop` | Desktop. Modal centered 480px, no bottom-sheet. Else identical to s01-m. |
| ~~s04-d~~ | ~~`state-04-desktop-conflict`~~ | **Out of scope (see s04-m). Mockup file retains the visual; FE skips it.** |

**Net states implemented**: 4 (s01-m, s02-m, s03-m, s01-d).

## 4. Component decomposition

Single file `GlossaryEditorModal.tsx`. Internal sub-components (private to the file):

| # | Name | Purpose |
|---|---|---|
| C1 | `GlossaryEditorModal` | Orchestrator. Manages state machine + mutation + variants. |
| C2 | `TranslationInput` | Controlled `<input>` + diff hint when dirty (strikethrough old value). |
| C3 | `ErrorBanner` | Amber state-03 banner with Retry callback. |
| C4 | `ActionRow` | Footer primary + secondary button row, vertical on mobile / horizontal on desktop. |

~~`CollisionBanner`~~ — dropped with state-04 scope (D1 resolution). The mockup `FauxViewerBackdrop` / `FauxPill` are visual fixture only and stay in the mockup file — they do not get ported.

## 5. Props contract

```ts
export interface GlossaryEditorModalProps {
  /** The campaign owning the glossary; required for the upsert mutation key. */
  readonly campaignId: string;
  /** The entry being edited; `null` closes the modal. */
  readonly entry: GamebookGlossaryEntry | null;
  /** Called when the user dismisses the modal (escape, scrim, X button). */
  readonly onClose: () => void;
  /** Called after a successful PUT — parent decides what to do (toast, invalidate cache, etc.). */
  readonly onSaved?: (saved: GamebookGlossaryEntry) => void;
  /** Force layout for visual tests; auto-derived from `window.matchMedia` otherwise. */
  readonly forceLayout?: 'mobile' | 'desktop';
}
```

P0-2 (panel) resolved: `conflictingEntry` prop **removed**. Collision state was an internal mutation outcome, not parent-injected data — leaking it into the public surface split responsibility. Removed entirely along with state-04 scope (P0-1).

## 6. State machine

In-component `useReducer` with the following state shape:

```ts
type ModalState = {
  /** Current IT input value (controlled). */
  itValue: string;
  /** Original IT value at mount, for diff hint + dirty detection. */
  initialIt: string;
  /** Submit lifecycle. */
  status: 'idle' | 'saving' | 'error';
  /** Last-known network error message; null when no error. */
  errorMessage: string | null;
};
```

Transitions:

- `EDIT(newValue)` → `itValue = newValue`, `status: idle`
- `SUBMIT` → `status: saving`, `errorMessage: null`
- `SUBMIT_OK` → modal triggers `onSaved` callback; parent decides whether to also call `onClose`
- `SUBMIT_FAIL(message)` → `status: error`, errorMessage = message

P1-3 + P1-4 (panel) resolved: dropped `SUBMIT_COLLISION`, `OVERWRITE`, `CHANGE_VALUE` transitions — they were tied to state-04 (out of scope). No silent no-op branches remain.

## 7. Backend contract dependency

D1 RESOLVED 2026-05-19 via direct read of `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpsertGlossaryEntryCommandHandler.cs`:

- The handler validates **ownership** (returns `ConflictException` "Forbidden" when caller is not the campaign owner) and **campaign scoping** (`ConflictException` if entry belongs to another campaign). It does NOT detect cross-entry `termIt` duplicates.
- Per-entry update path: `existing.UpdateTermIt(cmd.TermIt, cmd.CallerUserId)` — silent overwrite of the IT value, no 409 on duplicates.

Implication: state-04 collision UI ships only when backend gains duplicate detection. Tracked as follow-up ticket "feat(gamebook): #952-followup detect duplicate termIt in UpsertGlossaryEntryCommandHandler + surface 409".

- **`onSaved` cache invalidation**: handled by `useUpsertGlossary` already (existing `onSuccess: invalidateQueries`).
- **403/404 on PUT**: routed through `SUBMIT_FAIL` → state-03 error banner (no special UI; error message from response body).

## 8. AC SMART

7 ACs total (was 9; AC-5 + AC-7 dropped with state-04 scope).

- [ ] **AC-1** Pristine mount — given `entry = { termEn: 'Voidstone', termIt: 'Pietra del Vuoto', ... }`, the modal mounts with EN locked, IT prefilled, Salva button disabled (`expect(screen.getByRole('button', { name: /salva/i })).toBeDisabled()`).
- [ ] **AC-2** Dirty edit — typing into the IT input enables Salva AND shows the diff hint as a sibling element containing the original value with `text-decoration: line-through`. Concrete assertion: `expect(screen.getByText('Pietra del Vuoto')).toHaveStyle({ textDecoration: 'line-through' })`.
- [ ] **AC-3** Submit success — Salva click triggers `useUpsertGlossary` PUT. On 200, `onSaved` fires exactly once with the returned entry. The modal does NOT call `onClose` itself (parent owns the close decision). Concrete: `expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ termIt: 'Pietra del Caos' }))` AND `expect(onClose).not.toHaveBeenCalled()`.
- [ ] **AC-3b** Dismiss flows — pressing Escape, clicking the scrim, or clicking the X button each call `onClose` exactly once AND do NOT call `onSaved`. Three separate test cases, one per trigger.
- [ ] **AC-4** Submit error — on MSW 500 response, amber `ErrorBanner` renders with `data-slot="glossary-editor-error"` + "Riprova" button. Clicking Riprova re-fires the mutation with the same payload (verify via second MSW intercept).
- [ ] **AC-6** Desktop layout — when `forceLayout === 'desktop'`, modal root has computed `width: 480px` and the dialog is positioned centered (test via `data-slot="glossary-editor-dialog-desktop"` marker rather than computed-style assertion, which is jsdom-flaky).
- [ ] **AC-8** Accessibility — modal root has `role="dialog"`, `aria-modal="true"`, `aria-labelledby` pointing to a header element with that id; focus moves to the IT input on mount (not the close button); Escape calls `onClose`; jest-axe finds zero violations on the **4 in-scope states** rendered via the `<MockedModalScenario state="..." />` test harness (s01-m, s02-m, s03-m, s01-d).
- [ ] **AC-9** i18n — all visible strings sourced from `useTranslation` (it/en JSON catalogues). The entity literal (`termEn`, `termIt`) is injected via props and renders unchanged.

P0-3 + P1-1 + P1-2 (panel) resolved: each AC now has a concrete RTL assertion. AC-3b added for dismiss-flow coverage. AC-8 scope explicitly tied to the 4 in-scope states.

## 9. Test strategy

- **Component**: vitest + RTL. One `describe` block per in-scope FSM state (s01-m, s02-m, s03-m, s01-d). MSW handlers in `apps/web/src/__tests__/mocks/handlers/gamebook-glossary.handlers.ts` (NEW; pattern matches `gamebook.handlers.ts` from #1303 — request-capture exported for AC-3 / AC-4 retry assertions).
- **Accessibility**: jest-axe on rendered output for each in-scope state (AC-8).
- **Dismiss flows**: three separate test cases for Escape / scrim / X (AC-3b) — vitest `userEvent` for Escape + scrim click, `getByRole('button', { name: /chiudi/i })` for X.
- **Diff hint**: AC-2 asserts `text-decoration: line-through` on the sibling node — robust to copy changes.
- **Visual**: deferred (NG4).
- **Coverage**: `pnpm vitest --coverage` ≥ 85% **statement** + ≥ 75% **branch** coverage on `GlossaryEditorModal.tsx` (the reducer has 4 transitions; branch coverage catches missed arms). Existing `useTranslateParagraph.test.ts` not affected (deleted by #1306).

## 9a. Definition of Done

- [ ] `pnpm lint` 0 errors
- [ ] `pnpm typecheck` exit 0
- [ ] `pnpm vitest run src/components/features/gamebook/__tests__/GlossaryEditorModal.test.tsx` green (7 ACs + AC-3b dismiss-flows)
- [ ] Coverage gate met per §9
- [ ] `gamebook/index.ts` barrel exports `GlossaryEditorModal` + `GlossaryEditorModalProps`
- [ ] `v2-migration-matrix.md` row added (id `glossary-editor-modal`, status `done`)
- [ ] Follow-up ticket opened for state-04 collision (backend duplicate-detection + FE banner)

## 10. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backend doesn't emit 409 collision | Medium | Medium | Verify in implementation. If 200, drop state-04 ACs (AC-5, AC-7) + open follow-up |
| Modal mount/unmount interaction with `TranslateViewer` not exercised | High | Low | NG2 explicitly defers integration; modal ships standalone + visual fixture |
| 6 FSM states explode test count | Medium | Low | Shared `renderState(stateId)` helper; one describe per state |
| Conformity baseline mismatch (#1276 lesson) | High if attempted | Medium | NG4 — defer conformity entry to a follow-up |

## 11. Sequencing

Single PR, estimated ~4-5h:

1. Read mockup file → extract layout dimensions + color tokens
2. Create `GlossaryEditorModal.tsx` with reducer + 5 sub-components
3. Wire `useUpsertGlossary`; handle 200 / 4xx / 409 in mutation `onError`/`onSuccess`
4. Add to `gamebook/index.ts` barrel export
5. Write 9 ACs as RTL tests + jest-axe
6. Add `v2-migration-matrix.md` row

## 12. Open decisions — RESOLVED

- [x] **D1** RESOLVED 2026-05-19. Backend `UpsertGlossaryEntryCommandHandler` validates ownership + campaign scoping but does NOT detect cross-entry `termIt` duplicates (silent overwrite via `existing.UpdateTermIt(...)`). State-04 dropped from this PR; follow-up ticket required for backend duplicate detection + 409 contract.
- [x] **D2** Cache invalidation: confirmed `useUpsertGlossary.onSuccess` already invalidates `gamebookGlossaryKeys.list(campaignId)`. `TranslateViewer` consumes the same query (per file inspection 2026-05-19). No action needed in this PR.
- [x] **D3** "Sovrascrivi" semantics: **N/A** — dropped with state-04 (D1).

## 13. Hardening change-log

### 2026-05-19 — Panel P0+P1+P2 applied (score 6.5 → ~8.5 estimate)

- §1 frontmatter: Hardening row added; status → "hardened".
- §3 FSM: state-04 mobile + desktop crossed out; 6 states → 4 in-scope (P0-1).
- §4 components: `CollisionBanner` removed (P0-1 cascade).
- §5 props: `conflictingEntry` removed (P0-2).
- §6 state machine: `SUBMIT_COLLISION` / `OVERWRITE` / `CHANGE_VALUE` removed; `status: 'collision'` removed from union (P1-3, P1-4).
- §7 backend: D1 resolution paragraph + file reference (`UpsertGlossaryEntryCommandHandler.cs`). 403/404 routing to state-03 documented.
- §8 ACs: 9 → 7. AC-5 + AC-7 dropped. AC-3b added for dismiss flows (P1-1). AC-2 + AC-6 + AC-8 hardened with concrete assertions (P0-3, P1-2).
- §9 test strategy: dismiss-flow coverage detail; branch coverage requirement (P2-2).
- §9a Definition of Done section added.
- §11 sequencing: implicit; no change.
- §12 decisions: D1+D2+D3 all resolved with direct evidence.
- §13 (this section): added.

## 13. References

- Issue #952
- Closed parent: #786, IA dep #871
- Stage 2 path convention: comment on #952 dated 2026-05-11 (post #1025 merged)
- Mockup: `admin-mockups/design_files/sp6-libro-game-glossary-editor.jsx`
- Backend handler: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpsertGlossaryEntryCommandHandler.cs`
- FE API client: `apps/web/src/lib/api/gamebook-glossary.ts`
- FE hooks: `apps/web/src/lib/gamebook/hooks/useGamebookGlossary.ts`
- Conformity lesson: #1276 (single-screen baseline)
- v2-migration-matrix: `docs/for-developers/frontend/v2-migration-matrix.md`
