# Nanolith staging E2E setup — design

> **Date**: 2026-05-07
> **Owner**: @DegrassiAaron (badsworm@gmail.com — single tester / decision authority)
> **Status**: design (pre-implementation, awaiting plan)
> **Brainstorming output** of 2026-05-07 spec-panel + 3 multi-choice decisions
> **Cross-refs**: #78 (S3 retrieve key bug), #807 (A11y audit), #808 (SP6 v2 expansion freeze), #79x (PR-A pdf game id resolver, merged), #799 (PR-B chat typing indicator, merged), #810 (PR A11y stop-gap, merged)

---

## 1. Goal

Quando Aaron logga `badsworm@gmail.com` su `https://meepleai.app` da qualunque device, su staging deve trovare:

1. Game **Nanolith** caricato (id `6db3e01e-0b21-414c-8e3b-a899782feb40`, sharedGameId `94e99e38-1a5a-499c-89a9-2ea66173f63e`)
2. **Entrambi** i PDF Nanolith (`Press Start ENG`, `Rules ENG`) in stato `Ready` con almeno 20 chunks ciascuno
3. **2 agent attivi**: Tutor (per tutorial/setup) + Arbitro (per regole/interpretazione)
4. **Chat AI funzionante** con citation streaming via SSE
5. **UI** che usa solo componenti v2 `done` (rispetto freeze #808) e match layout dei mockup `admin-mockups/design_files/sp4-*.html`

Lo smoke test "gold path Nanolith" deve essere riproducibile da Aaron in qualunque sessione di test, da qualunque device, senza setup ulteriore.

---

## 2. Context (audit pre-design)

| Componente | Stato attuale | Note |
|------------|---------------|------|
| Game `Nanolith` | ✅ esiste in DB | `games.Id = 6db3e01e-...`, `SharedGameId = 94e99e38-...` |
| User `badsworm@gmail.com` | ✅ superadmin | `Status=Active`, `EmailVerified=true`, `PasswordHash=v1.210000.*` (PBKDF2-SHA256, set 2026-05-06 via SQL durante debug OAuth) |
| PDF "Press Start ENG" | ✅ Ready, 29 chunks | `pdf_documents.Id = 808b83ec-...` |
| PDF "Rules ENG" | ❌ stuck `Extracting` | `pdf_documents.Id = b680bc20-...`, file presente in S3 con key `pdf_uploads/6db3e01e-.../b680bc20..._Nanolith Rules ENG.pdf` (correctly saved) ma retrieve cerca `pdf_uploads/b680bc20.../b680bc20..._*` (wrong key) |
| Agents per Nanolith | ❓ unknown | da verificare con `SELECT FROM agent_definitions WHERE shared_game_id = '94e99e38-...'` |
| Bug S3 retrieve | 🔴 attivo | `PdfProcessingPipelineService.ExtractTextAsync:334` passa `(fileId, fileId)` invece di `(fileId, gameId)` a `RetrieveAsync`. Save (`UploadPdfCommandHandler`) usa `gameId` reale; retrieve usa `pdfId`. Mismatch. |
| Image API staging | ✅ ricostruita 2026-05-06 | da repo HEAD `c9e997c77`. Image precedente era 2026-04-17 (regressione 3 settimane) |
| CF Access bypass `/api/v1/auth/oauth/*/callback` | ✅ attivo | application separata "MeepleAI OAuth Callback Bypass" creata 2026-05-06 |
| Frontend A11y E2E job | ⚠️ `continue-on-error` | PR #810 stop-gap fino a token redesign Q3 (#807) |
| SP6 v2 expansion | 🔒 FREEZE | #808 — no nuovi v2 component fino a token redesign |

### Bounded contexts coinvolti

- `Authentication` — login email/password, sessione
- `GameManagement` — game record, page `/games/{id}`
- `SharedGameCatalog` — sharedGameId binding cross-BC
- `DocumentProcessing` — PDF upload, extract, chunk, embed, index
- `KnowledgeBase` — RAG retrieval, chat thread, AgentDefinition

---

## 3. Decisioni di scope (brainstorming output)

| Q | Decisione | Razionale |
|---|-----------|-----------|
| 1 — Bug S3 fix approach | **🅲 Hot-fix immediato + PR-F clean parallelo** | Sblocca smoke test oggi; debt tracked in task #78 → PR-F clean follow-up con helper riusabile |
| 2 — UI scope mockup `sp4-*` | **🅰 Match layout, riusa solo v2 `done`** | Rispetta freeze #808; smoke focus; iso-pixel rinviato post-Q3 token redesign |
| 3 — Agent count + typology | **🅱 2 agenti: Tutor + Arbitro** | Valida sistema multi-agent (picker, source priority); Aaron ha esperienza Nanolith e può differenziare Q tutorial vs Q regole |

### Defaults assunti (correggibili)

- **KB visibility**: shared (già configurato — `pdf_documents.SharedGameId` set su entrambi i PDF). Aaron è superadmin quindi vede tutto comunque.
- **Agent scope**: shared per `SharedGameId`, accessibile a qualunque user del game. Non user-private.

---

## 4. Architecture

Pipeline E2E per il "gold path Nanolith":

```
[Browser] ──CF Access──> [meepleai.app] ──proxy──> [API container :8080]
                                                          │
                                                          ▼
                              [PostgreSQL meepleai-postgres]
                              [Redis meepleai-redis]
                              [R2 bucket meepleai-uploads]
                                          │
                              [PdfProcessingPipelineService]
                                          │
                              ┌───────────┼───────────┐
                              ▼           ▼           ▼
                       [DocnetPdf     [text_chunks  [pgvector
                        text extr]     INSERT]       embeddings]
                                          │
                                          ▼
                              [SearchKbChunks RAG retrieval]
                                          │
                                          ▼
                              [HybridLlmService (DeepSeek/OpenRouter/Ollama)]
                                          │
                                          ▼
                              [SSE stream → ChatThreadView]
```

### Critical fix path

1. **Hot-fix S3 retrieve** (Fase 1): patch image staging in-place, S3 copy file Rules.pdf al path "wrong key" che retrieve attualmente cerca. Smoke immediato.
2. **PR-F clean** (Fase 5, post-smoke): branch `fix/pdf-blob-storage-retrieve-key-mismatch` da `main-dev`. Helper `PdfStorageKey.BucketGameKey(pdf)` riusa pattern di `PdfGameIdResolver` (PR-A). Test, code review, merge.

---

## 5. Components & data model

### Schema DB

```sql
-- games (esiste già)
SELECT "Id", "Name", "SharedGameId" FROM games WHERE "Name" = 'Nanolith';
-- ▶ 6db3e01e-... | Nanolith | 94e99e38-...

-- pdf_documents (1 Ready, 1 stuck)
SELECT "Id", "FileName", processing_state, "SharedGameId"
FROM pdf_documents
WHERE "SharedGameId" = '94e99e38-1a5a-499c-89a9-2ea66173f63e';
-- ▶ 808b83ec | Nanolith Press Start ENG.pdf | Ready    | 94e99e38
-- ▶ b680bc20 | Nanolith Rules ENG.pdf       | Failed¹  | 94e99e38
-- (¹ post Fase 2 SQL UPDATE)

-- agent_definitions (DA CREARE)
INSERT INTO agent_definitions (...) VALUES
  (gen_random_uuid(), 'Tutor Nanolith',   'Tutor',    '94e99e38-...', true, 'Published', ...),
  (gen_random_uuid(), 'Arbitro Nanolith', 'Arbitro',  '94e99e38-...', true, 'Published', ...);

-- agent_kb_sources (DA CREARE)
-- Tutor: Press Start primary, Rules secondary
-- Arbitro: Rules primary, Press Start secondary
```

### UI components (riuso v2 `done`)

Da v2-migration-matrix `done` rows:
- `MeepleCard` (entity=game) — hero `/games/[id]`
- `ChatThreadView` + `ChatMessageList` + `useThreadMessages` (post PR-B fix typing indicator amber)
- `LibraryHub` (per nav)
- `AgentSelector` esistente

**Nessun nuovo v2 component**. Se gap visivi vs `sp4-game-detail.html` ci sono, audit + tracking in task ma NO refactor durante questo lavoro (rispetto freeze).

---

## 6. SMART goals + Gherkin scenarios

### G1 — Login + Game state ready

**SMART**: Quando Aaron logga `badsworm@gmail.com` su `https://meepleai.app` da qualunque device dopo 2026-05-07, vede entro 3 secondi la pagina `/games/6db3e01e-...` con dati Nanolith caricati (titolo, publisher, anno, players, playtime).

**Measurable**:
- HTTP `GET /api/v1/games/{id}` → 200 OK con body matching DB record
- Frontend: pagina ha `data-testid="game-detail-loaded"` o equivalente entro 3s

```gherkin
Background:
  Given staging API è running con image c9e997c77+ (post-fix S3)
  And badsworm@gmail.com ha PasswordHash valido (PBKDF2 v1.210000.*)
  And game Nanolith esiste con id 6db3e01e-0b21-414c-8e3b-a899782feb40

Scenario: Login app + game detail load
  Given Aaron è su https://meepleai.app
  And ha cookie CF_AppSession valido (passato CF Access via Google SSO)
  When inserisce badsworm@gmail.com / TestNanolith2026!
  Then ottiene Set-Cookie meepleai_session
  And /api/v1/auth/me risponde 200 con role=superadmin
  When naviga a /games/6db3e01e-0b21-414c-8e3b-a899782feb40
  Then la pagina rendere il MeepleCard hero del game entro 3s
  And il titolo è "Nanolith"
  And nessun nuovo v2 component è stato aggiunto sotto apps/web/src/components/v2/
```

---

### G2 — KB indexing ready (entrambi PDF Ready)

**SMART**: Entrambi i PDF Nanolith (`Press Start ENG`, `Rules ENG`) sono in stato `Ready` con almeno 20 chunks ciascuno entro 5 min dal completamento dell'hot-fix S3.

**Measurable**:
```sql
SELECT pd.processing_state, COUNT(tc.*) AS chunks
FROM pdf_documents pd
LEFT JOIN text_chunks tc ON tc."PdfDocumentId" = pd."Id"
WHERE pd."SharedGameId" = '94e99e38-1a5a-499c-89a9-2ea66173f63e'
GROUP BY pd."Id", pd.processing_state;
-- expected: 2 rows, both state='Ready', chunks ≥ 20
```

```gherkin
Background:
  Given hot-fix S3 retrieve è applicato sul container API staging
  And bucket R2 ha entrambi i path key per Rules.pdf (gameId-based + pdfId-based, copia manuale per workaround)

Scenario: Press Start ENG indicizzato (no-op)
  Given PDF "Nanolith Press Start ENG.pdf" id 808b83ec-... è già processato
  Then nessun ulteriore action — già state='Ready' con 29 chunks

Scenario: Rules ENG indicizzato post hot-fix
  Given PDF "Nanolith Rules ENG.pdf" id b680bc20-... è in stato "Extracting" stuck
  When applico SQL UPDATE pdf_documents SET processing_state='Failed', "RetryCount"=0
  And POST /api/v1/documents/b680bc20-.../retry con cookie session badsworm
  Then entro 5 min, processing_state diventa 'Ready'
  And text_chunks count per quel pdfId ≥ 20
  And nessun "PostgresException 42703 column p.GameId does not exist" nei log
```

---

### G3 — Agent presence + KB sources linked (Tutor + Arbitro)

**SMART**: Esistono **2** `AgentDefinition` con `IsActive=true`, `Status="Published"`, collegati a SharedGameId `94e99e38-...`:

- `Tutor Nanolith` (`Typology="Tutor"`) con sources: Press Start (priority 1) + Rules (priority 2)
- `Arbitro Nanolith` (`Typology="Arbitro"`) con sources: Rules (priority 1) + Press Start (priority 2)

**Measurable**:
```sql
SELECT a."Id", a."Name", a."Typology", a."IsActive", a."Status",
       COUNT(s.*) AS source_count
FROM agent_definitions a
LEFT JOIN agent_kb_sources s ON s."AgentId" = a."Id"
WHERE a."SharedGameId" = '94e99e38-1a5a-499c-89a9-2ea66173f63e'
  AND a."Typology" IN ('Tutor', 'Arbitro')
GROUP BY a."Id";
-- expected: 2 rows, both IsActive=t Status='Published' source_count=2
```

```gherkin
Scenario: Tutor agent creato e linkato ai 2 KB
  Given KB Nanolith ha 2 PDF Ready (G2)
  When creo agent Tutor via API admin (POST /api/v1/agents) o SQL direct
  And linko Press Start come source priority=1
  And linko Rules come source priority=2
  Then SELECT su agent_definitions ritorna 1 riga Tutor
  And IsActive=true e Status='Published'
  And agent_kb_sources count = 2 con priorità 1+2

Scenario: Arbitro agent creato con priorità inversa
  Given KB Nanolith ha 2 PDF Ready (G2)
  When creo agent Arbitro via API admin
  And linko Rules come source priority=1
  And linko Press Start come source priority=2
  Then SELECT ritorna 1 riga Arbitro
  And source_count=2 con priorità 1+2

Scenario: Agent picker visibile in chat
  Given 2 agent Nanolith attivi (Tutor + Arbitro)
  When Aaron apre /games/6db3e01e-.../chat o equivalente
  Then la UI mostra AgentSelector con 2 opzioni
  And l'agent default è Tutor (per typology guida)
```

---

### G4 — Chat Q&A streaming + citation funzionante

**SMART**: Domanda di test ("Come si imposta il gioco per 2 giocatori?") tramite agent Tutor restituisce risposta SSE entro 30s con: ≥1 evento `Token` (type=7), ≥1 citation con reference al PDF Press Start, evento `Complete` (type=4) finale. Domanda specifica regole ("Quante azioni per turno?") via agent Arbitro restituisce citation Rules.

**Measurable**:
- `curl POST /api/v1/agents/qa/stream` con cookie sessione, parse eventi SSE
- Browser: `ChatThreadView` mostra typing indicator amber (validazione PR-B fix), poi token streaming, poi `RuleSourceCard` con citation visibile

```gherkin
Background:
  Given KB Nanolith Ready (G2)
  And 2 agent attivi con sources linkati (G3)
  And Aaron è loggato su browser (G1)

Scenario: Q&A streaming setup question via Tutor
  Given Aaron è su /games/6db3e01e-... con chat AI aperta su agent Tutor
  When invia domanda "Come si imposta il gioco per 2 giocatori?"
  Then user bubble appare immediatamente (amber, right-aligned)
  And dopo ≤2s, assistant placeholder bubble mostra typing indicator amber (validate PR-B #799)
  And entro 30s, primo evento Token (type=7) arriva via SSE
  And token si accumulano nel bubble assistente
  And risposta finale contiene riferimento a "tutorial" o "Press Start"
  And almeno 1 RuleSourceCard citation è visibile (snippet + page number)
  And risposta è coerente con tutorial PDF (Aaron ground truth manuale)

Scenario: Q&A streaming rules question via Arbitro
  Given Rules ENG Ready (G2)
  When Aaron switcha agent picker da Tutor ad Arbitro
  And invia "Quante azioni può fare un giocatore in un turno?"
  Then risposta cita primariamente Rules ENG
  And almeno 1 citation ha source="Nanolith Rules ENG.pdf"
  And risposta semantica corretta (Aaron ground truth)

Scenario: Out-of-scope question gracefully handled
  Given chat AI aperta
  When Aaron invia "Qual è il meteo a Roma?"
  Then risposta contiene disclaimer tipo "non posso rispondere fuori contesto del gioco"
  And nessuna citation hallucinated
```

---

### G5 — UI consistency (riuso v2 `done`, no nuovi component)

**SMART**: Le pagine `/games/{id}`, `/games/{id}/knowledge-base`, `/agents/{id}` usano componenti v2 da `done` rows v2-migration-matrix; **0 nuovi v2 component creati** durante questo lavoro; A11y E2E job rimane `continue-on-error: true` (no regressioni count).

**Measurable**:
- `git diff --stat main-dev..HEAD apps/web/src/components/v2/` → 0 file aggiunti/modificati nei nuovi v2
- Manual audit visivo: aprire le 3 pagine + screenshot, confronto con mockup `sp4-*.html`
- A11y E2E baseline: count violations rimane uguale o diminuisce, non aumenta

```gherkin
Scenario: Setup Nanolith non viola freeze SP6 v2
  Given branch di lavoro per setup Nanolith
  When commit finale è pushato
  Then `git diff --stat origin/main-dev..HEAD apps/web/src/components/v2/` è vuoto
  And nessun nuovo file sotto components/v2/

Scenario: Pagine reali "good enough" rispetto a mockup
  Given le 3 pagine /games/[id], /games/[id]/knowledge-base, /agents/[id]
  When apro ciascuna nel browser
  Then layout match concettualmente con mockup sp4-game-detail.html, sp4-kb-detail.html, sp4-agent-detail.html
  And gap visivi (se presenti) sono tracked come task follow-up post-Q3 token redesign

Scenario: A11y E2E non blocca PR
  Given freeze #808 e continue-on-error attivo (PR #810)
  When PR di setup Nanolith merge
  Then Frontend - A11y E2E può fail senza bloccare merge
  And nessuna regressione sul color-contrast count rispetto a baseline 30+ default
```

---

## 7. Implementation phases

| Fase | Durata | Output |
|------|--------|--------|
| 1 — Hot-fix S3 retrieve key | ~10 min | Container API staging patched (env override o SQL+S3 copy) |
| 2 — Re-index Rules.pdf | ~5 min + processing | `pdf_documents.processing_state='Ready'` per Rules.pdf, ≥20 chunks |
| 3 — Crea Tutor + Arbitro agents | ~20 min | 2 record `agent_definitions`, 4 record `agent_kb_sources` |
| 4 — Smoke test G4 (chat Q&A) | ~15 min | Validation manuale Aaron + log monitor |
| 5 — UI audit G5 (post-smoke) | ~20 min | Documentation gap + tracking issues |
| 6 — PR-F clean S3 fix | ~1.5h | Branch `fix/pdf-blob-storage-retrieve-key-mismatch`, code review, merge |

Totale Fase 1-5 (smoke ready): **~70 min**.

PR-F clean (Fase 6) può essere fatto in parallelo o sessione successiva (no impatto smoke).

---

## 8. Out of scope

Esplicitamente NON in scope di questo lavoro:

- **Iso-pixel match con mockup** — rinviato post-Q3 token redesign (#807 Fase 2)
- **Indicizzazione altri game** — solo Nanolith
- **Test multi-user** — solo `badsworm@gmail.com`
- **Mobile-specific UX fixes** — il bug "Avanti non risponde" su Google sign-in mobile è un debt separato (relativo a CF Access + Google OAuth che ora funziona via fallback email/password)
- **Stagger CI deploy fix** — task #74 PR-D2 (April workflow fixes) rimane separato
- **Visual regression baseline rebuild** — solo audit, no rebuild
- **3+ agent typology test** — Stratega/Narratore non testati in questo smoke

---

## 9. Risks

| Risk | Mitigation |
|------|-----------|
| Hot-fix S3 si rompe al prossimo deploy CI | Documentato in task #78; PR-F clean è priorità post-smoke |
| Rules.pdf re-index fallisce (oltre S3) | Press Start (29 chunks) basta per smoke G4 minimal; G2 può essere parzialmente verificato |
| Agent creation API non esiste / richiede UI admin | Fallback: SQL direct INSERT con random UUID + audit successivo |
| Chat Q&A risponde con allucinazioni | Aaron ha ground truth Nanolith; può flaggare citation false manualmente; non blocca smoke |
| Frontend gap vs mockup MAJOR | 🅲 fallback documentato (audit only, fix post-Q3) |

---

## 10. Acceptance (rollup)

Smoke "gold path Nanolith" è **ACCEPTABLE** quando:

- ✅ G1 (login + game load) verificato manualmente
- ✅ G2 (entrambi PDF Ready, ≥20 chunks ciascuno) verificato via SQL
- ✅ G3 (Tutor + Arbitro attivi, sources linkati) verificato via SQL
- ✅ G4 (almeno 1 successful Q&A streaming + citation) verificato manualmente
- ✅ G5 (`git diff` v2 = vuoto, A11y E2E non bloccante) verificato CI

Tutti i 5 SMART goals verificati = smoke pronto per qualunque sessione futura.

---

## 11. References

- `admin-mockups/design_files/sp4-game-detail.html` (UI ref game detail)
- `admin-mockups/design_files/sp4-kb-detail.html` (UI ref KB detail)
- `admin-mockups/design_files/sp4-agent-detail.html` (UI ref agent detail)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:334` (bug location)
- Task #78 (S3 retrieve key bug tracking)
- Issue #807 (A11y audit Q3-2026)
- Issue #808 (SP6 v2 freeze policy)
- v2-migration-matrix: `docs/for-developers/frontend/v2-migration-matrix.md`

---

**End of design.**
