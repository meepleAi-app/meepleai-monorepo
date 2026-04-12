# Game Utilities Toolkit — Feature Specification

> **Version**: 0.2
> **Date**: 2026-04-02
> **Status**: ✅ Phase 1 Complete — all OIs closed (2026-04-11)
> **Bounded Contexts**: `GameToolkit`, `GameToolbox`, `SessionTracking`

---

## 1. Goal

Fornire un insieme di utility digitali (dadi, timer, deck di carte, contatori) utilizzabili durante una serata di gioco da tavolo su **singolo dispositivo condiviso tra più giocatori**, sia in modalità standalone (fuori sessione) che integrata con i tool specifici di un gioco.

---

## 2. Fasi di Sviluppo

### Phase 1 — Single Device (questa spec)

- 1 dispositivo condiviso al tavolo, più giocatori si alternano
- Stato completamente locale (Zustand + localStorage)
- 100% offline per tutti i tool
- Nessuna sincronizzazione di rete richiesta
- Nessuna autorizzazione inter-dispositivo

### Phase 2 — Multi-Device (out of scope, futura)

I seguenti elementi sono esplicitamente rimandati a Phase 2:

- `VirtualTable` entity (join via QR/link/codice sala)
- SSE sync per CardDeck e Counter tra dispositivi
- Dado "privato" + bottone "Annuncia al tavolo"
- Autorizzazione host/partecipante
- Conflict resolution LWW multi-device

---

## 3. Modalità di Accesso

### 3.1 Standalone

- Accessibile dalla home in **massimo 2 tap**, senza sessione attiva
- Presenta i **tool universali** con configurazione di default
- Non richiede autenticazione
- Stato e log salvati in **localStorage** (non sincronizzati)

### 3.2 In-Session (Game-Linked)

- Avviato automaticamente all'inizio di una sessione di gioco
- Carica il `GameToolkit` pubblicato per quel gioco
- I tool game-specific sostituiscono i corrispondenti tool universali se i flag di override sono attivi (`OverridesDiceSet`, `OverridesScoreboard`, `OverridesTurnOrder`)
- Log azioni inviato a `SessionTracking` via domain event `ToolActionEvent`

---

## 4. Tool Catalog

### 4.1 DiceTool — Dado

| Proprietà | Valore |
|-----------|--------|
| **Tipi standard** | D4, D6, D8, D10, D12, D20, D100 |
| **Tipi custom** | Facce definite come `string[]` (es. `["⚔️","🛡️","💀","🌕","🌕","⚔️"]`) |
| **Visibilità** | Visibile a tutti (stesso device, stesso schermo) |
| **Multi-dado** | Supporto tiro multiplo (es. 2D6, 1D4+1D6) |
| **Animazione** | Rolling < 600ms su dispositivo mid-range (target: Moto G7 equiv.) |
| **Log** | Registra: `{ giocatore, configurazione, risultati[], totale, timestamp }` |
| **Offline** | 100% offline |

**Configurazione game-specific** (`DiceToolConfig`):
```json
{
  "name": "Arkham Horror — Skill Die",
  "faces": ["✅", "✅", "✅", "❌", "❌", "⭐"],
  "count": 2,
  "description": "Tira 2 dadi skill. ✅ = successo, ⭐ = successo critico"
}
```

> **OI-1 — CHIUSO ✅**: `DiceToolConfig` già supporta `CustomFaces: string[]?` con
> `DiceType.Custom` nel backend. DTO esposto, persiste come JSONB. Nessuna migrazione necessaria.

---

### 4.2 TimerTool — Timer

Il `TimerTool` ha due modalità di esecuzione selezionate automaticamente dal contesto:

| Contesto | Modalità | Implementazione |
|---------|---------|----------------|
| Standalone (no sessione) | **Locale** | `useLocalTimer` — `setInterval` JS, nessun server |
| In-Session (sessione attiva) | **SSE sync** | `useSSETimer` — Sprint 2 spec (`timer_tick` + `timer_state`) |

**REQ-TIMER-01**: In assenza di sessione attiva, il timer funziona localmente. Non richiede rete. Non sincronizza con altri dispositivi.

