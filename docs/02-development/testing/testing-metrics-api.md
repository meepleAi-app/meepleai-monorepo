# Testing Metrics API Implementation

**Issue**: #2139
**Status**: Backend Complete, Frontend Pending
**Date**: 2025-12-12

## Overview

Complete backend implementation of Testing Metrics API for the MeepleAI Testing Dashboard. Provides real-time metrics from Lighthouse, Playwright, and Prometheus.

## Architecture

### Bounded Context
**Administration** - `apps/api/src/Api/BoundedContexts/Administration/`

### CQRS Pattern
Following DDD/CQRS architecture with MediatR orchestration.

## Implementation

### 1. Domain Models (ValueObjects)

#### AccessibilityMetrics
**Path**: `Domain/ValueObjects/AccessibilityMetrics.cs`

```csharp
public sealed class AccessibilityMetrics : ValueObject
{
    public decimal LighthouseScore { get; }      // 0-100
    public int AxeViolations { get; }            // Count
    public IReadOnlyList<string> WcagLevels { get; } // A, AA, AAA
    public DateTime LastRunAt { get; }
    public string Status { get; }                // pass, warning, fail
    public bool MeetsQualityStandards { get; }   // Score>=90, Violations=0, WCAG AA
}
```

#### PerformanceMetrics
**Path**: `Domain/ValueObjects/PerformanceMetrics.cs`

```csharp
public sealed class PerformanceMetrics : ValueObject
{
    // Core Web Vitals
    public decimal Lcp { get; }                  // Largest Contentful Paint (ms)
    public decimal Fid { get; }                  // First Input Delay (ms)
    public decimal Cls { get; }                  // Cumulative Layout Shift

    // Additional metrics
    public decimal Fcp { get; }                  // First Contentful Paint (ms)
    public decimal Tti { get; }                  // Time to Interactive (ms)
    public decimal Tbt { get; }                  // Total Blocking Time (ms)
    public decimal SpeedIndex { get; }
    public decimal PerformanceScore { get; }     // 0-100
    public string BudgetStatus { get; }          // pass, warning, fail
    public bool MeetsCoreWebVitals { get; }      // LCP<=2500, FID<=100, CLS<=0.1
}
```

#### E2EMetrics
**Path**: `Domain/ValueObjects/E2EMetrics.cs`

```csharp
public sealed class E2EMetrics : ValueObject
{
    public decimal Coverage { get; }             // Percentage
    public decimal PassRate { get; }             // Percentage
    public decimal FlakyRate { get; }            // Percentage
    public decimal ExecutionTime { get; }        // Average ms
    public int TotalTests { get; }
    public int PassedTests { get; }
    public int FailedTests { get; }
    public int SkippedTests { get; }
    public int FlakyTests { get; }
    public string Status { get; }                // pass, warning, fail
    public bool MeetsQualityStandards { get; }   // Coverage>=90%, Pass>=95%, Flaky<=5%
}
```

### 2. Application Layer

#### Queries
**Path**: `Application/Queries/Testing/`

- `GetAccessibilityMetricsQuery` + `AccessibilityMetricsDto`
- `GetPerformanceMetricsQuery` + `PerformanceMetricsDto`
- `GetE2EMetricsQuery` + `E2EMetricsDto`

All queries implement `IQuery<TDto>` (not `IRequest<TDto>`) for CQRS compliance.

#### Handlers
**Path**: `Application/Queries/Testing/`

- `GetAccessibilityMetricsQueryHandler`
- `GetPerformanceMetricsQueryHandler`
- `GetE2EMetricsQueryHandler`

All implement `IQueryHandler<TQuery, TDto>` with structured logging.

**Default Behavior**: Return "no-data" status when reports are missing (non-blocking).

#### Service Interfaces
**Path**: `Application/Interfaces/`

- `IPrometheusClientService` - Prometheus API queries
- `ILighthouseReportParserService` - Parse Lighthouse JSON reports
- `IPlaywrightReportParserService` - Parse Playwright JSON reports

### 3. Infrastructure Layer

#### Services
**Path**: `Infrastructure/Services/`

##### PrometheusClientService
- Queries Prometheus HTTP API at `http://localhost:9090/api/v1/query`
- Supports PromQL queries
- Includes health check endpoint
- Polly retry + circuit breaker policies
- Configurable via `appsettings.json` → `Prometheus:Url`

##### LighthouseReportParserService
- Parses JSON reports from `apps/web/.lighthouseci/`
- Reads manifest.json and report files
- Extracts:
  - Accessibility: `categories.accessibility.score`, axe violations
  - Performance: Core Web Vitals, performance score, budget status
- Configurable via `appsettings.json` → `Lighthouse:ReportDirectory`

##### PlaywrightReportParserService
- Parses JSON reports from `apps/web/playwright-report/`
- Extracts:
  - Test statistics: total, passed, failed, skipped, flaky
  - Execution time
  - Pass rate, flaky rate
  - Coverage (heuristic: based on test count)
- Configurable via `appsettings.json` → `Playwright:ReportDirectory`

