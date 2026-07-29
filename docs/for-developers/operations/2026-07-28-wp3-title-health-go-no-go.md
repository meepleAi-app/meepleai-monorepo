# WP3 — Title-health metric + WP4 go/no-go read-out

**Epic:** [#3338](https://github.com/meepleAi-app/meepleai-monorepo/issues/3338) (RAG extraction heading-detection). **Date:** 2026-07-28.

## The metric

`TitleHealthMetric.Compute` (`apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/TitleHealthMetric.cs`) scores a game's extraction quality from the **section headings its chunks carry** (`text_chunks.Heading` — what retrieval sees):

- **PlausibleFraction** — fraction of DISTINCT headings that look like real section titles. A heading is *plausible* when it is 2–40 chars, carries a real word (a run of ≥3 consecutive letters — rejects `D`, `S U`, `I L E X Y R F`), and is ≥60 % letters (rejects `(14%), Oceani`, `© 2016 FryxGames www.…`). It measures title-**likeness**, not section-correctness; the target is gross extraction garbage.
- **CanonicalCoverage** — how many curated `SectionHeadingLexicon` section types are present.
- **Band** — `green` ≥ 0.80 · `yellow` ≥ 0.50 · `red` otherwise.

## Read-out (staging, 2026-07-28, post-WP1 deploy)

Computed over the real staging headings of 6 games. **Only Terraforming Mars was re-chunked with WP1**; the others reflect the pre-WP1 chunking (baseline).

| Game | lang | distinct | plausible % | canonical | band |
|---|---|---|---|---|---|
| Wingspan | en | 102 | 90 % | 0 | 🟢 green |
| Catan | en | 104 | 87 % | 2 | 🟢 green |
| Dominion | en | 121 | 85 % | 0 | 🟢 green |
| Ark Nova | en | 107 | 79 % | 4 | 🟡 yellow |
| **Terraforming Mars** | it | 68 | **72 %** | **11** | 🟡 **yellow** |

(`canonical` = distinct plausible headings matching a curated lexicon section word.)

## WP4 go/no-go decision — **DEFERRED / descope-able**

Terraforming Mars — the epic's motivating case — is **yellow (72 %)** after WP1, with the **highest canonical coverage (11)**: the WP1 heading-repair (embedded-title splitter + `Header`→`Title` promotion + synonym-aware boost) recovered the real section headings. The `Setup per N giocatori` query now works end-to-end (WP2: the `PREPARAZIONE` chunk is retrieved rank #1 and the answer is grounded).

The residual 28 % (garbage `Title` elements unstructured emits — `I L E X Y R F`, `D`, `(14%), Oceani`) is **demoted by WP1b (number-noise) and does not block retrieval**. WP4 (hi_res IT extraction) would push TM yellow → green by fixing the extraction at the geometry level, but its cost is real and load-bearing (per epic WP4: HttpClient 35 s timeout + retry storm, yolox weights not baked into the image, hi_res >90 s on CPU) and is **not justified to go yellow → green when the functional goal is already met**.

**Decision:** WP4 is deferred/descope-able. Re-evaluate if a broader IT cohort — once re-chunked with WP1 (the 12 other IT docs + the 65 currently-`Failed` PDFs, both pending a re-index) — shows a **red** cohort whose retrieval actually regresses. Until then WP1 (+ the number-noise demotion) is sufficient.

## Remaining WP3 (follow-up) — SHIPPED

- **Corpus `title-health` admin endpoint** — `GET /api/v1/admin/kb/title-health` (`AdminKnowledgeBaseEndpoints`, admin-scoped). Computes `TitleHealthMetric` per shared game over its distinct `text_chunks.Heading` values, returning `GameTitleHealthDto[]` (band, plausibleFraction, canonicalCoverage, distinctHeadings, dominant language). Query/handler: `KnowledgeBase/Application/Queries/GetCorpusTitleHealth`. Handler EF translation (nullable-`GameId` join → `shared_games`/`pdf_documents`, `LanguageOverride ?? Language`, server-side `Distinct`) is covered by a Testcontainers integration test.
- **Regression guard — runs against STAGING** (`.github/workflows/title-health-staging.yml`). `infra/scripts/title-health-assert.sh` + committed baseline `infra/fixtures/title-health-baseline.json`. Assert mode FAILs on a **band downgrade** (green→yellow/red, yellow→red), a **plausible-fraction drop** beyond `fractionRegressionTolerance` (0.05), or a **baselined game vanishing**. A new (unbaselined) game is a NOTICE, never a FAIL. Opens a deduped `title-health-regression` issue on failure (ADR-078).

### Why staging, not the CI seed snapshot

The gate originally ran inside `rag-smoke-dispatch.yml` against the published seed snapshot. **That snapshot is baked with the Docnet extractor (plain text, zero `Title` structure) → headingless corpus → the metric returns 0 games** and the capture aborts. Baking the snapshot with **Unstructured** (which produces headings) was attempted and **overran the 6h GitHub-hosted job limit** (~3× slower than Docnet + one PDF stalls the poller at 126/127; run `30433004576`). The 6h limit is hard on GitHub-hosted runners, so title-health cannot be armed against the CI snapshot. It was **re-architected to run against STAGING** — which carries the heading-aware re-indexed corpus (WP2) — via SSH-fetch (`title-health-staging.yml` logs in as admin on the server and GETs `localhost:8080/api/v1/admin/kb/title-health`, then `title-health-assert.sh --current-file` compares on the runner). The title-health steps were removed from `rag-smoke-dispatch.yml`, which now runs the retrieval smoke only.

### Capture step (required to arm the gate)

The baseline ships with an EMPTY `games` map → the gate SKIPs (dormant). Arm it by dispatching **`title-health-staging.yml` with `update_baseline=true`**, then download the `title-health-baseline-<run_id>` artifact and commit `infra/fixtures/title-health-baseline.json`. **Staging is not version-pinned** (unlike the seed snapshot), so **re-capture after any deliberate staging re-index** that changes headings; the guard still catches unintended band downgrades between captures.

### Still open

- Arm the title-health gate: `title-health-staging.yml -f update_baseline=true` → commit the baseline (needs the staging DEPLOY_METHOD=ssh vars + the staging admin creds on the server).
- Capture the `tmars-setup-it` rag-smoke golden baseline (`rag-smoke-dispatch.yml -f update_baseline=true`) so that query asserts instead of SKIPs.
