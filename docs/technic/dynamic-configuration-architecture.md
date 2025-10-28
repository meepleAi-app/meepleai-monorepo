# Dynamic Configuration System Architecture

> **Status**: ✅ Implemented (CONFIG-01 through CONFIG-06)
> **Last Updated**: 2025-10-27
> **Audience**: Backend developers extending the configuration system

## Overview

The dynamic configuration system enables runtime modification of application behavior without code changes or redeployment. It follows a **3-tier fallback architecture** that prioritizes database configurations while maintaining backward compatibility with traditional appsettings.json files.

**Key Features**:
- **Runtime updates**: Change configurations without server restart (most settings)
- **Environment isolation**: Separate configurations for Development/Staging/Production
- **Audit trail**: Complete history of who changed what and when
- **Validation**: Type-safe value validation before applying changes
- **Caching**: Hybrid L1 (in-memory) + L2 (Redis) caching for performance
- **Rollback capability**: Restore previous configuration values

---

## Architecture Overview

### 3-Tier Fallback Chain

```
┌─────────────────────────────────────────────────────────┐
│  TIER 1: Database (Highest Priority)                    │
│  - system_configurations table                          │
│  - Runtime editable via Admin UI                        │
│  - Environment-specific (Production/Development)        │
│  - HybridCache L1+L2 (5min TTL)                        │
│  - Audit trail with version control                     │
└─────────────────────────────────────────────────────────┘
                      ↓ (if key not found OR inactive)
┌─────────────────────────────────────────────────────────┐
│  TIER 2: appsettings.json (Backward Compatibility)      │
│  - Deployment-time configuration                        │
│  - ASP.NET Core IConfiguration provider                 │
│  - Hierarchical key format (AI:Model, Rag:TopK)        │
│  - No runtime changes (requires restart)                │
└─────────────────────────────────────────────────────────┘
                      ↓ (if key not found)
┌─────────────────────────────────────────────────────────┐
│  TIER 3: Hardcoded Defaults (Safety Fallback)           │
│  - Const values in service classes                      │
│  - Ensures system always works                          │
│  - Safe, tested default values                          │
│  - Documented in code comments                          │
└─────────────────────────────────────────────────────────┘
```

**Resolution Algorithm**:

```csharp
public async Task<T?> GetValueAsync<T>(string key, T? defaultValue = default)
{
    // 1. Try database (TIER 1)
    var dbConfig = await GetConfigurationByKeyAsync(key);
    if (dbConfig != null && dbConfig.IsActive)
    {
        return DeserializeValue<T>(dbConfig.Value, dbConfig.ValueType);
    }

    // 2. Try appsettings.json (TIER 2)
    var appsettingsValue = _configuration[key];
    if (!string.IsNullOrEmpty(appsettingsValue))
    {
        return (T)Convert.ChangeType(appsettingsValue, typeof(T));
    }

    // 3. Return hardcoded default (TIER 3)
    return defaultValue;
}
```

---

## Data Flow Diagrams

### Configuration Retrieval Flow

```
User/Service Request
        ↓
ConfigurationService.GetValueAsync<T>(key)
        ↓
    ┌───────────────┐
    │ Check Cache   │ → HybridCache L1 (in-memory)
    │ (5min TTL)    │ → HybridCache L2 (Redis)
    └───────────────┘
        ↓ (cache miss)
    ┌───────────────┐
    │ Query DB      │ → SELECT * FROM system_configurations
    │ (TIER 1)      │   WHERE key = @key AND is_active = true
    └───────────────┘
        ↓ (if found)
    ┌───────────────┐
    │ Cache Result  │ → Store in L1 + L2
    │ Return Value  │
    └───────────────┘
        ↓ (if NOT found)
    ┌───────────────┐
    │ Try           │ → IConfiguration[key]
    │ appsettings   │
    │ (TIER 2)      │
    └───────────────┘
        ↓ (if NOT found)
    ┌───────────────┐
    │ Return        │ → Hardcoded default value
    │ Default       │
    │ (TIER 3)      │
    └───────────────┘
```

### Configuration Update Flow

