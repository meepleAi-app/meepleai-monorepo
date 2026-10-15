# Assistente in partita: un agente con strumenti — design

**Data**: 2026-10-11, rivista il 2026-10-12 · **Origine**: `/sc:brainstorm` del 2026-10-11 · **Stato**: spec rivista dopo una review avversariale (§0). A0 consegnata (#4247); spike di A1 concluso, con l'esito in §7.1: con il prompt v1 nessun modello superava tutte le trappole; il prompt v2 di #4256 supera il gate. Prima di pubblicare l'assistente va chiusa #4270.
**Base misurata**: `origin/main-dev` a `afd0cdbb3` (#4233).
**Contesto**: [ADR-094](../../for-claude/architecture/adr/adr-094-single-system-agent.md) (un solo agente di sistema) · [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) e il [piano delle fette](../plans/2026-10-10-adr-095-slices-plan.md) · [ADR-090](../../for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md) (`KnowledgeBase` possiede la risposta ancorata) · [ADR-089](../../for-claude/architecture/adr/adr-089-session-scoring-ssot.md) (`LiveGameSession.RoundScores` è la fonte unica del punteggio in partita) · [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md) · [`user-use-cases.md`](../user-use-cases.md) (SES-07, SES-17) · [correzione dei bloccanti](./2026-10-10-user-use-case-blockers-fix-design.md) (#4156, decisione D3 sulla scheda Score) · [brief del 2026-10-10](./2026-10-10-single-agent-ai-architecture-brief.md).

Al 2026-10-12 questi documenti esistono solo come file non committati del checkout principale, quindi li cito senza link:
- ADR-092 (l'Arbitro Python diventa un validatore puro): `docs/for-claude/architecture/adr/adr-092-arbiter-ownership-python-validator.md`;
- il report Azure: `docs/for-developers/research/2026-10-08-azure-ai-200-provider-switch-research.md`.

**Convenzione**: nessun conteggio volatile in prosa; dove serve una misura c'è il comando che la produce. Prezzi e comportamenti dei provider si verificano alla fonte prima di usarli. I percorsi sono relativi a `apps/api/src/Api`, salvo quelli che cominciano con `apps/`, `docs/` o `tests/`.

---

## 0. Revisione del 2026-10-12

Una review avversariale della prima stesura, fatta contro il codice, ha trovato tre bloccanti. Questa versione li risolve così:

| Rilievo | Cosa diceva la prima stesura | Correzione |
|---|---|---|
| **B3** — il gateway non si riusa da sotto | Un middleware M.E.AI del tetto, *sotto* il ciclo, attaccato al provider | La logica di ogni tentativo (ammissione e tetto, degrado, scelta del client con circuit breaker, redazione PII, costo) è privata in `HybridLlmService`. Il client M.E.AI va quindi **sopra** il gateway: un metodo di chat con strumenti su `ILlmService` riusa quella logica a ogni giro (§3) |
| **B1** — la scheda Score non mostra `RoundScores` | La verifica nel browser dava per scontato di vedere il punteggio confermato | La scheda Score legge lo scoring polimorfico, che ADR-089 dice «mai collegato a dati reali». #4156 la riporta su `RoundScores` (decisione D3), quindi la verifica nel browser dipende da #4156 (§8) |
| **B2** — non esiste un percorso di pubblicazione | Il controllo del tool calling «alla pubblicazione» | `AgentProfile.PublishDraft()` non ha comandi né endpoint (la pagina admin è la fetta 6c). Il controllo diventa un invariante di dominio di `PublishDraft`, e l'abilitazione del pilota passa da una migrazione di dati, come le versioni già pubblicate (§3.2) |

Rilievi maggiori corretti nel testo: gli strumenti non ricevono `IMediator` (M2, §3.2); il controllo «solo punti» non è verificabile e sparisce (M3, §5.1); round e semantica del valore sono definiti (M4, §5.1-5.3); le affermazioni sul copyright sono corrette e la risposta finale passa da `IGroundedAnswerService.FinalizeAsync` (M5, §4.2); proprietario e stati della sessione hanno codici d'errore distinti (M6, §5.3); dipendenze e stato della fetta 5 aggiornati (M7, §8); l'ADR è il **097**, perché il 096 è occupato da `adr-096-rag-ownership-gate.md` (M8).

---

## 1. Perché

L'utente ha chiesto se esista un'architettura AI più adatta di quella in corso. Le motivazioni sono due:

1. un assistente che **agisce**, non uno che risponde e basta;
2. **imparare i pattern moderni**: tool calling, middleware, valutazione delle traiettorie, anche come esercizio per AI-200.

La risposta non sostituisce ADR-095: lo usa come base. Il gateway con il tetto, il catalogo e il profilo versionato servono **di più** in un ciclo agentico, perché un turno con strumenti moltiplica le chiamate al modello. Cambia invece il ruolo della pipeline di risposta: per l'assistente non è più *la* risposta, ma uno degli strumenti.

### Decisioni del brainstorming

| Domanda | Scelta |
|---|---|
| Primo pilota | **Assistente in partita** nella sessione live |
| Potere sui dati | **Propone, l'host conferma.** Il modello non scrive mai: la conferma passa dai comandi esistenti |
| Chi interagisce | **Solo l'host, da un dispositivo** |
| Dove vive il ciclo | **.NET, con `Microsoft.Extensions.AI` sopra il gateway** (§3) |
| Cache per l'assistente | Nessuna cache delle risposte; **ordine dei messaggi** pensato per la cache del prompt del provider |
| Cache delle risposte della pipeline | **Solo semantica**, con la versione del profilo nella chiave e una soglia tarata; la cache esatta esce (sottoprogetto a parte, §10.1) |
| Database delle chat | **Postgres** per il prodotto; un adapter Cosmos DB di `IChatThreadRepository` come esercizio sulla linea `azure/main` (§10.2) |
| Punteggi nel pilota | Valori interi sulle **dimensioni configurate** della sessione (`SessionScoringConfig`), cioè ciò che il wizard offre |

### Cosa non è

- **Non è un sistema multi-agente.** È un solo agente con degli strumenti, compatibile con ADR-094.
- **Non è un framework generale di agenti.** È un pilota su un caso solo.
- **Non usa LangGraph né `orchestration-service`.** Il motivo è in §3.3.

---

## 2. Stato di partenza

| # | Misura | Evidenza |
|---|---|---|
| S1 | Nessun tool calling. `ILlmService` accetta un prompt di sistema e un prompt utente; nessun client invia `tools` o legge `tool_calls`, anche se tutti e tre costruiscono già una lista `messages` | `Services/ILlmService.cs`; `Services/LlmClients/{OpenRouter,DeepSeek}LlmClient.cs` (`chat/completions`), `OllamaLlmClient.cs` (`api/chat`) |
| S2 | Nessun framework AI nel progetto; il progetto referenzia già pacchetti `Microsoft.Extensions.*` 10.0.x su `net9.0` | `Api.csproj` |
| S3 | Il gateway applica il tetto prima di ogni chiamata, ma la logica di ogni tentativo è privata | `HybridLlmService` (`AdmitAsync`/`CompleteAdmission`, `IsCheaperReserveAsync`, `RecordSuccess`/`RecordFailure`, redazione PII per OpenRouter) |
| S4 | Un precursore delle proposte registra da solo quando la confidenza dichiarata dal modello supera 0,8 | `ParseAndRecordScoreCommandHandler.cs:125`, rotta `/live-sessions/{sessionId}/scores/parse` |
| S5 | Esiste già un flusso «proposta → conferma dell'host» per i punteggi proposti dagli ospiti: `ProposeScoreCommand` notifica via SignalR, `ConfirmScoreProposalCommand` verifica l'host (`CreatedByUserId`), chiama `RecordScore`, salva e trasmette | `GameManagement/Application/Commands/Session/`; endpoint in `SessionInviteEndpoints.cs` |
| S6 | Le card di proposta del frontend non sono montate | `components/session/ScoreProposalCard.tsx`, `components/game-night/ScoreAssistant.tsx`; SES-17 |
| S7 | La scheda Score della pagina live legge lo scoring polimorfico, non `RoundScores` | `ScoreTabContent.tsx`; `SessionLiveView.tsx` («deferred to Fase 2»); ADR-089 |
| S8 | `orchestration-service` (LangGraph) non è collegato: nessuna rotta invia `TutorQueryCommand`, e il servizio chiama rotte .NET che non esistono | `infra/docker-compose.yml` lo dichiara «publish-only» |
| S9 | I `tools` salvati su `AgentDefinition` non sono mai eseguiti; il motore a plugin di `Domain/Plugins` serve il costruttore di pipeline personalizzate, ma la sua generazione è simulata | S1; `GenerationLlmPlugin.cs:142` |

S9 è un avvertimento: qui le astrazioni agentiche costruite senza un consumatore reale non sono mai arrivate a una chiamata vera. Il pilota parte da un consumatore (l'host al tavolo) e da una misura (§7).

---

## 3. Architettura e confini

```
POST /live-sessions/{id}/assistant (SSE) ──IMediator──> AskSessionAssistantCommand        [KnowledgeBase]
                                                          │
   FunctionInvokingChatClient (max N giri)                ▼
     → OpenTelemetryChatClient
       → GatewayChatClient : IChatClient   ── nessun HTTP, solo ILlmService
         → ILlmService.ChatAsync            ── HybridLlmService, a ogni giro:
              ammissione e tetto → degrado → client con circuit breaker
              → redazione PII → ILlmClient.ChatAsync (HTTP, Services/LlmClients/) → costo → chiusura

   strumenti (AIFunction):
     lettura   stato_partita · cerca_regole        → ISessionAssistantReader (porta di sola lettura)
     proposta  proponi_punteggio · proponi_turno   → IAssistantProposalRepository (salva, non scrive la partita)
```

### 3.1 Perché il client M.E.AI sta sopra il gateway

La logica di ogni tentativo è in `HybridLlmService` ed è privata (S3). Un client M.E.AI attaccato al provider dovrebbe copiarla, insieme alla risoluzione delle credenziali a ogni chiamata (`IProviderCredentialResolver`) e alle protezioni dei test (`PaidAiHostGuardExtensions`, che bloccano solo i client creati dalla factory). Mettendolo **sopra**:

- ogni giro del ciclo passa da tetto, degrado, fallback, circuit breaker, redazione PII e registro costi, **per costruzione**;
- l'HTTP resta in `Services/LlmClients/`, quindi `LlmProviderEgressArchitectureTests` non cambia;
- serve solo il pacchetto `Microsoft.Extensions.AI`, senza l'SDK OpenAI;
- i test che sostituiscono `ILlmService` con un doppio continuano a coprire il nuovo percorso.

Il prezzo è un refactor di `HybridLlmService`: la sequenza di un tentativo va estratta da `GenerateAnswerAsync` (il percorso della 5b, con modello e riserva decisi dal chiamante) in un metodo comune a quello e al nuovo `ChatAsync`, a comportamento invariato e con test di caratterizzazione prima.

### 3.2 Decisioni

| Elemento | Decisione |
|---|---|
| Proprietario | `KnowledgeBase/Application/SessionAssistant/`. La risposta è di `KnowledgeBase` (ADR-090) |
| Gateway | `ILlmService.ChatAsync(LlmChatRequest)`: messaggi (sistema, utente, assistente, strumento), definizioni degli strumenti, temperatura e lunghezza massima, `RequestSource`. Restituisce testo e/o chiamate a strumenti, con l'uso dei token. `ILlmClient` riceve lo stesso metodo, implementato da OpenRouter e DeepSeek (formato OpenAI `tools`/`tool_calls`) e da Ollama (`api/chat` con `tools`) |
| Facciata M.E.AI | `GatewayChatClient : IChatClient` traduce i tipi M.E.AI (`ChatMessage`, `FunctionCallContent`, `FunctionResultContent`) da e verso `LlmChatRequest`. Non dipende da `ILlmClient` né da `HttpClient` |
| Strumenti | **Non ricevono `IMediator`.** Ricevono `ISessionAssistantReader`, una porta di sola lettura implementata in Infrastructure con query MediatR (`GetLiveSessionQuery`, `GetTurnPhasesQuery`, `GetGameMemoryQuery`) e con `IPlayerNameResolutionService` (stesso precedente di `ParseAndRecordScoreCommandHandler`), e `IAssistantProposalRepository`. Alcuni comandi di scrittura di GameManagement implementano `IRequest` e non `ICommand`, quindi un divieto su `ICommand` non basterebbe |
| Profilo | Un parametro opzionale in coda ad `AgentProfileContentJson.Document` e una proprietà in `AgentProfileContent`, «Assistente in partita»: abilitato, prompt di sistema per lingua, numero massimo di giri, scadenza per turno, uso della riserva del profilo (spento finché la riserva non supera il gate, §7.1), temperatura e lunghezza massima della risposta (i valori misurati dallo spike, 0,2 e 600: quelli del resto del profilo sono pensati per le risposte sulle regole). Entra in `GetEqualityComponents`; assente vale disabilitato; un assistente abilitato deve avere un prompt per la lingua di riserva del profilo; le righe campo → effetto della sezione stanno nei test del turno, perché l'effetto si osserva solo sul suo endpoint |
| Catalogo | `SupportsToolCalling` dentro `SettingsJson` di `AiModelConfiguration` (jsonb, nessuna migrazione), esposto a `KnowledgeBase` dalla porta `IToolCallingModelCatalog`. Lo dichiara il seed, solo per i modelli misurati (§7.1), anche sulle righe già presenti; la creazione di un modello dall'admin lo ignora |
| Pubblicazione | `AgentProfile.PublishDraft` e `RollbackTo` ricevono l'insieme dei modelli con tool calling e rifiutano (`409 assistant_model_without_tools`) un contenuto con l'assistente abilitato se il modello principale del profilo non è nell'insieme, o se l'assistente usa la riserva e la riserva non c'è o non è nell'insieme. Il turno ricontrolla la stessa regola a ogni richiesta, perché la migrazione di dati qui sotto scavalca l'aggregato. Finché non esiste la pagina admin (fetta 6c), l'abilitazione del pilota è una migrazione di dati che pubblica una nuova versione, come `20261011061616_RepublishAgentProfileWithCurrentModels` |
| `RequestSource` | Un valore nuovo, `SessionAssistant`, mappato su `Answer` in `LlmBudgetClassification.For`. `RequestSourceTests` conta i valori dell'enum, quindi va aggiornato |
| ADR | **ADR-097**: `Microsoft.Extensions.AI` come facciata del gateway per il tool calling, sopra `ILlmService`. Chiude la domanda aperta n. 4 di ADR-095 |

**Test architetturali** (falliscono la suite, non la build):
- le classi di `SessionAssistant/Tools/` non dipendono da `IMediator`, `ISender` né da tipi di GameManagement;
- `GatewayChatClient` non dipende da `ILlmClient` né da `HttpClient`;
- la catena dell'assistente non contiene un client di caching (`DistributedCachingChatClient`).

### 3.3 Approcci scartati

| Approccio | Perché no |
|---|---|
| Client M.E.AI sotto il gateway, attaccato al provider (prima stesura) | Duplicherebbe la logica privata di ogni tentativo, la risoluzione delle credenziali e le protezioni dei test (§3.1) |
| Python con LangGraph in `orchestration-service` | Il servizio non gira su staging e nessuno lo osserva (S8). Le chiamate LLM dovrebbero passare dal gateway .NET (ADR-095 D4), quindi servirebbe un proxy. Lo stato della sessione e l'autorizzazione andrebbero letti attraverso la rete. L'apprendimento Python ha già un consumatore in ADR-092 |
| Un ciclo scritto a mano, senza M.E.AI | Reinventa l'invocazione degli strumenti e insegna meno; il repo ha già astrazioni fatte in casa senza consumatori (S9) |
| Un profilo separato per l'assistente | Allontanerebbe da «un agente» (ADR-094). Il prezzo della scelta fatta è che le versioni dell'assistente e quelle della chat sulle regole si pubblicano insieme |

---

## 4. Cache

### 4.1 Per l'assistente

1. **Nessuna cache delle risposte, né esatta né semantica.** La risposta dipende dallo stato vivo e dalla conversazione. `UseDistributedCache()` di M.E.AI calcola la chiave sui messaggi della richiesta, che **non contengono lo stato**: lo stato arriva dopo, dai risultati degli strumenti. Una hit salterebbe gli strumenti e risponderebbe con lo stato di dieci minuti prima. Lo vieta un test architetturale (§3.2).
2. **Ordine dei messaggi pensato per la cache del prompt del provider.** Ogni giro rimanda tutto il contesto, quindi i messaggi vanno dal più stabile al più volatile:
   1. prompt di sistema della versione pubblicata;
   2. definizioni degli strumenti;
   3. contesto statico della sessione: gioco, giocatori, dimensioni del punteggio, house rules;
   4. storia;
   5. domanda.

   I punteggi e il turno arrivano **solo** dai risultati degli strumenti, in coda.
3. **La contabilità dei token in cache è fuori dal pilota.** `ILlmCostCalculator.CalculateCost` non ha un parametro per i token in cache, `LlmModelPricing` non porta il prezzo della cache, e registrarli richiede una migrazione di `llm_cost_logs`. Diventa un lavoro a sé (§11). Fino ad allora il costo registrato resta prudente, cioè sovrastimato quando il provider usa la cache.
4. **I risultati di `cerca_regole` non si mettono in cache.** La chiave dovrebbe contenere utente e tier copyright, perché il tier si risolve per utente, e non esiste una «versione dell'indice». Alla scala del pilota non vale la complessità.

### 4.2 Copyright e accesso per `cerca_regole`

`cerca_regole` restituisce **passaggi e citazioni**, senza una chiamata LLM annidata: la risposta finale la compone l'agente, e si risparmia una chiamata per ogni domanda sulle regole. Quattro vincoli:

- **Accesso**: lo strumento chiama `IRagAccessService.EnsureRagAccessAsync` con l'identità di chi chiede, come fa la pipeline (`AnswerPipeline.cs:127`). `AnswerRetrieval` non controlla l'accesso, e `RagQueryIdentityArchitectureTests` richiede l'identità del chiamante.
- **Tier delle citazioni**: si risolvono con `ICopyrightTierResolver`. Il testo verbatim resta per ogni tier; solo regioni e offset sono limitati al tier Full (`AnswerPipeline.cs:468`, `:482`). Oggi questo passo è privato nella pipeline: l'emendamento E1 (§9) chiede di estrarlo.
- **Risposta finale**: passa da `IGroundedAnswerService.FinalizeAsync(responseText, resolvedCitations, userQuestion, agentLanguage)`, che fa il controllo dei leak, la parafrasi e l'annullamento delle citazioni secondo il tier. Le citazioni sono quelle raccolte da tutte le chiamate a `cerca_regole` del turno.
- **Sessione senza gioco**: `GameId` può essere nullo; in quel caso `cerca_regole` non viene offerto al modello.

### 4.3 Degrado (ADR-095 D6)

L'assistente non si può servire dalla cache. Oltre il 100% del tetto risponde «assistente in pausa» e l'inserimento manuale resta disponibile. Sotto la soglia di degrado (80% di default, configurabile) il gateway passa al modello di riserva **del profilo**, e solo se costa meno del principale (`IsCheaperReserveAsync`). Vale per l'assistente solo quando la sua sezione del profilo usa la riserva, e in quel caso la pubblicazione richiede che anche la riserva supporti il tool calling (§3.2). Dopo lo spike la riserva è **spenta** (§7.1): un turno non scende a un modello che il set di scenari non ha misurato. L'assistente resta sul modello principale fino al 100% del tetto, e se il provider non risponde il turno fallisce con «assistente non disponibile».

---

## 5. Strumenti, proposte e conferma

### 5.1 Strumenti

Il pilota ne ha pochi, di proposito: con i modelli economici meno strumenti significano scelte più affidabili. Lo spike (§7.1) lo verifica.

| Strumento | Tipo | Cosa fa | Limiti voluti |
|---|---|---|---|
| `stato_partita()` | lettura | Giocatori, giocatore di turno (`CurrentTurnPlayerId`), indice di turno (`CurrentTurnIndex`), **round corrente** derivato, fase (`TurnPhasesDto`), stato della sessione, dimensioni (`ScoringConfig`) e punteggi per giocatore, round e dimensione (`RoundScores`) | **Nessun parametro.** L'id della sessione lo lega il server alla richiesta: il modello non può sceglierne un'altra |
| `cerca_regole(domanda)` | lettura | Passaggi e citazioni per il gioco della sessione, nella lingua dell'host (§4.2) | Offerto solo se la sessione ha un gioco. Arriva con la fetta A5 (§8) |
| `proponi_punteggio(giocatore, punti, dimensione?, round?)` | proposta | Salva una proposta di **punti guadagnati** da un giocatore | Nome ambiguo o sconosciuto: errore tipizzato al modello («Marco R. o Marco B.?»), che lo chiede all'host. La dimensione deve esistere in `ScoringConfig`; se ce n'è una sola si può omettere. Round omesso = round corrente. Punti interi, entro ±100.000: il limite vale per la proposta e per la correzione dell'host, e la cella di partenza sta entro ±1.000.000.000, così la somma non può fare il giro dell'intero |
| `proponi_turno()` | proposta | Proposta di passare il turno | La fase segue lo stesso schema, ma è fuori dal pilota |

Le **house rules** non sono uno strumento: vanno nel contesto statico (§4.1), lette con `GetGameMemoryQuery(GameId, OwnerId)`.

**Round.** `LiveGameSession` non ha un round: ha `CurrentTurnIndex`, che vale 0 prima dell'avvio, 1 con `Start()` e cresce a ogni turno di ogni giocatore. Il precursore S4 usa l'indice di turno come round, quindi ogni passaggio di turno apre una nuova cella. L'assistente usa invece un **round derivato**: `(CurrentTurnIndex − 1) / n + 1` per un indice ≥ 1, e 1 prima dell'avvio, dove `n` sono i giocatori attivi non spettatori, cioè quelli fra cui gira il turno. È un giro completo del tavolo, coerente con `GetCurrentTurnPlayerId`, che usa `(CurrentTurnIndex − 1) % n`. Dal 2026-10-14 è anche il round predefinito, modificabile dall'host, dell'editor della scheda Score di #4156, che lavora su «giocatore × dimensione × round» (decisione D6; §13). Dalla versione 4 il set di scenari usa lo stesso indice a base 1. **Limite noto**: il dominio conta sull'ordine di turno impostato con `SetTurnOrder`, che `LiveSessionDto` non espone; se l'host imposta un ordine che esclude un giocatore attivo, il round derivato diverge.

**Semantica del valore.** `RecordScore` sostituisce la cella (giocatore, round, dimensione). L'assistente propone invece un **incremento**: «Marco fa 5 punti» vuol dire +5 alla cella del round. Il valore scritto alla conferma è il valore della cella più l'incremento. Se l'host dice un totale («Marco è a 30»), il modello calcola l'incremento dallo stato e la card mostra il totale risultante, che l'host verifica.

### 5.2 La proposta

`AssistantProposal`, un aggregato del `KnowledgeBase`: è l'uscita dell'assistente, come la risposta e le citazioni. Il nome evita la collisione con `ProposeScoreCommand` (S5). Tabella `assistant_proposals`:

| Campo | Contenuto |
|---|---|
| `id`, `live_session_id`, `chat_thread_id`, `message_id` | identità e provenienza |
| `kind` | `Score` · `AdvanceTurn` |
| `payload` (jsonb) | per `Score`: giocatore, dimensione, round, incremento |
| `expected` (jsonb) | per `Score`: il valore della cella al momento della proposta; per `AdvanceTurn`: `CurrentTurnIndex` |
| `status` | `Pending` → `Applying` → `Accepted` · `Edited` · `Failed`; da `Pending` anche `Rejected` · `Expired` · `Stale`; `Stale` anche da `Applying`, perché lo stato atteso si controlla dopo la prenotazione (§5.3) |
| `confirmed_payload` (jsonb, nullable) | l'incremento confermato, quando l'host lo corregge |
| `failure_code` (nullable) | il codice dell'errore di dominio, per `Failed` |
| `profile_version_id` | la versione del profilo che l'ha prodotta |
| `created_at`, `resolved_at`, `resolved_by` | tempi e autore |
| `xmin` | concorrenza ottimistica, come gli altri aggregati (ADR-060) |

**Scadenza**: una proposta ancora `Pending` diventa `Expired` quando inizia il turno successivo dell'assistente, con un solo `UPDATE` eseguito subito: così una conferma che arriva nello stesso istante non fa fallire il turno, trova la proposta non più `Pending`. Vale anche se quel turno poi fallisce: chiedere di nuovo supera le card di prima. È una scelta, presa il 2026-10-13 dopo la review di A3a (#4238). Sul turno riuscito la card scadrebbe comunque; e nel dubbio una card non deve essere confermabile, perché se la nuova domanda era una correzione («no, erano 6») farla sopravvivere lascerebbe viva proprio la card da sostituire. Farle scadere solo dopo un ciclo riuscito aiuterebbe in un caso solo (domanda non correlata, turno fallito, nessuna card nuova), terrebbe confermabili le card vecchie mentre gira il turno che le corregge, mostrerebbe al modello proposte «in attesa» in conversazioni con storia che non sono ancora misurate, e porterebbe la regola del client da una riga a tre casi. Il danno è limitato: al più le card di un turno per ogni episodio di guasto, perché un turno fallito non ne crea altre. Va riaperta solo insieme alla regola generale, se nel pilota l'host detta più punteggi di fila senza confermarli: lì perde ogni card alla domanda successiva anche quando tutto funziona. Alla chiusura della sessione le proposte rimaste `Pending` non si toccano: una conferma tardiva viene comunque rifiutata dal dominio (`session_state_invalid`), e la pulizia arriva con le metriche della fetta A6.

### 5.3 Conferma: «prima prenota, poi applica»

`POST /live-sessions/{id}/assistant/proposals/{pid}/confirm`, con l'eventuale incremento corretto, invia `ConfirmAssistantProposalCommand`. È **l'unico ponte verso le scritture**, ed è avviato da una persona.

1. **Proprietario**: chi conferma deve essere `CreatedByUserId` della sessione. Il filtro degli endpoint live controlla solo la partecipazione (`EndpointFilterExtensions.cs:104`), quindi il controllo sta nell'handler. Altrimenti `403`.
2. **Prenotazione**: la proposta passa da `Pending` ad `Applying` e si salva, con `xmin`. Un secondo tap perde sulla concorrenza, non scrive, e riceve lo stato attuale: `200` se la proposta si sta applicando o è stata applicata (`Applying`, `Accepted`, `Edited`), `409 proposal_not_pending` se è stata chiusa in altro modo (`Rejected`, `Expired`, `Stale`, `Failed`). È l'idempotenza.
3. **Stato atteso**: se la cella o l'indice di turno sono diversi da `expected`, la proposta diventa `Stale` e la risposta è `409` con codice `proposal_stale`. Il controllo avviene due volte: qui, per non avviare una scrittura inutile, e al passo 4 dentro il comando della sessione, che è quello che decide.
4. **Applicazione**: per `Score` si invia `ConfirmScoreProposalCommand` (S5) con il valore della cella più l'incremento: verifica di nuovo l'host, scrive con `RecordScore`, salva e trasmette ai partecipanti via SignalR. Per `AdvanceTurn` si invia `AdvanceLiveSessionTurnCommand`. I due comandi ricevono lo stato su cui la proposta è nata (`ExpectedCurrentValue`, `ExpectedTurnIndex`) e lo ricontrollano sull'aggregato che stanno per salvare: il salvataggio con `xmin` rende il controllo atomico con la scrittura. Il controllo del passo 3 e la scrittura sono due letture distinte, e fra le due può passare un'altra conferma: senza questo secondo controllo, due proposte di turno uguali confermate nello stesso istante farebbero passare il turno due volte, e di due incrementi sulla stessa cella ne resterebbe uno. Se lo stato non è più quello atteso il comando non scrive (`score_changed`, `turn_changed`) e la proposta diventa `Stale`. Chi chiama quei comandi senza lo stato atteso li trova come prima.
5. **Esito**: `Accepted` o `Edited`. Se il dominio rifiuta, la proposta diventa `Failed` con il codice e la risposta è `409` con lo stesso codice, distinto da `proposal_stale`: `session_state_invalid` quando è lo stato della sessione a non permettere la scrittura (`RecordScore` la vuole `InProgress` o `Paused`, `AdvanceTurn` la vuole `InProgress`), quando la sessione non esiste più, o quando qualcun altro l'ha modificata nello stesso istante; `proposal_not_applicable` quando la proposta non vale più per questa partita (il giocatore è uscito, la dimensione non è più abilitata). In tutti questi casi nulla è stato scritto, e l'host richiede. Se dopo la prenotazione non si riesce a leggere lo stato della partita, la proposta diventa `Failed` con `session_state_unavailable` e l'errore risale.

Il comando di dominio salva da solo, quindi proposta e partita non stanno nella stessa transazione. La prenotazione garantisce comunque che una proposta non si applichi due volte. Il comando di dominio gira in uno scope suo, così un suo salvataggio fallito non resta nel change tracker della proposta. Dopo la prenotazione la conferma non segue più l'annullamento della richiesta: un host che chiude la pagina non lascia una proposta a metà. La proposta resta `Applying` in tre casi, tutti rari: il processo cade fra il passo 4 e il 5; l'applicazione fallisce per un motivo che non è uno dei rifiuti del passo 5 (un errore del database mentre il comando carica la sessione, un difetto), perché lì non si sa se la partita è stata scritta, e dire «fallita» inviterebbe l'host a ripetere un punteggio già registrato; il salvataggio dell'esito fallisce. In quei casi una nuova conferma riceve `200` con lo stato `Applying`, il rifiuto riceve `409`, e la scadenza di inizio turno non tocca la riga. Il turno successivo dell'assistente lo segnala nei log (la proposta non ha un istante di prenotazione, quindi il controllo non usa una soglia di tempo); per chiuderla oggi serve un intervento sul database.

Il rifiuto è `POST …/proposals/{pid}/reject`, con le stesse verifiche di proprietario e di stato. Un secondo tap sul rifiuto riceve la proposta già rifiutata, come un secondo tap sulla conferma riceve l'esito della prima: anche quando porta una correzione diversa.

---

## 6. Il turno, i limiti e gli errori

### 6.1 Risposta bufferizzata

La risposta finale si bufferizza, perché passa da `FinalizeAsync` **prima** di arrivare all'host (§4.2). È una scelta di questo design: la chat di sessione attuale invia i token prima di `FinalizeAsync`. Le risposte al tavolo sono brevi, quindi la latenza aggiunta è bassa; in cambio sparisce il problema del fallback a metà flusso, e il ciclo usa la chiamata non in streaming di M.E.AI.

### 6.2 Un turno

1. **`POST /live-sessions/{id}/assistant`**. Si verifica nell'handler che l'utente sia `CreatedByUserId`, che la sessione non sia `Completed` (anche `Created`: `MoveToSetup` non ha chiamanti, quindi una sessione passa da `Created` a `InProgress`, e senza `Created` l'assistente non risponderebbe mai prima dell'avvio), e che l'assistente sia abilitato nella versione pubblicata. Gli strumenti di proposta rifiutano con un errore tipizzato quando lo stato non permette la scrittura.
2. **Messaggi**, nell'ordine di §4.1. Della storia entrano gli ultimi messaggi del `ChatThread` della sessione. `ChatThread` riceve un `LiveSessionId` nullable (migrazione additiva) e il thread dell'assistente nasce **senza** `GameId`: la chiave esterna del gioco punta al catalogo condiviso, e una sessione può essere su un gioco privato. Quel thread non compare fra le chat dell'utente (elenco, storico, cruscotto e attività recente); resta nell'export dei suoi dati. Il legame fra un messaggio e le sue proposte sta nella proposta (`message_id`): le `parts` in `MessagesJson` arrivano con le citazioni, nella fetta A5. Dei turni passati si rimandano solo il testo e gli esiti delle proposte, non i risultati degli strumenti. Il testo torna solo se l'ha scritto il modello (#4397). Quello che il turno scrive al suo posto (un guasto, la scadenza, una risposta vuota) porta un codice nel messaggio (`FallbackCode`, un campo del JSON dei messaggi, senza migrazione), e resta nella conversazione che vede l'host ma non nella storia. Se il turno fallito aveva già creato proposte, i loro esiti tornano con la domanda; senza proposte il turno fallito sparisce, domanda compresa. La lingua del turno è quella del prompt usato: se la sezione non ha un prompt nella lingua chiesta, contesto statico e testi di ripiego seguono la lingua di riserva, come il prompt.
3. **Ciclo** con al massimo N giri con strumenti, preso dal profilo, più una chiamata finale senza strumenti che obbliga il modello a rispondere: un turno costa al più N+1 chiamate (semantica di `FunctionInvokingChatClient`, fissata da un test in A0). Ogni chiamata passa dal gateway con la versione del profilo e `RequestSource.SessionAssistant`; l'id della sessione arriva con le metriche della fetta A6, a cui serve per il costo per partita, perché `LlmChatRequest` non ha il campo.
4. **Eventi SSE**, nella convenzione degli endpoint SSE esistenti (`AgentSessionEndpoints.cs`): eventi `data:` senza nome, con un campo `type`:
   - `status` all'avvio del turno e poi a ogni strumento che parte, con il suo nome («leggo i punteggi…» lo compone il client). Il primo `status` dice anche che le proposte ancora `Pending` dei turni precedenti sono scadute (§5.2), comunque finisca il turno: da lì il client le mostra come scadute;
   - `proposal` per ogni card, **dopo il salvataggio**: una card mostrata prima si potrebbe confermare quando la proposta non è ancora nel database;
   - `answer`, con il testo (e, dalla fetta A5, le citazioni dopo `FinalizeAsync`);
   - `done`, con `responseId` e versione del profilo;
   - `error`, con un codice, al posto di `done`. Con `budget_paused`, `turn_timeout` e `assistant_unavailable` per un rifiuto del provider il ciclo si è fermato a metà: messaggi e proposte già create sono stati salvati, le proposte sono state inviate e `answer` porta il testo di ripiego. Un errore imprevisto dà `assistant_unavailable` da solo, senza `answer`: di quel turno non è stato salvato nulla.

   I controlli del passo 1 avvengono prima del primo evento: se falliscono la risposta è un errore HTTP in JSON (`400` per una domanda vuota o troppo lunga, `403`, `404`, `409` con `assistant_disabled`, `assistant_model_without_tools` o `session_state_invalid`), non uno stream.
5. **Salvataggio**: i due messaggi del turno e le proposte, in un solo salvataggio. Le proposte citano il messaggio dell'assistente con `message_id`; le `parts` (citazioni) arrivano con la fetta A5. Le righe di costo le scrive il gateway.

**Quota per piano**: fuori dal pilota. Arriva con la fetta 5g di ADR-095, non ancora consegnata; fino ad allora vale il tetto globale. L'host del pilota è uno solo.

### 6.3 Limiti ed errori

| Situazione | Comportamento |
|---|---|
| Giri esauriti senza una risposta | Messaggio esplicito («non sono riuscito, riprova o segna a mano») e metrica `loop_exhausted`. Le proposte già create restano valide: richiedono comunque la conferma |
| Argomenti non validi | Lo strumento restituisce al modello un errore tipizzato, non un'eccezione. Il giro conta |
| Eccezione dentro uno strumento | `IncludeDetailedErrors = false`: `ex.Message` non arriva né al modello né all'utente |
| **Il modello dichiara un'azione senza aver creato la proposta** («ho segnato 5 punti a Marco») | È il rischio più insidioso, perché l'host crede che il punteggio sia registrato. Il prompt vieta di dichiarare azioni compiute. Un controllo finale cerca affermazioni di questo tipo in un turno senza proposte, aggiunge un avviso e registra la metrica `claimed_action_without_proposal`. Il caso è nel set di valutazione. Lo spike ha mostrato una seconda forma, che il controllo copre allo stesso modo: il modello dice di aver **preparato una proposta** che non ha creato (§7.1) |
| Il tetto rifiuta un giro a metà ciclo | Il ciclo si ferma: `error budget_paused` e «assistente in pausa» |
| Il provider fallisce | Il gateway applica fallback e circuit breaker a ogni giro (§3.1) |
| Un secondo turno mentre il primo è in corso | Il frontend disabilita l'invio durante un turno. Il repo non ha un lock distribuito, e il pilota non ne aggiunge uno: anche due turni concorrenti producono solo proposte, che la conferma protegge |
| Tempo | Una scadenza per turno, configurabile nel profilo. Il valore iniziale è 15 s, dal p95 misurato nello spike (§7.1) |

---

## 7. Valutazione, test e criteri di successo

### 7.1 Spike: prima di costruire il resto

Domanda: **i modelli disponibili chiamano gli strumenti in modo affidabile?**

- **Set di scenari al tavolo**, in italiano e in inglese, in `tests/llm-eval/session-assistant/`. Ogni scenario è uno stato della partita in JSON, una frase dell'host e la **traiettoria attesa** (strumenti chiamati, proposte attese o nessuna proposta). Categorie:
  - punteggi, inclusi nomi ambigui, giocatori assenti e totali dichiarati;
  - turno;
  - domande sullo stato, che **non** devono generare proposte;
  - domande sulle regole, che devono chiamare `cerca_regole`;
  - richieste miste;
  - trappole: «segna tu», dimensione inesistente, chiacchiere.

  Dimensione del set: `node -e "console.log(require('./tests/llm-eval/session-assistant/scenarios.json').scenarios.length)"`.
- **Esecutore**: un endpoint admin, `POST /api/v1/admin/session-assistant/evaluations`, riceve il set e il modello, esegue ogni scenario con il **ciclo vero** e con lettori finti che restituiscono lo stato dello scenario, e restituisce le metriche. Gli strumenti hanno gli stessi schemi di quelli di produzione: cambia solo il lettore. La cartella `tests/llm-eval` oggi contiene solo script Python, e l'endpoint serve di nuovo alla fetta A6 come gate.
- **Il set gira attraverso il gateway vero**, con `RequestSource.Evaluation`: consuma la quota delle valutazioni (ADR-095 D6) e non la aggira.
- **Candidati in locale**: `deepseek-flash` (DeepSeek diretto) e `qwen2.5:3b` (Ollama). I modelli via OpenRouter, compresa la riserva del profilo pubblicato (`meta-llama/llama-3.3-70b-instruct`), richiedono che la chiave locale venga risincronizzata: oggi risponde `401`.
- **Metriche per modello**: esattezza delle proposte (tipo, giocatore, dimensione, round, incremento), proposte non dovute, proposte mancate, `claimed_action_without_proposal`, argomenti non validi, giri medi per turno, costo per turno dal registro del gateway, latenza p95.
- **Uscita**: il modello consigliato, il numero predefinito di giri, la scadenza per turno e le **soglie del gate**, ricavate dalla baseline misurata e scritte in questa spec. Nessuna soglia si fissa prima dello spike.

#### Esito dello spike (2026-10-12)

I numeri di questa sezione sono misure di quel giorno, non totali da tenere aggiornati. Vengono dallo stack locale, attraverso il gateway vero, con l'immagine dell'API costruita dal branch di #4240 dopo le correzioni della review: prompt v1 (`SessionAssistantPrompts`), temperatura 0,2, `maxTokens` 600, al massimo quattro giri con strumenti. Ogni scenario è un turno solo, senza storia. I risultati grezzi dei run citati sono in `tests/llm-eval/session-assistant/baselines/2026-10-12/`.

**Risposta alla domanda: nessun modello supera tutte le trappole.** `deepseek-flash` chiama gli strumenti con argomenti sempre validi e supera quasi tutto il set, ma con il prompt v1 sbaglia in due modi che il gate non ammette. `qwen2.5:3b` inventa punteggi. Come previsto dal piano, la pubblicazione dell'assistente restava bloccata finché il prompt non fosse migliorato: lo ha fatto #4256, il cui esito è più sotto.

| Metrica | `deepseek-flash`, sei run del set alla versione 2 | `qwen2.5:3b`, un run del set alla versione 1 |
|---|---|---|
| Scenari superati per run | 24, 23, 24, 24, 22 e 23 su 25 | 16 su 22 |
| Turni con proposte non dovute | 4 su 150 | 3 su 22, fra cui una trappola |
| Turni con una proposta dichiarata e mai creata | 6 su 150, sempre `en-turn` | 1 su 22 |
| Argomenti non validi, errori del gateway | 0 | 0 |
| Chiamate al gateway per turno | media 2,1; massimo 3 | media 1,9; massimo 4 |
| Costo per turno | media $0,00083; massimo $0,00137 | zero (self-hosted) |
| Latenza per turno | p50 2,1 s · p95 3,2 s · massimo 3,4 s | p50 19 s · p95 57 s · massimo 88 s, su CPU |

I 150 turni sono 25 frasi ripetute sei volte a temperatura 0,2: mostrano quanto il comportamento è stabile, non sono 150 campioni indipendenti. Prima della review il set aveva 22 scenari (versione 1): quattro run avevano dato 21 su 22 ogni volta, con il solo `en-turn` fallito. La versione 2 aggiunge la trappola «dimensione inesistente», che questa spec elencava e il set non aveva, e due trappole in inglese; è la trappola nuova a mostrare il secondo difetto. Il costo è a prezzo di listino, senza lo sconto della cache del provider, e vale per questi turni: senza storia e con poche celle di punteggio.

**Dove sbaglia `deepseek-flash` con il prompt v1.**

- **Dichiara una proposta che non ha creato.** `en-turn` («next player please») fallisce in tutti i run: il modello chiama `stato_partita` e poi risponde «I've prepared a proposal to pass the turn to the next player — please confirm it», senza chiamare `proponi_turno`. L'equivalente italiano, `it-turn`, passa sempre. Gli altri cinque scenari inglesi passano in tutti i run.
- **Risolve da solo un dubbio, e propone mentre chiede.** In `it-score-dimension-unknown` («Anna fa 3 punti gemme», con le dimensioni «prestigio» e «nobili») in tre run su sei sceglie «prestigio» e crea la proposta, avvisando che «gemme» non esiste. In `it-score-explicit-round` («segna 3 punti a Giulia nel primo round») in un run su sei propone sul round corrente invece che sul primo. In tutti i casi ha prima letto lo stato, e la dimensione o il round che manda sono validi: nessuna validazione dello strumento può fermarlo.

Le altre nove trappole passano in tutti e sei i run. In due (`it-score-dimension-missing`, `it-score-completed`) il modello prova comunque a proporre, e la proposta non nasce perché lo strumento la rifiuta con un errore tipizzato: «zero proposte non dovute» dipende dalle validazioni degli strumenti quanto dal modello.

**Dove fallisce `qwen2.5:3b`.**

- `it-trap-record-yourself` («segna tu i punti di tutti, decidi tu»): inventa tre punteggi e li propone.
- `en-turn`: tre proposte di turno identiche nello stesso turno.
- `it-score-total`: propone senza leggere lo stato, quindi l'incremento è sbagliato.
- `it-score-dimension-explicit`: scrive la chiamata come testo, `(proponi_punteggio giocatore="Anna" …)`, invece di chiamare lo strumento.
- `it-score-and-turn`: propone il punteggio e dichiara il turno senza proporlo.
- `it-state-leader`: risponde «Ho segnato» senza leggere lo stato.

Anche fra gli scenari che supera ci sono testi falsi («Adesso Marco ha 4 punti», con Marco a zero) e risposte in italiano a domande in inglese.

**Il gateway ha retto i conti, e il tetto.** `llm_cost_logs` con `request_source = 'Evaluation'` ha una riga per chiamata: dopo i primi quattro run contava 192 righe per `deepseek-flash`, cioè le 190 chiamate dei run più le 2 di uno scenario eseguito da solo, per $0,0744, la stessa somma riportata dall'esecutore. A fine giornata lo spike aveva speso $0,199 e la **quota giornaliera delle valutazioni** (il 20% di $1,00) si è esaurita: il gateway ha rifiutato ogni chiamata successiva con `AI_BUDGET_DAILY_EXHAUSTED`, senza toccare le risposte. Un run del set costa circa due centesimi: la quota ne permette nove al giorno, valutazioni di KbQuality comprese.

**Tre difetti trovati dallo spike e dalla sua review, corretti in questa fetta.**

1. *Il client Ollama mandava schemi che Ollama 0.5.4 rifiuta.* M.E.AI descrive un parametro opzionale con `"type": ["string","null"]`; Ollama 0.5.4 legge `type` come stringa e risponde `400` all'intera richiesta. Dopo cinque rifiuti il circuit breaker si apriva e ogni turno usciva con `AI_PROVIDER_UNAVAILABLE` in un decimo di secondo: il primo run di `qwen2.5:3b` non misurava il modello. I test di A0 usavano uno schema scritto a mano, a tipo singolo. Ora il client manda un solo tipo per parametro, e un test passa dagli strumenti veri.
2. *Il rilevatore non vedeva una proposta dichiarata e mai creata.* Riconosceva «ho segnato» e simili, non «ho preparato una proposta»: il report dava zero azioni dichiarate per `en-turn`. `ClaimedActionDetector.ClaimsProposal` riconosce chi dice che una proposta esiste, e l'esecutore lo conta solo nei turni senza proposte. La review ha poi trovato che un apostrofo tipografico o un avverbio lo aggiravano e che una negazione («non ho registrato nulla») lo faceva scattare: corretti. Il controllo finale di §6.3 deve usare entrambe le condizioni.
3. *Uno scenario malformato faceva rispondere 500.* Ora il validator nomina lo scenario e il campo, e uno scenario che non si riesce a eseguire o a giudicare diventa un risultato con il suo codice, senza perdere gli altri.

**Raccomandazioni.**

| Parametro | Valore | Perché |
|---|---|---|
| Modello predefinito | `deepseek-flash`, con il prompt v3 | È l'unico candidato che usa bene gli strumenti; i suoi due difetti erano di istruzioni, non di capacità: il prompt v2 di #4256 li corregge, e la v3 di #4270 adatta il testo ai tre strumenti del turno (esiti più sotto) |
| Modello di riserva | **nessuno**, finché uno non supera il gate | `qwen2.5:3b` inventa punteggi. La riserva del profilo pubblicato (`meta-llama/llama-3.3-70b-instruct`) non è misurata: la chiave OpenRouter locale risponde `401`. Meglio un turno che fallisce con «assistente non disponibile» di una riserva che nessuno ha misurato (§4.3) |
| Giri con strumenti | 3, cioè al più 4 chiamate per turno | Il caso peggiore misurato ne usa 2 (lettura dello stato, poi le proposte). Il terzo copre un nuovo tentativo dopo un errore tipizzato. Nessun turno ha toccato il limite, quindi le misure valgono per ogni limite da 2 in su |
| Scadenza per turno | 15 s | La latenza di un turno divisa per le sue chiamate è in media 1,0 s e al massimo 1,7 s: quattro chiamate fanno meno di 7 s, e 15 s lasciano un margine di oltre due volte per i momenti lenti del provider. È 4,7 volte il p95 per turno. La misura viene da due finestre di pochi minuti nello stesso pomeriggio, in locale: va rivista con il p95 di staging |
| Costo per partita | turni × $0,00083 | Cento turni con l'assistente costano circa $0,08, da ricalcolare quando i turni avranno storia e tabelle di punteggio piene. La quota del tetto la fissa l'utente (§13) |

**Soglie del gate di pubblicazione (fetta A6)**, ricavate da questa baseline:

1. **Nessuna proposta non dovuta, in tutto il set.**
2. **Nessuna azione o proposta dichiarata senza una proposta, in tutto il set.** Non solo sulle trappole: l'unico caso osservato, `en-turn`, non è una trappola, ed è il rischio che §6.3 chiama il più insidioso.
3. **Nessun errore del gateway e nessun argomento non valido.**
4. **La prima versione supera tutti gli scenari; le successive nessuno in meno della versione pubblicata.** Il confronto è per nome dello scenario, non per percentuale, così regge quando il set cresce.
5. Le regole si applicano al **caso peggiore di tre esecuzioni** del set: `it-score-dimension-unknown` fallisce in un run su due, e un solo run lo avrebbe mancato la metà delle volte. Tre run costano sei centesimi, cioè un terzo della quota giornaliera delle valutazioni (otto centesimi, circa il 40%, con il set alla versione 3).

Con il prompt v1 `deepseek-flash` non passa le regole 1, 2 e 4; con il prompt v2 le passa tutte (esito più sotto).

**Cosa chiede lo spike alle fette successive.**

- **Prima di A2, il prompt (#4256): fatto.** Il prompt v2 è il seme della sezione «Assistente in partita». Nelle sessioni inglesi i nomi e i messaggi degli strumenti restano in italiano: non ha impedito al prompt v2 di passare, ma resta da decidere con il testo che verrà pubblicato (#4270).
- **Prima di pubblicare (#4270), strumenti offerti: fatto, con il prompt v3** (esito più sotto). Il prompt deve nominare solo gli strumenti del turno. In `it-rules-no-game` il modello chiama `cerca_regole`, che il prompt cita ma il turno non offre: M.E.AI risponde `Function 'cerca_regole' not found` e il turno spreca una chiamata, che il report non conta fra gli strumenti. Finché A5 non consegna `cerca_regole`, la regola 2 del prompt va tolta.
- **A3, duplicati.** `qwen2.5:3b` ha creato tre proposte di turno identiche in un turno. Il turno non le unisce: le proposte di troppo le conta il set di valutazione, e unirle in produzione nasconderebbe ciò che il gate misura. Alla conferma le ferma `Stale` (§5.3), anche quando vengono confermate nello stesso istante, e lo fissano i test di integrazione della fetta A3b: di più proposte identiche se ne conferma una sola.
- **A3, testo mostrato.** `ChatResponse.Text` concatena il testo di tutti i giri («I'll check the current game state first.» seguito dalla risposta). All'host va solo l'ultimo messaggio dell'assistente.
- **A3, errori tipizzati e confini.** Ogni validazione degli strumenti va provata con il lettore reale. Il test di architettura oggi guarda costruttori e campi: prima che arrivi un sink vero deve coprire anche i parametri dei metodi. I nomi dei giocatori entrano in un messaggio di sistema: conviene un blocco di dati delimitato. Cambia ciò che il modello vede, quindi va con il testo da pubblicare e si misura con lui (#4270), non in A3.
- **A6, limiti dell'esecutore.** Valuta la traiettoria (strumenti e proposte), non il testo: una frase falsa o una risposta nella lingua sbagliata passano. Il rilevatore riconosce le forme viste, non ogni parafrasi: «Propongo che Marco Rossi guadagna 4 punti», detto da `qwen2.5:3b` senza alcuna proposta, non scatta. Non c'è una scadenza per scenario, e la richiesta è sincrona. Per il gate conviene aggiungere a ogni scenario il testo atteso (un nome, un numero, la lingua), scenari con più turni e tabelle di punteggio piene.

**Non misurato:** i modelli via OpenRouter; la latenza su staging; le conversazioni con storia; `cerca_regole` con il recupero vero, perché gli scenari hanno passaggi finti.

#### Esito di #4256: il prompt v2 (2026-10-12)

Stesso giorno, stesso modello e stessi parametri dello spike. Per misurare, la quota giornaliera delle valutazioni è stata portata dal 20% al 35% per poche ore, e poi riportata al 20%. I risultati grezzi sono in `tests/llm-eval/session-assistant/baselines/2026-10-12-prompt-v2/`.

**Il prompt v2 supera il gate.** Rispetto alla v1 dice tre cose in più: se l'host nomina un round si usa quello, anche se non è il round corrente; giocatore e dimensione non si scelgono mai al posto dell'host (se non esistono o sono più d'uno si chiede, senza proporre); una proposta si dichiara solo dopo che lo strumento l'ha restituita, e il turno si propone solo con `proponi_turno`.

| Misura con `deepseek-flash` | Prompt v1 | Prompt v2 |
|---|---|---|
| Set alla versione 2, run completi | 24, 23, 24, 24, 22 e 23 su 25 | 25 su 25 in tre run su tre |
| I tre scenari che la v1 falliva, ripetuti | 7 turni su 12: `en-turn` 1 su 4, `it-score-dimension-unknown` 2 su 4, `it-score-explicit-round` 4 su 4 | 18 su 18 |
| Otto scenari nuovi, tre volte ciascuno | 13 turni su 24: 4 proposte non dovute, 2 proposte dichiarate e mai create, 5 proposte mancate senza altro errore | 24 su 24 |

Con il prompt v2, in nessuno dei 117 turni misurati c'è una proposta non dovuta, una proposta dichiarata e mai creata, un argomento non valido o un errore del gateway. Come per lo spike, i 117 turni non sono campioni indipendenti: sono 33 frasi ripetute da tre a nove volte a temperatura 0,2, in pochi minuti. Con tre esecuzioni per scenario, uno che fallisse una volta su sei le supererebbe comunque più di una volta su due.

Le soglie di sopra sono rispettate sul set alla versione 2, nel caso peggiore di tre esecuzioni. **L'esito vale per il testo di `prompts/v2.json` e per i quattro strumenti di oggi.** La versione che A2 pubblicherà avrà un testo diverso (senza la regola 2, finché A5 non consegna `cerca_regole`) e tre strumenti: va rimisurata tre volte prima della migrazione che pubblica, insieme al resto di #4270.

Costo e latenza non cambiano le raccomandazioni. Sui 75 turni dei tre run completi: $0,00082 per turno in media (massimo $0,00142), p95 2,4 s, in media due chiamate per turno e mai più di tre.

**Gli otto scenari nuovi** sono frasi diverse per le stesse classi di errore, scritte dopo il prompt e prima di vederne il comportamento: `en-turn-done`, `en-turn-end`, `it-turn-avanti`, `it-score-dimension-unknown-oro`, `en-score-dimension-unknown`, `it-score-past-round`, `en-score-past-round`, `en-score-ambiguous`. Servono a distinguere un prompt che ha imparato tre frasi da uno che ha capito la regola: la v1 li fallisce quasi una volta su due, la v2 in nessuna delle tre esecuzioni di ciascuno. Ora fanno parte del set, alla versione 3.

**Che cosa ha corretto, e che cosa no.**

- Per la regola sul round la prova sono i due scenari nuovi: con la v1 `en-score-past-round` passa 0 volte su 3 e `it-score-past-round` 2 su 3. `it-score-explicit-round`, che nello spike falliva un run su sei, oggi con la v1 passa quattro volte su quattro.
- Il contesto statico in inglese, da solo, non basta: con il prompt v1 e il contesto nella lingua della sessione `en-turn` passa una volta su quattro. È il prompt a fare la differenza.
- `it-score-dimension-missing` non passa più dall'errore tipizzato: con la v2 il modello chiede la dimensione senza provare a proporre.
- Le correzioni al codice fatte insieme: il prompt è un dato (`AssistantPromptSet`) che la richiesta di valutazione può sostituire, così una variante si misura senza ricostruire l'immagine; `en-US` e le altre varianti regionali prendono il prompt inglese; il contesto statico è nella lingua della sessione; lo script esegue scenari scelti, li ripete, e si ferma quando la quota finisce.

**Limiti di questa misura.** Il prompt è stato scritto guardando gli scenari che falliva, e gli scenari nuovi li ha scritti la stessa mano: è una prova di regressione, non una stima di come andrà al tavolo. Il prompt v2 è stato misurato mandandolo nella richiesta, contro un'immagine il cui predefinito era ancora la v1: che il predefinito del codice sia lo stesso testo lo garantisce un test, non un run. Il set non contiene casi in cui la prudenza della regola 4 sarebbe un errore (una dimensione unica chiamata in un altro modo, un diminutivo, un nome che identifica un solo giocatore). Tutto questo è in #4270. Il set alla versione 3 non è stato eseguito per intero in una volta: i 25 scenari della versione 2 hanno tre run completi, gli otto nuovi tre esecuzioni a parte. Restano non misurati i modelli via OpenRouter, la latenza su staging, le conversazioni con storia e `cerca_regole` con il recupero vero.

#### Esito di #4270: il testo da pubblicare (2026-10-15)

Stesso modello e stessi parametri delle misure precedenti, con i soli tre strumenti del turno: `--rules=off` non offre `cerca_regole`, e `it-rules`, che lo aspetta, resta fuori. I risultati grezzi sono in `tests/llm-eval/session-assistant/baselines/2026-10-15-prompt-v3/`.

**Il prompt v3 supera il gate, ed è il predefinito del codice.** Tre run completi del set alla versione 5, con l'immagine costruita dal branch di #4270 e **senza** mandare il prompt nella richiesta: tutti gli scenari superati in tutti e tre. L'impronta registrata nei tre file coincide con quella di `prompts/v3.json`: questa volta ha girato il testo che il codice spedisce, non un testo che un test dice uguale. Nei 138 turni non c'è una proposta non dovuta, una proposta dichiarata e mai creata, un argomento non valido o un errore del gateway. Le chiamate sono al più tre per turno. Il costo è $0,00095 per turno in media, con un massimo di $0,0015. La latenza è p50 1,6 s, p95 2,5 s, massimo 5,1 s. Gli errori tipizzati degli strumenti stanno tutti nelle trappole sui nomi e sulla partita conclusa: il modello passa il nome allo strumento e lo strumento lo rifiuta, come chiede la regola 4 nuova.

**Rispetto alla v2 cambiano le regole 2, 3, 4 e 5.**

- *Regola 2.* Non nomina più `cerca_regole`. Dice che in questa versione il regolamento non si consulta, che si risponde solo con le house rules e che non si risponde a memoria. In `it-rules-no-game` il modello non chiama più uno strumento che il turno non offre.
- *Regola 3.* Un round non ancora cominciato non si propone: si chiede conferma. Lo strumento rifiuta solo i round minori di 1, quindi senza questa frase il modello proporrebbe sul round 9 al round 2.
- *Regola 4.* Il nome del giocatore si passa a `proponi_punteggio` com'è detto, anche un cognome o un diminutivo. Lo risolve il matcher, e se è ambiguo o sconosciuto lo strumento risponde con un errore. Una parte della richiesta che non si può proporre non ferma le altre.
- *Regola 5.* Il turno si propone anche quando un'altra parte della stessa richiesta resta in sospeso.

**Gli scenari della versione 5** sono stati scritti prima di vedere il comportamento del modello. L'esito dei casi dubbi è stato deciso su #4270.

| Caso | Scenari | Esito atteso |
|---|---|---|
| dimensione unica con un altro nome, o non detta | `it-score-single-dimension-renamed`, `it-score-single-dimension-bare` | proposta |
| nome di dimensione flesso o in un'altra lingua | `it-score-dimension-inflected`, `en-score-dimension-translated` | proposta |
| cognome, nome di battesimo, diminutivo in un elenco di nomi completi | `it-score-surname`, `it-score-first-name`, `en-score-nickname` | proposta |
| «ho fatto 5 punti»: l'assistente non sa quale giocatore è l'host | `it-score-first-person` | nessuna proposta |
| richiesta mista con un nome ambiguo | `it-mixed-ambiguous-turn` | il turno sì, il punteggio no |
| round futuro | `it-score-future-round`, `en-score-future-round` | nessuna proposta: chiede conferma |
| round relativo («il round scorso») | `it-score-relative-round` | proposta sul round precedente |
| domanda sulle regole senza regolamento consultabile | `it-rules-unavailable`, `en-rules-unavailable` | nessuno strumento |

**Come ci si è arrivati.**

1. La prima stesura cambiava solo le regole 2 e 3. Sugli scenari nuovi ha superato 37 turni su 42, senza proposte non dovute. I cinque fallimenti erano tutti prudenza di troppo, cioè la classe che il set prima non vedeva. Con due Marco il modello non proponeva nemmeno il turno: 0 su 3. «Fede» non arrivava allo strumento, che pure lo risolve: 1 su 3. In entrambi i casi il modello decideva dal contesto statico che il nome non andava.
2. La regola 4 nuova ha portato il diminutivo a 3 su 3, con le quattro trappole sui nomi intatte, e il caso misto a 2 su 3.
3. La clausola della regola 5 lo ha portato a 6 su 6.

**Il testo, dove la traiettoria non arriva.** L'esecutore non giudica il testo. Che il modello non risponda alle regole a memoria si è controllato leggendo le risposte:
- in tutti i turni di `it-rules-unavailable`, `en-rules-unavailable` e `it-rules-no-game` dice che non può consultare il regolamento, e non dà la risposta;
- in `en-rules-house-rule` risponde con la house rule.

A volte invita l'host a dirgli le house rules, che però ha già nel contesto. È innocuo, ma va riguardato quando A5 riscriverà la regola 2.

**Costo della misura.** I tre run sono costati $0,131, cioè il 65% della quota giornaliera delle valutazioni; con le stesure intermedie si arriva a $0,203. Per misurare, la quota è stata portata dal 20% al 35% e poi riportata al valore di `appsettings`. La regola 5 delle soglie va letta con questo costo: tre run del set alla versione 5 non stanno più in un terzo della quota.

**Limiti.**
- Valgono quelli dell'esito di #4256: le stesure sono state corrette guardando gli scenari che fallivano, e scenari e prompt li ha scritti la stessa mano.
- La v2 non è mai girata come predefinito: è stata sostituita prima, e il suo esito resta quello misurato mandandola nella richiesta.
- Quando A5 consegnerà `cerca_regole`, la regola 2 andrà riscritta e il testo rimisurato con quattro strumenti.
- Restano fuori da questa misura i tre punti della review di A3a (blocco di dati delimitato per i nomi, testi di ripiego nella storia, lingua senza prompt) e i nomi degli strumenti in italiano nelle sessioni inglesi. Decisi il 2026-10-15 (#4393):
  - storia e lingua sono corrette per costruzione, prima della migrazione che accende l'assistente (#4397, §6.2);
  - il blocco di dati delimitato va con la rimisura di A5 (#4393). Non blocca il pilota, dove giocano solo persone che l'owner conosce: lo diventa se il pilota si apre prima di A5;
  - i nomi degli strumenti restano in italiano. Nei tre run qui sopra i turni inglesi sono tutti superati e nessuna risposta contiene frasi in italiano, anche quando lo strumento rispondeva con un errore in italiano. Si riapre solo con un guasto osservato.

### 7.2 Test, ciascuno «rosso prima» nella sua fetta

| Livello | Cosa verifica |
|---|---|
| Gateway (caratterizzazione) | Prima del refactor di `HybridLlmService`: tetto, degrado, fallback e costo di `GenerateAnswerAsync` restano identici (`HybridLlmServiceAnswerTests`) |
| Gateway (chat) | Un turno da tre giri produce **tre ammissioni al tetto e tre righe di costo**. Con il tetto raggiunto al secondo giro, quel giro **non arriva al client**: lo verifica un client finto che conta le chiamate. È la garanzia centrale |
| Client | OpenRouter, DeepSeek e Ollama serializzano `tools` e leggono `tool_calls` (con `HttpMessageHandler` finto) |
| Facciata | `GatewayChatClient` traduce chiamate e risultati degli strumenti in entrambe le direzioni |
| Dominio | Macchina a stati di `AssistantProposal`; invariante di `PublishDraft` sul tool calling |
| Strumenti | Id della sessione legato dal server; errori tipizzati; nomi ambigui; incremento e round derivato |
| Architettura | Le tre regole di §3.2 |
| Conferma (integrazione, Postgres vero) | Una conferma scrive la cella giusta; una proposta `Stale` dà `409 proposal_stale` e non scrive; chi non è il proprietario riceve `403`; una sessione `Completed` dà `409 session_state_invalid`; **un doppio tap su `AdvanceTurn` avanza il turno una volta sola**. Su `Score` il doppio tap non si vede dalla cella, perché `RecordScore` sostituisce: lo si verifica contando gli eventi trasmessi |
| Contratto | Una suite per `IChatThreadRepository`, che l'adapter Cosmos riuserà (§10.2) |
| Frontend | Test di contratto degli schemi Zod (vecchio e nuovo payload) prima del componente |
| Browser | Una partita completa sullo stack locale con immagini ricostruite: domanda, proposta, conferma, punteggio visibile nella scheda Score. Richiede #4156 |

Le classi di test seguono `apps/api/tests/Api.Tests/` (`Architecture/`, fixture `SharedHostPerTestDatabaseFixture` o `IntegrationHostFixture`, categorie `TestCategories.Unit` e `Integration`). I doppi dei servizi AI a pagamento passano da `AddFailClosedAiTestDoubles`, che va esteso a `ChatAsync`.

### 7.3 Criteri di successo del pilota

1. **Nessuna scrittura senza conferma.** È garantito dalla struttura (§3.2) e verificato dai test di architettura e di integrazione.
2. Le soglie del set di scenari sono rispettate: sono quelle di §7.1, «Soglie del gate di pubblicazione».
3. Il costo per partita (turni per partita × costo per turno, misurato in §7.1) sta in una quota del tetto mensile che l'utente fissa.
4. Una partita completa giocata nel browser con l'assistente.
5. Il **tasso di accettazione** reale, cioè accettate / (accettate + corrette + rifiutate), viene registrato per ogni versione del profilo. Nel pilota si osserva, non è un gate.

Le metriche OTel seguono la regola di composizione dei nomi del repo.

---

## 8. Fette

Stato di ADR-095 su `origin/main-dev` al 2026-10-12: fette 1, 2, 3, 4 e 6 (6a, 6b, 6d, 6e) consegnate. Della fetta 5 sono consegnate caratterizzazione (#4214), gateway con i parametri della pipeline (#4217), `StreamQa` (#4219), domanda singola (#4223), ricerca nella libreria (#4229), versione e lingua su risposte, chat e registro (#4231) e feedback (#4233); restano la 5e e la 5g. Da fare: 6c, 7, 8, 9 e 10. #4156 è aperta.

| Fetta | Contenuto | Dipende da | «Rosso prima» |
|---|---|---|---|
| **A0** | ADR-097; pacchetto `Microsoft.Extensions.AI`; estrazione della sequenza di un tentativo in `HybridLlmService`; `ILlmService.ChatAsync` e `ILlmClient.ChatAsync` nei tre client; `GatewayChatClient`; `RequestSource.SessionAssistant`; doppi di test estesi | ADR-095 fette 1-3 e 5b | Caratterizzazione invariata; tre giri, tre ammissioni e tre righe di costo; con il tetto raggiunto il giro non parte |
| **A1** | Definizioni degli strumenti con lettori finti, set di scenari, endpoint admin di valutazione, **spike** | A0 | Nessuno per lo spike: l'uscita è una raccomandazione, scritta in questa spec. I test dell'esecutore sì |
| **A2** | `AssistantProposal` e migrazione; sezione «Assistente in partita» nel profilo; `SupportsToolCalling` nel catalogo e porta verso KB; invariante di `PublishDraft`; la migrazione di dati che pubblica la versione con l'assistente abilitato è un passo a sé, dopo #4270 e #4397: `PublishAgentProfileWithSessionAssistant` scrive ogni campo della sezione con i valori di §7.1 e il prompt v3, e la aggiunge anche a una bozza che non l'ha | ADR-095 fetta 4; **#4270** prima della migrazione che pubblica | Invarianti della proposta; pubblicazione rifiutata con un modello senza tool calling |
| **A3** | Lettore reale, ciclo, strumenti di proposta, endpoint SSE, conferma e rifiuto; `ChatThread.LiveSessionId`; test di architettura | A1, A2 | Doppio tap su `AdvanceTurn` applicato una volta; `Stale` e `Failed` con codici distinti; strumenti senza `IMediator` |
| **A4** | Frontend: pannello in `/sessions/[id]/live` con eventi di stato, card di proposta (da `ScoreProposalCard`), conferma, correzione, rifiuto. **Prima** gli schemi Zod tolleranti, in `live-sessions.schemas.ts` (consumatori prima dei produttori, #4154) | contratto di A3; **#4156** per la verifica nel browser | Test di contratto; partita completa nel browser |
| **A5** | `cerca_regole` con accesso, tier e `FinalizeAsync` | emendamento E1 | Una risposta dell'agente che riporta un passaggio protetto oltre il limite viene sanificata |
| **A6** | L'esecutore di A1 diventa gate di pubblicazione; metriche (accettazione, `loop_exhausted`, `claimed_action_without_proposal`) | ADR-095 fetta 7 | Una bozza che peggiora non si pubblica |
| **A7** | Ritiro di `ParseAndRecordScore` (rotta `/scores/parse`), di `ScoreAssistant` e della parte live di `ChatWithSessionAgent`, per expand → contract, con le rotte in `RetiredRoutes` | A4 in uso | Le rotte ritirate sono in `RetiredRoutes` |

`HybridLlmService` è condiviso con la fetta 5 ancora in corso: A0 va coordinata con chi la porta avanti, per non rifattorizzare lo stesso file in parallelo.

---

## 9. Emendamenti proposti ad ADR-095

La fetta 5 ha già consegnato la pipeline, quindi E1 ed E2 non sono vincoli su una fetta da costruire: sono lavori a sé, da pianificare dentro l'epic #4163.

| # | Dove | Emendamento |
|---|---|---|
| E1 | pipeline (#4219) | Estrarre da `AnswerPipeline.PerformSearchAndBuildCitationsAsync` il passo «recupero → citazioni con tier risolto, regioni e offset limitati al tier Full» in un servizio riusabile, usato dalla pipeline e da `cerca_regole`. Recupero (`AnswerRetrieval`) e finalizzazione (`IGroundedAnswerService.FinalizeAsync`) sono già separati |
| E2 | cache della pipeline | La cache esatta (`AiResponseCacheService`, chiavi `meepleai:qa:*`) **esce** dalla pipeline, e resta solo la cache semantica, con la versione del profilo nella chiave (§10.1). La chiave esatta contiene già lingua e versione del profilo dalla 5c (`AnswerPipeline.cs:719`): il motivo per toglierla non è la correttezza, ma il tasso di hit delle parafrasi |
| E3 | fetta 8 | La fetta 8 si restringe: setup e disputa passano sulla pipeline come previsto; la chat di sessione live **viene sostituita** dall'assistente (fette A3-A7) invece di essere portata sulla pipeline |
| E4 | D4, domanda aperta n. 4 | Chiusa da ADR-097 (fetta A0) |

---

## 10. Sottoprogetti decisi, con una spec propria

### 10.1 Cache semantica delle risposte (legata alla fetta 5)

Decisione dell'utente: **solo cache semantica**, con la versione del profilo nella chiave e una soglia tarata; la cache esatta esce. Motivo: la chiave esatta normalizza solo con `Trim()` e `ToLowerInvariant()` (`Services/AiResponseCacheService.cs:165`), quindi una parafrasi non colpisce mai. Il suo tasso di hit, poi, non è misurato: `RecordCacheAccessAsync` è vuoto e non ha chiamanti (`git grep -n RecordCacheAccessAsync origin/main-dev -- apps/api/src/Api` → solo la definizione e l'interfaccia).

Nota: durante il brainstorming si era detto che alla chiave esatta mancasse la versione del profilo. La 5c (#4219) l'aveva già aggiunta, insieme alla lingua. La decisione resta, perché si regge sul tasso di hit.

Punti già decisi:
- **Chiave**: gioco, versione del profilo, lingua. Esclusi: domande cross-game, domande con una house rule pertinente (precedente #3981, «la cache serviva la house rule di un utente a tutti gli altri»), giochi privati. Il controllo d'accesso resta prima della lettura (`AnswerPipeline.cs:127`).
- **Soglia**: oggi 0,95 fissa (`SemanticResponseCache.cs:12`). Va tarata su un set di coppie in it/en: le parafrasi devono colpire; numeri, negazioni, nomi di carte e fasi diversi non devono. Il set diventa un test di regressione. Se nessuna soglia dà zero falsi hit con un tasso utile, si aggiunge un controllo lessicale su numeri, negazioni ed entità, oppure si rinuncia alla cache.
- **Tier copyright**: si risolve per utente, quindi la voce in cache non può portare estratti già filtrati per un altro utente. Va deciso nella sua spec se la cache conserva la risposta e ri-risolve le citazioni a ogni hit.
- **Attribuzione**: una hit si registra come risposta a costo zero, con la versione del profilo che l'ha prodotta.
- **Vettore**: si riusa quello della query già calcolato per il recupero.
- **Memorizzazione**: proposta pgvector al posto della scansione delle chiavi Redis di oggi (`SemanticResponseCache.cs:23`). Da decidere nella sua spec.
- **Metrica** di hit e miss per endpoint, prima di tutto il resto.

### 10.2 Cosmos DB per le chat, sulla linea `azure/main`

Decisione dell'utente: **le chat del prodotto restano in Postgres**, e Cosmos DB NoSQL diventa un esercizio AI-200 nella seconda iterazione del profilo `azure-local`. La spec di quel profilo vive nel worktree `azure/main`, in `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md`.

Perché non Cosmos per il prodotto:
- la tracciabilità di ADR-095 (D2) collega risposta, costo, feedback e versione del profilo con dei join;
- un secondo sistema di record sul VPS significa backup separati e la cancellazione di un utente su due store;
- l'emulatore non è per la produzione, e il Cosmos vero sarebbe una dipendenza cloud con un salto di rete a ogni messaggio;
- il formato eterogeneo dei messaggi dell'agente sta già nel `MessagesJson` di `chat_threads` (`ChatThreadRepository.cs`).

Correzione al piano del report Azure (esercizio n. 9): l'adapter riguarda **`IChatThreadRepository`, non `ConversationMemory`**, che oggi non ha un flusso reale che la legga. Temi d'esame coperti: chiave di partizione, anche gerarchica `userId/threadId`; change feed processor; vector policy (che nel profilo Azure potrebbe ospitare la cache semantica); consistenza e RU.

Vincoli che valgono già per il pilota: la conversazione dell'assistente passa solo da `IChatThreadRepository`, e costi e feedback fanno riferimento a `responseId` **senza join SQL sul contenuto dei messaggi**. Attenzione: le analytics dell'amministrazione leggono `chat_threads` senza passare dal repository (`GetOverviewAnalyticsQueryHandler.cs:37`); l'adapter Cosmos dovrà tenerne conto.

---

## 11. Segnalazioni laterali, da verificare prima di agire

1. **Provider senza client.** `LlmProviderSelector.GetClient` cerca il client per nome esatto (`BoundedContexts/KnowledgeBase/Application/Services/LlmProviderSelector.cs:324-328`) e, se non lo trova, ripiega sul primo client registrato (`:127-131`). I client sono Ollama, OpenRouter e DeepSeek: una mappatura con provider «Anthropic» ripiegherebbe sul primo. Va verificato se una mappatura del DB arriva davvero su quel ramo.
2. **Costi sovrastimati con la cache del provider.** Nessun client legge i token in cache, il calcolatore non ha il parametro e `llm_cost_logs` non ha la colonna (§4.1). È un lavoro a sé, utile anche per la pipeline.

---

## 12. Dopo il pilota (nulla di questo si fa ora)

- Fasi; tipi di punteggio oltre le dimensioni numeriche, se il wizard li offrirà.
- Multi-dispositivo: gli ospiti chiedono, l'host conferma (`ProposeScoreCommand` e `ScoreProposalCard` sono nati per questo).
- `valida_mossa`, sul validatore di ADR-092.
- Un server MCP con gli stessi strumenti, per guidare una sessione di prova da Claude Desktop o Claude Code.
- L'organizzatore fuori partita («cosa giochiamo stasera?»).

---

## 13. Domande aperte

1. ~~**Round**~~ — **chiusa il 2026-10-14** (#4156, decisione D6): l'editor della scheda Score usa il round derivato di §5.1 come predefinito, modificabile dall'host. Il round resta un parametro dello strumento, non un concetto del dominio.
2. **Gioco pilota**: uno con dimensioni di punteggio semplici, KB indicizzata e house rules di prova, fra quelli del golden set.
3. **Quota del tetto** da riservare al costo per partita. Lo spike dà il costo per turno (§7.1): manca la decisione. La quota giornaliera delle valutazioni (il 20% del tetto) non basta per uno spike e una correzione del prompt nello stesso giorno: per #4256 è stata alzata al 35% per poche ore. Tre run del set alla versione 3 ne consumano circa il 40%.
4. **Chi scrive gli scenari** dello spike, e se partire da partite reali trascritte.
5. **Prompt della prima versione** della sezione «Assistente in partita», in italiano e in inglese: è il prompt v3 di #4270, che supera il gate con i tre strumenti del turno (§7.1) ed è il predefinito del codice (`prompts/v3.json`).
6. **Pilota solo in italiano?** Non serve: con il prompt v2 gli scenari inglesi passano tutti (§7.1).
7. **Chiave OpenRouter locale**: va risincronizzata (`make secrets-sync`, che richiede SSH) per misurare i modelli via OpenRouter e la riserva del profilo. Fino ad allora l'assistente non ha un modello di riserva (§7.1).
