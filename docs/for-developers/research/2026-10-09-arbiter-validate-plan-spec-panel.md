# Spec panel — piano `/validate` (ADR-092) alla luce della baseline dei test Python

**Data**: 2026-10-09 · **Modalità**: critique · **Focus**: testing + architecture · **Oggetto**: `docs/superpowers/plans/2026-10-08-arbiter-validate-endpoint.md` (piano gemello di `2026-10-08-mechanic-claims-v3.md`, spec §6, ADR-092)
**Pannello**: Crispin (lead, testing), Nygard (esercizio), Fowler (interfacce), Newman (confine di servizio e versioni), Wiegers (requisiti), Adzic (esempi)
**Convenzione**: i conteggi sono quelli del run del 2026-10-09 sul worktree del branch `feature/mechanic-claims-v3` (commit `3d7b089c4`), riprodotti con il comando indicato; non sono totali vivi. Path relativi ad `apps/orchestration-service/` salvo nota.

---

## 0. Verdetto in breve

Il piano è eseguibile **per il kernel** (Task 1 è puro e non dipende da LangGraph), ma **non è eseguibile così com'è per la parte di servizio** (Task 2, 5, 6): il servizio Python che ospiterà `/validate` oggi è rotto sotto le dipendenze che un `pip install -r requirements.txt` fresco installa, e la CI non se ne accorge perché esegue un solo file di test su diciotto. I 35 fallimenti non sono 35 difetti: sono **una** causa di drift delle dipendenze (28 test), **un** difetto di prodotto che quel drift ha reso visibile (`/execute` restituisce 500), e **tre** test rotti da tempo (7 test). Il piano va preceduto da un Task 0 di stabilizzazione e deve cambiare il gate CI, altrimenti i suoi test non gireranno mai.

---

## 1. La baseline, classificata

Comando: `cd apps/orchestration-service && python -m pytest -q -p no:cacheprovider` (output completo salvato dal controller). Esito: **35 falliti, 96 superati** in 149 s.

| # | Famiglia | Test | Causa accertata | Natura |
|---|---|---|---|---|
| 20 | `ValueError: "ChatOpenAI" object has no field "ainvoke"` | `test_intent_classifier` (8), `test_tutor_agent` (7), `test_hybrid_search_integration` (5) | i test fanno `patch.object(classifier.llm, 'ainvoke', …)` su un'istanza reale di `ChatOpenAI`; con `langchain-openai` 1.x (modello pydantic con `extra='forbid'`) l'assegnazione è rifiutata. `requirements.txt` ha solo **floor** non pinnati (`langgraph>=0.2.45`, `langchain-core>=0.3.15`, `langchain-openai>=0.2.8`): localmente e in una CI fresca si risolvono a `langgraph 1.0.7`, `langchain-core 1.2.8`, `langchain-openai 1.1.7` | **drift di dipendenze** (test scritti per la 0.x) |
| 9 | `AttributeError: 'dict' object has no attribute 'intent' / 'current_agent' / …` | `test_orchestrator` (9) | con langgraph 1.x `graph.ainvoke(state)` restituisce un **dict**, non il dataclass `GameAgentState`; `GameOrchestrator.execute` (`src/application/orchestrator.py:351-354`) lo restituisce tale e quale | **drift** che espone un **difetto di prodotto**: `main.py` `/execute` legge `result_state.error`, `.current_agent`, `.agent_response` ⇒ `AttributeError` ⇒ 500 a ogni richiesta con una build fresca |
| 3 | `NameError: name 'orchestrator' is not defined` · `assert 500 == 200` | `test_api` (3) | la fixture `client` fa `return TestClient(app)` senza `with`: il `lifespan` non gira e il global `orchestrator` non esiste | **test rotto** (preesistente, indipendente dalle versioni) |
| 2 | `Expected 'execute' to have been called once` · `None is not None` | `test_conversation_repository` (2) | il repository salta il salvataggio quando `self.pool` è `None` (`conversation_repository.py:64-65`) e la fixture non imposta il pool | **test rotto** (fixture incompleta) |
| 1 | `assert True is False` | `test_tutor_agent::test_should_summarize_returns_true_after_threshold` | `should_summarize()` è `needs_summarization or turn_count >= max_turns_before_summary` (`tutor_state.py:79-81`); la fixture parte già sopra soglia o con `needs_summarization` vero | **test/fixture disallineati** |

Due fatti di contorno che pesano più dei numeri:

- **La CI esegue un solo file.** `.github/workflows/ci.yml` (job `python-tests`, righe 971-1005) lancia `python -m pytest tests/test_arbitro_agent.py` con Python 3.11 e un `pip install -r requirements.txt` fresco: gli altri diciassette file non girano in nessun workflow (`grep -rn "pytest" .github/workflows/*.yml` conferma). Quel file passa perché patcha `ChatOpenAI` a livello di modulo invece che sull'istanza. La "baseline verde" della CI misura l'8% della suite.
- **Il drift raggiunge anche l'immagine.** `deploy-staging.yml` builda `orchestration-service` con lo stesso `requirements.txt`, quindi l'immagine su GHCR porta langgraph 1.x; il servizio non è avviato su staging (opt-in `compose.staging.tutor.yml`), per questo nessuno ha visto il 500 di `/execute`.

