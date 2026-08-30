# Happy Path — A3 · Catalogo condiviso (admin)

> Catalogo scenari happy-path per l'area **A3 — Catalogo condiviso admin**: gestione degli shared game (CRUD completo create/edit/delete + publish/archive), categorie (CRUD), fasi gioco (upsert lista), import da PDF, wizard di contenuto, seeding/enrichment, RAG-setup per gioco, e i tool games (`AdminGameWizard`, phases, agent-test, processing) + catalog-ingestion / seed-queue.
>
> Formato e legenda: vedi [`_TEMPLATE.md`](./_TEMPLATE.md). Solo **happy path**. Osservabili **strutturali** (elemento a schermo, navigazione, badge/toast, empty-state) — mai testo letterale generato da LLM. Ambiente: `make dev` (full stack; serve l'AI per RAG-setup, wizard import, agent-test, suggerimenti fasi) + `make seed-sp4`.

## Intestazione

| Voce | Valore |
|------|--------|
| **Area** | A3 — Catalogo condiviso (admin) |
| **Utente** | **admin** (da `infra/secrets/admin.secret` — `ADMIN_EMAIL`/`ADMIN_PASSWORD`) per tutti gli scenari. L'intero route group `admin/(dashboard)` è gated dal layout server-side + `RequireAdminSessionFilter` lato BE. |
| **Prereq dati** | `make seed-sp4` — popola gli 8+ SP4 SharedGames già in stato **Published** (Azul, Catan, Wingspan, Brass: Birmingham, Gloomhaven, Ark Nova, …) con PDF regole indicizzati (`kb-azul-ita`, `kb-catan`, …) e agenti RAG collegati. Il seed (`infra/scripts/seed-sp4/20-games.sh`) automatizza esattamente il percorso `POST /admin/shared-games` + publish che gli scenari **Flow** esercitano a mano. |
| **Dati creati (marcatore)** | Ogni entità creata da uno scenario Flow usa `HP-TEST-<data>` nel titolo/nome (es. `HP-TEST-2026-07-10 Azul Copia`). Additivo: gli scenari **creano** entità nuove, non mutano i seed Published. Gli scenari di **ciclo CRUD** (A3-18…A3-20) creano ed **eliminano** la propria entità HP-TEST nello stesso ciclo, riportando lo stato al punto di partenza. |
| **Endpoint base** | `POST /api/v1/admin/shared-games` (create draft) · `PUT /api/v1/admin/shared-games/{id}` (update) · `POST /api/v1/admin/shared-games/{id}/publish` (publish) · `POST /api/v1/admin/shared-games/{id}/archive` (archive) · `DELETE /api/v1/admin/shared-games/{id}` (delete, admin-immediato) · categorie `POST/PUT/DELETE /api/v1/admin/categories[/{id}]` · fasi `PUT /api/v1/games/{gameId}/phase-templates` (upsert lista) · client FE `api.sharedGames.*`, `api.admin.*`, `api.games.*`, `admin-categories.ts`. Nota: il seed usa `POST …/quick-publish` (shortcut idempotente server-side); la superficie FE **non** espone `quick-publish` — pubblica via l'azione "Pubblica" della griglia All Games (→ `…/publish`). |

### Nota su quick-publish (seed) vs publish (browser)

Il flusso che il seed automatizza è **create draft → quick-publish**. Nel browser il percorso manuale equivalente è:

1. `/admin/shared-games/new` → form → `api.sharedGames.create()` → il gioco nasce in **Draft** e si viene reindirizzati al dettaglio.
2. `/admin/shared-games/all` → sulla card del gioco, quick-action **"Pubblica"** (`📤`) → `api.sharedGames.publish(id)` → badge card passa a **Pubblicato** + toast + contatore "Pubblicati" incrementa.

Gli scenari A3-02 (create) e A3-03b (publish) coprono insieme il percorso che `quick-publish` sintetizza in un colpo solo.

---

## Matrice di copertura

16 route (glob `admin/(dashboard)/shared-games/**` + `games/**` + `catalog-ingestion` + `catalog/seed-queue`, verificato 2026-07-10).

| Route | Liv. atteso | Scenario/i | Note |
|-------|-------------|------------|------|
| `admin/(dashboard)/shared-games` | Smoke | A3-01 | `redirect('/admin/shared-games/all')` — `smoke-aggregato` in A3-01 (l'osservabile è l'atterraggio su All Games) |
| `admin/(dashboard)/shared-games/all` | Smoke / Flow | A3-01, A3-03b, A3-18 | Griglia catalogo + filtri + widget "Recently Processed"; azione Pubblica in A3-03b; card "Elimina" (delete CRUD) in A3-18 |
| `admin/(dashboard)/shared-games/new` | Flow | A3-02, A3-18 | Form crea shared game (Draft) → redirect al dettaglio; A3-18 apre il ciclo CRUD |
| `admin/(dashboard)/shared-games/[id]` | Smoke / Flow | A3-03 (smoke tabs), A3-04 (Flow link agent), A3-18 (Flow edit drawer) | 3 tab: Details / Documents / Agent; "Edit Game" → drawer "Modifica gioco" (A3-18) |
| `admin/(dashboard)/shared-games/[id]/rag-setup` | Flow | A3-10 | Dashboard 4-pannelli: upload → embedding → agent → chat |
| `admin/(dashboard)/shared-games/[id]/knowledge-base` | Smoke | A3-11 | Documenti KB indicizzati + impostazioni KB per gioco |
| `admin/(dashboard)/shared-games/import` | Flow | A3-05 | Wizard 5-step import da PDF (upload → metadati → anteprima → saga → RAG test) |
| `admin/(dashboard)/shared-games/wizard` | Flow | A3-06 | Catalog Content Wizard 5-step (select game → upload PDF → review → agent → RAG test) |
| `admin/(dashboard)/shared-games/seeding` | Flow / Smoke | A3-07 (Flow enrich), A3-08 (Smoke dashboard) | Tabella seeding + enrichment BGG batch |
| `admin/(dashboard)/shared-games/categories` | Smoke / Flow | A3-09 (smoke), A3-19 (CRUD) | Tabella categorie giochi; ciclo Create/Edit/Delete categoria in A3-19 |
| `admin/(dashboard)/games/new` | Flow | A3-12 | `AdminGameWizard` 4-step (BGG search → details → PDF → launch) |
| `admin/(dashboard)/games/[gameId]/phases` | Flow | A3-13, A3-20 | Configurazione fasi turno (add/reorder/AI-suggest/save); ciclo add→edit→remove+save in A3-20 |
| `admin/(dashboard)/games/[gameId]/agent/test` | Flow | A3-14 | Test agente RAG: Auto Test Suite + Interactive Chat |
| `admin/(dashboard)/games/[gameId]/processing` | Smoke | A3-15 | Monitor pipeline PDF via SSE (Pending → … → Ready) |
| `admin/(dashboard)/catalog-ingestion` | Smoke | A3-16 | Dashboard BGG sync: hero + timeline + queue/failed |
| `admin/(dashboard)/catalog/seed-queue` | Smoke | A3-17 | Coda seed (Wikidata + BGG): input + queue + log SSE |

**Nessun buco**: tutte le 16 route mappano ad ≥1 scenario o sono `smoke-aggregato` con motivo.

**Delta vs `_coverage-map.md`**: la mappa globale elenca 16 righe A3 ma include `shared-games/[id]/kb` — questa route **non esiste** (glob 2026-07-10 mostra solo `[id]/knowledge-base`). Il conteggio effettivo resta 16 perché la mappa non elenca `catalog-ingestion` né `catalog/seed-queue` nella colonna route mentre il glob sì. A-FINAL riconcili: route reali = { shared-games ×10, games ×4, catalog-ingestion, catalog/seed-queue } = 16.

---

## Scenari

### A3-01 — Lista catalogo condiviso (All Games)

```gherkin
Scenario A3-01 [Smoke]: La griglia del catalogo condiviso carica con i giochi seed
  Given sono loggato come admin
    And il seed ha creato ≥8 shared game Published (Azul, Catan, Wingspan, …)
  When apro /admin/shared-games
  Then vengo reindirizzato a /admin/shared-games/all
    And vedo l'header "Tutti i Giochi"
    And vedo il widget "Recently Processed" e la barra filtri (search/category/status/players)
    And la griglia mostra le card dei giochi seed con badge di stato (es. "Pubblicato")
  Osservabile ✅: URL finale /admin/shared-games/all + heading "Tutti i Giochi" + ≥1 MeepleCard gioco (es. card "Azul") con badge stato, nessun errore Console/Network
  Route: /admin/shared-games (redirect) · /admin/shared-games/all
  Utente: admin
```

### A3-02 — Crea uno shared game (Draft) manualmente

```gherkin
Scenario A3-02 [Flow]: Creazione di un nuovo shared game marcato HP-TEST
  Given sono loggato come admin
    And sono su /admin/shared-games/new
  When compilo Title = "HP-TEST-2026-07-10 Azul Copia"
    And compilo Description = "Gioco di test happy-path"
    And lascio i default validi (Year, Min/Max Players, Playing Time, Min Age)
    And clicco "Create Game"
  Then il gioco viene creato in stato Draft (POST /api/v1/admin/shared-games)
    And vengo reindirizzato a /admin/shared-games/{nuovoId}
    And nell'header del dettaglio vedo il titolo "HP-TEST-2026-07-10 Azul Copia" con badge "Draft"
  Osservabile ✅: navigazione a /admin/shared-games/{id} + heading col titolo HP-TEST + badge "Draft" + POST shared-games 200/201 (Network)
  Route: /admin/shared-games/new → /admin/shared-games/[id]
  Utente: admin
```

### A3-03 — Dettaglio shared game: tab Details / Documents / Agent

```gherkin
Scenario A3-03 [Smoke]: Il dettaglio di un gioco seed mostra i tre tab popolati
  Given sono loggato come admin
    And esiste il gioco seed "Azul" (Published, con PDF indicizzato e agente RAG collegato)
  When apro /admin/shared-games/{azulId}
  Then vedo l'header con "Azul", badge "Published" e i pulsanti "Knowledge Base" / "RAG Setup" / "Edit Game"
    And il tab "Details" mostra Description + Game Information (Players, Playing Time, Min Age)
    And clicco il tab "Documents" e vedo ≥1 documento con stato di indicizzazione
    And clicco il tab "Agent" e vedo la card "Linked Agent" con l'agente collegato (spunta ✓ sul tab)
  Osservabile ✅: heading "Azul" + badge "Published" + tab Details con dati gioco + tab Documents con ≥1 documento + tab Agent con agente collegato, nessun errore Console/Network
  Route: /admin/shared-games/[id]
  Utente: admin
```

### A3-03b — Quick-publish di un gioco Draft dalla griglia

```gherkin
Scenario A3-03b [Flow]: Pubblicazione di un gioco Draft via azione "Pubblica"
  Given sono loggato come admin
    And ho creato (A3-02) il gioco Draft "HP-TEST-2026-07-10 Azul Copia"
    And sono su /admin/shared-games/all con la card del gioco visibile (filtro status = Draft se necessario)
  When apro le quick-action della card e clicco "Pubblica" (📤)
  Then la richiesta POST /api/v1/admin/shared-games/{id}/publish ha successo
    And compare un toast di conferma pubblicazione
    And il badge della card passa da "Draft" a "Pubblicato"
    And il contatore "Pubblicati" in cima alla griglia si aggiorna
  Osservabile ✅: toast pubblicazione + badge card "Pubblicato" + POST …/publish 200/204 (Network)
  Route: /admin/shared-games/all
  Utente: admin
  Nota: equivalente browser del `quick-publish` che il seed (20-games.sh) esegue lato server.
```

### A3-04 — Collega un agente AI a un gioco

```gherkin
Scenario A3-04 [Flow]: Link di un agent definition al gioco dal tab Agent
  Given sono loggato come admin
    And ho un gioco senza agente collegato (es. il gioco HP-TEST creato in A3-02, oppure un gioco seed con agente scollegato)
    And esiste ≥1 agent definition attiva (seed agents)
  When apro /admin/shared-games/{id}, seleziono il tab "Agent"
    And nella card "Link an Agent" scelgo un agente dal Select
    And clicco "Link"
  Then la richiesta POST /api/v1/admin/shared-games/{id}/link-agent/{agentId} ha successo
    And la card "Linked Agent" mostra ora nome + tipo dell'agente collegato
    And il tab "Agent" mostra la spunta ✓
  Osservabile ✅: card "Linked Agent" popolata (nome agente) + spunta ✓ sul tab Agent + POST link-agent 200 (Network)
  Route: /admin/shared-games/[id]
  Utente: admin
```

### A3-05 — Importa un gioco da PDF (wizard 5-step)

```gherkin
Scenario A3-05 [Flow]: Import di un gioco da regolamento PDF end-to-end
  Given sono loggato come admin
    And ho a disposizione un PDF di regole (es. un rulebook seed o un piccolo PDF di test) ≤150 MB
  When apro /admin/shared-games/import
    And nello Step 1 carico il PDF e attendo il completamento upload
    And clicco "Avanti →" allo Step 2 e verifico/correggo i metadati estratti dall'IA
    And procedo allo Step 3 (Anteprima) e clicco "Crea gioco →"
    And attendo lo Step 4 (saga ImportGameFromPdfCommand) fino al completamento auto-avanzato
    And nello Step 5 (RAG Test) invio una domanda sulle regole importate
  Then lo stepper avanza 1→2→3→4→5 con la progress bar che raggiunge 100%
    And al termine dello Step 4 il gioco risulta creato (avanzamento automatico allo Step 5)
    And nello Step 5 ricevo una risposta dal RAG agent sul documento importato
  Osservabile ✅: stepper allo Step 5 + progress 100% + risposta non-vuota nel pannello RAG Test dello Step 5, nessun errore Console/Network non atteso
  Route: /admin/shared-games/import
  Utente: admin
  Nota: la saga import + indicizzazione può richiedere alcuni minuti; osservabile = raggiungimento Step 5 + risposta presente, non tempistica.
```

### A3-06 — Wizard di contenuto catalogo (aggiungi PDF a gioco esistente)

```gherkin
Scenario A3-06 [Flow]: Aggiunta di documenti a un gioco esistente via Catalog Content Wizard
  Given sono loggato come admin
    And esiste il gioco seed "Catan"
    And ho a disposizione un PDF da caricare
  When apro /admin/shared-games/wizard
    And nello Step 1 cerco "Catan", clicco il risultato e lo seleziono
    And nello Step 2 seleziono ≥1 file PDF e clicco "Upload N file(s)"
    And nello Step 3 (Upload Complete) verifico il conteggio "Succeeded"
    And clicco "Setup Agent" per andare allo Step 4 (Agent Setup)
    And procedo allo Step 5 (RAG Test) e invio una domanda
  Then lo stepper avanza 1→2→3→4→5 (pallini arancioni con spunta sugli step completati)
    And lo Step 3 mostra ≥1 file in "Succeeded"
    And lo Step 5 mostra il pannello chat RAG utilizzabile
  Osservabile ✅: stepper allo Step 5 + Step 3 con successCount ≥1 + pannello InlineChat visibile, nessun errore Console/Network
  Route: /admin/shared-games/wizard
  Utente: admin
```

### A3-07 — Seeding: enrichment BGG di un gioco

```gherkin
Scenario A3-07 [Flow]: Enrichment di un gioco Skeleton dalla dashboard di seeding
  Given sono loggato come admin
    And sono su /admin/shared-games/seeding
    And esiste ≥1 gioco con ID esterno (bggId) in stato "Skeleton" o "Failed" (creato via seed o import)
  When seleziono la checkbox del gioco arricchibile
    And clicco "Enrich Selected (N)"
  Then la richiesta di enqueue enrichment ha successo (POST enqueue BGG enrichment)
    And compare il messaggio di feedback "Queued N game(s) for enrichment."
    And il pannello di stato coda mostra l'attività (queue attiva) e lo stato del gioco evolve (Enrichment Queued → Enriching → …)
  Osservabile ✅: messaggio "Queued N game(s) for enrichment." + badge stato del gioco che cambia (o pannello coda attivo) + POST enrichment 200 (Network)
  Route: /admin/shared-games/seeding
  Utente: admin
  Nota: se tutti i giochi seed sono già "Complete", creare prima uno Skeleton (import senza enrichment) oppure marcare lo step come blocked-env; il percorso di enqueue resta l'osservabile.
```

### A3-08 — Seeding dashboard (vista tabellare)

```gherkin
Scenario A3-08 [Smoke]: La dashboard di seeding carica con la tabella giochi
  Given sono loggato come admin
  When apro /admin/shared-games/seeding
  Then vedo l'header "Seeding & Enrichment"
    And la card "Games (N)" mostra la tabella con colonne Title, ID Ext., Data Status, Has PDF, Game Status, RAG Ready, Pipeline, Created
    And ogni riga ha un badge "Data Status" (es. Complete/Skeleton) colorato
    And filtro per status (es. "Complete") e la tabella si aggiorna
  Osservabile ✅: heading "Seeding & Enrichment" + tabella con ≥1 riga gioco e badge Data Status + effetto visibile del filtro status, nessun errore Console/Network
  Route: /admin/shared-games/seeding
  Utente: admin
```

### A3-09 — Categorie giochi

```gherkin
Scenario A3-09 [Smoke]: La pagina categorie carica la tabella delle categorie
  Given sono loggato come admin
  When apro /admin/shared-games/categories
  Then vedo l'header "Game Categories"
    And la CategoriesTable si carica (skeleton → contenuto reale, oppure empty-state legittimo se non ci sono categorie)
  Osservabile ✅: heading "Game Categories" + CategoriesTable renderizzata (righe o empty-state), nessun errore Console/Network
  Route: /admin/shared-games/categories
  Utente: admin
```

### A3-10 — RAG Setup di un gioco (dashboard 4-pannelli)

```gherkin
Scenario A3-10 [Flow]: Configurazione RAG di un gioco dalla dashboard dedicata
  Given sono loggato come admin
    And esiste il gioco seed "Wingspan" (con PDF indicizzato + agente collegato)
  When apro /admin/shared-games/{wingspanId}/rag-setup
  Then vedo l'header "RAG Setup: Wingspan" e il RagReadinessIndicator
    And il pannello sinistro mostra l'upload PDF + la lista documenti con stato "N/M pronti"
    And il pannello destro mostra Agent Setup (agente collegato) + il pannello InlineChat
  When invio una domanda nel pannello chat (agente collegato)
  Then ricevo una risposta dal RAG agent
  Osservabile ✅: heading "RAG Setup: Wingspan" + RagReadinessIndicator + lista documenti pronti + risposta non-vuota nel pannello InlineChat, nessun errore Console/Network non atteso
  Route: /admin/shared-games/[id]/rag-setup
  Utente: admin
```

### A3-11 — Knowledge Base per gioco (admin)

```gherkin
Scenario A3-11 [Smoke]: La pagina KB di un gioco mostra documenti e impostazioni
  Given sono loggato come admin
    And esiste il gioco seed "Azul" con PDF indicizzato
  When apro /admin/shared-games/{azulId}/knowledge-base
  Then vedo la sezione "Documenti Knowledge Base" con ≥1 documento indicizzato (GameKbDocuments)
    And vedo la sezione "Impostazioni KB" con i controlli override per gioco (GameKbSettings)
  Osservabile ✅: heading "Documenti Knowledge Base" + ≥1 documento KB + sezione "Impostazioni KB" renderizzata, nessun errore Console/Network
  Route: /admin/shared-games/[id]/knowledge-base
  Utente: admin
```

### A3-12 — Wizard "Add Game" (games/new, 4-step BGG→PDF→launch)

```gherkin
Scenario A3-12 [Flow]: Import di un gioco via AdminGameWizard con lancio processing
  Given sono loggato come admin
    And ho a disposizione un PDF di regole da caricare
  When apro /admin/games/new
    And nello Step "Cerca gioco" (BGG search) cerco e seleziono un gioco
    And nello Step "Game Details" confermo i dati e creo il gioco
    And nello Step "Upload PDF" carico il PDF e attendo l'upload
    And nello Step "Launch" avvio il processing
  Then vengo reindirizzato a /admin/games/{gameId}/processing?title=...
    And la pagina di processing mostra la pipeline PDF (Pending → … → Ready)
  Osservabile ✅: avanzamento stepper 1→2→3→4 + navigazione a /admin/games/{id}/processing + pipeline di processing visibile, nessun errore Console/Network non atteso
  Route: /admin/games/new → /admin/games/[gameId]/processing
  Utente: admin
  Nota: BGG search è admin-only (lecito per ADR-059 §2, path server-to-server admin). Se il PDF/AI non è disponibile, l'osservabile minimo è il raggiungimento dello Step "Launch".
```

### A3-13 — Definisci le fasi del turno di un gioco

```gherkin
Scenario A3-13 [Flow]: Configurazione e salvataggio delle fasi di un gioco
  Given sono loggato come admin
    And esiste il gioco seed "Catan"
  When apro /admin/games/{catanId}/phases
    And clicco "Aggiungi fase" e inserisco "HP-TEST Setup" come nome fase
    And aggiungo una seconda fase "HP-TEST Azioni"
    And uso le frecce su/giù per riordinare (opzionale) e clicco "Salva fasi"
  Then la richiesta di upsert phase templates ha successo (POST/PUT phase templates)
    And compare il messaggio "Fasi salvate con successo!"
  Osservabile ✅: messaggio di conferma "Fasi salvate con successo!" (role=status) + almeno le 2 righe fase compilate + chiamata upsert 200 (Network)
  Route: /admin/games/[gameId]/phases
  Utente: admin
  Nota alternativa: il pulsante "Suggerisci" genera fasi via AI dalle regole caricate — richiede full stack; l'happy-path minimo qui usa l'inserimento manuale + salvataggio.
```

### A3-14 — Testa l'agente RAG di un gioco (Auto Test / Chat)

```gherkin
Scenario A3-14 [Flow]: Verifica dell'agente RAG di un gioco dalla pagina di test
  Given sono loggato come admin
    And esiste il gioco seed "Azul" con rulebook processato e agente RAG
  When apro /admin/games/{azulId}/agent/test?title=Azul
  Then vedo l'header "Test Agent: Azul" e le due tab "Auto Test" / "Interactive Chat"
  When apro la tab "Interactive Chat" e invio una domanda sulle regole (es. "Quanti punti vale una riga completa?")
  Then ricevo una risposta dal RAG agent sul documento del gioco
  Osservabile ✅: heading "Test Agent: Azul" + due tab visibili + risposta non-vuota nella Interactive Chat, nessun errore Console/Network non atteso
  Route: /admin/games/[gameId]/agent/test
  Utente: admin
  Nota: contenuto LLM non deterministico → osservabile = presenza risposta, non testo letterale. Full stack richiesto.
```

### A3-15 — Monitor processing PDF di un gioco

```gherkin
Scenario A3-15 [Smoke]: La pagina di processing mostra la pipeline PDF
  Given sono loggato come admin
    And esiste un gioco con un PDF già processato (es. gioco seed "Azul") oppure un gameId valido
  When apro /admin/games/{gameId}/processing?title=Azul
  Then vedo il ProcessingMonitor con la pipeline a stadi (Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready)
    And per un documento già indicizzato gli stadi risultano completati (stato "Ready") oppure lo stato corrente è mostrato correttamente
  Osservabile ✅: ProcessingMonitor renderizzato con gli stadi della pipeline visibili + stato coerente (Ready per un PDF già indicizzato), nessun errore Console/Network non atteso
  Route: /admin/games/[gameId]/processing
  Utente: admin
```

### A3-16 — Catalog ingestion (BGG sync dashboard)

```gherkin
Scenario A3-16 [Smoke]: La dashboard di catalog ingestion carica i pannelli di sync
  Given sono loggato come admin
  When apro /admin/catalog-ingestion
  Then vedo l'header "Catalog ingestion" (sottotitolo "Admin · Catalog · BoardGameGeek sync")
    And vedo il SyncStatusHero, la SyncRunTimeline e i pannelli QueuePending / FailedItems
    And il pulsante "Export" è presente
  Osservabile ✅: heading "Catalog ingestion" + SyncStatusHero + SyncRunTimeline + griglia QueuePending/FailedItems, nessun errore Console/Network non atteso (empty-state legittimo se non ci sono run)
  Route: /admin/catalog-ingestion
  Utente: admin
```

### A3-17 — Catalog seed queue (Wikidata + BGG)

```gherkin
Scenario A3-17 [Smoke]: La pagina seed queue carica i pannelli input/coda + log SSE
  Given sono loggato come admin
    And il feature flag admin.catalog-seed.enabled è attivo (altrimenti i sotto-pannelli mostrano errore 503 legittimo)
  When apro /admin/catalog/seed-queue
  Then vedo l'header "Catalog seed queue" (sottotitolo "Admin · Catalog · Seed pipeline (Wikidata + BGG)")
    And vedo il SeedQueueStatusHero, la colonna input (BulkPaste / SingleAdd / WikidataSearch) e la SeedQueueList
    And in fondo è presente il SeedLogStream (log SSE)
  Osservabile ✅: heading "Catalog seed queue" + SeedQueueStatusHero + form di input + SeedQueueList (righe o empty-state) + SeedLogStream, nessun errore Console/Network non atteso
  Route: /admin/catalog/seed-queue
  Utente: admin
  Nota: se `admin.catalog-seed.enabled=false` i BE call ritornano 503 e ogni sotto-componente mostra il proprio error UI — in quel caso lo scenario è ⚠️ blocked-env (flag ambientale), non fail.
```

---

## Cicli CRUD con verifica di persistenza (spec §3.1)

Gli scenari seguenti (`A3-18…A3-20`) coprono il **ciclo di vita completo** delle entità catalogo gestibili dall'admin, con **reload di verifica** dopo ogni operazione mutante (l'osservabile che distingue una mutazione reale dal solo feedback ottimistico). Tutti operano **solo** su entità create dallo scenario e marcate `HP-TEST-<data>` — mai sui seed condivisi.

