# Game-Night User Flow — Design Spec

**Date**: 2026-05-09
**Author**: Brainstorming session (spec-panel critique → Socratic refinement)
**Scope**: Definire i 5 user goal SMART per il flusso primario "utente Alpha che usa MeepleAI in serata di gioco per chiarimenti sul regolamento", con criteri di accettazione Gherkin e mapping ai gap di migrazione v2 attualmente aperti.
**Status**: DRAFT — pending user review

---

## 1. Contesto

### 1.1 Use case primario (Cockburn)

- **Attore primario**: utente Alpha (giocatore non tecnico) durante una serata di gioco da tavolo
- **Goal generico**: ottenere chiarimenti su regolamenti, FAQ, edge case e procedure di scoring **senza interrompere il flusso del gioco** né perdere fiducia nella risposta
- **Trigger**: dubbio in tavola che il gruppo non sa risolvere

### 1.2 Job-To-Be-Done (Christensen)

L'utente "assume" MeepleAI al posto della combinazione di:

| Alternativa | Limite | Cosa MeepleAI deve replicare |
|---|---|---|
| Manuale PDF/cartaceo | Lento (2-5 min di sfoglio) | Velocità di lookup |
| Google / BGG forum | Risultati off-topic, non sempre affidabili | Ampiezza di copertura, ma filtrata |
| Amico esperto | Non sempre disponibile o accurato | Autorevolezza percepita, *ma con citazione* |

L'utente **non** assume MeepleAI per:
- Consigli di strategia (escluso D in Q4)
- Decisioni di lore o role-playing

### 1.3 Vincoli operativi (validati durante brainstorming)

| Dimensione | Risposta utente | Implicazione progettuale |
|---|---|---|
| **Device pattern** | Single-user + handoff (no multi-device sync) | No SSE multiplayer per chat; necessità "fast resume" sotto 3s |
| **Entry point** | Mix di chat-già-aperta + hub navigation + background resume | Hub deve avere "Giochi recenti"; route chat-in-game devono essere bookmarkable |
| **Tipologia domande** | Tutor + Arbitro (regole, ambiguità, setup, scoring, edge case) | Agente unico orientato fattuale; retrieval ibrido FAQ + RAG |
| **Trust model** | Citazione **sempre** + dichiarazione esplicita di incertezza | `/kb/[id]` diventa critica; chat UI deve avere chip "fonte" first-class |
| **Latenza** | <10s con spinner; accuratezza > velocità | Si può usare LLM grande + reranker completo; risposta atomica accettabile |

---

## 2. User Goals SMART (5)

### G1 — Lookup rapido con citazione autoritativa

