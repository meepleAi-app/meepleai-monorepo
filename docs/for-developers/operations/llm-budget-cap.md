# Tetto di spesa LLM

**Origine**: [ADR-095](../../for-claude/architecture/adr/adr-095-single-answer-pipeline-and-versioned-agent-profile.md) D4, D5 e D6 · fetta 3 ([#4166](https://github.com/meepleAi-app/meepleai-monorepo/issues/4166)) · piano: [`2026-10-10-adr-095-slices-plan.md`](../plans/2026-10-10-adr-095-slices-plan.md)

Il gateway LLM (`HybridLlmService`) **verifica il budget prima di ogni chiamata a un provider**. Una chiamata che porterebbe la spesa oltre il tetto non parte. Prima della fetta 3 il budget si guardava solo a posteriori: un job orario, con tetti di `appsettings`, passava a modelli più economici, ma nessuna chiamata veniva fermata.

## Cosa controlla

Il controllo è `LlmBudgetGate`. Per ogni chiamata:

1. **Modello nel catalogo** (D5, *fail closed*). Se il catalogo `AiModelConfiguration` non prezza il modello, la chiamata è rifiutata: registrata a $0, sfuggirebbe al tetto. Un modello Ollama vale come gratuito.
2. **Stima del costo massimo**: i caratteri del prompt diviso tre (per eccesso, i prompt in italiano hanno meno caratteri per token dell'inglese), più 1.500 token per immagine, più la lunghezza massima della risposta, ai prezzi del catalogo. Il tetto scatta quindi un po' prima del consumo reale.
3. **Tetti**: la spesa del giorno (UTC) più la stima non deve superare il tetto giornaliero; quella del mese, il tetto mensile.
4. **Quote**: il background e le valutazioni hanno ciascuno una quota del tetto, sia del giorno sia del mese. Le risposte agli utenti possono usare tutto il tetto.

Una chiamata a un modello gratuito passa sempre.

| Classe | Sorgenti (`RequestSource`) |
|---|---|
| Risposte | `Manual`, `RagPipeline`, `RagClassification`, `Translation` |
| Background | `ChunkTranslation`, `AdminOperation` (estrattore di meccaniche, playground), `EventDriven`, `AgentTask`, `AutomatedTest` |
| Valutazioni | `Evaluation` (KbQuality), `ABTesting` |

La spesa viene dal registro `llm_cost_logs` e si rilegge ogni 30 secondi. Fra due letture il gate somma da sé le riserve delle chiamate in corso e i costi reali di quelle chiuse: due chiamate concorrenti non passano sullo stesso margine.

## Valori e dove si cambiano

I valori stanno in `SystemConfiguration.LlmSystemConfigs`. Finché l'admin non salva una riga valgono i predefiniti di `LlmSystemConfig`:

| Campo | Predefinito | Significato |
|---|---|---|
| `DailyBudgetUsd` | 1 $ | tetto giornaliero |
| `MonthlyBudgetUsd` | 20 $ | tetto mensile, il vincolo di ADR-095 (sotto 20 € al mese) |
| `BackgroundQuotaPercent` | 30 | quota del background, in percentuale del tetto |
| `EvaluationQuotaPercent` | 20 | quota delle valutazioni; insieme al background non supera 100 |
| `DegradeThresholdPercent` | 80 | soglia del degrado, sul consumo del giorno (usata dalla fetta 3b) |

- **Tetti**: `/admin/ai` → *LLM config*, oppure `PUT /api/v1/admin/llm-config`.
- **Soglia e quote**: per ora solo l'API, con i campi opzionali `degradeThresholdPercent`, `backgroundQuotaPercent` ed `evaluationQuotaPercent`. Un campo assente lascia il valore salvato. La pagina arriva con la fetta 6 (#4169).
- Una modifica vale entro un minuto, il tempo della cache di `LlmSystemConfigProvider`.

**Tetto a zero**: ferma ogni chiamata a pagamento. È il modo di spegnere l'AI senza toccare i provider.

## Cosa vede chi chiama

- **Chat in streaming** (`/agents/qa/stream`): un evento `Error` con il codice
  - `AI_BUDGET_DAILY_EXHAUSTED` (tetto o quota del giorno), che il frontend mostra come «Il servizio AI è in pausa fino a domani…»;
  - `AI_BUDGET_MONTHLY_EXHAUSTED` (del mese);
  - `AI_MODEL_NOT_IN_CATALOG` (modello fuori catalogo);
  - `AI_BUDGET_UNAVAILABLE` (catalogo o spesa illeggibili).

  Nulla finisce nella cache delle risposte.
- **Chiamate non in streaming**: un `LlmCompletionResult` fallito, con `Metadata["error_code"]` e `Metadata["budget_rejection"]`.
- **Playground admin**: il messaggio al posto della risposta.

## Osservabilità

- Metrica: `meepleai_llm_budget_rejected_calls_total`, con le etichette `reason` (`DailyCapReached`, `MonthlyQuotaReached`, `ModelNotInCatalog`, …) e `budget_class`.
- Log: un warning per chiamata rifiutata, con la stima e la spesa del giorno e del mese.

Le chiamate rifiutate non scrivono righe nel registro costi, perché non hanno raggiunto il provider.

## Limiti noti

- **Una sola istanza.** Le riserve stanno in memoria. Con due istanze dell'API, ciascuna vedrebbe solo le proprie fino alla lettura successiva del registro. È il contratto di staging (ADR-087).
- **Spesa illeggibile.** Se il registro non si legge, si riusa la lettura dello stesso giorno, se c'è; altrimenti la chiamata non parte.
- **Fetta 3b.** Restano da fare:
  - il degrado fra la soglia e il tetto (modello di riserva del profilo, background in pausa);
  - il tetto di KbQuality dentro la quota delle valutazioni (oggi la generazione delle domande è già sotto la quota, ma il contatore di 50 $ al mese di `EvalCostCapBehavior` resta);
  - gli allarmi (`LlmBudgetMonitoringService`, `LlmCostAlertService`) che leggono questi valori invece dei propri.
