# Issue #5: Ollama Batch Parallelization

**Priority**: 🟢 Medium
**Category**: Performance Optimization
**Effort**: 4-6 hours
**Impact**: Medium - 3-5x faster PDF indexing

---

## 📋 Problem

Ollama embedding API processes **one chunk at a time** (sequential):

```csharp
// File: EmbeddingService.cs (line 174-210)
private async Task<EmbeddingResult> GenerateOllamaEmbeddingsAsync(List<string> texts, CancellationToken ct)
{
    var embeddings = new List<float[]>();

    // ❌ Sequential: 1 chunk at a time
    foreach (var text in texts)
    {
        var response = await _httpClient.PostAsync("/api/embeddings", content, ct);
        // ... process response ...
    }

    return EmbeddingResult.CreateSuccess(embeddings);
}
```

**Impact**: 100 chunks × 100ms = 10 seconds for PDF indexing (slow!)

---

## 🎯 Solution

**Parallelize requests** to Ollama (up to 8 concurrent):

```csharp
// Proposed: Parallel processing
private async Task<EmbeddingResult> GenerateOllamaEmbeddingsAsync(List<string> texts, CancellationToken ct)
{
    const int MaxConcurrency = 8; // Configurable

    var semaphore = new SemaphoreSlim(MaxConcurrency);
    var tasks = texts.Select(async text =>
    {
        await semaphore.WaitAsync(ct);
        try
        {
            return await GenerateSingleOllamaEmbeddingAsync(text, ct);
        }
        finally
        {
            semaphore.Release();
        }
    });

    var embeddings = await Task.WhenAll(tasks);

    return EmbeddingResult.CreateSuccess(embeddings.ToList());
}

// Result: 100 chunks / 8 parallel = 12.5 iterations × 100ms = 1.25s ✅ (8x faster!)
```

---

## 💰 Benefits

| Metric | Before (Sequential) | After (Parallel) | Improvement |
|--------|---------------------|------------------|-------------|
| **PDF Indexing Time** | 10s (100 chunks) | 2s | **5x faster** |
| **Server CPU** | 10% (idle waiting) | 60% (active) | Better utilization |
| **User Experience** | Slow upload | Fast upload | ⬆️ Satisfaction |

---

## 🛠️ Implementation

### 1. Refactor Ollama Client
```csharp
// File: Services/EmbeddingService.cs

private async Task<EmbeddingResult> GenerateOllamaEmbeddingsAsync(
    List<string> texts,
    CancellationToken ct)
{
    _logger.LogInformation("Generating {Count} embeddings via Ollama (parallel)", texts.Count);

    // Read max concurrency from config (default: 8)
    var maxConcurrency = _configuration.GetValue<int>("Ollama:MaxConcurrency", 8);

    var semaphore = new SemaphoreSlim(maxConcurrency);
    var exceptions = new List<Exception>();

    var tasks = texts.Select(async (text, index) =>
    {
        await semaphore.WaitAsync(ct);
        try
        {
            return await GenerateSingleOllamaEmbeddingAsync(text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for chunk {Index}", index);
            exceptions.Add(ex);
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    });

    var results = await Task.WhenAll(tasks);

    // Handle errors
    if (exceptions.Count > 0)
    {
        return EmbeddingResult.CreateFailure($"{exceptions.Count} embeddings failed");
    }

    var embeddings = results.Where(r => r != null).Select(r => r!).ToList();

    if (embeddings.Count != texts.Count)
    {
        return EmbeddingResult.CreateFailure("Some embeddings failed to generate");
    }

    _logger.LogInformation("Successfully generated {Count} embeddings via Ollama (parallel)", embeddings.Count);
    return EmbeddingResult.CreateSuccess(embeddings);
}

private async Task<float[]?> GenerateSingleOllamaEmbeddingAsync(string text, CancellationToken ct)
{
    var request = new { model = _embeddingModel, prompt = text };
    var json = JsonSerializer.Serialize(request);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");

    using var response = await _httpClient.PostAsync("/api/embeddings", content, ct);
    var responseBody = await response.Content.ReadAsStringAsync(ct);

    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException($"Ollama API error: {response.StatusCode}");
    }

    var ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody);
    return ollamaResponse?.Embedding;
}
```

### 2. Add Configuration
```json
// File: appsettings.json
{
  "Ollama": {
    "Url": "http://ollama:11434",
    "MaxConcurrency": 8  // Tune based on server capacity
  }
}
```

### 3. HttpClient Timeout Adjustment
```csharp
// File: Program.cs or ApplicationServiceExtensions.cs
services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(config["Ollama:Url"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(120); // Increased for parallel requests
});
```

---

## ✅ Acceptance Criteria

1. **Performance**
   - [ ] 100-chunk PDF indexed in <3s (vs 10s baseline)
   - [ ] Concurrency configurable (1-16 range)

2. **Reliability**
   - [ ] Individual failures don't block entire batch
   - [ ] Partial failures reported clearly

3. **Resource Usage**
   - [ ] Server CPU usage <80% under load
   - [ ] No connection pool exhaustion

4. **Configuration**
   - [ ] MaxConcurrency tunable per environment
   - [ ] Default value: 8 (safe for most setups)

---

## 🧪 Testing

```csharp
[Fact]
public async Task ParallelEmbeddings_FasterThanSequential()
{
    // Arrange
    var texts = Enumerable.Range(0, 100).Select(i => $"Test text {i}").ToList();
    var service = CreateEmbeddingService(maxConcurrency: 8);

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await service.GenerateEmbeddingsAsync(texts);
    stopwatch.Stop();

    // Assert
    Assert.True(result.Success);
    Assert.Equal(100, result.Embeddings.Count);
    Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 3000); // <3s for 100 chunks
}

[Fact]
public async Task ParallelEmbeddings_HandlesPartialFailures()
{
    // Arrange: Mock Ollama to fail on every 10th request
    var mockClient = CreateMockOllamaClient(failureRate: 0.1);
    var service = new EmbeddingService(mockClient, ...);
    var texts = Enumerable.Range(0, 100).Select(i => $"Text {i}").ToList();

    // Act
    var result = await service.GenerateEmbeddingsAsync(texts);

    // Assert
    Assert.False(result.Success);
    Assert.Contains("failed", result.ErrorMessage);
}
```

---

## 📈 Success Metrics

- **Indexing Speed**: 5x faster (10s → 2s)
- **Throughput**: 50 chunks/sec (vs 10 chunks/sec)
- **Failure Rate**: <1% under normal load

---

**Created**: 2025-11-22
**Estimated Effort**: 4-6 hours
