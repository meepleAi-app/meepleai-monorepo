# Piano — le fette di ADR-095

**Data**: 2026-10-10 · **ADR**: [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) (Accepted) · **Brief**: [`2026-10-10-single-agent-ai-architecture-brief.md`](../specs/2026-10-10-single-agent-ai-architecture-brief.md)
**Base misurata**: `origin/main-dev` a `652589fff` (#4160).

## Regole operative

- **Un worktree per fetta, fuori da `.claude/worktrees/` e senza `+` nel percorso**: `git worktree add D:/Repositories/wt-<issue> -b feature/issue-<n>-<desc> origin/main-dev`, poi `git config branch.<branch>.parent main-dev` e `cd apps/web && pnpm install --offline --prefer-offline`. Un `+` nel percorso fa fallire `next build` nell'hook pre-push.
- **Prima il test che fallisce.** Ogni fetta ha un test «rosso prima»: lo si scrive e lo si vede fallire per il motivo atteso, poi si corregge.
- **Consumatori prima dei produttori.** Nessun campo, DTO o tabella si toglie dal backend finché il frontend e gli altri lettori non lo tollerano. La regola viene da #4154.
- **Expand → contract**: le rimozioni di colonne, tabelle e rotte vanno in una consegna successiva a quella che smette di usarle (Migration Safety Gate; `rollback-runbook.md` §8.3).
- **Verifica nel browser** su immagini `api` e `web` ricostruite dal commit della PR (`docker compose -f docker-compose.yml -f compose.dev.yml build api web`, poi `make dev`).
- **Definition of Done**: test rosso → verde; suite del contesto verde (chiedendo il **conteggio** dei test eseguiti, non solo l'esito); typecheck e lint, oppure `dotnet build`; verifica nel browser dove c'è una UI; PR verso `main-dev`; code review; merge; issue chiusa con la DoD spuntata.

## Mappa delle fette

| Fetta | Cosa | Dove | Rosso prima | Dipende da | Issue |
|---|---|---|---|---|---|
| **0** | Schemi agente del frontend tolleranti all'assenza dei campi destinati a sparire | `apps/web/src/lib/api/schemas/` | contratto: payload vecchio **e** nuovo validati | — | **#4154** (PR 1) |
| **0b** | Switch dei due lettori vivi lato utente; lancio in sessione senza id dell'agente | `useSessionAgentLaunch`, `useHybridHubItems`, `LaunchSessionAgentCommandValidator` | lancio senza `AgentDefinitionId` → oggi `422` | 0 | **#4154** (PR 2) |
| **1** | Catalogo modelli come unico listino; registro costi attribuito | `AiModelConfiguration`, `SeedAiModelsCommandHandler`, `LlmCostCalculator`, `LlmCostService`, `IAnalysisCostEstimator` | una chiamata a un modello solo nel listino del codice è registrata a $0 | — | #4164 — 1a: catalogo, listino, `pricing_status` (PR #4180); 1b: costo attribuito a utente e `RequestSource`, anche in streaming (PR #4182) |
| **2** | Gateway unico: nessuna chiamata HTTP diretta ai provider | `ChunkTranslationService` (`VisionOcrAdapter`, mai registrato, rimosso) | test architetturale sugli host dei provider | — | #4165 (PR #4181) |
| **3** | Tetto unico prima della chiamata, quote per `RequestSource`, degrado, fail closed fuori catalogo | `HybridLlmService` (gateway), `LlmBudgetMonitoringService`, `LlmCostAlertService`, `UserBudgetService`, `TierEnforcementService` | una chiamata oltre il tetto giornaliero oggi parte | 1, 2 | #4166 — 3a: tetti e quote prima della chiamata, fail closed fuori catalogo, playground sul gateway (PR #4192); 3b: degrado (risposte sul modello di riserva del profilo, background e valutazioni in pausa), quota KbQuality dentro il tetto, allarmi sulla politica salvata, budget mode rimosso (PR #4195). Le quote per piano (`UserBudgetService` fail-open, `TierEnforcementService`, C7) non sono nei criteri della fetta: D6 le applica nella pipeline (D3, passo 2), quindi passano alla fetta 5. Runbook: [`llm-budget-cap.md`](../operations/llm-budget-cap.md) |
| **4** | Aggregato `AgentProfile` e versioni immutabili; seed della prima versione dai valori oggi fissi | `KnowledgeBase/Domain` (nuovo) | invarianti di D2 | — | #4167 (PR #4178) |
| **5** | Pipeline unica, prima fetta: `StreamQa`, `AskQuestion`, cross-game | `KnowledgeBase/Application` | cambiare la temperatura pubblicata cambia quella inviata (oggi resta 0,3) | 2, 3, 4 | #4168 — comprende le quote per piano bloccanti (C7), spostate dalla fetta 3. Consegne, in ordine: **5a** caratterizzazione del contratto SSE di `StreamQa` (PR #4214), un test HTTP con i doppi ai margini — embedding, reranker, LLM, cache — e tutto il resto vero, verde prima e dopo lo spostamento. **5b** (PR #4217) il gateway risponde con modello, riserva, temperatura e lunghezza dati dal chiamante; fallback prima del primo token e circuit breaker anche in streaming; dopo il primo token evento d'errore, costo dei token generati e fallimento sul circuit breaker; `ProfileVersionId` nel registro costi (expand). **5c** (PR #4219) pipeline e `StreamQa`: profilo pubblicato letto una volta per richiesta (assente: errore esplicito e controllo all'avvio), lingua, recupero, reranking e prompt dal profilo, con la tabella campo → effetto come «rosso prima». **5d** (PR #4223) `AskQuestion` sugli stessi passi (recupero, prompt, generazione): un rifiuto del gateway, prima una risposta vuota, è un 503 con il codice; via l'override del modello per complessità (`LlmRouting:QueryComplexityModels`, configurato vuoto); la cache semantica non serve voci di un'altra lingua o versione. **5e** cross-game e fusione unica (oggi lo stream è accumulato fino alla fine). Prima i consumatori: **5e-a** (PR #4226) lo schema del frontend di `/ask/global` accetta citazioni ed errori nella forma che il backend serializza — chiedeva `docId`/`snippet` e `message`/`code`, mai mandati, e il client scartava i due eventi in silenzio: la KB globale non mostrava né le citazioni né il motivo di un errore. **5e-b** (PR #4229) la ricerca nella libreria sulla pipeline: profilo, lingua, reranker, prompt con fonti numerate, streaming vero. Restano i pesi della fusione e le categorie (5e-c). **5f** la versione arriva dove si misura la risposta (le chiavi delle due cache la portano già, da 5c e 5d). **5f-1** (PR #PRNUM) versione del profilo e lingua nel completamento dello stream, nella risposta non in streaming (`/knowledge-base/ask`, `/agents/qa`), nel messaggio di chat salvato e nel registro delle richieste AI (`ai_request_logs`, due colonne nullable, expand). **5f-2** il feedback: oggi non si salva mai (la chat manda l'id sbagliato e il 400 viene ingoiato); corretto quello, versione e lingua si prendono dal messaggio salvato. **5g** quote per piano come passo 2 |
| **6** | Pagine admin: Agente, Modelli e prezzi, Budget e quote; rimozione delle impostazioni morte | `apps/web/src/app/admin` | la pagina Agente pubblica e la risposta lo riflette | 1, 3, 4, 5 | #4169 — **6a** Budget e quote (PR #4199): tetti, soglia di degrado e quote dalla pagina, consumo per classe dal gate; via dalla pagina soglie del circuit breaker e catena di fallback, che il runtime non legge. **6b** Modelli e prezzi (PR #4204): il prezzo di ogni modello si vede e si cambia (`PATCH …/pricing`); «predefinito del provider» sulla rotta vera (`POST …/set-primary`, che prima non esisteva); «in fallback order» con l'etichetta onesta; via «Configure», che non faceva nulla. I provider ammessi in creazione sono quelli con un client (OpenRouter, Ollama, DeepSeek). **6c** Agente, dopo la fetta 5. **6d** impostazioni morte (PR #4205): via dal tab Strategy i campi Retrieval/Generation/Confidence (mai inviati, ma il toast diceva «saved»), le schede con numeri scritti a mano e l'«Edit» senza handler dei mapping; via gli override KB per gioco (rotte, comando, query, sezione della pagina: salvati con nomi dei campi sbagliati e mai letti) e il tab Typologies (rotte admin mai esistite). **6e** via `AppBudget` (PR #4211): un secondo budget che il gateway non applicava. Escono rotte, dominio e pagina; la tabella esce con la consegna contract. La pagina «Costs» mostra la spesa dal registro e rimanda ai tetti del gateway. Restano da valutare i limiti dello storico chat |
| **7** | Qualità: domande di riferimento, valutazione, gate di pubblicazione, feedback aggregati | `KnowledgeBase`, admin | una bozza che peggiora non si pubblica | 5, 6 | #4170 |
| **8** | Pipeline, seconda fetta (sessione, setup, disputa) e switch dei lettori residui | lettori di `SharedGame.AgentDefinitionId` (`ContextualHandSlot` via `CreateSessionCommandHandler`, `GetGameRagReadinessQueryHandler`, query del catalogo con `EF.Property(a, "_gameId")`); `ChatWithSessionAgentCommandHandler`, `AskSessionAgentCommandHandler`, `StreamSetupGuideQueryHandler`, disputa | la disputa cita pagine recuperate (oggi nessun recupero) | 5, 0b; #4156 per la sessione | #4171 |
| **9** | «Nessun lettore»: nessun codice legge né scrive `agent_definitions` | rotte, playground, `AgentSession.AgentDefinitionId`, `AgentDefinitionId` di `PrivateGame`/`SharedGame` | test architetturale «nessun riferimento ad `AgentDefinition`» | 8; #4162 e le fette #4138 sugli `AgentDefinitionId` | #4172 |
| **10** | Contract: `DROP TABLE agent_definitions`, più le colonne già in expand (`user_library_entries.CustomAgentConfigJson`, `tier_definitions.max_agents`, `token_tiers.max_agents_created`, `private_games.agent_definition_id`) | migration | gate di migration | 9 **deployata** | #4173 |

**Parallelismo**: 0, 1, 2 e 4 non dipendono fra loro e possono partire insieme. La 3 attende 1 e 2; la 5 attende 2, 3 e 4. Dalla 5 in poi la catena è sequenziale.

**Fuori da questa mappa, ma in corso**: #4155 (possesso) diventa il passo 1 della pipeline quando arriva la fetta 5; fino ad allora si corregge sui punti d'ingresso attuali, come da design. #4156, #4157 e #4158 sono indipendenti.

## Fetta 0 — schemi agente tolleranti (si parte da qui)

**Obiettivo**: nessuno schema del frontend fallisce quando il backend smette di mandare un campo che ADR-094 e ADR-095 ritirano. In questo modo ogni fetta backend successiva può togliere campi senza ripetere #4154.

**Campi destinati a sparire**: `type` (già tolto dal backend in #4147), `strategyName`, `strategyParameters`, `gameId`, `kbCardIds`, `agentDefinitionId` sui giochi.

**Schemi da verificare uno per uno** (in `apps/web/src/lib/api/schemas/`, elenco ottenuto con `grep -rln "strategyName\|isSystemDefined\|AgentDtoSchema\|agentDefinitionId"`):

| Schema | Stato |
|---|---|
| `agents.schemas.ts` (`AgentDtoSchema`, `GetAllAgentsResponseSchema`, `RecentAgentsResponseSchema`) | **fatto in `D:/Repositories/wt-4154`**, non ancora committato: `type` tolto, `strategy*` opzionali, test di contratto `agent-dto.contract.test.ts` (payload reale senza `type`, payload vecchio con `type` scartato, `strategy*` assenti, `isSystemDefined` ancora obbligatorio) |
| `agent-definitions.schemas.ts` | da verificare (letto dall'admin) |
| `kb-consuming-agents.schemas.ts` | da verificare |
| `playground-scenarios.schemas.ts` | da verificare |
| `private-games.schemas.ts` | da verificare (`agentDefinitionId`) |
| `test-results.schemas.ts` | da verificare |

**Lavoro**:

1. Per ogni schema: catturare un payload reale dallo stack locale (immagini ricostruite), scrivere il test di contratto vecchio/nuovo, rendere opzionali i campi destinati a sparire. Un campo resta obbligatorio solo se qualcuno lo legge e la sua assenza cambierebbe il significato, come `isSystemDefined` (#4081).
2. Correggere i consumatori che il compilatore segnala. Già noti: `hybrid-hub.mappers.ts:84` (`agent.type`) e `GameProvider.tsx:118`, che non è montato da nessuna pagina.
3. Allineare le fixture di `agentsClient.*.test.ts` al payload reale.

**Verifica**: la suite Vitest degli schemi e dei client agenti, verde con il conteggio; `pnpm typecheck` e `pnpm lint`. Nel browser: nessun errore di validazione su `/library` né su `/sessions/{id}/live`. L'assistente in sessione può ancora non partire per il motivo di #4156 (`/game-sessions/{id}/agent/launch` con un id di `LiveGameSession`): lo si annota e non lo si confonde con questa fetta.

**Consegna**: una PR, la prima di #4154, branch `feature/issue-4154-agentdto-schema-type`.

## Domande aperte, da chiudere prima della fetta indicata

| Fetta | Domanda |
|---|---|
| 4 | Modello della prima versione del profilo. *Chiusa a metà*: `deepseek-chat` è dismesso (changelog ufficiale), e #4179 ha pubblicato la versione 2 con `deepseek-flash` e, come riserva, `meta-llama/llama-3.3-70b-instruct`. Resta da decidere se la residenza dei dati (Cina) è accettabile. |
| 2 | Il gateway adotta `Microsoft.Extensions.AI` (`IChatClient`) o resta sull'astrazione attuale? Se lo adotta, serve un ADR a sé prima di cominciare. |
| 5 | Fallback a metà streaming: dopo il primo token si interrompe o si ricomincia? *Chiusa il 2026-10-11 (#4168)*: ADR-095 D3 la fissa già, fallback solo prima del primo token. Dopo, lo stream termina con un evento d'errore, si registra il costo dei token generati, il circuit breaker registra il fallimento e la cronologia non mostra la risposta parziale come completa. |
| 7 | Chi scrive le domande di riferimento, e per quali giochi si parte. |

## Issue

Epic **#4163**. Le fette 0 e 0b sono #4154; le fette da 1 a 10 sono #4164–#4173, una per fetta, con il proprio «rosso prima» e la propria DoD presi da questo piano.