**Operazioni disponibili per entità** (verificato nel codice 2026-07-10):

| Entità | Create | Edit | Delete | Note |
|--------|:------:|:----:|:------:|------|
| **Shared game** | ✅ `/new` → `POST …/shared-games` | ✅ drawer "Modifica gioco" → `PUT …/shared-games/{id}` | ✅ card/bulk "Elimina" → `DELETE …/shared-games/{id}` | Delete admin-immediato (nessuna approvazione). `SharedGameCatalog` usa soft-delete lato BE (`IsDeleted`) → osservabile = sparizione dalla lista attiva. Alternativa non-distruttiva: "Archivia" (`…/{id}/archive`, badge → Archiviato). |
| **Category** | ✅ "Add Category" → `POST …/admin/categories` | ✅ matita per-riga → `PUT …/admin/categories/{id}` | ✅ cestino per-riga → `DELETE …/admin/categories/{id}` | Hard-delete (nessun `IsDeleted` sul DTO). Il BE ritorna 409 se `gameCount > 0`: l'happy-path elimina una categoria **HP-TEST senza giochi collegati**. |
| **Game phase** | ✅ "Aggiungi fase" (riga locale) | ✅ rename + frecce su/giù (locale) | ✅ cestino per-riga (locale) | Nessun endpoint DELETE per singola fase: add/rename/reorder/remove sono **locali fino a "Salva fasi"** → upsert dell'intera lista (`PUT …/games/{gameId}/phase-templates`). La rimozione persiste perché la fase omessa dal payload viene cancellata. |

