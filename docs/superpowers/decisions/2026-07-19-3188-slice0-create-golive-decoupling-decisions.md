# Decision: Slice-0 — decouple create from go-live on the direct-create path (#3188)

**Status**: Accepted (core decisions) — 2 sub-decisions *pending final product confirmation*
**Created**: 2026-07-19
**Decider**: Product Owner (badsworm) via /sc:spec-panel, su raccomandazione ragionata del panel (Cockburn+Newman weighted)
**Epic**: [#3188](https://github.com/meepleAi-app/meepleai-monorepo/issues/3188) (parent #3157 C2b/C2c)
**Blocks**: Slice 1-6 dell'epic — non implementabili senza queste decisioni
**Companion**: [`docs/superpowers/audits/2026-07-19-3188-spec-panel-review.md`](../audits/2026-07-19-3188-spec-panel-review.md) (review completo, ground truth, 55 finding)

---

## Context

Il path direct-create (`POST /game-sessions` → `CreateSessionCommandHandler`, `SkipGameNightEnvelope=false`) confonde `create` con `go-live`: hand-builda il link `game_night_sessions` nato `InProgress`/`StartedAt=now` al persistence layer (`CreateSessionCommandHandler.cs:190-200`) bypassando l'aggregato `GameNightEvent`; `Session.OpenLiveMode()` non viene mai chiamato → `SessionStartedDomainEvent` non scatta → la tracking Session resta `Active/StartedAt=null/IsLive=false`. Un flip naive born-`Pending` rompe le semantiche live perché 5 consumer leggono liveness da `link.Status==InProgress` (max-1-live index, Complete, LiveQuery roster, FE `mapNightLive`, Finalize).

Lo spec-panel ha confermato il claim core sul codice e ha sollevato **4 gap materiali** + una decisione di prodotto bloccante che qui viene risolta.

---

## Decisions

### D1 — Intento di POST /game-sessions: **BOTH, default DRAFT**

`POST /game-sessions` crea un **draft** di default. Post-condition:
- link `game_night_sessions`: **`Pending`**
- tracking Session: `Active`, `StartedAt=null`, `IsLive=false`
- slot live (indice parziale): **libero**
- `GameNightEvent.Status`: **NON** promossa

Il go-live è un'azione **esplicita e separata** (vedi D2). Rationale (Cockburn): il goal safe/minimal-guarantee/reversibile (UC-A "logga una partita già giocata") è il default; il goal side-effecting/singleton (UC-B "avvia un tavolo live ora") richiede opt-in esplicito dell'attore. Elimina il CRUD-as-use-case overload che è la radice dell'anomalia. Supporta nativamente i multi-draft (invariante #19).

### D2 — Superficie go-live: **sub-resource REST dedicata**

`POST /api/v1/sessions/{id}/go-live` — coerente con la convention lifecycle esistente (`/sessions/{id}/{pause|resume|end|complete|abandon}`, `sessionsClient.ts:137-193`). Delega al pattern three-phase già corretto in `StartGameNightSessionCommandHandler` (`AddSession → StartCurrentSession → OpenSessionLiveModeCommand last`), con propria operation OpenAPI, idempotency, error surface `409`. **Rifiutato** il flag boolean su `CreateSessionCommand` (già flag-heavy con `SkipGameNightEnvelope`+`SkipKbReadinessGate` → control-coupling smell).

### D3 — Selezione draft al go-live: **`sessionId` esplicito**

Il go-live opera sul `sessionId` indicato nella URL, non sul `FirstOrDefault(Pending)`. Richiede un nuovo `GameNightEvent.StartSession(sessionId)` sull'aggregato che valida il target `Pending` e riusa `EnsureCanStartSession` (il metodo `GameNightSession.Start()` è `internal` → la promozione deve passare per l'aggregato). Nessuna ambiguità utente con multi-draft coesistenti.

### D4 — Canonical liveness owner: **`Session.IsLive` (SessionTracking)**

