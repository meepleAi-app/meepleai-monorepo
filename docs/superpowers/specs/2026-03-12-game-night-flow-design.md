# Design: Serata di Gioco Improvvisata — User Story End-to-End

**Status**: Approved
**Date**: 2026-03-12
**Author**: AI-assisted design session

## Overview

User story che copre l'intero flusso dalla scoperta di un gioco su BGG fino al salvataggio della partita per riprenderla in un secondo momento. Attraversa 6 bounded context: UserLibrary, SharedGameCatalog, DocumentProcessing, KnowledgeBase, SessionTracking, GameToolkit.

### La Storia

> Amici arrivano inaspettatamente. Cerco il gioco nell'app — non c'e'. Lo cerco su BGG, lo trovo, lo aggiungo come gioco privato posseduto. Carico il PDF del regolamento, accetto il disclaimer copyright. Ricevo notifica quando e' pronto. Creo l'agente RAG. L'agente ci aiuta a preparare la tavola, tracciare i punteggi, e arbitrare dispute sulle regole. A fine serata salviamo lo stato della partita per riprendere dopo.

### Decisioni di Design

| Decisione | Scelta | Motivazione |
|-----------|--------|-------------|
| Target utente | Progressive disclosure (nuovi + esperti) | Wizard per nuovi, shortcut per esperti |
| Flow di attivazione | Smart Hub con checklist nella pagina gioco | Un solo punto di riferimento per il flusso pre-partita |
| Interazione agente in partita | 3 tab esplicite (Setup, Regole, Punteggi) | Separazione chiara dei ruoli agente |
| Salva e riprendi | Punteggi + turno automatici, foto/note/riepilogo AI opzionali | Salvataggio zero-friction, arricchimento opzionale |
| Multi-device | Single-device per ora | Backend pronto per futuro (SessionCode, ruoli Host/Player/Spectator) |
| Attesa PDF processing | Progress bar + notifica (entrambi) | L'utente sceglie se aspettare o navigare via |

### Approccio Architetturale: Smart Hub

La pagina dettaglio gioco (`/games/{id}`) diventa l'hub centrale con checklist interattiva. La sessione live vive in una pagina dedicata (`/sessions/{id}`). Questo approccio bilancia semplicita' di implementazione (~12-15 giorni) con esperienza utente coerente.

---

## Backend Readiness

| Step | Feature | Backend Status | Entita'/Endpoint |
|------|---------|---------------|------------------|
| 1 | Cerca nella libreria | ✅ Done | `GetUserLibraryQuery` |
| 2 | Cerca su BGG | ✅ Done | `GET /bgg/search` (cache 1h, rate limit 20/h) |
| 3a | Aggiungi come gioco privato | ✅ Done | `AddPrivateGameCommand(source: BGG)` |
| 3b | Marca come posseduto | ✅ Done | `GameState.Owned` su `UserLibraryEntry` |
| 4 | Carica PDF | ✅ Done | Pipeline 7 stati, chunked upload, S3 |
| 5 | Disclaimer copyright | ✅ Done | `AcceptCopyrightDisclaimerCommand`, `CopyrightDisclaimerAcceptedAt` |
| 6 | Notifica PDF pronto | ✅ Done | `PdfNotificationEventHandler` multi-canale, SSE `/notifications/stream` |
| 7 | Crea agente RAG | ✅ Done | `POST /agents/user` (tier-aware), auto-associa documenti |
| 8 | Agente setup tavola | ✅ Done | `TutorAgent` + RAG 6-layer |
| 9 | Tracciamento punteggi | ✅ Done | `ScoreEntry`, `GameToolkit.ScoreTracker`, `POST /game-sessions/{id}/score` |
| 10 | Arbitraggio regole | ✅ Done | `ArbitroAgent`, `POST /game-sessions/{id}/ask-agent` |
| 11a | Salva stato partita | ✅ Done | `AgentGameStateSnapshot`, `ToolkitSessionState` (500KB max) |
| 11b | Upload foto | ✅ Done | `SessionMedia` (type: Photo), blob storage |
| 11c | Pausa/riprendi | ✅ Done | `Session.Pause()`, `Session.Resume()`, `AgentSession.CurrentGameState` |

