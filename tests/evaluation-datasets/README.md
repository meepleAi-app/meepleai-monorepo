# RAG Evaluation Datasets

Evaluation datasets for measuring MeepleAI RAG pipeline quality, as defined in ADR-024 Phase 0.

## Dataset Overview

| Dataset | Source Format | Samples | Games | Difficulty Levels |
|---------|--------------|---------|-------|-------------------|
| `mozilla-boardgames.json` | Mozilla Structured QA | 25 | 6 | easy, medium, hard |
| `meepleai-custom.json` | MeepleAI Custom | 35 | 3 | easy, medium, hard |
| **Total** | **Combined** | **60** | **8** | **3 levels** |

## Dataset Formats

### Mozilla Structured QA (`mozilla-boardgames.json`)

Based on the Mozilla Structured QA format. Each sample includes:
- `id`: Unique identifier (e.g., `moz-001`)
- `question`: Natural language question about board game rules
- `expected_answer`: Reference answer from the rulebook
- `source`: Rulebook identifier (e.g., `catan-rulebook`)
- `source_page`: Page number in source document
- `section`: Relevant section heading
- `difficulty`: easy | medium | hard
- `category`: setup | gameplay | scoring
- `expected_keywords`: Keywords that should appear in a correct answer
- `dataset_source`: Always `"mozilla"`

### MeepleAI Custom (`meepleai-custom.json`)

Custom format designed for MeepleAI's multi-complexity evaluation. Each sample includes all Mozilla fields plus:
- `game_id`: Game identifier for filtering (e.g., `catan`, `terraforming-mars`, `spirit-island`)
- `relevant_chunk_ids`: Expected chunk IDs for Recall@K calculation (populated after initial indexing)

## Games Covered

### Mozilla Dataset (6 games)
- **Catan** (7 samples) - Setup, gameplay, scoring, trading
- **Ticket to Ride** (4 samples) - Setup, route claiming, game end
- **Pandemic** (5 samples) - Actions, outbreaks, curing, hand limits
- **Carcassonne** (3 samples) - Meeple placement, scoring
- **Azul** (3 samples) - Factory displays, floor line, game end
- **7 Wonders** (3 samples) - Card distribution, drafting

### MeepleAI Custom Dataset (3 complexity tiers)
- **Catan** (8 samples) - Simple rules, well-known game
- **Terraforming Mars** (10 samples) - Medium complexity, engine building
- **Spirit Island** (17 samples) - High complexity, asymmetric powers

## Difficulty Distribution

| Difficulty | Mozilla | Custom | Total | Description |
|-----------|---------|--------|-------|-------------|
| Easy | 7 | 10 | 17 | Direct factual questions (single-hop) |
| Medium | 14 | 15 | 29 | Questions requiring rule synthesis |
| Hard | 4 | 10 | 14 | Complex interactions, edge cases |

## Category Distribution

| Category | Count | Description |
|----------|-------|-------------|
| Setup | 12 | Initial game setup, components |
| Gameplay | 32 | Core mechanics, turn structure |
| Scoring | 10 | Victory conditions, point calculation |
| Edge Cases | 6 | Rule interactions, special scenarios |

## Evaluation Metrics

These datasets are used with `IDatasetEvaluationService` to compute:

| Metric | Description | Phase 0 Target |
|--------|-------------|----------------|
| Recall@5 | Relevant chunks in top 5 results | Baseline only |
| Recall@10 | Relevant chunks in top 10 results | Baseline only |
| nDCG@10 | Ranking quality of retrieved chunks | Baseline only |
| MRR | Mean Reciprocal Rank | Baseline only |
| Answer Correctness | Keyword match / LLM-as-judge | Baseline only |
| P95 Latency | 95th percentile response time | Baseline only |

## Usage

### Loading a Dataset

```csharp
var json = File.ReadAllText("tests/evaluation-datasets/mozilla-boardgames.json");
var dataset = EvaluationDataset.FromJson(json);

// Validate minimum requirements (>= 30 samples)
var (isValid, errors) = dataset.Validate();
```

### Combining Datasets

```csharp
var mozilla = EvaluationDataset.FromJson(mozillaJson);
var custom = EvaluationDataset.FromJson(customJson);
mozilla.Merge(custom); // Combined dataset with 60 samples
```

### Running Evaluation

```csharp
var options = new EvaluationOptions
{
    Configuration = "baseline",
    TopK = 10,
    EvaluateAnswerCorrectness = true
};

var result = await evaluationService.EvaluateDatasetAsync(dataset, options);
```

## Adding New Samples

1. Follow the JSON schema defined by `EvaluationSample` record
2. Use unique IDs with appropriate prefix (`moz-` for Mozilla, `ma-` for MeepleAI)
3. Include at least 3 `expected_keywords` per sample
4. Assign appropriate `difficulty` and `category`
5. Run validation: dataset must have >= 30 samples total

## Related Documentation

- **ADR-024**: RAG Evaluation Framework design decisions
- **EvaluationDataset.cs**: Domain model with JSON serialization
- **EvaluationSample.cs**: Sample record with factory methods
- **DatasetEvaluationService.cs**: Metric calculation implementation
- **Baseline Report**: `docs/evaluation-reports/baseline-2026-02.md`
