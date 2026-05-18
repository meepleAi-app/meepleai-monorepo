# SP7 — Game Night Create Wizard — Phase 0.5 Contract

| Field | Value |
|---|---|
| **Issue** | [#950](https://github.com/meepleAi-app/meepleai-monorepo/issues/950) |
| **Parent** | Wave 3 (frontend cluster) — note: original parent reference `#582` is Wave D sessions (CLOSED), corrected to Wave 3 |
| **Sibling shipped** | [#685](https://github.com/meepleAi-app/meepleai-monorepo/issues/685) / [#1170](https://github.com/meepleAi-app/meepleai-monorepo/issues/1170) — `/game-nights` INDEX (closed 2026-05-12) |
| **Mockup** | `admin-mockups/design_files/sp7-game-night-create.{html,jsx}` — 1629 LOC |
| **Status** | draft — Phase 0.5 contract (pre-implementation) |
| **Date** | 2026-05-18 |
| **Authors** | spec-panel (Wiegers/Fowler/Newman/Nygard/Adzic/Cockburn) |
| **Branch** | `feature/issue-950-spec-game-night-create` |
| **Re-panel target** | ≥ 8.0/10 from 5.1/10 baseline |

## 1. Problem statement

Replace the legacy `/game-nights/new` page (42 LOC, `_content.tsx`) with the v2 mockup `sp7-game-night-create` (1629 LOC, **10 FSM states**: 9 mobile + 1 desktop split-form). The original issue body classified this as **Tier S** (single-shot, 4 AC), but the mockup describes a **Tier L+** wizard with:

- 4-step wizard (Quando · Dove · Chi · Cosa)
- Desktop split-form (12-col: 8-col form + 4-col RSVP-card live preview)
- Conflict detection (date warning step 1)
- Player invite autocomplete (registered + email mixed)
- Game candidates picker (library + decide-at-group toggle)
- 6 NEW v2 components

Without a Phase 0.5 contract, implementation is guaranteed rework (Wave C.1 + D.2 documented pattern).

## 2. Goals & Non-goals

### Goals

- **G1**: Document the 10 FSM states + 6 v2 components for deterministic implementation.
- **G2**: Audit hook composition (4 new hooks needed) + backend dependency (3 missing endpoints + 1 schema gap).
- **G3**: Decide state management strategy (URL SSOT vs useReducer vs Zustand).
- **G4**: Define draft autosave + retry + failure-recovery policy (Nygard panel feedback).
- **G5**: Decide invitee schema: extend `CreateGameNightCommand` to support email-only invitations OR drop email column from mockup.
- **G6**: Provide ≥ 6 Gherkin scenarios covering the FSM (Adzic panel feedback).
- **G7**: Re-panel review target ≥ 8.0/10 to unblock implementation dispatch.

### Non-goals

- **NG1**: No implementation (Phase 0.5 is contract drafting only).
- **NG2**: No backend endpoint implementation; this spec inventories the gaps and proposes the sequencing.
- **NG3**: No change to legacy `/game-nights/new` route until implementation PR — current behavior preserved.
- **NG4**: No mockup HTML modification (visual SoT is fixed).

## 3. Tier classification

**Tier L+** (declared override of original Tier S body classification).

Justification:

| Metric | Threshold (Wave C.1/D.2 retro) | This route |
|---|---|---|
| Mockup LOC | Tier L if > 800 | **1629** |
| FSM states | Tier L if ≥ 5 | **10** |
| New components | Tier L if ≥ 4 | **6** |
| Hook composition | Tier L if ≥ 3 hooks | **5** (1 existing + 4 new) |
| Backend gaps | Phase 0.5 mandatory if ≥ 1 | **4** (3 endpoints + 1 schema) |
| Cartesian state explosion | Tier L if states × inputs > 12 | **40+** (10 states × 4 wizard steps) |

Implication: **Phase 0.5 contract is mandatory, not optional** (pattern P-Wave-C.1 / Wave-D.2 from session memory).

## 4. FSM states inventory

From the mockup top-of-file comment:

| ID | State | Notes |
|---|---|---|
| 01 | `step1-date` | Step 1 default — date+time picker with mini-calendar |
| 02 | `step1-warning` | Step 1 conflict warning (`var(--c-warning)` amber tone) — surfaces when `useGameNightConflictCheck` returns hit |
| 03 | `step2-location` | Step 2 toggle 4 options (Casa Marco · Casa amico · Online · Da definire) |
| 04 | `step3-empty` | Step 3 empty + regulars suggestions (from `useRegularsForUser`) |
| 05 | `step3-typing` | Step 3 autocomplete dropdown active (`usePlayerSearch`) |
| 06 | `step3-filled` | Step 3 with 6 invitees (mix registered + email) — **schema-dependent** |
| 07 | `step4-games` | Step 4 game candidates picker (3 selected from `useMyGameLibrary`) |
| 08 | `step4-decide-group` | Step 4 toggle "lascia decidere al gruppo" active |
| 09 | `mobile-step-flow` | All 4 steps side-by-side (compact overview) |
| 10 | `desktop-split` | Desktop split-form (form + RSVPCard live preview) |

State 06 has **schema dependency**: see §7.

## 5. Component breakdown (6 NEW v2)

Canonical path: `apps/web/src/components/features/game-night-create/` (NOT `components/v2/` — FREEZE per #1023 lifted but path-migration is the convention).

| # | Component | Purpose | Mockup ref |
|---|---|---|---|
| C1 | `GameNightCreateWizard` | Orchestrator; mounts step components by `currentStep` |  L:198-1629 (multiple) |
| C2 | `GameNightDateTimePicker` | Step 1 — mini-calendar + time slot + conflict warning surface | L:194-380 |
| C3 | `GameNightLocationToggle` | Step 2 — 4-option segmented control | L:382-520 |
| C4 | `PlayerInviteAutocomplete` | Step 3 — search input + dropdown + regulars suggestions + chip list | L:522-880 |
| C5 | `GameCandidatesPicker` | Step 4 — library grid + decide-at-group toggle | L:882-1100 |
| C6 | `RSVPCardLivePreview` | Desktop split — live preview consuming form state | L:1102-1300 |

Reused primitives (NOT new):

- `StepIndicator` (SP6-B port, 3→4 step) — needs interface check; if SP6-B doesn't already export, add a small variant adapter (lives in `components/ui/step-indicator/` per Stage 2 conventions, not new SP7 surface).
- `EntityChip` (Wave 1) — for game/player chips.
- `Avatar`/`EmailAvatar` — already exist or trivially extracted from mockup (`L:170-192`).
- `MeepleCard` — NOT a fit here; the wizard prefers stepper + compact rows.

## 6. Hook composition matrix

Audit run 2026-05-18 against `apps/web/src/hooks/queries/`.

| Hook | Status | Endpoint | Notes |
|---|---|---|---|
| `useCreateGameNight` | ✅ existing | `POST /api/v1/game-nights` | `useGameNights.ts:58` — mutation; invalidates `gameNightKeys.all` on success |
| `useMyGameLibrary` | 🟡 candidate exists | `GET /api/v1/users/me/library/games?...` | Verify: `useLibrary.ts` exports `useUserLibrary(params)` (Issue #2614 / #3513) — confirm it returns the shape Step 4 needs (cover/players/duration/weight) and that `enabled` allows lazy fetch when Step 4 opens |
| `usePlayerSearch` | ❌ NEW | `GET /api/v1/players?q=` | Returns registered users searchable by name/email prefix. Debounce 250ms client-side. Cache key `['players', 'search', q]`. Stale 30s. |
| `useGameNightConflictCheck` | ❌ NEW | `GET /api/v1/game-nights/check-conflict?at=<iso>` | Returns conflicting events (if any) for the authed user's calendar window ±2h. Triggers state-02 warning surface |
| `useRegularsForUser` | ❌ NEW | `GET /api/v1/users/me/regulars` | Returns historical co-participants ranked by event count, last 12 months. Cache key `['users', 'me', 'regulars']`. Stale 5min. |

**Net new frontend hooks**: 3 (`usePlayerSearch`, `useGameNightConflictCheck`, `useRegularsForUser`) — assuming `useUserLibrary` from `useLibrary.ts` is reusable (verify in Foundation phase).

## 7. Backend dependency matrix

Audit run 2026-05-18 via `grep -rn "MapGet" apps/api/src/Api/Routing` + schema inspection.

| Need | Endpoint | Status | Resolution |
|---|---|---|---|
| Player autocomplete | `GET /api/v1/players?q={query}` | ❌ **missing** | Add in `AgentMemoryEndpoints.cs` or new `PlayersEndpoints.cs` (BC `AgentMemory` already owns `/players/me/*`). Pagination optional V1. |
| User library games | `GET /api/v1/users/me/library/games` | 🟡 **likely-existing** | Verify via `useLibrary` query keys; if absent, surface in BC `UserLibrary`. |
| Conflict check | `GET /api/v1/game-nights/check-conflict?at={iso}` | ❌ **missing** | Add in BC `GameManagement` group. Query: SELECT events WHERE scheduledAt OVERLAPS [at-2h, at+2h] AND (organizerId=user OR userId IN invitees). |
| Regulars | `GET /api/v1/users/me/regulars` | ❌ **missing** | Add in BC `AgentMemory` (co-participants are an AgentMemory concept). |
| **Schema** | `CreateGameNightCommand` | ⚠️ **email-gap** | Currently `InvitedUserIds: List<Guid>?` only. Mockup state-06 mixes email+user. **Decision required**: extend command with `InvitedEmails: List<string>?` OR drop email surface from mockup. See §8. |

**Risk**: like Wave 3 INDEX (#732 4-week backend roadmap), this introduces 3 new endpoints + 1 schema extension across 2 BCs (AgentMemory + GameManagement). Recommended sequencing: backend Phase 1 (endpoints + schema) → frontend Foundation (hooks) → frontend implementation.

## 8. State management decision

**Decision: URL SSOT for `?step=` + `useReducer` for in-memory form state.**

### Rationale

| Strategy | Pro | Con | Verdict |
|---|---|---|---|
| URL state SSOT (all fields) | Deep-linkable, refresh-safe | URL bloat (player IDs + email arrays) | Reject — too much serialization |
| `useReducer` only | Encapsulated, type-safe transitions | No deep-link to a step | Reject — loses test fixture surface |
| **URL `?step=N` + `useReducer` for fields** | Deep-link to step N (for visual tests `?step=3-filled`), form state encapsulated | Two sources of truth (acceptable: URL = navigation, reducer = data) | **Accept** |
| Zustand | Cross-component sharing | Overkill for single-page wizard | Reject |

### Reducer skeleton

```ts
type WizardState = {
  step: 1 | 2 | 3 | 4;
  date: { iso: string | null; conflictCheckedAt: string | null; conflictResult: ConflictResult | null };
  location: { kind: 'home' | 'friend' | 'online' | 'tbd'; details: string };
  invitees: ReadonlyArray<{ kind: 'user'; id: string } | { kind: 'email'; address: string }>;
  games: { decideAtGroup: boolean; selected: ReadonlyArray<string> };
  draft: { savedAt: string | null; status: 'idle' | 'saving' | 'error' };
};

type WizardAction =
  | { type: 'goToStep'; step: 1 | 2 | 3 | 4 }
  | { type: 'setDate'; iso: string }
  | { type: 'recordConflict'; result: ConflictResult }
  | { type: 'setLocation'; kind: WizardState['location']['kind']; details: string }
  | { type: 'addInvitee'; invitee: WizardState['invitees'][number] }
  | { type: 'removeInvitee'; key: string }
  | { type: 'toggleDecideAtGroup' }
  | { type: 'toggleGame'; gameId: string }
  | { type: 'draftSaveStart' }
  | { type: 'draftSaveSuccess'; savedAt: string }
  | { type: 'draftSaveError' };
```

### Visual-test surface

`?step=N` and `?fixture=<state-id>` are gated by `IS_VISUAL_TEST_BUILD` (Wave B.3 pattern). Production build constant-folds them out.

## 9. Draft autosave + retry policy

Nygard panel feedback: zero draft autosave + retry policy in original body.

### Decisions

- **Autosave trigger**: debounce 800ms after any reducer action that mutates form state (skip `goToStep`).
- **Storage**: `localStorage` keyed `meepleai:gamenight-create-draft:<userId>` (avoid cross-user leak).
- **Schema**: snapshot of `WizardState` minus `draft` itself (avoid recursion).
- **Lifetime**: cleared on `useCreateGameNight` mutation success OR after 7 days TTL (avoid stale state when schema evolves — guard with `schemaVersion: 1`).
- **Retry policy on submit failure**: exponential backoff `[1s, 2s, 4s]` max 3 attempts; surface user-facing error on final attempt with "Riprova" button + "Salva bozza e esci" fallback.
- **Conflict check failure**: silent fallback to "no conflicts" (UX) + analytics event `gamenight.conflict-check.failed`. Don't block submission.

### Risks

- Network flake during `useCreateGameNight` mutation: covered by retry.
- User loses tab mid-wizard: covered by autosave + restore-on-mount.
- Schema migration breaks deserialization: guarded by `schemaVersion`.

## 10. Email invitee schema decision

Mockup state-06 shows **mix of registered users + email invitations**.

Current backend (`CreateGameNightCommand`, `CreateGameNightInputSchema`):
- `InvitedUserIds: List<Guid>?` — registered users only.

### Decision: Extend command (Option A)

**Option A** (proposed): Extend with `InvitedEmails: List<string>?`.

- Backend: 1 schema migration (additive, no breaking change), validator (RFC 5321, max 200 chars, max 49 emails).
- Server-side flow: send invitation email with magic link → recipient registers → user account auto-linked to game night.
- Schema migration: add `InvitedEmails` column to draft `GameNightInvitation` aggregate (BC `Authentication` or new BC `Invitation` — coordinate with #847 Wave 2 deferred invitation aggregate).

**Option B** (alternative): Drop email column from mockup.

- Mockup state-06 becomes "registered-only Step 3 filled".
- Lower scope, but reduces UX value (Marco can't invite Federica who hasn't signed up yet).

**Recommendation**: Option A, but **coordinate with #847** to share the invitation aggregate. If #847 sequencing makes this expensive, fall back to Option B and revisit when #847 ships.

## 11. AC SMART per phase

5-commit TDD decomposition (Wave C.1 / Wave D.2 pattern).

### Foundation (commit 1)

- [ ] AC-F.1: `lib/game-nights/wizard-reducer.ts` (pure reducer + 100% unit coverage on action handlers)
- [ ] AC-F.2: `lib/game-nights/wizard-validators.ts` (Zod schemas for each step + step-completion predicate)
- [ ] AC-F.3: `lib/game-nights/wizard-fixture.ts` (visual-test fixture sentinel exposing 10 states deterministically; gated by `IS_VISUAL_TEST_BUILD`)
- [ ] AC-F.4: i18n it+en (~80 keys/locale)
- [ ] AC-F.5: hooks (`usePlayerSearch`, `useGameNightConflictCheck`, `useRegularsForUser`) **— gated on backend endpoints landed**; if blocked, ship Foundation with stub hooks returning `{ data: [], isLoading: false }` and document gap in commit body

### Components (commit 2)

- [ ] AC-C.1: 6 v2 components in `components/features/game-night-create/`
- [ ] AC-C.2: unit tests per component (Vitest, ≥ 85% coverage per component)
- [ ] AC-C.3: `data-slot="game-night-create-{N}"` attribute on each component root (for E2E selector)

### Orchestrator + page (commit 3)

- [ ] AC-O.1: `GameNightCreateWizard` in `app/(authenticated)/game-nights/new/_components/`
- [ ] AC-O.2: `page.tsx` wiring (replaces legacy)
- [ ] AC-O.3: `useReducer` integration + URL `?step=` sync (URL → reducer init only; reducer → URL on `goToStep`)
- [ ] AC-O.4: Autosave hook `useGameNightDraftPersist` + restore on mount + clear on submit success
- [ ] AC-O.5: `useCreateGameNight` mutation with retry `[1s, 2s, 4s]`

### E2E (commit 4)

- [ ] AC-E.1: `e2e/visual-migrated/sp7-game-night-create.spec.ts` covering 10 FSM states × { desktop 1280, mobile 375 } = 20 PNGs
- [ ] AC-E.2: `e2e/v2-states/game-night-create.spec.ts` covering happy path + 6 Gherkin scenarios (see §12)
- [ ] AC-E.3: `e2e/a11y/game-night-create.spec.ts` covering axe-core WCAG 2.1 AA per state + reduced-motion contract

### Baselines + matrix update (commit 5)

- [ ] AC-B.1: Bootstrap baselines (`visual-regression-migrated.yml` mode=bootstrap)
- [ ] AC-B.2: `v2-migration-matrix.md` row: id `game-night-create`, status `done`, PR ref
- [ ] AC-B.3: `mockup-ownership.bootstrap.json` entry + conformity baseline via `bootstrap-mockup-baselines.yml` (single screen, not multi-state catalog — avoid #1276 architectural pitfall)

## 12. Gherkin scenarios (Adzic ≥ 6)

### Scenario 1: Happy path desktop

```gherkin
Given Marco is authenticated on /game-nights/new (desktop 1280)
When he selects a date 14 days out
And selects location "Casa Marco"
And invites 4 regulars from the suggestion list
And toggles "lascia decidere al gruppo"
And clicks "Crea evento"
Then `POST /api/v1/game-nights` is called with `{ scheduledAt, location, invitedUserIds[4], gameIds: [] }`
And the user is redirected to `/game-nights/{id}` with toast "Evento creato"
And the localStorage draft key is cleared
```

### Scenario 2: Date conflict warning + override

```gherkin
Given Marco is on Step 1
When he selects a date that overlaps an existing event (per `useGameNightConflictCheck`)
Then state-02 warning surfaces with the conflicting event title + amber accent
When he clicks "Continua comunque"
Then the warning is dismissed but the date is preserved
And the wizard advances to Step 2
```

### Scenario 3: Mixed invitee types

```gherkin
Given backend supports `InvitedEmails: List<string>?` (Option A)
And Marco is on Step 3
When he searches "Federica" and adds her from autocomplete (registered)
And types "ospite@example.com" and adds via enter (email)
Then the invitees list shows 2 entries: 1 Avatar (registered), 1 EmailAvatar (dashed)
When he clicks "Avanti"
Then the wizard advances to Step 4 with both invitees preserved
```

### Scenario 4: Draft autosave + restore

```gherkin
Given Marco completed Steps 1 + 2
When the browser tab closes
And Marco reopens /game-nights/new within 7 days
Then the wizard mounts with restored state (date + location pre-filled)
And the StepIndicator shows current step as Step 3
And a non-intrusive banner reads "Bozza ripristinata · scarta" with a discard button
```

### Scenario 5: Submit failure + retry

```gherkin
Given Marco filled all steps and clicks "Crea evento"
When `POST /api/v1/game-nights` returns 503 on first attempt
Then the wizard retries after 1s, 2s, 4s
When all retries fail
Then an error toast surfaces with "Riprova" + "Salva bozza e esci" buttons
And the form state is preserved
```

### Scenario 6: Mobile step flow + decide-at-group

```gherkin
Given Marco is on /game-nights/new (mobile 375)
When he completes Steps 1-3
And on Step 4 toggles "lascia decidere al gruppo" ON
Then the game candidates picker grid is dimmed (state-08)
And the submit payload has `gameIds: []` (empty array, not undefined)
And the RSVP-card preview shows "Giochi: da decidere al gruppo"
```

## 13. Test strategy

- **Foundation tests**: pure reducer + validators + fixture sentinel.
- **Components tests**: shallow render + interaction (testing-library + user-event).
- **Orchestrator tests**: integration with mocked hooks + URL sync.
- **E2E visual**: 10 states × 2 viewports = 20 baselines via `visual-regression-migrated.yml`.
- **E2E states**: 6 Gherkin scenarios via `e2e/v2-states/game-night-create.spec.ts` with `page.context().route()` API mocks (no `IS_VISUAL_TEST_BUILD` for behavior tests).
- **A11y**: axe-core WCAG 2.1 AA per state + reduced-motion guarantee.
- **Conformity gate**: mockup baseline = single canonical screen (state-10 desktop-split) to avoid catalog-vs-route mismatch (lesson from #1276).

## 14. Sequencing

```
Week 1 (backend):
  - Phase 1a: GET /api/v1/players?q= (BC AgentMemory)
  - Phase 1b: GET /api/v1/users/me/regulars (BC AgentMemory)
  - Phase 1c: GET /api/v1/game-nights/check-conflict (BC GameManagement)
  - Phase 1d: Schema extension `InvitedEmails: List<string>?` (CreateGameNightCommand + Validator)
  - Verify GET /api/v1/users/me/library/games (likely exists, document)

Week 2 (frontend Foundation):
  - Commit 1 (Foundation): reducer + validators + fixture + i18n + 3 NEW hooks
  - Re-panel review checkpoint (Foundation merged, contract validated)

Week 3 (frontend Components + Orchestrator):
  - Commit 2 (Components): 6 v2 components + unit tests
  - Commit 3 (Orchestrator): wizard + page wiring + autosave

Week 4 (frontend E2E + Baselines):
  - Commit 4 (E2E): 3 spec files (visual-migrated, v2-states, a11y)
  - Commit 5 (Baselines + matrix): bootstrap + matrix row + conformity entry
```

Critical path: Week 1 backend is sequential blocker (mirror Wave 3 INDEX backend roadmap #732 — 4 weeks).

If backend cannot start Week 1, Foundation ships with stub hooks (returning empty), implementation degrades gracefully (regulars empty, autocomplete shows "Nessun risultato"), and we re-prioritize backend ASAP.

## 15. Risks + mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backend endpoints delayed | High | High (blocks Steps 3 + 1 conflict) | Stub hooks pattern (Wave 3 INDEX precedent); Foundation can ship without backend |
| Schema email-gap unresolved | High | Medium (Option A vs B) | This spec recommends Option A + coordinate with #847; Option B is the fallback |
| Conformity catalog mismatch | Medium | Medium | Lesson from #1276 — bootstrap single canonical screen only, no multi-state mockup baseline |
| Reducer complexity drift | Low | Medium | Pure-function discipline + 100% reducer unit coverage gate |
| Autosave cross-user leak | Low | High (PII leak) | localStorage key includes `userId`; clear on logout via auth hook |
| Bundle budget | Medium | Low | New components fit within Wave 3 budget; estimate +50 KB gzip; if > +80 KB consider code-split per step |

## 16. References

- Issue [#950](https://github.com/meepleAi-app/meepleai-monorepo/issues/950) — this spec's target.
- Sibling [#685](https://github.com/meepleAi-app/meepleai-monorepo/issues/685) / [#1170](https://github.com/meepleAi-app/meepleai-monorepo/issues/1170) — `/game-nights` INDEX (closed, blueprint).
- Backend precedent [#732](https://github.com/meepleAi-app/meepleai-monorepo/pull/732) — Wave 3 backend roadmap (4-week sequencing).
- Deferred [#847](https://github.com/meepleAi-app/meepleai-monorepo/issues/847) — Invitation aggregate (Wave 2 staging access) — coordinate for email schema.
- Conformity lesson [#1276](https://github.com/meepleAi-app/meepleai-monorepo/issues/1276) — multi-screen mockup catalog vs single-screen route mismatch.
- Mockup: `admin-mockups/design_files/sp7-game-night-create.{html,jsx}`.
- Pattern: Wave C.1 + Wave D.2 5-commit decomposition (session memory).
- DS de-versioning umbrella [#1023](https://github.com/meepleAi-app/meepleai-monorepo/issues/1023) (closed) — path conventions (`components/features/<feature>/`).

## 17. Open decisions (post-panel review)

- [ ] **D1**: Approve Option A (email schema extension) vs Option B (drop email)? → impacts #847 coordination.
- [ ] **D2**: Confirm `useUserLibrary` from `useLibrary.ts` matches Step 4 needs (cover/players/duration/weight)?
- [ ] **D3**: Confirm `BC AgentMemory` is the right home for `/users/me/regulars` (vs new BC)?
- [ ] **D4**: Bundle budget — accept +50 KB gzip or require code-split per step?
- [ ] **D5**: Schedule — start Week 1 backend now or sequence after #822 backend Phase 5?
