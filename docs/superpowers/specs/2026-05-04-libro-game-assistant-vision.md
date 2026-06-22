---
title: Libro Game AI Assistant — Vision Document
status: 🟢 ACTIVE reference (vision document, no expiry)
type: vision-doc
date: 2026-05-04
authors: [DegrassiAaron]
related-issues: []
review-cycle: spec-panel + brainstorming + ultrathink-review
---

# 🎲 Libro Game AI Assistant — Vision Document

> ## ⚠️ DOCUMENT TYPE: VISION, NOT MVP SPEC
>
> Questo documento descrive la **vision a 12 mesi** del prodotto AI assistant per gamebook/libro game su MeepleAI. Include scope MVP + roadmap Phase 1.5/v2/v3.
>
> **Per implementare**, consultare [§6 MVP Phase 1 Scope](#6-mvp-phase-1--implementation-scope) che è il subset effettivamente buildabile in 4-5 mesi (3 dev fullstack + 1 ML + 1 designer).
>
> **Origine**: spec-panel review + brainstorming session + ultrathink critical review (sessione del 2026-05-04 con Aaron).

---

## Indice

- [Executive Summary](#executive-summary)
- [1. Context & Persona](#1-context--persona)
- [2. User Goals SMART](#2-user-goals-smart)
- [3. Gherkin Scenarios](#3-gherkin-scenarios)
- [4. Non-Functional Requirements](#4-non-functional-requirements)
- [5. Open Questions, Roadmap, Risk Register](#5-open-questions-roadmap-risk-register)
- [6. MVP Phase 1 — Implementation Scope](#6-mvp-phase-1--implementation-scope)
- [7. Decisions Log](#7-decisions-log)

---

## Executive Summary

**Pain risolto**: gruppi casual italofoni che giocano gamebook narrativi (Tainted Grail, ISS Vanguard, Stuffed Fables, etc.) in lingua estera (tipicamente EN) interrompono il flow per tradurre live, cercare regole, gestire setup. La partita "muore alla seconda sessione".

**Soluzione**: companion app smartphone che (a) indicizza il manuale fotografato, (b) traduce on-demand i paragrafi narrativi in italiano da leggere ad alta voce, (c) risponde a domande sulle regole con citazione pagina sorgente, (d) guida lo step-by-step del setup iniziale.

**Differentiation strategica**:
- *Casual italian gaming community* (first mover sul segmento)
- *Provider-agnostic LLM platform* (OpenRouter, multi-model)
- *Trasparenza vs black-box competitor* (Dized, BoardGameArena)

**Status MVP**: Phase 1 scope (4-5 mesi realistic, 3 dev fullstack + 1 ML + 1 designer) definito in §6. Vision completa (12 mesi roadmap) descritta in §1-5.

---

## 1. Context & Persona

### 1.1 Caso d'uso primario

Un gruppo di amici (3-6 persone) si riunisce in salotto per giocare un **libro game/gamebook narrativo** (esempi: *Tainted Grail*, *7th Continent*, *ISS Vanguard*, *Andor Chronicles*, *Stuffed Fables*). Il gioco si articola come storia ramificata: si legge un paragrafo, i giocatori prendono decisioni, si va al paragrafo successivo.

**Asset disponibili al tavolo**:
- Manuale fisico (cartaceo) **in lingua diversa dall'italiano** — tipicamente EN, occasionalmente DE/FR/ES.
- Smartphone con camera (un solo device "principale" — il GM).
- Eventuali smartphone aggiuntivi degli altri giocatori (read-only, opt-in v2+).
- Connessione WiFi domestica (instabile è la norma).

**Asset NON disponibili**:
- PDF del manuale.
- Account/profili dei giocatori (sessione "anonima al tavolo").
- Dadi, segnalini, fogli personaggio: gestiti fisicamente.

### 1.2 Primary persona

> **"Sara, 32 anni, designer freelance, casual boardgamer"**
>
> Compra 3-4 board game all'anno, gioca 1-2 volte al mese con amici e partner. Capisce concetti base ma non vuole "fare il GM". Quando il gioco è in inglese e il gruppo non vuole attendere che lei traduca live ogni paragrafo, la partita rischia di morire alla seconda sessione.
>
> *Job-to-be-done emozionale*: «Voglio passare la serata divertendomi con i miei amici, non a fare la traduttrice/lettrice del manuale».

**Persona secondaria** (player al tavolo, non utente diretto): Marco, Giulia, Luca — ascoltatori passivi della traduzione letta da Sara.

**Persona esplicitamente NON-target MVP**: hardcore boardgamer (BGG power-user), famiglie con bambini sotto i 10 anni, developer enterprise.

> ⚠️ **Pain Sara — implicazione MVP**: «la partita rischia di morire alla seconda sessione» implica che save/resume cross-day è critico per risolvere il pain dichiarato. **Phase 1 MVP non include save/resume**, MVP-1 sì (vedi §6.2 trigger esplicito).

### 1.3 Vincoli & decisioni architetturali

| Vincolo | Decisione | Razionale |
|---------|-----------|-----------|
| Lingua manuale | LLM single-pass narrative translation (any source language → IT) | Quality bar adeguata a casual senza overhead two-pass |
| Topologia device | Single-device GM master + multi-device read-only opt-in via QR (v2+) | Casual reality: 1 telefono in mano al GM |
| Modalità traduzione | On-demand per paragrafo + override "pre-traduci capitolo" user-controlled | Costo controllato, copyright meno esposto |
| State persistence | Session in-memory + cache locale 24h. Save/resume cross-day deferred MVP-1 | Scope reduction MVP |
| Pricing | Freemium (50 pag/mese gratis) + consumable credits per power-user | Casual prova gratis |
| Engine indicizzazione | Riusa MeepleAI esistente: smoldocling-service + embedding-service + RAG | Zero nuova infrastruttura |
| LLM provider | **OpenRouter** abstraction layer + Anthropic/OpenAI/DeepSeek tiers | Anti-vendor lock-in, cost optimization |
| Server baseline | **Hetzner CAX31** (16 GB RAM, €14/mese) day 1, NON CAX21 borderline | Production safety, no OOM-killer risk (review issue #4) |

### 1.4 Mapping su Bounded Context MeepleAI esistenti

| Capability | BC riusato | Estensione richiesta |
|-----------|-----------|---------------------|
| Foto manuale → testo strutturato | **DocumentProcessing** | Pipeline photo-first (multi-shot, dewarping, page-stitching) |
| Q&A regole | **KnowledgeBase** (RAG, AI agents) | ✅ Riusabile diretto + filtro `source=phone_camera` |
| Setup guidato step-by-step | **GameToolbox** (phases templates) | Estensione: derivazione phases da manuale fotografato |
| Traduzione narrativa | ❌ **Nuovo cross-cutting** | Nuovo `TranslationService` come capability orizzontale (non BC) |
| House rules / glossario per-gioco | **AgentMemory** | ✅ Riusabile diretto |
| Multi-device read-only (v2+) | **SessionTracking** | Estensione minore: QR code generation + ephemeral session token |

**Nessun nuovo Bounded Context**. Il caso d'uso libro game è una *configurazione/preset* sopra l'infrastruttura esistente.

### 1.5 Out of scope MVP (Phase 1)

- ❌ Save/resume cross-day → MVP-1 (vedi §6.2 trigger)
- ❌ State machine narrativa attiva (TTS, voice input, automazione meccaniche)
- ❌ Multi-device collaborativo full → v3
- ❌ Modalità famiglia / parental control
- ❌ Editing post-OCR del testo manuale
- ❌ Supporto offline completo
- ❌ User-controlled LLM provider selection (preset, BYOK, transparency UI) → v2 (vedi §4.10)
- ❌ AI Narrator audio → v2 (vedi §4.9)
- ❌ Pre-translate chapter background → MVP-1
- ❌ Setup wizard avanzato → MVP-1 (basta paragrafo setup tradotto in MVP)

---

## 2. User Goals SMART

Quattro user goal coprono le 3 capability core + la pre-condizione tecnica obbligatoria.

### 🎯 G1 — Acquisire manuale fotografandolo

> Sara apre l'app, fotografa le pagine del manuale fisico, e dopo pochi minuti il manuale è interrogabile.

| Criterio SMART | Valore |
|---------------|--------|
| **S — Specific** | Convertire foto multiple di pagine manuale in knowledge base interrogabile (testo OCR + embedding vettoriale + metadata pagina/paragrafo) |
| **M — Measurable** | (a) ≥ 95% delle pagine fotografate sono indicizzate con confidence ≥ 0.7; (b) UI mostra preview confidence per pagina con bandiera per pagine sotto-soglia; (c) **accuracy reale validata su test set golden 5 manuali reali** (review issue #3 — non solo confidence) |
| **A — Achievable** | Riuso pipeline DocumentProcessing esistente (smoldocling + unstructured) con preprocessor "phone-camera" (dewarping + page detection) + parallel batch processing |
| **R — Relevant** | Pre-condizione di tutto il resto |
| **T — Time-bound** | (a) Throughput ≥ 10 pag/min con parallel batch (review issue: serial 8 sec/pag = 6.6 min, OVER budget); (b) Manuale completo (50 pagine) indicizzato in < 5 min totali; (c) UI permette di iniziare a giocare con G2/G3 anche su manuale parzialmente indicizzato |

**Definition of Done**: Sara ha caricato 50 foto del manuale di *Tainted Grail* (in EN). Dopo ≤ 5 min, può chiedere "come funziona il combat?" e ricevere una risposta citante una pagina specifica con confidence ≥ 0.85.

### 🎯 G2 — Ricevere guida step-by-step per setup iniziale

> Sara dice "stiamo per giocare in 4, prepara il tavolo" e ottiene una checklist di passaggi specifici.

| Criterio SMART | Valore |
|---------------|--------|
| **S — Specific** | Generare guida ordinata di N step parametrizzata per numero di giocatori, derivata dalle pagine "Setup" del manuale indicizzato |
| **M — Measurable** | (a) Ogni step ha titolo + componenti necessari + descrizione + checkbox "fatto"; (b) Guida cita ≥ 1 pagina sorgente per ogni step **con citation accuracy ≥ 90% validata** (review issue #2: groundedness hallucination è reale, non solo Q&A); (c) Sara può marcare step come "fatto" |
| **A — Achievable** | RAG query su KB con filtro tematico "setup" + LLM structuring in formato checklist + traduzione automatica IT |
| **R — Relevant** | Setup è il momento più frustrante per casual |
| **T — Time-bound** | Guida generata in ≤ 30 sec dalla richiesta |

**Definition of Done**: Sara apre il gioco mai giocato prima, dice "siamo in 4", ottiene una guida di 6-10 step in italiano con componenti elencati. Marca ogni step "fatto" via tap.

> **Phase 1 MVP scope** (vedi §6): G2 ridotto a "mostra il paragrafo Setup tradotto con citazione". Setup wizard interattivo a checklist deferred MVP-1.

### 🎯 G3 — Risolvere dubbi sulle regole durante il gioco

> A metà partita, Marco chiede "quanti dadi tira il mago?". Sara digita la domanda, ottiene risposta italiana con citazione.

| Criterio SMART | Valore |
|---------------|--------|
| **S — Specific** | Q&A in linguaggio naturale (testo) sul manuale indicizzato, con risposta in italiano + citazione pagina sorgente + preview foto pagina |
| **M — Measurable** | (a) **P95 latenza query → risposta < 5 sec** (rivisto da 3 sec — review issue: Claude Haiku tipica P95 ~4-5 sec end-to-end); (b) confidence score visibile (alta/media/bassa); (c) per confidence bassa: messaggio esplicito "non sono sicuro" + offer "rifotografa pagina / house rule"; (d) **hallucination rate ≤ 3% su test set golden, 0% target su confidence > 0.85** (review issue #8: target definito, non aperto) |
| **A — Achievable** | Riuso KnowledgeBase BC esistente (RAG + reranker + LLM answer generation) + filtro `language=it` per risposta tradotta |
| **R — Relevant** | Pain point #1 dichiarato |
| **T — Time-bound** | P50 < 3 sec, P95 < 5 sec, P99 < 8 sec (timeout con fallback). Risposta deve essere "leggibile in 1 frase" |

**Definition of Done**: Sara fa 20 domande sparse durante una sessione di 4 ore. ≥ 17/20 risposte sono giudicate "utili" da Sara. ≥ 18/20 risposte arrivano in < 5 sec. Le 2-3 "non lo so" hanno azionable suggestion.

### 🎯 G4 — Leggere ad alta voce paragrafi narrativi tradotti

> Sara dice "ora paragrafo §147". L'app traduce e mostra il testo italiano. Sara legge ad alta voce.

| Criterio SMART | Valore |
|---------------|--------|
| **S — Specific** | Identificare paragrafo specifico nel manuale (per numero §, capitolo, o snippet testo), tradurre in italiano preservando tono narrativo, mostrare in formato leggibile a voce alta |
| **M — Measurable** | (a) Latenza traduzione on-demand < 5 sec per paragrafo medio (200-400 parole); (b) modalità "pre-traduci capitolo" disponibile (deferred MVP-1); (c) cache traduzione per la sessione (free), persist 24h locale; (d) UI mostra testo con font ≥ 18pt; (e) **fallback per manuali NON paragrafo-numerati**: ricerca per snippet testo (review issue: Andor è chapter-based, non tutti i gamebook hanno § numbering) |
| **A — Achievable** | LLM single-pass con prompt: source language + tono dedotto + glossario per-gioco + costraint "preserva paragrafazione e dialoghi" |
| **R — Relevant** | Pain point #1 acuto. Senza questo, casual non può giocare gamebook in lingua estera. |
| **T — Time-bound** | (a) On-demand: P95 < 5 sec; (b) tap "prossimo §" → testo visibile in < 1 sec se cached, < 5 sec se non cached |

**Definition of Done**: Sara gioca 1 sessione di 4 ore traducendo 30 paragrafi narrativi. ≥ 28/30 traduzioni sono giudicate "lette senza esitare". Costo per Sara: ≤ 30 credits (~€1.50) o entro la free tier mensile.

### 📊 Goal coverage matrix

| Goal | Capability MeepleAI BC | Pricing tier | Latenza target |
|------|------------------------|--------------|----------------|
| G1 — Acquisire manuale | DocumentProcessing (estensione photo-first) | Free | 5 min/manuale 50 pag |
| G2 — Setup guidato | GameToolbox + KnowledgeBase | Free | 30 sec generation |
| G3 — Q&A regole | KnowledgeBase (riuso diretto) | Free | < 5 sec P95 |
| G4 — Traduzione narrativa | Nuovo TranslationService cross-cutting | Freemium 50pag/mese + credits | < 5 sec on-demand |

---

## 3. Gherkin Scenarios

Per ogni user goal, scenari nel formato Adzic specification-by-example.

Convenzione tag: `@happy` percorso principale, `@edge` casi limite plausibili, `@error` failure mode.

### G1 — Acquisire manuale

#### G1.1 — Upload manuale completo @happy
```gherkin
Given Sara è loggata in MeepleAI con un account free
And ha aperto la tab "Nuovo libro game"
And ha selezionato "Tainted Grail" dal catalogo SharedGameCatalog
When fotografa 50 pagine del manuale (lingua sorgente: EN)
And tappa "Inizia indicizzazione"
Then l'app mostra una progress bar con counter "X/50 pagine indicizzate"
And ogni pagina mostra preview + confidence badge entro 8 sec dall'upload
And il manuale completo è interrogabile entro 5 minuti totali (parallel batch processing)
And Sara può iniziare ad usare G2/G3/G4 anche su manuale parzialmente indicizzato
```

#### G1.2 — Foto sfocata o angolata @edge
```gherkin
Given Sara sta fotografando il manuale in salotto con luce bassa
When carica una foto con confidence OCR < 0.5
Then l'app marca quella pagina con badge giallo "qualità bassa"
And mostra un'azione "📸 Rifotografa" sulla card della pagina
And nei risultati Q&A successivi quella pagina è retrocessa nel ranking
And se Sara chiede una regola che dipende da quella pagina, l'app risponde "Non sono sicuro — la pagina X è poco leggibile, vuoi rifotografarla?"
```

#### G1.3 — Pagine duplicate o mancanti @edge
```gherkin
Given Sara ha caricato 50 foto ma 3 sono duplicate della stessa pagina
And ha saltato per errore le pagine 12-13
When l'indicizzazione completa
Then l'app rileva duplicati via similarity score > 0.95 e mostra "3 pagine sembrano duplicate, vuoi mantenerle tutte?"
And l'app rileva gap nella numerazione e mostra "Sembrano mancare le pagine 12-13, vuoi aggiungerle?"
And Sara può ignorare entrambi i warning e procedere
```

#### G1.4 — Connessione interrotta durante upload @error
```gherkin
Given Sara ha caricato 30 delle 50 foto
And il WiFi domestico cade per 2 minuti
When la connessione torna
Then l'app riprende automaticamente l'upload delle restanti 20 foto
And nessuna foto già uploaded viene ri-uploadata
And Sara non perde alcun progresso
```

#### G1.5 — Manuale di gioco non in SharedGameCatalog @edge (NEW from review)
```gherkin
Given Sara fotografa un manuale di un gioco non presente nel SharedGameCatalog
When tappa "Inizia indicizzazione"
Then l'app chiede "Non riconosco questo gioco. Vuoi:"
   | Azione                          | Effetto                                                  |
   | 🆕 Crea nuovo gioco             | Nome + editore + anno → aggiunto al catalogo             |
   | 🔍 Cerca su BoardGameGeek       | API call BGG per autodetect e prefill                    |
   | 📖 Indicizza solo per me        | Manuale privato, non condiviso al catalogo               |
And l'indicizzazione procede dopo conferma
```

#### G1.6 — Manuale in lingua non-Latin script @error (NEW from review)
```gherkin
Given Sara fotografa un manuale di un gamebook giapponese (CJK script)
When carica le foto
Then l'app rileva script non supportato dall'OCR pipeline corrente
And risponde "Il manuale è in giapponese — al momento supportiamo solo Latin script (EN/IT/DE/FR/ES/PT/NL)."
And offre alternative: [📅 Notifica quando supportato, 🔗 Suggerisci feature, ⏭️ Procedi solo con immagini (no OCR)]
```

### G2 — Setup guidato

#### G2.1 — Setup primo capitolo per gruppo di 4 @happy
```gherkin
Given Sara ha indicizzato il manuale di "Tainted Grail"
When apre la tab "Setup partita"
And imposta "numero giocatori = 4"
And tappa "Genera guida setup"
Then in ≤ 30 sec l'app mostra una checklist di 8 step in italiano:
   | # | Step                                                   | Componenti               |
   | 1 | Posiziona il tabellone Avalon al centro                | Tabellone                |
   | 2 | Distribuisci 1 segnalino salute per giocatore          | Segnalini salute (4)     |
   | 3 | Ogni giocatore sceglie un personaggio                  | Card personaggio (8)     |
   | 4 | Ogni giocatore prende 5 carte azione                   | Mazzo azioni             |
   | 5 | Posiziona il segnalino narrativo sul Menhir 1          | Segnalino + tabellone    |
   | 6 | Mescola il mazzo Esplorazione                          | Mazzo Esplorazione       |
   | 7 | Posiziona 3 carte Evento scoperte                      | Mazzo Eventi             |
   | 8 | Leggi il paragrafo §1 per iniziare                     | Manuale                  |
And ogni step ha checkbox "fatto" + link "📖 fonte: pag. 4" con accuracy citation ≥ 90%
And quando Sara marca tutti gli 8 step come fatti, l'app suggerisce "Pronti? Tap qui per leggere §1"
```

#### G2.2 — Setup variabile per numero di giocatori @edge
```gherkin
Given il manuale di "Stuffed Fables" specifica setup diverso per 2/3/4 player
When Sara imposta "numero giocatori = 3"
Then la guida mostra solo le componenti per 3 player
And ogni step che varia per player count menziona esplicitamente la variante usata
And se il numero giocatori è fuori range supportato, l'app avverte "Il manuale supporta 2-4 giocatori — vuoi procedere comunque?"
```

#### G2.3 — Manuale senza sezione "setup" chiara @error
```gherkin
Given il manuale ha le istruzioni setup sparse in più capitoli
When Sara tappa "Genera guida setup"
Then l'app risponde "Non riesco a identificare una sezione setup chiara. Vuoi che cerchi le istruzioni nel testo generale?"
And offre 3 opzioni: [✅ Cerca nel testo, 📷 Indica tu le pagine setup, ⏭️ Salta e inizia direttamente]
```

### G3 — Q&A regole

#### G3.1 — Domanda chiara su regola ben documentata @happy
```gherkin
Given la partita di "Tainted Grail" è in corso
And il manuale è completamente indicizzato con confidence media > 0.85
When Marco chiede "quanti dadi tira il guerriero per attaccare con una spada a due mani?"
And Sara digita la domanda nell'app
Then l'app risponde in < 5 sec:
   "Il guerriero tira 3 dadi quando attacca con armi a due mani, +1 dado se ha 'Forza Bruta' attiva."
And mostra una citazione: "📖 Manuale, p.18 — Combattimento Marziale"
And mostra preview thumbnail della pagina sorgente
And confidence badge: 🟢 Alta
```

#### G3.2 — Domanda ambigua @edge
```gherkin
Given Sara chiede "cosa succede quando si pareggia?"
When l'app interpreta la domanda
Then l'app rileva ambiguità (può essere combat, iniziativa, voto, esplorazione)
And risponde "La tua domanda può riferirsi a 4 contesti diversi:"
And mostra 4 opzioni cliccabili
And Sara tappa "Combattimento" → ottiene risposta dettagliata + citazione
```

#### G3.3 — Domanda su regola non chiara @edge
```gherkin
Given Sara chiede "se due giocatori muoiono nello stesso turno, chi diventa il leader?"
And il manuale non copre esplicitamente questo caso
When l'app processa la query
Then risponde "Non sono sicuro — questa regola non è chiara dal manuale di Tainted Grail."
And confidence badge: 🟡 Bassa
And offre 3 azioni:
   | Azione                   | Effetto                                                     |
   | 📷 Rifotografa pagina     | Apre camera per ri-OCR di pagine specifiche                |
   | 🤝 Definisci house rule   | Salva la decisione del gruppo per le query future          |
   | 🔍 Cerca su BoardGameGeek | Apre query esterna (link BGG forum)                        |
And se Sara sceglie house rule e digita la decisione, AgentMemory salva la regola per la sessione
```

#### G3.4 — House rule trump manuale @edge (NEW from review)
```gherkin
Given Sara ha definito una house rule "i critici fanno doppio danno" che contraddice il manuale
When Marco chiede "quanto danno fa un colpo critico?"
Then l'app risponde "Per il vostro gruppo: doppio danno (house rule definita 14 min fa)."
And include footnote: "📖 Regola ufficiale manuale: +50% danno (p.22) — ma state usando la vostra variante"
And confidence badge: 🟢 Alta (house rule esplicita)
```

#### G3.5 — Latenza eccede target @error
```gherkin
Given la connessione internet è degradata
When Sara fa una domanda
And la risposta non arriva entro 8 sec (P99 timeout)
Then l'app mostra "⏳ Sto pensando, ci vuole più del solito..."
And dopo 15 sec aggiuntivi, se ancora niente, mostra "Connessione lenta — riprova o passa a modalità manuale"
And offre azione "📖 Mostrami le pagine sul tema X" (fallback ai risultati di ricerca grezza senza LLM generation)
```

### G4 — Traduzione narrativa

#### G4.1 — Lettura paragrafo richiesto dal GM @happy
```gherkin
Given Sara ha indicizzato un manuale EN
And la partita è in corso
When Sara digita "vai al §147"
Then l'app cerca il paragrafo §147 nel manuale
And in < 5 sec mostra il testo tradotto in italiano formattato per lettura ad alta voce:
   - Font 20pt, line height 1.5, larghezza max 60ch
   - Dialoghi evidenziati in corsivo
And il testo originale EN è disponibile in toggle "🌐 Mostra originale"
And la traduzione viene cached per la sessione (no re-cost)
```

#### G4.2 — Pre-traduzione capitolo @happy (deferred MVP-1)
```gherkin
Given Sara è in setup pre-partita
When Sara tappa "📚 Pre-traduci Capitolo 1"
And conferma il prompt "Pre-traduzione di 50 paragrafi consumerà ~30 credits"
Then l'app inizia traduzione background dei 50 paragrafi
And mostra progress "12/50 paragrafi tradotti" sopra la UI di gioco
And quando Sara apre §X durante la partita, il testo è già pronto (latenza < 1 sec)
```

#### G4.3 — Paragrafo con riferimenti tecnici @edge
```gherkin
Given il manuale contiene oggetti "Sword of Avalon", "Wraithstone", personaggi "Niamh"
And il glossario per-gioco ha mappato:
   | EN              | IT                |
   | Sword of Avalon | Spada di Avalon   |
   | Wraithstone     | Pietra Spettrale  |
   | Niamh           | Niamh (invariato) |
When Sara apre §147 che contiene "Niamh raises the Sword of Avalon and the Wraithstone glows."
Then la traduzione output è "Niamh solleva la Spada di Avalon e la Pietra Spettrale brilla."
And il glossario è applicato consistentemente in tutti i paragrafi della stessa sessione
```

#### G4.4 — Paragrafo non trovato @error
```gherkin
Given Sara digita "vai al §299" ma il manuale ha solo paragrafi fino a §250
When l'app cerca §299
Then risponde "Paragrafo §299 non trovato nel manuale indicizzato."
And offre 3 azioni:
   | Azione                          | Effetto                                            |
   | 🔍 Cerca testo simile           | Mostra paragrafi con numerazione vicina (§290-§300) |
   | 📷 Rifotografa pagine mancanti  | Apre camera per ri-OCR pagine non indicizzate      |
   | ⏭️ Salta paragrafo              | Marca come "letto" e lascia gestione manuale       |
```

#### G4.5 — Manuale chapter-based (no §) @edge (NEW from review)
```gherkin
Given Sara ha indicizzato un manuale chapter-based (es. "Andor Chronicles")
When Sara cerca "il prossimo segmento"
Then l'app mostra l'indice dei capitoli disponibili
And consente di cercare via snippet testo: "L'eroe entra nella foresta oscura"
And ritorna il segmento più simile via semantic search con confidence ≥ 0.75
And se confidence < 0.75: "Non sono sicuro di quale segmento intendi — vuoi navigare manualmente?"
```

#### G4.6 — Quota mensile esaurita @error
```gherkin
Given Sara è in piano free (50 pag/mese di traduzione)
And ha già consumato 49/50 pagine questo mese
When Sara apre il 51° paragrafo
Then l'app mostra modale: "Hai esaurito la tua quota gratuita di traduzioni questo mese."
And offre 2 azioni (semplificate Phase 1 — review issue #5):
   | Azione                              | Effetto                                            |
   | 💎 Acquista 100 credits (€5)        | Sblocca 100 traduzioni aggiuntive                  |
   | ⏸️ Continua senza traduzione        | Mostra solo testo originale, gruppo traduce manualmente |
And la quota free si resetta automaticamente il 1° del mese successivo
```

#### G4.7 — WiFi cade durante traduzione @error (NEW from review)
```gherkin
Given Sara sta leggendo §147 ad alta voce al gruppo
And richiede §148
When la connessione cade prima che la traduzione arrivi
Then l'app mostra l'ultima frase tradotta visibile (non hard error)
And status indicator discreto: "🟡 Sto riprovando..."
And dopo 10 sec senza riconnessione: "Connessione persa — paragrafo originale disponibile" + bottone "Mostra EN"
And alla riconnessione, traduzione completa appare automatically
And UI non mai blocca completamente o mostra modal full-screen (review issue #4.3)
```

### 📊 Scenario coverage matrix

| Goal | @happy | @edge | @error | Totale |
|------|--------|-------|--------|--------|
| G1 — Acquisire manuale | 1 | 3 | 2 | 6 |
| G2 — Setup guidato | 1 | 1 | 1 | 3 |
| G3 — Q&A regole | 1 | 3 | 1 | 5 |
| G4 — Traduzione narrativa | 2 | 2 | 3 | 7 |
| **Totale** | **5** | **9** | **7** | **21** |

---

## 4. Non-Functional Requirements

### 4.1 ⚡ Performance

| Aspetto | Target | Misurazione |
|---------|--------|-------------|
| Q&A regole — P50 | < 3 sec | Telemetry `query_to_first_token` |
| Q&A regole — P95 | **< 5 sec** (rivisto da 3 sec) | Telemetry stesso |
| Q&A regole — P99 | < 8 sec (timeout con fallback) | Telemetry + alerting |
| Traduzione on-demand — P95 | < 5 sec per paragrafo medio | Telemetry `translation_request_to_complete` |
| Setup guidato generation | < 30 sec end-to-end | Telemetry `setup_request_to_render` |
| OCR throughput | **≥ 10 pag/min batch parallel** (rivisto da 8 sec/pag seriale) | Telemetry `pages_indexed_per_minute` |
| Manuale completo (50 pag.) | < 5 min totali | Job tracking |
| Concurrent users per session | 4 telefoni simultanei senza degradazione | Load test pre-release |

### 4.2 ♿ Accessibilità

| Requisito | Target |
|-----------|--------|
| Touch target minimi | ≥ 48×48 dp |
| Font size testo narrativo | ≥ 18pt default, ≥ 24pt opzionale "lettura distante" |
| Contrasto testo | WCAG AA (≥ 4.5:1) |
| Voice input | Voice-to-text per Q&A (riuso device-native, low expectation in salotto rumoroso — vedi R-14) |
| Screen always-on | Durante sessione attiva, opzione "tieni schermo acceso" abilitata di default |
| Reduced motion | Rispetta `prefers-reduced-motion` |
| Localizzazione UI | Italiano per MVP. Stringhe estratte i18n-ready |
| TTS audio narrator | **Roadmap v2** — vedi §4.9 (eliminata duplicazione review issue) |

### 4.3 🌐 Offline & resilienza

| Scenario | Behavior atteso |
|----------|----------------|
| WiFi cade durante upload manuale | Resume automatico, no perdita progresso |
| WiFi cade durante Q&A | Mostra errore graceful + offer retry, no crash |
| **WiFi cade durante traduzione mid-flow** | **Last frase visibile + status indicator discreto + auto-retry, no hard error modal** (review issue) |
| App killed dal SO durante OCR background | Job server-side continua, push notification a completion |
| Batteria smartphone < 10% | UI mostra warning discreto |
| Server LLM rate limited / down | Circuit breaker → degrade graceful |
| Foto esistente già indicizzata da altro user | Riusa indice via SharedGameCatalog hash → costo 0 |

### 4.4 🔒 Privacy & sicurezza

| Aspetto | Decisione |
|---------|-----------|
| Manuale caricato | Storato lato server. Visibile solo a Sara. Cancellabile via "Elimina manuale" |
| Foto pagine | Compresse + dewarped + storate. Cancellabili da Sara |
| Conformità GDPR | Right to deletion implementato. Data export su richiesta. Privacy policy chiara |
| Copyright manuale | **PREREQUISITE pre-launch** — legal review obbligatoria (vedi §5.1 PR-1, ex OQ-1) |
| Glossario per-gioco | Locale + sincronizzato per session |
| House rules | Salvate in AgentMemory |
| Auth multi-device opt-in (v2+) | QR code → token ephemeral **8 ore** (rivisto da 4 — sessioni gamebook 5-6h tipiche, review issue) |
| Dati telemetria | Anonimizzati. Opt-out disponibile. Niente tracking di contenuto traduzioni |

### 4.5 📱 Compatibilità device

| Aspetto | Supporto MVP |
|---------|--------------|
| iOS | ≥ 16 (Safari + PWA) |
| Android | ≥ 12 (Chrome + PWA) |
| Form factor | Smartphone portrait + landscape. Tablet roadmap. |
| Camera | Risoluzione minima 8MP. Warning su < 5MP |
| Connessione | WiFi/4G/5G. Bandwidth minima 1 Mbps |
| Storage locale | < 200 MB cache per sessione attiva. Auto-purge 24h |

### 4.6 💰 Cost analysis (consolidato — review issue #4.6)

**Target unitari** (post OpenRouter + DeepSeek tier):

| Operazione | Model preferito | Cost/op | Volume/utente attivo/mese | Cost utente/mese |
|-----------|-----------------|---------|--------------------------|------------------|
| Q&A regola (high-confidence) | DeepSeek-V3 via OpenRouter | €0.003 | 60 query | €0.18 |
| Q&A regola (high-stakes) | Claude Haiku 4.5 | €0.015 | 10 query | €0.15 |
| Traduzione paragrafo | Claude Sonnet 4.5 | €0.025 | 90 pag (3 sess × 30 pag) | €2.25 |
| Setup guide generation | Claude Haiku | €0.020 | 3/mese | €0.06 |
| **TOTALE LLM utente attivo/mese** | | | | **~€2.64** |

**Cost per active user (full picture)** — vedi §4.11 per breakdown infrastruttura:

| Scenario | Infra fissa/utente | LLM | Storage | **Total** |
|----------|-------------------|-----|---------|-----------|
| 50 utenti (CAX31) | €0.40 | €2.64 | €0.05 | **€3.09** |
| 200 utenti (CAX31) | €0.10 | €2.64 | €0.04 | **€2.78** |
| 1000 utenti (cluster) | €0.10 | €2.64 | €0.03 | **€2.77** |

**Revenue assumption** (rivisto conservative — review issue):

- **Phase 1 MVP** (2-tier solo Free + Credits):
  - Free tier: 70% utenti, €0 revenue
  - Credits avg buyer: 30% utenti × €5/100 credits ≈ €1.50/utente/mese
  - **Revenue avg/utente Phase 1 MVP: €1.50**
- **Post-v2** (con Premium subscription attivata):
  - Free tier: 60% utenti, €0 revenue
  - Credits avg buyer: 30% utenti × €5/100 credits ≈ €1.50/utente/mese
  - Premium subscription: 10% utenti × €8/mese = €0.80/utente/mese
  - **Revenue avg/utente post-v2: €2.30** (rivisto da €3.50 ottimistico)

**Break-even MVP Phase 1**: ~600 utenti attivi (LLM cost €2.64 vs revenue €1.50 → cross con scale).
**Break-even post-v2**: ~250 utenti attivi.

> ⚠️ **Implicazione strategica**: MVP Phase 1 è **loss-making fino a ~600 utenti**. Pricing-to-cost gap sanato dall'attivazione tier Premium in v2. Considerare aumento Credits price (€5 → €7) se scale lenta.

### 4.7 🧪 Testabilità

| Livello | Strategia | Owner | Effort |
|---------|-----------|-------|--------|
| Unit | State helpers (setup checklist, glossary application, cache lookup) | Backend dev | 1 week |
| Integration | OCR pipeline foto → KB indexed (Testcontainers) | Backend dev | 1 week |
| E2E | "Sessione salotto" full journey | Playwright dev | 2 weeks |
| Visual | Manuali reali fotografati in condizioni domestiche. Dataset golden 5 manuali. | QA | 1 week |
| Usability | 5 sessioni reali con gruppi target | UX researcher | 2 weeks |
| Chaos | Spegni WiFi, kill app, batteria 5%, server LLM 503 | Manuale + scripted | 1 week |
| **LLM evaluation** | **Test set 100 query Q&A + 50 paragrafi traduzione golden** | **ML engineer + IT native speaker (gamebook expertise)** | **2 weeks dedicati** (review issue #10 — owner + budget esplicito) |

### 4.8 🌍 Internazionalizzazione (i18n) & localizzazione (l10n)

#### Architectural readiness (MVP must-have)

| Aspetto | Decisione MVP | Razionale |
|---------|--------------|-----------|
| Stringhe UI | Tutte estratte in file resource (`it.json`) | Aggiunta lingua = nuovo file, no code change |
| Locale formatting | `Intl.DateTimeFormat`, `Intl.NumberFormat` | Nessuna formattazione manuale |
| Encoding | UTF-8 ovunque | Supporto immediato |
| Pluralizzazione | ICU MessageFormat | Plurali corretti |
| Date input parsing | Formato locale-aware | Evita ambiguità |

#### Lingue supportate per dimensione

| Dimensione | MVP | Roadmap v1 | Roadmap v2+ |
|-----------|-----|------------|-------------|
| **UI lingua app** | IT | + EN | + DE, FR, ES, PT |
| **Lingua sorgente manuale** (OCR) | EN, IT (latin script) | + DE, FR, ES, PT, NL | + JP, ZH (CJK pipeline) |
| **Lingua target traduzione** | IT | + EN, DE, FR, ES | + qualsiasi LLM-supported |
| **Glossario per-gioco** | IT/EN bilingual MVP | Multi-lingue parallel | + auto-suggest da BGG |

> ❌ **RTL support DEFERRED v3+** (review issue #4.8 — gamebook market dominato latin script, over-engineering per MVP).

### 4.9 🎙️ AI Narrator audio (roadmap v2 — non MVP)

> **Scope roadmap**: l'app legge ad alta voce con voce sintetica narrativa (TTS), eliminando la duplicazione con §4.2 — questa è feature di prodotto, non solo accessibility.

#### Use case roadmap

> Sara apre §147. Tap "🎙️ Leggi tu" → l'app riproduce audio narrativo in italiano. Sara e gli amici ascoltano insieme.

#### Modalità di consumo

| Modalità | Descrizione | Quando preferita |
|----------|-------------|------------------|
| **Solo testo** (MVP) | Mostra traduzione, GM legge | Default casual |
| **Testo + audio toggle** (v2) | Mostra testo + bottone "🎙️ Leggi" | Sara sceglie paragrafo per paragrafo |
| **Audio-only immersivo** (v2.5) | Schermo si oscura, solo audio | Sessioni atmosferiche |
| **Dual-track (audio + sottotitoli)** (v3) | Audio + testo evidenziato in sync | Accessibilità |

#### Architettura tecnica

| Aspetto | Decisione roadmap |
|---------|-------------------|
| Engine TTS cloud | ElevenLabs (quality top), OpenAI TTS (bilanciato), Google Cloud TTS, Azure Neural TTS |
| **Engine TTS self-hostable** | **Coqui TTS / Piper / Bark** — required per coerenza con Privacy strict preset (review issue #4.9) |
| Voice selection | Catalogo curato di 4-6 voci italiane |
| Tono adattivo | Prompt LLM pre-TTS analizza paragrafo → suggerisce parametri |
| Streaming | Audio generato in streaming. Latency target: primo audio < 3 sec |
| Cache | Audio cached per la sessione. Persist 24h locale |
| Storage | Audio NON storato server-side. Solo cache CDN edge-region |

#### Trigger di prioritizzazione

- ≥ 30% utenti MVP richiedono TTS in feedback survey
- Costo medio sessione testo < €1.50 (margine sufficiente)
- Almeno 1 competitor lancia feature simile

### 4.10 🎛️ User-controlled LLM provider selection (roadmap v2)

> **NON in MVP Phase 1** (review issue #6: over-engineering per casual primary). Default Auto invisibile in MVP. Preset selector + BYOK + transparency UI introdotti in v2.

#### Tre modalità di selezione (v2)

| Mode | Audience | UX |
|------|----------|-----|
| **🤖 Auto (default)** | Casual primary | Sistema sceglie best provider per ogni task via OpenRouter routing |
| **🎚️ Preset (intermediate)** | Power-user, privacy-conscious | 4-5 preset curati |
| **🔧 Custom (advanced)** | Developer, B2B, enterprise | Per ogni task scegli provider+modello |

#### Preset curati (v2)

| Preset | Optimization | Provider mix | Quality bar |
|--------|--------------|--------------|-------------|
| **🌱 Risparmio** | Cost-first | DeepSeek-V3 / Llama 3.1 self-hosted | "Buono" |
| **⚖️ Bilanciato** (default) | Cost+quality | OpenRouter routing dinamico | "Eccellente" |
| **💎 Premium** | Quality-first | Claude Sonnet 4.5 / GPT-4o | "Top" |
| **🇪🇺 Privacy EU-only** | Data sovereignty | Mistral Large + self-hosted | "Buono" |
| **🔒 Privacy strict** | Zero data retention | Self-hosted Llama 3.1 70B | "Discreto" |

#### BYOK (v2.5)

| Aspetto | Valore |
|---------|--------|
| Audience | Enterprise, dev, hobbyist |
| Pricing | "BYOK Pro" subscription €3/mese |
| Sicurezza | Key cifrata at-rest (AES-256) |

### 4.11 🖥️ Infrastructure cost analysis (Hetzner CAX31 baseline)

> **Decisione architetturale** (review issue #4): **CAX31 day 1**, NON CAX21 borderline. €14/mese fisso evita OOM-killer in produzione.

#### Server reference

**Hetzner CAX31** (Cloud ARM-based):
- 8 vCPU Ampere Altra ARM
- 16 GB RAM
- 160 GB NVMe SSD
- 20 TB traffic incluso
- **€11.49/mese** + IVA (€14.02 IVA inc.)

#### Capacity assessment

| Componente | RAM stimata | CAX31 fit |
|-----------|-------------|-----------|
| API .NET 9 | 400-600 MB | ✅ |
| PostgreSQL 16 + pgvector | 1.5-2 GB | ✅ |
| Redis | 200-400 MB | ✅ |
| Next.js 16 (SSR) | 500-800 MB | ✅ |
| embedding-service Python | 800 MB-1.5 GB | ✅ |
| reranker-service Python | 600 MB-1 GB | ✅ |
| smoldocling-service Python (OCR) | 1.5-2 GB | ✅ |
| unstructured-service Python | 500 MB-800 MB | ✅ |
| Monitoring | 300-500 MB | ✅ |
| **TOTALE picco** | **6.4-9.0 GB** | **✅ Comfortable su 16 GB** |

#### Cost breakdown infrastruttura mensile

| Voce | MVP (CAX31) | Growth (CAX31 + Box) | Scale (cluster) |
|------|-------------|----------------------|-----------------|
| Hetzner server | €14.02 | €14.02 | €42 (3× CAX31) |
| Storage Box 1 TB | – | €4.65 | €4.65 |
| Domain + DNS | €1.00 | €1.00 | €1.00 |
| Cloudflare | €0 (free) | €0 | €20 (Pro) |
| TLS / Monitoring | €0 | €0 | €29 |
| **TOTALE INFRA FISSA** | **€15.02/mese** | **€19.67/mese** | **€96.65/mese** |

#### Self-hosted LLM roadmap (5000+ MAU trigger)

> ⚠️ **Review issue ROI math**: il saving reale è ~€150-300/mese (Q&A bulk solo), NON €700/mese stimato originale. Translation Sonnet rimane cloud per quality bar.

**Setup**: Hetzner Dedicated GEX44 (RTX 4000 Ada 20GB) — €184/mese.
**Use case**: Q&A bulk routing (DeepSeek replacement self-hosted), embedding self-hosted.
**Break-even**: 5000 MAU con €900/mese cloud Q&A → €184 self-hosted = €700 saving lordo. Quality match Llama 3.1 70B vs DeepSeek validation richiesta.

#### Disaster recovery

| Scenario | Recovery strategy | Cost |
|----------|-------------------|------|
| CAX31 down | Restore da Storage Box su nuovo CAX31 | RTO ~2h, €0 incremental |
| Region down | Hot standby Helsinki | +€14/mese standby, RTO 15 min |
| Data loss | Daily backup su Storage Box, weekly su Cloudflare R2 | +€2/mese |
| **DR add-on totale** | | **+€16/mese per RTO 15 min** |

---

## 5. Open Questions, Roadmap, Risk Register

### 5.1 ❓ Open Questions & Prerequisites

#### 🚨 Prerequisites (BLOCKERS pre-launch)

| # | Item | Owner | Deadline |
|---|------|-------|----------|
| **PR-1** | **Copyright stance — Legal review** (ex-OQ-1, promosso da open question — review issue #2) | Legal advisor da identificare (owner: Aaron) | Pre-MVP launch (entro mese 4 di sprint) |
| **PR-2** | **OCR validation su 5 manuali gamebook reali** (test layout artistici — review issue #3 + R-13) | ML engineer | Pre-Phase 1 sprint start (2 settimane) |
| **PR-3** | **Test set golden creation** (100 Q&A + 50 paragrafi narrativi) | ML engineer + IT native speaker | Pre-MVP launch (4 settimane lead time) |

#### Decisioni differite (rinviabili)

| # | Question | Owner | Trigger per riapertura |
|---|----------|-------|------------------------|
| OQ-2 | Edition lock: come distinguiamo 1st vs 2nd edition? | Product | Quando un utente reporta "regole sbagliate" |
| OQ-3 | Hallucination rate target → **DEFINITO**: ≤ 3% golden, 0% target su confidence > 0.85 (review issue #8) | – | – |
| OQ-4 | Glossario auto-bootstrap (NER vs manual curation) | Product + ML | Dopo G1 implementato — A/B test 5 giochi |
| OQ-5 | Multi-tenant glossario: condivisione cross-utente? | Product + Privacy | Quando SharedGameCatalog dedup è in produzione |
| OQ-6 | Pricing localization: elasticità casual italiano vs internazionale? | Pricing team | Pre-launch in nuovi mercati |
| OQ-7 | Save/resume MVP-1 trigger esplicito (review issue #7) | Product | Misurare retention post-MVP — se < 40% sessioni hanno follow-up entro 7 giorni |
| OQ-8 | Validation MOS DeepSeek su traduzione narrativa italiana | ML | Pre-attivazione DeepSeek tier in production |
| OQ-9 | OpenRouter vs direct API overhead | Backend | Primo trimestre post-launch |
| OQ-10 | BYOK launch timing — v2 o v2.5? | Product | Dopo validation power-user adopters |
| OQ-11 | EU-only preset infra cost (self-hosted Llama capacity) | DevOps | Pre-v2 launch |
| OQ-12 | Cost transparency UI granularità (A/B test) | Product + Design | Pre-v2 launch |
| OQ-13 | Multi-device QR session lifecycle (review issue #1.3 critical) | Product + Backend | Pre-v2 implementation |

### 5.2 🗺️ Roadmap

#### Phase 0 — Foundation (esistente)
> Stato corrente MeepleAI: BC funzionanti, pipeline PDF + RAG operativa, frontend Next.js v2 in migration.

#### Phase 1 — MVP libro game (vedi §6 per scope dettagliato)

**Effort realistic**: **4-5 mesi, 3 dev fullstack + 1 ML + 1 designer** (review issue #9 — riallineato da 2-3 mesi sottostimato).

#### Phase 1.5 — Polish & retention (post-MVP, 1-2 mesi)

| Feature | Trigger | Priorità |
|---------|---------|----------|
| **Save/resume base** (paragrafo + glossario + house rules) | Sara's pain top priority — esplicita non opzionale (review issue #7) | **Alta MVP+1** |
| Setup wizard interattivo (G2 full) | Se MVP usa solo "show setup paragraph" e completion rate < 60% | Alta |
| UI lingua EN | 30%+ utenti registrati hanno locale non-IT | Alta se trigger |
| Pre-translate chapter background (G4.2) | Se utenti chiedono ripetutamente | Media |
| Multi-device QR opt-in (read-only) | Se 30%+ sessioni hanno > 4 player al tavolo | Media |
| Voice-to-text Q&A | Se Q&A typing < 5/sessione media | Bassa (R-14 risk) |

#### Phase 2 — Differentiation (3-4 mesi)

- Two-pass translation quality (Premium tier moat)
- AI Narrator audio (§4.9)
- LLM provider control: preset selector + transparency UI (§4.10)
- Glossario auto-bootstrap via NER

#### Phase 2.5 — Power-user

- BYOK (Bring Your Own API Key) + "BYOK Pro" subscription
- Custom per-task LLM mode

#### Phase 3 — Expansion (3+ mesi)

- Lingue UI: DE, FR, ES, PT
- Lingue source: JP/ZH (CJK pipeline)
- AI Narrator multilingue + voice cloning
- Multi-device collaborativo full (sync real-time)
- Modalità famiglia con parental control
- Tablet-optimized UI

### 5.3 🛡️ Risk register

| Risk | Probabilità | Impatto | Mitigazione |
|------|-------------|---------|-------------|
| **R-1**: LLM pricing raddoppia | 🟡 Media | 🟡 Medio (declassato grazie a OpenRouter) | Multi-provider abstraction. Pricing rivisto trimestrale |
| **R-2**: OCR quality bassa | 🔴 Alta | 🟡 Medio | Test set 20 manuali. Smoldocling fallback unstructured. UI confidence + manual override |
| **R-3**: Copyright takedown | 🟡 Media | 🔴 Alto | **PR-1 legal review pre-launch**. TOS robusto. Reactive takedown process |
| **R-4**: Competitor giant entry (Asmodee, Dized funded) | 🟡 Media | 🔴 Alto | Moat = casual italian + traduzione narrativa quality. Differentiation roadmap (AI Narrator) v2 |
| **R-5**: LLM hallucination su regole critiche | 🟡 Media | 🔴 Alto | Confidence threshold ≥ 0.8. Test set golden 100 Q&A. Telemetry user-flagged. Human review loop |
| **R-6**: Casual non vuole installare app | 🟡 Media | 🟡 Medio | PWA prioritized. Web-first dev priority |
| **R-7**: Stagionalità board game | 🟢 Bassa | 🟡 Medio | Pricing freemium + credits assorbe variabilità |
| **R-8**: Glossario errato propaga errori | 🟡 Media | 🟡 Medio | Glossario per-sessione esposto in UI. Auto-flag inconsistencies |
| **R-9**: WiFi domestico instabile | 🔴 Alta | 🟡 Medio | Cache aggressiva. Pre-translate option. Graceful degradation (G4.7 scenario) |
| **R-10**: GDPR non-compliance su foto manuali | 🟢 Bassa | 🔴 Alto | Right to deletion. Privacy policy. Annual GDPR audit |
| **R-11**: DeepSeek GDPR (data trasferimento extra-UE) | 🟡 Media | 🟡 Medio | Restringere uso a task non-PII (NER glossario). Disclosure TOS |
| **R-12**: OpenRouter SPOF | 🟢 Bassa | 🟡 Medio | Direct API fallback configurato per provider critici |
| **R-13** (NEW): **OCR fallisce su layout artistici gamebook** (Tainted Grail, ISS Vanguard) | 🔴 **Alta** | 🔴 **Alto** | **PR-2 validation upfront**. Manual override UI. Curated OCR per gioco premium |
| **R-14** (NEW): **Voice-to-text in salotto rumoroso fallisce** | 🔴 Alta | 🟢 Bassa (feature roadmap, non MVP critica) | Aspettativa user lowered. Type-first UI primario |
| **R-15** (NEW): **Test set golden creation è collo di bottiglia ML eval** | 🔴 Alta | 🟡 Medio | **PR-3 owner + 4 settimane lead time**. Procurement IT native speaker con gamebook expertise |

### 5.4 🏆 Competitive moat (Porter)

| Competitor | Loro strength | Nostra differentiation |
|-----------|---------------|------------------------|
| **BoardGameArena** | Catalogo enorme di giochi *digitalizzati* | Loro: digital-only. Noi: companion del fisico |
| **Tabletopia** | Tabletop digitale 3D | Stesso: digital-only |
| **Dized** | Tutorial board game ufficiali pre-prodotti | Loro: slow per gioco, no traduzione. Noi: AI generative + traduzione |
| **BGG forum** | Community Q&A umana | Loro: high quality ma async. Noi: instant + traduzione + cited |
| **Google Translate / DeepL** | Traduzione generic | Loro: zero context gaming. Noi: glossario + tono narrativo + manuale OCR'd |
| **Manuali italiani ufficiali** | Traduzione ufficiale | Loro: solo top-seller (10%). Noi: long-tail (90%) |

**Sustainable moats**:
1. Casual italian gaming community (first-mover)
2. House rules database — network effect (con tensione vs Privacy strict preset, vedi OQ-5)
3. Glossario per-gioco crowdsourced
4. Integration multi-capability (OCR + RAG + traduzione + setup)
5. Provider-agnostic platform (OpenRouter)
6. Trust transparency (reputational moat EU-aware market)

---

## 6. MVP Phase 1 — Implementation Scope

> ## 🎯 Questo è il subset effettivamente buildabile in 4-5 mesi
>
> Drastic scope cut dalla vision per evitare scope creep severo (review issue #1). Effort estimate realistic — vedi §6.4 per breakdown per componente.

### 6.1 ✅ What's IN — Phase 1

| Feature | Scope ridotto |
|---------|---------------|
| **G1 Acquire manuale** | Photo-first ingestion. Quality bar realistic: confidence visibile, manual override per pagine fail. **PR-2 validation OCR su 5 manuali pre-sprint start.** |
| **G3 Q&A regole** | On-demand, latency P95 < 5 sec, hallucination ≤ 3% golden. House rule capability G3.4 inclusa. |
| **G4 Translation on-demand single paragraph** | Cache 24h locale + sessione. **NO pre-translate chapter** (deferred MVP-1). Fallback chapter-based G4.5 incluso. Graceful WiFi loss G4.7. |
| **LLM mode** | **Default Auto invisible**. OpenRouter routing internal. **NO preset, NO BYOK, NO transparency UI** (deferred v2). |
| **Pricing** | **2 tier solamente: Free 50 pag/mese + Credits €5/100 pag**. NO subscription premium, NO BYOK Pro (deferred v2). |
| **Topology** | **Single-device only**. NO multi-device QR opt-in (deferred MVP-1). |
| **Infrastructure** | **CAX31 day 1** (€14/mese). Cloud-managed Python services optional se carico cresce. NO borderline RAM gamble. |
| **i18n architecture** | Stringhe estratte i18n-ready, ma UI lingua solo IT. NO selector lingua (deferred MVP-1). |
| **Test plan** | Unit + Integration + E2E + LLM evaluation con test set golden 100 Q&A + 50 trad. |

### 6.2 ❌ What's OUT — Phase 1 (con deferral target)

| Feature | Deferred to | Trigger |
|---------|-------------|---------|
| **Save/resume cross-day** | **MVP-1** (Phase 1.5) | **Esplicito high-priority** — risolve persona Sara pain |
| G2 Setup wizard interattivo | MVP-1 | Se MVP "show setup paragraph" completion < 60% |
| G4.2 Pre-translate chapter | MVP-1 | Richiesta utenti |
| Multi-device QR opt-in | MVP-1 | 30%+ sessioni > 4 player |
| UI lingua EN | MVP-1 | 30%+ utenti locale non-IT |
| AI Narrator audio | v2 | 30%+ utenti chiedono TTS |
| Two-pass translation | v2 | Premium tier validation |
| LLM preset selector + transparency | v2 | Power-user segment validation |
| BYOK | v2.5 | Dev/enterprise inbound |
| Multi-device collaborative full | v3 | Market validation |
| Source language CJK | v3 | Mercato JP/ZH |
| RTL support | v3+ | Mercato Arabic/Hebrew |
| Modalità famiglia / parental | v3 | Famiglia segment validation |

### 6.3 📋 Acceptance criteria Phase 1

MVP è "shippable" quando:

1. ✅ 5 sessioni reali end-to-end completate con gruppi target (gamebook in EN tradotto)
2. ✅ ≥ 70% utenti completano almeno 1 sessione di 2h+
3. ✅ Costo medio sessione ≤ €3.00 (target consolidato §4.6)
4. ✅ Hallucination rate Q&A ≤ 3% su test set golden (PR-3)
5. ✅ OCR validation su 5 manuali gamebook reali ≥ 85% pages confidence accettabile (PR-2)
6. ✅ Legal review copyright completata + TOS aggiornato (PR-1)
7. ✅ Latenza P95 Q&A < 5 sec, traduzione < 5 sec
8. ✅ Pricing engine 2-tier funzionante con cap free 50 pag/mese

### 6.4 ⏱️ Realistic effort estimate

| Componente | Effort | Owner |
|-----------|--------|-------|
| Photo-first ingestion (dewarping + page detect + multi-shot stitching) | 4-6 weeks | 1 ML engineer dedicated |
| Translation service (cache + glossary + OpenRouter integration) | 3-4 weeks | 1 backend dev |
| Q&A flow with confidence + citations + house rule | 3 weeks | 1 fullstack |
| 5+ UI screens + onboarding | 4-6 weeks | 1 designer + 1 fullstack |
| LLM evaluation infra + golden test set creation | **2 weeks dedicated** + IT native speaker | 1 ML + contractor |
| Pricing engine + Free tier counter | 1-2 weeks | 1 fullstack |
| Legal review + TOS update | 2-3 weeks | Legal advisor |
| OCR validation on real manuals | 2 weeks | 1 ML |
| Integration testing + chaos engineering | 2 weeks | 1 QA |
| **TOTALE realistic** | **4-5 mesi** | **3 fullstack + 1 ML + 1 designer + legal advisor part-time** |

> ⚠️ **NON 2-3 mesi 4 persone come stimato originalmente** (review issue #9).

### 6.5 🚦 Pre-launch prerequisites checklist

Prima di pubblicare MVP:

- [ ] **PR-1**: Legal review copyright + TOS aggiornato + privacy policy GDPR-compliant
- [ ] **PR-2**: OCR validated su ≥ 5 manuali gamebook reali (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor, 7th Continent)
- [ ] **PR-3**: Test set golden 100 Q&A + 50 paragrafi narrativi italiani con expert validation
- [ ] **CAX31 deployment** + monitoring + alerting + backup automated
- [ ] **Pricing engine tested** end-to-end (Free counter + Credits checkout + Stripe/payment integration)
- [ ] **Hallucination rate ≤ 3%** validato su test set golden in CI gate
- [ ] **5 sessioni usability testing** completate, feedback incorporato
- [ ] **Disaster recovery drill** eseguito (restore da backup in < 2h)

---

## 7. Decisions Log

Decisioni prese durante la sessione di brainstorming 2026-05-04, in ordine cronologico.

| # | Decisione | Razionale |
|---|-----------|-----------|
| D-01 | Job-to-be-done: companion passivo + per libro game traduzione narrativa | Sara cita esplicitamente "manuale non in italiano" come pain primario non considerato nelle 3 user story originali |
| D-02 | Modalità traduzione: on-demand default + override "pre-traduci capitolo" user-controlled | Costo controllato, flessibilità per power-user senza forzare decisioni a casual |
| D-03 | Topologia device: single-device GM + multi-device read-only opt-in via QR | Casual reality, progressive enhancement per nerd entusiasta |
| D-04 | Save/resume **OUT MVP, IN MVP-1** | Scope reduction per shipping velocity, ma promosso esplicitamente in Phase 1.5 (review issue) |
| D-05 | Customer focus: casual gruppo (B) | Mercato espandibile, pain acuto, non hardcore vocal minority |
| D-06 | Pricing MVP: Freemium 50 pag/mese + Credits €5/100 pag (2 tier solamente) | Riduzione da 4 tier originali per evitare confusione casual (review issue #5) |
| D-07 | Quality bar traduzione: LLM single-pass narrative MVP, two-pass premium roadmap | Sweet spot quality/cost per casual, premium come differentiation v2 |
| D-08 | LLM provider: OpenRouter abstraction + Anthropic primary + DeepSeek tier per cost | Anti-vendor lock-in, cost optimization, multi-tier per use case |
| D-09 | User-controlled LLM provider selection: **OUT MVP, v2** | Over-engineering per casual primary (review issue #6) |
| D-10 | Infrastructure: **CAX31 day 1**, NON CAX21 borderline | Production safety, no OOM-killer risk (review issue #4) |
| D-11 | Documento type: **Vision Doc, NOT MVP spec** | Scope creep severo durante brainstorming → riposizionamento honest (review path B) |
| D-12 | OQ-1 Copyright promosso a **PR-1 Prerequisite** | Blocker pre-launch, non rinviabile (review issue #2) |
| D-13 | OQ-3 Hallucination rate **DEFINITO**: ≤ 3% golden, 0% target su confidence > 0.85 | Eliminata open question rinviabile, target operationale (review issue #8) |
| D-14 | Test set golden creation: 4 settimane lead time + IT native speaker + ML engineer (PR-3) | Owner + budget esplicito (review issue #10) |
| D-15 | OCR validation 5 manuali gamebook reali pre-sprint start (PR-2) | R-13 mitigation upfront, evita scoperta late di layout artistici problematici (review issue #3) |
| D-16 | Roadmap effort: **4-5 mesi** 3 fullstack + 1 ML + 1 designer | Riallineato da 2-3 mesi sottostimato (review issue #9) |
| D-17 | RTL support **OUT v3+** | Over-engineering MVP, gamebook market dominato latin script |
| D-18 | TTS self-hostable engine identificato (Coqui/Piper/Bark) per coerenza Privacy strict preset | Eliminata inconsistenza §4.9 vs §4.10 (review issue) |
| D-19 | Multi-device token expiry: **8 ore** (rivisto da 4) | Sessioni gamebook tipiche 5-6h, 4h causava expiry mid-session (review issue) |
| D-20 | Q&A latency P95: **5 sec** (rivisto da 3 sec) | Realistic LLM Haiku end-to-end performance (review issue) |

---

## Appendix A — Riferimenti

- **MeepleAI CLAUDE.md**: `D:\Repositories\meepleai-monorepo-frontend\CLAUDE.md`
- **Sessione brainstorming**: chat session 2026-05-04
- **Spec-panel review iniziale**: stesso session, output del comando `/sc:spec-panel`
- **Ultrathink review**: stesso session, response a path B selection
- **BC esistenti riferiti**: DocumentProcessing, KnowledgeBase, GameToolbox, AgentMemory, SessionTracking, SharedGameCatalog

## Appendix B — Glossario domain

| Termine | Definizione |
|---------|-------------|
| **Gamebook / libro game** | Board game con narrativa ramificata letta a paragrafi numerati (Tainted Grail, ISS Vanguard, etc.) |
| **GM (Game Master)** | Giocatore al tavolo che conduce la sessione, legge i paragrafi, gestisce regole. In contesto MeepleAI è il primary user dell'app. |
| **Paragrafo §** | Unità narrativa identificata da numero (es. §147) nel manuale gamebook |
| **House rule** | Variante di regola decisa dal gruppo che differisce dal manuale ufficiale |
| **OCR confidence** | Score 0-1 sicurezza del modello OCR sulla riconoscibilità testo. Confidence ≠ accuracy. |
| **MOS (Mean Opinion Score)** | Metrica 1-5 quality TTS / traduzione, valutata da human raters |
| **BYOK (Bring Your Own Key)** | Pattern in cui utente fornisce propria API key per LLM provider, bypassando billing platform |

---

**Fine Vision Document.**
