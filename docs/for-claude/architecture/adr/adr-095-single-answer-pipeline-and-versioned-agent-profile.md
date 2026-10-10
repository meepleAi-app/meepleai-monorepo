# ADR-095 — Una sola pipeline di risposta, un profilo dell'agente con versioni, un gateway LLM con un tetto unico

**Date**: 2026-10-10
**Status**: Accepted (2026-10-10). Le osservazioni della linea #4138 sull'ordine di migrazione sono integrate; il piano a fette è [`2026-10-10-adr-095-slices-plan.md`](../../../for-developers/plans/2026-10-10-adr-095-slices-plan.md).
**Origine**: brief [`2026-10-10-single-agent-ai-architecture-brief.md`](../../../for-developers/specs/2026-10-10-single-agent-ai-architecture-brief.md) (`/sc:brainstorm`), con le decisioni prese dall'utente il 2026-10-10
**Related**: [ADR-094](./adr-094-single-system-agent.md) (un solo agente di sistema; questo ADR gli dà un corpo a runtime) · [ADR-090](./adr-090-in-session-grounded-answer-ownership.md) (`KnowledgeBase` possiede la risposta ancorata) · [ADR-059](./adr-059-catalog-seed-legal-posture.md) e [`copyright-tier-rag.md`](../copyright-tier-rag.md) (copyright) · [`rollback-runbook.md` §8.3](../../../for-developers/operations/rollback-runbook.md) (expand → contract) · #4154 (lettori lato utente degli agenti) · #4155 (possesso)

## Decisione

1. **L'agente è un profilo di configurazione**: un nuovo aggregato `AgentProfile` in `KnowledgeBase` sostituisce `AgentDefinition`.
2. **Il profilo ha versioni immutabili.** Si modifica solo una bozza; pubblicare congela una versione; ogni risposta registra la versione che l'ha prodotta.
3. **C'è una sola pipeline di risposta** per le domande sulle regole, in `KnowledgeBase`. Legge sempre la versione pubblicata del profilo.
4. **Ogni chiamata LLM passa da un solo gateway**, che sceglie il modello dal catalogo, applica il tetto di budget prima di spendere e registra il costo. Le chiamate HTTP dirette ai provider sono vietate da un test architetturale.
5. **Il catalogo modelli è l'unico listino.** Un modello senza prezzo non si pubblica e non si chiama.
6. **Il budget è un tetto unico per tutta l'AI** (utenti, background, valutazioni), con un degrado a soglie configurabile dall'admin.
7. **La valutazione blocca la pubblicazione** di una bozza che peggiora sulle domande di riferimento, quando queste esistono. Quando non esistono, si limita ad avvisare.

## Contesto — misurato su `origin/main-dev` (`9ea893dc0`)

ADR-094 ha deciso «un agente, di sistema, configurato dall'admin». Il codice non ha ancora un posto dove quella configurazione abbia effetto.

