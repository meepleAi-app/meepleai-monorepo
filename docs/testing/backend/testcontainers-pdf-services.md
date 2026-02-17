# Testcontainers PDF Services Integration

**Status**: ✅ Implemented
**Date**: 2026-01-29
**Related Issues**: Testing Infrastructure Enhancement

## Overview

Extended `SharedTestcontainersFixture` with support for PDF processing services (Unstructured, SmolDocling, Embedding, Reranker). These services are **conditionally enabled** via environment variable to avoid overhead for tests that don't need PDF processing.

## Configuration

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `TEST_PDF_SERVICES` | Enable PDF service containers | `false` |
| `TEST_UNSTRUCTURED_URL` | External Unstructured service URL | (start container) |
| `TEST_SMOLDOCLING_URL` | External SmolDocling service URL | (start container) |
| `TEST_EMBEDDING_URL` | External Embedding service URL | (start container) |
| `TEST_RERANKER_URL` | External Reranker service URL | (start container) |

### Docker Images

Services expect pre-built Docker images:

```bash
# Build all PDF service images
cd apps/unstructured-service && docker build -t infra-unstructured-service:latest .
cd ../smoldocling-service && docker build -t infra-smoldocling-service:latest .
cd ../embedding-service && docker build -t infra-embedding-service:latest .
cd ../reranker-service && docker build -t infra-reranker-service:latest .
```

## Usage

### Basic Integration Test (No PDF Services)

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private string _databaseName = null!;

    public MyIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database
        _databaseName = $"test_{GetType().Name}_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task MyTest_ValidInput_ReturnsExpectedResult()
    {
        // Test uses shared PostgreSQL + Redis containers
        // No PDF services needed
    }
}
```

### PDF Processing Test (With Services)

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "PDF")]
public class PdfExtractionRealBackendTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private UnstructuredPdfTextExtractor? _extractor;

    public PdfExtractionRealBackendTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Check if PDF services are available
        if (!_fixture.ArePdfServicesEnabled || string.IsNullOrEmpty(_fixture.UnstructuredServiceUrl))
        {
            // Skip test gracefully if services not available
            return;
        }

        // Initialize extractor with real service URL
        var httpClient = new HttpClient { BaseAddress = new Uri(_fixture.UnstructuredServiceUrl) };
        _extractor = new UnstructuredPdfTextExtractor(httpClient, NullLogger<UnstructuredPdfTextExtractor>.Instance);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExtractText_SimpleRulebook_ReturnsAccurateText()
    {
        // Skip if services not available
        if (_extractor == null)
        {
            Assert.Skip("PDF services not enabled. Set TEST_PDF_SERVICES=true to run this test.");
        }

        // Arrange
        var pdfPath = "../../../../data/rulebook/catan_rulebook.pdf";
        var expectedKeywords = new[] { "settlement", "resource", "dice" };

        // Act - Real extraction call
        var result = await _extractor.ExtractTextAsync(pdfPath, CancellationToken.None);

        // Assert - Validate real extraction quality
        result.Text.Should().NotBeNullOrEmpty();
        result.Text.Length.Should().BeGreaterThan(1000);
        expectedKeywords.Should().AllSatisfy(keyword =>
            result.Text.Should().Contain(keyword, Because: "rulebook should contain game terminology"));
    }
}
```

## Running Tests

### Without PDF Services (Fast)

```bash
# Standard test run - no PDF services
cd apps/api/tests/Api.Tests
dotnet test

# Output: PostgreSQL + Redis start (~10s), PDF services skipped
```

### With PDF Services (Comprehensive)

```bash
# Enable PDF services
export TEST_PDF_SERVICES=true
dotnet test --filter "Category=PDF"

# Output: All containers start in parallel (~30-60s depending on model loading)
```

### CI/CD Integration

```yaml
# .github/workflows/backend-ci.yml
- name: Run Integration Tests
  run: dotnet test --filter "Category=Integration&Category!=PDF"

- name: Run PDF Backend Tests
  if: contains(github.event.pull_request.labels.*.name, 'pdf-processing')
  env:
    TEST_PDF_SERVICES: "true"
  run: dotnet test --filter "Category=PDF"
```

## Architecture

### Parallel Container Startup

PDF services start **in parallel** with PostgreSQL and Redis for maximum efficiency:

```
Time: 0s ────────────────────────> 30s
         ┌─ PostgreSQL (10s) ─────┐
         ├─ Redis (8s) ────────────┤
         ├─ Unstructured (25s) ────┤
         ├─ SmolDocling (28s) ─────┤
         ├─ Embedding (30s) ───────┤
         └─ Reranker (22s) ────────┘
                              ↓
                        All ready at ~30s
```

**Without parallelization**: ~106s (sum of all)
**With parallelization**: ~30s (max of all)
**Time saved**: ~76s (72% faster)

### Service Health Checks

Each PDF service container waits for `/health` endpoint before considering itself ready:

```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilHttpRequestIsSucceeded(r => r
        .ForPath("/health")
        .ForPort(servicePort)
        .ForStatusCode(HttpStatusCode.OK)))
```

Timeout: Default Testcontainers timeout (5 minutes) - sufficient for model loading.

### Graceful Degradation

If PDF service containers fail to start:

1. **Non-fatal**: Logs warning, returns `null` URL
2. **Tests skip gracefully**: Use `Assert.Skip()` if services unavailable
3. **Other tests continue**: PostgreSQL/Redis tests unaffected

Example:
```
❌ Unstructured service failed to start after 3 attempts: Docker image not found
   Ensure Docker image 'infra-unstructured-service:latest' is built
✅ Tests without PDF services continue normally
```

## Performance Characteristics

| Scenario | Container Startup | Test Execution | Total |
|----------|------------------|----------------|-------|
| **No PDF services** | ~10s (Postgres + Redis) | Varies | ~10s + test time |
| **PDF services enabled** | ~30s (all parallel) | Varies | ~30s + test time |
| **External services** | 0s (skip containers) | Varies | 0s + test time |

### Memory Usage

| Service | Memory (Idle) | Memory (Active) | Notes |
|---------|---------------|-----------------|-------|
| PostgreSQL | ~20 MB | ~50 MB | Shared buffers: 256MB |
| Redis | ~10 MB | ~20 MB | In-memory cache |
| Unstructured | ~200 MB | ~500 MB | Model loading overhead |
| SmolDocling | ~1.5 GB | ~2.5 GB | VLM model in memory |
| Embedding | ~500 MB | ~800 MB | Sentence transformers |
| Reranker | ~400 MB | ~600 MB | Cross-encoder model |
| **Total** | ~2.6 GB | ~4.5 GB | **Ensure CI has 8GB+ RAM** |

## Migration Guide

### Converting Existing PDF Tests

**Before** (Each test creates own container):

```csharp
public class UnstructuredPdfExtractionIntegrationTests : IAsyncLifetime
{
    private IContainer? _unstructuredContainer;
    private HttpClient? _httpClient;

    public async ValueTask InitializeAsync()
    {
        _unstructuredContainer = new ContainerBuilder()
            .WithImage("infra-unstructured-service:latest")
            .WithPortBinding(8001, true)
            .Build();

        await _unstructuredContainer.StartAsync(); // 25s per test class!
        var port = _unstructuredContainer.GetMappedPublicPort(8001);
        _httpClient = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
    }
}
```

**After** (Uses shared container):

```csharp
[Collection("SharedTestcontainers")]
public class UnstructuredPdfExtractionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private UnstructuredPdfTextExtractor? _extractor;

    public UnstructuredPdfExtractionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.ArePdfServicesEnabled || string.IsNullOrEmpty(_fixture.UnstructuredServiceUrl))
        {
            return; // Skip if services not available
        }

        var httpClient = new HttpClient { BaseAddress = new Uri(_fixture.UnstructuredServiceUrl) };
        _extractor = new UnstructuredPdfTextExtractor(httpClient, NullLogger<UnstructuredPdfTextExtractor>.Instance);

        await Task.CompletedTask; // 0s - container already running!
    }
}
```

**Benefits**:
- ✅ 25s saved per test class (0s vs 25s startup)
- ✅ Graceful skipping when services unavailable
- ✅ CI/CD can selectively enable PDF tests
- ✅ Local development doesn't require PDF services by default

## Troubleshooting

### Docker Images Not Found

**Error**:
```
❌ Unstructured service failed to start: Docker image 'infra-unstructured-service:latest' not found
```

**Solution**:
```bash
cd apps/unstructured-service
docker build -t infra-unstructured-service:latest .
```

### Services Taking Too Long to Start

**Symptom**: Tests timeout waiting for health checks

**Causes**:
1. Model downloading on first run (transformers cache)
2. Insufficient Docker resources (CPU/RAM)
3. Network issues (model downloads)

**Solutions**:
- Pre-download models before tests (Docker build step)
- Increase Docker memory limit to 8GB+
- Use external services in CI (`TEST_UNSTRUCTURED_URL=http://external-service`)

### Parallel Test Conflicts

**Symptom**: PDF service calls fail intermittently

**Cause**: PDF services are **shared** across all test classes

**Solution**: Use unique file paths or test isolation:

```csharp
// Use unique test data per test
var pdfPath = $"tests/data/temp/test_{Guid.NewGuid():N}.pdf";
```

## Future Enhancements

1. **Container Reuse**: Persist containers across test runs for faster execution
2. **Model Caching**: Share model cache volumes to avoid re-downloads
3. **Selective Services**: Enable only specific services (e.g., only Unstructured)
4. **Performance Profiling**: Track PDF service latency per test

## Related Documentation

- [Testcontainers Configuration](testcontainers-configuration.md)
- [Backend Testing Patterns](backend-testing-patterns.md)
- [PDF Test Corpus Organization](pdf-test-corpus.md)
