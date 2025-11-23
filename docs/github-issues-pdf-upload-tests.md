# GitHub Issues: PDF Upload Test Suite Code Review

> Generated from code review on 2025-11-23
>
> **Total Issues:** 21
> - Critical (P0): 3
> - High (P1): 4
> - Medium (P2): 8
> - Low (P3): 6

---

## Critical Priority (P0) - Fix Before Merge

### Issue #1: DbContext disposal test creates invalid test scenario

**Labels:** `bug`, `tests`, `critical`, `pdf-upload`

**Description:**

The test `UploadPdf_WhenDatabaseFails_RollsBackTransaction` disposes the shared `_dbContext` instance and then tries to use it via the handler, causing an immediate `ObjectDisposedException` rather than testing actual database failure scenarios.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 774-798

**Current Code:**
```csharp
// Dispose the context to simulate database unavailability
await _dbContext.DisposeAsync();

var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();

// Act & Assert - Should handle gracefully without leaving partial data
var act = async () => await handler.Handle(command, TestCancellationToken);
await act.Should().ThrowAsync<Exception>("database failure should throw exception");
```

**Problem:**
The handler constructor receives the disposed `_dbContext` instance, which will throw `ObjectDisposedException` immediately, not simulating a real database failure.

**Expected Behavior:**
Test should simulate actual database connection failures or transaction rollback scenarios.

**Proposed Fix:**
```csharp
// Create a fresh handler with a context that will be disposed mid-operation
using var scopedContext = CreateFreshDbContext();
var scopedHandler = CreateHandlerWithContext(scopedContext);

// Start the operation, then dispose the context mid-flight
var task = scopedHandler.Handle(command, TestCancellationToken);
await scopedContext.DisposeAsync(); // Dispose while operation is running

// Act & Assert
var act = async () => await task;
await act.Should().ThrowAsync<DbUpdateException>("database failure should throw exception");
```

**Acceptance Criteria:**
- [ ] Test properly simulates database failure during upload operation
- [ ] Test verifies no partial data is left in database
- [ ] Test verifies proper error handling and logging
- [ ] Test passes consistently

**Priority:** Critical - Invalid test scenario provides false confidence

---

### Issue #2: Barrier resource leak in concurrent race condition test

**Labels:** `bug`, `tests`, `critical`, `resource-leak`, `pdf-upload`

**Description:**

The `UploadPdf_WithRaceConditions_MaintainsDataIntegrity` test creates a `Barrier` object but never disposes it, potentially causing resource leaks in long-running test suites.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Line: 664

**Current Code:**
```csharp
var barrier = new Barrier(simultaneousUploads);
var tasks = new List<Task<PdfUploadResult>>();

for (int i = 0; i < simultaneousUploads; i++)
{
    var index = i;
    tasks.Add(Task.Run(async () =>
    {
        // ...
        barrier.SignalAndWait();
        // ...
    }));
}
```

**Problem:**
`Barrier` implements `IDisposable` but is not disposed, leading to resource leaks.

**Proposed Fix:**
```csharp
using var barrier = new Barrier(simultaneousUploads);
var tasks = new List<Task<PdfUploadResult>>();
// ... rest of test
```

**Acceptance Criteria:**
- [ ] Barrier is properly disposed after test completes
- [ ] Test still functions correctly with proper disposal
- [ ] No resource leaks detected in test suite runs

**Priority:** Critical - Resource leaks can cause test suite instability

---

### Issue #3: Missing test timeouts can cause indefinite hangs

**Labels:** `enhancement`, `tests`, `critical`, `reliability`, `pdf-upload`

**Description:**

Integration tests that involve Testcontainers, network calls, and database operations lack explicit timeouts. If a test hangs due to infrastructure issues, it will run indefinitely.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- File: `apps/api/tests/Api.Tests/Integration/PdfUploadQuotaEnforcementIntegrationTests.cs`
- All test methods

**Current Code:**
```csharp
[Fact]
public async Task UploadPdf_WithCorruptedPdf_ReturnsError()
{
    // Test can hang indefinitely if infrastructure fails
}
```

**Proposed Fix:**
```csharp
[Fact(Timeout = 30000)] // 30 seconds max
public async Task UploadPdf_WithCorruptedPdf_ReturnsError()
{
    // Test will fail after 30s if hanging
}
```

**Implementation Guidelines:**
- Unit tests: 5s timeout
- Integration tests with Testcontainers: 30s timeout
- Performance tests: 60s timeout

**Acceptance Criteria:**
- [ ] All integration tests have appropriate timeouts
- [ ] Timeout values documented in test documentation
- [ ] CI/CD pipeline doesn't experience hanging tests

**Priority:** Critical - Prevents CI/CD pipeline hangs

---

## High Priority (P1) - Fix This Sprint

### Issue #4: Synchronous File.Exists() blocks threads in async tests

**Labels:** `performance`, `tests`, `high`, `async`, `pdf-upload`

**Description:**

The blob storage validation test uses synchronous `File.Exists()` in an async test method, blocking threads unnecessarily.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Line: 981

**Current Code:**
```csharp
File.Exists(doc.FilePath).Should().BeTrue("file should exist in blob storage location");
```

