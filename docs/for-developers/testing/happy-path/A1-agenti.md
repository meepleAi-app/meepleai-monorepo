# Happy Path — A1 · Agenti AI (admin)

> Catalogo scenari happy-path per l'area **A1 — Agenti AI admin** (`admin/(dashboard)/agents/**`). Solo percorso di successo. Utente: **admin** (login da `infra/secrets/admin.secret`, `ADMIN_EMAIL`/`ADMIN_PASSWORD`).
>
> Formato e legenda: vedi [`_TEMPLATE.md`](./_TEMPLATE.md). Mappa globale route→area: [`_coverage-map.md`](./_coverage-map.md).

## Intestazione

- **Area**: A1 — tooling admin per agenti AI, RAG playground, inspector, config, usage, template review, infrastruttura.
- **Prerequisiti dati (seed `make seed-sp4`)**: admin verificato; 13 shared-games con PDF indicizzati (Azul, Catan, Wingspan, …) — servono come *game context* nel RAG playground; 5 agenti user-facing game-scoped (Azul Rules Expert, Wingspan Rules, …) creati via `/agents/create-with-setup` (NB: sono nel dominio U4, **non** sono `agent-definitions` admin). Le **agent-definitions** admin (ciclo Draft→Testing→Published) **non sono seedate** → gli scenari Flow che ne hanno bisogno la creano da zero con marcatore `HP-TEST`.
- **Utente**: admin per tutti gli scenari (l'intero gruppo `admin/(dashboard)` è protetto da `RequireRole(['Admin'])` + guardia server su cookie view-mode).
- **Stack**: `make dev` (full) — il RAG playground e il debug chat richiedono l'AI stack (embedding + OpenRouter). Con `make dev-core` gli scenari di streaming vanno marcati `⚠️ blocked-env`.

### Nota architetturale — molte route sono redirect (consolidamento admin, epic #5490)

12 delle 25 route dell'area sono **redirect server-side** (`redirect()` in `page.tsx`) verso 6 pagine reali con deep-link a tab. Non hanno UI propria: il loro happy path è "l'URL redirige alla destinazione attesa e la destinazione carica". Sono verificate da scenari `[Smoke]` dedicati al redirect (osservabile = URL finale + heading della destinazione), evitando di duplicare lo smoke della pagina di destinazione.

Mappa dei redirect:

| Route sorgente | Redirige a |
|----------------|-----------|
| `agents/builder` | `agents/definitions` |
| `agents/sandbox` | `agents/playground` |
| `agents/debug-chat` | `agents/playground?tab=chat` |
| `agents/debug` | `agents/inspector` |
| `agents/chat-limits` | `agents/config?tab=limits` |
| `agents/models` | `agents/config?tab=models` |
| `agents/strategy` | `agents/config` |
| `agents/pipeline` | `agents/inspector?tab=pipeline` |
| `agents/ab-testing/new` | `agents/playground?tab=compare` |
| `agents/ab-testing/[id]` | `agents/playground?tab=compare` |
| `agents/ab-testing/results` | `agents/playground?tab=compare` |
| `agents/chat-history` | `agents/usage?tab=chat-log` |

---

## Matrice di copertura (25 route)

| Route | Tipo pagina | Scenario/i | Note |
|-------|-------------|-----------|------|
| `agents` (landing) | Dashboard read-only ("AI Mission Control") | **A1-01** | KPI + service health + azioni rapide + ultime esecuzioni RAG |
| `agents/definitions` | Lista + azioni lifecycle + delete | **A1-02**, A1-10, A1-28 | Smoke lista; lifecycle publish in A1-10; delete + reload persistenza in A1-28 |
| `agents/definitions/create` | Form transazionale | **A1-03**, A1-28, A1-29 (Flow) | Crea agent definition; persistenza create in A1-28/A1-29 |
| `agents/definitions/[id]` | Dettaglio read-only | **A1-04**, A1-28, A1-29 | Detail agente; verifica edit/create persistiti dopo reload in A1-28/A1-29 |
| `agents/definitions/[id]/edit` | Form transazionale | **A1-05**, A1-28 (Flow) | Modifica agent definition; edit + reload persistenza in A1-28 |
| `agents/definitions/playground` | Chat streaming full ("Agent Playground") | **A1-06** (Flow) | Esegui agente selezionato, streaming SSE |
| `agents/playground` | Chat streaming debug ("RAG Playground") | **A1-07** (Flow) | Debug chat con game context + timeline |
| `agents/config` | Pagina 3 tab (strategy/models/limits) | **A1-08** (Smoke), **A1-09** (Flow) | Smoke tab switch; Flow salva limiti chat |
| `agents/inspector` | Dashboard 3 tab (esecuzioni/pipeline/waterfall) | **A1-11** | Live executions + filtri + selezione dettaglio |
| `agents/analytics` | Dashboard 3 tab metriche | **A1-12** | KPI + grafici + top agenti |
| `agents/usage` | Dashboard 3 tab (openrouter/token/chat-log) | **A1-13** | Utilizzo & costi OpenRouter |
| `agents/templates` | Coda review + azioni | **A1-14** | Template review queue (approve/reject) — Smoke su carico + empty-state |
| `agents/infrastructure` | Dashboard read-only | **A1-15** | Service health + connectivity |
| `agents/builder` | Redirect → `definitions` | **A1-16** | |
| `agents/sandbox` | Redirect → `playground` | **A1-17** | |
| `agents/debug-chat` | Redirect → `playground?tab=chat` | **A1-18** | |
| `agents/debug` | Redirect → `inspector` | **A1-19** | |
| `agents/chat-limits` | Redirect → `config?tab=limits` | **A1-20** | |
| `agents/models` | Redirect → `config?tab=models` | **A1-21** | |
| `agents/strategy` | Redirect → `config` | **A1-22** | |
| `agents/pipeline` | Redirect → `inspector?tab=pipeline` | **A1-23** | |
| `agents/ab-testing/new` | Redirect → `playground?tab=compare` | **A1-24** | smoke-aggregato con A1-25/A1-26 (stessa destinazione) |
| `agents/ab-testing/[id]` | Redirect → `playground?tab=compare` | **A1-25** | |
| `agents/ab-testing/results` | Redirect → `playground?tab=compare` | **A1-26** | |
| `agents/chat-history` | Redirect → `usage?tab=chat-log` | **A1-27** | |

**Copertura**: 25/25 route → ≥1 scenario. Nessuna route `skip`. Prevalenza Smoke (dashboard read-only + redirect); Flow transazionali (create, edit, esecuzione playground, salva limiti, lifecycle) + 2 Flow di ciclo CRUD/persistenza sulle Agent Definition (A1-28 ciclo completo crea→edita→elimina con reload, A1-29 persistenza create isolata).

---

## Scenari

### Dashboard & viste read-only

```gherkin
Scenario A1-01 [Smoke]: AI Mission Control carica con KPI e stato servizi
  Given sono loggato come admin
  When apro /admin/agents
  Then la pagina "AI Mission Control" carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'header "AI Mission Control"
    And la riga KPI mostra le 5 card (Esecuzioni Oggi, Latenza Media, Error Rate, Token Consumati, Costo Oggi) con valore o "0"/"—" (non skeleton perenne)
    And la card "Stato Servizi" elenca Embedding Service / Reranker / OpenRouter / Vector DB con un badge di stato
    And la card "Azioni Rapide" mostra ≥1 bottone di navigazione
  Osservabile ✅: heading "AI Mission Control" + 5 KPI card risolte + lista servizi con badge + azioni rapide visibili
  Route: admin/(dashboard)/agents
  Utente: admin
```

```gherkin
Scenario A1-02 [Smoke]: Lista Agent Definitions carica (dati o empty-state)
  Given sono loggato come admin
  When apro /admin/agents/definitions
  Then la pagina "Agent Definitions" carica senza errori non attesi
    And lo stato "Loading..." lascia il posto alla tabella agenti oppure a un empty-state legittimo
    And vedo il bottone "Create Agent" e il bottone "Strategy Builder"
  When clicco "Strategy Builder"
  Then si apre uno sheet laterale (BuilderClient) — azione primaria con effetto visibile
  Osservabile ✅: heading "Agent Definitions" + tabella/empty-state + bottone "Create Agent" + apertura sheet Strategy Builder
  Route: admin/(dashboard)/agents/definitions
  Utente: admin
```

```gherkin
Scenario A1-04 [Smoke]: Dettaglio di una Agent Definition
  Given sono loggato come admin
    And esiste ≥1 agent definition (creata da A1-03 con nome "HP-TEST-2026-07-10 Agente Regole")
  When apro /admin/agents/definitions (lista) e clicco sulla riga di "HP-TEST-2026-07-10 Agente Regole"
  Then arrivo alla pagina di dettaglio /admin/agents/definitions/{id} e carica senza errori non attesi
    And l'header mostra nome e descrizione dell'agente + badge di stato (Draft/Testing/Published) + badge Active/Inactive
    And la sezione "Configuration" mostra Model, Strategy, Temperature, Max Tokens
    And la sezione "Channel Configuration" è presente; clic su "Enable Channel" rivela l'endpoint WebSocket (effetto visibile)
  Osservabile ✅: nome agente nell'header + badge stato + sezione Configuration popolata + toggle Channel produce dettagli
  Route: admin/(dashboard)/agents/definitions/[id]
  Utente: admin
```

```gherkin
Scenario A1-11 [Smoke]: RAG Inspector — live executions e selezione dettaglio
  Given sono loggato come admin
    And il seed ha generato esecuzioni RAG (o le genera un run di A1-07 poco prima)
  When apro /admin/agents/inspector
  Then la pagina "RAG Inspector" carica senza errori non attesi
    And la stats-bar mostra 5 metriche (Esecuzioni, Latenza Media, Errori, Cache Hit, Costo)
    And il tab "Esecuzioni" mostra la tabella "Live Executions" con righe reali oppure "No executions found" (empty-state legittimo)
  When (se ci sono righe) clicco una riga della tabella
  Then la riga risulta selezionata (evidenziata) e il pannello dettaglio si aggiorna
  When passo al tab "Pipeline"
  Then il tab cambia contenuto (diagramma pipeline o messaggio "No execution trace")
  Osservabile ✅: heading "RAG Inspector" + stats-bar 5 valori + tabella/empty-state + cambio tab Pipeline con effetto visibile
  Route: admin/(dashboard)/agents/inspector
  Utente: admin
```

```gherkin
Scenario A1-12 [Smoke]: Analisi Agenti — KPI e grafici
  Given sono loggato come admin
  When apro /admin/agents/analytics
  Then la pagina "Analisi Agenti" carica senza errori non attesi
    And vedo i selettori periodo (7d/30d/90d) e il tab di default "Panoramica"
    And nel tab Panoramica vedo le KPI card e i due riquadri grafico (Utilizzo nel Tempo, Costi per Modello) con dato o "Nessun dato disponibile"/empty-feature legittimo
  When clicco il periodo "30d"
  Then il bottone "30d" diventa attivo (effetto visibile) e i dati si ricaricano
  When passo al tab "Top Agenti"
  Then il contenuto cambia mostrando la tabella top agenti o "Nessun agente trovato"
  Osservabile ✅: heading "Analisi Agenti" + KPI/grafici o empty legittimo + toggle periodo attivo + cambio tab Top Agenti
  Route: admin/(dashboard)/agents/analytics
  Utente: admin
```

```gherkin
Scenario A1-13 [Smoke]: Utilizzo & Costi — dashboard OpenRouter con tab
  Given sono loggato come admin
  When apro /admin/agents/usage
  Then la pagina "Utilizzo & Costi" carica senza errori non attesi
    And nel tab di default "OpenRouter" vedo le sezioni Panoramica (KPI), Grafici, Limiti di Velocità, Richieste Recenti — con dati o empty-feature legittimo se l'endpoint non è implementato
  When clicco il tab "Log Chat"
  Then il contenuto cambia mostrando i filtri chat-history e la tabella (o skeleton→contenuto)
  Osservabile ✅: heading "Utilizzo & Costi" + sezioni KPI/grafici/rate-limit visibili + cambio tab "Log Chat" con effetto visibile
  Route: admin/(dashboard)/agents/usage
  Utente: admin
```

```gherkin
Scenario A1-14 [Smoke]: Template Review Queue carica (dati o empty)
  Given sono loggato come admin
  When apro /admin/agents/templates
  Then la pagina "Template Review Queue" carica senza errori non attesi
    And vedo la griglia delle review card (con bottoni Approve/Reject) oppure "No templates pending review" oppure l'empty-feature "Funzionalità non disponibile" (endpoint non implementato) — tutti stati legittimi
  Osservabile ✅: heading "Template Review Queue" + review-grid con card, o empty-state "No templates pending review", o empty-feature legittimo
  Route: admin/(dashboard)/agents/templates
  Utente: admin
```

```gherkin
Scenario A1-15 [Smoke]: AI Infrastructure dashboard carica
  Given sono loggato come admin
  When apro /admin/agents/infrastructure
  Then la pagina "AI Infrastructure" carica senza errori non attesi
    And il componente InfrastructureDashboard renderizza (service health / connectivity) con dati o stati di caricamento risolti
  Osservabile ✅: heading "AI Infrastructure" + dashboard infrastruttura renderizzata (nessuno skeleton perenne, nessun errore Console)
  Route: admin/(dashboard)/agents/infrastructure
  Utente: admin
```

```gherkin
Scenario A1-08 [Smoke]: Configurazione AI — switch tra i 3 tab
  Given sono loggato come admin
  When apro /admin/agents/config
  Then la pagina "Configurazione AI" carica senza errori non attesi
    And il tab di default "Strategy" è attivo e mostra il suo contenuto
  When clicco il tab "Models"
  Then il contenuto cambia mostrando lo stato dei modelli (health badge / storico) o empty legittimo
  When clicco il tab "Limits"
  Then il contenuto cambia mostrando il form "Limiti per Tier" (Free/Normal/Premium)
  Osservabile ✅: heading "Configurazione AI" + 3 TabsTrigger (Strategy/Models/Limits) + cambio tab con contenuto visibilmente diverso
  Route: admin/(dashboard)/agents/config
  Utente: admin
```

### Flussi transazionali (Flow)

```gherkin
Scenario A1-03 [Flow]: Creare una Agent Definition
  Given sono loggato come admin
  When apro /admin/agents/definitions/create
    And compilo "Agent Name" = "HP-TEST-2026-07-10 Agente Regole"
    And compilo "Description" con un testo breve
    And lascio Model = "GPT-4", Max Tokens e Temperature ai default
    And invio il form (submit)
  Then compare un toast "Agent \"HP-TEST-2026-07-10 Agente Regole\" created successfully"
    And vengo reindirizzato a /admin/agents/definitions
    And la nuova definition "HP-TEST-2026-07-10 Agente Regole" appare nella lista (stato Draft)
  Osservabile ✅: toast di successo con il nome + URL torna a /definitions + riga "HP-TEST-…" presente in tabella
  Route: admin/(dashboard)/agents/definitions/create → admin/(dashboard)/agents/definitions
  Utente: admin
```

```gherkin
Scenario A1-05 [Flow]: Modificare una Agent Definition esistente
  Given sono loggato come admin
    And esiste la definition "HP-TEST-2026-07-10 Agente Regole" (creata in A1-03)
  When apro /admin/agents/definitions e clicco "Edit" sulla riga di quella definition
    (oppure apro direttamente /admin/agents/definitions/{id}/edit)
  Then il form "Edit Agent Definition" carica precompilato col nome e la config correnti
  When aggiorno "Description" con un nuovo testo e invio il form
  Then compare un toast "Agent \"HP-TEST-2026-07-10 Agente Regole\" updated successfully"
    And vengo reindirizzato a /admin/agents/definitions
  Osservabile ✅: form precompilato + toast di update con il nome + URL torna a /definitions
  Route: admin/(dashboard)/agents/definitions/[id]/edit → admin/(dashboard)/agents/definitions
  Utente: admin
```

```gherkin
Scenario A1-06 [Flow]: Eseguire un agente nell'Agent Playground (streaming)
  Given sono loggato come admin
    And esiste ≥1 agent definition attiva (es. "HP-TEST-2026-07-10 Agente Regole" oppure una definition di default)
  When apro /admin/agents/definitions/playground
    And nel selettore "Select Agent" scelgo un agente
    And (opzionale) nel selettore "Game Context (RAG)" scelgo "Azul"
    And nella chat scrivo "Come si vince ad Azul?" e invio
  Then l'assistant produce una risposta in streaming (i token appaiono progressivamente)
    And al termine il messaggio assistant è non-vuoto
    And il pannello laterale "Debug" mostra eventi/metadati della richiesta
  Osservabile ✅: messaggio user inviato + risposta assistant non-vuota in streaming + pannello Debug popolato
  Route: admin/(dashboard)/agents/definitions/playground
  Utente: admin
  Note: richiede AI stack (make dev). Con make dev-core → ⚠️ blocked-env. Osservabile strutturale (risposta presente), non testo letterale (LLM non deterministico).
```

```gherkin
Scenario A1-07 [Flow]: Debug chat nel RAG Playground con game context
  Given sono loggato come admin
    And il gioco "Azul" ha un PDF regole indicizzato (seed KB)
  When apro /admin/agents/playground
    And nella StrategySelectorBar seleziono il gioco "Azul"
    And scrivo "Quanti giocatori supporta Azul?" e premo Invio
  Then l'assistant risponde in streaming (indicatore di digitazione → testo)
    And al termine il messaggio assistant è non-vuoto
    And il pannello "Debug Timeline" mostra gli eventi della pipeline RAG
  Osservabile ✅: gioco selezionato + risposta assistant non-vuota in streaming + timeline debug con ≥1 evento
  Route: admin/(dashboard)/agents/playground
  Utente: admin
  Note: richiede AI stack (make dev). Con make dev-core → ⚠️ blocked-env. Osservabile strutturale, non testo letterale.
```

```gherkin
Scenario A1-09 [Flow]: Salvare i limiti chat per tier (config → tab Limits)
  Given sono loggato come admin
  When apro /admin/agents/config?tab=limits (oppure /config e clicco il tab "Limits")
  Then il form "Limiti per Tier" carica i valori correnti (Free/Normal/Premium)
  When incremento di 1 il valore "Premium tier" (mantenendo Premium ≥ Normal ≥ Free)
    And clicco "Salva"
  Then compare l'alert "Limiti aggiornati con successo."
    And la riga "Aggiornato il …" mostra il timestamp aggiornato
  Osservabile ✅: form limiti precaricato + alert "Limiti aggiornati con successo." dopo il salvataggio + timestamp aggiornato
  Route: admin/(dashboard)/agents/config (tab limits)
  Utente: admin
  Note: additivo/reversibile — il valore può essere riportato al precedente in un secondo salvataggio.
```

```gherkin
Scenario A1-10 [Flow]: Ciclo lifecycle di una Agent Definition (Draft → Testing → Published)
  Given sono loggato come admin
    And esiste la definition "HP-TEST-2026-07-10 Agente Regole" in stato Draft (da A1-03)
  When apro /admin/agents/definitions/{id} (dettaglio della definition)
    And clicco "Start Testing"
  Then compare il toast "Agent moved to Testing" e il badge di stato diventa "Testing"
  When clicco "Publish"
  Then compare il toast "Agent published" e il badge di stato diventa "Published"
  Osservabile ✅: toast "Agent moved to Testing" + badge "Testing" → toast "Agent published" + badge "Published"
  Route: admin/(dashboard)/agents/definitions/[id]
  Utente: admin
  Note: opera sui dati HP-TEST creati in A1-03 (additivo, non tocca il seed). Reversibile via "Unpublish".
```

### Redirect (smoke sul redirect)

```gherkin
Scenario A1-16 [Smoke]: /agents/builder redirige a /agents/definitions
  Given sono loggato come admin
  When apro /admin/agents/builder
  Then l'URL finale è /admin/agents/definitions
    And carica la pagina "Agent Definitions" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/definitions + heading "Agent Definitions"
  Route: admin/(dashboard)/agents/builder → admin/(dashboard)/agents/definitions
  Utente: admin
```

```gherkin
Scenario A1-17 [Smoke]: /agents/sandbox redirige a /agents/playground
  Given sono loggato come admin
  When apro /admin/agents/sandbox
  Then l'URL finale è /admin/agents/playground
    And carica la pagina "RAG Playground" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/playground + heading "RAG Playground"
  Route: admin/(dashboard)/agents/sandbox → admin/(dashboard)/agents/playground
  Utente: admin
```

```gherkin
Scenario A1-18 [Smoke]: /agents/debug-chat redirige al RAG Playground
  Given sono loggato come admin
  When apro /admin/agents/debug-chat
  Then l'URL finale è /admin/agents/playground?tab=chat
    And carica la pagina "RAG Playground" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/playground(?tab=chat) + heading "RAG Playground"
  Route: admin/(dashboard)/agents/debug-chat → admin/(dashboard)/agents/playground
  Utente: admin
```

```gherkin
Scenario A1-19 [Smoke]: /agents/debug redirige a /agents/inspector
  Given sono loggato come admin
  When apro /admin/agents/debug
  Then l'URL finale è /admin/agents/inspector
    And carica la pagina "RAG Inspector" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/inspector + heading "RAG Inspector"
  Route: admin/(dashboard)/agents/debug → admin/(dashboard)/agents/inspector
  Utente: admin
```

```gherkin
Scenario A1-20 [Smoke]: /agents/chat-limits redirige a config tab Limits
  Given sono loggato come admin
  When apro /admin/agents/chat-limits
  Then l'URL finale è /admin/agents/config?tab=limits
    And carica "Configurazione AI" col tab "Limits" attivo (form "Limiti per Tier" visibile)
  Osservabile ✅: URL = /admin/agents/config?tab=limits + heading "Configurazione AI" + form limiti tier visibile
  Route: admin/(dashboard)/agents/chat-limits → admin/(dashboard)/agents/config
  Utente: admin
```

```gherkin
Scenario A1-21 [Smoke]: /agents/models redirige a config tab Models
  Given sono loggato come admin
  When apro /admin/agents/models
  Then l'URL finale è /admin/agents/config?tab=models
    And carica "Configurazione AI" col tab "Models" attivo
  Osservabile ✅: URL = /admin/agents/config?tab=models + heading "Configurazione AI" + tab Models attivo
  Route: admin/(dashboard)/agents/models → admin/(dashboard)/agents/config
  Utente: admin
```

```gherkin
Scenario A1-22 [Smoke]: /agents/strategy redirige a config (tab Strategy default)
  Given sono loggato come admin
  When apro /admin/agents/strategy
  Then l'URL finale è /admin/agents/config
    And carica "Configurazione AI" col tab "Strategy" attivo di default
  Osservabile ✅: URL = /admin/agents/config + heading "Configurazione AI" + tab Strategy attivo
  Route: admin/(dashboard)/agents/strategy → admin/(dashboard)/agents/config
  Utente: admin
```

```gherkin
Scenario A1-23 [Smoke]: /agents/pipeline redirige a inspector tab Pipeline
  Given sono loggato come admin
  When apro /admin/agents/pipeline
  Then l'URL finale è /admin/agents/inspector?tab=pipeline
    And carica "RAG Inspector" col tab "Pipeline" attivo
  Osservabile ✅: URL = /admin/agents/inspector?tab=pipeline + heading "RAG Inspector" + tab Pipeline attivo
  Route: admin/(dashboard)/agents/pipeline → admin/(dashboard)/agents/inspector
  Utente: admin
```

```gherkin
Scenario A1-24 [Smoke]: /agents/ab-testing/new redirige al Playground tab Compare
  Given sono loggato come admin
  When apro /admin/agents/ab-testing/new
  Then l'URL finale è /admin/agents/playground?tab=compare
    And carica la pagina "RAG Playground" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/playground?tab=compare + heading "RAG Playground"
  Route: admin/(dashboard)/agents/ab-testing/new → admin/(dashboard)/agents/playground
  Utente: admin
  Note: smoke-aggregato — A1-24/A1-25/A1-26 verificano la stessa destinazione (?tab=compare); basta un giro per instradamento con l'ID/segmento corrispondente.
```

```gherkin
Scenario A1-25 [Smoke]: /agents/ab-testing/[id] redirige al Playground tab Compare
  Given sono loggato come admin
  When apro /admin/agents/ab-testing/HP-TEST-id (un id qualsiasi)
  Then l'URL finale è /admin/agents/playground?tab=compare
    And carica la pagina "RAG Playground" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/playground?tab=compare + heading "RAG Playground"
  Route: admin/(dashboard)/agents/ab-testing/[id] → admin/(dashboard)/agents/playground
  Utente: admin
```

```gherkin
Scenario A1-26 [Smoke]: /agents/ab-testing/results redirige al Playground tab Compare
  Given sono loggato come admin
  When apro /admin/agents/ab-testing/results
  Then l'URL finale è /admin/agents/playground?tab=compare
    And carica la pagina "RAG Playground" senza errori non attesi
  Osservabile ✅: URL = /admin/agents/playground?tab=compare + heading "RAG Playground"
  Route: admin/(dashboard)/agents/ab-testing/results → admin/(dashboard)/agents/playground
  Utente: admin
```

```gherkin
Scenario A1-27 [Smoke]: /agents/chat-history redirige a usage tab Chat Log
  Given sono loggato come admin
  When apro /admin/agents/chat-history
  Then l'URL finale è /admin/agents/usage?tab=chat-log
    And carica "Utilizzo & Costi" col tab "Log Chat" attivo (filtri + tabella chat-history)
  Osservabile ✅: URL = /admin/agents/usage?tab=chat-log + heading "Utilizzo & Costi" + tab Log Chat attivo
  Route: admin/(dashboard)/agents/chat-history → admin/(dashboard)/agents/usage
  Utente: admin
```

### Ciclo CRUD con verifica di persistenza (Flow) — Agent Definition

> Copre il requisito CRUD della spec §3.1: ogni mutazione è confermata da un **reload** che riconferma lo stato persistito nel backend (non solo il feedback ottimistico della UI). Opera **solo** su una definition marcata `HP-TEST` creata dallo scenario stesso — nessun dato di seed viene toccato. Il ciclo completo Create→Edit→Delete è **interamente esposto in UI**: create/edit via form transazionali, delete via il menu azioni riga della `BuilderTable` (voce "Elimina").

```gherkin
Scenario A1-28 [Flow]: Ciclo CRUD completo Agent Definition (crea → edita → elimina) con reload di persistenza
  Given sono loggato come admin
  # --- CREATE ---
  When apro /admin/agents/definitions/create
    And compilo "Agent Name" = "HP-TEST-2026-07-10 CRUD Agente"
    And compilo "Description" = "definition di prova per ciclo CRUD"
    And lascio Model, Max Tokens, Temperature ai default
    And invio il form (submit)
  Then compare il toast "Agent \"HP-TEST-2026-07-10 CRUD Agente\" created successfully"
    And vengo reindirizzato a /admin/agents/definitions
    And la riga "HP-TEST-2026-07-10 CRUD Agente" (stato Bozza) appare nella tabella
  When ricarico /admin/agents/definitions (reload del browser)
  Then la riga "HP-TEST-2026-07-10 CRUD Agente" è ancora presente nella tabella (persistita nel backend)
  # --- EDIT ---
  When dal menu azioni (⋯) della riga clicco "Modifica"
    (oppure apro /admin/agents/definitions/{id}/edit)
  Then il form "Edit Agent Definition" carica precompilato col nome e la config correnti
  When aggiorno "Description" = "HP-TEST-2026-07-10 descrizione modificata" e invio il form
  Then compare il toast "Agent \"HP-TEST-2026-07-10 CRUD Agente\" updated successfully"
    And vengo reindirizzato a /admin/agents/definitions
  When apro il dettaglio /admin/agents/definitions/{id} e ricarico la pagina (reload del browser)
  Then il dettaglio mostra la descrizione aggiornata "HP-TEST-2026-07-10 descrizione modificata" (valore persistito dopo reload)
  # --- DELETE ---
  When torno a /admin/agents/definitions
    And dal menu azioni (⋯) della riga "HP-TEST-2026-07-10 CRUD Agente" clicco "Elimina"
  Then compare il toast "Agent definition deleted"
    And la riga "HP-TEST-2026-07-10 CRUD Agente" sparisce dalla tabella
  When ricarico /admin/agents/definitions (reload del browser)
  Then la riga "HP-TEST-2026-07-10 CRUD Agente" resta assente dalla tabella (delete persistito nel backend)
  Osservabile ✅: post-create la riga è presente e sopravvive al reload · post-edit la nuova descrizione persiste dopo reload del dettaglio · post-delete la riga è assente e resta assente dopo reload
  Route: admin/(dashboard)/agents/definitions/create → admin/(dashboard)/agents/definitions/[id]/edit → admin/(dashboard)/agents/definitions/[id] → admin/(dashboard)/agents/definitions
  Utente: admin
  Dati creati: "HP-TEST-2026-07-10 CRUD Agente" (rimossa a fine ciclo dallo step Delete)
  Note: il ciclo è additivo e auto-pulente (la definition creata è eliminata nello stesso scenario). La UI espone Create/Edit/Delete completi; il Delete è immediato (nessun dialog di conferma — clic su "Elimina" nel menu riga → toast di successo). Endpoint reali: POST/PUT/DELETE /api/v1/admin/agent-definitions[/{id}].
```

```gherkin
Scenario A1-29 [Flow]: Persistenza della creazione — la nuova Agent Definition sopravvive al reload
  Given sono loggato come admin
  When apro /admin/agents/definitions/create
    And creo la definition "HP-TEST-2026-07-10 Persist Create" (compilo "Agent Name" + "Description", default per il resto, submit)
  Then compare il toast "Agent \"HP-TEST-2026-07-10 Persist Create\" created successfully"
    And atterro su /admin/agents/definitions con la riga "HP-TEST-2026-07-10 Persist Create" (stato Bozza) in tabella
  When ricarico /admin/agents/definitions (reload del browser)
  Then la riga "HP-TEST-2026-07-10 Persist Create" è ancora presente (create persistito)
  When apro il dettaglio /admin/agents/definitions/{id} e ricarico
  Then il dettaglio carica con nome "HP-TEST-2026-07-10 Persist Create" e badge stato "Draft" (dato letto dal backend dopo reload)
  Osservabile ✅: toast di successo + riga presente in lista dopo reload + dettaglio popolato dopo reload (nome + badge Draft)
  Route: admin/(dashboard)/agents/definitions/create → admin/(dashboard)/agents/definitions → admin/(dashboard)/agents/definitions/[id]
  Utente: admin
  Dati creati: "HP-TEST-2026-07-10 Persist Create" (eliminabile dal menu riga "Elimina" a fine giro, come in A1-28)
  Note: isola la sola verifica di persistenza della Create (distinta dal ciclo completo A1-28), utile quando si vuole confermare rapidamente che il create atterra nel backend. Reversibile via Delete UI.
```

---

## Auto-verifica

- **Copertura**: tutte le 25 route della sezione A1 di `_coverage-map.md` compaiono nella matrice, ognuna con ≥1 scenario. Nessuno `skip`.
- **Osservabili**: ogni scenario dichiara un `Osservabile ✅` con almeno un marcatore strutturale (heading, badge, toast, URL finale, cambio tab, riga presente/assente dopo reload).
- **Solo happy path**: nessuno scenario negativo/errore/edge; i form Flow (A1-03, A1-05, A1-09, A1-28, A1-29) descrivono solo il submit valido; il Delete di A1-28 opera solo sul dato `HP-TEST` creato dallo scenario stesso.
- **Dati marcati**: le entità create dai Flow usano il prefisso `HP-TEST-2026-07-10` (agent definition). Nessun dato di seed viene mutato/distrutto (A1-09 e A1-10 reversibili; A1-28 e A1-29 creano ed eliminano solo dati `HP-TEST`).
- **Ciclo CRUD & persistenza (spec §3.1)**: l'entità **Agent definition** ha il ciclo completo **Create → Edit → Delete** esposto in UI, coperto dal ciclo di vita A1-28 con **reload di verifica dopo ogni operazione** (create, edit, delete). La verifica di persistenza della sola Create è isolata in A1-29. Nessun `Delete` inventato: le altre entità dell'area (template review, ab-test) **non** espongono un delete — le route templates/ab-testing sono coperte da Smoke (A1-14) o sono redirect (A1-24/25/26).
- **Ambiente**: A1-06 e A1-07 (streaming) segnalano la dipendenza dall'AI stack (`make dev`); con `make dev-core` vanno marcati `⚠️ blocked-env`. A1-28/A1-29 (CRUD su definition) **non** dipendono dall'AI stack — funzionano anche con `make dev-core`.
- **Conteggio**: 29 scenari (A1-01 … A1-29) — 8 Flow transazionali + 21 Smoke.

### Riepilogo tipi

| Tipo | Scenari |
|------|---------|
| Flow | A1-03, A1-05, A1-06, A1-07, A1-09, A1-10, A1-28, A1-29 (8) |
| Smoke | A1-01, A1-02, A1-04, A1-08, A1-11, A1-12, A1-13, A1-14, A1-15, A1-16 … A1-27 (21) |

### Disponibilità operazioni CRUD in UI (Agent definition)

| Operazione | Esposta in UI? | Dove | Endpoint | Scenario |
|-----------|----------------|------|----------|----------|
| Create | ✅ | `/definitions/create` (form) | POST `/api/v1/admin/agent-definitions` | A1-03, A1-28, A1-29 |
| Read | ✅ | `/definitions` (lista) + `/definitions/{id}` (dettaglio) | GET `…/agent-definitions[/{id}]` | A1-02, A1-04 |
| Edit | ✅ | `/definitions/{id}/edit` (form precompilato) | PUT `…/agent-definitions/{id}` | A1-05, A1-28 |
| Delete | ✅ | `BuilderTable` → menu riga (⋯) → "Elimina" (immediato, no dialog) | DELETE `…/agent-definitions/{id}` | A1-28 |
