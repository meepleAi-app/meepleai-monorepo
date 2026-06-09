# Token Vocabulary Violations — Inventory

| Field | Value |
|---|---|
| **Date** | 2026-06-05 |
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

## Related audits

- [`2026-06-09-mockup-token-violations.md`](./2026-06-09-mockup-token-violations.md) — DS-17-2 sister audit for `admin-mockups/**` legacy CSS variable literals (`var(--bg-base|--gaming-*|--nh-*|--e-*)`). Scope-disjoint from this report; whitelist-incremental baseline of 1500 violations carried over from the token bridge era pending DS-16 unwind.