**Backend completeness: ~90%**. Il lavoro rimanente e' prevalentemente frontend wiring.

---

## Sezione 1: Smart Hub — Pagina Dettaglio Gioco Privato

### Struttura `/games/{id}`

La pagina dettaglio del gioco privato si arricchisce con una **Activation Checklist** — un componente visivo che mostra lo stato di attivazione e guida l'utente.

```
┌─────────────────────────────────────────────────┐
│  ← Libreria          AZUL          ⚙️ Settings  │
│─────────────────────────────────────────────────│
│                                                  │
│  [Immagine]   Titolo: Azul                      │
│               Publisher: Plan B Games            │
│               2-4 giocatori | 30-45 min          │
│               Complessita: 1.77                  │
│               Stato: Posseduto ✅                │
│                                                  │
│─────────────────────────────────────────────────│
│                                                  │
│  🎯 ATTIVA L'ASSISTENTE AI                      │
│                                                  │
│  ✅ 1. Gioco aggiunto alla libreria             │
│                                                  │
│  ⬜ 2. Carica il regolamento (PDF)              │
│     [Drag & drop o clicca per caricare]         │
│     ─ oppure ─                                   │
│     [Seleziona file]                             │
│                                                  │
│  ⬜ 3. Agente AI pronto                         │
│     (disponibile dopo il caricamento PDF)        │
│                                                  │
│─────────────────────────────────────────────────│
│                                                  │
│  [ 🎲 Inizia Partita ]  (disabilitato finche'   │
│                           step 1-2 non completi) │
│                                                  │
└─────────────────────────────────────────────────┘
```

### Comportamento della Checklist

#### Step 1 — Gioco aggiunto
Sempre verde. Se l'utente e' qui, il gioco esiste gia'.

#### Step 2 — PDF Upload (stato espanso quando attivo)
- Mostra dropzone inline per upload
- Al click/drop → **modal disclaimer copyright**: "Confermo di possedere una copia di questo gioco. Il PDF del regolamento sara' utilizzato esclusivamente come riferimento personale per l'assistente AI."
- Dopo accettazione → upload inizia → **progress bar inline** con stati (Uploading → Extracting → Chunking → Embedding → Ready)
- Se l'utente naviga via → progress continua in background → **notifica in-app** quando pronto
- Se l'utente resta → progress bar si aggiorna via SSE → step diventa ✅ automaticamente
- Backend: `AcceptCopyrightDisclaimerCommand` → chunked upload → SSE su `GET /pdfs/{pdfId}/progress/stream`

#### Step 3 — Agente AI (si sblocca dopo step 2)
- Auto-creazione: quando il PDF raggiunge stato `Ready`, l'agente viene **creato automaticamente** come `TutorAgent` associato al gioco
- Lo step mostra: "✅ Assistente AI pronto" con link "Prova una domanda →"
- Per utenti avanzati: link "Configura agente ⚙️" per cambiare tipo/modello LLM
- Backend: `POST /agents/user` con GameId + auto-associazione documenti

#### Bottone "Inizia Partita"
- Disabilitato (con tooltip) finche' almeno step 1 e 2 non sono completi
- L'agente (step 3) e' consigliato ma **non bloccante** — puoi iniziare una partita senza AI
- Click → crea `Session` via `POST /game-sessions` → redirect a `/sessions/{id}`

### Per l'utente esperto
- Se il PDF e' gia' caricato e l'agente esiste → checklist tutta verde → solo "Inizia Partita" prominente
- Gli step completati sono collassati (una riga con ✅), non occupano spazio
- Shortcut: bottone "Inizia Partita" anche nel header della pagina
- Nella library list view, icona ▶️ accanto ai giochi con agente pronto → crea sessione direttamente