| # | Misura | Evidenza |
|---|---|---|
| C1 | Nessun handler che risponde agli utenti legge `AgentDefinition.Config`, `.Prompts` o `.Strategy`; li legge solo il playground admin | `PlaygroundChatCommandHandler.cs:154-160,442,470` |
| C2 | Più di dieci punti d'ingresso producono risposte LLM, ciascuno con prompt e parametri propri; la fusione del recupero ha 4-5 implementazioni | `StreamQaQueryHandler`, `AskQuestionQueryHandler`, `CrossGameStreamQaQueryHandler`, `ChatWithSessionAgentCommandHandler`, `AskSessionAgentCommandHandler`, `StreamSetupGuideQueryHandler`, disputa sulle regole…; `HybridFusionCore`, `RagPromptAssemblyService`, `FuseGlobally`, `HybridSearchService` |
| C3 | Temperatura e lunghezza massima sono fisse; `topK`/`minScore` sono fissati in ogni handler; la lingua del recupero in `StreamQa` è `"en"` | `HybridLlmService.cs:37-38`; `StreamQaQueryHandler.cs:362-371` |
| C4 | L'unica impostazione che sceglie il modello (`StrategyModelMapping`) è in sola lettura nella UI admin; diverse pagine salvano valori che nessuno legge (budget e `FallbackChainJson` di `LlmSystemConfig`, override KB per gioco, campi del tab Strategy) | `AgentStrategyTabContent.tsx:127-134,179`; `LlmConfigTab.tsx:23-28` |
| C5 | Esistono almeno tre listini prezzi che si contraddicono; un modello assente dal listino nel codice viene registrato a $0. L'estrattore di meccaniche stima i costi con un listino proprio («DeepSeek list pricing as of 2026-04») | `LlmCostCalculator.cs:25-107,132-136`; `SeedAiModelsCommandHandler.cs:70-71,99-100`; `GenerateMechanicAnalysisCommandHandler.cs:42` (`IAnalysisCostEstimator`) |
| C6 | Non tutte le chiamate LLM passano dai client registrati: due servizi chiamano OpenRouter direttamente, fuori dal registro costi e da ogni tetto | `ChunkTranslationService.cs:38,128`; `VisionOcrAdapter.cs:28` |
| C7 | Le quote per utente non bloccano: il budget crediti è fail-open, `TierAction.AgentQuery` non ha chiamanti, la policy di rate limit `AgentQuery` non è montata | `UserBudgetService.cs:79-104`; `ChatWithSessionAgentCommandHandler.cs:327-343`; `RateLimitingServiceExtensions.cs:263` |
| C8 | Lo streaming non aggiorna il circuit breaker e non ha fallback | `HybridLlmService.cs:232-286` |
| C9 | Sei rotte di scrittura sugli agenti erano aperte a qualunque utente autenticato, senza controllo di proprietà. `PATCH /agents/{id}/configuration` cambiava il modello dell'agente di sistema per tutti, e `unpublish` poteva spegnerlo. La linea #4138 le ritira in [#4162](https://github.com/meepleAi-app/meepleai-monorepo/pull/4162) (elenco in `EndpointContractTests.RetiredRoutes`; lato frontend esce `AgentSettingsDrawer`); ne restano le equivalenti admin sotto `/admin/agent-definitions` | #4162, 2026-10-10 |

Vincolo dato dall'utente: **budget in token sotto 20 € al mese** nei prossimi sei mesi, e **provider intercambiabili**, senza un fornitore privilegiato. La sola valutazione KbQuality ha oggi un tetto di $50 al mese (`EvalQualityOptions.cs:7`).

## Decisioni nel dettaglio

### D1 — `AgentProfile`, nuovo aggregato in `KnowledgeBase`

`AgentProfile` è un aggregato **singleton**: esiste un solo profilo di sistema. Il contesto è `KnowledgeBase` perché possiede la pipeline (ADR-090); un aggregato nuovo non richiede un contesto nuovo. `AgentDefinition` si ritira per expand → contract (§ Migrazione).

Contenuto di una versione:

| Sezione | Campi |
|---|---|
| Identità | nome, descrizione, persona |
| Modello | modello principale e modello di riserva (riferimenti al catalogo, D5) |
| Generazione | temperatura, lunghezza massima della risposta, stile delle citazioni |
| Prompt | prompt di sistema **per lingua**; lingua di ripiego |
| Recupero | numero di passaggi prima e dopo il reranking, soglia, pesi della fusione, reranker sì/no |
| Fonti | categorie di documento ammesse (regolamento, FAQ, errata, house rules) |

Il budget **non** sta nel profilo: è una politica del gateway (D6) e vale anche per chiamate che col profilo non hanno a che fare.

### D2 — Versioni immutabili

- **Stati**: `Draft` → `Published` → `Archived`.
- **Invarianti**:
  - al più una `Draft`;
  - esattamente una `Published` dal primo rilascio in poi;
  - una versione pubblicata o archiviata non si modifica.
