# Agente unico configurabile — brief di architettura

**Data**: 2026-10-10 · **Origine**: `/sc:brainstorm` · **Stato**: brief. Le decisioni sono ratificate in [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) (Proposed), che le precisa: il profilo è un **nuovo aggregato** `AgentProfile` invece di un'evoluzione di `AgentDefinition`, ed esiste un **gateway LLM unico** per tutte le chiamate.
**Contesto**: [ADR-094](../../for-claude/architecture/adr/adr-094-single-system-agent.md) (un solo agente di sistema), [ADR-090](../../for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md) (`KnowledgeBase` possiede la risposta ancorata), [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md), report Azure `docs/for-developers/research/2026-10-08-azure-ai-200-provider-switch-research.md` (provider intercambiabili; al 2026-10-10 è ancora un file non committato del checkout principale, non su `main-dev`).

## Decisioni del brainstorming

| Domanda | Scelta |
|---|---|
| Architettura | **Una sola pipeline di risposta + un profilo dell'agente con versioni** (bozza → pubblicato → archiviato) |
| Priorità nella scelta del modello | **Cambiare provider facilmente**: nessun fornitore privilegiato, il modello si sceglie dall'admin |
| Cosa cambia l'admin senza deploy | **Modello e parametri · prompt per lingua · recupero e fonti · budget e quote** |
| Budget mensile in token nei prossimi 6 mesi | **Sotto 20 €** |

## Il problema, misurato sul codice di `origin/main-dev` (`9ea893dc0`)

1. **L'agente di ADR-094 non governa nessuna risposta.** `AgentDefinition.Config`, `Prompts` e `Strategy` sono letti solo da `PlaygroundChatCommandHandler`. Le risposte agli utenti ignorano l'agente.
2. **Dieci e più punti d'ingresso producono risposte LLM, ciascuno con le sue regole**: `StreamQaQueryHandler`, `AskQuestionQueryHandler`, `CrossGameStreamQaQueryHandler`, `ChatWithSessionAgentCommandHandler`, `AskSessionAgentCommandHandler`, `StreamSetupGuideQueryHandler`, la disputa sulle regole, le traduzioni del librogame, il toolkit da KB e altri.
   - Il recupero ha 4-5 implementazioni di fusione.
   - `topK` e `minScore` sono fissati in ogni handler.
   - Temperatura 0,3 e `maxTokens` 1500 sono fissi in `HybridLlmService`.
   - I prompt arrivano da tre fonti: database, `appsettings` e testo nel codice.
   - La lingua del recupero è fissata a `"en"` in `StreamQa`.
3. **L'admin non può cambiare ciò che conta, e può cambiare ciò che non conta.**
   - La mappatura strategia → modello (`StrategyModelMapping`) è l'unica impostazione efficace, ed è in sola lettura nella UI.
   - I due prompt letti dal database si possono cancellare con un clic, senza conferma, ma non modificare.
   - Budget, fallback chain, override KB per gioco e i campi di recupero del tab Strategy vengono salvati («Changes saved») e nessuno li legge.
4. **I costi non si governano.**
   - Il prezzo viene da un listino scritto nel codice (`LlmCostCalculator`), che non concorda con quello nel database (`AiModelConfiguration`). Un modello assente vale $0.
   - I costi non sono attribuiti all'utente.
   - Le quote per utente non bloccano: `UserBudgetService` fail-open, `TierAction.AgentQuery` senza chiamanti, la policy di rate limit `AgentQuery` non montata.
   - I lavori in background (estrazione meccaniche, traduzioni, valutazioni KbQuality, con un tetto proprio di $50 al mese) spendono fuori da ogni tetto.
5. **La qualità non si vede.** I feedback 👍👎 si leggono solo per un gioco alla volta, con l'UUID digitato a mano, e nessuna pagina misura la qualità delle risposte.

## Architettura proposta

### 1. Il profilo dell'agente: una configurazione, non un CRUD

L'unico `AgentDefinition` di sistema diventa un **profilo con versioni**. Una sola versione è pubblicata alla volta, e la pipeline legge sempre quella.

| Sezione | Contenuto | Oggi |
|---|---|---|
| Modello | modello principale e di riserva, scelti dal **catalogo modelli** | `StrategyModelMapping` (non modificabile) |
| Generazione | temperatura, lunghezza massima, stile delle citazioni | fissi nel codice |
| Prompt | prompt di sistema **per lingua**, persona | tre fonti diverse |
| Recupero | quanti passaggi, soglia, pesi della fusione, reranker sì/no, lingua della query | fissi in ogni handler |
| Fonti | tipi di documento ammessi (regolamento, FAQ, errata, house rules) e politica copyright | impliciti |
| Budget | tetti e degrado (sotto) | sparsi e in parte morti |

