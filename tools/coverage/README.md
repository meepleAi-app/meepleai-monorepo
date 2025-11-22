# Coverage & Testing Scripts

Scripts for running tests with code coverage analysis and tracking coverage trends.

## Scripts

### 🧪 **run-backend-coverage.sh**
**Purpose:** Run .NET backend tests with code coverage collection

**What it does:**
1. Runs `dotnet test` with coverage collection enabled (`--collect:"XPlat Code Coverage"`)
2. Generates `coverage.cobertura.xml` in each test project
3. Optionally generates HTML reports via `reportgenerator`
4. Validates coverage meets threshold (default: 90%)
5. Opens HTML report in browser (with `--open` flag)

**Usage:**
```bash
# Run tests with coverage
bash tools/coverage/run-backend-coverage.sh

# Generate HTML report
bash tools/coverage/run-backend-coverage.sh --html

# Generate and open HTML report
bash tools/coverage/run-backend-coverage.sh --open

# Set custom threshold
bash tools/coverage/run-backend-coverage.sh --threshold 85
```

**Output:**
- Coverage XML: `apps/api/tests/*/TestResults/*/coverage.cobertura.xml`
- HTML Report: `apps/api/coverage-report/index.html`
- Console: Coverage % per assembly

**Who uses it:** Developers, CI/CD pipeline
**When:** Before committing, during PR review, in CI builds
**Requirements:** .NET SDK 9, `reportgenerator` (optional for HTML)

---

### 🐳 **run-backend-coverage-docker.sh**
**Purpose:** Run backend tests with coverage inside Docker container (isolated environment)

**What it does:**
1. Builds test Docker image with .NET SDK
2. Runs tests inside container (consistent environment)
3. Extracts coverage reports from container to host
4. Uses Testcontainers for database dependencies

**Usage:**
```bash
# Run tests in Docker
bash tools/coverage/run-backend-coverage-docker.sh

# With HTML report generation
bash tools/coverage/run-backend-coverage-docker.sh --html
```

**Who uses it:**
- CI/CD pipeline (ensures consistent test environment)
- Developers without local .NET SDK installed
- Testing against specific .NET SDK version

**When:** In CI, troubleshooting environment-specific test failures
**Requirements:** Docker Desktop, Docker Compose

---

### 🌐 **run-frontend-coverage.sh**
**Purpose:** Run Next.js/React frontend tests with coverage using Jest

**What it does:**
1. Runs `pnpm test --coverage` in `apps/web/`
2. Generates coverage reports (HTML, LCOV, JSON)
3. Enforces coverage thresholds (90%+ for statements, branches, functions, lines)
4. Outputs coverage summary to console

**Usage:**
```bash
# Run frontend tests with coverage
bash tools/coverage/run-frontend-coverage.sh

# Run in watch mode (development)
cd apps/web && pnpm test:watch
```

**Output:**
- Coverage HTML: `apps/web/coverage/lcov-report/index.html`
- Coverage Data: `apps/web/coverage/coverage-final.json`
- LCOV: `apps/web/coverage/lcov.info` (for SonarQube/Codecov)

**Who uses it:** Frontend developers, CI/CD pipeline
**When:** Before PR, during development, in CI builds
**Requirements:** Node.js 20+, pnpm

---

### 📊 **coverage-trends.sh** & **coverage-trends.ps1**
**Purpose:** Track code coverage over time and detect regressions

**What it does:**
1. Parses coverage reports (backend Cobertura XML + frontend JSON)
2. Extracts line/branch coverage percentages
3. Compares against previous baseline
4. Stores historical data in `coverage-history.json`
5. Alerts if coverage drops below threshold

**Usage:**
```bash
# Track backend coverage trend
bash tools/coverage/coverage-trends.sh

# PowerShell version (Windows)
.\tools\coverage\coverage-trends.ps1
```

**Output:**
```
Backend Coverage: 92.3% (lines), 89.7% (branches)
Frontend Coverage: 91.5% (statements), 90.1% (branches)
Trend: ↑ +1.2% since last run
```