- **Rollback**: si crea una nuova versione con il contenuto di una archiviata e la si pubblica. La storia resta monotona e il numero di versione identifica sempre un contenuto.
- **Tracciabilità**: ogni risposta, ogni riga del registro costi e ogni feedback portano il `ProfileVersionId`. È ciò che rende confrontabili due versioni (costo per domanda, 👍👎, valutazione).

### D3 — Una sola pipeline di risposta

È un servizio di `KnowledgeBase` usato da ogni punto d'ingresso che risponde a una domanda sulle regole. Il chiamante passa `{domanda, ambito (un gioco | i giochi accessibili), lingua, utente, contesto di sessione opzionale}`; l'ambito resta un parametro della domanda, come stabilito da ADR-094.

```
1. accesso       — possesso dichiarato / KB pubblica / gioco privato (RagAccessService; #4155)
2. quota utente  — quota per piano, prima di spendere
3. recupero      — una sola implementazione di fusione; lingua della query = lingua dell'utente
4. reranking     — se attivo nel profilo
5. prompt        — dalla versione pubblicata, nella lingua dell'utente
6. generazione   — tramite il gateway (D4)
7. copyright     — tier per chunk, controllo di leak, citazioni
8. registro      — versione del profilo, costo, latenza, «non lo so», id della risposta per il feedback
```

La stessa pipeline serve streaming e risposta unica, con le stesse garanzie: fallback prima del primo token, circuit breaker aggiornato in entrambi i casi (C8).

**Fuori dalla pipeline, ma dentro il gateway**: traduzione del librogame, traduzione dei chunk, OCR vision, estrazione delle meccaniche, generazione di toolkit, valutazioni. Non sono domande sulle regole, ma spendono dallo stesso budget.

### D4 — Gateway LLM unico

Il livello provider esistente (`HybridLlmService` e i client OpenRouter, DeepSeek e Ollama) diventa il **gateway**: l'unico componente autorizzato a chiamare un provider. Ogni chiamata dichiara un `RequestSource` (risposta, background, valutazione), e il gateway:

1. risolve il modello dal catalogo (D5): dal profilo per la pipeline, da configurazione per gli altri usi;
2. stima il costo massimo (token in ingresso contati + lunghezza massima × prezzo in uscita) e lo **verifica contro il budget** (D6) prima di chiamare;
3. chiama il provider, con fallback e circuit breaker anche in streaming;
4. registra il costo reale, attribuito a utente, `RequestSource` e versione del profilo.

**Divieto**: nessun codice fuori dal gateway costruisce richieste HTTP verso gli host dei provider LLM. Un test architetturale lo verifica. I siti attuali (C6) migrano sul gateway.

L'adozione di `Microsoft.Extensions.AI` (`IChatClient`) come astrazione interna del gateway, suggerita dal report Azure, **non** è decisa qui: è un dettaglio d'implementazione che merita un ADR a sé.

### D5 — Il catalogo modelli è l'unico listino

`AiModelConfiguration` diventa l'unica fonte per provider, id del modello, prezzi (ingresso, uscita, lettura e scrittura in cache), abilitazione e **regione dei dati**. `LlmCostCalculator` perde il suo listino. Due regole:

- un modello senza prezzo **non può essere pubblicato** nel profilo;
- il gateway **rifiuta** una chiamata a un modello assente dal catalogo (fail closed), invece di registrarla a $0. Per non rompere nulla, il catalogo va popolato con tutti i modelli in uso *prima* di attivare il rifiuto.

### D6 — Un tetto unico, con degrado

