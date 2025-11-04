---
title: "[CODE QUALITY] Clean Up Code Smells (382+ instances)"
labels: ["code-quality", "priority-low", "P3", "code-scanning", "tech-debt", "maintainability"]
---

## Summary

**382+ open notes** for various code smells including unused variables, commented code, duplicate code, long methods, and other maintainability issues.

### Impact
- **Severity**: 📝 **NOTE** (Low Priority - P3)
- **Risk**: Reduced code maintainability, readability, technical debt
- **Production Impact**: None immediate, but increases maintenance cost over time

---

## Problem Categories

### 1. Unused Code (Estimated ~150 instances)
- Unused variables
- Unused imports
- Unused private methods
- Unused parameters

### 2. Commented Code (Estimated ~80 instances)
- Old commented-out code blocks
- Debug comments left in production code
- TODO comments without tracking

### 3. Code Duplication (Estimated ~60 instances)
- Copy-pasted logic
- Similar validation patterns
- Repeated error handling

### 4. Complexity Issues (Estimated ~50 instances)
- Long methods (>50 lines)
- Deeply nested conditionals (>3 levels)
- High cyclomatic complexity

### 5. Naming Issues (Estimated ~42 instances)
- Non-descriptive variable names (`data`, `temp`, `x`)
- Inconsistent naming conventions
- Misleading names

---

## Examples and Solutions

### 1. Unused Variables

```csharp
// ❌ BAD: Unused variable
public async Task<GameDto> GetGameAsync(int id)
{
    var timestamp = DateTime.UtcNow; // Never used
    var game = await _dbContext.Games.FindAsync(id);
    return MapToDto(game);
}

// ✅ GOOD: Remove unused variable
public async Task<GameDto> GetGameAsync(int id)
{
    var game = await _dbContext.Games.FindAsync(id);
    return MapToDto(game);
}
```

### 2. Commented Code

```csharp
// ❌ BAD: Old commented code
public async Task ProcessAsync()
{
    // var oldApproach = DoOldThing();
    // if (oldApproach.Success) { ... }

    await DoNewThingAsync();
}

// ✅ GOOD: Remove commented code (use git history if needed)
public async Task ProcessAsync()
{
    await DoNewThingAsync();
}
```

### 3. Code Duplication

```csharp
// ❌ BAD: Duplicated validation logic
public async Task<Game> CreateGameAsync(CreateGameRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Name is required");
    if (request.Name.Length > 200)
        throw new ValidationException("Name too long");
    // ... create game
}

public async Task<Game> UpdateGameAsync(int id, UpdateGameRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Name is required");
    if (request.Name.Length > 200)
        throw new ValidationException("Name too long");
    // ... update game
}

// ✅ GOOD: Extract common validation
private void ValidateGameName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ValidationException("Name is required");
    if (name.Length > 200)
        throw new ValidationException("Name too long");
}

public async Task<Game> CreateGameAsync(CreateGameRequest request)
{
    ValidateGameName(request.Name);
    // ... create game
}

public async Task<Game> UpdateGameAsync(int id, UpdateGameRequest request)
{
    ValidateGameName(request.Name);
    // ... update game
}
```

### 4. Complex Methods

```csharp
// ❌ BAD: Long method with deep nesting
public async Task<ProcessResult> ProcessComplexDataAsync(Data data)
{
    if (data != null)
    {
        if (data.IsValid)
        {
            if (await CanProcessAsync(data))
            {
                if (data.Type == DataType.A)
                {
                    // 20 lines of logic
                }
                else if (data.Type == DataType.B)
                {
                    // 20 lines of logic
                }
                else
                {
                    // 20 lines of logic
                }
            }
        }
    }
    return result;
}

// ✅ GOOD: Extract methods, reduce nesting
public async Task<ProcessResult> ProcessComplexDataAsync(Data data)
{
    ValidateData(data);

    if (!await CanProcessAsync(data))
        return ProcessResult.Skipped();

    return data.Type switch
    {
        DataType.A => await ProcessTypeAAsync(data),
        DataType.B => await ProcessTypeBAsync(data),
        _ => await ProcessDefaultAsync(data)
    };
}

private static void ValidateData(Data data)
{
    if (data == null || !data.IsValid)
        throw new ValidationException("Invalid data");
}

private async Task<ProcessResult> ProcessTypeAAsync(Data data)
{
    // Extracted logic for Type A
}
```

### 5. Poor Naming

```csharp
// ❌ BAD: Non-descriptive names
public async Task<bool> DoStuff(int x)
{
    var temp = await _repo.GetData(x);
    var flag = temp != null;
    return flag;
}

// ✅ GOOD: Descriptive names
public async Task<bool> GameExistsAsync(int gameId)
{
    var game = await _gameRepository.FindByIdAsync(gameId);
    var exists = game != null;
    return exists;
}

// ✅ BETTER: Simplified with clear intent
public async Task<bool> GameExistsAsync(int gameId)
{
    return await _gameRepository.ExistsAsync(gameId);
}
```

---

## Automated Cleanup

### Frontend (TypeScript/React)

```bash
cd apps/web

# Fix auto-fixable lint issues
pnpm lint --fix

# Remove unused imports
pnpm lint -- --fix --rule 'no-unused-vars: error'

# Format code
pnpm format
```

### Backend (C#)

```bash
cd apps/api

# Format code style
dotnet format

# Remove unused usings
dotnet format --include-generated --verify-no-changes

# Analyze code
dotnet build /p:EnforceCodeStyleInBuild=true /p:TreatWarningsAsErrors=true
```

