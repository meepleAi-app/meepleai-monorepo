# Happy Path — A2 · Knowledge Base admin

> Catalogo scenari happy-path per l'area **A2 — Knowledge Base admin** (tooling admin KB + mechanic-extractor + RAG quality). Formato e legenda: vedi [`_TEMPLATE.md`](./_TEMPLATE.md). Solo **happy path**. Osservabili basati su struttura, non su testo LLM letterale.

## Intestazione

- **Area**: A2 — Knowledge Base admin (`admin/(dashboard)/knowledge-base/**` + `admin/(dashboard)/rag-quality`).
- **Utente**: **admin** (da `infra/secrets/admin.secret` — `ADMIN_EMAIL`/`ADMIN_PASSWORD`). Tutte le route sotto `admin/(dashboard)` richiedono ruolo admin.
- **Prerequisiti dati (seed `make seed-sp4`)**:
  - Giochi con PDF regole indicizzati (stato `Ready`/`Completed`): **Azul**, **Catan**, **Wingspan**, **Brass: Birmingham**, **Ark Nova**, **Spirit Island**, **7 Wonders Duel**, **Codenames**, **Carcassonne**, **Ticket to Ride**, **Pandemic**, **Terraforming Mars** (`data.json:games[]`).
  - Vettori pgvector popolati (dai PDF indicizzati) → `vectors`, `rag-quality` mostrano conteggi > 0.
  - Snapshot `latest` (auto) creato dopo l'indicizzazione dei PDF seed.
- **Dati creati** dagli scenari Flow: PDF marcato `HP-TEST-<data>` (es. `HP-TEST-2026-07-10-regole.pdf`, poi cancellato in A2-18), snapshot manuale datato (creato in A2-09; creato+cancellato in A2-19), golden claim `HP-TEST-<data>` (creato/editato/disattivato in A2-20). I cicli CRUD con Delete (A2-18/19/20) rimuovono a fine ciclo solo il dato HP-TEST che hanno creato.
- **PDF di prova per upload**: un rulebook PDF locale qualsiasi (anche un seed rulebook già presente in `infra/`); l'osservabile è strutturale (avanzamento → stato completato in coda), non il contenuto.

### ⚠️ Caveat ambientali (blocked-env potenziali)

- **Feature flag `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED`**: le route `mechanic-extractor/dashboard`, `mechanic-extractor/golden`, `mechanic-extractor/golden/[gameId]` chiamano `notFound()` (404) se il flag **non** è `'true'` (`isMechanicValidationEnabled()`). In un ambiente `make dev` senza il flag impostato queste 3 route rendono la pagina 404 → gli scenari relativi sono **⚠️ blocked-env** (non fail). Verificare `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED` nell'env del container `web` prima di eseguirli.
- **AI stack**: gli scenari che generano un'analisi mechanic-extractor (`A2-11`) richiedono `make dev` full (LLM). Con `make dev-core` la generazione non produce claim → l'osservabile "pipeline conclusa" non è raggiungibile (⚠️ blocked-env).
- **`embedding`**: la pagina interroga il servizio embedding Python; se il servizio è giù lo `StatusBadge` mostra `Unavailable` (empty-state legittimo, comunque **pass** come Smoke se la pagina carica senza errori JS).

---

## Matrice di copertura

