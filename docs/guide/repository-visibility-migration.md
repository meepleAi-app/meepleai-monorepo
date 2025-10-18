# Repository Visibility Migration Guide

**Version**: 1.0
**Last Updated**: 2025-01-18
**Author**: DevOps Team

---

## Overview

This guide provides step-by-step instructions for migrating the MeepleAI monorepo from **public** to **private** (or vice versa) on GitHub, including necessary configuration changes, cost analysis, and rollback procedures.

**Estimated Time**: 2-3 hours (including testing)
**Risk Level**: Medium (affects CI/CD and security scanning)
**Reversible**: Yes (rollback plan included)

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Impact Analysis](#2-impact-analysis)
3. [Pre-Migration Checklist](#3-pre-migration-checklist)
4. [Migration Procedure](#4-migration-procedure)
5. [Post-Migration Verification](#5-post-migration-verification)
6. [Cost Optimization](#6-cost-optimization)
7. [Rollback Procedure](#7-rollback-procedure)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Prerequisites

### Required Access

- [ ] **GitHub Admin Access**: Repository settings permissions
- [ ] **GitHub Billing Access**: To review/upgrade plan if needed
- [ ] **Local Git Clone**: Up-to-date clone of the repository

### Required Tools

- [ ] Git CLI (`git --version` ≥ 2.30)
- [ ] GitHub CLI (`gh --version` ≥ 2.30) - Optional but recommended
- [ ] PowerShell 7+ (for automation scripts)

### Backup Checklist

Before proceeding, ensure you have:

- [ ] **Local backup** of entire repository (including `.git`)
- [ ] **Export of Issues/PRs** (GitHub Settings → Export)
- [ ] **Copy of Secrets**: Document all GitHub Actions secrets (don't export values)
- [ ] **CI/CD run history**: Download recent workflow logs for reference

**Backup Command**:
```bash
# Full repository backup (including history)
git clone --mirror https://github.com/DegrassiAaron/meepleai-monorepo.git meepleai-backup
cd meepleai-backup
git bundle create ../meepleai-monorepo-backup-$(date +%Y%m%d).bundle --all
```

---

## 2. Impact Analysis

### GitHub Features Comparison

| Feature | Public Repository | Private Repository |
|---------|-------------------|-------------------|
| **GitHub Actions Minutes** | 2,000 min/month (Free) | 2,000 min/month (Free) |
| **GitHub Actions Storage** | 500 MB | 500 MB |
| **Dependabot** | ✅ Free | ✅ Free |
| **CodeQL (SAST)** | ✅ Free | ⚠️ Requires Advanced Security ($49/user/month) |
| **Branch Protection** | ✅ Available | ✅ Available |
| **Collaborators** | Unlimited (read-only) | Limited by plan (Free: unlimited) |
| **GitHub Pages** | ✅ Free | ❌ Requires Pro/Enterprise |
| **Wiki** | ✅ Public | 🔒 Private (requires auth) |

### Cost Analysis

#### Current Setup (Public Repository)
- **Total Monthly Cost**: $0
  - GitHub Actions: Free (under 2,000 min/month)
  - CodeQL: Free
  - Dependabot: Free

#### After Migration (Private Repository)

**Option A: Minimal Changes (Recommended for Budget-Conscious)**
- **Total Monthly Cost**: $0
  - GitHub Actions: Free (2,000 min/month sufficient)
  - CodeQL: **Disabled** (replaced with free alternatives)
  - Dependabot: Free
  - **Trade-off**: Less comprehensive SAST scanning

**Option B: Full Security Suite**
- **Total Monthly Cost**: $49-$98/month
  - GitHub Actions: Free
  - GitHub Advanced Security: $49/active committer/month
  - Dependabot: Free
  - **Benefit**: Full CodeQL SAST coverage

**Option C: Upgrade to GitHub Pro**
- **Total Monthly Cost**: $7/user/month
  - GitHub Actions: 3,000 min/month
  - CodeQL: Still requires Advanced Security (+$49)
  - **Benefit**: More Actions minutes, protected branches

### CI/CD Usage Estimate

**Current Monthly GitHub Actions Minutes**:
```
API CI (ci-api):        ~10 min/run
Web CI (ci-web):        ~5 min/run
Security Scan:          ~8 min/run
RAG Evaluation:         ~12 min/run

Per PR (all workflows): ~35 min
Monthly estimate (20 PRs): 700 min

Headroom: 2,000 - 700 = 1,300 min/month ✅ Safe margin
```

**Recommendation**: Free tier (2,000 min/month) is sufficient.

---

## 3. Pre-Migration Checklist

### 3.1 Review Current Configuration

- [ ] **List all workflows**: Check `.github/workflows/*.yml`
  - [ ] `ci.yml` - Main CI pipeline
  - [ ] `security-scan.yml` - Security scanning (CodeQL)
  - [ ] Other workflows?

- [ ] **List all secrets**: Settings → Secrets and variables → Actions
  - [ ] `OPENROUTER_API_KEY`
  - [ ] `REDIS_URL` (if needed)
  - [ ] Other secrets?

- [ ] **Review collaborators**: Settings → Collaborators
  - Current: Public (anyone can view/fork)
  - After migration: List who needs access

- [ ] **Check integrations**: Settings → Integrations
  - Dependabot
  - Third-party apps (if any)

### 3.2 Decide on Security Scanning Strategy

Choose **one** option:

#### Option A: Disable CodeQL, Use Free Alternatives ✅ (Recommended for Private)

**Pros**:
- $0 additional cost
- Adequate security coverage for most projects
- Easy to implement

**Cons**:
- Less comprehensive than CodeQL
- Manual review needed for some vulnerability types

**Implementation**:
- Disable CodeQL job in `security-scan.yml`
- Enable Semgrep (free SAST tool)
- Keep .NET Analyzers and Dependabot

**See**: [Section 6.2 - Semgrep Setup](#62-semgrep-alternative-configuration)

#### Option B: Keep CodeQL, Pay for Advanced Security

**Pros**:
- Best-in-class SAST
- Deep integration with GitHub Security tab
- Automatic PR security alerts

**Cons**:
- $49/month per active committer
- Requires GitHub billing setup

**Implementation**:
- Enable GitHub Advanced Security in repository settings
- Keep `security-scan.yml` unchanged

#### Option C: Hybrid Approach

**Pros**:
- Balanced cost/coverage
- CodeQL for critical paths, Semgrep for others

**Cons**:
- More complex configuration

**Implementation**:
- CodeQL on `main` branch only (reduce minutes)
- Semgrep on PRs

**Decision**: _____________________ (Choose A, B, or C)

### 3.3 Communication Plan

- [ ] **Notify team**: Announce planned migration date (if team exists)
- [ ] **Update documentation**: Mark date in this guide
- [ ] **Stakeholder approval**: Get sign-off from project owner

**Migration Date**: _____________________ (Schedule at least 3 days in advance)

---

## 4. Migration Procedure

### 4.1 GitHub Settings Changes

**Step 1: Change Repository Visibility**

1. Navigate to: `https://github.com/DegrassiAaron/meepleai-monorepo/settings`
2. Scroll to **Danger Zone** (bottom of page)
3. Click **Change repository visibility**
4. Select **Make private**
5. **Read the warnings carefully**:
   - ⚠️ Forks of this repository will become detached
   - ⚠️ GitHub Pages will be disabled (if used)
   - ⚠️ Wikis will become private
6. Type repository name to confirm: `DegrassiAaron/meepleai-monorepo`
7. Click **I understand, change repository visibility**

**Step 2: Configure Collaborator Access** (if needed)

1. Go to: Settings → Collaborators
2. Click **Add people**
3. Add collaborators with appropriate roles:
   - **Admin**: Full access (settings, delete)
   - **Write**: Push to branches, merge PRs
   - **Read**: View code, clone, fork (within org)

**Step 3: Update Branch Protection Rules**

1. Go to: Settings → Branches
2. Edit rule for `main` branch (or create if missing):
   - ☑ Require a pull request before merging
   - ☑ Require approvals: 1 (or more)
   - ☑ Dismiss stale pull request approvals when new commits are pushed
   - ☑ Require status checks to pass before merging
     - Required checks: `ci-api`, `ci-web`
   - ☑ Require conversation resolution before merging
   - ☑ Do not allow bypassing the above settings (even for admins)

### 4.2 Update CI/CD Configuration

**If you chose Option A (Disable CodeQL):**

**File**: `.github/workflows/security-scan.yml`

**Changes**:
```diff
jobs:
- # Comment out or delete the 'codeql' job
- codeql:
-   name: CodeQL Security Analysis
-   runs-on: ubuntu-latest
-   # ... (entire job removed)

  dependency-scan:
    name: Dependency Vulnerability Scan
    # ... (keep this job)

  dotnet-analyzers:
    name: .NET Security Analyzers
    # ... (keep this job)

+ semgrep-scan:
+   name: Semgrep SAST
+   runs-on: ubuntu-latest
+   # ... (see Section 6.2 for full configuration)
```

**Commit changes**:
```bash
git checkout -b chore/migrate-to-private-repo
git add .github/workflows/security-scan.yml
git commit -m "chore: disable CodeQL for private repo, add Semgrep alternative"
```

**If you chose Option B (Keep CodeQL):**

**Enable Advanced Security**:
1. Go to: Settings → Code security and analysis
2. Click **Enable** for GitHub Advanced Security
3. Confirm billing (if prompted)
4. No workflow changes needed

**If you chose Option C (Hybrid):**

See advanced configuration in [Section 6.3](#63-hybrid-codeql-configuration).

### 4.3 Update Documentation

**Files to update**:

1. **README.md** (if it mentions "public repository")
   ```diff
   - This is a public repository for MeepleAI...
   + This is a private repository for MeepleAI...
   ```

2. **CLAUDE.md** (update troubleshooting section if needed)
   ```diff
   + - **Private repository note**: CodeQL replaced with Semgrep (see docs/guide/repository-visibility-migration.md)
   ```

3. **CONTRIBUTING.md** (if it exists, update fork instructions)
   ```diff
   - Fork this repository to contribute
   + Request collaborator access to contribute (repository is private)
   ```

**Commit documentation changes**:
```bash
git add README.md CLAUDE.md CONTRIBUTING.md
git commit -m "docs: update for private repository visibility"
```

### 4.4 Test Migration Changes

**Before pushing to GitHub**:

1. **Local testing**:
   ```bash
   # Verify workflows are valid YAML
   yamllint .github/workflows/*.yml

   # Or use GitHub Actions validator (requires gh CLI)
   gh workflow list
   ```

2. **Create PR**:
   ```bash
   git push origin chore/migrate-to-private-repo
   gh pr create --title "Migrate repository to private visibility" \
                --body "See docs/guide/repository-visibility-migration.md for details"
   ```

3. **Review PR**:
   - Check that CI workflows run successfully
   - Verify no unexpected failures
   - Review security scan results (Semgrep instead of CodeQL)

4. **Merge PR** (only after thorough review)

---

## 5. Post-Migration Verification

### 5.1 Immediate Checks (Within 1 Hour)

- [ ] **Verify repository is private**: Go to repo URL in incognito mode → should show 404
- [ ] **Test CI/CD**: Create a small test PR, verify workflows run
- [ ] **Check Actions minutes**: Settings → Billing → Usage (should show recent runs)
- [ ] **Verify Dependabot**: Security → Dependabot (should still show alerts)
- [ ] **Test collaborator access**: Have collaborator clone repo (if applicable)

### 5.2 Security Scan Verification

**If using Semgrep (Option A)**:

- [ ] Check Semgrep scan results in PR checks
- [ ] Review any new findings: Security → Code scanning alerts
- [ ] Compare with previous CodeQL results (if available)

**If using CodeQL (Option B)**:

- [ ] Verify Advanced Security is active: Settings → Code security
- [ ] Check CodeQL results in PR checks
- [ ] Review security alerts: Security → Code scanning

### 5.3 Functionality Tests

Run the full test suite to ensure nothing broke:

```bash
# Backend tests
cd apps/api
dotnet test

# Frontend tests
cd apps/web
pnpm test

# E2E tests (optional but recommended)
pnpm test:e2e
```

### 5.4 Monitoring Setup (First Week)

- [ ] **Monitor GitHub Actions minutes**:
  - Check daily for first week: Settings → Billing → Usage
  - Set reminder to review monthly usage

- [ ] **Track security alerts**:
  - Security → Dependabot (should continue working)
  - Security → Code scanning (Semgrep or CodeQL)

- [ ] **Review collaborator activity** (if applicable):
  - Insights → Traffic (only for admins)

---

## 6. Cost Optimization

### 6.1 Reduce GitHub Actions Minutes

**Strategy 1: Cache Dependencies**

Already implemented in `ci.yml`:
```yaml
# .NET build cache (already present)
- uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}

# pnpm cache (already present)
- uses: pnpm/action-setup@v2
  with:
    run_install: false
```

**Strategy 2: Use Concurrency Limits**

Add to workflows to cancel outdated runs:
```yaml
# Add to .github/workflows/ci.yml
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```

**Strategy 3: Filter Workflow Triggers**

```yaml
# Only run on relevant paths
on:
  pull_request:
    paths:
      - 'apps/api/**'
      - 'apps/web/**'
      - '.github/workflows/ci.yml'
```

**Potential Savings**: 20-30% reduction in minutes

### 6.2 Semgrep Alternative Configuration

**Create**: `.github/workflows/semgrep.yml`

```yaml
name: Semgrep SAST

on:
  pull_request:
    branches: [main, develop]
  push:
    branches: [main]
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday

jobs:
  semgrep:
    name: Semgrep Security Scan
    runs-on: ubuntu-latest

    permissions:
      contents: read
      security-events: write  # For uploading SARIF

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Run Semgrep
        uses: semgrep/semgrep-action@v1
        with:
          config: >-
            p/security-audit
            p/secrets
            p/owasp-top-ten
            p/csharp
            p/typescript
          generateSarif: true

      - name: Upload SARIF file
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: semgrep.sarif
        if: always()
```

**Semgrep Rules Coverage**:
- ✅ SQL Injection
- ✅ XSS (Cross-Site Scripting)
- ✅ CSRF
- ✅ Hardcoded secrets
- ✅ Insecure crypto
- ✅ Path traversal
- ✅ Command injection
- ✅ OWASP Top 10

**Comparison with CodeQL**:

| Feature | CodeQL | Semgrep |
|---------|--------|---------|
| **Languages** | 10+ (C#, JS, TS, Java, Go, etc.) | 30+ (C#, JS, TS, Python, etc.) |
| **Rule Quality** | ⭐⭐⭐⭐⭐ Excellent (GitHub-maintained) | ⭐⭐⭐⭐ Very Good (community + paid) |
| **False Positives** | Low | Medium |
| **Speed** | Slower (~5-8 min) | Faster (~2-4 min) |
| **Cost (Private)** | $49/user/month | Free (OSS) |
| **GitHub Integration** | Native | SARIF upload |

**Recommendation**: Semgrep is an excellent free alternative for private repositories.

### 6.3 Hybrid CodeQL Configuration

Run CodeQL only on `main` branch (reduce minutes):

**File**: `.github/workflows/security-scan.yml`

```yaml
jobs:
  codeql:
    name: CodeQL Security Analysis
    runs-on: ubuntu-latest
    # Only run on main branch (not PRs)
    if: github.ref == 'refs/heads/main'

    strategy:
      matrix:
        language: [csharp, javascript]

    steps:
      # ... (rest of CodeQL config)
```

**Combine with Semgrep on PRs**:

```yaml
# In semgrep.yml
on:
  pull_request:  # Run Semgrep on all PRs
    branches: [main, develop]
```

**Result**:
- PRs: Fast Semgrep scan (~3 min)
- Main branch: Full CodeQL scan (~8 min, only on merge)
- **Savings**: ~5 min per PR × 20 PRs/month = 100 min/month saved

---

## 7. Rollback Procedure

**If you need to revert to public repository:**

### 7.1 Immediate Rollback (Within 24 Hours)

**Step 1: Change Visibility Back**

1. Go to: Settings → Danger Zone
2. Click **Change repository visibility**
3. Select **Make public**
4. Confirm

**Step 2: Re-enable CodeQL** (if you disabled it)

```bash
git checkout main
git revert <commit-hash>  # Revert the commit that disabled CodeQL
git push origin main
```

**Step 3: Disable Advanced Security** (if you enabled it)

1. Go to: Settings → Code security and analysis
2. Disable GitHub Advanced Security (avoid charges)

### 7.2 Restore from Backup (If Issues Occur)

**Worst-case scenario**: Repository corrupted or data loss

```bash
# Restore from bundle backup
git clone meepleai-monorepo-backup-YYYYMMDD.bundle meepleai-monorepo-restored
cd meepleai-monorepo-restored

# Push to GitHub (overwrites remote)
git remote add origin https://github.com/DegrassiAaron/meepleai-monorepo.git
git push origin --all --force
git push origin --tags --force
```

**⚠️ Warning**: Force push will overwrite all recent changes. Use only as last resort.

### 7.3 Rollback Verification

- [ ] Repository is public again
- [ ] CI/CD workflows running (CodeQL restored if applicable)
- [ ] All branches and tags intact
- [ ] Issues and PRs visible

---

## 8. Troubleshooting

### 8.1 GitHub Actions Minutes Exceeded

**Symptom**: Workflows stop running mid-month, error: "Out of Actions minutes"

**Solution**:
```bash
# Check current usage
gh api /repos/DegrassiAaron/meepleai-monorepo/actions/usage
```

**Options**:
1. **Wait for monthly reset** (first day of next month)
2. **Buy additional minutes**: Settings → Billing → Add minutes
3. **Use self-hosted runner** (free, see [Section 8.4](#84-self-hosted-runner-setup))

### 8.2 CodeQL Not Working After Migration

**Symptom**: CodeQL job fails with "Advanced Security is not enabled"

**Solution**:
1. Go to: Settings → Code security and analysis
2. Click **Enable** for GitHub Advanced Security
3. Confirm billing (if prompted)
4. Re-run workflow

**Alternative**: Disable CodeQL, use Semgrep (see [Section 6.2](#62-semgrep-alternative-configuration))

### 8.3 Dependabot Alerts Missing

**Symptom**: No Dependabot alerts after migration

**Solution**:
1. Go to: Settings → Code security and analysis
2. Verify **Dependabot alerts** is enabled
3. Verify **Dependabot security updates** is enabled
4. Check Security tab → Dependabot (may take 1-2 hours to populate)

**Manual trigger**:
```bash
gh api -X POST /repos/DegrassiAaron/meepleai-monorepo/dependabot/updates
```

### 8.4 Self-Hosted Runner Setup

**If you need unlimited GitHub Actions minutes:**

**Step 1: Install Runner**

```bash
# On Windows (PowerShell as Admin)
mkdir C:\actions-runner
cd C:\actions-runner
Invoke-WebRequest -Uri https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-win-x64-2.311.0.zip -OutFile actions-runner.zip
Expand-Archive -Path actions-runner.zip -DestinationPath .

# Configure
./config.cmd --url https://github.com/DegrassiAaron/meepleai-monorepo --token <TOKEN>
# Get token from: Settings → Actions → Runners → New self-hosted runner

# Run as service
./svc.cmd install
./svc.cmd start
```

**Step 2: Update Workflows**

```yaml
# .github/workflows/ci.yml
jobs:
  ci-api:
    runs-on: self-hosted  # Instead of ubuntu-latest
```

**Security Considerations**:
- ⚠️ Self-hosted runners have access to repository secrets
- ⚠️ Ensure runner machine is secure (firewall, antivirus, updates)
- ⚠️ Do NOT use for public repositories (security risk)

---

## 9. Post-Migration Optimization

### 9.1 Review Monthly Costs (After 30 Days)

**Checklist**:
- [ ] Check GitHub Actions minutes usage: Settings → Billing → Usage
- [ ] Review Advanced Security costs (if enabled): Settings → Billing → GitHub Advanced Security
- [ ] Evaluate Semgrep effectiveness (false positives, missed vulnerabilities)
- [ ] Decision: Continue current strategy or adjust?

**Cost Tracking Spreadsheet** (recommended):

| Month | Actions Minutes | Advanced Security | Total Cost | Notes |
|-------|----------------|-------------------|------------|-------|
| Jan 2025 | 850/2000 | $0 (Semgrep) | $0 | Migration month |
| Feb 2025 | ? | ? | ? | |

### 9.2 Security Scan Comparison

**Run side-by-side comparison** (first month):

```bash
# Generate Semgrep report
semgrep --config=auto --sarif > semgrep-report.sarif

# If you have CodeQL results (before migration), compare:
# - Number of alerts
# - Severity distribution
# - False positive rate
```

**Metrics to Track**:
- **Total alerts**: Semgrep vs. CodeQL
- **High/Critical severity**: Should be similar
- **False positives**: Semgrep may have 10-20% more
- **Time to scan**: Semgrep should be faster

**Decision Point** (after 1 month):
- If Semgrep misses critical issues → Consider upgrading to Advanced Security
- If Semgrep works well → Continue with free tier

---

## 10. Summary Checklist

### Pre-Migration
- [ ] Backup repository (Git bundle)
- [ ] Review GitHub Actions usage (ensure <2000 min/month)
- [ ] Choose security scan strategy (Semgrep vs. CodeQL)
- [ ] Notify team (if applicable)

### Migration Day
- [ ] Change repository visibility to private
- [ ] Add collaborators (if needed)
- [ ] Update branch protection rules
- [ ] Update CI/CD workflows (disable CodeQL or enable Advanced Security)
- [ ] Update documentation (README, CLAUDE.md, CONTRIBUTING)
- [ ] Create PR with changes, test thoroughly
- [ ] Merge PR

### Post-Migration
- [ ] Verify repository is private (incognito test)
- [ ] Test CI/CD workflows (create test PR)
- [ ] Check security scans (Semgrep or CodeQL)
- [ ] Monitor GitHub Actions minutes (daily for first week)
- [ ] Review after 30 days (cost, security scan effectiveness)

---

## 11. References

### GitHub Documentation
- [GitHub Pricing Plans](https://github.com/pricing)
- [GitHub Advanced Security Pricing](https://docs.github.com/en/billing/managing-billing-for-github-advanced-security/about-billing-for-github-advanced-security)
- [GitHub Actions Billing](https://docs.github.com/en/billing/managing-billing-for-github-actions/about-billing-for-github-actions)
- [Changing Repository Visibility](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility)

### Security Tools
- [Semgrep Documentation](https://semgrep.dev/docs/)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [Dependabot Documentation](https://docs.github.com/en/code-security/dependabot)

### Internal Documentation
- `docs/security-scanning.md` - Current security scan setup
- `.github/workflows/security-scan.yml` - Security workflow configuration

---

## Appendix A: GitHub Plans Comparison

| Feature | Free | Pro ($7/user/month) | Team ($4/user/month) | Enterprise |
|---------|------|---------------------|----------------------|------------|
| **Actions Minutes (Private)** | 2,000 | 3,000 | 3,000 | 50,000 |
| **Actions Storage** | 500 MB | 1 GB | 2 GB | 50 GB |
| **Advanced Security** | ❌ | ➕ $49/user | ➕ $49/user | Included |
| **Collaborators (Private)** | Unlimited | Unlimited | Unlimited | Unlimited |
| **Branch Protection** | ✅ | ✅ | ✅ | ✅ |
| **Code Owners** | ❌ | ✅ | ✅ | ✅ |
| **Draft PRs** | ✅ | ✅ | ✅ | ✅ |
| **Multiple PR Reviewers** | ❌ | ✅ | ✅ | ✅ |

**Recommendation for MeepleAI**:
- **Free**: Sufficient for solo/small team, use Semgrep for security
- **Pro**: Only if you need >2,000 Actions minutes/month
- **Advanced Security**: Only if security compliance requires CodeQL

---

## Appendix B: Semgrep Configuration Example

**File**: `.semgrep.yml` (custom rules for MeepleAI)

```yaml
rules:
  - id: detect-hardcoded-openrouter-key
    pattern: |
      "sk-or-v1-..."
    message: Hardcoded OpenRouter API key detected
    severity: ERROR
    languages: [csharp, javascript, typescript]

  - id: detect-sql-injection-aspnet
    pattern: |
      $DB.ExecuteSqlRaw($SQL + ...)
    message: Potential SQL injection via string concatenation
    severity: ERROR
    languages: [csharp]

  - id: detect-xss-react
    pattern: |
      dangerouslySetInnerHTML={{ __html: $USER_INPUT }}
    message: Potential XSS via dangerouslySetInnerHTML
    severity: WARNING
    languages: [javascript, typescript]
```

---

**End of Guide**

For questions or issues during migration, contact the DevOps team or create an issue in the repository.
