# SI-4 — startedAt chip derivato + resume wiring (#2635)

**Data**: 2026-07-05 · **Issue**: #2635 (SI-4, umbrella #2619) · **Predecessori**: SI-1b (#2632), WS1/C2/C4 (#2633/#2634)
**Stato**: DRAFT per `/sc:spec-panel` · **Scope scelto utente**: *Chip + resume-full (BE net-new)*.

## 1. Contesto e discovery (workflow 3-reader; agent BE re-open ricostruito inline)

Acceptance #2635: *Given a live Session, When the play UI renders, Then mostra una chip read-only `▶ Ora di inizio {startedAt} · derivata` (no time-picker/durata — **Invariante 5**); e "▶ Riprendi" apre una **NUOVA** live Session (fresh `startedAt`), GameNight ri-promossa planned/**completed**→in-progress, campaign `createdAt` invariato (**D3**, no draft reactivation).*

### Fatti accertati
**startedAt (sorgente):**
- `Session.StartedAt` (SessionTracking, `DateTime?`) settato SOLO in `Session.OpenLiveMode()` (Session.cs:391) → raises `SessionStartedDomainEvent`. `IsLive => StartedAt.HasValue && FinalizedAt is null`.
- `GameNightSession.StartedAt` (GameManagement, `DateTimeOffset?`) settato da `Start()` (Pending→InProgress). Esposto da `GameNightSessionDto.StartedAt` (night-live read).
- **`SessionLiveView` (`/sessions/[id]/live`) consuma già `sessionQuery.data.startedAt`** (SessionLiveView.tsx:740, `useElapsedTime`) → è la "play UI" con header, home naturale della chip (Invariante 5 "header drawer": "Iniziata alle HH:MM" read-only).
- **`GamebookCampaignSpineDto` NON espone startedAt** (solo `HasLiveSession` bool + counts) — gap per la chip sul resume-picker gamebook.
- **Invariante 5** (domain spec 2026-06-04:149-159): `Session.startedAt` NON è user input, derived da "Apri Live mode". UI: ❌ time-picker "Ora di inizio" rimosso, ❌ "Durata" input rimosso, ✅ display read-only header, ✅ "Note" unico input.

**Resume (path BE):**
- Il "▶ Riprendi" del resume-picker gamebook (`library/[gameId]/play`, `ResumeHero`/`MultiCampaignList`/`StaleWarningCard`) è oggi **pura navigazione** (`<Link>` → `/library/{gameId}/play/{campaignId}`, no mutation).
- Aprire una nuova live Session per una campagna = path Attach SI-1b `POST /game-nights/{id}/gamebook-sessions` (`AttachGamebookCampaignToGameNightCommand`), ri-chiamabile (`EnsureCanStartSession()` max-1-live guard) se nessuna live + night non Completed.
- **La re-promozione GameNight è wired**: `SessionStartedHandler` (GameManagement EventHandler) consuma `SessionStartedDomainEvent` → `HandleFirstSessionStarted`.
- ⚠️ **GAP #15**: `HandleFirstSessionStarted` accetta solo **Published→InProgress** (o InProgress idempotente); **Completed lancia** `InvalidOperationException` ("Invariant #15 requires Published or InProgress"). D3 vuole **completed→in-progress** → **BE net-new**.
- ⚠️ **Side-effect stale**: il night raggiunge Completed via `FinalizeNight()` → `NightFinalizedEvent` (summary ecc.). Re-promuovere Completed→InProgress NON annulla quell'evento → **summary/side-effect stale** di una serata ora di nuovo in-progress.

## 2. Decisioni proposte (per il panel)

| # | Decisione | Opzione raccomandata | Alternative |
|---|-----------|---------------------|-------------|
| **D1 Chip surface** | dove la chip | **`SessionLiveView`** header (ha già startedAt, Invariante 5 header drawer) + **anche** sul resume-picker gamebook (serve startedAt sullo spine DTO) | solo SessionLiveView |
| **D2 Chip copy/format** | testo | `▶ Ora di inizio {HH:MM} · derivata` read-only (assoluto, non elapsed); UTC-pinned deterministico o TZ locale? | relativo "Iniziata Xgg fa" (precedente mockup) |
| **D3 Invariante 5 enforce** | rimuovere input | Verificare che NESSUN time-picker "Ora di inizio"/"Durata" input esista sulla session/live surface (rimuovere se presente) | — |
| **D4 Resume action** | Riprendi | Wire "▶ Riprendi" a una NUOVA live Session via Attach path (`POST /gamebook-sessions`), non più pura navigazione | mutation dedicata nuova |
| **D5 Re-promozione Completed** | BE #15 | **`HandleFirstSessionStarted` accetta Completed→InProgress** (net-new) — la serata completata torna in-progress su nuova live Session | negare (resume solo Published/InProgress) |
| **D6 Side-effect stale** | NightFinalizedEvent | **Da risolvere**: re-promuovere lascia summary/side-effect stale. Opzioni: (a) idempotente/rigenerato al prossimo finalize; (b) evento di "riapertura" che invalida il summary; (c) accettare come debito tracciato | — |
| **D7 startedAt sullo spine** | gamebook chip | Aggiungere `sessionStartedAt` (o simile) a `GamebookCampaignSpineDto` (dalla sitting live) per la chip sul resume-picker | chip solo su SessionLiveView |

