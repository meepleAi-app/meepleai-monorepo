---
title: Libro Game Nanolith — Demo Dogfooding Design
status: draft
type: spec-design
date: 2026-05-07
authors: [DegrassiAaron]
related-issues: []
related-specs:
  - docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md
related-plans:
  - docs/for-developers/plans/2026-05-06-sp6-libro-game-migration.md
review-cycle: brainstorming + spec-self-review + sc:spec-panel
---

# Libro Game Nanolith — Demo Dogfooding Design

> **DOCUMENT TYPE**: Spec design per **MVP Iter 1** (1.A + 1.B) di una demo dogfooding. NON è il vision a 12 mesi (vedi [vision-doc 2026-05-04](2026-05-04-libro-game-assistant-vision.md)). NON è il plan d'implementazione (verrà dopo via writing-plans skill).

## Executive Summary

**Caso d'uso**: Aaron (project owner, superadmin di MeepleAI) gioca a **Nanolith** con 3-4 amici sul tavolo di casa. L'app aiuta con (a) tutorial setup dal Press Start indicizzato, (b) Q&A regole in-game dal Rules indicizzato, (c) traduzione paragrafi narrativi dello Storybook fotografando le pagine fisiche, (d) save/resume cross-day per la campagna mista.

**Pain risolto**: lo Storybook è cartaceo solo, in EN. Senza app, Aaron deve leggere ad alta voce traducendo a braccio o consultare il PDF cartaceo. La partita rischia di "morire alla seconda sessione" (pain dichiarato vision §1.2). Il dogfooding di Aaron è il **primo alpha test reale** del vision.

**Differenza dal vision**:
- Vision G1 (acquisizione manuale via foto Rules) ❌ skippato — KB Rules + Press Start già pre-indicizzati
- Vision G4 (traduzione paragrafo da KB indicizzato) ❌ inadeguato — Storybook NON sarà mai indicizzato (privacy + copyright + ephemeralità). Sostituito da **N3** "translate-on-the-fly" via foto pagina single-shot
- Vision §1.5 dichiara "save/resume → MVP-1" ❌ ribaltato — N4 save/resume cross-day è MVP perché dogfooding richiede campagna persistente

**Status MVP demo**: scope-cap rigoroso a **2 settimane di lavoro** (Iter 1.A + Iter 1.B). Encounter Book UX cheatsheet + multi-device + voice/TTS + altre feature deferred a Iter 2 o vision MVP-1/v2.

---

## Indice

