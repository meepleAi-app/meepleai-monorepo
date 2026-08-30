# Happy Path — U4 · Chat RAG & Agenti

> Catalogo scenari **happy-path** per l'area U4. Solo percorso di successo. Osservabili **strutturali** (presenza risposta, ≥1 chip citazione, streaming avvenuto), **mai** testo letterale generato da LLM. Formato e legenda: [`_TEMPLATE.md`](./_TEMPLATE.md). Mappa route globale: [`_coverage-map.md`](./_coverage-map.md) § U4.

## Intestazione

- **Area**: U4 — Chat RAG & Agenti (chat in-game, thread `/chat`, catalogo agenti, editor RuleSpec/proposte).
- **Prerequisiti dati (seed `make seed-sp4`)**:
  - Giochi con **PDF regole indicizzato** (`kbDocs[]._targetState = "indexed"`): Azul (`azul-regole-ita.pdf` + `azul-rules-en.pdf`), Catan, Wingspan, Brass, Ark Nova, Spirit Island, 7 Wonders Duel, Codenames, Carcassonne, Ticket to Ride, Pandemic, Terraforming Mars (`data.json:kbDocs[]`).
  - **marco** possiede in libreria (`Owned`) Azul + 11 altri giochi indicizzati → chat-in-game abilitabile su tutti.
  - **13 agenti RAG** seed (`data.json:agents[]`): es. `Azul Rules Expert` (owner marco, game azul, kb azul-ita+eng), `Brass Helper` (marco), `7 Wonders Duel Coach` (marco), `Catan Coach` (luca), `Wingspan Rules` (sara), `Game Night Host` (sara, standalone senza gioco).
  - **5 thread chat** seed (`data.json:chats[]`): `Come si gioca ad Azul?`, `Come calcolo i bonus di riga?` (marco/azul), `Strategia inizio partita` (luca/catan), `Link industry obbligatorio?` (marco/brass), `Azione Guadagna cibo?` (sara/wingspan).
- **Utenti**:
  - **marco** (`marco@meepleai.test`, premium, verificato, ruolo `User`) → tutti gli scenari chat/agenti lato utente.
  - **admin** (da `infra/secrets/admin.secret`, ruolo `Admin`) → **obbligatorio** per `/editor` e `/editor/agent-proposals` (protetti da `RequireRole={['Admin','Editor']}` — un `User` vede "Access Denied").
- **🔴 Ambiente**: l'area U4 richiede lo **stack AI completo** (`cd infra && make dev`, **non** `make dev-core`). Senza embedding/reranker/LLM la QA-stream non produce risposta né citazioni → gli scenari Flow di chat vanno marcati **⚠️ blocked-env** (spec §10).
- **🔴 Non-determinismo LLM**: il testo della risposta cambia a ogni run. Gli osservabili sono **strutturali** (bolla risposta non-vuota, chip/card citazione presente, indicatore streaming/typing comparso, connessione "Connesso"), **mai** una stringa attesa.

### Note architetturali rilevate durante l'esplorazione (impattano gli scenari)