**Problem:**
Synchronous I/O in async context blocks threads and reduces test suite performance.

**Proposed Fix:**
```csharp
// Option 1: Document why sync is acceptable
File.Exists(doc.FilePath).Should().BeTrue("file should exist in blob storage location");
// Note: File.Exists is acceptable here as it's a fast metadata-only operation

// Option 2: Use async wrapper
await FileExistsAsync(doc.FilePath).Should().BeTrueAsync();

private static Task<bool> FileExistsAsync(string path) =>
    Task.Run(() => File.Exists(path));
```

**Acceptance Criteria:**
- [ ] Either use async file operations or document why sync is acceptable
- [ ] No thread starvation in test suite
- [ ] Test performance not degraded

**Priority:** High - Affects test suite performance

---

### Issue #5: Hardcoded magic numbers cause test fragility

**Labels:** `refactoring`, `tests`, `high`, `maintainability`, `pdf-upload`

**Description:**

Quota limits and file sizes are hardcoded throughout tests, creating maintenance burden and risk of drift from actual configuration.

**Location:**
- Multiple files across test suite

**Current Code:**
```csharp
// In tests
const int FreeTierDailyLimit = 5;
const int FreeTierWeeklyLimit = 20;

// Elsewhere
var nearLimitSize = (10 * 1024 * 1024) - 1024;
var overLimitSize = (10 * 1024 * 1024) + 1024;
```

**Problem:**
- Values duplicated across multiple test files
- No single source of truth
- Easy for tests to drift from actual configuration
- Difficult to update when limits change

**Proposed Fix:**

Create shared test constants:

```csharp
// apps/api/tests/Api.Tests/Constants/TestConstants.cs
namespace Api.Tests.Constants;

public static class PdfUploadTestConstants
{
    // Quota Limits (must match production configuration)
    public static class QuotaLimits
    {
        public const int FreeTierDaily = 5;
        public const int FreeTierWeekly = 20;
        public const int NormalTierDaily = 20;
        public const int NormalTierWeekly = 100;
        public const int PremiumTierDaily = 100;
        public const int PremiumTierWeekly = 500;
    }

    // File Sizes
    public static class FileSizes
    {
        public const int TestMaxBytes = 10 * 1024 * 1024; // 10 MB for test efficiency
        public const int SmallPdf = 50 * 1024; // 50 KB
        public const int MediumPdf = 1024 * 1024; // 1 MB
        public const int LargePdf = 5 * 1024 * 1024; // 5 MB
        public const int NearLimit = TestMaxBytes - 1024;
        public const int OverLimit = TestMaxBytes + 1024;
    }

    // Test Data
    public static class TestData
    {
        public const string TestUserEmail = "test@uploadtest.com";
        public const string TestGameName = "Test Game for PDF Upload";
    }
}
```

**Acceptance Criteria:**
- [ ] All magic numbers extracted to constants
- [ ] Constants documented with source of truth
- [ ] All tests updated to use constants
- [ ] Documentation added explaining how to update limits

**Priority:** High - Reduces technical debt and maintenance burden

---

### Issue #6: Overly broad error message assertions reduce test value

**Labels:** `tests`, `high`, `quality`, `pdf-upload`

**Description:**

Many tests use overly broad assertions like `Contains("Invalid")` which could match many different error scenarios, reducing the test's ability to catch regressions.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 384, 414, 445, etc.

**Current Code:**
```csharp
result.Message.Should().Contain("Invalid", "file type or corrupted content should be rejected");
```

**Problem:**
"Invalid" could match:
- "Invalid file type"
- "Invalid file size"
- "Invalid user"
- "Invalid game"
- Any other validation failure

This means the test could pass even if the wrong validation is triggered.

**Proposed Fix:**
```csharp
// More specific assertion
result.Message.Should().Match(m =>
    m.Contains("Invalid PDF", StringComparison.OrdinalIgnoreCase) ||
    m.Contains("corrupted", StringComparison.OrdinalIgnoreCase),
    "should indicate PDF validation failure specifically");

// Or use exact message matching for critical validations
result.Message.Should().Be("Invalid PDF file: Missing PDF header signature. File appears to be corrupted or not a valid PDF.");
```

**Acceptance Criteria:**
- [ ] All error message assertions use specific matching
- [ ] Tests fail if wrong validation is triggered
- [ ] Error messages documented as part of API contract

**Priority:** High - Improves test reliability and regression detection

---

### Issue #7: InMemoryDatabase used to test real database failures

**Labels:** `bug`, `tests`, `high`, `pdf-upload`

**Description:**

Storage failure tests use InMemory database which behaves differently from PostgreSQL, making the tests less valuable.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 717-771 (multiple tests)

**Current Code:**
```csharp
services.AddDbContext<MeepleAiDbContext>(options =>
{
    options.UseInMemoryDatabase("BlobStorageFailureTest");
});
```

**Problem:**
- InMemory database doesn't enforce constraints the same way PostgreSQL does
- Transaction behavior is different
- Connection failures can't be simulated
- Tests provide false confidence

**Proposed Fix:**

