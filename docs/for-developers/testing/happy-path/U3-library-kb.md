# Happy Path — U3 · Library & Knowledge Base

> Catalogo scenari happy-path per l'area **U3 — Library & Knowledge Base**.
> Formato: [`_TEMPLATE.md`](./_TEMPLATE.md) · Mappa globale: [`_coverage-map.md`](./_coverage-map.md) (sez. U3).
> Solo **happy path**. GWT keywords in inglese, prosa in italiano. Osservabili **strutturali** (elemento/heading/navigazione/badge), mai testo letterale LLM-generato.

## Intestazione

- **Prerequisiti dati (seed `make seed-sp4`)**:
  - Giochi catalogo con PDF regole **indicizzati** (`processingState=Ready`): Azul, Catan, Wingspan, Brass, Gloomhaven, Ark Nova, Spirit Island, 7 Wonders Duel, Codenames, Carcassonne, Ticket to Ride, Pandemic, Terraforming Mars (14 KB doc, vedi `data.json:kbDocs[]`, tutti `_targetState: indexed`).
  - Library di `marco@meepleai.test`: 11 giochi **Owned** (azul, catan, wingspan, brass, arknova, spirit, 7wonders, codenames, carcassonne, pandemic, terraforming) + 1 **Wishlist** (gloomhaven). Vedi `data.json:library.marco`.
  - PDF regola reale per l'upload: `azul-regole-ita.pdf` (source `azul_rulebook.pdf` in `infra/scripts/seed-sp4/data/rulebook/`).
- **Utenti**:
  - `marco@meepleai.test` (premium, verificato) — persona utente-standard (ruolo `User`).
  - **admin** (da `infra/secrets/admin.secret`) — richiesto per gli scenari `/upload` (route riservata a ruoli `Admin`/`Editor`, vedi nota ⚠️ sotto).
- **Marcatore dati Flow**: ogni entità creata usa `HP-TEST-2026-07-10` nel nome/titolo.

> ⚠️ **Nota di scoping route** (allineata a `_coverage-map.md`): `library/[gameId]/agent` è in **U4** (chat RAG); `library/[gameId]/{toolbox,toolkit,play/**}` sono in **U7** (toolkit/gamebook play). Questo catalogo copre le 18 route U3 elencate nella matrice sotto.

> ⚠️ **Nota ambientale — `/upload` è Admin/Editor-only**: `upload/page.tsx` avvolge `UploadClient` in `RequireRole allowedRoles={['Admin', 'Editor']}`. `marco` è ruolo `User` → **non** può usare `/upload`. L'upload "utente-standard" avviene invece via **private game** (`/library/private/add` step PDF, che usa il campo `privateGameId`). Lo scenario di upload-catalogo (U3-05) usa quindi l'account **admin**.

> ⚠️ **Nota ambientale — indicizzazione PDF lenta**: `POST /ingest/pdf` avvia una pipeline asincrona (estrazione → chunking → embedding → indexing) che richiede **minuti** su un PDF reale. L'osservabile di successo è il documento che raggiunge lo stato **`Ready`/indexed** (badge o polling), non l'istante di upload. Gli scenari che lo richiedono dichiarano un'attesa esplicita; se la pipeline eccede il tempo ragionevole di esecuzione, marcare `⚠️ blocked-env` (non fail) e usare i PDF già indicizzati dal seed per gli scenari a valle.

---

## Matrice di copertura (18 route → scenari)

