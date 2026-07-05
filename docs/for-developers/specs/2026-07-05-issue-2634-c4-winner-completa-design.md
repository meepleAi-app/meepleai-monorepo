# C4 — Winner display + per-game "Completa" sul night-live hub (#2634)

**Data**: 2026-07-05 · **Issue**: #2634 (SI-3, umbrella #2619) · **Predecessori**: WS1 (PR #2665), C2 (PR #2669)
**Stato**: DRAFT per `/sc:spec-panel` · **Scope scelto dall'utente**: *Winner + Completa (BE shipped)* — Abbandona/Archivia rinviati a SI-3-proper.

## 1. Contesto e discovery (workflow 4-reader)

WS1 ha riabilitato l'avvio live (`Avvia prossimo gioco`); C2 ha renderizzato il diary. Manca il **completamento** di una partita con **winner**, e la chip winner (`PlannedGamesPane`, `isCompleted && game.winner`) è **dead code** perché il mapper non popola mai `PlannedGame.winner`.

### Fatti accertati
**Close flow (BE):**
- **Completa** (per-partita, con winner opzionale) È SHIPPED: `CompleteGameNightSessionCommand(GameNightId, Guid? WinnerId, UserId)` → `gameNight.CompleteCurrentSession(WinnerId)` → `GameNightSession.Complete(winnerId)` setta `WinnerId` + `CompletedAt` (InProgress→Completed **sulla sessione**, la serata resta InProgress). Route `POST /api/v1/game-nights/{id}/sessions/complete` (204, organizer-guarded, 409 se nessuna live). Body `CompleteGameNightSessionRequest(Guid? WinnerId)`.
- **Abbandona/Archivia**: nessun concetto BE (fuori scope, → SI-3-proper).
- ⚠️ *Landmine documentato*: due night-close path contraddittori (`FinalizeGameNightCommand` Published-only vs `CompleteGameNightCommand` EF-level InProgress-only). **Non li tocchiamo** — C4 usa solo il per-session `CompleteGameNightSessionCommand`; il night-finalize è SI-3-proper.

**Winner semantics (D1 risolta):**
- `GameNightSession.WinnerId` è un `Guid?` **opaco, non validato, non risolto a nome**.
- Il tracking Session (`GameNightSession.SessionId == Session.Id`) ha una convenzione winner consolidata: `Session.Finalize(winnerId)` valida che `winnerId` sia un **`Participant.Id`** → `SetFinalRank(1)`; il nome = `Participant.DisplayName` (**guest-capable**, `UserId` nullable, `DisplayName` per-row).
- **Decisione D1 (pinned)**: `GameNightSession.WinnerId` È un **`Participant.Id`** del tracking Session correlato. Il BE risolve il nome via i Participants di quel Session.

**Roster (guest-capable) — già esiste:**
- `Participant` (owned by tracking Session): `Id, SessionId, UserId (Guid? = guest), DisplayName (MaxLen 50), IsOwner, FinalRank`.
- Query esistenti: `GetSessionDetailsQuery` → `SessionDetailsDto.Participants` (`ParticipantDto{Id, UserId, DisplayName, IsOwner, JoinOrder, FinalRank, TotalScore}`), route `GET /api/v1/game-sessions/{sessionId}`. Guest inclusi.

**FE:**
- `PlannedGameWinner{name, initials, color:number}` — `color` = hue 0-359 (`hsl(color,60%,55%)`). Chip a `PlannedGamesPane.tsx:241-264`.
- Mapper `mapNightLiveToViewModel(dto, now)` — nessun input roster; `GameNightSessionDto` non ha nome winner (`winnerId:uuid?` solo).
- Util: `userHue(id):number` (FNV-1a, funziona su qualsiasi id incl. Participant.Id), `userHsl`. `extractInitials` è game-title-tuned → serve un helper **person-initials**.
- Nessun 3-way selector; solo `gameNightSessionClient.completeSession(gameNightId, winnerId?)` (non wired al read-model view). La `Completa` fa coppia con la CTA WS1 `Avvia prossimo gioco` (game loop: live→Completa→transition→Avvia prossimo).

