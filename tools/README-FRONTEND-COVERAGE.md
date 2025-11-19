# Frontend Code Coverage Tools

This directory contains scripts for running frontend code coverage analysis.

## Available Script

### `run-frontend-coverage.sh` (Local Execution)

**Requirements**: Node.js 20+ and pnpm installed

**Usage**:
```bash
# Basic usage (90% threshold)
./tools/run-frontend-coverage.sh

# Open HTML report in browser
./tools/run-frontend-coverage.sh --open

# Set custom threshold
./tools/run-frontend-coverage.sh --threshold 85

# Run in watch mode
./tools/run-frontend-coverage.sh --watch

# Help
./tools/run-frontend-coverage.sh --help
```

**Features**:
- ✅ Fast execution with pnpm
- ✅ Automatic HTML report generation (Jest built-in)
- ✅ Automatic browser opening
- ✅ Color-coded output
- ✅ Coverage summary display (4 metrics)
- ✅ Watch mode support

**Output**:
- LCOV Report: `apps/web/coverage/lcov.info`
- HTML Report: `apps/web/coverage/lcov-report/index.html`
- JSON Summary: `apps/web/coverage/coverage-summary.json`
- JSON Detail: `apps/web/coverage/coverage-final.json`
- Clover XML: `apps/web/coverage/clover.xml`

---

## Quick Start

### Recommended
```bash
./tools/run-frontend-coverage.sh --open
```

### Manual Alternative
```bash
cd apps/web
pnpm test:coverage
open coverage/lcov-report/index.html
```

### Watch Mode (Development)
```bash
./tools/run-frontend-coverage.sh --watch
```

---

## Coverage Thresholds

**Current Thresholds**: 90% for all metrics

Enforced in:
- ✅ Jest configuration (`apps/web/jest.config.js`)
- ✅ CI/CD pipeline (GitHub Actions)
- ✅ Local scripts (recommended, blocking)

**Metrics**:
- **Lines**: Percentage of code lines executed
- **Statements**: Percentage of statements executed
- **Functions**: Percentage of functions called
- **Branches**: Percentage of decision paths taken

---

## Understanding Coverage Output

### Console Output Example

```
==================================================
  Coverage Summary
==================================================
Lines:       92.45%
Statements:  92.31%
Functions:   91.78%
Branches:    90.12%
Threshold:   90%
==================================================

Coverage files:
  • LCOV:        coverage/lcov.info
  • HTML Report: coverage/lcov-report/index.html
  • JSON:        coverage/coverage-summary.json
```

### HTML Report

The HTML report provides:
- **Summary Dashboard**: Overall coverage metrics
- **File Browser**: Browse coverage by directory/file
- **Source View**: Line-by-line coverage highlighting
  - ✅ Green: Covered lines
  - ❌ Red: Uncovered lines
  - 🟡 Yellow: Partially covered branches
- **Function List**: See which functions are tested
- **Search**: Find specific files or code

---

## Coverage Collection

### Files Included

```javascript
// From jest.config.js
collectCoverageFrom: [
  'src/**/*.{js,jsx,ts,tsx}',  // All source files
  '!src/**/*.d.ts',             // Exclude type definitions
  '!src/**/__tests__/fixtures/**',
  '!src/**/__tests__/utils/**',
  '!src/test-utils/**',
  '!src/**/test-utils.{ts,tsx}',
  '!src/**/*.worker.{js,jsx,ts,tsx}',
  '!src/workers/**',
]
```

### Files Excluded

- Type definition files (`*.d.ts`)
- Test fixtures and utilities
- Web workers
- E2E tests (`/e2e/`)
- Build output (`/.next/`)

---

## Troubleshooting

### Script Execution Permission Denied
```bash
chmod +x tools/run-frontend-coverage.sh
```

### Coverage Below Threshold

1. **View detailed coverage report**:
   ```bash
   ./tools/run-frontend-coverage.sh --open
   ```

2. **Identify uncovered code** in the HTML report (red/yellow highlighting)

3. **Add tests** for uncovered code:
   - Focus on business logic components
   - Test user interactions
   - Cover error cases and edge cases

### Tests Hanging

The script will timeout if tests hang. Check for:
- Unresolved promises
- Missing `await` in async tests
- Timer mocks not advancing
- Infinite loops

### Out of Memory

For large codebases:

```bash
# Increase Node.js memory
NODE_OPTIONS=--max_old_space_size=4096 ./tools/run-frontend-coverage.sh

# Or run in sequence (slower but uses less memory)
cd apps/web
pnpm test:coverage -- --runInBand
```

---

## CI/CD Integration

Coverage runs automatically in GitHub Actions on every PR and push to main.

**CI Workflow**: `.github/workflows/ci.yml`

**CI Jobs**:
- `ci-web-unit`: Runs tests with 90% threshold enforcement
- Coverage results uploaded to Codecov
- HTML reports available as artifacts

**View Coverage**:
- **Codecov Dashboard**: https://codecov.io/gh/DegrassiAaron/meepleai-monorepo
- **PR Comments**: Codecov bot posts coverage diff
- **Artifacts**: Available in GitHub Actions workflow runs

---

## Advanced Usage

### Run Coverage for Specific Tests

```bash
cd apps/web

# Specific test file
pnpm test:coverage -- path/to/test.spec.ts

# Tests matching pattern
pnpm test:coverage -- --testNamePattern="LoginForm"

# Specific directory
pnpm test:coverage -- src/components/
```

### Custom Reporters

Edit `jest.config.js` to add reporters:

```javascript
coverageReporters: [
  'text',          // Console output
  'text-summary',  // Summary
  'html',          // HTML report
  'lcov',          // LCOV for Codecov
  'json',          // JSON format
  'cobertura',     // Cobertura XML
],
```

### Watch Mode

```bash
# Run coverage in watch mode
./tools/run-frontend-coverage.sh --watch

# Watch specific pattern
cd apps/web
pnpm test:coverage -- --watch --testPathPattern="components"
```

### VS Code Integration

Install the "Jest" extension (`orta.vscode-jest`) for:
- ✅ Inline coverage highlights
- ✅ Green/red gutters in editor
- ✅ Auto-run tests on save
- ✅ Coverage percentages in status bar

---

## Documentation

**Comprehensive Guide**: [docs/04-frontend/testing/code-coverage.md](../docs/04-frontend/testing/code-coverage.md)

**Related Docs**:
- [Testing Guide](../docs/04-frontend/testing/testing-guide.md) (if exists)
- [Backend Code Coverage](../docs/02-development/testing/backend-code-coverage.md)
- [CI/CD Pipeline](../.github/workflows/ci.yml)

---

## Support

**Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues

**Coverage Target**: ≥90% (lines, statements, functions, branches)

**Last Updated**: 2025-11-19
