# MockEmbeddingService Pattern Documentation

**Issue**: #2599 - AI/Embedding Service Tests
**Created**: 2026-01-18
**Status**: Implemented

---

## Overview

`MockEmbeddingService` is a centralized test helper for AI/embedding tests, providing deterministic embeddings based on text content hash. This eliminates the need for real embedding API calls during tests.

---

## Location

`apps/api/tests/Api.Tests/TestHelpers/MockEmbeddingService.cs`

---

## Features

### 1. Deterministic Embeddings
- Same text → same embedding vector (reproducible tests)
- Hash-based generation using `text.GetHashCode()` as seed
- Normalized unit vectors (industry standard)
- Default dimensions: 384 (configurable)

### 2. Full IEmbeddingService Interface Support
- `GenerateEmbeddingsAsync(List<string>)` - Batch embeddings
- `GenerateEmbeddingAsync(string)` - Single embedding
- `GenerateEmbeddingsAsync(List<string>, string language)` - Multi-language batch
- `GenerateEmbeddingAsync(string, string language)` - Multi-language single
- `GetEmbeddingDimensions()` - Returns configured dimensions
- `GetModelName()` - Returns mock model name

### 3. Factory Methods
```csharp
// Default mock (384 dimensions, success mode)
var mock = new MockEmbeddingService();

// Failing mock for error scenarios
var failMock = MockEmbeddingService.CreateFailingMock("API unavailable");

// Custom dimensions (e.g., OpenAI ada-002 uses 1536)
var largeMock = MockEmbeddingService.CreateWithDimensions(1536);
```

### 4. Failure Mode
- Configurable failure behavior for error testing
- Custom error messages
- Returns `EmbeddingResult.CreateFailure(message)`

---

## Usage Patterns

### Pattern 1: Unit Tests (Direct Instantiation)
**Use Case**: Testing services that depend on IEmbeddingService

```csharp
public class MyServiceTests
{
    private readonly IEmbeddingService _embeddingService;
    private readonly MyService _service;

    public MyServiceTests()
    {
        // Use mock instead of real service
        _embeddingService = new MockEmbeddingService();
        _service = new MyService(_embeddingService);
    }

    [Fact]
    public async Task TestMethod_ShouldUseEmbeddings()
    {
        // Arrange
        var text = "test input";

        // Act
        var result = await _service.ProcessText(text);

        // Assert - embeddings are deterministic
        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(384);
    }
}
```

### Pattern 2: Integration Tests (Moq Setup)
**Use Case**: When using Moq for full dependency mocking

```csharp
public class HandlerTests
{
    private readonly Mock<IEmbeddingService> _embeddingMock;

    public HandlerTests()
    {
        _embeddingMock = new Mock<IEmbeddingService>();

        // Setup mock to return deterministic embeddings
        _embeddingMock
            .Setup(x => x.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string text, CancellationToken ct) =>
            {
                var mock = new MockEmbeddingService();
                return mock.GenerateEmbeddingAsync(text, ct);
            });
    }
}
```

### Pattern 3: Failure Scenarios
**Use Case**: Testing error handling paths

```csharp
[Fact]
public async Task ProcessText_WhenEmbeddingFails_ShouldHandleError()
{
    // Arrange
    var failingMock = MockEmbeddingService.CreateFailingMock("API rate limit");
    var service = new MyService(failingMock);

    // Act
    var result = await service.ProcessText("test");

    // Assert
    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().Contain("API rate limit");
}
```

### Pattern 4: Custom Dimensions
**Use Case**: Testing with different embedding models

```csharp
[Theory]
[InlineData(384)]   // HuggingFace BGE-M3
[InlineData(768)]   // Sentence-BERT
[InlineData(1536)]  // OpenAI ada-002
public async Task ProcessText_WithDifferentDimensions_ShouldWork(int dimensions)
{
    // Arrange
    var mock = MockEmbeddingService.CreateWithDimensions(dimensions);
    var service = new MyService(mock);

    // Act
    var result = await service.ProcessText("test");

    // Assert
    result.Embedding.Should().HaveCount(dimensions);
}
```

---

## Implementation Example: EmbeddingServiceTests

**File**: `apps/api/tests/Api.Tests/Services/EmbeddingServiceTests.cs`

This test suite demonstrates comprehensive testing of `EmbeddingService` using mocked providers:

**Coverage** (23 tests):
- ✅ Primary provider success scenarios
- ✅ Fallback chain when primary fails
- ✅ Error handling when both providers fail
- ✅ Input validation (null/empty texts)
- ✅ Cancellation token support
- ✅ Multi-language support (en, it, de, fr, es)
- ✅ Configuration variations (fallback enabled/disabled)

**Key Learnings**:
1. Use `EmbeddingProviderResult` (not `BatchEmbeddingResult`) for provider results
2. Mock `IEmbeddingProviderFactory` to control provider creation
3. Setup provider properties: `ProviderName`, `ModelName`, `Dimensions`
4. Verify fallback chain with `Times.Once` on both providers
5. Test language validation with invalid codes falling back to "en"

