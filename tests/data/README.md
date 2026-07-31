# tests/data

Output directory for the **golden evaluation dataset**.

`golden_dataset.json` is **generated** here (git-ignored / not committed) and consumed by:

- `tools/run-golden-dataset-evaluation.ps1` → reads `tests/data/golden_dataset.json`
- `tools/golden-dataset-generator/` → produces it

## Generators (moved out of `tests/`)

The dataset generators are tooling, not tests, and now live under
`tools/golden-dataset-generator/`:

| File | Purpose |
|------|---------|
| `merge_datasets.py` | Merge `board_game_qa_batch*.json` files into `golden_dataset.json` |
| `GenerateGoldenDataset*.cs` | C# generators (rulebook-PDF based) |
| `dataset-qa-giochi-10.pdf` | Source QA dataset (10 games) |

This README keeps the folder tracked so the generated output path stays stable.
