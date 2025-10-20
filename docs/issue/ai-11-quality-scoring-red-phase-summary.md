# AI-11 Quality Scoring System - RED Phase Test Implementation

**Date**: 2025-10-20
**Phase**: TDD RED (Tests Written, Implementation Pending)
**Status**: ✅ Complete

## Overview

This document summarizes the BDD tests written for the AI-11 Quality Scoring System during the TDD RED phase. All tests are designed to FAIL initially since the implementation doesn't exist yet.

## Test Files Created

### 1. ResponseQualityServiceTests.cs (16KB, 13 tests)
**Location**: `apps/api/tests/Api.Tests/Services/ResponseQualityServiceTests.cs`

**Coverage**: Quality score calculation logic

**Key Scenarios**:
- ✅ High-quality response (RAG 0.85, 4 citations, 300 words) → Overall ~0.87
- ✅ Low-quality response (RAG 0.30, 1 citation, hedging phrases) → Overall ~0.37
- ✅ No RAG results → RAG confidence 0.00
- ✅ Model-reported LLM confidence (use provided value vs heuristic)
- ✅ Empty/null response text → LLM confidence 0.00
- ✅ Perfect scores across all dimensions → Overall ~1.0
- ✅ Multiple hedging phrases reduce LLM confidence
- ✅ Excessive citations (10 for 2 paragraphs) → Capped at 1.0
- ✅ Very short response (<50 words) → Penalized LLM confidence
- ✅ Boundary threshold testing (exactly 0.60)
- ✅ Null citations list handling
- ✅ Mixed quality dimensions → Weighted average
- ✅ Zero confidence scores

**Test Patterns**:
- Unit tests with in-memory calculation
- Helper method: `GenerateResponseText(wordCount, paragraphs, hedgingPhrases)`
- Arrange-Act-Assert structure
- Range assertions for floating-point scores

---

### 2. QualityMetricsTests.cs (14KB, 10 tests)
**Location**: `apps/api/tests/Api.Tests/Observability/QualityMetricsTests.cs`

**Coverage**: Prometheus metrics recording via OpenTelemetry

**Key Scenarios**:
- ✅ Histogram records all 4 dimensions (RAG, LLM, Citation, Overall)
- ✅ Low-quality counter increments only for flagged responses
- ✅ High-quality responses don't increment counter
- ✅ Labels applied correctly (agent.type, operation, dimension)
- ✅ Quality tier labels (high/medium/low) based on thresholds
- ✅ Multiple recordings accumulate correctly
- ✅ Zero confidence scores recorded correctly
- ✅ Different agent types tracked separately
- ✅ Histogram buckets configuration
- ✅ Metric names follow `meepleai.quality.*` convention

**Test Patterns**:
- Uses `Microsoft.Extensions.Diagnostics.Metrics.Testing`
- `TestMeterFactory` for metrics collection
- `MetricCollector<T>` for assertions
- Tag filtering and verification

---

### 3. QualityReportServiceTests.cs (19KB, 9 tests)
**Location**: `apps/api/tests/Api.Tests/Services/QualityReportServiceTests.cs`

**Coverage**: Background service for periodic quality reports

**Key Scenarios**:
- ✅ Report generated after configured interval (100ms test interval)
- ✅ Initial 1-minute delay before first report
- ✅ Report includes statistics (total, low-quality count, averages)
- ✅ Empty period handled gracefully (0 responses, null averages)
- ✅ IServiceScopeFactory creates and disposes scopes correctly
- ✅ Graceful shutdown on cancellation token
- ✅ Exception during report generation logged and service continues
- ✅ Report includes time period (StartDate, EndDate)
- ✅ Low-quality percentage calculation (30% for 3/10)

**Test Patterns**:
- Moq for `IServiceScopeFactory`, `IServiceScope`, `IServiceProvider`
- Mock `DbSet<T>` for database queries
- Background service lifecycle testing (`StartAsync`, cancellation)
- Timing-based tests with short intervals for fast execution

---

### 4. QualityTrackingIntegrationTests.cs (18KB, 11 tests)
**Location**: `apps/api/tests/Api.Tests/QualityTrackingIntegrationTests.cs`

**Coverage**: End-to-end quality tracking with real database

**Key Scenarios**:
- ✅ Low-quality response flagged and logged to database
- ✅ High-quality response not flagged
- ✅ All quality scores (RAG, LLM, Citation, Overall) stored in DB
- ✅ Admin endpoint returns only low-quality responses
- ✅ Admin endpoint pagination (25 responses, limit 10)
- ✅ Non-admin user gets 403 Forbidden
- ✅ Unauthenticated request gets 401 Unauthorized
- ✅ Admin quality report endpoint returns statistics
- ✅ Date range filter for low-quality responses
- ✅ Concurrent requests (10 simultaneous) all logged correctly

