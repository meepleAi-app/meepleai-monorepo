# Tetto di spesa LLM

**Origine**: [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) D4, D5 e D6 · fetta 3 ([#4166](https://github.com/meepleAi-app/meepleai-monorepo/issues/4166)) · piano: [`2026-10-10-adr-095-slices-plan.md`](../plans/2026-10-10-adr-095-slices-plan.md)

Il gateway LLM (`HybridLlmService`) **verifica il budget prima di ogni chiamata a un provider**. Una chiamata che porterebbe la spesa oltre il tetto non parte, e fra la soglia di degrado e il tetto le risposte passano al modello di riserva mentre il background si ferma. Prima della fetta 3 il budget si guardava solo a posteriori: un job orario, con tetti di `appsettings`, passava a modelli più economici, ma nessuna chiamata veniva fermata.

## Cosa controlla

Il controllo è `LlmBudgetGate`. Per ogni chiamata:

1. **Modello nel catalogo** (D5, *fail closed*). Se il catalogo `AiModelConfiguration` non prezza il modello, la chiamata è rifiutata: registrata a $0, sfuggirebbe al tetto. Un modello Ollama vale come gratuito. I prezzi si leggono e si cambiano in `/admin/ai?tab=models`, tab **Models** (#4169), oppure con `PATCH /api/v1/admin/ai-models/{id}/pricing`. Un prezzo cambiato vale entro un minuto, ed è registrato come verificato oggi dall'admin, così il seed non lo sovrascrive con un listino più vecchio. Attenzione: un modello di un provider a pagamento con prezzo 0/0 conta come gratuito e sfugge al tetto. La pagina lo segnala come «No price».
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

- **Tetti, soglia e quote**: `/admin/ai?tab=config`, tab **Budget** (#4169), oppure `PUT /api/v1/admin/llm/config`. Nell'API i campi `degradeThresholdPercent`, `backgroundQuotaPercent` ed `evaluationQuotaPercent` sono opzionali: un campo assente lascia il valore salvato.
- **Consumo**: la stessa pagina mostra la spesa di oggi e del mese, per classe, e se il degrado è attivo. È la vista del gate, cioè il registro più le chiamate in corso, servita da `GET /api/v1/admin/llm/budget/usage`. Se la spesa non si legge, la pagina lo dice: in quel caso il gate rifiuta le chiamate a pagamento.
- **Non si cambiano più dalla pagina** (#4169): le soglie del circuit breaker, che il runtime prende da `AI:CircuitBreaker` di appsettings, e la catena di fallback (`fallbackChainJson`), che il runtime non ha mai letto. L'ordine di fallback vero viene dai modelli attivi del catalogo. Nell'API restano accettate, opzionali, finché le colonne non escono.
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

## Risposte della pipeline (#4168)

Le risposte della pipeline unica (ADR-095 D3) passano da un ingresso proprio del gateway, `GenerateAnswerStreamAsync` e `GenerateAnswerAsync`. Modello principale, riserva, temperatura e lunghezza massima arrivano dalla versione pubblicata del profilo, non dalla strategia di instradamento. Ci passano la chat in streaming (`/agents/qa/stream`, #4219) e la domanda singola (`/knowledge-base/ask`, `/agents/qa`, l'auto-test admin); la ricerca cross-game segue con la fetta 5e, la sessione con la fetta 8. Sulla domanda singola un rifiuto non è più una risposta vuota: è un `503` con il codice (`LlmCallRefusedException`). Le garanzie sono le stesse in streaming e in risposta unica:

| caso | cosa succede |
|---|---|
| il principale fallisce **prima del primo token** (stream vuoto, eccezione) | si prova la riserva; il circuito del principale conta il guasto |
| il circuito del principale è aperto | il principale non si chiama, si passa alla riserva |
| tetto o quota raggiunti | la risposta si ferma, senza provare la riserva: spenderebbe dallo stesso budget |
| il principale è fuori catalogo | si passa alla riserva; se nessuno dei due è nel catalogo, il rifiuto è `AI_MODEL_NOT_IN_CATALOG` |
| degrado e riserva più economica | la riserva parte per prima, il principale resta dietro |
| lo stream si interrompe **dopo il primo token** | nessun secondo modello. L'ultimo chunk porta `AI_STREAM_INTERRUPTED`. Il costo stimato dei token generati (circa 4 caratteri per token, sul listino del catalogo) entra nel registro come riga fallita, e il circuito conta il guasto |
| nessun modello risponde | un solo chunk con `AI_PROVIDER_UNAVAILABLE`, mai uno stream vuoto. Il dettaglio del provider resta nei log |

Ogni tentativo aggiorna il circuit breaker, tranne quando è il client a smettere di leggere. Ogni riga del registro costi di una risposta porta la versione del profilo (`llm_cost_logs.profile_version_id`, ADR-095 D2). Fanno eccezione le righe dei tentativi falliti senza token, che non hanno costo.

## La quota di domande del piano (#4168, 5g-b)

Non è un tetto di spesa: è il numero di domande al giorno che il piano dell'utente prevede (`TierDefinition.Limits.MaxAgentQueriesPerDay`, modificabile dalla pagina dei piani dell'admin). Sta nella pipeline al passo 2 di ADR-095 D3, dopo l'accesso e prima di tutto il resto, nel passo che i tre percorsi di risposta condividono (`IAnswerPreconditions`): la chat (`/agents/qa/stream`), la domanda singola (`/knowledge-base/ask`, `/agents/qa`) e la domanda fra giochi (`/knowledge-base/ask/global`).

| caso | cosa succede |
|---|---|
| la domanda arriva oltre la quota | si ferma prima della cache e prima del gateway. Nello stream: un evento `Error` con codice `TIER_LIMIT_EXCEEDED` e i campi `limitType`, `current`, `max`, `upgradeUrl`. Fuori dallo stream: `403` con `error: "tier_limit_exceeded"` e gli stessi campi |
| la risposta è stata consegnata, dal modello o **dalla cache** | il contatore cresce di uno, prima dell'evento `Complete` |
| il gateway rifiuta (`AI_BUDGET_*`), la risposta si interrompe a metà, l'accesso è negato, la ricerca non trova niente | il contatore non cresce |
| una domanda fra giochi | conta uno, quanti che siano i giochi |
| admin ed editor | nessun limite, e il contatore non cresce |
| Redis non risponde | la domanda passa. È voluto: la spesa la protegge il tetto del gateway, che invece blocca (`AI_BUDGET_UNAVAILABLE`) |

**Il contatore** è in Redis: `tier:usage:{userId}:AgentQuery:{yyyy-MM-dd}`, dove il giorno è quello **UTC**, e scade da solo dopo due giorni. Per questo il frontend dice all'utente l'ora del suo fuso in cui il conteggio riparte, con «mezzanotte UTC» accanto.

**Quando Redis è giù la quota non vale per nessuno.** Lo si vede da un warning nel log dell'API («The plan quota of user … could not be read») e dalla metrica `meepleai.answer.quota.unreadable`, che su Prometheus si chiama `meepleai_answer_quota_unreadable_checks_total` (l'exporter aggiunge l'unità e `_total`). Non c'è ancora un allarme che la legga.

**Verificare e contare sono due momenti**: due domande partite insieme all'ultima disponibile passano entrambe. È accettato (#4168).

**Che cosa non conta nella quota**: il playground dell'admin, la valutazione dei dataset e l'auto-test del wizard, che non passano dai tre percorsi; e i gestori che generano con l'LLM sulla KB per conto loro (arbitro, tutor, suggerimento di mossa, guida alla preparazione), che hanno un cancello d'accesso ma non il passo condiviso. Se debbano contare è una decisione aperta in #4168.

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

## Costo per partita dell'assistente (#4456)

Il criterio 3 del pilota dell'assistente in partita (spec `2026-10-11-session-assistant-agent-design.md`, §7.3) confronta il costo di una partita con una quota del tetto mensile. Ogni turno dell'assistente salva sul messaggio di risposta il costo e i token di **tutte** le chiamate al modello che ha fatto, anche quando si ferma con un codice (budget, scadenza): sono i campi `CostUsd` e `TokenCount` nel JSON dei messaggi del thread della partita (`"ChatThreads"."MessagesJson"`, riconoscibile da `"LiveSessionId"`). Le domande non hanno costo, e i messaggi salvati prima di #4456 non hanno la chiave: il loro costo è assente, non zero.

**Costo per partita**, dalla più cara:

```sql
SELECT t."LiveSessionId"                                      AS partita,
       count(*) FILTER (WHERE m->>'Role' = 'assistant')       AS turni,
       sum((m->>'CostUsd')::numeric)                          AS costo_usd,
       count(*) FILTER (WHERE m->>'Role' = 'assistant'
                          AND m->>'CostUsd' IS NULL)          AS turni_senza_costo
FROM "ChatThreads" t
CROSS JOIN LATERAL jsonb_array_elements(t."MessagesJson"::jsonb) AS m
WHERE t."LiveSessionId" IS NOT NULL
GROUP BY t."LiveSessionId"
ORDER BY costo_usd DESC NULLS LAST;
```

**È una stima, e si riconcilia col registro.** Un turno finito in eccezione prima del salvataggio (`assistant_unavailable` dal `catch` del turno) non lascia messaggio: il suo costo sta solo in `llm_cost_logs`. La differenza fra le due somme, nella stessa finestra, è il costo che i messaggi non portano: i turni persi così, più i turni salvati prima di #4456. Le date della finestra sono in UTC; meglio una finestra di giorni, perché il registro data ogni chiamata e il messaggio la fine del turno.

```sql
WITH salvati AS (
  SELECT sum((m->>'CostUsd')::numeric) AS costo
  FROM "ChatThreads" t
  CROSS JOIN LATERAL jsonb_array_elements(t."MessagesJson"::jsonb) AS m
  WHERE t."LiveSessionId" IS NOT NULL
    AND m->>'Role' = 'assistant'
    AND (m->>'Timestamp')::timestamptz >= timestamptz '2026-10-01 00:00+00'
    AND (m->>'Timestamp')::timestamptz <  timestamptz '2026-11-01 00:00+00'
), registro AS (
  SELECT sum(total_cost_usd) AS costo
  FROM llm_cost_logs
  WHERE request_source = 'SessionAssistant'
    AND created_at >= timestamptz '2026-10-01 00:00+00'
    AND created_at <  timestamptz '2026-11-01 00:00+00'
)
SELECT registro.costo                              AS registro_usd,
       salvati.costo                               AS salvati_usd,
       registro.costo - coalesce(salvati.costo, 0) AS non_attribuito_usd
FROM registro, salvati;
```

Le valutazioni del set di scenari hanno `request_source = 'Evaluation'` e non entrano in nessuna delle due somme. Un tetto **per partita**, imposto dal sistema, chiederebbe invece l'id della sessione nelle righe del registro: oggi nessuno lo chiede, e se arriva si riapre da #4456.

## Osservabilità

- Metrica: `meepleai_llm_budget_rejected_calls_total`, con le etichette `reason` (`DailyCapReached`, `MonthlyQuotaReached`, `BackgroundPaused`, `ModelNotInCatalog`, …) e `budget_class`.
- Log: un warning per chiamata rifiutata, con la stima e la spesa del giorno e del mese; un warning per risposta passata al modello di riserva («Budget degrade: …»).

Le chiamate rifiutate non scrivono righe nel registro costi, perché non hanno raggiunto il provider.

## Limiti noti

- **Streaming instradato senza fallback.** Lo streaming instradato, quello delle chat non ancora passate sulla pipeline, non va a un altro provider se il modello scelto è fuori catalogo e non aggiorna il circuit breaker (C8 di ADR-095). Il messaggio dice quale modello aggiungere al catalogo. Le risposte della pipeline hanno l'una e l'altra cosa (sezione sopra), e le chat ci passano con le fette 5c–5e e 8.
- **Una sola istanza.** Le riserve stanno in memoria. Con due istanze dell'API, ciascuna vedrebbe solo le proprie fino alla lettura successiva del registro. È il contratto di staging (ADR-087).
- **Spesa illeggibile.** Se il registro non si legge, si riusa la lettura dello stesso giorno, se c'è; altrimenti la chiamata non parte.
- **Multimodale senza degrado.** Le chiamate con immagini tengono il loro modello: la riserva del profilo è un modello di solo testo, e non tutte le riserve leggono immagini. Il tetto le ferma come le altre.
- **Riserva che lancia in streaming.** Il ritorno al modello instradato copre la riserva che chiude lo stream senza contenuto, il modo in cui i client riportano un errore del provider. Un client che invece lancia un'eccezione la propaga al consumatore, come senza degrado.
- **Riserva ignota alla prima lettura fallita.** Se il profilo non si è mai letto e la prima lettura fallisce, per un minuto non c'è riserva e le risposte restano sul modello principale (che il tetto continua a fermare).