#### Dependency Injection
**Path**: `Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs`

```csharp
// Testing metrics services
services.AddScoped<IPrometheusClientService, PrometheusClientService>();
services.AddScoped<ILighthouseReportParserService, LighthouseReportParserService>();
services.AddScoped<IPlaywrightReportParserService, PlaywrightReportParserService>();

// HttpClient for Prometheus with Polly policies
services.AddHttpClient<PrometheusClientService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
```

### 4. HTTP Endpoints

#### Routing
**Path**: `Routing/TestingMetricsEndpoints.cs`

All endpoints require admin session (`RequireAdminSession()`).

##### GET /api/v1/admin/testing/accessibility
Returns Lighthouse accessibility metrics:
- Lighthouse score
- Axe violations count
- WCAG levels passed
- Status and quality standards check

##### GET /api/v1/admin/testing/performance
Returns performance metrics:
- Core Web Vitals (LCP, FID, CLS)
- Additional metrics (FCP, TTI, TBT, Speed Index)
- Performance score
- Budget status

##### GET /api/v1/admin/testing/e2e
Returns Playwright E2E metrics:
- Test statistics (total, passed, failed, skipped, flaky)
- Coverage, pass rate, flaky rate
- Execution time
- Status and quality standards check

#### Endpoint Registration
**File**: `Program.cs` (line 371)

```csharp
v1Api.MapTestingMetricsEndpoints();    // Issue #2139: Testing metrics API
```

## Configuration

### appsettings.json

```json
{
  "Prometheus": {
    "Url": "http://localhost:9090"
  },
  "Lighthouse": {
    "ReportDirectory": "../../../apps/web/.lighthouseci"
  },
  "Playwright": {
    "ReportDirectory": "../../../apps/web/playwright-report"
  }
}
```

## Data Sources

### 1. Lighthouse Reports
**Location**: `apps/web/.lighthouseci/`
**Format**: JSON
**Content**:
- `manifest.json` - Index of reports
- `lhr-*.json` - Individual Lighthouse HTML Reports

**Key Paths**:
- Accessibility: `categories.accessibility.score`
- Performance: `categories.performance.score`
- Audits: `audits.{audit-name}.numericValue`

### 2. Playwright Reports
**Location**: `apps/web/playwright-report/`
**Format**: JSON
**Content**:
- Test results JSON with statistics

**Key Paths**:
- `stats.total` - Total tests
- `stats.expected` - Passed tests
- `stats.unexpected` - Failed tests
- `stats.skipped` - Skipped tests
- `stats.flaky` - Flaky tests
- `stats.duration` - Total duration (ms)

### 3. Prometheus Metrics
**Location**: `http://localhost:9090/api/v1/query`
**Format**: PromQL queries
**Examples**:
- `rate(http_requests_total[5m])`
- `meepleai_api_request_duration_seconds`

## Quality Standards

### Accessibility
- **Pass**: Lighthouse >= 90, Violations = 0, WCAG AA
- **Warning**: Lighthouse >= 75
- **Fail**: Lighthouse < 75

### Performance
- **Pass**: LCP <= 2500ms, FID <= 100ms, CLS <= 0.1, Score >= 90
- **Warning**: LCP <= 4000ms, FID <= 300ms, CLS <= 0.25
- **Fail**: Below warning thresholds

### E2E Tests
- **Pass**: Coverage >= 90%, Pass >= 95%, Flaky <= 5%
- **Warning**: Coverage >= 80%, Pass >= 80%, Flaky <= 10%
- **Fail**: Below warning thresholds

## Error Handling

### Missing Reports
- **Behavior**: Return default metrics with "no-data" status
- **Rationale**: Non-blocking for dashboard, allows graceful degradation
- **Logging**: Warning level

### Prometheus Unavailable
- **Behavior**: Return null from queries
- **Retry**: 3 attempts with exponential backoff
- **Circuit Breaker**: Opens after 5 failures, 30s break

### File I/O Errors
- **Behavior**: Catch exceptions, log errors, return null
- **Logging**: Error level with exception details

## Testing Strategy