---

## Sezione 2: Sessione Live — `/sessions/{id}`

Layout a pannello singolo con 3 tab (single-device).

### Layout

```
┌──────────────────────────────────────────────────┐
│  ← Azul          SESSIONE LIVE          ⏸️ Pausa │
│  Codice: X7K9M2        Iniziata: 21:15          │
│──────────────────────────────────────────────────│
│                                                   │
│  [ 📋 Setup ]  [ ⚖️ Regole ]  [ 🏆 Punteggi ]   │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │                                             │ │
│  │         (contenuto tab attiva)              │ │
│  │                                             │ │
│  └─────────────────────────────────────────────┘ │
│                                                   │
│  ┌─────────────────────────────────────────────┐ │
│  │  Mini scoreboard (sempre visibile)          │ │
│  │  Marco: 45 | Luca: 38 | Sara: 41           │ │
│  └─────────────────────────────────────────────┘ │
│                                                   │
└──────────────────────────────────────────────────┘
```

### Tab 1: Setup (TutorAgent)

Attiva all'inizio della sessione. Chat con il `TutorAgent` focalizzata sulla preparazione.

**Contenuto**:
- Chat con agente pre-caricato con prompt di contesto: "Stai aiutando a preparare una partita di {gameName} per {playerCount} giocatori"
- L'agente usa il RAG sul regolamento per rispondere a domande di setup
- **Suggerimenti rapidi** in basso: chip cliccabili con domande frequenti generate dall'agente ("Setup iniziale", "Distribuzione componenti", "Prima mossa")
- Backend: `POST /agents/{id}/chat` con `GameSessionId` per contesto

**Quando l'utente e' soddisfatto**, passa naturalmente alle altre tab. Il contesto della chat viene preservato.

### Tab 2: Regole (ArbitroAgent)

Per dubbi e dispute durante la partita.

**Contenuto**:
- Chat con `ArbitroAgent` — prompt: "Sei l'arbitro della partita. Rispondi citando il regolamento. Se la regola e' ambigua, dai la tua interpretazione e spiega perche'."
- **Formato risposta strutturato**:
  ```
  📖 Regola: "Un giocatore non puo' prendere tessere..."
  📄 Fonte: Regolamento Azul, pag. 5
  ⚖️ Verdetto: Marco ha ragione perche'...
  ```
- **Chip rapidi**: "Chi ha ragione?", "Si puo' fare X?", "Cosa succede quando..."
- L'agente ha accesso alla cronologia della partita (punteggi, turno corrente) per contesto
- Backend: `POST /game-sessions/{id}/ask-agent` con ArbitroAgent

### Tab 3: Punteggi (Toolkit ScoreTracker)

Widget, non chat. Gestione punteggi con il `GameToolkit.ScoreTracker`.

```
┌─────────────────────────────────────────┐
│  Turno: 3 di 5          [Prossimo →]   │
│─────────────────────────────────────────│
│                                         │
│  👤 Marco     ████████████░░  45 pts    │
│  👤 Sara      ██████████░░░░  41 pts    │
│  👤 Luca      █████████░░░░░  38 pts    │
│                                         │
│─────────────────────────────────────────│
│  Aggiungi punteggio:                    │
│                                         │
│  [Marco ▼]  [+___]  [Categoria ▼]      │
│                          [ ✓ Salva ]    │
│                                         │
│  Storico ultimo turno:                  │
│  Marco +12 (Pattern) | Sara +8 (Row)   │
│─────────────────────────────────────────│
│                                         │
│  [📊 Classifica]  [📈 Andamento]       │
│                                         │
└─────────────────────────────────────────┘
```

**Funzionalita'**:
- Aggiunta punteggio per giocatore, per turno, con categoria opzionale
- Barra progresso visiva per ogni giocatore
- Storico per turno consultabile
- Avanzamento turno manuale ("Prossimo →")
- Backend: `POST /game-sessions/{id}/score`, `GET /game-sessions/{id}/scoreboard`

