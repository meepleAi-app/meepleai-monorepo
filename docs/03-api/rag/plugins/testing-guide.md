# Plugin Testing Guide

> **Comprehensive Guide to Testing RAG Plugins**

This guide covers how to test your plugins using the MeepleAI Plugin Testing Framework. It provides patterns, examples, and best practices for ensuring plugin quality.

## Overview

The testing framework provides four main components:

| Component | Purpose |
|-----------|---------|
| `PluginTestHarness<T>` | Base class with built-in contract tests |
| `PluginMocks` | Factory for creating test data |
| `PluginContractTests` | Standalone contract verification |
| `PluginBenchmarks` | Performance measurement utilities |

## Quick Start

### 1. Create Test Class

```csharp
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

public class MyPluginTests : PluginTestHarness<MyPlugin>
{
    protected override MyPlugin CreatePlugin()
    {
        return new MyPlugin(Logger);
    }

    // Inherited: 10+ contract tests automatically included
}
```

### 2. Run Tests

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~MyPluginTests"
```

### 3. Add Custom Tests

```csharp
public class MyPluginTests : PluginTestHarness<MyPlugin>
{
    protected override MyPlugin CreatePlugin() => new MyPlugin(Logger);

    [Fact]
    public async Task ExecuteAsync_WithSpecificInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = PluginMocks.CreateQueryInput("test query");

        // Act
        var output = await Plugin.ExecuteAsync(input);

        // Assert
        output.Success.Should().BeTrue();
        output.Result.Should().NotBeNull();
    }
}
```

---

## Using PluginTestHarness

### Inherited Contract Tests

When you extend `PluginTestHarness<TPlugin>`, you automatically get these tests:

```csharp
// Identity tests
[Fact] Plugin_HasValidId()           // Id format and length
[Fact] Plugin_HasValidName()         // Name not empty
[Fact] Plugin_HasValidVersion()      // SemVer format
[Fact] Plugin_HasValidCategory()     // Valid enum value

// Schema tests
[Fact] Plugin_InputSchema_IsValidJsonSchema()
[Fact] Plugin_OutputSchema_IsValidJsonSchema()
[Fact] Plugin_ConfigSchema_IsValidJsonSchema()
[Fact] Plugin_Metadata_IsConsistent()  // Metadata matches properties

// Validation tests
[Fact] ValidateConfig_WithDefaultConfig_Succeeds()
[Fact] ValidateInput_WithValidInput_Succeeds()

// Execution tests
[Fact] ExecuteAsync_WithValidInput_ReturnsOutput()
[Fact] HealthCheckAsync_ReturnsValidResult()
```

### Customizing the Harness

**Custom Valid Input**:
```csharp
protected override PluginInput CreateValidInput()
{
    return PluginMocks.CreateQueryInput(
        query: "How do I play this game?",
        gameId: Guid.NewGuid()
    );
}
```

**Custom Configuration**:
```csharp
protected override PluginConfig CreateDefaultConfig()
{
    return new PluginConfig
    {
        TimeoutMs = 60000,
        Parameters = JsonDocument.Parse("""
            {"model": "gpt-4", "maxTokens": 500}
            """)
    };
}
```

**Setup with Dependencies**:
```csharp
private readonly Mock<IVectorStore> _vectorStoreMock = new();

protected override MyPlugin CreatePlugin()
{
    _vectorStoreMock
        .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
        .ReturnsAsync(new[] { CreateTestDocument() });

    return new MyPlugin(Logger, _vectorStoreMock.Object);
}
```

---

## Using PluginMocks

### Input Factories

**Basic Input**:
```csharp
// Simple valid input
var input = PluginMocks.CreateValidInput();

// With specific execution ID
var input = PluginMocks.CreateValidInput(executionId: Guid.NewGuid());
```

**Query Input**:
```csharp
// Basic query
var input = PluginMocks.CreateQueryInput("How do I set up the game?");

// With game and user context
var input = PluginMocks.CreateQueryInput(
    query: "How do I set up Catan?",
    gameId: Guid.Parse("..."),
    userId: Guid.Parse("...")
);
```

**Custom Payload**:
```csharp
var input = PluginMocks.CreateInputWithPayload("""
    {
        "query": "custom query",
        "options": {
            "topK": 5,
            "includeMetadata": true
        }
    }
    """);