```
Admin User (Admin UI)
        ↓
PUT /api/v1/admin/configurations/{id}
        ↓
    ┌──────────────────┐
    │ Validate Session │ → ActiveSessionMiddleware
    │ (Admin role)     │ → 401 if not logged in
    └──────────────────┘ → 403 if not Admin
        ↓
    ┌──────────────────┐
    │ Validate Value   │ → ConfigurationService.ValidateConfigurationAsync()
    │ (Type + Range)   │ → Check type: int/double/bool/json/string
    └──────────────────┘ → Domain-specific rules (e.g., RateLimit > 0)
        ↓ (if valid)
    ┌──────────────────┐
    │ Update Database  │ → PreviousValue = CurrentValue
    │                  │ → Value = NewValue
    │                  │ → Version++
    │                  │ → UpdatedByUserId = AdminId
    │                  │ → UpdatedAt = NOW()
    └──────────────────┘
        ↓
    ┌──────────────────┐
    │ Invalidate Cache │ → Remove from L1 (all environments)
    │                  │ → Remove from L2 (all environments)
    └──────────────────┘
        ↓
    ┌──────────────────┐
    │ Log Audit Trail  │ → CreatedAt, UpdatedByUserId
    │ Return DTO       │
    └──────────────────┘
```

---

## Role-Based Configuration Resolution

### Environment Prioritization

The system supports environment-specific overrides using the `Environment` field:

**Priority Order**:
1. **Environment-specific** (`Environment = "Production"`)
2. **All environments** (`Environment = "All"`)

**Example Query**:

```sql
SELECT * FROM system_configurations
WHERE "Key" = 'RateLimit:Admin:MaxTokens'
  AND "IsActive" = true
  AND ("Environment" = 'Production' OR "Environment" = 'All')
ORDER BY CASE WHEN "Environment" = 'Production' THEN 0 ELSE 1 END
LIMIT 1;
```

**Resolution Logic**:

```
Configuration Key: RateLimit:Admin:MaxTokens
Current Environment: Production

Database Results:
1. Environment="Production", Value="150" → ✅ SELECTED
2. Environment="All", Value="100"       → Ignored (lower priority)

If no Production-specific config exists:
1. Environment="All", Value="100"       → ✅ SELECTED (fallback)

If no database config exists:
→ Try appsettings.json:RateLimit:Admin:MaxTokens
→ Return hardcoded default (e.g., 100)
```

### Per-Role Configuration (Future Enhancement)

While the database schema supports role-based configurations via naming conventions (`RateLimit:Admin:MaxTokens`), the current implementation does NOT dynamically resolve by authenticated user role.

**Current Behavior**:
- Services explicitly request role-specific keys: `GetValueAsync("RateLimit:Admin:MaxTokens")`
- No automatic role-based routing

**Future Enhancement** (not implemented):

```csharp
// Proposed API
var maxTokens = await _configService.GetValueForRoleAsync<int>(
    "RateLimit:MaxTokens",
    session.User.Role,  // Automatic role-based resolution
    defaultValue: 100
);

// Would resolve to:
// 1. RateLimit:Admin:MaxTokens (if role = Admin)
// 2. RateLimit:Editor:MaxTokens (if role = Editor)
// 3. RateLimit:User:MaxTokens (if role = User)
// 4. RateLimit:MaxTokens (generic fallback)
```

---

## Adding New Configuration Keys

### Step-by-Step Guide

**Example**: Add `Email:SmtpTimeout` configuration for email service timeout

#### 1. Define Hardcoded Default (TIER 3)

**File**: `apps/api/src/Api/Services/EmailService.cs`

```csharp
public class EmailService : IEmailService
{
    // Hardcoded defaults (TIER 3)
    private const int DEFAULT_SMTP_TIMEOUT_SECONDS = 30;

    private readonly IConfigurationService _configService;
    private readonly ILogger<EmailService> _logger;

    // Constructor...
}
```

#### 2. Create Getter Method

**File**: `apps/api/src/Api/Services/EmailService.cs`

```csharp
private async Task<int> GetSmtpTimeoutAsync()
{
    try
    {
        var timeout = await _configService.GetValueAsync<int>(
            key: "Email:SmtpTimeout",
            defaultValue: DEFAULT_SMTP_TIMEOUT_SECONDS
        );

        // Validation: 1-300 seconds
        if (timeout < 1)
        {
            _logger.LogWarning(
                "Email:SmtpTimeout {Value} is too low, using default {Default}",
                timeout, DEFAULT_SMTP_TIMEOUT_SECONDS
            );
            return DEFAULT_SMTP_TIMEOUT_SECONDS;
        }

        if (timeout > 300)
        {
            _logger.LogWarning(
                "Email:SmtpTimeout {Value} exceeds 300s, capping at 300",
                timeout
            );
            return 300;
        }

        return timeout;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load Email:SmtpTimeout, using default");
        return DEFAULT_SMTP_TIMEOUT_SECONDS;
    }
}
```