| # | Route | Liv. | Scenario/i |
|---|-------|------|------------|
| 1 | `admin/(dashboard)/knowledge-base` (landing / KbExplorer) | Smoke | A2-01 |
| 2 | `admin/(dashboard)/knowledge-base/documents` | Smoke · **Flow (CRUD)** | A2-02 · **A2-18** (Delete + persistenza) |
| 3 | `admin/(dashboard)/knowledge-base/upload` | Flow | **A2-03** |
| 4 | `admin/(dashboard)/knowledge-base/queue` | Smoke | A2-04 |
| 5 | `admin/(dashboard)/knowledge-base/processing` | Smoke | A2-05 (smoke-aggregato) |
| 6 | `admin/(dashboard)/knowledge-base/rag-pipeline` | Smoke | A2-05 (smoke-aggregato) |
| 7 | `admin/(dashboard)/knowledge-base/pipeline` | Smoke | A2-05 (smoke-aggregato) |
| 8 | `admin/(dashboard)/knowledge-base/embedding` | Smoke | A2-06 |
| 9 | `admin/(dashboard)/knowledge-base/vectors` | Smoke | A2-07 |
| 10 | `admin/(dashboard)/knowledge-base/games` | Smoke | A2-08 |
| 11 | `admin/(dashboard)/knowledge-base/snapshots` | Flow | **A2-09** (crea) · **A2-19** (ciclo crea→cancella + persistenza) |
| 12 | `admin/(dashboard)/knowledge-base/settings` | Smoke | A2-10 |
| 13 | `admin/(dashboard)/knowledge-base/feedback` | Flow | **A2-11** |
| 14 | `admin/(dashboard)/knowledge-base/mechanic-extractor` (editor Variant C) | Flow | **A2-12** |
| 15 | `admin/(dashboard)/knowledge-base/mechanic-extractor/analyses` | Flow | **A2-13** |
| 16 | `admin/(dashboard)/knowledge-base/mechanic-extractor/review` | Flow | **A2-14** |
| 17 | `admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard` | Smoke | A2-15 (⚠️ feature-flag) |
| 18 | `admin/(dashboard)/knowledge-base/mechanic-extractor/golden` | Smoke | A2-16 (⚠️ feature-flag) |
| 19 | `admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]` | Smoke · **Flow (CRUD)** | A2-16 · **A2-20** (crea→edita→cancella + persistenza) (⚠️ feature-flag) |
| 20 | `admin/(dashboard)/rag-quality` | Smoke | A2-17 |