### Unit Tests
**Status**: Pending (Issue #2139 continuation)

**Scope**:
- Handler logic with mocked services
- ValueObject validation
- DTO mapping

**Location**: `apps/api/tests/Api.Tests/Unit/Administration/Queries/Testing/`

### Integration Tests
**Status**: Pending (Issue #2139 continuation)

**Scope**:
- End-to-end endpoint tests
- Service integration with mock reports
- Error scenarios

**Location**: `apps/api/tests/Api.Tests/Integration/Administration/Testing/`

### Test Files Needed
```
- GetAccessibilityMetricsQueryHandlerTests.cs
- GetPerformanceMetricsQueryHandlerTests.cs
- GetE2EMetricsQueryHandlerTests.cs
- LighthouseReportParserServiceTests.cs
- PlaywrightReportParserServiceTests.cs
- PrometheusClientServiceTests.cs
- TestingMetricsEndpointsTests.cs (integration)
```

## Frontend Integration

### API Client
**Path**: `apps/web/lib/api.ts`

```typescript
// Add to API client
export async function getAccessibilityMetrics() {
  return fetch('/api/v1/admin/testing/accessibility', {
    credentials: 'include'
  }).then(r => r.json());
}

export async function getPerformanceMetrics() {
  return fetch('/api/v1/admin/testing/performance', {
    credentials: 'include'
  }).then(r => r.json());
}

export async function getE2EMetrics() {
  return fetch('/api/v1/admin/testing/e2e', {
    credentials: 'include'
  }).then(r => r.json());
}
```

### Dashboard Component
**Path**: `apps/web/pages/admin/testing-dashboard.tsx`

**Features**:
- Real-time metric display
- Visual indicators (✅ pass, ⚠️ warning, ❌ fail)
- Auto-refresh (configurable interval)
- Quality standards badges
- Trend charts (optional)

## Monitoring

### Logging
- **Level**: Information for successful queries
- **Level**: Warning for missing data
- **Level**: Error for failures
- **Format**: Structured logging with context
- **Fields**: UserId, MetricType, Status, Values

### Metrics (Prometheus)
- `meepleai_testing_metrics_requests_total{endpoint}`
- `meepleai_testing_metrics_errors_total{endpoint,error_type}`
- `meepleai_testing_metrics_duration_seconds{endpoint}`

## Performance

### Caching Strategy
- **Reports**: File system cache (built-in OS)
- **Prometheus**: No caching (real-time)
- **Future**: Add HybridCache with 5-minute TTL

### Response Time
- **Target**: < 200ms per endpoint
- **Lighthouse/Playwright**: File I/O ~10-50ms
- **Prometheus**: HTTP ~50-100ms

## Security

### Authentication
- Admin session required (cookie-based)
- Role: Admin only
- IP logging for audit

### Data Protection
- No sensitive data in metrics
- Read-only operations
- No user input (query parameters)

## Future Enhancements

### Phase 2
1. Add unit and integration tests
2. Implement caching with HybridCache
3. Add Prometheus metric tracking
4. Create Grafana dashboard

### Phase 3
1. Historical trend data
2. Alerting thresholds
3. Comparison with baseline
4. Automated quality reports

### Phase 4
1. Multi-environment support
2. Custom quality thresholds
3. Export to CSV/JSON
4. Scheduled reports

## Files Created

### Domain
- `BoundedContexts/Administration/Domain/ValueObjects/AccessibilityMetrics.cs`
- `BoundedContexts/Administration/Domain/ValueObjects/PerformanceMetrics.cs`
- `BoundedContexts/Administration/Domain/ValueObjects/E2EMetrics.cs`

### Application
- `BoundedContexts/Administration/Application/Queries/Testing/GetAccessibilityMetricsQuery.cs`
- `BoundedContexts/Administration/Application/Queries/Testing/GetAccessibilityMetricsQueryHandler.cs`
- `BoundedContexts/Administration/Application/Queries/Testing/GetPerformanceMetricsQuery.cs`
- `BoundedContexts/Administration/Application/Queries/Testing/GetPerformanceMetricsQueryHandler.cs`
- `BoundedContexts/Administration/Application/Queries/Testing/GetE2EMetricsQuery.cs`
- `BoundedContexts/Administration/Application/Queries/Testing/GetE2EMetricsQueryHandler.cs`
- `BoundedContexts/Administration/Application/Interfaces/IPrometheusClientService.cs`
- `BoundedContexts/Administration/Application/Interfaces/ILighthouseReportParserService.cs`
- `BoundedContexts/Administration/Application/Interfaces/IPlaywrightReportParserService.cs`

### Infrastructure
- `BoundedContexts/Administration/Infrastructure/Services/PrometheusClientService.cs`
- `BoundedContexts/Administration/Infrastructure/Services/LighthouseReportParserService.cs`
- `BoundedContexts/Administration/Infrastructure/Services/PlaywrightReportParserService.cs`

### Routing
- `Routing/TestingMetricsEndpoints.cs`

### Documentation
- `docs/02-development/testing/testing-metrics-api.md`

## Build Status

**Compilation**: ✅ Success (0 errors, 1617 warnings - existing codebase warnings)

**Tests**: ⏳ Pending (create in continuation)

**Deployment**: Ready for integration testing

## Next Steps

1. **Testing** (Priority: High)
   - Create unit tests for handlers
   - Create integration tests for endpoints
   - Mock service implementations

2. **Frontend Integration** (Priority: High)
   - Create Testing Dashboard page
   - Implement API client calls
   - Add visual components

3. **Documentation** (Priority: Medium)
   - Update API specification
   - Add OpenAPI annotations
   - Create user guide

4. **Performance** (Priority: Low)
   - Add caching layer
   - Optimize file I/O
   - Load testing

## References

- **Issue**: #2139
- **Architecture**: DDD/CQRS with MediatR
- **Pattern**: Repository, CQRS, ValueObject
- **Testing**: xUnit, Moq, Testcontainers