#### 3. Use Configuration in Service Logic

```csharp
public async Task SendEmailAsync(string to, string subject, string body)
{
    var timeout = await GetSmtpTimeoutAsync();

    using var client = new SmtpClient(_smtpHost, _smtpPort)
    {
        Timeout = timeout * 1000  // Convert to milliseconds
    };

    await client.SendMailAsync(to, subject, body);
}
```

#### 4. Add appsettings.json Entry (TIER 2 - Optional)

**File**: `apps/api/src/Api/appsettings.json`

```json
{
  "Email": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpTimeout": 45
  }
}
```

#### 5. Create Database Migration (TIER 1)

**File**: `apps/api/src/Api/Migrations/20251027120000_AddEmailSmtpTimeout.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddEmailSmtpTimeout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Insert Production configuration
        migrationBuilder.Sql(@"
            INSERT INTO system_configurations (
                ""Id"", ""Key"", ""Value"", ""ValueType"",
                ""Description"", ""Category"", ""IsActive"",
                ""RequiresRestart"", ""Environment"", ""Version"",
                ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId""
            ) VALUES (
                'config-email-smtp-timeout-prod',
                'Email:SmtpTimeout',
                '45',
                'int',
                'SMTP connection timeout in seconds (1-300)',
                'Email',
                true,
                false,
                'Production',
                1,
                NOW(),
                NOW(),
                'demo-admin-001'
            );
        ");

        // Insert Development configuration
        migrationBuilder.Sql(@"
            INSERT INTO system_configurations (
                ""Id"", ""Key"", ""Value"", ""ValueType"",
                ""Description"", ""Category"", ""IsActive"",
                ""RequiresRestart"", ""Environment"", ""Version"",
                ""CreatedAt"", ""UpdatedAt"", ""CreatedByUserId""
            ) VALUES (
                'config-email-smtp-timeout-dev',
                'Email:SmtpTimeout',
                '30',
                'int',
                'SMTP connection timeout in seconds (1-300)',
                'Email',
                true,
                false,
                'Development',
                1,
                NOW(),
                NOW(),
                'demo-admin-001'
            );
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM system_configurations
            WHERE ""Key" = 'Email:SmtpTimeout';
        ");
    }
}
```

#### 6. Apply Migration

```bash
cd apps/api
dotnet ef migrations add AddEmailSmtpTimeout --project src/Api
dotnet ef database update --project src/Api
```

#### 7. Add Unit Tests

**File**: `apps/api/tests/Api.Tests/Services/EmailServiceTests.cs`

```csharp
[Fact]
public async Task SendEmailAsync_UsesDatabaseConfiguration_WhenAvailable()
{
    // Arrange
    _configServiceMock
        .Setup(x => x.GetValueAsync<int>("Email:SmtpTimeout", 30, null))
        .ReturnsAsync(45);  // Database value

    // Act
    await _emailService.SendEmailAsync("test@example.com", "Subject", "Body");

    // Assert
    _smtpClientMock.Verify(x => x.Timeout == 45 * 1000);  // 45 seconds
}

[Fact]
public async Task SendEmailAsync_UsesAppsettingsConfiguration_WhenDatabaseReturnsNull()
{
    // Arrange
    _configServiceMock
        .Setup(x => x.GetValueAsync<int>("Email:SmtpTimeout", 30, null))
        .ReturnsAsync((int?)null);  // Database has no value

    // appsettings.json has Email:SmtpTimeout = 60
    // (set up in test configuration)

    // Act
    await _emailService.SendEmailAsync("test@example.com", "Subject", "Body");

    // Assert
    _smtpClientMock.Verify(x => x.Timeout == 60 * 1000);  // 60 seconds from appsettings
}

[Fact]
public async Task SendEmailAsync_UsesHardcodedDefault_WhenNoDatabaseOrAppsettings()
{
    // Arrange
    _configServiceMock
        .Setup(x => x.GetValueAsync<int>("Email:SmtpTimeout", 30, null))
        .ReturnsAsync((int?)null);  // No database value

    // No appsettings.json value configured

    // Act
    await _emailService.SendEmailAsync("test@example.com", "Subject", "Body");

    // Assert
    _smtpClientMock.Verify(x => x.Timeout == 30 * 1000);  // 30 seconds (hardcoded default)
}

[Theory]
[InlineData(-5, 30)]   // Negative → default
[InlineData(0, 30)]    // Zero → default
[InlineData(500, 300)] // Exceeds max → capped at 300
public async Task SendEmailAsync_ValidatesTimeoutValue(int dbValue, int expectedTimeout)
{
    // Arrange
    _configServiceMock
        .Setup(x => x.GetValueAsync<int>("Email:SmtpTimeout", 30, null))
        .ReturnsAsync(dbValue);

    // Act
    await _emailService.SendEmailAsync("test@example.com", "Subject", "Body");

    // Assert
    _smtpClientMock.Verify(x => x.Timeout == expectedTimeout * 1000);
}
```

