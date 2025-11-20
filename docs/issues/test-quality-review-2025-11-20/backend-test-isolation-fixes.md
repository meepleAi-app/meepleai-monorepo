# Issue: Fix Backend Test Isolation Issues

**ID**: TEST-002
**Category**: Backend Testing - Reliability
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Fix test isolation issues in tests using shared In-Memory database contexts. Currently, some tests seed data in constructor and reuse the same DbContext across multiple tests, leading to potential side effects and flaky tests.

---

## 🎯 Problem Statement

### Current Anti-Pattern

Da `CitationValidationServiceTests.cs:28-48`:
```csharp
public class CitationValidationServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly CitationValidationService _service;
    private readonly Guid _gameId;
    private readonly Guid _pdf1Id;
    private readonly Guid _pdf2Id;

    public CitationValidationServiceTests()
    {
        // ❌ PROBLEM: Shared context created in constructor
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"CitationValidationTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        // ❌ PROBLEM: Seed data in constructor
        SeedTestData();
    }

    private void SeedTestData()
    {
        _dbContext.PdfDocuments.AddRange(/* ... */);
        _dbContext.SaveChanges(); // ⚠️ Mutates shared state
    }

    // All 24 tests share the same context and seed data
}
```

### Issues
- ⚠️ **Shared state** between tests
- ⚠️ **Order dependency**: Tests might pass/fail based on execution order
- ⚠️ **Difficult debugging**: Hard to isolate which test caused failure
- ⚠️ **Flaky tests**: Intermittent failures in CI
- ⚠️ **Data corruption**: One test can affect another's data

### Impact
```csharp
// Example: Test A modifies data
[Fact]
public async Task Test01_ValidateCitations_ModifiesData()
{
    var pdf = _dbContext.PdfDocuments.First(); // Gets seeded data
    pdf.PageCount = 999; // ⚠️ MUTATES SHARED STATE
    await _dbContext.SaveChangesAsync();
    // ...
}

// Test B expects original data (FAILURE!)
[Fact]
public async Task Test02_ValidateCitations_ExpectsOriginal()
{
    var pdf = _dbContext.PdfDocuments.First();
    Assert.Equal(10, pdf.PageCount); // ❌ FAILS! PageCount is 999
}
```

---

## 🔧 Solution: Fresh Context Per Test

### Recommended Pattern

```csharp
public class CitationValidationServiceTests
{
    // ✅ NO shared mutable state in fields

    [Fact]
    public async Task ValidateCitations_AllValid_ReturnsValid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);

        // Seed data specific to this test
        var (gameId, pdf1Id, pdf2Id) = await SeedTestDataAsync(context);

        var snippets = new List<Snippet>
        {
            new Snippet("text1", $"PDF:{pdf1Id}", page: 1, line: 0, score: 0.9f),
            new Snippet("text2", $"PDF:{pdf1Id}", page: 5, line: 0, score: 0.8f),
        };

        // Act
        var result = await service.ValidateCitationsAsync(snippets, gameId.ToString(), CancellationToken.None);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ValidCitations);

        // Context disposed automatically
    }

    // ✅ Helper methods for test setup
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    private static CitationValidationService CreateService(MeepleAiDbContext context)
    {
        var mockLogger = new Mock<ILogger<CitationValidationService>>();
        return new CitationValidationService(context, mockLogger.Object);
    }

    private static async Task<(Guid gameId, Guid pdf1Id, Guid pdf2Id)> SeedTestDataAsync(
        MeepleAiDbContext context)
    {
        var gameId = Guid.NewGuid();
        var pdf1Id = Guid.NewGuid();
        var pdf2Id = Guid.NewGuid();

        context.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = pdf1Id,
                GameId = gameId,
                FileName = "test-rules.pdf",
                PageCount = 10,
                // ... other properties
            },
            new PdfDocumentEntity
            {
                Id = pdf2Id,
                GameId = gameId,
                FileName = "test-expansion.pdf",
                PageCount = 5,
                // ... other properties
            });

        await context.SaveChangesAsync();
        return (gameId, pdf1Id, pdf2Id);
    }
}
```