**REQ-TIMER-02**: Con sessione attiva, il timer utilizza il canale SSE di sessione (Sprint 2). La sincronizzazione è autoritativa lato server. Latenza display ≤ 100ms.

**REQ-TIMER-03**: Quando una sessione viene avviata mentre un timer standalone è in esecuzione, il timer locale si ferma e il controllo passa al timer SSE. L'utente vede una notifica di transizione.

**REQ-TIMER-04**: Quando la sessione termina con timer SSE attivo, il timer si ferma. Non viene creato automaticamente un timer locale sostitutivo.

| Proprietà | Valore |
|-----------|--------|
| **Tipi** | Countdown, Count-up, Turno |
| **Timer turno** | Durata configurabile nel `GameToolkit`; avvio con 1 tap |
| **Alert** | Suono + vibrazione a 10s dal termine (countdown) |
| **Offline** | 100% offline in modalità locale |

---

### 4.3 CardDeckTool — Mazzo di Carte

Su singolo device, "condiviso" significa condiviso tra i giocatori che usano lo stesso dispositivo — nessuna sincronizzazione di rete.

| Proprietà | Valore |
|-----------|--------|
| **Stato** | Locale (Zustand), visibile a tutti sullo stesso schermo |
| **Azioni** | Draw, Shuffle, Discard, Reset deck |
| **Visibilità carta** | Visibile a tutti (stesso schermo) |
| **Undo** | 30 secondi dopo draw/discard (client-side rollback) |
| **Zone** | Draw pile, Discard pile |
| **Auto-reshuffle** | Configurabile: rimescola discard nel draw quando deck vuoto |
| **Offline** | 100% offline |

**Configurazione game-specific** (`CardToolConfig`):
```json
{
  "name": "Pandemic — Infection Deck",
  "totalCards": 48,
  "cardFaces": ["Milano", "Parigi", "Cairo"],
  "zones": ["draw", "discard"],
  "reshuffleDiscardOnEmpty": true
}
```

---

### 4.4 CounterTool — Contatore / Scoreboard

| Proprietà | Valore |
|-----------|--------|
| **Stato** | Locale (Zustand), visibile a tutti |
| **Operazioni** | Increment, Decrement, Reset, Set value |
| **Valore iniziale** | Configurabile (default 0) |
| **Range** | Min/Max opzionali |
| **Istanze** | Multipli counter per sessione (es. un counter per giocatore) |
| **Offline** | 100% offline |

---

### 4.5 RandomizerTool — Randomizzatore

| Proprietà | Valore |
|-----------|--------|
| **Input** | Lista di elementi custom (stringhe), max 50 voci |
| **Estrazione** | Sampling **senza rimpiazzo** — elemento rimosso dal pool dopo estrazione |
| **Reset** | Bottone esplicito per ripristinare il pool originale |
| **Pool persistence** | Pool resettato a ogni avvio del tool (non persiste tra sessioni) |
| **Visibilità** | Visibile a tutti (stesso schermo) |
| **Offline** | 100% offline |
| **Use case** | Scegliere il primo giocatore, assegnare ruoli, estrarre mappa |

---

## 5. Sync Model — Phase 1

**Tutto locale.** Nessuna chiamata di rete per i tool. Stato gestito interamente in Zustand con persistenza in localStorage.

```
┌─────────────────────────────────────────────┐
│           PHASE 1 — SYNC MODEL              │
├──────────────────────┬──────────────────────┤
│  Tool standalone     │  Tool in-session     │
├──────────────────────┼──────────────────────┤
│ Stato: Zustand       │ Stato: Zustand       │
│ Persist: localStorage│ Persist: server      │
│ Rete: nessuna        │ Rete: solo log event │
│ Tutti i tool         │ Tutti i tool         │
│ (eccetto TimerTool   │ TimerTool → SSE      │
│  che è sempre local) │ Sprint 2             │
└──────────────────────┴──────────────────────┘
```

> **Phase 2 note**: VirtualTable, SSE tool sync e dado privato saranno aggiunti
> senza breaking changes grazie all'astrazione `useTimer(config)` e al layer Zustand.

---

## 6. Log e Persistenza

### 6.1 Standalone Tool Log