Use Testcontainers with PostgreSQL for all failure scenarios:

```csharp
// Use the existing PostgreSQL container and simulate failures
var failureServiceProvider = services.BuildServiceProvider();
var testDbContext = failureServiceProvider.GetRequiredService<MeepleAiDbContext>();

// Simulate failures by:
// 1. Closing connections mid-transaction
// 2. Violating constraints
// 3. Simulating deadlocks
// 4. Testing rollback behavior
```

**Acceptance Criteria:**
- [ ] All database failure tests use real PostgreSQL via Testcontainers
- [ ] Tests verify actual transaction rollback behavior
- [ ] Tests simulate realistic failure scenarios
- [ ] Test coverage maintained or improved

**Priority:** High - Tests need to simulate real database behavior

---

## Medium Priority (P2) - Fix Next Sprint

### Issue #8: Significant code duplication in mock setup

**Labels:** `refactoring`, `tests`, `medium`, `maintainability`, `pdf-upload`

**Description:**

The `RegisterMockServices` method and individual test mock setups contain significant duplication, making tests harder to maintain and update.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 185-253 and throughout test methods

**Problem:**
- Same mock setup logic repeated across multiple tests
- Changes require updating multiple locations
- Difficult to maintain consistency
- Verbose test setup obscures test intent

**Proposed Solution:**

Implement Test Fixture Builder pattern:

```csharp
// apps/api/tests/Api.Tests/Helpers/PdfUploadTestFixtureBuilder.cs
public class PdfUploadTestFixtureBuilder
{
    private readonly ServiceCollection _services = new();
    private Mock<IBlobStorageService>? _blobStorage;
    private Mock<IPdfTextExtractor>? _textExtractor;
    private Mock<IPdfUploadQuotaService>? _quotaService;
    private string? _tempDirectory;

    public static PdfUploadTestFixtureBuilder Create() => new();

    public PdfUploadTestFixtureBuilder WithSuccessfulBlobStorage(string tempDir)
    {
        _tempDirectory = tempDir;
        _blobStorage = new Mock<IBlobStorageService>();
        _blobStorage.Setup(b => b.StoreAsync(/*...*/))
            .ReturnsAsync(/*...*/);
        return this;
    }

    public PdfUploadTestFixtureBuilder WithFailingBlobStorage(string errorMessage)
    {
        _blobStorage = new Mock<IBlobStorageService>();
        _blobStorage.Setup(b => b.StoreAsync(/*...*/))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, errorMessage));
        return this;
    }

    public PdfUploadTestFixtureBuilder WithQuotaLimit(int daily, int weekly)
    {
        _quotaService = new Mock<IPdfUploadQuotaService>();
        _quotaService.Setup(q => q.CheckQuotaAsync(/*...*/))
            .ReturnsAsync(PdfUploadQuotaResult.Success(daily, daily, weekly, weekly, /*...*/));
        return this;
    }

    public PdfUploadTestFixtureBuilder WithQuotaDenied(string reason)
    {
        _quotaService = new Mock<IPdfUploadQuotaService>();
        _quotaService.Setup(q => q.CheckQuotaAsync(/*...*/))
            .ReturnsAsync(PdfUploadQuotaResult.Denied(reason, 5, 5, 20, 20, /*...*/));
        return this;
    }

    public IServiceProvider Build()
    {
        // Register all mocks
        if (_blobStorage != null)
            _services.AddSingleton(_blobStorage.Object);
        // ... register other services

        return _services.BuildServiceProvider();
    }
}

// Usage in tests
var fixture = PdfUploadTestFixtureBuilder.Create()
    .WithFailingBlobStorage("Simulated storage failure")
    .WithQuotaLimit(5, 20)
    .Build();
```

**Acceptance Criteria:**
- [ ] Builder pattern implemented for test fixtures
- [ ] All integration tests updated to use builder
- [ ] Code duplication reduced by >50%
- [ ] Tests remain readable and maintainable
- [ ] Test execution time not impacted

**Priority:** Medium - Improves maintainability

---

### Issue #9: Inconsistent assertion style across test suite

**Labels:** `refactoring`, `tests`, `medium`, `code-style`, `pdf-upload`

**Description:**

Test suite mixes xUnit assertions (`Assert.*`) with FluentAssertions (`Should()`), creating inconsistent style and reduced readability.

**Location:**
- Multiple test files

**Current Code:**
```csharp
// Mixed styles
Assert.NotNull(handler);  // xUnit
result.Success.Should().BeTrue();  // FluentAssertions
Assert.Equal(5, count);  // xUnit
doc.Should().NotBeNull();  // FluentAssertions
```

**Proposed Fix:**

Standardize on FluentAssertions throughout:

```csharp
// Consistent FluentAssertions style
handler.Should().NotBeNull();
result.Success.Should().BeTrue();
count.Should().Be(5);
doc.Should().NotBeNull();
```

