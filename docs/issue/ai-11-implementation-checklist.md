# AI-11 Quality Scoring System - Implementation Checklist

**Phase**: GREEN (Make Tests Pass)
**Prerequisites**: RED phase complete (43 tests written)

## Quick Reference

### Test Files Written
- ✅ `ResponseQualityServiceTests.cs` (13 tests)
- ✅ `QualityMetricsTests.cs` (10 tests)
- ✅ `QualityReportServiceTests.cs` (9 tests)
- ✅ `QualityTrackingIntegrationTests.cs` (11 tests)

---

## Implementation Order

### Step 1: Models (Foundation)
**Location**: `apps/api/src/Api/Models/`

#### 1.1 Create `QualityScores.cs`
```csharp
namespace Api.Models;

public class QualityScores
{
    public double RagConfidence { get; set; }
    public double LlmConfidence { get; set; }
    public double CitationQuality { get; set; }
    public double OverallConfidence { get; set; }
    public bool IsLowQuality => OverallConfidence < 0.60;
}
```

#### 1.2 Create `QualityReport.cs`
```csharp
namespace Api.Models;

public class QualityReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalResponses { get; set; }
    public int LowQualityCount { get; set; }
    public double LowQualityPercentage =>
        TotalResponses > 0 ? (LowQualityCount * 100.0 / TotalResponses) : 0;
    public double? AverageRagConfidence { get; set; }
    public double? AverageLlmConfidence { get; set; }
    public double? AverageCitationQuality { get; set; }
    public double? AverageOverallConfidence { get; set; }
}
```

#### 1.3 Update `Contracts.cs`
```csharp
public record LowQualityResponsesResult(
    int TotalCount,
    List<LowQualityResponseDto> Responses
);

public record LowQualityResponseDto(
    Guid Id,
    DateTime CreatedAt,
    string Query,
    double RagConfidence,
    double LlmConfidence,
    double CitationQuality,
    double OverallConfidence,
    bool IsLowQuality
);
```

**Test to Pass**: Compilation errors resolved

---

### Step 2: Database Migration
**Location**: `apps/api/src/Api/Migrations/`

#### 2.1 Create Migration
```bash
cd apps/api
dotnet ef migrations add AddQualityScoresToAiRequestLogs --project src/Api
```

#### 2.2 Migration Content
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<double>(
        name: "rag_confidence",
        table: "ai_request_logs",
        type: "double precision",
        nullable: false,
        defaultValue: 0.0);

    migrationBuilder.AddColumn<double>(
        name: "llm_confidence",
        table: "ai_request_logs",
        type: "double precision",
        nullable: false,
        defaultValue: 0.0);

    migrationBuilder.AddColumn<double>(
        name: "citation_quality",
        table: "ai_request_logs",
        type: "double precision",
        nullable: false,
        defaultValue: 0.0);

    migrationBuilder.AddColumn<double>(
        name: "overall_confidence",
        table: "ai_request_logs",
        type: "double precision",
        nullable: false,
        defaultValue: 0.0);

    migrationBuilder.AddColumn<bool>(
        name: "is_low_quality",
        table: "ai_request_logs",
        type: "boolean",
        nullable: false,
        defaultValue: false);

    // Add index for low-quality queries
    migrationBuilder.CreateIndex(
        name: "idx_ai_request_logs_is_low_quality",
        table: "ai_request_logs",
        column: "is_low_quality");

    migrationBuilder.CreateIndex(
        name: "idx_ai_request_logs_overall_confidence",
        table: "ai_request_logs",
        column: "overall_confidence");
}
```

#### 2.3 Update `AiRequestLogEntity.cs`
```csharp
public double RagConfidence { get; set; }
public double LlmConfidence { get; set; }
public double CitationQuality { get; set; }
public double OverallConfidence { get; set; }
public bool IsLowQuality { get; set; }
```

**Test to Pass**: Integration tests can query quality columns

---

### Step 3: Core Service - ResponseQualityService
**Location**: `apps/api/src/Api/Services/ResponseQualityService.cs`

#### 3.1 Implementation Strategy
```csharp
public class ResponseQualityService
{
    // Calculation logic
    public QualityScores CalculateQualityScores(
        List<RagSearchResult> ragResults,
        List<Citation>? citations,
        string? responseText,
        double? modelReportedConfidence = null)
    {
        var ragConfidence = CalculateRagConfidence(ragResults);
        var llmConfidence = modelReportedConfidence ??
            CalculateLlmConfidence(responseText);
        var citationQuality = CalculateCitationQuality(citations, responseText);
        var overallConfidence = CalculateOverallConfidence(
            ragConfidence, llmConfidence, citationQuality);

        return new QualityScores
        {
            RagConfidence = ragConfidence,
            LlmConfidence = llmConfidence,
            CitationQuality = citationQuality,
            OverallConfidence = overallConfidence
        };
    }

