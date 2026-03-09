# ACT — Zero-Cost CI/CD Formalization

**Epic**: #2967
**Status**: Pending (complete after CHECK phase)

## Improvements to Formalize

*Based on CHECK results, document what to standardize or improve.*

### If Successful
- [ ] Document as standard infrastructure pattern
- [ ] Add runner health to existing monitoring dashboard
- [ ] Consider multi-runner for parallel CI jobs
- [ ] Remove `|| 'ubuntu-latest'` fallback (optional, keeps safety)

### If Performance Degrades
- [ ] Profile ARM64 vs x86 build differences
- [ ] Optimize Dockerfiles for ARM64 (`FROM --platform=linux/arm64`)
- [ ] Consider Hetzner Cloud x86 ($4.49/month) as alternative
- [ ] Increase VM resources (Oracle allows up to 4 OCPU, 24GB free)

### If Reliability Issues
- [ ] Add secondary runner (different Oracle region)
- [ ] Implement automatic failback to GitHub-hosted on runner failure
- [ ] Add PagerDuty/Slack alerting for runner downtime

## Lessons Learned

*Fill after validation phase.*
