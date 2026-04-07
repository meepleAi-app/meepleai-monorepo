# Design Spec: Mobile Library & KB-Linked MeepleCard Experience

**Data**: 2026-04-07
**Scope**: `apps/web/src/`, `apps/api/src/Api/BoundedContexts/{UserLibrary,KnowledgeBase,SharedGameCatalog}/`
**Branch base**: `main-dev`
**Stato**: Draft — in attesa di review

---

## Contesto e Motivazione

L'utente mobile vuole browsare la propria libreria di giochi tramite MeepleCard, visualizzando a colpo d'occhio le informazioni chiave (giocatori, complessita, durata, meccaniche, rating) e capendo dai ManaPips quali entita sono collegate (KB, sessioni, espansioni). Cliccando un ManaPip KB, vede i documenti indicizzati e puo avviare una chat RAG selezionando quali KB usare.

Il codebase ha gia le fondamenta:
- MeepleCard con 6 varianti e 16 entity types
- ManaPips system (ManaLinkFooter, ManaSymbol, ManaBadge) con `onPipClick`
- `ChatThread.SelectedKnowledgeBaseIdsJson` per filtrare RAG per documento
- `DocumentCategory` (Rulebook, Expansion, Errata, etc.) e `VersionLabel`
- UserLibrary BC (24 commands, 15 queries), SharedGameCatalog BC (46 commands, 23 queries)
- KnowledgeBase BC con RAG hybrid search e chat threads

**Manca**: wiring frontend tra ManaPip click e KB docs, UI selezione KB pre-chat, read model composito per linked entity counts, shared game browsing come MeepleCard.

---

## User Story

> Come giocatore mobile, voglio browsare la mia libreria vedendo MeepleCard con info a colpo d'occhio e ManaPips che indicano le entita collegate, cosi da capire rapidamente quali giochi hanno regolamenti indicizzati e avviare una chat RAG selezionando le KB desiderate.

---

## Decisioni di Design

### 1. Scroll Pattern — Griglia Verticale

**Decisione**: griglia verticale 2 colonne su mobile (come oggi), con scroll standard e tap su card per dettaglio.

- Layout: `grid-cols-2` su mobile, `grid-cols-3 lg:grid-cols-4` su desktop
- Variante card: `MeepleCardGrid` (default, aspect ratio 7:10)
- No carousel orizzontale full-card: impraticabile con 50+ giochi
- Virtualizzazione lista obbligatoria per librerie > 30 giochi (tanstack-virtual)

### 2. ManaPip KB — Visibilita Condizionale

**Decisione**: il ManaPip KB appare SOLO se il gioco ha almeno 1 VectorDocument in stato `Ready`.

- `kbDocumentCount === 0` → ManaPip KB nascosto (non grigio)
- `kbDocumentCount > 0` → ManaPip KB visibile con count badge
- Su shared game card: ManaPip KB visibile (indicatore) ma NON cliccabile per chat
- Motivazione: mostrare solo cio che e azionabile, evitare aspettative insoddisfatte

### 3. Selezione KB Default — Latest + Tutto il Contesto

**Decisione**: pre-selezionare il regolamento con `VersionLabel` piu recente + tutte le KB non-regolamento.

- Regolamenti (DocumentCategory: Rulebook): radio button, solo 1 selezionabile, default = latest
- Altre KB (Expansion, Errata, QuickStart, Reference, PlayerAid): checkbox, tutte pre-selezionate
- Se solo 1 documento totale: skip selezione, chat diretta
- La selezione viene salvata in `ChatThread.SelectedKnowledgeBaseIdsJson`

### 4. Chat Solo da Libreria

**Decisione**: la chat RAG e disponibile solo per giochi nella propria libreria.

- Shared game card: ManaPip KB visibile come indicatore (mostra che ha regolamenti), NON cliccabile
- CTA su shared game card: "Aggiungi alla libreria per chattare con l'agente"
- Motivazione: quota enforcement, tracking usage, incentivo add-to-library
- Dopo add-to-library: redirect a libreria, card del gioco aggiunto visibile, ManaPip KB attivo

