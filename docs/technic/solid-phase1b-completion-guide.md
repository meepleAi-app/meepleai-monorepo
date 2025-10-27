# SOLID Refactoring Phase 1B: Endpoint Extraction - Completion Guide

## Current State

- **Program.cs**: 6,387 lines
- **Target**: ~150 lines
- **To Extract**: ~6,180 lines (144 endpoints)
- **Status**: Phase 1 & 2 complete, Phase 1B in progress

## Completed Work

### ✅ Phase 1 (Configuration Extraction)
- Infrastructure services → `AddInfrastructureServices()`
- Application services → `AddApplicationServices()`
- Authentication services → `AddAuthenticationServices()`
- Observability services → `AddObservabilityServices()`

### ✅ Phase 2 (Middleware Extraction)
- Middleware pipeline → `ConfigureMiddlewarePipeline()`

### ✅ Phase 1B Partial (Routing Extraction)
- CookieHelpers.cs created
- AuthEndpoints.cs created (~40 endpoints, ~900 lines)

## Remaining Work

### 🔄 Phase 1B: Complete Endpoint Extraction

Extract remaining 104 endpoints to 6 routing files:

| File | Endpoints | Lines | Patterns |
|------|-----------|-------|----------|
| GameEndpoints.cs | ~10 | ~300 | `/games` (root level) |
| RuleSpecEndpoints.cs | ~25 | ~1,200 | `/games/{id}/rulespecs/*`, `/rulespecs/*`, `/comments/*` |
| PdfEndpoints.cs | ~15 | ~600 | `/games/{id}/pdfs/*`, `/pdfs/*`, `/ingest/pdf` |
| ChatEndpoints.cs | ~20 | ~900 | `/chats/*` |
| AiEndpoints.cs | ~25 | ~1,500 | `/agents/*`, `/rag/*`, `/chess/*`, `/bgg/*` |
| AdminEndpoints.cs | ~15 | ~700 | `/admin/*`, `/logs`, `/alerts/*`, `/prompts`, `/api-keys` |

## Systematic Extraction Process

### Step 1: Read Endpoint Sections

For each routing file, identify the line ranges in Program.cs:

```bash
# Find all endpoints matching a pattern
grep -n 'v1Api.MapPost("/games"' Program.cs
grep -n 'v1Api.MapGet("/chats' Program.cs
```

Use the grep output from this analysis:
- Line 2174-2227: Games endpoints
- Line 2327-2605: PDF/Ingest endpoints
- Line 2605-3220: RuleSpec endpoints
- Line 5935-6275: Chat endpoints
- Line 1049-2096: AI/Agent endpoints (qa, explain, setup, chess)
- Line 2227-2327: BGG endpoints
- Line 3268-5316: Admin endpoints

### Step 2: Extract Using Line Ranges

Read Program.cs sections and copy endpoint definitions:

```powershell
# Example: Extract game endpoints (lines 2174-2227)
$content = Get-Content Program.cs -Raw
$gameSection = $content.Substring($startPos, $endPos - $startPos)
```

### Step 3: Template for Each Routing File

```csharp
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Routing;

/// <summary>
/// [Domain] endpoints.
/// Handles [brief description].
/// </summary>
public static class [Domain]Endpoints
{
    public static RouteGroupBuilder Map[Domain]Endpoints(this RouteGroupBuilder group)
    {
        // Extract endpoints from Program.cs
        // Replace v1Api.MapXXX with group.MapXXX
        // Remove /api/v1 prefix from paths

        group.MapGet("/resource", async (...) => { ... });
        group.MapPost("/resource", async (...) => { ... });
        // ... more endpoints ...

        return group;
    }

    // Private helper methods if needed
    private static void MapSubGroup(RouteGroupBuilder group)
    {
        // ...
    }
}
```

### Step 4: Update Program.cs

Replace lines 209-6387 with:

```csharp
using Api.Routing;

// ... (lines 1-208 stay unchanged) ...

var app = builder.Build();

// Migrations (lines 187-203 stay unchanged)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    if (!ShouldSkipMigrations(app, db))
    {
        db.Database.Migrate();
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrant.EnsureCollectionExistsAsync();
    }
}

// Middleware pipeline
app.ConfigureMiddlewarePipeline(forwardedHeadersEnabled);

// Infrastructure endpoints (unversioned)
app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

// Health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// API v1 endpoint routing
var v1Api = app.MapGroup("/api/v1");

v1Api.MapAuthEndpoints();
v1Api.MapGameEndpoints();
v1Api.MapRuleSpecEndpoints();
v1Api.MapPdfEndpoints();
v1Api.MapChatEndpoints();
v1Api.MapAiEndpoints();
v1Api.MapAdminEndpoints();

app.Run();

// Helper methods
static bool ShouldSkipMigrations(WebApplication app, MeepleAiDbContext db)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return true;
    }

    if (app.Configuration.GetValue<bool?>("SkipMigrations") == true)
    {
        return true;
    }

    var providerName = db.Database.ProviderName;
    if (providerName != null && providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connection = db.Database.GetDbConnection();
        var connectionString = connection?.ConnectionString;
        var dataSource = connection?.DataSource;

        if (!string.IsNullOrWhiteSpace(connectionString) &&
            (connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase) ||
             connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataSource) &&
            dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

public partial class Program { }
```