**Acceptance Criteria:**
- [ ] All tests use FluentAssertions consistently
- [ ] No xUnit assertions remain (except where FluentAssertions doesn't support)
- [ ] Code review checklist updated to enforce FluentAssertions
- [ ] All tests still pass

**Priority:** Medium - Improves code consistency and readability

---

### Issue #10: Missing cancellation token tests

**Labels:** `enhancement`, `tests`, `medium`, `pdf-upload`

**Description:**

No tests verify that cancellation tokens are properly respected throughout the upload pipeline.

**Location:**
- All handler test files

**Missing Coverage:**
- Cancellation during file upload
- Cancellation during blob storage
- Cancellation during text extraction
- Cancellation during embedding generation
- Cancellation during vector indexing

**Proposed Tests:**

```csharp
[Fact(Timeout = 5000)]
public async Task UploadPdf_WhenCancelledDuringUpload_ThrowsOperationCanceledException()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.Cancel();

    var handler = CreateHandler();
    var command = CreateValidCommand();

    // Act & Assert
    await handler.Awaiting(h => h.Handle(command, cts.Token))
        .Should().ThrowAsync<OperationCanceledException>();
}

[Fact(Timeout = 10000)]
public async Task UploadPdf_WhenCancelledDuringProcessing_CleansUpResources()
{
    // Arrange
    var cts = new CancellationTokenSource();
    var handler = CreateHandler();
    var command = CreateValidCommand();

    // Act - Start upload, cancel mid-processing
    var uploadTask = handler.Handle(command, cts.Token);
    await Task.Delay(100); // Let it start
    cts.Cancel();

    // Assert
    await uploadTask.Should().ThrowAsync<OperationCanceledException>();

    // Verify cleanup
    var doc = await GetDocument();
    doc.ProcessingStatus.Should().Be("failed");
    doc.ProcessingError.Should().Contain("cancelled");
}
```

**Acceptance Criteria:**
- [ ] Tests for cancellation at each pipeline stage
- [ ] Tests verify proper resource cleanup on cancellation
- [ ] Tests verify database state is consistent after cancellation
- [ ] All tests use timeouts to prevent hangs

**Priority:** Medium - Important for production reliability

---

### Issue #11: Unreliable GC.Collect() in performance tests

**Labels:** `bug`, `tests`, `medium`, `flaky`, `pdf-upload`

**Description:**

Memory efficiency test uses `GC.Collect()` which is unreliable and can cause flaky tests.

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 574-586

**Current Code:**
```csharp
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var initialMemory = GC.GetTotalMemory(false);

// ... perform upload

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var finalMemory = GC.GetTotalMemory(false);

var memoryGrowth = finalMemory - initialMemory;
memoryGrowth.Should().BeLessThan(largePdfSize * 2);
```

**Problem:**
- GC timing is non-deterministic
- Memory measurements vary by runtime and environment
- Test can be flaky based on GC behavior
- LOH allocations may not be collected immediately

**Proposed Fix:**

Option 1: Remove memory assertions (not reliable in tests)
```csharp
// Remove GC.Collect() and memory assertions
// Trust that proper using statements handle resource disposal
```

Option 2: Use memory profiling tools instead
```csharp
// Use dotMemory or similar for actual memory profiling
// Keep test focused on functional correctness
```

Option 3: Assert on resource disposal, not memory
```csharp
// Verify streams are disposed, not memory usage
var streamDisposed = false;
var mockStream = new MemoryStream();
mockStream.Disposed += () => streamDisposed = true;

// ... perform upload

streamDisposed.Should().BeTrue("stream should be disposed after upload");
```

**Acceptance Criteria:**
- [ ] Remove or replace unreliable GC.Collect() assertions
- [ ] Test doesn't fail intermittently due to GC timing
- [ ] If keeping memory tests, document acceptable variance
- [ ] Consider moving memory tests to manual profiling suite

**Priority:** Medium - Can cause flaky test failures

---

### Issue #12: Missing test data builders

**Labels:** `enhancement`, `tests`, `medium`, `maintainability`, `pdf-upload`

**Description:**

Tests create command and entity objects inline, leading to verbose setup code that obscures test intent.

**Current State:**
```csharp
var command = new UploadPdfCommand(
    GameId: testGame.Id.ToString(),
    UserId: testUser.Id,
    File: formFile);
```

**Proposed Solution:**

Create fluent test data builders:

```csharp
// apps/api/tests/Api.Tests/Builders/PdfUploadCommandBuilder.cs
public class PdfUploadCommandBuilder
{
    private string _gameId = Guid.NewGuid().ToString();
    private Guid _userId = Guid.NewGuid();
    private IFormFile _file;

    public PdfUploadCommandBuilder()
    {
        _file = CreateValidPdf(50 * 1024); // 50KB default
    }

    public PdfUploadCommandBuilder ForGame(GameEntity game)
    {
        _gameId = game.Id.ToString();
        return this;
    }

    public PdfUploadCommandBuilder ByUser(UserEntity user)
    {
        _userId = user.Id;
        return this;
    }

    public PdfUploadCommandBuilder WithCorruptedFile()
    {
        _file = CreateCorruptedPdf();
        return this;
    }

    public PdfUploadCommandBuilder WithOversizedFile()
    {
        _file = CreateValidPdf(11 * 1024 * 1024); // 11 MB
        return this;
    }

    public PdfUploadCommandBuilder WithFile(IFormFile file)
    {
        _file = file;
        return this;
    }

    public UploadPdfCommand Build() => new(_gameId, _userId, _file);

    private static IFormFile CreateValidPdf(int sizeBytes) { /*...*/ }
    private static IFormFile CreateCorruptedPdf() { /*...*/ }
}

// Usage in tests
var command = new PdfUploadCommandBuilder()
    .ForGame(testGame)
    .ByUser(testUser)
    .WithCorruptedFile()
    .Build();
```

**Acceptance Criteria:**
- [ ] Builders created for UploadPdfCommand
- [ ] Builders created for test entities (User, Game, PdfDocument)
- [ ] At least 5 tests updated to use builders as proof of concept
- [ ] Documentation added for using builders

**Priority:** Medium - Improves test readability

---

### Issue #13: Missing parameterized tests for validation scenarios

**Labels:** `enhancement`, `tests`, `medium`, `pdf-upload`

**Description:**

Similar validation tests are duplicated instead of using parameterized tests (Theory).

**Location:**
- File: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs`
- Lines: 362-482 (Invalid PDF Scenarios)

**Current Code:**
```csharp
[Fact]
public async Task UploadPdf_WithCorruptedPdf_ReturnsError() { /*...*/ }

[Fact]
public async Task UploadPdf_WithNonPdfFile_ReturnsError() { /*...*/ }

[Fact]
public async Task UploadPdf_WithEmptyFile_ReturnsError() { /*...*/ }

[Fact]
public async Task UploadPdf_WithMalformedPdfStructure_ReturnsError() { /*...*/ }
```

**Proposed Refactoring:**

```csharp
public enum TestFileType
{
    Corrupted,
    NonPdf,
    Empty,
    Malformed,
    ValidSmall,
    ValidLarge
}

[Theory]
[InlineData(TestFileType.Corrupted, "Invalid PDF")]
[InlineData(TestFileType.NonPdf, "file type")]
[InlineData(TestFileType.Empty, "No file provided")]
[InlineData(TestFileType.Malformed, "incomplete")]
public async Task UploadPdf_WithInvalidFile_ReturnsExpectedError(
    TestFileType fileType,
    string expectedErrorFragment)
{
    // Arrange
    var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
    var testUser = await _dbContext!.Users.FirstAsync();
    var testGame = await _dbContext.Games.FirstAsync();

    var file = CreateTestFile(fileType);
    var command = new UploadPdfCommand(
        GameId: testGame.Id.ToString(),
        UserId: testUser.Id,
        File: file);

    // Act
    var result = await handler.Handle(command, TestCancellationToken);

    // Assert
    result.Success.Should().BeFalse($"{fileType} file should be rejected");
    result.Message.Should().Contain(expectedErrorFragment,
        $"error for {fileType} should mention '{expectedErrorFragment}'");
    result.Document.Should().BeNull();

    // Verify no database record created
    var docCount = await _dbContext.PdfDocuments.CountAsync();
    docCount.Should().Be(0, $"{fileType} file should not create database record");
}

private IFormFile CreateTestFile(TestFileType type) => type switch
{
    TestFileType.Corrupted => CreateMockFormFile("corrupted.pdf", CreateCorruptedPdfBytes()),
    TestFileType.NonPdf => CreateMockFormFile("document.txt", "text"u8.ToArray(), "text/plain"),
    TestFileType.Empty => CreateMockFormFile("empty.pdf", Array.Empty<byte>()),
    TestFileType.Malformed => CreateMockFormFile("malformed.pdf", "%PDF-1.4\nincomplete"u8.ToArray()),
    _ => throw new ArgumentException($"Unknown file type: {type}")
};
```

**Benefits:**
- Reduces code duplication
- Makes adding new test cases trivial
- Improves test maintainability
- Makes test patterns more visible

**Acceptance Criteria:**
- [ ] Invalid file scenarios combined into Theory test
- [ ] File size scenarios combined into Theory test
- [ ] At least 30% reduction in test code lines
- [ ] All original test cases still covered

**Priority:** Medium - Reduces maintenance burden

---

### Issue #14: No test categories or traits

**Labels:** `enhancement`, `tests`, `medium`, `organization`, `pdf-upload`

**Description:**

Tests lack categorization making it difficult to run subsets of tests (e.g., only fast tests, only integration tests).

**Proposed Implementation:**

```csharp
// Create test categories
public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Performance = "Performance";
    public const string Security = "Security";
    public const string Slow = "Slow";
}

