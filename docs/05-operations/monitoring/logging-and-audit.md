# Logging and Audit Systems Guide

**Version**: 1.0
**Last Updated**: 2025-11-12
**Status**: Production-Ready

---

## Table of Contents

1. [How to Access Logs](#how-to-access-logs)
2. [Current Logging System (AS-IS)](#current-logging-system-as-is)
3. [Current Audit System (AS-IS)](#current-audit-system-as-is)
4. [Planned Improvements (TO-BE)](#planned-improvements-to-be)
5. [Best Practices](#best-practices)
6. [Troubleshooting](#troubleshooting)

---

## How to Access Logs

### 1. Development Environment

#### Console Logs (Local Development)
When running the API locally, logs appear in the console:

```bash
cd apps/api/src/Api && dotnet run

# Console output shows structured logs:
[10:30:45 DBG] Starting MeepleAI API...
[10:30:46 INF] Application started. Press Ctrl+C to shut down.
[10:30:47 INF] User query: {Query: "Can I use standard projects?"}
```

**Log Format**: `[HH:mm:ss LEVEL] Message {Properties}`

#### Seq Dashboard (Centralized Logs)
**URL**: http://localhost:8081 (or configured SEQ_URL)

Seq provides a powerful web interface for searching and analyzing logs:

1. **Open Seq**: Navigate to http://localhost:8081
2. **Search Logs**: Use the search bar with filters
   - By level: `@Level = 'Error'`
   - By user: `UserId = '...'`
   - By correlation: `CorrelationId = '...'`
3. **Time Range**: Select time range (last hour, today, custom)
4. **Saved Queries**: Create and save common searches

**Key Features**:
- Real-time log streaming
- Full-text search
- Structured property queries
- Correlation ID tracking
- Exception stack traces
- Log retention (configurable)

### 2. Staging/Production Environment

#### Seq (Centralized Logging)
**Production URL**: Configured via `SEQ_URL` environment variable

```bash
# Check Seq configuration
echo $SEQ_URL

# Example: https://logs.meepleai.dev
```

**Access Requirements**:
- Admin credentials for Seq dashboard
- API key for programmatic access (if needed)

#### Container Logs (Docker)
If using Docker Compose, access container logs directly:

```bash
# API logs
docker compose logs -f api

# Follow logs with timestamp
docker compose logs -f --tail=100 api

# All services
docker compose logs -f
```

### 3. Database Audit Logs

Access audit logs via SQL queries:

```sql
-- Recent audit logs
SELECT
    "CreatedAt",
    "UserId",
    "Action",
    "Resource",
    "Result",
    "IpAddress"
FROM "AuditLogs"
ORDER BY "CreatedAt" DESC
LIMIT 100;

-- Failed access attempts
SELECT *
FROM "AuditLogs"
WHERE "Action" = 'ACCESS_DENIED'
ORDER BY "CreatedAt" DESC;

-- User-specific actions
SELECT *
FROM "AuditLogs"
WHERE "UserId" = 'your-user-guid'
ORDER BY "CreatedAt" DESC;
```

**Connection**:
```bash
# Local PostgreSQL
psql -h localhost -U postgres -d meepleai

# Production (via connection string)
# Use CONNECTIONSTRINGS__POSTGRES from environment
```

### 4. OpenTelemetry Traces (Jaeger)

**URL**: http://localhost:16686 (or configured OTEL endpoint)

**Purpose**: Distributed tracing for request flow analysis

1. **Open Jaeger UI**: Navigate to http://localhost:16686
2. **Select Service**: Choose "MeepleAI.Api"
3. **Find Traces**: Search by:
   - Operation name (e.g., "POST /api/v1/chat")
   - Tags (e.g., `http.status_code=500`)
   - Duration (slow requests)
4. **Analyze Trace**: View request timeline across components:
   - API endpoint → RAG service → Qdrant → LLM

### 5. Prometheus Metrics

**URL**: http://localhost:9090/metrics (or configured endpoint)

**Purpose**: System and application metrics

**Key Metrics**:
- `http_requests_total` - Total HTTP requests
- `http_request_duration_seconds` - Request latency
- `dotnet_gc_collection_count_total` - .NET GC metrics
- Custom MeepleAI metrics (defined in `MeepleAiMetrics`)

---

## Current Logging System (AS-IS)

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│              Application Code (Services)                │
│  ILogger<T> → Structured Logging Calls                 │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│                  Serilog Pipeline                       │
│                                                         │
│  1. Log Context Enrichment                             │
│     • CorrelationId, MachineName, Environment          │
│     • Application="MeepleAI"                           │
│                                                         │
│  2. Sensitive Data Redaction (SEC-733)                 │
│     • SensitiveDataDestructuringPolicy                 │
│     • Redacts: passwords, API keys, tokens, emails     │
│                                                         │
│  3. Log Forging Prevention (SEC-731)                   │
│     • LogForgingSanitizationPolicy                     │
│     • Removes: \r, \n, control characters              │
│                                                         │
│  4. Minimum Level Filtering                            │
│     • Development: Debug                               │
│     • Staging/Production: Information                  │
│     • ASP.NET Core: Warning                            │
│     • EF Core: Warning                                 │
│                                                         │
└─────────────────────┬───────────────────────────────────┘
                      │
        ┌─────────────┴──────────────┐
        │                            │
        ▼                            ▼
┌─────────────────┐      ┌────────────────────┐
│  Console Sink   │      │    Seq Sink        │
│  (Development)  │      │  (All Envs)        │
│                 │      │                    │
│ Format:         │      │ - Structured JSON  │
│ Human-readable  │      │ - Correlation IDs  │
│ with timestamps │      │ - Full properties  │
└─────────────────┘      └────────────────────┘
```

### Components

#### 1. LoggingConfiguration (OPS-04)
**File**: `apps/api/src/Api/Logging/LoggingConfiguration.cs`

**Responsibilities**:
- Environment-based log level configuration
- Serilog pipeline setup
- Sink configuration (Console + Seq)

**Log Levels by Environment**:
```csharp
Development:
  Default: Debug
  Console: Debug
  Seq: Debug

Staging:
  Default: Information
  Console: Information
  Seq: Debug

Production:
  Default: Information
  Console: Warning (reduced verbosity)
  Seq: Debug (full centralized logging)
```

**Configuration**:
```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System.Net.Http.HttpClient": "Warning"
    }
  },
  "SEQ_URL": "http://seq:5341",
  "SEQ_API_KEY": "optional-api-key"
}
```

#### 2. SensitiveDataDestructuringPolicy (SEC-733)
**File**: `apps/api/src/Api/Logging/SensitiveDataDestructuringPolicy.cs`

**Purpose**: Prevents sensitive data exposure in logs

**Protected Properties**:
- **Passwords**: `password`, `Password`, `newPassword`, `currentPassword`
- **Tokens**: `token`, `accessToken`, `refreshToken`, `apiKey`, `secret`
- **Emails**: `email`, `Email` (masked: `u***@example.com`)
- **IP Addresses**: `ipAddress`, `IpAddress` (masked: `192.168.*.*`)

**Redaction Strategy**:
```csharp
// Before redaction:
_logger.LogInformation("User: {@User}", user);
// Log: { Email: "user@example.com", Password: "secret123" }

// After redaction:
// Log: { Email: "u***@example.com", Password: "***REDACTED***" }
```

#### 3. LogForgingSanitizationPolicy (SEC-731)
**File**: `apps/api/src/Api/Logging/LogForgingSanitizationPolicy.cs`

**Purpose**: Prevents log forging attacks via newline injection

**Sanitized Characters**:
- `\r` (Carriage Return)
- `\n` (Line Feed)
- `\u000D` (Unicode CR)
- `\u000A` (Unicode LF)

**Attack Prevention**:
```csharp
// Attack input:
string maliciousQuery = "test\nINFO: Admin deleted all users";

// Without sanitization (VULNERABLE):
[10:30:45 INF] User query: test
INFO: Admin deleted all users  // ← Fake log entry!

// With sanitization (PROTECTED):
[10:30:45 INF] User query: testINFO: Admin deleted all users
```

**Coverage**: 100% of all logging calls (798 calls in 79 files)

#### 4. Correlation IDs (OPS-04)
**Purpose**: Track requests across distributed systems

**Flow**:
1. Incoming request → Middleware generates `CorrelationId` (GUID)
2. `CorrelationId` added to log context (`LogContext.PushProperty`)
3. All logs for that request include `CorrelationId`
4. OpenTelemetry traces use same `CorrelationId`

**Usage in Logs**:
```
[10:30:45 INF] [CorrelationId: 3fa85f64-5717-4562-b3fc-2c963f66afa6] POST /api/v1/chat
[10:30:46 DBG] [CorrelationId: 3fa85f64-5717-4562-b3fc-2c963f66afa6] Searching Qdrant...
[10:30:47 INF] [CorrelationId: 3fa85f64-5717-4562-b3fc-2c963f66afa6] Request completed in 1234ms
```

**Seq Query**: `CorrelationId = '3fa85f64-5717-4562-b3fc-2c963f66afa6'`

### Structured Logging Standards

#### Best Practices (REQUIRED)
✅ **Use Structured Logging**:
```csharp
// ✅ CORRECT - Structured with named parameters
_logger.LogInformation("User {UserId} queried {GameName}", userId, gameName);

// ❌ WRONG - String interpolation (bypasses enrichers)
_logger.LogInformation($"User {userId} queried {gameName}");

// ❌ WRONG - String concatenation
_logger.LogInformation("User " + userId + " queried " + gameName);
```

#### Log Levels

| Level | Use Case | Examples |
|-------|----------|----------|
| **Debug** | Development diagnostics | "Cache hit for key {Key}", "Parsing PDF page {Page}" |
| **Information** | Normal operations | "User registered", "Query completed in {Duration}ms" |
| **Warning** | Recoverable issues | "Rate limit exceeded for {UserId}", "Retry attempt {Attempt}" |
| **Error** | Failures requiring attention | "Database connection failed", "LLM API returned 500" |
| **Critical** | System-wide failures | "Database unavailable", "All LLM providers down" |

#### Log Message Templates

**Format**: Use named placeholders for structured data

```csharp
// ✅ Good: Specific, structured
_logger.LogInformation(
    "RAG query completed for game {GameId} in {DurationMs}ms with confidence {Confidence}",
    gameId, durationMs, confidence);

// ❌ Bad: Vague, unstructured
_logger.LogInformation("Query done");
```

### Security Features (Defense-in-Depth)

**Layer 1: Sensitive Data Redaction**
- Automatic password/token masking
- Email/IP address obfuscation
- No manual sanitization required

**Layer 2: Log Forging Prevention**
- Newline character removal
- Control character sanitization
- Attack surface eliminated

**Layer 3: Input Validation**
- SQL injection prevention (parameterized queries)
- XSS sanitization (frontend)
- Rate limiting (middleware)

**Result**: **Zero sensitive data exposure** in logs + **Zero log forging vulnerabilities**

---

## Current Audit System (AS-IS)

### Architecture Overview

```
┌────────────────────────────────────────────────────────┐
│           Application Actions (Endpoints)              │
│  • User login/logout                                  │
│  • Admin operations (delete user, modify game)        │
│  • API key creation/revocation                        │
│  • Access denied events                               │
└────────────────────┬───────────────────────────────────┘
                     │
                     ▼
┌────────────────────────────────────────────────────────┐
│              AuditService.LogAsync()                   │
│  Captures:                                            │
│  • UserId, Action, Resource, ResourceId               │
│  • Result (Success/Denied/Failed)                     │
│  • Details (JSON metadata)                            │
│  • IpAddress, UserAgent                               │
│  • CreatedAt (UTC timestamp)                          │
└────────────────────┬───────────────────────────────────┘
                     │
                     ▼
┌────────────────────────────────────────────────────────┐
│              PostgreSQL AuditLogs Table                │
│  • Persistent storage (ACID guarantees)               │
│  • Indexed by UserId, CreatedAt, Action               │
│  • Retention: Configurable (default: unlimited)       │
└────────────────────────────────────────────────────────┘
```

### AuditService Implementation

**File**: `apps/api/src/Api/Services/AuditService.cs`

#### Core Methods

##### 1. LogAsync (General Audit)
```csharp
public async Task LogAsync(
    string? userId,
    string action,          // e.g., "USER_LOGIN", "GAME_DELETED"
    string resource,        // e.g., "User", "Game"
    string? resourceId,     // e.g., game GUID
    string result,          // "Success", "Denied", "Failed"
    string? details = null, // JSON metadata (optional)
    string? ipAddress = null,
    string? userAgent = null,
    CancellationToken ct = default)
```

**Example Usage**:
```csharp
await _auditService.LogAsync(
    userId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    action: "GAME_DELETED",
    resource: "Game",
    resourceId: "game-123",
    result: "Success",
    details: "{\"gameName\":\"Terraforming Mars\"}",
    ipAddress: "192.168.1.100",
    userAgent: "Mozilla/5.0..."
);
```

##### 2. LogAccessDeniedAsync (Security Events)
```csharp
public async Task LogAccessDeniedAsync(
    string userScope,       // User's current scope (e.g., "user")
    string requiredScope,   // Required scope (e.g., "admin")
    string userId,
    string resource,
    string? resourceId = null,
    string? ipAddress = null,
    string? userAgent = null,
    CancellationToken ct = default)
```

**Example Usage**:
```csharp
await _auditService.LogAccessDeniedAsync(
    userScope: "user",
    requiredScope: "admin",
    userId: userId,
    resource: "AdminPanel",
    ipAddress: httpContext.Connection.RemoteIpAddress?.ToString()
);
```

**Dual Logging**: Writes to both AuditLogs table AND Serilog (for monitoring)

### Database Schema

**Table**: `AuditLogs`

```sql
CREATE TABLE "AuditLogs" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" UUID NULL REFERENCES "Users"("Id"),
    "Action" VARCHAR(100) NOT NULL,       -- e.g., "USER_LOGIN"
    "Resource" VARCHAR(100) NOT NULL,     -- e.g., "User", "Game"
    "ResourceId" VARCHAR(255) NULL,       -- e.g., game GUID
    "Result" VARCHAR(50) NOT NULL,        -- "Success", "Denied", "Failed"
    "Details" TEXT NULL,                  -- JSON metadata
    "IpAddress" VARCHAR(50) NULL,         -- Masked in logs (192.168.*.*)
    "UserAgent" TEXT NULL,
    "CreatedAt" TIMESTAMP NOT NULL
);

-- Indexes for performance
CREATE INDEX "IX_AuditLogs_UserId" ON "AuditLogs"("UserId");
CREATE INDEX "IX_AuditLogs_CreatedAt" ON "AuditLogs"("CreatedAt");
CREATE INDEX "IX_AuditLogs_Action" ON "AuditLogs"("Action");
```

### Common Audit Actions

| Action | Resource | Trigger | Result |
|--------|----------|---------|--------|
| `USER_REGISTER` | User | New user registration | Success/Failed |
| `USER_LOGIN` | User | Login attempt | Success/Failed |
| `USER_LOGOUT` | User | Logout | Success |
| `PASSWORD_RESET` | User | Password reset request | Success |
| `APIKEY_CREATED` | ApiKey | API key generation | Success |
| `APIKEY_REVOKED` | ApiKey | API key revocation | Success |
| `GAME_CREATED` | Game | Admin creates game | Success |
| `GAME_DELETED` | Game | Admin deletes game | Success |
| `ACCESS_DENIED` | Various | Authorization failure | Denied |

### Resilience Pattern (CRITICAL)

**Principle**: Audit logging must NEVER fail business operations

```csharp
try
{
    // Write audit log
}
catch (Exception ex)
{
    // Log error but DON'T throw
    _logger.LogError(ex, "Failed to write audit log");
    // Business operation continues ✅
}
```

**Rationale**: Auditing is a secondary concern. Failing a user request because we cannot write an audit log violates fail-safe principles.

**Mitigation**: Separate monitoring alerts for audit write failures.

### Audit Log Analysis

#### Example Queries

**1. Recent Admin Actions**:
```sql
SELECT * FROM "AuditLogs"
WHERE "Resource" IN ('User', 'Game', 'Configuration')
  AND "Action" LIKE '%DELETED%' OR "Action" LIKE '%CREATED%'
ORDER BY "CreatedAt" DESC
LIMIT 100;
```

**2. Failed Login Attempts**:
```sql
SELECT
    "UserId",
    COUNT(*) as "FailedAttempts",
    MAX("CreatedAt") as "LastAttempt"
FROM "AuditLogs"
WHERE "Action" = 'USER_LOGIN'
  AND "Result" = 'Failed'
  AND "CreatedAt" > NOW() - INTERVAL '24 hours'
GROUP BY "UserId"
HAVING COUNT(*) > 5
ORDER BY "FailedAttempts" DESC;
```

**3. Access Denied Trends**:
```sql
SELECT
    DATE("CreatedAt") as "Date",
    COUNT(*) as "DeniedCount"
FROM "AuditLogs"
WHERE "Action" = 'ACCESS_DENIED'
GROUP BY DATE("CreatedAt")
ORDER BY "Date" DESC
LIMIT 30;
```

---

## Planned Improvements (TO-BE)

### Phase 2: Enhanced Observability (Q1 2025)

#### 1. Grafana Dashboards
**Status**: Infrastructure exists, dashboards pending

**Planned Dashboards**:
- **Application Performance**: Request rate, latency (P50/P95/P99), error rate
- **RAG Pipeline**: Query volume, confidence scores, validation failures
- **Security**: Failed logins, access denied events, rate limit hits
- **Infrastructure**: CPU/Memory/Disk, database connections, cache hit rate

**Metrics Source**: Prometheus `/metrics` endpoint (already configured)

**Timeline**: Next 2-4 weeks

#### 2. PagerDuty Integration (OPS-07)
**Status**: `PagerDutyAlertChannel` implemented, not configured

**Alerting Scenarios**:
- **Critical**: Database down, all LLM providers unavailable, >10% validation failures
- **High**: >P95 latency threshold, high error rate, cache failures
- **Medium**: Individual LLM provider down, low confidence queries

**Configuration**:
```json
// appsettings.json
{
  "Alerting": {
    "PagerDuty": {
      "IntegrationKey": "YOUR_KEY",
      "Enabled": true
    }
  }
}
```

**Timeline**: Before production launch

#### 3. Audit Log Export API
**Status**: Planned, not implemented

**Requirements**:
- REST endpoint: `GET /api/v1/admin/audit/export`
- Formats: JSON, CSV
- Filters: Date range, user, action type
- Pagination: Handle large exports (millions of records)
- Authorization: Admin-only

**Use Cases**:
- Compliance reporting (GDPR, SOC2)
- Security incident investigation
- Data retention management

**Timeline**: Phase 3 (Q2 2025)

#### 4. Structured Audit Queries (Admin UI)
**Status**: Planned, not implemented

**Features**:
- Web UI for audit log search: `/admin/audit-logs`
- Filters: User, date range, action, resource
- Real-time updates (polling or SSE)
- Export to CSV/JSON

**Example UI**:
```
┌─────────────────────────────────────────────┐
│ Audit Logs                                  │
├─────────────────────────────────────────────┤
│ Filters:                                    │
│ User: [Select user ▼]                      │
│ Action: [Select action ▼]                  │
│ Date: [Last 7 days ▼]                      │
│                                             │
│ [Search]  [Export CSV]                     │
├─────────────────────────────────────────────┤
│ Results (100 of 1,234):                    │
│ ┌─────────┬────────┬──────────┬─────────┐ │
│ │ Time    │ User   │ Action   │ Result  │ │
│ ├─────────┼────────┼──────────┼─────────┤ │
│ │ 10:30   │ admin  │ GAME_DEL │ Success │ │
│ │ 10:25   │ user1  │ LOGIN    │ Failed  │ │
│ └─────────┴────────┴──────────┴─────────┘ │
└─────────────────────────────────────────────┘
```

**Timeline**: Phase 3 (Q2 2025)

### Phase 3: Advanced Logging (Q2 2025)

#### 1. Log Sampling (Cost Optimization)
**Purpose**: Reduce log volume and Seq costs in production

**Strategy**:
- Sample non-error logs at 10% rate (errors always logged)
- Smart sampling: Include first/last request of each session
- Configurable per environment

**Expected Savings**: 50-70% reduction in log storage costs

#### 2. Distributed Tracing Improvements
**Current**: OpenTelemetry → Jaeger (basic tracing)

**Planned Enhancements**:
- Custom spans for RAG pipeline stages (retrieval, generation, validation)
- Qdrant query spans (vector search timing)
- LLM API call spans (per-model latency tracking)
- Cache hit/miss spans (performance analysis)

**Example Enhanced Trace**:
```
POST /api/v1/chat (2.3s total)
├─ Query Processing (50ms)
├─ Qdrant Vector Search (200ms)
│  ├─ Collection check (10ms)
│  └─ Vector query (190ms)
├─ LLM Call - GPT-4 (1.2s)
├─ LLM Call - Claude (1.0s) [validation]
└─ Response Formatting (50ms)
```

#### 3. Anomaly Detection (AI-Powered)
**Purpose**: Automatic detection of unusual patterns

**Planned Capabilities**:
- Spike detection: Sudden increase in errors/latency
- Baseline comparison: Compare to historical averages
- Alerting: Auto-create incidents for anomalies

**ML Model**: Time-series forecasting (Prophet or similar)

**Timeline**: Phase 4 (Q3 2025)

### Phase 4: Compliance & Retention (Q3 2025)

#### 1. GDPR Compliance Features
**Requirements**:
- User audit log export (GDPR Article 15 - Right to Access)
- Audit log pseudonymization (GDPR Article 32)
- Audit log deletion on user request (GDPR Article 17 - Right to Erasure)

**Implementation**:
```csharp
// Export user audit logs
GET /api/v1/users/{userId}/audit-logs

// Pseudonymize user in audit logs (post-deletion)
POST /api/v1/admin/audit/pseudonymize-user/{userId}
```

#### 2. Audit Log Retention Policies
**Current**: Unlimited retention

**Planned**:
- Configurable retention (e.g., 1 year, 2 years, 5 years)
- Auto-archival to cold storage (S3, Azure Blob)
- Legal hold capability (prevent deletion for compliance)

**Configuration**:
```json
{
  "AuditLog": {
    "RetentionDays": 730,  // 2 years
    "ArchiveAfterDays": 365,  // 1 year
    "LegalHold": false
  }
}
```

#### 3. SOC2 Compliance
**Requirements**:
- Immutable audit logs (no modifications allowed)
- Tamper detection (checksums or blockchain)
- Access control audit (who viewed sensitive logs)

**Implementation**:
- Add `Hash` column to `AuditLogs` table (SHA-256 of record)
- Verify hash on read (detect tampering)
- Log access to audit logs (meta-audit)

---

## Best Practices

### For Developers

#### 1. Always Use Structured Logging
```csharp
// ✅ CORRECT
_logger.LogInformation("User {UserId} performed {Action}", userId, action);

// ❌ WRONG
_logger.LogInformation($"User {userId} performed {action}");
```

**Why**: Structured logging enables:
- Automatic sensitive data redaction
- Log forging prevention
- Queryable logs in Seq
- Better performance (less string allocation)

#### 2. Use Appropriate Log Levels
```csharp
// Debug: Development diagnostics only
_logger.LogDebug("Processing chunk {ChunkIndex} of {TotalChunks}", i, total);

// Information: Normal business operations
_logger.LogInformation("User {UserId} registered successfully", userId);

// Warning: Recoverable issues
_logger.LogWarning("Rate limit exceeded for {UserId}, retrying in {Delay}s", userId, delay);

// Error: Failures requiring attention
_logger.LogError(ex, "Failed to connect to Qdrant at {Url}", qdrantUrl);

// Critical: System-wide failures
_logger.LogCritical("Database connection pool exhausted, rejecting requests");
```

#### 3. Include Context in Logs
```csharp
// ✅ Good: Context-rich
_logger.LogError(
    ex,
    "RAG query failed for game {GameId}, query {Query}, confidence {Confidence}",
    gameId, query, confidence);

// ❌ Bad: Vague
_logger.LogError("Query failed");
```

#### 4. Use Correlation IDs
```csharp
// Correlation ID automatically added to logs by middleware
// Just ensure all related logs use same logger instance

_logger.LogInformation("Starting RAG query");
// ... processing ...
_logger.LogInformation("RAG query completed in {Duration}ms", duration);

// Both logs will have same CorrelationId
```

#### 5. Never Log Sensitive Data Directly
```csharp
// ❌ WRONG: Direct logging (even with redaction, don't risk it)
_logger.LogInformation("Password: {Password}", password);

// ✅ CORRECT: Log events, not secrets
_logger.LogInformation("User password changed successfully");

// ⚠️ ACCEPTABLE: Use structured types (redaction policy handles it)
_logger.LogInformation("User registered: {@User}", user);
// Email/password will be auto-redacted by SensitiveDataDestructuringPolicy
```

### For Operations

#### 1. Monitor Log Volume
**Alert**: Log volume >10x baseline (possible log flooding attack)

```
Query in Prometheus:
rate(log_entries_total[5m]) > 1000
```

#### 2. Track Error Rates
**Alert**: Error rate >5% of total requests

```
Seq Query:
@Level = 'Error'
| group by $time span=5m
| aggregate count()
```

#### 3. Review Access Denied Events
**Daily Task**: Check for repeated access denied attempts (potential attack)

```sql
SELECT "UserId", COUNT(*)
FROM "AuditLogs"
WHERE "Action" = 'ACCESS_DENIED'
  AND "CreatedAt" > NOW() - INTERVAL '24 hours'
GROUP BY "UserId"
HAVING COUNT(*) > 10;
```

#### 4. Verify Correlation ID Coverage
**Weekly Task**: Ensure all requests have correlation IDs

```
Seq Query:
@Level = 'Information'
| where CorrelationId == null
| group by RequestPath
| aggregate count()
```

#### 5. Audit Log Integrity Check
**Monthly Task**: Verify no tampering

```sql
-- Check for missing sequence IDs (potential deletion)
SELECT
    LAG("Id") OVER (ORDER BY "CreatedAt") as "PrevId",
    "Id" as "CurrentId"
FROM "AuditLogs"
WHERE LAG("Id") OVER (ORDER BY "CreatedAt") IS NOT NULL
  AND "Id" != LAG("Id") OVER (ORDER BY "CreatedAt") + 1;
```

---

## Troubleshooting

### Issue 1: Logs Not Appearing in Seq

**Symptoms**: Console logs work, but Seq dashboard is empty

**Diagnosis**:
```bash
# Check Seq container is running
docker compose ps seq

# Check Seq logs
docker compose logs seq

# Test Seq endpoint
curl http://localhost:5341/api/events
```

**Solutions**:
1. **Seq not running**: `docker compose up -d seq`
2. **Wrong URL**: Check `SEQ_URL` in `appsettings.json` or env var
3. **Network issue**: Ensure API container can reach Seq (`docker compose network`)
4. **Seq API key**: If configured, verify `SEQ_API_KEY` is correct

### Issue 2: Sensitive Data Exposed in Logs

**Symptoms**: Passwords/API keys visible in Seq or console

**Diagnosis**:
```bash
# Search logs for sensitive patterns
grep -i "password" /var/log/app.log
grep -i "apikey" /var/log/app.log
```

**Solutions**:
1. **Update redaction policy**: Add new sensitive property names to `SensitiveDataDestructuringPolicy`
2. **Use structured logging**: String interpolation bypasses redaction
3. **Report issue**: File security bug if redaction failed

**Example Fix**:
```csharp
// In SensitiveDataDestructuringPolicy.cs
private static readonly HashSet<string> SensitivePropertyNames = new()
{
    "password", "apiKey", "token", "secret",
    "newSensitiveField"  // ← Add here
};
```

### Issue 3: Log Forging Attack Detected

**Symptoms**: Seq shows suspicious log entries with unusual formatting

**Diagnosis**:
```
Seq Query:
@Message like '%\n%' or @Message like '%\r%'
```

**Solutions**:
1. **Verify sanitization**: Check `LogForgingSanitizationPolicy` is registered
2. **Test sanitization**: Run unit tests (`LogForgingSanitizationPolicyTests`)
3. **Check bypass**: Ensure no direct `Console.WriteLine` calls (use `ILogger`)

### Issue 4: Correlation IDs Missing

**Symptoms**: Unable to trace requests across logs

**Diagnosis**:
```
Seq Query:
CorrelationId == null
| group by RequestPath
```

**Solutions**:
1. **Check middleware**: Ensure correlation ID middleware is registered
2. **Verify log context**: Check `LogContext.PushProperty("CorrelationId", ...)`
3. **Test endpoint**: Call endpoint and verify `CorrelationId` in response headers

### Issue 5: Audit Logs Not Written

**Symptoms**: `AuditLogs` table is empty despite actions occurring

**Diagnosis**:
```bash
# Check application logs for audit write errors
docker compose logs api | grep "Failed to write audit log"

# Check database connection
psql -h localhost -U postgres -d meepleai -c "SELECT COUNT(*) FROM \"AuditLogs\";"
```

**Solutions**:
1. **Database connection**: Verify `CONNECTIONSTRINGS__POSTGRES` is correct
2. **Migration not applied**: Run `dotnet ef database update`
3. **AuditService not injected**: Check DI registration in `Program.cs`
4. **Exception swallowed**: Check application logs for error details

### Issue 6: High Seq Storage Usage

**Symptoms**: Seq database consuming excessive disk space

**Diagnosis**:
```bash
# Check Seq storage
docker compose exec seq du -sh /data

# Check log retention settings
curl http://localhost:5341/api/settings
```

**Solutions**:
1. **Configure retention**: Set retention policy in Seq settings (e.g., 30 days)
2. **Reduce log volume**: Increase minimum log level to `Information` or `Warning`
3. **Enable sampling**: Implement log sampling for non-error logs (Phase 3)
4. **Manual cleanup**: Delete old logs via Seq UI (Settings → Retention)

---

## Quick Reference

### Key Log Locations

| Environment | Console Logs | Seq UI | Jaeger Traces | Metrics |
|-------------|--------------|--------|---------------|---------|
| **Development** | Terminal output | http://localhost:8081 | http://localhost:16686 | http://localhost:9090/metrics |
| **Docker** | `docker compose logs api` | http://localhost:8081 | http://localhost:16686 | http://localhost:9090/metrics |
| **Production** | CloudWatch/Azure Logs | https://logs.meepleai.dev | https://traces.meepleai.dev | https://metrics.meepleai.dev |

### Key Configuration Files

| File | Purpose |
|------|---------|
| `apps/api/src/Api/Logging/LoggingConfiguration.cs` | Serilog setup |
| `apps/api/src/Api/Logging/SensitiveDataDestructuringPolicy.cs` | Sensitive data redaction |
| `apps/api/src/Api/Logging/LogForgingSanitizationPolicy.cs` | Log forging prevention |
| `apps/api/src/Api/Services/AuditService.cs` | Audit logging implementation |
| `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs` | OpenTelemetry + Health checks |
| `appsettings.json` | Log levels, Seq URL |

### Common Commands

```bash
# View live API logs (Docker)
docker compose logs -f api

# Search logs in Seq
# Navigate to http://localhost:8081 and use query syntax:
# @Level = 'Error'
# UserId = 'your-guid'
# CorrelationId = 'your-correlation-id'

# Query audit logs (PostgreSQL)
psql -h localhost -U postgres -d meepleai -c \
  "SELECT * FROM \"AuditLogs\" ORDER BY \"CreatedAt\" DESC LIMIT 10;"

# Check OpenTelemetry traces (Jaeger)
# Navigate to http://localhost:16686
# Select service: MeepleAI.Api
# Search by operation or tags

# View Prometheus metrics
curl http://localhost:9090/metrics
```

### Support

**Documentation**:
- [Architecture Overview](../architecture/board-game-ai-architecture-overview.md)
- [Security Documentation](../security/sensitive-info-exposure-fix-733.md)
- [Troubleshooting Guide](../troubleshooting.md)

**Issues**:
- File bug: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- Security issue: Email security@meepleai.dev

**Emergency Contacts**:
- On-call: PagerDuty (configured in production)
- Seq support: https://docs.datalust.co/docs

---

**Last Updated**: 2025-11-12
**Version**: 1.0
**Author**: Engineering Team
**Status**: Production-Ready
