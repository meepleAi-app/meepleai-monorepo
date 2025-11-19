# Issue #003: Refactor RagService Exception Handling and Extract Configuration

**Priority**: 🔴 CRITICAL
**Effort**: 40-50 hours
**Impact**: ⭐⭐⭐ HIGH
**Category**: Code Quality & Complexity Reduction
**Status**: Not Started

---

## Problem Description

`RagService.cs` is a **995 LOC service** with **very high cyclomatic complexity** caused by:
- **24 duplicated try-catch blocks** with similar exception handling patterns
- Configuration logic mixed with RAG orchestration
- Multiple responsibilities (Q&A, Explain, Search, Configuration)
- **11 injected dependencies** (high coupling)

**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/RagService.cs:995`

### Specific Issues

#### 1. Exception Handling Duplication (24 blocks!)

**Pattern repeated 24 times** across methods:
```csharp
// In AskAsync (lines 242-248)
catch (HttpRequestException ex)
{
    return RagExceptionHandler.HandleException(
        ex, _logger,
        RagExceptionHandler.GetLogAction(...),
        gameId, "qa", activity, stopwatch,
        () => new QaResponse("Network error connecting to AI service..."));
}

// In AskWithHybridSearchAsync (lines 681-687) - SAME PATTERN
catch (HttpRequestException ex)
{
    return RagExceptionHandler.HandleException(
        ex, _logger,
        RagExceptionHandler.GetLogAction(...),
        gameId, "hybrid-search", activity, stopwatch,
        () => new QaResponse("Network error..."));
}

// In AskWithCustomPromptAsync (lines 878-884) - SAME PATTERN AGAIN
catch (HttpRequestException ex)
{
    return RagExceptionHandler.HandleException(
        ex, _logger,
        RagExceptionHandler.GetLogAction(...),
        gameId, "custom-prompt", activity, stopwatch,
        () => new QaResponse("Network error..."));
}
```

**Good News**: `RagExceptionHandler` already exists but is not fully utilized!
**Location**: `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`

#### 2. Configuration Logic Embedded

**Lines 930-994** contain RAG configuration management:
```csharp
public async Task<RagConfig> GetRagConfigAsync()
{
    // 3-tier fallback: DB → appsettings → defaults
    // This is infrastructure concern, not RAG orchestration
}

private bool ValidateRagConfig(RagConfig config)
{
    // Validation logic should be in domain service
}
```

This should be extracted to a dedicated service.

---

## Proposed Solution

### Goal: Reduce RagService from 995 LOC to ~400 LOC

**Refactoring Strategy**:

1. **Extract RagConfigurationProvider** (~40 LOC)
   - Move: `GetRagConfigAsync()` + `ValidateRagConfig()`
   - Handle 3-tier fallback logic (DB → appsettings → defaults)

2. **Fully Utilize RagExceptionHandler** (already exists)
   - Apply to all 24 exception blocks
   - Reduce boilerplate by ~300 LOC

3. **Standardize Error Responses**
   - Create operation-specific error response factories
   - Reduce duplicate error message strings

---

## Acceptance Criteria

- [ ] **RagConfigurationProvider** service created (~40 LOC)
- [ ] Configuration logic extracted from RagService
- [ ] RagExceptionHandler pattern applied to all 24 catch blocks
- [ ] RagService reduced to ≤400 LOC (60% reduction from 995)
- [ ] All existing tests pass without modification
- [ ] No breaking changes to API contracts
- [ ] Cyclomatic complexity reduced by 50%+
- [ ] Unit tests for RagConfigurationProvider
- [ ] Integration tests verify error handling works correctly

---

## Implementation Plan

### Phase 1: Extract RagConfigurationProvider (8 hours)

#### 1.1 Create RagConfigurationProvider Service

**File**: `apps/api/src/Api/Services/Rag/RagConfigurationProvider.cs`

```csharp
namespace Api.Services.Rag;

/// <summary>
/// Provides RAG configuration with 3-tier fallback:
/// 1. Database (dynamic runtime config)
/// 2. appsettings.json (deployment config)
/// 3. Hardcoded defaults (failsafe)
/// </summary>
public class RagConfigurationProvider : IRagConfigurationProvider
{
    private readonly IConfigurationService? _configService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<RagConfigurationProvider> _logger;

