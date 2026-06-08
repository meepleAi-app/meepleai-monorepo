---
title: "Admin Shared Game Import Workflow — Spec Panel Review"
date: 2026-06-08
panel: [Cockburn, Wiegers, Adzic, Fowler, Nygard, Crispin]
mode: discussion
focus: [requirements, architecture, testing]
status: ready-for-plan
bounded_contexts: [SharedGameCatalog, DocumentProcessing, KnowledgeBase, Administration]
related:
  - PR #1980 (BGG admin gate)
  - PR #1892 (admin custom cover upload)
  - PR #1903 (BGG user-side block ADR)
---

# Admin Shared Game Import Workflow — Spec Panel Consolidated Review

## 0. Summary

Workflow admin per popolare il catalogo `SharedGameCatalog` partendo dal solo PDF del regolamento, arricchendo i metadati via BoardGameGeek (BGG) come fonte esterna, ed eseguendo refinement (compresa publish) nella pagina `/admin/shared-games/{uuid}`.

La spec riusa la saga `ImportGameFromPdfCommand` esistente (Nygard-style 3-step + compensation), estendendola con `BggId` nel metadata DTO e con upload cover su storage interno.

**Status**: 6 decisioni lockate da utente in panel discussion (3 P0 + 3 secondarie). Ready for `/superpowers:writing-plans`.

---

## 1. User story originale (verbatim)

> *"Come admin, voglio creare un gioco da inserire tra i shared games. Ho il pdf del regolamento. Dopo averlo creato, voglio raccogliere i metadati del gioco dall'esterno e poi, nella pagina /admin/shared-games/{uuid gioco} eseguire l'edit."*

**Riformulazione canonica** (post panel synthesis): l'ordine letterale "*creare poi raccogliere*" era una semplificazione di linguaggio. L'ordine implementativo lockato è **arricchimento BGG prima, create-via-saga-atomica dopo, edit fine + publish nella pagina `/admin/shared-games/{uuid}`** (cfr. § 3, DEC-1).

---

## 2. Use Case UC-ADM-01

```
UC-ADM-01: Importa shared game da regolamento PDF
Primary Actor: Admin (role = Admin)
Stakeholders & Interests:
  - Admin: vuole popolare il catalogo rapidamente con dati BGG-validati
  - User finale: vuole giochi con metadata ricchi e PDF indexato per RAG
  - System: vuole record consistenti senza orphan PDF in DB

Goal Level: ☁ summary (composto da 3 sub-goal)
Scope: SharedGameCatalog (scrittura), DocumentProcessing (lettura/scrittura PDF), KnowledgeBase (trigger indexing)

Preconditions:
  - Admin autenticato con role Admin (verifica via useAdminRole + admin gate route)
  - PDF del regolamento disponibile (≤ 50MB, MIME application/pdf)

Postconditions Success:
  - SharedGame creato in status Draft con metadata BGG-arricchiti (o manuali)
  - PdfDocument linkato come Rulebook v1.0 attivo
  - Indexing in stato pending|completed|failed (warning se failed)
  - Admin atterra su /admin/shared-games/{gameId} con UI di edit

Postconditions Failure:
  - Nessun SharedGame orfano in DB (compensation eseguita)
  - PdfDocument orphan rimane in DB (recuperabile da admin in altro flow)

Main Success Scenario (MSS):
  1. Admin naviga a /admin/shared-games e clicca "Importa nuovo gioco"
  2. Sistema apre /admin/shared-games/new (wizard 3-step)
  3. STEP 1 — Upload PDF
     Admin carica PDF → sistema valida (size/MIME/quota=esente) → memorizza orphan
     Sistema mostra preview metadata estratti (es. titolo da SmolDocling, se disponibile)
  4. STEP 2 — Cerca su BGG
     Admin digita query (precompilato con title estratto se presente) → "Cerca su BGG"
     Sistema interroga BGG XML API → mostra top 10 risultati (manuale-select obbligatorio)
     Admin seleziona match → sistema fetch detail BGG + download cover image
  5. STEP 3 — Review metadata
     Form precompilato con dati BGG: admin può editare ogni campo
     Cover image precaricata da BGG (re-upload interno via STORAGE_PROVIDER)
     Admin clicca "Crea gioco"
  6. Sistema esegue saga ImportGameFromPdfCommand (atomica)
  7. Sistema redirige a /admin/shared-games/{gameId} con success toast
     (toast include IndexingStatus: "pending"|"failed" + correlationId per debug)
  8. Pagina edit espone form completo + RagReadinessIndicator + CTA "Pubblica"
  9. Admin esegue eventuale edit fine → save → vedi AC-9..10
  10. Admin clicca "Pubblica" quando ready → game passa a status Published

Extensions:
  3a. PDF upload fail (size/MIME/storage) → errore inline al field, admin re-tenta
  4a. BGG offline (timeout 10s × 3 retry) → fallback manuale (vedi Adzic Scenario 2)
  4b. BGG rate-limit 429 → countdown UX (useBggRateLimit), retry possibile dopo cooldown
  4c. Multi-match BGG con ambiguità → admin sceglie esplicitamente (no auto-select)
  4d. Zero match BGG → admin procede in manuale (no BGG → bggId=null nel record)
  6a. Saga step 2 (link PDF) fail → compensation auto-delete game → user-facing error
  6b. Saga step 3 (indexing enqueue) fail → game creato + warning persistente in UI
  9a. Concurrency edit con RowVersion mismatch → 409 + diff UI (vedi Adzic Scenario 3)
```