- [1. Context & Persona](#1-context--persona)
- [2. User Goals SMART](#2-user-goals-smart)
- [3. Scenari Gherkin](#3-scenari-gherkin)
- [4. Architettura & Data Flow](#4-architettura--data-flow)
- [5. Persistence Model](#5-persistence-model)
- [6. Failure Modes & Graceful Degradation](#6-failure-modes--graceful-degradation)
- [7. Testing Strategy](#7-testing-strategy)
- [8. Pre-condizioni & Seed Dataset](#8-pre-condizioni--seed-dataset)
- [9. Out of Scope](#9-out-of-scope)
- [10. Decisions Log](#10-decisions-log)
- [11. Open Questions Carry-forward](#11-open-questions-carry-forward)

---

## 1. Context & Persona

### 1.1 Persona dogfooding

> **Aaron (badsworm@gmail.com), superadmin MeepleAI**, project owner. Possiede Nanolith fisico, ha pre-indicizzato Rules + Press Start dal datasource `data/rulebook/nanolith_datasource/` (2 PDF: `Nanolith Rules ENG.pdf` 101MB + `Nanolith Press Start ENG.pdf` 36MB). Ha pre-creato un agente `Nanolith Tutor` linked a entrambi i KB. Vuole giocare 1 campagna mista con 3-4 amici al tavolo, smartphone in mano (single-device GM), connessione WiFi domestica.

> *Job-to-be-done emozionale*: «Voglio giocare Nanolith con i miei amici senza che la sera muoia tra traduzione live, regole consultate sul PDF, e dimenticarci a che paragrafo eravamo alla seconda sessione».

### 1.2 Modello d'uso

Nanolith è una **campagna mista** (vedi brainstorming): lo **Storybook** è narrativa lunga multi-sessione (mesi, branching, paragrafi numerati `§N`); l'**Encounter Book** è on-demand sotto-sessione self-contained. Per Iter 1 entrambi usano lo stesso flusso N3 (foto pagina → OCR → translate); UX cheatsheet card per Encounter è deferred a Iter 2.

### 1.3 Mapping su Bounded Context MeepleAI esistenti (riuso totale)

| Capability | BC riusato | Estensione richiesta |
|---|---|---|
| Q&A setup tutorial + regole in-game (N1, N2) | `KnowledgeBase` (RAG su Press Start + Rules) | filtro `language=it` per output IT |
| Foto pagina Storybook → IT (N3) | `DocumentProcessing` + `KnowledgeBase` chat-stream | nuovo endpoint `POST /api/v1/gamebook/{gameId}/photo-translate` (single-shot OCR + paragraph segmentation + translate) |
| Save state campagna cross-day (N4) | `SessionTracking` + `GameManagement` | nuovo aggregato `GamebookCampaignSession` (paragrafi letti, glossario, scelte narrative, party) |
| Glossario per-campagna (N4) | `AgentMemory` | riuso diretto `MemoryNote` con tag `nanolith-glossary-{campaign_id}` |

**Nessun nuovo Bounded Context**.

### 1.4 Asset di codice riusabili

| Asset | Stato | Riuso |
|---|---|---|
| `useTranslateParagraph` hook | In worktree commit `aa72b1b70` (cherry-pickable) | Backbone single-shot translate flow |
| Mockup SP6-D translation-viewer fullscreen | Untracked HTML/JSX | Spec UI per Storybook reading viewer |
| Mockup SP6-C play-session | Untracked HTML/JSX | Spec UI per shell route `/play` |
| Componenti `v2/gamebook/*` (16 file) | Su `main-dev` | CameraViewfinder, PageThumb, ConfidenceBadge, OfflineBanner, CancelModal, ecc. |
| `chat-stream` LLM pipeline | Production | Backbone traduzione (no nuovo endpoint LLM) |
| `KnowledgeBase` BC + RAG | Production | Q&A regolamento Nanolith (riuso 100%) |
| `AgentDefinition` + `AgentMemory` BC | Production | Glossario per-campagna |
| `SessionTracking` BC | Production | Save state cross-day |

L'unico asset critico mancante è il backend pipeline single-shot photo → OCR → paragraph segmentation. Il resto è già pronto.

### 1.5 Vincolo FREEZE v2 attivo (issue #807, #808)

**MEMORY**: SP6 v2 expansion FREEZE 2026-05-06 vieta nuovi v2 component con pattern `hsl(*, 89%, 48%)` + `hsla(*, 89%, *, 0.10)` fino a token redesign #807 Fase 2.

**Decisione**: il viewer Storybook di N3 è una **page composition** (`apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/page.tsx`) che usa **solo primitive v2 esistenti** (`auth-card`, `btn`, `drawer`, `CameraViewfinder`, `PageThumb`, `ConfidenceBadge`, `OfflineBanner`). I mockup SP6-D + SP6-C usano solo token CSS semantic vars (`var(--c-agent)`, `var(--c-game)`, `var(--c-kb)`, `var(--c-warning)`), zero pattern HSL freeze-violating, quindi sono spec UI valida.

---

## 2. User Goals SMART

Quattro user goal numerati N1-N4, mappati a Iter 1.A/1.B per scope-cap.

### 🎯 N1 — Q&A setup & tutorial Nanolith (Press Start KB)

> Aaron, prima di iniziare la prima sessione, chiede al chat "come si setupa Nanolith per 4 giocatori?" e riceve guida actionable in IT con citazione pagina.

| SMART | Valore |
|---|---|
| **S — Specific** | Q&A in linguaggio naturale (testo, IT) sul Press Start KB, risposta che enumera step di setup con citazione pagina sorgente del Press Start indicizzato |
| **M — Measurable** | (a) ≥ 80% delle risposte di setup giudicate "actionable senza ricorrere al PDF" da Aaron su 5 query test; (b) citation accuracy ≥ 90% verificata manualmente; (c) confidence score visibile (alta/media/bassa); (d) per confidence bassa: messaggio esplicito "non sono sicuro, controlla pag. X" |
| **A — Achievable** | **Zero codice nuovo**: riuso totale `useAgentChatStream` + `KnowledgeBase` RAG + agente Nanolith preesistente |
| **R — Relevant** | Setup è il momento più frustrante del primo gioco; senza app, Aaron deve leggere ad alta voce IT-tradotto il quickstart EN agli amici |
| **T — Time-bound** | (a) P50 < 4 sec, P95 < 8 sec (rilassato vs in-game perché setup è asincrono); (b) Aaron usa il chat 5-10 volte durante setup; (c) **scope effort: 0 giorni** (riuso al 100%) |

**Definition of Done**: Aaron apre `/library/games/nanolith/play` 30 minuti prima dell'arrivo amici. Pone 5 domande tipiche ("setup 4 giocatori", "componenti necessari", "come si decide chi inizia", "fase 1 prima azione", "regola dei dadi neri"). Riceve 5 risposte IT con citazione Press Start, ≥ 4/5 actionable senza fallback PDF.

**Iter mapping**: Pre-Iter 1.A (validazione baseline pre-condizioni). Se N1 fallisce, niente Iter 1.A.

### 🎯 N2 — Q&A regole in-game durante partita (Rules KB)

> Durante la sessione, Marco chiede "il mago tira d6 o d8 per il fuoco?". Aaron digita la domanda nel chat panel laterale, ottiene risposta IT in <5 sec con citazione Rules pag. 47.

| SMART | Valore |
|---|---|
| **S — Specific** | Q&A in linguaggio naturale (testo, IT) sul Rules KB durante una sessione attiva, risposta single-statement (1-3 frasi) leggibile a voce alta in <10 sec, citazione pagina + preview cliccabile |
| **M — Measurable** | (a) **P95 latenza < 5 sec** (matching vision §4 G3 target); (b) ≥ 17/20 risposte giudicate "utili" su sessione 4h; (c) hallucination rate target 0% su confidence > 0.85 (manualmente verificato post-sessione); (d) confidence < 0.7 → messaggio "non sono sicuro" + suggerimento alternativo |
| **A — Achievable** | **Zero codice nuovo**: stesso meccanismo di N1, KB diverso |
| **R — Relevant** | Pain point #1 vision: senza app, ogni domanda ferma il flow per 2-5 minuti |
| **T — Time-bound** | (a) P50 < 3 sec, P95 < 5 sec; (b) Aaron pone 15-25 domande in 4h; (c) **scope effort: 0 giorni** (riuso al 100%) |

**Definition of Done**: Su 1 sessione 4h, Aaron pone 20 domande regole sparse, ≥ 17 risposte "utili" (annotate inline "OK / sbagliata / non lo so"), ≥ 18 in <5 sec. Le 2-3 "non lo so" hanno suggerimento azionable.

**Iter mapping**: Iter 1.A.

### 🎯 N3 — Tradurre paragrafo Storybook via foto pagina

> Aaron raggiunge §147 nel libro narrativo. Apre la fotocamera, scatta una foto della pagina aperta, vede una lista "§146, §147, §148" segmentata, sceglie §147, l'app traduce in IT e mostra il testo a font 26px sul viewer fullscreen. Aaron legge ad alta voce.

| SMART | Valore |
|---|---|
| **S — Specific** | Pipeline single-shot: 1 foto pagina A4 storybook → backend OCR + paragraph segmentation (riconosce numerazione § e separa paragrafi) → utente seleziona quale § tradurre → LLM single-pass translate IT preservando tono narrativo → display fullscreen viewer (mockup SP6-D) |
| **M — Measurable** | (a) **paragraph segmentation accuracy ≥ 90%** su 20 foto storybook test; (b) **OCR confidence ≥ 0.7** su pagine fotografate in luce salotto normale; (c) **translation latency P95 < 8 sec** end-to-end (foto → primo testo IT visibile streaming); (d) testo IT renderizzato a font 26px line-height 1.6 max-width 60ch (mockup SP6-D spec, WCAG-compliant a 1.5m); (e) ≥ 28/30 paragrafi tradotti giudicati "letti senza esitazione" |
| **A — Achievable** | (a) Backend nuovo endpoint `POST /api/v1/gamebook/{gameId}/photo-translate` (riuso smoldocling per OCR + nuovo `ParagraphSegmentationService`); (b) Frontend cherry-pick `useTranslateParagraph` da `aa72b1b70` adattato single-shot; (c) UI page composition con primitive v2 esistenti (FREEZE-compliant); (d) WiFi instabile → retry exponential backoff 31s totali |
| **R — Relevant** | Pain assoluto della demo: senza N3, niente partita. Storybook è solo EN, Aaron non può leggere ad alta voce a braccio per 30 paragrafi/sessione |
| **T — Time-bound** | (a) Foto-to-text-visible: P95 < 8 sec end-to-end; (b) costo LLM: ≤ 3¢/paragrafo medio target (200-400 parole) — superadmin bypass quota; (c) **scope effort: ~5-7 giorni** Iter 1.A |

**Definition of Done**: Aaron fotografa 30 pagine storybook durante sessione 4h. ≥ 28/30 traduzioni: OCR riconosce paragrafo target, traduzione IT leggibile a voce alta senza esitazione, latency < 8 sec. ≤ 2 fallback (rifoto / numero § manuale).

**Iter mapping**: **Iter 1.A core** (cuore della demo).

### 🎯 N4 — Resume campagna cross-day con glossario

> Aaron chiude l'app a metà sessione (paragrafo §289, glossario 12 termini, party 4 personaggi). Una settimana dopo riapre `/library/games/nanolith/play`, vede "Riprendi sessione: ultimo §289 — 12 termini glossario". Tappa, ritrova esattamente lo stato. La traduzione successiva di §290 usa coerentemente i termini del glossario.

| SMART | Valore |
|---|---|
| **S — Specific** | Aggregato persistente `GamebookCampaignSession` (campaign_id, party, last_paragraph_id, scoring, scelte narrative, traduzioni cached, glossario per-game) salvato in DB Postgres. Glossario auto-arricchito da ogni traduzione (LLM piggy-back call estrazione term-level). Resume UI: card "Riprendi" su `/library/games/nanolith/play` se sessione attiva |
| **M — Measurable** | (a) **save state survives**: chiusura app + ripristino dopo 7 giorni → 100% paragrafi precedentemente tradotti restano consultabili senza ri-tradurre (cache hit); (b) **glossary consistency**: termine "Voidstone" tradotto come "Pietra del Vuoto" alla prima occorrenza → tutte le traduzioni successive usano stessa traduzione (≥ 95% consistency su 20 termini test); (c) **resume latency**: tap "Riprendi" → UI restored < 2 sec; (d) **transaction-safe**: nessun data loss su crash mid-write |
| **A — Achievable** | (a) Estensione `SessionTracking` BC con aggregato `GamebookCampaignSession` (factory + 1 EF migration); (b) Riuso `AgentMemory.MemoryNote` per glossario; (c) Frontend hook `useCampaignSession(campaignId)` query + mutation; (d) Glossary extraction: piggy-back LLM call su translate (1 prompt template combinato) |
| **R — Relevant** | Senza N4, alla seconda sessione Aaron ritrova solo PDF cartacei → **partita muore alla seconda sessione**. N4 risolve esattamente il pain |
| **T — Time-bound** | (a) Save automatico ogni 30 sec o ad ogni tap "prossimo §" / "rifotografa"; (b) Resume garantito ≥ 90 giorni (DB persistence); (c) **scope effort: ~5-7 giorni** Iter 1.B |

**Definition of Done**: Sessione 1 (4h, 30 paragrafi tradotti, 12 termini glossario). Chiude. Una settimana dopo riapre, tappa "Riprendi", ritrova: ultimo paragrafo §289, glossario 12 termini, party 4 personaggi. Traduce §290: "Voidstone" → "Pietra del Vuoto" coerente con sessione 1.

**Iter mapping**: **Iter 1.B** (seconda settimana).

### 📊 Goal coverage matrix

| Goal | Iter | Effort stima | Riuso | Codice nuovo |
|---|---|---|---|---|
| N1 — Q&A setup Press Start | Pre-1.A | 0 giorni | 100% | nessuno |
| N2 — Q&A regole Rules in-game | 1.A | 0 giorni | 100% | nessuno |
| N3 — Translate Storybook foto | 1.A | 5-7 giorni | 70% | endpoint photo-translate + paragraph-segmentation + viewer wired |
| N4 — Resume + glossario | 1.B | 5-7 giorni | 60% | aggregato `GamebookCampaignSession` + EF migration + glossary extraction prompt |

**Totale Iter 1**: ~10-14 giorni di lavoro effettivo distribuiti su 2 settimane calendario.

---

## 3. Scenari Gherkin

Convenzione tag: `@happy` happy path, `@edge` casi limite plausibili, `@error` failure mode, `@dogfood` setup reale Aaron (vincolato all'account `badsworm@gmail.com` e Nanolith fisico).

### N1 — Q&A setup tutorial Press Start

#### N1.1 — Setup partita per 4 giocatori @happy @dogfood
```gherkin
Given Aaron è loggato come superadmin badsworm@gmail.com
And il gioco "Nanolith" è in collection di Aaron
And il KB "Nanolith Press Start ENG.pdf" è indicizzato con confidence ≥ 0.85
And l'agente "Nanolith Tutor" è creato e linkato al Press Start KB
When Aaron apre `/library/games/nanolith/play` 30 minuti prima dell'arrivo amici
And tappa l'icona chat panel laterale
And digita "come si setupa Nanolith per 4 giocatori?"
Then in P95 < 8 sec riceve una risposta IT che enumera ≥ 5 step actionable
And ogni step cita una pagina specifica del Press Start ("pag. 4", "pag. 7", ...)
And almeno 4 step su 5 sono giudicati "actionable senza fallback PDF" da Aaron
And la response include un confidence score visibile (alta/media/bassa)
```

#### N1.2 — Domanda fuori contesto Press Start @edge
```gherkin
Given Aaron è in chat con l'agente Nanolith Tutor
When digita "qual è il setup ottimale di Tainted Grail?"
Then l'agente risponde "Non ho informazioni su Tainted Grail nel mio KB. Vuoi cambiare gioco?"
And NON inventa una risposta (hallucination = 0 in questo caso edge)
And confidence score = "bassa" o messaggio "non sono sicuro"
```

#### N1.3 — Confidence bassa su Press Start @edge
```gherkin
Given Aaron chiede una regola edge-case ("setup variante 5 giocatori")
And il Press Start non copre quella variante (confidence retrieval < 0.7)
When la response viene generata
Then l'agente risponde "Non sono sicuro — il quickstart copre 2-4 giocatori. Per 5+ controlla il regolamento completo (Rules KB)."
And suggerisce esplicitamente di passare al Rules KB
And NON inventa step setup per 5 giocatori
```

### N2 — Q&A regole in-game

#### N2.1 — Domanda regola durante combat @happy @dogfood
```gherkin
Given Aaron sta giocando una sessione attiva di Nanolith con 3 amici
And ha il chat panel laterale aperto sull'agente Nanolith Tutor con KB Rules
When Marco al tavolo chiede "il mago tira d6 o d8 per il fuoco?"
And Aaron digita "quanti dadi tira il mago per il fuoco?"
Then in P95 < 5 sec riceve una risposta IT single-statement ≤ 3 frasi
And la risposta cita una pagina specifica delle Rules ("pag. 47, sezione Magia")
And la risposta è leggibile a voce alta in < 10 sec
And il confidence score è ≥ 0.85
```

#### N2.2 — Domanda ambigua o multi-interpretazione @edge
```gherkin
Given Aaron è in chat
When digita "che succede se tira 3?"
Then l'agente risponde con 1-2 chiarimenti possibili invece di guessare
And include "Vuoi sapere: (a) tiro attacco, (b) tiro difesa, (c) tiro magia?"
And NON inventa una regola fittizia
```

#### N2.3 — Hallucination guard su confidence borderline @error
```gherkin
Given una domanda con retrieval confidence 0.65 (sotto target 0.85, sopra threshold 0.5)
When la risposta viene generata
Then la UI mostra il confidence badge "media" (warning amber)
And la risposta include esplicitamente "non sono certo, controlla pag. X"
And se Aaron post-sessione marca questa risposta "sbagliata", entra in metric tracking
And hallucination rate target ≤ 3% verificato post-sessione su 20 domande sample
```

#### N2.4 — Latenza superiore a P95 budget @error
```gherkin
Given una domanda viene posta
And il backend supera il P95 budget (5 sec)
When passano 8 sec senza risposta visibile
Then la UI mostra un loader esplicito "sto cercando..."
And se passano 15 sec totali, il sistema mostra timeout + retry CTA
And NON lascia Aaron in attesa silenziosa
```

### N3 — Translate Storybook via foto pagina

#### N3.1 — Foto pagina A4 con 3 paragrafi @happy @dogfood
```gherkin
Given Aaron è in `/library/games/nanolith/play/paragraph/§147`
And il Storybook fisico è aperto alla pagina contenente §146, §147, §148
And la luce salotto è normale (non bassa)
When Aaron tappa il bottone "Fotografa pagina" (CameraViewfinder primitive)
And scatta 1 foto della pagina A4 con il telefono
And la foto è uploaded a `POST /api/v1/gamebook/{gameId}/photo-translate`
Then in ≤ 3 sec backend OCR riconosce 3 paragrafi separabili (segmentation accuracy ≥ 90%)
And la UI mostra una lista "Paragrafi trovati: §146, §147, §148" con preview snippet IT first 50 chars
And Aaron tappa "§147"
And in ≤ 1 sec post-tap il primo token della traduzione IT è visibile nel viewer fullscreen (streaming)
And in ≤ 5 sec post-tap la traduzione IT completa è visibile (completion)
And cumulato dallo scatto al primo token: P95 < 8 sec
And il testo è renderizzato a font 26px, line-height 1.6, max-width 60ch (mockup SP6-D spec)
And Aaron legge ad alta voce senza esitare
```

#### N3.2 — Foto sfocata o luce bassa @edge
```gherkin
Given Aaron fotografa una pagina con luce ambient bassa
When backend OCR riporta confidence < 0.5 sulla pagina
Then la UI mostra un banner amber "Foto poco leggibile — confidence 35%"
And mostra azioni: [📸 Rifotografa, 🔢 Inserisci numero § manualmente, ⏭️ Procedi comunque]
And se Aaron tappa "Procedi comunque" la traduzione viene tentata ma con disclaimer
And NON viene salvata cached con confidence < 0.5 (no cache pollution)
```

#### N3.3 — Backend non riesce a segmentare paragrafi @edge
```gherkin
Given Aaron fotografa una pagina densa (testo continuo senza § visibili)
When backend OCR riconosce solo 1 blocco testo, no segmentation
Then la UI mostra "Non vedo paragrafi numerati. Inserisci il numero §:"
And Aaron digita manualmente "§147"
And il sistema traduce l'intera pagina come fosse il paragrafo §147
And la sessione continua normalmente
```

#### N3.4 — Connessione WiFi instabile durante upload @error
```gherkin
Given Aaron scatta una foto con WiFi che cala intermittente
When l'upload fallisce al primo tentativo
Then il sistema retry con exponential backoff [1s, 2s, 4s, 8s, 16s] = 31s totali
And durante i retry mostra cancel button visibile
And se tutti i retry falliscono, errore esplicito "Upload fallito — verifica connessione"
And la foto resta in coda offline (IndexedDB) per re-upload manuale
```

#### N3.5 — Glossario incoerente al primo paragrafo @edge
```gherkin
Given è il primo paragrafo tradotto della campagna (glossario vuoto)
When LLM traduce §147 e estrae "Voidstone" come termine glossario
Then la UI mostra inline una pill cliccabile "Voidstone → Pietra del Vuoto" sopra il testo
And Aaron può tappare per modificare la traduzione del termine se non gli piace
And la modifica viene persistita in `AgentMemory.MemoryNote[tag=nanolith-glossary-{campaign_id}]`
And tutte le traduzioni successive useranno la traduzione approvata da Aaron
```

#### N3.6 — Costo LLM oltre soglia mensile @edge
```gherkin
Given Aaron è in superadmin mode con bypass quota
When traduce 100 paragrafi in una sessione (≥ €3 di costo)
Then la UI mostra un cost indicator discreto in bottom-right ("€2.40 questa sessione")
And NON blocca l'azione (superadmin bypass)
And invia evento telemetry a backend per tracking dogfooding cost real-world
```

#### N3.7 — Foto di pagina con script non-Latin @error
```gherkin
Given Aaron per errore fotografa una pagina di un manuale giapponese
When backend OCR rileva script CJK
Then risponde "Script non supportato (giapponese) — al momento supportiamo Latin script"
And mostra azione [⏪ Riprova con altra pagina]
And NON tenta traduzione random
```

### N4 — Resume cross-day + glossario

#### N4.1 — Resume sessione 1 settimana dopo @happy @dogfood
```gherkin
Given Aaron ha giocato sessione 1 il 2026-05-14 (30 paragrafi, 12 termini glossario, party 4 personaggi)
And ha chiuso l'app a paragrafo §289
And la `GamebookCampaignSession` è persistita in DB
When Aaron riapre `/library/games/nanolith/play` il 2026-05-21 (7 giorni dopo)
Then la UI mostra una card "Riprendi sessione: ultimo §289 letto il 14 maggio · 12 termini glossario"
And tappa la card
And in < 2 sec la sessione è ripristinata: ultimo §, glossario, party
And il viewer è posizionato pronto a ricevere il prossimo paragrafo §290
```

#### N4.2 — Coerenza glossario nella sessione 2 @happy @dogfood
```gherkin
Given Aaron ha glossario sessione 1: { Voidstone: "Pietra del Vuoto", Reaver: "Razziatore", ... }
When Aaron traduce §290 in sessione 2 (foto pagina contenente "Voidstone")
Then il LLM riceve glossario inline nel prompt template
And la traduzione di §290 usa "Pietra del Vuoto" coerente con sessione 1 (≥ 95% term consistency)
And il glossario non viene "reset" o "ri-imparato"
```

#### N4.3 — Crash mid-write durante save state @error
```gherkin
Given Aaron sta traducendo §200 e il backend è a metà del save transaction
When il browser crasha (refresh accidentale) o la rete cade
Then la transaction `GamebookCampaignSession.UpdateLastParagraph` è atomica (rollback su crash)
And al resume, l'ultimo paragrafo persistito è §199 (ultimo committed)
And la traduzione cached di §200 è in IndexedDB locale (non persa)
And Aaron al resume può "ricommit" §200 senza ri-tradurre
```

#### N4.4 — Multipli campagne attive sullo stesso gioco @edge
```gherkin
Given Aaron ha 2 campagne Nanolith parallele (gruppo A con amici-of-friends, gruppo B con la fidanzata)
When apre `/library/games/nanolith/play`
Then la UI mostra una lista "Campagne attive" con 2 card
And ogni card ha glossario, party, ultimo § distinti
And Aaron sceglie quale riprendere
And il save state non si "contamina" tra campagne (campaign_id diverso)
```

#### N4.5 — Resume dopo > 90 giorni @edge
```gherkin
Given Aaron non gioca per 100 giorni
And la `GamebookCampaignSession` è ancora persistita (no auto-purge)
When riapre l'app
Then la card "Riprendi" mostra warning "Ultima sessione 100 giorni fa — vuoi riprendere o ricominciare?"
And se sceglie "ricomincia", la sessione vecchia viene archiviata (soft-delete) NON cancellata hard
And se sceglie "riprendi", il glossario è ancora consistente
```

---

## 4. Architettura & Data Flow

### 4.1 Topology runtime

```
[Aaron phone, Chrome]
   └── /library/games/nanolith/play  (Iter 1)
        ├── ContextBar: campagna corrente, ultimo § letto
        ├── CameraButton → POST /photo-translate
        │                  ├── OCR backend (smoldocling-service)
        │                  ├── Paragraph segmentation (NEW)
        │                  ├── LLM translate IT (chat-stream existing)
        │                  └── Cache 24h locale + DB persistente
        ├── TranslationViewer fullscreen (mockup SP6-D)
        │   ├── Render paragrafo IT, font 26px, lh 1.6, max-width 60ch
        │   ├── Tap "prossimo §" / "rifotografa"
        │   └── Glossario inline (terms già tradotti coerenti)
        └── ChatPanel laterale → useAgentChatStream existing
            └── Q&A Rules KB con citazione pagina
```

### 4.2 Data flow N3 happy path frame-by-frame

```
┌─────────────────────────────────────────────────────────────────────────┐
│ FRONTEND (mobile, /library/games/nanolith/play/paragraph/§147)         │
│                                                                         │
│  Frame 1: tap "Fotografa pagina"                          [t=0s]       │
│  Frame 2: CameraViewfinder primitive                                   │
│           scatta JPEG ~2-4MB, mostra preview confirm/retake [t=0-1s]   │
│  Frame 3: POST multipart/form-data                                     │
│    URL:  /api/v1/gamebook/nanolith/photo-translate                     │
│    Hdr:  Idempotency-Key: ${campaignSessionId}:${ts}                   │
│    Body: photo + targetLang=it + sourceLang=en + currentParagraph=§147 │
│          + glossary[] (loaded from AgentMemory cache)        [t=1-2s]  │
└─────────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ BACKEND (.NET 9, MediatR)                                               │
│                                                                         │
│  Frame 4: validate (size ≤10MB, format, ownership)         [t=2s]       │
│           persiste foto temporanea S3 (R2) → photoId                    │
│  Frame 5: smoldocling-service OCR                          [t=2-4s]     │
│           output: testo grezzo + bounding boxes + confidence            │
│  Frame 6: ParagraphSegmentationService NEW                 [t=4-4.5s]   │
│           regex r"§\s*(\d+)" → separa N paragrafi                       │
│           fallback no-match → 1 paragrafo whole-page                    │
│  Frame 7: response phase 1 sync (≤ 4.5s totale)                         │
│    JSON: {photoId, paragraphs:[{number,snippet_en,confidence},...]}    │
└─────────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ FRONTEND                                                                │
│  Frame 8: UI segmentation list                                          │
│           Aaron sceglie "§147"                              [t=4.5-7s]  │
│  Frame 9: POST translate-specific                                       │
│    URL:  /api/v1/gamebook/nanolith/photo-translate/{photoId}/translate │
│    Body: {paragraph:"§147", glossary:[...12 terms]}          [t=7s]    │
└─────────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ BACKEND                                                                 │
│  Frame 10: chat-stream LLM existing (SSE streaming)                     │
│            prompt template "translate-narrative-with-glossary"          │
│            input: §147 EN text + glossary[] + tone=narrative            │
│            output: streaming chunks IT      [first token <1s, total <5s]│
│  Frame 11: post-translate persist (async, no block)                     │
│            GamebookCampaignSession.translatedParagraphs[§147] = txt     │
│            LLM piggy-back: extract new terms → glossary delta           │
│            AgentMemory.MemoryNote[nanolith-glossary-{cid}].append       │
│            lastParagraphRead = §147                                     │
└─────────────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ FRONTEND                                                                │
│  Frame 12: viewer fullscreen rendering (streaming)                      │
│            text chunk-by-chunk, font 26px / lh 1.6 / max-width 60ch     │
│            glossary pills inline cliccabili                             │
│            aria-live="polite" per a11y                          [t=12s] │
│  Frame 13: optimistic state update                                      │
│            useCampaignSession(cid).lastParagraphRead = §147             │
│            rollback se backend conferma fallisce               [t=12s]  │
│  END: Aaron legge ad alta voce                                          │
└─────────────────────────────────────────────────────────────────────────┘

Total P95 budget user-perceived (frames 1→12 first token):
  - Foto upload+OCR+segmentation visibile (frame 7): ≤ 4.5s
  - User choice (frame 8): ~2-3s (user-controlled, not in budget)
  - Translate streaming first token visible (frame 10): ≤ 1s post-tap
  - Wall clock perceived first token: ~7-8s (target P95 < 8s rispettato)
  - Translate streaming completion (frame 12): ≤ 5s post-tap
```

### 4.3 Componenti coinvolti

**Frontend (page composition)**:
- Route NEW: `/library/games/[gameId]/play/paragraph/[num]/page.tsx` — server component sottile + client island viewer
- Hook NEW: `useTranslateParagraph(campaignId)` — cherry-pick da `aa72b1b70`, adatta single-shot
- Hook NEW: `useCampaignSession(campaignId)` — `useQuery` + `useMutation` save state
- Composizione primitive v2 esistenti: `CameraViewfinder`, `PageThumb`, `ConfidenceBadge`, `OfflineBanner`, `auth-card`, `btn`, `drawer` (Q&A panel)
- **Zero nuovi component v2 con HSL pattern** (FREEZE-compliant)

**Backend (.NET 9, MediatR)**:
- Endpoint NEW 1: `POST /api/v1/gamebook/{gameId}/photo-translate` → `UploadAndSegmentPhotoCommand` → `IUploadAndSegmentPhotoHandler`
- Endpoint NEW 2: `POST /api/v1/gamebook/{gameId}/photo-translate/{photoId}/translate` → `TranslateParagraphCommand` → `ITranslateParagraphHandler` (riusa chat-stream)
- Service NEW: `IParagraphSegmentationService` (Domain interface, Application impl con regex + fallback)
- Service riusato: `ISmoldoclingClient` (DocumentProcessing BC)
- Aggregato NEW: `GamebookCampaignSession` (in `SessionTracking` BC) con factory + `MarkParagraphRead()` method
- EF migration: nuove tabelle `gamebook_campaign_sessions` + `gamebook_photo_artifacts`
- Riuso `AgentMemory.MemoryNote` con tag `nanolith-glossary-{campaign_id}`

**Glossary feedback loop**:
- Round-trip: ogni translate → glossary extraction prompt piggy-back (1 LLM call combinato)
- Persistenza: `MemoryNote` per-campaign tagged
- Inline editing: tap pill → edit modal → PUT to MemoryNote (optimistic + rollback)

---

## 5. Persistence Model

### 5.1 Schema DB nuovo (1 EF migration)

**Tabella `gamebook_campaign_sessions`** — `SessionTracking` BC:

| Campo | Tipo | Note |
|---|---|---|
| `id` | uuid PK | aggregato root |
| `user_id` | uuid FK auth_users | owner |
| `shared_game_id` | uuid FK shared_games | gioco |
| `title` | varchar(120) | "Campagna 1", default auto |
| `party_json` | jsonb | `{members:[{name,class,hp,...}]}` free-form per-gioco |
| `last_paragraph_id` | varchar(20) | es. `"§289"` |
| `scoring_json` | jsonb | free-form per-gioco |
| `translated_paragraphs_jsonb` | jsonb | `{"§147":{text_it,translated_at,glossary_used:[...],confidence}}` |
| `created_at, updated_at, deleted_at` | timestamptz | audit + soft delete |
| `created_by, updated_by` | uuid | audit |
| `row_version` | bytea (timestamp) | optimistic concurrency |

Indici: `(user_id, deleted_at)`, `(shared_game_id, deleted_at)`, GIN su `translated_paragraphs_jsonb`.

**Tabella `gamebook_photo_artifacts`** — `DocumentProcessing` BC, transitoria:

| Campo | Tipo | Note |
|---|---|---|
| `id` | uuid PK | photoId |
| `campaign_session_id` | uuid FK | parent |
| `s3_key` | varchar | R2 storage key |
| `ocr_confidence_avg` | decimal(3,2) | da smoldocling |
| `detected_paragraphs_jsonb` | jsonb | output Frame 6 (cache segmentation per re-translate altri §) |
| `created_at` | timestamptz | |
| `expires_at` | timestamptz | now() + 24h, background job purge |

**Riuso esistente**: `MemoryNote` in `AgentMemory` BC con tag `nanolith-glossary-{campaign_id}`, content JSON `{term_en, term_it, first_seen_paragraph, user_edited: bool}`.

### 5.2 Cache layer multi-livello

```
Lvl 1: IndexedDB browser locale (offline-first re-read)
   - chiave: campaign_id:paragraph_id
   - valore: text_it cached + glossary snapshot
   - TTL: 24h
   - hit rate target: ≥ 80% per re-read sessione corrente
Lvl 2: Server-side translated_paragraphs_jsonb (DB)
   - persistenza cross-device + cross-day
   - hit rate target: 100% per paragrafi già tradotti, no re-LLM
Lvl 3: LLM provider (OpenRouter chain)
   - solo cache miss livelli 1+2
   - costo reale: ~3¢/paragrafo medio 200-400 parole
```

### 5.3 Idempotency

- Upload foto: `Idempotency-Key: ${campaignSessionId}:${ts}` + server dedup `(campaignSessionId, sha256(photoBytes))` last 5min → stesso `photoId`
- Translate paragraph: `Idempotency-Key: ${photoId}:${paragraphNumber}` → cached response entro 24h
- Save state: optimistic locking via `row_version`, retry max 3 con exponential backoff

### 5.4 Recovery strategies

| Scenario | Strategia |
|---|---|
| Crash mid-translate | LLM stream interrotto → frontend mostra parziale + auto-retry; backend non persiste finché stream completa |
| Crash mid-save state | Transaction atomica: `MarkParagraphRead` aggiorna `last_paragraph_id` + `translated_paragraphs_jsonb` insieme. Rollback su write fail |
| Browser cache cleared | DB Lvl 2 ricostruisce IndexedDB Lvl 1 al primo resume |
| User cambia device | Last-write-wins su `last_paragraph_id` con warning UI se conflict (`updated_at` delta < 5min) |
| Sessione 90+ giorni stale | Resume mostra warning, NO auto-purge. Soft-delete solo via user action explicit "ricomincia" |

---

## 6. Failure Modes & Graceful Degradation

### 6.1 Failure modes (18 finali consolidati)

| # | Failure | Frame | Detection | User-visible behavior | Fallback |
|---|---|---|---|---|---|
| 1 | Camera permission denied | 2 | browser API | Inline swap → file picker, ≤ 500ms | gallery upload |
| 2 | Photo > 10MB | 4 | backend validate | Toast "Foto troppo grande, ricomprimi (max 10MB)" | retry compresso lato client |
| 3 | OCR confidence < 0.5 | 5 | smoldocling output | Banner amber "Foto poco leggibile (35%)" + 3 azioni: [📸 Rifoto, 🔢 Numero manuale, ⏭️ Procedi] | manual fallback |
| 4 | Paragraph segmentation no match | 6 | regex empty | "Non vedo paragrafi numerati. Inserisci §:" + input | manual input |
| 5 | LLM 429 / rate limit | 10 | provider error | Toast "Provider lento" + countdown retry | OpenRouter fallback chain (existing) |
| 6 | LLM stream interrupted | 10-12 | SSE close pre-complete | Mostra parziale + "Sto riprovando…" + CTA "Mostra EN" | full retry stesso prompt |
| 7 | WiFi drop / S3 upload fail | 3-4 | XHR error | Exponential backoff `[1s,2s,4s,8s,16s]=31s` + cancel CTA | IndexedDB offline queue, manual re-upload |
| 8 | DB write `MarkParagraphRead` fail | 11 | EF exception | Optimistic UI + retry async background + log error | localStorage fallback per quel § (re-sync su next success) |
| 9 | Resume conflict (device 2 attivo) | N4.1 | `updated_at` conflict | Warning "Altra sessione aperta su altro device — ultima modifica X min fa" | manual user choice |
| 10 | Glossary inconsistency (LLM ignora glossario) | 10 | post-translate diff check | Pill cliccabile term-edit + auto-highlight | inline edit + apply MemoryNote |
| 11 | Browser crash / tab close mid-stream | 12 | beforeunload | Save partial → IndexedDB + recovery prompt al next open | recovery UI |
| 12 | Script non-Latin (CJK) | 5 | smoldocling lang detect | "Script non supportato (giapponese)" + retry CTA | nessuno automatico |
| 13 | Concurrency conflict optimistic locking | 8/11 | `DbUpdateConcurrencyException` | Refetch + auto-merge se possibile, prompt user se ambiguo | optimistic retry max 3 |
| 14 | Privacy/EXIF (location GPS, volti background) | upload pre-frame 3 | client EXIF parse | strip EXIF location prima upload, NO display volti, NO policy block | client-side EXIF stripper |
| 15 | OpenRouter chain totalmente esausta | 10 | catch-all fallback | banner red persistente "Tutti i provider AI non rispondono. Riprova tra qualche minuto." | offline use traduzioni cached |
| 16 | Foto orientation EXIF (landscape vs portrait) | 5 | OCR text incoerente | auto-rotate via `image-orientation: from-image` + EXIF normalization backend pre-OCR | re-photo CTA |
| 17 | Long paragraph multi-pagina (§147 occupa 4 pag) | 6 | fragment ending mid-sentence | Banner "Sembra che §147 continui sulla pagina dopo. Fotografa la pagina successiva?" + concat workflow | concatenate translations |
| 18 | R2 storage quota exhausted | 4 | `507 Insufficient Storage` | Toast "Storage temporaneo pieno, contatta admin" | local FS fallback emergency |

### 6.2 Strategie trasversali

- **Visible failures only**: nessun silent error, ogni fallimento → toast/banner + actionable suggestion
- **Fallback chain LLM**: OpenRouter → Anthropic Claude Haiku → DeepSeek (riuso esistente)
- **Photo retention 24h**: background job purge per copyright safety + cost control
- **Offline-first re-read**: IndexedDB Lvl 1 → re-leggere paragrafi già tradotti senza WiFi
- **Hallucination detection**: confidence < 0.7 → forced disclaimer "non sono certo, controlla pag. X"
- **Privacy minimization**: EXIF location stripped pre-upload, no face detection persistence, no permanent photo storage

### 6.3 Cost cap (out of scope MVP demo)

Superadmin bypass quota durante dogfooding. Cost indicator visibile ma non blocca. Integrazione paywall paywall enforcement → quando si esporrà l'app a Sara/beta tester (out of scope Iter 1).

---

## 7. Testing Strategy

### 7.1 Unit (target 90%+ backend, 85%+ frontend)

**Backend (xUnit + FluentAssertions)**:
- `ParagraphSegmentationServiceTests` — regex multi-paragrafo, fallback no-match, edge cases (`§ 147` con spazio, `§147a` lettera, multi-page)
- `GamebookCampaignSessionTests` — factory, `MarkParagraphRead()`, soft-delete, concurrency `RowVersion`
- `UploadAndSegmentPhotoHandlerTests` — happy + 4 validation errors + OCR fail + S3 fail
- `TranslateParagraphHandlerTests` — happy + glossary inline + LLM error + idempotency cache hit
- `GlossaryExtractionPromptTests` — JSON parsing robustness, dedup case-insensitive, `user_edited` preservation

**Frontend (Vitest + RTL)**:
- `useTranslateParagraph.test.ts` — 22 tests cherry-pick + 5 nuovi single-shot pattern
- `useCampaignSession.test.ts` — query/mutation/optimistic/rollback (~15 tests)
- Component tests page composition (renders, primitives wired, glossary pills clickable, error states)

### 7.2 Integration (Testcontainers Postgres)

- `GamebookCampaignSessionRepositoryTests` — migration + CRUD + soft-delete query filter
- `UploadAndSegmentPhotoIntegrationTests` — full pipeline upload → S3 (testcontainer minio) → smoldocling (mock) → segmentation → DB write
- `MemoryNoteGlossaryIntegrationTests` — tag-based query, dedup, multi-campaign isolation

### 7.3 E2E (Playwright)

- `N3.1-photo-translate-happy.spec.ts` — camera mock + photo file fixture + backend mock + assert IT text visible at font 26px + glossary pills present
- `N3.2-low-confidence-fallback.spec.ts` — mock OCR < 0.5 → assert banner + 3 actions
- `N3.4-offline-retry.spec.ts` — block network mid-upload, assert backoff sequence + cancel CTA
- `N4.1-resume-cross-day.spec.ts` — seed DB state, reload page, assert resume card + state restored
- `N4.4-multi-campaign.spec.ts` — seed 2 campaigns, assert listing + isolation
- `latency-budget.spec.ts` — assertion P95 < 8s end-to-end con backend mock latency-injected

### 7.4 Coverage gates

- Backend ≥ 90% line + branch su nuovi service/handler/aggregato
- Frontend ≥ 85% statements
- Latency budget P95 < 8s asserito su CI con mock fissi
- Hallucination rate ≤ 3% verificato post-sessione **manualmente** (review esplicita Aaron su 20 sample) — non automabile

---

## 8. Pre-condizioni & Seed Dataset

### 8.1 Pre-condizioni dogfooding (sessione 1 demo reale Aaron)

Da verificare/setup **prima** di Iter 1.A merge:

| # | Pre-condizione | Owner | Verificabile via |
|---|---|---|---|
| 1 | Account `badsworm@gmail.com` con role `SuperAdmin` | Aaron | `SELECT role FROM auth_users WHERE email='badsworm@gmail.com'` |
| 2 | Nanolith in `shared_games` catalog | Aaron | `SELECT id FROM shared_games WHERE LOWER(name)='nanolith'` |
| 3 | `Nanolith Press Start ENG.pdf` indicizzato (KB tutorial) | Aaron | `pdf_documents` row con `embedding_status='complete'` + chunks indexed |
| 4 | `Nanolith Rules ENG.pdf` indicizzato (KB regole) | Aaron | idem |
| 5 | `AgentDefinition` "Nanolith Tutor" linked a entrambi i KB + `IsActive=true` | Aaron | `agent_definitions.kb_links` array, `is_active=true` |
| 6 | LLM provider chain configurato (OpenRouter primary) | Aaron (già setup) | `make secrets-sync` |
| 7 | R2 storage configurato (foto temp 24h) | Aaron | factory `STORAGE_PROVIDER=s3` |
| 8 | Background job purge foto 24h schedulato | Iter 1.A task | `Api.HostedServices.PhotoArtifactPurgeJob` |

### 8.2 Materiali fisici Aaron (verifica logistica)

- Storybook fisico Nanolith — al tavolo con gli amici
- Encounter Book fisico Nanolith — non usato Iter 1
- Telefono carico, WiFi domestico stabile, illuminazione ambient adeguata per OCR

### 8.3 Seed dataset E2E CI (NON per demo reale, solo Playwright)

Per E2E test riproducibili senza toccare account reali:

```typescript
// tests/e2e/fixtures/nanolith-e2e-seed.ts
export const NANOLITH_E2E_SEED = {
  user: 'nanolith-e2e@meeple.test',
  game: { id: 'e2e-nanolith-uuid', name: 'Nanolith E2E' },
  knowledgeBases: [
    { type: 'press-start', source: 'fixtures/press-start-trim.pdf' },  // 5 pag CI speed
    { type: 'rules', source: 'fixtures/rules-trim.pdf' }  // 10 pag CI
  ],
  agent: { id: 'e2e-nanolith-tutor', kbs: ['press-start', 'rules'] },
  campaign: {
    id: 'e2e-campaign-1',
    party: [{ name: 'TestHero', class: 'fixture', hp: 10 }],
    lastParagraphId: '§145',
    glossary: [{ term_en: 'Voidstone', term_it: 'Pietra del Vuoto' }]
  },
  fixturePhoto: 'fixtures/storybook-page-fixture.jpg'
};
```

I tag `@dogfood` nei scenari Gherkin (Sezione 3) si riferiscono al setup reale di Aaron (#1-#8). I scenari `@happy` non taggati `@dogfood` usano il seed E2E.

---

## 9. Out of Scope

### 9.1 Out of Scope Iter 1 (deferred a Iter 2 o vision MVP-1/v2/v3)

| Feature | Motivazione | Successivo iter |
|---|---|---|
| Encounter Book UX cheatsheet card popup | Aspettare prima sessione reale per capire consumo encounter | Iter 2 (post-feedback) |
| G1 vision (acquisizione manuale via foto Rules) | KB già pre-indicizzato | vision Phase 1.5 |
| Pre-translate chapter background | Aaron preferisce on-demand controllo costi | vision MVP-1 |
| Setup wizard interattivo a checklist | Riusiamo Q&A chat panel su Press Start (zero work) | vision MVP-1 |
| Multi-device collaborativo full + QR | Aaron al tavolo single-device | vision v3 |
| Voice input / TTS narrator | Aaron legge ad alta voce manualmente | vision v2 |
| AI Narrator audio | idem | vision v2 |
| User-controlled LLM provider selection | Superadmin usa default chain | vision v2 |
| Indicizzazione persistente foto storybook al KB | Storybook NON va nel KB (privacy + copyright + ephemeralità) | mai (decisione architetturale) |
| Cost cap & paywall enforcement | Superadmin bypass | quando si espone a Sara/beta |
| Real-time hallucination detection automatico | Aaron review manualmente post-sessione | vision v2 (instrumentation) |
| Glossary export/import cross-game | Glossario per-campagna isolato | nice-to-have iter 3+ |
| Storybook table of contents auto-extracted | Aaron scrolla manualmente | nice-to-have iter 3+ |
| PDF cartaceo viewer integrato in app | Libro fisico al tavolo | mai |

### 9.2 Out of Scope assoluto MVP demo

- Marketing polish (animations elaborate, micro-interactions ricche, branded onboarding)
- Telemetria heavy per dashboard analytics — bastano `console.log` + 1-2 evento `cost_per_session`
- Setup demo deterministico per fiere/showcase — vedi seed E2E §8.3
- Multi-locale UI (tutto IT, no i18n switch)
- Accessibility audit completo WCAG AAA — manteniamo AA livello base
- Performance benchmark suite — manteniamo solo P95 latency budget assertion in CI

---

## 10. Decisions Log

Decisioni prese durante la sessione di brainstorming socratico (2026-05-07):

| # | Decisione | Razionale |
|---|---|---|
| D1 | "Demo" = dogfooding personale di Aaron (non showcase / non test interno) | Risposta utente: "(a)". Quality bar = "non rovinare la sera con gli amici". Failure mode tollerabile = visibile, mai silenzioso |
| D2 | 4 documenti fisici Nanolith, di cui 2 PDF (Rules + Press Start) indicizzati. Storybook + Encounter Book sono cartacei → richiedono flusso "foto live → translate" non in vision originale | Risposta utente: "(b)". Conferma G5 "translate-on-the-fly" come nuovo user goal (qui N3) |
| D3 | Modalità campagna mista: Storybook lungo (mesi), Encounter one-shot self-contained | Risposta utente: "(d)". Save/resume cross-day OBBLIGATORIO (ribalta vision §1.5 deferral MVP-1 → MVP) |
| D4 | Deadline soft "quando funziona bene". Scope-cap: ~2 settimane di lavoro = Solid (B), né bones (A) né polished (C) | Risposta utente: "(d)". Bones rischio dogfooding fail; polished overdesign senza esperienza reale |
| D5 | UX granularità foto: pagina intera A4 → segmentation multi-paragrafo. UX dipende dal contesto (Storybook viewer fullscreen, Encounter cheatsheet card). Encounter rinviato Iter 2 | Risposta utente: "(d)". Storybook è il flusso primario Iter 1, Encounter posteriormente |
| D6 | Approccio architetturale = (2) Estensione `v2/gamebook` esistente, NO standalone NO localStorage-only | Risposta utente: "si va bene". Riuso 70% asset esistenti, future-proof verso vision roadmap |
| D7 | FREEZE v2 (#807, #808): N3 viewer = page composition di primitive v2 esistenti, zero nuovi component v2 con HSL pattern. Mockup SP6-D + SP6-C già FREEZE-compliant (usano solo token semantic vars) | Risposta utente: "(a) ho accesso a claude design se serve, non voglio avere codice legacy". Verificato grep su mockup |
| D8 | 4 user goal SMART: N1 (Q&A setup Press Start, riuso 100%), N2 (Q&A Rules in-game, riuso 100%), N3 (translate Storybook foto, Iter 1.A core), N4 (resume + glossario, Iter 1.B) | Risposta utente: "si" alle metriche e scope. Conferma 4 goal ben separati |
| D9 | Two-phase API (segment + translate) invece che single-shot auto-pick | Aaron sceglie il paragrafo (a volte deve leggere il "prossimo dopo §146"). Single-shot rischia § sbagliato |
| D10 | Streaming SSE + cache full text al complete | UX migliore (Aaron inizia a leggere primi caratteri); cache solo su completion per evitare cache pollution |
| D11 | Photo storage S3 (R2) con retention 24h forzata + EXIF location stripping client-side | Privacy minimization, copyright safety storybook, cost control |
| D12 | EF migration con 2 nuove tabelle (`gamebook_campaign_sessions` + `gamebook_photo_artifacts`) | Risposta utente: "s5 nuove tabelle". Production-grade, no localStorage-only |
| D13 | Failure modes review: 18 finali (15 originali - 3 fusi/rimossi + 6 aggiunti) | Risposta utente: "s6 review". Aggiunti privacy/EXIF, OpenRouter chain exhausted, orientation, long paragraph, glossary race, R2 quota |
| D14 | Tag `@dogfood` su scenari Gherkin per setup reale Aaron + Nanolith reali. Seed E2E solo per CI | Risposta utente: "s7 reali". Revert su decisione precedente fixture-only |
| D15 | Aaron email = `badsworm@gmail.com` (non `@libero.it` che è email Claude/sistema) | Risposta utente: "@gmail.com, badsworm@gmail.com" |

---

## 11. Open Questions Carry-forward

Domande da risolvere **solo dopo l'esperienza reale** della prima sessione dogfooding:

1. Encounter Book UX: card popup vs viewer fullscreen vs sidebar slide-out?
2. Glossario per-game vs cross-game (universal terms)?
3. Glossary auto-edit detection (LLM ignora glossario → flag automatico)?
4. Storybook reading "next page" auto-suggest dopo lettura (predizione §148 dopo §147)?
5. Foto multi-pagina batch (snap 5 foto in fila → process in background → leggi nel frattempo)?
6. Audio TTS read-aloud (Aaron mani occupate con dadi/segnalini)?

Da prioritizzare DOPO feedback dogfooding sessione 1.

---

## 12. Riferimenti

- [Vision Document 2026-05-04](2026-05-04-libro-game-assistant-vision.md) — vision a 12 mesi, persona "Sara casual designer", 4 user goal G1-G4
- [SP6 Migration Plan 2026-05-06](../../for-developers/plans/2026-05-06-sp6-libro-game-migration.md) — plan tecnico Phase A/B/C (cherry-pick demo + index + upload wizard)
- Mockup SP6-D translation-viewer: `admin-mockups/design_files/sp6-libro-game-translation-viewer.html` (untracked)
- Mockup SP6-C play-session: `admin-mockups/design_files/sp6-libro-game-play-session.html` (untracked)
- Worktree commit demo: `aa72b1b70` (`useTranslateParagraph` + `TranslateParagraphDemo` + 43 tests)
- Datasource Nanolith: `data/rulebook/nanolith_datasource/` (Rules ENG.pdf + Press Start ENG.pdf)
- MEMORY freeze policy: `project_sp6_v2_freeze.md` (issue #807, #808)
