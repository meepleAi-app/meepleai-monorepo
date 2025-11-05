# Code Quality Refactoring Guide

## Overview

This document describes the code quality improvements made to reduce code duplication and improve maintainability in the MeepleAI API codebase.

## Summary of Improvements

### ✅ Completed

1. **EndpointHelpers** - Common authentication and validation patterns
2. **ConfigurationHelper** - 3-tier configuration fallback (Database → Config → Defaults)
3. **Exception Middleware** - Already exists (`ApiExceptionHandlerMiddleware`)

### 📋 Recommended

1. **Endpoint Decomposition** - Break down large endpoint files into logical groups
2. **Common Response Patterns** - Extract repeated response patterns
3. **Validation Middleware** - Create reusable validation attributes

---

## 1. EndpointHelpers

**Location:** `src/Api/Helpers/EndpointHelpers.cs`

### Purpose

Reduces code duplication for common endpoint patterns:
- Authentication checks
- Authorization (role-based)
- Request validation
- Standard error responses

### Usage Examples

#### Before: Manual Authentication Check

```csharp
group.MapGet("/admin/users", async (HttpContext context, UserManagementService userService) =>
{
    // ❌ Repetitive authentication check (appears in 81 endpoints!)
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // ❌ Repetitive role check
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Business logic...
    var users = await userService.GetAllUsersAsync();
    return Results.Json(users);
});
```

#### After: Using EndpointHelpers

```csharp
group.MapGet("/admin/users", async (HttpContext context, UserManagementService userService) =>
{
    // ✅ Clean, reusable authentication + authorization check
    var (session, errorResult) = EndpointHelpers.CheckAdminAuth(context);
    if (session == null)
    {
        return errorResult!;
    }

    // Business logic...
    var users = await userService.GetAllUsersAsync();
    return Results.Json(users);
});
```

#### Available Helper Methods

```csharp
// Authentication only
var (session, errorResult) = EndpointHelpers.CheckAuth(context);

// Admin role required
var (session, errorResult) = EndpointHelpers.CheckAdminAuth(context);

// Editor or Admin role required
var (session, errorResult) = EndpointHelpers.CheckEditorOrAdminAuth(context);

// Validate single required parameter
var error = EndpointHelpers.ValidateRequired(gameId, "gameId");
if (error != null) return error;

// Validate multiple required parameters
var error = EndpointHelpers.ValidateRequired(
    (gameId, "gameId"),
    (query, "query"),
    (userId, "userId")
);
if (error != null) return error;
```

### Impact

- **Code Reduction:** ~5-8 lines per endpoint → ~400-650 lines saved across 81 endpoints
- **Consistency:** Standardized error responses and status codes
- **Maintainability:** Single source of truth for auth logic

---

## 2. ConfigurationHelper

**Location:** `src/Api/Helpers/ConfigurationHelper.cs`

### Purpose

Standardizes the 3-tier configuration fallback pattern used across services:
1. **Database** (via `IConfigurationService`) - Highest priority
2. **appsettings.json** (via `IConfiguration`) - Fallback
3. **Hardcoded defaults** - Final fallback

### Usage Examples

#### Before: Manual Fallback Logic

```csharp
public class LlmService
{
    private readonly IConfigurationService _configService;
    private readonly IConfiguration _fallbackConfig;

    public async Task<string> GetModelAsync()
    {
        // ❌ Repetitive fallback pattern (appears in ~15 services!)
        var model = await _configService.GetValueAsync<string>("AI:Model", null);
        if (model != null)
        {
            return model;
        }

        model = _fallbackConfig.GetValue<string>("AI:Model");
        if (model != null)
        {
            return model;
        }

        return "gpt-4o-mini"; // Default
    }
}
```

#### After: Using ConfigurationHelper

```csharp
public class LlmService
{
    private readonly ConfigurationHelper _config;

    public async Task<string> GetModelAsync()
    {
        // ✅ Clean, single-line configuration retrieval
        return await _config.GetStringAsync("AI:Model", "gpt-4o-mini");
    }
}
```

#### Available Helper Methods

```csharp
// Type-safe getters with automatic fallback
await config.GetStringAsync("AI:Model", "gpt-4o-mini");
await config.GetIntAsync("AI:MaxTokens", 4000);
await config.GetBoolAsync("Features:StreamingResponses", true);
await config.GetDoubleAsync("AI:Temperature", 0.7);
await config.GetLongAsync("RateLimit:MaxTokens", 100000);

// Generic getter for custom types
await config.GetValueAsync<T>("Key", defaultValue);
```