### A3-18 — Ciclo CRUD shared game (crea → edita → elimina)

```gherkin
Scenario A3-18 [Flow]: Ciclo di vita completo di uno shared game marcato HP-TEST
  Given sono loggato come admin
    And sono su /admin/shared-games/new
  When creo un gioco con Title = "HP-TEST-2026-07-10 CRUD Game" (compilo Description + default validi → "Create Game")
  Then vengo reindirizzato a /admin/shared-games/{id} con heading "HP-TEST-2026-07-10 CRUD Game" e badge "Draft"
    And dopo reload della pagina di dettaglio il gioco è ancora presente con lo stesso titolo (persistito · POST …/shared-games)
  When clicco "Edit Game", nel drawer "Modifica gioco" cambio la Descrizione (es. "HP-TEST descrizione modificata") e clicco "Salva modifiche"
  Then compare il toast "Gioco aggiornato" e il drawer si chiude (PUT /api/v1/admin/shared-games/{id})
    And dopo reload la Descrizione mostra il nuovo valore nel tab Details (persistito)
  When vado su /admin/shared-games/all, filtro status = Draft finché vedo la card del gioco HP-TEST
    And apro le quick-action della card e clicco "Elimina" (🗑️)
  Then la richiesta DELETE /api/v1/admin/shared-games/{id} ha successo e la card sparisce dalla griglia (lista invalidata)
    And dopo reload di /admin/shared-games/all la card del gioco HP-TEST resta assente
  Osservabile ✅: gioco presente post-create+reload (badge Draft) · Descrizione aggiornata post-edit+reload + toast "Gioco aggiornato" · card assente post-delete+reload · PUT 200 + DELETE 200/204 (Network)
  Route: /admin/shared-games/new → /admin/shared-games/[id] → /admin/shared-games/all
  Utente: admin
  Dati creati: "HP-TEST-2026-07-10 CRUD Game" (eliminato a fine ciclo)
  Nota: la quick-action "Elimina" per-card esegue il delete immediato (nessun dialog di conferma per la singola card; la conferma "Eliminare N giochi?" appare solo per la bulk-action multi-selezione). Il delete è admin-immediato; lato BE è un soft-delete (`IsDeleted`), quindi l'osservabile è la sparizione dalla lista attiva, non la cancellazione fisica. Alternativa non-distruttiva equivalente: "Archivia" (📦) → badge card "Archiviato".
```