#### 8. Update Documentation

Add to `docs/guide/admin-configuration.md`:

```markdown
### Email Configuration

| Key | Description | Range | Default |
|-----|-------------|-------|---------|
| Email:SmtpTimeout | SMTP connection timeout | 1-300 seconds | 30 |
```

---

## Integration Patterns

### Pattern 1: Service Constructor Injection

**Best Practice**: Inject `IConfigurationService` in service constructor

```csharp
public class RagService : IRagService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IConfigurationService configService,
        ILogger<RagService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<RagResult> SearchAsync(string query)
    {
        // Load configuration at runtime
        var topK = await _configService.GetValueAsync<int>("Rag:TopK", 5);
        var minScore = await _configService.GetValueAsync<double>("Rag:MinScore", 0.7);

        // Use configuration values
        var results = await _vectorDb.SearchAsync(query, topK, minScore);
        return results;
    }
}
```

**Pros**:
- Clean dependency injection
- Testable (mock `IConfigurationService`)
- Consistent pattern across services

**Cons**:
- Additional async call per request (mitigated by caching)

### Pattern 2: Singleton Configuration Loading

**Use Case**: Rarely-changing configuration that doesn't need runtime updates

```csharp
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfigurationService _configService;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private Dictionary<string, bool> _cachedFlags = new();
    private DateTime _lastReload = DateTime.MinValue;

    public async Task<bool> IsEnabledAsync(string featureName)
    {
        // Reload every 5 minutes
        if (DateTime.UtcNow - _lastReload > TimeSpan.FromMinutes(5))
        {
            await _reloadLock.WaitAsync();
            try
            {
                if (DateTime.UtcNow - _lastReload > TimeSpan.FromMinutes(5))
                {
                    await ReloadFlagsAsync();
                }
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        return _cachedFlags.GetValueOrDefault(featureName, false);
    }

    private async Task ReloadFlagsAsync()
    {
        var flags = await _configService.GetConfigurationsAsync(
            category: "FeatureFlags",
            activeOnly: true
        );

        _cachedFlags = flags.Items.ToDictionary(
            f => f.Key.Replace("Features:", ""),
            f => bool.Parse(f.Value)
        );

        _lastReload = DateTime.UtcNow;
    }
}
```

**Pros**:
- Reduced database calls
- Better performance for frequently accessed configs

**Cons**:
- Stale data during reload interval
- More complex code

### Pattern 3: Scoped Configuration Context

**Use Case**: Multiple related configurations needed together

```csharp
public class RateLimitContext
{
    public int MaxTokens { get; set; }
    public int RefillRate { get; set; }
    public int BurstSize { get; set; }
}

public class RateLimitService : IRateLimitService
{
    public async Task<RateLimitContext> GetRateLimitContextAsync(UserRole role)
    {
        var rolePrefix = $"RateLimit:{role}:";

        return new RateLimitContext
        {
            MaxTokens = await _configService.GetValueAsync<int>(
                $"{rolePrefix}MaxTokens", 100
            ),
            RefillRate = await _configService.GetValueAsync<int>(
                $"{rolePrefix}RefillRate", 10
            ),
            BurstSize = await _configService.GetValueAsync<int>(
                $"{rolePrefix}BurstSize", 20
            )
        };
    }

    public async Task<bool> CheckRateLimitAsync(string userId, UserRole role)
    {
        var context = await GetRateLimitContextAsync(role);

        // Use context.MaxTokens, context.RefillRate, etc.
        // ...
    }
}
```