> **Note copertura**:
> - `settings` è marcata `Flow` nella mappa globale ma la pagina è **read-only** (`KBSettings` mostra la configurazione KB/RAG, nessuna mutazione dall'UI): trattata come **Smoke** qui. Delta annotato in A-FINAL se serve.
> - `processing`, `rag-pipeline`, `pipeline` sono tre viste di monitoraggio pipeline read-only sovrapponibili → coperte da **A2-05** come smoke-aggregato (stesso criterio, tre route).
> - **Ciclo CRUD + persistenza (spec §3.1)**: `documents` → **A2-18** (Delete di un doc HP-TEST + reload di verifica); `snapshots` → **A2-19** (crea→cancella su snapshot HP-TEST + reload); `golden/[gameId]` → **A2-20** (crea→edita→cancella claim HP-TEST + reload, ⚠️ feature-flag). Operazioni assenti annotate in ciascuno scenario (nessun Delete inventato).
> - Nessuna route dell'area resta scoperta: 20/20 mappate.

---

## Scenari

```gherkin
Scenario A2-01 [Smoke]: Esploratore KB master-detail carica l'albero giochi→documenti
  Given sono loggato come admin
    And il seed ha indicizzato PDF per più giochi (Azul, Catan, …)
  When apro /admin/knowledge-base
  Then lo skeleton (data-testid="kb-explorer-loading") lascia il posto all'albero KB
    And nel pannello sinistro (KbTree) vedo la lista dei giochi con stato KB
    And espandendo un gioco (es. "Azul") compaiono i suoi documenti
    And selezionando un documento il pannello destro (KbDocDetailPanel) mostra il dettaglio
    And l'URL riflette la selezione con ?doc=<id>
  Osservabile ✅: albero giochi non-vuoto + espansione mostra ≥1 documento + pannello dettaglio popolato + ?doc=<id> in URL · nessun errore Console/Network
  Route: admin/(dashboard)/knowledge-base
  Utente: admin
```

```gherkin
Scenario A2-02 [Smoke]: Documents Library elenca i PDF con analytics e filtri
  Given sono loggato come admin
    And esistono documenti PDF indicizzati dal seed
  When apro /admin/knowledge-base/documents
  Then le 4 card analytics (Total Documents, Completed, Processing, Storage) mostrano numeri reali (non "—")
    And la tabella documenti elenca righe con nome file, gioco, stato (badge), pagine, chunk, dimensione, data
    And imposto il filtro Status su "Completed"
  Then la tabella si aggiorna mostrando solo documenti con badge "Completed"
  Osservabile ✅: card analytics con valori numerici + tabella con ≥1 riga + filtro Status produce un effetto visibile (subset filtrato) · nessun errore Console/Network
  Route: admin/(dashboard)/knowledge-base/documents
  Utente: admin
```

```gherkin
Scenario A2-03 [Flow]: Carica ed accoda un PDF alla KB dall'admin (upload → embed)
  Given sono loggato come admin
    And ho un file PDF di prova rinominato HP-TEST-2026-07-10-regole.pdf
  When apro /admin/knowledge-base/upload
    And nel selettore "Seleziona Gioco" cerco "Azul" e lo seleziono (compare il badge "Selezionato")
    And trascino/seleziono il PDF nella drop zone
  Then l'upload parte (POST /api/v1/ingest/pdf) e la barra di avanzamento sale da 0 → 100%
    And dopo l'upload il PDF viene accodato automaticamente (POST /api/v1/admin/queue/enqueue)
    And la riga di caricamento passa a stato "completed" con spunta verde
    And il contatore "Caricamenti (1/1)" conferma il completamento
  Osservabile ✅: badge "Selezionato" sul gioco + progress bar → 100% + spunta verde "completed" + contatore N/N · POST /ingest/pdf e /queue/enqueue 2xx (Network)
  Route: admin/(dashboard)/knowledge-base/upload
  Utente: admin
```

```gherkin
Scenario A2-04 [Smoke]: Dashboard della coda di elaborazione carica lo stato job
  Given sono loggato come admin
    And il seed ha prodotto job di elaborazione PDF (o coda vuota legittima)
  When apro /admin/knowledge-base/queue
  Then il dashboard coda (QueueDashboardClient) carica senza errori
    And mostra lo stato della coda (GET /api/v1/admin/queue/status) e la lista dei job (o empty-state "nessun job")
  Osservabile ✅: pagina coda renderizzata con stato/lista job (o empty-state legittimo) · nessun errore Console/Network 4xx/5xx non atteso
  Route: admin/(dashboard)/knowledge-base/queue
  Utente: admin
```

```gherkin
Scenario A2-05 [Smoke]: Le tre viste di monitoraggio pipeline caricano (processing · rag-pipeline · pipeline)
  Given sono loggato come admin
    And esistono documenti processati dal seed
  When apro in sequenza /admin/knowledge-base/processing, /admin/knowledge-base/rag-pipeline e /admin/knowledge-base/pipeline
  Then ogni pagina carica il proprio contenuto di monitoraggio (flusso pipeline / metriche di step / distribuzione documenti)
    And gli skeleton lasciano il posto a dati reali (o empty-state legittimo)
  Osservabile ✅: per ciascuna delle 3 route: intestazione + almeno un pannello metriche/flusso popolato o empty-state · nessun errore Console/Network
  Route: admin/(dashboard)/knowledge-base/processing · admin/(dashboard)/knowledge-base/rag-pipeline · admin/(dashboard)/knowledge-base/pipeline
  Utente: admin
```

```gherkin
Scenario A2-06 [Smoke]: Stato servizio embedding e metriche di throughput
  Given sono loggato come admin
    And il servizio embedding è attivo (make dev full)
  When apro /admin/knowledge-base/embedding
  Then il pannello "Service Status" mostra un badge di salute (Healthy/Unavailable) e i campi Model/Device/Dimensions/Languages
    And la sezione "Throughput Metrics" mostra le KPI (Total Requests, Failures, Failure Rate, Avg Duration, …) con valori numerici
    And il pulsante "Refresh" ricarica i dati (info + metrics)
  Osservabile ✅: badge stato servizio + campi modello popolati + KPI throughput con valori · azione Refresh senza errori · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/embedding
  Utente: admin
```

```gherkin
Scenario A2-07 [Smoke]: Vector store — KPI + ricerca semantica pgvector
  Given sono loggato come admin
    And i PDF seed sono stati embeddati (pgvector popolato)
  When apro /admin/knowledge-base/vectors
  Then la KPI strip mostra Total Vectors, Games Indexed, Dimensions, Avg Health con valori reali
    And nella "Semantic Search" digito una query (es. "punteggio riga completa") e premo Search
  Then compare un blocco risultati "N results found" con righe Doc ID · Page · Chunk · Snippet (o "No results" legittimo)
  Osservabile ✅: KPI vettori con numeri > 0 + ricerca semantica produce un elenco risultati (o empty-state) · POST vector search 2xx · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/vectors
  Utente: admin
```

```gherkin
Scenario A2-08 [Smoke]: Knowledge Base per gioco — stato KB e filtri
  Given sono loggato come admin
    And più giochi hanno KB completa/parziale/assente
  When apro /admin/knowledge-base/games
  Then le card riepilogo (Totale giochi, KB completa, KB parziale, Senza KB) mostrano i conteggi
    And la lista giochi mostra righe con badge di stato KB (Completa/Parziale/Nessuna KB), conteggio documenti e chunk
    And clicco la card "KB completa" per filtrare
  Then la lista si restringe ai soli giochi con KB completa
  Osservabile ✅: card conteggi + lista giochi con badge stato + filtro card produce subset visibile · nessun errore Console/Network
  Route: admin/(dashboard)/knowledge-base/games
  Utente: admin
```

```gherkin
Scenario A2-09 [Flow]: Crea uno snapshot manuale della KB
  Given sono loggato come admin
    And apro /admin/knowledge-base/snapshots
    And la lista mostra almeno lo snapshot "latest" (auto)
  When clicco "Nuovo snapshot"
  Then il pulsante entra in stato "Creazione..." (export in corso)
    And al termine la lista snapshot viene ricaricata e include un nuovo snapshot datato oltre a "latest"
  Osservabile ✅: stato "Creazione..." transitorio + lista snapshot aggiornata con ≥1 snapshot in più rispetto allo stato iniziale · nessun banner d'errore export · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/snapshots
  Utente: admin
```

```gherkin
Scenario A2-10 [Smoke]: Impostazioni KB/RAG in sola lettura caricano la configurazione
  Given sono loggato come admin
  When apro /admin/knowledge-base/settings
  Then il pannello KBSettings mostra la configurazione corrente della KB e della pipeline RAG (parametri/valori)
    And lo skeleton lascia il posto ai valori reali
  Osservabile ✅: intestazione "Settings" + almeno un blocco di configurazione con valori popolati · nessun errore Console/Network
  Route: admin/(dashboard)/knowledge-base/settings
  Utente: admin
```

```gherkin
Scenario A2-11 [Flow]: Revisiona i feedback KB degli utenti per un gioco
  Given sono loggato come admin
    And il seed ha prodotto feedback thumbs up/down su risposte KB per un gioco (es. Azul)
  When apro /admin/knowledge-base/feedback
    And incollo l'UUID del gioco "Azul" nel campo "Game ID"
  Then compare il pannello KbFeedbackPanel con il contatore "N feedback totali"
    And la lista mostra righe con icona thumbs up/down, badge esito (Utile/Non utile), id messaggio e data
    And imposto il filtro esito su "Utili"
  Then la lista si restringe ai soli feedback "Utile"
  Osservabile ✅: pannello feedback con contatore totali + ≥1 riga feedback (o empty-state se il gioco non ha feedback) + filtro esito produce subset visibile · GET admin-kb-feedback 2xx · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/feedback
  Utente: admin
```

```gherkin
Scenario A2-12 [Flow]: Editor mechanic-extractor Variant C — note umane + AI assist su una sezione
  Given sono loggato come admin
    And il gioco "Azul" ha un PDF regole in stato Ready (seed)
    And l'AI stack è attivo (make dev full)
  When apro /admin/knowledge-base/mechanic-extractor
    And seleziono il gioco "Azul" e il suo PDF dai due selettori
  Then il PDF si apre nell'iframe di sinistra e a destra compaiono i tab sezione (Summary/Mechanics/Victory/Resources/Phases/FAQ)
  When scrivo ≥10 caratteri di note nella sezione "Summary" (auto-save dopo ~2s → "Save" abilitato) e premo "AI Assist"
  Then compare il riquadro "AI-Generated Draft" con il testo prodotto e i pulsanti Accept/Reject
    And premo "Accept" e la sezione mostra il blocco verde "Accepted Draft" con una spunta sul tab
  Osservabile ✅: iframe PDF + tab sezione + riquadro "AI-Generated Draft" + blocco "Accepted Draft" post-Accept + spunta verde sul tab · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor
  Utente: admin
```

```gherkin
Scenario A2-13 [Flow]: Pipeline async mechanic-analyses — genera un'analisi e avanza il ciclo di vita
  Given sono loggato come admin
    And il gioco "Catan" ha un PDF Ready (seed) e l'AI stack è attivo
  When apro /admin/knowledge-base/mechanic-extractor/analyses
    And nella "Start a new analysis" seleziono gioco "Catan" + PDF, lascio il cost cap a 0.50 e premo "Generate"
  Then l'analisi viene creata (202) e compare il pannello "Analysis status" con l'ID
    And durante l'esecuzione appare il badge "Running" (polling /status ogni 2s) e la tabella "Section runs" si popola
    And al termine lo stato diventa "Draft" con claim > 0 (badge stato + conteggio Claims)
  When premo "Submit for review" (abilitato con claims > 0)
  Then lo stato passa a "InReview" e il pulsante "Approve" si abilita
  Osservabile ✅: pannello Analysis status con ID + badge Running transitorio + Section runs popolata + transizione Draft→InReview via "Submit for review" · endpoint generate/status/submit-review 2xx · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor/analyses
  Utente: admin
```

```gherkin
Scenario A2-14 [Flow]: Anteprima analisi finale mechanic-extractor prima dell'attivazione
  Given sono loggato come admin
    And esiste una bozza mechanic-extractor con sezioni compilate per un gioco (da A2-12/A2-13 o seed)
  When apro /admin/knowledge-base/mechanic-extractor/review?sharedGameId=<id>&pdfDocumentId=<pdfId>
  Then la pagina carica l'anteprima "<gioco> — Anteprima Analisi"
    And mostra il badge "N/6 sezioni completate" e la stats bar (Sezioni, Meccaniche, Risorse, Token AI)
    And rende le sezioni compilate (Sommario, Meccaniche come chip, Condizioni di Vittoria, Risorse in tabella, Fasi, FAQ)
    And il footer copyright "© 2026 MeepleAI — Contenuto originale" è presente
  Osservabile ✅: intestazione "Anteprima Analisi" + badge sezioni completate + ≥1 sezione renderizzata (es. chip meccaniche) + footer copyright · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor/review
  Utente: admin
```

```gherkin
Scenario A2-15 [Smoke]: Dashboard validazione AI Comprehension (feature-flag)
  Given sono loggato come admin
    And NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED = 'true' nell'ambiente web
  When apro /admin/knowledge-base/mechanic-extractor/dashboard
  Then compare l'intestazione "AI Comprehension Validation — Dashboard"
    And le 3 tile riepilogo (Certified/NotCertified/NotEvaluated) e la tabella per-gioco caricano (data-testid="dashboard-content"), o empty-state legittimo
    And la card "Certification Thresholds" mostra il form soglie
  Osservabile ✅: intestazione dashboard + summary cards + tabella o empty-state + card soglie · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor/dashboard
  Utente: admin
  Nota: se il flag NON è 'true' la route rende 404 (notFound) → ⚠️ blocked-env, non fail.
```

```gherkin
Scenario A2-16 [Smoke]: Golden Set — picker giochi e CRUD claim per gioco (feature-flag)
  Given sono loggato come admin
    And NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED = 'true' nell'ambiente web
  When apro /admin/knowledge-base/mechanic-extractor/golden
  Then compare "Golden Set Curation" con la lista dei giochi condivisi cliccabili
  When clicco un gioco (es. "Azul") → naviga a /golden/<gameId>
  Then la pagina mostra il titolo del gioco + il GoldenVersionHashBadge, il pulsante "New claim" e la lista claim (GoldenClaimsList) o empty-state
  Osservabile ✅: lista giochi nel picker + navigazione al dettaglio + titolo gioco/badge hash + pulsante "New claim" + lista claim o empty-state · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor/golden · admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]
  Utente: admin
  Nota: se il flag NON è 'true' entrambe le route rendono 404 (notFound) → ⚠️ blocked-env, non fail.
```

```gherkin
Scenario A2-17 [Smoke]: RAG Quality Dashboard carica health e conteggi indicizzazione
  Given sono loggato come admin
    And i PDF seed sono indicizzati/embeddati
  When apro /admin/rag-quality
  Then compare l'intestazione "RAG Quality Dashboard"
    And le 4 summary card (Total Indexed Documents, Embedded Chunks, RAPTOR Summaries, Entity Relations) mostrano valori reali
    And la card "Top Games by Chunk Count" elenca i giochi ordinati per numero di chunk (o empty-state legittimo)
    And il pulsante refresh ricarica il report senza errori
  Osservabile ✅: intestazione dashboard + 4 summary card con valori + lista "Top Games" (o empty-state) · nessun errore Console/Network
  Route: admin/(dashboard)/rag-quality
  Utente: admin
```

### Cicli CRUD & verifica persistenza (spec §3.1)

> Questi scenari verificano la **mutazione reale e persistente** del dato via browser: dopo ogni operazione un **reload** della pagina riconferma lo stato (distingue la persistenza dal solo feedback ottimistico). I `Delete` operano **solo** su dati marcati `HP-TEST-<data>` creati dallo scenario stesso — **mai** su seed condivisi né sullo snapshot `latest`.

```gherkin
Scenario A2-18 [Flow]: Ciclo di vita documento KB — cancella un PDF HP-TEST e verifica la persistenza (Delete + reload)
  Given sono loggato come admin
    And ho appena caricato via A2-03 il PDF HP-TEST-2026-07-10-regole.pdf sul gioco "Azul" (esiste in Documents Library)
  When apro /admin/knowledge-base/documents
    And nella barra "Search documents..." digito "HP-TEST-2026-07-10-regole" per isolare la riga
  Then la tabella mostra la riga del documento HP-TEST con il suo stato (badge)
  When nella colonna Actions della riga premo l'icona cestino (title "Delete document")
    And nel dialog "Delete Document" confermo con "Delete"
  Then la riga del documento HP-TEST sparisce dalla tabella (POST /api/v1/admin/pdfs/bulk/delete 2xx → lista invalidata/ricaricata)
    And dopo un reload della pagina (e ri-applicando il filtro "HP-TEST-2026-07-10-regole") la tabella mostra l'empty-state "No documents match your filters" — il documento resta assente
  Osservabile ✅: riga HP-TEST presente pre-delete → assente dopo Delete → ancora assente dopo reload (persistenza) · POST /pdfs/bulk/delete 2xx (Network) · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/documents
  Utente: admin
  Dati creati/rimossi: PDF HP-TEST-2026-07-10-regole.pdf (creato in A2-03, rimosso qui a chiusura del ciclo Create→Delete)
  Operazioni assenti: nessun **Edit** di campo sul documento dall'UI (la sola azione non-distruttiva è "Reindex", che ri-processa il PDF, non modifica un campo) → Edit non applicabile; **Create** avviene nel flusso di upload A2-03 (route /upload), non in questa pagina.
```

```gherkin
Scenario A2-19 [Flow]: Ciclo CRUD snapshot KB — crea e poi cancella uno snapshot manuale (crea → cancella + persistenza)
  Given sono loggato come admin
    And apro /admin/knowledge-base/snapshots
    And la lista "Snapshot disponibili" mostra almeno lo snapshot "latest" (badge "auto")
  When premo "Nuovo snapshot"
  Then il pulsante entra in stato "Creazione..." (POST /api/v1/admin/rag-backup/export)
    And al termine la lista si ricarica e include un nuovo snapshot datato (id ≠ "latest") oltre a "latest"
    And dopo un reload della pagina il nuovo snapshot datato è ancora in lista (persistenza della creazione)
  When sulla card del nuovo snapshot datato premo il cestino (l'azione Delete è esposta solo per snapshot con id ≠ "latest")
    And nel dialog "Elimina snapshot" confermo con "Elimina"
  Then lo snapshot datato sparisce dalla lista (DELETE /api/v1/admin/rag-backup/snapshots/{id} 2xx → lista invalidata/ricaricata)
    And dopo un reload della pagina lo snapshot datato resta assente e "latest" è ancora presente e intatto
  Osservabile ✅: snapshot datato presente dopo create+reload · assente dopo Delete+reload · "latest" mai toccato · POST /rag-backup/export 2xx + DELETE /rag-backup/snapshots/{id} 2xx (Network) · nessun banner d'errore export/delete · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/snapshots
  Utente: admin
  Dati creati/rimossi: 1 snapshot datato manuale (creato e rimosso nello stesso ciclo; "latest" auto preservato)
  Operazioni assenti: nessun **Edit** di uno snapshot dall'UI (le sole azioni sono Crea, Ripristina, Elimina) → Edit non applicabile. Il Delete è volutamente inibito su "latest" (nessun cestino renderizzato): il ciclo opera solo sullo snapshot datato creato dallo scenario.
```

```gherkin
Scenario A2-20 [Flow]: Ciclo CRUD golden claim — crea, edita e disattiva un claim HP-TEST (crea → edita → cancella + persistenza) (⚠️ feature-flag)
  Given sono loggato come admin
    And NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED = 'true' nell'ambiente web
    And apro /admin/knowledge-base/mechanic-extractor/golden e clicco un gioco (es. "Azul") → /golden/<gameId>
    And la pagina mostra il titolo del gioco, il GoldenVersionHashBadge e la lista claim (GoldenClaimsList) o empty-state
  When premo "New claim" e nel dialog compilo Section="Mechanics", Statement="HP-TEST-2026-07-10 claim di verifica persistenza (≥10 char)", Expected page=1, Source quote="HP-TEST-2026-07-10 citazione sorgente di verifica (≥10 char)" → "Create claim"
  Then i campi Statement/Expected page/Source quote del form si resettano (il dialog "create" resta aperto per un nuovo inserimento) e chiudo il dialog manualmente (POST /golden 2xx)
    And il nuovo claim appare nel gruppo "Mechanics" della lista
    And dopo un reload della pagina il claim HP-TEST è ancora presente nel gruppo "Mechanics" (persistenza della creazione)
  When sulla riga del claim HP-TEST premo l'icona matita (aria-label "Edit claim <id>"), nel dialog "Edit golden claim" cambio Expected page da 1 a 2 → "Save changes"
  Then il dialog si chiude e la riga del claim mostra "Expected page" = 2 (PUT /golden/<id> 2xx)
    And dopo un reload della pagina il claim HP-TEST mostra ancora Expected page = 2 (persistenza dell'edit)
  When sulla riga del claim HP-TEST premo l'icona cestino (aria-label "Deactivate claim <id>") e nel dialog "Deactivate this golden claim?" confermo con "Deactivate"
  Then il claim HP-TEST sparisce dalla lista dei claim attivi (DELETE /golden/<id> 2xx, soft-delete → il version hash del golden set cambia)
    And dopo un reload della pagina il claim HP-TEST resta assente dalla lista attiva
  Osservabile ✅: claim HP-TEST presente dopo create+reload · Expected page=2 dopo edit+reload · assente dalla lista attiva dopo deactivate+reload · POST /golden + PUT /golden/<id> + DELETE /golden/<id> 2xx (Network) · nessun errore Console
  Route: admin/(dashboard)/knowledge-base/mechanic-extractor/golden/[gameId]
  Utente: admin
  Dati creati/rimossi: 1 golden claim "HP-TEST-2026-07-10 …" (creato, editato e disattivato nello stesso ciclo)
  Note: se il flag NON è 'true' la route rende 404 (notFound) → ⚠️ blocked-env, non fail. Il "Delete" è un **soft-delete/deactivate** (rimosso dalla lista attiva, ripristinabile lato backend): l'osservabile happy-path è l'assenza dalla lista attiva post-reload, coerente con il ciclo Delete della spec §3.1.
```

---

## Auto-verifica (checklist autore)

- **Copertura route**: 20/20 route dell'area presenti nella matrice (19 `knowledge-base/**` da glob + `rag-quality`). Corrisponde all'elenco A2 di `_coverage-map.md`. Nessun buco.
- **Conteggio scenari**: 20 scenari (A2-01…A2-20). +3 rispetto al corpus iniziale (A2-01…A2-17): A2-18 (Delete doc), A2-19 (ciclo snapshot crea→cancella), A2-20 (ciclo golden crea→edita→cancella).
- **Aggregati dichiarati**: `processing` + `rag-pipeline` + `pipeline` → **A2-05** (smoke-aggregato, 3 route, stesse osservazioni di monitoraggio). `golden` + `golden/[gameId]` → **A2-16** (flusso picker→dettaglio); `golden/[gameId]` ha in aggiunta il ciclo CRUD **A2-20**. Motivi annotati.
- **Delta livello**: `settings` mappata Flow ma pagina read-only → declassata a Smoke (A2-10), delta annotato per A-FINAL.
- **Ogni scenario ha ≥1 osservabile** strutturale (`Osservabile ✅`) verificabile a schermo, nessuna asserzione su testo LLM letterale (A2-12/A2-13/A2-14 osservano struttura: presenza riquadro AI, badge stato, chip, footer).
- **Cicli CRUD & persistenza (spec §3.1)** verificati nel codice e coperti con reload:
  - **KB/PDF doc** (`documents/page.tsx`): **Create** via upload (A2-03, route `/upload`) · **Delete** reale per-riga (`bulkDeletePdfs([id])` → `POST /api/v1/admin/pdfs/bulk/delete`) → **A2-18** con reload · **Edit** di campo **assente** (solo "Reindex" = ri-processo) → annotato.
  - **Snapshot** (`snapshots/page.tsx`): **Create** (`exportKbSnapshot` → `POST /rag-backup/export`) + **Delete** (`deleteKbSnapshot` → `DELETE /rag-backup/snapshots/{id}`, cestino solo per id ≠ `latest`) → ciclo completo **A2-19** con reload · **Edit assente** → annotato.
  - **Golden claim** (`golden/[gameId]` → `GoldenClaimsList`/`GoldenClaimForm`): **Create** (`POST /golden`) + **Edit** (`PUT /golden/{id}`) + **Delete/deactivate** (`DELETE /golden/{id}`, soft-delete) → ciclo completo **A2-20** con reload (⚠️ feature-flag).
- **Solo happy path**: nessuno scenario negativo/errore/edge. Le mutazioni **massive/globali** delle pagine (bulk-delete multi-selezione, purge stale, cleanup orphans, restore snapshot, suppress analisi) restano **escluse**. I `Delete` inclusi (A2-18/19/20) sono **cicli di vita mirati** su un singolo dato `HP-TEST-<data>` creato dallo scenario, con reload di persistenza (spec §3.1) — mai su seed condivisi né su `latest`.
- **Dati marcati**: A2-03 carica un PDF `HP-TEST-<data>` (poi rimosso dal ciclo A2-18). A2-09 crea uno snapshot manuale (additivo, non distrugge `latest`); A2-19 crea+rimuove uno snapshot datato nello stesso ciclo. A2-12/A2-13 creano bozze/analisi sui giochi seed (additive). A2-20 crea/edita/disattiva un golden claim `HP-TEST-<data>` nello stesso ciclo.
- **Utente**: admin per tutti (area admin-only).
- **Blocked-env dichiarati**: A2-15, A2-16 e A2-20 dipendono dal feature flag `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED`; A2-11/A2-12/A2-13 dipendono dall'AI stack (`make dev` full). A2-18 dipende dal PDF HP-TEST creato in A2-03 (eseguire A2-03 prima). Caveat esplicitati nell'intestazione e nelle note scenario.
