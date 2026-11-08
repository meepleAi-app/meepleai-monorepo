# ADR-094 — Un solo agente di sistema, configurato dall'admin

**Date**: 2026-10-09
**Status**: Accepted — ratifica uno stato che il codice aveva già di fatto, e ne trae le conseguenze sul modello
**Issue**: [#4138](https://github.com/meepleAi-app/meepleai-monorepo/issues/4138) (decisione) · chiude [#4103](https://github.com/meepleAi-app/meepleai-monorepo/issues/4103) e la parte backend di [#4102](https://github.com/meepleAi-app/meepleai-monorepo/issues/4102)
**Related**: ADR-090 (proprietà della risposta ancorata in sessione) · ADR-056 (UoW esplicita) · [`rollback-runbook.md` §8.3](../../../for-developers/operations/rollback-runbook.md) (expand → contract, **prerequisito di lettura** per ogni fetta residua)

## Decisione

**Un** agente, di sistema, configurato dall'admin e usato da tutti. Non legato a un gioco.

Lo **scope** del recupero resta un parametro della **domanda** — un gioco, o l'insieme accessibile per la porta globale — non una proprietà dell'agente.

Conseguenza diretta: `AgentDefinition` non porta più un tipo, e non porterà più `GameId`, `KbCardIds` né `Strategy`. Resta configurabile `Name`, `Description`, `Config` (`Model`, `MaxTokens`, `Temperature`), `Prompts`, `ChatLanguage`, `Status`. Cioè **una pagina di configurazione**, non un CRUD.

## Context — lo stato attuale era già quello, senza che nessuno l'avesse deciso

Le quattro misure sotto sono state **rieseguite** il 2026-10-09 su `main-dev` (`938ded483`), non copiate dalla issue.

| # | Misura | Esito |
|---|---|---|
| E1 | `AskQuestionQuery` non ha un `agentId`: i suoi parametri sono `GameId, Question, ThreadId?, SearchMode?, Language, BypassCache, UserId?, UserRole?` | ✅ `KnowledgeBase/Application/Queries/AskQuestionQuery.cs:12` |
| E2 | `AskQuestionQueryHandler` non menziona `AgentDefinition` | ✅ `grep -c AgentDefinition … → 0` |
| E3 | Il percorso di **recupero** non legge `KbCardIds` | ✅ 0 riscontri su `AskQuestionQueryHandler`, `StreamQaQueryHandler`, `MultiGameHybridSearchService`, `ResilientRetrievalService`, `HybridSearchEngine`, `IMultiGameHybridSearchService` |
| E4 | L'agente è portante **solo** nel playground admin, che legge `Config` e `Prompts` | ✅ `PlaygroundChatCommandHandler.cs:154-161, 180-182` |

Quindi i tipi di agente non hanno mai avuto effetto sulla risposta, e `GameId`/`KbCardIds` su `AgentDefinition` erano campi che mentivano.

## 🔴 Tre correzioni all'inventario della issue

La issue #4138 è corretta nella decisione e **incompleta in tre punti** che cambiano il piano. Sono emersi eseguendo il ritiro, non leggendolo.

### 1. I «132 riscontri di `AgentType`» erano una collisione di nome

Il censimento iniziale (`grep -rln AgentType`) dava ~132 file e faceva sembrare il ritiro enorme. Il **value object** `AgentType` aveva **quattro** consumatori: i due handler di `AgentDefinition` e i due validator.

Tutto il resto era `ChatThread.AgentType` / `ChatSession.AgentType` / `SessionChatMessage`, cioè una `string?` con valori `auto|tutor|arbitro|decisore` — un concetto **diverso**, che questa decisione non tocca.

> Il grep diagnostico non era un inventario; il compilatore lo è stato: **54 errori su 27 siti** dopo aver tolto il campo dall'entità. Vale come metodo per le fette residue.

### 2. `KbCardIds` NON è write-only: ha quattro lettori fuori dal recupero, e uno decide

La issue dice che «i soli consumatori sono il cascade di `DeleteKbDocument` e `AutoCreateAgentOnPdfReadyHandler`». Ce ne sono almeno **quattro**, e il quarto non è una lettura passiva:

| lettore | cosa fa | riferimento |
|---|---|---|
| `DeleteKbDocumentCommandHandler` | cascade: rimuove un id | `:84` |
| `AutoCreateAgentOnPdfReadyHandler` | append di un id | `:202` |
| `LinkUserAgentDocumentsCommandHandler` | **unisce** i documenti già collegati per non sovrascrivere quelli dell'admin | `:68` |
| `DegradedAgentService` | 🔴 **cancello di capacità**: `hasKbCards = agent.KbCardIds.Count > 0` decide cosa l'agente sa fare in modalità degradata | `:129` |

Più i DTO admin (`AgentDefinitionDto.KbCardIds`), la vista «used by» (`GetConsumingAgentsByDocumentIdQuery`, via containment JSONB) e due regole di validazione.

**Conseguenza sul piano**: ritirare `KbCardIds` non è una cancellazione. `DegradedAgentService` ha bisogno di una risposta — il segnale «questo agente ha documenti» va derivato dallo scope della domanda, non dall'agente — e `LinkUserAgentDocumentsCommandHandler` perde la sua ragione d'essere.

### 3. Ci sono DUE `AgentDefinitionId`, non uno

La DoD nomina `PrivateGame.AgentDefinitionId`. Esiste anche **`SharedGame.AgentDefinitionId`** (#4228), con **più** riscontri del primo:

| FK | riscontri | proprietà | eventi di dominio |
|---|---|---|---|
| `PrivateGame.AgentDefinitionId` | 27 | `UserLibrary/Domain/Entities/PrivateGame.cs:28` | `AgentLinkedToPrivateGameEvent`, `AgentUnlinkedFromPrivateGameEvent` |
| `SharedGame.AgentDefinitionId` | **39** | `SharedGameCatalog/Domain/Aggregates/SharedGame.cs:110` | `AgentLinkedToSharedGameEvent`, `AgentUnlinkedFromSharedGameEvent` |

Ed è il secondo che `DegradedAgentService` interroga per risalire dall'agente al gioco. Il ritiro dello scope deve coprirli **entrambi**, con i loro eventi.

> Nota su #4103: la sua DoD chiedeva di *documentare* l'asimmetria («i private games ce l'hanno: se la differenza è voluta, va detta»). Con un agente unico l'asimmetria **si annulla** — si ritirano entrambi i lati — ma l'asimmetria vera non era quella descritta: il `SharedGame` aveva la colonna, era la **tab admin** a chiamare tre rotte inesistenti.

## Conseguenze operative

### Ogni colonna esce in due consegne, non una

`AgentDefinition.Type` lo ha dimostrato: il primo tentativo droppava le colonne nella stessa consegna che smetteva di leggerle, e il **Migration Safety Gate** l'ha bloccato correttamente (runbook §8.2). La direttiva `-- safe:` esiste ma chiede una motivazione, e lì sarebbe stata falsa.

Il pattern da seguire per `GameId`, `KbCardIds`, `Strategy` e i due `AgentDefinitionId`:

1. **expand** — la colonna resta come **shadow property nullable**: nel modello, assente dall'aggregato, scritta a `NULL`, letta da nessuno. Se era `NOT NULL` senza default, questo passo è obbligatorio: smettere di mapparla farebbe fallire ogni `INSERT` con `23502`. Gli **indici** invece escono subito: un indice non fa parte di nessun contratto di codice.
2. **contract** — `DROP COLUMN`, con la direttiva e l'evidenza che nessuno legge più.

### L'assenza di un produttore non è l'assenza di dati

Ritirando `agent.created` (il suo unico emittente era `CreateUserAgentCommandHandler`) avevo rimosso i rami corrispondenti del feed attività. Errore: `GetActivityFeedQueryHandler` legge `domain_event_logs` su **90 giorni** (`RetentionDays`), quindi le righe storiche sopravvivono al ritiro per mesi.

Regola: ritirando un evento, controlla la **finestra** di ogni percorso di lettura. `GetEventTypeStatsQueryHandler` ha `WindowDays = 1`, quindi lì l'esposizione è di 24 ore ed è accettabile; il feed no.

### Il compilatore non è un inventario per le rotte

Le quattro rotte di creazione utente sono state ritirate con build verde, mentre **sedici** test le guidavano ancora via `PostAsJsonAsync` — una rotta in un test è una **stringa**. Il censimento che le trova è un grep sulle stringhe, in `src` e nei test, e lo stesso vale per i nomi di evento, i `data-testid` e i `page.route()` dei mock.

Il contrappeso è `RetiredRoutes` in `apps/api/tests/Api.Tests/Routing/EndpointContractTests.cs`: una **terza** categoria, deliberatamente distinta da `PendingBackendRoutes`, perché «pending» si legge come un invito a implementare.

## Ciclo di vita: non scavalcare lo stato perché «è uno solo»

`AgentDefinitionStatus` (`Draft`/`Testing`/`Published`) e le rotte `start-testing`/`publish`/`unpublish` restano. Con un'istanza unica il raggio d'esplosione è **l'intero prodotto**: un prompt di sistema sbagliato, o un `model` non instradabile salvato, degrada ogni risposta di ogni utente.

La pagina di configurazione deve editare un `Draft` e **promuovere** esplicitamente — non modificare a caldo l'istanza pubblicata.

## Gate che difendono la decisione

| gate | cosa rende inesprimibile |
|---|---|
| `AgentDefinitionShapeArchitectureTests` | il ritorno di un membro ritirato, **campi privati inclusi** — EF mappava `builder.Property<string>("_typeValue")`, quindi un controllo sul solo pubblico passerebbe mentre la colonna torna |
| `RetiredRoutes` (`EndpointContractTests`) | il rimontaggio di una delle quattro rotte di creazione |
| `AgentBuilderForm.models.test.tsx` | una tendina dei modelli che torni a offrire un id non instradabile — asserito **sul form**, perché un predicato corretto e un form che non lo chiama danno lo stesso verde |
| `Migration Safety Gate` | un `DROP COLUMN` senza motivazione verificabile |

L'elenco dei membri ritirati in `AgentDefinitionShapeArchitectureTests` contiene `Type`, `UpdateType`, `RaiseUserCreatedEvent` e i due campi privati. **Non** contiene ancora `GameId`/`KbCardIds`/`Strategy`: elencarli prima del loro ritiro renderebbe la suite rossa per lavoro non fatto.

## Stato del ritiro

**Fatto** — superfici FE di creazione utente e sezione `/agents` · quattro rotte di creazione + comandi/handler/validator · `AgentSetupPanel` e i metodi client morti · tab Agent di `/admin/shared-games/<id>` (chiude #4103) · tendina dei modelli da `/admin/ai-models` (chiude #4102) · `AgentDefinition.Type` + value object + evento `agent.created` e la sua catena, con la migrazione di **expand**.

**Aperto** — `DROP COLUMN type_value, type_description` (contract) · `GameId`, `KbCardIds`, `Strategy`, ciascuno expand → contract · i **due** `AgentDefinitionId` e le rotte `link-agent`/`unlink-agent` · contratto dei tier (`MaxAgents` e il `var agents = 0;` cablato) · `/admin/agents/definitions/create` come pagina Draft → promozione · i due `AgentSelector` + `ChatEntryOrchestrator`, che **funzionano** (`/games/{id}/agents` è montata) e sono quindi un ridisegno deliberato, non una rottura.