### Mini Scoreboard (sempre visibile)

Barra fissa in fondo alla pagina, visibile in tutte le tab. Mostra nome e punteggio di ogni giocatore in formato compatto. Aggiornamento real-time quando si aggiungono punteggi nella tab Punteggi.

### Header della sessione

- **Nome gioco** + link per tornare all'hub
- **Codice sessione** (per futuro multi-device)
- **Bottone Pausa ⏸️** — apre il flusso "Salva e Riprendi"
- **Timer** opzionale (se il gioco ha tempo limite)

### Interazione tra tab

- Ogni tab **mantiene il proprio stato** — passare tra tab non perde la chat
- L'ArbitroAgent puo' **vedere i punteggi correnti** (contesto condiviso via `GameSessionId`)
- Il TutorAgent **non vede i punteggi** ma conosce il **turno corrente** — focalizzato su setup e componenti
- Chat Setup e Regole usano **thread separati** (`ChatThreadId` diversi)

### Note di design

- **Categorie punteggio**: il dropdown `[Categoria ▼]` nella tab Punteggi e' opzionale. Le categorie sono definite manualmente dall'utente (es. "Pattern", "Row", "Column" per Azul). Non auto-generate dall'agente.
- **Codice sessione**: visibile nell'header ma **non cliccabile/condivisibile** nella fase single-device. Sara' attivato con il futuro multi-device.

---

## Sezione 3: Salva e Riprendi

### Flusso "Pausa" — Fine serata

Quando l'utente clicca **⏸️ Pausa** nell'header:

```
┌──────────────────────────────────────────────┐
│                                              │
│         ⏸️ Metti in Pausa la Partita         │
│                                              │
│  La sessione verra' salvata con tutti i      │
│  punteggi e il turno corrente.               │
│                                              │
│─────────────────────────────────────────────│
│                                              │
│  📝 Note (opzionale)                        │
│  ┌────────────────────────────────────────┐  │
│  │ Era il turno di Marco, stava          │  │
│  │ scegliendo le tessere blu...          │  │
│  └────────────────────────────────────────┘  │
│                                              │
│  📷 Foto del tavolo (opzionale)             │
│  [📸 Scatta foto]  [📁 Carica da file]     │
│                                              │
│  🤖 Riepilogo agente (opzionale)            │
│  [ Genera riepilogo automatico ]             │
│                                              │
│─────────────────────────────────────────────│
│                                              │
│  [ Annulla ]          [ ⏸️ Salva e Pausa ]  │
│                                              │
└──────────────────────────────────────────────┘
```

### Dati salvati automaticamente (zero input utente)

| Dato | Sorgente | Persistenza |
|------|----------|-------------|
| Punteggi di tutti i giocatori | `ScoreEntry` | Gia' persistiti ad ogni inserimento |
| Turno corrente | `ToolkitSessionState` (TurnManager) | Salvato nel widget state JSON |
| Stato widget (risorse, note) | `ToolkitSessionState` | Max 500KB per widget |
| Cronologia chat Setup + Regole | `ChatThread` + `ChatMessage` | Gia' persistiti ad ogni messaggio |
| Partecipanti e ruoli | `Participant` | Gia' persistiti |

### Dati opzionali aggiunti dall'utente

| Dato | Entita' | Obbligatorio |
|------|---------|-------------|
| Note testuali | `SessionNote` (visibility: private) | No |
| Foto del tavolo | `SessionMedia` (type: Photo) | No |
| Riepilogo agente | `AgentGameStateSnapshot` | No |

### "Genera riepilogo automatico"

Chiamata a `POST /game-sessions/{id}/ask-agent` con prompt:

> "Genera un riepilogo dello stato attuale della partita. Includi: punteggi, turno, ultime regole discusse, decisioni chiave prese durante la sessione."

L'agente ha accesso a punteggi correnti, cronologia chat (Setup + Regole), stato toolkit. Il riepilogo viene salvato come `AgentGameStateSnapshot` con `BoardStateJson`.

