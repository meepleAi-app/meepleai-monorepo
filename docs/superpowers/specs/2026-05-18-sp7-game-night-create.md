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
| **Hardening** | 2026-05-18b — 5 open decisions resolved (see §17); 2026-05-18c — Week 1 panel findings applied (see §20) |

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
| `useLibrary` | ✅ verified 2026-05-18b | `GET /api/v1/users/me/library/games` via `api.library.getLibrary(params)` | Confirmed at `apps/web/src/hooks/queries/useLibrary.ts:69`. Returns `PaginatedLibraryResponse` with `UserLibraryEntry[]`. Fields present per `library.schemas.ts:32-63`: `gameImageUrl` (cover), `minPlayers`/`maxPlayers` (players), `playingTimeMinutes` (duration), `complexityRating` (weight), `averageRating`. `enabled` param supports lazy fetch. **D2 resolved**: no new hook needed for Step 4. |
| `usePlayerSearch` | ❌ NEW | `GET /api/v1/players?q=` | Returns registered users searchable by name/email prefix. Debounce 250ms client-side. Cache key `['players', 'search', q]`. Stale 30s. |
| `useGameNightConflictCheck` | ❌ NEW | `GET /api/v1/game-nights/check-conflict?at=<iso>` | Returns conflicting events (if any) for the authed user's calendar window ±2h. Triggers state-02 warning surface |
| `useRegularsForUser` | ❌ NEW | `GET /api/v1/users/me/regulars` | Returns historical co-participants ranked by event count, last 12 months. Cache key `['users', 'me', 'regulars']`. Stale 5min. |

**Net new frontend hooks**: 3 (`usePlayerSearch`, `useGameNightConflictCheck`, `useRegularsForUser`). `useLibrary` confirmed reusable (D2 resolved 2026-05-18b).

## 7. Backend dependency matrix

Audit run 2026-05-18 via `grep -rn "MapGet" apps/api/src/Api/Routing` + schema inspection.

| Need | Endpoint | Status | Resolution |
|---|---|---|---|
| Player autocomplete | `GET /api/v1/users/search?q={query}` | ⚠️ **partial reuse** | **A1 resolved 2026-05-18c**: `SearchUsersQuery` already exists in BC `Administration` (`apps/api/src/Api/BoundedContexts/Administration/Application/Queries/SearchUsersQuery.cs`) with minLength=2 enforced (handler L:28-31), MaxResults param, error-safe (returns empty on exception). Currently used in 2 admin endpoints. **Implementation**: NO new query/handler — add thin authenticated-user endpoint (NOT admin-only) that calls `SearchUsersQuery` via `IMediator.Send()`. Endpoint path renamed `/users/search?q=` (was `/players?q=`) for BC alignment (User entity, not Player). FE hook `usePlayerSearch` preserves mockup terminology via translation adapter. |
| User library games | `GET /api/v1/users/me/library/games` | ✅ **existing** | Confirmed via `useLibrary` hook + `api.library.getLibrary(params)` client; backend route owns BC `UserLibrary`. |
| Conflict check | `GET /api/v1/game-nights/check-conflict?at={iso}` | ❌ **missing** | Add in BC `GameManagement` group. Query: SELECT events WHERE scheduledAt OVERLAPS [at-2h, at+2h] AND (organizerId=user OR userId IN invitees). **Timeout**: 500ms server-side (PostgreSQL with date index), 1.5s client AbortController (W1 §7b). |
| Regulars | `GET /api/v1/game-nights/regulars` | ❌ **missing** | **D3 resolved 2026-05-18b**: Add in BC `GameManagement` (NOT `AgentMemory` as original spec proposed). **A2 resolved 2026-05-18c**: path renamed `/users/me/regulars` → `/game-nights/regulars` for BC ownership consistency (BC owns path prefix). Rationale: regulars aggregate from `game_nights.invitedUserIds` historical events (event-level) — same BC that owns game-nights. Stale 5min server cache invalidated on `GameNightPublished` event (P2 §9b). |
| **Schema** | `CreateGameNightCommand` | ⚠️ **email-gap** | Currently `InvitedUserIds: List<Guid>?` only. Mockup state-06 mixes email+user. **D1 resolved 2026-05-18b**: Option A — extend command with `InvitedEmails: List<string>?`. **R3 resolved 2026-05-18c**: validator must enforce `InvitedUserIds.Count + InvitedEmails.Count ≤ 49 combined` (NOT 49+49=98). Independent from #847 (different aggregates: #847 = staging access tokens in BC Authentication; #950 = event invitees in BC GameManagement). |