**Example Test**:
```csharp
[Fact]
public async Task GenerateEmbeddingsAsync_WhenPrimaryFails_ShouldTryFallback()
{
    // Arrange
    _primaryProviderMock
        .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new EmbeddingProviderResult
        {
            Success = false,
            ErrorMessage = "Primary provider error"
        });

    _fallbackProviderMock
        .Setup(x => x.GenerateBatchEmbeddingsAsync(texts, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new EmbeddingProviderResult
        {
            Success = true,
            Embeddings = expectedEmbeddings
        });

    // Act
    var result = await _service.GenerateEmbeddingsAsync(texts);

    // Assert - Fallback succeeded
    result.Success.Should().BeTrue();

    // Verify both providers were called
    _primaryProviderMock.Verify(/* ... */, Times.Once);
    _fallbackProviderMock.Verify(/* ... */, Times.Once);
}
```

---

## Related Services Test Status

### ✅ Services with Tests
- **EmbeddingService**: ✅ 23 unit tests (NEW - Issue #2599)
- **RagService**: ✅ Integration + Performance tests
- **RagConfigurationProvider**: ✅ Unit tests
- **RagAccuracyEvaluator**: ✅ Unit tests
- **RagValidationPipeline**: ✅ Unit + Integration tests
- **EmbeddingBasedSemanticChunker**: ✅ Unit tests

### ⏳ Services WITHOUT Dedicated Unit Tests
- **HybridSearchService**: Only used in RagService integration tests
- **KeywordSearchService**: Only used indirectly
- **QdrantService**: Integration tests exist, no unit tests

**Rationale**: These are infrastructure/integration services better suited for integration testing rather than unit tests with heavy mocking.

---

## Benefits of MockEmbeddingService

### 1. Speed
- ❌ Real API call: 200-500ms per request
- ✅ Mock: <1ms per request
- **Result**: 99% faster test execution

### 2. Reliability
- ❌ Real API: Network failures, rate limits, API downtime
- ✅ Mock: Deterministic, always available
- **Result**: Elimina test flakiness

### 3. Cost
- ❌ Real API: $0.0001-0.0004 per 1K tokens (adds up in CI/CD)
- ✅ Mock: $0
- **Result**: Zero API costs in testing

### 4. Reproducibility
- ❌ Real API: Embeddings may vary slightly between calls
- ✅ Mock: Same text → identical embedding every time
- **Result**: Consistent test results

### 5. Offline Development
- ❌ Real API: Requires internet connection and API keys
- ✅ Mock: Works offline, no credentials needed
- **Result**: Faster local development workflow

---

## When to Use Real vs Mock

### Use MockEmbeddingService ✅
- **Unit tests**: Testing business logic, not embedding quality
- **Integration tests**: When embedding is not the focus
- **Performance tests**: Isolating non-embedding bottlenecks
- **CI/CD pipelines**: Fast, reliable, cost-free
- **Offline development**: No API keys or network needed

### Use Real Embedding Service ❌
- **E2E tests**: Validating full RAG pipeline accuracy
- **Embedding quality tests**: Testing embedding model itself
- **Production validation**: Verifying real API integration
- **Benchmark tests**: Comparing embedding model performance

---

## Migration Guide

### Converting Existing Tests to Use MockEmbeddingService

**Before** (Manual Mock Setup):
```csharp
var embeddingMock = new Mock<IEmbeddingService>();
embeddingMock
    .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new EmbeddingResult
    {
        Success = true,
        Embeddings = new List<float[]> { new float[384] /* random values */ }
    });
```

**After** (MockEmbeddingService):
```csharp
var embeddingService = new MockEmbeddingService(dimensions: 384);
// That's it! Fully functional, deterministic, complete interface support
```

**Benefits**:
- ✅ Less code (1 line vs 7 lines)
- ✅ Deterministic embeddings (same text → same vector)
- ✅ Full interface support (all 4 methods)
- ✅ Centralized behavior changes

---

## Future Enhancements

### Potential Improvements
1. **Similarity Simulation**: Make related texts have similar embeddings
2. **Language-Aware**: Different embedding spaces per language
3. **Dimension Validation**: Ensure normalized vectors
4. **Performance Modes**: Fast mode for large batch tests
5. **Logging Support**: Optional ILogger for debugging

### Related Issues
- #2558 - Test infrastructure improvements
- #2599 - AI/Embedding Service Tests (this implementation)

---

## References

**Code**:
- MockEmbeddingService: `apps/api/tests/Api.Tests/TestHelpers/MockEmbeddingService.cs`
- EmbeddingServiceTests: `apps/api/tests/Api.Tests/Services/EmbeddingServiceTests.cs`
- RagTestHelpers: `apps/api/tests/Api.Tests/Helpers/RagTestHelpers.cs`

**Commits**:
- 93875b87: Add MockEmbeddingService for AI/embedding test support
- e0fd7951: Add comprehensive EmbeddingService unit tests

**Related Documentation**:
- Testing Guide: `docs/05-testing/README.md`
- Test Infrastructure: `docs/05-testing/test-infrastructure.md`

---

**Maintainer**: MeepleAI Development Team
**Last Updated**: 2026-01-18