// Apply to tests
[Fact]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Slow)]
public async Task UploadPdf_WithLargeFile_HandlesMemoryEfficiently()
{
    // ...
}

[Theory]
[Trait("Category", TestCategories.Security)]
[InlineData("../../etc/passwd.pdf")]
[InlineData("<script>alert('xss')</script>.pdf")]
public async Task UploadPdf_WithMaliciousFilename_Rejects(string filename)
{
    // ...
}
```

**Usage:**
```bash
# Run only fast tests
dotnet test --filter "Category!=Slow"

# Run only security tests
dotnet test --filter "Category=Security"

# Run integration tests
dotnet test --filter "Category=Integration"
```

**Acceptance Criteria:**
- [ ] Test category constants created
- [ ] All tests categorized appropriately
- [ ] CI pipeline updated to use categories
- [ ] Documentation updated with category usage

**Priority:** Medium - Improves developer experience

---

### Issue #15: Missing custom assertions for domain objects

**Labels:** `enhancement`, `tests`, `medium`, `readability`, `pdf-upload`

**Description:**

Tests repeat complex assertion patterns. Custom assertions would improve readability and reusability.

**Proposed Implementation:**

```csharp
// apps/api/tests/Api.Tests/Assertions/PdfUploadAssertions.cs
public static class PdfUploadAssertions
{
    public static PdfUploadResultAssertions Should(this PdfUploadResult result)
        => new(result);
}

