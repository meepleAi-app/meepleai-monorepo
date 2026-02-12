# Plugin Testing Guide

> **Comprehensive testing patterns for RAG plugins**

## Testing Framework Components

| Component | Purpose |
|-----------|---------|
| `PluginTestHarness<T>` | Base class with 10+ contract tests |
| `PluginMocks` | Test data factories |
| `PluginContractTests` | Standalone verification |
| `PluginBenchmarks` | Performance measurement |

---

## Quick Start

```csharp
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

public class MyPluginTests : PluginTestHarness<MyPlugin>
{
    protected override MyPlugin CreatePlugin()
        => new MyPlugin(Logger);

    // Inherited: 10+ contract tests auto-included

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsExpectedResult()
    {
        var input = PluginMocks.CreateQueryInput("test query");
        var output = await Plugin.ExecuteAsync(input);

        output.Success.Should().BeTrue();
        output.Result.Should().NotBeNull();
    }
}
```

---

## PluginTestHarness (Inherited Tests)

### Auto-Included Contract Tests
```csharp
// Identity (4 tests)
Plugin_HasValidId()
Plugin_HasValidName()
Plugin_HasValidVersion()
Plugin_HasValidCategory()

// Schemas (4 tests)
Plugin_InputSchema_IsValidJsonSchema()
Plugin_OutputSchema_IsValidJsonSchema()
Plugin_ConfigSchema_IsValidJsonSchema()
Plugin_Metadata_IsConsistent()

// Validation (2 tests)
ValidateConfig_WithDefaultConfig_Succeeds()
ValidateInput_WithValidInput_Succeeds()

// Execution (2 tests)
ExecuteAsync_WithValidInput_ReturnsOutput()
HealthCheckAsync_ReturnsValidResult()
```

### Customization
```csharp
// Custom valid input
protected override PluginInput CreateValidInput()
    => PluginMocks.CreateQueryInput("How do I play?", gameId: Guid.NewGuid());

// Custom config
protected override PluginConfig CreateDefaultConfig()
    => new() { TimeoutMs = 60000, Parameters = JsonDocument.Parse("{...}") };

// Setup dependencies
private readonly Mock<IVectorStore> _vectorStore = new();

protected override MyPlugin CreatePlugin()
{
    _vectorStore.Setup(x => x.SearchAsync(...)).ReturnsAsync(docs);
    return new MyPlugin(Logger, _vectorStore.Object);
}
```

---

## PluginMocks (Test Data Factories)

### Input Factories
```csharp
// Basic
PluginMocks.CreateValidInput()
PluginMocks.CreateValidInput(executionId: Guid.NewGuid())

// Query
PluginMocks.CreateQueryInput("How do I setup?")
PluginMocks.CreateQueryInput("Question", gameId: guid, userId: guid)

// Custom payload
PluginMocks.CreateInputWithPayload("""{"query": "...", "options": {...}}""")

// With pipeline context
PluginMocks.CreateInputWithPreviousOutputs(execId, prevOutputs)

// Invalid (for testing validation)
PluginMocks.CreateInvalidInput_EmptyExecutionId()
PluginMocks.CreateInvalidInput_NullPayload()
PluginMocks.CreateInvalidInput_MissingQuery()
```

### Output Factories
```csharp
// Success
PluginMocks.CreateSuccessfulOutput(executionId)
PluginMocks.CreateSuccessfulOutput(execId, JsonDocument.Parse("{...}"))
PluginMocks.CreateSuccessfulOutput(execId, confidence: 0.95)

// Failure
PluginMocks.CreateFailedOutput(execId, "Error message", "ERROR_CODE")

// Retrieval
var docs = PluginMocks.CreateRetrievedDocuments(count: 5);
PluginMocks.CreateRetrievalOutput(execId, docs)
```

### Config Factories
```csharp
PluginMocks.CreateDefaultConfig()
PluginMocks.CreateConfigWithTimeout(5000)
PluginMocks.CreateInvalidConfig_NegativeTimeout()
PluginMocks.CreateInvalidConfig_NegativeRetries()
```

### Health Check Factories
```csharp
PluginMocks.CreateHealthyResult()
PluginMocks.CreateDegradedResult("Cache unavailable")
PluginMocks.CreateUnhealthyResult("LLM API unreachable")
```

---

## PluginContractTests

### Full Verification
```csharp
[Fact]
public void Plugin_MeetsAllContractRequirements()
{
    var result = PluginContractTests.VerifyContract(Plugin);
    result.AssertAllPassed();
}
```

### Individual Checks
```csharp
PluginContractTests.VerifyId(Plugin).Passed.Should().BeTrue()
PluginContractTests.VerifyVersion(Plugin).Passed.Should().BeTrue()
await PluginContractTests.VerifyValidationBehaviorAsync(Plugin)
await PluginContractTests.VerifyExecutionBehaviorAsync(Plugin, input)
await PluginContractTests.VerifyHealthCheckBehaviorAsync(Plugin)
```

---

## PluginBenchmarks

### Basic Benchmark
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
// Quick (5 iter, no warmup)
BenchmarkOptions.Quick

// Standard (20 iter, 3 warmup)
BenchmarkOptions.Standard

// Thorough (100 iter, 10 warmup)
BenchmarkOptions.Thorough