Ciclo di vita: **bozza → pubblicata → archiviata**. Si torna indietro ripubblicando una versione archiviata. Ogni risposta registra la versione del profilo che l'ha prodotta: è così che costi, feedback e valutazioni si attribuiscono a una versione.

### 2. Una sola pipeline di risposta

È di proprietà di `KnowledgeBase`, come già stabilisce ADR-090, e la usano tutti i punti d'ingresso. Ognuno passa solo `{domanda, ambito (un gioco | i giochi accessibili), lingua, utente, contesto opzionale della sessione}`.

```
accesso (possesso / KB pubblica — #4155)
  → budget e quota (prima di spendere)
  → recupero (una sola implementazione) → reranking
  → prompt dal profilo pubblicato (lingua dell'utente)
  → LLM tramite il livello provider (con cache del prompt)
  → controllo copyright → citazioni
  → registro: costo attribuito all'utente e alla versione del profilo, latenza, «non lo so», id risposta per il feedback
```

Migrazione per fette, ciascuna verificabile:

1. le tre chat: `StreamQa`, `AskQuestion` e la ricerca cross-game;
2. l'assistente in sessione (`ChatWithSession`, `AskSessionAgent`);
3. la guida al setup e la disputa sulle regole. Oggi la disputa chiede citazioni di pagina senza recuperare passaggi.

Le funzioni che non sono «domande sulle regole» (traduzione del librogame, generazione toolkit) **non** passano dalla pipeline. Passano però dallo stesso livello provider e dallo stesso budget.

### 3. Il livello provider, intercambiabile

- **Un'astrazione sola per chat ed embedding.** Il report Azure indica `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator`), con middleware per cache e OpenTelemetry. I client esistenti (OpenRouter, DeepSeek, Ollama) ci si adattano, e un profilo Azure diventa un'altra registrazione.
- **Un solo catalogo modelli nel database** (`AiModelConfiguration`): provider, id del modello, prezzi in ingresso, in uscita e cache, abilitato sì/no, regione dei dati. Il listino in `LlmCostCalculator` sparisce. Un modello senza prezzo **non si può pubblicare** nel profilo.
- **Il profilo sceglie dal catalogo.** Cambiare provider significa pubblicare una nuova versione del profilo, senza deploy.

### 4. Il budget: un tetto unico per tutta l'AI

Con meno di 20 € al mese il tetto dev'essere **unico e reale**: vale per tutte le chiamate LLM, sia degli utenti sia dei lavori in background, e si applica **prima** della chiamata.

- **Tetto mensile e tetto giornaliero** (mensile/30, con un margine). Le soglie pilotano il degrado:

  | consumo del giorno | effetto |
  |---|---|
  | < 80% | normale |
  | 80-100% | si passa al modello di riserva più economico del profilo; i lavori in background vengono messi in pausa |
  | ≥ 100% | solo risposte dalla cache; per le domande nuove, un messaggio chiaro all'utente: «servizio AI in pausa fino a domani» |

- **Quote per piano** applicate davvero, nella pipeline: domande al giorno per utente.
- **Leve di risparmio, prima di cambiare modello:**
  - la cache del prompt, perché prompt di sistema e passaggi dello stesso gioco si ripetono;
  - la cache delle risposte per gioco, che già esiste per `AskQuestion`;
  - il reranking per mandare meno passaggi al modello.
- **I lavori in background hanno una quota propria** dentro il tetto: KbQuality oggi ha 50 $ al mese da solo, più dell'intero budget.

### 5. Il ciclo dell'admin

| Pagina | Cosa fa | Sostituisce |
|---|---|---|
| **Agente** | profilo: bozza, confronto con la versione pubblicata, **prova** nel playground (stessa pipeline), pubblica, storico | `/admin/agents/definitions`, il tab Strategy, `/admin/ai?tab=prompts` |
| **Modelli e prezzi** | catalogo: abilita, prezzi, regione, stato di salute | `/admin/ai?tab=models`, `/admin/agents/config?tab=models` |
| **Budget e quote** | tetti, soglie di degrado, quote per piano, consumo del giorno e del mese (utenti e background) | `/admin/ai?tab=config` (budget morti), i limiti sparsi |
| **Qualità** | domande di riferimento per gioco (domanda, pagine attese, risposta attesa); valutazione di una bozza **prima** di pubblicarla; feedback 👍👎 aggregati e collegati alla risposta e alla versione | `/admin/knowledge-base/feedback` (UUID a mano) |
| **Costi** | costo per domanda, per versione, per utente e per provider; tendenza verso il tetto | sei pagine sparse |

