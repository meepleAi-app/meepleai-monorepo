# Quick Start Guide - 3-Tier Git Workflow

**Setup Time**: 2 minutes
**Audience**: Solo developer on MeepleAI project

---

## ✅ Setup Completed

You're all set! The 3-tier workflow is now active:

- 🔴 **main** (Production): Protected, PR-only from main-staging
- 🟡 **main-staging** (Pre-Production): Protected, CI/CD required
- 🔵 **main-dev** (Development): Minimal protection, agile

---

## 🚀 Daily Workflow Commands

### **Start New Feature**
```bash
# Switch to main-dev and create feature branch
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-123-add-search

# Or use helper:
./scripts/git-workflow.sh feature issue-123-add-search
```

### **Work and Commit**
```bash
# Make changes
git add .
git commit -m "feat(game): add complexity filter"

# Push frequently
git push -u origin feature/issue-123-add-search
```

### **Merge to Development**
```bash
# Manual merge
git checkout main-dev
git merge feature/issue-123-add-search --no-ff
git push origin main-dev
git branch -D feature/issue-123-add-search

# Or use helper (all-in-one):
./scripts/git-workflow.sh merge issue-123-add-search
```

### **Promote to Staging**
```bash
# Manual promotion
git checkout main-staging
git pull origin main-staging
git merge main-dev --no-ff -m "chore(release): promote to staging"
git push origin main-staging

# Or use helper (with optional tests):
./scripts/git-workflow.sh staging
```

### **Release to Production**
```bash
# Create release PR
gh pr create --base main --head main-staging \
  --title "Release v1.2.0" \
  --body "Release summary..."

# Or use helper:
./scripts/git-workflow.sh release v1.2.0 "Game Search Feature"

# After PR merge, tag release:
./scripts/git-workflow.sh tag v1.2.0 "Game Search Release"
```

---

## 📊 Quick Status Check

```bash
# Check branch status and recent commits
./scripts/git-workflow.sh status

# Output example:
# 📍 Current Branch: main-dev
# 🌿 Branch Status:
#   main: ✅ Synced
#   main-staging: ✅ Synced
#   main-dev: ✅ Synced
# 🔀 Recent Commits (main-dev): [last 5 commits]
# 📦 Uncommitted Changes: [status]
```

---

## 🔥 Emergency Hotfix

```bash
# Create hotfix from production
./scripts/git-workflow.sh hotfix critical-bug

# Fix the issue
git add . && git commit -m "fix: critical bug"

# Test locally
cd apps/api/src/Api && dotnet test
cd apps/web && pnpm test

# Merge to staging first, then main
./scripts/git-workflow.sh hotfix-merge critical-bug

# Follow PR prompts to complete
```

---

## 🎯 Most Common Scenarios

### **Scenario 1: Quick Fix (< 30 min work)**
```bash
# Direct commit to main-dev
git checkout main-dev
git pull
# ... fix issue ...
git add . && git commit -m "fix(api): null check in game service"
git push origin main-dev
```

### **Scenario 2: Multi-Day Feature**
```bash
# Day 1: Start
git checkout -b feature/complex-ai main-dev
git commit -m "feat(ai): add retrieval logic"
git push -u origin feature/complex-ai

# Day 2: Continue
git checkout feature/complex-ai
git pull  # Sync if multi-machine
git commit -m "feat(ai): add reranking"
git push

# Day 3: Complete
git checkout main-dev
git merge feature/complex-ai --no-ff
git push origin main-dev
git branch -D feature/complex-ai
```

### **Scenario 3: Weekly Release Cycle**
```bash
# Monday-Thursday: Develop on main-dev
# ... feature work ...

# Friday: Promote to staging
./scripts/git-workflow.sh staging
# Validate staging environment over weekend

# Monday: Release to production
./scripts/git-workflow.sh release v1.x.0
# Review PR, merge when ready
./scripts/git-workflow.sh tag v1.x.0
```

---

## 🔍 Verification Commands

### **Check Branch Protection**
```bash
# Via GitHub API
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection \
  | jq '{pr_required: .required_pull_request_reviews.required_approving_review_count, checks: .required_status_checks.contexts}'
```

### **Monitor CI/CD**
```bash
# Check PR status
gh pr view 3028 --json statusCheckRollup \
  | jq '.statusCheckRollup[] | {name, status, conclusion}'

# Watch workflow runs
gh run list --branch main-staging --limit 5
```

### **Git Status Overview**
```bash
# All branches
git branch -a

# Recent activity
git log --oneline --graph --all -10

# Divergence check
git log main..main-staging --oneline
git log main-staging..main-dev --oneline
```

---

## ⚡ Power User Tips

### **Aliases (Add to ~/.gitconfig)**
```ini
[alias]
    # Quick workflow shortcuts
    fd = checkout main-dev
    fs = checkout main-staging
    fm = checkout main

    # Status shortcuts
    st = status -sb
    lg = log --oneline --graph --all -10

    # Safe operations
    pushf = push --force-with-lease
    pullr = pull --rebase
```

### **Bash Aliases (Add to ~/.bashrc or ~/.bash_profile)**
```bash
# Navigate to repo
alias meeple='cd /d/Repositories/meepleai-monorepo-frontend'

# Workflow shortcuts
alias gwf='./scripts/git-workflow.sh'
alias gwf-status='./scripts/git-workflow.sh status'
alias gwf-staging='./scripts/git-workflow.sh staging'
```

**Usage after aliases**:
```bash
meeple                    # Jump to repo
gwf feature add-search    # Create feature
gwf-status                # Check status
gwf-staging               # Promote to staging
```

---

## 📝 Cheat Sheet

| Action | Command |
|--------|---------|
| **Create feature** | `git checkout -b feature/NAME main-dev` |
| **Commit** | `git add . && git commit -m "TYPE: message"` |
| **Merge to dev** | `git checkout main-dev && git merge feature/NAME` |
| **Promote to staging** | `git checkout main-staging && git merge main-dev` |
| **Release PR** | `gh pr create --base main --head main-staging` |
| **Check status** | `./scripts/git-workflow.sh status` |
| **Emergency hotfix** | `git checkout -b hotfix/NAME main` |

---

## 🚨 Common Issues

### **Issue: "Required status check expected"**
**Cause**: Push bypassed protection (admin privilege)
**Fix**: This is normal for solo dev. CI still runs.

### **Issue: Merge conflicts on staging**
**Solution**:
```bash
git checkout main-staging
git merge main-dev
# Resolve conflicts in editor
git add .
git commit
git push origin main-staging
```

### **Issue: CI failing on staging**
**Quick fix**:
```bash
# Fix on staging directly
git checkout main-staging
# ... fix ...
git commit -m "fix(ci): resolve issue"
git push origin main-staging
```

### **Issue: Forgot to sync after production release**
```bash
./scripts/git-workflow.sh sync
# Or wait for auto-sync workflow to run
```

---

## 📚 Full Documentation

- **Complete Workflow**: `docs/02-development/git-workflow.md`
- **Branch Protection Setup**: `docs/02-development/branch-protection-setup.md`
- **Helper Script Source**: `scripts/git-workflow.sh`

---

## ✅ You're Ready!

Your 3-tier workflow is configured and operational.

**Next Steps**:
1. ✅ Start developing with `git checkout main-dev`
2. ✅ Use helper script: `./scripts/git-workflow.sh help`
3. ✅ Monitor PR #3028 for first production release

**Questions?** Check full documentation or open GitHub issue.

---

**Last Updated**: 2026-01-24
**Version**: 1.0