### A3-19 — Ciclo CRUD categoria (crea → edita → elimina)

```gherkin
Scenario A3-19 [Flow]: Ciclo di vita completo di una categoria gioco marcata HP-TEST
  Given sono loggato come admin
    And sono su /admin/shared-games/categories
  When clicco "Add Category" e nel dialog compilo Name = "HP-TEST-2026-07-10 Cat", scelgo un'emoji e un colore, poi salvo
  Then il dialog si chiude e una nuova riga "HP-TEST-2026-07-10 Cat" appare nella CategoriesTable (POST /api/v1/admin/categories)
    And dopo reload della pagina la riga della categoria HP-TEST è ancora presente (persistita)
  When clicco la matita (✏️) sulla riga HP-TEST, nel dialog cambio Name in "HP-TEST-2026-07-10 Cat Edit" e salvo
  Then la riga si aggiorna in-place col nuovo nome (PUT /api/v1/admin/categories/{id})
    And dopo reload il nuovo nome persiste nella tabella
  When clicco il cestino (🗑️) sulla riga HP-TEST e confermo "Delete" nel dialog DeleteCategoryConfirm
  Then la richiesta DELETE /api/v1/admin/categories/{id} ha successo e la riga sparisce dalla tabella
    And dopo reload la riga della categoria HP-TEST resta assente
  Osservabile ✅: riga categoria presente post-create+reload · nome aggiornato post-edit+reload · riga assente post-delete+reload · POST/PUT/DELETE 200/204 (Network)
  Route: /admin/shared-games/categories
  Utente: admin
  Dati creati: categoria "HP-TEST-2026-07-10 Cat" (eliminata a fine ciclo)
  Nota: la categoria è hard-delete (nessun `IsDeleted` sul DTO). Il BE ritorna 409 se giochi sono ancora taggati con la categoria — l'happy-path elimina una categoria HP-TEST **senza giochi collegati** (creata e mai assegnata), così il delete riesce. Il dialog di conferma mostra un warning solo se `gameCount > 0`.
```

