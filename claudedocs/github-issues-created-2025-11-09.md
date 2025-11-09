# GitHub Issues Created - 2025-11-09

## Summary

Created 6 GitHub issues to track cleanup follow-up tasks and process improvements.

**Date**: 2025-11-09
**Source**: Cleanup analysis and recommendations
**Total Issues**: 6 (3 high priority, 3 optional)

---

## 🎯 High Priority Issues

### Issue #813: Review TODO/FIXME Comments
**Title**: `chore: Review and resolve TODO/FIXME comments (5 files)`
**Labels**: `documentation`
**Priority**: Medium

**Description**: Review 5 files containing TODO, FIXME, or HACK comments to determine if they represent valid work items or obsolete markers.

**Tasks**:
- Locate all files with TODO/FIXME/HACK comments
- Review each for relevance
- Convert valid items to GitHub issues
- Remove or document obsolete comments

**Commands**:
```bash
find apps/api/src -type f -name "*.cs" -exec grep -Hn "//\s*TODO\|//\s*FIXME\|//\s*HACK" {} \;
```

---

### Issue #814: Investigate Failing Integration Tests
**Title**: `test: Investigate and fix 33 failing integration tests`
**Labels**: `kind/task`, `area/infra`
**Priority**: High

**Description**: 33 integration tests failing across N8n webhooks, Chess agent, and admin endpoints. Need systematic investigation and fixes.

**Test Categories**:
- N8n Webhook Tests (6+ failures)
- Chess Agent Tests (15+ failures)
- Admin Endpoints Tests (5+ failures)
- Logging/Streaming Tests (7+ failures)

**Commands**:
```bash
cd apps/api && dotnet test --verbosity detailed
dotnet test --filter "FullyQualifiedName~ChessAgent"
dotnet test --filter "FullyQualifiedName~N8n"
```

**Context**: Tests were passing before, likely regression or environment issue.

---

### Issue #815: Dependency Audit
**Title**: `chore: Run comprehensive dependency audit (backend + frontend)`
**Labels**: `kind/task`, `area/infra`, `area/security`
**Priority**: Medium

**Description**: Comprehensive audit of backend (.NET) and frontend (pnpm) dependencies to identify outdated, vulnerable, or unused packages.

**Audit Scope**:
- Backend: NuGet packages (outdated, vulnerable, unused)
- Frontend: pnpm packages (audit, outdated)
- Security patches prioritization
- Documentation updates

**Commands**:
```bash
# Backend
cd apps/api && dotnet list package --outdated
dotnet list package --vulnerable --include-transitive

# Frontend
cd apps/web && pnpm audit --audit-level=high
pnpm outdated
```

**Success Metrics**:
- Zero high/critical vulnerabilities
- <5% outdated packages
- All tests passing after updates

---

## 🛠️ Optional Enhancement Issues

### Issue #816: Automated Cleanup Scripts
**Title**: `devex: Implement automated cleanup scripts for cache directories`
**Labels**: `kind/task`, `area/infra`, `enhancement`
**Priority**: Low

**Description**: Create automated scripts to periodically clean cache directories (Serena MCP, CodeQL, Playwright) preventing 120MB+ buildup.

**Proposed Script**: `tools/cleanup-caches.sh` (and `.ps1`)

**Features**:
- Monthly scheduled cleanup
- Dry-run mode
- Verbose output
- Cross-platform (bash + PowerShell)

**Automation Options**:
1. GitHub Actions (monthly scheduled workflow)
2. Git hook (post-merge)
3. Developer command (`pnpm cleanup`)

**Impact**: Prevents ~120MB cache accumulation, improves developer experience

---

### Issue #817: Pre-commit Hooks for Large Files
**Title**: `devex: Add pre-commit hooks to prevent large file commits`
**Labels**: `kind/task`, `area/infra`, `area/security`
**Priority**: Low

**Description**: Implement pre-commit hooks to prevent accidentally committing large files (like alerts.json) to repository.

