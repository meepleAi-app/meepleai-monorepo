# Post-mortem Template

> Use this template for every incident requiring a post-mortem per [rollback-runbook §14.1](./rollback-runbook.md#141-when-required) or [operations-manual §17](./operations-manual.md#17-incident-response). Target fill time: **< 30 min for a typical P1 incident**.

## How to use

1. Copy this template to `post-mortems/YYYY-MM-DD-<slug>.md` (e.g. `post-mortems/2026-05-15-api-oom-cascade.md`).
2. Fill every section. Sections marked **REQUIRED** must be complete before review.
3. Open a tracking GitHub issue per action item, link bidirectionally.
4. Submit for review within the timeline mandated by severity:
   - **P1 prod**: < 48 h
   - **P1 staging**: < 1 week
   - **Failed rollback**: < 24 h (with founder review)
5. Once reviewed, link the post-mortem from the parent incident issue and close.

## Storage convention

```
docs/for-developers/operations/post-mortems/
└── YYYY-MM-DD-<short-kebab-slug>.md   (one file per incident, immutable post-review)
```

The slug should be 2–5 words describing the **symptom**, not the root cause (the cause may take days to confirm). Examples: `api-oom-cascade`, `pgvector-index-rebuild`, `staging-deploy-stuck`.

---

# Post-mortem — \<Incident title\> (YYYY-MM-DD)

## Header  **REQUIRED**

| Field | Value |
|---|---|
| **Incident ID** | `INC-YYYY-NNN` (sequential, see latest in `post-mortems/`) |
| **Date detected** | YYYY-MM-DD HH:MM UTC |
| **Date resolved** | YYYY-MM-DD HH:MM UTC |
| **Duration** | `<hours>h <minutes>m` (resolved − detected) |
| **Severity** | P1 / P2 / P3 / P4 — see [operations-manual §17 Severity Levels](./operations-manual.md#severity-levels) |
| **Environment** | prod / staging / dev |
| **Author** | @github-handle (incident responder primary) |
| **Reviewers** | @handle1, @handle2 (≥ 1 required, ≥ 2 for P1) |
| **Status** | draft → in-review → published |

## Summary  **REQUIRED**

One paragraph (≤ 5 sentences): **what happened**, **who was impacted**, **how it was resolved**. Written for a reader unfamiliar with the system.

## Impact  **REQUIRED**

| Dimension | Detail |
|---|---|
| **Users affected** | Count / % of active users (estimate OK if exact unavailable) |
| **Functionality impacted** | Which features were degraded or unavailable |
| **Data integrity** | None / Recoverable / Permanent loss (if loss, quantify) |
| **Revenue impact** | None / $estimate (if applicable) |
| **Reputation impact** | None / Internal-only / Customer-visible / Public |
| **SLO breach** | Yes/No — which SLO, by how much |

## Timeline  **REQUIRED**

All times in UTC. Each entry: `HH:MM` + marker (`DETECT` / `DECIDE` / `ACTION` / `RESOLVE`) + concise factual statement.

```
HH:MM  DETECT   — <how the incident was discovered>
HH:MM  DECIDE   — <key decision and rationale>
HH:MM  ACTION   — <what was done>
HH:MM  RESOLVE  — <service confirmed healthy / final smoke test passed>
```

Reference issue/PR/dashboard links inline. Do not editorialise — keep judgement for the "what went well/poorly" section.

## Root cause  **REQUIRED**

Five-whys analysis. Each "why" should advance one layer deeper (symptom → trigger → contributing factor → systemic cause → cultural/process root). Stop when you reach a cause that, if removed, would have prevented the incident.

1. **Why did X happen?**  A
2. **Why A?**  B
3. **Why B?**  C
4. **Why C?**  D
5. **Why D?**  E ← root cause

If the chain bottoms out at < 5 levels because the root is clear earlier, state that explicitly. If you cannot complete 5 whys (insufficient evidence), flag the gaps under "Unknowns".

### Unknowns

List anything you cannot determine from available evidence. Each unknown should become an action item to add observability or improve audit trail.

## Resolution  **REQUIRED**

What action restored service. Reference:
- **Rollback scenario** (if applicable): [rollback-runbook §6.1 / §6.2 / §6.3](./rollback-runbook.md#6-three-rollback-scenarios)
- **Hotfix commit / PR**
- **Manual intervention**: exact commands run (for reproducibility audit)

## What went well

Concrete, list 2–3 items. Examples:
- Alert fired within 90 s of error-rate breach (well below SLO of 5 min)
- Rollback procedure §6.1 worked exactly as documented; no improvisation needed
- On-call rotation responded within 10 min

## What went poorly

Concrete, list 2–3 items. **No blame** — describe the system gap, not the person.
- (Bad) "Alice deployed the bad migration without reviewing."
- (Good) "Migration safety gate (#1087) does not yet block forbidden patterns; the broken migration was allowed through review."

## Action items  **REQUIRED**

Each action item has all 5 fields. Items without owner+date are not actionable and must be reworked before review.

| # | Owner | Due date (absolute) | Priority | Status | Action | Tracking issue |
|---|---|---|---|---|---|---|
| 1 | @handle | 2026-MM-DD | P1 / P2 / P3 | open / in-progress / done | Concrete deliverable (verb + outcome) | #issueN |
| 2 | @handle | 2026-MM-DD | P2 | open | … | #issueN |

**Priority mapping**:
- **P1** action items (block next deploy, < 1 week)
- **P2** action items (sprint scope, < 1 month)
- **P3** action items (backlog, no hard deadline)

## Lessons learned

What general principle does this incident reinforce? What should the team do **differently** in similar future situations? 2–4 sentences. Not "we should be more careful" — actionable patterns or constraints. Example:
> Migrations that drop columns must use the expand → migrate → contract pattern (rollback-runbook §8.3) without exception. The "we'll be fast enough" shortcut taken here caused 47 min of read errors on the previous app version. New rule: PR template checkbox required for any DropColumn migration.

## Cross-links

- Parent incident issue: #N
- Related runbook section: [rollback-runbook §X](./rollback-runbook.md)
- Related PR(s): #N (root cause), #N (fix)
- Dashboard snapshots (if any): `claudedocs/post-mortems/INC-YYYY-NNN-*.png`
