# SP5-b — Observability completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** Complete live-RAG observability (AC-OBS-1): wire the already-declared `RagFirstTokenLatency` histogram, add `retrieval_empty` counter + `citations_per_answer` histogram, and add the SLO recording rule + retrieval-empty alert + Grafana panel + doc.

**Architecture:** BE-only. Metrics via `System.Diagnostics.Metrics` on `MeepleAiMetrics.Rag.cs` (same pattern as SP2 T12's `MeepleAiMetrics.LiveSessionSse.cs`). Instrumentation in the streaming RAG handler `ChatWithSessionAgentCommandHandler` + `RagPromptAssemblyService`. SLO config in `infra/prometheus-rules.yml` + Grafana dashboard JSON.

**Tech Stack:** .NET 9 (xUnit + MeterListener); Prometheus rules YAML; Grafana JSON.

## Global Constraints
- De-risk reference: `.superpowers/sdd/sp5b-derisk-brief.md`.
- **Async-iterator safety (CRITICAL)**: `HandleCore` in `ChatWithSessionAgentCommandHandler` is a streaming `IAsyncEnumerable` (yield ~:382, broadcast :563-595). Wrap EVERY metric `Record()/Add()` in try/catch (swallow + optional debug log) — a metrics fault must NEVER abort the user's live chat stream. Never compute metrics before the `yield return`'s observable effect.
- **Cardinality (CRITICAL)**: NO `gameSessionId`/`agentSessionId`/`userId` tag on any metric. Record with NO tags.
- **No worktree**: commit directly on `feature/issue-2582-sp5b-observability`.
- Metric naming: dotted `meepleai.rag.*` on `Meter "MeepleAI.Api"` (Prometheus scrape → underscore).
- SLO thresholds are **provisional** (no live-RAG history): mirror the chat-path 800ms TTFT baseline; alerts `warning` not `critical` until ≥1 week of real data; mark "needs tuning" in the PR.

## Reading list (REUSE)
`MeepleAiMetrics.Rag.cs` (the existing `RagFirstTokenLatency` at :38-41 + `RagRetrievalFallbacks` :68 + `RagErrorsDetected` :349-style), `MeepleAiMetrics.LiveSessionSse.cs` (SP2 T12 pattern), `SseMetricsTests.cs:88-105` (MeterListener test pattern), `ChatWithSessionAgentCommandHandler.cs:145/:382/:550-557`, `RagPromptAssemblyService.cs:282-285`, `infra/prometheus-rules.yml:132-195,824-842`.

---

## Task 1: Declare `retrieval_empty` counter + `citations_per_answer` histogram
**Files:** `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs`; Test: `apps/api/tests/Api.Tests/.../RagMetricsTests.cs` (or extend the existing Rag metrics test).

**Interfaces:**
- Produces: `RagRetrievalEmpty` (`Counter<long>`, `meepleai.rag.retrieval_empty`), `RagCitationsPerAnswer` (`Histogram<long>`, `meepleai.rag.citations_per_answer`), + a `RecordRetrievalEmpty()` / `RecordCitationsPerAnswer(long)` helper if the file uses that style. Confirm `RagFirstTokenLatency` already exists (:38-41) — do NOT redeclare.

- [ ] **Step 1: Failing test** (MeterListener, mirror `SseMetricsTests.cs:88-105`): assert the two new instruments are published with the right names/types; recording moves the counter/histogram.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Declare** the Counter + Histogram on the `Meter` (mirror the existing declarations in the file). Add helper methods if the file's convention uses them.
- [ ] **Step 4: Run → PASS** + the existing Rag metrics suite (no regression).
- [ ] **Step 5: Commit** — `feat(observability): #2582 SP5-b declare rag retrieval_empty + citations_per_answer metrics`

---

## Task 2: Record first-token latency + citations count (in the handler)
**Files:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`; Test: the handler's metrics test (extend `ChatWithSessionAgent*Tests` or a new metrics test).

**Interfaces:** Consumes T1's metrics + the existing `RagFirstTokenLatency`.

- [ ] **Step 1: Failing test** — a streamed chat records `meepleai.rag.first_token_latency` EXACTLY once (≥0 ms observation) AND `meepleai.rag.citations_per_answer` once with the citation count (incl. zero-case). Use MeterListener.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement**:
  - `Stopwatch sw = Stopwatch.StartNew();` at `HandleCore` entry (~:145).
  - First-token: a `bool firstTokenRecorded = false;` guard; immediately AFTER the first `yield return CreateEvent(StreamingEventType.Token, …)` (~:382), if `!firstTokenRecorded` → in a `try { RagFirstTokenLatency.Record(sw.Elapsed.TotalMilliseconds); } catch { /* metrics must not break the stream */ }`, set the flag.
  - Citations: after `citationDtos` is built (~:550-557), `try { RagCitationsPerAnswer.Record(citationDtos.Count); } catch { }` (record once, post-stream; use `citationDtos.Count`, NOT earlier lists).
- [ ] **Step 4: Run → PASS** + the KnowledgeBase unit suite (no regression — the chat stream still works).
- [ ] **Step 5: Commit** — `feat(observability): #2582 SP5-b record rag first-token latency + citations-per-answer`

---

## Task 3: Record retrieval-empty (single-source in RagPromptAssemblyService)
**Files:** `apps/api/src/Api/.../RagPromptAssemblyService.cs` (~:282-285, the `if (filteredChunks.Count == 0)` detection); Test: the RAG retrieval service test.

**Interfaces:** Consumes T1's `RagRetrievalEmpty`.

- [ ] **Step 1: Failing test** — when retrieval returns ZERO chunks, `meepleai.rag.retrieval_empty` increments by 1; when retrieval returns chunks, it does NOT increment.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3: Implement** — at the `filteredChunks.Count == 0` site, `try { RagRetrievalEmpty.Add(1); } catch { }`. Single-source (do NOT also count in the handler).
- [ ] **Step 4: Run → PASS** + the service suite.
- [ ] **Step 5: Commit** — `feat(observability): #2582 SP5-b record rag retrieval-empty counter`

---

## Task 4: Streaming-safety regression (metrics fault must not abort the stream)
**Files:** Test only (`ChatWithSessionAgent*Tests`).

- [ ] **Step 1: Failing/guard test** — inject a metrics recorder that THROWS (or arrange a condition that would throw inside the metric Record path); drain the chat stream; assert the stream STILL completes (StreamingComplete yielded) and no exception escapes. This locks in the try/catch wrapping from T2/T3.
- [ ] **Step 2: Run** — if T2/T3 wrapped correctly, this passes; if a Record isn't wrapped, it fails → fix the wrapping.
- [ ] **Step 3: Verify** the test genuinely exercises a throwing-metric path (not a no-op).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `test(observability): #2582 SP5-b metrics fault does not abort live chat stream`

---

## Task 5: SLO recording rule + retrieval-empty alert + Grafana panel + doc
**Files:** `infra/prometheus-rules.yml`; the relevant Grafana dashboard JSON under `infra/monitoring/grafana/dashboards/`; an observability doc/runbook (the existing RAG SLO doc — find it, e.g. BGAI-082 reference).

- [ ] **Step 1: Add the recording rule** `meepleai:slo:live_rag_ttft:p95:5m` over `meepleai_rag_first_token_latency_bucket` — mirror the existing `meepleai:slo:rag_ttft:p95:5m` (prometheus-rules.yml:132-168). Target ≤ 800ms.
- [ ] **Step 2: Add the retrieval-empty alert** — `rate(meepleai_rag_retrieval_empty_total[15m]) / rate(<total live-rag answers>[15m]) > 0.05` (provisional), `severity: warning`, model on `RagErrorsDetected` (:349). Annotate "threshold provisional — tune after baseline data".
- [ ] **Step 3: Add a Grafana panel** for `meepleai_rag_citations_per_answer` (p50/p95 + `le="0"` fraction) to the appropriate dashboard JSON.
- [ ] **Step 4: Validate** — `promtool check rules infra/prometheus-rules.yml` (if promtool available; else verify YAML parses + the PromQL mirrors the existing rules' syntax). The Grafana JSON must be valid JSON.
- [ ] **Step 5: Doc** — add the live-RAG SLO targets + the cardinality rule (no session tags) to the observability ADR/runbook, marking thresholds provisional.
- [ ] **Step 6: Commit** — `feat(observability): #2582 SP5-b live-RAG SLO rule + retrieval-empty alert + citations panel + doc`

---

## Self-Review
- **AC-OBS-1**: first-token latency (T2) + retrieval-empty (T3) + citations-per-answer (T2) + SLO (T5). ✅
- **Streaming safety**: every Record wrapped (T2/T3), regression-tested (T4).
- **Cardinality**: no session tags (all tasks).
- **Risk**: SLO thresholds provisional (T5) — don't page on unvalidated targets.

## Out of scope (→ SP5-c)
- timeout/circuit-breaker, degradation-contract/bulkhead, backfill.
- Tuning the provisional SLO thresholds after real data (a future ops task, not this PR).