### Flusso "Riprendi" — Giorni dopo

Nella pagina hub `/games/{id}`, sotto la checklist:

```
┌─────────────────────────────────────────────┐
│  ⏸️ PARTITA IN PAUSA                       │
│                                              │
│  Azul — 8 Marzo 2026, 21:15                │
│  Turno 3 di 5 | Marco: 45, Sara: 41,       │
│  Luca: 38                                   │
│                                              │
│  📷 1 foto  📝 1 nota  🤖 Riepilogo AI     │
│                                              │
│  [ ▶️ Riprendi Partita ]  [ 🗑️ Abbandona ] │
│                                              │
└─────────────────────────────────────────────┘
```

### Cosa succede al click "Riprendi Partita"

1. `Session.Resume()` → stato torna `Active`
2. Redirect a `/sessions/{id}`
3. Il `ToolkitSessionState` viene ricaricato → punteggi, turno, risorse ripristinati
4. Le chat mantengono la cronologia (stessi `ChatThreadId`)
5. Se c'e' un riepilogo agente, viene mostrato come **primo messaggio** nella tab Regole: "📋 Ecco dove eravamo rimasti: ..."
6. L'utente continua esattamente da dove aveva lasciato

### Edge cases

- **Piu' sessioni in pausa per lo stesso gioco**: lista ordinata per data (piu' recente prima)
- **Sessione vecchia** (>30 giorni): badge "Vecchia" + conferma "Vuoi ancora riprendere?"
- **Abbandona**: soft-delete, con conferma "La partita verra' archiviata" (recuperabile da admin se necessario)

---

## Sezione 4: Flusso di Ingresso da BGG

### Punto di partenza: Discover Page

L'utente e' nella tab BGG Search della pagina Discover.

```
┌──────────────────────────────────────────────┐
│  🔍 Cerca su BoardGameGeek                  │
│  ┌────────────────────────────────────────┐  │
│  │ azul                                  │  │
│  └────────────────────────────────────────┘  │
│                                              │
│  Risultati:                                  │
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │ [img] Azul (2017)                     │  │
│  │       Plan B Games | 2-4 giocatori    │  │
│  │       ★ 7.8 su BGG                    │  │
│  │                     [ ➕ Aggiungi ]    │  │
│  └────────────────────────────────────────┘  │
│                                              │
└──────────────────────────────────────────────┘
```

### Click "Aggiungi" — Sequenza

**Backend** (2 chiamate orchestrate dal frontend):

1. `AddPrivateGameCommand(source: "BoardGameGeek", bggId: 230802, userId)` → crea `PrivateGame`
2. `AddGameToLibraryCommand(userId, gameId, state: Owned)` → crea `UserLibraryEntry`

**Conflitto con catalogo condiviso**: se il `BggId` esiste gia' come `SharedGame`, il backend lancia `ConflictException`. Il frontend mostra:

```
┌──────────────────────────────────────┐
│  ℹ️ Azul e' gia' nel catalogo!     │
│                                      │
│  Questo gioco e' disponibile nel     │
│  catalogo condiviso con regolamento  │
│  e agente AI gia' pronti.            │
│                                      │
│  [ Aggiungi dal catalogo ]           │
│  [ Aggiungi come privato comunque ]  │
└──────────────────────────────────────┘
```

### Dopo l'aggiunta — Redirect all'Hub

Successo → redirect a `/games/{id}` con:
- Toast: "Azul aggiunto alla tua libreria come gioco posseduto"
- Checklist con step 1 completato, step 2 (PDF) espanso e pronto

### Shortcut utente esperto

- **Dalla libreria**: click sul gioco → hub con checklist
- **Dalla pagina gioco**: se PDF + agente esistono → "Inizia Partita" subito visibile
- **Quick action**: nella library list view, icona ▶️ accanto ai giochi con agente pronto

---

## Flow Completo