**Proposed Solution**:
- Update `.pre-commit-config.yaml`
- Add `check-added-large-files` hook (1MB limit)
- Custom hook for `alerts.json` specifically
- Exclusions for legitimate large files (datasets, PDFs)

**Configuration**:
```yaml
- id: check-added-large-files
  args: ['--maxkb=1000']  # 1MB limit
  exclude: |
    (?x)^(
      data/.*|
      datasets/.*|
      .*\.pdf$
    )$
```

**Success Metrics**: Zero accidental large file commits in 6 months

---

### Issue #818: Quarterly Security Review Process
**Title**: `security: Establish quarterly security scan review process`
**Labels**: `kind/policy`, `area/security`, `area/infra`
**Priority**: Low

**Description**: Establish systematic quarterly process to review CodeQL security scans, dependency vulnerabilities, and security best practices.

**Review Scope**:
1. CodeQL Security Scan Results
2. Dependency Vulnerabilities (backend + frontend)
3. Security Best Practices Audit
4. Infrastructure Security

**Schedule**:
- Q1: January
- Q2: April
- Q3: July
- Q4: October

**Automation**:
- GitHub Actions reminder workflow (scheduled)
- Auto-create review issue each quarter

**Deliverables**:
- `docs/security/YYYY-QN-security-review.md` (quarterly)
- Security posture improvements
- Prioritized remediation backlog

---

## 📊 Issue Summary

| Issue | Title | Priority | Labels | Effort |
|-------|-------|----------|--------|--------|
| #813 | Review TODO/FIXME comments | Medium | `documentation` | 1-2 hours |
| #814 | Fix 33 failing tests | High | `kind/task`, `area/infra` | 4-8 hours |
| #815 | Dependency audit | Medium | `kind/task`, `area/infra`, `area/security` | 2-4 hours |
| #816 | Automated cleanup scripts | Low | `kind/task`, `area/infra`, `enhancement` | 3-4 hours |
| #817 | Pre-commit hooks | Low | `kind/task`, `area/infra`, `area/security` | 2-3 hours |
| #818 | Security review process | Low | `kind/policy`, `area/security`, `area/infra` | 4-6 hours initial |

**Total Estimated Effort**: 16-27 hours across all issues

---

## 🎯 Recommended Work Order

### Sprint 1 (High Priority)
1. **Issue #814**: Fix failing tests (critical for CI stability)
2. **Issue #815**: Dependency audit (security priority)

### Sprint 2 (Medium Priority)
3. **Issue #813**: Review TODO comments (code quality)

### Sprint 3 (Process Improvements)
4. **Issue #817**: Pre-commit hooks (prevent future issues)
5. **Issue #816**: Automated cleanup (developer experience)
6. **Issue #818**: Security review process (establish cadence)

---

## 📚 Related Documentation

- **Cleanup Report**: `claudedocs/cleanup-report-2025-11-09.md`
- **Code Quality Fixes**: `claudedocs/code-quality-fixes-2025-11-09.md`
- **Completion Summary**: `claudedocs/cleanup-completion-summary-2025-11-09.md`
- **Security Guide**: `docs/SECURITY.md`
- **Contributing**: `CONTRIBUTING.md`

---

## 🔗 Quick Links

- [Issue #813 - TODO Review](https://github.com/DegrassiAaron/meepleai-monorepo/issues/813)
- [Issue #814 - Failing Tests](https://github.com/DegrassiAaron/meepleai-monorepo/issues/814)
- [Issue #815 - Dependency Audit](https://github.com/DegrassiAaron/meepleai-monorepo/issues/815)
- [Issue #816 - Cleanup Scripts](https://github.com/DegrassiAaron/meepleai-monorepo/issues/816)
- [Issue #817 - Pre-commit Hooks](https://github.com/DegrassiAaron/meepleai-monorepo/issues/817)
- [Issue #818 - Security Reviews](https://github.com/DegrassiAaron/meepleai-monorepo/issues/818)

---

**Issues Created By**: Claude Code `/sc:cleanup`
**Date**: 2025-11-09
**Total Issues**: 6
**Status**: ✅ All issues created successfully
