# RAG Evaluation Suite Scaffolding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing RAG retrieval-evaluation engine honest and usable: exclude unlabeled samples from retrieval metrics, add language support + per-language breakdown, add a report formatter + admin runner, an AI-proposes/human-verifies labeling-assist workflow, and an EN seed dataset.

**Architecture:** All types live under `apps/api/src/Api/BoundedContexts/KnowledgeBase/` (Domain/Evaluation + Application/Evaluation), are `internal`, file-based (JSON via snake_case DTOs), and use `IRagService` as the only external collaborator (mocked in tests). CQRS: any HTTP entry uses `IMediator.Send` only. No DbContext anywhere in this subsystem.

**Tech Stack:** .NET 9, MediatR, System.Text.Json (SnakeCaseLower), xUnit + FluentAssertions + Moq.

## Global Constraints

- Issue #3433; branch `feature/issue-3433-rag-eval-scaffolding` (parent `main-dev`).
- Retrieved chunk id == `snippet.source` (per the current evaluator, `DatasetEvaluationService.cs:113`). Labeling candidates MUST expose and collect that same `source` value into `relevant_chunk_ids`.
- JSON is snake_case-lower via `JsonNamingPolicy.SnakeCaseLower`; a C# `Language` prop maps to `"language"`.
- "Labeled" == `RelevantChunkIds.Count > 0`.
- Unlabeled samples: EXCLUDED from recall@k / nDCG@10 / MRR aggregates; `AnswerCorrectness` still computed over all successful samples. Report both counts.
- No CI workflow added (runner is a `make` target only). Execution against staging is deferred until the #3427 re-index completes.
- Commit after each task. Test filter base: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "<...>"`. Kill testhost before runs (`Get-Process testhost | Stop-Process -Force`).

---

### Task 1: Coverage counts + shared aggregation helper

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationMetrics.cs` (add `LabeledSampleCount`, `UnlabeledSampleCount`; extend `Create` + `Empty`; add static `Compute(IReadOnlyList<EvaluationSampleResult>)`).
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationMetricsTests.cs`

**Interfaces:**
- Produces: `EvaluationMetrics` with new `int LabeledSampleCount { get; init; }` and `int UnlabeledSampleCount { get; init; }`; `EvaluationMetrics.Create(recallAt5, recallAt10, ndcgAt10, mrr, p95LatencyMs, answerCorrectness, sampleCount, labeledSampleCount, unlabeledSampleCount)`; `static EvaluationMetrics Compute(IReadOnlyList<EvaluationSampleResult> sampleResults)`.

**Behavior of `Compute`:** filter `IsSuccess`; if none → `Empty`. Partition successful into `labeled` (`RelevantChunkIds.Count > 0`) and `unlabeled`. recall@5/@10 = `labeled.Average(HitAt5/HitAt10 ? 1:0)`; ndcg@10 = `labeled.Average(NdcgAt10)`; mrr = `labeled.Average(ReciprocalRank)`; if `labeled` empty these are `0.0`. answerCorrectness = `successful.Average(AnswerCorrectness)`. p95 over successful latencies (unchanged formula). `sampleCount = successful.Count`, `labeledSampleCount = labeled.Count`, `unlabeledSampleCount = successful.Count - labeled.Count`.

- [ ] **Step 1 (RED):** In `EvaluationMetricsTests.cs` add `Compute_ExcludesUnlabeledFromRetrievalMetrics_ButCountsThem`: build results = 1 labeled (HitAt5=true, ReciprocalRank=1.0, RelevantChunkIds=["c1"]) + 1 unlabeled (HitAt5=false, ReciprocalRank=1.0, RelevantChunkIds=[]), both `IsSuccess`. Assert `RecallAt5 == 1.0` (only the labeled one counts, not 0.5), `Mrr == 1.0` (labeled only, not polluted), `LabeledSampleCount == 1`, `UnlabeledSampleCount == 1`, `SampleCount == 2`. Use the existing `EvaluationSampleResult` construction pattern from the file.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~EvaluationMetricsTests.Compute_ExcludesUnlabeled"` → FAIL (Compute not defined / counts absent).
- [ ] **Step 3 (GREEN):** Add the two `int` props (default 0) to the record, extend `Empty` (both 0) and `Create` (two new params, `Math.Max(0, ...)`), and implement static `Compute` per the behavior above. Move the existing aggregation math from `DatasetEvaluationService.ComputeMetrics` into it (Task 2 will delegate).
- [ ] **Step 4:** Run the filter → PASS. Also run `--filter "FullyQualifiedName~EvaluationMetricsTests"` → all PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): coverage counts + shared metrics aggregation (#3433)`.

---

### Task 2: Both aggregators delegate to Compute (fix unlabeled pollution)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Evaluation/Services/DatasetEvaluationService.cs` (`ComputeMetrics` body → `return EvaluationMetrics.Compute(sampleResults);`).
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationResult.cs` (`CalculateMetrics` private static → `return EvaluationMetrics.Compute(sampleResults);`).
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Evaluation/Services/DatasetEvaluationServiceTests.cs`

**Interfaces:**
- Consumes: `EvaluationMetrics.Compute` (Task 1).

- [ ] **Step 1 (RED):** In `DatasetEvaluationServiceTests.cs` add `EvaluateDataset_WithMixedLabeledAndUnlabeled_ReportsCoverageAndCleanRetrievalMetrics`: mock `IRagService.AskAsync` (via existing `CreateQaResponse`) to return a snippet whose `source` matches the labeled sample's relevant id; dataset = 1 labeled sample (`relevant_chunk_ids` = that source) + 1 unlabeled sample. Assert `result.Metrics.UnlabeledSampleCount == 1`, `result.Metrics.LabeledSampleCount == 1`, and `result.Metrics.RecallAt5 == 1.0` (labeled retrieved), i.e. the unlabeled one didn't drag recall to 0.5.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~DatasetEvaluationServiceTests.EvaluateDataset_WithMixedLabeled"` → FAIL (coverage counts 0 / recall polluted).
- [ ] **Step 3 (GREEN):** Replace `ComputeMetrics` body with `return EvaluationMetrics.Compute(sampleResults);` and `EvaluationResult.CalculateMetrics` body with `return EvaluationMetrics.Compute(sampleResults);`. Delete the now-dead duplicated math in both.
- [ ] **Step 4:** Run the new filter + `--filter "Category=Unit&BoundedContext=KnowledgeBase"` (eval subset) → PASS, no regressions.
- [ ] **Step 5:** Commit `fix(rag-eval): exclude unlabeled samples from retrieval metrics (#3433)`.

---

### Task 3: `Language` field on EvaluationSample (+ dataset default)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationSample.cs` (add `string? Language`).
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationDataset.cs` (`SampleDto.Language`, wire in `FromJson`/`ToJson`).
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationDatasetTests.cs`

**Interfaces:**
- Produces: `EvaluationSample.Language` (`string?`, JSON key `language`).

- [ ] **Step 1 (RED):** In `EvaluationDatasetTests.cs` add `FromJson_ReadsLanguageField_RoundTrips`: JSON dataset with one sample carrying `"language": "it"` (plus the required fields + ≥ nothing else; single sample is fine here since Validate only warns). Assert `dataset.Samples[0].Language == "it"`, and `EvaluationDataset.FromJson(dataset.ToJson()).Samples[0].Language == "it"` (round-trip).
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~EvaluationDatasetTests.FromJson_ReadsLanguage"` → FAIL (Language null).
- [ ] **Step 3 (GREEN):** Add `public string? Language { get; init; }` to `EvaluationSample`. Add `public string? Language { get; set; }` to private `SampleDto`. In `FromJson` sample mapping set `Language = s.Language`; in `ToJson` sample mapping set `Language = sample.Language`.
- [ ] **Step 4:** Run the filter + `--filter "FullyQualifiedName~EvaluationDatasetTests"` + `~EvaluationSampleTests` → PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): language field on evaluation samples (#3433)`.

---

### Task 4: EvaluationReportFormatter (markdown + json, coverage + per-language)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Evaluation/Services/EvaluationReportFormatter.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Evaluation/Services/EvaluationReportFormatterTests.cs`

**Interfaces:**
- Consumes: `EvaluationResult` (has `Metrics`, `SampleResults`, `DatasetName`), `EvaluationMetrics` (with coverage counts, Task 1).
- Produces: `internal static class EvaluationReportFormatter` with `static string ToMarkdown(EvaluationResult result, IReadOnlyDictionary<string, EvaluationMetrics> byLanguage)` and `static string ToJson(EvaluationResult result, IReadOnlyDictionary<string, EvaluationMetrics> byLanguage)`. Also `static IReadOnlyDictionary<string, EvaluationMetrics> MetricsByLanguage(EvaluationResult result, EvaluationDataset dataset)` — groups sample results by their sample's `Language` (`"unknown"` when null) via `SampleId` join, running `EvaluationMetrics.Compute` per group.

- [ ] **Step 1 (RED):** In `EvaluationReportFormatterTests.cs` add `ToMarkdown_IncludesCoverageAndPerLanguageBreakdown`: build an `EvaluationResult` (via its `Create`) from 2 sample results + a `byLanguage` dict `{ "en": metricsA, "it": metricsB }`. Assert the markdown string contains `Recall@10`, `Labeled`, `Unlabeled`, `en`, `it`. Add `ToJson_IsValidJsonWithMetrics`: assert `JsonDocument.Parse(json)` succeeds and root has a `recall_at_10` (or documented key) and a `by_language` object.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~EvaluationReportFormatterTests"` → FAIL (class missing).
- [ ] **Step 3 (GREEN):** Implement the formatter. Markdown: a header table (recall@5/@10, nDCG@10, MRR, P95, answer-correctness, labeled/unlabeled coverage) + a per-language section. JSON: a serializable projection (snake_case) `{ dataset, metrics{...}, coverage{ labeled, unlabeled }, by_language{...} }`. Implement `MetricsByLanguage` grouping.
- [ ] **Step 4:** Run the filter → PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): evaluation report formatter with coverage + language breakdown (#3433)`.

---

### Task 5: Labeling-assist — generate candidates + merge labels

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Evaluation/Commands/GenerateLabelingCandidatesCommand.cs` (+ Handler).
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Evaluation/Commands/MergeLabelsCommand.cs` (+ Handler).
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Evaluation/LabelingReview.cs` (review DTOs).
- Create: tests `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Evaluation/Commands/LabelingAssistTests.cs`

**Interfaces:**
- Produces: `LabelingReview` (record: `IReadOnlyList<LabelingReviewItem> Items`), `LabelingReviewItem` (`string SampleId`, `string Question`, `IReadOnlyList<LabelingCandidate> Candidates`), `LabelingCandidate` (`string ChunkId` (= snippet.source), `int Page`, `float Score`, `string Snippet`, `bool? Relevant`). `GenerateLabelingCandidatesCommand(string DatasetPath, int TopN = 10) : IRequest<LabelingReview>`. `MergeLabelsCommand(string DatasetPath, LabelingReview Review) : IRequest<EvaluationDataset>` — returns dataset with `RelevantChunkIds` = the `ChunkId`s where `Relevant == true`, per SampleId.

- [ ] **Step 1 (RED):** In `LabelingAssistTests.cs`, `Generate_DumpsTopNCandidatesFromRetrieval`: mock `IRagService.AskAsync` returning 2 snippets (source "s1"/"s2", page/score/text set); dataset (loaded from a temp file written in the test) with 1 sample. Assert the returned `LabelingReview.Items[0].Candidates` has 2 items with `ChunkId=="s1"`, `Relevant==null`. `Merge_CollectsRelevantTrueIntoRelevantChunkIds`: given a `LabelingReview` with candidate s1 `Relevant=true`, s2 `Relevant=false` for sample X, assert merged dataset sample X `RelevantChunkIds` == `["s1"]`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~LabelingAssistTests"` → FAIL.
- [ ] **Step 3 (GREEN):** Implement both commands + handlers + DTOs. Generate: load dataset (`EvaluationDataset.FromJson`), for each sample `AskAsync(GameId ?? "", Question, null, false, ct)`, take top-N snippets → candidates (`ChunkId = snippet.source`, `Page = snippet.page`, `Score = snippet.score`, `Snippet = snippet.text`, `Relevant = null`). Merge: for each review item, set the matching sample's `RelevantChunkIds` to candidates where `Relevant == true`; rebuild the dataset (use `AddSample` on a fresh `EvaluationDataset.Create`).
- [ ] **Step 4:** Run the filter → PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): labeling-assist candidate generation + merge (#3433)`.

---

### Task 6: Admin endpoint + make target

**Files:**
- Create: `apps/api/src/Api/Routing/KnowledgeBase/AdminEvalEndpoints.cs` (routes: `POST /api/v1/admin/eval/retrieval` → `RunEvaluationCommand` then format; `POST /api/v1/admin/eval/labeling-candidates` → `GenerateLabelingCandidatesCommand`). Admin-only, `IMediator.Send` only.
- Modify: the endpoint registrar that maps KnowledgeBase admin endpoints (follow the pattern of a sibling `Map*Endpoints()` call in `Program`/routing registration — locate it first).
- Modify: `infra/Makefile` (add `eval-retrieval` target calling the endpoint via curl, mirroring the rag-smoke pattern; `ENV`/dataset path parameterized).
- Modify: DI — ensure `RunEvaluationCommandHandler` / `GenerateLabelingCandidatesCommandHandler` resolve via MediatR (verify the assembly scan picks up `IRequestHandler<>`; if the concrete-class registration at `KnowledgeBaseServiceExtensions.cs:472-473` is the only one, add the handlers to the MediatR registration).
- Test: `apps/api/tests/Api.Tests/.../AdminEvalEndpoints*` OR an integration test following the existing admin-endpoint test pattern.

**Interfaces:**
- Consumes: `RunEvaluationCommand`, `GenerateLabelingCandidatesCommand`, `EvaluationReportFormatter`.

- [ ] **Step 1 (RED):** Endpoint handler test: `POST /api/v1/admin/eval/retrieval` with a body `{ datasetPath }` returns 200 + a body containing metrics (mock the mediator or use the WebApplicationFactory admin pattern already used in the repo — locate a sibling admin-endpoint test first and mirror it). Assert admin-auth is required (non-admin → 403).
- [ ] **Step 2:** Run the endpoint test → FAIL (route missing).
- [ ] **Step 3 (GREEN):** Add the endpoints (admin-gated), wire the registrar, confirm MediatR resolves the handlers, add the `make eval-retrieval` target.
- [ ] **Step 4:** Run the endpoint test + `dotnet build` (routing analyzer clean) → PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): admin eval endpoint + make runner (#3433)`.

> If the admin WebApplicationFactory endpoint test proves disproportionately heavy for this slice, downgrade Step 1-2 to a handler-level test of the two commands' wiring and note the endpoint is smoke-covered manually (it is not on any CI path — execution is deferred until the #3427 re-index completes). Do not skip the auth assertion.

---

### Task 7: EN seed dataset

**Files:**
- Create: `tests/evaluation-datasets/meepleai-en-seed.json`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Evaluation/EvaluationSeedDatasetTests.cs`

**Interfaces:**
- Consumes: `EvaluationDataset.FromJson`, `EvaluationSample.Language` (Task 3).

- [ ] **Step 1 (RED):** `EvaluationSeedDatasetTests.cs`: `EnSeed_LoadsAsValidLanguageTaggedDataset`: read `tests/evaluation-datasets/meepleai-en-seed.json`, `EvaluationDataset.FromJson`, assert every sample `Language == "en"`, `RelevantChunkIds` empty (unlabeled by design), and sample count ≥ 12. Resolve the path relative to the repo root the same way sibling dataset tests do (locate one first).
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~EvaluationSeedDatasetTests"` → FAIL (file missing).
- [ ] **Step 3 (GREEN):** Author `meepleai-en-seed.json` (`source_type: "meepleai_custom"`) with ~12 EN samples over the 5 golden games (Catan, Wingspan, Dominion, Ark Nova, 7 Wonders): each `{ id, question, expected_answer, source, source_page, section, difficulty, category, game_id, expected_keywords, relevant_chunk_ids: [], dataset_source: "meepleai_custom", language: "en" }`.
- [ ] **Step 4:** Run the filter → PASS.
- [ ] **Step 5:** Commit `feat(rag-eval): EN seed evaluation dataset (#3433)`.

---

## Self-Review

- **Spec coverage:** C1→T3, C2→T1+T2, C3→T4+T6, C4→T5, C5→T7. All covered.
- **Type consistency:** `EvaluationMetrics.Compute` defined T1, consumed T2/T4. `snippet.source` == candidate `ChunkId` == `relevant_chunk_ids` entry, consistent T5. `Language` (`string?`, key `language`) defined T3, consumed T4/T7.
- **Known risk (documented):** `snippet.source` is not a stable per-game chunk id; labels captured now may need re-verification after a corpus re-index (already noted in the spec's Rischi section).
- **Deferred/out of scope:** mass labeling, IT authoring, execution on staging (post-reindex).