**Final Line Count**: ~150-200 lines

### Step 5: Move Helpers to Routing Namespace

Create `Routing/RouteHelpers.cs`:

```csharp
namespace Api.Routing;

public static class RouteHelpers
{
    public static ChatMessageResponse MapToChatMessageResponse(ChatLogEntity entity)
    {
        return new ChatMessageResponse(
            entity.Id,
            entity.ChatId,
            entity.UserId,
            entity.Level,
            entity.Message,
            entity.SequenceNumber,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedByUserId,
            entity.IsInvalidated,
            entity.MetadataJson
        );
    }
}
```

### Step 6: Verification

```bash
cd apps/api
dotnet build
dotnet test

# Verify line count
wc -l src/Api/Program.cs
# Should be ~150-200 lines

wc -l src/Api/Routing/*.cs
# Should be ~6,000 lines total
```

## Critical Requirements Checklist

- [ ] All 144 endpoints extracted (none missing)
- [ ] All endpoint logic preserved exactly (no changes)
- [ ] All comments with feature IDs preserved (AUTH-07, AI-14, etc.)
- [ ] RouteGroupBuilder extension pattern used correctly
- [ ] Build succeeds with 0 errors
- [ ] All tests pass (no regressions)
- [ ] Program.cs reduced to ~150-200 lines
- [ ] Routing files total ~6,000 lines

## Common Pitfalls

1. **Missing endpoints**: Verify all 144 endpoints are extracted
2. **Changed logic**: Copy paste exact code, don't refactor
3. **Lost comments**: Preserve ALL feature ID comments
4. **Path prefixes**: Remove `/api/v1` prefix in routing files
5. **Helper dependencies**: Ensure CookieHelpers is accessible

## Automation Options

### Option 1: PowerShell Script

Create `tools/extract-all-endpoints.ps1`:

```powershell
# Read Program.cs
$content = Get-Content "src/Api/Program.cs" -Raw

# Define endpoint ranges (from grep analysis)
$ranges = @{
    "Auth" = @(290, 1047)
    "Ai" = @(1048, 2096)
    "Game" = @(2174, 2227)
    "Bgg" = @(2227, 2327)
    "Pdf" = @(2327, 2605)
    "RuleSpec" = @(2605, 3220)
    "Admin" = @(3268, 5316)
    "Chat" = @(5935, 6275)
}

# Extract each domain
foreach ($domain in $ranges.Keys) {
    $start = $ranges[$domain][0]
    $end = $ranges[$domain][1]

    # Calculate character positions
    # Extract section
    # Create routing file
}
```

### Option 2: MCP morphllm

Use morphllm-fast-apply for pattern-based extraction:

```bash
# Extract endpoints matching pattern
mcp morphllm edit_file \
  --path "Program.cs" \
  --code_edit "Extract all v1Api.MapXXX endpoints to routing classes" \
  --instruction "Move endpoint definitions to Routing/*.cs files"
```

### Option 3: Manual Extraction

For each routing file:
1. Search for endpoint pattern: `grep -n 'v1Api.Map.*"/path'`
2. Read line range in Program.cs
3. Copy endpoint definition
4. Paste into routing file
5. Replace `v1Api` with `group`
6. Remove `/api/v1` prefix
7. Test build
8. Repeat for next endpoint

## Testing Strategy

### Unit Tests
- No changes needed (tests call endpoints via HTTP)

### Integration Tests
- Verify all 144 endpoints still respond correctly
- Check authentication still works
- Verify authorization policies preserved

### Manual Testing
```bash
# Start API
cd apps/api/src/Api && dotnet run

# Test auth endpoint
curl http://localhost:8080/api/v1/auth/me

# Test game endpoint
curl http://localhost:8080/api/v1/games

# Test admin endpoint (with auth)
curl -H "X-API-Key: xxx" http://localhost:8080/api/v1/admin/stats
```

## Success Metrics

- **Program.cs**: 6,387 → ~150 lines (-97%)
- **Routing files**: 7 files, ~6,000 lines total
- **Build**: 0 errors, 0 warnings
- **Tests**: 100% passing (no regressions)
- **Endpoints**: All 144 working

## Next Steps After Completion

1. **Commit**: Create feature branch and commit changes
2. **PR**: Create pull request with detailed description
3. **Review**: Code review focusing on completeness
4. **Merge**: Merge to main after approval
5. **Document**: Update CLAUDE.md with new structure

## Estimated Effort

- **Manual extraction**: 6-8 hours (systematic, careful)
- **Automated script**: 2-3 hours (script + verification)
- **MCP morphllm**: 1-2 hours (tool + verification)

**Recommendation**: Use automated approach for speed and accuracy.