**Pros**:
- Groups related configurations
- Single cache lookup for multiple values
- Clean API for consumers

**Cons**:
- Requires coordination between related configs

---

## Database Schema

### SystemConfigurationEntity

**File**: `apps/api/src/Api/Infrastructure/Entities/SystemConfigurationEntity.cs`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | string (PK) | No | Unique identifier (GUID or custom ID) |
| `Key` | string | No | Configuration key (hierarchical: "Category:Subcategory:Name") |
| `Value` | string | No | Configuration value (stored as string, deserialized by ValueType) |
| `ValueType` | string | No | Value type: "string", "int", "long", "double", "bool", "json" |
| `Description` | string | Yes | Human-readable description for admin UI |
| `Category` | string | No | Grouping category (e.g., "RateLimit", "AI", "FeatureFlags") |
| `IsActive` | bool | No | Whether configuration is applied (inactive configs ignored) |
| `RequiresRestart` | bool | No | Whether changing this requires server restart |
| `Environment` | string | No | Target environment: "Development", "Staging", "Production", "All" |
| `Version` | int | No | Incremented on each update for change tracking |
| `PreviousValue` | string | Yes | Value before last update (enables quick rollback) |
| `CreatedAt` | DateTime | No | Creation timestamp (UTC) |
| `UpdatedAt` | DateTime | No | Last update timestamp (UTC) |
| `CreatedByUserId` | string (FK) | No | User who created the configuration |
| `UpdatedByUserId` | string (FK) | Yes | User who last updated the configuration |
| `LastToggledAt` | DateTime | Yes | When IsActive last changed |

**Indexes**:
- Primary key on `Id`
- Unique composite index on `(Key, Environment)`
- Index on `Category` (for category filtering)
- Index on `IsActive` (for active-only queries)

**Foreign Keys**:
- `CreatedByUserId` → `users(Id)` (RESTRICT)
- `UpdatedByUserId` → `users(Id)` (SET NULL)

---

## Code Examples

### Reading Configuration (Service)

```csharp
public class RagService : IRagService
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<RagService> _logger;

    // Hardcoded defaults (TIER 3)
    private const int DEFAULT_TOP_K = 5;
    private const double DEFAULT_MIN_SCORE = 0.7;

    public RagService(
        IConfigurationService configService,
        ILogger<RagService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<RagResult> SearchAsync(string query)
    {
        // Load configuration dynamically (TIER 1 → TIER 2 → TIER 3)
        var topK = await _configService.GetValueAsync<int>("Rag:TopK", DEFAULT_TOP_K);
        var minScore = await _configService.GetValueAsync<double>("Rag:MinScore", DEFAULT_MIN_SCORE);

        _logger.LogDebug(
            "RAG search using TopK={TopK}, MinScore={MinScore}",
            topK, minScore
        );

        // Use configuration values
        var results = await _vectorDb.SearchAsync(query, topK, minScore);
        return results;
    }
}
```

### Creating Configuration (Admin Endpoint)

```csharp
// POST /api/v1/admin/configurations
v1Api.MapPost("/admin/configurations", async (
    CreateConfigurationRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger) =>
{
    // 1. Validate admin session
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value)
        || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (session.User.Role != UserRole.Admin)
    {
        return Results.StatusCode(403);
    }

    try
    {
        // 2. Create configuration with validation
        var config = await configService.CreateConfigurationAsync(
            request,
            session.User.Id
        );

        logger.LogInformation(
            "Configuration {Key} created with ID {Id}",
            request.Key, config.Id
        );

        // 3. Return created resource
        return Results.Created(
            $"/api/v1/admin/configurations/{config.Id}",
            config
        );
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(
            "Failed to create configuration {Key}: {Error}",
            request.Key, ex.Message
        );
        return Results.BadRequest(new { error = ex.Message });
    }
});
```

### Updating Configuration (TypeScript)

