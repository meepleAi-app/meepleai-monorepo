# 🚀 MeepleAI MVP - Quick Start Guide

**Last Updated**: 2025-01-15
**Time to Complete**: ~15 minutes

---

## ⚡ Fast Track Setup (5 Commands)

```bash
# 1. Generate GitHub Issues (28 issues for Sprint 1-5)
bash tools/generate-mvp-issues.sh

# 2. Setup Codecov (add token to GitHub secrets first!)
# Visit: https://app.codecov.io/gh/YOUR_ORG/meepleai-monorepo
# Copy token → GitHub Settings → Secrets → Actions → New: CODECOV_TOKEN

# 3. Push CI/CD pipeline
git add .github/workflows/test-automation-mvp.yml .codecov.yml
git commit -m "ci: add test automation pipeline with 90% coverage gates"
git push origin refactor/ddd-phase1-foundation

# 4. Trigger pipeline manually (verify setup)
gh workflow run test-automation-mvp.yml

# 5. View results
gh run list --workflow=test-automation-mvp.yml
```

**Expected Result**: 28 issues created, CI pipeline running, coverage gates enforced.

---

## 📋 Prerequisites Check

```bash
# Check GitHub CLI
gh --version  # Should be >= 2.40.0

# Check authentication
gh auth status  # Should show "Logged in to github.com"

# Check repository access
gh repo view  # Should show meepleai-monorepo details

# Check .NET
dotnet --version  # Should be 9.0.x

# Check Node.js
node --version  # Should be 20.x

# Check pnpm
pnpm --version  # Should be 9.x
```

**All checks passing?** ✅ Proceed to next section.

**Some checks failing?** ❌ Install missing tools:
- GitHub CLI: https://cli.github.com/
- .NET 9: https://dotnet.microsoft.com/download
- Node.js 20: https://nodejs.org/
- pnpm 9: `npm install -g pnpm@9`

---

## 🎯 Step-by-Step Setup

### 1. Generate GitHub Issues (2 minutes)

```bash
cd D:\Repositories\meepleai-monorepo

# Linux/macOS/WSL
bash tools/generate-mvp-issues.sh

# Windows PowerShell
pwsh tools/generate-mvp-issues.ps1
```

**What this does**:
- Creates 28 issues across 5 sprints + 3 CI/CD issues
- Assigns milestones (Sprint 1-5)
- Adds labels (sprint-X, backend, frontend, testing, etc.)
- Includes detailed task lists and acceptance criteria

**Verify**:
```bash
gh issue list --milestone "MVP Sprint 1"  # Should show 5 issues
gh issue list | wc -l  # Should show 28 total issues
```

---

### 2. Setup Codecov Integration (3 minutes)

#### 2.1. Activate Repository on Codecov

1. Visit: https://app.codecov.io/gh/YOUR_ORG/meepleai-monorepo
2. Click "Activate repository"
3. Copy the `CODECOV_TOKEN` (looks like: `1234abcd-5678-efgh-9012-ijklmnopqrst`)

#### 2.2. Add Token to GitHub Secrets

**Option A: Via Web UI**
1. Go to: https://github.com/YOUR_ORG/meepleai-monorepo/settings/secrets/actions
2. Click "New repository secret"
3. Name: `CODECOV_TOKEN`
4. Value: (paste token from Codecov)
5. Click "Add secret"

**Option B: Via GitHub CLI**
```bash
gh secret set CODECOV_TOKEN --body "YOUR_TOKEN_HERE"
```

**Verify**:
```bash
gh secret list  # Should show CODECOV_TOKEN
```

---

### 3. Push CI/CD Configuration (2 minutes)

```bash
# Stage files
git add .github/workflows/test-automation-mvp.yml
git add .codecov.yml
git add tools/generate-mvp-issues.sh
git add tools/generate-mvp-issues.ps1
git add claudedocs/test_automation_strategy_2025.md
git add claudedocs/mvp_implementation_plan.md
git add claudedocs/EXECUTIVE_SUMMARY.md
git add QUICK_START_MVP.md

# Commit
git commit -m "ci: add comprehensive test automation pipeline

- Add GitHub Actions workflow with parallel execution
- Configure Codecov with 90% coverage gates
- Generate 28 issues for Sprint 1-5 MVP
- Add complete test automation strategy (15K words)
- Setup issue generation scripts (bash + PowerShell)
- Target: <10 min CI pipeline, 90%+ coverage"

# Push
git push origin refactor/ddd-phase1-foundation
```

