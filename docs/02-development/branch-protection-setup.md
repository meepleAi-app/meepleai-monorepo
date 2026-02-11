# Branch Protection Setup - Quick Guide

**Time Required**: ~5 minutes
**Repository**: MeepleAI Monorepo

## Overview

Configurazione protezioni per workflow a 3 livelli:
- 🔴 **main** (Production): Massima protezione
- 🟡 **main-staging** (Pre-Production): Media protezione
- 🔵 **main-dev** (Development): Minima protezione

---

## ⚡ Quick Setup (Copy-Paste)

### 1️⃣ Navigate to Branch Protection Rules

```
GitHub → Settings → Branches → Add branch protection rule
```

---

### 2️⃣ Main Branch (Production)

**Branch name pattern**: `main`

#### Required Settings

**✅ Require a pull request before merging**
- ✅ Require approvals: `1`
- ✅ Dismiss stale pull request approvals when new commits are pushed
- ☐ Require review from Code Owners *(optional)*

**✅ Require status checks to pass before merging**
- ✅ Require branches to be up to date before merging
- **Required status checks** (add these):
  - `validate-source-branch`
  - `ci-success`
  - `frontend / Frontend - Build & Test`
  - `backend / Backend - Build & Test`

**✅ Require conversation resolution before merging**

**✅ Require signed commits** *(recommended)*

**✅ Require linear history**

**✅ Include administrators** *(enforce on yourself)*

**☐ Allow force pushes** *(NEVER)*

**☐ Allow deletions** *(NEVER)*

---

### 3️⃣ Main-Staging Branch (Pre-Production)

**Branch name pattern**: `main-staging`

#### Required Settings

**✅ Require status checks to pass before merging**
- **Required status checks**:
  - `validate-source-branch`
  - `ci-success`
  - `frontend / Frontend - Build & Test`
  - `backend / Backend - Build & Test`

**☐ Require a pull request before merging** *(allow direct commits)*

**✅ Allow force pushes**
- ✅ Specify who can force push
  - **Select**: `Yourself` (or your GitHub username)

**☐ Allow deletions**

---

### 4️⃣ Main-Dev Branch (Development)

**Branch name pattern**: `main-dev`

#### Required Settings

**✅ Require status checks to pass before merging**
- **Required status checks**:
  - `validate-source-branch`
  - `frontend / Frontend - Build & Test` *(non-blocking)*
  - `backend / Backend - Build & Test` *(non-blocking)*

**☐ Require a pull request before merging** *(allow direct commits)*

**✅ Allow force pushes**
- ✅ Specify who can force push
  - **Select**: `Yourself`

**☐ Allow deletions**

---

## 🔍 Verification Checklist

After setup, verify with test PRs:

### Test 1: Wrong Source Branch to Main
```bash
git checkout -b test-wrong-source
git push -u origin test-wrong-source
# Create PR: test-wrong-source → main
# ❌ Should FAIL with: "Branch 'main' can only be updated from 'main-staging'"
```

### Test 2: Correct Flow to Main-Staging
```bash
git checkout main-dev
git checkout -b test-staging-flow
git push -u origin test-staging-flow
# Create PR: test-staging-flow → main-dev
# ❌ Should FAIL (not in allowed patterns)

# Or direct merge
git checkout main-dev
git merge test-staging-flow
git push origin main-dev
# ✅ Should SUCCESS
```

### Test 3: Staging to Main
```bash
git checkout main-staging
git merge main-dev
git push origin main-staging
# Create PR: main-staging → main
# ✅ Should be ALLOWED (after CI passes)
```

---

## 📋 Status Checks Reference

If CI workflow names change, update these status check names:

| Status Check | Workflow | Job |
|--------------|----------|-----|
| `validate-source-branch` | `branch-policy.yml` | `validate-source-branch` |
| `ci-success` | `ci.yml` | `ci-success` |
| `frontend / Frontend - Build & Test` | `ci.yml` | `frontend` |
| `backend / Backend - Build & Test` | `ci.yml` | `backend` |

---

## 🚨 Troubleshooting

### Issue: Can't find status check in dropdown

**Solution**: Status checks only appear after they've run at least once.

1. Make a dummy commit to trigger CI
2. Wait for CI to complete
3. Return to branch protection and add the check

### Issue: Direct push to main-dev blocked

**Check**: "Require pull request" should be **unchecked** for main-dev

### Issue: Force push denied to main-staging

**Check**: "Allow force pushes" enabled + "Specify who can force push" includes your username

---

## 🔄 Updating Rules

To modify rules later:

```
Settings → Branches → [Branch name] → Edit
```

**Important**: Rules apply retroactively to open PRs

---

## 📝 Notes

- **Self-approval**: As solo dev, you approve your own PRs (allowed with 1 approval requirement)
- **Bypass rules**: Admins can bypass, but GitHub logs these events
- **CI failure**: PRs cannot merge if required status checks fail
- **Branch deletion**: Merged branches can be auto-deleted (enable in repo settings)

---

## ✅ Quick Validation

Run this after setup:

```bash
# 1. Check protection status
gh api repos/:owner/:repo/branches/main/protection | jq '.required_status_checks'
gh api repos/:owner/:repo/branches/main-staging/protection | jq '.required_status_checks'
gh api repos/:owner/:repo/branches/main-dev/protection | jq '.required_status_checks'

# 2. Test workflow
./scripts/git-workflow.sh status
```

---

**Last Updated**: 2026-01-24
**Version**: 1.0 (Three-tier setup)