## 2. Decisioni proposte (per il panel)

| # | Decisione | Opzione raccomandata | Alternative |
|---|-----------|---------------------|-------------|
| **D1 WinnerId** | cosa È WinnerId | **`Participant.Id`** del tracking Session (guest-capable, convenzione esistente). Test che pinna la semantica. | UserId (perde i guest) |
| **D2 Name resolution** | dove risolvere il nome | **BE**: `GetGameNightLiveQueryHandler` risolve `WinnerId→DisplayName` via i Participants (batch, il handler ha già `MeepleAiDbContext`), aggiunge `WinnerName` a `GameNightSessionDto`. | FE fetch roster (più round-trip, id-space sul FE) |
| **D3 Completa command** | quale path | **Riusa `CompleteGameNightSessionCommand`** shipped (per-session, non tocca il night-finalize landmine). | nuovo comando |
| **D4 Winner picker source** | roster per il picker | **Riusa `GET /game-sessions/{sessionId}`** (`SessionDetailsDto.Participants`, guest-capable) per la sessione live — zero nuovo endpoint. | nuovo endpoint roster |
| **D5 Chip color/initials** | come costruire `winner` | `color = userHue(winnerId)` (hash del Participant.Id, deterministico anche per guest); `initials = personInitials(winnerName)` (nuovo helper, non `extractInitials`); `name = session.winnerName`. | — |
| **D6 Completa UX** | dove/come | Azione organizer-only nel `NightLiveClientView`, sulla partita live (`status==='live'`); apre un picker winner (opzionale "nessun winner") → `completeSession(winnerId?)` → invalida live+diary key. Complementare alla CTA WS1. | inline senza picker (no winner) |
| **D7 No-winner** | Completa senza winner | Consentito (BE `WinnerId` nullable) → partita completata senza chip winner. | winner obbligatorio |

## 3. Scope (C4, questa slice)
**IN**: (a) BE `WinnerName` su `GameNightSessionDto` + risoluzione batch via Participants (guest-capable) + test che pinna `WinnerId=Participant.Id`; (b) FE `completeSession` wired (hook RQ) + winner picker (roster da `GET /game-sessions/{sessionId}`) + azione organizer "Completa" nel view; (c) mapper popola `PlannedGame.winner` da `winnerId`+`winnerName` (`personInitials` + `userHue`). **OUT** (→ SI-3-proper): Abbandona, Archivia/resumable, night-level in-progress→completed transition, riconciliazione dei 2 finalize path, actor-avatar diary (D5-full).

## 4. Acceptance criteria
- **AC1** Organizer completa la partita live con un winner (Participant) → la chip mostra `🏆 {DisplayName} ha vinto` con avatar (initials + hue deterministico), incl. **winner guest** (no UserId).
- **AC2** Completa senza winner → partita completed, nessuna chip winner, no crash.
- **AC3** Dopo Completa, la partita passa a `completed` e la serata torna `transition` (nessuna live) → la CTA WS1 "Avvia prossimo gioco" ridiventa disponibile.
- **AC4** Non-organizer non vede l'azione Completa; il BE 403 è il backstop.
- **AC5** Completa quando nessuna partita è live → 409 gestito (no crash, feedback).
- **AC6** `WinnerName` risolto correttamente per un `Participant.Id` valido; `null`/sconosciuto → nessun nome (no crash).
- **AC7** Nessuna regressione su WS1 (avvio) / C2 (diary) / C1 (currentGame).

## 5. Test plan
- **BE unit**: `GetGameNightLiveQueryHandler` risolve `WinnerName` (Participant match, guest, WinnerId null, WinnerId sconosciuto→null); pin test `WinnerId=Participant.Id`.
- **BE integration** (Testcontainers): complete-with-participant-winner → live DTO espone `WinnerName`; guest winner.
- **FE unit**: `personInitials`; mapper popola `winner{name,initials,color}` (+ null-winner); `useCompleteGameNightSession` mutation (success invalida keys, 409/403 error); winner-picker component; view azione Completa (organizer-gated, live-only).
- **Regressione**: cluster game-nights FE + BE GameNight.

