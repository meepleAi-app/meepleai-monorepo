# Spec — RAG enhancement live-path: validation & gated rollout (#3390 Slice 4)

**Date**: 2026-08-02
**Epic**: #3390 (unificare la risposta agente in-sessione dietro RAG grounded), programma-ombrello #3397
**Status**: Accepted — hardened by a `/sc:spec-panel` review (Wiegers, Crispin, Nygard, Adzic, Hightower, Fowler); the four open decisions were **resolved 2026-08-02** (§9).
**Related**: ADR-090 (ownership + consolidation direction — **prerequisite**), #3388 (grounding contract), #3389 (RetrievalPolicy), #3467/#3477 (golden eval-set + baseline), #3475 (eval harness path fix), BGG freeze #2123 / ADR-059 (closed-domain outbound ban).

---

## 1. Executive summary — the reframe

The naïve framing of Slice 4 is *"flip the `rag.enhancement.*` flags on the live path and measure each against the baseline."* **The panel rejected that framing as unexecutable and unsafe.** Two code-anchored facts (verified) break it:

- **Enhancements are gated OFF for two independent reasons** (`RagPromptAssemblyService.cs:185`): `if (enhancementsAllowed && userTier != null)`. The live path passes `RetrievalPolicy.LiveSession` (`EnhancementsEnabled=false`) **and** `userTier: null`.
- **The eval harness measures a different pipeline than the live path** (`DatasetEvaluationService.cs:111` uses `RagService.AskWithHybridSearchAsync`, not `RagPromptAssemblyService.AssemblePromptAsync` where enhancement gating lives). Flipping the enhancement flags therefore has **zero effect** on the eval numbers — a #3475-class wrong-path defect, latent again.

Slice 4 is therefore a **sequenced program**, not a config change: (Step 0) consolidate the duplicated grounded pipeline onto one seam so eval and live share it; (Step 1) make the eval exercise that seam with a self-verifying canary; (Step 2) resolve the enhancement gate as an explicit policy value decided in production and inherited by the eval; (Step 3) build the CRAG web-egress kill-switch **before** measuring CRAG; (Step 4) per-enhancement measurement under a real statistical decision rule; (Step 5) staged live rollout gated on online observability with automatic rollback.

