# Game Night Full Experience — Specification

**Data**: 2026-04-10  
**Autore**: spec-panel (Wiegers, Adzic, Cockburn, Fowler, Nygard, Crispin)  
**Bounded Contexts**: GameManagement, SessionTracking, GameToolbox, KnowledgeBase

---

## Contesto

L'utente vuole avviare una Game Night con 2 amici usando l'app MeepleAI per:
1. Tracciare lo stato della partita (turno, punteggio, risorse giocatore)
2. Consultare un AI agent per regole, setup, dispute
3. Usare strumenti digitali (dadi, carte, timer)
4. Mantenere un diary automatico con foto

---

## Infrastruttura Esistente (Non rispecificare)

| Componente | File | Status |
|-----------|------|--------|
| GameNight lifecycle | GameNightEvent aggregate | ✅ |
| Session Checkpoint | SessionCheckpoint entity | ✅ |
| DiceRoll entity | DiceRoll.cs | ✅ |
| CardDeck + DrawCards | SessionDeck, DrawCardsCommand | ✅ |
| ScoreEntry | ScoreEntry, UpdateScoreCommand | ✅ |
| GameToolbox | Toolbox, ToolboxTemplate, Phases | ✅ |
| KB Agent Chat | AgentSession, useAgentChatStream | ✅ |
| Diary Timeline | DiaryEntry, useGameNightDiary, SSE | ✅ |
| PlayerResources VO | PlayerResources (PR #265) | ✅ |
| Auto-save 60s | AutoSaveSessionJob (Quartz) | ✅ |
| SignalR real-time | useSessionSync | ✅ |
| SessionMedia (foto) | SessionMedia.cs, UploadSessionMediaCommand | ✅ |

---

## Gap Identificati dal Panel

### GAP-001 — Game State Tier Config [CRITICO - P0]

**Problema**: Il sistema manca di un meccanismo esplicito per configurare la complessità dello stato di gioco. `StartGameNightSessionCommand` non ha un parametro `StateTier`.

**Specifica**:
```typescript
enum GameStateTier {
  Minimal = 0,  // Solo turno/fase/round corrente
  Score = 1,    // Punteggi + turno
  Full = 2,     // Risorse giocatore + scheda + campi custom
}
```

**Acceptance Criteria**:
```
AC-001-1: StartGameNightSessionCommand accetta StateTier (default: Minimal)
AC-001-2: StateTier viene propagato a CreateSessionCommand
AC-001-3: Il wizard mostra step per selezionare il tier (step 3)
AC-001-4: Il tier suggerito è pre-selezionato se esiste un ToolboxTemplate per il gioco
```

---

### GAP-002 — KB-Ready Game Filter nel GamePicker [CRITICO - P0]

**Problema**: `InlineGamePicker` non filtra i giochi per KB-readiness. `GameNightGame` type non ha `kbStatus`.

**Specifica**:
```typescript
// In GameNightGame type:
kbStatus?: 'indexed' | 'not_indexed' | 'unknown';
```

**Acceptance Criteria**:
```
AC-002-1: InlineGamePicker accetta prop filterKbReady=true per mostrare solo giochi indicizzati
AC-002-2: I giochi con kbStatus='indexed' mostrano badge "AI" verde
AC-002-3: Stato vuoto con filterKbReady='Nessun gioco con AI disponibile'
AC-002-4: Default (filterKbReady=false) mantiene comportamento pre-esistente
```

---

### GAP-003 — Backend Orchestration Saga [IMPORTANTE - P1]

**Problema**: Il flusso "avvia sessione → init toolbox → warm agent" è delegato al frontend. `StartGameNightSessionCommandHandler` non applica automaticamente il `ToolboxTemplate` del gioco.

**Acceptance Criteria**:
```
AC-003-1: Se esiste un ToolboxTemplate per il gioco, viene applicato automaticamente
AC-003-2: L'operazione è fire-and-forget: non blocca se fallisce
AC-003-3: Se non esiste template, la sessione si avvia normalmente senza toolbox
```

---

### GAP-004 — Offline-First Core Actions [IMPORTANTE - P1, piano separato]

**Problema**: Dadi, punteggi, pausa devono funzionare senza connessione con sync alla riconnessione.

**Deferiti**: complessità SW/IndexedDB, piano dedicato `2026-04-10-game-night-offline.md`

---

### GAP-005 — Photo Attachment al Diary [MEDIO - P1]

**Problema**: `DiaryEntryType.photo` esiste ma manca un pulsante nell'UI per scattare foto durante la sessione.

**Acceptance Criteria**:
```
AC-005-1: QuickActions ha pulsante "Foto" (opzionale via prop onOpenPhoto)
AC-005-2: Apre file picker con capture='environment' per fotocamera mobile
AC-005-3: Upload via api.sessions.uploadMedia (backend SessionMedia esistente)
AC-005-4: Upload asincrono (non blocca l'uso dell'app)
```

---

### GAP-006 — Dispute Resolution Explicit Flow [MEDIO - P1]

**Problema**: Il pannello Arbitro AI non crea automaticamente una diary entry con la decisione.

**Acceptance Criteria**:
```
AC-006-1: Nel pannello Arbitro, dopo una risposta AI, appare "Registra disputa nel diary"
AC-006-2: Il pulsante crea entry dispute_resolved con domanda + risposta AI
AC-006-3: Il pulsante diventa "✓ Disputa registrata" per 3s dopo il click
AC-006-4: Visibile solo quando c'è una risposta AI e lo streaming è completato
```

---

### GAP-007 — LiveSession Mobile UX Thumb Zone [MEDIO, piano separato]

**Problema**: `LiveSessionView` ha layout mobile ma non è verificato che sia ottimizzato per uso one-handed.

**Deferiti**: refinement visuale, piano dedicato `2026-04-10-game-night-mobile-ux.md`

---

## Requisiti Funzionali

### REQ-001: Selezione Gioco KB-Ready

| ID | Criterio |
|----|---------|
| AC-001 | GamePickerDialog/InlineGamePicker mostra solo giochi con kbStatus='indexed' se filterKbReady=true |
| AC-002 | Badge "AI" verde per giochi con PDF indicizzati |
| AC-003 | Toggle "Mostra tutti" opzionale |
| AC-004 | Loading skeleton durante fetch kbStatus |

### REQ-002: Game State Tier

| ID | Criterio |
|----|---------|
| AC-001 | T1 Minimal: solo currentPlayerId, currentPhase, roundNumber |
| AC-002 | T2 Score: T1 + ScoreEntry[] |
| AC-003 | T3 Full: T2 + PlayerResources + customFields |
| AC-004 | Tier configurabile nel wizard step 3 |

### REQ-003: AI Agent Rules + Dispute

| ID | Criterio |
|----|---------|
| AC-001 | Ogni risposta AI ha streaming visibile (già implementato) |
| AC-002 | Pulsante "Registra disputa" compare dopo risposta completata |
| AC-003 | Entry dispute_resolved creata con {question, ruling} |

### REQ-004: Photo Diary

| ID | Criterio |
|----|---------|
| AC-001 | Pulsante Foto in QuickActions |
| AC-002 | File picker con capture=environment per fotocamera |
| AC-003 | Upload asincrono (non blocca UI) |
| AC-004 | Entry photo creata nel diary |

---

## Quality Score (post-panel)

| Dimensione | Score |
|-----------|-------|
| Chiarezza | 8/10 |
| Completezza | 7.5/10 |
| Testabilità | 9/10 |
| Consistenza | 8/10 |
| Fattibilità | 8.5/10 |