## 6. Rischi / questioni aperte
- **R1** Il picker roster viene dal tracking Session (`GET /game-sessions/{sessionId}`): verificare che sia participant-guarded e che il `sessionId` (GameNightSession.SessionId) risolva i Participants attesi.
- **R2** `personInitials` su nomi con 1 parola / emoji / vuoti — fallback robusto.
- **R3** Landmine dei 2 finalize path: **non toccato** in C4, ma da segnalare per SI-3-proper.

---
*Draft per spec-panel — D1-D7 da pressure-testare.*


## 7. Spec-Panel Verdict (Nygard/Wiegers/Crispin; Fowler retry-capped)

# Spec-Panel Verdict — C4 Design (#2634)

**Panel:** Nygard (failure-modes) · Wiegers (requirements) · Crispin (test strategy). Convergence is high: the happy path and id-space premise are sound, but the draft rests on three code-contradicted assumptions (roster endpoint is unguarded; WinnerId write is unvalidated; per-session complete never finalizes the tracking Session). Two of these puncture the headline AC. None block the slice, but each needs an explicit decision before coding.

## 1. LOCKED-DECISIONS (D1–D7)

| # | Decision | Verdict | One-line reason |
|---|----------|---------|-----------------|
| **D1** | WinnerId IS a Participant.Id of the correlated tracking Session | **REFINED** | Semantic is correct and guest-capable, but it is an *unenforced* convention — `Complete(winnerId)` stores the Guid opaquely and the validator checks only GameNightId/UserId. A read-side pin-test documents intent; it does not make the field non-opaque. Must be enforced on write OR paired with a fail-closed read (see §2). Drop the `FinalRank==1` framing — Completa never finalizes the tracking Session. |
| **D2** | Resolve WinnerId→DisplayName in BE (GetGameNightLiveQueryHandler) | **REFINED** | Right layer (handler already participant-guarded + has DbContext). MUST scope the lookup by **(SessionId, WinnerId)**, not Participant.Id alone, so a stray/cross-session id fails closed to null instead of resolving a plausible wrong name. One batched query over all completed sessions. Explicitly label as a deliberate GameManagement→SessionTracking cross-BC read. |
| **D3** | Reuse shipped per-session CompleteGameNightSessionCommand | **AGREE (w/ mandatory note)** | Strongest decision — avoids the two contradictory night-finalize commands; 409/403 backstops already exist. Note is load-bearing: this command completes the sub-aggregate and never calls `Session.Finalize`, leaving the tracking Session live/un-finalized (see §2.5). |
| **D4** | Source picker roster from GET /game-sessions/{sessionId} | **OVERTURNED** | Rationale is factually wrong: endpoint is `.RequireAuthenticatedUser()` only, handler filters on `s.Id && !IsDeleted` — a pre-existing IDOR, not participant-guarded. Re-source the roster from the **already-guarded night-live read model** (extend GameNightLiveDto with in-progress Participant.Id + DisplayName), unifying the id-space with D2 and removing the unguarded round-trip. |
| **D5** | Chip: color=userHue(winnerId), initials=personInitials(winnerName) | **AGREE (w/ refinement)** | Pure and guest-capable (Participant.Id is a GUID PK → deterministic hue for guests). Confirm the mapper emits color as a **hue number 0–359** (chip does `hsl(${color},…)`), not an hsl string. Pin `personInitials` with codepoint-safe + empty-fallback tests. Per-session hue means the same guest changes color per game — cosmetic, non-blocking. |
| **D6** | Organizer-only, live-only "Completa" with winner picker | **REFINED** | FE gate + BE 403 backstop is the right shape. MUST pending-lock while in flight (Complete handler does not catch `DbUpdateConcurrencyException` → double-submit returns 500) and render 409 (no live) vs 403 (non-organizer) as distinct feedback. Reconcile with the **existing** `useGameNightMultiSession.completeSession` write path — do not ship two divergent mutation/cache strategies. |
| **D7** | Allow Completa with no winner (WinnerId nullable) | **AGREE** | Nullable end-to-end (command → aggregate → DTO). Add one assertion: mapper does not synthesize a winner object when winnerName is null (chip suppressed, no crash). |

