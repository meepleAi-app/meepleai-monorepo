# ADR 2026-06-09 — Wikidata enrichment architecture

**Status**: Accepted
**Date**: 2026-06-09
**Issue**: #1823
**Spike**: `docs/spikes/1823/spike-summary.md`

---

## Context

Issue #1823 calls for a backend job that enriches `shared_game_catalog` with cover images from Wikidata + Wikimedia Commons (PD / CC0 / CC-BY / CC-BY-SA only). The spec proposed 30-40% catalog coverage and 5-7 days of implementation effort.

A spec-panel critique (sess.46h, six experts: Wiegers, Fowler, Newman, Nygard, Crispin, Hightower) raised seven CRITICAL + MAJOR concerns blocking implementation:
- R-001: unmeasured coverage hypothesis
- R-002: unspecified retry count + dead-letter window
- N-001: distributed rate-limiter required for multi-pod
- N-002: missing circuit breaker
- H-001: missing CDN / cache headers / WAF policy
- H-002: missing observability (Prometheus metrics)
- F-001: DEC-3b client separation conflicts with rate-limit coordination

This ADR records the architectural decisions, validated by an M0 spike against the live Wikidata SPARQL + Commons APIs.

---

## Decision (DEC-3 LOCKED, spec-panel sess.46f + updated by spike sess.46h)

### DEC-3a — SPARQL strategy (UNCHANGED from sess.46f)
**Decision**: Option A — extend existing `WikidataCatalogProvider` with `FetchCoverImageAsync(qid)` method + `wdt:P18` (image) SPARQL helper.

**Rationale**: Reuses already-shipped HttpClient + rate-limit + retry infrastructure from #1903 catalog seed work. No parallel client to maintain.

### DEC-3b — Commons fetcher (UPDATED post-spike)
**Decision (refined)**: Option A with rate-limit coordination — new `IWikimediaCommonsClient` with HttpClientFactory pattern, BUT injected shared `IWikimediaRateLimiter` token-bucket service consumed by BOTH `WikidataCatalogProvider` and `IWikimediaCommonsClient`.

**Rationale**: Preserves bounded-context separation (catalog seed vs image fetch are different domains) while addressing Fowler F-001 concern. Single coordinator prevents Wikimedia IP ban from distributed rate-limit violations.

### DEC-3c — License whitelist (UNCHANGED + VALIDATED)
**Decision**: Hardcoded constants in `LicenseValidator.cs`:
- `PUBLIC DOMAIN` / `PD`
- `CC0`
- `CC-BY` / `CC-BY-N.N` (any version)
- `CC-BY-SA` / `CC-BY-SA-N.N` (any version)

Regex (case-insensitive): `^(public domain|PD|CC0|CC[ -]BY([ -][0-9.]+)?|CC[ -]BY[ -]SA([ -][0-9.]+)?)$`

**Spike validation**: 13/14 sample images had machine-readable license, 13/13 matched whitelist (100%). Industry-standard whitelist is correct.