- Struttura: `{ id, timestamp, toolType, action, actorLabel, result }`
- Salvato in **localStorage**, chiave `meepleai:toolkit:log`
- Retention: 100 voci o 7 giorni (whichever first)
- Limite localStorage: max 500KB; voci più vecchie rimosse automaticamente
- Non sincronizzato, non inviato al server

### 6.2 Session Tool Log

- Ogni azione tool emette `ToolActionEvent` verso `SessionTracking`
- Struttura: `{ sessionId, userId, toolType, action, actorLabel, result, timestamp }`
- Persistito nel BC `SessionTracking`
- Visibile nel riepilogo post-partita

### 6.3 Transizione Standalone → In-Session

```
Given: utente ha usato toolkit standalone (log in localStorage)
When:  avvia sessione di gioco
Then:  - Toolkit carica i tool del gioco
       - Log standalone rimane in localStorage (non migra)
       - Nuove azioni → SessionToolLog (server)
       - Timer standalone attivo → si ferma (REQ-TIMER-03)
```

### 6.4 Transizione In-Session → Standalone

```
Given: sessione di gioco terminata
Then:  - Toolkit torna ai tool universali
       - Log sessione rimane su server
       - Nuovo log standalone riparte vuoto in localStorage
       - Timer SSE → si ferma (REQ-TIMER-04)
```

---

## 7. Offline Strategy

Tutti i tool sono **100% offline in Phase 1** — nessuna eccezione.

| Tool | Offline Phase 1 | Note |
|------|----------------|------|
| DiceTool | ✅ Completo | |
| TimerTool standalone | ✅ Completo | |
| TimerTool in-session | ⚠️ Degradato | Timer SSE richiede connessione; fallback a timer locale se offline |
| CardDeckTool | ✅ Completo | Stato Zustand, nessuna rete |
| CounterTool | ✅ Completo | |
| RandomizerTool | ✅ Completo | |

**TimerTool offline fallback**: se la sessione è attiva ma la connessione SSE cade, il timer passa automaticamente a `useLocalTimer` con l'ultimo stato ricevuto dal server. Al reconnect, risincronizza con `timer_state`.

---

## 8. Creazione Toolkit Game-Specific

- Creati **esclusivamente dallo staff MeepleAI** tramite admin panel
- Workflow esistente: draft → published (immutabile al publish)
- Un toolkit pubblicato è **versionato** — le sessioni attive usano la versione al momento dell'avvio
- Non esposto alla community per la creazione (Phase 1 e Phase 2)

---

## 9. UX Requirements

### 9.1 Entry Point Standalone

```
Home → [Toolkit] (bottom nav o card prominente)
Max 2 tap per raggiungere qualsiasi tool universale
```

### 9.2 Tool Grid

- Layout a griglia con card per ogni tool disponibile
- Tool game-specific in sezione separata "Tool per [Nome Gioco]"
- Badge "Game-specific" per tool override
- Indicatore offline mode in header

### 9.3 Touch Target e Accessibilità

- Bottoni: touch target minimo **48×48px** (Material/Apple HIG)
- Area dado: generosa, animazione satisfying, feedback tattile (vibrazione)
- Timer: visibile in overlay collassabile durante la serata
- Font minimo 16px per risultati e punteggi (leggibili a distanza)
- Contrasto colori: WCAG AA minimo

### 9.4 Single-Device UX

- Nessun concetto di "turno privato" — tutto visibile sullo stesso schermo
- Log azioni sempre accessibile con scroll verso il basso
- Possibilità di nominare il "giocatore corrente" prima di un tiro per il log
  (es. "Chi tira?" → dropdown giocatori dalla `SharedContext`)

---

## 10. Architettura — Note Tecniche

### 10.1 Struttura dati DiceToolConfig

Verificare che il campo `faces` in `DiceToolConfig` supporti `string[]`:

```csharp
// Attuale (da verificare)
public record DiceToolConfig(string Name, int Sides, int Count, string? Description);

// Richiesto per Phase 1
public record DiceToolConfig(
    string Name,
    int? Sides,              // null se faces custom
    IReadOnlyList<string>? Faces,  // null se sides standard
    int Count,
    string? Description
);
```

### 10.2 DefaultToolkitTemplate — Client-side

I tool universali (standalone) sono **hardcoded nel client**:

```ts
// lib/config/default-toolkit.ts
export const DEFAULT_TOOLKIT: ToolkitConfig = {
  dice: [
    { name: 'D6', sides: 6, count: 1 },
    { name: '2D6', sides: 6, count: 2 },
    { name: 'D20', sides: 20, count: 1 },
  ],
  timers: [
    { name: 'Timer libero', type: 'countdown', defaultSeconds: 60 },
  ],
  counters: [
    { name: 'Punti', initialValue: 0 },
  ],
  randomizer: { name: 'Randomizzatore', items: [] },
}
```

Nessuna chiamata API per il setup standalone. Latenza zero, funziona offline.

### 10.3 Timer Hook Abstraction

```ts
// lib/hooks/useTimer.ts
function useTimer(config: TimerConfig): TimerState {
  const session = useCurrentSession()
  return session
    ? useSSETimer(session.id, config)   // Sprint 2 — server autoritativo
    : useLocalTimer(config)              // standalone — setInterval
}
```

### 10.4 Event Flow

```
Azione tool (client)
  ↓
Zustand store update (immediato, sincrono)
  ↓
localStorage persist (standalone)
  OR
POST /api/v1/sessions/{id}/tool-events (in-session)
  → SessionTracking BC
```

### 10.5 Requisiti Non-Funzionali

| Requisito | Target | Misurazione |
|-----------|--------|-------------|
| Animazione dado | < 600ms | Profiler React su Moto G7 equiv. |
| Risposta tap tool | < 100ms perceived | Lighthouse interaction budget |
| localStorage budget | < 500KB totali | Monitorato in DevTools |
| Time-to-interactive Toolkit | < 1.5s | Lighthouse su 3G slow |
| Timer display (SSE) | ≤ 100ms latency | Sprint 2 spec |
| Cross-client sync (SSE) | ≤ 500ms | Sprint 2 spec |

---

## 11. Open Items

| # | Item | Owner | Priorità | Blocca |
|---|------|-------|----------|--------|
| OI-1 | ~~Verificare `DiceToolConfig` supporto `string[]` faces~~ — **CHIUSO** ✅ `CustomFaces: string[]?` già presente | — | — | — |
| OI-2 | *(rimandato a Phase 2)* VirtualTable design | — | — | Phase 2 |
| OI-3 | *(rimandato a Phase 2)* Transport annuncio dado | — | — | Phase 2 |
| OI-4 | ~~Schema localStorage `StandaloneToolLog`~~ — **CHIUSO** ✅ `ToolLogEntry` + `toolkit-log.ts` + tests | — | — | — |
| OI-5 | TimerTool offline fallback — **DEFERRED Phase 2** ✅ (Timer.tsx usa sempre local timer in Phase 1) | — | — | Phase 2 |
| OI-6 | ~~`POST /api/v1/sessions/{id}/tool-events` non esiste~~ — **CHIUSO** ✅ Implementato come `POST /game-sessions/{id}/events` + `useSessionToolLog` hook | — | — | — |
| OI-7 | ~~Player dropdown (§9.4)~~ — **CHIUSO** ✅ Free-text "Chi gioca?" in `toolkit/play/page.tsx` | — | — | — |

---

## 12. Out of Scope — Phase 1

- VirtualTable e multi-device sync
- Dado privato e bottone "Annuncia"
- Join tavolo via QR/link/codice
- Animazioni 3D per i dadi
- Salvataggio cloud del log standalone
- Toolkit creati da utenti non-staff
- Integrazione con sistemi di scoring esterni
- Storico statistico tiri (distribuzione probabilità)

---

## 13. Acceptance Criteria

