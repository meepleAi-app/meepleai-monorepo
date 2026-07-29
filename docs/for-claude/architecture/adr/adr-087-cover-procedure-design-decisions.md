# ADR-087 — Cover procedure design decisions (D1–D5)

**Status**: Accepted
**Date**: 2026-07-29
**Tracker**: [#3373](https://github.com/meepleAi-app/meepleai-monorepo/issues/3373)
**Supersedes/relates**: [ADR (wikidata-enrichment)](./adr-2026-06-09-wikidata-enrichment-architecture.md) (DEC-3e amended by D4), [ADR-060](./adr-060-live-session-persistence.md) (xmin precedent, referenced by D3)

## Context

A multi-agent spec-panel (critique + Socratic, 2026-07-29) reviewed the game-cover procedure — four cover sources (user-custom, PDF-derived, BGG re-upload, Wikidata/Commons) resolved at read time by `CoverUrlResolver` with precedence `user → PDF(L4) → BGG(L2.5) → Wikidata(L2) → placeholder`, backed by deterministic per-source R2 keys.

The review surfaced three tactical bugs — all shipped (#3369 batch→runner, #3370 preview size, #3371 retry jitter) — plus **five structural design decisions** that must be settled before any broader cover refactor, because the refactor would cement whatever answer is left implicit. This ADR records the ratified outcome of those five decisions. Two (D2, D5-C) were ratified as implementable-now and shipped in #3379; three (D1, D3, D4) and one sub-decision (D5-A) were ratified in the Socratic session on 2026-07-29.

Fresh evidence that informed the ratifications:
- The PDF **cover-generation metric** (`meepleai.cover.generation.total{source,outcome}`, D5-C, #3379) now measures the PDF failure rate → D1's retry budget is tunable on data.
- **Staging is a single Compose instance** (`container_name: meepleai-api`, no Kubernetes/HPA) → D4 is a real-deployment fact, not a hypothetical.
- The **Wikidata at-least-once pipeline is proven end-to-end** on staging (Chess → Q718 → CC0 image → R2 upload → resolver presigned URL) → the retry/dead-letter model that D1 extends to PDF is validated.

## Decisions

### D1 — Unified delivery contract *(ratified)*

One declared delivery semantics applies to **both** the PDF and Wikidata cover paths:
- **Transient** infra failure (`r2-upload-error`, DB save failure) → **bounded retry**.
- **Permanent** outcome (heuristic-reject/`Skipped`, corrupted image) → **terminal**.

PDF gains bounded retry by reusing `BackfillPdfCoversJob` + an attempt counter (terminal after ~3, tunable against the D5-C metric). No separate dead-letter apparatus is duplicated. Rationale: today the same `r2-upload-error` is terminal on the PDF path but retried on Wikidata — an accidental asymmetry, not a chosen contract. Implementation: [#3382](https://github.com/meepleAi-app/meepleai-monorepo/issues/3382).

### D2 — Runner as mandatory SSOT boundary *(ratified — SHIPPED)*

Every Wikidata enrichment entry point MUST route through `WikidataCoverEnrichmentRunner` (attempt-log + retry/dead-letter + SSE). Enforced at compile time by Roslyn analyzer **MAI006** (`NoEnrichCatalogCoverCommandBypassAnalyzer`), which flags `new EnrichCatalogCoverCommand` outside the runner. Shipped: #3377 / PR #3379.

### D3 — Cross-store consistency: reconcile + benign last-writer-wins *(ratified)*

- **Orphan reconcile**: the orphan-recovery job is extended to `Failed`-without-`CoverR2Key` rows by scanning for the deterministic physical key (`covers/pdf/{id}/cover-preview.webp`) — the same determinism that gives idempotency gives recovery.
- **Concurrency**: the Wikidata cover write is wrapped in `try/catch(DbUpdateConcurrencyException)` with reload+retry; **last-writer-wins is declared benign-by-design** because two concurrent enrichments of the same QID produce identical columns (same license, same deterministic key). `xmin` (per ADR-060) is deliberately NOT added here — the write is commutative, not merely un-collided. Implementation: [#3382](https://github.com/meepleAi-app/meepleai-monorepo/issues/3382).

### D4 — Deployment contract: single-pod, presidiato *(ratified)*

The enrichment tier is single-instance **by contract**. The in-process `InMemoryWikimediaRateLimiter` (5 RPS) and the dead-letter gauge are correct only at one instance; a second instance would double the Wikimedia request rate (ToS violation) and skew the metric, **silently**.
- **Now**: a Prometheus tripwire `count(up{job="meepleai-api"}) > 1` (alert `MultipleApiInstances`) makes a scale-out **loud**; a comment in the compose files; this DEC-3e amendment. Implemented in the same PR as this ADR.
- **Deferred (fast-follow)**: a Redis fail-closed lease on the batch (hard prevention) and a DB-`COUNT`-backed dead-letter gauge — both only matter at >1 instance, which the tripwire now guards. A distributed Redis rate-limiter stays in reserve for an actual HPA roadmap. Deferred tasks: [#3383](https://github.com/meepleAi-app/meepleai-monorepo/issues/3383).

Rationale: staging is Compose single-instance with no HPA roadmap, so a distributed rate-limiter is YAGNI; but leaving the constraint incidental (pinned only by `container_name`) is unacceptable because it touches legal correctness (Wikimedia ToS) and would fail silently.

### D5 — Executable writer↔resolver contract + generation telemetry

- **D5-C — telemetry *(ratified — SHIPPED)***: `meepleai.cover.generation.total{source,outcome}` emitted at every terminal generation site across the three PDF producers. Shipped: #3378 / PR #3379.
- **D5-A — CoverKind key-builder *(ratified)***: the suffix convention (`.webp` / none / `-preview.webp`) is centralized into a `CoverKind` enum → shared key-builder used by both the writers and the resolver, so write-key and read-key coincide by construction and the double-suffix `.webp.webp`→404 becomes impossible; plus a Docker-free contract-test that runs locally (today `CoverR2ConventionIntegrationTests` skips on the MinIO limit). Eliminates the root cause of the `ValidateIdentifier` bug that recurred three times. Implementation: [#3384](https://github.com/meepleAi-app/meepleai-monorepo/issues/3384).

## Consequences

- **Positive**: a single delivery contract (D1) removes the "PDF failures need manual reset" surprise; the reconcile + benign-LWW (D3) closes the orphan/race without the cost of `xmin`+outbox; the single-pod tripwire (D4) converts a silent legal risk into a loud alert at near-zero cost; the `CoverKind` builder (D5-A) makes the suffix bug structurally impossible.
- **Costs / risks**: D1+D3 are a single ~M PDF work-stream (shared jobs — must not compete on records); D4's hard-prevention (lease) and correct multi-pod gauge are deferred and only become necessary if the org adopts HPA; D5-A is an ~M refactor whose value is realized when the cover-stack is next touched.
- **Sequence**: D5-C ✅ (baseline) → **D1+D3 as one PDF work-stream** → **D4 tripwire+docs** (parallel, independent) → **D5-A** refactor. Nothing else in a broader cover refactor should start before the D5-C baseline (already in `main-dev`) exists, since it is the only way to prove no regression.

## Links

- Umbrella tracker + Socratic ratification: [#3373](https://github.com/meepleAi-app/meepleai-monorepo/issues/3373)
- Shipped: #3369, #3370, #3371 (tactical); #3377 (D2), #3378 (D5-C) via PR #3379
- To implement: #3382 (D1+D3), #3383 (D4 deferred), #3384 (D5-A)