```
DISCOVER (BGG Search)
  │
  │ click "Aggiungi"
  │ → AddPrivateGameCommand + AddGameToLibraryCommand
  ▼
SMART HUB (/games/{id})
  │
  │ Checklist: ✅ Aggiunto
  │ → Upload PDF inline + disclaimer modal
  │ → Progress bar SSE (o notifica se naviga via)
  │ → Auto-creazione agente quando PDF = Ready
  │ Checklist: ✅ Aggiunto ✅ PDF ✅ Agente
  │
  │ click "Inizia Partita"
  │ → POST /game-sessions
  ▼
SESSIONE LIVE (/sessions/{id})
  │
  │ Tab Setup: TutorAgent prepara la tavola
  │ Tab Regole: ArbitroAgent arbitra dispute
  │ Tab Punteggi: ScoreTracker + classifica
  │ Mini scoreboard sempre visibile
  │
  │ click "⏸️ Pausa"
  │ → Note + foto + riepilogo opzionali
  │ → Session.Pause()
  ▼
SMART HUB (/games/{id})
  │
  │ Card "Partita in Pausa" visibile
  │ click "▶️ Riprendi"
  │ → Session.Resume() + reload stato
  ▼
SESSIONE LIVE (/sessions/{id})
  → Tutto ripristinato, si continua a giocare
```

---

## Issues GitHub Correlate

| Issue | Titolo | Fase |
|-------|--------|------|
| #211 | API contracts for Sprint 2 Game Night endpoints | Fase 1-2 |
| #212 | Behavioral examples (Given/When/Then) for Game Table | Testing |
| #213 | SSE operational requirements for live session | Fase 3 |
| #214 | QuickView dual-context: game mode vs session mode | Fase 3 |
| #216 | Server-side timer for live session | Fase 3 |
| #218 | Performance budget for live session feed | Fase 3 |
| #33 | Epic: Email, Notifiche & Calendario | Fase 2 |

---

## Roadmap di Implementazione

### Fase 1: "Zero to Private Game" (~3-5 giorni)
- Wizard "Aggiungi da BGG" con conflitto catalogo condiviso
- UI disclaimer copyright (modal)
- Upload PDF inline con progress bar SSE
- Checklist component riutilizzabile

### Fase 2: "PDF → Agente Pronto" (~2-3 giorni)
- Deep link nella notifica verso hub
- Auto-creazione agente quando PDF = Ready
- Status check agente con polling/SSE

### Fase 3: "Game Table Live" (~5-8 giorni)
- Crea sessione dal gioco
- Score tracker widget wiring
- Ask Agent in session (tab Setup + Regole)
- Mini scoreboard persistente
- Chip suggerimenti rapidi

### Fase 4: "Salva e Riprendi" (~3-4 giorni)
- Modal pausa con note/foto/riepilogo opzionali
- Card "Partita in Pausa" nell'hub
- Resume sessione con stato completo
- Riepilogo agente come primo messaggio al resume

### Fase 5: Polish (~2-3 giorni)
- Mobile bottom sheets per sessione live
- Shortcut nella library list view
- Sessione vecchia (>30 giorni) con conferma
- Edge cases e error handling

**Stima totale: ~15-23 giorni di sviluppo**

### Prerequisiti

- Game Table S2-S5 scaffolding gia' completato (80+ componenti, Zod schemas, React Query hooks)
- Backend endpoints tutti disponibili
- SSE streaming infrastruttura gia' in produzione

---

## Evoluzione Futura (Out of Scope)

- **Multi-device**: amici si uniscono con codice sessione dal proprio telefono (backend pronto)
- **Merge privato→condiviso**: se un gioco privato viene aggiunto al catalogo condiviso, offrire merge automatico
- **DecisoreAgent**: terzo tipo agente per suggerimenti strategici
- **Offline resilience**: queue locale per score updates senza connessione
- **Voice chat**: domande all'agente via voce (spec gia' scritta in admin-invite-onboarding-design)
- **Replay**: playback della sessione con timeline
