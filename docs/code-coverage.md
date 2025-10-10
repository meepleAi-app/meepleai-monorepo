# Code Coverage Guide

## Overview

This document provides guidance on how to measure, track, and monitor code coverage for the MeepleAI monorepo.

## Current Status

### Backend (API)

**Test Framework**: xUnit with Moq and Testcontainers
**Coverage Tool**: Coverlet (collector + msbuild)
**Target Coverage**: Not currently enforced (recommended: 80%+)

**Test Categories**:
- **Unit Tests**: Service layer tests with mocked dependencies
- **Integration Tests**: Tests using Testcontainers (PostgreSQL, Qdrant)
- **API Tests**: End-to-end endpoint tests with WebApplicationFactory

**Test Count**: 100+ tests across:
- Service tests (Auth, Game, RagService, PdfStorage, etc.)
- Integration tests (Qdrant, RuleSpec, PDF processing)
- Endpoint tests (Admin, QA, Authentication)
- Security/authorization tests

### Frontend (Web)

**Test Framework**: Jest with React Testing Library
**Coverage Tool**: Jest built-in coverage
**Target Coverage**: 90% (enforced in jest.config.js)

**Coverage Thresholds** (from `apps/web/jest.config.js`):
```javascript
coverageThreshold: {
  global: {
    branches: 80,
    functions: 80,
    lines: 80,
    statements: 80
  }
}
```

## Running Coverage Reports

### Backend Coverage

#### Full Test Suite with Coverage

⚠️ **Warning**: This takes 10-15+ minutes due to Testcontainers initialization.

```bash
cd apps/api
dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=./coverage/
```

**Output**: `apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml`

#### Specific Test Class Coverage

For faster coverage runs, target specific test classes:

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~QdrantServiceTests" -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura
```

#### Coverage Output Formats

Coverlet supports multiple formats:

```bash
# Cobertura XML (compatible with most CI tools)
-p:CoverletOutputFormat=cobertura

# JSON
-p:CoverletOutputFormat=json

# lcov (for SonarQube, Codecov)
-p:CoverletOutputFormat=lcov

# HTML (requires reportgenerator tool)
-p:CoverletOutputFormat=opencover

# Multiple formats (comma-separated)
-p:CoverletOutputFormat="cobertura,json,lcov"
```

#### Viewing Coverage Reports

Install the `reportgenerator` global tool:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Generate HTML report:

```bash
cd apps/api/tests/Api.Tests
reportgenerator -reports:coverage/coverage.cobertura.xml -targetdir:coverage/html -reporttypes:Html
```

Open `coverage/html/index.html` in a browser.

### Frontend Coverage

#### Run All Tests with Coverage

```bash
cd apps/web
pnpm test:coverage
```

**Output**: `apps/web/coverage/` directory with HTML report

#### View Coverage Report

```bash
cd apps/web
pnpm test:coverage
# Open coverage/lcov-report/index.html in browser
```

#### Coverage Configuration

Coverage settings in `apps/web/jest.config.js`:

- **Collect from**: All `src/**/*.{js,jsx,ts,tsx}` files
- **Exclude**:
  - `_app.tsx`, `_document.tsx`
  - `.d.ts` type definition files
  - Test files
  - Node modules

## CI/CD Coverage

### Current CI Setup

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs tests but **does not currently collect or publish coverage reports**.

#### API CI Job

```yaml
- name: Test
  run: dotnet test --verbosity normal
  working-directory: apps/api
  env:
    CI: true
```

#### Web CI Job

```yaml
- name: Test
  run: pnpm test
  working-directory: apps/web
```

### Recommended CI Enhancements

#### Option 1: Codecov Integration

Add to `.github/workflows/ci.yml`:

```yaml
# API Coverage
- name: Test with Coverage
  run: dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=lcov -p:CoverletOutput=./coverage/
  working-directory: apps/api

- name: Upload API Coverage
  uses: codecov/codecov-action@v4
  with:
    files: apps/api/tests/Api.Tests/coverage/coverage.info
    flags: api

# Web Coverage
- name: Test with Coverage
  run: pnpm test:coverage
  working-directory: apps/web

- name: Upload Web Coverage
  uses: codecov/codecov-action@v4
  with:
    files: apps/web/coverage/lcov.info
    flags: web
```

#### Option 2: Coveralls Integration

```yaml
- name: Coveralls
  uses: coverallsapp/github-action@v2
  with:
    github-token: ${{ secrets.GITHUB_TOKEN }}
    path-to-lcov: apps/api/tests/Api.Tests/coverage/coverage.info
```

#### Option 3: GitHub Actions Artifacts

Store coverage reports as artifacts:

```yaml
- name: Upload Coverage Report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: |
      apps/api/tests/Api.Tests/coverage/
      apps/web/coverage/
    retention-days: 30
