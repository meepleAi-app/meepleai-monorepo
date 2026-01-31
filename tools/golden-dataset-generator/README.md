# Golden Dataset Generator

Tool per generare il golden dataset (1000 Q&A pairs) per testing del sistema RAG.

## Files

- **GenerateGoldenDatasetSimple.cs** - Generator template-based (ALPHA, usato per v1.0)
- **GenerateGoldenDatasetWithPDF.cs** - Generator basato su PDF (FUTURO, richiede OpenRouter API)

## Utilizzo

### Generazione Template-Based (Alpha)

```bash
# Dalla repository root
dotnet run --project tools/golden-dataset-generator/GenerateGoldenDatasetSimple.csproj
```

**Output**: `tests/data/golden_dataset.json` (1000 Q&A pairs)

### Generazione PDF-Based (Futuro)

```bash
# Richiede OPENROUTER_API_KEY environment variable
export OPENROUTER_API_KEY="your-key"
dotnet run --project tools/golden-dataset-generator/GenerateGoldenDatasetWithPDF.csproj
```

## Struttura Dataset

```json
{
  "metadata": {
    "total_test_cases": 1000,
    "games_count": 10
  },
  "games": [
    {
      "game_id": "terraforming-mars",
      "test_cases": [
        {
          "question": "Domanda in italiano?",
          "expected_answer_keywords": ["keyword1", "keyword2"],
          "expected_citations": [{"page": 5, "snippet_contains": "testo"}],
          "forbidden_keywords": ["inventato"],
          "difficulty": "easy",
          "category": "gameplay"
        }
      ]
    }
  ]
}
```

## Distribution

- **Difficulty**: Easy 55%, Medium 25%, Hard 20%
- **Category**: Gameplay 40%, Setup 25%, Endgame 20%, Edge Cases 15%

## Testing

```bash
# Run unit tests
dotnet test --filter "FullyQualifiedName~GoldenDataset"
```

## Issue Reference

- **Issue #1797**: BGAI-059a - Generate Golden Dataset (1000 Q&A pairs)
- **Blocks**: Issue #1000 (BGAI-060 - Baseline accuracy test)