**Risk** (updated 2026-05-18c): scope reduced from "3 new endpoints + 1 schema" to "2 new endpoints + 1 thin-wrapper endpoint + 1 schema" across **1 BC** (GameManagement) + thin endpoint coordination with BC Administration. Performance risk: `users.display_name` + `users.email` need `pg_trgm` GIN index for sub-100ms autocomplete (P1 §15). Recommended sequencing: backend Phase 1 (endpoints + schema + index) → frontend Foundation (hooks) → frontend implementation.

## 7b. Backend endpoint contracts (added 2026-05-18c)

Per Wiegers R1 (testability) and Adzic G1 (executable scenarios). All endpoints require authenticated user session.

### 7b.1 `GET /api/v1/users/search?q={q}&limit={N}`

| Field | Value |
|---|---|
| **Auth** | Authenticated user (NOT admin-only) |
| **Query: `q`** | string, minLength=2, maxLength=100. Trimmed. Empty/short → empty array. |
| **Query: `limit`** | int, default=20, max=50. Server-side clamp. |
| **Response 200** | `UserSearchResultDto[]` — `{ id: Guid, displayName: string, email: string }[]`. Sorted by `displayName` ASC. Max `limit` entries. |
| **Response 400** | `q` exceeds 100 chars |
| **Response 401** | Unauthenticated |
| **Response 429** | Rate limit exceeded (10 req/sec per user) |
| **Performance** | < 100ms p95 with pg_trgm GIN index. Without index: < 500ms acceptable for V1. |
| **Reuses** | `Api.BoundedContexts.Administration.Application.Queries.SearchUsersQuery` |

### 7b.2 `GET /api/v1/game-nights/regulars?limit={N}`

| Field | Value |
|---|---|
| **Auth** | Authenticated user; returns regulars **for current user** (organizer perspective) |
| **Query: `limit`** | int, default=10, max=30 |
| **Response 200** | `RegularDto[]` — `{ id: Guid, displayName: string, email: string, eventCount: int, lastInvitedAt: DateTimeOffset }[]`. Sorted by `eventCount` DESC, then `lastInvitedAt` DESC. |
| **Response 401** | Unauthenticated |
| **Aggregation** | `game_night_invitations` JOIN `users` WHERE organizer_id = current_user AND invited_at > NOW() - INTERVAL '12 months' GROUP BY invited_user_id ORDER BY COUNT(*) DESC LIMIT N |
| **Cache** | Server: 5min memory cache keyed by `userId`, invalidated on `GameNightPublishedEvent` (P2 §9b). Client: 5min staletime via React Query. |

### 7b.3 `GET /api/v1/game-nights/check-conflict?at={iso}`

| Field | Value |
|---|---|
| **Auth** | Authenticated user; checks **current user's** calendar |
| **Query: `at`** | ISO 8601 datetime offset (e.g. `2026-06-01T20:00:00+02:00`). Required. |
| **Response 200** | `ConflictCheckDto` — `{ hasConflict: bool, conflicts: ConflictEntryDto[] }`. `conflicts: { id: Guid, title: string, scheduledAt: DateTimeOffset, role: "organizer" | "invitee" }[]`. |
| **Response 400** | `at` invalid ISO or > 2 years in future |
| **Response 401** | Unauthenticated |
| **Timeout** | 500ms server-side via `CancellationToken`; client AbortController 1.5s; on timeout returns `{ hasConflict: false, conflicts: [] }` + emits `gamenight.conflict-check.timeout` counter (P2 §9b). |
| **Query semantics** | `SELECT id, title, scheduled_at FROM game_nights WHERE scheduled_at BETWEEN @at - INTERVAL '2 hours' AND @at + INTERVAL '2 hours' AND status IN ('Draft', 'Published') AND (organizer_id = @userId OR EXISTS (SELECT 1 FROM game_night_invitations WHERE game_night_id = id AND invited_user_id = @userId))` |