    private double CalculateRagConfidence(List<RagSearchResult> results)
    {
        // Average of top results, 0.0 if empty
    }

    private double CalculateLlmConfidence(string? text)
    {
        // Heuristic: word count, hedging phrases
    }

    private double CalculateCitationQuality(List<Citation>? citations, string? text)
    {
        // Ratio of citations to paragraphs, cap at 1.0
    }

    private double CalculateOverallConfidence(double rag, double llm, double citation)
    {
        // Weighted average: RAG 40%, LLM 40%, Citation 20%
        return (rag * 0.4) + (llm * 0.4) + (citation * 0.2);
    }
}
```

**Tests to Pass**: `ResponseQualityServiceTests.cs` (13 tests)

**Key Algorithms**:
- **RAG Confidence**: `results.Average(r => r.Score)` or `0.0` if empty
- **LLM Confidence**: Penalties for:
  - Very short (<50 words): -0.3
  - Short (<100 words): -0.15
  - Each hedging phrase: -0.05
  - Start at 0.85, cap at 0.0-1.0
- **Citation Quality**: `Math.Min(citations.Count / paragraphCount, 1.0)`
- **Hedging Phrases**: "might", "possibly", "unclear", "I'm not sure", "maybe", "perhaps", "I think"

---

### Step 4: Observability - QualityMetrics
**Location**: `apps/api/src/Api/Observability/QualityMetrics.cs`

#### 4.1 Implementation
```csharp
public class QualityMetrics
{
    private readonly Histogram<double> _qualityScoreHistogram;
    private readonly Counter<long> _lowQualityCounter;

    public QualityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MeepleAI.Quality");

        _qualityScoreHistogram = meter.CreateHistogram<double>(
            "meepleai.quality.score",
            unit: "score",
            description: "Quality score distribution across dimensions");

