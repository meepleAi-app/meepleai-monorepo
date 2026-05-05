# DR Walkthrough Log

> Quarterly review log for `docs/operations/disaster-recovery-runbook.md`.
> Triggered by `infra/scripts/dr-walkthrough-reminder.sh` (cron: 1st jan/apr/jul/oct).

## How to log a walkthrough

After receiving a quarterly reminder:

1. Open `docs/operations/disaster-recovery-runbook.md`.
2. Read end-to-end. For each step, verify:
   - Commands still work as written (no deprecated flags, no removed scripts).
   - Service names, paths, env vars match current `infra/docker-compose.yml`.
   - Required secrets are listed in `infra/secrets/*.secret.example`.
3. Note any **drift** (runbook says X, reality is Y).
4. Add a row to the table below (most recent first).
5. If drift was found, open a separate PR to fix the runbook.

## Walkthrough Entries (most recent first)

| Date | Drift Found | Status | Notes |
|------|-------------|--------|-------|