---

## 2. Requisiti (Wiegers, Adzic)

**WIEGERS.** Il piano dice «`pytest -q` dalla cartella (conftest imposta `OPENROUTER_API_KEY=test-key`); nuovi test in `tests/`» e nelle Global Constraints non fissa alcun criterio di uscita per la suite Python. Con 35 rossi preesistenti, "i test passano" non è verificabile: un implementer onesto riporterà "35 failed, come prima", e nessun gate lo fermerà. Serve un criterio misurabile: *zero fallimenti sui file che la CI esegue, e i file nuovi aggiunti alla lista della CI nello stesso PR*. Lo stesso vale per il contratto: «`confidence` sempre `null`» è misurabile; «nessuna chiamata LLM dentro `/validate`» lo è solo se un test lo prova (un `patch` su `ChatOpenAI` che asserisce zero invocazioni).

**ADZIC.** Gli esempi del kernel (Task 1) sono buoni e discriminanti. Manca l'esempio che collega i due mondi: *Given* un claim con `kind = Exception` e `polarity` lessicale ambigua («puoi … ma non puoi …»), *When* `/validate`, *Then* `forbid` vince — il test di polarità lo copre, ma nessun test di API lo attraversa da JSON a verdetto. E manca l'esempio negativo di ADR-092 §4 lato Python: `rules` vuoto ⇒ `Unknown` con `execution_time_ms` presente (c'è) e **nessuna** metrica `validate_verdict_valid_total` incrementata (non c'è).

---

## 3. Architettura e confini (Fowler, Newman, Hohpe)

**FOWLER — il kernel è nel posto giusto, il servizio no.** `validation_kernel.py` non importa langgraph né langchain: è l'unico pezzo del piano immune dal drift, e Task 1 può partire oggi. Ma `main.py` è un modulo monolitico con globali riempiti dal `lifespan` (`orchestrator`, `intent_cache`), e Task 2 vi aggiunge la route `/validate` e quattro contatori in un `dict` di metriche. Due conseguenze: (1) il test di `/validate` userà la stessa fixture `TestClient(app)` senza lifespan che oggi fa fallire `test_api`; funzionerà solo perché `/validate` non tocca i globali, cioè per coincidenza; (2) ogni futuro test del servizio erediterà la trappola. Raccomandazione: un router separato `src/api/validate_router.py` registrato con `app.include_router`, senza dipendenze dai globali, e una fixture `client` che usa `with TestClient(app) as client` (lifespan reale) **oppure** un'app di test costruita senza lifespan — ma una delle due, dichiarata.

**NEWMAN — versioni e contratto.** Il servizio ha floor senza tetto su tre librerie che hanno fatto un major nel frattempo. Un piano che aggiunge codice a quel servizio senza decidere la versione costruisce su sabbia: (a) pinnare a `langgraph<1`, `langchain-core<1`, `langchain-openai<1` e riallineare `pydantic` al pin già presente, oppure (b) migrare il servizio a langgraph 1.x (stato come `TypedDict`, `execute` che riconverte il dict). La (a) costa un'ora e ferma l'emorragia; la (b) è il debito vero, da fare con il Task 0 del piano o in un'issue separata, non "mentre si aggiunge `/validate`". In entrambi i casi un file `constraints.txt` o pin esatti, e `pip install` in CI con `--require-hashes` o almeno pin esatti, perché oggi la CI installa versioni diverse a ogni run.

Sul contratto C# ↔ Python il piano è solido (snake_case, `Unknown` su qualunque errore, test di contratto da entrambi i lati). Un'aggiunta: la **versione del contratto** nel body (`"contract": "v1"`) o nell'header, così il C# può rifiutare una risposta di un servizio più vecchio invece di interpretarla come `Unknown`.

**HOHPE — il kernel riceve le regole dal chiamante.** Scelta giusta: Python non legge il DB e non ha stato, il C# resta proprietario dei claim (ADR-090/092). Ma `rules` nel body può crescere (tutti i claim della card pubblicata): fissare un limite (`max_rules`, 422 oltre) e misurare la dimensione media, altrimenti il timeout di 3 s del client C# lo scoprirà in produzione.

---

## 4. Test e qualità (Crispin, Gregory)

**CRISPIN — tre correzioni al piano, tutte prima del Task 2.**