## 3. Scope (SI-4, questa slice)
**IN**: (a) chip read-only startedAt su SessionLiveView (+ resume-picker gamebook) + enforce Invariante 5; (b) wire "▶ Riprendi" → nuova live Session (Attach path); (c) BE `HandleFirstSessionStarted` Completed→InProgress + gestione side-effect stale [D6]; (d) `startedAt` sullo spine DTO. **OUT**: parallel play, edit manuale di startedAt (vietato da Inv.5).

## 4. Acceptance criteria (rev. post spec-panel — §7 item 9)
- **AC1** Una live Session mostra la chip read-only `▶ Ora di inizio {data ora locale} · derivata` (no time-picker, no durata input) su SessionLiveView. ✅
- **AC2** Nessun input editabile per startedAt/durata sulla session surface (Invariante 5) — assert-absent + regression guard. ✅
- **AC3** (riscritta) Riprendere una serata già **InProgress** (2ª+ sitting) apre una NUOVA live Session; il GameNight resta InProgress con la nuova live child E **rimane finalizzabile** dopo (nessun live child orfano). `createdAt` della campaign invariato (D3). ✅
- **AC4** (riscritta) Resume-from-**Completed** è rifiutato al **command boundary** con 409 ("start a new session"), MAI un 500 dal post-commit handler (`HandleFirstSessionStarted` resta strict Published/InProgress). ✅
- **AC5** max-1-live rispettato sul resume: 409 (`MAX_LIVE_SESSIONS_EXCEEDED`) se una sitting è già live, su entrambe le chain (Start + Attach); nessuna Session orfana. ✅
- **AC6** Nessuna regressione su WS1/C2/C4 (avvio/diary/winner) né sul night-live. ✅

## 5. Test plan
- **BE unit/integration**: `HandleFirstSessionStarted` Completed→InProgress (nuovo) + idempotenza; Attach re-callable per resume; side-effect stale [D6]; spine DTO startedAt.
- **FE unit**: chip startedAt (format, Invariante-5 no-input) su SessionLiveView; resume action (Attach mutation, 409 max-live); resume-picker chip.
- **Regressione**: cluster game-nights + sessions-live.