1. **`/library/[gameId]/agent` è un redirect 307** → `/library/{gameId}?tab=aiChat` (`app/(authenticated)/library/[gameId]/agent/page.tsx`). La chat non è più una pagina a sé: vive nel **tab "AI Chat"** del dettaglio gioco. L'esempio spec §5 U4-03 resta valido come *flusso*, ma l'osservabile finale (chip → PDF) usa `CitationModal` (tab "PDF originale"), non un page-jump.
2. **`/hub/agents` è un redirect** → `/agents` (`app/(authenticated)/hub/agents/page.tsx`, issue #2153). È coperto come smoke del redirect.
3. **`/editor/agent-proposals/create` e `/editor/agent-proposals/[id]/edit` sono pagine "Feature Removed"**: le typology proposals sono state sostituite dalle Agent Definitions gestite dagli admin. Non c'è più un flusso reale di creazione/modifica proposta → coperte come smoke della pagina statica, **non** come Flow.
4. **`/editor/agent-proposals/[id]/test`** (Test Sandbox) è ancora funzionale ma richiede una **proposta Draft esistente**; il seed SP4 non crea typology proposals → la lista è verosimilmente vuota e il test non è raggiungibile con dati seed → **⚠️ blocked-env / skip** (nessun `[id]` valido).
5. **Chat inline (`GameChatTab` / `useGameChat`)** e **thread `/chat/[threadId]` (`ChatThreadView` / `qaStream`)** sono due UI diverse sullo stesso backend SSE (`POST /agents/qa/stream`, eventi TOKEN=7, CITATIONS, COMPLETE=4). Entrambe rendono citazioni cliccabili: inline → `CitationChip`→`CitationModal`; thread → `RuleSourceCard`/`CitationBlock` + `ChatInfoPanel`.

---

## Matrice di copertura

| Route | Liv. atteso | Scenario/i | Note |
|-------|------|-----------|------|
| `(chat)/chat` | Flow | U4-01 | Lista thread seed raggruppati per agente + apertura thread |
| `(chat)/chat/new` | Flow | U4-02 | Entry orchestrator (game + agent picker) → crea thread |
| `(chat)/chat/[threadId]` | Flow | U4-03, U4-04 | Streaming SSE + citazioni + apertura PDF sorgente |
| `(chat)/chat/agents/create` | Flow | U4-05 | Wizard 4-step crea agente utente → `POST /agents/user` → redirect |
| `(authenticated)/library/[gameId]/agent` | Flow | U4-06 (redirect), U4-07 (chat-in-game), U4-08 (citazione→PDF) | Redirect 307 → tab AI Chat; chat inline + `CitationModal` |
| `(authenticated)/agents` | Smoke | U4-09 | Catalogo agenti (hero + filtri + grid) |
| `(authenticated)/agents/[id]` | Smoke | U4-10, U4-11 | Dettaglio agente (hero + tabs); CTA "Gioca" → `/chat/new?agentId=` |
| `(authenticated)/hub/agents` | Smoke | U4-12 | Redirect → `/agents` |
| `(authenticated)/pipeline-builder` | Smoke | U4-13 | Canvas builder RAG carica |
| `(authenticated)/editor` | Smoke | U4-14, U4-15 | Landing editor (guard ruolo) + editor RuleSpec con `?gameId=` |
| `(authenticated)/editor/agent-proposals` | Smoke | U4-16 | Lista proposte (empty-state legittimo) |
| `(authenticated)/editor/agent-proposals/create` | Smoke | U4-17 | Pagina "Feature Removed" (flusso rimosso) |
| `(authenticated)/editor/agent-proposals/[id]/edit` | smoke-aggregato (U4-17) | — | Stessa pagina statica "Feature Removed"; nessun `[id]` seed → coperta con create |
| `(authenticated)/editor/agent-proposals/[id]/test` | skip: `blocked-env` | — | Test Sandbox richiede una proposta Draft; il seed SP4 non ne crea → nessun `[id]` valido (vedi Nota 4) |

Copertura: **13/14 route** con scenario esplicito o smoke-aggregato; **1 route** (`[id]/test`) marcata `skip: blocked-env` con motivo. Nessun buco silenzioso.

---

## Scenari

```gherkin
Scenario U4-01 [Flow]: Lista thread chat raggruppati per agente
  Given sono loggato come marco@meepleai.test
    And il seed ha creato 2 thread di marco su Azul ("Come si gioca ad Azul?", "Come calcolo i bonus di riga?")
        e 1 thread su Brass ("Link industry obbligatorio?")
  When apro /chat
  Then la lista carica (skeleton → contenuto) e mostra i thread raggruppati per agente
    And vedo la sezione/gruppo dell'agente "Azul Rules Expert" con ≥1 card thread
    And clicco la card "Come si gioca ad Azul?"
  Then vengo navigato su /chat/{threadId} e la conversazione si apre
  Osservabile ✅: header "Le tue Chat" (desktop) · ≥1 card thread visibile · click card → URL /chat/{id} + vista thread montata (data-testid="chat-thread-view")
  Route: (chat)/chat, (chat)/chat/[threadId]
  Utente: marco
```

```gherkin
Scenario U4-02 [Flow]: Nuova chat dal picker gioco + agente
  Given sono loggato come marco@meepleai.test
    And ho in libreria Azul con PDF regole indicizzato
  When apro /chat/new
  Then vedo la schermata di avvio con selezione gioco e agente (griglia MeepleCard)
    And seleziono il gioco "Azul" e un agente/tipo
    And avvio la conversazione (crea thread)
  Then vengo portato in un thread /chat/{threadId} pronto a ricevere il primo messaggio
  Osservabile ✅: griglia di selezione visibile · dopo la creazione URL = /chat/{id} · input messaggio presente e abilitato
  Route: (chat)/chat/new
  Utente: marco
```

```gherkin
Scenario U4-03 [Flow]: Risposta citata su una regola di gioco (SSE + citazioni)
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And lo stack AI è attivo (make dev, non dev-core)
    And apro un thread su Azul da /chat (es. "Come si gioca ad Azul?") — Azul ha PDF regole indicizzato
  When scrivo "Quanti punti vale completare una riga orizzontale?" e invio
  Then compare il messaggio utente (ottimistico) e parte la risposta AI in streaming (SSE POST /agents/qa/stream)
    And vedo l'indicatore di streaming/typing durante la generazione
    And al termine la bolla risposta dell'assistente è non-vuota
    And compare ≥1 citazione della fonte (card "RuleSourceCard" o chip citazione) e/o citazioni raccolte nel pannello info a destra
  Osservabile ✅: bolla utente presente · indicatore streaming comparso (data-testid="message-streaming" o chip "Connesso"/typing) · bolla assistente con testo non-vuoto · ≥1 elemento citazione (RuleSourceCard / CitationBlock / voce in ChatInfoPanel) — MAI testo letterale
  Route: (chat)/chat/[threadId]
  Utente: marco
  ⚠️ Richiede stack AI completo. Se AI giù → blocked-env.
```

```gherkin
Scenario U4-04 [Flow]: Click su citazione apre il PDF sorgente alla pagina
  Given ho completato U4-03 e la risposta contiene ≥1 citazione con pagina (es. [Azul, p.N])
  When clicco sulla citazione (chip/card sorgente)
  Then si apre il visualizzatore del PDF sorgente posizionato sulla pagina citata
    And vedo il titolo "Sorgente — Pagina N" (PdfPageModal) oppure la modale citazione con tab "PDF originale" (CitationModal) e lo snippet citato
  Osservabile ✅: modale/pannello PDF aperto · intestazione mostra il numero di pagina citato · rendering della pagina PDF (o tab "PDF originale" montato) · snippet citazione visibile
  Route: (chat)/chat/[threadId]
  Utente: marco
  ⚠️ Dipende da U4-03 (richiede stack AI completo).
```

```gherkin
Scenario U4-05 [Flow]: Wizard creazione agente utente (4 step)
  Given sono loggato come marco@meepleai.test
    And ho in libreria ≥1 gioco con PDF indicizzato (es. Azul)
  When apro /chat/agents/create
    And Step 1: seleziono il gioco "Azul" dalla libreria
    And Step 2: scelgo un tipo agente (es. Tutor)
    And Step 3: assegno il nome "HP-TEST-2026-07-10 Azul Tutor" e seleziono il/i PDF KB
    And Step 4: rivedo il riepilogo e confermo la creazione
  Then l'agente viene creato (POST /api/v1/agents/user) e vengo reindirizzato a /chat/new
  Osservabile ✅: wizard a 4 step naviga avanti (step indicator avanza) · alla conferma navigazione verso /chat/new · nessun errore Console/Network · (verifica additiva) il nuovo agente compare in /agents
  Route: (chat)/chat/agents/create
  Utente: marco
```

```gherkin
Scenario U4-06 [Flow]: Redirect legacy agente → tab AI Chat del gioco
  Given sono loggato come marco@meepleai.test
    And conosco l'id del gioco Azul in libreria (gameId)
  When navigo direttamente a /library/{azulId}/agent
  Then vengo reindirizzato (307) a /library/{azulId}?tab=aiChat
    And il dettaglio gioco Azul si apre con il tab "AI Chat" attivo
  Osservabile ✅: URL finale contiene ?tab=aiChat · il pannello tab "AI Chat" è renderizzato (role="tabpanel" aria-labelledby="game-tab-aiChat")
  Route: (authenticated)/library/[gameId]/agent
  Utente: marco
```

```gherkin
Scenario U4-07 [Flow]: Chat-in-game inline (tab AI Chat) con risposta citata
  Given sono loggato come marco@meepleai.test
    And apro il dettaglio di Azul sul tab AI Chat (/library/{azulId}?tab=aiChat)
    And Azul ha ≥1 documento KB in stato "indexed" (la chat inline è abilitata)
    And lo stack AI è attivo
  When scrivo nella input "Come si assegnano i punti alla fine della partita?" e invio
  Then compare la bolla utente e parte la risposta dell'agente (Tutor) con indicatore "Cerco nella KB"
    And al termine la bolla agente è non-vuota
    And per una risposta grounded compaiono ≥1 CitationChip (📖 p. N — …) e un ConfidenceBadge
  Osservabile ✅: bolla utente presente · TypingIndicator comparso durante la generazione · bolla agente non-vuota · ≥1 CitationChip visibile (data-slot="citation-chip") · ConfidenceBadge presente — MAI testo letterale
  Route: (authenticated)/library/[gameId]/agent (→ ?tab=aiChat)
  Utente: marco
  ⚠️ Richiede stack AI completo. Se AI giù → blocked-env.
```

```gherkin
Scenario U4-08 [Flow]: Anteprima citazione inline → tab "PDF originale"
  Given ho completato U4-07 e la risposta contiene ≥1 CitationChip (📖 p. N)
  When clicco il CitationChip
  Then si apre la CitationModal con il tab "Snippet" attivo che mostra il testo della citazione
    And clicco il tab "PDF originale"
  Then il tab PDF viene montato (lazy) e mostra il documento sorgente alla pagina citata
  Osservabile ✅: modale citazione aperta (data-slot="citation-modal") · intestazione "📖 p. N" · tab "Snippet" con testo citazione · click "PDF originale" → CitationPdfTab montato (rendering pagina o gate ownership legittimo)
  Route: (authenticated)/library/[gameId]/agent (→ ?tab=aiChat)
  Utente: marco
  ⚠️ Dipende da U4-07 (richiede stack AI completo).
```

```gherkin
Scenario U4-09 [Smoke]: Catalogo agenti carica con dati
  Given sono loggato come marco@meepleai.test
    And il seed ha creato agenti RAG (incl. quelli di marco: Azul Rules Expert, Brass Helper, 7 Wonders Duel Coach)
  When apro /agents
  Then la pagina carica (skeleton → contenuto) mostrando l'hero con le statistiche e i filtri
    And la griglia risultati mostra ≥1 card agente
    And digito "Azul" nel filtro di ricerca
  Then la griglia si restringe agli agenti che matchano (effetto visibile a schermo)
  Osservabile ✅: hero agenti + barra filtri visibili · ≥1 card agente in griglia (data-slot="agents-library-view") · filtro ricerca modifica il conteggio/righe · nessun errore 4xx/5xx/Console
  Route: (authenticated)/agents
  Utente: marco
```

```gherkin
Scenario U4-10 [Smoke]: Dettaglio agente con tabs
  Given sono loggato come marco@meepleai.test
    And apro /agents e conosco l'id di un agente di marco (es. "Azul Rules Expert")
  When apro /agents/{agentId}
  Then il dettaglio carica con l'hero (nome agente, avatar 🤖, meta tipo/modello/invocazioni)
    And vedo la barra tab (Identità / Conoscenza / Performance / Storico / Impostazioni)
    And clicco il tab "Conoscenza"
  Then il pannello Conoscenza si apre mostrando i documenti KB dell'agente (o empty-state legittimo)
  Osservabile ✅: hero agente con nome visibile (data-slot="agent-detail-view") · role="tablist" con le 5 tab · click tab "Conoscenza" → pannello knowledge visibile (lista doc o stato vuoto)
  Route: (authenticated)/agents/[id]
  Utente: marco
```

```gherkin
Scenario U4-11 [Flow]: CTA "Gioca" dal dettaglio agente pre-seleziona l'agente in nuova chat
  Given sono loggato come marco@meepleai.test
    And apro il dettaglio di un agente attivo di marco (/agents/{agentId})
  When clicco la CTA "Gioca" nell'hero dell'agente
  Then vengo portato a /chat/new con l'agente pre-selezionato (?agentId={agentId})
  Osservabile ✅: URL destinazione = /chat/new?agentId={agentId} · la schermata nuova chat si apre con l'agente pre-selezionato
  Route: (authenticated)/agents/[id], (chat)/chat/new
  Utente: marco
```

```gherkin
Scenario U4-12 [Smoke]: Redirect /hub/agents → /agents
  Given sono loggato come marco@meepleai.test
  When navigo a /hub/agents
  Then vengo reindirizzato al catalogo canonico /agents
    And il catalogo agenti si apre
  Osservabile ✅: URL finale = /agents · hero + griglia agenti renderizzati · nessun errore Console/Network
  Route: (authenticated)/hub/agents
  Utente: marco
```

```gherkin
Scenario U4-13 [Smoke]: Pipeline Builder — il canvas carica
  Given sono loggato come marco@meepleai.test
  When apro /pipeline-builder
  Then la pagina carica (skeleton a 3 pannelli → contenuto) mostrando il builder visuale della pipeline RAG
    And vedo il pannello sinistro (nodi), il canvas centrale e il pannello destro (proprietà)
  Osservabile ✅: layout builder a 3 colonne renderizzato (non resta sullo skeleton) · nessun errore 4xx/5xx/Console
  Route: (authenticated)/pipeline-builder
  Utente: marco
```

```gherkin
Scenario U4-14 [Smoke]: Editor RuleSpec — landing con guard ruolo (senza gameId)
  Given sono loggato come admin (ruolo Admin, da admin.secret)
  When apro /editor
  Then supero il guard RequireRole (Admin/Editor) e la pagina editor si carica
    And senza query string vedo il messaggio che richiede un gameId ("Specifica un gameId nella query string")
  Osservabile ✅: nessun "Access Denied" (admin autorizzato) · intestazione "Editor RuleSpec" · messaggio/hint di gameId mancante visibile
  Route: (authenticated)/editor
  Utente: admin
```

```gherkin
Scenario U4-15 [Flow]: Editor RuleSpec carica lo spec di un gioco
  Given sono loggato come admin
    And conosco un gameId con RuleSpec disponibile (es. il gioco Azul / demo)
  When apro /editor?gameId={gameId}
  Then l'editor carica il RuleSpec: pannello editor (rich/JSON) + pannello Preview
    And il badge di validazione mostra "Contenuto valido"
    And il Preview elenca le informazioni gioco e le regole (atoms)
  Osservabile ✅: intestazione "Editor RuleSpec" con "Game: {gameId}" · editor + preview affiancati · "✓ Contenuto valido" · Preview con tabella info gioco / lista regole
  Route: (authenticated)/editor
  Utente: admin
```

```gherkin
Scenario U4-16 [Smoke]: Lista proposte agente (editor)
  Given sono loggato come admin (autorizzato all'editor)
  When apro /editor/agent-proposals
  Then supero il guard e vedo la pagina "My Typology Proposals" con il pulsante "Create Proposal"
    And la lista carica (skeleton → contenuto): con il seed SP4 non ci sono proposte → empty-state "No proposals yet"
  Osservabile ✅: intestazione "My Typology Proposals" · pulsante "Create Proposal" presente · empty-state "No proposals yet" (o tabella proposte se presenti) · nessun errore Console/Network
  Route: (authenticated)/editor/agent-proposals
  Utente: admin
```

```gherkin
Scenario U4-17 [Smoke]: Pagina creazione proposta = "Feature Removed"
  Given sono loggato come admin
  When apro /editor/agent-proposals/create
  Then la pagina statica "Feature Removed" spiega che le typology proposals sono sostituite dalle Agent Definitions
    And è presente il pulsante "Back to Proposals" che riporta a /editor/agent-proposals
  Osservabile ✅: titolo "Feature Removed" visibile · testo che rimanda alle Agent Definitions · click "Back to Proposals" → /editor/agent-proposals · (stessa pagina statica vale per .../[id]/edit — smoke-aggregato)
  Route: (authenticated)/editor/agent-proposals/create (+ [id]/edit smoke-aggregato)
  Utente: admin
```

---

## Auto-verifica

- **Copertura route**: le 14 route U4 di `_coverage-map.md` sono tutte in matrice. 13 hanno scenario esplicito o smoke-aggregato; 1 (`editor/agent-proposals/[id]/test`) è `skip: blocked-env` con motivo documentato (Nota 4: nessuna proposta Draft nel seed).
- **Ogni scenario ha ≥1 osservabile** concreto (`Osservabile ✅`) e dichiara `Route` + `Utente`.
- **Solo happy path**: nessuno scenario negativo/errore/edge. Gli stati "Feature Removed" e gli empty-state seed sono trattati come *esiti legittimi* di smoke (pagina carica correttamente), non come fallimenti.
- **Osservabili strutturali per l'LLM**: U4-03/04/07/08 (e il ramo generativo di U4-05) non asseriscono mai testo letterale; usano presenza bolla, indicatore streaming/typing, presenza card/chip citazione, apertura modale PDF con numero pagina.
- **Dipendenze AI segnalate**: gli scenari che invocano `qaStream` (U4-03, U4-04, U4-07, U4-08) sono marcati "richiede stack AI completo → blocked-env se AI giù" (spec §10; `make dev`, non `dev-core`).
- **Ruoli**: gli scenari editor (U4-14…U4-17) usano **admin** perché `RequireRole={['Admin','Editor']}` / `EditorAuthGuard` bloccano un `User` — coerente con il codice.
- **Dati marcati**: l'unico Flow che *crea* un'entità (U4-05, nuovo agente) usa il marcatore `HP-TEST-2026-07-10` nel nome (spec §7.1). Gli altri Flow riusano thread/agenti/PDF seed (additivo, non distruttivo).
- **Redirect verificati nel codice**: U4-06 (`library/[gameId]/agent` → `?tab=aiChat`), U4-12 (`hub/agents` → `/agents`) confermati leggendo i rispettivi `page.tsx`.