```

## Coverage Best Practices

### What to Measure

✅ **Should have high coverage**:
- Business logic services (RagService, GameService, etc.)
- Data validation and transformation
- Authentication and authorization logic
- Error handling paths
- API endpoint logic

⚠️ **Lower priority for coverage**:
- DTO/Model classes with no logic
- Database entity classes
- Configuration classes
- Middleware (if simple pass-through)

### Coverage Targets by Component

| Component | Recommended Target | Current Status |
|-----------|-------------------|----------------|
| Backend Services | 85-90% | ❓ Unknown |
| Backend Endpoints | 80-85% | ❓ Unknown |
| Backend Infrastructure | 70-75% | ❓ Unknown |
| Frontend Components | 90% | ✅ Enforced |
| Frontend Utils | 90% | ✅ Enforced |

### Improving Coverage

#### Identify Gaps

Generate coverage report and look for:

1. **Red/uncovered lines**: Write tests for these
2. **Partially covered branches**: Add tests for edge cases
3. **High complexity, low coverage**: Prioritize these for testing

#### Writing Tests for Coverage

```csharp
// ❌ Don't write tests just for coverage
[Fact]
public void PropertySetter_SetsValue()
{
    var obj = new MyClass();
    obj.Name = "test";
    Assert.Equal("test", obj.Name);
}

// ✅ Write meaningful tests that validate behavior
[Fact]
public void ValidateUser_WithInvalidEmail_ReturnsError()
{
    var service = new UserService();
    var result = service.ValidateUser(new User { Email = "invalid" });
    Assert.False(result.IsValid);
    Assert.Contains("email", result.Errors[0].ToLower());
}
```

## Monitoring Coverage Trends

### Local Monitoring

Create a script to track coverage over time:

```powershell
# tools/measure-coverage.ps1
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"

# API Coverage
cd apps/api
dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=json -p:CoverletOutput="./coverage/coverage-$timestamp.json"

# Web Coverage
cd ../web
pnpm test:coverage -- --json --outputFile="coverage/coverage-$timestamp.json"

Write-Host "Coverage reports saved with timestamp: $timestamp"
```

### CI Monitoring

Set up coverage tracking:

1. **Codecov**: Provides graphs, trends, and PR comments
2. **Coveralls**: Shows coverage change per commit
3. **SonarQube/SonarCloud**: Comprehensive code quality + coverage
4. **GitHub Actions Summary**: Post coverage to PR comments

### Setting Coverage Gates

Enforce minimum coverage in CI:

```yaml
# Fail build if coverage drops below threshold
- name: Check Coverage Threshold
  run: |
    dotnet test -p:CollectCoverage=true -p:Threshold=80 -p:ThresholdType=line
  working-directory: apps/api
```

## Troubleshooting

### Issue: Coverage Tests Hang with Testcontainers

**Symptom**: `dotnet test` with coverage hangs at "Starting test execution"

**Causes**:
- Testcontainers Docker initialization is slow on Windows
- Multiple containers being created/destroyed for each test
- Resource contention

**Solutions**:

1. **Run specific test classes** instead of full suite
2. **Use test filters** to avoid integration tests:
   ```bash
   dotnet test --filter "FullyQualifiedName!~IntegrationTests" -p:CollectCoverage=true
   ```
3. **Increase timeout** in test settings
4. **Use CI for full coverage**, local for quick feedback

### Issue: Coverage Report Not Generated

**Check**:
1. Coverlet package is installed (`coverlet.msbuild` or `coverlet.collector`)
2. Output path exists or can be created
3. Correct syntax: `-p:` not `/p:` in some shells

### Issue: Low Coverage Despite Many Tests

**Possible reasons**:
- Tests not actually executing code (mocks return early)
- Code in catch blocks not tested
- Private methods not covered (test public API instead)
- Generated code included in coverage (exclude via settings)

## Excluding Code from Coverage

### Backend (Coverlet)

Add to `Api.Tests.csproj`:

```xml
<PropertyGroup>
  <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
  <ExcludeByAttribute>GeneratedCode,ExcludeFromCodeCoverage</ExcludeByAttribute>
</PropertyGroup>
```

Use attributes in code:

```csharp
[ExcludeFromCodeCoverage]
public class GeneratedDatabaseContext : DbContext
{
    // Migration-generated code
}
```

### Frontend (Jest)

Already configured in `jest.config.js`:

```javascript
collectCoverageFrom: [
  'src/**/*.{js,jsx,ts,tsx}',
  '!src/pages/_app.tsx',
  '!src/pages/_document.tsx',
  '!**/*.d.ts',
],
```

## Next Steps

### Immediate Actions

1. ✅ **Document coverage process** (this document)
2. 🔲 **Run full coverage locally once** to establish baseline
3. 🔲 **Add coverage collection to CI** (choose Codecov/Coveralls/Artifacts)
4. 🔲 **Set initial coverage targets** (start at 70%, increase over time)
5. 🔲 **Add coverage badge** to README.md

### Long-term Goals

1. 🔲 **Achieve 80%+ backend coverage**
2. 🔲 **Maintain 90% frontend coverage** (already enforced)
3. 🔲 **Set up coverage trend monitoring**
4. 🔲 **Prevent coverage regressions in PRs**
5. 🔲 **Create coverage reports for each release**

## Resources

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Jest Coverage Documentation](https://jestjs.io/docs/configuration#collectcoverage-boolean)
- [Codecov Documentation](https://docs.codecov.com/docs)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)

## Last Updated

**Date**: 2025-10-09
**Status**: Initial documentation created
**Next Review**: After establishing coverage baseline
