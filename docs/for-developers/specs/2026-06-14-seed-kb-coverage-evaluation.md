# Seed KB Coverage Evaluation

> **Status**: DRAFT — 2026-06-14
> **Author**: spec-panel synthesis (Wiegers + Adzic + Cockburn + Crispin + Nygard)
> **Trigger**: «valutare quanti giochi vengono trovati da un giocatore dopo il seed completo con KB indicizzata»
> **Related**: SP4 seed (#1579), KB indexing pipeline (epic #2242), BGG ban (#2123, ADR-059), `make seed-index` snapshot workflow

## 1. Problema

Un developer / QA che esegue `make seed-sp4 && make seed-index` deve poter rispondere senza ambiguità a quattro domande distinte:

1. Quanti giochi vede un utente Free tier autenticato **navigando il catalogo**?
2. Quanti giochi sono **raggiungibili da ricerca testuale**?
3. Per quanti giochi la **KB è "Ready"** (PDF processato, chunk embeddings popolati, `HasKnowledgeBase=true`)?
4. Per quanti giochi l'**agente RAG produce citation valide** in risposta a una domanda canonica?

I quattro numeri sono diversi e ognuno isola un layer della pipeline (data plane / search plane / index plane / retrieval plane). Senza una specifica condivisa ogni misurazione è interpretabile in modo difforme.

## 2. Definizioni operative

| Termine | Definizione |
|---|---|
| **Seed completo** | `make seed-sp4` (modular REST, 8 giochi SP4) + `make seed-index` (re-bake snapshot KB). |
| **KB Ready** | `pdf_documents.processing_state = 'Ready'` AND `pdf_documents.is_active_for_rag = true` AND `count(pgvector_embeddings WHERE source_chunk_id = text_chunks.id) = count(text_chunks WHERE pdf_document_id = pd.id)` AND `shared_games.has_knowledge_base = true`. |
| **Free tier user** | Utente con `role='User'`, email verificata, sessione attiva. Nessun filter aggiuntivo applica sui 3 endpoint user-side. |
| **RAG citation valida** | `POST /api/v1/knowledge-base/ask` ritorna `citations.length > 0` con `relevanceScore > 0`. |

## 3. Metriche (4 totali)

Ogni metrica include: definizione formale, endpoint, scenario Adzic Given/When/Then, soglia pass/fail.

### M1 — Catalog Browse Coverage

**Definizione**: numero di giochi distinti restituiti da una paginazione completa di `GET /api/v1/catalog/games/new?limit=50` come Free tier user.

**Endpoint**: `GET /api/v1/catalog/games/new?limit=50` — `RequireAuthorization()`, soft-delete filtered, no tier gate.

**Scenario** (Adzic):
```
Given un utente "marco@meepleai.test" autenticato (Free tier)
And seed SP4 eseguito con N giochi (default 8)
When invoca GET /api/v1/catalog/games/new?limit=50
Then la risposta contiene `items.length == N` distinct gameIds
And ogni `item.id` corrisponde a una riga `shared_games WHERE is_deleted = false`
```

**Soglia**: M1 == seed game count (8/8 = 100% per SP4 default).

### M2 — Search Coverage

**Definizione**: numero di giochi seedati che restituiscono ≥ 1 hit su query "titolo letterale" via `GET /api/v1/games/search`.

**Endpoint**: `GET /api/v1/games/search?q=<title>` — ILIKE Postgres su `Title`, min 2 char, max 20 risultati split 50/50 library/catalog.

**Scenario** (Adzic):
```
Given utente Free tier autenticato
And seed SP4 con titoli [Azul, I Coloni di Catan, Wingspan, ...]
When per ogni titolo T invoca GET /api/v1/games/search?q=T
Then ogni invocazione restituisce ≥ 1 item con item.id corrispondente al gioco
And item.name match-case-insensitive T
```

**Soglia**: M2 == seed game count.
**Edge**: titoli con caratteri speciali (es. "7 Wonders Duel") devono comunque match — verifica che ILIKE gestisca correttamente il numero iniziale.

### M3 — KB Ready Coverage

**Definizione**: numero di giochi seedati con `shared_games.has_knowledge_base = true` AND ≥ 1 `pdf_documents` con `processing_state = 'Ready'` AND embedding coverage = 100%.

**Endpoint amministrativo**: `GET /api/v1/admin/kb/games` — restituisce per ogni gioco `{ status, docCount, totalChunks, latestIndexedAt }`.

**Query SQL diagnostica** (ground truth — verificata 2026-06-14 contro `meepleai_staging`):
```sql
SELECT
  sg.title,
  sg.has_knowledge_base AS kb_flag,
  COUNT(DISTINCT pd."Id") FILTER (WHERE pd.processing_state = 'Ready') AS pdfs_ready,
  COUNT(DISTINCT tc."Id") AS chunks,
  COUNT(DISTINCT pe.id) AS embeddings,
  COALESCE(MAX(vd."IndexingStatus"), 'no_vd') AS vector_status,
  CASE
    WHEN COUNT(DISTINCT pe.id) > 0 THEN 'complete'
    WHEN COUNT(DISTINCT tc."Id") > 0 AND COUNT(DISTINCT pe.id) = 0 THEN 'embeddings_missing'
    WHEN COUNT(DISTINCT tc."Id") = 0 THEN 'no_kb'
    ELSE 'partial'
  END AS status
FROM shared_games sg
LEFT JOIN pdf_documents pd ON pd.shared_game_id = sg.id AND pd.is_active_for_rag = true
LEFT JOIN text_chunks tc ON tc."PdfDocumentId" = pd."Id"
LEFT JOIN vector_documents vd ON vd."PdfDocumentId" = pd."Id"
LEFT JOIN pgvector_embeddings pe ON pe.vector_document_id = vd."Id"
WHERE sg.is_deleted = false
GROUP BY sg.id, sg.title, sg.has_knowledge_base
ORDER BY sg.title;
```

**Versione runnable**: `infra/scripts/kb-coverage-query.sql`. Esegui via:
```bash
pwsh -c "docker cp infra/scripts/kb-coverage-query.sql meepleai-postgres:/tmp/; docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -f /tmp/kb-coverage-query.sql"
```

**Schema notes** (verificate 2026-06-14 contro live `meepleai_staging`):
- `shared_games`: snake_case (`id`, `is_deleted`, `has_knowledge_base`)
- `pdf_documents`: mixed (`Id` PascalCase + `processing_state`, `is_active_for_rag`, `shared_game_id` snake_case)
- `text_chunks`: PascalCase (`Id`, `PdfDocumentId`, `SharedGameId`)
- `vector_documents`: mixed (`Id`, `PdfDocumentId`, `GameId`, `IndexingStatus` PascalCase + `shared_game_id` snake_case)
- `pgvector_embeddings`: snake_case (`id`, `vector_document_id`, `source_chunk_id` (NULLABLE), `game_id`)

**Linkage chain corretta** (verificata):
```
shared_games.id
  ← pdf_documents.shared_game_id
  ← text_chunks."PdfDocumentId"           (1:N chunks)
  ↓ pdf_documents."Id"
  ← vector_documents."PdfDocumentId"      (1:1 unique index)
  ← pgvector_embeddings.vector_document_id (1:N embeddings)
```

`pgvector_embeddings.source_chunk_id` è nullable e per snapshot pre-2026-05 risulta NULL. La relazione `embedding → chunk` è derivata via JOIN `pgvector_embeddings.vector_document_id` → `vector_documents."PdfDocumentId"` → `text_chunks."PdfDocumentId"` + `chunk_index` correlato.

**Scenario** (Adzic):
```
Given seed SP4 completo + make seed-index terminato senza errori
When eseguo la query SQL diagnostica
Then conto giochi con status='complete' AND kb_flag=true
And ogni gioco con status='complete' deve avere pdfs_ready ≥ 1
And il drift counter Prometheus meepleai.pdf.indexed.no.kb.flag.total deve essere 0
```

**Soglia attesa** (con seed SP4 baseline, pre-gap-fix):
- 4 happy-path: Azul (2 PDF), Catan, Wingspan, Brass (in-progress fixture)
- 1 failure-by-design: Gloomhaven (`_targetState: failed`)
- 3 senza PDF: Ark Nova, Spirit Island, 7 Wonders Duel → KB no_kb

**Soglia attesa post gap-fix**: 7/8 complete (Gloomhaven resta failed by design).

### M4 — RAG Citation Coverage

**Definizione**: numero di giochi con `has_knowledge_base=true` per cui `POST /api/v1/knowledge-base/ask { gameId, question: "<canonica>" }` restituisce `citations.length > 0`.

**Endpoint**: `POST /api/v1/knowledge-base/ask` — `RequireSession()`, `NullPricingEngine` (no quota gate), vector + keyword hybrid search.

**Domanda canonica**: `"Come funziona questo gioco? Quali sono le regole base?"` (lingua italiana, neutrale rispetto al gioco, attiva sia recall vettoriale sia keyword).

**Scenario** (Adzic):
```
Given utente Free tier autenticato
And gioco G con has_knowledge_base = true
When invoca POST /api/v1/knowledge-base/ask con gameId=G, question="Come funziona questo gioco?"
Then risposta contiene citations.length ≥ 1
And ogni citation ha relevanceScore > 0 AND documentId riconducibile a pdf_documents.id collegato a G
And response time < 30s (soglia operativa, non funzionale)
```

**Soglia**: M4 ≥ M3 - 1 (tolleranza di 1 falso negativo per query non-cooperative).
**Pass strict**: M4 == M3.

## 4. Inventario atteso post-seed (SP4 dopo Q1-Q4 closure)

13 giochi totali (8 baseline + 5 nuovi), tutti happy path indexed:

| # | Game | Slug | M1 catalog | M2 search | M3 KB Ready | M4 RAG cite | Note |
|---|------|------|:---:|:---:|:---:|:---:|---|
| 1 | Azul | azul | ✅ | ✅ | ✅ (2 PDF ITA+ENG) | ✅ | Happy path completo |
| 2 | Catan | catan | ✅ | ✅ | ✅ (1 PDF) | ✅ | Happy path (Q4: canonical EN; IT IT via translation service follow-up) |
| 3 | Wingspan | wingspan | ✅ | ✅ | ✅ (1 PDF) | ✅ | Happy path |
| 4 | Brass: Birmingham | brass | ✅ | ✅ | ✅ (1 PDF) | ✅ | Q1: fixture `_processing` rimossa |
| 5 | Gloomhaven | gloomhaven | ✅ | ✅ | ✅ (1 PDF, 51 MB) | ✅ | Q2: fixture `failed` rimossa, ora indicizzato |
| 6 | Ark Nova | arknova | ✅ | ✅ | ✅ (1 PDF) | ✅ | Q3 chiusura PDF orphan |
| 7 | Spirit Island | spirit | ✅ | ✅ | ✅ (1 PDF, 55 MB) | ✅ | Q3 chiusura PDF orphan |
| 8 | 7 Wonders Duel | 7wonders | ✅ | ✅ | ✅ (1 PDF) | ✅ | Q3 chiusura PDF orphan |
| 9 | Codenames | codenames | ✅ | ✅ | ✅ (1 PDF) | ✅ | Catalog expansion |
| 10 | Carcassonne | carcassonne | ✅ | ✅ | ✅ (1 PDF) | ✅ | Catalog expansion |
| 11 | Ticket to Ride | ticket | ✅ | ✅ | ✅ (1 PDF, 177 MB) | ✅ | Catalog expansion (PDF grande) |
| 12 | Pandemic | pandemic | ✅ | ✅ | ✅ (1 PDF) | ✅ | Catalog expansion |
| 13 | Terraforming Mars | terraforming | ✅ | ✅ | ✅ (1 PDF, 39 MB) | ✅ | Catalog expansion |

**Baseline atteso post Q1-Q4**: M1=13, M2=13, M3=13, M4≥12 (tolleranza 1 per query non-cooperative).
**Pass strict**: M1=M2=M3=M4=13.

**Total assets seedati**:
- 13 game records
- 14 kbDocs (Azul ha 2 ITA+ENG, gli altri 12 hanno 1 PDF ciascuno)
- 13 agenti (1 per gioco + 0 universal duplicato)
- 38 library entries totali (Q3 distribuzione: marco 12, sara 8, andrea 6, luca 5, giulia 5; era 19 prima)
- 5 events (era 4, +1 "Strategici di gennaio")

## 5. Gap analysis

### 5.1 PDF orphan (mappa già disponibili in `data/rulebook/`)

3 giochi seedati senza `kbDoc` entry, ma PDF source presenti:

| Game | sourcePdf | Size | Azione |
|---|---|---|---|
| Ark Nova | `ark-nova_rulebook.pdf` | ~9.5 MB | Aggiungi `kb-arknova` a `data.json` |
| Spirit Island | `spirit-island_rulebook.pdf` | ~55 MB | Aggiungi `kb-spirit` a `data.json` |
| 7 Wonders Duel | `7-wonders-duel_rulebook.pdf` | ~6.5 MB | Aggiungi `kb-7wonders` a `data.json` |

**Effort**: 1 edit `data.json` + re-run `make seed-sp4` (~30 min).
**Outcome**: M3 e M4 passano da 4/8 a 7/8.

### 5.2 Catalog expansion (giochi popolari mancanti, PDF disponibili)

136 PDF totali in `data/rulebook/` (gitignored), inclusi giochi mainstream non seedati:

| Game | Publisher | sourcePdf | Note |
|---|---|---|---|
| Codenames | Czech Games | `codenames_rulebook.pdf` | Party game, alta brand awareness |
| Carcassonne | Hans im Glück | `carcassone_rulebook.pdf` | Classico gateway |
| Ticket to Ride | Days of Wonder | `ticket-to-ride_rulebook.pdf` | Family gateway |
| Pandemic | Z-Man Games | `pandemic_rulebook.pdf` | Cooperative classic |
| Terraforming Mars | Stronghold | `terraforming-mars_rulebook.pdf` | Heavy euro |

**Attribute sourcing**: NO BGG (ban #2123/ADR-059). Usa: publisher pages, Wikipedia, neutrale.
**Effort**: ~2-3h (5 game record + 5 kbDoc + 5 agent + library/event references).
**Outcome**: catalogo Free tier passa da 8 a 13.

### 5.3 Gap di observability

Nessun endpoint risponde direttamente a «X seedati su Y hanno KB ready». Compositore via:
- `GET /api/v1/admin/kb/games` (per-game status)
- Query SQL diagnostica (cf. M3)
- Script `evaluate-kb-coverage.sh` (vedi sezione 6)

Follow-up opzionale (out of scope MVP): endpoint `/api/v1/admin/seed/coverage-evaluation` che restituisce JSON aggregato `{ total_seeded, with_pdf, kb_ready, search_hits, coverage_pct }`.

## 6. Script di esecuzione

**Path**: `infra/scripts/evaluate-kb-coverage.sh`

**Dipende da**: `infra/scripts/seed-sp4/lib/common.sh` (auth + curl helpers, riutilizzati).

**Usage**:
```bash
# Run after `make seed-sp4 && make seed-index`
cd infra/scripts
./evaluate-kb-coverage.sh              # Table output + JSON in /tmp/
./evaluate-kb-coverage.sh --json       # JSON only
./evaluate-kb-coverage.sh --target staging  # Run against staging
```

**Output table**:
```
Game                  | M1 cat | M2 srch | M3 KB | M4 RAG | PDF | Chunks | Embedding
─────────────────────┼────────┼─────────┼───────┼────────┼─────┼────────┼──────────
Azul                  |   ✅   |   ✅    |  ✅   |   ✅   |  2  |   84   |   100%
I Coloni di Catan     |   ✅   |   ✅    |  ✅   |   ✅   |  1  |   42   |   100%
...
─────────────────────┴────────┴─────────┴───────┴────────┴─────┴────────┴──────────
TOTALS: M1=8/8 M2=8/8 M3=4/8 M4=4/8
```

**Exit codes**:
- `0`: M1=M2=catalog size AND M3≥expected_kb AND M4≥M3-1
- `1`: degraded (any metric below expected, but auth/DB OK)
- `2`: critical (API unreachable, auth failed, DB unreachable)

## 7. User flow verification (manuale, post-seed)

Da eseguire dopo `make seed-sp4 && make seed-index` con `make dev` attivo. Tutti i flow come `marco@meepleai.test` (Free tier).

### Flow 1: filter library by KB indexed → add game

**Mockup ref**: `admin-mockups/design_files/sp4-library-desktop.html` (route `/library`).

```
Given marco autenticato, naviga /library
When seleziona filtro "Solo giochi con KB indicizzata"
Then la lista mostra solo i giochi con shared_games.has_knowledge_base = true
And per ogni gioco è visibile un badge "KB" (cf. CLAUDE.md memory: PR #2307, Discover KB badge)
When clicca "Aggiungi alla libreria" su un gioco filtrato
Then user_library_entries riceve nuova riga AND la libreria personale si aggiorna
```

**Comparison**: aprire mockup `sp4-library-desktop.html` e route live `/library` side-by-side. Annotare divergenze (token, layout, IA).

### Flow 2: start chat with game agent

**Mockup ref**: `admin-mockups/design_files/sp4-game-chat-tab.html` (component embedded in `/library/[gameId]/agent`, `/games/[id]`).

```
Given marco con Azul nella libreria
When naviga /library/{azul-id}/agent
Then vede chat thread vuoto con prompt input
When invia "Come funziona Azul?"
Then risposta agent contiene citations (verifica M4)
And citation links portano al PDF viewer overlay (sp4-citation-pdf-viewer.html)
```

**Edge**: testare con gioco SENZA KB (es. Gloomhaven failed) → UI deve mostrare stato degraded chiaro.

### Flow 3: start session

**Mockup ref**: `admin-mockups/design_files/sp4-session-catan-live.html` (route `/sessions/[id]/live`).

```
Given marco con Catan nella libreria
When naviga /library/{catan-id} e clicca "Avvia sessione"
Then redirige a /sessions/{newSessionId}/live con _targetState=Created
When clicca "Avvia partita live"
Then session.StartedAt = now, _targetState=InProgress
And asse A invariante #10 verificata: max 1 live per GameNightEvent
```

**Edge**: verifica errore 409 + `X-Warning-Code` se utente tenta seconda sessione live sullo stesso gioco (invariante #10).

## 8. Acceptance criteria (per chiusura issue)

- [ ] Spec doc shipped in `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`
- [ ] Script `infra/scripts/evaluate-kb-coverage.sh` eseguibile, idempotente, exit codes documentati
- [ ] Fix PDF orphan: `data.json` con 3 nuovi `kbDocs` + 3 nuovi `agents` per Ark Nova / Spirit Island / 7 Wonders Duel
- [ ] Catalog expansion: 5 nuovi giochi (Codenames, Carcassonne, Ticket to Ride, Pandemic, Terraforming Mars) + KB + agenti
- [ ] Local run `make seed-sp4 && make seed-index` verde con M1=M2=13, M3=12, M4≥11 (Gloomhaven excluded by design)
- [ ] Verifica E2E 3 flow manuale con confronto mockup side-by-side, divergenze documentate

## 9. Q1-Q4 closure (lockate 2026-06-14)

- **Q1 — RESOLVED**: rimossa fixture `_processing: true` su `kb-brass` → happy path indexed. Brass ora atteso `complete` in M3/M4.
- **Q2 — RESOLVED**: rimossa fixture `_targetState: "failed"` + `_note` su `kb-gloomhaven` → happy path indexed. Gloomhaven ora atteso `complete`. Filename rinominato `gloomhaven-scenarios.pdf` → `gloomhaven-rules.pdf` per coerenza nomenclatura.
- **Q3 — RESOLVED**: i 5 nuovi giochi inseriti in `library` (distribuzione: codenames/carcassonne/ticket/pandemic 3 utenti, terraforming 2 utenti) e `events` (1 nuovo evento `e-strategici` "Strategici di gennaio" con terraforming/pandemic/carcassonne). `playRecords` invariato (favoriteGameSlug resta sui giochi originali, soglia conservativa).
- **Q4 — RESOLVED via canonical EN + translation deferred**: titolo del seed Catan rinominato da `I Coloni di Catan` → `Catan` (canonical EN). Risolve il dual-row anomaly su snapshot DB.
  - **Translation service**: esiste backend (`Api.Infrastructure.Translation.IGenericTranslationService` + `OpenRouterTranslationService`, modelli DeepSeek V3 + Claude Sonnet 4.5) ma è wired SOLO per traduzione delle risposte LLM in `AskQuestionQueryHandler`, NON per i game titles del catalogo.
  - **Schema attuale**: `shared_games.title` è una singola colonna (no `shared_game_translations` table, no `title_it`/`title_en`).
  - **Frontend i18n**: react-intl attivo per UI labels (`apps/web/src/locales/it.json`), ma entity titles letti direttamente da `SharedGameDto.Title`.
  - **Follow-up dedicato** (out of scope MVP): aprire issue per "Wire translation service to catalog entities" — opzioni:
    - (A) Table-based: migration `shared_game_translations(shared_game_id, locale, title, description)` + BE `GameTitleResolver` + FE `useGameTitle()` hook.
    - (B) On-demand: `IGenericTranslationService` con cache Redis 30gg per titoli; trigger asincrono al fetch quando `locale=it`.
  - **Mitigazione interim**: il mapping curated UI in `apps/web/src/locales/it.json` può fornire override per i 13 titoli del seed (chiave `games.<slug>` → titolo IT), senza modifiche BE.

## 9.1 Baseline misurato su snapshot DB (2026-06-14)

Verifica contro `meepleai_staging` (snapshot precaricato, 160 giochi totali, 125 con embeddings) filtrato sui 13 titoli target SP4:

| Game | kb_flag | pdfs_ready | chunks | embeddings | vector_status | status |
|---|---|---:|---:|---:|---|---|
| 7 Wonders Duel | ✅ | 1 | 41 | 41 | completed | complete |
| Ark Nova | ✅ | 2 | 94 | 94 | completed | complete |
| Azul | ✅ | 1 | 46 | 46 | completed | complete |
| Brass: Birmingham | ✅ | 1 | 47 | 47 | completed | complete |
| Carcassonne | ✅ | 1 | 34 | 34 | completed | complete |
| Codenames | ✅ | 1 | 21 | 21 | completed | complete |
| Gloomhaven | ❌ | 0 | 0 | 0 | no_vd | no_kb |
| I Coloni di Catan | ❌ | 0 | 0 | 0 | no_vd | no_kb (vedi Q4) |
| Pandemic | ✅ | 2 | 86 | 86 | completed | complete |
| Spirit Island | ✅ | 1 | 126 | 126 | completed | complete |
| Terraforming Mars | ✅ | 1 | 214 | 214 | completed | complete |
| Ticket to Ride | ✅ | 1 | 36 | 36 | completed | complete |
| Wingspan | ✅ | 2 | 48 | 48 | completed | complete |

**Misure**: M3 = **11/13 complete** sul snapshot. Catan IT richiede `Catan` EN alias (Q4) per arrivare a 12/13.

## 10. Riferimenti

- ADR-059 Catalog Seed Legal Posture — `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`
- ADR-060 Live Session Persistence — `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md`
- SP4 seed dataset — PR #1579
- KB indexing epic — #2242 + sub-issues #2244, #2263
- Snapshot workflow — `docs/for-developers/workflows/snapshot-seed-workflow.md`
- MeepleCard design tokens — `docs/for-developers/frontend/meeple-card-design-tokens.md`
- Mockup index — `admin-mockups/MOCKUPS_INDEX.md`
