---
title: External API Spike Protocol (Gate 0 template)
status: ACTIVE
canonical: true
date: 2026-06-20
author: Claude Opus 4.7 (sc:spec-panel synthesis)
applies_to: future external API integration (Wikidata, BGG, OpenRouter, Wikimedia Commons, ...)
origin: extracted from #2055 Wikidata L2 Phase G design (DEC-3k LOCKED 2026-06-20)
---

# External API Spike Protocol — Gate 0 Template

> **Purpose**: empirical hard-stop gate per integrations con external API third-party. Trasforma assumption (es. "coverage rate ≥30% expected") in evidence committed PRE-implementation.

## Quando applicare questo protocollo

| Scenario | Apply? |
|----------|--------|
| Nuova integration con external API che impatta UX (cover images, prices, ratings) | ✅ MANDATORY |
| Migration tra provider (es. OpenRouter model swap, BGG → Wikidata) | ✅ MANDATORY |
| Internal service integration (BC-to-BC, MediatR commands) | ❌ Skip (no third-party) |
| Library upgrade (es. ImageSharp 2→3, EF Core 8→9) | ❌ Skip (use ADR + migration plan instead) |
| Bug fix su integration esistente | ❌ Skip (use systematic debugging) |

**Trigger esempi**: BGG ToS scenario (#1903), OpenRouter model price changes, future Wikimedia Commons full-text fetch, hypothetical Discogs music DB integration.

## Anatomia del protocollo (4 fasi)

### Fase 0 — Spike Definition (~30min)

Output: spike kickoff doc che dichiara hypothesis + metric + acceptance threshold.

**Template**:

```markdown
# Spike — [External API name] [feature scope] — YYYY-MM-DD

**Hypothesis**: la copertura/disponibilità/qualità di [feature X] è ≥ [N%] sui [target population].

**Sample size**: N = 50 random sampled from [shared_games | active_users | recent_orders]
- Rationale: 50 = statistical sample minimum per binomial proportion ±10% confidence interval at 90% level
- Override: N=100 se population <500, N=20 se rate-limit budget esiguo

**Metrics tracked**:
| Metric | Target threshold | Branch decision |
|--------|------------------|-----------------|
| [Primary metric, es. qid_resolved_rate] | ≥70% | GREEN if met; otherwise drill |
| [Secondary metric, es. accepted_license_rate] | ≥60% of primary | GREEN if met |
| [Latency metric, es. sparql_p95_latency] | ≤2000ms | DESCOPE M[X] features if exceeded |
| [Cost metric, es. tokens/request OR API quota burn] | <[N] units | ABORT if exceeded |

**Decision matrix**:
- coverage_below_15pct → **ABORT**: feature infeasible, propose alternative (es. L1 placeholder, manual curation)
- coverage_15_to_25pct → **DESCOPE**: ship core only, drop non-essential features (es. FE attribution, advanced filters)
- coverage_above_25pct → **GREEN**: proceed full plan

**Exit conditions**:
- Audit doc committed con sample data raw (JSON/CSV) + aggregate metrics + decision recorded
- GitHub issue comment con decision summary linked to audit doc
- Plan TDD updated post-decision (descopa o abort tasks segnati)
```

### Fase 1 — Spike Execution (~2-6h depending on API)

**Pattern**:

1. **Script standalone** (NOT integration nel main codebase):
   - Path convention: `infra/scripts/spikes/[topic]/sample-N.sh` (bash) o `.csx` (C# script) o `.py`
   - Inputs: sample IDs (es. UUID list from DB query)
   - Outputs: raw JSON/CSV con 1 row per sample + metric aggregates
   - NO production wiring — DB read-only, NO write side effects

2. **Rate-limit respect**: usa sleep/exponential backoff anche durante spike (proteggi quota production)

3. **Reproducibility**: spike script idempotent + deterministic sample IDs (es. fixed seed)

### Fase 2 — Audit Doc Output (~1h)

Output: `docs/for-developers/audits/YYYY-MM-DD-[topic]-spike.md`

**Template structure**:

```markdown
# Spike Audit — [Topic] — YYYY-MM-DD

## Summary
- Decision: **GREEN | DESCOPE | ABORT**
- Recommended next: [plan task / descopa task / abort path]

## Methodology
- Sample size: N = [actual N]
- Sample selection: [random | top-N by usage | stratified]
- API endpoint: [URL]
- Rate-limit: [N RPS]
- Run date: [YYYY-MM-DD HH:MM UTC]

## Raw data
- Attach JSON/CSV: [link to file]
- Sample IDs: [list of N UUIDs]

## Aggregate metrics
| Metric | Threshold | Observed | Decision branch |
|--------|-----------|----------|-----------------|
| ... | ... | ... | ... |

## Edge cases discovered
- [List of N edge cases con sample ID reference]

## Decision rationale
- [Why GREEN/DESCOPE/ABORT]
- [Trade-offs accepted]

## Recommended plan adjustment
- [DESCOPE task X] OR
- [REVISE acceptance criterion Y] OR
- [ABORT — propose alternative Z]
```

### Fase 3 — Hard-Stop Gate (decision lock)

**Convention**: il plan TDD `docs/superpowers/plans/YYYY-MM-DD-[topic].md` ha Phase 0 con esattamente questo checklist:

```markdown
## Phase 0 — Gate 0 Spike (HARD STOP)

- [ ] Spike script committed: `infra/scripts/spikes/[topic]/sample-N.sh`
- [ ] Spike executed (timestamp committed in script log)
- [ ] Audit doc committed: `docs/for-developers/audits/YYYY-MM-DD-[topic]-spike.md`
- [ ] Decision recorded in audit doc (GREEN / DESCOPE / ABORT)
- [ ] Phase 1+ tasks updated based on decision

**HARD STOP**: NO Phase 1 task can begin until Phase 0 is fully checked. Document decision in PR description quoting audit doc URL.
```

## Sample spike — Wikidata L2 enrichment (#1823)

Esempio applicato (instance del template):
- Spike script: `infra/scripts/spikes/wikidata-cover/sample-50.csx` (hypothetical)
- Audit doc: `docs/for-developers/audits/2026-06-09-wikidata-coverage-spike.md`
- Decision: **GREEN** (qid_resolved=60%, accepted_license=100%, sparql_p95=1800ms)
- Plan: `docs/superpowers/plans/2026-06-09-large-medium-remaining-plan.md` § Phase 3 (subsequente: `docs/superpowers/plans/2026-06-20-wikidata-l2.md` per Phase G post-shipped)

## FAQ

### Posso skippare lo spike se ho ANY signal che il feature funzionerà?

No. Lo spike non è opzionale — è la condizione di acceptance per AC tipo "≥30% coverage expected" che altrimenti sarebbero speculation (Wiegers C-001 anti-pattern). Skip causa over-engineering downstream.

### Spike costa tempo, posso fare quick proof-of-concept?

Lo spike È un PoC strutturato. Differenza chiave: spike ha threshold + decision matrix lockata PRE-execution, PoC tipicamente no.

### Se il sample size N=50 non è raggiungibile?

Riduci a N=20 con stricter confidence interval declaration. Sotto N=10 non considerare spike statistically valid — usa direttamente alternative analysis (es. manual review).

### Cosa succede se Phase 0 spike fa GREEN ma poi implementation diverge?

Lo spike non è garanzia di success post-Phase 1+. È un gate iniziale. Se implementation rileva nuovi gap, riapri Phase 0 sub-spike (es. Wikidata L2 ha sub-spike per Commons license metadata machine-readability post-M2).

### Posso riusare spike data per future audit?

Sì, fino a [3 mesi] post-execution. Oltre, re-run spike per refresh — external API change.

## References

- Origin: [#2055 Wikidata L2 Phase G design](../../superpowers/specs/2026-06-20-wikidata-l2-design.md) § Gate 0
- DEC-3k LOCKED: 2026-06-20 (this template made repo-wide pattern)
- Sample applied: [Wikidata L2 #1823 spike](../audits/2026-06-09-wikidata-coverage-spike.md) (esempio canonico)
- Related: [Brainstorming skill](../../../../.claude/skills/) (Phase 0 = brainstorm output crystallization)