### DI Registration

The `ConfigurationHelper` is already registered in the DI container:

```csharp
// In ApplicationServiceExtensions.cs
services.AddScoped<ConfigurationHelper>();
```

### Impact

- **Code Reduction:** ~10-15 lines per service → ~150-225 lines saved across 15 services
- **Consistency:** Standardized fallback behavior
- **Reliability:** Centralized error handling and logging

---

## 3. Exception Middleware (Already Exists)

**Location:** `src/Api/Middleware/ApiExceptionHandlerMiddleware.cs`

### Features

✅ Already implemented with excellent features:
- Catches all unhandled exceptions in `/api/*` endpoints
- Maps exceptions to appropriate HTTP status codes
- Structured JSON error responses
- Correlation IDs for debugging
- Stack traces in development mode
- Metrics recording for monitoring (`MeepleAiMetrics.RecordApiError`)
- Path sanitization for logging

### No Action Required

The existing middleware is well-designed and covers all exception handling needs.

---

## 4. Recommended: Endpoint Decomposition

### Problem

**AdminEndpoints.cs**: 2,801 lines, 81 endpoints
**AiEndpoints.cs**: 1,361 lines, 12 endpoints

### Recommended Structure

Create logical endpoint groups using extension methods:

```
src/Api/Routing/
├── Admin/
│   ├── UsersEndpoints.cs          (6 endpoints)
│   ├── SessionsEndpoints.cs       (3 endpoints)
│   ├── ApiKeysEndpoints.cs        (8 endpoints)
│   ├── ConfigurationsEndpoints.cs (14 endpoints)
│   ├── PromptsEndpoints.cs        (17 endpoints)
│   ├── N8nEndpoints.cs            (9 endpoints)
│   ├── AnalyticsEndpoints.cs      (5 endpoints)
│   └── ... (other groups)
├── Ai/
│   ├── AgentsEndpoints.cs         (7 endpoints)
│   ├── BggEndpoints.cs            (2 endpoints)
│   └── ChessKnowledgeEndpoints.cs (3 endpoints)
└── ... (existing files)
```

### Example: UsersEndpoints.cs

```csharp
namespace Api.Routing.Admin;

public static class UsersEndpoints
{
    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder group)
    {
        // GET /admin/users - List all users
        group.MapGet("/admin/users", async (
            HttpContext context,
            UserManagementService userManagement) =>
        {
            var (session, errorResult) = EndpointHelpers.CheckAdminAuth(context);
            if (session == null) return errorResult!;

            var users = await userManagement.GetAllUsersAsync();
            return Results.Json(users);
        })
        .WithName("ListUsers")
        .WithTags("User Management")
        .WithDescription("Get all users (Admin only)")
        .Produces<IEnumerable<UserDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /admin/users - Create user
        group.MapPost("/admin/users", async (
            CreateUserRequest request,
            HttpContext context,
            UserManagementService userManagement) =>
        {
            var (session, errorResult) = EndpointHelpers.CheckAdminAuth(context);
            if (session == null) return errorResult!;

            var validationError = EndpointHelpers.ValidateRequired(
                (request.Email, "email"),
                (request.Password, "password")
            );
            if (validationError != null) return validationError;

            var user = await userManagement.CreateUserAsync(request, session.User.Id);
            return Results.Created($"/admin/users/{user.Id}", user);
        })
        .WithName("CreateUser")
        .WithTags("User Management");

        // ... other endpoints

        return group;
    }
}
```

### Main File Integration

Update `AdminEndpoints.cs` to delegate to group files:

```csharp
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        // Delegate to logical groups
        group.MapUsersEndpoints();
        group.MapSessionsEndpoints();
        group.MapApiKeysEndpoints();
        group.MapConfigurationsEndpoints();
        group.MapPromptsEndpoints();
        group.MapN8nEndpoints();
        group.MapAnalyticsEndpoints();
        // ... other groups

        return group;
    }
}
```

### Benefits

- **Maintainability:** Easier to find and modify related endpoints
- **Code Review:** Smaller files are easier to review
- **Testing:** Logical groups align with test organization
- **Team Collaboration:** Reduces merge conflicts

---

## 5. Migration Strategy

### Phase 1: Use Helpers in Existing Code (Low Risk)

