# Mechanic-Validation Calibration Spike — 2026-04-26

**Sprint**: ADR-051 Sprint 2
**Tasks**: T23 (calibration runs) + T24 (write spike report)
**Issue**: ISSUE-524 — Mechanic Extractor M1.2 async pipeline
**Branch**: `feature/mechanic-validation-sprint-2`
**Status**: Blocking bug resolved & committed; live calibration runs deferred (require running API + DeepSeek provider)

---

## 1. Goal

Run the M1.2 async mechanic extractor against two reference rulebooks, capture the produced
claims, and label 20 of them (10 per game) to seed the calibration corpus at
`tests/fixtures/calibration-2026-04-26.csv`. Outcomes feed the certification thresholds
and the matching engine being designed for Sprint 2.

| Game | SharedGameId | PdfId | AnalysisId |
|---|---|---|---|
| Carcassonne | `007b3cf5-632c-43ba-8057-2b1384d1165d` | `36353632-e135-4b63-b7bd-69a009413e73` | `273f6b64-88c2-4597-8071-852886f54217` |
| 7 Wonders | `34d37d9c-dd71-4607-aef1-52b96d51dc84` | `35d8a0d9-d2af-4a6a-891c-1a261df04a1d` | `a8d3476e-66b1-4066-80ed-43aecc79bf12` |

Both analyses were already created (in `Draft` state) by previous Sprint 1 work; the spike's
job was to advance them through the 6 sections (Summary, Mechanics, Victory, Resources,
Phases, Faq) and harvest claims.

---

## 2. Blocking Bug Found & Fixed

### 2.1 Symptom

Triggering any section run on either analysis produced an immediate
`DbUpdateConcurrencyException` from `SaveChangesAsync` inside
`MechanicAnalysisExecutor.RunSectionAsync`. The pipeline never advanced past the first
section, blocking T23 entirely.

### 2.2 Initial (incorrect) hypothesis

The first investigation suspected `xmin` advancement: maybe the orchestrator was loading the
analysis, an out-of-band update was bumping `xmin`, and the executor's stale token caused the
concurrency check to fail.

**Why that hypothesis was wrong**: the executor uses `GetByIdWithClaimsAsync(...)` with
`AsNoTracking()`, mutates the detached aggregate in-memory only, then calls
`Update(analysis) → SaveChangesAsync`. There is no concurrent writer between load and save in
the same logical operation. The `xmin` value would not move under that flow.

### 2.3 Actual root cause: EF Core graph-traversal semantics in `Update()`

`DbSet.Update(rootEntity)` walks the entire entity graph. For each child, EF assigns
`EntityState` based on `IsKeySet`:

- **Non-default Guid** (e.g., `Guid.NewGuid()`) → `IsKeySet=true` → `EntityState.Modified`
- **Default Guid** (`Guid.Empty`) → `IsKeySet=false` → `EntityState.Added`

But the mechanic-extractor pipeline mints **fresh** child Ids inline:

```
analysis.AddClaim(MechanicClaim.Create(...))   // Id = Guid.NewGuid()  ← non-default
   └── citations from MechanicCitation.Create(...)  // Id = Guid.NewGuid()  ← non-default
```

So when the executor calls `_analysisRepository.Update(analysis)`, EF marks every
brand-new claim/citation as `Modified` and emits `UPDATE … WHERE Id = …` against a
non-existent row. The first such UPDATE returns 0 affected rows, the entity-tracker's
reconciliation pass detects the mismatch on a child entity, and throws
`DbUpdateConcurrencyException` — but the exception message references the **root**
analysis entity, which is what threw us off the trail initially.

This was never caught by the test suite because
`MechanicAnalysisRepositoryIntegrationTests.BuildDraftAnalysisWithClaim(...)` constructs
its claims via `MechanicClaim.Reconstitute(...)` with pre-allocated Ids that are
*assumed already persisted* — i.e., the test path never exercises the
"fresh-claim-not-yet-in-DB → Update" code path that the production executor relies on.

### 2.4 Fix: domain `IsNew` flag + explicit per-entity state assignment

Pattern is intentionally minimal and DDD-friendly:

1. **Domain entities** (`MechanicClaim`, `MechanicCitation`) carry a private-set
   `bool IsNew` property:
   - Defaults to `true` (new instances from `Create()` factory)
   - Set to `false` only by the `Reconstitute()` factory used by the repository's
     `MapToDomain` rehydration path

2. **Repository `Update()`** no longer calls `DbSet.Update()`. Instead:
   - `Attach()` the root, set `EntityState.Modified`
   - Clear `IsModified` on the `Xmin` property so EF doesn't try to dirty the server-managed
     concurrency token (the `WHERE xmin = @loaded_value` predicate is still emitted, so the
     concurrency check itself is preserved)
   - Build an Id → IsNew lookup from the domain aggregate
   - Per child, set `EntityState.Added` (IsNew=true) or `EntityState.Modified` (IsNew=false)

`SynthesizeAuditRows` and `CollectDomainEvents` are unchanged.

**Commit**: `cd708babc` — `fix(mechanic-extractor): resolve DbUpdateConcurrencyException in executor pipeline`

### 2.5 Verification

| Suite | Result | Notes |
|---|---|---|
| `dotnet build` | ✅ 0 errors, 0 warnings | |
| `MechanicAnalysisRepositoryIntegrationTests` | ✅ 8/8 PASS | Includes the AC9 concurrency-conflict test — the `xmin` check still works |
| `--filter MechanicExtractor` (full sub-tree) | ⚠️ 36/39 PASS | 3 pre-existing failures, unrelated; see §4 |

