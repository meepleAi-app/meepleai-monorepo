# Audit: l'agente AI in-sessione e le sue opzionalità (Q&A gioco + sessione live)

**Data:** 2026-07-29
**Trigger (domanda utente):** analizzare l'agente dell'app e le opzionalità che fornisce quando risponde a domande di un gioco o gestisce una sessione.
**Metodo:** `/sc:spec-panel`. (1) Ricostruzione della specifica *implicita* dal codice via 4 ricognizioni parallele (RAG/Q&A backend · sessione live backend · UI frontend · agent-config/toolkit). (2) **Verifica avversariale** dei 10 finding "da confermare": 1 verificatore per claim legge il codice reale, i 4 critici passano da un secondo verificatore con mandato di *confutare* (verdetti riportati § 2). (3) **Discussion panel** a 5 lenti indipendenti sui due finding critici confermati (§ 3). Tutti i riferimenti `file:line` sono stati letti sul codice, non inferiti.

---

## 1. Executive summary + verdict

Le opzionalità dell'agente **esistono e sono profonde** (pipeline RAG con RRF+rerank+sentence-window, scoring polimorfico strategy-pattern, degradazione graziosa multi-livello, IDOR guard diffusi, 8 `AgentType` × 4 `AgentMode` configurabili). Il problema non è la mancanza di feature: è la **duplicazione non riconciliata** e le **promesse UI non mantenute**.

Il difetto dominante, confermato da verifica avversariale e da consenso 5/5 del panel:

> **Il grounding dell'agente in-sessione è una proprietà della *modalità di input*, non un invariante del sistema.** Lo scenario più naturale in-sessione — *"scatto una foto del tavolo e chiedo una regola"* — è esattamente quello che salta il rulebook e restituisce una risposta autorevole (`confidence 0.85`) **silenziosamente non-grounded**, senza alcun disclaimer.

**Verdict: fondamenta solide, contratto di affidabilità assente.** La base tecnica è di alta qualità, ma manca il prerequisito che il prodotto vende — la garanzia che una risposta non-grounded sia etichettata come tale.

Finding tracciati come issue: **#3388** (P1), **#3389** (P2), **#3390** (epic).

---

## 2. Verifica avversariale — 10 finding (6 confermati, 4 qualificati)

