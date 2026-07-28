# Spec-panel #3331 — Co-located CI runner on the staging server

**Date**: 2026-07-28 · **Issue**: [#3331](https://github.com/meepleAi-app/meepleai-monorepo/issues/3331) · **Method**: 4-expert panel (Nygard, Hightower, Fowler, Newman) over a shared technical recon.

## Problem

A self-hosted GitHub Actions runner is **co-located on the Hetzner staging VPS (~8GB ARM64)**, sharing the box with the app containers. On 2026-07-28 the deploy (run `30334115340`) **failed with an OOM**: the pre-deploy `Frontend build check` (`next build` + `tsc`, `NODE_OPTIONS=--max-old-space-size=4096`) ran on this runner and was SIGKILL'd, cascading into a missing migration artifact and a failed deploy job. Worked around with `workflow_dispatch -f skip_tests=true`.

## Recon findings (topology & root cause)

1. **CI no longer uses the self-hosted runner.** `ci.yml`'s `select-runner` hardcodes `ubuntu-latest` (`ci.yml:74`) → the 14,700+ tests already run on cloud. The self-hosted runner today runs only: `deploy-staging` orchestration (detect-changes, pre-deploy-check, snapshot-baseline, deploy-SSH, validate), `rollback`, `e2e`, and ops one-offs.
2. **Heavy compilation is already on cloud**, evicted one incident at a time: backend build (#2650, Roslyn OOM), image build (2026-05-08 swap-thrash), migration SQL gen, build-ai. The frontend OOM is the same pattern.
3. **The OOM is structural.** The runner has a `MemoryMax=3G` cgroup cap (`infra/runner/systemd-overrides/10-memory-limits.conf`, #2019) that exists to protect the app containers. A 4GB Node heap cannot fit inside a 3G cap on a shared 8GB box → guaranteed SIGKILL.
4. **The frontend build check is redundant.** `ci.yml Build Frontend` already ran `pnpm build` on cloud for the PR, and the real web image is built on cloud (`build` job, always `ubuntu-latest`). The pre-deploy local build gates nothing new.
5. **The deploy does not require co-location.** Every server-touching step (snapshot-baseline, apply-migrations, deploy) reaches `STAGING_HOST` over **public SSH** — it would run identically from a cloud runner. The only host-local step is `df /` (rewritable as `ssh $HOST df /`).
6. **Doc drift.** ADR-044 / README describe a dedicated **Oracle Cloud 24GB VM** that was never the live runner; reality is the ~8GB co-located Hetzner box. Machine naming is inconsistent (CAX21/CAX31/CX31).

## Options assessed

| Option | Nygard | Hightower | Fowler | Newman | Verdict |
|---|:---:|:---:|:---:|:---:|---|
| **A** — remove redundant `Frontend build check` from self-hosted | strong-yes | strong-yes | strong-yes | strong-yes | **Do now, €0** |
| **E** — eliminate self-hosted: build on hosted + deploy SSH from cloud | strong-yes | strong-yes | yes | conditional | **Preferred endpoint** (after gate) |
| **D** — dedicated small Hetzner VM (~€5/mo) + ephemeral | fallback | conditional | conditional | yes | Structural fallback |
| **C** — ephemeral runner on the same box | conditional | conditional | no | no | Solves state accretion, not RAM |
| **B** — cron prune of runner cache | no | no | conditional | conditional | Already covered; disk not RAM |
| **F** — lower heap / disable check | strong-no | no | no | no | Fragile symptom-tuning |
| **G** — ARC / Kubernetes | — | — | — | strong-no | Overkill |

**Panel consensus:** the co-location is a **bulkhead violation** (Nygard) / **availability coupling** (Hightower) / **architectural smell being paid off one incident at a time** (Newman). Every relocation that *didn't* break the deploy proves the co-location was never load-bearing. It must not survive into production — there a CI build OOM killing app containers is unacceptable.

## Decision (sequenced)

### 1. NOW — Option A (done in this PR, €0)
Removed the redundant `Frontend build check` (+ `Setup Frontend`) from `deploy-staging.yml`'s `pre-deploy-check`, mirroring the Backend build check removal (#2650). Reconciled the doc drift (ADR-044 Update, README reality note). **This is a tourniquet, not a cure — #3331 stays open.**

### 2. GATE — measure before choosing E vs D
Collect the **actual monthly cloud minutes** for the self-hosted-capable heavy suites (`backend-e2e`, `test-performance`) if run on hosted ARM64 (`ubuntu-24.04-arm`, $0.005–0.008/min). Free tier = 2,000 min/mo. This single number decides the endpoint; do not guess.

### 3. BEFORE PRODUCTION — endpoint
- **Option E (preferred, 3/4 experts):** eliminate the self-hosted runner. Build/test on GitHub-hosted; run the deploy as a hosted job that SSHes to `STAGING_HOST` with a dedicated deploy key locked via `command=` in `authorized_keys` + a non-root deploy user (optional WireGuard/Tailscale hop). Removes both the failure mode **and** the entire pet apparatus (`runner-maintenance.yml`, `runner-health-check.yml`, memory overrides, disk gate, prune crons). Likely €0 at staging cadence. Re-add the `df /` gate as `ssh $HOST df /`.
- **Option D (fallback):** if the heavy suites blow the free tier (est. €10–40/mo on cloud), move the runner to a dedicated CAX11 (~€5/mo) provisioned from the existing `cloud-init.yml`, running `--ephemeral`. Keeps cache + self-hosted; re-aligns with ADR-044's original intent. Just a `vars.RUNNER` re-point (reversible).

Both are two-way doors (re-enable self-hosted by setting `vars.RUNNER`).

## Rejected / deferred
- **B, F**: band-aids that address the wrong axis (disk/heap, not the CI-vs-runtime bulkhead) and give false confidence.
- **C**: correct hygiene (no state accretion) but does not fix RAM contention; only worth it bundled with D.
- **G (ARC/k8s)**: overkill for one-person, single-VM, staging-only ops.

## Risks
- Closing #3331 on Option A alone: the specific OOM disappears but the co-location remains; the next heavy pre-deploy step re-triggers the class. **A is necessary-but-not-sufficient.**
- E's cost is not automatically €0 — `backend-e2e`/`test-performance` on cloud could cost €10–40/mo and forfeit persistent Docker/pnpm cache. Measure first (the gate).
- E secret handling: scope the deploy key (`command=`, non-root user, pinned `known_hosts`).
- Don't delete `cloud-init.yml` — it is the IaC for the D fallback. Supersede ADR-044 by recording the decision, not by removing the recipe.
- Before finalizing, verify GitHub → Settings → Actions → Runners so a stale-registered Oracle runner isn't left in the inventory.