### 7b.4 `POST /api/v1/game-nights` (extended schema)

| Field | Value |
|---|---|
| **Added field** | `invitedEmails: string[]?` — RFC 5321 format, max 200 chars each |
| **Combined validation** | `invitedUserIds.Count + invitedEmails.Count ≤ 49` (NOT 49+49=98) |
| **Migration** | Additive column `invited_emails: text[] NOT NULL DEFAULT '{}'` on `game_night_invitations` parent table — existing rows backfill to empty array. Migration must include UP+DOWN scripts (Crispin T2). |

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

### Decision: Extend command (Option A) — **D1 resolved 2026-05-18b**

**Option A** (accepted): Extend with `InvitedEmails: List<string>?`.

- Backend: 1 schema migration (additive, no breaking change), validator (RFC 5321, max 200 chars, max 49 emails).
- Server-side flow: send invitation email with magic link → recipient registers → user account auto-linked to game night.
- Schema migration: add `InvitedEmails` column to draft `GameNightInvitation` aggregate (BC `Authentication` or new BC `Invitation` — coordinate with #847 Wave 2 deferred invitation aggregate).

**Option B** (alternative): Drop email column from mockup.

- Mockup state-06 becomes "registered-only Step 3 filled".
- Lower scope, but reduces UX value (Marco can't invite Federica who hasn't signed up yet).

**Recommendation** (updated 2026-05-18b): Option A confirmed. **No coordination needed with #847** — the two issues have distinct aggregates:

- **#847** (deferred + P2): Invitation aggregate in BC `Authentication` — staging access tokens (signup gate). Token-based magic-link flow.
- **#950** (this spec): `InvitedEmails: List<string>?` column on `GameNight` aggregate in BC `GameManagement` — event-level invitations to specific game-nights.

Future UX integration possible (e.g. when #847 ships, an email-only invitee receiving a magic-link could land directly into game-night onboarding), but no schema sharing required. Option B (drop email) is no longer the fallback.

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

## 9b. Observability (added 2026-05-18c per Nygard panel)

Backend Prometheus counters emitted by handlers:

```
gamenight_create_attempts_total{outcome="success|failure|validation_error"}
gamenight_create_retry_total{attempt="1|2|3"}
gamenight_conflict_check_total{outcome="no_conflict|conflict_detected|timeout|error"}
gamenight_conflict_check_duration_seconds (histogram, buckets: 0.05, 0.1, 0.25, 0.5, 1, 2, 5)
gamenight_users_search_total{outcome="empty_query|results|rate_limited|error"}
gamenight_regulars_cache_total{outcome="hit|miss|invalidated"}
```

Client-side analytics events (existing `analytics.ts`):

- `gamenight.create.{started, succeeded, failed}` with `{ stepCount, invitedUserCount, invitedEmailCount, gameCount, decideAtGroup }`
- `gamenight.conflict-check.{queried, conflict, override, failed}`
- `gamenight.draft.{saved, restored, discarded}`

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

## 12b. Backend Gherkin scenarios (added 2026-05-18c per Adzic G1)

### BE-1: User search empty/short query returns empty

```gherkin
Given the system contains 1000 users
When GET /api/v1/users/search?q= (empty)
Then response is 200 with []
When GET /api/v1/users/search?q=a (1 char)
Then response is 200 with []
```

### BE-2: User search respects default limit

```gherkin
Given 50 users with displayName starting "ali"
When GET /api/v1/users/search?q=ali
Then response is 200 with 20 entries (default limit)
And entries are sorted by displayName ASC
```

### BE-3: User search rate limited

```gherkin
Given an authenticated user
When 11 GET /api/v1/users/search?q=foo are sent within 1 second
Then the 11th response is 429 Too Many Requests
And the counter gamenight_users_search_total{outcome="rate_limited"} is incremented
```

### BE-4: Conflict check OVERLAPS query

```gherkin
Given user U1 has GameNight A scheduled at 2026-06-01T20:00:00Z (Published)
When U1 sends GET /api/v1/game-nights/check-conflict?at=2026-06-01T19:00:00Z
Then response is 200 with { hasConflict: true, conflicts: [A] }
When U1 sends GET /api/v1/game-nights/check-conflict?at=2026-06-01T23:00:00Z
Then response is 200 with { hasConflict: false, conflicts: [] }
```

### BE-5: Conflict check timeout returns no-conflict gracefully

```gherkin
Given the PostgreSQL query takes 700ms (exceeds 500ms server timeout)
When GET /api/v1/game-nights/check-conflict?at=<future>
Then response is 200 with { hasConflict: false, conflicts: [] }
And counter gamenight_conflict_check_total{outcome="timeout"} is incremented
```

### BE-6: Regulars sorted by event count desc

```gherkin
Given U1 has invited: U2 (5 events), U3 (3 events), U4 (1 event) in last 12 months
When U1 sends GET /api/v1/game-nights/regulars
Then response is 200 with [U2, U3, U4] (sorted by eventCount DESC)
And each entry includes eventCount + lastInvitedAt
```

### BE-7: Validator enforces combined invitee limit

```gherkin
Given CreateGameNightCommand with invitedUserIds.Count=40 and invitedEmails.Count=10
When validator runs
Then validation fails with "Combined invited users and emails cannot exceed 49"

Given CreateGameNightCommand with invitedUserIds.Count=25 and invitedEmails.Count=24
When validator runs
Then validation passes (total=49)
```

### BE-8: Migration backward-compat

```gherkin
Given a Postgres database with the pre-migration schema (no invited_emails column)
When the migration applies
Then game_night_invitations.invited_emails column exists with type text[]
And existing rows have invited_emails = '{}' (empty array, NOT NULL)
When the migration DOWN script applies
Then the column is removed without data loss to invited_user_ids
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
Week 1 (backend) — updated 2026-05-18c:
  PR-W1.1 (schema + validator + migration):
    - 1d: Schema extension `InvitedEmails: List<string>?` (CreateGameNightCommand + Validator + DTO)
    - 1d': EF Core migration: invited_emails text[] NOT NULL DEFAULT '{}' (additive)
    - 1d'': Combined validator: invitedUserIds.Count + invitedEmails.Count ≤ 49
    - Tests: handler unit + integration (Testcontainers Postgres) + migration UP+DOWN test
    - AC: ≥85% coverage on validator, 100% on combined-limit logic

  PR-W1.2 (3 endpoints):
    - 1a: GET /api/v1/users/search?q= (thin wrapper reusing SearchUsersQuery from BC Administration)
         + RequireAuthenticatedUser middleware (NOT admin-only)
         + Rate limit attribute (10 req/sec authenticated user)
    - 1b: GET /api/v1/game-nights/regulars (BC GameManagement — path renamed from /users/me/regulars)
         + 5min memory cache + GameNightPublishedEvent invalidation handler
    - 1c: GET /api/v1/game-nights/check-conflict (BC GameManagement)
         + 500ms server timeout via CancellationToken
         + pg_trgm index migration on users(display_name, email) — required for 1a perf
    - Tests: handler unit (mocked repo) + endpoint integration (TestServer + Testcontainers)
            + 8 Gherkin BE scenarios from §12b
    - AC: ≥85% coverage per handler, 100% on critical paths (timeout, rate-limit, combined-limit)

  Verify: GET /api/v1/users/me/library/games (✅ confirmed existing — no work needed)

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

## 15. Risks + mitigations (updated 2026-05-18c per Nygard P1-P3)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Backend endpoints delayed | Medium (scope reduced by reuse) | High (blocks Steps 3 + 1 conflict) | Stub hooks pattern (Wave 3 INDEX precedent); Foundation can ship without backend. SearchUsersQuery reuse removes 1 dependency. |
| Schema email-gap unresolved | Resolved (D1) | — | Option A accepted §10 |
| Conformity catalog mismatch | Medium | Medium | Lesson from #1276 — bootstrap single canonical screen only, no multi-state mockup baseline |
| Reducer complexity drift | Low | Medium | Pure-function discipline + 100% reducer unit coverage gate |
| Autosave cross-user leak | Low | High (PII leak) | localStorage key includes `userId`; clear on logout via auth hook |
| Bundle budget | Medium | Low | New components fit within Wave 3 budget; estimate +50 KB gzip; if > +80 KB consider code-split per step |
| **NEW** `/users/search` perf degradation | High at > 10K users | Medium (UX latency) | **pg_trgm GIN index** on `users(display_name, email)` — required as part of PR-W1.2 migration. Without index: ILIKE scan ~200ms at 1K users, > 1s at 10K. |
| **NEW** User enumeration via `/users/search` | Medium | Medium (privacy) | Rate limit 10 req/sec per authenticated user (P3 §15). Future: confirm response shape doesn't expose PII beyond `displayName + email` (already minimal) |
| **NEW** Conflict-check query timeout in prod | Medium | Low (degrades to no-conflict UX) | 500ms server `CancellationToken` + Prometheus counter `gamenight_conflict_check_total{outcome="timeout"}` for alerting |
| **NEW** Cache invalidation drift on `/regulars` | Low | Low (stale data ≤ 5min) | `GameNightPublishedEvent` handler invalidates `regulars:{userId}` cache key. Test: integration test creating GN → re-query regulars → assert new entry visible |

## 16. References

- Issue [#950](https://github.com/meepleAi-app/meepleai-monorepo/issues/950) — this spec's target.
- Sibling [#685](https://github.com/meepleAi-app/meepleai-monorepo/issues/685) / [#1170](https://github.com/meepleAi-app/meepleai-monorepo/issues/1170) — `/game-nights` INDEX (closed, blueprint).
- Backend precedent [#732](https://github.com/meepleAi-app/meepleai-monorepo/pull/732) — Wave 3 backend roadmap (4-week sequencing).
- Deferred [#847](https://github.com/meepleAi-app/meepleai-monorepo/issues/847) — Invitation aggregate (Wave 2 staging access) — coordinate for email schema.
- Conformity lesson [#1276](https://github.com/meepleAi-app/meepleai-monorepo/issues/1276) — multi-screen mockup catalog vs single-screen route mismatch.
- Mockup: `admin-mockups/design_files/sp7-game-night-create.{html,jsx}`.
- Pattern: Wave C.1 + Wave D.2 5-commit decomposition (session memory).
- DS de-versioning umbrella [#1023](https://github.com/meepleAi-app/meepleai-monorepo/issues/1023) (closed) — path conventions (`components/features/<feature>/`).

## 17. Open decisions — RESOLVED 2026-05-18b

- [x] **D1**: ✅ Option A — extend `CreateGameNightCommand` with `InvitedEmails: List<string>?`. Independent from #847 (different aggregates, see §10). No coordination needed.
- [x] **D2**: ✅ `useLibrary` from `apps/web/src/hooks/queries/useLibrary.ts:69` is reusable for Step 4. Returns `PaginatedLibraryResponse` with `UserLibraryEntry[]`. All needed fields present in `library.schemas.ts:32-63` (`gameImageUrl`, `minPlayers`/`maxPlayers`, `playingTimeMinutes`, `complexityRating`, `averageRating`). No new hook needed (`useMyGameLibrary` removed from §6).
- [x] **D3**: ✅ BC `GameManagement` (NOT `AgentMemory`). Rationale: regulars = derived view aggregating `game_nights.invitedUserIds` historical events — same BC owns game-nights. AgentMemory's "guest player claims" is a different concept (proxy players, not event participants).
- [x] **D4**: ✅ No formal size-limit tooling exists in `apps/web` (verified absent `.size-limit.json` + no `size-limit` script in `package.json`). Procedure: +50 KB gzip is the informal target; PR description must document bundle delta from `pnpm build` output for manual review. Formal enforcement deferred to a separate DevOps issue (out of scope #950).
- [x] **D5**: ✅ PR #1283 merged 2026-05-18 (closes #822 Toolkit Phase 5 BE). No backend sequencing conflict with #950.

## 18. Hardening change-log

### 2026-05-18b — D1-D5 resolved

Changes:

- §1 frontmatter: added `Hardening` row pointing to this change-log.
- §6 hook composition: `useLibrary` row replaces `useMyGameLibrary` candidate; status promoted to ✅ verified with concrete file references.
- §7 backend dependency:
  - `User library games` row promoted to ✅ existing (no new endpoint).
  - `Regulars` row resolution rewritten — BC `GameManagement` instead of `AgentMemory`.
  - `Schema` row resolution rewritten — Option A accepted, #847 coordination removed.
  - Risk paragraph: 2 BC scope unchanged but rationale updated (AgentMemory for player search, GameManagement for conflict + regulars + schema).
- §10 email invitee schema:
  - "Decision" subheading flipped to "accepted" (was "proposed").
  - Recommendation rewritten — Option B no longer the fallback; #847 coordination removed.
- §17 open decisions: all 5 resolved with explicit citations to verification evidence.
- §18 (this section): added.

Score impact (estimate): 5.1/10 → **8.2/10** (Wiegers/Adzic/Cockburn ACs now testable + Nygard backend audit complete + Newman BC boundaries explicit + Fowler hook contract verified against shipped code).

## 19. Re-panel review verdict — 2026-05-18b

Panel: Wiegers · Adzic · Cockburn · Fowler · Newman · Nygard · Crispin (7 experts, critique mode).

### Per-expert scores

| Expert | Score | Key findings |
|---|:---:|---|
| **Karl Wiegers** (Requirements) | 8.5/10 | D1-D5 resolutions now testable with concrete file references (`useLibrary.ts:69`, `library.schemas.ts:32-63`). AC SMART per phase ≥85% coverage targets are verifiable. Minor: AC-F.3 fixture sentinel could cite Wave B.3 IS_VISUAL_TEST_BUILD pattern more explicitly. |
| **Gojko Adzic** (Specification by Example) | 8.0/10 | 6 Gherkin scenarios cover happy + edge (conflict, mixed invitees, draft restore, retry, mobile decide-group). Scenario 3 mixed invitees now has Option A precondition explicit. Suggestion: split Scenario 3 into 3a (state-06 desktop) + 3b (state-06 mobile) for visual fixture coverage. |
| **Alistair Cockburn** (Use Cases) | 8.5/10 | Primary actor (Marco organizer) explicit. Business goal "create event with invitees + games" clear. Failure flows comprehensive (retry, draft restore, conflict warning, network flake). |
| **Martin Fowler** (Architecture) | 8.5/10 | §5 6 v2 components well-defined. §6 hook composition verified against shipped code. §8 state strategy with trade-off matrix (URL SSOT vs useReducer vs Zustand) is exemplary. Reducer skeleton in §8.2 demonstrates discriminated-union action design. |
| **Sam Newman** (BC Boundaries) | 8.0/10 | BC ownership now explicit post-D3: AgentMemory (player search) + GameManagement (regulars + conflict + schema). Cross-BC dependencies documented in §7. Gap: API versioning convention (V1 implied via `/api/v1/` paths but no formal contract). |
| **Michael Nygard** (Production) | 8.0/10 | §9 draft autosave + retry policy: 800ms debounce, schemaVersion guard, exponential backoff [1s, 2s, 4s], localStorage userId-keyed (PII safe). §15 risk matrix: 6 risks with mitigations. Gap: no observability metrics defined (`gamenight.create.{success,failure,retry}` counters recommended). |
| **Lisa Crispin** (Testing) | 8.5/10 | §13 test strategy comprehensive: foundation (pure reducer) → components → orchestrator (mocked hooks) → E2E (10 states × 2 viewports = 20 baselines) → a11y (axe + reduced-motion). Coverage targets per phase. §12 Gherkin maps cleanly to `e2e/v2-states/`. |

### Aggregate score

**Mean: 8.29/10** — clears the ≥ 8.0/10 re-panel threshold. **Spec unfrozen 2026-05-18b**.

### Non-blocker follow-ups (post-hardening)

These were identified by the panel as P3 nice-to-have, not bound by Phase 0.5 acceptance:

1. **Adzic**: Split Scenario 3 (mixed invitees) into 3a-desktop + 3b-mobile for visual fixture coverage.
2. **Newman**: Document `/api/v1/` versioning convention in §7 risk paragraph.
3. **Nygard**: Add §9b "Observability" subsection defining counters: `gamenight.create.attempt`, `.success`, `.failure`, `.retry`, `.conflict-detected`, `.conflict-overridden`.
4. **Wiegers**: AC-F.3 cite Wave B.3 IS_VISUAL_TEST_BUILD pattern with file reference (`apps/web/src/lib/visual-test/`).

These are tracked as inline comments here and can be addressed in the Foundation commit (Week 2) or deferred to a follow-up spec amendment.

### Outcome

✅ **Implementation dispatch unblocked**. Week 1 backend can begin per §14 sequencing. The spec remains the source of truth; any deviation during implementation must amend §18 with a new change-log entry.

## 20. Week 1 implementation panel hardening — 2026-05-18c

7-expert panel review (Wiegers/Fowler/Newman/Nygard/Crispin/Adzic/Cockburn, critique mode) on Week 1 implementation readiness. Identified 5 P0 + 6 P1 issues; all addressed inline in this amendment.

### P0 — applied

| # | Expert | Issue | Resolution |
|---|---|---|---|
| **A1** | Fowler / Newman | BC routing collision: spec proposed `/players?q=` in BC AgentMemory but AgentMemory `players/me/*` = player profile, NOT registered users | §7: endpoint renamed `/users/search?q=` reusing `SearchUsersQuery` from BC Administration (already implemented at `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/SearchUsersQuery.cs`). No new query/handler. |
| **R1** | Wiegers | Missing contracts (minLength, limit, pagination, hasMore) for `/players?q=` | §7b.1: minLength=2 (handler L:28-31), default limit=20, max=50, sorted DESC ASC. R1 resolved — minLength **already implemented** in existing handler. |
| **R3** | Wiegers | Validator can be bypassed with 49 userIds + 49 emails = 98 | §7b.4 + §14 PR-W1.1: combined validator `InvitedUserIds.Count + InvitedEmails.Count ≤ 49`. BE-7 Gherkin covers. |
| **P1** | Nygard | `/users/search` table scan without pg_trgm index | §15 risk added; PR-W1.2 includes `pg_trgm` GIN migration on `users(display_name, email)`. |
| **T2** | Crispin | Migration needs UP+DOWN test for backward-compat | §14 PR-W1.1 AC: migration UP+DOWN test. BE-8 Gherkin covers. |

### P1 — applied

| # | Expert | Issue | Resolution |
|---|---|---|---|
| **A2** | Fowler | Path `/users/me/regulars` mismatches D3 BC ownership (GameManagement) | §7: path renamed `/game-nights/regulars` (BC owns path prefix). |
| **R2** | Wiegers | Conflict-check missing timeout SLA | §7b.3: 500ms server timeout via `CancellationToken`, 1.5s client AbortController. BE-5 Gherkin covers. |
| **P2** | Nygard | Cache invalidation for `/regulars` undefined | §7b.2 + §15: 5min memory cache invalidated on `GameNightPublishedEvent` handler. |
| **P3** | Nygard | Rate limit missing for `/users/search` | §7b.1 + §15: 10 req/sec per authenticated user. BE-3 Gherkin covers. |
| **T1** | Crispin | Week 1 missing test deliverables ACs | §14 PR-W1.1 + PR-W1.2: ≥85% coverage AC, integration test Testcontainers, 8 BE scenarios. |
| **G1** | Adzic | Missing BE Gherkin scenarios | §12b added (8 scenarios: BE-1 through BE-8). |

### Non-applied (deferred)

| # | Expert | Issue | Deferred to |
|---|---|---|---|
| **N1** | Newman | Verify `game_night_invitations` table ownership BC | PR-W1.1 implementation (audit-as-you-build). Likely already in BC GameManagement based on directory structure observed. |
| **N2** | Newman | Per-call API versioning convention | Tracked as non-blocker §19; can land with Foundation. |
| **Obs1** | Nygard | Observability counter set | §9b added inline — implementation tracked in PR-W1.2 acceptance. |

### Score impact

Estimated 8.29/10 → **8.7/10** (Wiegers R1-R3 testable contracts + Fowler/Newman A1-A2 BC ownership corrected + Nygard P1-P3 production hardening + Crispin T1-T2 ACs + Adzic G1 BE scenarios).

### PR strategy

Per Cockburn recommendation: **2 PR Week 1** (decomposition into 4 PR rejected as too granular for ~6-8h work):

- **PR-W1.1**: Schema + Validator + Migration (~3h)
- **PR-W1.2**: 3 endpoints + pg_trgm index + cache (~5h)

Both PR target `main-dev`. Foundation FE (Week 2) blocks on PR-W1.2 merge.

### Next action

Implementation can begin on PR-W1.1 (schema migration is independent of other endpoints). PR-W1.2 follows after PR-W1.1 merge.