### 5. Aggiornamento ManaPips — Stale-While-Revalidate

**Decisione**: React Query SWR, niente push real-time.

- `staleTime: 5 * 60 * 1000` (5 minuti)
- Refresh al mount della libreria
- L'indicizzazione PDF e un processo raro, SWR e sufficiente
- No SSE/WebSocket per ManaPips count (overengineering)

### 6. Quota Check — Pre-azione

**Decisione**: mostrare quota residua PRIMA del tentativo di add-to-library.

- Badge visibile: "3/10 giochi" accanto al bottone "Aggiungi"
- Quota piena: bottone diventa "Upgrade per aggiungere" (disabilitato per add)
- Endpoint esistente: `GetLibraryQuotaQuery`
- Quota tiers: Free (10), Basic (50), Premium (500), Enterprise (unlimited)

---

## Requisiti Funzionali

### RF-01: MeepleCard Info a Colpo d'Occhio

La MeepleCard in variante Grid mostra le seguenti informazioni senza aprire il dettaglio:

| Campo | Posizione | Fonte Dati | Componente |
|-------|-----------|------------|------------|
| Giocatori (min-max) | Icona top overlay | `SharedGame.MinPlayers/MaxPlayers` | Cover overlay label |
| Complessita (1-5) | Badge top-right | `SharedGame.Complexity` | Cover overlay badge |
| Durata (minuti) | Icona accanto complessita | `SharedGame.PlayingTimeMinutes` | Cover overlay |
| Meccaniche principali | Tag strip verticale (left edge) | `SharedGame.Mechanics[]` | Vertical tag strip (max 3) |
| Rating | Badge con stelline | `SharedGame.AverageRating` | Rating badge |
| ManaPips entita collegate | Footer card | Read model composito | ManaLinkFooter |

### RF-02: ManaPip KB Click — Bottom Sheet Documenti

**Trigger**: click su ManaPip KB nel ManaLinkFooter di una card in libreria.

**Bottom Sheet mostra**:
```
┌─────────────────────────────────────┐
│ 📜 Knowledge Base — {GameTitle}     │
│─────────────────────────────────────│
│                                     │
│ 📕 Regolamenti                      │
│  ┌─────────────────────────────────┐│
│  │ ✅ Rules v2 (2nd Edition)       ││
│  │    Rulebook · 45 pagine         ││
│  └─────────────────────────────────┘│
│  ┌─────────────────────────────────┐│
│  │ ✅ Rules v1 (1st Edition)       ││
│  │    Rulebook · 38 pagine         ││
│  └─────────────────────────────────┘│
│                                     │
│ 📚 Altre Knowledge Base             │
│  ┌─────────────────────────────────┐│
│  │ ✅ FAQ                          ││
│  │    Reference · 12 pagine        ││
│  └─────────────────────────────────┘│
│  ┌─────────────────────────────────┐│
│  │ 🔄 Errata v1.2 (in elaborazione)│
│  │    Errata · Processing...       ││
│  └─────────────────────────────────┘│
│                                     │
│ [  💬 Chatta con l'Agente  ]       │
│                                     │
└─────────────────────────────────────┘
```

**Stati documento**:
- ✅ Ready: cliccabile, incluso nel RAG
- 🔄 Processing: visibile, non selezionabile per chat
- ❌ Failed: visibile con messaggio errore, azione "Riprova"

### RF-03: Shared Games Browsing come MeepleCard

- Stessa griglia verticale della libreria (2 col mobile)
- MeepleCardGrid con entity type `game` e variant `grid`
- Ricerca full-text (endpoint `SearchSharedGamesQuery` esistente)
- ManaPip KB visibile come indicatore (non cliccabile)
- Bottone "Aggiungi alla libreria" con quota check pre-azione
- Dopo add: redirect a libreria con scroll alla card appena aggiunta

### RF-04: Add to Library Flow

```
Shared Games → Click "Aggiungi" → Quota check:
  ├─ Quota OK → AddGameToLibraryCommand → Redirect libreria → Card con badge "Nuovo"
  └─ Quota piena → Toast "Hai raggiunto il limite di {max} giochi. Upgrade il tuo piano."
```

