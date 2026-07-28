# ADR-044: Migrate CI/CD to Self-Hosted ARM64 Runner

**Status**: **Superseded by Option E** (#3331, 2026-07-28) — see [Update 2026-07-28](#update-2026-07-28--drift-correction--direction-3331). The original decision (a self-hosted ARM64 runner) below is historical.
**Date**: 2026-03-09
**Issue**: #2970 (Epic #2967)
**Decision Makers**: Engineering Lead
**Follow-up**: #5563 (Post-migration hardening)

---

## Context

GitHub Actions CI/CD costs were growing with the repository's expanding test suite (14,700+ backend tests, 1,100+ frontend tests, 70+ E2E specs). The free tier (2,000 minutes/month) was insufficient, and paid minutes added recurring cost with no performance benefit.

Oracle Cloud provides a free-tier ARM64 VM (Ampere A1, 4 OCPUs, 24GB RAM, 200GB disk) that can serve as a self-hosted GitHub Actions runner at zero cost.

### Constraints

- ARM64 architecture differs from x86-64 (GitHub-hosted default)
- Some CI tools lack ARM64 support (CodeQL CLI, Snyk Docker action)
- .NET JIT compilation is 30-40% slower on ARM64 vs x86-64
- Single VM = single point of failure (no auto-scaling)

## Decision

Migrate all GitHub Actions workflows to a self-hosted ARM64 runner using a **configuration toggle pattern** with automatic fallback.

### Runner Selection Pattern

```yaml
runs-on: ${{ vars.RUNNER || 'ubuntu-latest' }}
```

- **`vars.RUNNER` set**: All jobs run on self-hosted ARM64 runner
- **`vars.RUNNER` empty/unset**: All jobs fall back to GitHub-hosted `ubuntu-latest`
- **Per-workflow override**: Individual workflows can hardcode `ubuntu-latest` (e.g., CodeQL)

### ARM64-Incompatible Tools (Excluded)

| Tool | Reason | Mitigation |
|------|--------|------------|
| CodeQL CLI | No linux/arm64 binary | Hardcoded `runs-on: ubuntu-latest` |
| Snyk Docker action | No ARM64 image | Replaced with `sudo npm install -g snyk` |
| Semgrep Docker | No ARM64 image | Replaced with `pip install semgrep` |
| k6 deb repository | No ARM64 packages | Direct tarball download from GitHub releases |

### Operational Hardening

| Mechanism | Frequency | Purpose |
|-----------|-----------|---------|
| `runner-maintenance.yml` | Weekly (Sunday 3 AM) | Docker prune, temp cleanup, disk alerts |
| Post-build cleanup | Per CI/deploy run | Container and image prune on self-hosted |
| Pre-job disk check | Per build job | Emergency cleanup if disk > 90% |
| `runner-health-check.yml` | Every 15 minutes | Docker, disk, memory monitoring |
| ARM64 verification step | Per CI/deploy run | Assert `uname -m == aarch64` when RUNNER is set |

## Consequences

### Positive

- **Zero CI cost**: Self-hosted runner minutes are free
- **Persistent cache**: Docker layers, npm/pnpm cache persist across runs (no cold starts)
- **Full control**: Custom tooling, pre-installed dependencies, no rate limits
- **Instant rollback**: Clear `vars.RUNNER` to revert all workflows to GitHub-hosted

### Negative

- **Single point of failure**: One VM serves all CI jobs (mitigated by fallback pattern)
- **Performance delta**: Backend tests ~30-40% slower on ARM64 (mitigated by 60-min timeout)
- **Maintenance burden**: VM requires periodic updates, disk management, monitoring
- **No auto-scaling**: Queue saturation possible during high-PR activity (mitigated by concurrency groups)

### Trade-offs

| Aspect | Self-Hosted ARM64 | GitHub-Hosted x86 |
|--------|-------------------|-------------------|
| Cost | Free | $0.008/min (Linux) |
| Performance | 30-40% slower (.NET) | Baseline |
| Cache | Persistent (instant) | Cold start per run |
| Availability | Single VM (+ fallback) | Multi-region, auto-scaled |
| Maintenance | Manual (weekly cron) | Zero |
| Tooling | Some tools need ARM64 fixes | Full x86 ecosystem |

## Alternatives Considered

### 1. Pay for GitHub-hosted minutes
- **Rejected**: Recurring cost with no performance benefit over free tier
- **Cost**: ~$50-100/month at current usage

### 2. Self-hosted x86 runner (non-ARM)
- **Rejected**: Oracle free tier only offers ARM64; x86 VMs have cost
- **Would avoid**: ARM64 compatibility issues

### 3. Hybrid approach (some workflows on each)
- **Partially adopted**: Security scans (CodeQL) remain on GitHub-hosted
- **Full hybrid rejected**: Complexity of maintaining two runner configs per workflow

## Rollback Procedure

1. **Full rollback**: Delete `vars.RUNNER` variable in GitHub org/repo settings → all workflows fall back to `ubuntu-latest` immediately
2. **Per-workflow rollback**: Hardcode `runs-on: ubuntu-latest` in specific workflow file
3. **Verification**: Check `runner.name` in workflow logs — GitHub-hosted runners contain "GitHub Actions"

## Update 2026-07-28 — Drift correction & direction (#3331)

The runner topology drifted from this ADR's original design. Recorded here so future decisions start from reality, not the 2026-03 plan.

**Migration status (Option E, #3331):** Phase 1 (SSH disk-gate, PR #3346) merged; **Phase 2 cutover done** — the `RUNNER` repo variable was removed, so all workflows now run on GitHub-hosted; **Phase 3 (the PR carrying this update)** deletes the runner-babysitting apparatus (3 workflows, the Prometheus runner alerts, the `infra/runner/` pet-care scripts). The `cloud-init.yml`/`setup-vm.sh`/`setup-runner.sh` IaC is kept for the D fallback. The final manual step is deregistering the runner on the VPS. Plan: [`docs/superpowers/plans/2026-07-28-issue-3331-eliminate-self-hosted-runner.md`](../../../superpowers/plans/2026-07-28-issue-3331-eliminate-self-hosted-runner.md).

**Reality vs the original decision:**
- The active runner is **NOT** the dedicated Oracle Cloud 24GB VM this ADR assumed. It is **co-located on the Hetzner staging VPS (~8GB ARM64)**, sharing the box with the app containers (`/home/deploy/actions-runner`, user `deploy`). The `infra/runner/cloud-init.yml` Oracle recipe was never the live deploy runner.
- "Migrate ALL workflows to self-hosted" was **not sustained**: `ci.yml`'s `select-runner` hardcodes `ubuntu-latest` (`ci.yml:74`), so the 14,700+ tests already run on GitHub-hosted cloud. The self-hosted runner today executes only the deploy-staging orchestration (SSH), rollback, e2e, and ops one-offs.
- Heavy compilation was evicted to cloud one incident at a time: backend build (#2650, Roslyn OOM), image build (2026-05-08 swap-thrash), migration SQL gen, build-ai. The `Frontend build check` OOM (2026-07-28, deploy run 30334115340) is the same pattern's latest instance.

**Root cause of the #3331 OOM:** the co-located runner has a `MemoryMax=3G` cgroup cap (`infra/runner/systemd-overrides/10-memory-limits.conf`, #2019) to protect the app containers; the redundant frontend build ran with a 4GB Node heap → SIGKILL. Structural, not bad luck.

**Direction (spec-panel #3331, unanimous across Nygard/Hightower/Fowler/Newman):**
1. **Done (the PR carrying this update):** removed the redundant `Frontend build check` from `deploy-staging.yml` pre-deploy — it duplicated `ci.yml Build Frontend` (cloud) and the real cloud image build. Build surface on the deploy host is now zero (backend + frontend). This is a tourniquet, **not** a cure — #3331 stays open for the structural fix.
2. **Gate:** measure actual monthly cloud minutes for `backend-e2e` + `test-performance` on hosted ARM64.
3. **Before production:** decouple CI from the app box for good — either **eliminate the self-hosted runner** (build on hosted, deploy via SSH from cloud with a scoped `command=`-restricted deploy key; the deploy path is already 100% public-SSH to `STAGING_HOST`) if the heavy suites fit the free/cheap tier, or **move the runner to a dedicated VM** (`cloud-init.yml` already exists as the IaC recipe) if they don't. A CI build OOM must never be able to take down the app it deploys to.

Spec: [`docs/superpowers/specs/2026-07-28-spec-panel-3331-cicd-runner.md`](../../../superpowers/specs/2026-07-28-spec-panel-3331-cicd-runner.md).

## References

- Epic: #2967 (Self-Hosted Runner Infrastructure)
- Runner setup: #2969
- Migration: #2970
- ARM64 fixes: #5547, #5553, #5557
- Post-migration hardening: #5563
- [GitHub Self-Hosted Runner Docs](https://docs.github.com/en/actions/hosting-your-own-runners)
- [Oracle Cloud Free Tier](https://www.oracle.com/cloud/free/)
