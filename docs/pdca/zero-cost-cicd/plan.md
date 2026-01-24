# Plan: Zero-Cost CI/CD Infrastructure

**Created**: 2026-01-23
**Status**: Planning
**Estimated Total Time**: 6-8 hours (across 2 weeks)

---

## Hypothesis

**Goal**: Eliminate GitHub Actions costs by implementing self-hosted runner on Oracle Always Free infrastructure while maintaining CI/CD quality and developer experience.

**Approach**: GitHub Free + Oracle Always Free ARM64 VM with GitHub Actions runner

**Why This Approach**:
1. **Zero Migration**: No repository migration, continues using GitHub
2. **Unlimited CI/CD**: Self-hosted runner = no Actions minute consumption
3. **Zero Cost**: Oracle Always Free tier (4 ARM cores, 24GB RAM, 200GB storage) has no expiration
4. **Maintains Integration**: Claude code review, Issues, Wiki unchanged
5. **Reversible**: Can revert to GitHub-hosted runners if needed

---

## Expected Outcomes (Quantitative)

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| **Monthly Cost** | $0 (within quota) | $0 (unlimited) | GitHub billing dashboard |
| **CI/CD Minutes** | 1,350/2,000 quota | Unlimited (self-hosted) | GitHub Actions usage |
| **Setup Time** | N/A | 2-3 hours | Time tracking |
| **Runner Uptime** | N/A | >99% | Monitoring alerts |
| **Build Performance** | Baseline | ≤ current | Workflow duration comparison |
| **Developer Friction** | None | None | No workflow changes required |

---

## Architecture

### Current State
```
GitHub-Hosted Runners (ubuntu-latest)
├── Backend CI: ~15 min/run
├── Frontend CI: ~10 min/run
└── E2E Tests: ~20 min/run
Total: 1,350 min/month (30 commits)
```

### Target State
```
Oracle Always Free VM (ARM64)
├── Ubuntu 22.04 ARM64
├── GitHub Actions Runner (self-hosted)
├── Docker (for containerized builds)
└── Monitoring (Prometheus Node Exporter)

GitHub Workflows
├── backend-ci.yml → runs-on: [self-hosted, linux, ARM64]
├── frontend-ci.yml → runs-on: [self-hosted, linux, ARM64]
└── e2e-tests.yml → runs-on: [self-hosted, linux, ARM64]
```

---

## Implementation Phases

### Week 1: Setup (2-3 hours)

**Phase 1.1: Oracle Cloud + VM Provisioning** (30 min)
- Create Oracle Cloud Always Free account
- Provision ARM64 VM (4 cores, 24GB RAM)
- Configure network (VCN, subnet, security rules)
- Setup SSH access

**Phase 1.2: Runner Installation** (1.5 hours)
- System updates and Docker installation
- GitHub Actions Runner download and configuration
- Systemd service setup
- Connection validation

**Phase 1.3: Workflow Migration** (1 hour)
- Update `.github/workflows/*.yml` files
- Add conditional path filters (optimization)
- Docker layer caching configuration
- Test workflow execution

### Week 2: Validation (1-2 hours)

**Phase 2.1: Performance Monitoring** (30 min)
- Verify build times ≤ current baseline
- Check runner uptime and stability
- Monitor resource usage (CPU, RAM, disk)

**Phase 2.2: Cost Validation** (30 min)
- Confirm GitHub Actions usage = 0 minutes
- Verify Oracle Cloud bill = $0
- Document baseline metrics

**Phase 2.3: Monitoring Setup** (1 hour) - Optional
- Prometheus Node Exporter installation
- Grafana dashboard configuration
- Alert rules for runner down/disk full

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Oracle suspends Always Free** | High | Very Low | Fallback: Hetzner ($4.49/month) or GitLab Free |
| **ARM64 compatibility issues** | Medium | Low | Use ARM64-compatible base images, test early |
| **Runner maintenance overhead** | Low | Medium | Automate updates, document procedures |
| **Single point of failure** | Medium | Low | Oracle SLA 99.95%, implement monitoring |
| **Network connectivity issues** | Medium | Very Low | Oracle outbound traffic 10TB/month free |
| **Disk space exhaustion** | Low | Medium | Regular Docker pruning, 200GB allocation |

---

## Dependencies

**Technical**:
- Oracle Cloud account (Always Free tier available)
- GitHub repository admin access (runner registration)
- SSH key pair for VM access
- Basic Linux/Docker knowledge

**Documentation**:
- [GitHub Alternatives & Cost Optimization Guide](../../04-deployment/github-alternatives-cost-optimization.md)
- [Oracle Always Free Tier Docs](https://www.oracle.com/cloud/free/)
- [GitHub Self-Hosted Runners Docs](https://docs.github.com/en/actions/hosting-your-own-runners)

---

## Acceptance Criteria

### Phase 1: Setup Complete
- ✅ Oracle VM provisioned and accessible via SSH
- ✅ GitHub Actions runner installed and online
- ✅ All workflows updated to use self-hosted runner
- ✅ Test commit triggers successful build on self-hosted runner
- ✅ Build artifacts generated correctly

### Phase 2: Validation Complete
- ✅ GitHub Actions usage shows 0 minutes consumed
- ✅ Oracle Cloud billing shows $0 charges
- ✅ Build times ≤ baseline (no performance degradation)
- ✅ Runner uptime >99% over 2-week period
- ✅ Documentation updated with setup procedure

### Phase 3: Monitoring (Optional)
- ✅ Prometheus Node Exporter running
- ✅ Grafana dashboard showing runner health
- ✅ Alert rules configured for critical failures

---

## Success Metrics

**Primary**:
1. **Cost Elimination**: GitHub Actions bill = $0/month
2. **Zero Disruption**: No developer workflow changes
3. **Performance Maintained**: Build times ≤ current

**Secondary**:
1. **Setup Efficiency**: Completed within 3 hours
2. **Reliability**: >99% runner uptime
3. **Documentation Quality**: Clear troubleshooting guide

---

## Rollback Plan

**If Setup Fails**:
1. Revert workflows to `runs-on: ubuntu-latest`
2. Delete Oracle VM resources
3. Continue with GitHub-hosted runners (accept occasional $1-2/month overage)

**If Performance Degrades**:
1. Investigate ARM64 compatibility issues
2. Optimize Dockerfiles for ARM64
3. Consider Hetzner Cloud x86 alternative ($4.49/month)

**If Maintenance Too High**:
1. Implement automation for runner updates
2. Setup monitoring for proactive alerts
3. If still unacceptable: revert to GitHub-hosted runners

---

## Next Steps

1. **Create GitHub Epic**: `epic/zero-cost-cicd-infrastructure`
2. **Create GitHub Issues**: 8 issues following implementation timeline
3. **Initialize PDCA Tracking**: Setup `docs/pdca/zero-cost-cicd/do.md`
4. **Schedule Execution**: Week 1 setup, Week 2 validation

---

**References**:
- [GitHub Alternatives Documentation](../../04-deployment/github-alternatives-cost-optimization.md)
- [Oracle Cloud Signup](https://signup.cloud.oracle.com)
- [GitHub Runner Releases](https://github.com/actions/runner/releases)
