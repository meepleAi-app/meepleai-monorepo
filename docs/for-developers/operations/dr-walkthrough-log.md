# DR Walkthrough Log

> Cadence: every 90 days. See [`rollback-runbook.md` §13](./rollback-runbook.md#13-quarterly-dr-drill-cadence).
> Automated reminder: `infra/scripts/dr-walkthrough-reminder.sh` (cron `0 9 1 1,4,7,10 *`).

Each entry records one quarterly drill of the rollback runbook. Newest entry on top.

## Log

| Date | Operator | Scenario | RTO actual | Drift discovered | Follow-up issues | Commit / PR |
|---|---|---|---|---|---|---|
| _(no entries yet — first drill due Q3 2026)_ | | | | | | |

## Entry template

```markdown
| 2026-MM-DD | <name> | A | <X min> | <description or "none"> | #..., #... | <SHA or PR#> |
```

Scenarios reference [`rollback-runbook.md` §6](./rollback-runbook.md#6-three-rollback-scenarios):
- **A** — bad image (no schema change)
- **B** — bad migration (schema change applied)
- **C** — slow leak / performance regression

## Drill rotation

| Quarter | Default scenario | Notes |
|---|---|---|
| Q1 | A (bad image) | Fastest drill, builds operator muscle memory |
| Q2 | B (bad migration) | Verifies DB restore path + backup integrity |
| Q3 | A (bad image) | Most-likely real-world case |
| Q4 | C (slow leak) | Performance baselining + Grafana flow |