---

## 3. Decisions Log (utente-lockate)

| ID | Decisione | Rationale | Locked by |
|----|-----------|-----------|-----------|
| **DEC-1** | Saga atomica: arricchimento BGG **prima** del create | Letterale "create poi raccogli" era semplificazione di linguaggio. Atomic = no stato intermedio incoerente, no rollback complesso, allineato a saga esistente | User reply 2026-06-08 ("Ho semplificato") |
| **DEC-2** | BGG match: **sempre manuale**, no auto-select anche con high confidence | Priorità precisione catalogo > latenza. Riduce false-positive. Stabile per test (no flakiness ML confidence) | User reply 2026-06-08 ("manuale sempre") |
| **DEC-3** | `/admin/shared-games/{uuid}` = destinazione **post-create** per edit fine + publish | Single responsibility per page: `/new` = wizard creazione, `/{uuid}` = edit + publish. Mainstream interpretation | User reply 2026-06-08 ("si") |
| **DEC-4** | Cover image: **re-upload sul nostro storage** durante saga | Indipendenza da BGG uptime per immagini pubbliche. Usa STORAGE_PROVIDER factory esistente + pattern PR #1892 CoverUrlResolver pre-wired (P178). Failure handling: vedi F6 in § 7 | AskUserQuestion 2026-06-08 (opzione 2) |
| **DEC-5** | Permission: **solo role Admin** | Coerente con BGG admin-gate PR #1980 e admin route gate generico. Editor/SuperAdmin: TBD future iteration | AskUserQuestion 2026-06-08 |
| **DEC-6** | Quota PDF: **admin esente** da `PdfUploadQuotaService` | Uso operativo del catalogo non deve essere limitato come utente free/paid. Bypass via role check | AskUserQuestion 2026-06-08 |

---

## 4. Acceptance Criteria (Wiegers SMART)