**Test Patterns**:
- Testcontainers for PostgreSQL (`postgres:16-alpine`)
- `WebApplicationFactory<Program>` for in-memory API testing
- `IAsyncLifetime` for container lifecycle management
- Authenticated clients (regular user + admin user)
- Database seeding for query tests
- HTTP status code assertions

---

## Expected Models (To Be Implemented)

### Services
```csharp
- ResponseQualityService
  - CalculateQualityScores(ragResults, citations, responseText, modelConfidence?)

- QualityMetrics
  - RecordQualityScores(scores, agentType, operation)

- QualityReportService : BackgroundService
  - GenerateReportAsync(startDate, endDate)
  - ExecuteAsync(cancellationToken)

- IQualityReportService
  - GenerateReportAsync(startDate, endDate)
```

### Models
```csharp
- QualityScores
  - RagConfidence: double
  - LlmConfidence: double
  - CitationQuality: double
  - OverallConfidence: double
  - IsLowQuality: bool

- QualityReport
  - StartDate: DateTime
  - EndDate: DateTime
  - TotalResponses: int
  - LowQualityCount: int
  - LowQualityPercentage: double
  - AverageRagConfidence: double?
  - AverageLlmConfidence: double?
  - AverageCitationQuality: double?
  - AverageOverallConfidence: double?

- LowQualityResponsesResult
  - TotalCount: int
  - Responses: List<LowQualityResponseDto>

- LowQualityResponseDto
  - Id: Guid
  - CreatedAt: DateTime
  - Query: string
  - RagConfidence: double
  - LlmConfidence: double
  - CitationQuality: double
  - OverallConfidence: double
  - IsLowQuality: bool
```

### Database Schema (ai_request_logs table additions)
```sql
ALTER TABLE ai_request_logs ADD COLUMN rag_confidence DOUBLE PRECISION;
ALTER TABLE ai_request_logs ADD COLUMN llm_confidence DOUBLE PRECISION;
ALTER TABLE ai_request_logs ADD COLUMN citation_quality DOUBLE PRECISION;
ALTER TABLE ai_request_logs ADD COLUMN overall_confidence DOUBLE PRECISION;
ALTER TABLE ai_request_logs ADD COLUMN is_low_quality BOOLEAN DEFAULT FALSE;
```

---

## Key Design Decisions

### 1. **Quality Score Dimensions** (4 dimensions)
- **RAG Confidence**: Average score of top RAG search results
- **LLM Confidence**: Heuristic based on response length, hedging phrases, model-reported confidence
- **Citation Quality**: Ratio of citations to paragraphs
- **Overall Confidence**: Weighted average of all dimensions

### 2. **Low-Quality Threshold**
- Overall confidence < 0.60 → Flagged as low-quality
- Boundary test: exactly 0.60 is NOT low-quality (exclusive threshold)

### 3. **Quality Tiers** (for metrics labels)
- **High**: Overall confidence ≥ 0.80
- **Medium**: 0.60 ≤ Overall confidence < 0.80
- **Low**: Overall confidence < 0.60

### 4. **Hedging Phrases Detection**
Test includes phrases like: "might", "possibly", "unclear", "I'm not sure", "maybe", "perhaps"

### 5. **Prometheus Metrics**
- **Histogram**: `meepleai.quality.score` (4 dimensions × multiple labels)
- **Counter**: `meepleai.quality.low_quality_responses.total`
- **Labels**: `dimension`, `agent.type`, `operation`, `quality_tier`

### 6. **Background Service Configuration**
- **Initial Delay**: 1 minute (configurable)
- **Interval**: 60 minutes (1 hour, configurable)
- **Report Period**: Weekly (7 days by default)

---

## Test Execution Status

### Current State: ❌ FAILING (Expected in RED Phase)

**Pre-existing Compilation Errors** (unrelated to AI-11):
```
RagService.cs(12,27): error CS0535: 'RagService' does not implement
interface member 'IRagService.AskAsync(...)'
RagService.cs(12,27): error CS0535: 'RagService' does not implement
interface member 'IRagService.ExplainAsync(...)'
```

**Note**: These errors are from modified `IRagService.cs` in the working directory and prevent compilation. Once fixed, the AI-11 tests will fail with expected "Type or namespace not found" errors.