// Custom
new BenchmarkOptions { Iterations = 50, WarmupIterations = 5 }
```

### Specialized Benchmarks
```csharp
// Throughput
var result = await PluginBenchmarks.RunThroughputBenchmarkAsync(
    Plugin, inputFactory, durationSeconds: 10
);
result.OperationsPerSecond.Should().BeGreaterThan(100);

// Concurrency
var result = await PluginBenchmarks.RunConcurrencyBenchmarkAsync(
    Plugin, inputFactory, concurrency: 10, operationsPerTask: 50
);
result.SuccessCount.Should().Be(500);

// Memory
var result = await PluginBenchmarks.RunMemoryBenchmarkAsync(
    Plugin, inputFactory, iterations: 1000
);
result.MemoryGrowthBytes.Should().BeLessThan(10_000_000);
```

---

## Testing Patterns

### Unit Testing (Isolated)
```csharp
public class VectorRetrievalPluginTests : PluginTestHarness<VectorRetrievalPlugin>
{
    private readonly Mock<IVectorStore> _vectorStore = new();

    protected override VectorRetrievalPlugin CreatePlugin()
        => new(Logger, _vectorStore.Object);

    [Fact]
    public async Task ExecuteAsync_WithMatchingDocs_ReturnsDocuments()
    {
        _vectorStore.Setup(x => x.SearchAsync(...)).ReturnsAsync(docs);
        var input = PluginMocks.CreateQueryInput("test");

        var output = await Plugin.ExecuteAsync(input);

        output.Success.Should().BeTrue();
        output.Result!.RootElement.GetProperty("documents")
            .GetArrayLength().Should().Be(5);
    }
}
```

### Integration Testing (Real Dependencies)
```csharp
public class VectorRetrievalIntegrationTests : IClassFixture<QdrantFixture>
{
    [Fact]
    public async Task ExecuteAsync_WithRealVectorStore_ReturnsDocuments()
    {
        var plugin = new VectorRetrievalPlugin(logger, _qdrant.VectorStore);
        await _qdrant.SeedTestDocuments();

        var output = await plugin.ExecuteAsync(input);

        output.Success.Should().BeTrue();
    }
}
```

### Parameterized Testing
```csharp
[Theory]
[InlineData("rules query", "rules")]
[InlineData("strategy tips", "strategy")]
public async Task ExecuteAsync_ClassifiesQueryCorrectly(string query, string expected)
{
    var input = PluginMocks.CreateQueryInput(query);
    var output = await Plugin.ExecuteAsync(input);

    output.Result!.RootElement.GetProperty("queryType")
        .GetString().Should().Be(expected);
}
```

### Error Handling
```csharp
[Fact]
public async Task ExecuteAsync_WithTimeout_ReturnsTimeoutError()
{
    var config = new PluginConfig { TimeoutMs = 100 };
    var output = await SlowPlugin.ExecuteAsync(input, config);

    output.Success.Should().BeFalse();
    output.ErrorCode.Should().Be("TIMEOUT");
}

[Fact]
public async Task ExecuteAsync_WithCancellation_ReturnsCancelled()
{
    var cts = new CancellationTokenSource();
    cts.Cancel();

    var output = await Plugin.ExecuteAsync(input, cancellationToken: cts.Token);

    output.ErrorCode.Should().Be("CANCELLED");
}
```

---

## Test Organization

### Directory Structure
```
tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Plugins/
├── Testing/                    # Framework
│   ├── PluginTestHarness.cs
│   ├── PluginMocks.cs
│   ├── PluginContractTests.cs
│   └── PluginBenchmarks.cs
├── Routing/                    # By category
│   └── IntentRoutingPluginTests.cs
├── Retrieval/
│   ├── VectorRetrievalPluginTests.cs
│   └── HybridRetrievalPluginTests.cs
└── Integration/
    └── FullPipelineTests.cs
```

### Naming Conventions
```csharp
// Class: [ClassName]Tests
public class VectorRetrievalPluginTests

// Method: [Method]_[Scenario]_[ExpectedResult]
ExecuteAsync_WithValidQuery_ReturnsDocuments()
ExecuteAsync_WithEmptyQuery_ReturnsValidationError()
HealthCheckAsync_WhenStoreUnavailable_ReturnsUnhealthy()
```

### Test Categories
```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Integration")]
[Trait("Category", "Performance")]

// Run by category
dotnet test --filter "Category=Unit"
```

---

## Running Tests

```bash
# All plugin tests
dotnet test --filter "FullyQualifiedName~Plugins"

# Specific plugin
dotnet test --filter "FullyQualifiedName~VectorRetrievalPluginTests"

# By category
dotnet test --filter "Category=Unit"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Verbose
dotnet test --logger "console;verbosity=detailed"
```

---

## Best Practices

### ✅ DO
- Use test harness for consistent contract testing
- Mock external dependencies for unit tests
- Test edge cases (empty, null, timeout)
- Test error handling explicitly
- Use meaningful test names
- Keep tests independent
- Run benchmarks in CI

### ❌ DON'T
- Test implementation details (test behavior)
- Skip validation tests
- Ignore flaky tests (fix root cause)
- Over-mock (integration tests have value)
- Test third-party code

### Coverage Targets
| Type | Target |
|------|--------|
| **Unit** | 90%+ |
| **Contract** | 100% of plugins |
| **Integration** | Critical paths |
| **Performance** | All plugins |

---

**See Also**: [Plugin Development](plugin-development-guide.md) | [Plugin Contract](plugin-contract.md) | [Built-in Plugins](built-in-plugins/)