    // CONFIG-04: Hardcoded defaults (lowest priority)
    private const int DefaultTopK = 5;
    private const double DefaultMinScore = 0.7;

    public RagConfigurationProvider(
        ILogger<RagConfigurationProvider> logger,
        IConfigurationService? configService = null,
        IConfiguration? configuration = null)
    {
        _logger = logger;
        _configService = configService;
        _configuration = configuration;
    }

    public async Task<RagConfig> GetConfigAsync()
    {
        var config = new RagConfig();

        // Tier 1: Database (highest priority)
        if (_configService != null)
        {
            config.TopK = await _configService.GetValueAsync<int?>("Rag:TopK") ?? config.TopK;
            config.MinScore = await _configService.GetValueAsync<double?>("Rag:MinScore") ?? config.MinScore;
            config.Temperature = await _configService.GetValueAsync<double?>("Rag:Temperature") ?? config.Temperature;
            _logger.LogDebug("Loaded RAG config from database");
        }

        // Tier 2: appsettings.json (fallback)
        if (_configuration != null)
        {
            config.TopK = _configuration.GetValue<int?>("Rag:TopK") ?? config.TopK;
            config.MinScore = _configuration.GetValue<double?>("Rag:MinScore") ?? config.MinScore;
            config.Temperature = _configuration.GetValue<double?>("Rag:Temperature") ?? config.Temperature;
            _logger.LogDebug("Loaded RAG config from appsettings.json");
        }

        // Tier 3: Hardcoded defaults (already set in constructor)
        if (config.TopK == 0) config.TopK = DefaultTopK;
        if (config.MinScore == 0) config.MinScore = DefaultMinScore;

        // Validate
        if (!ValidateConfig(config))
        {
            _logger.LogWarning("Invalid RAG config detected, using defaults");
            return new RagConfig
            {
                TopK = DefaultTopK,
                MinScore = DefaultMinScore
            };
        }

        return config;
    }

    private bool ValidateConfig(RagConfig config)
    {
        if (config.TopK < 1 || config.TopK > 100)
        {
            _logger.LogWarning("Invalid TopK: {TopK}, must be 1-100", config.TopK);
            return false;
        }

        if (config.MinScore < 0 || config.MinScore > 1)
        {
            _logger.LogWarning("Invalid MinScore: {MinScore}, must be 0-1", config.MinScore);
            return false;
        }

        return true;
    }
}

public interface IRagConfigurationProvider
{
    Task<RagConfig> GetConfigAsync();
}

public class RagConfig
{
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.7;
    public double Temperature { get; set; } = 0.2;
    // Add other RAG config properties as needed
}
```

**Testing** (`RagConfigurationProviderTests.cs`):
```csharp
public class RagConfigurationProviderTests
{
    [Fact]
    public async Task GetConfigAsync_WithDatabaseConfig_ReturnsDatabaseValues()
    {
        // Arrange
        var mockConfigService = new Mock<IConfigurationService>();
        mockConfigService.Setup(x => x.GetValueAsync<int?>("Rag:TopK"))
            .ReturnsAsync(10);
        mockConfigService.Setup(x => x.GetValueAsync<double?>("Rag:MinScore"))
            .ReturnsAsync(0.8);

        var provider = new RagConfigurationProvider(
            Mock.Of<ILogger<RagConfigurationProvider>>(),
            mockConfigService.Object);

        // Act
        var config = await provider.GetConfigAsync();

        // Assert
        Assert.Equal(10, config.TopK);
        Assert.Equal(0.8, config.MinScore);
    }

    [Fact]
    public async Task GetConfigAsync_WithInvalidTopK_ReturnsDefaults()
    {
        // Test validation logic
    }

    [Fact]
    public async Task GetConfigAsync_WithNoConfigService_UsesAppsettings()
    {
        // Test appsettings fallback
    }