| ID | AC | Misurabilità |
|----|----|---|
| **AC-1** | Solo utente con role = `Admin` può accedere a `/admin/shared-games/new` e a `/admin/shared-games/{uuid}`. Non-admin riceve 403. | Unit test handler + E2E gate route |
| **AC-2** | Upload PDF accetta `application/pdf` ≤ 50MB. Altri MIME/size superato → errore specifico al field. | Unit test `UploadPdfCommandValidator` + manual UX |
| **AC-3** | Sistema interroga BGG XML API v2 con query string. p95 < 5s. Retry max 3 con exponential backoff. | Integration test con BGG mock + perf assertion |
| **AC-4** | Selezione match BGG precompila: title, year, description, minPlayers, maxPlayers, playingTimeMinutes, minAge, categories, mechanics, designers, publishers, **bggId**, cover image. | Integration test saga end-to-end |
| **AC-5** | Admin può sovrascrivere ogni campo precompilato prima di "Crea gioco". | E2E Playwright editing |
| **AC-6** | "Crea gioco" invoca `ImportGameFromPdfCommand`. Su successo: game in status = `Draft`, PDF linked come Rulebook v1.0 attivo, indexing async dispatched. | Integration test handler |
| **AC-7** | Sistema redirige a `/admin/shared-games/{gameId}` entro 2s dal 201. Success toast contiene `IndexingStatus` + `CorrelationId`. | E2E timing assertion |
| **AC-8** | Se `IndexingStatus = "failed"`, pagina `/{uuid}` mostra warning persistente con CTA "Riavvia indexing" (re-dispatch `IndexDocumentCommand`). | Integration test + manual UX |
| **AC-9** | Pagina `/admin/shared-games/{uuid}` espone edit form per: Title, Description, Year, MinPlayers, MaxPlayers, PlayingTimeMinutes, MinAge, Categories, Mechanics, Publishers, Designers, CoverImage, BggId. NON editabili: Id, CreatedAt, CreatedBy. | Component test `GameForm` |
| **AC-10** | Salvataggio edit invoca `UpdateSharedGameCommand` con `RowVersion`. Conflict → 409 + diff UI. Successo → `UpdatedAt` + `UpdatedBy` aggiornati. | Integration test concurrency |
| **AC-11** | Admin può transizionare Draft → Published via CTA "Pubblica". CTA visibile solo se `Status = Draft`. Invoca `PublishSharedGameCommand`. | Integration test state machine |
| **AC-12** | Idempotency: doppio submit "Crea gioco" con stesso `Idempotency-Key` (UUID v4 generato client) entro 5 min → restituisce identico `gameId` (no duplicate). | Integration test idempotency |

---

## 5. Scenarios Given/When/Then (Adzic)

### Scenario 1 — Happy path completo (BGG match unico)

```gherkin
Feature: Importa shared game da PDF con arricchimento BGG

Scenario: Admin crea CATAN da PDF con match BGG univoco
  Given sono autenticato come Admin
  And nel catalogo non esiste un gioco con bggId = 13
  When carico il PDF "catan-rulebook-it.pdf" (3.2 MB)
  Then vedo "PDF caricato come bozza" e pulsante "Cerca su BGG"

  When inserisco "Catan" nella ricerca BGG e clicco "Cerca"
  Then ricevo una lista di risultati BGG ordinati per rilevanza
  And il primo risultato è { bggId: 13, name: "CATAN", year: 1995 }

  When seleziono il primo risultato
  Then il form metadata si precompila con:
    | campo            | valore                                |
    | title            | CATAN                                 |
    | yearPublished    | 1995                                  |
    | minPlayers       | 3                                     |
    | maxPlayers       | 4                                     |
    | playingTimeMinutes | 90                                  |
    | minAge           | 10                                    |
    | designers        | ["Klaus Teuber"]                      |
    | publishers       | ["KOSMOS"]                            |
    | categories       | ["Strategy", "Negotiation"]           |
    | bggId            | 13                                    |
  And la cover image è scaricata da BGG e re-uploadata su nostro storage
  And vedo preview cover dal nostro CDN (non da BGG)

  When clicco "Crea gioco"
  Then la saga ImportGameFromPdfCommand esegue 3 step atomici
  And ricevo redirect a "/admin/shared-games/{gameId}"
  And il game ha status "Draft"
  And il PDF è linked come Rulebook v1.0 attivo
  And indexingStatus = "pending"
  And vedo banner "Indicizzazione in corso, ~2 min" con CorrelationId

  When l'indexing completa async (≤2 min)
  Then RagReadinessIndicator passa a "Pronto"
  And banner indexing scompare
```

### Scenario 2 — BGG offline → fallback manuale

```gherkin
Scenario: Admin completa creazione anche con BGG irraggiungibile
  Given BGG XML API è offline (timeout dopo 10s × 3 retry = ~30s totali)
  And ho caricato un PDF valido

  When clicco "Cerca su BGG"
  Then vedo errore "BGG non raggiungibile. Procedi con metadata manuali."
  And il form metadata è vuoto ma editabile
  And vedo CTA "Riprova BGG" e "Procedi senza BGG"

  When scelgo "Procedi senza BGG"
  And compilo manualmente: title="Wingspan", yearPublished=2019, minPlayers=1, maxPlayers=5
  And carico una cover image manualmente dal mio file system
  And clicco "Crea gioco"
  Then il game è creato in Draft
  And nel record bggId è null
  And cover image è uploaded su nostro storage (path admin custom upload)
  And nessun campo BGG-only è popolato
```