## 2. NEW MUST-FIX ITEMS THE DRAFT MISSED (ranked)

1. **[BLOCKER] Re-source the picker roster off the guarded read model.** The draft asserts GET /game-sessions/{sessionId} is participant-guarded; it is not (IDOR exposing any session's DisplayNames/scores/notes to any authenticated user). Extend GameNightLiveDto with the in-progress session's Participant.Id + DisplayName, or add userId+membership scoping to the query. Do not build on an endpoint mislabeled as guarded.
2. **[BLOCKER] Make AC1 (guest winner) testable against the real pipeline.** WS1's start path posts no participants, so `BuildParticipantsAsync` auto-seeds **organizer-only**. For any WS1-started game the roster is `{organizer}` and the guest-winner AC is unreachable except via a hand-seeded fixture — the exact "acceptance-tests-must-exercise-the-real-pipeline" failure mode. Decide: scope AC1 down to organizer-as-winner for the WS1 flow (guest-winner deferred + tracking issue), OR pull RSVP roster seeding into scope (out of C4 → tracking issue). No AC may be demonstrable only via a hand-seeded fixture.
3. **[BLOCKER] Enforce WinnerId integrity, or fail closed + document.** Mirror `Session.Finalize`'s `_participants.Any(p=>p.Id==winnerId)` guard on the complete path, throwing **ConflictException (409) / ValidationException (400) — never ArgumentException/500**. If not enforced, the D2 read MUST scope by SessionId and null out non-members, AND AC6 must explicitly cover "WinnerId set but not a current participant → no name, no crash" as accepted, tracked risk.
4. **[HIGH] Map xmin concurrency loss to 409 in CompleteGameNightSessionCommandHandler.** The handler catches only `InvalidOperationException`; a stale-xmin double-complete throws `DbUpdateConcurrencyException` → uncaught → 500, violating never-500 (ADR-060). The Start handler already handles this; the Complete handler does not. Catch → ConflictException + FE pending-lock.
5. **[HIGH] Document the session-level "un-finalized tracking Session / dual winner source" landmine.** Completa completes the sub-aggregate but never `Finalize()`s the tracking Session: it stays `IsLive=true`, `FinalRank` unset, `SessionFinalizedEvent` consumers (KB index, play records, summary) never fire, `GetActiveSessionQuery` may keep surfacing the "completed" game as active, and GameNightSession.WinnerId diverges from the tracking-Session winner. The draft's OUT-of-scope note covers the *night*-finalize commands, not this *session*-level gap. Record as SI-3-proper tracking issue + AC tolerating a null/divergent winner.
6. **[MEDIUM] Add WinnerName to the wire contract with a round-trip test.** New field needs the C# GameNightSessionDto change AND the Zod `GameNightSessionDtoSchema` (project "Zod at the wire" rule) + a schema round-trip test — omitted from the test plan.
7. **[MEDIUM] Pin personInitials edge cases.** Emoji/surrogate-safe (spread / `Intl.Segmenter` / `codePointAt` — never `charAt(0)`, which splits surrogate pairs; guest DisplayName MaxLen 50 permits emoji), single-word → 1–2 letters, empty/whitespace → stable fallback glyph. Add a test that a Skipped/Corrupted row never renders a winner chip.
8. **[LOW/UX] Surface the last-game dead-end.** Completing the final planned game yields `status='transition'`, `nextGame=null`, and no night-finalize CTA in C4 (night-finalize is out of scope) — the organizer has no affordance to end the night. Flag as a UX risk even if resolution is deferred.

## 3. GENUINE EXPERT DISAGREEMENT

**Is write-side WinnerId validation mandatory in C4, or optional-with-mitigation?**
- **Nygard (mandatory):** C4 is the *first consumer* to assign meaning to WinnerId, therefore C4 is where the invariant must become enforced — "not merely documented." A read-side pin-test alone leaves a data-integrity error that AC6 codifies as acceptable, masking rather than rejecting it.
- **Wiegers / Crispin (enforce-OR-accept):** Enforcing is preferred, but an explicitly-documented unvalidated write **plus** a session-scoped fail-closed read is an acceptable ship, provided the accepted risk is tracked and AC6 spells out the null-resolution behavior. The key demand is *an explicit decision*, not necessarily enforcement.

**Facilitator ruling:** Enforce on write. The cost is one participant existence-check the handler can run against the same roster D2/D4 already load, it converts D1 from convention to invariant, and it removes the need for defensive-read scaffolding downstream. Fail-closed read is retained anyway as defense-in-depth (D2 scoping), so we get both. This is the lower-regret path and closes the AC6 "masks a wrong write" objection.

Secondary (not a true two-sided split): Wiegers questions whether a full picker modal is even justified while the WS1 roster is organizer-only ("justify or defer until roster is populated"). Resolved by must-fix #2 — once AC1 scope is decided, the picker's shape follows.

## 4. RECOMMENDED BUILD ORDER

1. **Roster + AC1 scope decision first (must-fix #1, #2).** Extend GameNightLiveDto to carry the in-progress session's Participant.Id + DisplayName; lock the AC1 scope (organizer-winner now, guest-winner tracked). This unblocks D2/D4 and fixes the id-space.
2. **BE WinnerId write guard (#3).** Add participant-membership validation in CompleteGameNightSessionCommandHandler → ConflictException/ValidationException.
3. **BE concurrency mapping (#4).** Catch `DbUpdateConcurrencyException` → ConflictException in the Complete handler.
4. **BE name resolution (D2, #5-scoping).** Single batched query scoped by (SessionId, WinnerId); add nullable WinnerName to GameNightSessionDto; label the cross-BC read.
5. **Wire contract (#6).** Zod schema + round-trip test.
6. **FE mutation (D6).** Reconcile into a single completeSession source of truth (extend `useGameNightMultiSession.completeSession`); pending-lock; distinct 409/403 feedback.
7. **FE chip mapper (D5, #7).** personInitials codepoint-safe + fallback; color as hue number; Skipped/Corrupted-no-chip test.
8. **Docs/tracking (#5, #8).** SI-3-proper tracking issue for the un-finalized tracking Session + AC for null/divergent winner; note last-game dead-end.

**Gate:** Steps 1–3 are the blockers — the slice must not enter review until the roster is re-sourced off a guarded surface, AC1 is testable through the real pipeline, and no path on the Completa flow can return 500.
## 8. Decisioni utente (2026-07-05, lockate)
- **AC1/guest-winner** → **Includi RSVP seeding in C4**: lo start flow seeda il roster dai partecipanti (accepted RSVP) invece del solo organizer → winner multi-giocatore reale. Guest-capable per design; il dato guest dipende dal meccanismo guest-player (se assente, tracked).
- **Tracking Session** → **Finalizza anche il tracking Session**: Completa dispatcha `Session.Finalize(winnerId)` cross-BC → (a) write-validation del WinnerId GRATIS (guard esistente `Session.Finalize`), (b) no orphan/active-session, (c) winner canonico `FinalRank==1` + eventi (play records/summary). Transazione atomica.
- Adottate le altre lockate del §7: D4→roster sul read model live guardato (no IDOR endpoint), D2 scoped by (SessionId,WinnerId) fail-closed, catch `DbUpdateConcurrencyException`→409 + pending-lock FE, `personInitials` codepoint-safe, `WinnerName`+roster su DTO con Zod round-trip, D5/D7.

**Build order**: (1) RSVP roster seeding nello start flow · (2) Completa+`Session.Finalize` cross-BC atomico + concurrency→409 · (3) `GameNightLiveDto` +`WinnerName` (scoped) +roster in-progress · (4) Zod wire + round-trip · (5) FE mutation completeSession (pending-lock, 409/403 distinti) · (6) FE winner picker + azione Completa · (7) FE mapper chip (`personInitials` + `userHue`) · (8) docs/tracking.
