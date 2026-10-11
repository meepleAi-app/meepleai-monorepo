# Tetto di spesa LLM

**Origine**: [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) D4, D5 e D6 · fetta 3 ([#4166](https://github.com/meepleAi-app/meepleai-monorepo/issues/4166)) · piano: [`2026-10-10-adr-095-slices-plan.md`](../plans/2026-10-10-adr-095-slices-plan.md)

Il gateway LLM (`HybridLlmService`) **verifica il budget prima di ogni chiamata a un provider**. Una chiamata che porterebbe la spesa oltre il tetto non parte, e fra la soglia di degrado e il tetto le risposte passano al modello di riserva mentre il background si ferma. Prima della fetta 3 il budget si guardava solo a posteriori: un job orario, con tetti di `appsettings`, passava a modelli più economici, ma nessuna chiamata veniva fermata.

## Cosa controlla

Il controllo è `LlmBudgetGate`. Per ogni chiamata:

1. **Modello nel catalogo** (D5, *fail closed*). Se il catalogo `AiModelConfiguration` non prezza il modello, la chiamata è rifiutata: registrata a $0, sfuggirebbe al tetto. Un modello Ollama vale come gratuito.
2. **Stima del costo massimo**: i caratteri del prompt diviso tre (per eccesso, i prompt in italiano hanno meno caratteri per token dell'inglese), più 1.500 token per immagine, più la lunghezza massima della risposta, ai prezzi del catalogo. Il tetto scatta quindi un po' prima del consumo reale.
3. **Tetti**: la spesa del giorno (UTC) più la stima non deve superare il tetto giornaliero; quella del mese, il tetto mensile.
4. **Quote**: il background e le valutazioni hanno ciascuno una quota del tetto, sia del giorno sia del mese. Le risposte agli utenti possono usare tutto il tetto.
5. **Pausa oltre la soglia**: quando la spesa del giorno ha raggiunto la soglia di degrado, il background e le valutazioni non partono più, anche con la quota intatta (vedi [Degrado](#degrado)).

Una chiamata a un modello gratuito passa sempre.

| Classe | Sorgenti (`RequestSource`) |
|---|---|
| Risposte | `Manual`, `RagPipeline`, `RagClassification`, `Translation`, `AgentTask` (chat dell'agente di sessione, risposta ancorata, arbitro, riepilogo del turno) |
| Background | `ChunkTranslation`, `AdminOperation` (estrattore di meccaniche, playground, chat di debug), `EventDriven`, `AutomatedTest` |
| Valutazioni | `Evaluation` (KbQuality, valutazione dei dataset), `ABTesting` |

La spesa viene dal registro `llm_cost_logs` e si rilegge ogni 30 secondi. Fra due letture il gate somma da sé le riserve delle chiamate in corso e i costi reali di quelle chiuse: due chiamate concorrenti non passano sullo stesso margine. Se la lettura fallisce si riusa quella dello stesso giorno, e si riprova dopo 5 secondi, non a ogni chiamata.

Uno stream che il consumatore interrompe dopo aver ricevuto testo non arriva al chunk finale, quindi il suo costo non entra nel registro; il provider però lo fattura. Il gate lo conta con la stima fino alla fine del mese (in memoria: un riavvio lo dimentica).

## Valori e dove si cambiano

I valori stanno in `SystemConfiguration.LlmSystemConfigs`. Finché l'admin non salva una riga valgono i predefiniti di `LlmSystemConfig`:

| Campo | Predefinito | Significato |
|---|---|---|
| `DailyBudgetUsd` | 1 $ | tetto giornaliero |
| `MonthlyBudgetUsd` | 20 $ | tetto mensile, il vincolo di ADR-095 (sotto 20 € al mese) |
| `BackgroundQuotaPercent` | 30 | quota del background, in percentuale del tetto |
| `EvaluationQuotaPercent` | 20 | quota delle valutazioni; insieme al background non supera 100 |
| `DegradeThresholdPercent` | 80 | soglia del degrado, sul consumo del giorno |

- **Tetti**: `/admin/ai` → *LLM config*, oppure `PUT /api/v1/admin/llm/config`.
- **Soglia e quote**: per ora solo l'API, con i campi opzionali `degradeThresholdPercent`, `backgroundQuotaPercent` ed `evaluationQuotaPercent`. Un campo assente lascia il valore salvato. La pagina arriva con la fetta 6 (#4169).
- Una modifica vale entro un minuto, il tempo della cache di `LlmSystemConfigProvider`.

**Tetto a zero**: ferma ogni chiamata a pagamento. È il modo di spegnere l'AI senza toccare i provider.

Il messaggio del rifiuto è in italiano, come gli altri messaggi d'errore che il backend manda agli utenti.

## Degrado

D6 di ADR-095 definisce tre fasce sul consumo del giorno:

| Spesa del giorno | Risposte | Background e valutazioni |
|---|---|---|
| sotto la soglia (80%) | modello principale | entro la loro quota |
| fra la soglia e il tetto | **modello di riserva** del profilo agente | **in pausa** fino a domani |
| oltre il tetto | non partono | non partono |

La spesa che conta è quella di tutta l'AI: il registro, più le chiamate chiuse e quelle in corso dopo l'ultima lettura (`LlmBudgetGate.IsDegradedAsync`). La spesa di ieri non conta, anche se resta nel mese. Con il tetto giornaliero a zero non c'è degrado: l'AI a pagamento è spenta e le chiamate gratuite passano come sempre.

**Modello di riserva.** È il `FallbackModelId` della versione **pubblicata** del profilo agente (#4167), letto al più una volta al minuto (`PublishedProfileReserveModelProvider`). Pubblicare una versione con una riserva diversa vale quindi entro un minuto. Il passaggio riguarda le risposte instradate, unarie e in streaming. Una chiamata a modello esplicito tiene il suo modello: il playground, per esempio, prova il modello che l'admin ha scelto.

La risposta resta sul modello principale, con un warning nel log, quando:

- il profilo non ha una riserva, o non si legge e non è mai stato letto;
- nessun provider configurato serve il modello di riserva;
- il circuito del provider della riserva è aperto;
- la riserva non è nel catalogo, e verrebbe rifiutata (D5);
- la riserva non costa meno del modello instradato (`LlmBudgetGate.IsCheaperReserveAsync`, confronto sul prezzo di un milione di token in ingresso più uno in uscita). Un modello gratuito non viene quindi mai sostituito. Un modello instradato fuori catalogo invece sì, perché verrebbe rifiutato.

Se la riserva fallisce (provider giù, chiave rifiutata), la risposta torna al modello instradato: in streaming quando la riserva chiude lo stream senza contenuto, nelle chiamate unarie prima della catena di fallback. Il degrado non deve rendere una risposta meno disponibile. La riserva passa dallo stesso gate di ogni altra chiamata, e il suo costo conta verso il tetto. Il profilo pubblicato con #4179 ha `meta-llama/llama-3.3-70b-instruct` su OpenRouter (0,22 $ + 0,50 $ per milione di token) contro `deepseek-flash` (0,30 $ + 1,20 $).

**Background in pausa.** Le chiamate rifiutate hanno `reason=BackgroundPaused` nella metrica, il codice `AI_BUDGET_DAILY_EXHAUSTED` e il messaggio «Per oggi il lavoro in background è in pausa…» (o «le valutazioni»). Riprendono il giorno dopo, o prima se l'admin alza il tetto giornaliero o la soglia.

## KbQuality

Il tetto di KbQuality è la **quota delle valutazioni** (D6: «il tetto di KbQuality diventa una quota dentro il tetto unico»). Prima della fetta 3b era un contatore per tenant di 50 $ al mese (`EvalQuality:MonthlyCostCap`), fuori da ogni altro budget.

- `EvalCostCapBehavior` confronta la stima della valutazione con quanto resta della quota (`ILlmBudgetStatus.GetRemainingUsdAsync(RequestSource.Evaluation)`). Il residuo è il minimo fra la quota del giorno, la quota del mese e i due tetti, e vale zero oltre la soglia di degrado. Una stima più alta risponde `402` con il residuo, come prima.
- Le chiamate LLM della valutazione (la generazione delle domande) passano dal gateway con `RequestSource.Evaluation`: il gate le conta e le ferma alla quota. Il behavior non incrementa più nulla.
- **L'override dell'admin** (`overrideCostCap`) salta la stima, non la quota: le chiamate le ferma comunque il gate. Chi ha bisogno di più alza `EvaluationQuotaPercent`.
- La tabella `kb_quality_budget_counters` non si legge né si scrive più. Resta finché una consegna successiva non la toglie (expand → contract); il job di pulizia mensile continua a svuotarla.

## Allarmi

Leggono la stessa politica che il gate applica, non più i propri valori di `appsettings`.

- **`LlmBudgetMonitoringService`** (ogni `LlmBudgetAlerts:CheckIntervalMinutes`, 60 minuti): sulla spesa del giorno e del mese, un warning alla soglia di degrado e un allarme critico al tetto. Un tetto a zero vuol dire AI a pagamento spenta, quindi non genera allarmi.
- **`LlmCostAlertService`** (comando `CheckLlmCostAlerts`): un allarme oltre il tetto del giorno, oltre sette volte il tetto del giorno sulla settimana, e quando la proiezione del mese supera il tetto mensile. Anche qui un tetto a zero non genera allarmi.

Il **budget mode** (`LlmModelOverrideService`) non c'è più. Sostituiva per tutto il processo i modelli cari con quelli economici quando il job orario vedeva superata una soglia, senza fermare nulla. Il suo posto l'ha preso il degrado del gateway, che agisce prima della chiamata e con il modello che l'admin ha scelto nel profilo. Le chiavi `LlmBudgetAlerts:DailyBudgetUsd`, `MonthlyBudgetUsd` e le soglie in `appsettings` non si leggono più, e sono state tolte.

## Cosa vede chi chiama

- **Chat in streaming** (`/agents/qa/stream`): un evento `Error` con il codice
  - `AI_BUDGET_DAILY_EXHAUSTED` (tetto o quota del giorno, background in pausa), che il frontend mostra come «Il servizio AI è in pausa fino a domani…»;
  - `AI_BUDGET_MONTHLY_EXHAUSTED` (del mese);
  - `AI_MODEL_NOT_IN_CATALOG` (modello fuori catalogo);
  - `AI_BUDGET_UNAVAILABLE` (catalogo o spesa illeggibili).

  Lo stesso vale per la chat cross-game, la chat dell'agente di sessione e la chat di debug. Nulla finisce nella cache delle risposte.
- **Chiamate non in streaming**: un `LlmCompletionResult` fallito, con `Metadata["error_code"]` e `Metadata["budget_rejection"]`.
- **Playground admin**: il messaggio al posto della risposta; l'esecuzione è salvata come errore.
- **Traduzione del librogame**: `503` con il codice e il messaggio (`LlmCallRefusedException`).
- **Valutazione dei dataset**: il campione risulta fallito, con il motivo, invece di una risposta vuota valutata come sbagliata.

## Osservabilità

- Metrica: `meepleai_llm_budget_rejected_calls_total`, con le etichette `reason` (`DailyCapReached`, `MonthlyQuotaReached`, `BackgroundPaused`, `ModelNotInCatalog`, …) e `budget_class`.
- Log: un warning per chiamata rifiutata, con la stima e la spesa del giorno e del mese; un warning per risposta passata al modello di riserva («Budget degrade: …»).

Le chiamate rifiutate non scrivono righe nel registro costi, perché non hanno raggiunto il provider.

## Limiti noti

- **Streaming senza fallback.** Lo streaming instradato non passa a un altro provider se il modello scelto è fuori catalogo: non aveva fallback nemmeno prima (C8 di ADR-095), e lo avrà con la pipeline della fetta 5. Il messaggio dice quale modello aggiungere al catalogo.
- **Una sola istanza.** Le riserve stanno in memoria. Con due istanze dell'API, ciascuna vedrebbe solo le proprie fino alla lettura successiva del registro. È il contratto di staging (ADR-087).
- **Spesa illeggibile.** Se il registro non si legge, si riusa la lettura dello stesso giorno, se c'è; altrimenti la chiamata non parte.
- **Multimodale senza degrado.** Le chiamate con immagini tengono il loro modello: la riserva del profilo è un modello di solo testo, e non tutte le riserve leggono immagini. Il tetto le ferma come le altre.
- **Riserva che lancia in streaming.** Il ritorno al modello instradato copre la riserva che chiude lo stream senza contenuto, il modo in cui i client riportano un errore del provider. Un client che invece lancia un'eccezione la propaga al consumatore, come senza degrado.
- **Riserva ignota alla prima lettura fallita.** Se il profilo non si è mai letto e la prima lettura fallisce, per un minuto non c'è riserva e le risposte restano sul modello principale (che il tetto continua a fermare).