public class PdfUploadResultAssertions
{
    private readonly PdfUploadResult _result;

    public PdfUploadResultAssertions(PdfUploadResult result)
        => _result = result;

    public AndConstraint<PdfUploadResultAssertions> BeSuccessful(
        string because = "",
        params object[] becauseArgs)
    {
        _result.Success.Should().BeTrue(because, becauseArgs);
        _result.Document.Should().NotBeNull(because, becauseArgs);
        _result.Document!.Id.Should().NotBeEmpty(because, becauseArgs);
        _result.Message.Should().NotBeNullOrWhiteSpace(because, becauseArgs);
        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    public AndConstraint<PdfUploadResultAssertions> FailWith(
        string expectedMessage,
        string because = "",
        params object[] becauseArgs)
    {
        _result.Success.Should().BeFalse(because, becauseArgs);
        _result.Message.Should().Contain(expectedMessage, because, becauseArgs);
        _result.Document.Should().BeNull(because, becauseArgs);
        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    public AndConstraint<PdfUploadResultAssertions> FailDueToQuota(
        string because = "",
        params object[] becauseArgs)
    {
        return FailWith("quota", because, becauseArgs)
            .And.Message.Should().Match(m =>
                m.Contains("limit", StringComparison.OrdinalIgnoreCase) &&
                (m.Contains("daily", StringComparison.OrdinalIgnoreCase) ||
                 m.Contains("weekly", StringComparison.OrdinalIgnoreCase)));
    }
}

// Usage
result.Should().BeSuccessful("valid PDF within quota should succeed");
result.Should().FailWith("Invalid PDF", "corrupted file should be rejected");
result.Should().FailDueToQuota("user has exceeded daily limit");
```

**Acceptance Criteria:**
- [ ] Custom assertions created for PdfUploadResult
- [ ] Custom assertions created for PdfUploadQuotaResult
- [ ] At least 10 tests updated to use custom assertions
- [ ] Documentation added for creating custom assertions

**Priority:** Medium - Improves test readability

---

## Low Priority (P3) - Technical Debt

### Issue #16: Potential idempotency issue in background processing

**Labels:** `bug`, `low`, `edge-case`, `pdf-upload`

**Description:**

If a background processing task is queued twice (e.g., due to retry logic), the same PDF could be processed multiple times.

**Location:**
- File: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- Line: 240

**Current Code:**
```csharp
_backgroundTaskService.ExecuteWithCancellation(
    storageResult.FileId!,
    (ct) => ProcessPdfAsync(storageResult.FileId!, storageResult.FilePath!, ct));
```

**Problem:**
No check to prevent duplicate processing if task is queued multiple times.

**Proposed Fix:**
```csharp
private async Task ProcessPdfAsync(string pdfId, string filePath, CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // Idempotency check
    var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
    if (pdfDoc == null)
    {
        _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
        return;
    }

    if (pdfDoc.ProcessingStatus != "pending")
    {
        _logger.LogInformation(
            "PDF {PdfId} already processed (status: {Status}), skipping duplicate processing",
            pdfId,
            pdfDoc.ProcessingStatus);
        return;
    }

    // Continue with processing...
}
```

**Acceptance Criteria:**
- [ ] Idempotency check added to ProcessPdfAsync
- [ ] Test added for duplicate processing scenario
- [ ] Logging added for duplicate detection
- [ ] No negative impact on normal processing flow

**Priority:** Low - Edge case, unlikely in current implementation

---

### Issue #17: Quota incremented before processing completes

**Labels:** `enhancement`, `low`, `business-logic`, `pdf-upload`

**Description:**

Quota is incremented immediately after upload, even if background processing fails. Users who upload files that fail processing still consume their quota.

**Location:**
- File: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- Line: 243

**Current Code:**
```csharp
// Extract text asynchronously
_backgroundTaskService.ExecuteWithCancellation(/*...*/);

// Increment upload count after successful upload
await _quotaService.IncrementUploadCountAsync(userId, cancellationToken);
```

**Problem:**
If PDF fails processing (extraction error, indexing error), user has consumed quota but gained no value.

**Business Impact:**
- Poor user experience for users with failed uploads
- Users may hit limits without successfully processing PDFs

**Proposed Solution:**

Implement two-phase quota:

```csharp
// Phase 1: Reserve quota slot
await _quotaService.ReserveUploadSlotAsync(userId, pdfId, cancellationToken);

// Phase 2: In ProcessPdfAsync, confirm or release
if (processingSuccessful)
{
    await quotaService.ConfirmUploadAsync(userId, pdfId, ct);
}
else
{
    await quotaService.ReleaseUploadSlotAsync(userId, pdfId, ct);
}
```

**Alternative:** Compensating transaction
```csharp
// In ProcessPdfAsync on failure
if (processingFailed)
{
    await _quotaService.DecrementUploadCountAsync(userId, ct);
    _logger.LogInformation("Released quota for failed upload {PdfId}", pdfId);
}
```

**Acceptance Criteria:**
- [ ] Product owner consulted on desired behavior
- [ ] Solution implemented (two-phase or compensating)
- [ ] Tests added for quota release on failure
- [ ] Documentation updated

**Priority:** Low - Business decision needed, current behavior may be intentional

---

### Issue #18: Parallel test execution disabled

**Labels:** `performance`, `low`, `test-infrastructure`, `pdf-upload`

**Description:**

`PdfUploadQuotaEnforcementIntegrationTests` runs sequentially due to Redis state sharing, significantly increasing test suite duration.

**Current State:**
- Test suite: ~2 minutes
- Sequential execution due to `[Collection]` attribute

**Potential Improvement:**
- Parallel execution: ~30 seconds
- 4x speed improvement

**Proposed Solution:**

Use unique Redis key prefixes per test:

```csharp
public class PdfUploadQuotaEnforcementIntegrationTests : IAsyncLifetime
{
    private readonly string _testPrefix = Guid.NewGuid().ToString("N");

    public async ValueTask InitializeAsync()
    {
        // ... container setup

        // Register quota service with test-specific prefix
        services.AddScoped<IPdfUploadQuotaService>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var config = sp.GetRequiredService<IConfigurationService>();
            var logger = sp.GetRequiredService<ILogger<PdfUploadQuotaService>>();

            return new PdfUploadQuotaService(redis, config, logger, TimeProvider.System)
            {
                KeyPrefix = $"test:{_testPrefix}:"
            };
        });
    }
}
```

**Challenges:**
- Requires modifying `PdfUploadQuotaService` to support key prefix
- Need to ensure cleanup of test keys

**Acceptance Criteria:**
- [ ] Tests can run in parallel without conflicts
- [ ] Test suite duration reduced by >50%
- [ ] No test flakiness introduced
- [ ] Redis keys properly cleaned up

**Priority:** Low - Nice to have, current speed acceptable

---

### Issue #19: Shared container optimization opportunity

**Labels:** `performance`, `low`, `test-infrastructure`, `pdf-upload`

**Description:**

Each test class creates new Testcontainers (~10s overhead each), slowing down test suite.

**Current State:**
- Container startup: ~10s per test class
- Total overhead: ~30-40s for multiple test classes

**Proposed Solution:**

Use shared containers across test classes:

```csharp
[CollectionDefinition("SharedInfrastructure")]
public class SharedInfrastructureCollection :
    ICollectionFixture<SharedContainersFixture>
{
}