    [Fact]
    public async Task GetConfigAsync_WithNoConfig_UsesHardcodedDefaults()
    {
        // Test hardcoded defaults
    }
}
```

---

#### 1.2 Update RagService to Use RagConfigurationProvider

**Remove from RagService** (lines 930-994):
```csharp
// DELETE THESE METHODS
private async Task<RagConfig> GetRagConfigAsync() { ... }
private bool ValidateRagConfig(RagConfig config) { ... }
```

**Update RagService constructor**:
```csharp
public class RagService : IRagService
{
    private readonly IRagConfigurationProvider _configProvider; // NEW

    public RagService(
        // ... existing dependencies
        IRagConfigurationProvider configProvider) // NEW
    {
        // ... existing initialization
        _configProvider = configProvider; // NEW
    }

    public async Task<QaResponse> AskAsync(...)
    {
        // OLD: var config = await GetRagConfigAsync();
        // NEW:
        var config = await _configProvider.GetConfigAsync();

        // Rest of method unchanged
    }
}
```

**Register in DI** (`Program.cs`):
```csharp
builder.Services.AddScoped<IRagConfigurationProvider, RagConfigurationProvider>();
```

**Savings**: ~65 LOC removed from RagService

---

### Phase 2: Standardize Exception Handling (20 hours)

#### 2.1 Analyze Current RagExceptionHandler

**Current Location**: `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`

Review existing implementation and identify:
- What exception types are already handled
- What error response patterns are already supported
- What needs to be extended

---

#### 2.2 Create Operation-Specific Error Response Factories

**File**: `apps/api/src/Api/Services/Rag/RagErrorResponses.cs`

```csharp
namespace Api.Services.Rag;

public static class RagErrorResponses
{
    public static QaResponse NetworkError(string operation)
        => new QaResponse(
            $"Network error connecting to AI service during {operation}. Please try again.",
            Array.Empty<Snippet>());

    public static QaResponse ValidationError(string message)
        => new QaResponse(
            $"Validation error: {message}",
            Array.Empty<Snippet>());

    public static QaResponse EmbeddingError()
        => new QaResponse(
            "Error generating embeddings. Please try again.",
            Array.Empty<Snippet>());

    public static QaResponse SearchError()
        => new QaResponse(
            "Error searching knowledge base. Please try again.",
            Array.Empty<Snippet>());

    public static QaResponse LlmError()
        => new QaResponse(
            "Error generating AI response. Please try again.",
            Array.Empty<Snippet>());

    public static QaResponse UnexpectedError()
        => new QaResponse(
            "An unexpected error occurred. Please try again.",
            Array.Empty<Snippet>());

    // Explain responses
    public static ExplanationResponse ExplainNetworkError()
        => new ExplanationResponse(
            "Network error connecting to AI service. Please try again.",
            Array.Empty<Snippet>());