**Smart**: Un utente Alpha in serata può ottenere risposta a una domanda di regolamento (categorie A/B/C/E/F della tassonomia in §1.3) **in ≤10s** con **almeno una citazione esplicita** (pagina + sezione del manuale) per il **≥90%** delle query, entro il rilascio Alpha v2 (post token redesign #807).

**Sostituisce**: manuale + Google. **Drives**: chat-in-game v2 design (gap mockup), system prompt agente, KB ingestion con metadati pagina/sezione.

**Acceptance criteria (Gherkin)**:

```gherkin
Scenario G1.1: Lookup regola puntuale con citazione (categoria A)
  Given utente Alpha è loggato e ha "Wingspan" indicizzato in KB (kbId=wingspan-v2)
  And la chat-in-game è aperta su /library/games/wingspan?tab=aiChat
  When digita "Posso giocare due carte uccello nello stesso turno?" e invia
  Then entro 10 secondi vede una risposta atomica
  And la risposta contiene almeno un chip "Fonte: Manuale p.X §Y"
  And il chip è cliccabile e apre /kb/wingspan-v2#chunk-<id> con il chunk evidenziato

Scenario G1.2: Edge case raro con confidenza esplicita (categoria F)
  Given chat-in-game aperta su un gioco con KB indicizzata
  When utente chiede "Cosa succede se il mazzo finisce a metà partita?"
  And l'agente recupera chunk con confidence_score < 0.70
  Then la risposta inizia con un marker di incertezza ("Non sono sicuro, ma direi…")
  And il chip fonte mostra anche il livello di confidenza ("conf. 62%")
  And include un suggerimento alternativo ("verifica anche su BGG forum")
```

---

### G2 — Fast resume su device handoff

**Smart**: Quando lo smartphone viene passato da un giocatore all'altro al tavolo, la persona che lo riceve può **continuare la conversazione in ≤3s** senza re-auth, vedendo cronologia chat, agente attivo e gioco di contesto, per il **100%** dei passaggi nel corso di una serata.

**Sostituisce**: friction di re-orientamento. **Drives**: nessun re-auth per messaggio, header chat persistente con contesto gioco+agente, no modal di onboarding al riapertura.

**Acceptance criteria (Gherkin)**:

```gherkin
Scenario G2.1: Handoff durante chat in-game, sessione preservata
  Given Marco ha una chat aperta su /library/games/wingspan?tab=aiChat con 5 messaggi
  And l'agente attivo è "Tutor"
  When Marco passa il telefono a Lucia
  And Lucia tocca lo schermo per riattivarlo
  Then entro 3 secondi Lucia vede gli ultimi 5 messaggi senza scroll
  And vede chiaramente il nome del gioco "Wingspan" nell'header
  And vede il badge agente attivo "Tutor"
  And può digitare e inviare senza inserire credenziali
```

---

### G3 — Accesso rapido al gioco corrente dall'hub

**Smart**: Da app appena aperta (cold start o background), un utente Alpha raggiunge la **chat-in-game di un gioco usato nelle ultime 24h in ≤2 tap**, per il **100%** dei giochi presenti nella libreria utente, entro il rilascio Alpha v2 (post token redesign #807).

**Sostituisce**: navigazione manuale lunga. **Drives**: blocco "Giochi recenti" in dashboard, wiring dei componenti v2 `GamesHero`/`GamesResultsGrid` (pending — vedi §4), preserve scroll/state al ritorno da background.

**Acceptance criteria (Gherkin)**:

```gherkin
Scenario G3.1: Cold start, gioco recente, 2 tap fino alla chat
  Given utente Alpha apre l'app per la prima volta nella serata
  And ha giocato "Wingspan" nelle ultime 24h (presente in libreria recente)
  When la dashboard è caricata
  Then vede un blocco "Giochi recenti" con "Wingspan" come prima card
  When tocca la card "Wingspan" (1° tap)
  Then atterra su /library/games/wingspan
  When tocca la tab "AI Chat" (2° tap)
  Then la chat-in-game è pronta a ricevere input entro 2 secondi totali

Scenario G3.2: Resume da background preserva scroll e agente
  Given utente ha la chat aperta con scroll a metà cronologia
  And mette l'app in background per 30 minuti
  When riapre l'app dal task switcher
  Then atterra esattamente sulla chat-in-game con scroll preservato
  And l'agente attivo è ancora "Tutor"
```

---

### G4 — Navigabilità della fonte (KB detail funzionante)

**Smart**: Ogni chip "fonte" mostrato in chat (vedi G1) **apre la pagina `/kb/[id]` corrispondente** con il chunk citato evidenziato e contesto navigabile (chunk precedente/successivo) entro **≤2s**, per il **100%** delle citazioni emesse, entro il rilascio Alpha v2 (post token redesign #807).

**Sostituisce**: dover sfogliare PDF a mano. **Drives**: implementazione dei 6 stub pending in `apps/web/src/components/v2/kb-detail/` (KbHeader, KbChunkListPanel, KbChunkPreview, ChunkSearchBox, MarkdownRenderBlock, KbProcessingState).

**Acceptance criteria (Gherkin)**:

```gherkin
Scenario G4.1: Citazione cliccabile apre KB con chunk evidenziato
  Given chat in-game ha appena risposto con chip "Fonte: Manuale p.12 §3.2"
  When utente tocca il chip
  Then atterra su /kb/wingspan-v2#chunk-abc123 entro 2 secondi
  And il chunk citato è evidenziato visivamente (background colorato)
  And lo scroll è già posizionato sul chunk
  And può navigare al chunk precedente/successivo con frecce
  And può tornare alla chat con back-button preservando contesto

Scenario G4.2: Search dentro la KB per esplorare il regolamento
  Given utente è su /kb/wingspan-v2 (arrivato da una citazione)
  When digita "endgame" nella ChunkSearchBox
  Then vede chunk con match evidenziati entro 1 secondo
  And può tornare alla chat in qualsiasi momento
```

---

### G5 — Disclosure calibrata dell'incertezza

**Smart**: L'agente AI **dichiara incertezza esplicitamente** (testo + indicatore visivo) quando la confidenza del retrieval è **< 70%**, per il **100%** di queste risposte, evitando il pattern "amico-esperto-che-tira-a-indovinare", entro il rilascio Alpha v2 (post token redesign #807).

**Sostituisce**: rischio di accettare risposte sbagliate come definitive. **Drives**: aggiornamento system prompt agente Tutor/Arbitro, esposizione `confidence_score` dal retrieval pipeline al frontend, design del chip "fonte" con coding colore per livello confidenza.

**Acceptance criteria (Gherkin)**:

```gherkin
Scenario G5.1: Confidenza alta, risposta diretta
  Given utente fa una domanda di setup (categoria C)
  And il retrieval ha trovato un chunk con confidence_score = 0.92
  When la risposta viene composta
  Then la risposta non contiene marker di incertezza
  And il chip fonte mostra "conf. 92%" con colore verde

Scenario G5.2: Confidenza bassa, marker esplicito + alternative
  Given utente fa una domanda su edge case raro (categoria F)
  And il miglior chunk recuperato ha confidence_score = 0.55
  When la risposta viene composta
  Then la risposta inizia con "Non sono sicuro, ma…"
  And il chip fonte mostra "conf. 55%" con colore arancione
  And include una sezione "Alternative da verificare" con suggerimenti (BGG, manuale p.X)
  And l'utente vede un disclaimer testuale di 1 riga

Scenario G5.3: Confidenza nulla, risposta bloccata
  Given utente fa una domanda fuori KB
  And nessun chunk ha confidence_score > 0.30
  When la risposta viene composta
  Then la risposta è "Non ho trovato risposta affidabile nel regolamento di <gioco>"
  And include CTA per cercare su BGG o caricare un'integrazione PDF
```

---

## 3. Esclusioni esplicite (out of scope per questo spec)

- **Chat strategica/consigli di gioco** (categoria D in Q4) — agente Stratega non in scope serata
- **Multi-device sync real-time** della chat (rifiutato in Q2) — niente SSE multiplayer chat
- **Voice input / output** — non emerso come requisito; rinviato a futuro
- **Modalità offline** — non discussa, assunta connettività disponibile
- **Deep-link / widget OS** (D in Q3) — fuori scope Alpha
- **Discover, Game-Nights, Player detail, Toolkit detail** — non bloccanti per il flusso serata (pending Wave 3+)
- **Sessione live tracciata** (`/sessions/live/[sessionId]`) — opzionale, non sul critical path del flusso "chiarimento regolamento"

---

## 4. Mapping ai gap di migrazione v2 (priorità di implementazione)

| Goal | Componenti pending | Path | Stato | Blocker |
|---|---|---|---|---|
| **G1** | Chat-in-game v2 (no mockup SP4) | `/library/games/[gameId]?tab=aiChat` | ❌ scope gap | **Issue Claude Design** per mockup |
| **G1, G5** | System prompt agente con citazione + confidenza | Backend KnowledgeBase BC | 🟡 da rivedere | Coordinamento backend agent config |
| **G2** | Header chat persistente con contesto gioco+agente | Componente chat container | 🟡 esistente, verificare AC | Audit UX su mobile |
| **G3** | `GamesHero`, `GamesResultsGrid`, `GamesEmptyState` | `apps/web/src/components/v2/games/` | ✅ implementati, ❌ **non wired** su tab catalog/kb | Wiring nel `DashboardClient.tsx` |
| **G3** | "Giochi recenti" widget in dashboard | Dashboard Alpha | 🟡 da progettare | Possibile riuso `RecentActivityRail` da `/library` (PR #574 done) |
| **G4** | `KbHeader`, `KbChunkListPanel`, `KbChunkPreview`, `ChunkSearchBox`, `MarkdownRenderBlock`, `KbProcessingState` | `apps/web/src/components/v2/kb-detail/` | ❌ 6 stub `return null` | Implementazione Tier M, 1-2 PR |
| **G5** | Chip "fonte" con color coding confidenza | Componente chat (parte di G1) | ❌ design gap | Insieme al mockup chat-in-game |

**Priorità implementazione consigliata**:

1. **G3 wiring** (effort S, sblocca subito coerenza visiva hub) — single PR
2. **G4 KB detail** (effort M, sblocca G1.1/G4 acceptance) — 1-2 PR Tier M
3. **G1 + G5 chat-in-game v2** (effort L, richiede mockup design + backend coordination + frontend) — multi-PR, richiede issue Claude Design upstream
4. **G2 fast resume** (effort S audit + eventuale tweak) — può essere PR dopo G1

---

## 5. Open questions (da risolvere in implementation plan)

- **OQ1**: la soglia confidence_score è uniforme (0.70) o per categoria di domanda? (es. setup C più stretta, edge-case F più tollerante)
- **OQ2**: come si comporta G1 se l'utente chiede in lingua diversa dal regolamento? Translate-on-the-fly già esiste (`/library/games/[gameId]/translate` PR #790) — riusare?
- **OQ3**: G3 "Giochi recenti" usa quale criterio di "recente"? Last-opened, last-played-session, o last-asked-AI?
- **OQ4**: G4 "back to chat con contesto preservato" richiede un meccanismo specifico (history.back, deep-link con state)? Va specificato in plan.
- **OQ5**: G2 "fast resume" su iOS richiede attenzione speciale (Safari kill background tabs aggressivamente)? Service worker?

---

## 6. Riferimenti

- Spec-panel critique precedente (in conversation history) — analisi gap migrazione v2 per flusso serata di gioco
- v2 migration matrix: [`docs/for-developers/frontend/v2-migration-matrix.md`](../../for-developers/frontend/v2-migration-matrix.md)
- v2 design migration spec: [`docs/for-developers/specs/2026-04-26-v2-design-migration.md`](../../for-developers/specs/2026-04-26-v2-design-migration.md)
- Libro-game vision: [`docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md`](./2026-05-04-libro-game-assistant-vision.md)
- Alpha mode scope: `CLAUDE.md` §"Alpha Mode" — Auth → Games + BGG → PDF upload → RAG Chat → Library
- Bounded Contexts coinvolti: KnowledgeBase (RAG, agenti, chat), GameManagement (catalogo), DocumentProcessing (PDF→KB), UserLibrary (giochi recenti)
