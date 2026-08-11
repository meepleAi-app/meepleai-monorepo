# tests/

Repository-level test, smoke, performance and evaluation assets.
The .NET unit/integration suite is the bulk; the rest are runnable harnesses.

| Folder | What | Run |
|--------|------|-----|
| `Api.Tests/` | .NET 9 xUnit suite — unit, integration (Testcontainers), E2E | `dotnet test tests/Api.Tests/Api.Tests.csproj` |
| `api-smoke/` | Bruno collection + runners + `agent-endpoints-smoke.http` | `tests/api-smoke/run-smoke.sh --env local` |
| `k6/` | Load/performance scenarios + utils | `k6 run tests/k6/scenarios/<scenario>.js` |
| `llm-eval/` | LLM evaluation harness (`golden-set/`, `ocr-validation/`) | `python tests/llm-eval/<harness>/run_validation.py` |
| `evaluation-datasets/` | RAG evaluation datasets (JSON) | consumed by eval tooling |
| `fixtures/` | SQL seed + JSON fixtures (+ `nanolith-storybook/`) | loaded by CI smoke workflows |
| `data/` | Golden dataset **output** (generated) — see [`data/README.md`](./data/README.md) | — |

## Not here (moved out — these are tooling, not tests)

| Was | Now |
|-----|-----|
| `tests/load/epic-4068-permission-load-test.js` | `tests/k6/scenarios/` (it's a k6 test) |
| `tests/data/merge_datasets.py`, `Dataset QA…pdf` | `tools/golden-dataset-generator/` |
| `tests/scripts/game-reset/` | `infra/scripts/game-reset/` (next to the reset scripts; run via `make game-reset-rollback-rehearse`) |

## CI

Backend suite runs in `ci.yml` / `dev-fast.yml`; api-smoke in `api-smoke.yml`;
k6 in `test-performance.yml` + `deploy-staging.yml`. See `.github/workflows/`.
