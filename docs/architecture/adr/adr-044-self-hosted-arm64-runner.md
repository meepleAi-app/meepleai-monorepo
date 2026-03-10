# ADR-044: Migrate CI/CD to Self-Hosted ARM64 Runner

**Status**: Accepted
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

## References

- Epic: #2967 (Self-Hosted Runner Infrastructure)
- Runner setup: #2969
- Migration: #2970
- ARM64 fixes: #5547, #5553, #5557
- Post-migration hardening: #5563
- [GitHub Self-Hosted Runner Docs](https://docs.github.com/en/actions/hosting-your-own-runners)
- [Oracle Cloud Free Tier](https://www.oracle.com/cloud/free/)