public class SharedContainersFixture : IAsyncLifetime
{
    public IContainer PostgresContainer { get; private set; }
    public IContainer RedisContainer { get; private set; }

    public async Task InitializeAsync()
    {
        // Start containers once
        PostgresContainer = await CreatePostgresContainer();
        RedisContainer = await CreateRedisContainer();
    }

    public async Task DisposeAsync()
    {
        // Stop containers at end of all tests
        await PostgresContainer?.DisposeAsync();
        await RedisContainer?.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // Truncate tables between tests instead of recreating containers
        // Much faster than container recreation
    }
}

// Usage
[Collection("SharedInfrastructure")]
public class UploadPdfIntegrationTests
{
    private readonly SharedContainersFixture _containers;

    public UploadPdfIntegrationTests(SharedContainersFixture containers)
    {
        _containers = containers;
    }
}
```

**Benefits:**
- Faster test suite execution
- Reduced CI/CD time
- Lower resource usage

**Challenges:**
- Need proper cleanup between tests
- Potential for test pollution
- More complex fixture management

**Acceptance Criteria:**
- [ ] Shared container fixture implemented
- [ ] Proper cleanup between tests
- [ ] Test suite duration reduced by >20s
- [ ] No test flakiness introduced

**Priority:** Low - Optimization, not critical

---

### Issue #20: Missing security tests

**Labels:** `security`, `tests`, `low`, `pdf-upload`

**Description:**

No tests verify security protections against common attack vectors.

**Missing Test Coverage:**

1. **Path Traversal:**
```csharp
[Theory]
[Trait("Category", TestCategories.Security)]
[InlineData("../../etc/passwd.pdf")]
[InlineData("..\\..\\windows\\system32\\config\\sam.pdf")]
[InlineData("/etc/passwd.pdf")]
[InlineData("C:\\Windows\\System32\\config\\sam.pdf")]
public async Task UploadPdf_WithPathTraversalAttempt_Sanitizes(string maliciousFilename)
{
    var command = new PdfUploadCommandBuilder()
        .WithFilename(maliciousFilename)
        .Build();

    var result = await handler.Handle(command, CancellationToken.None);

    // Should either reject or sanitize
    if (result.Success)
    {
        var doc = await GetDocument(result.Document!.Id);
        doc.FilePath.Should().NotContain("..");
        doc.FileName.Should().NotContain("..");
    }
}
```

2. **XSS in Filenames:**
```csharp
[Theory]
[Trait("Category", TestCategories.Security)]
[InlineData("<script>alert('xss')</script>.pdf")]
[InlineData("javascript:alert('xss').pdf")]
[InlineData("on error=alert('xss').pdf")]
public async Task UploadPdf_WithXSSInFilename_Sanitizes(string xssFilename)
{
    // Test that filename is sanitized and doesn't execute as script
}
```

3. **SQL Injection:**
```csharp
[Theory]
[Trait("Category", TestCategories.Security)]
[InlineData("'; DROP TABLE pdf_documents; --.pdf")]
[InlineData("1' OR '1'='1.pdf")]
public async Task UploadPdf_WithSQLInjectionAttempt_DoesNotExecute(string sqlFilename)
{
    // Test that SQL injection is prevented
}
```

4. **Null Byte Injection:**
```csharp
[Fact]
[Trait("Category", TestCategories.Security)]
public async Task UploadPdf_WithNullByteInFilename_Rejects()
{
    var filename = "file\0.pdf.exe";
    // Should reject or sanitize null byte
}
```

5. **Unicode/RTL Attacks:**
```csharp
[Theory]
[Trait("Category", TestCategories.Security)]
[InlineData("file\u202Efdp.exe")] // RTL override to disguise .exe as .pdf
[InlineData("مستند.pdf")] // Arabic filename
[InlineData("文档.pdf")] // Chinese filename
[InlineData("😀.pdf")] // Emoji filename
public async Task UploadPdf_WithUnicodeFilename_HandlesCorrectly(string unicodeFilename)
{
    // Test Unicode handling
}
```

**Acceptance Criteria:**
- [ ] All security test scenarios implemented
- [ ] Tests verify PathSecurity.SanitizeFilename works correctly
- [ ] Tests verify no code execution possible
- [ ] Tests verify SQL injection prevented
- [ ] Documentation added for security testing strategy

**Priority:** Low - Security protections likely exist, tests provide verification

---

### Issue #21: Missing edge case tests

**Labels:** `tests`, `low`, `coverage`, `pdf-upload`

**Description:**

Several edge cases lack test coverage.

**Missing Test Cases:**

1. **Exactly 0 byte file:**
```csharp
[Fact]
public async Task UploadPdf_WithExactlyZeroBytes_Rejects()
{
    var file = CreateMockFormFile("empty.pdf", new byte[0]);
    // Should reject with clear message
}
```

2. **Exactly max size file:**
```csharp
[Fact]
public async Task UploadPdf_WithExactlyMaxSize_Accepts()
{
    var maxSize = 10 * 1024 * 1024; // Exactly 10 MB
    var file = CreateMockFormFile("exact.pdf", CreateValidPdfBytes(maxSize));
    // Should accept (boundary condition)
}
```

3. **PDF with valid header but corrupted body:**
```csharp
[Fact]
public async Task UploadPdf_WithValidHeaderCorruptedBody_Rejects()
{
    var bytes = CreatePdfWithValidHeaderButCorruptedBody();
    // Should detect during validation
}
```

4. **Very long filename (>255 chars):**
```csharp
[Fact]
public async Task UploadPdf_WithVeryLongFilename_Truncates()
{
    var longName = new string('a', 300) + ".pdf";
    // Should truncate or reject
}
```

5. **Filename with only extension:**
```csharp
[Fact]
public async Task UploadPdf_WithOnlyExtension_Rejects()
{
    var file = CreateMockFormFile(".pdf", CreateValidPdfBytes(1024));
    // Should reject or generate name
}
```

6. **Concurrent uploads of same file:**
```csharp
[Fact]
public async Task UploadPdf_SameFileConcurrently_BothSucceed()
{
    // Test deduplication or both succeed
}
```

**Acceptance Criteria:**
- [ ] All edge cases tested
- [ ] Behavior documented
- [ ] No unexpected failures in production

**Priority:** Low - Edge cases, unlikely scenarios

---

## Summary

| Priority | Count | Must Complete By |
|----------|-------|------------------|
| P0 (Critical) | 3 | Before merge |
| P1 (High) | 4 | This sprint |
| P2 (Medium) | 8 | Next sprint |
| P3 (Low) | 6 | Backlog |
| **Total** | **21** | |

## Recommended Action Order

1. **Week 1:** Fix P0 issues #1, #2, #3
2. **Week 2:** Fix P1 issues #4, #5, #6, #7
3. **Week 3:** Address P2 refactoring #8, #9, #10
4. **Week 4:** Complete P2 enhancements #11-15
5. **Backlog:** P3 issues for future sprints

---

**Generated:** 2025-11-23
**Review Version:** 1.0
**Codebase:** meepleai-monorepo
**Component:** PDF Upload Test Suite
