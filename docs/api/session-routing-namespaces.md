# Session Routing Namespaces

Il codebase espone tre namespace HTTP distinti per le sessioni. Usare il namespace corretto in base al contesto.

## 1. `/api/v1/sessions` — Session Flow v2.1 (nuovo)

**File:** `apps/api/src/Api/Routing/SessionFlowEndpoints.cs`  
**BC:** `SessionTracking` (esteso per v2.1)  
**Uso:** avvio partite con KB readiness check, diary, GameNight ad-hoc, turn order

| Metodo | Path | Scopo |
|--------|------|-------|
| GET | `/games/{id}/kb-readiness` | Verifica KB pronta prima di avviare |
| POST | `/sessions/{id}/pause` | Mette in pausa la sessione attiva |
| POST | `/sessions/{id}/resume` | Riprende una sessione in pausa |
| PUT | `/sessions/{id}/turn-order` | Imposta ordine turni (manual/random) |
| POST | `/sessions/{id}/turn/advance` | Avanza al turno successivo |
| POST | `/sessions/{id}/scores-with-diary` | Aggiorna punteggio + emette diary event |
| GET | `/sessions/{id}/diary` | Diary della singola sessione |
| GET | `/game-nights/{id}/diary` | Diary UNION di tutte le sessioni della serata |
| GET | `/sessions/current` | Sessione attiva/paused dell'utente (recovery) |
| GET | `/sessions/{id}/diary/stream` | SSE diary stream (v2.2) |
| POST | `/game-nights/{id}/complete` | Chiude la serata e finalizza le sessioni |

---

## 2. `/api/v1/game-sessions` — Legacy SessionTracking

**File:** `apps/api/src/Api/Routing/SessionTracking/SessionCommandEndpoints.cs`, `SessionQueryEndpoints.cs`, `SessionTrackingEndpoints.cs`  
**BC:** `SessionTracking` (endpoints storici)  
**Uso:** creazione sessione base, join, scoreboard, SSE sync, statistiche

| Metodo | Path | Scopo |
|--------|------|-------|
| POST | `/game-sessions` | Crea sessione (vecchio flow) |
| PUT | `/game-sessions/{id}/scores` | Aggiorna punteggio (senza diary) |
| POST | `/game-sessions/{id}/participants` | Aggiunge partecipante |
| POST | `/game-sessions/{id}/notes` | Aggiunge nota |
| POST | `/game-sessions/{id}/finalize` | Finalizza sessione |
| POST | `/game-sessions/{id}/dice` | Tiro dado (senza diary event) |
| GET | `/game-sessions/active` | Sessione attiva |
| GET | `/game-sessions/code/{code}` | Sessione per codice join |
| GET | `/game-sessions/{id}/scoreboard` | Scoreboard |
| GET | `/game-sessions/{id}` | Dettaglio sessione |
| GET | `/game-sessions/history` | Storico sessioni |
| GET | `/game-sessions/{id}/stream` | SSE sync stream (vecchio) |
| GET | `/game-sessions/session-statistics` | Statistiche aggregate |

> ⚠️ Questi endpoint sono mantenuti per retrocompatibilità con il frontend esistente. Per nuove feature usare `/sessions`.

---

## 3. `/api/v1/live-sessions` — GameNight Improvvisata

**File:** `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`  
**BC:** `SessionTracking` (epic #379 — GameNight Improvvisata)  
**Uso:** flusso completo Game Night Improvvisata con AI score parsing, dispute, snapshot

| Metodo | Path | Scopo |
|--------|------|-------|
| POST | `/live-sessions` | Crea sessione live |
| POST | `/live-sessions/{id}/start` | Avvia sessione |
| POST | `/live-sessions/{id}/pause` | Pausa |
| POST | `/live-sessions/{id}/resume` | Riprendi |
| POST | `/live-sessions/{id}/complete` | Completa |
| POST | `/live-sessions/{id}/players` | Aggiungi giocatore |
| POST | `/live-sessions/{id}/scores` | Registra punteggio |
| POST | `/live-sessions/{id}/scores/parse` | Parse NLP punteggio (ScoreAssistant) |
| POST | `/live-sessions/{id}/disputes` | Apri disputa regole |
| POST | `/live-sessions/{id}/trigger-snapshot` | Salva snapshot pausa |

---

## Quale usare?

| Scenario | Namespace |
|----------|-----------|
| Avvio partita con KB ready check + Contextual Hand | `/sessions` |
| Turn order, diary, GameNight ad-hoc, SSE diary | `/sessions` |
| Join sessione esistente via codice | `/game-sessions` |
| Statistiche aggregate, scoreboard legacy | `/game-sessions` |
| Game Night Improvvisata full flow (AI, dispute, snapshot) | `/live-sessions` |

---

*Ultimo aggiornamento: 2026-04-11*