    // Search responses
    public static SearchResponse SearchNetworkError()
        => new SearchResponse(
            "Network error during search. Please try again.",
            Array.Empty<SearchResult>());
}
```

---

#### 2.3 Refactor AskAsync Exception Handling

**Before** (lines 75-286, with 5 catch blocks):
```csharp
public async Task<QaResponse> AskAsync(...)
{
    try
    {
        // ... 200 lines of business logic
    }
    catch (HttpRequestException ex)
    {
        return RagExceptionHandler.HandleException(
            ex, _logger,
            RagExceptionHandler.GetLogAction(...),
            gameId, "qa", activity, stopwatch,
            () => new QaResponse("Network error connecting to AI service..."));
    }
    catch (ValidationException ex)
    {
        return RagExceptionHandler.HandleException(
            ex, _logger,
            RagExceptionHandler.GetLogAction(...),
            gameId, "qa", activity, stopwatch,
            () => new QaResponse($"Validation error: {ex.Message}"));
    }
    catch (Exception ex)
    {
        return RagExceptionHandler.HandleException(
            ex, _logger,
            RagExceptionHandler.GetLogAction(...),
            gameId, "qa", activity, stopwatch,
            () => new QaResponse("An unexpected error occurred..."));
    }
}
```

**After** (reduced boilerplate):
```csharp
public async Task<QaResponse> AskAsync(...)
{
    return await RagExceptionHandler.ExecuteWithHandlingAsync(
        operation: "qa",
        gameId: gameId,
        activity: activity,
        logger: _logger,
        action: async () =>
        {
            // ... 200 lines of business logic (unchanged)
            // All exception handling moved to ExecuteWithHandlingAsync
        },
        errorResponseFactory: (ex) => ex switch
        {
            HttpRequestException => RagErrorResponses.NetworkError("Q&A"),
            ValidationException validationEx => RagErrorResponses.ValidationError(validationEx.Message),
            EmbeddingException => RagErrorResponses.EmbeddingError(),
            _ => RagErrorResponses.UnexpectedError()
        });
}
```

**New method in RagExceptionHandler**:
```csharp
public static class RagExceptionHandler
{
    public static async Task<TResponse> ExecuteWithHandlingAsync<TResponse>(
        string operation,
        string gameId,
        Activity? activity,
        ILogger logger,
        Func<Task<TResponse>> action,
        Func<Exception, TResponse> errorResponseFactory)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            var result = await action();
            success = true;
            return result;
        }
        catch (Exception ex)
        {
            // Metrics
            MeepleAiMetrics.RagRequests.Add(1,
                new KeyValuePair<string, object?>("game_id", gameId),
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("success", false));

            // Tracing
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Logging
            var logAction = GetLogAction(ex);
            logAction(logger, ex, gameId, operation);

            // Return error response
            return errorResponseFactory(ex);
        }
        finally
        {
            stopwatch.Stop();
            MeepleAiMetrics.RagLatency.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("success", success));
        }
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

#### 2.4 Apply Pattern to All Methods

Apply `ExecuteWithHandlingAsync` pattern to:

1. **AskAsync** (lines 75-286) - 5 catch blocks → 1 handler
2. **ExplainAsync** (lines 294-454) - 4 catch blocks → 1 handler
3. **AskWithHybridSearchAsync** (lines 522-725) - 8 catch blocks → 1 handler
4. **AskWithCustomPromptAsync** (lines 732-922) - 7 catch blocks → 1 handler

**Total reduction**: ~24 catch blocks → 4 handler calls (~250 LOC saved)

---

### Phase 3: Testing (12 hours)

#### 3.1 Unit Tests for RagConfigurationProvider

**File**: `tests/Api.Tests/Services/Rag/RagConfigurationProviderTests.cs`

```csharp
public class RagConfigurationProviderTests
{
    [Fact]
    public async Task GetConfigAsync_WithDatabaseConfig_ReturnsDatabaseValues() { }

    [Fact]
    public async Task GetConfigAsync_DatabaseConfigOverridesAppsettings() { }

    [Fact]
    public async Task GetConfigAsync_AppsettingsOverridesDefaults() { }

    [Fact]
    public async Task GetConfigAsync_InvalidTopK_ReturnsDefaults() { }

    [Fact]
    public async Task GetConfigAsync_InvalidMinScore_ReturnsDefaults() { }

    [Fact]
    public async Task GetConfigAsync_NoConfigSources_ReturnsHardcodedDefaults() { }
}
```

---

#### 3.2 Integration Tests for Exception Handling

**File**: `tests/Api.Tests/Services/Rag/RagServiceExceptionHandlingTests.cs`

```csharp
public class RagServiceExceptionHandlingTests
{
    [Fact]
    public async Task AskAsync_NetworkError_ReturnsErrorResponse()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var ragService = CreateRagService(embeddingService: mockEmbeddingService.Object);

        // Act
        var result = await ragService.AskAsync("tic-tac-toe", "How to play?");

        // Assert
        Assert.Contains("Network error", result.Answer);
        Assert.Empty(result.Snippets);
    }

    [Fact]
    public async Task AskAsync_ValidationError_ReturnsValidationErrorResponse() { }

    [Fact]
    public async Task AskAsync_UnexpectedError_ReturnsGenericErrorResponse() { }

    [Fact]
    public async Task AskAsync_ErrorMetricsRecorded() { }

    [Fact]
    public async Task AskAsync_ErrorTracingRecorded() { }
}
```