- **Due tetti globali**, mensile e giornaliero, in valuta, configurabili dall'admin. Valgono per tutte le chiamate del gateway.
- **Quote per `RequestSource`** dentro il tetto: il background e le valutazioni non possono consumare il budget delle risposte. Il tetto di KbQuality diventa una di queste quote.
- **Degrado a soglie** sul consumo del giorno, configurabile:

  | soglia (predefinita) | effetto |
  |---|---|
  | < 80% | normale |
  | 80-100% | modello di riserva del profilo; il background va in pausa |
  | ≥ 100% | solo risposte dalla cache; le domande nuove ricevono un messaggio esplicito («servizio AI in pausa fino a domani») |

- **Quote per piano** applicate nella pipeline (D3, passo 2), quindi davvero bloccanti (C7).

### D7 — Valutazione e pubblicazione

Una bozza si pubblica se:

1. **le domande di riferimento esistono** per almeno uno dei giochi coinvolti, e la valutazione della bozza **non peggiora** rispetto alla versione pubblicata. Altrimenti la pubblicazione è bloccata;
2. **le domande di riferimento non esistono**: la pubblicazione è consentita, con un avviso esplicito che la registra come «non valutata»;
3. **in ogni caso**, il costo stimato per domanda della bozza sta dentro il budget.

La valutazione usa la stessa pipeline delle risposte, ma con `RequestSource = valutazione`, quindi consuma la sua quota.

### D8 — Superficie admin

Cinque pagine sostituiscono quelle attuali:

- **Agente**: bozza, confronto con la versione pubblicata, prova nel playground (la stessa pipeline), pubblicazione, storico;
- **Modelli e prezzi**: il catalogo;
- **Budget e quote**: tetti, soglie, quote per piano, consumo;
- **Qualità**: domande di riferimento, valutazioni, feedback aggregati e collegati a risposta e versione;
- **Costi**.

Le impostazioni che oggi non hanno effetto (C4) **si tolgono**, non si lasciano a salvare a vuoto.

## Alternative considerate

