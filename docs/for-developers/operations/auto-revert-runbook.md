# Auto-Revert Bot Runbook — `main-dev`

> **Last verified**: 2026-05-13
> **Audience**: Single-operator on-call during beta phase
> **Maintenance owner**: @badsworm (founder)
> **Scope**: `.github/workflows/dev-auto-revert.yml` — automated revert of `main-dev` HEAD when `Dev Async` is red for ≥ 30min
> **Phase**: 1 of 3 (SHADOW — classifier only, no writes). See [issue #843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843) for the full rollout plan.
> **Sibling docs**: [rollback-runbook.md](./rollback-runbook.md) · [devops-policy.md](./devops-policy.md) · [ADR-054](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md) · [ADR-055](../../for-claude/architecture/adr/adr-055-auto-revert-bot-identity.md)

---

## TL;DR — what this bot does (and doesn't do, yet)

**Today (Phase 1 / SHADOW)**
- Polls `Dev Async` runs on `main-dev` every 15 minutes
- Classifies each completed run as one of: `green`, `green-equivalent`, `green-pending`, `infra-flake`, `operator-override`, `configuration-error`, `real-failure`, `unknown`
- If `real-failure` AND the run has been red ≥ 30 min, emits a Slack alert: **"SHADOW: would revert HEAD …"**
- Writes the verdict + reasoning to the workflow run's step summary every time it runs
- **Does NOT** revert anything, push anything, open any PR

**Tomorrow (Phase 2)**
- Adds the revert PR creation + auto-merge path
- Adds preventive notifications at T-10 / T-20 minutes
- Adds the `no-auto-revert` PR-label bypass
- Adds the revert-of-revert kill switch

This runbook is **Phase 1 only**. The Phase 2 sections will be appended when that PR lands.

---

## 1. How the bot decides

Decision matrix (full table in [issue #843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843)):

| Latest `Dev Async` `conclusion` | Job annotations contain | Verdict | Action |
|---|---|---|---|
| `success` | — | `green` | no-op |
| `failure` | real test/build error | **`real-failure`** | would-revert candidate |
| `failure` | only network / `EAI_AGAIN` / `503 service` | `infra-flake` | alert-only |
| `cancelled` | `exceeded the maximum execution time` | `infra-flake` | alert-only |
| `cancelled` | `concurrency` | `green-pending` | no-op (newer push superseded) |
| `cancelled` | (none of the above) | `operator-override` | no-op |
| `timed_out` | — | `infra-flake` | alert-only |
| `action_required` | — | `configuration-error` | alert-only |
| `skipped` / `neutral` | — | `green-equivalent` | no-op |
| anything else | — | `unknown` | log only |

**The classifier is the single highest-risk component.** False positives cause wrongful reverts (Phase 2); false negatives leave `main-dev` red. Phase 1 exists to tune this mapping against real-world data before the revert path is enabled.

---

## 2. How to disable the bot globally

The bot is read-only in Phase 1 so disablement is mainly relevant for Phase 2 onward. To pre-emptively prevent the Phase 2 revert path from running at all even if the variable gets flipped:

1. Disable the workflow via the UI: **Actions → Dev Auto-Revert → ⋯ → Disable workflow**.
2. Or remove `.github/workflows/dev-auto-revert.yml` in a PR.

In Phase 1 the workflow simply produces step summaries; there is nothing to "disable" beyond stopping the cron.

For Phase 2 onward, the global kill switch is the repository variable:

```
Settings → Variables → Actions → AUTO_REVERT_ENABLED  (set to "false")
```

Default is `"false"`. Setting it back to `"false"` is the one-step kill switch.

---

## 3. How to opt-out a single PR (Phase 2)

> Not yet wired — placeholder.

In Phase 2, add the label `no-auto-revert` to a PR. The bot inspects the PR associated with the offending commit; if the label is present, it skips the revert and posts a comment on the PR explaining the skip.

The label name must match exactly. The bot will not respect any near-name variant.

---

## 4. Manual override — recover from a wrongful revert (Phase 2)

> Not yet wired — placeholder.

In Phase 2, if the bot reverts something it should not have:

1. Identify the revert PR in the GitHub UI (search: `is:pr author:app/github-actions main-dev`)
2. Re-revert (`git revert <revert-sha>`) on a fresh branch
3. Open a PR with title `revert: restore <SHA> — bot wrongful revert`
4. Apply label `no-auto-revert` BEFORE merging
5. Open issue describing the misclassification so the classifier can be tuned

---

## 5. Reading the step summary (Phase 1 only diagnostic)

Every cron run produces a markdown table in the workflow run page (Actions → Dev Auto-Revert → run → Summary):

| field | meaning |
|---|---|
| HEAD | short SHA of `main-dev` HEAD at classifier run time |
| Dev Async run | link to the run that was classified |
| conclusion | raw `Dev Async` conclusion (`success` / `failure` / `cancelled` / …) |
| age | minutes since the `Dev Async` run started |
| verdict | classifier output (`real-failure` / `infra-flake` / …) |
| reason | human-readable explanation of the verdict |
| would revert? | `true` only when `verdict=real-failure` AND `age ≥ threshold` |
| mode | always `SHADOW` in Phase 1 |

Phase 1 review cadence: skim the last 7 days of runs weekly. Look for:

- `unknown` verdicts → annotation pattern not in the catalog → add to classifier
- `infra-flake` ↔ `real-failure` misclassifications → tune the regex in `classify` step
- Frequency of `would_revert=true` → predicts Phase 2 revert rate

---

## 6. Slack notifications

Phase 1 emits **one** Slack notification only:

- **When**: `verdict=real-failure` crosses the 30-minute threshold (the "would-revert" intent log)
- **Channel**: same webhook as all CI notifications (`SLACK_GITNOTIFY_WEBHOOK_URL`), routed to the `automation` environment in `notify-slack.yml`
- **Format**: failed event with `commit_message = "SHADOW: would revert HEAD <short> — Dev Async red for <N>min (verdict=real-failure)"`

A debug knob exists via `workflow_dispatch` with `force_notify: true` — useful to validate the Slack integration without waiting for a real red event.

Preventive T-10 / T-20 notifications and idempotency ledgering are deferred to Phase 2 (they require a writeable persistence layer the read-only Phase 1 bot does not have).

---

## 7. Cost & cadence

| Knob | Phase 1 default | Phase 2 target |
|---|---|---|
| Cron interval | `*/15 * * * *` | `*/5 * * * *` |
| Runs per month | ~2,880 | ~8,640 |
| Estimated minutes/month | ~480 | ~1,500 |
| % of #850 soft cap (3,100 min/month) | ~16 % | ~48 % |

Each Phase 1 run completes in ~10 s on `ubuntu-latest`. If cost pressure surfaces, the cadence can be widened to `*/30` via the workflow file edit; no other config knob is needed.

---

## 8. Phase 2 promotion checklist

To progress from Phase 1 to Phase 2:

- [ ] 7 consecutive days of Phase 1 step-summary review with no `unknown` verdicts left untriaged
- [ ] Classifier regex tuned for any new annotation patterns observed
- [ ] False-positive rate (would-revert that the operator would have NOT reverted) ≤ 5 % over the review window
- [ ] Implement the revert PR creation step, the kill-switch step (AC-3), the label-bypass step (AC-4), and the preventive notifications (T-10 / T-20) with the idempotency ledger (AC-5)
- [ ] Promote permissions from `contents: read` → `contents: write` and `pull-requests: read` → `pull-requests: write`
- [ ] Flip `vars.AUTO_REVERT_ENABLED = 'true'`
- [ ] Promote ADR-055 from `Proposed` → `Accepted`
- [ ] Tighten cron to `*/5`

---

## 9. Escalation

Single-operator project; on-call = @badsworm. If the bot misbehaves outside of business hours:

1. Disable the workflow via UI (procedure §2)
2. Open an issue tagged `area/devops` describing the misclassification
3. Continue normally on Monday

There is no paging tier and no SLA for Phase 1 alerts.

---

## Refs

- Issue [#843](https://github.com/meepleAi-app/meepleai-monorepo/issues/843) — implementation tracking + Phase rollout
- Epic [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) — DevOps Multi-Branch Strategy
- [ADR-054](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md) — Multi-Branch Strategy
- [ADR-055](../../for-claude/architecture/adr/adr-055-auto-revert-bot-identity.md) — Bot identity & push mechanism
- [Cost optimization issue #850](https://github.com/meepleAi-app/meepleai-monorepo/issues/850)
- Workflow file: `.github/workflows/dev-auto-revert.yml`
- Monitored workflow: `.github/workflows/dev-async.yml`
- Slack primitive: `.github/workflows/notify-slack.yml`