---

#### 3.3 Verify All Existing Tests Pass

Run full test suite:
```bash
dotnet test --filter "FullyQualifiedName~RagService"
```

Ensure:
- [ ] All 20+ existing RagService tests pass
- [ ] Integration tests pass
- [ ] No breaking changes to API contracts

---

### Phase 4: Documentation and Cleanup (2 hours)

#### 4.1 Update RagService Documentation

Add XML documentation:
```csharp
/// <summary>
/// RAG (Retrieval-Augmented Generation) orchestration service.
/// Coordinates embedding generation, vector search, and LLM response generation.
/// </summary>
/// <remarks>
/// Configuration: Uses <see cref="IRagConfigurationProvider"/> for 3-tier config fallback.
/// Exception Handling: Uses <see cref="RagExceptionHandler"/> for consistent error responses.
/// Metrics: Records latency and success/failure via OpenTelemetry.
/// </remarks>
public class RagService : IRagService
{
    // ...
}
```

---

#### 4.2 Update Architecture Documentation

**File**: `docs/01-architecture/services/rag-service.md`

Document:
- RagConfigurationProvider pattern
- Exception handling strategy
- Error response factory pattern
- Metrics and tracing

---

## File Changes Summary

### New Files (2)
- ✅ `apps/api/src/Api/Services/Rag/RagConfigurationProvider.cs` (~40 LOC)
- ✅ `apps/api/src/Api/Services/Rag/RagErrorResponses.cs` (~60 LOC)

### Modified Files (2)
- 📝 `apps/api/src/Api/Services/RagService.cs` (995 → 400 LOC, -595 LOC)
- 📝 `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs` (+50 LOC for ExecuteWithHandlingAsync)

### New Test Files (2)
- ✅ `tests/Api.Tests/Services/Rag/RagConfigurationProviderTests.cs` (~100 LOC)
- ✅ `tests/Api.Tests/Services/Rag/RagServiceExceptionHandlingTests.cs` (~150 LOC)

**Total LOC Change**: -340 LOC (net reduction)

---

## Dependencies

**Blocks**:
- None

**Blocked by**:
- None

**Related Issues**:
- Issue #002: Migrate ConfigurationService to CQRS (RagConfigurationProvider may benefit from CQRS config)
- Issue #007: Extract Validation Framework (error response patterns similar)

---

## Success Metrics

- ✅ RagService reduced from 995 to ≤400 LOC (60% reduction)
- ✅ 24 catch blocks → 4 handler calls (83% reduction in exception boilerplate)
- ✅ RagConfigurationProvider extracted (~40 LOC)
- ✅ Cyclomatic complexity reduced by 50%+
- ✅ All existing tests pass (20+ tests)
- ✅ New tests added (15+ tests for config provider + exception handling)
- ✅ Zero breaking changes to API
- ✅ Code review approved

---

## Risk Mitigation

**Risk 1**: Breaking existing error handling behavior
- **Mitigation**: Preserve exact error response messages
- **Verification**: Integration tests for all error scenarios

**Risk 2**: Performance regression from exception handling wrapper
- **Mitigation**: Benchmark before/after
- **Verification**: Performance tests with k6

**Risk 3**: Missing error scenarios
- **Mitigation**: Test all exception types (HttpRequestException, ValidationException, etc.)
- **Verification**: 100% test coverage for exception handling

---

## Estimated Timeline

**Total Effort**: 40-50 hours

| Phase | Task | Hours |
|-------|------|-------|
| 1 | Extract RagConfigurationProvider | 8h |
| 2 | Standardize exception handling | 20h |
| 3 | Testing (unit + integration) | 12h |
| 4 | Documentation and cleanup | 2h |
| - | **Buffer** | 8-10h |

**Recommended approach**: 1 week sprint (8 hours/day)

---

## References

- Analysis: `docs/02-development/backend-codebase-analysis.md`
- Current File: `apps/api/src/Api/Services/RagService.cs:995`
- Existing Handler: `apps/api/src/Api/Services/Rag/RagExceptionHandler.cs`