- Badge "Nuovo" (o "Appena aggiunto") visibile per 24h dopo add
- Card posizionata in cima alla libreria (sort by `addedAt DESC`)
- ManaPips calcolati al volo: se shared game aveva KB, i ManaPips li riflettono

### RF-05: Avvio Chat RAG con Selezione KB

**Trigger**: click "Chatta con l'Agente" dal bottom sheet KB (RF-02) o azione primaria card.

**Flusso selezione** (se > 1 documento):

```
┌─────────────────────────────────────┐
│ Seleziona Knowledge Base            │
│─────────────────────────────────────│
│                                     │
│ 📕 Regolamento (scegli versione)    │
│  ○ Rules v1 (1st Edition)           │
│  ● Rules v2 (2nd Edition) ← default │
│                                     │
│ 📚 Altre Knowledge Base             │
│  ☑ FAQ                              │
│  ☑ Errata v1.2                      │
│  ☐ Player Aid                       │
│                                     │
│ [  Avvia Chat  ]                    │
│                                     │
└─────────────────────────────────────┘
```

**Regole**:
- Regolamento: radio button (1 solo), default = latest version
- Altre KB: checkbox, default = tutte selezionate
- Almeno 1 documento deve essere selezionato per abilitare "Avvia Chat"
- Se solo 1 documento totale: skip questa schermata, chat diretta
- Selezione salvata in `ChatThread.SelectedKnowledgeBaseIdsJson`
- Primo messaggio sistema: "Sto usando: {lista documenti selezionati}"

---

## Requisiti Non-Funzionali

### RNF-01: Performance Mobile

| Metrica | Target | Come |
|---------|--------|------|
| First Contentful Paint (libreria) | < 1.5s | Skeleton cards + SWR cache |
| Scroll 60fps con 100+ card | Garantito | tanstack-virtual, lazy images |
| Bottom sheet apertura | < 200ms | Pre-fetch KB data on card mount |
| Chat first response | < 3s | SSE streaming gia implementato |

### RNF-02: Accessibilita

- ManaPips: `aria-label="Knowledge Base: 3 documenti"` su ogni pip
- Bottom sheet: focus trap, dismissible con swipe down e Escape
- Selezione KB: radio/checkbox accessibili con label associata
- Touch target minimo: 44x44px per tutti gli elementi interattivi

### RNF-03: Offline / Connessione Lenta

- Libreria: cached via React Query, navigabile offline (senza ManaPip counts aggiornati)
- ManaPips: mostrano ultimo dato cached, non scompaiono se offline
- Chat: richiede connessione, mostrare stato "Connessione necessaria" se offline

---

## Architettura — Read Model Composito

### Problema (A-01 dal Spec Panel)

MeepleCard ha bisogno di dati da 3 BC per mostrare ManaPips accurati. Evitare N+1 chiamate API.

### Soluzione: Estendere GetUserLibraryQuery Response

```csharp
// Aggiungere al DTO esistente di UserLibrary
public record LibraryEntryDto
{
    // ... campi esistenti (GameId, Title, ImageUrl, Status, etc.)

    // NUOVO: conteggi entita collegate per ManaPips
    public LinkedEntityCountsDto LinkedEntities { get; init; }
}

public record LinkedEntityCountsDto
{
    public int KbDocuments { get; init; }    // VectorDocuments in stato Ready
    public int Sessions { get; init; }       // GameSessions count
    public int Expansions { get; init; }     // SharedGame expansions count
    public int Notes { get; init; }          // AgentMemory notes count
}
```

**Query**: LEFT JOIN su `vector_documents` (WHERE state = Ready), `game_sessions`, etc. raggruppati per GameId. Singola query SQL con subquery aggregate.

### Ownership KB: Shared Game → Library (A-03)

**Decisione**: LINK, non copia. Quando un utente aggiunge un shared game alla libreria, i `VectorDocument` dello shared game sono accessibili tramite `SharedGameId` FK. Non vengono duplicati.