**Verify**:
```bash
git log -1 --oneline  # Should show commit message
git push --dry-run    # Should show "Everything up-to-date" after push
```

---

### 4. Verify CI Pipeline (5 minutes)

#### 4.1. Trigger Workflow Manually

```bash
gh workflow run test-automation-mvp.yml
```

#### 4.2. Monitor Execution

```bash
# View running workflows
gh run list --workflow=test-automation-mvp.yml

# Get latest run ID
RUN_ID=$(gh run list --workflow=test-automation-mvp.yml --limit 1 --json databaseId --jq '.[0].databaseId')

# Watch live progress
gh run watch $RUN_ID

# View logs (after completion)
gh run view $RUN_ID --log
```

#### 4.3. Check Results

```bash
# View summary
gh run view $RUN_ID

# Expected output:
# ✓ backend-unit (2m 15s)
# ✓ backend-integration (5m 30s)
# ✓ frontend-unit (2m 10s)
# ✓ e2e-tests-chromium-1 (2m 30s)
# ✓ e2e-tests-chromium-2 (2m 40s)
# ... (12 E2E jobs total)
# ✓ coverage-gate (1m 0s)
# ✓ test-report (30s)
```

**Pipeline Success?** ✅ Proceed to next section.
**Pipeline Failed?** ❌ Check troubleshooting section below.

---

### 5. Setup Project Board (3 minutes)

```bash
# Create project board (via web UI)
# Go to: https://github.com/YOUR_ORG/meepleai-monorepo/projects/new

# Or via CLI (requires gh project extension)
gh project create --owner YOUR_ORG --title "MeepleAI MVP" --type table

# Link issues to project
gh issue list --milestone "MVP Sprint 1" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-project "MeepleAI MVP"
```

**Expected Board Columns**:
- 📋 Backlog
- 🔄 In Progress
- 👀 In Review
- ✅ Done

---

## 🎯 Sprint 1 Kickoff Checklist

### Pre-Sprint Planning

- [ ] All 28 issues created and reviewed
- [ ] CI/CD pipeline passing (green checkmarks)
- [ ] Codecov integration working (coverage reports visible)
- [ ] Project board setup with Sprint 1 issues
- [ ] Team members assigned to issues
- [ ] Development environment verified (dotnet, node, pnpm working)

### Sprint 1 Goals (Week 1-2)

**Focus**: Authentication & Settings

**Issues to Complete** (5 total):
1. ✅ OAuth Integration Complete (already done, add tests)
2. 🆕 2FA/TOTP Management UI (new frontend)
3. 🆕 Settings Pages - 4 Tabs Implementation (new frontend)
4. 🆕 User Profile Management Service (new backend)
5. 🧪 Unit Test Suite - Authentication Module (new tests)

**Definition of Done**:
- [ ] All features implemented and working
- [ ] 95%+ test coverage for new code
- [ ] All tests passing in CI
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] No critical bugs in staging

---

## 🧪 Local Testing Workflow

### Backend Tests

```bash
cd apps/api

# Run all tests
dotnet test

# Run unit tests only
dotnet test --filter "Category=Unit"

# Run integration tests only
dotnet test --filter "Category=Integration"

# Run specific test
dotnet test --filter "FullyQualifiedName~OAuthServiceTests.HandleCallbackAsync"

# Run with coverage
dotnet test /p:CollectCoverage=true

# Generate HTML coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
reportgenerator -reports:"coverage.info" -targetdir:"coverage-report" -reporttypes:Html
```

### Frontend Tests

```bash
cd apps/web

# Run all tests
pnpm test

# Watch mode (re-run on changes)
pnpm test:watch

# Coverage report
pnpm test:coverage
open coverage/index.html

# E2E tests (interactive)
pnpm test:e2e:ui

# E2E tests (headless)
pnpm test:e2e

# E2E tests (debug mode)
pnpm test:e2e:debug
```

---

## 📊 Coverage Monitoring

### View Coverage Locally

```bash
# Backend
cd apps/api
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=html
open coverage/index.html

# Frontend
cd apps/web
pnpm test:coverage
open coverage/index.html
```

### View Coverage on Codecov

```bash
# Open Codecov dashboard
open "https://app.codecov.io/gh/YOUR_ORG/meepleai-monorepo"

# Or via CLI (requires codecov CLI)
codecov --help
```

### Coverage Requirements

| Component | Target | Gate |
|-----------|--------|------|
| Backend Unit | 95%+ | Hard fail if <90% |
| Backend Integration | 90%+ | Hard fail if <85% |
| Frontend Unit | 91%+ | Hard fail if <90% |
| E2E Critical Paths | 85%+ | Hard fail if <80% |
| **Overall Project** | **90%+** | **Hard fail** |