**Who uses it:** Tech leads monitoring code quality, CI/CD for trend alerts
**When:** After major refactoring, weekly metrics review, in CI summary
**Requirements:** `jq` (for bash version), coverage reports already generated

---

### 📏 **measure-coverage.ps1**
**Purpose:** PowerShell wrapper for measuring coverage on Windows

**What it does:**
- Runs backend tests with coverage
- Parses Cobertura XML reports
- Formats output for PowerShell pipeline

**Usage:**
```powershell
# Measure coverage (Windows)
.\tools\coverage\measure-coverage.ps1

# Get coverage percentage only
(.\tools\coverage\measure-coverage.ps1).LinePercentage
```

**Who uses it:** Windows developers without WSL/bash
**When:** Alternative to `run-backend-coverage.sh` on Windows
**Requirements:** PowerShell 5.1+, .NET SDK 9

---

### 🔧 **refactor-test-isolation.sh**
**Purpose:** Utility for refactoring test fixtures to improve isolation

**Status:** ⚠️ **Specialized Tool** - Use only during test architecture refactoring

**What it does:**
- Analyzes test classes for shared state issues
- Suggests fixtures to move to setup/teardown
- Identifies tests that modify global state

**Usage:**
```bash
# Analyze test isolation issues
bash tools/coverage/refactor-test-isolation.sh apps/api/tests/

# Generate refactoring report
bash tools/coverage/refactor-test-isolation.sh --report
```

**Who uses it:** Senior developers refactoring test suite
**When:** During test suite cleanup sprints, when flaky tests are detected
**Note:** Run sparingly, not part of regular workflow

---

## Coverage Requirements

### Backend (C# - xUnit)
- **Target:** 90%+ line coverage, 85%+ branch coverage
- **Excluded:** Generated code, migrations, program.cs
- **Tools:** coverlet, reportgenerator

### Frontend (TypeScript - Jest)
- **Target:** 90%+ statements, branches, functions, lines
- **Excluded:** `.next/`, `node_modules/`, test files
- **Tools:** Jest built-in coverage via Istanbul

---

## CI/CD Integration

### GitHub Actions Workflow
```yaml
jobs:
  coverage:
    runs-on: ubuntu-latest
    steps:
      - name: Backend Coverage
        run: bash tools/coverage/run-backend-coverage.sh --threshold 90

      - name: Frontend Coverage
        run: bash tools/coverage/run-frontend-coverage.sh

      - name: Track Trends
        run: bash tools/coverage/coverage-trends.sh
```

### Coverage Reports Storage
- **Backend:** `apps/api/coverage-report/`
- **Frontend:** `apps/web/coverage/lcov-report/`
- **History:** `coverage-history.json` (gitignored)

---

## Troubleshooting

**"reportgenerator not found":**
```bash
# Install globally
dotnet tool install -g dotnet-reportgenerator-globaltool

# Or use without HTML reports
bash tools/coverage/run-backend-coverage.sh
```

**Frontend coverage not working:**
```bash
# Clear Jest cache
cd apps/web && pnpm test:clear

# Ensure coverage config in jest.config.js
pnpm test --coverage
```

**Docker tests fail:**
```bash
# Check Docker is running
docker ps

# Rebuild test image
bash tools/coverage/run-backend-coverage-docker.sh --rebuild
```

---

## Best Practices

1. **Run before committing:**
   ```bash
   bash tools/coverage/run-backend-coverage.sh
   bash tools/coverage/run-frontend-coverage.sh
   ```

2. **Monitor trends weekly:**
   ```bash
   bash tools/coverage/coverage-trends.sh
   ```

3. **Don't obsess over 100%** - Focus on critical paths (business logic, API endpoints)

4. **Use Docker in CI** - Ensures consistent test environment

---

**Last Updated:** 2025-11-22
**Maintained by:** Testing team
**See also:** `README-COVERAGE.md`, `README-FRONTEND-COVERAGE.md` in tools/