```
UserLibraryEntry.GameId → SharedGame.Id → VectorDocument.SharedGameId
```

Il frontend usa `GameId` per query KB. Il backend risolve: se il gioco e uno shared game, cerca VectorDocuments per `SharedGameId`.

---

## Scenari di Errore

| Scenario | Comportamento | UI |
|----------|--------------|-----|
| Gioco senza KB | ManaPip KB nascosto | Nessun pip KB nel footer |
| KB in Processing | ManaPip KB visibile, doc non selezionabile in sheet | Badge 🔄 su doc, checkbox disabilitata |
| KB Failed | Visibile in sheet con errore | Badge ❌, azione "Riprova indicizzazione" |
| Qdrant down | Chat non disponibile | Toast "Servizio temporaneamente non disponibile" |
| Quota libreria piena | Bottone add disabilitato | "Upgrade per aggiungere" + contatore quota |
| Shared game KB rimossa da admin | ManaPip KB scompare al prossimo refresh | SWR aggiorna automaticamente |
| Tutti i doc deselezionati | Bottone "Avvia Chat" disabilitato | Tooltip "Seleziona almeno un documento" |
| Connessione persa durante chat | Messaggio non inviato | "Riprova" button sul messaggio fallito |

---

## Acceptance Criteria (Gherkin)

### AC-01: Browsing libreria con info a colpo d'occhio

```gherkin
Scenario: Card mostra info gioco a colpo d'occhio
  Given utente con "Terraforming Mars" in libreria (1-5 giocatori, complessita 3.2, 120 min, rating 8.4)
  When apre la libreria su mobile
  Then la MeepleCard Grid di "Terraforming Mars" mostra:
    | Campo       | Valore visualizzato |
    | Giocatori   | 1-5                 |
    | Complessita | 3.2                 |
    | Durata      | 120 min             |
    | Rating      | 8.4                 |
  And il footer ManaPips mostra i pip delle entita collegate
```

### AC-02: ManaPip KB click apre bottom sheet

```gherkin
Scenario: Click ManaPip KB mostra documenti
  Given utente su MeepleCard di "Gloomhaven" con 3 KB docs (Rules v1, Rules v2, FAQ)
  And ManaPip KB visibile con count "3"
  When clicca il ManaPip KB
  Then appare bottom sheet "Knowledge Base - Gloomhaven"
  And mostra 3 documenti raggruppati per tipo:
    | Sezione      | Documento  | Tipo      | Versione | Stato |
    | Regolamenti  | Rules v2   | Rulebook  | 2nd Ed   | Ready |
    | Regolamenti  | Rules v1   | Rulebook  | 1st Ed   | Ready |
    | Altre KB     | FAQ        | Reference | -        | Ready |
  And mostra bottone "Chatta con l'Agente"
```

### AC-03: ManaPip KB nascosto se nessun documento

```gherkin
Scenario: Gioco senza KB non mostra ManaPip KB
  Given utente con "Catan" in libreria senza documenti KB indicizzati
  When vede la MeepleCard di "Catan"
  Then il footer ManaPips NON include il pip KB (📜)
```

### AC-04: Aggiunta gioco da shared games

```gherkin
Scenario: Add shared game alla libreria con quota OK
  Given utente con 3/10 giochi in libreria (piano Free)
  And naviga nella sezione shared games
  When cerca "Ark Nova" e clicca "Aggiungi alla libreria"
  Then viene reindirizzato alla propria libreria
  And "Ark Nova" appare in cima con badge "Nuovo"
  And contatore quota mostra "4/10"
  And ManaPips riflettono le entita collegate da shared game

Scenario: Add shared game con quota piena
  Given utente con 10/10 giochi in libreria (piano Free)
  When vede il bottone "Aggiungi" su uno shared game
  Then il bottone mostra "Upgrade per aggiungere" ed e disabilitato
  And mostra contatore "10/10 giochi"
```

### AC-05: Selezione KB e avvio chat