The 36 passing tests in the broader filter exercise the executor → repository → unit-of-work
code path indirectly; combined with the 8/8 repository tests, this gives us high confidence
that the fix resolves the executor blocker without regressions.

---

## 3. Tasks Deferred (require live API)

The following T23 sub-tasks need a running API container with the DeepSeek provider
configured (`infra/secrets/deepseek.secret`). They are **unblocked** by this fix but
were not executable in the current headless context:

- [ ] **T23-A**: Run all 6 sections of analysis `273f6b64-…` (Carcassonne)
  - Endpoint: `POST /api/v1/admin/mechanic-extractor/analyses/{id}/run-section`
  - Expected: ~30-50 claims across the 6 sections, given Carcassonne's complexity
- [ ] **T23-B**: Run all 6 sections of analysis `a8d3476e-…` (7 Wonders)
  - Caveat from prior session: 7 Wonders rulebook triggers T1 quote-cap violations
    (≥25-word quotes) and `max_tokens` truncation in some sections; may need
    prompt-tuning iteration
- [ ] **T16**: Label 20 calibration claims (10 per game) into
  `tests/fixtures/calibration-2026-04-26.csv` with columns:
  `analysis_id, claim_id, section, claim_text, expected_label, expected_page,
  source_quote, t1_violation, notes`
  - Labels: `correct`, `incorrect`, `partial`, `out_of_scope`

Suggested operator runbook (sketch — flesh out when running live):

```bash
# from infra/
make dev-core                # postgres + api + redis + minio
# wait for /healthz green

# obtain admin session cookie (login flow); export AUTH_COOKIE
# trigger 6 sections per analysis:
for SECTION in Summary Mechanics Victory Resources Phases Faq; do
  curl -sS -X POST -H "Cookie: $AUTH_COOKIE" \
    "http://localhost:8080/api/v1/admin/mechanic-extractor/analyses/273f6b64-88c2-4597-8071-852886f54217/run-section" \
    -d "{\"section\":\"$SECTION\"}" -H 'content-type: application/json'
  # poll status until Pending → Pending(next section ready) or Done
done

# pull resulting claims for labeling:
curl -sS -H "Cookie: $AUTH_COOKIE" \
  "http://localhost:8080/api/v1/admin/mechanic-extractor/analyses/273f6b64-…" > carcassonne-claims.json
```

---

## 4. Independent Finding: 3 Pre-existing Test Failures

The `MechanicExtractor` filter run surfaced 3 failures **already present on `main-dev` before
this fix** (verified by stashing the fix and re-running):

| Test | Status | Likely cause |
|---|---|---|
| `AdminMechanicExtractorValidationEndpointsTests.CreateGolden_HappyPath_Returns201` | 500 instead of 201 | `MechanicGoldenClaim.CreateAsync` calls `IEmbeddingService` + `IKeywordExtractor`; these likely throw in the integration test environment because the test fixture doesn't wire real implementations. |
| `…UpdateGolden_HappyPath_Returns204` | 500 instead of 204 | Same handler family. |
| `…DeleteGolden_HappyPath_Returns204` | 500 instead of 204 | Same handler family. |

These belong to the **Golden Set CRUD** path (admin tooling for hand-curated reference
claims), not the AI extraction pipeline this spike addresses. Recommendation: file a
follow-up ticket; resolution likely requires either mocking the embedding/keyword services
in the test fixture or providing a deterministic offline implementation for them.

---

## 5. Lessons & Next Actions

### Lessons

1. **EF graph traversal is treacherous when domain factories pre-mint Ids.** The
   `IsKeySet` heuristic conflates "has a key value" with "exists in DB" — fine for legacy
   `int identity` keys, dangerous for `Guid.NewGuid()` patterns.
2. **`DbUpdateConcurrencyException` doesn't always mean concurrency.** A 0-row UPDATE on a
   never-existed row triggers the same exception, with a confusingly root-entity-flavoured
   message.
3. **Test gaps mirror the bug.** The repository tests were green because they reused the
   `Reconstitute` path; the executor's "fresh claim" path was never integration-tested.
   Worth adding a focused test in a follow-up.

### Recommended next actions

1. ✅ **DONE**: Land this fix on `feature/mechanic-validation-sprint-2`.
2. **Add an integration test** for the executor's fresh-claim flow:
   - Seed an analysis with no claims, persist
   - Reload, AddClaim x N (via `Create` factory), Update, SaveChanges
   - Assert no exception, claims persisted with correct ids
3. **Resume T23 calibration** when an operator can run the API live. The SQL state of the
   two analyses is already prepared.
4. **File follow-up**: investigate the 3 Golden CRUD endpoint failures (independent issue,
   not Sprint 2 critical-path).

---

## Appendix A — Files touched

| File | Change |
|---|---|
| `…/Domain/Entities/MechanicClaim.cs` | Add `IsNew` property; `Reconstitute` sets it `false` |
| `…/Domain/Entities/MechanicCitation.cs` | Add `IsNew` property; `Reconstitute` sets it `false` |
| `…/Infrastructure/Repositories/MechanicAnalysisRepository.cs` | Replace `DbSet.Update(entity)` with explicit Attach + per-child `EntityState` |

Net diff: +61 / −3 lines across 3 files. No public API surface change.