1. **Task 0 (nuovo): stabilizzare la suite.** Pin delle dipendenze (scelta a/b sopra); fix di `GameOrchestrator.execute` (riconvertire il dict in `GameAgentState` o rendere lo stato un `TypedDict` coerente con `main.py`); fixture `client` con lifespan; fixture del repository con pool finto; soglia di `should_summarize` nel test. Esito atteso: **0 falliti** su tutta la cartella `tests/`, con il comando e i conteggi nel report. Senza questo, la "verifica" del piano non distingue un regressione da un rumore di fondo.
2. **Gate CI completo.** `ci.yml` job `python-tests`: da `tests/test_arbitro_agent.py` a `tests/` (tutta la cartella), con i file nuovi del piano inclusi automaticamente. Finché il gate esegue un file solo, i test di `/validate` sono decorativi.
3. **Un test che prova l'assenza di LLM.** `patch('src.application.arbitro_agent.ChatOpenAI')` e `patch('langchain_openai.ChatOpenAI')` nel test API di `/validate`, con asserzione `assert_not_called()`: è il requisito di ADR-092 §1 reso osservabile, e oggi nel piano non esiste.

**GREGORY — chi legge i report.** Il piano fa scrivere al subagente «RED/GREEN per item»; con 35 rossi di fondo il RED è sempre vero e il GREEN mai. Il criterio va cambiato in «RED = i test nuovi falliscono *e* i preesistenti restano al loro stato; GREEN = i nuovi passano e il totale dei falliti non cresce», oppure, dopo il Task 0, nel semplice «0 falliti».

---

## 5. Esercizio (Nygard)

- **`/execute` restituisce 500 con una build fresca.** È il difetto più grave emerso, e non è nel piano perché il piano assume che il servizio funzioni. Va in un'issue a sé, con il test di `test_orchestrator` come riproduzione, e va chiuso **prima** di accendere il servizio su staging (ADR-092 §6), altrimenti il tutor andrà in errore mentre `/validate` funziona.
- **Prerequisiti ADR-092 §6 restano fuori dal piano** (`langgraph-checkpoint-postgres`, deadline per chunk, metriche nel Prometheus esistente): il piano lo dichiara; con langgraph 1.x il pacchetto checkpoint ha anch'esso un major, un motivo in più per decidere la versione prima.
- **Metriche**: il `dict` in memoria di `main.py` si azzera a ogni riavvio e non ha label; per `validate_verdict_*_total` basta, ma il piano gemello C# emette `meepleai_arbiter_validation_unavailable_total` con il `Meter` OTel. Due sistemi di metriche per una feature: accettabile oggi, da scrivere nel manuale operativo.

---

## 6. Modifiche proposte al piano (da applicare solo con il consenso del committente)

| Dove | Cosa |
|---|---|
| Global Constraints | aggiungere: «suite Python a 0 fallimenti sull'intera cartella `tests/` prima di ogni commit; i file nuovi entrano nel job `python-tests` di `ci.yml` nello stesso PR; dipendenze pinnate (langgraph/langchain `<1` oppure migrazione dichiarata)» |
| **Task 0 (nuovo)** | stabilizzazione: pin, fix di `GameOrchestrator.execute`, fixture `client` con lifespan, fixture pool del repository, soglia nel test di riassunto; DoD = 0 falliti con comando e conteggi; issue separata per il 500 di `/execute` se si sceglie la migrazione a 1.x |
| Task 2 | route in `src/api/validate_router.py` + `include_router`; test API con `patch` su `ChatOpenAI` e `assert_not_called()`; test `rules` vuoto ⇒ nessun contatore di verdetto incrementato; limite `max_rules` (422) |
| Task 3 | aggiungere al contratto un campo `contract: "v1"` e il rifiuto lato C# di versioni sconosciute ⇒ `Unknown` con motivo |
| Task 6 | `ci.yml`: `python -m pytest tests/` al posto del singolo file; nota nel manuale operativo sui due sistemi di metriche |
| Review Focus | aggiungere il caso «`/validate` sotto `patch` di `ChatOpenAI` non invoca mai l'LLM» |

---

## 7. Fonti

- Run locale del 2026-10-09: `python -m pytest -q -p no:cacheprovider` (Python 3.12.10; installati `langgraph 1.0.7`, `langchain-core 1.2.8`, `langchain-openai 1.1.7`, `pydantic 2.13.4`, `fastapi 0.115.0`)
- `apps/orchestration-service/requirements.txt` (floor senza tetto su langgraph/langchain; `pydantic==2.9.2`)
- `.github/workflows/ci.yml` job `python-tests` (Python 3.11, un solo file)
- `src/application/orchestrator.py:337-360`, `main.py` (`/execute`, lettura di attributi su `result_state`), `src/domain/tutor_state.py:79-81`, `src/infrastructure/conversation_repository.py:64-65`, `tests/test_api.py` (fixture `client`)
- ADR-092; spec `2026-10-08-mechanic-claims-v3-defeasible-rules-design.md` §6; piano `2026-10-08-arbiter-validate-endpoint.md`