## 6. Rischi / questioni aperte
- **R1 [D6]** Re-promozione Completed→InProgress lascia `NightFinalizedEvent` side-effect stale (summary) — la scelta di gestione è la questione più delicata (tocca invariante #15).
- **R2** La chip su DUE superfici (SessionLiveView + resume-picker) raddoppia il lavoro; valutare se una basta.
- **R3** Copy/format della chip non nei mockup (nuova) — assoluto vs relativo, TZ.

---
*Draft per spec-panel — D1-D7 da pressure-testare; R1/D6 è il nodo critico.*


## 7. Spec-Panel Verdict (Fowler/Nygard/Crispin; Wiegers retry-capped) — RESUME-FULL OVERTURNED

# SI-4 (#2635) — Spec-Panel Consolidated Verdict

**Panel**: Fowler (modeling) · Nygard (resilience) · Crispin (test/edge). **Consensus is unusually strong**: the FE chip half is sound; the resume half rests on a factually wrong discovery, and the true blocker sits one call earlier than the draft targets.

## 1. Locked Decisions (D1–D7)

| # | Decision | Verdict | One-line reason |
|---|----------|---------|-----------------|
| **D1** | Chip on SessionLiveView **+** resume-picker | **OVERTURNED → one surface** | 3/3 challenge. Resume-picker is not live (`CampaignStatus='Resumable'`); spine would surface the last *Completed* sitting's `StartedAt` mislabeled as current. Data on SessionLiveView is already in hand (`SessionLiveView.tsx:740`). |
| **D2** | Absolute `Ora di inizio {HH:MM} · derivata` | **REFINED** | Absolute + read-only + "derivata" is correct for Invariante 5, but render **date+time in the viewer's local TZ** from the UTC instant via a pure FE mapper; bare `HH:MM` is ambiguous for a multi-day campaign. |
| **D3** | Enforce Invariante 5 (remove time/duration input) | **REFINED (agree in principle)** | Nothing to remove — no editable `startedAt`/duration input exists on the live surface today. Reframe as **assert-absent + regression guard**, not removal work. |
| **D4** | Wire Riprendi via SI-1b Attach path | **OVERTURNED (as written)** | Attach is organizer-scoped (Forbidden if caller≠OrganizerId) **and** calls `AddSession` which requires `Status==Published`. It cannot serve a completed/solo-owner resume as-is. Define a real resume command first. |
| **D5** | `HandleFirstSessionStarted` accepts Completed→InProgress | **OVERTURNED** | Necessary-but-insufficient (dead code — `AddSession:414` 409s first) **and** unsafe (silently un-completes a terminal state from a passive post-commit handler → AC4 500 + crash-window). |
| **D6** | Handle stale `NightFinalizedEvent` summary side-effect | **OVERTURNED (phantom)** | `NightFinalizedEvent` has **no** `INotificationHandler`; sole consumer is `SseEventTypeMapper.cs:84` (ephemeral). No persisted summary/recap/email/ledger exists. The "critical node" evaporates in current code. |
| **D7** | Add `startedAt` to `GamebookCampaignSpineDto` | **OVERTURNED (drop)** | Follows from D1. Couples the GameManagement read model to SessionTracking timing for a cosmetic chip and ships a misleading "start time" (a past sitting's start on a non-live campaign). YAGNI. |

## 2. New Must-Fix Items (ranked)

1. **Fix the discovery — `AddSession` is the true blocker, not `HandleFirstSessionStarted`.** `GameNightEvent.cs:414` requires `Status==Published` and throws first (→409). Relaxing D5 alone is dead code. This also blocks the **common InProgress 2nd-sitting** case (Crispin), not just Completed.
2. **Delete D6's stale-summary machinery.** Restate the discovery: only durable finalize side-effect is `_autoSaveScheduler.RemoveAsync` (self-healing, re-registered by Attach). No invalidation event needed.
3. **Resolve D5 on domain grounds at the command boundary, never by widening a guard.** Any allow/deny for a completed night must return 409/400 **before** `CreateSession` — a throw in the post-commit `SessionStartedHandler` (ADR-060) is a 500 over already-committed live state (AC4 violation).
4. **Verify Completed is even reachable for played gamebook nights.** `FinalizeNight` requires `Status==Published` (line 491) and rejects InProgress; a night with a started session may never reach Completed via the normal door. Retarget D5 to the **reachable InProgress 2nd-sitting** path before coding.
5. **Define the real resume command for D4**: explicit auth scope (owner vs organizer) and the completed-night decision location. Attach is not a drop-in.
6. **If same-night reopen is chosen, make it atomic**: fold the status transition into the **same transaction** as `AddSession`/`StartCurrentSession` — do not rely on the post-commit `OpenLiveMode` event, which leaves a crash-window (durably-Completed night owning a live InProgress child → unrecoverable).
7. **Scope chip to SessionLiveView; drop D7; lock TZ**: `DateTimeOffset` + ISO-with-offset on any wire field, pure FE local formatter, test-pinned TZ (guards against `Date.parse` local-time corruption that would also corrupt `useElapsedTime`).
8. **Expand the test plan**: double-resume → 409 (max-live), concurrent resume → xmin loser 409, crash-before-status-flip recovery, read-model churn (`Recenti` ↔ `In corso`), Invariante-5 no-input guard, chip coexistence with the existing elapsed-timer, max-5-sittings cap edge.
9. **Reframe AC4**: "no 500" is trivially satisfied by the existing 409 path and does **not** prove resume works. Add an AC asserting the night reaches InProgress with a fresh live Session **and remains finalizable** afterward.

## 3. Recommendation on the Critical D5/D6 Question

**Do NOT allow silent `Completed→InProgress` re-promotion.** Unanimous.

- **The stale-summary problem does not exist.** `NightFinalizedEvent` has no handler — remove all D6 machinery. If a reopen ever ships, emit an **additive `NightReopenedEvent`** (registered in `SseEventTypeMapper`) so the SSE/read side can react (toast, move card) — an *invalidation* event would invalidate nothing.

- **Preferred model: resume = a fresh sitting context, not a resurrected night.** The campaign already tolerates multiple nights (spine resolves per-session via `FindByLinkedSessionIdAsync`), so a new sitting/ad-hoc night is the evolutionarily-consistent choice and dissolves the terminal-state smell, the lifecycle dead-end (a re-promoted night can only re-complete via `CompleteAdHoc`, which is silent/asymmetric), and the crash-window entirely.

- **If product genuinely requires resurrecting the *same* night**, it must be a first-class, explicitly-named `Reopen()` transition with its own event and a defined re-finalize door, executed atomically inside the resume transaction — **and** every guard must be enumerated and changed together (`AddSession` Published-guard, `FinalizeNight` re-entry, max-5 cap), not one widened method.

- **Scope call — resume-from-Completed is OUT for SI-4.** Retarget to the reachable everyday gap (**InProgress 2nd-sitting resume**, currently blocked by `AddSession`'s Published-only guard). Reject a completed-night reuse at the command boundary with `409` ("night finalized — start a new session"). This is the smallest correct slice and matches the spine DTO's own comment that Completed is "a future manual flag from SI-8."

## 4. Genuine Disagreement (recorded, both sides)

**(a) Is Attach the right mechanism for D4?**
- *Fowler*: Attach is "convenient coupling, not a fit" — organizer-scoped and Published-guarded; define a purpose-built resume command instead.
- *Nygard / Crispin*: Attach's navigation→mutation shape is fine **if** `AddSession` reachability is fixed; the gap is scope, not mechanism.
- **Panel call**: fixing `AddSession` is required either way, so the mechanism choice is downstream of item #4 (verify reachability) — decide after re-discovery; do not pre-commit to Attach.

**(b) How real is the Completed source-state?**
- *Fowler / Nygard*: treat Completed as reachable and design to *reject* it (terminal-state smell is the headline).
- *Crispin*: the premise may be false — `FinalizeNight` rejects InProgress, so a played gamebook night likely can't reach Completed at all; D5 may be optimizing a non-existent edge while the InProgress path is the true break.
- **Panel call**: Crispin's reachability check (item #4) gates the others' concern — run it first; if Completed is unreachable, the entire terminal-state debate is moot for this slice.

## 5. Build Order

0. **Re-discovery pass** (BE): trace the full Attach traversal — `AddSession:414` (Published-only), `StartCurrentSession` (inv #10), `EnsureCanStartSession`, `FinalizeNight:491`. Correct the doc: D6 phantom + `AddSession` as primary blocker. Verify whether Completed is reachable for played nights.
1. **Re-scope**: target InProgress 2nd-sitting resume; declare resume-from-Completed OUT (command-boundary 409).
2. **FE chip (independent, ship early)**: SessionLiveView-only, local-TZ pure mapper (D1 narrowed + D2). Drop resume-picker chip + D7.
3. **D3 verify-and-lock**: regression test asserting no editable `startedAt`/duration input; confirm chip does not collide with the existing `useElapsedTime` surface.
4. **BE resume command**: relax `AddSession` for InProgress; define command with explicit owner/organizer auth; enforce the completed-night decision at the command boundary (never-500).
5. **Reopen path (only if product mandates same-night)**: explicit atomic `Reopen()` + additive `NightReopenedEvent` + `SseEventTypeMapper` registration; keep passive `HandleFirstSessionStarted` strict.
6. **Test expansion**: concurrency/idempotency (double-click, xmin loser), crash-before-flip recovery, read-model churn, Invariante-5 guard, TZ serialization, max-5 cap.
7. **Rewrite ACs**: AC3 = night reaches InProgress with a fresh live Session; AC4 = subsequent finalize path remains valid (no orphaned live child).

**Bottom line**: keep the chip (one surface, local TZ), drop D7, and send the resume half back for a re-discovery + re-scope pass — the "critical node" (D6) is a ghost, the real blocker (`AddSession`) is unaddressed, and re-promoting a terminal state is a smell to design out, not a guard to widen.
## 8. Re-discovery + re-scope (2026-07-05, post-verdict, scelta utente "Chip + resume InProgress-2ª-sitting")

**Re-discovery (build-order step 0)**: `GameNightEvent.AddSession` (GameNightEvent.cs:410-427) richiede `Status==Published` (:414) e lancia `InvalidOperationException` altrimenti. Il night passa a `InProgress` dopo la 1ª partita (`HandleFirstSessionStarted`, dispatch async via outbox post-commit). Quindi avviare una **2ª+ partita/sitting** su un night già InProgress **fallisce** (o è racy con la flip async). Confermato il finding del panel: il vero blocker è `AddSession`, e colpisce il caso **quotidiano InProgress 2ª-sitting**, non solo Completed. La chain resume (WS1 `StartGameNightSessionCommand` e SI-1b `AttachGamebookCampaign`) chiama entrambe `AddSession` → sono affette.

**Re-scope lockato**:
- **Chip**: SessionLiveView-only, TZ locale, mapper puro, assoluto `▶ Ora di inizio {data ora} · derivata` (D1 narrowed + D2 refined). **Drop D7** (spine chip). D3 = assert-absent + regression test.
- **Resume = InProgress 2ª-sitting**: **rilassare `AddSession` a `Published || InProgress`** (reject Draft/Cancelled/**Completed**/Corrupted con messaggio chiaro). `EnsureCanStartSession`/`StartCurrentSession` (max-1-live #10) invariati → non si creano 2 InProgress. Wire il "▶ Riprendi" gamebook a creare una nuova live Session via la chain esistente.
- **resume-from-Completed OUT**: rifiutato al **command boundary** (409 "night finalized — start a new session"), MAI un throw nel post-commit `SessionStartedHandler` (never-500, AC4). `HandleFirstSessionStarted` resta strict Published/InProgress.
- **D6 fantasma eliminato**: `NightFinalizedEvent` non ha handler; nessun summary stale da gestire. Nessun `NightReopenedEvent` (non serve — non si riapre lo stesso night).

**Build order**: (1) chip FE SessionLiveView (indipendente, ship early) + Invariante-5 regression · (2) BE relax `AddSession` a Published||InProgress + reject Completed@boundary + test (incl. multi-game 2ª-sitting che oggi si rompe) · (3) FE wire "▶ Riprendi" → nuova live Session · (4) test concorrenza (double-resume 409, xmin loser) + AC riscritte.

## 9. As-built (2026-07-05)

**Step 1 — Chip** (commit `f6f8f3c69`): `formatSessionStartedAt` pure mapper (UTC instant → data+ora locale del viewer), chip read-only `▶ Ora di inizio {…} · derivata` su `LiveTopBar`/`SessionLiveView`. Una sola superficie (D1 narrowed), **D7 dropped**.

**Step 2 — BE resume** (commit `49f2848cd`): `GameNightEvent.AddSession` rilassato a `Published || InProgress`; **Completed** rifiutato con "Cannot add sessions to a finalized game night — start a new session." (→409 via i due handler che già mappano `InvalidOperationException`→`ConflictException`); Draft/Cancelled/Corrupted rifiutati col messaggio generico. Guard max-1-live #10 (`EnsureCanStartSession`:439 / `StartCurrentSession`:459) **invariati** → nessuna 2ª InProgress. Sblocca anche il 2ª-sitting per **giochi normali** già wired in `NightLiveClientView` (`useStartNextGame` → `StartGameNightSessionCommand`). Test: 3 domain (`AddSession` InProgress/Completed-guidance/Cancelled) + 3 handler (Start 2ª-sitting + Start double-resume + Attach 2ª-sitting).

**Step 3 — FE resume entry** (commit `93885e4a5`, decisione utente **= play-page**, non il resume-picker): scoperta bloccante — le campagne del resume-picker (`useUserCampaigns` → `GamebookCampaign`) **non hanno `gameNightId`** (solo lo `spine` lo espone, e solo se attached). Quindi l'entry è sulla **play page** `library/[gameId]/play/[campaignId]/_content.tsx`: CTA `▶ Riprendi la serata` sotto `SerataSpineStrip`, mostrata solo se `isSerataResumable(spine, currentUser.id)` (organizer + Published/InProgress + `!hasLiveSession`). Apre una NUOVA live Session via `POST /game-nights/{id}/gamebook-sessions` (`gameNightSessionClient.attachGamebookCampaign` + hook `useResumeGamebookSitting`) e instrada a `/sessions/{id}` (il FORK redirige la sessione in-progress a `/live`, dove appare la chip step-1). Feedback inline distinti: 409 max-live vs 403 organizer. Campagne standalone → lettura pura invariata. Test: predicato puro `isSerataResumable` (6 casi) + component (success-route / 409 / 403) + hook (delegate / max-live 409 / 403).

**Step 4 — Concorrenza + AC**: double-resume-while-live → 409 `MAX_LIVE_SESSIONS_EXCEEDED` senza Session orfana, su **entrambe** le chain (Start + Attach); xmin loser → 409 (test pre-esistenti in entrambi gli handler); resume-from-Completed → 409 al boundary. AC §4 riscritte (AC3 = InProgress 2ª-sitting + finalizzabile dopo; AC4 = Completed rifiutato al boundary, mai 500).