| Alternativa | Perché no |
|---|---|
| Evolvere `AgentDefinition` invece di un aggregato nuovo | Meno migrazione, ma `AgentDefinition` porta con sé tipo, strategia, gioco, KB card, tool e gli eventi di creazione utente, cioè il modello che ADR-094 ritira. Un aggregato nuovo nasce con la forma giusta e rende esplicita la fine del vecchio. (Scelta dell'utente.) |
| Profilo modificabile sul posto, con un registro delle modifiche | Non si potrebbe dire con certezza quale configurazione ha prodotto una risposta passata: costi, feedback e valutazioni non sarebbero attribuibili. |
| Piattaforma di agenti esterna (Foundry Agent Service, OpenAI Assistants) | Lega a un fornitore (contro la priorità «provider intercambiabili»), duplica la base di conoscenza fuori da pgvector, obbliga a rifare la protezione del copyright ed è fuori dall'esame AI-200. |
| Tetto solo sulla pipeline delle risposte | Con un budget sotto 20 € al mese il rischio viene dal background senza tetto (C6, KbQuality): un tetto parziale non garantisce il vincolo. |
| Valutazione solo consultiva | Non impedisce la regressione silenziosa di un prompt; con il gate bloccante «solo se il set esiste», l'alpha non resta comunque bloccata. |
| Chiamata a $0 per i modelli senza prezzo (comportamento attuale) | È proprio il modo in cui una spesa sfugge al tetto. |

## Conseguenze

### Positive

- La configurazione dell'admin ha effetto, e un solo posto la governa.
- Ogni risposta è riconducibile a una versione: le modifiche si confrontano e si annullano.
- Il vincolo dei 20 € al mese diventa verificabile: il tetto è applicato prima della spesa e copre tutte le chiamate.
- Cambiare provider vuol dire pubblicare una versione, senza deploy.

### Negative / debito

- **È una migrazione ampia**: un aggregato nuovo, punti d'ingresso da portare sulla pipeline, siti di chiamata diretta da portare sul gateway, il ritiro di `AgentDefinition` e delle sue rotte, pagine admin da sostituire. Va fatta a fette (§ Migrazione), ciascuna verificabile da sola.
- **Fail closed sui modelli fuori catalogo**: se il catalogo è incompleto, una funzione si ferma invece di spendere in silenzio. È voluto, ma richiede di popolarlo prima di attivare il rifiuto.
- **Il degrado al 100%** fa sparire le risposte nuove fino al giorno dopo. Con un budget così basso è il prezzo del vincolo: la soglia la decide l'admin.

### Rischi

- La pipeline unica concentra le regole: un difetto lì tocca tutte le funzioni. Mitigazione: valutazione bloccante e versioni immutabili con rollback immediato.
- La stima del costo massimo prima della chiamata è conservativa (usa la lunghezza massima), quindi il tetto scatta un po' prima del reale. Si riconcilia dopo la chiamata.

## Migrazione (expand → contract, ogni fetta col suo test «rosso prima»)

> L'ordine segue una regola che #4154 ha reso esplicita: **i consumatori si adeguano prima dei produttori**, per i campi come per le tabelle. Le osservazioni sulle fette 0, 8 e 9-10 vengono dalla linea #4138 (2026-10-10).

0. **Schemi agente tolleranti, prima di ogni ritiro backend.** Ogni schema Zod del frontend che legge `AgentDto` (oggi esige `type`, `strategyName` e `strategyParameters`) accetta l'assenza dei campi destinati a sparire. *Rosso prima*: un test di contratto che valida sia il payload vecchio sia quello nuovo. Senza questa fetta, ogni ritiro di campo o DTO lato backend ripete #4154 su scala più larga.
1. **Catalogo e registro**: popolare `AiModelConfiguration` con tutti i modelli in uso; costo calcolato dal catalogo, sia quello reale (`LlmCostCalculator`) sia quello stimato (`IAnalysisCostEstimator` dell'estrattore); registro attribuito a utente e `RequestSource`. *Rosso prima*: una chiamata a un modello presente solo nel listino del codice viene registrata a $0.
2. **Gateway unico**: migrare `ChunkTranslationService` e `VisionOcrAdapter`, gli unici due siti che chiamano un provider direttamente (C6). L'estrattore di meccaniche passa già dal servizio LLM: il suo listino proprio si riallinea nella fetta 1. *Rosso prima*: il test architetturale che vieta HTTP diretto agli host dei provider.
3. **Tetto e quote**: la verifica del budget prima della chiamata, le quote per `RequestSource` e il degrado; il fail closed sui modelli fuori catalogo. *Rosso prima*: una chiamata oltre il tetto giornaliero oggi parte.
4. **`AgentProfile` e versioni**: l'aggregato, gli invarianti di D2, il seed della prima versione dai valori oggi fissi nel codice. *Rosso prima*: i test di dominio sugli invarianti.
5. **Pipeline, prima fetta**: `StreamQa`, `AskQuestion` e la ricerca cross-game sulla pipeline, che legge la versione pubblicata. *Rosso prima*: cambiare la temperatura nella versione pubblicata cambia quella inviata al provider (oggi resta 0,3).
6. **Pagine admin**: Agente, Modelli e prezzi, Budget e quote; rimozione delle impostazioni morte.
7. **Qualità**: domande di riferimento, valutazione, gate di pubblicazione, feedback aggregati.
8. **Pipeline, seconda fetta, e switch dei lettori vivi**: l'assistente in sessione (`ChatWithSession`, `AskSessionAgent`), la guida al setup e la disputa sulle regole passano sulla pipeline. Contemporaneamente si sostituiscono **esplicitamente** i due lettori ancora vivi di `AgentDefinition`:
   - `GET /games/{id}/agents`, letto da `useSessionAgentLaunch` per l'assistente della sessione live;
   - `GET /agents?scope=my-library`, letto da `useHybridHubItems` per la libreria.
   - `SharedGame.AgentDefinitionId`, rimasto in sola lettura dopo [#4176](https://github.com/meepleAi-app/meepleai-monorepo/pull/4176), che ha due lettori che cambiano ciò che vede l'utente:
     - `ContextualHandSlot` (vista Hand della sessione), che mostra «Agente AI — Pronto» e il pulsante «Chiedi» → `/chat?agentId=` solo se il gioco ha un collegamento; il valore arriva da `CreateSessionCommandHandler.TryResolveAgentDefinitionIdAsync` (SessionTracking);
     - `GetGameRagReadinessQueryHandler` (il blocco agente della readiness).
   - Le query del catalogo che leggono il collegamento con `EF.Property(a, "_gameId")`: `GetSharedGameById`, `SearchSharedGames`, `AgentDefinitionChangedForCatalogAggregatesHandler`.

   Se #4154 non li ha già tolti, un adattatore li serve dalla versione pubblicata di `AgentProfile` fino allo switch. Non si rompono mai. Con un agente unico, «il gioco ha un agente» diventa «il gioco ha una KB interrogabile da questo utente», che è la stessa domanda del controllo d'accesso (#4155).
9. **Nessun lettore**: una consegna, **deployata**, in cui nessun codice legge né scrive `agent_definitions`: rotte utente e admin, playground, `AgentSession.AgentDefinitionId`, `SharedGame.AgentDefinitionId` (dopo lo switch dei suoi lettori nella fetta 8). Il lato `PrivateGame` (`link-agent`/`unlink-agent`, `LinkUserAgentDocumentsCommand`), il codice morto del collegamento su `SharedGame` e `DegradedAgentService` escono già con #4176. *Rosso prima*: il test architetturale «nessun riferimento ad `AgentDefinition`».
10. **Contract**: `DROP TABLE agent_definitions` e dei riferimenti residui, attraverso il Migration Safety Gate, in una consegna **successiva** alla 9. Nello stesso contract vanno le colonne già portate in expand dalla linea #4138: `user_library_entries.CustomAgentConfigJson`, `tier_definitions.max_agents`, `token_tiers.max_agents_created`, `private_games.agent_definition_id` (#4176). La tabella ha colonne `NOT NULL` (`strategy` jsonb senza default, `kb_card_ids` con default `'[]'`): finché la versione precedente la legge o la scrive, la drop è bloccata, come per le colonne ([`rollback-runbook.md` §8.3](../../../for-developers/operations/rollback-runbook.md)).

Le rimozioni per campo di `GameId`, `KbCardIds` e `Strategy`, previste da ADR-094 fra i punti aperti, sono **sospese**: le assorbe il ritiro dell'intera tabella (fette 9-10).

## Verifica

- **Test architetturali**:
  - nessuna richiesta HTTP agli host dei provider LLM fuori dal gateway;
  - nessun handler di risposta che costruisce un prompt o sceglie un modello fuori dalla pipeline;
  - nessun riferimento ad `AgentDefinition` dalla fetta 9 in poi.
- **Metriche** (nomi composti secondo la regola OTel del repo): costo per `RequestSource` e per versione del profilo, consumo rispetto al tetto, tasso di «non lo so», latenza p95 per versione.
- **Prova del vincolo**: un test di integrazione porta il consumo del giorno oltre il tetto e verifica che la chiamata successiva **non** raggiunga il provider.

## Domande aperte (non bloccano l'accettazione)

1. **Modello predefinito della prima versione.** È un valore del profilo, non una decisione di questo ADR. Il candidato più economico (DeepSeek) tratta i dati in Cina, e l'alias in uso (`deepseek-chat`) è dato per ritirato da una fonte secondaria, mentre oggi risponde ancora. Va verificato sulla pagina ufficiale.
2. **Chi scrive le domande di riferimento**, e per quali giochi si parte.
3. **Semantica del fallback a metà streaming**: dopo il primo token si interrompe o si ricomincia? Va deciso nella fetta 5.
4. **`Microsoft.Extensions.AI`** come astrazione interna del gateway: è un ADR separato.
