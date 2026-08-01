# RAG Evaluation Suite — Scaffolding (Slice 4)

**Issue**: #3433 · **Parent**: SP3 #3269 (Slice 4 / D3-full, deferita by-design) · **Data**: 2026-08-01

## Motivazione

Harness, metriche (recall@k / nDCG@10 / MRR) e runner della valutazione retrieval **esistono già** (ADR-024, `DatasetEvaluationService` + `EvaluationMetrics` + `RunEvaluationCommand`). Sono però inutilizzabili come segnale di qualità perché:

1. I `relevant_chunk_ids` di tutti i dataset (`tests/evaluation-datasets/*.json`) sono **vuoti** → `CalculateRecallAtK` ritorna `1.0` per convenzione (`DatasetEvaluationService.cs:221-224`), quindi recall/nDCG/MRR aggregati sono privi di significato.
2. Nessun sample è in **italiano**.

Il ground-truth (quali chunk sono davvero rilevanti) e l'authoring IT madrelingua sono lavoro **umano**. Questo scaffolding li abilita e rende le metriche oneste, senza pretendere di produrre il ground-truth automaticamente.

## Decisioni chiave (brainstorming 2026-08-01)

- **Modello di labeling**: *AI-propone + umano-verifica*. L'harness esegue il retrieval e propone i top-N candidati; l'umano marca `relevant` Y/N. Trade-off accettato: il ground-truth eredita il bias del retriever (un chunk rilevante che il retriever non recupera non viene proposto). Mitigazione futura possibile: union multi-strategia (fuori scope MVP).
- **Sample non labellati**: *esclusi dall'aggregato retrieval + coverage riportata*. Un sample con `RelevantChunkIds` vuoto non entra nella media recall/nDCG/MRR; il report conta `labeled`/`unlabeled`. Chiude il bug del `1.0` degenere senza deprimere artificialmente le metriche.

## Componenti

### C1 — Campo `Language` su `EvaluationSample`
Aggiungere `public string? Language { get; init; }` (`"en"` | `"it"`; `null` = non specificato). Il caricatore dataset (`LoadDatasetCommandHandler`) deserializza il campo `language`. Il report emette un **breakdown per lingua** oltre all'aggregato. Nessun secondo file separato: un dataset può contenere sample misti EN/IT (più semplice del merge di due file; `EvaluationDataset.Merge` resta disponibile).

### C2 — Fix metriche unlabeled
Nel calcolo aggregato (`DatasetEvaluationService`, ~righe 190-216):
- Distinguere i result **labeled** (`RelevantChunkIds.Count > 0`) dagli **unlabeled**.
- recall@5/@10, nDCG@10, MRR calcolati **solo sui labeled**.
- `AnswerCorrectness` (usa `ExpectedKeywords`, non i chunk id) resta calcolato su **tutti** i sample con retrieval riuscito.
- `EvaluationMetrics` guadagna `LabeledSampleCount` e `UnlabeledSampleCount`; il report li espone come "coverage".
- `CalculateRecallAtK` resta invariato (la convenzione `1.0` su relevant-set vuoto è corretta come primitiva; il fix è nel chiamante che non aggrega gli unlabeled).

### C3 — Runner
`make eval-retrieval DATASET=<path>` che esegue `RunEvaluationCommand` sul dataset ed emette un report **markdown + JSON** (recall@5/@10, nDCG@10, MRR, coverage labeled/unlabeled, breakdown per lingua). Invocazione via endpoint admin (pattern coerente con rag-smoke/title-health, che colpiscono endpoint via curl): il plan verifica per primo se un endpoint admin che esegue `RunEvaluationCommand` esiste già e lo riusa; altrimenti aggiunge `POST /api/v1/admin/eval/retrieval` (CQRS: solo `IMediator.Send`).

### C4 — Labeling-assist harness (pezzo nuovo abilitante)
Due step:
1. **Genera candidati**: per ogni query del dataset esegue `IRagService` retrieval (top-N), producendo un file review JSON — per query, i top-N candidati `{ chunkId, source, page, snippet, relevant: null }`.
2. **Merge label**: legge il file review compilato dall'umano e raccoglie i `relevant: true` in `RelevantChunkIds` del dataset.

Trasforma il labeling da "leggere il rulebook a mano" a "validare candidati".

### C5 — Seed EN
`tests/evaluation-datasets/meepleai-en-seed.json`: ~12 query EN sui 5 giochi golden (Catan, Wingspan, Dominion, Ark Nova, 7 Wonders), con `question` + `expected_answer` + `expected_keywords`, `language: "en"`, `relevant_chunk_ids: []` (da labellare via C4).

## Data flow (labeling)

```
dataset (relevant_chunk_ids vuoti)
  → [C4.1 genera candidati] → review.json (relevant: null)
  → [UMANO marca relevant] → review.json compilato
  → [C4.2 merge] → dataset (relevant_chunk_ids popolati)
  → [C3 runner] → report (recall/nDCG/MRR sui labeled + coverage + breakdown lingua)
```

## Testing (TDD)

- **C2**: dataset con mix labeled/unlabeled → recall aggregato NON degenera a `1.0`; `LabeledSampleCount`/`UnlabeledSampleCount` corretti (guard esplicito sul bug `221-224`).
- **C1**: round-trip serializzazione `language`; breakdown per lingua nel report con dataset misto.
- **C4**: dato un `IRagService` mock, `genera candidati` produce il file review nel formato atteso; `merge` popola `RelevantChunkIds` dai `relevant: true`.
- **C5**: il seed carica come dataset valido (N sample, tutti `language: "en"`, `relevant_chunk_ids` vuoti).

## Fuori scope (richiede umano)

- Labeling di massa dei `relevant_chunk_ids`.
- Authoring/validazione delle query IT madrelingua.
- **Esecuzione** del labeling-assist / runner su staging: il corpus è ora mid-reindex (#3427) e i chunk id sono instabili → l'esecuzione va fatta a re-index completato. Questa issue consegna solo il **codice**.
- Slice 4 non è un gate di promozione prod (prod non esiste ancora); è valore analitico.

## Rischi / trade-off

- **Bias del retriever nel ground-truth** (C4): accettato per l'MVP; documentato.
- **Instabilità chunk id post-reindex**: i label vanno ri-verificati dopo ogni big-bang re-index (i chunk id sono corpus-specifici). Documentato nel runbook del labeling.