**Regola di pubblicazione**: una bozza si pubblica solo se la valutazione sulle domande di riferimento non peggiora rispetto alla versione pubblicata, e se il suo costo stimato per domanda sta dentro il budget. L'A/B test fra versioni (che già esiste) è un'opzione successiva.

## Costi — stima per scegliere il modello predefinito

**Ipotesi**: circa 3.000 token in ingresso e 500 in uscita per domanda, senza cache. Una chiamata reale del 2026-10-10 ne ha usati 3.761 in tutto. I prezzi sono di listino, ottobre 2026, da fonti secondarie dove indicato.

| Modello | per 1.000 domande | domande che stanno in 20 € al mese |
|---|---|---|
| DeepSeek V4-Flash ($0,14 / $0,28) `(sec.)` | ~$0,56 | ~38.000 |
| GPT-5 mini ($0,25 / $2,00) `(sec.)` | ~$1,75 | ~12.000 |
| Gemini 2.5 Flash ($0,30 / $2,50) `(sec.)` | ~$2,15 | ~10.000 |
| Claude Haiku 4.5 ($1 / $5) | ~$5,50 | ~3.900 |
| Claude Sonnet 4.5 ($3 / $15, non riverificato) | ~$16,50 | ~1.300 |

Fissi: il VPS Hetzner (circa 16 € al mese) ospita embedding e reranker, quindi il loro costo per domanda è zero. Un profilo Azure sempre acceso costa 150-250 $ al mese (report del 2026-10-08).

**Lettura**: con 20 € al mese, qualunque modello «flash/mini» copre la fase alpha con ampio margine. Quello che manda fuori budget non sono le domande degli utenti, ma i lavori in background senza tetto e un modello di fascia alta scelto per errore. Per questo il budget sta nella pipeline e nel catalogo, non in un avviso.

## Impatto sulle issue aperte

- **#4154**: con la scelta della rimozione, il lancio dell'assistente in sessione non deve più scegliere un agente, perché la pipeline usa il profilo pubblicato. Le rotte utente `GET /agents?scope=my-library` e `GET /games/{id}/agents` si ritirano (expand → contract). `AgentDto` resta solo all'admin, come DTO del profilo.
- **#4155**: il controllo di possesso è il primo passo della pipeline. Il codice d'errore e la CTA restano come da design.
- **#4156**: l'assistente in sessione entra nella pipeline alla fetta 2. Il contratto `/live-sessions` va fissato prima.

## Domande aperte

1. **Modello predefinito.** La tabella dice DeepSeek V4-Flash per costo, ma elabora i dati in Cina, e l'alias `deepseek-chat` in uso è dato per ritirato da una fonte secondaria (oggi risponde ancora). Da verificare sulla pagina ufficiale prima di sceglierlo.
2. **Chi scrive le domande di riferimento** per la valutazione, e per quali giochi si parte. Un insieme piccolo e curato vale più di uno grande e generato.
3. **Lingue**: italiano e inglese bastano per l'alpha? Il profilo ha prompt per lingua; la lingua della query di recupero va allineata a quella dell'utente.
4. **Streaming ovunque?** Oggi lo streaming non aggiorna il circuit breaker e non ha fallback: la pipeline deve dare le stesse garanzie in streaming e non.
5. **Pulizia**: le impostazioni morte (budget di `LlmSystemConfig`, `FallbackChainJson`, override KB per gioco, `AdminRagStrategy`, chiavi `LlmRouting:*Model`, tipologie) vanno tolte o collegate, non lasciate a «salvare» a vuoto.

## Prossimi passi

1. ADR: pipeline unica, profilo con versioni, catalogo modelli come unico listino, tetto unico di budget.
2. Piano a fette, ciascuna con il suo test «rosso prima»:
   1. il catalogo modelli come listino unico e il registro costi attribuito;
   2. il tetto unico prima della chiamata;
   3. la pipeline per le tre chat;
   4. la pagina Agente con le versioni;
   5. Qualità;
   6. la sessione.