```

**With Pipeline Context**:
```csharp
var previousOutput = PluginMocks.CreateSuccessfulOutput(executionId);
var input = PluginMocks.CreateInputWithPreviousOutputs(
    executionId,
    new Dictionary<string, PluginOutput>
    {
        ["router-1"] = previousOutput
    }
);
```

### Output Factories

**Successful Output**:
```csharp
var output = PluginMocks.CreateSuccessfulOutput(executionId);

// With custom result
var output = PluginMocks.CreateSuccessfulOutput(
    executionId,
    JsonDocument.Parse("""{"answer": "Test answer"}""")
);

// With confidence
var output = PluginMocks.CreateSuccessfulOutput(executionId, confidence: 0.95);
```

**Failed Output**:
```csharp
var output = PluginMocks.CreateFailedOutput(
    executionId,
    errorMessage: "Vector store unavailable",
    errorCode: "CONNECTION_FAILED"
);
```

**Retrieval Output**:
```csharp
var documents = PluginMocks.CreateRetrievedDocuments(count: 5);
var output = PluginMocks.CreateRetrievalOutput(executionId, documents);
```

### Invalid Input Factories

For testing validation:

```csharp
// Empty execution ID
var invalidInput = PluginMocks.CreateInvalidInput_EmptyExecutionId();

// Null payload
var invalidInput = PluginMocks.CreateInvalidInput_NullPayload();

// Missing required fields
var invalidInput = PluginMocks.CreateInvalidInput_MissingQuery();
```

### Configuration Factories

```csharp
// Default config
var config = PluginMocks.CreateDefaultConfig();

// Custom timeout
var config = PluginMocks.CreateConfigWithTimeout(5000);

// Invalid configs for testing validation
var config = PluginMocks.CreateInvalidConfig_NegativeTimeout();
var config = PluginMocks.CreateInvalidConfig_NegativeRetries();
```

### Health Check Factories

```csharp
var healthy = PluginMocks.CreateHealthyResult();
var degraded = PluginMocks.CreateDegradedResult("Cache unavailable");
var unhealthy = PluginMocks.CreateUnhealthyResult("LLM API unreachable");
```

### Document Factories

```csharp
// Single document
var doc = PluginMocks.CreateRetrievedDocument(
    id: "doc-1",
    content: "Game setup instructions...",
    score: 0.95
);

// Multiple documents
var docs = PluginMocks.CreateRetrievedDocuments(count: 10);

// With custom metadata
var docs = PluginMocks.CreateRetrievedDocuments(
    count: 5,
    metadata: new Dictionary<string, object>
    {
        ["source"] = "rulebook",
        ["page"] = 5
    }
);
```

---

## Using PluginContractTests

### Full Contract Verification

```csharp
[Fact]
public void Plugin_MeetsAllContractRequirements()
{
    var plugin = new MyPlugin(Logger);
    var result = PluginContractTests.VerifyContract(plugin);

    result.AssertAllPassed();

    // Or inspect results
    result.AllPassed.Should().BeTrue(result.ToString());
}
```

### Individual Verifications

```csharp
[Fact]
public void Plugin_HasValidId()
{
    var result = PluginContractTests.VerifyId(Plugin);
    result.Passed.Should().BeTrue(string.Join(", ", result.FailedChecks));
}

[Fact]
public void Plugin_HasValidVersion()
{
    var result = PluginContractTests.VerifyVersion(Plugin);
    result.Passed.Should().BeTrue();
}

[Fact]
public async Task Plugin_ValidationBehaviorIsCorrect()
{
    var result = await PluginContractTests.VerifyValidationBehaviorAsync(Plugin);
    result.Passed.Should().BeTrue(string.Join(", ", result.FailedChecks));
}

[Fact]
public async Task Plugin_ExecutionBehaviorIsCorrect()
{
    var input = PluginMocks.CreateValidInput();
    var result = await PluginContractTests.VerifyExecutionBehaviorAsync(Plugin, input);
    result.Passed.Should().BeTrue();
}

[Fact]
public async Task Plugin_HealthCheckBehaviorIsCorrect()
{
    var result = await PluginContractTests.VerifyHealthCheckBehaviorAsync(Plugin);
    result.Passed.Should().BeTrue();
}
```

### Understanding Results

```csharp
var result = PluginContractTests.VerifyContract(plugin);

