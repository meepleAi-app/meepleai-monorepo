# #1823 M0 — Wikidata QID hit-rate spike

> **Goal**: Empirically measure Wikidata coverage of boardgame catalog BEFORE committing 5-7gg implementation effort. Output ADR + go/no-go decision gate.

**Issue**: #1823 — Wikidata enrichment job for legal-clean cover images
**Branch**: `feature/issue-1823-m0-wikidata-spike`
**Effort**: ~4-6h
**Pre-req**: spec-panel critique sess.46h LOCKED (DEC-3 unchanged; M0 spike PR separated per Wiegers + Crispin + Newman convergence)

---

## Hypothesis to validate

H1 (plan author): Wikidata QID hit-rate on representative catalog sample ≥ 25%
H2 (Wiegers concern): wishful "30-40%" estimate may be 15-25% actual
H3 (Newman concern): coverage strongly biased toward EN-language games

**Decision threshold**:
- **GO** if QID-hit-rate ≥ 25% AND license-machine-readable-rate ≥ 80%
- **NO-GO** otherwise → close #1823, defer to publisher-direct partnerships

---

## Sample selection

**Strategy**: 30 representative games from realistic distribution (scaled down from initial 100-game proposal for spike efficiency — 30 samples across 4 buckets is sufficient for go/no-go decision per binomial confidence interval analysis with 25% threshold)

| Bucket | Count | Source |
|---|---|---|
| BGG Top 100 | 15 | Well-known, EN-centric, expected high hit-rate |
| BGG Top 500 mid-tier | 8 | Mainstream, mixed language |
| Italian publisher games | 4 | Cranio/dV Giochi/Asmodee IT — measures IT bias |
| Long-tail / niche | 3 | Small publishers, expected low hit-rate |

**Rationale**: Statisticamente, MeepleAI catalog mirrors BGG distribution but with stronger IT bias. The 4-bucket sample reflects this without requiring real staging DB access (which would block this spike on infra).

---

## Methodology

### Step 1: SPARQL QID resolution
For each game `Title` (+ optional `Year`):

```sparql
SELECT ?game ?gameLabel ?image WHERE {
  ?game wdt:P31/wdt:P279* wd:Q131436 .  # instance of board game
  ?game rdfs:label "{title}"@en .
  OPTIONAL { ?game wdt:P18 ?image . }
  OPTIONAL { ?game wdt:P577 ?year . FILTER(YEAR(?year) = {year}) }
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en" . }
}
LIMIT 5
```

**Per-game outcome**:
- `qid_found`: bool — at least 1 result returned
- `image_p18`: string|null — Commons filename
- `disambiguation_required`: bool — > 1 result

### Step 2: Commons license fetch
For each `image_p18`:

```
GET https://commons.wikimedia.org/wiki/Special:FilePath/{filename}?action=raw
GET https://commons.wikimedia.org/w/api.php?action=query&prop=imageinfo&iiprop=extmetadata&titles=File:{filename}&format=json
```

**Per-image outcome**:
- `license_machine_readable`: bool — `LicenseShortName` field populated
- `license_code`: string|null — extracted code (PD/CC0/CC-BY-2.0/...)
- `license_whitelist_match`: bool — matches `{PD, CC0, CC-BY-*, CC-BY-SA-*}`

### Step 3: Rate limiting
- Wikidata SPARQL: 5 RPS hard cap (sleep 200ms between queries)
- Commons API: 5 RPS hard cap (same)
- Total spike duration: 30 games × 2 requests × 200ms ≈ 12s + overhead = ~30-60s

### Step 4: Aggregation metrics

| Metric | Formula | Threshold |
|---|---|---|
| QID-hit-rate | `qid_found / total_sample` | ≥ 25% GO |
| P18-image-rate | `image_p18 != null / qid_found` | report only |
| License-machine-readable-rate | `license_machine_readable / image_p18` | ≥ 80% GO |
| License-whitelist-rate | `license_whitelist_match / license_machine_readable` | report only |
| IT-bucket hit-rate | `qid_found in IT bucket / 4` | report bias |
| Niche-bucket hit-rate | `qid_found in niche bucket / 3` | report bias |

---

## Output artifacts

1. `docs/spikes/1823/sample-list.json` — input 100-game list with bucket tags
2. `docs/spikes/1823/spike-results.json` — per-game raw output
3. `docs/spikes/1823/spike-summary.md` — aggregated metrics + go/no-go decision
4. `docs/for-claude/architecture/adr/adr-2026-06-09-wikidata-enrichment-architecture.md` — ADR documenting DEC-3 validation + measured data

---

## Risks

- **Wikidata SPARQL endpoint downtime**: SPIKE_RETRY=3 with exponential backoff. If endpoint down >30min during spike window, defer + retry next day.
- **Wikimedia ToS violation**: 5 RPS strict + User-Agent header `MeepleAI-Spike/1.0 (contact@meepleai.app)`. NO production traffic during spike (single-pod, single-pass).
- **License field parsing**: Commons `extmetadata.LicenseShortName` field is non-standardized text. Manual review for edge cases in summary.

---

## Out of scope (deferred to Phase B Infrastructure PR)

- Migration columns (already shipped per discovery)
- Service skeleton (`WikidataEnrichmentService` class scaffold)
- Circuit breaker scaffolding (Polly registration)
- Distributed rate-limiter (Redis token bucket)
- Production deploy plan

These move to Phase B contingent on M0 GO decision.

---

## References

- Issue: #1823
- Spec-panel critique: sess.46h
- Plan: `docs/superpowers/plans/2026-06-09-large-medium-remaining-plan.md` § Phase 3
- Wikidata SPARQL endpoint: `https://query.wikidata.org/sparql`
- Commons API: `https://commons.wikimedia.org/w/api.php`
- DEC-3 LOCKED: sess.46f spec-panel
