# Smoke Set — Manual UI Checklist

> Companion to `infra/scripts/smoke-set.sh` (automated) and `infra/scripts/pg-readback.sh` (DB-direct).
> Covers the 2/9 G1 scenarios that cannot be automated reliably from a shell script: **A4** (SSE chat with citations) and **C5** (real RAG query latency).
>
> Added per Fix #1 from spec-panel review (2026-05-05).
> Refs: `docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md` G1.

## When to run

- Before promoting `main-staging` → `main` (release readiness)
- After CF Tunnel migration cutover
- After any change to `/chat` UI, RAG pipeline, SSE streaming, or citations rendering

## Pre-conditions

- `bash infra/scripts/smoke-set.sh https://meepleai.app` returns exit 0 (automated smoke green)
- `bash infra/scripts/pg-readback.sh` returns exit 0 (migrations applied)
- Logged in as test owner with `Catan` (or any) game indexed in personal library

## A4 — Chat with citations (SSE streaming)

**Goal**: verify that `/chat` produces a non-empty response via Server-Sent Events with at least one valid `[citation:N]` tag, and that each citation has snippet + page + source_doc_id.

| Step | Action | Expected | Pass? |
|------|--------|----------|-------|
| A4.1 | Open `https://meepleai.app/chat` | Page loads, login persists, game picker visible | ☐ |
| A4.2 | Select an indexed game (e.g. Catan) | Chat input enabled, history empty (or recent) | ☐ |
| A4.3 | Send: `Quanti dadi servono per il primo turno?` | SSE response streams in within ~3s (TTFT), progressive tokens visible | ☐ |
| A4.4 | Wait for stream completion (≤10s total) | Response complete, no SSE error, no console errors | ☐ |
| A4.5 | Verify citations present | At least 1 `[citation:N]` tag in body, rendered as clickable | ☐ |
| A4.6 | Hover/click citation 1 | Tooltip/expansion shows `snippet` + `page_number` + `source_doc_id` | ☐ |
| A4.7 | Verify snippet is real | Text in snippet matches a passage findable in the rulebook PDF | ☐ |

**Result**: ☐ PASS  ☐ FAIL — note: ____________________

## C5 — RAG real query <10s end-to-end

**Goal**: verify the spec G1.C5 invariant ("RAG query <10s end-to-end via SSE"). The automated smoke uses a perf proxy on `/api/v1/shared-games`; this checklist measures the real RAG path.

| Step | Action | Expected | Pass? |
|------|--------|----------|-------|
| C5.1 | Open browser DevTools → Network tab, filter "EventSource" | Empty | ☐ |
| C5.2 | Open `/chat`, select an indexed game | Page ready | ☐ |
| C5.3 | Note current time (T0) | T0 = wall-clock | ☐ |
| C5.4 | Send any RAG-eligible question (rule-related) | Submit, watch Network tab | ☐ |
| C5.5 | Note time when first SSE token arrives (TTFT) | TTFT = T1 - T0, **target ≤3s** | ☐ |
| C5.6 | Note time when stream closes (T2) | Total = T2 - T0, **target ≤10s** | ☐ |
| C5.7 | (Optional) Verify Prometheus counter incremented | `rag_query_duration_seconds_bucket{le="10"}` +1 | ☐ |

**Measurement**:
- TTFT (T1 - T0): _____ s (≤3s pass)
- Total (T2 - T0): _____ s (≤10s pass)

**Result**: ☐ PASS  ☐ FAIL — note: ____________________

## Logging the run

After completing both A4 and C5 manually, record outcome in the DR walkthrough log
(`docs/operations/dr-walkthrough-log.md`) or in your release checklist:

```
| YYYY-MM-DD | Manual smoke (A4+C5) | PASS/FAIL | TTFT=Xs, Total=Ys, notes |
```

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| SSE never starts (TTFT > 10s) | DeepSeek/OpenRouter unreachable, embedding service down | Check `make staging-with-tutor` logs; verify outbound network |
| Citations missing | KB not indexed for selected game, or RAG fallback hit | Run `pg-readback.sh`; check `/admin/knowledge-base` for game status |
| Console errors during stream | Auth cookie expired, CSRF mismatch | Re-login; clear cookies |
| TTFT >3s but Total <10s | LLM cold start (DeepSeek warmup) | Acceptable on first query of a session; re-run for warm-cache measurement |
