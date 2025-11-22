# Quick Win #001: Extract and Standardize RagExceptionHandler Pattern

**Priority**: ⚡ QUICK WIN
**Effort**: 4 hours
**Impact**: ⭐⭐ MEDIUM
**ROI**: Very High (standardizes error handling across services)
**Status**: Not Started

---

## Problem

24+ similar `catch (HttpRequestException)` blocks duplicated across services:
- RagService (8 blocks)
- HybridSearchService (4 blocks)
- QdrantService (3 blocks)
- EmbeddingService (5 blocks)
- BggApiService (4+ blocks)

Each block has similar structure but slightly different error messages.

---

## Good News

`RagExceptionHandler` **already exists** at:
`apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`

We just need to extend its usage to other services!

---

## Solution

Standardize exception handling pattern across all services using existing `RagExceptionHandler`.

---

## Implementation Steps

### Step 1: Review Existing Handler (30 minutes)

**File**: `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`

Review current implementation:
- What exception types are handled?
- What error response patterns are supported?
- What needs to be extended?

---

### Step 2: Extend RagExceptionHandler (1 hour)

Add generic exception handling method (if not already present):

```csharp
public static class RagExceptionHandler
{
    public static T HandleException<T>(
        Exception ex,
        ILogger logger,
        string gameId,
        string operation,
        Activity? activity,
        Stopwatch stopwatch,
        Func<T> errorResponseFactory)
    {
        // Record metrics
        MeepleAiMetrics.RagRequests.Add(1,
            new KeyValuePair<string, object?>("game_id", gameId),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", false));

        // Set trace status
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

        // Log appropriately
        var logAction = GetLogAction(ex);
        logAction(logger, ex, gameId, operation);

        // Record latency
        stopwatch.Stop();
        MeepleAiMetrics.RagLatency.Record(stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", false));

        // Return error response
        return errorResponseFactory();
    }

    private static Action<ILogger, Exception, string, string> GetLogAction(Exception ex)
        => ex switch
        {
            HttpRequestException => (logger, ex, gameId, op) =>
                logger.LogError(ex, "Network error in {Operation} for game {GameId}", op, gameId),
            ValidationException => (logger, ex, gameId, op) =>
                logger.LogWarning(ex, "Validation error in {Operation} for game {GameId}", op, gameId),
            _ => (logger, ex, gameId, op) =>
                logger.LogError(ex, "Unexpected error in {Operation} for game {GameId}", op, gameId)
        };
}
```

---

### Step 3: Apply to HybridSearchService (1 hour)

**File**: `apps/api/src/Api/Services/HybridSearchService.cs`

**Before**:
```csharp
try
{
    // Business logic
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error in hybrid search for game {GameId}", gameId);
    MeepleAiMetrics.SearchRequests.Add(1, ...);
    return new SearchResponse("Network error...", Array.Empty<SearchResult>());
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in hybrid search for game {GameId}", gameId);
    return new SearchResponse("Unexpected error...", Array.Empty<SearchResult>());
}
```

**After**:
```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    return RagExceptionHandler.HandleException(
        ex,
        _logger,
        gameId,
        "hybrid-search",
        activity,
        stopwatch,
        () => new SearchResponse(
            ex is HttpRequestException
                ? "Network error connecting to search service"
                : "An unexpected error occurred during search",
            Array.Empty<SearchResult>()));
}
```

**Files to update**:
- `HybridSearchService.cs` - 4 exception blocks
- Test that all error scenarios still work

---

### Step 4: Apply to Other Services (1.5 hours)

Apply same pattern to:

1. **QdrantService.cs** (3 exception blocks)
   ```csharp
   catch (Exception ex)
   {
       return RagExceptionHandler.HandleException(
           ex, _logger, collectionName, "vector-search", activity, stopwatch,
           () => Array.Empty<SearchResult>());
   }
   ```

2. **EmbeddingService.cs** (5 exception blocks)
   ```csharp
   catch (Exception ex)
   {
       return RagExceptionHandler.HandleException(
           ex, _logger, text, "embedding-generation", activity, stopwatch,
           () => Array.Empty<float>());
   }
   ```

3. **BggApiService.cs** (4 exception blocks)
   ```csharp
   catch (Exception ex)
   {
       return RagExceptionHandler.HandleException(
           ex, _logger, gameName, "bgg-api-search", activity, stopwatch,
           () => new BggSearchResult { Games = Array.Empty<BggGame>() });
   }
   ```

---

### Step 5: Testing (30 minutes)

**Unit Tests**:
- Test each service's error handling
- Verify metrics are recorded
- Verify logging is correct
- Verify tracing works

**Integration Tests**:
- Test network errors (mock HttpClient)
- Test validation errors
- Test unexpected errors

---

## Files Changed

**Modified Files** (5):
- `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs` (+20 LOC)
- `apps/api/src/Api/Services/HybridSearchService.cs` (-40 LOC)
- `apps/api/src/Api/Services/QdrantService.cs` (-30 LOC)
- `apps/api/src/Api/Services/EmbeddingService.cs` (-50 LOC)
- `apps/api/src/Api/Services/BggApiService.cs` (-40 LOC)

**Total LOC Change**: -140 LOC (net reduction)

---

## Acceptance Criteria

- [ ] RagExceptionHandler extended to support all service types
- [ ] HybridSearchService uses RagExceptionHandler
- [ ] QdrantService uses RagExceptionHandler
- [ ] EmbeddingService uses RagExceptionHandler
- [ ] BggApiService uses RagExceptionHandler
- [ ] All existing tests pass
- [ ] Error messages remain consistent with current behavior
- [ ] Metrics and tracing work correctly

---

## Success Metrics

- ✅ 20+ exception blocks → 5 handler calls (75% reduction)
- ✅ Consistent error logging across all services
- ✅ Consistent metrics tracking
- ✅ Consistent trace error status
- ✅ ~140 LOC reduction

---

## Estimated Timeline

| Task | Time |
|------|------|
| Review existing handler | 30min |
| Extend RagExceptionHandler | 1h |
| Apply to HybridSearchService | 1h |
| Apply to other services | 1.5h |
| Testing | 30min |
| **Total** | **4h** |

---

## Related Issues

- Issue #003: Refactor RagService (builds on this pattern)

---

## References

- Current Handler: `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`
- Analysis: `docs/02-development/backend-codebase-analysis.md`