// Check overall status
if (!result.AllPassed)
{
    // Get all failures
    foreach (var failure in result.AllFailedChecks)
    {
        Console.WriteLine($"Failed: {failure}");
    }

    // Get category-specific results
    var idResult = result.Results["Id"];
    Console.WriteLine($"Id checks: {idResult.PassedChecks.Count} passed, {idResult.FailedChecks.Count} failed");
}

// Summary
Console.WriteLine(result.ToString());
// Output:
// Contract Verification: FAILED
// Passed: 18 / 20
//
// Failed Checks:
//   - Id: Id should be lowercase alphanumeric with hyphens only
//   - Version: Version should follow semantic versioning (e.g., 1.0.0)
```

---

## Using PluginBenchmarks

### Basic Benchmarking

```csharp
[Fact]
public async Task Plugin_MeetsPerformanceRequirements()
{
    var result = await PluginBenchmarks.RunBenchmarkAsync(
        Plugin,
        PluginMocks.CreateValidInput
    );

    result.MeanMs.Should().BeLessThan(100);
    result.P95Ms.Should().BeLessThan(200);
    result.SuccessRate.Should().Be(1.0);
}
```

### Benchmark Options

```csharp
// Quick benchmark (5 iterations, no warmup)
var result = await PluginBenchmarks.RunBenchmarkAsync(
    Plugin,
    PluginMocks.CreateValidInput,
    options: BenchmarkOptions.Quick
);

// Standard benchmark (20 iterations, 3 warmup)
var result = await PluginBenchmarks.RunBenchmarkAsync(
    Plugin,
    PluginMocks.CreateValidInput,
    options: BenchmarkOptions.Standard
);

// Thorough benchmark (100 iterations, 10 warmup)
var result = await PluginBenchmarks.RunBenchmarkAsync(
    Plugin,
    PluginMocks.CreateValidInput,
    options: BenchmarkOptions.Thorough
);

// Custom options
var result = await PluginBenchmarks.RunBenchmarkAsync(
    Plugin,
    PluginMocks.CreateValidInput,
    options: new BenchmarkOptions
    {
        Iterations = 50,
        WarmupIterations = 5,
        GcCollect = true
    }
);
```

### Throughput Benchmark

```csharp
[Fact]
public async Task Plugin_HandlesExpectedThroughput()
{
    var result = await PluginBenchmarks.RunThroughputBenchmarkAsync(
        Plugin,
        PluginMocks.CreateValidInput,
        durationSeconds: 10
    );

    result.OperationsPerSecond.Should().BeGreaterThan(100);
    result.SuccessRate.Should().BeGreaterThan(0.99);
}
```

### Concurrency Benchmark

```csharp
[Fact]
public async Task Plugin_HandlesParallelExecution()
{
    var result = await PluginBenchmarks.RunConcurrencyBenchmarkAsync(
        Plugin,
        PluginMocks.CreateValidInput,
        concurrency: 10,
        operationsPerTask: 50
    );

    result.SuccessCount.Should().Be(500);
    result.FailureCount.Should().Be(0);
    result.ThroughputPerSecond.Should().BeGreaterThan(50);
}
```

### Memory Benchmark

```csharp
[Fact]
public async Task Plugin_DoesNotLeakMemory()
{
    var result = await PluginBenchmarks.RunMemoryBenchmarkAsync(
        Plugin,
        PluginMocks.CreateValidInput,
        iterations: 1000
    );

    // Memory growth should be minimal
    result.MemoryGrowthBytes.Should().BeLessThan(10_000_000); // 10MB
}
```

### Benchmark Results

```csharp
var result = await PluginBenchmarks.RunBenchmarkAsync(...);

// Timing statistics
result.MeanMs       // Average execution time
result.MedianMs     // Median execution time
result.MinMs        // Minimum execution time
result.MaxMs        // Maximum execution time
result.StdDevMs     // Standard deviation
result.P95Ms        // 95th percentile
result.P99Ms        // 99th percentile

// Success metrics
result.SuccessCount
result.FailureCount
result.SuccessRate

// Raw data
result.Durations    // All individual durations
result.Errors       // All error messages
```

---

## Testing Patterns

### Unit Testing

Test plugin logic in isolation:

```csharp
public class VectorRetrievalPluginTests : PluginTestHarness<VectorRetrievalPlugin>
{
    private readonly Mock<IVectorStore> _vectorStore = new();