### A3-20 — Ciclo fasi gioco (aggiungi → edita/riordina → rimuovi) con salvataggio

```gherkin
Scenario A3-20 [Flow]: Ciclo di vita delle fasi di un gioco con persistenza via upsert
  Given sono loggato come admin
    And esiste il gioco seed "Catan"
    And sono su /admin/games/{catanId}/phases
  When clicco "Aggiungi fase" e inserisco Nome = "HP-TEST Fase A"
    And clicco di nuovo "Aggiungi fase" e inserisco Nome = "HP-TEST Fase B"
    And clicco "Salva fasi"
  Then compare il messaggio "Fasi salvate con successo!" (role=status · PUT /api/v1/games/{gameId}/phase-templates)
    And dopo reload della pagina le due fasi "HP-TEST Fase A" e "HP-TEST Fase B" sono ancora presenti nell'ordine salvato (persistite)
  When rinomino "HP-TEST Fase A" in "HP-TEST Fase A1", uso la freccia giù per spostarla sotto "HP-TEST Fase B" e clicco "Salva fasi"
  Then ricompare "Fasi salvate con successo!"
    And dopo reload il nuovo nome "HP-TEST Fase A1" e il nuovo ordine (B prima di A1) persistono
  When clicco il cestino (🗑️) sulla riga "HP-TEST Fase A1" e la riga "HP-TEST Fase B", poi clicco "Salva fasi"
  Then l'upsert persiste la lista senza le fasi HP-TEST rimosse
    And dopo reload nessuna fase "HP-TEST" è più presente (rimozione persistita: la fase omessa dal payload viene cancellata)
  Osservabile ✅: 2 fasi HP-TEST presenti post-add+reload · nome+ordine aggiornati post-edit+reload · fasi HP-TEST assenti post-remove+reload · ogni "Salva fasi" mostra "Fasi salvate con successo!" + PUT 200 (Network)
  Route: /admin/games/[gameId]/phases
  Utente: admin
  Dati creati: fasi "HP-TEST Fase A/B" sul gioco Catan (rimosse a fine ciclo con l'upsert finale — riportano il gioco allo stato fasi pre-scenario)
  Nota: non esiste un endpoint DELETE per singola fase — add/rename/reorder/remove sono locali fino a "Salva fasi", che fa l'upsert dell'intera lista. La rimozione persiste perché la fase omessa dal payload non viene reinserita. Il pulsante "Suggerisci" (AI) è un percorso alternativo di generazione fasi, fuori da questo ciclo CRUD manuale.
```