### DEC-3d — Image variant pipeline (UNCHANGED from sess.46f)
**Decision**: Option A — SixLabors.ImageSharp 3.x (managed C#, .NET 9 compatible, no native deps).

**Rationale**: Production-proven, zero deployment friction. Pin major version to avoid unexpected v4 license changes.

### DEC-3d-1 — ImageSharp → Magick.NET migration (LOCKED 2026-06-20)

**Decision**: Replace `SixLabors.ImageSharp` 3.1.12 with `Magick.NET-Q8-AnyCPU` 14.x (Apache 2.0).

**Rationale**:
- ImageSharp 3.x changed license to Six Labors Split License (commercial use requires paid license). MeepleAI is proprietary → incompatible.
- ImageSharp 2.1.x is EOL upstream (no security patches → audit risk).
- `Magick.NET-Q8-AnyCPU` is Apache 2.0 (commercial-friendly), provides feature parity (WebP/JPEG/PNG encode + resize + metadata Strip).
- Native deps (~70MB Linux/Windows/macOS binaries) are acceptable — image is already large from OCR/PDF stack.

**Cross-cutting impact**: rifactoring touches `WebpVariantGenerator` + `SessionAttachmentService` (both Phase F + pre-existing) + their tests. CI lint guard added (`infra/scripts/lint-deps-imagesharp.sh`) to prevent regression.

**Issue**: #2055 Phase G AC-G2.

### DEC-3e — Rate-limit topology (NEW post-spike, addresses N-001)
**Decision**: Single-pod batch CRON (HPA=1) with in-process 5 RPS token bucket. NO distributed rate-limiter required.

**Rationale**: Spike measured 30k catalog × 2 API calls × 200ms = 3.3 hours per full pass. Well within 24h CRON window. Multi-pod operation would require distributed rate-limiter (Redis token bucket); single-pod constraint eliminates that complexity for similar latency budget. Document `HPA.minReplicas=HPA.maxReplicas=1` in deployment manifest.

### DEC-3f — Circuit breaker (NEW post-spike, addresses N-002)
**Decision**: Polly circuit breaker on `WikidataCatalogProvider` AND `IWikimediaCommonsClient`. OPEN circuit after 3 consecutive 5xx within 60s for 5 min.

**Rationale**: Prevents retry storm + log noise during Wikidata outages. Standard reliability pattern.

### DEC-3g — Observability (NEW post-spike, addresses H-002)
**Decision**: 3 Prometheus metrics from day 1:
- `meepleai_wikidata_enrichment_attempts_total{outcome="success|failure|dead_letter"}` (counter)
- `meepleai_wikidata_sparql_latency_seconds` (histogram, buckets 0.1/0.5/1/5/10s)
- `meepleai_wikidata_qid_hit_rate` (gauge, computed per batch)

**Rationale**: Without these, the 59.6% catalog-wide coverage forecast cannot be validated post-deploy. Observability is gate-blocker per Hightower H-002.

### DEC-3h — CDN + cache policy (NEW post-spike, addresses H-001)
**Decision**: R2 covers/* served via Cloudflare CDN with:
- `Cache-Control: public, max-age=31536000, immutable` on R2 object upload
- Cloudflare edge cache rule: covers/* cached 1 year
- Cloudflare WAF rate-limit: 1000 RPS per IP (anti-abuse)

**Rationale**: 1-year immutability based on per-game R2 key being deterministic (`covers/{gameId}/cover.webp`). Cache busting via QID re-verification (DEC-3i below).

### DEC-3i — Quarterly QID re-verification (NEW post-spike, addresses Newman SN-001)
**Decision**: Add `WikidataQidLastVerifiedAt` column to `shared_games`. Quarterly cron re-checks QID is still valid + license unchanged.

**Rationale**: Wikidata schema can change (Q-numbers reassigned, P18 deprecated). Quarterly check prevents stale CoverR2Key pointing to dead source attribution links.

### DEC-3j — Retry + dead-letter policy (NEW post-spike, addresses R-002)
**Decision**:
- Retry count: **3** with exponential backoff (1m / 5m / 15m)
- Dead-letter retention: **7 days** (visible via admin UI page)
- Failure semantics:
  - 4xx (404 QID missing, 403 forbidden) → dead-letter immediately (no retry)
  - 5xx (Wikidata server error) → retry 3× with backoff
  - timeout → retry 3× with backoff
  - license-mismatch → dead-letter (no retry, never succeed)

---

## Spike validation

Spike methodology: 30 boardgames across 4 buckets (BGG top 100, mid-tier, Italian publishers, niche).

| Metric | Threshold | Measured | Status |
|---|---|---|---|
| QID hit-rate | ≥ 25% (GO) | **60%** | ✅ +35pp margin |
| License machine-readable | ≥ 80% (GO) | **93%** | ✅ +13pp margin |

Catalog-wide forecast (weighted): **~59.6%** (vs spec hopeful 30-40%).

Bucket bias: IT publishers 23pp behind EN top BGG. Documented as known limitation.

---

## Consequences

### Positive
- Empirical hit-rate forecast (59.6%) is HIGHER than spec claim — better ROI on 5-7gg effort
- License validator complexity LOW (100% of machine-readable licenses pre-validated in spike sample)
- Architecture compromises (DEC-3b refined, DEC-3e single-pod) reduce operational complexity
- Observability + circuit breaker + dead-letter prevent production firefighting

### Negative
- IT publisher coverage 50% (vs 73% top BGG) means ~50% IT games won't have covers post-batch. Mitigation: follow-up spike for IT-specific fallback (Italian Wikipedia? Publisher API?) tracked separately.
- Quarterly re-verification cron adds operational burden (small, but persistent)
- Single-pod constraint blocks horizontal scaling — acceptable for batch but locks design

### Neutral
- HttpClient v3.x via existing `WikidataCatalogProvider` reuse is sound
- 1-year R2 cache is fine given deterministic key + quarterly re-verification

---

## Implementation milestones (Phase B onward)

Per spike GO decision, proceed to Phase B Infrastructure PR:

| Milestone | Effort | Status |
|---|---|---|
| M0 spike + ADR | ~4-6h | ✅ this PR |
| M1 EF migration (SharedGameEntity columns) | 4h | ✅ already shipped via #1903/#1821 |
| M2 `IWikimediaRateLimiter` token bucket | 4h | Phase B |
| M3 `WikidataCatalogProvider.FetchCoverImageAsync` SPARQL helper | 6h | Phase B |
| M4 `IWikimediaCommonsClient` + license fetcher | 6h | Phase B |
| M5 `LicenseValidator` + whitelist regex | 2h | Phase B |
| M6 `ImageSharp` webp variant generator | 4h | Phase B |
| M7 R2 upload pipeline | 4h | Phase B |
| M8 `EnrichCatalogCoverCommand` + Handler | 4h | Phase B |
| M9 BackgroundService scheduler + dead-letter | 6h | Phase C |
| M10 Polly circuit breaker | 2h | Phase C |
| M11 Prometheus metrics | 3h | Phase C |
| M12 Admin ad-hoc trigger endpoint | 3h | Phase C |
| M13 Admin dead-letter visibility page (BE+FE) | 6h | Phase C |
| M14 `<MeepleCard>` attribution footer (FE) | 4h | Phase D |
| M15 Quarterly QID re-verification cron | 4h | Phase D |
| M16 Integration tests (Testcontainers + mock APIs) | 8h | Phase D |
| M17 Staging dry-run | 4h | Phase D |
| **Total Phase B+C+D** | **~70h ≈ 8-9 working days** | Net of M0 |

Net-of-M0 effort: **~9 working days** (vs original spec estimate 5-7gg pre-spike). Plan revised upward post-spike given concrete observability + circuit breaker + cache policy work.

---

## References

- Issue: #1823
- Umbrella: #1821
- Spec-panel critique: sess.46h (Wiegers + Fowler + Newman + Nygard + Crispin + Hightower)
- Spike summary: `docs/spikes/1823/spike-summary.md`
- Plan: `docs/superpowers/plans/2026-06-09-large-medium-remaining-plan.md` § Phase 3
- DEC-3 origin: sess.46f spec-panel + user-locked

## Follow-up ADRs

- [ADR-082](./adr-082-external-media-enrichment-ports.md) — External Media Enrichment ports/adapters layout (Proposed 2026-06-20, issue [#2055](https://github.com/meepleAi-app/meepleai-monorepo/issues/2055) Phase G). Formalizes the BC-internal port pattern for `IWikidataCoverEnrichmentRunner` + `IWikimediaCommonsClient` and rejects the `MediaEnrichment` shared-BC alternative. Closes Newman SN-001 gap from 2026-06-20 spec-panel.
