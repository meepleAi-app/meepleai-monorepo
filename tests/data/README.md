# tests/data

Output directory for the **golden evaluation dataset**.

`golden_dataset.json` is **generated** here (git-ignored / not committed) and consumed by:

- `tools/run-golden-dataset-evaluation.ps1` → reads `tests/data/golden_dataset.json`
- `tools/golden-dataset-generator/` → produces it

## Come generarlo

Dalla **root del repository**:

```bash
dotnet run --project tools/golden-dataset-generator/GenerateGoldenDatasetSimple.csproj
```

Produce 1000 Q&A pairs su 10 giochi. Senza questo file i test del golden dataset
(`GoldenDatasetLoaderTests`, `GoldenDatasetAccuracyIntegrationTests`) si **saltano** riportando
questo stesso comando — non falliscono e non vanno disattivati a mano.

> Storia (#3655): fino al 2026-08-11 il generatore abortiva con «Run from repository root!» anche
> quando *eri* nella root, perché cercava `tests/rulebook`, spostata in `data/rulebook` sette mesi
> prima. La via documentata qui non era percorribile, e i 30 test che dipendono dal dataset erano
> stati disattivati a mano invece che risolti.

## Generators (moved out of `tests/`)

The dataset generators are tooling, not tests, and now live under
`tools/golden-dataset-generator/`:

| File | Purpose |
|------|---------|
| `merge_datasets.py` | Merge `board_game_qa_batch*.json` files into `golden_dataset.json` |
| `GenerateGoldenDataset*.cs` | C# generators (rulebook-PDF based) |
| `dataset-qa-giochi-10.pdf` | Source QA dataset (10 games) |

This README keeps the folder tracked so the generated output path stays stable.
