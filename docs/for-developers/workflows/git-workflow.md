# Git Workflow - MeepleAI

**Team**: 1 Developer • **Strategy**: Three-tier (main → main-staging → main-dev)

---

## Branch Strategy

| Branch | Purpose | Protection | Deploy | Updates |
|--------|---------|------------|--------|---------|
| **🔴 main** | Production stable | Max (PR from staging only) | Production | PR from `main-staging` |
| **🟡 main-staging** | Release candidate | Medium (CI/CD required) | Staging (auto) | PR/commit/push from `main-dev` |
| **🔵 main-dev** | Active dev | Minimal (lint/type) | Dev (optional) | Feature branches, commits |

### Protection Rules

| Feature | main | main-staging | main-dev |
|---------|------|--------------|----------|
| Direct push | ❌ | ✅ | ✅ |
| Force push | ❌ | ⚠️ With lease | ⚠️ With lease |
| PR required | ✅ From staging | ❌ | ❌ |
| Status checks | All CI/CD ✅ | Build+Tests+Security ✅ | Lint+Typecheck ✅ |

**main-staging → main Quality Gates**: Backend 90% coverage, Frontend 85%, Integration tests, Security scan, Performance check (optional)

**Child Branches**: `frontend-dev`, `backend-dev` → merge into `main-dev`

---

## Daily Workflows

### Feature Development

```bash
# 1. Start from main-dev
git checkout main-dev && git pull && git checkout -b feature/issue-123-search

# 2. Develop + commit
git add . && git commit -m "feat(game): add complexity filter"

# 3. Test locally
cd apps/api/src/Api && dotnet test
cd apps/web && pnpm typecheck && pnpm lint && pnpm test

# 4. Merge to main-dev (solo dev, no PR)
git push -u origin feature/issue-123-search
git checkout main-dev && git merge feature/issue-123-search && git push

# 5. Cleanup
git branch -D feature/issue-123-search && git push origin --delete feature/issue-123-search
```

**Shortcuts**: Direct commit to `main-dev` (small changes) or use `frontend-dev`/`backend-dev` for isolation

### Release to Staging

```bash
# 1. Test locally
cd apps/api/src/Api && dotnet test && cd apps/web && pnpm test && pnpm test:e2e

# 2. Merge to staging (triggers CI/CD)
git checkout main-staging && git pull && git merge main-dev --no-ff -m "chore: promote to staging"
git push origin main-staging

# 3. Validate staging environment (smoke tests, feature checks)
```

**Cherry-pick** (if `main-dev` has experimental code):
```bash
git checkout main-staging && git cherry-pick abc123f def456a && git push
```

### Production Release

```bash
# 1. Create PR: main-staging → main
gh pr create --base main --head main-staging --title "Release v1.2.0" --body "Summary + Checklist + Coverage"

# 2. Merge PR (GitHub UI) → auto-deploys

# 3. Tag release
git checkout main && git pull && git tag -a v1.2.0 -m "Release v1.2.0" && git push origin v1.2.0

# 4. Sync main-dev
git checkout main-dev && git merge main --no-ff -m "chore: sync with production v1.2.0" && git push
```

### Hotfix

```bash
# 1. Branch from main
git checkout main && git pull && git checkout -b hotfix/critical-bug

# 2. Fix + test
git commit -m "fix(auth): prevent race condition" && dotnet test && pnpm test

# 3. PR to staging first
git push -u origin hotfix/critical-bug
gh pr create --base main-staging --head hotfix/critical-bug

# 4. Validate staging → Fast-track to main
gh pr create --base main --head main-staging --title "Hotfix: Auth Fix"

# 5. Backport to main-dev
git checkout main-dev && git cherry-pick <hotfix-sha> && git push

# 6. Cleanup
git branch -D hotfix/critical-bug && git push origin --delete hotfix/critical-bug
```

**Super-Urgent** (bypass staging, only for outages): Hotfix → main directly, then backport to staging + dev

---

## CI/CD Workflows

### main-dev CI (`.github/workflows/main-dev-ci.yml`)

**Trigger**: Push to `main-dev`, `frontend-dev`, `backend-dev`

| Job | Steps | Blocking |
|-----|-------|----------|
| backend-quality | Lint (`dotnet format --verify`), Unit tests | ❌ Non-blocking tests |
| frontend-quality | Lint, Typecheck (`pnpm lint && pnpm typecheck`) | ✅ Blocking |

### main-staging CI/CD (`.github/workflows/main-staging-ci.yml`)

**Trigger**: Push to `main-staging`