**Baseline** (#3477, golden set 34 samples / 28 citation-graded, staging): citation_accuracy **0.286** (calibrated floor), citation_structural_validity **1.00**, answer_correctness **0.55**, P95 **2529ms** (already > the 1500ms Phase-5 target), recall/nDCG/MRR **N/A** (snippet.source is doc-level → recall degenerate by construction; needs chunk-id labeling, #3427/#3438).

---

## 2. Objective

Enable on the in-session live path **only** the enhancements (AdaptiveRouting, CRAG, RAPTOR, RAG-Fusion, GraphTraversal) whose per-enhancement effect on grounding quality is **statistically distinguishable from noise and positive**, without regressing correctness or net user-perceived latency, with CRAG web-fallback **structurally unable** to make outbound calls (closed rulebook domain).

---

## 3. Sequenced plan (the dependency graph, corrected)

Fowler's rule — *make the change easy, then make the easy change* — reorders the draft's "dependencies" into the critical path:

- **Step 0 — Consolidate (ADR-090 prerequisite; tracked as a SEPARATE gating issue, D3).** Extract the grounded pipeline (assemble → tier resolve → generate → leak guard → map citations → grounding) into a single `IGroundedAnswerService` in KnowledgeBase, invoked by **both** `AskGroundedSessionQueryHandler` and `ChatWithSessionAgentCommandHandler` (the streaming adapter). Rationale: R5 wiring five toggles into two divergent handlers manufactures drift. Enhancements must wire **once**. Per D3 this is its own issue (**#3490**, independently reviewable/mergeable) that is a **HARD dependency** — no `rag.enhancement.*` flag touches the live path until it lands.
- **Step 1 — One seam for eval + live (subsumes R1).** The eval harness must invoke the **same** `IGroundedAnswerService` the session path uses, with the **same** `RetrievalPolicy` value passed in — measuring the live path *by construction*, not by a third impersonating harness. Fowler: the #3475 class is structural ("two entry points will diverge"); the fix is one entry point, not a requirement to "use the right one."
- **Step 2 — Resolve the gate as an explicit policy value (R2 decision).** See §9-D1. Model `RetrievalPolicy` as an immutable value object carrying an explicit `EnabledEnhancements` **set**; resolve tier→set **once at the boundary**, not re-checked deep inside `AssemblePromptAsync`. Then "LiveSession + exactly {CRAG}" is `LiveSession with EnabledEnhancements={Crag}` — R3's "exactly one on" is a one-line construction, and R2 collapses to a factory function. The tier/set used in eval **MUST** equal the one resolved in production (asserted via the shared seam), or #3475 returns.
- **Step 3 — CRAG web-egress kill-switch + guard (before any CRAG measurement).** See §7. A dedicated `rag.enhancement.crag-web-fallback` flag (default OFF) **and** an architectural egress guard (no web-egress collaborator injected on the live path) so "0 web calls" is a structural fact, not a hoped-for test.
- **Step 4 — Per-enhancement measurement** under the statistical decision rule (§6).
- **Step 5 — Staged live rollout** with online observability gate + automatic rollback (§8).

---

## 4. Requirements (panel-hardened)

Each requirement has a **fit criterion** (metric + threshold + measurement procedure). Given/When/Then examples in §5.

- **R0 (consolidation gate)** — No `rag.enhancement.*` flag may be enabled on the live path until Step 0 lands: a single `IGroundedAnswerService` invoked by both in-session handlers. *Fit*: a characterization test asserts identical grounded output for a fixed `(input, policy)` across both handler adapters.
- **R1 (single-seam fidelity)** — Eval and live invoke the same retrieval entry point with the same `RetrievalPolicy`. *Fit (self-verifying canary)*: with a deliberately-corrupting canary enhancement (returns empty context) as the only one ON, eval citation_accuracy drops measurably below 0.286. If flipping any `rag.enhancement.*` flag produces a **null delta** on every graded metric, R1 FAILS (you are on the wrong path).
- **R1a (fired-rate observability)** — Each eval run emits, per sample, a signal that `activeEnhancements` was non-empty and the LiveSession-with-enhancements pipeline ran. *Fit*: a run where any graded sample went through the baseline path is **INCONCLUSIVE**, not a pass.
- **R2 (gate resolution — tier-independent, D1)** — The `userTier=null` + `EnhancementsEnabled=false` double-gate is replaced by an explicit `EnabledEnhancements` set on `RetrievalPolicy`, resolved **at the boundary from the GLOBAL `rag.enhancement.* ` flags** (NOT per-tier). Grounding is a correctness property, not a tier perk, so it does not vary by user tier. The gate at `RagPromptAssemblyService.cs:185` becomes: use `retrievalPolicy.EnabledEnhancements` directly when present; else fall back to the legacy `enhancementsAllowed && userTier != null → GetActiveEnhancementsAsync(userTier)` path (backward-compatible for the classic QA paths). *Fit*: eval and live construct the SAME `RetrievalPolicy` value → Nygard's hard constraint (eval gate == prod gate) holds by construction, because there is no tier to keep in sync.
- **R3 (per-enhancement protocol)** — Baseline and **exactly one** enhancement measured in the **same harness run on the same build**, with cache bypass, deterministic ordering, and an interleaved baseline to detect drift. Repeat for all 5. *Fit*: §6 decision rule.
- **R4 (per-enhancement gate)** — An enhancement PASSES only if **all** hold on the 28-graded set (same-run baseline):
  1. Δcitation_accuracy significant by paired **McNemar** at p<0.05 **after Holm–Bonferroni correction for 5 comparisons**, and citation_accuracy never below the 0.286 floor;
  2. citation_structural_validity ≥ 1.00 (no regression);
  3. answer_correctness ≥ 0.55 (no regression), reported as mean±CI over ≥N repetitions;
  4. **enhancement-fired rate ≥ 90%** per run (R1a); and **no offline latency-cap** (D2) — instead the net user-perceived P95 latency delta (enhancement-on minus baseline, **including timed-out/fail-open requests**) must not regress **beyond the test-retest noise floor** (§6). There is deliberately no fixed offline ceiling; the operational latency ceiling is the **R6 online P95/P99 SLO + auto-rollback** — a slow enhancement that passes offline is caught live, not shipped blind;
  5. CRAG only: `crag_web_calls_total == 0`, **egress-guard-verified** (§7), on a sample **engineered to force the low-relevance corrective branch**.
- **R5 (staged live rollout)** — Enable each flag on **one canary tier** after its offline gate passes; hold ≥N live sessions; promote only if the R6 online SLOs hold; **auto-disable on trigger breach**; widen tier-by-tier. One flag armed per live window (never two — attribution).
- **R6 (online observability gate)** — For each enhancement, after the offline gate, require a minimum live sample where `meepleai.agent.response.grounding{grounding_status=Grounded}` rate does **not** regress vs the pre-flip window on the same `retrieval_profile`. `meepleai.rag.enhancement.activations` proves the gate opened on the live path (guards a *second* silent no-op at `:185`); `meepleai.rag.crag.verdicts` is a **standing production SLO** for web-fallback=0.
- **R7 (failure modes)** — Budget scope is **shared** (D2): the existing 700ms Slice-2 retrieval budget covers baseline + enhancements together — the user waits for the total, so a per-enhancement budget would understate real latency. Unchanged from Slice 2. Plus: a **circuit breaker** that sheds an enhancement after N consecutive timeouts (so a down LLM endpoint doesn't make every request pay the full timeout); partial-result policy for RAG-Fusion multi-query (proceed-with-partial vs discard); and a **load test** under real concurrency (a single-user 34-sample staging eval cannot certify a latency budget on the hottest path).
- **R8 (recall blind-spot control)** — While recall is N/A, an enhancement that raises citation_accuracy by **pruning** citations (precision↑, recall↓) must not silently pass. *Fit*: a compensating manual/exploratory pass on a fixed per-enhancement sub-sample flags "confidently grounded but incomplete" answers; OR a proxy coverage metric. The doc-level-snippet limitation is stated, not silently exempted.

---

## 5. Executable examples (Specification by Example)

**R1 fidelity canary** (retires the wrong-path risk before trusting any delta):
```
Given the harness routes through IGroundedAnswerService with a LiveSession policy
  And EnabledEnhancements = { a corrupting canary that returns empty context } ONLY
When POST /api/v1/admin/eval/retrieval runs on the golden set
Then citation_accuracy drops measurably below 0.286
Else  R1 FAILS — the harness is not on the live path
```

**Per-enhancement gate** (template, instantiate 5×: rag-fusion / adaptive-routing / raptor / graph / crag):
```
Given staging index (116 games) + golden set (28 citation-graded), bypassCache=true
  And EnabledEnhancements = { RagFusion } ONLY on the LiveSession path
When the eval runs twice (test-retest) via IGroundedAnswerService
Then enhancement-fired rate ≥ 90% per run
  And paired McNemar on per-sample citation-correctness is significant at p<0.05 (Holm-corrected)
  And citation_accuracy ≥ 0.286  And structural_validity ≥ 1.00  And answer_correctness ≥ 0.55
  And net P95 latency delta ≤ T ms (incl. fail-open cases)
Else INCONCLUSIVE (not PASS)
```

**CRAG negative-space** (the assertion is vacuous unless the branch is forced and the guard is real):
```
Given the closed rulebook domain with web egress denied at the boundary (no web collaborator injected)
  And EnabledEnhancements = { Crag }, rag.enhancement.crag-web-fallback = OFF
  And a golden sample engineered so top-k relevance falls BELOW CRAG's threshold (forces refine branch)
When IGroundedAnswerService evaluates retrieval
Then crag_web_calls_total == 0  And the egress guard records zero outbound attempts
  And CRAG refines/decomposes over the LOCAL corpus only
  And a detection canary (force one call in a test) confirms the guard actually trips
```

---

## 6. Statistical decision rule (why single-run point estimates are invalid)

With 28 graded samples at p≈0.286, SE(citation_accuracy) ≈ √(0.286·0.714/28) ≈ **0.085** → a single-run 95% CI ≈ **±0.17**. A move 0.286→0.40 is inside sampling noise; answer_correctness adds grader nondeterminism. Therefore:

1. **Paired design** — same golden items, baseline vs enhancement, same run/build. Analyze discordant pairs with **McNemar's test**; report the CI, not a point estimate.
2. **Noise floor** — run the baseline **twice** first; measure test-retest variance. No delta below the noise floor is actionable.
3. **Multiple-comparison correction** — 5 enhancements inflate false positives → **Holm–Bonferroni**.
4. **Pre-register a minimum detectable effect** and record that n=28 only powers detection of **large** effects. Small-but-real gains are honestly reported as *underpowered/inconclusive*, not failures.
5. **Never compare against the stored #3477 number** (different day/build/possibly different judge) — re-baseline in the same run.

---

## 7. CRAG web-egress: kill-switch + architectural guard (before Step 4)

Risk: web-fallback is suppressed today **only** because enhancements are off wholesale (`RagPromptAssemblyService.cs:185`). The instant CRAG is enabled to measure it, egress becomes reachable — a closed-domain outbound call is latency + correctness + **BGG-freeze-adjacent** (#2123 / ADR-059) risk. An offline assertion does not stop a network call in production. Required, landing **before** any CRAG measurement:

- **`rag.enhancement.crag-web-fallback`** flag, default OFF (the dedicated off-switch risk (e) admits is missing today).
- **Architectural egress guard**: the live grounded path is constructed with **no web-egress collaborator injected** → the web branch is structurally unreachable, not merely flag-gated. The `EvaluationCragPlugin`/`RetrievalWebPlugin` remain unwired from the live pipeline.
- **`crag_web_calls_total`** observable defined (you cannot test a metric that doesn't exist) + `meepleai.rag.crag.verdicts` used as a standing SLO (any web-fallback verdict on the live path = alert).
- **Detection canary**: an integration test forces one web call and confirms the guard throws/returns rather than dials out — an assertion never seen to fail is not evidence.

---

## 8. Live rollout & rollback (offline-green is not the last gate)

Offline eval on 34 samples is a **necessary but not sufficient** gate. The class of failure — eval and live diverge — is not retired by R1 (which fixes one instance); it is retired by **watching production**. Required:

- **Shadow first** (recommended): compute `activeEnhancements` and the grounding delta on live traffic **without serving** the enhanced answer, to observe the real-traffic effect at zero user risk before any flip.
- **Canary by tier** (uses the R2 tier resolution): enable → one low-risk tier → hold the R6 live window → widen. One enhancement armed per window.
- **Rollback trigger + automation** (not "retain the ability to flip"): auto-disable when `grounding_status=Ungrounded` rate rises >X% over the pre-flip window, OR `crag.verdicts` shows any web-fallback, OR the in-session P95/P99 SLO breaches. Wire to the Slice-1 alert (`HighUngroundedRateOnRagLivePath`, #3480) — extend it per-enhancement.
- **Dashboard before the first flag.**

---

## 9. Resolved decisions (2026-08-02)

- **D1 — enhancement gate → tier-independent (RESOLVED).** The double-gate is replaced by an explicit `RetrievalPolicy.EnabledEnhancements` set resolved at the boundary from the **global** `rag.enhancement.*` flags (not per-tier); grounding does not vary by user tier. The `:185` gate uses the set directly when present, else the legacy tier path. Nygard's hard constraint (eval gate == prod gate) is satisfied *by construction* — eval and live build the same policy value, and there is no tier to keep in sync. See R2. (Chosen over the synthetic-shared-tier option, which coupled grounding to tier and required keeping tier-eval == tier-prod.)
- **D2 — no fixed offline latency cap (RESOLVED).** The offline gate does NOT impose a fixed ms ceiling; it requires enhancement-fired rate ≥ 90% and no P95 regression **beyond the test-retest noise floor** (§6). Budget scope is **shared** (the 700ms Slice-2 retrieval budget covers baseline + enhancements). The operational latency ceiling is the **R6 online P95/P99 SLO + auto-rollback** — a slow enhancement that slips past offline is caught in live traffic, not shipped blind. See R4.4, R7. (Honest caveat: this trades an explicit offline ceiling for a data-driven gate; it is only safe *because* R6 carries the online SLO — do not weaken R6.)
- **D3 — consolidation is a SEPARATE gating issue (RESOLVED → #3490).** The ADR-090 `IGroundedAnswerService` refactoring is tracked as **issue #3490** (independently reviewable/mergeable, ADR-090 already lists it), a **HARD dependency** that must land before any `rag.enhancement.*` flag touches the live path. Slice 4 does not absorb it; Slice 4 is *gated* by it. #3490 also carries the D1 work (`RetrievalPolicy.EnabledEnhancements` + tier-independent gate). See Step 0, R0.
- **D4 — compensating manual recall control, do NOT block (RESOLVED).** Real recall requires chunk-level snippet ids (today `snippet.source` is doc-level) **in addition to** `RelevantChunkIds` labeling — a separate plumbing workstream, not just data entry. Blocking Slice 4 on it would delay on work largely orthogonal to the enhancement decision. Proceed with R8's manual coverage pass per enhancement to catch precision↑/recall↓; track real recall as a #3427/#3438 follow-up (the labeling machinery — `GenerateLabelingCandidates` + `MergeLabels` → `RelevantChunkIds` — already exists; the missing piece is chunk-level retrieval ids + the labels themselves).

---

## 10. Out of scope
Text-path (non-in-session) enhancement tuning; removing the multimodal fallback; the chunk-id labeling work itself (#3427/#3438, a dependency for D4).

---

## 11. Panel provenance
`/sc:spec-panel` (2026-08-02), 6 independent lenses on the draft: Wiegers (measurable fit criteria — R4/R2 not yet acceptance criteria), Crispin (statistical power, fired-rate, isolation, negative-space operationalization), Nygard (latency-as-primary-failure, fail-open refunds nothing, tier-gate #3475 recreation, CRAG runtime kill-switch, load test), Adzic (executable examples; R2 unexampleable = not a requirement; forcing sample for CRAG), Hightower (offline-only rollout, unused Slice-1 telemetry, rollback automation, shadow/canary), Fowler (consolidation is prerequisite; R1 == the same refactoring; RetrievalPolicy as an EnabledEnhancements value object; egress as a structural fact).