```typescript
// apps/web/src/lib/api.ts
export const configurationApi = {
  updateConfiguration: async (
    id: string,
    request: UpdateConfigurationRequest
  ): Promise<SystemConfigurationDto> => {
    const response = await fetch(
      `${API_BASE}/api/v1/admin/configurations/${id}`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',  // Send session cookie
        body: JSON.stringify(request),
      }
    );

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Failed to update configuration');
    }

    return response.json();
  },

  invalidateCache: async (key: string): Promise<void> => {
    await fetch(`${API_BASE}/api/v1/admin/configurations/cache/invalidate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ key }),
    });
  },
};
```

### SQL Queries

**Get Active Configuration**:

```sql
SELECT * FROM system_configurations
WHERE "Key" = 'RateLimit:Admin:MaxTokens'
  AND "IsActive" = true
  AND ("Environment" = 'Production' OR "Environment" = 'All')
ORDER BY CASE WHEN "Environment" = 'Production' THEN 0 ELSE 1 END
LIMIT 1;
```

**Audit Trail**:

```sql
SELECT
    "Key",
    "Value",
    "PreviousValue",
    "Version",
    "UpdatedAt",
    u."Email" AS "UpdatedBy"
FROM system_configurations sc
LEFT JOIN users u ON sc."UpdatedByUserId" = u."Id"
WHERE sc."Key" = 'Rag:TopK'
ORDER BY sc."UpdatedAt" DESC
LIMIT 10;
```

**Rollback to Previous Value**:

```sql
UPDATE system_configurations
SET "Value" = "PreviousValue",
    "PreviousValue" = "Value",
    "Version" = "Version" + 1,
    "UpdatedAt" = NOW(),
    "UpdatedByUserId" = 'admin-user-id'
WHERE "Id" = 'config-rag-topk-prod'
  AND "PreviousValue" IS NOT NULL;
```

---

## Performance Considerations

### HybridCache Integration

The configuration service uses `HybridCacheService` (PERF-05) for two-tier caching:

**Cache Key Format**: `config:{key}:{environment}`

**Example**: `config:RateLimit:Admin:MaxTokens:Production`

**TTL Configuration**:
- L1 (in-memory): 5 minutes
- L2 (Redis): 5 minutes (shared with L1 TTL)

**Cache Invalidation**:
- Manual: `ConfigurationService.InvalidateCacheAsync(key)`
- Automatic: After configuration updates

**Performance Impact**:

| Operation | No Cache | With HybridCache | Improvement |
|-----------|----------|------------------|-------------|
| First Request (cold) | ~5-10ms (DB query) | ~5-10ms (DB query + cache store) | Baseline |
| Subsequent Requests (warm) | ~5-10ms | ~0.1ms (L1 hit) | 50-100x faster |
| After Cache Expiry | ~5-10ms | ~1-2ms (L2 Redis hit) | 2.5-10x faster |

**Cache Hit Rate** (typical):
- L1 (in-memory): 85-90%
- L2 (Redis): 5-10%
- Database: 5-10%

### Query Optimization

**Indexed Queries** (fast):

```sql
-- Uses (Key, Environment) composite index
SELECT * FROM system_configurations
WHERE "Key" = 'Rag:TopK' AND "Environment" = 'Production';

-- Uses Category index
SELECT * FROM system_configurations
WHERE "Category" = 'RateLimit' AND "IsActive" = true;
```

**Full Table Scans** (avoid):

```sql
-- No index on Description → full table scan
SELECT * FROM system_configurations
WHERE "Description" LIKE '%rate limit%';  -- ❌ Slow
```

**Bulk Operations** (use transactions):

```csharp
public async Task BulkUpdateConfigurationsAsync(
    BulkConfigurationUpdateRequest request,
    string userId)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    try
    {
        foreach (var update in request.Updates)
        {
            // Update logic...
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        // Invalidate all affected caches
        foreach (var config in updatedConfigs)
        {
            await InvalidateCacheAsync(config.Key);
        }
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

## Related Documentation

- **API Reference**: [docs/api/configuration-endpoints.md](../api/configuration-endpoints.md)
- **Admin Guide**: [docs/guide/admin-configuration.md](../guide/admin-configuration.md)
- **AI/LLM Configuration**: [docs/issue/config-03-ai-llm-configuration-guide.md](../issue/config-03-ai-llm-configuration-guide.md)
- **Database Schema**: [docs/database-schema.md](../database-schema.md)
- **HybridCache**: [docs/technic/perf-05-hybridcache-implementation.md](perf-05-hybridcache-implementation.md)

---

**Document Version**: 1.0.0
**Generated with Claude Code**
**Co-Authored-By**: Claude <noreply@anthropic.com>