### Scenario 3 — Edit post-creazione con concurrency

```gherkin
Scenario: Due admin editano lo stesso game in concorrenza
  Given esiste game { id: "abc-123", title: "Wingspan", rowVersion: v1 }
  And Admin-A apre /admin/shared-games/abc-123
  And Admin-B apre /admin/shared-games/abc-123 (stesso rowVersion: v1)

  When Admin-A modifica title="Wingspan ITA" e salva
  Then il salvataggio ha successo (200) e rowVersion diventa v2

  When Admin-B modifica minPlayers=2 e salva (con rowVersion: v1 stale)
  Then ricevo errore 409 ConflictException con messaggio:
    "Il gioco è stato modificato da Admin-A alle HH:MM. Ricarica e riprova."
  And vedo diff tra il mio editing e la versione corrente in v2
  And ho 2 opzioni UI: "Ricarica e perdi modifiche" / "Sovrascrivi forzato"

  When clicco "Ricarica e perdi modifiche"
  Then la pagina reloada con rowVersion=v2 e dati di Admin-A
  And i miei edit vengono persi (warning toast)
```

### Edge scenarios (da implementare come sub-issue)

- **E1**: Multi-match BGG (5+ risultati con stesso title) → manual select obbligatorio (no auto)
- **E2**: PDF già linked a un altro game nel frattempo → saga fail 409 in step 2 → compensation + error UI
- **E3**: Admin abbandona wizard dopo upload PDF → orphan PDF rimane (cleanup via cron, vedi `RetryFailedPdfsJob`)
- **E4**: Indexing fallisce ma admin pubblica comunque → game pubblicato ma RAG search non funziona → warning UI persistente

---

## 6. Architecture mapping (Fowler)

### 6.1 Component map (esistente vs nuovo)

