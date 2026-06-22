# Integration Suite Baseline — pre-A

**Date:** 2026-05-29
**Branch base:** `main-dev` HEAD (commit `9b0e0aec7`)
**Command:** `dotnet test --filter "Category=Integration" --logger "console;verbosity=normal"`

## Summary

| Metric | Value |
|---|---|
| PASS | 874 |
| FAIL | 50 |
| SKIP | 27 |
| Duration | 17:47 min |
| Total tests | unknown (testhost crashed mid-run) |
| Exit code | 0 (dotnet test returns 0 even with FAIL) |

The full log file (~3 MB) is not committed (`.gitignore`).

## Failure clusters

| Pattern | Count |
|---|---|
| `Npgsql.NpgsqlException : Exception while reading from stream` or `EndOfStreamException` or `Connessione interrotta` | 146 stack-trace occurrences |
| HTTP `401 Unauthorized` or `500 InternalServerError` assertion failures | 32 |

The 146 Npgsql occurrences include repeated stack traces from the same failure (one fail can emit multiple stack frames matching the pattern). The 50-FAIL summary count is the canonical metric.

## Critical observation

Mid-run the host process crashed:

> L'esecuzione dei test attivi è stata interrotta. Motivo: Arresto anomalo del processo host di test.

This is consistent with the resource-exhaustion hypothesis: 30+ test classes spinning Postgres containers + WebHosts + Npgsql connection pools simultaneously exceeded the host's available resources.

## Comparison reference

- After Phase A (delete `AuthBoundedContextTestBase` + convert 3 outliers), expected Npgsql cluster ≤ 5 stack-trace occurrences and no testhost crash.
- After Phase C (5 FrontendSdk fix), expected 0 fail in two consecutive deterministic runs.