### Benefits
- ✅ **Complete isolation**: Each test has its own context
- ✅ **No order dependency**: Tests can run in any order
- ✅ **Easy debugging**: Failures are isolated to single test
- ✅ **Explicit setup**: Clear what data each test needs
- ✅ **Parallel execution**: Tests can run concurrently

---

## 📝 Implementation Checklist

### Phase 1: Identify Affected Files (1 hour)
- [ ] Search for tests with shared DbContext in constructor
- [ ] Document all affected test files
- [ ] Assess impact (number of tests, complexity)

**Search pattern**:
```bash
cd apps/api/tests/Api.Tests
# Find test files with DbContext in constructor
rg "private readonly.*DbContext" -A 10 | rg "public.*Tests\(\)"
```

### Phase 2: Refactor High-Priority Files (3-4 hours)
Priority files (highest risk of flaky tests):
- [ ] `CitationValidationServiceTests.cs` (24 tests)
- [ ] `RagValidationPipelineServiceTests.cs`
- [ ] Any other files with shared In-Memory DB

**Refactoring steps per file**:
1. Remove shared `_dbContext` field
2. Remove `SeedTestData()` from constructor
3. Create `CreateFreshDbContext()` helper method
4. Create `SeedTestDataAsync()` helper method
5. Update each test to use fresh context
6. Verify tests pass and are isolated

### Phase 3: Add Test Isolation Validation (1 hour)
- [ ] Add test runner that runs tests in random order
- [ ] Add CI job that runs tests with `--random-seed`
- [ ] Document test isolation requirements in testing guide

---

## ✅ Acceptance Criteria

- [ ] No shared mutable DbContext in test class fields
- [ ] Each test creates its own fresh context
- [ ] All tests pass when run in random order
- [ ] All tests pass when run in parallel (xUnit default)
- [ ] Tests are faster or same speed (no performance regression)
- [ ] Documentation updated with isolation best practices
- [ ] CI includes random-order test execution

---

## 📊 Impact Analysis

### Before
- ~5-10 test files with shared context
- Potential for flaky tests (hard to quantify)
- Difficult to debug failures
- Cannot run tests in parallel safely

### After
- 0 test files with shared context
- Zero flaky tests due to isolation issues
- Easy debugging (failures isolated)
- Safe parallel test execution
- Improved CI reliability

### Performance Impact
- **Expected**: Minimal to none
- **In-Memory DB**: Very fast creation (<1ms)
- **Trade-off**: Small setup overhead vs reliability gain
- **Mitigation**: Helper methods reduce boilerplate

---

## 🔗 Related Issues

- [TEST-001](./backend-test-naming-standardization.md) - Standardize Naming
- [TEST-003](./backend-test-data-factories.md) - Test Data Factories (complementary)
- [TEST-007](./backend-reduce-magic-numbers.md) - Reduce Magic Numbers

---

## 📚 References

- [xUnit Test Isolation](https://xunit.net/docs/shared-context)
- [Microsoft Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#avoid-logic-in-tests)
- [Test Isolation Patterns](https://martinfowler.com/articles/practical-test-pyramid.html#IsolationOfTests)

---

## 📈 Effort Estimate

**Total: 4-6 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Identify files | 1h | Search codebase, assess impact |
| Refactor CitationValidationServiceTests | 2h | 24 tests to migrate |
| Refactor other files | 1-2h | Depends on number found |
| Add CI validation | 1h | Random-order test execution |
| Documentation | <1h | Update testing guide |

---

## 🧪 Testing Strategy

### Validation Steps
1. **Unit test level**:
   ```bash
   dotnet test --logger "console;verbosity=detailed"
   ```

2. **Random order** (verify no dependencies):
   ```bash
   dotnet test -- xUnit.parallelizeTestCollections=true
   ```

3. **Parallel execution** (verify isolation):
   ```bash
   dotnet test --parallel
   ```

4. **Multiple runs** (verify consistency):
   ```bash
   for i in {1..10}; do dotnet test --no-build; done
   ```

### Success Metrics
- [ ] All tests pass in all 4 validation scenarios
- [ ] Zero intermittent failures in 10 consecutive runs
- [ ] CI passes consistently across 10+ runs

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open - Ready for Implementation
**Assignee**: TBD