```gherkin
# ── DADO ──────────────────────────────────────────────────────────

Scenario: Dado standard su singolo device
  Given è il turno di Marco
  When Marco preme "Tira 2D6"
  Then animazione rolling < 600ms
  And risultato visibile a tutti sullo stesso schermo
  And log registra "Marco: 2D6 → 4+3 = 7"

Scenario: Dado con facce custom (game-specific)
  Given il gioco "Arkham Horror" ha DiceToolConfig con faces ["✅","✅","✅","❌","❌","⭐"]
  When il giocatore tira 2 dadi skill
  Then ogni risultato mostra uno dei simboli definiti (non un numero)
  And il log registra i simboli estratti, non gli indici

Scenario: Dado offline
  Given nessuna connessione di rete
  When il giocatore tira un D20
  Then animazione e risultato appaiono normalmente
  And log salvato in localStorage

# ── TIMER ─────────────────────────────────────────────────────────

Scenario: Timer standalone locale
  Given utente in modalità standalone
  When crea un countdown da 3 minuti e lo avvia
  Then timer funziona localmente senza rete
  And suona alert a 10 secondi dal termine

Scenario: Transizione timer standalone → SSE
  Given timer standalone in esecuzione (2 minuti rimanenti)
  When utente avvia una sessione di Scythe
  Then timer standalone si ferma
  And notifica "Timer fermato — sessione avviata"
  And timer SSE di sessione è disponibile (120s configurato nel GameToolkit)

Scenario: Sessione termina con timer SSE attivo
  Given timer SSE di sessione sta girando (45s rimanenti)
  When la sessione viene chiusa
  Then timer SSE si ferma
  And NON viene creato automaticamente un timer locale
  And utente vede "Sessione terminata — timer fermato"

Scenario: Timer offline fallback in sessione
  Given sessione attiva con timer SSE
  When la connessione SSE cade
  Then timer continua localmente dall'ultimo stato ricevuto
  When connessione ripristinata
  Then timer risincronizza con il server via timer_state

# ── CARD DECK ─────────────────────────────────────────────────────

Scenario: Pesca carta — singolo device
  Given Pandemic Infection Deck da 48 carte
  When giocatore preme "Pesca"
  Then carta mostrata a tutti sullo schermo
  And deck aggiornato a 47 carte rimanenti
  And bottone "Annulla" disponibile per 30 secondi

Scenario: Undo draw — dentro la finestra
  Given giocatore ha pescato una carta 15 secondi fa
  When preme "Annulla"
  Then carta torna nel deck (48 rimanenti)
  And log registra "Draw annullato"

Scenario: Undo draw — finestra scaduta
  Given giocatore ha pescato una carta 35 secondi fa
  When prova ad annullare
  Then sistema rifiuta "Finestra undo scaduta"
  And carta rimane pescata

Scenario: Deck vuoto con auto-reshuffle
  Given Pandemic Infection Deck (reshuffleDiscardOnEmpty: true)
  And draw pile vuoto, discard pile con 48 carte
  When giocatore preme "Pesca"
  Then discard pile rimescolato nel draw pile
  And tutti vedono "48 carte rimanenti"
  And notifica "Mazzo rimescolato automaticamente" per 3 secondi

# ── RANDOMIZER ────────────────────────────────────────────────────

Scenario: Estrazione senza rimpiazzo
  Given pool con 4 giocatori ["Alice","Bob","Carlo","Diana"]
  When si estrae il primo giocatore → "Bob"
  Then pool diventa ["Alice","Carlo","Diana"]
  When si estrae ancora → "Alice"
  Then pool diventa ["Carlo","Diana"]

Scenario: Pool esaurito
  Given pool con 2 elementi, entrambi estratti
  When si tenta un'altra estrazione
  Then bottone "Estrai" è disabilitato
  And compare "Pool esaurito — premi Reset per ricominciare"

# ── STANDALONE → IN-SESSION ───────────────────────────────────────

Scenario: Transizione standalone → in-session
  Given utente ha usato toolkit standalone (2 tiri dadi, 1 timer nel log locale)
  When avvia sessione di Catan
  Then toolkit carica i tool di Catan
  And log standalone rimane in localStorage (non migra)
  And nuove azioni registrate nel SessionToolLog su server

# ── OFFLINE ───────────────────────────────────────────────────────

Scenario: Tutti i tool funzionano offline (standalone)
  Given nessuna connessione di rete
  When utente apre toolkit standalone
  Then DiceTool, TimerTool, CardDeckTool, CounterTool, RandomizerTool
       funzionano tutti normalmente
  And log salvato in localStorage
```

---

*Spec v0.2 — Aggiornata dopo spec-panel review e chiarimento architetturale Phase 1/Phase 2*
*Experts: Cockburn, Wiegers, Adzic, Fowler*
*Review: superpowers:code-reviewer*
