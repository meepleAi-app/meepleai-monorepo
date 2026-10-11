# Assistente in partita: un agente con strumenti — design

**Data**: 2026-10-11 · **Origine**: `/sc:brainstorm` del 2026-10-11 · **Stato**: spec da revisionare. Nessun codice scritto.
**Base misurata**: `origin/main-dev` a `4c3f02149` (#4213).
**Contesto**: [ADR-094](../../for-claude/architecture/adr/adr-094-single-system-agent.md) (un solo agente di sistema) · [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) e il [piano delle fette](../plans/2026-10-10-adr-095-slices-plan.md) · [ADR-090](../../for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md) (`KnowledgeBase` possiede la risposta ancorata) · [ADR-089](../../for-claude/architecture/adr/adr-089-session-scoring-ssot.md) (`LiveGameSession` è la fonte unica del punteggio in partita) · [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md) · [`user-use-cases.md`](../user-use-cases.md) (SES-07, SES-17, AI-03) · [brief del 2026-10-10](./2026-10-10-single-agent-ai-architecture-brief.md).

Al 2026-10-11 questi documenti esistono solo come file non committati del checkout principale, quindi li cito senza link:
- ADR-092 (l'Arbitro Python diventa un validatore puro): `docs/for-claude/architecture/adr/adr-092-arbiter-ownership-python-validator.md`;
- il report Azure: `docs/for-developers/research/2026-10-08-azure-ai-200-provider-switch-research.md`;
- il panel neuro-simbolico: `docs/for-developers/research/2026-10-08-neuro-symbolic-architecture-spec-panel.md`.

**Convenzione**: nessun conteggio volatile in prosa; dove serve una misura c'è il comando che la produce. Prezzi e comportamenti dei provider si verificano alla fonte prima di usarli.

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
| Dove vive il ciclo | **.NET, con `Microsoft.Extensions.AI` dentro il gateway** |
| Cache per l'assistente | Nessuna cache delle risposte; **cache del prompt del provider** come leva di costo; cache dei risultati di `cerca_regole` per la durata della sessione |
| Cache delle risposte della pipeline | **Solo semantica**, con la versione del profilo nella chiave e una soglia tarata; la cache esatta esce (sottoprogetto a parte, §10.1) |
| Database delle chat | **Postgres** per il prodotto; un adapter Cosmos DB di `IChatThreadRepository` come esercizio sulla linea `azure/main` (§10.2) |
| Tipi di punteggio nel pilota | **Solo a punti** |

### Cosa non è

- **Non è un sistema multi-agente.** È un solo agente con degli strumenti, compatibile con ADR-094.
- **Non è un framework generale di agenti.** È un pilota su un caso solo.
- **Non usa LangGraph né `orchestration-service`.** Il motivo è in §3.3.

---

## 2. Stato di partenza

| # | Misura | Evidenza |
|---|---|---|
| S1 | Nessun tool calling. `ILlmService` accetta un prompt di sistema e un prompt utente; nessun client invia `tools` o legge `tool_calls` | `Services/ILlmService.cs`; `git grep -c -E 'tool_calls\|"tools"' origin/main-dev -- 'apps/api/src/Api/Services/LlmClients/*.cs'` → 0 per ogni file |
| S2 | Nessun framework AI nel progetto | `git show origin/main-dev:apps/api/src/Api/Api.csproj \| grep -iE 'Extensions\.AI\|SemanticKernel\|ModelContextProtocol'` → nessuna riga |
| S3 | Il gateway applica il tetto prima di ogni chiamata | `HybridLlmService.cs:36` (`ILlmBudgetGate`, #4166) |
| S4 | Un precursore delle proposte **registra da solo** quando la confidenza dichiarata dal modello supera 0,8 | `ParseAndRecordScoreCommandHandler.cs:125`; rotta `/live-sessions/{sessionId}/scores/confirm` in `LiveSessionEndpoints.cs:226` |
| S5 | Le card di proposta esistono e non sono montate | `components/session/ScoreProposalCard.tsx`, `components/game-night/ScoreAssistant.tsx`; SES-17 in `user-use-cases.md` |
| S6 | Le chat sono salvate in modelli paralleli | `ChatThread` → `ChatMessage`, `ChatSession`, `AgentSession` (KnowledgeBase), `SessionChatMessage` (SessionTracking), `ConversationMemory` |
| S7 | `orchestration-service` (LangGraph) non è collegato: nessuna rotta invia `TutorQueryCommand`, e il servizio chiama rotte .NET che non esistono | mappatura del 2026-10-11; `infra/docker-compose.yml` lo dichiara «publish-only» |
| S8 | I `tools` salvati su `AgentDefinition` non sono mai eseguiti | S1 |
| S9 | Il motore a plugin di `Domain/Plugins` serve il costruttore di pipeline personalizzate, ma la sua generazione è simulata | `RagPipelineEndpoints.cs`, `CustomPipeline/*Command.cs`; `GenerationLlmPlugin.cs:142` («Simulate LLM generation») |

S8 e S9 sono un avvertimento: qui le astrazioni agentiche costruite senza un consumatore reale non sono mai arrivate a una chiamata vera. Il pilota parte da un consumatore (l'host al tavolo) e da una misura (§7).

---

## 3. Architettura e confini

```
POST /live-sessions/{id}/assistant (SSE) ──IMediator──> AskSessionAssistantCommand   [KnowledgeBase]
                                                          │
          IChatClient del gateway (l'unico registrato):   ▼
          [FunctionInvocation, max N giri] → [OpenTelemetry] → [BudgetGate + costo] → [adapter provider]
                    │                                                               Services/LlmClients/
                    ▼ strumenti (AIFunction, involucri di query MediatR)
   lettura:  stato_partita (GameManagement) · cerca_regole (pipeline KB)
   proposta: proponi_punteggio · proponi_turno  → salvano una proposta, NON scrivono
```

### 3.1 L'ordine dei middleware è una garanzia

`FunctionInvokingChatClient` chiama il provider **più volte** dentro lo stesso turno. Il middleware del tetto deve quindi stare **sotto** l'invocazione degli strumenti, attaccato al provider: così ogni giro passa dal controllo e viene registrato nel costo. Messo sopra, controllerebbe solo la prima chiamata del ciclo.

### 3.2 Decisioni

| Elemento | Decisione |
|---|---|
| Proprietario | `KnowledgeBase/Application/SessionAssistant/`. La risposta è di `KnowledgeBase` (ADR-090); GameManagement e AgentMemory si leggono solo via `IMediator` |
| Adapter M.E.AI | Vive in `Services/LlmClients/`, dentro il perimetro di `LlmProviderEgressArchitectureTests`. Il DI espone **solo la catena composta**, mai l'adapter nudo: per la regola #4179 chi ottiene un client LLM può chiamare un provider |
| `ILlmService` | Invariato. Le due facciate condividono catalogo, `ILlmBudgetGate` e registro costi. Migrare gli altri consumatori su M.E.AI è fuori da questo lavoro |
| Profilo | Una sezione additiva in `AgentProfileContent`, «Assistente in partita»: abilitato, prompt di sistema per lingua, numero massimo di giri, scadenza per turno. Assente equivale a disabilitata |
| Catalogo | Una capacità `SupportsToolCalling` per modello. Pubblicare una versione con l'assistente abilitato richiede che **sia il modello principale sia quello di riserva** la abbiano |
| Garanzia strutturale | Un test architetturale vieta alle classi di `SessionAssistant/Tools/` di referenziare un `ICommand` di GameManagement: il modello non può scrivere, per costruzione |
| ADR | **ADR-096**: `Microsoft.Extensions.AI` come facciata del gateway per il tool calling. Chiude la domanda aperta n. 4 di ADR-095 |

### 3.3 Approcci scartati

| Approccio | Perché no |
|---|---|
| Python con LangGraph in `orchestration-service` | Il servizio non gira su staging e nessuno lo osserva (S7). Le chiamate LLM dovrebbero passare dal gateway .NET (ADR-095 D4), quindi servirebbe un proxy. Lo stato della sessione e l'autorizzazione andrebbero letti attraverso la rete. L'apprendimento Python ha già un consumatore in ADR-092 (il validatore) |
| Un ciclo scritto a mano su `ILlmService` | Reinventa ciò che M.E.AI offre già, insegna meno, e aggiunge un'astrazione fatta in casa a un repo che ne ha già senza consumatori (S8, S9) |
| Un profilo separato per l'assistente | Allontanerebbe da «un agente» (ADR-094). Il prezzo della scelta fatta è che le versioni dell'assistente e quelle della chat sulle regole si pubblicano insieme |

---

## 4. Cache

### 4.1 Per l'assistente

1. **Nessuna cache delle risposte, né esatta né semantica.** La risposta dipende dallo stato vivo e dalla conversazione. In particolare, `UseDistributedCache()` di M.E.AI calcola la chiave sui messaggi della richiesta, che **non contengono lo stato**: lo stato arriva dopo, dai risultati degli strumenti. Una hit salterebbe gli strumenti e risponderebbe con lo stato di dieci minuti prima. Un test verifica che la catena dell'assistente non contenga un client di caching.
2. **La cache del prompt del provider è la leva principale.** Ogni giro rimanda tutto il contesto, quindi i messaggi vanno ordinati dal più stabile al più volatile:
   1. prompt di sistema della versione pubblicata;
   2. definizioni degli strumenti;
   3. contesto statico della sessione: gioco, giocatori, tipo di punteggio e dimensioni, house rules;
   4. storia;
   5. domanda.

   I punteggi e il turno arrivano **solo** dai risultati degli strumenti, in coda.
3. **Contabilità della cache del prompt.** L'adapter legge i token in cache dall'`usage` della risposta e il costo usa `CacheReadPricePerMillion`, campo che il catalogo ha già (`ModelPricing.cs:23`) e che nessun calcolo usa. Una metrica registra il tasso di hit per versione del profilo. Il comportamento cambia da provider a provider: alcuni mettono in cache in automatico, altri vogliono marcatori espliciti (Anthropic, `cache_control`). Va verificato alla fonte per ogni modello del catalogo prima di contarci.
4. **I risultati di `cerca_regole` restano in cache per tutta la sessione**: le regole non cambiano a metà partita. La chiave contiene gioco, versione dell'indice, versione del profilo e domanda normalizzata.

### 4.2 Conseguenza sulla pipeline

`cerca_regole` restituisce **passaggi e citazioni**, senza una chiamata LLM annidata: la risposta finale la compone l'agente. Si risparmia una chiamata per ogni domanda sulle regole, ma la pipeline della fetta 5 deve offrire `recupera()` separato da `rispondi()`, e il **controllo copyright** (tier per chunk, controllo dei leak, citazioni) deve potersi applicare a un testo generato fuori dalla pipeline. È l'emendamento E1 (§9).

### 4.3 Degrado (ADR-095 D6)

L'assistente non si può servire dalla cache. Oltre il 100% del tetto risponde «assistente in pausa» e l'inserimento manuale resta disponibile. Fra l'80% e il 100% usa il modello di riserva, che supporta il tool calling per il vincolo di §3.2.

---

## 5. Strumenti, proposte e conferma

### 5.1 Strumenti

Il pilota ne ha pochi, di proposito: con i modelli economici meno strumenti significano scelte più affidabili. Lo spike (§7.1) lo verifica.

| Strumento | Tipo | Cosa fa | Limiti voluti |
|---|---|---|---|
| `stato_partita()` | lettura | Giocatori e ordine di turno, giocatore di turno, round, fase, stato della sessione, punteggi per giocatore e dimensione (`GetLiveSessionQuery`, `GetSessionScoresQuery`, `GetTurnPhasesQuery`) | **Nessun parametro.** L'id della sessione lo lega il server alla richiesta: il modello non può sceglierne un'altra |
| `cerca_regole(domanda)` | lettura | Passaggi e citazioni da `recupera()` della pipeline unica, per il gioco della sessione e nella lingua dell'host | Il gioco lo lega il server. Arriva con la fetta A5 (§8) |
| `proponi_punteggio(giocatore, valore, dimensione?, round?)` | proposta | Risolve il nome con `IPlayerNameResolutionService`, controlla che la dimensione esista, usa il round corrente se manca, e salva una proposta | Nome ambiguo o sconosciuto: restituisce un errore tipizzato al modello («Marco R. o Marco B.?»), che lo chiede all'host. Solo punteggi a punti; per gli altri tipi l'assistente dice di segnarli a mano |
| `proponi_turno()` | proposta | Proposta di passare il turno | La fase segue lo stesso schema, ma è fuori dal pilota |

Le **house rules** non sono uno strumento: vanno nel contesto statico (§4.1), perché non cambiano durante la partita.

### 5.2 La proposta

È un oggetto del `KnowledgeBase`: è l'uscita dell'assistente, come la risposta e le citazioni. Tabella `session_assistant_proposals`:

| Campo | Contenuto |
|---|---|
| `id`, `live_session_id`, `chat_thread_id`, `message_id` | identità e provenienza |
| `kind` | `Score` · `AdvanceTurn` |
| `payload` (jsonb) | tipizzato per tipo: per `Score` giocatore, dimensione, valore, round |
| `expected_state` (jsonb) | round e giocatore di turno al momento della proposta |
| `status` | `Pending` → `Accepted` · `Edited` · `Rejected` · `Expired` · `Stale` |
| `confirmed_payload` (jsonb, nullable) | il valore confermato, quando l'host lo corregge |
| `profile_version_id` | la versione del profilo che l'ha prodotta |
| `created_at`, `resolved_at`, `resolved_by` | tempi e autore |

Transizioni:
- `Pending` → `Accepted` o `Edited` (conferma), `Rejected` (rifiuto), `Stale` (lo stato è cambiato), `Expired` (scadenza);
- ogni altro stato è finale.

**Scadenza**: una proposta ancora `Pending` diventa `Expired` quando inizia il turno successivo dell'assistente o quando la sessione si chiude.

### 5.3 Conferma

`POST /live-sessions/{id}/assistant/proposals/{pid}/confirm`, con l'eventuale valore corretto, invia `ConfirmAssistantProposalCommand`. È **l'unico ponte verso le scritture**, ed è avviato da una persona:

1. verifica che chi conferma sia l'owner della sessione (`403` altrimenti);
2. accetta solo una proposta `Pending`: una seconda conferma restituisce lo stato attuale e **non scrive**. È l'idempotenza contro il doppio tap;
3. rilegge lo stato: se round o giocatore di turno sono diversi da `expected_state`, la proposta diventa `Stale` e la risposta è `409`;
4. invia `RecordLiveSessionScoreCommand(SessionId, PlayerId, Round, Dimension, Value)` oppure `AdvanceLiveSessionTurnCommand(SessionId)`, che hanno già la loro concorrenza `xmin`;
5. registra l'esito, con il valore proposto e quello confermato quando differiscono.

Il rifiuto è `POST …/proposals/{pid}/reject`, con le stesse verifiche di proprietà e di stato.

---

## 6. Il turno, i limiti e gli errori

### 6.1 Risposta bufferizzata

La risposta finale si bufferizza e non si trasmette token per token. La chat di sessione attuale fa già così (`ChatWithSessionAgentCommandHandler`): è il modo di applicare il controllo copyright **prima** che il testo arrivi all'utente. Le risposte al tavolo sono brevi, quindi la latenza aggiunta è bassa. In cambio sparisce il problema del fallback a metà flusso, e il ciclo usa la chiamata non in streaming di M.E.AI. L'SSE resta per gli eventi.

### 6.2 Un turno

1. **`POST /live-sessions/{id}/assistant`**. Si verifica che l'utente sia owner della sessione, che la sessione non sia `Completed` e che l'assistente sia abilitato nella versione pubblicata. Poi si conteggia la quota del piano: **un turno vale una domanda**, qualunque sia il numero di giri.
2. **Messaggi**, nell'ordine di §4.1. Della storia entrano gli ultimi messaggi del `ChatThread` della sessione, che riceve un `LiveSessionId` nullable (modifica additiva). Dei turni passati si rimandano solo il testo e gli esiti delle proposte, non i risultati degli strumenti.
3. **Ciclo** con al massimo N giri, preso dal profilo. Ogni giro passa dal gateway con la versione del profilo, l'id della sessione e `RequestSource.SessionAssistant`. È un valore nuovo, mappato sulla classe di budget `Answer` in `LlmBudgetClass.For`, il cui `switch` rifiuta i valori non mappati: così il costo dell'assistente si attribuisce a parte e il costo per partita (§7.3) si misura.
4. **Eventi SSE**:
   - `status` mentre gira uno strumento («leggo i punteggi…»);
   - `proposal` per ogni card;
   - `answer`, con testo e citazioni, dopo il controllo copyright;
   - `done`, con `responseId` e versione del profilo;
   - `error`, con un codice.
5. **Salvataggio**: messaggi con le loro `parts` (testo, citazioni, riferimenti alle proposte), proposte, righe di costo. Queste ultime le scrive già il gateway.

### 6.3 Limiti ed errori

| Situazione | Comportamento |
|---|---|
| Giri esauriti senza una risposta | Messaggio esplicito («non sono riuscito, riprova o segna a mano») e metrica `loop_exhausted`. Le proposte già create restano valide: richiedono comunque la conferma |
| Argomenti non validi | Lo strumento restituisce al modello un errore tipizzato, non un'eccezione. Il giro conta |
| Eccezione dentro uno strumento | `IncludeDetailedErrors = false`: `ex.Message` non arriva né al modello né all'utente |
| **Il modello dichiara un'azione senza aver creato la proposta** («ho segnato 5 punti a Marco») | È il rischio più insidioso, perché l'host crede che il punteggio sia registrato. Il prompt vieta di dichiarare azioni compiute. Un controllo finale cerca affermazioni di questo tipo in un turno senza proposte, aggiunge un avviso e registra la metrica `claimed_action_without_proposal`. Il caso è nel set di valutazione |
| Il tetto rifiuta un giro a metà ciclo | Il ciclo si ferma: `error budget_paused` e «assistente in pausa» |
| Il provider fallisce | Fallback sul modello di riserva del catalogo, a ogni giro |
| Un secondo turno mentre il primo è in corso | `409 assistant_busy`, con un lock per sessione |
| Tempo | Una scadenza per turno, configurabile nel profilo. Il valore iniziale viene dal p95 misurato nello spike |

---

## 7. Valutazione, test e criteri di successo

### 7.1 Spike: prima di costruire il resto

Domanda: **i modelli del catalogo scelti per costo chiamano gli strumenti in modo affidabile?**

- **Set di scenari al tavolo**, in italiano e in inglese, in `tests/llm-eval/session-assistant/`. Ogni scenario è uno stato della partita in JSON, una frase dell'host e la **traiettoria attesa** (strumenti chiamati, proposte attese o nessuna proposta). Categorie:
  - punteggi, inclusi nomi ambigui e giocatori assenti;
  - turno;
  - domande sullo stato, che **non** devono generare proposte;
  - domande sulle regole, che devono chiamare `cerca_regole`;
  - richieste miste;
  - trappole: «segna tu», tipo di punteggio non supportato, chiacchiere.

  Dimensione del set: `find tests/llm-eval/session-assistant -name '*.json' | wc -l`.
- **Metriche per ogni modello candidato**: esattezza delle proposte (tipo, giocatore, valore, round), proposte non dovute, proposte mancate, `claimed_action_without_proposal`, argomenti non validi, giri medi per turno, costo per turno dal registro del gateway, latenza p95.
- **Il set gira attraverso il gateway vero**, con `RequestSource.Evaluation` (classe di budget `Evaluation`): consuma la quota delle valutazioni (ADR-095 D6) e non la aggira.
- **Uscita**: il modello consigliato, il numero predefinito di giri, la scadenza per turno e le **soglie del gate**, ricavate dalla baseline misurata. Nessuna soglia si fissa prima dello spike. Il set resta come riferimento permanente dell'assistente.

### 7.2 Test, ciascuno «rosso prima» nella sua fetta

| Livello | Cosa verifica |
|---|---|
| Dominio | Macchina a stati della proposta e invarianti di §5.2 |
| Strumenti | L'id della sessione è legato dal server; errori di validazione tipizzati; nomi ambigui |
| Architettura | Gli strumenti non referenziano un `ICommand` di GameManagement; la catena dell'assistente non contiene client di caching; l'adapter M.E.AI sta nel perimetro di `LlmProviderEgressArchitectureTests` |
| Gateway | Un turno da tre giri produce **tre controlli del tetto e tre righe di costo**. Con il tetto raggiunto al secondo giro, quel giro **non arriva al provider**: lo verifica un provider finto che conta le chiamate. È la garanzia centrale |
| Conferma (Testcontainers) | Una conferma produce un punteggio; una proposta `Stale` dà `409` e non scrive; chi non è owner riceve `403`; un doppio tap non registra due punteggi |
| Contratto | Una suite per `IChatThreadRepository`, che l'adapter Cosmos riuserà (§10.2) |
| Browser | Una partita completa sullo stack locale con immagini ricostruite: domanda, proposta, conferma, punteggio visibile nella scheda Score |

Il gate di pubblicazione è quello di ADR-095 D7, esteso alla sezione dell'assistente: una bozza che peggiora l'esattezza delle proposte o aumenta quelle non dovute non si pubblica.

### 7.3 Criteri di successo del pilota

1. **Nessuna scrittura senza conferma.** È garantito dalla struttura (§3.2) e verificato dai test di architettura e di integrazione.
2. Le soglie del set di scenari sono rispettate; si fissano dopo lo spike.
3. Il costo per partita (turni per partita × costo per turno) sta in una quota del tetto mensile che l'utente fissa dopo lo spike.
4. Una partita completa giocata nel browser con l'assistente.
5. Il **tasso di accettazione** reale, cioè accettate / (accettate + corrette + rifiutate), viene registrato per ogni versione del profilo. Nel pilota si osserva, non è un gate.

Le metriche OTel seguono la regola di composizione dei nomi del repo.

---

## 8. Fette

Stato di ADR-095 su `origin/main-dev` al 2026-10-11: fette 1, 2, 3, 4 e 6 (6a, 6b, 6d, 6e) consegnate; la fetta 5 è in corso (5a mergiata, #4214); 6c, 7, 8, 9 e 10 da fare. #4156 è aperta, e serve alla verifica nel browser: la scheda Score si blocca sulle sessioni create dal wizard.

| Fetta | Contenuto | Dipende da | «Rosso prima» |
|---|---|---|---|
| **A0** | ADR-096; adapter `IChatClient` in `Services/LlmClients/`; middleware del tetto **interno** al ciclo; lettura dei token in cache e costo con `CacheReadPricePerMillion` | ADR-095 fette 1-3; **dopo la 5b**, che modifica lo stesso gateway | Tre giri, tre controlli e tre righe di costo; con il tetto raggiunto il giro non parte |
| **A1** | Spike sul set di scenari (§7.1) | A0 | Nessuno: l'uscita è una raccomandazione, scritta in questa spec |
| **A2** | Aggregato della proposta e migrazione; sezione «Assistente in partita» in `AgentProfileContent`; capacità `SupportsToolCalling` nel catalogo, controllata alla pubblicazione | ADR-095 fetta 4 | Invarianti della proposta; pubblicazione rifiutata con un modello senza tool calling |
| **A3** | Ciclo, `stato_partita`, strumenti di proposta, endpoint SSE, conferma e rifiuto; `RequestSource.SessionAssistant`; `ChatThread` con `LiveSessionId` e `parts`; test di architettura | A0, A2 | Un doppio tap non registra due punteggi; uno strumento che importa un comando GameManagement fa fallire la build |
| **A4** | Frontend: pannello in `/sessions/[id]/live` con eventi di stato, card di proposta (da `ScoreProposalCard`), conferma, correzione, rifiuto. **Prima** gli schemi Zod tolleranti (regola di #4154: i consumatori prima dei produttori) | contratto di A3; #4156 per la verifica nel browser | Test di contratto dello schema; partita completa nel browser |
| **A5** | `cerca_regole` | ADR-095 fetta 5 con l'emendamento E1 | Una risposta dell'agente che riporta un passaggio oltre il limite viene bloccata |
| **A6** | Il set di scenari diventa gate di pubblicazione; metriche (accettazione, `loop_exhausted`, `claimed_action_without_proposal`, hit della cache del prompt) | ADR-095 fetta 7 | Una bozza che peggiora non si pubblica |
| **A7** | Ritiro di `ParseAndRecordScore`, di `ScoreAssistant` e della parte live di `ChatWithSessionAgent`, per expand → contract, con le rotte in `RetiredRoutes` | A4 in uso | Le rotte ritirate sono in `RetiredRoutes` |

---

## 9. Emendamenti proposti ad ADR-095

La fetta 5 è in corso, quindi E1 ed E2 vanno concordati con chi la porta avanti **prima della 5c**, che costruisce il servizio della pipeline.

| # | Fetta | Emendamento |
|---|---|---|
| E1 | **5c** | La pipeline espone `recupera()` (accesso, recupero, reranking, passaggi con tier copyright) separato da `rispondi()` (prompt, generazione, controllo copyright, registro). Il controllo copyright si può applicare anche a un testo generato fuori dalla pipeline |
| E2 | **5f** | Oggi la 5f prevede «versione su messaggio di chat e feedback, e nelle chiavi delle due cache». Con la decisione di §10.1 la cache esatta (`AiResponseCacheService`, chiavi `meepleai:qa:*`) **esce** dalla pipeline, e resta solo la cache semantica, con la versione del profilo nella chiave |
| E3 | **8** | La fetta 8 si restringe: setup e disputa passano sulla pipeline come previsto; la chat di sessione live **viene sostituita** dall'assistente (fette A3-A7) invece di essere portata sulla pipeline |
| E4 | D4, domanda aperta n. 4 | Chiusa da ADR-096 (fetta A0) |

La domanda aperta n. 3 (fallback a metà streaming) **non riguarda l'assistente**, che bufferizza la risposta (§6.1). Resta aperta per `StreamQa`, che la 5b affronta.

---

## 10. Sottoprogetti decisi, con una spec propria

### 10.1 Cache semantica delle risposte (legata alla fetta 5)

Decisione dell'utente: **solo cache semantica**, con la versione del profilo nella chiave e una soglia tarata; la cache esatta esce. Motivo: la chiave esatta normalizza solo con `Trim()` e `ToLowerInvariant()` (`AiResponseCacheService.cs:165`), quindi una parafrasi non colpisce mai. E il suo tasso di hit non è misurato: `RecordCacheAccessAsync` è vuoto e non ha chiamanti (`git grep -n RecordCacheAccessAsync origin/main-dev -- apps/api/src/Api` → solo la definizione e l'interfaccia).

Punti già decisi:
- **Chiave**: gioco, versione del profilo, lingua. Esclusi: domande cross-game, domande con una house rule pertinente (precedente #3981, «la cache serviva la house rule di un utente a tutti gli altri»), giochi privati. Il controllo d'accesso resta prima della lettura, come oggi (`StreamQaQueryHandler.cs:116` prima di `:145`).
- **Soglia**: oggi 0,95 fissa (`SemanticResponseCache.cs:12`). Va tarata su un set di coppie in it/en: le parafrasi devono colpire; numeri, negazioni, nomi di carte e fasi diversi non devono. Il set diventa un test di regressione. Se nessuna soglia dà zero falsi hit con un tasso utile, si aggiunge un controllo lessicale su numeri, negazioni ed entità, oppure si rinuncia alla cache.
- **Attribuzione**: una hit si registra come risposta a costo zero, con la versione del profilo che l'ha prodotta. D2 resta intatto.
- **Vettore**: si riusa quello della query già calcolato per il recupero, senza un embedding in più e senza rischiare una codifica diversa.
- **Memorizzazione**: proposta pgvector al posto della scansione delle chiavi Redis di oggi (`SemanticResponseCache.cs:23`). Da decidere nella sua spec.
- **Metrica** di hit e miss per endpoint, prima di tutto il resto.

### 10.2 Cosmos DB per le chat, sulla linea `azure/main`

Decisione dell'utente: **le chat del prodotto restano in Postgres**, e Cosmos DB NoSQL diventa un esercizio AI-200 nella seconda iterazione del profilo `azure-local`. La spec di quel profilo vive nel worktree `azure/main`, in `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md`.

Perché non Cosmos per il prodotto:
- la tracciabilità di ADR-095 (D2) collega risposta, costo, feedback e versione del profilo con dei join;
- un secondo sistema di record sul VPS significa backup separati e la cancellazione di un utente su due store;
- l'emulatore non è per la produzione, e il Cosmos vero sarebbe una dipendenza cloud con un salto di rete a ogni messaggio;
- il formato eterogeneo dei messaggi dell'agente è coperto da una colonna `parts jsonb`.

Correzione al piano del report Azure (esercizio n. 9): l'adapter riguarda **`IChatThreadRepository`, non `ConversationMemory`**. `ConversationMemory` oggi non ha un flusso reale che la legga, mentre l'assistente darebbe all'adapter traffico vero. Temi d'esame coperti: chiave di partizione, anche gerarchica `userId/threadId`; change feed processor; vector policy (che nel profilo Azure potrebbe ospitare la cache semantica); consistenza e RU.

Vincolo che vale già per il pilota: la conversazione passa solo da `IChatThreadRepository`, e costi e feedback fanno riferimento a `responseId` **senza join SQL sul contenuto dei messaggi**.

---

## 11. Segnalazioni laterali, da verificare prima di agire

1. **Provider senza client.** `LlmProviderSelector.GetClient` cerca il client per nome esatto (`LlmProviderSelector.cs:324-328`) e, se non lo trova, ripiega sul primo client registrato (`:127-131`). I client sono Ollama, OpenRouter e DeepSeek: una mappatura con provider «Anthropic» ripiegherebbe sul primo. Va verificato se una mappatura del DB arriva davvero su quel ramo.
2. **Costi sovrastimati.** Nessun client legge i token in cache (`git grep -n -i -E 'cached_tokens|prompt_cache_hit' origin/main-dev -- apps/api/src/Api` → nessun risultato): quando un provider mette in cache in automatico, il costo registrato supera quello reale. È prudente per il tetto, ma rende invisibile il risparmio. A0 lo corregge per la facciata M.E.AI; per `ILlmService` va deciso a parte.

---

## 12. Dopo il pilota (nulla di questo si fa ora)

- Fasi e altri tipi di punteggio (vittoria sì/no, obiettivi, classifica).
- Multi-dispositivo: gli ospiti chiedono, l'host conferma (`ScoreProposalCard` era nata per questo).
- `valida_mossa`, sullo strumento `/validate` del validatore di ADR-092.
- Un server MCP con gli stessi strumenti, per guidare una sessione di prova da Claude Desktop o Claude Code.
- L'organizzatore fuori partita («cosa giochiamo stasera?»).

---

## 13. Domande aperte

1. **Gioco pilota**: uno con punteggio a punti, KB indicizzata e house rules di prova. Conviene sceglierlo fra quelli del golden set.
2. **Quota del tetto** da riservare al costo per partita, dopo lo spike.
3. **Chi scrive gli scenari** dello spike, e se partire da partite reali trascritte.
4. **Prompt della prima versione** della sezione «Assistente in partita», in italiano e in inglese.
5. **Thread**: uno per coppia (sessione live, host)? Con l'host unico del pilota coincide con uno per sessione.
