# ADR-092 — Un solo Arbitro: Python validatore puro, C# proprietario della risposta

**Date**: 2026-10-08
**Status**: Accepted — decisione del committente presa nel brainstorming del 2026-10-08, a valle della revisione a pannello `docs/for-developers/research/2026-10-08-neuro-symbolic-architecture-spec-panel.md` §2.4.
**Related**: ADR-004 (AI Agents BC) · ADR-006 (multi-layer validation) · ADR-090 (grounded answer: `KnowledgeBase` owner) · spec `docs/for-developers/specs/2026-10-08-mechanic-claims-v3-defeasible-rules-design.md` §5 · #3759 (orchestration-service) · #3668 (`Constraint` non valutati).

---

## Context

Esistono due Arbitri che rispondono alla stessa domanda («questa mossa è legale, e perché?»):

| | Python `apps/orchestration-service` | C# `KnowledgeBase` / `GameManagement` |
|---|---|---|
| Codice | `src/application/arbitro_agent.py` (LangGraph lineare `retrieve_rules → validate_move → generate_explanation`), `rule_engine.py` | `ArbitroAgentService.cs`, `MoveValidationDomainService.cs`, `AskArbiterCommandHandler.cs` |
| Validazione | `re.match` sulla notazione algebrica; i `Constraint` sono saltati («no board model exists», #3668) | match a keyword («cannot», «must not») su `RuleAtom` |
| Confidence | fissa: `0.95` se valido, `0.90` se no | `avg(score chunk) × (citazioni > 0 ? 1 : 0.5)` |
| Spiegazione | `gpt-4o-mini` via OpenRouter | pipeline RAG con citazioni e ADR-006 |
| Stato | pubblicato su GHCR, **non avviato su staging** (opt-in `compose.staging.tutor.yml`); `enable_checkpointing` dichiarato e non cablato | in produzione |

ADR-090 stabilisce che la risposta grounded all'utente è di `KnowledgeBase`. Nessun ADR dice chi è il proprietario del **verdetto**. Finché la cosa resta aperta, ogni lavoro su uno dei due Arbitri rischia di essere buttato, e la confidence fissa di Python è un valore che i consumatori leggono come misura.

Due fatti tirano in direzioni opposte: il C# ha già citazioni, validazione multi-livello e osservabilità; l'esame AI-200 che il committente prepara elenca Python, container e messaging fra le competenze attese, e il pilota ASP (clingo) vive naturalmente in Python.

## Decision

1. **Il verdetto è di Python, la risposta è di C#.** `orchestration-service` diventa un **validatore puro**: riceve stato, mossa e regole, restituisce verdetto, regole applicate, regole violate e passi di spiegazione. Non chiama alcun LLM per produrre il verdetto. `KnowledgeBase` resta proprietario della risposta all'utente (ADR-090) e compone spiegazione, citazioni e validazione multi-livello (ADR-006) a partire dal verdetto.
2. **Contratto**: `POST /validate` con `{game_id, state, move, rules}` dove `rules` sono i claim v3 della mechanic card pubblicata (spec §5). Risposta `{verdict: Valid | Invalid | Unknown, applied_rule_ids[], violated_rule_ids[], explanation_steps[], confidence: number | null}`. `confidence` è `null` salvo quando deriva da una misura reale; **nessun valore fisso**.
3. **Kernel deterministico iniziale** (risoluzione defettibile leggera): regole applicabili per match del `Trigger` su stato e mossa; scarto delle regole sovrascritte via `Overrides`; vince la `Priority` più alta; conflitto allo stesso livello senza `Overrides` ⇒ `Unknown`. ASP/clingo si innesta su questo kernel come pilota successivo, non come prerequisito.
4. **Il C# chiama Python** da `AskArbiterCommandHandler` attraverso un client registrato in `KnowledgeBase` e invocato via `IMediator` (nessuna iniezione diretta di servizi negli endpoint). In assenza di risposta entro il budget di latenza il verdetto è `Unknown`, mai `Valid` per default.
5. **Ritiro del doppione C#**: `MoveValidationDomainService` (match a keyword) e la parte di verdetto di `ArbitroAgentService` vengono marcati `[Obsolete]` con riferimento a questo ADR e rimossi quando `/validate` è in produzione. `RuleSpec`/`RuleAtom` restano come sorgente testuale finché i claim v3 non li coprono.
6. **Prerequisiti operativi** prima di caricare Python di responsabilità: servizio avviato su staging con `/health` e metriche nel Prometheus esistente; checkpointer LangGraph cablato su Postgres (`langgraph-checkpoint-postgres`, nessun servizio nuovo); deadline per chiamata come nello streaming (#3601). `/execute` resta per il tutor fino a una deprecazione separata.

## Consequences

**Positive**: un solo proprietario per il verdetto e uno per la risposta, coerente con ADR-090; la confidence torna onesta; il pilota ASP e l'esercizio AI-200 (container Python, messaging, osservabilità) hanno un consumatore reale; il C# non duplica logica di validazione.

**Negative**: una chiamata di rete sincrona nel percorso della chat (budget di latenza da fissare e misurare dal dump `[RAG-TUNE]`); `orchestration-service` va osservato e mantenuto davvero; finché i claim v3 non coprono un gioco, `/validate` risponde `Unknown` su quel gioco.

**Neutral**: LangGraph resta per il tutor e per l'orchestrazione futura; la scelta non impegna su Neo4j, ASP o XState.

## Alternatives considered

- **Tutto in C#, ritirare Python**: un linguaggio solo, ma si perde il pezzo LangGraph e l'esercizio Python per l'esame; il pilota ASP richiederebbe un binding .NET.
- **Python proprietario end-to-end**: contraddice ADR-090 e duplica citazioni e validazione già in C#.
- **Non decidere**: il lavoro Python resta orfano e la confidence fissa continua a circolare.

## Verifica

- Test Python: una mossa senza regole valutabili ⇒ `confidence: null`, `applied_rule_ids: []`, `verdict: Unknown`.
- Test C#: timeout di `/validate` ⇒ risposta all'utente con verdetto `Unknown` e nessuna citazione inventata.
- Gate: fetta multi-hop del golden set (`docs/for-developers/research/2026-10-08-neuro-symbolic-architecture-spec-panel.md` §4, punto 1) confrontata prima e dopo.