| Route | Liv. | Scenario/i | Note |
|-------|------|-----------|------|
| `(authenticated)/library` | Flow | **U3-01** (smoke hub), **U3-02** (add-game catalogo), **U3-22** (CRUD: add→remove entry) | Hero + 6 tab (all/games/agents/kb/sessions/chat) + `?action=add` drawer + remove via bulk-select |
| `(authenticated)/library/wishlist` | Flow | **U3-03** (smoke list), **U3-04** (add wishlist), **U3-23** (CRUD: add→remove) | "My Wishlist" + `AddToWishlistDialog` + 🗑️ Remove card |
| `(authenticated)/library/private` | Smoke | **U3-06**, **U3-21** (CRUD: create→edit→delete), **U3-25** (delete gioco creato da wizard) | Lista private games (`PrivateGamesClient`) — CRUD completo (✏️ Edit, 🗑️ Delete) |
| `(authenticated)/library/private/add` | Flow | **U3-07**, **U3-21**/**U3-25** (create leg del ciclo CRUD) | Wizard 3-step (game→pdf→agent); crea private game |
| `(authenticated)/library/private/[id]` | Smoke | **U3-08** | `PrivateGameHub` |
| `(authenticated)/library/private/[id]/toolkit/configure` | Flow | **U3-09** | `UserToolkitConfiguratorClient` |
| `(authenticated)/library/[gameId]` | Smoke | **U3-10** | `GameDetailDesktop` 5 tab / `LibroGameDetailView` |
| `(authenticated)/library/[gameId]/kb` | Smoke | **U3-11**, **U3-24** (delete PDF via ActionsMenu→DeleteDialog) | `KbHubContent` — KB status + lista PDF + delete PDF (owner-scoped) |
| `(authenticated)/private-games/[id]` | Smoke | **U3-12** | `PrivateGameDetailClient` — header + sezione PDF |
| `(authenticated)/upload` | Flow | **U3-05**, **U3-24** (upload leg del ciclo KB CRUD) | PDF Import Wizard (Admin/Editor); upload → indexing (nessun delete PDF qui) |
| `(authenticated)/knowledge-base` | Smoke | **U3-13** | Redirect → `/library` |
| `(authenticated)/knowledge-base/global` | Flow | **U3-14** | `KbGlobaleView` — ricerca KB globale |
| `(authenticated)/knowledge-base/[id]` | Smoke | **U3-15** | KB detail split-view (chunks + preview) |
| `(authenticated)/knowledge-base/[id]/pdf` | Smoke | **U3-16** | Stub "Coming Soon" → redirect a `[id]` |
| `(authenticated)/kb/[id]` | Smoke | **U3-17** | Redirect → `/knowledge-base/[id]` |
| `(public)/library/shared/[token]` | Smoke | **U3-18** | Libreria pubblica via token (no auth) |
| `(authenticated)/gamebook` | Smoke | **U3-19** | `GamebookIndexView` (v1 fixture stub) |
| `(authenticated)/gamebook/upload` | Smoke | **U3-20** | `GamebookUploadView` (FSM statico, no camera/upload reale) |

**Nessuna route U3 resta scoperta.** 18/18 mappate. Osservazioni sui limiti reali del codice (stub/redirect/role-gate) annotate nelle note sopra e nei singoli scenari.

---

## Scenari

### U3-01 — Library hub (smoke)

```gherkin
Scenario U3-01 [Smoke]: Il library hub carica con hero, tab ed elenco giochi
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And la mia library seed contiene 11 giochi Owned + 1 Wishlist
  When apro /library
  Then vedo lo skeleton che lascia il posto al contenuto reale
    And vedo l'hero della libreria con la CTA "+ Aggiungi gioco"
    And vedo la barra tab con 6 voci (all/games/agents/kb/sessions/chat) con conteggi
    And vedo la griglia con le card dei giochi della mia library (≥11 elementi nel tab "all"/"games")
    And clicco il tab "games" e la griglia mostra le card dei soli giochi (effetto visibile)
  Osservabile ✅: hero + CTA aggiungi + 6 tab con badge conteggio + griglia con ≥11 card gioco + switch tab produce cambio contenuto · nessun errore Console/Network
  Route: (authenticated)/library
  Utente: marco
```

### U3-02 — Aggiungi un gioco alla library dal catalogo (flow)

```gherkin
Scenario U3-02 [Flow]: Aggiungo un gioco del catalogo alla mia library (owned)
  Given sono loggato come marco@meepleai.test
    And "Ticket to Ride" esiste nel catalogo condiviso ma NON è nella mia library seed
  When apro /library e clicco "+ Aggiungi gioco" (URL diventa /library?action=add)
    And nel drawer scelgo la card "Da Catalogo" (choice "catalog")
    And digito "Ticket" nel campo di ricerca del catalogo
    And clicco il pulsante di selezione sulla card "Ticket to Ride"
  Then compare un toast di conferma di aggiunta
    And il drawer si chiude e vengo rediretto a /library/{ticketId}
    And la pagina di dettaglio del gioco carica con il titolo "Ticket to Ride"
  Osservabile ✅: toast success + URL /library/{gameId} + heading dettaglio "Ticket to Ride" (POST /api/v1/library/games/{gameId} 200) · dato marcabile: gioco reale del catalogo, additivo alla library
  Route: (authenticated)/library
  Utente: marco
```

### U3-03 — Wishlist list (smoke)

```gherkin
Scenario U3-03 [Smoke]: La pagina wishlist carica con l'elenco desideri
  Given sono loggato come marco@meepleai.test
    And la mia library seed ha almeno 1 gioco a stato Wishlist (Gloomhaven)
  When apro /library/wishlist
  Then vedo l'heading "My Wishlist"
    And vedo la griglia di MeepleWishlistCard con ≥1 elemento (oppure l'empty-state "Your wishlist is empty" se vuota — entrambi legittimi)
    And vedo il pulsante "Add to Wishlist"
  Osservabile ✅: heading "My Wishlist" + (griglia card wishlist ≥1 OPPURE empty-state legittimo) + pulsante "Add to Wishlist" (GET /api/v1/wishlist) · nessun errore
  Route: (authenticated)/library/wishlist
  Utente: marco
```

### U3-04 — Aggiungi un gioco alla wishlist (flow)

```gherkin
Scenario U3-04 [Flow]: Aggiungo un gioco alla wishlist dalla dialog
  Given sono loggato come marco@meepleai.test
    And apro /library/wishlist
    And conosco l'UUID di un gioco del catalogo (es. Codenames, dal dettaglio /library/{codenamesId})
  When clicco "Add to Wishlist" per aprire la dialog
    And incollo l'UUID del gioco nel campo "Game"
    And seleziono priorità "High"
    And clicco "Add to Wishlist" per confermare
  Then la dialog si chiude
    And l'elenco wishlist si aggiorna mostrando una nuova MeepleWishlistCard
  Osservabile ✅: dialog chiusa + nuova card wishlist presente in griglia (POST /api/v1/wishlist 200/201) · dato marcabile: entry wishlist additiva
  Route: (authenticated)/library/wishlist
  Utente: marco
```

### U3-05 — Upload PDF regole e indicizzazione (flow, admin)

```gherkin
Scenario U3-05 [Flow]: Carico un rulebook PDF e lo vedo raggiungere lo stato indicizzato
  Given sono loggato come admin (la route /upload richiede ruolo Admin/Editor)
    And ho a disposizione il file reale azul-regole-ita.pdf (seed rulebook)
  When apro /upload
    And vedo l'heading "PDF Import Wizard" con lo step indicator (1.Upload → 2.Parse → 3.Review → 4.Publish)
    And nello step 1 seleziono un gioco esistente nel GamePicker (es. "Azul") e clicco "Confirm Game Selection"
    And nel PdfUploadForm carico azul-regole-ita.pdf
  Then l'upload parte (barra/percentuale di caricamento) e al termine il documento compare nella PdfTable
    And attendo che l'indicizzazione asincrona progredisca fino allo stato terminale "Ready"
  Osservabile ✅: heading "PDF Import Wizard" + wizard step + riga PDF nella PdfTable + il documento raggiunge stato Ready (POST /api/v1/ingest/pdf → polling processingState=Ready). Se l'indicizzazione supera il tempo ragionevole → ⚠️ blocked-env
  Route: (authenticated)/upload
  Utente: admin
```

### U3-06 — Private games list (smoke)

```gherkin
Scenario U3-06 [Smoke]: La lista dei giochi privati carica
  Given sono loggato come marco@meepleai.test
  When apro /library/private
  Then vedo lo skeleton che lascia il posto al contenuto reale
    And vedo la lista dei miei giochi privati (card) oppure un empty-state legittimo
    And vedo un'azione per aggiungere un gioco privato
  Osservabile ✅: contenuto reale renderizzato (lista giochi privati o empty-state) + CTA aggiungi (GET /api/v1/private-games) · nessun errore
  Route: (authenticated)/library/private
  Utente: marco
```

### U3-07 — Aggiungi un gioco privato (flow)

```gherkin
Scenario U3-07 [Flow]: Creo un gioco privato con il wizard 3-step (compact game creation)
  Given sono loggato come marco@meepleai.test
  When apro /library/private/add
    And vedo il wizard con gli step (Crea gioco → Carica PDF → Configura agente)
    And nello step "Crea gioco" inserisco titolo "HP-TEST-2026-07-10 Gioco Privato"
    And confermo la creazione del gioco
  Then il gioco privato viene creato
    And il wizard avanza (allo step PDF opzionale) oppure completa e mi porta alla lista/dettaglio
  Osservabile ✅: creazione confermata (POST /api/v1/private-games 200/201) + avanzamento wizard / redirect a lista o dettaglio del nuovo gioco · dato marcato HP-TEST-2026-07-10
  Route: (authenticated)/library/private/add
  Utente: marco
```

### U3-08 — Private game detail hub (smoke)

```gherkin
Scenario U3-08 [Smoke]: Il dettaglio di un gioco privato carica (PrivateGameHub)
  Given sono loggato come marco@meepleai.test
    And ho un gioco privato (creato in U3-07 o già presente)
  When apro /library/private/{privateGameId}
  Then vedo lo skeleton che lascia il posto al contenuto reale
    And vedo l'header del gioco privato (titolo + metadati)
    And vedo le sezioni/azioni del hub (es. PDF/agente/toolkit CTA)
  Osservabile ✅: header gioco privato con titolo + sezioni hub renderizzate (GET /api/v1/private-games/{id}) · nessun errore
  Route: (authenticated)/library/private/[id]
  Utente: marco
```

### U3-09 — Configura toolkit di un gioco privato (flow)

```gherkin
Scenario U3-09 [Flow]: Apro il configuratore toolkit di un gioco privato
  Given sono loggato come marco@meepleai.test
    And ho un gioco privato con un id valido
  When apro /library/private/{privateGameId}/toolkit/configure
  Then vedo il configuratore toolkit (UserToolkitConfiguratorClient) caricare
    And vedo i controlli per selezionare/comporre gli strumenti del toolkit
    And eseguo l'azione primaria di configurazione/salvataggio disponibile
  Then l'azione produce un effetto visibile (conferma/aggiornamento stato toolkit)
  Osservabile ✅: configuratore renderizzato + controlli strumenti + azione primaria con effetto visibile a schermo · dato marcabile se crea un toolkit HP-TEST
  Route: (authenticated)/library/private/[id]/toolkit/configure
  Utente: marco
```

### U3-10 — Apri il dettaglio di un gioco della library (smoke)

```gherkin
Scenario U3-10 [Smoke]: Il dettaglio di un gioco owned carica con i tab
  Given sono loggato come marco@meepleai.test
    And "Azul" è nella mia library seed (Owned) con PDF regole indicizzato
  When apro /library/{azulId}
  Then vedo lo skeleton che lascia il posto al dettaglio del gioco
    And vedo il titolo "Azul"
    And vedo i tab del dettaglio (Info / AI Chat / Toolbox / House Rules / Partite) su desktop
    And clicco un tab diverso da "Info" (es. "House Rules") e il contenuto cambia (URL con ?tab=)
  Osservabile ✅: titolo "Azul" + tab del dettaglio + switch tab produce cambio contenuto e ?tab= in URL (GET /api/v1/library/games/{gameId}) · nessun errore
  Route: (authenticated)/library/[gameId]
  Utente: marco
```

### U3-11 — KB hub di un gioco (smoke)

```gherkin
Scenario U3-11 [Smoke]: Il KB hub di un gioco mostra stato e documenti
  Given sono loggato come marco@meepleai.test
    And "Azul" ha un PDF regole indicizzato (seed KB)
  When apro /library/{azulId}/kb
  Then vedo lo skeleton che lascia il posto al contenuto reale
    And vedo lo stato KB del gioco (documentCount / livello di copertura)
    And vedo la lista dei PDF del gioco con badge di stato (es. "Ready" per il rulebook indicizzato)
  Osservabile ✅: pannello stato KB + lista PDF con almeno 1 riga a stato Ready (GET /api/v1/games/{gameId}/knowledge-base + /library/games/{gameId}/pdfs) · nessun errore
  Route: (authenticated)/library/[gameId]/kb
  Utente: marco
```

### U3-12 — Dettaglio private-games (smoke)

```gherkin
Scenario U3-12 [Smoke]: Il dettaglio /private-games/[id] mostra header e sezione PDF
  Given sono loggato come marco@meepleai.test
    And ho un gioco privato con un id valido
  When apro /private-games/{privateGameId}
  Then vedo lo skeleton (data-testid private-game-loading) che lascia il posto al dettaglio (data-testid private-game-detail)
    And vedo l'header del gioco (titolo + eventuali metadati)
    And vedo la sezione PDF/KB/Chat (PrivateGamePdfSection) con la form/stato di upload
  Osservabile ✅: header con titolo gioco + PrivateGamePdfSection renderizzata (GET /api/v1/private-games/{id}) · nessun errore
  Route: (authenticated)/private-games/[id]
  Utente: marco
```

### U3-13 — /knowledge-base redirect (smoke)

```gherkin
Scenario U3-13 [Smoke]: /knowledge-base reindirizza alla library
  Given sono loggato come marco@meepleai.test
  When navigo verso /knowledge-base
  Then vengo reindirizzato a /library (nessuna pagina di elenco KB standalone esiste)
    And il library hub carica correttamente
  Osservabile ✅: URL finale /library + hero library visibile · nessun errore
  Route: (authenticated)/knowledge-base
  Utente: marco
```

### U3-14 — Ricerca KB globale (flow)

```gherkin
Scenario U3-14 [Flow]: Cerco nella knowledge base globale e ottengo risultati
  Given sono loggato come marco@meepleai.test
    And esistono PDF indicizzati per i miei giochi (seed KB)
  When apro /knowledge-base/global
    And nella HeroSearch vedo il placeholder "Cerca nella knowledge base…" e i documenti recenti (branch home)
    And digito una query di regole (es. "punteggio") e invio la ricerca
  Then l'URL si aggiorna con ?q=... e passo al branch risultati
    And vedo l'header conteggio risultati ("N risultati per «punteggio»") con la lista dei documenti/pagine corrispondenti
    (oppure un empty-state "Nessun risultato" legittimo se la query non matcha)
  Osservabile ✅: HeroSearch + transizione home→risultati con ?q= in URL + lista risultati (o empty-state legittimo) (POST /api/v1/knowledge-base/search/global) · nessun errore
  Route: (authenticated)/knowledge-base/global
  Utente: marco
```

### U3-15 — Apri un documento KB (smoke)

```gherkin
Scenario U3-15 [Smoke]: Il dettaglio di un documento KB mostra chunk e anteprima
  Given sono loggato come marco@meepleai.test
    And conosco l'id di un documento KB indicizzato (es. dalla lista PDF del KB hub Azul, o da /knowledge-base/global)
  When apro /knowledge-base/{docId}
  Then vedo lo skeleton che lascia il posto alla split-view
    And vedo l'header del documento (KbHeader) e il link "Torna alla Libreria"
    And vedo la lista dei chunk nella colonna sinistra
    And il primo chunk è auto-selezionato e la sua anteprima è mostrata nella colonna destra
  Osservabile ✅: split-view (data-slot kb-detail-split-view) + KbHeader + lista chunk ≥1 + anteprima chunk renderizzata (GET /api/v1/kb-docs/{id} + /kb-docs/{id}/chunks) · nessun errore
  Route: (authenticated)/knowledge-base/[id]
  Utente: marco
```

### U3-16 — /knowledge-base/[id]/pdf placeholder (smoke)

```gherkin
Scenario U3-16 [Smoke]: La route PDF-viewer inline è uno stub che reindirizza al dettaglio KB
  Given sono loggato come marco@meepleai.test
    And conosco l'id di un documento KB
  When apro /knowledge-base/{docId}/pdf
  Then vedo la pagina placeholder "Coming Soon" con il messaggio di reindirizzamento
    And dopo ~2s vengo reindirizzato a /knowledge-base/{docId}
    And il dettaglio KB (split-view) carica correttamente
  Osservabile ✅: pagina "Coming Soon" visibile → redirect a /knowledge-base/{docId} → split-view KB caricata · nessun errore. (Nota: il viewer PDF inline non è ancora implementato — happy path = redirect)
  Route: (authenticated)/knowledge-base/[id]/pdf
  Utente: marco
```

### U3-17 — /kb/[id] redirect (smoke)

```gherkin
Scenario U3-17 [Smoke]: Lo shorthand /kb/[id] reindirizza al dettaglio KB canonico
  Given sono loggato come marco@meepleai.test
    And conosco l'id di un documento KB
  When navigo verso /kb/{docId}
  Then vengo reindirizzato a /knowledge-base/{docId}
    And il dettaglio KB (split-view) carica correttamente
  Osservabile ✅: URL finale /knowledge-base/{docId} + split-view KB caricata · nessun errore
  Route: (authenticated)/kb/[id]
  Utente: marco
```

### U3-18 — Libreria condivisa pubblica via token (smoke)

```gherkin
Scenario U3-18 [Smoke]: Una libreria condivisa è visualizzabile pubblicamente via token
  Given esiste un share link attivo per la libreria di un utente
    And ho il token di condivisione (creato via POST /api/v1/library/share da un utente loggato, es. marco)
    And NON sono autenticato (route pubblica)
  When apro /library/shared/{token}
  Then vedo lo skeleton che lascia il posto al contenuto reale
    And vedo l'heading "Libreria di {ownerDisplayName}"
    And vedo il badge conteggio "{N} giochi" e la griglia di MeepleCard dei giochi condivisi
    And vedo la CTA footer "Vuoi creare la tua libreria?" con il pulsante "Inizia Ora"
  Osservabile ✅: heading "Libreria di …" + badge "N giochi" + griglia card giochi (o empty-state "Libreria Vuota" legittimo) + CTA registrazione (GET /api/v1/library/shared/{token}) · nessun errore. Precondizione share link: se non ottenibile → ⚠️ blocked-env
  Route: (public)/library/shared/[token]
  Utente: anonimo (multi-utente: token creato da marco)
```

### U3-19 — Gamebook landing (smoke)

```gherkin
Scenario U3-19 [Smoke]: L'indice gamebook ("I tuoi manuali") carica
  Given sono loggato come marco@meepleai.test
  When apro /gamebook
  Then vedo lo skeleton (fallback) che lascia il posto al contenuto reale
    And vedo l'hero gamebook e la griglia di GamebookCard (dati fixture v1) oppure l'empty-state
    And il widget quota è visibile
  Osservabile ✅: hero gamebook + griglia GamebookCard (o empty-state) + widget quota renderizzati · nessun errore. (Nota: /gamebook è un v1 carryover con dati fixture — endpoint /api/v1/gamebooks non ancora esposto)
  Route: (authenticated)/gamebook
  Utente: marco
```

### U3-20 — Gamebook upload landing (smoke)

```gherkin
Scenario U3-20 [Smoke]: La pagina di caricamento manuale gamebook carica (FSM statico)
  Given sono loggato come marco@meepleai.test
  When apro /gamebook/upload
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo la vista di caricamento manuale (GamebookUploadView) con i suoi passi/stati
  Osservabile ✅: GamebookUploadView renderizzata con la sequenza di step visibile · nessun errore. (Nota: la fotocamera/upload reale NON è ancora cablata — questa route è uno stub FSM visivo; happy path = la vista carica. Il caricamento manuale funzionale è deferito a una sub-issue Interactions)
  Route: (authenticated)/gamebook/upload
  Utente: marco
```

---

## Scenari CRUD & persistenza (spec §3.1)

> Questi scenari **rafforzano** gli happy-path CRUD già presenti aggiungendo le operazioni **Delete** (finora assenti) e il **reload di verifica** dopo ogni create/edit/delete come osservabile di persistenza reale (non solo feedback ottimistico). Coprono le 4 entità gestibili U3. I `Delete` operano **solo** su dati marcati `HP-TEST-2026-07-10` creati dallo scenario stesso — mai su dati seed.
>
> **Disponibilità operazioni per entità** (verificata nel codice: `page.tsx` + componenti + `apps/web/src/lib/api/**`):
>
> | Entità | Create | Edit | Delete | Note sulla superficie UI reale |
> |--------|:------:|:----:|:------:|--------------------------------|
> | **Private game** | ✅ wizard | ✅ dialog | ✅ dialog | `PrivateGamesClient.tsx`: card action ✏️ Edit (`PUT /private-games/{id}`), 🗑️ Delete → `AlertDialog` conferma (`DELETE /private-games/{id}`, soft-delete). CRUD completo su `/library/private`. |
> | **Library entry** | ✅ drawer | ⚠️ **assente in UI raggiungibile** | ✅ bulk-select | Remove via selezione multipla su tab "games" del `LibraryHub` → `BulkSelectionBar` "Archivia" → conferma (`DELETE /library/games/{id}`). Edit (note/preferito/stato → `PATCH /library/games/{id}`) **esiste nei componenti** (`EditNotesModal`, `FavoriteToggle`, `GameActionsModal`) ma la loro unica superficie di montaggio (`GameTableZoneTools`) è **orfana** dopo la migrazione S4 → `GameDetailDesktop` (5 tab): non raggiungibile da una route U3 primaria. Non inventato: annotato assente. |
> | **Wishlist** | ✅ dialog | ❌ **assente in UI** | ✅ card action | `MeepleWishlistCard`: 🗑️ Remove (`DELETE /wishlist/{id}`). Edit: la card espone la prop `onUpdate` (✏️) ma `wishlist/page.tsx` **non la passa** → nessun dialog di modifica cablato. Endpoint `PUT /wishlist/{id}` esiste ma non ha superficie UI. Non inventato: annotato assente. |
> | **KB/PDF doc** | ✅ upload | ❌ **assente in UI** | ✅ solo KB hub gioco | Delete PDF **solo** sul KB hub del gioco `/library/[gameId]/kb` → riga "Open" → `ActionsMenu` → "Delete" → `DeleteDialog` conferma (`DELETE /api/v1/pdf/{id}`, endpoint **owner-scoped**). **Assente** su: wizard `/upload` (`PdfTable` ha solo Log/Retry), dettaglio `/knowledge-base/[id]` (`KbHeader` è stub → `null`), sezione PDF di `/private-games/[id]` (solo upload). Rename/metadata-edit di un KB doc: nessuna UI. |

### U3-21 — Ciclo CRUD gioco privato: crea → edita → elimina (flow)

```gherkin
Scenario U3-21 [Flow]: Ciclo di vita completo di un gioco privato con verifica di persistenza
  Given sono loggato come marco@meepleai.test
    And apro /library/private (lista dei giochi privati, PrivateGamesClient)
  When clicco "Aggiungi gioco" (data-testid add-private-game-btn) e nel wizard/drawer creo un gioco privato con titolo "HP-TEST-2026-07-10 Gioco Privato CRUD" (confermo la creazione)
  Then il nuovo gioco compare come card nella griglia (data-testid games-grid)
    And dopo reload di /library/private la card "HP-TEST-2026-07-10 Gioco Privato CRUD" è ancora presente (persistita — POST /api/v1/private-games)
  When sulla card apro l'azione "✏️ Edit", nel dialog "Modifica gioco" cambio il titolo in "HP-TEST-2026-07-10 Gioco Privato CRUD v2" e clicco "Salva modifiche"
  Then il dialog si chiude e la card mostra il titolo aggiornato "…CRUD v2"
    And dopo reload di /library/private il titolo aggiornato persiste (PUT /api/v1/private-games/{id})
  When sulla card apro l'azione "🗑️ Delete" e nel dialog di conferma clicco il pulsante distruttivo (data-testid confirm-delete-btn)
  Then il dialog si chiude e la card "…CRUD v2" sparisce dalla griglia
    And dopo reload di /library/private la card resta assente (soft-delete — DELETE /api/v1/private-games/{id})
  Osservabile ✅: card presente post-create+reload · titolo "…v2" post-edit+reload · card assente post-delete+reload · nessun errore Console/Network
  Route: (authenticated)/library/private (+ /library/private/add)
  Utente: marco
  Dati creati: "HP-TEST-2026-07-10 Gioco Privato CRUD" → v2 (eliminata a fine ciclo)
```

### U3-22 — Ciclo library entry: aggiungi → rimuovi (flow)

```gherkin
Scenario U3-22 [Flow]: Aggiungo un gioco del catalogo alla library e poi lo rimuovo, con verifica di persistenza
  Given sono loggato come marco@meepleai.test
    And "Ticket to Ride" esiste nel catalogo condiviso ma NON è nella mia library seed
  When apro /library, clicco "+ Aggiungi gioco" (/library?action=add), scelgo "Da Catalogo", cerco "Ticket" e seleziono "Ticket to Ride"
  Then compare il toast di conferma e il gioco risulta nella mia library (POST /api/v1/library/games/{gameId})
    And dopo reload di /library il gioco "Ticket to Ride" è presente nella griglia del tab "games" (persistito)
  When sul tab "games" entro in modalità selezione (long-press su una card → onLongPressEnter), seleziono la card "Ticket to Ride"
    And nella BulkSelectionBar clicco l'azione "Archivia" (data-slot library-bulk-selection-archive) e confermo nell'AlertDialog
  Then la card "Ticket to Ride" sparisce dalla griglia (DELETE /api/v1/library/games/{gameId})
    And dopo reload di /library il gioco resta assente dalla mia library
  Osservabile ✅: gioco presente post-add+reload · assente post-remove+reload · toast/BulkSelectionBar + conteggio tab aggiornati · nessun errore. (Nota: la modalità selezione si attiva via long-press sulla card nel tab "games"; il remove è un'azione bulk sulla selezione)
  Route: (authenticated)/library
  Utente: marco
  Dati creati: library entry "Ticket to Ride" (rimossa a fine ciclo — additiva, il catalogo/gioco NON viene toccato)
```

### U3-23 — Ciclo wishlist: aggiungi → rimuovi (flow)

```gherkin
Scenario U3-23 [Flow]: Aggiungo un gioco alla wishlist e poi lo rimuovo, con verifica di persistenza
  Given sono loggato come marco@meepleai.test
    And apro /library/wishlist
    And conosco l'UUID di un gioco del catalogo NON già in wishlist (es. Codenames, dal dettaglio /library/{codenamesId})
  When clicco "Add to Wishlist", incollo l'UUID del gioco, seleziono priorità "High" e confermo con "Add to Wishlist"
  Then la dialog si chiude e compare una nuova MeepleWishlistCard con badge priorità "Alta" (POST /api/v1/wishlist)
    And dopo reload di /library/wishlist la card è ancora presente (persistita)
  When sulla card apro l'azione "🗑️ Remove"
  Then la card sparisce dalla griglia wishlist (DELETE /api/v1/wishlist/{id})
    And dopo reload di /library/wishlist la card resta assente (o compare l'empty-state "Your wishlist is empty" se era l'unica)
  Osservabile ✅: card wishlist presente post-add+reload · assente post-remove+reload · nessun errore. (Nota: l'EDIT wishlist NON è cablato in UI — la card espone la prop onUpdate ma la pagina non la passa; la priorità si imposta solo in fase di add)
  Route: (authenticated)/library/wishlist
  Utente: marco
  Dati creati: entry wishlist (rimossa a fine ciclo — additiva)
```

### U3-24 — Ciclo KB/PDF doc: upload → indicizzazione → elimina (flow, admin)

```gherkin
Scenario U3-24 [Flow]: Carico un rulebook PDF marcato e poi lo elimino dal KB hub del gioco, con verifica di persistenza
  Given sono loggato come admin (la route /upload richiede ruolo Admin/Editor)
    And ho a disposizione il file reale azul-regole-ita.pdf (seed rulebook) da caricare come documento HP-TEST aggiuntivo
  When apro /upload, nello step 1 seleziono il gioco "Azul" e confermo, poi nel PdfUploadForm carico azul-regole-ita.pdf
  Then l'upload parte e il documento compare nella PdfTable (POST /api/v1/ingest/pdf)
    And attendo che l'indicizzazione asincrona raggiunga lo stato terminale "Ready" (se eccede il tempo ragionevole → ⚠️ blocked-env)
    And dopo reload, aprendo /library/{azulId}/kb, il PDF appena caricato è elencato tra i documenti del gioco (persistito)
  When sul KB hub /library/{azulId}/kb, sulla riga del PDF HP-TEST clicco "Open" → nell'ActionsMenu scelgo "Delete" → nel DeleteDialog confermo il pulsante distruttivo
  Then il PDF sparisce dalla lista documenti del KB hub (DELETE /api/v1/pdf/{pdfId})
    And dopo reload di /library/{azulId}/kb il PDF eliminato resta assente
  Osservabile ✅: PDF presente in PdfTable/KB hub post-upload+reload (stato Ready) · assente dal KB hub post-delete+reload · nessun errore. (Note: l'upload catalogo è Admin/Editor-only → persona admin; il delete PDF è un endpoint owner-scoped disponibile solo sul KB hub del gioco. Si elimina il documento HP-TEST appena caricato, mai un PDF seed preesistente)
  Route: (authenticated)/upload → (authenticated)/library/[gameId]/kb
  Utente: admin
  Dati creati: documento KB HP-TEST su "Azul" (eliminato a fine ciclo)
```

### U3-25 — Elimina un gioco privato dal wizard di creazione a valle (flow)

```gherkin
Scenario U3-25 [Flow]: Un gioco privato creato tramite il wizard 3-step è eliminabile dalla lista, con verifica di persistenza
  Given sono loggato come marco@meepleai.test
  When apro /library/private/add e nel wizard (Crea gioco → Carica PDF → Configura agente) creo un gioco privato "HP-TEST-2026-07-10 Wizard Cleanup" completando lo step "Crea gioco"
  Then il gioco privato viene creato (POST /api/v1/private-games) e — completato/uscito dal wizard — apro /library/private
    And la card "HP-TEST-2026-07-10 Wizard Cleanup" è presente nella griglia
    And dopo reload di /library/private la card persiste
  When sulla card apro "🗑️ Delete" e confermo nel dialog distruttivo (data-testid confirm-delete-btn)
  Then la card sparisce dalla griglia
    And dopo reload di /library/private la card resta assente (DELETE /api/v1/private-games/{id})
  Osservabile ✅: gioco creato-da-wizard presente in /library/private post-reload · assente post-delete+reload · nessun errore. (Complementa U3-07, che copre solo la creazione: qui si verifica il cleanup persistente del dato creato dal wizard)
  Route: (authenticated)/library/private/add → (authenticated)/library/private
  Utente: marco
  Dati creati: "HP-TEST-2026-07-10 Wizard Cleanup" (eliminata a fine ciclo)
```

---

## Auto-verifica (autore)

- **Copertura route**: 18/18 route U3 di `_coverage-map.md` presenti nella matrice → almeno 1 scenario ciascuna. Nessun buco. ✅
- **Osservabili**: ogni scenario (U3-01…U3-25) dichiara `Osservabile ✅` con marker strutturali (heading, badge, griglia, redirect URL, chip stato). ✅
- **CRUD & persistenza (spec §3.1)**: gli scenari U3-21…U3-25 aggiungono le operazioni **Delete** (finora assenti) + il **reload di verifica** dopo ogni create/edit/delete come osservabile di persistenza. Entità con ciclo coperto: **Private game** (create→edit→delete, U3-21/U3-25) · **Library entry** (add→remove, U3-22) · **Wishlist** (add→remove, U3-23) · **KB/PDF doc** (upload→delete, U3-24). Operazioni **assenti in UI raggiungibile** (annotate, non inventate): Library-entry **edit** (superficie `GameTableZoneTools` orfana post-migrazione S4); Wishlist **edit** (prop `onUpdate` non cablata in pagina); KB/PDF **edit/rename** (nessuna UI) e delete su wizard `/upload`/dettaglio `/knowledge-base/[id]`/sezione private-game PDF. ✅
- **Solo happy path**: nessuno scenario negativo/errore/edge; gli empty-state sono ammessi come esito legittimo dove il seed potrebbe non popolare. ✅
- **Flow vs Smoke** allineati al comportamento reale del codice (non solo alla stima della mappa):
  - `knowledge-base/[id]/pdf` è marcata Flow nella mappa ma è uno **stub "Coming Soon"+redirect** → declassata a **Smoke (redirect)** con nota (U3-16).
  - `gamebook/upload` è marcata Flow nella mappa ma è un **FSM statico senza upload reale** → **Smoke** con nota (U3-20).
  - `knowledge-base` è redirect a `/library` → **Smoke (redirect)** (U3-13). `kb/[id]` redirect (U3-17).
- **Dati marcati**: entità create dai Flow usano `HP-TEST-2026-07-10` (U3-07 gioco privato; wishlist/library additivi in U3-02/U3-04; toolkit HP-TEST opzionale in U3-09; cicli CRUD U3-21…U3-25 tutti su dati HP-TEST creati dallo scenario). ✅
- **Delete solo su dati HP-TEST**: ogni `Delete` (U3-21/U3-22/U3-23/U3-24/U3-25) opera esclusivamente sull'entità creata dallo scenario stesso, mai su dati seed condivisi (in U3-24 si elimina il PDF HP-TEST appena caricato, non un rulebook seed). ✅
- **Vincoli ambientali dichiarati**: `/upload` Admin/Editor-only (U3-05/U3-24 usano admin); indicizzazione PDF lenta → `blocked-env` se eccede (U3-05/U3-24); share link pubblico come precondizione (U3-18). ✅
- **Endpoint tracciati** (da `apps/web/src/lib/api/**`): add-library `POST /api/v1/library/games/{id}`, **remove-library** `DELETE /api/v1/library/games/{id}`; wishlist `POST /api/v1/wishlist`, **remove-wishlist** `DELETE /api/v1/wishlist/{id}`; upload `POST /api/v1/ingest/pdf`, **delete-pdf** `DELETE /api/v1/pdf/{id}` (owner, solo KB hub); private games `POST/GET /api/v1/private-games`, **edit** `PUT /api/v1/private-games/{id}`, **delete** `DELETE /api/v1/private-games/{id}` (soft); library-entry edit `PATCH /api/v1/library/games/{id}` (esiste, superficie UI orfana); KB globale `POST /api/v1/knowledge-base/search/global`; KB detail `GET /api/v1/kb-docs/{id}(+/chunks)`; shared library `GET /api/v1/library/shared/{token}`; game KB `GET /api/v1/games/{id}/knowledge-base` + `/api/v1/library/games/{id}/pdfs`. ✅

**Totale U3: 25 scenari** — Flow: 11 (U3-02, U3-04, U3-05, U3-07, U3-09, U3-14, U3-21, U3-22, U3-23, U3-24, U3-25) · Smoke: 14 (U3-01, U3-03, U3-06, U3-08, U3-10, U3-11, U3-12, U3-13, U3-15, U3-16, U3-17, U3-18, U3-19, U3-20; i due hub con doppia natura U3-01/U3-03 restano smoke). Cicli CRUD con verifica di persistenza (reload): 5 (U3-21…U3-25), che coprono **Delete per 4 entità gestibili** (private game, library entry, wishlist, KB/PDF doc).