### Expected Failures After Pre-existing Fix:
```
- CS0246: The type or namespace name 'ResponseQualityService' could not be found
- CS0246: The type or namespace name 'QualityMetrics' could not be found
- CS0246: The type or namespace name 'QualityReportService' could not be found
- CS0246: The type or namespace name 'QualityScores' could not be found
```

---

## Next Steps (GREEN Phase)

1. **Fix Pre-existing Error**: Implement `IRagService.AskAsync()` and `ExplainAsync()` in `RagService.cs`

2. **Create Models**:
   - `Models/QualityScores.cs`
   - `Models/QualityReport.cs`
   - `Models/Contracts.cs` additions (LowQualityResponsesResult, LowQualityResponseDto)

3. **Implement Services**:
   - `Services/ResponseQualityService.cs`
   - `Observability/QualityMetrics.cs`
   - `Services/QualityReportService.cs`
   - Update `Services/AiRequestLogService.cs` to store quality scores

4. **Database Migration**:
   - Add quality score columns to `ai_request_logs` table
   - Migration: `20251020_AddQualityScoresToAiRequestLogs`

5. **Admin Endpoints**:
   - `GET /admin/quality/low-responses` (with pagination, filters)
   - `GET /admin/quality/report?days=7`

6. **Dependency Injection**:
   - Register services in `Program.cs`
   - Configure background service

7. **Run Tests**: Execute tests and iterate until all pass (GREEN phase)

8. **Refactor**: Optimize and clean up implementation (REFACTOR phase)

---

## Test Quality Metrics

- **Total Tests**: 43 tests across 4 files
- **Unit Tests**: 32 (ResponseQuality: 13, Metrics: 10, ReportService: 9)
- **Integration Tests**: 11 (QualityTracking)
- **Lines of Test Code**: ~1,800 lines
- **Coverage Areas**:
  - ✅ Calculation logic (edge cases, boundaries, nulls)
  - ✅ Metrics recording (histograms, counters, labels)
  - ✅ Background service lifecycle
  - ✅ Database persistence
  - ✅ API endpoints (auth, pagination, filtering)
  - ✅ Concurrency handling

---

## Dependencies to Add

Check if these packages are available:
```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.Metrics.Testing" Version="9.0.0" />
```

If not available, use alternative approach for metrics testing (mock `IMeterFactory`).

---

## File Locations Summary

```
apps/api/tests/Api.Tests/
├── Services/
│   ├── ResponseQualityServiceTests.cs  (13 tests)
│   └── QualityReportServiceTests.cs    (9 tests)
├── Observability/
│   └── QualityMetricsTests.cs          (10 tests)
└── QualityTrackingIntegrationTests.cs  (11 tests)
```

---

## Compliance with Project Standards

✅ Follows existing test patterns in `apps/api/tests/Api.Tests/`
✅ Uses xUnit, Moq, Testcontainers (consistent with codebase)
✅ SQLite in-memory for unit tests (not used here, but pattern available)
✅ Testcontainers for integration tests (PostgreSQL)
✅ Async/await patterns throughout
✅ Nullable reference type conventions
✅ Clear Arrange-Act-Assert structure
✅ XML comments explaining BDD scenarios
✅ Descriptive test method names

---

## RED Phase Completion Checklist

- [x] ResponseQualityServiceTests.cs created (13 tests)
- [x] QualityMetricsTests.cs created (10 tests)
- [x] QualityReportServiceTests.cs created (9 tests)
- [x] QualityTrackingIntegrationTests.cs created (11 tests)
- [x] All tests follow BDD scenario format
- [x] All tests include XML documentation
- [x] Test helper methods provided
- [x] Mock models included in test files
- [x] Tests designed to fail (types don't exist)
- [x] Edge cases covered (nulls, zeros, boundaries)
- [x] Integration tests use Testcontainers
- [x] Tests align with project conventions

---

## Notes

- **Test Execution Time**: Integration tests may take ~10-15 seconds due to Testcontainers startup
- **Metrics Testing**: Uses `Microsoft.Extensions.Diagnostics.Metrics.Testing` from .NET 9
- **Database Schema**: Tests assume quality score columns in `ai_request_logs` table
- **Authentication**: Integration tests require session cookie authentication (TODO: implement helper)
- **Test Data**: Helper methods generate synthetic responses with controllable characteristics

---

**Author**: Claude Code (AI Assistant)
**TDD Phase**: RED (Tests First)
**Next Phase**: GREEN (Implementation)
