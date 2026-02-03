## Summary

<!-- Briefly describe what this PR does -->
<!-- See [Contributing Guidelines](../CONTRIBUTING.md) for detailed PR process -->

## Related Issue

<!-- Link to the issue this PR addresses (e.g., Closes #123) -->

Closes #

## Type of Change

<!-- Check the relevant option(s) -->

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Refactoring (no functional changes)
- [ ] Performance improvement
- [ ] Test addition/improvement
- [ ] Configuration/infrastructure change

## Changes Made

<!-- List the key changes made in this PR -->

-
-
-

## Testing

<!-- Describe the testing performed -->

### Test Coverage

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] E2E tests added/updated (if applicable)
- [ ] All tests passing locally
- [ ] **Test names follow BDD convention** (see [Testing Guidelines](../CONTRIBUTING.md#testing-guidelines))

### Coverage Metrics

<!--
Run coverage and report the delta. See docs/05-testing/backend/coverage-baseline-2026-01-27.md for baseline.

Backend: cd apps/api && dotnet test --collect:"XPlat Code Coverage"
Frontend: cd apps/web && pnpm test:coverage
-->

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Backend Line Coverage | | | |
| Backend Branch Coverage | | | |
| Frontend Statement Coverage | | | |

#### Coverage Requirements

- [ ] **Backend coverage ≥50%** (or no decrease from baseline)
- [ ] **Frontend coverage ≥85%** (or no decrease from baseline)
- [ ] New code has appropriate test coverage
- [ ] No coverage regressions in modified files

#### Bounded Context Coverage (Backend)

<!-- If modifying a specific bounded context, verify its coverage -->

| Bounded Context | Tests Before | Tests After | Notes |
|-----------------|--------------|-------------|-------|
| | | | |

### Manual Testing

<!-- Describe any manual testing performed -->

-

## Checklist

- [ ] Code follows project style guidelines (see [CONTRIBUTING.md](../CONTRIBUTING.md#coding-standards))
- [ ] Self-review of code completed
- [ ] Comments added for complex logic
- [ ] Documentation updated (if applicable)
- [ ] No new warnings introduced
- [ ] Tests added/updated and passing
- [ ] **Test names follow BDD-style naming convention** (see [Testing Guidelines](../CONTRIBUTING.md#testing-guidelines))
- [ ] **Coverage metrics reported above** (see [Coverage Baseline](../docs/05-testing/backend/coverage-baseline-2026-01-27.md))
- [ ] **No coverage regressions** (coverage delta ≥0%)
- [ ] Changes are backwards compatible (or breaking changes documented)
- [ ] No secrets or API keys committed (see [SECURITY.md](../SECURITY.md))

## Screenshots/Recordings (if applicable)

<!-- Add screenshots or recordings demonstrating the changes -->

## Additional Notes

<!-- Any additional context or information -->
