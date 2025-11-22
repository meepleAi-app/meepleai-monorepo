# Backend Code Coverage Tools

This directory contains scripts for running backend code coverage analysis.

## Available Scripts

### 1. `run-backend-coverage.sh` (Local Execution)

**Requirements**: .NET SDK 9.0 installed locally

**Usage**:
```bash
# Basic usage (90% threshold)
./tools/run-backend-coverage.sh

# Generate HTML report
./tools/run-backend-coverage.sh --html

# Generate and open HTML report in browser
./tools/run-backend-coverage.sh --html --open

# Set custom threshold
./tools/run-backend-coverage.sh --threshold 85

# Help
./tools/run-backend-coverage.sh --help
```

**Features**:
- ✅ Fast execution (uses local .NET SDK)
- ✅ Optional HTML report generation
- ✅ Automatic browser opening
- ✅ Color-coded output
- ✅ Coverage summary display

**Output**:
- Cobertura XML: `apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml`
- HTML Report: `apps/api/tests/Api.Tests/coverage-report/index.html`

---

### 2. `run-backend-coverage-docker.sh` (Docker-based)

**Requirements**: Docker installed and running

**Usage**:
```bash
# Basic usage (90% threshold)
./tools/run-backend-coverage-docker.sh

# Set custom threshold
./tools/run-backend-coverage-docker.sh --threshold 85

# Help
./tools/run-backend-coverage-docker.sh --help
```

**Features**:
- ✅ No .NET SDK required
- ✅ Isolated execution environment
- ✅ Automatic service orchestration (PostgreSQL, Qdrant, Redis)
- ✅ Cross-platform compatibility

**Output**:
- Cobertura XML: `apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml`

**Note**: Use the local script's `--html` option with ReportGenerator to generate HTML reports from the XML output.

---

## Quick Start

### If you have .NET SDK installed:
```bash
./tools/run-backend-coverage.sh --html --open
```

### If you only have Docker:
```bash
./tools/run-backend-coverage-docker.sh
```

### If neither is installed:
Coverage runs automatically in CI/CD pipeline. View results in:
- **GitHub Actions**: Check the "API - Unit & Integration Tests" job
- **Codecov**: https://codecov.io/gh/DegrassiAaron/meepleai-monorepo

---

## Coverage Threshold

**Current Threshold**: 90% line coverage

This threshold is enforced in:
- ✅ CI/CD pipeline (GitHub Actions)
- ⚠️ Local scripts (recommended, not blocking)

---

## Understanding Coverage Output

### Console Output Example

```
==================================================
  Coverage Summary
==================================================
Line Coverage: 92%
Threshold:     90%
==================================================

Coverage files:
  • Cobertura XML: tests/Api.Tests/coverage/coverage.cobertura.xml
  • HTML Report:   tests/Api.Tests/coverage-report/index.html
```

### HTML Report

The HTML report (when generated) provides:
- **Summary Dashboard**: Overall coverage metrics
- **File Explorer**: Browse coverage by namespace/class
- **Line-by-line Coverage**: See which lines are covered/uncovered
- **Branch Coverage**: See which decision branches are tested
- **Risk Hotspots**: Identify low-coverage areas

---

## Generating HTML Reports

### From Local Script
```bash
./tools/run-backend-coverage.sh --html
```

### From Docker Script Output
```bash
# 1. Run Docker script to generate XML
./tools/run-backend-coverage-docker.sh

# 2. Install ReportGenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# 3. Generate HTML report
reportgenerator \
  -reports:apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml \
  -targetdir:apps/api/tests/Api.Tests/coverage-report \
  -reporttypes:Html

# 4. Open report
open apps/api/tests/Api.Tests/coverage-report/index.html  # macOS
xdg-open apps/api/tests/Api.Tests/coverage-report/index.html  # Linux
start apps/api/tests/Api.Tests/coverage-report/index.html  # Windows
```

---

## Troubleshooting

### Script Execution Permission Denied
```bash
chmod +x tools/run-backend-coverage.sh
chmod +x tools/run-backend-coverage-docker.sh
```

### Docker Script: Services Not Ready
```bash
# Check service status
cd infra
docker compose ps

# Restart services if needed
docker compose down -v
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis
```

### Coverage Below Threshold

1. **View detailed coverage report**:
   ```bash
   ./tools/run-backend-coverage.sh --html --open
   ```

2. **Identify low-coverage files** in the HTML report

3. **Add tests** for uncovered code:
   - Focus on Domain and Application layers
   - Test edge cases and error paths
   - Ensure business logic is covered

### Tests Hanging

The scripts include a 5-minute hang timeout. If tests consistently hang:

1. Check for deadlocks in async code
2. Look for infinite loops
3. Verify external service connections (PostgreSQL, Qdrant, Redis)

---

## CI/CD Integration

Coverage is automatically run in GitHub Actions on every PR and push to main.

**CI Workflow**: `.github/workflows/ci.yml`

**CI Jobs**:
- `ci-api-unit-integration`: Runs tests with 90% threshold enforcement
- Coverage results uploaded to Codecov
- HTML reports available as artifacts on failures

**View Coverage**:
- **Codecov Dashboard**: https://codecov.io/gh/DegrassiAaron/meepleai-monorepo
- **PR Comments**: Codecov bot posts coverage diff
- **Artifacts**: Available in GitHub Actions workflow runs (7-day retention)

---

## Advanced Usage

### Custom Coverage Configuration

Edit `apps/api/Directory.Build.props` to customize:

```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura,json,opencover</CoverletOutputFormat>
  <Threshold>90</Threshold>
  <ThresholdType>line</ThresholdType>
  <Exclude>[*]Api.Migrations.*,[*]*.Generated.*</Exclude>
</PropertyGroup>
```

### Run Coverage for Specific Tests

```bash
cd apps/api

# Integration tests only
dotnet test \
  --filter "Category=Integration" \
  -p:CollectCoverage=true \
  -p:Threshold=80

# Specific namespace
dotnet test \
  --filter "FullyQualifiedName~KnowledgeBase" \
  -p:CollectCoverage=true
```

### Coverage in Watch Mode

```bash
cd apps/api
dotnet watch test \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura
```

---

## Documentation

**Comprehensive Guide**: [docs/02-development/testing/backend-code-coverage.md](../docs/02-development/testing/backend-code-coverage.md)

**Related Docs**:
- [Testing Strategy](../docs/02-development/testing/testing-strategy.md)
- [Testing Guide](../docs/02-development/testing/testing-guide.md)
- [CI/CD Pipeline](../.github/workflows/ci.yml)

---

## Support

**Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues

**Coverage Target**: ≥90% line coverage

**Last Updated**: 2025-11-19