La liveness canonica è `Session.StartedAt != null && FinalizedAt == null` (owned da SessionTracking). Il `link.Status` diventa **proiezione derivata** aggiornata via `SessionStartedDomainEvent`/`OpenLiveMode`. Unifica i due read-path oggi divergenti (`GetGameNightLiveQueryHandler` su `link.Status` vs `FinalizeSessionCommandHandler` warning #13 su `Session.StartedAt/FinalizedAt`). Il FE riceve un **nuovo campo additivo `isLive`** (non un overload dell'enum `Status`).

---

## Sub-decisions (recommended — pending final product confirmation)

### D5 — Backward-compat del flip default → draft *(RACCOMANDATO, da confermare)*

Oggi il DTO pubblico `CreateSessionRequest` strippa `SkipGameNightEnvelope` (`SessionCommandEndpoints.cs:34-67`, #2920), quindi **ogni** POST client-visibile ottiene live-on-create; il passaggio a draft-default è **breaking**.

**Raccomandazione**: poiché l'unico consumer noto è il FE in-repo (deploy coordinato BE+FE), gestire come **migrazione coordinata in-repo** — nella Slice 3 aggiornare i call-site FE che intendono "live" a chiamare esplicitamente il go-live (D2), spedendo BE+FE nello stesso release train. Nessuna deprecation window RFC 8594 (assunto: nessun consumer API esterno).
**Da confermare**: esistono consumer dell'API esterni al monorepo? Se sì → serve `Deprecation`/`Sunset` header (precedente in `SessionCommandEndpoints.cs:124-162`).

### D6 — Semantica del cap max-5-per-night *(RACCOMANDATO, da confermare)*

`CreateSessionCommandHandler.cs:183-187` conta oggi **tutti** i `GameNightSessions` a prescindere dallo status → con N draft coesistenti (D1) un utente che logga 5 draft blocca il 6° con zero live.

**Raccomandazione**: il cap conta le sessioni **non-terminali** (`Pending` + `InProgress`), escludendo `Completed`/`Skipped`/`Corrupted`. Preserva un limite sano sul WIP concorrente senza bloccare la storia completa di una night attiva. Boundary test sul 6° draft non-terminale.
**Da confermare**: il cap di 5 rappresenta "WIP concorrente" (→ non-terminale) o "totale partite loggate per serata" (→ totale)?

---

## Consequences / Corrections all'epic

- **Item 2 è three-phase, non two-phase.** La 3ª fase (`OpenSessionLiveModeCommand`) è ciò che setta `Session.StartedAt`/`IsLive`; ometterla riproduce l'anomalia. Coerente con D4.
- **Item 3 copre due branch.** Il guard read-check va rilocato da `Session.Status==Active` at create a `link.Status==InProgress` at go-live, su **entrambi** i branch (attach-to-existing-night + ad-hoc puro).
- **Nuova Slice 5 (data-migration).** Righe legacy `InProgress`/`StartedAt=now` con Session mai aperta live → split-brain vs D4; template `20260719111334`.
- **`CreateAdHoc()` in scope.** `GameNightEvent.cs:132-149` setta `Status=InProgress` diretto: secondo conflate-create-with-go-live domain-level da ridefinire per atterrare `Published`.

## Refined DoD (sostituisce quella dell'epic)

- [x] Product decision su intento direct-create → **D1 Both/default-draft**
- [x] Superficie go-live → **D2 sub-resource**
- [x] Selezione draft → **D3 sessionId esplicito**
- [x] Canonical liveness owner → **D4 Session.IsLive**
- [ ] D5 backward-compat confermata (consumer esterni?)
- [ ] D6 semantica cap max-5 confermata
- [ ] Direct-create draft nasce `Pending`; go-live promuove via `StartSession(sessionId)` three-phase (max-1-live enforced su indice + `EnsureCanStartSession`)
- [ ] Multi-draft coesistono per night; live view / winner-picker / Complete lavorano off `Session.IsLive`
- [ ] Righe legacy riconciliate (Slice 5, idempotente)
- [ ] mapping 23505→409 rilocato sul go-live write, index name in costante condivisa
- [ ] Blast radius: + `CompleteGameNightSessionCommandHandlerTests`, `FinalizeSessionCommandHandlerTests`, concurrent-go-live test, `game-nights-live.schemas.test.ts`
- [ ] No regressione C1/C2a

## Slice plan → sub-issues

Vedi il review companion per scope/file/acceptance dettagliati per slice:
0. Decision record (questo doc) ✅
1. Read-model tolerance (forward-compat, deploy FIRST) — no product gate residuo
2. go-live sub-resource + `StartSession(sessionId)` + three-phase + 23505→409
3. Flip born-status direct-create + guard relocation (entrambi i branch) + FE call-site migration (D5)
4. Reconcile complete/finalize transitions
5. Data-migration/backfill righe legacy InProgress
6. Canonical live read-model migration su `Session.IsLive` (+ campo additivo `isLive` FE, deploy FE-first)