| Job | Steps | Gate |
|-----|-------|------|
| backend-full-suite | Build, Test + Coverage, Security scan (Semgrep) | ✅ 90% coverage threshold |
| frontend-full-suite | Build, Test + Coverage, E2E, Security scan | ✅ 85% coverage threshold |
| deploy-staging | Deploy to staging environment | Depends on both suites passing |

### main Production (`.github/workflows/main-production-ci.yml`)

**Trigger**: PR to `main` (must be from `main-staging`)

| Job | Validation |
|-----|------------|
| validate-pr-source | Error if head ≠ `main-staging` |
| production-checks | Verify staging CI/CD passed |
| deploy-production | Deploy on PR merge + Health check |

---

## GitHub Protection Setup

### main
```
☑️ Require PR (from main-staging only)
☑️ Require status checks: backend-full-suite, frontend-full-suite, validate-pr-source
☑️ Require signed commits (optional), linear history, conversation resolution
☑️ Include administrators
☐ Force push (NEVER), deletions (NEVER)
```

### main-staging
```
☑️ Require status checks: backend-full-suite, frontend-full-suite
☐ Require PR (allow direct commits)
☑️ Allow force push with lease (cleanup only)
```

### main-dev
```
☑️ Require status checks: backend-quality, frontend-quality
☐ Require PR (allow direct commits)
☑️ Allow force push with lease
```

---

## Common Scenarios

| Scenario | Command |
|----------|---------|
| **Quick fix on dev** | `git checkout main-dev && git add . && git commit -m "fix: ..." && git push` |
| **Multi-day feature** | Day 1: `git checkout -b feature/x main-dev && git push -u origin feature/x`<br>Day N: Merge to `main-dev` + cleanup |
| **Rollback production** | Revert: `git checkout main && git revert <sha> && git push`<br>Tag: `git reset --hard v1.1.0 && git push --force-with-lease` (emergency only) |
| **Sync after drift** | `git checkout main-dev && git merge origin/main --no-ff && git push` |
| **Merge conflicts** | `git merge main-dev` → resolve → `git add . && git commit && git push` |
| **CI/CD failing** | Fix forward: `git commit -m "fix(ci): ..." && git push`<br>Rollback: `git reset --hard HEAD~1 && git push --force-with-lease` |

**Always**: Sync `main-staging` + `main-dev` after rollback.

---

## Automation

### Helper Script (`scripts/git-workflow.sh`)

```bash
#!/bin/bash
case "$1" in
  "feature") git checkout main-dev && git pull && git checkout -b "feature/$2" ;;
  "staging") git checkout main-staging && git merge main-dev --no-ff -m "chore: promote" && git push ;;
  "release") gh pr create --base main --head main-staging --title "Release $2" --body "Checklist..." ;;
  "sync") git checkout main-dev && git merge main --no-ff -m "chore: sync" && git push ;;
esac
```

**Usage**: `./scripts/git-workflow.sh feature add-filter`, `./scripts/git-workflow.sh staging`, `./scripts/git-workflow.sh release v1.2.0`

### Auto-Sync Workflow (`.github/workflows/sync-branches.yml`)

**Trigger**: Push to `main` → Auto-merge `main` → `main-dev`

---

## Quality Checklists

### Before main-staging
- [ ] Unit + integration tests pass (`dotnet test`, `pnpm test`)
- [ ] No console errors, migrations tested, secrets updated (if needed)

### Before main (Production)
- [ ] Staging validated (smoke tests), all CI/CD green
- [ ] Rollback plan in PR, alerts configured, docs updated, migrations backward-compatible

---

## Commit Format (Conventional Commits)

**Pattern**: `<type>(<scope>): <subject>`

**Types**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`, `ci`, `style`, `build`, `revert`

**Examples**:
```bash
feat(game): add complexity filter
fix(auth): prevent race condition
docs(workflow): update guide
```

### Branch Naming

**Pattern**: `<type>/<scope>-<issue>-<description>`

**Examples**: `feature/issue-123-search`, `fix/issue-456-auth-bug`, `hotfix/critical-db`, `refactor/simplify-rag`

---

## Reference

**Flow Diagram**:
```
feature/* → main-dev 🔵 → main-staging 🟡 → PR+CI/CD → main 🔴 → v1.x.x
hotfix/* -.-> main (emergency) -.-> backport staging+dev
main -.-> auto-sync main-dev
```

**Legend**: ✅ Allowed | ❌ Blocked | ⚠️ With lease (caution)

---

**Updated**: 2026-01-24 • **Version**: 2.0 • **Team**: Development