    protected override VectorRetrievalPlugin CreatePlugin()
    {
        return new VectorRetrievalPlugin(Logger, _vectorStore.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingDocuments_ReturnsDocuments()
    {
        // Arrange
        var docs = PluginMocks.CreateRetrievedDocuments(5);
        _vectorStore
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);

        var input = PluginMocks.CreateQueryInput("How do I play?");

        // Act
        var output = await Plugin.ExecuteAsync(input);

        // Assert
        output.Success.Should().BeTrue();
        var result = output.Result!.RootElement;
        result.GetProperty("documents").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVectorStoreThrows_ReturnsFailedOutput()
    {
        // Arrange
        _vectorStore
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        var input = PluginMocks.CreateQueryInput("test");

        // Act
        var output = await Plugin.ExecuteAsync(input);

        // Assert
        output.Success.Should().BeFalse();
        output.ErrorCode.Should().Be("RETRIEVAL_FAILED");
    }
}
```

### Integration Testing

Test with real dependencies:

```csharp
public class VectorRetrievalIntegrationTests : IClassFixture<QdrantFixture>
{
    private readonly QdrantFixture _qdrant;

    public VectorRetrievalIntegrationTests(QdrantFixture qdrant)
    {
        _qdrant = qdrant;
    }

    [Fact]
    public async Task ExecuteAsync_WithRealVectorStore_ReturnsDocuments()
    {
        // Arrange
        var plugin = new VectorRetrievalPlugin(
            NullLogger.Instance,
            _qdrant.VectorStore
        );

        // Seed test data
        await _qdrant.SeedTestDocuments();

        var input = PluginMocks.CreateQueryInput("game setup");

        // Act
        var output = await plugin.ExecuteAsync(input);

        // Assert
        output.Success.Should().BeTrue();
    }
}
```

### Snapshot Testing

Compare outputs against known-good snapshots:

```csharp
[Fact]
public async Task ExecuteAsync_OutputMatchesSnapshot()
{
    var input = PluginMocks.CreateQueryInput("test query");
    var output = await Plugin.ExecuteAsync(input);

    // Using Verify library
    await Verify(output.Result);
}
```

### Parameterized Testing

Test multiple scenarios:

```csharp
[Theory]
[InlineData("rules query", "rules")]
[InlineData("strategy tips", "strategy")]
[InlineData("general question", "general")]
public async Task ExecuteAsync_ClassifiesQueryCorrectly(string query, string expectedType)
{
    var input = PluginMocks.CreateQueryInput(query);
    var output = await Plugin.ExecuteAsync(input);

    output.Result!.RootElement
        .GetProperty("queryType")
        .GetString()
        .Should().Be(expectedType);
}

[Theory]
[MemberData(nameof(TestCases))]
public async Task ExecuteAsync_HandlesVariousInputs(PluginInput input, bool expectSuccess)
{
    var output = await Plugin.ExecuteAsync(input);
    output.Success.Should().Be(expectSuccess);
}

public static IEnumerable<object[]> TestCases()
{
    yield return new object[] { PluginMocks.CreateValidInput(), true };
    yield return new object[] { PluginMocks.CreateInvalidInput_EmptyExecutionId(), false };
}
```

### Error Handling Tests

```csharp
[Fact]
public async Task ExecuteAsync_WithTimeout_ReturnsTimeoutError()
{
    // Arrange - plugin that takes too long
    var slowPlugin = new SlowPlugin(Logger);
    var config = new PluginConfig { TimeoutMs = 100 };
    var input = PluginMocks.CreateValidInput();

    // Act
    var output = await slowPlugin.ExecuteAsync(input, config);

    // Assert
    output.Success.Should().BeFalse();
    output.ErrorCode.Should().Be("TIMEOUT");
}

[Fact]
public async Task ExecuteAsync_WithCancellation_ReturnsCancelled()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.Cancel();
    var input = PluginMocks.CreateValidInput();

    // Act
    var output = await Plugin.ExecuteAsync(input, cancellationToken: cts.Token);

    // Assert
    output.Success.Should().BeFalse();
    output.ErrorCode.Should().Be("CANCELLED");
}
```

### Health Check Tests

```csharp
[Fact]
public async Task HealthCheckAsync_WhenHealthy_ReturnsHealthyStatus()
{
    // Arrange - ensure dependencies are available
    _vectorStore.Setup(x => x.PingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    // Act
    var result = await Plugin.HealthCheckAsync();

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.CheckDurationMs.Should().BePositive();
}

[Fact]
public async Task HealthCheckAsync_WhenDependencyUnavailable_ReturnsUnhealthy()
{
    // Arrange
    _vectorStore.Setup(x => x.PingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    // Act
    var result = await Plugin.HealthCheckAsync();

    // Assert
    result.Status.Should().Be(HealthStatus.Unhealthy);
    result.Message.Should().Contain("unavailable");
}
```

---

## Test Organization

### Recommended Structure

```
tests/Api.Tests/
└── BoundedContexts/
    └── KnowledgeBase/
        └── Domain/
            └── Plugins/
                ├── Testing/                    # Framework files
                │   ├── PluginTestHarness.cs
                │   ├── PluginMocks.cs
                │   ├── PluginContractTests.cs
                │   ├── PluginBenchmarks.cs
                │   └── PluginTestFrameworkTests.cs
                │
                ├── Routing/                    # By category
                │   └── IntentRoutingPluginTests.cs
                │
                ├── Retrieval/
                │   ├── VectorRetrievalPluginTests.cs
                │   └── HybridRetrievalPluginTests.cs
                │
                ├── Generation/
                │   └── AnswerGenerationPluginTests.cs
                │
                └── Integration/               # Integration tests
                    └── FullPipelineTests.cs
```

### Naming Conventions

```csharp
// Test class: [ClassName]Tests
public class VectorRetrievalPluginTests

// Test method: [Method]_[Scenario]_[ExpectedResult]
public async Task ExecuteAsync_WithValidQuery_ReturnsDocuments()
public async Task ExecuteAsync_WithEmptyQuery_ReturnsValidationError()
public async Task HealthCheckAsync_WhenStoreUnavailable_ReturnsUnhealthy()
```

### Test Categories

```csharp
[Trait("Category", "Unit")]
public class VectorRetrievalPluginTests { }

[Trait("Category", "Integration")]
public class VectorRetrievalIntegrationTests { }

[Trait("Category", "Performance")]
public class VectorRetrievalBenchmarkTests { }

// Run specific categories
// dotnet test --filter "Category=Unit"
// dotnet test --filter "Category=Integration"
```

---

## Best Practices

### Do

✅ **Use the test harness** for consistent contract testing
✅ **Mock external dependencies** for unit tests
✅ **Test edge cases** (empty input, null values, timeouts)
✅ **Test error handling** explicitly
✅ **Use meaningful test names** that describe the scenario
✅ **Keep tests independent** (no shared state)
✅ **Run benchmarks in CI** to catch performance regressions

### Don't

❌ **Don't test implementation details** (test behavior)
❌ **Don't skip validation tests** (they catch real issues)
❌ **Don't ignore flaky tests** (fix root cause)
❌ **Don't over-mock** (integration tests have value)
❌ **Don't test third-party code** (trust your dependencies)

### Coverage Goals

| Test Type | Target Coverage |
|-----------|-----------------|
| Unit tests | 90%+ |
| Contract tests | 100% of plugins |
| Integration tests | Critical paths |
| Performance tests | All plugins |

---

## Running Tests

### Command Line

```bash
# All plugin tests
dotnet test --filter "FullyQualifiedName~Plugins"

# Specific plugin
dotnet test --filter "FullyQualifiedName~VectorRetrievalPluginTests"

# By category
dotnet test --filter "Category=Unit"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Visual Studio

1. Open Test Explorer (Ctrl+E, T)
2. Filter by namespace: `Plugins`
3. Run all or select specific tests

### CI/CD

```yaml
# .github/workflows/test.yml
- name: Run Plugin Tests
  run: |
    dotnet test \
      --filter "FullyQualifiedName~Plugins" \
      --logger "trx;LogFileName=plugin-tests.trx" \
      /p:CollectCoverage=true

- name: Run Plugin Benchmarks
  run: |
    dotnet test \
      --filter "Category=Performance" \
      --logger "console;verbosity=normal"
```

---

## Related Documentation

- [Plugin Development Guide](plugin-development-guide.md) - Creating plugins
- [Plugin Contract](plugin-contract.md) - Interface specification
- [Built-in Plugins](built-in-plugins/) - Reference implementations
