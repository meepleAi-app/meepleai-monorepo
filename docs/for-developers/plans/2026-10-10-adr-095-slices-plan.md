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
| **1** | Catalogo modelli come unico listino; registro costi attribuito | `AiModelConfiguration`, `SeedAiModelsCommandHandler`, `LlmCostCalculator`, `LlmCostService`, `IAnalysisCostEstimator` | una chiamata a un modello solo nel listino del codice è registrata a $0 | — | #4164 |
| **2** | Gateway unico: nessuna chiamata HTTP diretta ai provider | `ChunkTranslationService`, `VisionOcrAdapter` | test architetturale sugli host dei provider | — | #4165 |
| **3** | Tetto unico prima della chiamata, quote per `RequestSource`, degrado, fail closed fuori catalogo | `HybridLlmService` (gateway), `LlmBudgetMonitoringService`, `LlmCostAlertService`, `UserBudgetService`, `TierEnforcementService` | una chiamata oltre il tetto giornaliero oggi parte | 1, 2 | #4166 |
| **4** | Aggregato `AgentProfile` e versioni immutabili; seed della prima versione dai valori oggi fissi | `KnowledgeBase/Domain` (nuovo) | invarianti di D2 | — | #4167 |
| **5** | Pipeline unica, prima fetta: `StreamQa`, `AskQuestion`, cross-game | `KnowledgeBase/Application` | cambiare la temperatura pubblicata cambia quella inviata (oggi resta 0,3) | 2, 3, 4 | #4168 |
| **6** | Pagine admin: Agente, Modelli e prezzi, Budget e quote; rimozione delle impostazioni morte | `apps/web/src/app/admin` | la pagina Agente pubblica e la risposta lo riflette | 1, 3, 4, 5 | #4169 |
| **7** | Qualità: domande di riferimento, valutazione, gate di pubblicazione, feedback aggregati | `KnowledgeBase`, admin | una bozza che peggiora non si pubblica | 5, 6 | #4170 |
| **8** | Pipeline, seconda fetta (sessione, setup, disputa) e switch dei lettori residui | `ChatWithSessionAgentCommandHandler`, `AskSessionAgentCommandHandler`, `StreamSetupGuideQueryHandler`, disputa | la disputa cita pagine recuperate (oggi nessun recupero) | 5, 0b; #4156 per la sessione | #4171 |
| **9** | «Nessun lettore»: nessun codice legge né scrive `agent_definitions` | rotte, playground, `AgentSession.AgentDefinitionId`, `AgentDefinitionId` di `PrivateGame`/`SharedGame` | test architetturale «nessun riferimento ad `AgentDefinition`» | 8; #4162 e le fette #4138 sugli `AgentDefinitionId` | #4172 |
| **10** | Contract: `DROP TABLE agent_definitions` | migration | gate di migration | 9 **deployata** | #4173 |

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
| 4 | Modello della prima versione del profilo. Il seed usa i valori oggi fissi (DeepSeek `deepseek-chat`). Va verificato sulla pagina ufficiale se l'alias è ritirato, e va deciso se la residenza dei dati (Cina) è accettabile. |
| 2 | Il gateway adotta `Microsoft.Extensions.AI` (`IChatClient`) o resta sull'astrazione attuale? Se lo adotta, serve un ADR a sé prima di cominciare. |
| 5 | Fallback a metà streaming: dopo il primo token si interrompe o si ricomincia? |
| 7 | Chi scrive le domande di riferimento, e per quali giochi si parte. |

## Issue

Epic **#4163**. Le fette 0 e 0b sono #4154; le fette da 1 a 10 sono #4164–#4173, una per fetta, con il proprio «rosso prima» e la propria DoD presi da questo piano.