1. Start using `EndpointHelpers` and `ConfigurationHelper` in new endpoints
2. Gradually refactor existing endpoints during bug fixes or feature work
3. No need to refactor everything at once

### Phase 2: Decompose One Endpoint Group (Medium Risk)

1. Choose a small, stable group (e.g., BggEndpoints - 2 endpoints)
2. Extract into separate file
3. Run full test suite
4. Monitor for issues
5. If successful, proceed with other groups

### Phase 3: Complete Decomposition (Higher Risk)

1. Decompose remaining endpoint groups
2. Follow test-driven approach (run tests after each extraction)
3. Use feature flags if concerned about stability

---

## 6. Testing Recommendations

### Before Refactoring

```bash
# Ensure all tests pass
cd apps/api
dotnet test
```

### After Each Change

```bash
# Run targeted tests for affected endpoints
dotnet test --filter "FullyQualifiedName~UserManagementEndpoints"

# Run integration tests
dotnet test --filter "Category=Integration"
```

### After Complete Refactoring

```bash
# Full test suite
dotnet test

# Code coverage
pwsh tools/measure-coverage.ps1 -Project api
```

---

## 7. Code Review Checklist

When refactoring endpoints, ensure:

- [ ] ✅ Authentication check uses `EndpointHelpers.CheckAuth()` or variants
- [ ] ✅ Required parameter validation uses `EndpointHelpers.ValidateRequired()`
- [ ] ✅ Configuration retrieval uses `ConfigurationHelper`
- [ ] ✅ All endpoints have `.WithName()`, `.WithTags()`, `.WithDescription()`
- [ ] ✅ All endpoints have `.Produces<T>()` for Swagger documentation
- [ ] ✅ Error responses are consistent (use helper methods)
- [ ] ✅ All tests pass
- [ ] ✅ No duplicate code patterns

---

## 8. Metrics

### Current State

| File | Lines | Endpoints | Avg Lines/Endpoint |
|------|-------|-----------|-------------------|
| AdminEndpoints.cs | 2,801 | 81 | ~35 |
| AiEndpoints.cs | 1,361 | 12 | ~113 |

### Potential Savings with Helpers

| Pattern | Occurrences | Lines/Pattern | Total Saved |
|---------|-------------|---------------|-------------|
| Auth checks | ~93 | 6 | ~558 lines |
| Config fallback | ~15 | 12 | ~180 lines |
| Validation | ~50 | 4 | ~200 lines |
| **Total** | | | **~938 lines** |

### After Decomposition

| Group | Endpoints | Est. Lines | File |
|-------|-----------|-----------|------|
| Users | 6 | ~200 | UsersEndpoints.cs |
| Sessions | 3 | ~100 | SessionsEndpoints.cs |
| ApiKeys | 8 | ~250 | ApiKeysEndpoints.cs |
| ... | ... | ... | ... |

---

## 9. Additional Recommendations

### Create Validation Attributes

For complex validation scenarios, consider custom attributes:

```csharp
public class ValidateGameIdAttribute : Attribute, IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var gameId = context.GetArgument<string>(0);
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return Results.BadRequest(new { error = "gameId is required" });
        }

        return await next(context);
    }
}
```

### Extract Common Response Patterns

Create response helpers for repeated patterns:

```csharp
public static class ResponseHelpers
{
    public static IResult Success(object data)
        => Results.Json(new { ok = true, data });

    public static IResult Created(string id, object data)
        => Results.Created($"/api/v1/resource/{id}", data);

    public static IResult NotFound(string message)
        => Results.NotFound(new { error = message });
}
```

---

## 10. Next Steps

1. ✅ **Start using helpers immediately** - No refactoring needed, just use in new code
2. 📝 **Plan decomposition** - Identify which endpoint group to tackle first
3. 🧪 **Write tests** - Ensure comprehensive test coverage before refactoring
4. 🔄 **Incremental refactoring** - Do one group at a time, test thoroughly
5. 📊 **Measure impact** - Track lines of code, maintainability metrics

---

## Conclusion

These improvements provide immediate value:
- **EndpointHelpers**: Reduce ~558 lines of duplicate auth code
- **ConfigurationHelper**: Reduce ~180 lines of fallback logic
- **Exception Middleware**: Already excellent!

The decomposition of large endpoint files is recommended but can be done incrementally over time. Start using the helpers today for immediate benefits!
