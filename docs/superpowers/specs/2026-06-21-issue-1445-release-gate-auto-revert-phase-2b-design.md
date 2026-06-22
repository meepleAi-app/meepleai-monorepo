# Design — Issue #1445 Release-gate Phase 2b Auto-revert

**Issue**: [#1445 — Release-gate auto-revert (Phase 2b)](https://github.com/meepleAi-app/meepleai-monorepo/issues/1445)
**Parent epic**: #1016 Release-gate Phase 2 overview (CLOSED 2026-05-22)
**Predecessor**: #1444 / PR #1457 Phase 2a Verdict synthesis (MERGED 2026-05-23, ≥14gg gate ✅ soddisfatto al 2026-06-21)
**Sibling**: #843 main-dev auto-revert (CLOSED 2026-05-13, currently SHADOW mode — `vars.AUTO_REVERT_ENABLED` not set)
**Spec maturity (input)**: 8.5/10 post `/sc:spec-panel` 2026-05-23 (Nygard · Wiegers · Cockburn · Fowler · Newman · Crispin)
**Spec maturity (this design)**: 9.5/10 post brainstorm 2026-06-21 (A4/B5/C1d/C2d/C3b decisions locked)
**Earliest dispatch**: 2026-06-22 (dispatch window opens domani)

## Status

PROPOSED — pending writing-plans → implementation.

## Context

Issue #1445 ships an auto-revert bot for the release branch `main-staging`. When a release PR merges and a `blocker`-tier check surfaces post-merge, the bot opens + auto-merges a revert PR within ~16 min (15 min cooldown + ≤60 s decision logic). Blast radius is higher than #843 (`main-dev`) because `main-staging` feeds production.

The input spec (8.5/10) had 12 SMART AC + 5 Gherkin scenarios + 15-row test matrix + 3-phase migration plan + 9 strategic decisions ratified. **5 ambiguity gaps** remained that blocked implementation, resolved in this design.

## Decision log — 5 brainstorm gates closed

| Gate | Area | Decision | Rationale |
|---|---|---|---|
| **B5** | State persistence cross-cron-tick (idempotency) | GitHub Actions `concurrency: group: release-gate-auto-revert, cancel-in-progress: false` + append-only JSONL on dedicated side branch `release-gate-state/auto-revert-events` | Concurrency serializes overlapping ticks for free; JSONL gives durable audit trail that feeds Area A telemetry natively |
| **A4** | Outcome lifecycle (`true_positive_pending` → confirmed/false_positive) | Silent confirmation @ 7gg auto-emits `true_positive_confirmed` event; operator can override via explicit label `revert-outcome:false-positive` (or `:true-positive` to accelerate) on revert PR; reconciler weekly Monday 07 UTC | Zero friction on happy path (true-positive is the common case); false-positive requires conscious action with audit trail; AC-7 ≤2% rolling 30d becomes measurable |
| **C1d** | Fix-forward detection rule | Bot prima cerca label `release-fix-forward`; fallback regex titolo `^(revert\|fix\|hotfix)(\(\S+\))?:`. PR senza nessuno → NON considerato fix-forward | Zero-burden via Conventional Commits convention + explicit escape via label under stress; no false-abort on random PRs |
| **C2d** | False-detection override label | NESSUNA override esplicita per false-detection. Operatore rinomina titolo del PR random se serve | Forces semantically-correct titles; YAGNI per MVP — può essere aggiunto come Phase 2b.1 follow-up se serve |
| **C3b** | Race fix-forward arriving DOPO `pr create` ma PRIMA `pr merge --admin` | Re-check fix-forward subito prima del merge; se trovato → close revert PR con commento, emit outcome bucket distinto `aborted_fix_forward_race` | 1 API call extra ~200ms, elimina race meaningful (~30-60s window), outcome bucket distinto preserva analisi |

## Section 1 — Architettura componenti & file layout

### Nuovi file

```
scripts/release-gate/
├── lib/
│   ├── auto-revert.mjs              # NEW — pure logic (no I/O), pattern P64
│   └── auto-revert-events.mjs       # NEW — pure helpers JSONL: parse + serialize + reconcile
├── run-auto-revert.mjs              # NEW — imperative CLI shell (Octokit + git ops + side-branch push)
├── reconcile-revert-outcomes.mjs    # NEW — cron weekly: legge labels su revert PR, emette outcome events
└── __tests__/
    ├── auto-revert.test.mjs         # NEW — ~46 unit + integration tests
    └── auto-revert-events.test.mjs  # NEW — JSONL parser + reconciler tests

state/
└── auto-revert-events.jsonl         # NEW — append-only event log (su side branch dedicato)

.github/workflows/
├── release-gate-auto-revert.yml          # NEW — cron */5 * * * * + workflow_dispatch (decision tick)
├── release-gate-reconcile-outcomes.yml   # NEW — cron 0 7 * * 1 weekly (reconciler A4)
└── release-gate-revert-metrics-check.yml # NEW — cron 0 9 * * 1 weekly (AC-7 breach alerter, Layer 4)

.github/release-gates.yml                # EXTEND — aggiunge bot.phase2b.{enabled, dry_run_mode}

scripts/release-gate/lib/validate.mjs    # EXTEND — schema validation per phase2b sub-keys
```

### Pattern separation (P64 da #1440)

| Modulo | Responsabilità | I/O? |
|---|---|---|
| `lib/auto-revert.mjs` | Pure decision function: `decideRevertAction(input) → output` | NO |
| `lib/auto-revert-events.mjs` | Pure JSONL: `parseEventLog`, `serializeEvent`, `findActiveRevert`, helpers | NO |
| `run-auto-revert.mjs` | Imperative shell: Octokit calls, side-branch git ops, Slack POST, dispatch a `decideRevertAction()` | YES |
| `reconcile-revert-outcomes.mjs` | Imperative shell weekly: lista revert PR ultimi 30gg, dispatch a `reconcileOutcomes()`, append new events | YES |

### Riuso esistente

| Esistente | Riuso |
|---|---|
| `lib/classify.mjs` (Phase 1) | `loadGates()`, `classifyCheck()` per identificare blocker-tier |
| `lib/parse-bot-comment.mjs` (Phase 2c) | `pickLatestBotComment()` + parse per AC-12 `isNewBlocker(checkName, preMergeComment)` |
| `lib/validate.mjs` (Phase 1+2a) | Estendi schema con `bot.phase2b.{enabled, dry_run_mode}` |
| Side-branch state pattern (Phase 2c digest) | Stesso pattern (git clone shallow → modify → push retry su non-FF) ma su branch separato |
| Octokit mock harness (`__tests__/integration.test.mjs`) | Stessa convenzione di mocking per nuovi test |

### Workflow YAML invariants

```yaml
# .github/workflows/release-gate-auto-revert.yml
name: Release-gate Auto-Revert (Phase 2b)
on:
  schedule:
    - cron: "*/5 * * * *"
  workflow_dispatch:
    inputs:
      dry_run_override:
        description: "Force dry-run even if phase2b.dry_run_mode=false"
        type: boolean
        default: false

concurrency:
  group: release-gate-auto-revert        # B5
  cancel-in-progress: false

permissions:
  contents: write          # AC-9
  pull-requests: write     # AC-9
  checks: read             # AC-9
  actions: read            # AC-9
  # NO actions:write — AC-9 prevents self-modification
  # NO id-token:write — no OIDC needed
```

### File NON modificati

`comment.mjs`, `build-digest.mjs`, `.github/release-gates.yml` checks list.

## Section 2 — Event schema JSONL + state storage

### Side branch dedicato

| Aspect | Valore |
|---|---|
| Branch name | `release-gate-state/auto-revert-events` |
| File path | `state/auto-revert-events.jsonl` |
| Format | JSONL (one JSON object per line) |
| Append strategy | Read full → append → push FF-only |
| Retention | Mai truncated dal bot (~2KB/anno stimato) |

Branch separato da Phase 2c digest per evitare retry-loop incrociati su `git push` (Phase 2c è weekly, Phase 2b è ~10 commit/anno).

### Event schema v1

```typescript
type EventBase = {
  schemaVersion: 1
  eventId: string              // deterministic da (eventType, mergeSha, timestamp_ms)
  eventType: 'revert_opened' | 'revert_aborted' | 'revert_aborted_at_merge' | 'outcome_updated'
  timestamp: string            // ISO 8601 UTC
  runUrl: string
  mode: 'live' | 'dry_run'
}

type RevertOpenedEvent = EventBase & {
  eventType: 'revert_opened'
  originalPr: number
  revertPr: number
  mergeSha: string
  blockerCheck: { name: string; conclusion: string; checkRunUrl: string; classifiedAt: string }
  decisionRationale: {
    cooldownElapsedMs: number
    fixForwardCheck: 'none' | 'detected_pre_create_aborted' | 'race_detected_at_merge'
    shaPinned: string
    isNewBlocker: boolean
    cascadeCheck: 'pass' | 'rejected_double_revert'
  }
  outcome: 'true_positive_pending'
}

type RevertAbortedEvent = EventBase & {
  eventType: 'revert_aborted'
  originalPr: number
  mergeSha: string
  blockerCheck: { name: string; conclusion: string }
  abortReason: 'cooldown_not_elapsed' | 'sha_moved_staleness' | 'kill_switch_active'
             | 'cascade_prevented' | 'aborted_fix_forward' | 'skipped_pre_existing'
  outcome: string   // == abortReason
}

type RevertAbortedAtMergeEvent = EventBase & {
  eventType: 'revert_aborted_at_merge'
  originalPr: number
  revertPr: number
  mergeSha: string
  raceWindowMs: number
  detectedFixForward: { number: number; titleOrLabel: 'label' | 'title_prefix' }
  outcome: 'aborted_fix_forward_race'
}

type OutcomeUpdatedEvent = EventBase & {
  eventType: 'outcome_updated'
  originalPr: number
  revertPr: number
  mergeSha: string
  previousOutcome: 'true_positive_pending'
  newOutcome: 'true_positive_confirmed' | 'false_positive'
  trigger: 'label_explicit' | 'silent_confirmation_7d_elapsed'
  rationale: string | null
}
```

### Idempotency contract

`findActiveRevert(events, mergeSha, checkName) → Event | null` — pure function:

1. Filter events by `mergeSha + blockerCheck.name == checkName`
2. Sort by timestamp ASC
3. Walk: `revert_opened` → state=`active_revert`; `revert_aborted_at_merge` for same revertPr → state=`closed` (release lock); `revert_aborted` → state=`aborted` (terminal)
4. Return latest `revert_opened` if state=`active_revert`, else `null`

### Git push retry pattern (riusa Phase 2c con estensione)

```bash
for attempt in 1 2 3; do
  git fetch origin release-gate-state/auto-revert-events
  git reset --hard origin/release-gate-state/auto-revert-events
  # re-read JSONL, re-check findActiveRevert on fresh state, append if still needed
  git push origin release-gate-state/auto-revert-events && break
  sleep $((attempt * 5))
done
```

Retry rare grazie a concurrency group; serve solo per race con `reconcile-revert-outcomes.mjs` weekly che condivide lo stesso branch.

## Section 3 — Decision flow & control logic

### Pure decision function

```typescript
type DecisionInput = {
  killSwitchEnabled: boolean
  dryRunMode: boolean
  latestMergedRelease: { prNumber: number; mergeSha: string; mergeTime: Date; isAutoRevertPr: boolean } | null
  currentHeadSha: string
  cooldownMs: number              // 900_000 (15 min)
  now: Date
  blockers: Array<{ name: string; conclusion: string; checkRunUrl: string }>
  preMergeBotComment: ParsedBotComment | null
  fixForwards: Array<{ number: number; matchedVia: 'label' | 'title_prefix'; createdAt: Date }>
  jsonlEvents: Event[]
}

type DecisionOutput =
  | { action: 'open_revert'; mergeSha: string; blockerCheck: BlockerInfo; rationale: Rationale }
  | { action: 'abort'; reason: AbortReason; rationale: Rationale }
  | { action: 'noop_idempotent'; existingRevertPr: number }
  | { action: 'noop_no_blockers' }
  | { action: 'noop_no_recent_merge' }
```

### Tick decision flow (cron */5 minutes)

```
[1]  Kill switch check (AC-4)          → if disabled: log + EXIT (zero API calls)
[2]  Find latest merged release PR     → if none: noop_no_recent_merge
[3]  Cascade prevention (AC-8)         → if PR is itself a revert: abort cascade_prevented
[4]  Cooldown check (AC-1)             → if < 15min: abort cooldown_not_elapsed
[5]  SHA pin check (AC-2)              → if HEAD moved: abort sha_moved_staleness
[6]  Fetch + classify blockers         → if none: noop_no_blockers
[7]  AC-12 filter NEW blockers only    → if all pre-existing: abort skipped_pre_existing
[8]  B5 idempotency check              → if active revert exists: noop_idempotent (no event)
[9]  Fix-forward pre-create (C1d)      → if found: abort aborted_fix_forward
[10] DECISION: open_revert
[11] git revert -m 1 → push revert/auto-{sha[:8]}
[12] gh pr create (DRAFT if dryRunMode, label auto-revert,phase2b)
[13] Append `revert_opened` event to JSONL (B5 retry)
[14] C3b RE-CHECK fix-forward          → if found: close revert PR + emit aborted_at_merge
[15] If dryRunMode: SKIP merge, leave DRAFT for operator review + EXIT
[16] gh pr merge --admin --squash --delete-branch
[17] Slack POST (soft-fail per spec AC-8 originale)
```

### Step ordering invariants

| Invariant | Enforced by |
|---|---|
| Kill switch = primo check (zero API calls when off) | [1] before everything |
| SHA pin re-checked due volte (decision + merge time) | [5] + implicit in [16] |
| Idempotency PRIMA del fix-forward check (cost saving) | [8] before [9] |

### Cooldown clock injection

```typescript
function cooldownElapsed(mergeTime: Date, now: Date, cooldownMs: number): boolean {
  return now.getTime() - mergeTime.getTime() >= cooldownMs
}
```

Env `AUTO_REVERT_CLOCK_SOURCE=test` + `AUTO_REVERT_TEST_NOW=<ISO 8601>` per test deterministico boundary.

### Dry-run mode behavioral diff (AC-5)

| Step | Live mode | Dry-run mode |
|---|---|---|
| [12] `gh pr create` | label `auto-revert,phase2b`, draft=false | label `auto-revert,phase2b,dry-run`, title prefix `[DRY-RUN] `, draft=true |
| [13] JSONL `mode` | `"live"` | `"dry_run"` |
| [16] `gh pr merge --admin` | EXECUTED | **SKIPPED** (DRAFT resta open per review) |
| [17] Slack | template normale | template `[DRY-RUN] :test_tube:` prefix |

Exit dry-run → operatore manual PR che flippa `bot.phase2b.dry_run_mode: false`.

### Outcome bucket mapping

| Outcome | Origine event | AC mapping |
|---|---|---|
| `true_positive_pending` | `revert_opened` initial state | Transient — matura via `outcome_updated` event (A4: silent @7gg o label esplicita) |
| `true_positive_confirmed` | `outcome_updated` via label/silent 7gg | AC-7 numerator OK |
| `false_positive` | `outcome_updated` via label esplicita | AC-7 numerator BAD (threshold ≤2%) |
| `aborted_fix_forward` | `revert_aborted{reason: aborted_fix_forward}` | C1d telemetry |
| `aborted_fix_forward_race` | `revert_aborted_at_merge` | C3b telemetry |
| `skipped_pre_existing` | `revert_aborted{reason: skipped_pre_existing}` | AC-12 telemetry |
| `cooldown_not_elapsed` | `revert_aborted{reason: cooldown_not_elapsed}` | Debug |
| `sha_moved_staleness` | `revert_aborted{reason: sha_moved_staleness}` | AC-2 |
| `cascade_prevented` | `revert_aborted{reason: cascade_prevented}` | AC-8 |
| `kill_switch_active` | NO event emitted | Operational silence per AC-4 |

## Section 4 — Outcome lifecycle + reconciler weekly

### Workflow separato

```yaml
# .github/workflows/release-gate-reconcile-outcomes.yml
name: Release-gate Reconcile Outcomes (Phase 2b)
on:
  schedule:
    - cron: "0 7 * * 1"        # Lunedì 07:00 UTC (prima del digest 08 UTC)
  workflow_dispatch:

concurrency:
  group: release-gate-auto-revert   # STESSO group del decision workflow
  cancel-in-progress: false

permissions:
  contents: write
  pull-requests: read
  issues: read
```

### Labels convention

| Label | Significato | Aggiunto da |
|---|---|---|
| `auto-revert` | Marker meccanico | bot (step [12]) |
| `phase2b` | Distingue da #843 main-dev | bot (step [12]) |
| `dry-run` | Marker DRAFT PR dry-run mode | bot solo in dry_run_mode |
| `revert-outcome:false-positive` | Operatore: "revert sbagliato" | operatore manualmente |
| `revert-outcome:true-positive` | Operatore: "revert corretto" (accelera silent 7gg) | operatore manualmente |

### Reconciler pure function

```typescript
function reconcileOutcomes(
  revertPRs: PRState[],
  events: Event[],
  now: Date,
): OutcomeUpdatedEvent[] {
  // 1. Filter: state=merged + labels auto-revert+phase2b + NOT dry-run
  // 2. For each eligible PR:
  //    - Find revert_opened event (skip if missing — drift tolerance)
  //    - Skip if outcome_updated already exists (idempotent)
  //    - Priority: label revert-outcome:false-positive (immediate emit)
  //    - Priority: label revert-outcome:true-positive (immediate emit)
  //    - Else: if mergedAt + 7gg elapsed → emit silent_confirmation
  //    - Else: pending (no event this run)
  // 3. Return new events array (to be appended)
}
```

### Edge cases

| Edge case | Resolution |
|---|---|
| Label added then removed | Already emitted = terminal; manual issue per correzione |
| Revert-of-revert mergiato dopo 3gg | Reconciler valuta solo PR labels, no chain logic — operatore può marcare manualmente |
| Revert PR re-opened via GH UI | State no più `merged` → escluso da reconciliation |
| Reconciler concurrent con auto-revert tick | Concurrency group condiviso serializza gratis |
| Reconciler fail mid-run (push reject) | Retry 5s/10s; se 3 fall consecutivi: workflow fail, next Monday re-evaluates idempotently |
| Operatore aggiunge label SUBITO post-merge (<7gg) | Label esplicita prende priorità su silent — no race |

### AC-5 dry-run exit criterion

```bash
node scripts/release-gate/reconcile-revert-outcomes.mjs --report-only
```

Output:

```
=== Phase 2b Dry-Run Maturity Report ===
Period: 2026-06-23 → 2026-07-07 (14d)

Counts (mode=dry_run only):
  revert_opened:                 7
  true_positive_confirmed:       2  (1 via label, 1 silent @7gg)
  false_positive:                0  ← if > 0 BLOCKS exit
  pending (< 7gg):               4
  aborted (any reason):          1

Exit gate (AC-5):
  ✓ 0 false-reverts                              (need: 0)
  ✓ 1+ true-positive validated                   (need: ≥1)

DECISION: ready to flip phase2b.dry_run_mode=false
```

### AC-7 false-revert rate aggregation

```
false_revert_rate_30d =
  count(outcome_updated, newOutcome=false_positive, last 30d, mode=live) /
  count(revert_opened, last 30d, mode=live)
```

Sotto-threshold 2%. Lo stesso `reconcile-revert-outcomes.mjs --metrics-only` emette JSON one-line per consumption esterno.

## Section 5 — Telemetria & observability

### Stack metric (pragmatic — no Prometheus build)

Decisione: NON costruire Prometheus exporter. Riusiamo JSONL come source of truth + CLI per derivazioni on-demand + auto-issue per breach AC-7.

### Layer 1 — Structured logs JSON (AC-10)

```typescript
type StructuredLogLine = {
  level: 'info' | 'warn' | 'error'
  ts: string
  event_type:
    | 'tick_start' | 'tick_end' | 'decision_made'
    | 'pr_created' | 'pr_merged' | 'pr_closed_race'
    | 'slack_sent' | 'slack_failed'
    | 'jsonl_appended' | 'jsonl_push_retry'
  workflow_run_id: string
  pr_number: number | null
  decision: 'open_revert' | 'abort' | 'noop_idempotent' | 'noop_no_blockers' | null
  outcome: string | null
  abort_reason: AbortReason | null
  head_sha: string | null
  merge_sha: string | null
  latency_ms: number
}
```

Inspection: `gh run view <run_id> --log | grep '"event_type":"decision_made"' | jq`.

### Layer 2 — JSONL append-only (B5)

Source of truth durabile per audit AC-6 + aggregation AC-7. ~2KB/anno.

### Layer 3 — CLI on-demand

| Mode | Flag | Output | Use case |
|---|---|---|---|
| Reconcile + write events | (default) | Emette `outcome_updated` events nel JSONL | Pipeline weekly |
| Report-only | `--report-only` | Print Phase 2b maturity report | Operatore pre flip dry_run_mode=false |
| Metrics-only | `--metrics-only` | Print AC-7 rate + raw counts (JSON one-line) | Auto-alert workflow Layer 4 |

`--metrics-only` example output:

```json
{"ts":"2026-06-23T08:00:00Z","window_days":30,"mode":"live","total_reverts":12,"true_positive_confirmed":10,"false_positive":1,"pending":1,"false_revert_rate":0.083,"threshold":0.02,"breach":true}
```

### Layer 4 — Auto-issue su AC-7 breach

```yaml
# .github/workflows/release-gate-revert-metrics-check.yml
on:
  schedule:
    - cron: "0 9 * * 1"      # Lunedì 09:00 UTC (post-reconcile 07 + post-digest 08)
  workflow_dispatch:

permissions:
  contents: read
  issues: write

jobs:
  check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - id: metrics
        run: |
          OUTPUT=$(node scripts/release-gate/reconcile-revert-outcomes.mjs --metrics-only)
          echo "metrics=$OUTPUT" >> "$GITHUB_OUTPUT"
      - if: fromJSON(steps.metrics.outputs.metrics).breach == true
        run: |
          gh issue create \
            --title "🚨 Phase 2b false-revert breach ${{ fromJSON(steps.metrics.outputs.metrics).false_revert_rate }}" \
            --label "release-gate,phase2b,breach,blocker" \
            --body "..."
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

Threshold:
- `false_revert_rate > 0.02` over 30d → P0 issue (AC-7 breach)
- Escalation: `> 0.05` over 7d → emergency comment with explicit kill switch suggestion

### Layer 5 — Slack notification template (AC-11 pinned)

**Live:**

```
:warning: Auto-revert fired — PR #{N} reverted via #{M}
Original blocker: `{check_name}` ({conclusion})
Rationale: 15-min cooldown elapsed · no fix-forward · SHA pinned · new (not pre-existing)
Audit: {run_url}
```

**Dry-run:**

```
[DRY-RUN] :test_tube: Auto-revert would fire — PR #{N} would be reverted (DRAFT #{M})
Original blocker: `{check_name}` ({conclusion})
Audit: {run_url}
Operator review: {revert_pr_url}
```

**Race abort (C3b):**

```
:no_entry: Auto-revert aborted at merge — PR #{N} revert (#{M}) closed
Reason: fix-forward race detected at T+{race_window_ms}ms
Fix-forward PR: #{fix_forward_pr}
Audit: {run_url}
```

Soft-fail: Slack POST failure → log `slack_failed`, workflow continua.

### Observable surfaces (where operator looks)

| Surface | Contenuto | Query |
|---|---|---|
| Slack `#meepleai-release` | Real-time + DRY-RUN markers + race aborts | Visual |
| GH Issues label `release-gate,phase2b,breach` | AC-7 breach alerts | `gh issue list --label release-gate,phase2b,breach` |
| Branch `release-gate-state/auto-revert-events` | Full audit JSONL | `git show <ref>:state/auto-revert-events.jsonl \| jq -s ...` |
| Revert PR labels | Outcome tagging | `gh pr list --label auto-revert,phase2b` |
| GH Actions logs | Structured logs JSON | `gh run list --workflow=release-gate-auto-revert.yml` |
| CLI on-demand | Maturity report + AC-7 rate | `node scripts/release-gate/reconcile-revert-outcomes.mjs --report-only` |

## Section 6 — Testing strategy

### Test matrix esteso (~46 test)

#### Unit — `__tests__/auto-revert.test.mjs` (pure `decideRevertAction`)

| # | Scenario | AC |
|---|---|---|
| 1 | Kill switch enabled=false → action='abort', reason='kill_switch_active', no event emitted | AC-4 |
| 2 | Cooldown not elapsed (10min) → abort cooldown_not_elapsed | AC-1 |
| 3 | Cooldown exactly at boundary (15:00) → proceeds | AC-1 |
| 4 | Cooldown elapsed (16min) → proceeds | AC-1 |
| 5 | SHA moved during cooldown → abort sha_moved_staleness | AC-2 |
| 6 | Latest release PR is itself a revert (two-key) → abort cascade_prevented | AC-8 |
| 7 | Title `revert: ` only (no body match) → NOT cascade | AC-8 |
| 8 | All blockers pre-existing → abort skipped_pre_existing | AC-12 |
| 9 | Mixed pre-existing + new → proceeds with new | AC-12 |
| 10 | Fix-forward label `release-fix-forward` → abort aborted_fix_forward | C1d |
| 11 | Fix-forward title `^fix: ...` → abort | C1d |
| 12 | Fix-forward title `^chore: doc` → NOT fix-forward | C1d |
| 13 | Happy path → action='open_revert' with rationale populated | AC-6 |
| 14 | Dry-run mode → action='open_revert' with draft=true, title `[DRY-RUN]` | AC-5 |
| 15 | Multiple new blockers → action='open_revert' with first + all in audit array | AC-6 |

#### Unit — `__tests__/auto-revert-events.test.mjs` (pure JSONL helpers)

16. `parseEventLog('')` → `[]`
17. `parseEventLog(validJSONL)` → typed Event[]
18. `parseEventLog(mixedJSONL)` skips malformed + warns
19. `serializeEvent → parseEventLog` round-trip preserves fields
20. `findActiveRevert([], ...)` → `null`
21. `findActiveRevert([opened], ...)` → opened
22. `findActiveRevert([opened, aborted_at_merge], ...)` → `null` (lock released)
23. `findActiveRevert([opened, outcome_updated], ...)` → opened (terminal doesn't unlock)
24. `findActiveRevert` isola per `(mergeSha, checkName)`
25. `eventId` deterministic da `(eventType, mergeSha, ts)`

#### Unit — `__tests__/reconcile-outcomes.test.mjs` (pure `reconcileOutcomes`)

26. PR mergiata 6gg, no label → no event
27. PR mergiata 7gg + 1s, no label → `outcome_updated{silent_confirmation_7d_elapsed}`
28. Label `revert-outcome:false-positive` (5gg) → emit immediately false_positive
29. Label `revert-outcome:true-positive` (5gg) → emit immediately true_positive_confirmed
30. BOTH labels → priorità false-positive + warning log
31. Already-finalized outcome_updated → no re-emit (idempotency)
32. PR state=closed (not merged) → no event
33. PR label `dry-run` → excluded

#### Integration — `__tests__/integration-auto-revert.test.mjs` (Octokit mock)

34. Happy path E2E live mode
35. C3b race: revert opens → fix-forward arrives → re-check closes revert PR
36. Dry-run mode: DRAFT PR + no merge + Slack `[DRY-RUN]`
37. Audit trail: revert PR body contains all 5 AC-6 fields (snapshot)
38. Slack 503 → soft-fail, revert mergiato comunque

#### Integration — `__tests__/integration-reconcile.test.mjs`

39. Weekly run with 4 PRs (mixed labels + ages) → 3 events (1 pending excluded)
40. Re-run same week → no duplicate events (idempotency)

#### Snapshot tests

41. Slack message template (live + dry-run + C3b-race-abort = 3 snapshots)
42. Revert PR body template (live + dry-run = 2 snapshots)
43. Each event type → JSONL string (revert_opened + 6 abort_reason variants + revert_aborted_at_merge + 4 outcome_updated combos)

#### Schema validation — extends `__tests__/validate.test.mjs`

44. `.github/release-gates.yml` MUST have `bot.phase2b.enabled` boolean
45. `.github/release-gates.yml` MUST have `bot.phase2b.dry_run_mode` boolean
46. Missing either key → CI fail

### Test runner & convention

- **Framework**: Vitest (riusa `scripts/release-gate/vitest.config.mjs`)
- **Mocking**: pattern Octokit factory mock (Phase 2c)
- **Clock injection**: `AUTO_REVERT_CLOCK_SOURCE=test` + `AUTO_REVERT_TEST_NOW=<ISO 8601>`
- **Side branch mock**: in-memory `JSONLStore` test double; mai tocca real git
- **CI**: runs in existing release-gate workflow + nuovo step validate.mjs (già covered)

## Section 7 — Migration / rollout plan

### Phase A — Schema-only landing (Day 0)

| Step | Cosa | Verification |
|---|---|---|
| A.1 | PR mergiata con tutti i file + 46 test | `pnpm test` 46/46 |
| A.2 | `.github/release-gates.yml` con `bot.phase2b.enabled: false` + `dry_run_mode: true` | Schema validator OK |
| A.3 | Side branch `release-gate-state/auto-revert-events` creato vuoto | `git ls-remote` returns SHA |
| A.4 | Cron schedule attivo MA `enabled=false` → exit at step [1] | Verifica workflow runs 5min con kill_switch_active log |

**Duration**: 1-2 giorni post-merge per shake-out cron schedule.

### Phase B — Dry-run period (Day 3-17, ≥14gg)

| Step | Cosa | Verification |
|---|---|---|
| B.1 | Operatore flippa `bot.phase2b.enabled: true` (mantiene `dry_run_mode: true`) | PR mergiato → cron entra nel flow |
| B.2 | Daily monitoring via `--report-only` | Verifica counts dry_run events |
| B.3 | Naturally wait for blocker (no forzare regressioni) | Quando emerge: DRAFT revert PR + Slack `[DRY-RUN]` |
| B.4 | Operatore review DRAFT PR + verdict label | Reconciler weekly emette `outcome_updated` |
| B.5 | Exit: 14gg + `--report-only` "DECISION: ready to flip" | Manual flip Phase C |

**Reality check (#843 sibling): 39gg in shadow mode senza promotion** → Phase B può estendersi indefinitamente. Non c'è urgenza di flip Phase C finché AC-5 exit criterion non soddisfatto.

### Phase C — Live mode (earliest Day 17+)

| Step | Cosa | Verification |
|---|---|---|
| C.1 | Operatore flippa `bot.phase2b.dry_run_mode: false` | Cron entra in live mode |
| C.2 | 24h on-call: monitor Slack + GH Actions logs real-time | Manual verification ogni revert |
| C.3 | Day 17-23: operatore review ogni revert ENTRO 1h | Slack thread con verdict label entro 1h |
| C.4 | Day 24+: normal operation + weekly auto-issue su AC-7 breach | Steady state |

### Rollback path (4 livelli)

| Level | Trigger | Action | Recovery |
|---|---|---|---|
| L1 | Specifico revert sbagliato | Operatore re-merge dell'original PR | Minuti |
| L2 | AC-7 breach | Single-line PR: `bot.phase2b.enabled: false` | <10 min |
| L3 | Live mode problematico | Single-line PR: `bot.phase2b.dry_run_mode: true` | <10 min |
| L4 | Bug fondamentale | Revert PR Phase A iniziale | ~1h |

### Coordination con #843 sibling

| Consideration | Mitigation |
|---|---|
| #843 promosso a live mentre Phase 2b dry-run → cascade risk | AC-8 cascade prevention (two-key) catches via test #6 + #7 |
| Convention label (`auto-revert` shared, `phase2b` distinguisher) | Documentato nella label table |
| Branch naming: Phase 2b usa `revert/auto-{sha[:8]}` | Nessun conflict (SHA-based) |

### Documentation deliverables

| File | Contenuto |
|---|---|
| `docs/for-developers/operations/release-gate-bot.md` (EDIT) | Phase 2b operator runbook |
| `scripts/release-gate/README.md` (EDIT) | "Phase 2b auto-revert" section |
| CLAUDE.md (EDIT post-merge) | 1-line entry "Known Pitfalls" se merita |

### Timeline

```
Day 0  → Phase A merge (schema-only, kill-switch ON)
Day 3  → Phase B start (operatore: enabled=true, dry_run_mode=true)
       │ ~14gg+ monitoring (può estendersi se exit criterion non soddisfatto)
Day 17+→ Phase C start (dry_run_mode=false, --report-only verde)
       │ 24h on-call + daily review × 7gg
Day 24+→ Steady-state + weekly auto-issue
```

## Out of scope

- Auto-revert on `warning`-tier checks
- Reverting commits older than `main-staging` HEAD parent
- Email/SMS notification (Slack only)
- Modifying branch protection (separate runbook task)
- Auto-promotion dry-run → live (manual flip required)
- Per-aggregate ordering of revert decisions
- Prometheus exporter (decisione confermata: JSONL + CLI sufficient per scale attesa)
- `skip-fix-forward` override label (C2d: out of MVP, può essere Phase 2b.1 follow-up)
- `force-revert` override label sull'original PR (out of MVP)

## References

- **Issue**: [#1445](https://github.com/meepleAi-app/meepleai-monorepo/issues/1445)
- **Parent epic**: #1016 Release-gate Phase 2 overview (CLOSED 2026-05-22)
- **Predecessor**: #1444 / PR #1457 Phase 2a (MERGED 2026-05-23)
- **Sibling**: #843 main-dev auto-revert (CLOSED 2026-05-13, SHADOW mode)
- **ADR-055**: Auto-revert bot identity & push mechanism (`docs/for-claude/architecture/adr/adr-055-auto-revert-bot-identity.md`)
- **Spec input**: `gh issue view 1445 --json body`
- **Phase 2c digest pipeline** (reference for state-branch pattern): `scripts/release-gate/build-digest.mjs` + `lib/digest-builder.mjs`
- **GitHub Checks API**: https://docs.github.com/en/rest/checks/runs

## Estimated effort

| Phase | Effort |
|---|---|
| Spec finalization + 5 brainstorm decisions ratified | 0d (DONE — questo doc) |
| Pure `lib/auto-revert.mjs` + `lib/auto-revert-events.mjs` + 25 unit tests | 1.5-2d |
| Imperative `run-auto-revert.mjs` + `reconcile-revert-outcomes.mjs` + 7 integration tests | 1.5-2d |
| 3 workflow YAML + permissions audit | 0.5d |
| Validator schema extension (`phase2b.*` keys) + 3 schema tests | 0.5d |
| Side branch bootstrap + dry-run period setup | 0.5d |
| Code review + iteration | 1d |
| **Dry-run period (workflow live, no merge)** | ≥14d |
| Live mode flip + 24h on-call | 1d |
| Telemetry observation post-live | 14d |

**Earliest live**: 2026-07-13 (dispatch 2026-06-22 + 7d impl + 14d dry-run + flip).

## Spec maturity progression

| Iteration | Score | Notes |
|---|---|---|
| Initial (stub from #1016 overview) | 6.0/10 | 7 AC, 2 scenarios, no test matrix, 2 open questions |
| `/sc:spec-panel` 2026-05-23 (Nygard et al.) | 8.5/10 | 12 SMART AC, 5 Gherkin, 15-row test matrix, 9 strategic decisions, 2 questions RESOLVED, 3-phase migration |
| **Brainstorm 2026-06-21 (questo doc)** | **9.5/10** | 5 ambiguity gaps closed (B5/A4/C1d/C2d/C3b), 46-test matrix, 4-level rollback, telemetry stack pragmatic, edge-case table reconciler, structured log schema |