        _lowQualityCounter = meter.CreateCounter<long>(
            "meepleai.quality.low_quality_responses.total",
            unit: "responses",
            description: "Total low-quality responses");
    }

    public void RecordQualityScores(QualityScores scores, string agentType, string operation)
    {
        var tier = GetQualityTier(scores.OverallConfidence);

        _qualityScoreHistogram.Record(scores.RagConfidence,
            new("dimension", "rag_confidence"),
            new("agent.type", agentType),
            new("operation", operation),
            new("quality_tier", tier));

        _qualityScoreHistogram.Record(scores.LlmConfidence,
            new("dimension", "llm_confidence"),
            new("agent.type", agentType),
            new("operation", operation),
            new("quality_tier", tier));

        _qualityScoreHistogram.Record(scores.CitationQuality,
            new("dimension", "citation_quality"),
            new("agent.type", agentType),
            new("operation", operation),
            new("quality_tier", tier));

        _qualityScoreHistogram.Record(scores.OverallConfidence,
            new("dimension", "overall_confidence"),
            new("agent.type", agentType),
            new("operation", operation),
            new("quality_tier", tier));

        if (scores.IsLowQuality)
        {
            _lowQualityCounter.Add(1,
                new("agent.type", agentType),
                new("operation", operation));
        }
    }

    private string GetQualityTier(double confidence)
    {
        return confidence >= 0.80 ? "high" :
               confidence >= 0.60 ? "medium" : "low";
    }
}
```

**Tests to Pass**: `QualityMetricsTests.cs` (10 tests)

**Dependency**: Add to `Program.cs`:
```csharp
builder.Services.AddSingleton<QualityMetrics>();
```

---

### Step 5: Background Service - QualityReportService
**Location**: `apps/api/src/Api/Services/QualityReportService.cs`

#### 5.1 Interface
```csharp
public interface IQualityReportService
{
    Task<QualityReport> GenerateReportAsync(DateTime startDate, DateTime endDate);
}
```

#### 5.2 Implementation
```csharp
public class QualityReportService : BackgroundService, IQualityReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QualityReportService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _initialDelay;

    public QualityReportService(
        IServiceScopeFactory scopeFactory,
        ILogger<QualityReportService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(
            configuration.GetValue<double>("QualityReporting:IntervalMinutes", 60));
        _initialDelay = TimeSpan.FromMinutes(
            configuration.GetValue<double>("QualityReporting:InitialDelayMinutes", 1));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_initialDelay, stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IQualityReportService>();
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddDays(-7);
                await service.GenerateReportAsync(startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quality report");
            }
        }
    }

    public async Task<QualityReport> GenerateReportAsync(DateTime startDate, DateTime endDate)
    {
        // Query ai_request_logs between dates
        // Calculate statistics
        // Return QualityReport
    }
}
```

**Tests to Pass**: `QualityReportServiceTests.cs` (9 tests)

**Configuration** (`appsettings.json`):
```json
"QualityReporting": {
  "IntervalMinutes": 60,
  "InitialDelayMinutes": 1
}
```

---

### Step 6: Update AiRequestLogService
**Location**: `apps/api/src/Api/Services/AiRequestLogService.cs`

#### 6.1 Add Quality Score Storage
```csharp
public async Task LogRequestAsync(
    // ... existing parameters
    QualityScores qualityScores)
{
    var log = new AiRequestLogEntity
    {
        // ... existing properties
        RagConfidence = qualityScores.RagConfidence,
        LlmConfidence = qualityScores.LlmConfidence,
        CitationQuality = qualityScores.CitationQuality,
        OverallConfidence = qualityScores.OverallConfidence,
        IsLowQuality = qualityScores.IsLowQuality
    };
    _dbContext.AiRequestLogs.Add(log);
    await _dbContext.SaveChangesAsync();
}
```

#### 6.2 Integrate with Q&A Flow
Update Q&A endpoint to:
1. Call `ResponseQualityService.CalculateQualityScores()`
2. Pass scores to `AiRequestLogService.LogRequestAsync()`
3. Call `QualityMetrics.RecordQualityScores()`

---

### Step 7: Admin Endpoints
**Location**: `apps/api/src/Api/Program.cs`

#### 7.1 GET /admin/quality/low-responses
```csharp
adminApi.MapGet("/quality/low-responses", async (
    [FromQuery] int limit = 50,
    [FromQuery] int offset = 0,
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    MeepleAiDbContext dbContext,
    ClaimsPrincipal user) =>
{
    // Authorization: Admin role required
    var query = dbContext.AiRequestLogs.Where(log => log.IsLowQuality);

    if (startDate.HasValue)
        query = query.Where(log => log.CreatedAt >= startDate.Value);
    if (endDate.HasValue)
        query = query.Where(log => log.CreatedAt <= endDate.Value);

    var totalCount = await query.CountAsync();
    var responses = await query
        .OrderByDescending(log => log.CreatedAt)
        .Skip(offset)
        .Take(limit)
        .ToListAsync();

    return Results.Ok(new LowQualityResponsesResult(totalCount, responses));
})
.RequireAuthorization("Admin");
```

#### 7.2 GET /admin/quality/report
```csharp
adminApi.MapGet("/quality/report", async (
    [FromQuery] int days = 7,
    IQualityReportService reportService) =>
{
    var endDate = DateTime.UtcNow;
    var startDate = endDate.AddDays(-days);
    var report = await reportService.GenerateReportAsync(startDate, endDate);
    return Results.Ok(report);
})
.RequireAuthorization("Admin");
```

**Tests to Pass**: `QualityTrackingIntegrationTests.cs` (11 tests)

---

### Step 8: Dependency Injection
**Location**: `apps/api/src/Api/Program.cs`

```csharp
// Services
builder.Services.AddScoped<ResponseQualityService>();
builder.Services.AddScoped<IQualityReportService, QualityReportService>();
builder.Services.AddSingleton<QualityMetrics>();