---

## Manual Remediation Plan

### Phase 1: Quick Wins (2-3 days)
Auto-fixable issues that can be batch processed:

- [ ] **Unused imports/usings** - Run `dotnet format` and `pnpm lint --fix`
- [ ] **Code formatting** - Apply consistent style
- [ ] **Simple unused variables** - Remove obvious cases
- [ ] **Commented code** - Remove old commented blocks

### Phase 2: Duplicated Code (3-4 days)
Extract common patterns:

- [ ] **Validation logic** - Create validator classes
- [ ] **Error handling** - Standardize exception handling
- [ ] **DTO mapping** - Use AutoMapper or consistent mapping methods
- [ ] **Database queries** - Extract to repository methods

### Phase 3: Complexity Reduction (4-5 days)
Refactor complex methods:

- [ ] **Long methods** - Extract methods (max 30 lines)
- [ ] **Deep nesting** - Use guard clauses and early returns
- [ ] **Switch statements** - Convert to pattern matching
- [ ] **Complex conditionals** - Extract to named methods

### Phase 4: Naming Improvements (2-3 days)
Rename for clarity:

- [ ] **Variables** - Use descriptive names
- [ ] **Methods** - Follow verb-noun convention
- [ ] **Classes** - Single responsibility principle
- [ ] **Constants** - Use PascalCase for public, UPPER_CASE for private

---

## Code Quality Tools

### 1. SonarCloud/SonarQube

Add SonarCloud to CI pipeline:

```yaml
# .github/workflows/sonar.yml
name: SonarCloud Analysis
on:
  push:
    branches: [ main ]
  pull_request:

jobs:
  sonar:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: SonarCloud Scan
        uses: SonarSource/sonarcloud-github-action@master
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

### 2. Code Metrics Dashboard

Track metrics over time:
- **Cyclomatic Complexity**: Target < 10 per method
- **Lines of Code**: Target < 50 per method
- **Duplication**: Target < 3%
- **Test Coverage**: Maintain > 80%

### 3. EditorConfig Rules

Update `.editorconfig`:

```ini
# Remove unused usings
dotnet_diagnostic.IDE0005.severity = warning

# Use var when type is obvious
csharp_style_var_when_type_is_apparent = true:suggestion

# Prefer expression body for methods
csharp_style_expression_bodied_methods = when_on_single_line:suggestion

# Naming conventions
dotnet_naming_rule.constants_should_be_pascal_case.severity = warning
```

---

## Testing

### Before Refactoring
1. Ensure all existing tests pass
2. Add tests for any untested code
3. Capture code coverage baseline

### During Refactoring
1. Run tests after each change
2. Verify behavior unchanged
3. Add tests for extracted methods

### After Refactoring
1. Verify test coverage maintained or improved
2. Run full integration test suite
3. Perform manual smoke tests

---

## Prevention Strategy

### 1. Pre-commit Hooks

```bash
# .git/hooks/pre-commit
#!/bin/bash

# Backend: Format and analyze
cd apps/api && dotnet format --verify-no-changes || exit 1

# Frontend: Lint and format
cd apps/web && pnpm lint && pnpm typecheck || exit 1
```

### 2. CI/CD Quality Gates

```yaml
# Fail build on:
- Code coverage < 80%
- Cyclomatic complexity > 15
- Duplication > 5%
- Any critical code smells
```

### 3. Code Review Checklist

```markdown
- [ ] No unused variables or imports
- [ ] No commented-out code
- [ ] Methods < 50 lines
- [ ] Nesting depth < 3 levels
- [ ] Descriptive variable/method names
- [ ] No duplicated logic (DRY principle)
- [ ] Single responsibility per method/class
```

---

## Acceptance Criteria

- [ ] All auto-fixable issues resolved (format, unused imports)
- [ ] Code duplication reduced by 50%
- [ ] No methods > 50 lines
- [ ] No nesting > 3 levels
- [ ] All variables/methods have descriptive names
- [ ] SonarCloud quality gate passes
- [ ] All tests pass
- [ ] Code coverage maintained or improved

---

## Estimated Effort

- **Total Time**: 11-15 days (1 developer)
- **Complexity**: Low-Medium (mostly mechanical refactoring)
- **Risk**: Low (covered by existing tests)

### Breakdown
- Phase 1 (Quick Wins): 2-3 days
- Phase 2 (Duplication): 3-4 days
- Phase 3 (Complexity): 4-5 days
- Phase 4 (Naming): 2-3 days

---

## Benefits

### Short-term
- ✅ Cleaner, more readable code
- ✅ Easier code reviews
- ✅ Faster onboarding for new developers

### Long-term
- ✅ Reduced maintenance cost
- ✅ Fewer bugs from duplicated logic
- ✅ Better code reusability
- ✅ Improved testability

---

## References

- [Martin Fowler - Refactoring](https://refactoring.com/)
- [Clean Code by Robert C. Martin](https://www.oreilly.com/library/view/clean-code-a/9780136083238/)
- [Microsoft: Code metrics](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-values)
- [SonarQube: Code Smells](https://docs.sonarqube.org/latest/user-guide/concepts/)

---

**Priority**: P3 - LOW
**Category**: Code Quality > Maintainability > Technical Debt
**Related Issues**: #[code-scanning-tracker]

---

## Notes

This is a **continuous improvement** task. While not urgent, addressing these issues incrementally will:
- Reduce technical debt
- Improve developer productivity
- Make the codebase more maintainable

**Recommendation**: Allocate 1-2 hours per sprint for code smell cleanup as part of regular development.