| # | Claim | Verdetto | Adversariale | Correzione emersa |
|---|---|---|---|---|
| **C1** | Doppio backend chat in-session (testo→RAG citazioni / immagini→multimodale senza RAG) | 🟡 PARZIALE | STANDS | **Peggiore**: la risposta con immagine perde le citazioni **ma non mostra alcun disclaimer** → *silenziosamente non-grounded*. `SessionLiveView.tsx:1257`, `LiveAgentChat.tsx:240`, `ChatCommandHandlers.cs:193/201/244` |
| **C2** | `userTier:null` nel path B disattiva gli enhancement RAG | 🟡 PARZIALE | STANDS | **Più forte**: enhancement (CRAG/RAPTOR/Fusion/Graph) **inattivi in ogni path utente**, solo debug admin (`StreamDebugQaQueryHandler.cs:249`). Il confronto "path A li attiva" era errato: `AskQuestionQueryHandler` è pipeline separata. Baseline hybrid+RRF+rerank+sentence-window resta attivo |
| **C3** | Due enum ruolo con valori invertiti | 🟡 PARZIALE | STANDS | Fatti esatti, rischio **latente non attivo** (bridge `CreateSessionCommand` non trasporta il ruolo, enum persistiti per nome). Ma: **terzo enum omonimo** `GameManagement…Entities.ParticipantRole` (collisione di nome) + enum escono come **int verso il FE** (`SseJsonOptions.cs:15`) → rischio conflazione lato frontend |
| **C4** | Budget in-sessione fail-open vs quota Q&A fail-closed | ✅ CONFERMATO | QUALIFIED | Asimmetria reale, ma: 403 fail-closed è **dead code oggi** (`NullPricingEngine`); il "modello di fallback" del path B **non esiste** (`hasBudget` scoped/mai riletto, log fuorviante); fail-open **documentato come scelta intenzionale** |
| **C5** | `AgentModeSelector` (RulesClarifier/…) non montato | ✅ CONFERMATO | — | Orfano (0 route). L'omonimo **admin** `AgentModeSelector` *è* montato — componenti distinti |
| **C6** | Note visibility private/shared solo-FE | ✅ CONFERMATO | — | Scartato già nel handler FE (`_visibility` unused), POST diary text-only, `DiaryEntry` senza campo visibility. Documentato (#2570) |
| **C7** | `SelectedDocumentIds` accettato ma non persistito | ✅ CONFERMATO | — | Ignorato in scrittura e sempre vuoto in risposta. Documentato (MVP #657/#658) |
| **C8** | Dispute Arbitro solo SignalR, no idratazione REST | ✅ CONFERMATO | — | L'endpoint REST **esiste già** (`GET /live-sessions/games/{gameId}/dispute-history`) + query per-sessione non wired; non perse lato server (EF-backed). **Fix = cablare l'endpoint esistente** |
| **C9** | Status "online" mockati + latenza 42ms hardcoded | ✅ CONFERMATO | — | `AgentSelector` sempre "online" (POC, poll no-op); `ChatAgentPanel` pip "Online" statico + `42ms` hardcoded in 3 punti |
| **C10** | Due sistemi di scoring indipendenti | ✅ CONFERMATO | — | Confermato; dentro `SessionTracking` ce ne sono **due** (polimorfico + storico) → ≥3 code-path. Nessuna sincronizzazione tra i BC |

### Superficie di opzionalità (ricostruita)

**Tassonomia agente** — 2 concetti (`AgentDefinition` template admin-only vs `AgentConfiguration`/`AgentConfig` runtime self-service); 8 `AgentType` (`RAG · Citation · Confidence · RulesInterpreter · Conversation · Strategist · Narrator · Tutor`); 4 `AgentMode` (`Chat · Player · Ledger · Arbiter`); provider `OpenRouter`/`Ollama`; routing per tier utente e complessità query.

**Scenario A — Q&A su un gioco:** 3 famiglie di flussi (QA classico `AskQuestionQueryHandler` · chat sessione `ChatWithSessionAgentCommandHandler` · Arbitro `ValidateMoveCommand`); 4 strategie retrieval + 5 enhancement gated; citazioni con copyright tier; SSE streaming; AgentMemory (house rules/notes/preferences); degradazione `Full→Degraded(BGG)→None`.

**Scenario B — sessione live:** ⚠️ **due aggregati paralleli** (`SessionTracking.Session` RowVersion vs `GameManagement.LiveGameSession` Xmin); scoring polimorfico `Points/BinaryWin/Objectives/Ranking` (solo Host edita); turni; note cifrate AES; widget/toolkit; dispute; real-time **doppio canale** (SignalR + SSE); gating agente `AgentSessionMode`; player User-linked vs guest + RSVP a fasi.

---

## 3. Discussion panel — 5 lenti sul grounding in-sessione

**Consenso 5/5:** *lo stato di grounding deve diventare un invariante esplicito del contratto di risposta, non un effetto collaterale della modalità di input.*

| Lente | Frase chiave | Rischio |
|---|---|---|
| 💬 Fiducia utente | «Un sistema onesto *a intermittenza* è meno affidabile di uno mai onesto.» | L'utente ha imparato *"nessun avviso = grounded"*; il path immagine infrange la promessa implicita nel momento di massimo bisogno |
| 🔍 RAG quality | «Il grounding è proprietà della *modalità*, non invariante del *sistema*.» | Chiudere il gap costa poco (la query è il testo del turno); ⚠️ CRAG web-fallback su dominio chiuso **peggiora** la fedeltà |
| 🛡️ Affidabilità prod | «Degrada *mentendo*: cieco sul peggior failure mode con strumenti che mentono.» | Log `"fallback model"` inesistente (↑MTTR); `confidence 0.85` = manometro dipinto; nessuna metrica grounded/non-grounded |
| 🧪 Testing | «Non è un bug di codice: è un **buco di specifica**.» | Failure trust-critical invisibile alla CI: nessun invariante cross-path né negative-space assertion |
| 🏗️ Architettura | «Il confine è tracciato lungo la costura sbagliata: la modalità di input ha *leakato* fino a biforcare la Published Language.» | Chi possiede "risposta grounded" (`KnowledgeBase`) deve possedere la risposta; `SessionTracking` deve *consumarla* |

**Tensione:** warning fatigue vs onestà → risolta con copy calibrato per-modalità (spiega *cosa* ha grounded), non allarme generico.

---

## 4. Piano d'azione

*Sequenza su cui il panel converge: prima rendere onesto il segnale, poi chiudere il gap di capacità — due lavori distinti, il primo non aspetta il secondo.*

| Fase | Azione | Effort | Issue |
|---|---|---|---|
| **1 — Onestà** | Contratto condiviso `groundingStatus` non-nullable prodotto da entrambi gli handler; disclaimer per-modalità; **rimuovere confidence 0.85** | S/M | **#3388** (P1) |
| **2a — Osservabilità** | Metrica `{path, groundingStatus, citationCount, retrievalProfile, enhancementsActive}` + alert; correggere log "fallback" menzognero | M | (in #3388/#3390) |
| **2b — Quick win qualità** | Disaccoppiare profilo retrieval da `userTier` (profilo esplicito "live-session"); non tocca i confini | M | **#3389** (P2) |
| **3 — Unificazione strutturale** | Turno multimodale attraverso il retrieval (vision=stato, testo=query→citazioni); `SessionTracking`→consumer | L | **#3390** (epic) |
| **Prerequisito enhancement** | Eval set golden (recall@k + citation-accuracy) su 3-5 regolamenti prima di attivare enhancement; **disabilitare CRAG web-fallback** | L | (in #3390) |

### Finding minori (non ancora tracciati come issue)

- **C8** — cablare l'endpoint REST dispute-history esistente nel tab Arbitro (fix banale, alto valore).
- **C6/C7/C9** — rimuovere/cablare le opzionalità "fantasma" (note-visibility solo-FE, `SelectedDocumentIds` non persistito, mock "online"/42ms).
- **C3** — mapper esplicito + test ai confini per i due enum ruolo; valutare rinomina del terzo enum omonimo.
- **C1/C10** — ADR che dichiara l'SSOT tra i due modelli di sessione e i due sistemi di scoring.

---

## 5. Note di metodo

- I verdetti "PARZIALE" non indicano finding deboli: indicano che il *nucleo* è confermato ma il *framing* iniziale è stato corretto dalla verifica avversariale (in C1/C2 la realtà è **peggiore**; in C3/C4 più **sfumata**).
- La verifica è stata condotta su `main-dev` al 2026-07-29. I punti marcati "mock/hardcoded/non persistito" sono i più a rischio di divergere dalla documentazione: ri-verificare prima di trattarli come definitivi in futuro.