// Background service
builder.Services.AddHostedService<QualityReportService>();
```

---

## Test Execution Plan

### Phase 1: Unit Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~ResponseQualityServiceTests"
dotnet test --filter "FullyQualifiedName~QualityMetricsTests"
dotnet test --filter "FullyQualifiedName~QualityReportServiceTests"
```

### Phase 2: Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~QualityTrackingIntegrationTests"
```

### Phase 3: Full Suite
```bash
dotnet test
```

---

## Debugging Checklist

### If ResponseQualityServiceTests Fail:
- [ ] Check RAG confidence calculation (average vs empty list)
- [ ] Verify LLM confidence heuristic (word count penalties)
- [ ] Test hedging phrase detection (case-insensitive?)
- [ ] Validate citation quality (paragraph counting)
- [ ] Confirm weighted average formula (40/40/20)
- [ ] Test boundary at 0.60 (exclusive threshold)

### If QualityMetricsTests Fail:
- [ ] Verify `TestMeterFactory` usage
- [ ] Check metric names exactly match expected
- [ ] Validate tag keys and values
- [ ] Test counter increments only for low-quality
- [ ] Confirm histogram records all 4 dimensions

### If QualityReportServiceTests Fail:
- [ ] Check `IServiceScopeFactory` mocking
- [ ] Verify timer intervals (use short intervals for tests)
- [ ] Test cancellation token handling
- [ ] Validate exception logging
- [ ] Confirm scope disposal

### If QualityTrackingIntegrationTests Fail:
- [ ] Verify Testcontainers starts PostgreSQL
- [ ] Check database connection string
- [ ] Test migration applied automatically
- [ ] Validate authentication setup (session cookies)
- [ ] Confirm admin role authorization

---

## Performance Considerations

- **Hedging Phrase Detection**: Use `StringComparison.OrdinalIgnoreCase` for case-insensitive matching
- **Database Indexes**: Add indexes on `is_low_quality` and `overall_confidence` columns
- **Background Service**: Use `PeriodicTimer` for efficient scheduling
- **Metrics**: OpenTelemetry histograms are efficient, no batching needed

---

## Acceptance Criteria

- [ ] All 13 ResponseQualityService tests pass
- [ ] All 10 QualityMetrics tests pass
- [ ] All 9 QualityReportService tests pass
- [ ] All 11 QualityTrackingIntegration tests pass
- [ ] Total: 43/43 tests passing
- [ ] Code coverage ≥ 80% for new services
- [ ] Prometheus metrics visible at `/metrics`
- [ ] Admin endpoints return 401/403 for unauthorized
- [ ] Low-quality responses flagged in database
- [ ] Background service runs without errors

---

## Completion Definition of Done (DoD)

1. ✅ All 43 tests passing (GREEN phase)
2. ✅ Database migration applied successfully
3. ✅ Services registered in DI container
4. ✅ Admin endpoints implemented with authorization
5. ✅ Prometheus metrics recording correctly
6. ✅ Background service running and generating reports
7. ✅ Quality scores stored in `ai_request_logs` table
8. ✅ Integration with Q&A flow complete
9. ✅ Documentation updated (this file + summary doc)
10. ✅ Code review ready (clean, commented, follows conventions)

---

**Estimated Implementation Time**: 6-8 hours
**Complexity**: Medium-High
**Dependencies**: OpenTelemetry metrics, EF Core, background services

**Next Document**: `ai-11-implementation-notes.md` (during GREEN phase)