```gherkin
Scenario: Selezione KB multipla con versione regolamento
  Given utente su MeepleCard "Gloomhaven" con 3 KB docs
  When clicca "Chatta con l'Agente"
  Then vede schermata selezione KB:
    | Sezione        | Documento | Tipo Selezione | Default        |
    | Regolamento    | Rules v1  | Radio          | Non selezionato |
    | Regolamento    | Rules v2  | Radio          | Selezionato     |
    | Altre KB       | FAQ       | Checkbox       | Selezionato     |
  When seleziona Rules v2 e FAQ e clicca "Avvia Chat"
  Then la chat si apre
  And il primo messaggio sistema dice "Sto usando: Rules v2 (2nd Edition), FAQ"
  And le risposte RAG usano solo quei 2 documenti come contesto

Scenario: Gioco con 1 solo documento KB
  Given utente su MeepleCard con 1 solo documento KB (Rulebook)
  When clicca "Chatta con l'Agente"
  Then la chat si apre direttamente (skip selezione)
  And il messaggio sistema dice "Sto usando: {nome documento}"
```

### AC-06: Shared game card — KB non cliccabile

```gherkin
Scenario: ManaPip KB su shared game non avvia chat
  Given utente nella sezione shared games
  And "Ark Nova" ha 2 documenti KB indicizzati
  When vede la MeepleCard di "Ark Nova"
  Then ManaPip KB e visibile (indicatore)
  But ManaPip KB NON e cliccabile
  And tooltip mostra "Aggiungi alla libreria per chattare con l'agente"
```

---

## Componenti Frontend da Creare/Modificare

| Componente | Azione | Descrizione |
|------------|--------|-------------|
| `MeepleCardGrid` | Modifica | Aggiungere overlay info (players, complexity, duration) |
| `ManaLinkFooter` | Modifica | Passare `linkedEntities` dal read model composito |
| `KbBottomSheet` | **Nuovo** | Bottom sheet con lista documenti KB raggruppati per tipo |
| `KbSelector` | **Nuovo** | UI selezione KB pre-chat (radio regolamento + checkbox altre) |
| `SharedGameGrid` | Modifica | Usare MeepleCardGrid con ManaPip KB non cliccabile |
| `AddToLibraryButton` | **Nuovo** | Bottone con quota check pre-azione |
| `useGameLinkedEntities` | **Nuovo** | Hook React Query per read model composito |
| `useLibraryQuota` | **Nuovo/Modifica** | Hook per quota check con caching |

## Endpoint Backend da Creare/Modificare

| Endpoint | Azione | Descrizione |
|----------|--------|-------------|
| `GET /api/v1/library` | Modifica | Aggiungere `linkedEntities` counts nel response DTO |
| `GET /api/v1/knowledge-base/{gameId}/documents` | Verifica | Deve ritornare docs raggruppati per categoria con stato |
| `POST /api/v1/chat-threads` | Verifica | Accettare `selectedKnowledgeBaseIds` nel body |
| `GET /api/v1/library/quota` | Verifica | Gia esistente, confermare response format |
| `GET /api/v1/shared-games/search` | Verifica | Aggiungere `kbDocumentCount` nel response |

---

## Dipendenze e Ordine di Implementazione

```
1. Backend: Read model composito (LinkedEntityCounts nel library DTO)
   └── 2. Frontend: useGameLinkedEntities hook + ManaLinkFooter wiring
       └── 3. Frontend: KbBottomSheet (click ManaPip → lista docs)
           └── 4. Frontend: KbSelector (selezione pre-chat)
               └── 5. Frontend: Chat launch con SelectedKnowledgeBaseIds

In parallelo:
1b. Frontend: AddToLibraryButton con quota check
1c. Frontend: SharedGameGrid con MeepleCard e ManaPip non cliccabile
1d. Frontend: MeepleCardGrid overlay info (players, complexity, duration)
```

---

## Out of Scope

- Upload PDF da mobile (esiste gia in desktop)
- Creazione nuovi shared games
- Admin approval workflow
- Push notifications per KB indexing completata
- Chat history cross-device sync (gia gestito da ChatThread persistence)