---

## 🚨 Troubleshooting

### Issue Generation Fails

**Error**: `gh: command not found`
**Fix**: Install GitHub CLI from https://cli.github.com/

**Error**: `HTTP 401: Bad credentials`
**Fix**: Re-authenticate with `gh auth logout && gh auth login`

**Error**: `milestone "MVP Sprint 1" not found`
**Fix**: Create milestones manually first:
```bash
gh api repos/YOUR_ORG/meepleai-monorepo/milestones -f title="MVP Sprint 1" -f due_on="2025-02-15T00:00:00Z"
```

---

### CI Pipeline Fails

**Error**: `CODECOV_TOKEN not found`
**Fix**: Add token to GitHub secrets (see Step 2 above)

**Error**: `Tests failed - coverage below 90%`
**Fix**: Write more tests to increase coverage
```bash
# Find uncovered code
dotnet test /p:CollectCoverage=true
# Look at coverage-report/index.html for uncovered lines
```

**Error**: `E2E tests timeout`
**Fix**: Increase timeout in playwright.config.ts
```typescript
timeout: 30000,  // 30 seconds per test
```

---

### Coverage Below Threshold

**Problem**: Coverage report shows 85% (below 90% gate)

**Solution**:
1. Identify uncovered code
   ```bash
   dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=html
   open coverage/index.html  # Look for red/yellow lines
   ```

2. Write missing tests
   ```csharp
   [Fact]
   public async Task UncoveredMethod_WithValidInput_ReturnsExpectedResult()
   {
       // Arrange
       var input = "test";

       // Act
       var result = await _sut.UncoveredMethod(input);

       // Assert
       result.Should().NotBeNull();
   }
   ```

3. Run tests again
   ```bash
   dotnet test /p:CollectCoverage=true
   ```

---

## 📚 Documentation Reference

### Quick Links

- **Test Strategy**: [claudedocs/test_automation_strategy_2025.md](./claudedocs/test_automation_strategy_2025.md)
- **Implementation Plan**: [claudedocs/mvp_implementation_plan.md](./claudedocs/mvp_implementation_plan.md)
- **Executive Summary**: [claudedocs/EXECUTIVE_SUMMARY.md](./claudedocs/EXECUTIVE_SUMMARY.md)
- **Product Spec**: [claudedocs/meepleai_complete_specification.md](./claudedocs/meepleai_complete_specification.md)
- **Roadmap**: [claudedocs/roadmap_meepleai_evolution_2025.md](./claudedocs/roadmap_meepleai_evolution_2025.md)

### External Resources

- [xUnit Docs](https://xunit.net/)
- [Jest Docs](https://jestjs.io/)
- [Playwright Docs](https://playwright.dev/)
- [Testcontainers Docs](https://dotnet.testcontainers.org/)
- [Codecov Docs](https://docs.codecov.com/)

---

## ✅ Success Criteria

### Setup Complete When:

- [x] GitHub CLI installed and authenticated
- [x] 28 issues created with milestones and labels
- [x] Codecov token added to GitHub secrets
- [x] CI/CD pipeline pushed and passing
- [x] Project board setup with Sprint 1 issues
- [x] Team members assigned to issues
- [x] Development environment verified

**All checkboxes marked?** ✅ You're ready to start Sprint 1!

---

## 🚀 Start Sprint 1

```bash
# Create feature branch
git checkout -b feature/sprint-1-auth

# Pick first issue
gh issue list --milestone "MVP Sprint 1" --assignee @me

# Start with TDD (write test first)
cd apps/api/tests/Api.Tests
# Create test file, write failing test

# Implement feature
cd ../../src/Api/Services
# Write service code to pass test

# Run tests
dotnet test --watch

# Commit when tests pass
git add .
git commit -m "feat(auth): implement OAuth unit tests

- Add OAuthService unit tests (95% coverage)
- Test all 3 providers (Google, Discord, GitHub)
- Test CSRF protection
- Test token encryption

Closes #<issue-number>"

# Push and create PR
git push origin feature/sprint-1-auth
gh pr create --title "Sprint 1: OAuth Integration Tests" --body "Closes #<issue-number>"
```

**Happy coding!** 🎉

---

**Document Status**: ✅ Ready for Team Use
**Last Updated**: 2025-01-15
**Estimated Setup Time**: 15 minutes
**Support**: Open GitHub Discussion or contact dev-team@meepleai.com

