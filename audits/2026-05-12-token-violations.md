# Token Vocabulary Violations — Inventory

| Field | Value |
|---|---|
| **Date** | 2026-05-31 |
| **Generator** | `pnpm lint:tokens` (DS-2) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../docs/for-developers/specs/2026-05-12-token-canonicalization.md) |
| **Rule** | `local/no-hardcoded-color-utility` |
| **Total violations** | 0 |
| **Files affected** | 0 |
| **Clusters affected** | 0 |

## Violations by cluster

| Cluster | Violations | Suggested stage |
|---|---|---|

## Top 20 files

| File | Violations |
|---|---|

## Notes

- Rule is in `warn` mode during DS-3 inventory + DS-4..DS-11 cluster migrations.
- Switched to `error` in DS-12 once `pnpm lint:tokens --max-warnings 0` is green.
- Companion JSON: [`2026-05-12-token-violations.json`](./2026-05-12-token-violations.json).