| Component | Path | Status | Modifica richiesta |
|-----------|------|--------|---------------------|
| `ImportGameFromPdfCommand` | `BoundedContexts/SharedGameCatalog/Application/Commands/ImportGameFromPdfCommand.cs` | ✅ esiste | ➕ Aggiungi `int? BggId` a `ImportGameMetadataDto` |
| `ImportGameFromPdfCommandHandler` | stesso path | ✅ esiste | 🔧 Propaga `BggId` a `CreateSharedGameCommand` (oggi hardcoded `null` riga 150) |
| `CreateSharedGameCommand` | `BoundedContexts/SharedGameCatalog/Application/Commands/` | ✅ esiste (accetta già BggId) | nessuna |
| `AddDocumentToSharedGameCommand` | stesso BC | ✅ esiste | nessuna |
| `UploadPdfCommand` (orphan) | `BoundedContexts/DocumentProcessing/Application/Commands/` | ✅ esiste | ⚠️ Verifica supporto upload **senza** gameId (orphan mode) |
| `SearchBggGamesQuery` | TBD | ❓ verificare bridge MediatR | 🆕 Crea se manca (`useSearchBggGames` FE chiama endpoint) |
| `GetBggGameDetailQuery` | TBD | ❓ verificare | 🆕 Crea se manca (per fetch full detail post-select) |
| `UpdateSharedGameCommand` | TBD | ❓ verificare | 🆕 Crea se manca (NO mega-update; SRP) |
| `PublishSharedGameCommand` | TBD | ❓ verificare | 🆕 Crea se manca |
| `CoverUrlResolver` | `BoundedContexts/SharedGameCatalog/...` (pre-wired PR #1892) | ✅ esiste | 🔧 Usa per resolve cover post-upload |
| `STORAGE_PROVIDER` factory | `infra/secrets/storage.secret` | ✅ esiste | nessuna |
| `useSearchBggGames` | `apps/web/src/hooks/queries/` | ✅ esiste | nessuna |
| `useBggRateLimit` | `apps/web/src/lib/domain-hooks/` | ✅ esiste | 🔧 Wire countdown UX in wizard step 2 |
| `BggSearchPanel` | `apps/web/src/components/admin/shared-games/` | ✅ esiste | 🔧 Probabile riuso parziale in wizard |
| `GameForm` | stesso path | ✅ esiste | 🔧 Riuso in step 3 wizard + page `/{uuid}` |
| `EditGameDrawer` | stesso path | ✅ esiste | Considera se page `/{uuid}` riusa drawer o full-page form |
| `PdfUploadSection` | stesso path | ✅ esiste | 🔧 Riuso in step 1 wizard |
| `ImageUpload` | stesso path | ✅ esiste | 🔧 Riuso in step 3 wizard |
| `RagReadinessIndicator` | `.../rag-setup/` | ✅ esiste | 🔧 Mount nella page `/{uuid}` |
| `PdfIndexingStatus` | stesso path | ✅ esiste | 🔧 Mount nella page `/{uuid}` |
| Page `/admin/shared-games/new` | TBD | 🆕 da creare | Wizard 3-step orchestrator |
| Page `/admin/shared-games/[uuid]` | TBD | ❓ verificare esistenza | 🆕 Crea se manca |

### 6.2 Sequence flow (wizard create)

```
Admin Browser              Next.js API Route        .NET API (MediatR)         BGG XML API     Storage
     │                          │                         │                          │              │
     │── 1. Upload PDF ──────────────────────────────────►│                          │              │
     │                          │                         │── UploadPdfCommand ─────────────────────►│
     │                          │                         │◄── PdfDocumentId ────────────────────────┤
     │◄── PdfDocumentId ──────────────────────────────────┤                          │              │
     │                          │                         │                          │              │
     │── 2. Search "Catan" ─────────────────────────────►│                          │              │
     │                          │                         │── SearchBggGamesQuery ──►│              │
     │                          │                         │◄── [match1, match2, …] ──┤              │
     │◄── results ────────────────────────────────────────┤                          │              │
     │                          │                         │                          │              │
     │── 3. Select bggId=13 ────────────────────────────►│                          │              │
     │                          │                         │── GetBggGameDetailQuery ►│              │
     │                          │                         │◄── full detail + cover ──┤              │
     │                          │                         │── download cover ────────────────────────►│
     │                          │                         │◄── cover URL nostro CDN ─────────────────┤
     │◄── metadata + coverUrl ────────────────────────────┤                          │              │
     │                          │                         │                          │              │
     │── 4. ImportGameFromPdf ──────────────────────────►│                          │              │
     │   (Idempotency-Key UUID)│                         │── saga step 1: Create ──►│ (DB)         │
     │                          │                         │── saga step 2: Link PDF ►│ (DB)         │
     │                          │                         │── saga step 3: Index ───►│ (async)      │
     │◄── { gameId, status, correlationId } ──────────────┤                          │              │
     │                          │                         │                          │              │
     │── 5. Redirect /admin/shared-games/{gameId} ──────►│                          │              │
```

### 6.3 Pagina `/admin/shared-games/[uuid]` — composition

```
┌─ Page layout admin ────────────────────────────────────┐
│ Header: titolo gioco + Status badge (Draft/Published)  │
│ ─────────────────────────────────────────────────────  │
│ ┌─ Tab "Dettagli" (default) ───┐ ┌─ Tab "RAG" ──────┐ │
│ │ GameForm (edit campi)         │ │ RagReadinessIndic │ │
│ │ - Title, Year, Description    │ │ PdfIndexingStatus │ │
│ │ - Players, Time, Age          │ │ CTA "Riavvia indx"│ │
│ │ - Categories (TagInput)       │ │                   │ │
│ │ - Mechanics (TagInput)        │ └───────────────────┘ │
│ │ - Designers, Publishers       │                       │
│ │ - BggId (editabile)           │ ┌─ Tab "PDFs" ─────┐ │
│ │ - CoverImage (ImageUpload)    │ │ PdfDocumentList   │ │
│ │ [Save] (PATCH UpdateShared..)│ │ + Upload nuovo    │ │
│ └───────────────────────────────┘ └───────────────────┘ │
│                                                          │
│ ─────────────────────────────────────────────────────   │
│ Footer: [Pubblica] (se Status=Draft) | [Elimina] (danger)│
└──────────────────────────────────────────────────────────┘
```

---

## 7. Failure modes (Nygard)

| # | Failure | Probabilità | Detection | Mitigation | UX |
|---|---------|-------------|-----------|------------|-----|
| **F1** | BGG XML API timeout/5xx | 🟡 media | HTTP timeout 10s + retry 3 backoff | Fallback manuale (Scenario 2) | Errore esplicito + CTA "Procedi senza BGG" |
| **F2** | BGG rate-limit 429 | 🔴 alta in burst | `useBggRateLimit` esistente | Coda client + countdown | "Riprova tra Xs" countdown visibile |
| **F3** | PDF upload fail (size/MIME/storage) | 🟡 media | `UploadPdfCommandValidator` | Errore inline al field | Causa specifica visibile (es. "File 67MB supera limite 50MB") |
| **F4** | Saga step 2 fail (link PDF) | 🟢 bassa | Try/catch in handler:51 | Compensation `DeleteSharedGameCommand` (esistente) | Error toast: "Import fallito, riprova" |
| **F5** | Compensation FALLISCE (DELETE game fail dopo step 2 fail) | 🟢 molto bassa | Log warning handler:69 | Orphan game rimane → cron cleanup futuro | Error toast generico + log per ops |
| **F6** | Download cover BGG fail | 🟡 media (CDN flaky) | HTTP exception in cover downloader | Fallback: salva ImageUrl=BGG-direct (degraded), warning UI | Banner "Cover non scaricata, link diretto temporaneo" |
| **F7** | Re-upload cover su nostro storage fail | 🟢 bassa | Storage exception | Stesso F6: fallback link BGG diretto | Stesso F6 |
| **F8** | Indexing fail (RAG/Qdrant down) | 🟡 media | `IndexingStatus = "failed"` in result | Game creato + warning persistente | RagReadinessIndicator rosso + CTA "Riavvia indexing" |
| **F9** | Doppio-submit "Crea gioco" | 🟡 media (user impazienza) | Idempotency-Key middleware | Stesso `gameId` ritornato | UI disable button after first click + spinner |
| **F10** | PDF già linked nel frattempo | 🟢 bassa | DB FK constraint o saga check | Saga fail 409 in step 2 → compensation | Error: "PDF già usato da altro gioco" |
| **F11** | Admin abbandona wizard dopo upload PDF | 🟡 alta (UX reale) | TTL su orphan PDF | Cron cleanup orphan dopo N giorni | Nessuna UX (silenzioso) |
| **F12** | RowVersion concurrency edit | 🟡 media | `DbUpdateConcurrencyException` | 409 + diff UI (Scenario 3) | Vedi AC-10 |

### Operational requirements

- 📊 **Observability**: ogni step della saga deve loggare con `gameId`, `pdfId`, `bggId`, `requestedBy`, **`correlationId`** (nuovo, propagato da request). Handler attuale già logga base info.
- 🔁 **Idempotency**: header `Idempotency-Key` (UUID v4 client) con TTL 5 min in Redis (esistente).
- ⏱️ **Timeout E2E saga**: < 30s escluso indexing async.
- 🧹 **Orphan PDF cleanup**: TTL N giorni (defer al follow-up, non blocker MVP).

---

## 8. Quality attributes & Testing strategy (Crispin)

### NFR table

| Attributo | Target | Verifica |
|-----------|--------|----------|
| Perf — upload PDF p95 | < 5s per 10MB | Load test K6 |
| Perf — BGG search p95 | < 5s | Integration test con BGG mock |
| Perf — saga E2E p95 | < 30s (excl. indexing) | Integration test Testcontainers |
| Perf — redirect post-create | < 2s | E2E Playwright timing |
| Reliability — BGG offline | flow utilizzabile manuale | Chaos test mock BGG down |
| Reliability — compensation | 100% rollback su step 2 fail | Unit test handler con mock failure |
| Security — permission gate | 403 per non-admin su `/new` e `/{uuid}` | Integration test auth |
| Security — Idempotency | no duplicate game su double-submit | Integration test |
| A11y — wizard + edit page | axe AA pass (gate blocking) | E2E axe (gate esistente, vedi CLAUDE.md a11y restore) |
| UX — wizard keyboard nav | drag-drop ha alternativa keyboard | Manual a11y review |
| i18n — IT/EN keys | tutte le stringhe i18n-ized | grep raw strings + test renderWithIntl |

### Testing pyramid

```
          ╱╲
         ╱E2╲     ▸ 1 happy path Playwright (Scenario 1)
        ╱────╲    ▸ 1 fallback BGG Playwright (Scenario 2)
       ╱ Integ ╲  ▸ 4-5 Testcontainers: saga atomic, compensation, idempotency, concurrency, permission
      ╱─────────╲
     ╱  Unit/CH  ╲ ▸ 15-20: validator, BGG mock, error mapping, RowVersion handler
    ╱──────────────╲
```

### Edge cases mandatori per test

1. **BGG zero-match** → form vuoto, admin manuale, bggId=null
2. **BGG multi-match (10+)** → admin select obbligatorio, no auto
3. **Idempotency double-submit** identico body → 1 game in DB
4. **Idempotency triple-submit** body diverso, stesso key → 1 game (primo vince)
5. **PDF già linked** → 409 saga step 2 → compensation eseguita
6. **Compensation FAIL** → log warning, orphan game tracked
7. **Concurrency edit** → 409 + diff UI (Scenario 3)
8. **Permission non-admin** → 403 + redirect login

---

## 9. Quality scoring spec finale

| Dimensione | Original | Post-panel | Delta |
|-----------|----------|------------|-------|
| Clarity | 4/10 | 9/10 | +5 (DEC-1..6 risolvono ambiguità) |
| Completeness | 3/10 | 9/10 | +6 (12 AC + 3 scenarios + NFR table) |
| Testability | 2/10 | 9/10 | +7 (Given/When/Then + pyramid + edge cases) |
| Consistency | 6/10 | 9/10 | +3 (allineata a saga esistente + pattern PR #1892) |
| **Overall** | **3.75/10** | **9.0/10** | **+5.25** |

---

## 10. Roadmap

### 🟧 P1 — Pre-implementation verifies (1-2h)

- **V1**: Verifica esistenza `UpdateSharedGameCommand` + `PublishSharedGameCommand` (MediatR bridge)
- **V2**: Verifica esistenza route `/admin/shared-games/[uuid]/page.tsx`
- **V3**: Verifica `SearchBggGamesQuery` + `GetBggGameDetailQuery` come MediatR commands (oltre l'hook FE)
- **V4**: Verifica supporto orphan PDF upload in `UploadPdfCommand` (senza gameId obbligatorio)
- **V5**: Verifica `CoverUrlResolver` API surface per cover re-upload

### 🟨 P2 — Backend deltas (1-2g)

- **B1**: Estendi `ImportGameMetadataDto` con `int? BggId`
- **B2**: Propaga `BggId` in `ImportGameFromPdfCommandHandler.CreateSharedGameAsync` (riga 150 oggi `null`)
- **B3**: Crea command + handler mancanti identificati in V1-V5
- **B4**: Aggiungi `Idempotency-Key` middleware support su `ImportGameFromPdfCommand`
- **B5**: Aggiungi `CorrelationId` propagazione nel saga
- **B6**: Bypass `PdfUploadQuotaService` per role Admin (DEC-6)
- **B7**: Cover downloader service (BGG URL → STORAGE_PROVIDER upload, con fallback F6/F7)

### 🟩 P3 — Frontend wizard (2-3g)

- **F1**: Page `/admin/shared-games/new` orchestrator (3-step state machine)
- **F2**: Step 1 wizard riuso `PdfUploadSection` (orphan mode)
- **F3**: Step 2 wizard riuso `BggSearchPanel` + `useBggRateLimit` countdown
- **F4**: Step 3 wizard form review riuso `GameForm` + `ImageUpload`
- **F5**: i18n IT/EN keys batched (pattern P201)
- **F6**: a11y verify (drag-drop keyboard, listbox roles, axe AA)

### 🟪 P4 — Page `/{uuid}` edit (1-2g se nuova, 0.5g se esiste)

- **E1**: Page route + layout (tabs Dettagli/RAG/PDFs)
- **E2**: Edit form full riuso `GameForm`
- **E3**: Mount `RagReadinessIndicator` + `PdfIndexingStatus`
- **E4**: CTA "Pubblica" (visible if Status=Draft) + `PublishSharedGameCommand`
- **E5**: Concurrency handling 409 + diff UI

### 🟦 P5 — Test suite (parallelo a P2-P4)

- Unit handler tests (15-20)
- Integration saga tests (5)
- E2E Playwright happy + fallback (2)
- A11y axe gate (auto)

### Effort indicativo totale

5-7 giorni full-stack incluse pre-verifies (P1) e test suite (P5).

---

## 11. Follow-ups / Future work

- **FU-1** (P3): Editor role permission (DEC-5 escalation)
- **FU-2** (P3): Auto-extracted title da PDF via SmolDocling come seed BGG search
- **FU-3** (P3): Orphan PDF cleanup cron (F11)
- **FU-4** (P3): Compensation failure recovery automatic (F5)
- **FU-5** (P3): Bulk import (CSV o multi-PDF) → out-of-scope MVP
- **FU-6** (P3): Auto-match BGG opt-in con confidence threshold (DEC-2 reverse)
- **FU-7** (P3): Versioning Rulebook PDF (admin sostituisce v1.0 con v1.1) — usa `VersionBadge` esistente

---

## 12. References (codebase grounding)

### Backend

- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ImportGameFromPdfCommand.cs:31` — saga command + DTO
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ImportGameFromPdfCommandHandler.cs:36` — handler 3-step saga
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommand.cs` — PDF upload (verifica orphan support)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfUploadQuotaService.cs` — quota da bypassare per admin
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/` — `IndexDocumentCommand` (fire-and-forget step 3)

### Frontend

- `apps/web/src/components/admin/shared-games/BggSearchPanel.tsx` — riuso parziale wizard step 2
- `apps/web/src/components/admin/shared-games/GameForm.tsx` — riuso step 3 + page `/{uuid}`
- `apps/web/src/components/admin/shared-games/EditGameDrawer.tsx` — pattern edit (valutare drawer vs page)
- `apps/web/src/components/admin/shared-games/PdfUploadSection.tsx` — riuso step 1
- `apps/web/src/components/admin/shared-games/ImageUpload.tsx` — riuso step 3 (manual cover)
- `apps/web/src/components/admin/shared-games/rag-setup/RagReadinessIndicator.tsx` — mount page `/{uuid}`
- `apps/web/src/components/admin/shared-games/PdfIndexingStatus.tsx` — mount page `/{uuid}`
- `apps/web/src/hooks/queries/useSearchBggGames.ts` — BGG search hook (admin-gated)
- `apps/web/src/lib/domain-hooks/useBggRateLimit.ts` — rate-limit countdown UX
- `apps/web/src/lib/api/clients/bggClient.ts` — BGG API client

### Patterns & memory

- **CLAUDE.md** § Code Standards — CQRS rule, RowVersion concurrency, soft delete, audit
- **MEMORY** P178 — CoverUrlResolver pre-wired discovery (PR #1892)
- **MEMORY** P179 — Spec-panel blob storage signature trust-but-verify
- **MEMORY** P201 — i18n batched via atomic Python merge
- **MEMORY** F2 PR #1980 — BGG admin gate `useAdminRole`

---

## 13. Glossary

| Term | Definition |
|------|-----------|
| Shared game | Gioco nel `SharedGameCatalog` BC, visibile a tutti gli utenti del catalogo condiviso |
| Orphan PDF | `PdfDocument` caricato in DB ma non ancora linkato a un game |
| Saga | Pattern transazionale a step con compensation per atomicità distribuita (Nygard) |
| BGG | BoardGameGeek, fonte esterna per metadata giochi (XML API v2) |
| Draft / Published | Status del game; Draft = visibile solo admin, Published = visibile catalog pubblico |
| RowVersion | EF Core optimistic concurrency token (`[Timestamp] byte[]`) |
| Idempotency-Key | UUID v4 client-generated per dedup di submit ripetuti |
| Indexing | Processo RAG: chunking PDF + embedding + vector storage in Qdrant |
| CoverUrlResolver | Service pre-wired (PR #1892) per resolve cover URL con fallback chain |

---

**Spec status**: ✅ ready-for-plan
**Next step suggerito**: `/superpowers:writing-plans` su questa spec per generare TDD plan implementativo