---

## Auto-verifica

- **Copertura**: tutte le 16 route dell'area compaiono nella matrice, ognuna con ≥1 scenario o `smoke-aggregato` motivato (`shared-games` redirect). Nessun buco.
- **Copertura CRUD (spec §3.1)**: le 3 entità catalogo gestibili dall'admin hanno il ciclo di vita completo con verifica di persistenza via reload — shared game (A3-18: create/edit/delete), category (A3-19: create/edit/delete), game phase (A3-20: add/edit/remove+save). Tutte e tre espongono Create + Edit + Delete nella UI (nessun Delete inventato): shared game via card/bulk "Elimina" (`DELETE …/shared-games/{id}`, admin-immediato, soft-delete BE `IsDeleted`), category via cestino per-riga (`DELETE …/admin/categories/{id}`, hard-delete), phase via cestino locale + "Salva fasi" (upsert lista, nessun endpoint per-fase).
- **Osservabili**: ogni scenario dichiara ≥1 `Osservabile ✅` strutturale (navigazione, heading, badge, toast, stato pipeline, presenza risposta, **stato post-reload** per i cicli CRUD) — nessuna asserzione su testo letterale generato da LLM.
- **Persistenza**: gli scenari CRUD (A3-18…A3-20) verificano la mutazione reale con un **reload** dopo ogni operazione (create/edit/delete), distinguendo la persistenza backend dal solo feedback ottimistico. Nessun conflitto di concorrenza atteso sull'happy path (categorie/fasi upsert idempotenti; shared game update via drawer prima del delete).
- **Happy path only**: nessuno scenario negativo/errore/edge. Le note su blocked-env (A3-07 nessun gioco Skeleton, A3-17 feature flag off) e i vincoli happy-path (A3-19 categoria senza giochi collegati per evitare il 409) sono precondizioni ambientali, non test di errore.
- **Utente**: admin per tutti (route group gated server-side + BE).
- **Marcatori**: entità create dai Flow usano `HP-TEST-<data>` (A3-02/A3-18 gioco, A3-13/A3-20 fasi, A3-19 categoria). A3-03b riusa il gioco di A3-02. I cicli CRUD A3-18…A3-20 eliminano la propria entità HP-TEST a fine ciclo (delete solo su dati HP-TEST, mai sui seed). Ordine additivo — i giochi seed Published non vengono mutati.
- **Tracciamento API**: create → `POST /api/v1/admin/shared-games`; update → `PUT …/shared-games/{id}`; publish → `POST …/{id}/publish`; archive → `POST …/{id}/archive`; delete → `DELETE …/shared-games/{id}`; link agent → `POST …/{id}/link-agent/{agentId}`; categorie → `POST/PUT/DELETE …/admin/categories[/{id}]`; fasi → `PUT …/games/{gameId}/phase-templates` (upsert); import wizard → saga `ImportGameFromPdfCommand`; games wizard → `/admin/shared-games/wizard/*` + processing. Il `quick-publish` del seed è documentato come shortcut server-side non esposto in FE.
- **Conteggio**: 20 scenari (Flow: A3-02, A3-03b, A3-04, A3-05, A3-06, A3-07, A3-10, A3-12, A3-13, A3-14, A3-18, A3-19, A3-20 = 13 · Smoke: A3-01, A3-03, A3-08, A3-09, A3-11, A3-15, A3-16, A3-17 = 8, dove A3-01 assorbe anche il redirect `shared-games`). +3 scenari CRUD (A3-18…A3-20) rispetto ai 17 precedenti.
