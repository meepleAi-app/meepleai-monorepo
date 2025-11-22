# Quick Win #003: Session Validation Middleware

**Priority**: ⚡ QUICK WIN
**Effort**: 3 hours
**Impact**: ⭐ LOW-MEDIUM
**ROI**: Medium (cleaner code, better for complex scenarios)
**Status**: Not Started

---

## Problem

Session validation pattern repeated **64 times** in endpoints:

```csharp
// Repeated in every protected endpoint
var (authenticated, session, error) = context.TryGetActiveSession();
if (!authenticated) return error!;
```

While this is already extracted to an extension method (good!), it still:
- Clutters endpoint code
- Requires manual checking in every endpoint
- Can be forgotten (security risk)

---

## Current State (Already Good!)

✅ Extension method exists:
```csharp
// In CookieHelpers.cs
public static (bool Authenticated, Session? Session, IResult? Error)
    TryGetActiveSession(this HttpContext context)
{
    // ... session validation logic
}
```

**This is already better than inline validation!**

---

## Opportunity for Improvement

For endpoints requiring authentication, we can:
1. Use middleware for automatic session validation
2. Make session available via HttpContext.Items
3. Reduce boilerplate in endpoints

**Note**: This is optional since extension method already works well.

---

## Solution Options

### Option A: Endpoint Filter (Recommended)

Create an endpoint filter for session validation:

**File**: `apps/api/src/Api/Filters/RequireSessionFilter.cs`

```csharp
namespace Api.Filters;

/// <summary>
/// Endpoint filter that validates active session and injects it into HttpContext.
/// </summary>
public class RequireSessionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Validate session
        var (authenticated, session, error) = httpContext.TryGetActiveSession();

        if (!authenticated)
            return error!;

        // Store session in HttpContext.Items for endpoint use
        httpContext.Items["Session"] = session;

        // Continue to endpoint
        return await next(context);
    }
}

/// <summary>
/// Extension methods for easier use of RequireSessionFilter.
/// </summary>
public static class SessionFilterExtensions
{
    public static RouteHandlerBuilder RequireSession(
        this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<RequireSessionFilter>();
    }

    /// <summary>
    /// Gets the validated session from HttpContext.Items.
    /// Throws if session not found (filter not applied).
    /// </summary>
    public static Session GetSession(this HttpContext context)
    {
        if (context.Items.TryGetValue("Session", out var session))
            return (Session)session!;

        throw new InvalidOperationException(
            "Session not found. Ensure RequireSession filter is applied.");
    }
}
```

---

### Option B: Middleware (Global)

Create middleware for automatic session validation:

**File**: `apps/api/src/Api/Middleware/SessionValidationMiddleware.cs`

```csharp
namespace Api.Middleware;

/// <summary>
/// Middleware that validates session for protected endpoints.
/// Skips endpoints marked with [AllowAnonymous].
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        // Skip if endpoint allows anonymous
        var allowAnonymous = endpoint?.Metadata
            .GetMetadata<IAllowAnonymous>() != null;

        if (!allowAnonymous)
        {
            // Validate session
            var (authenticated, session, error) = context.TryGetActiveSession();

            if (!authenticated)
            {
                await error!.ExecuteAsync(context);
                return; // Short-circuit
            }

            // Store session for endpoint use
            context.Items["Session"] = session;
        }

        await _next(context);
    }
}
```

---

## Implementation Steps

### Step 1: Choose Approach (10 minutes)

**Recommendation**: Use **Option A (Endpoint Filter)** because:
- ✅ Opt-in (safer, no breaking changes)
- ✅ More flexible (can be applied per endpoint)
- ✅ Easier to test
- ✅ Better for debugging

Option B (Middleware) is more invasive and affects all endpoints.

---

### Step 2: Create Endpoint Filter (30 minutes)

Create `RequireSessionFilter` as shown in Option A above.

**File**: `apps/api/src/Api/Filters/RequireSessionFilter.cs`

---

### Step 3: Refactor Endpoints (1.5 hours)

**Before**:
```csharp
app.MapGet("/games/{gameId}", async (
    string gameId,
    HttpContext context,
    IMediator mediator) =>
{
    // Manual session validation
    var (authenticated, session, error) = context.TryGetActiveSession();
    if (!authenticated) return error!;

    // Use session
    var query = new GetGameByIdQuery(gameId, session.UserId);
    var result = await mediator.Send(query);

    return Results.Ok(result);
})
.RequireAuthorization();
```

**After**:
```csharp
app.MapGet("/games/{gameId}", async (
    string gameId,
    HttpContext context,
    IMediator mediator) =>
{
    // Session automatically validated by filter
    var session = context.GetSession();

    // Use session
    var query = new GetGameByIdQuery(gameId, session.UserId);
    var result = await mediator.Send(query);

    return Results.Ok(result);
})
.RequireAuthorization()
.RequireSession(); // <-- Add filter
```

**Even cleaner** (inject Session via parameter):
```csharp
// Add parameter binding for Session
app.MapGet("/games/{gameId}", async (
    string gameId,
    Session session, // <-- Injected from HttpContext.Items
    IMediator mediator) =>
{
    var query = new GetGameByIdQuery(gameId, session.UserId);
    var result = await mediator.Send(query);

    return Results.Ok(result);
})
.RequireAuthorization()
.RequireSession();
```

**To enable parameter binding**, add:

```csharp
// In Program.cs
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Custom parameter binding
builder.Services.Configure<RouteHandlerOptions>(options =>
{
    options.ThrowOnBadRequest = true;
});

// Extension for Session binding
public static class SessionBindingExtensions
{
    public static RouteHandlerBuilder BindSession(
        this RouteHandlerBuilder builder)
    {
        return builder.Add(endpointBuilder =>
        {
            var originalRequestDelegate = endpointBuilder.RequestDelegate;
            endpointBuilder.RequestDelegate = async context =>
            {
                // Session should be in Items (set by RequireSessionFilter)
                var session = context.GetSession();

                // Make available for parameter binding
                context.Request.RouteValues["session"] = session;

                await originalRequestDelegate!(context);
            };
        });
    }
}
```

**Files to refactor** (64 endpoints):
- GameEndpoints.cs (~12 endpoints)
- ChatEndpoints.cs (~8 endpoints)
- AdminEndpoints.cs (~20 endpoints)
- AuthEndpoints.cs (~15 endpoints)
- AiEndpoints.cs (~9 endpoints)

---

### Step 4: Testing (1 hour)

**Unit Tests** (`RequireSessionFilterTests.cs`):

```csharp
public class RequireSessionFilterTests
{
    [Fact]
    public async Task InvokeAsync_WithValidSession_CallsNext()
    {
        // Arrange
        var httpContext = CreateHttpContextWithSession();
        var filter = new RequireSessionFilter();
        var nextCalled = false;

        var next = new EndpointFilterDelegate(async (ctx) =>
        {
            nextCalled = true;
            return Results.Ok();
        });

        var filterContext = CreateFilterContext(httpContext);

        // Act
        await filter.InvokeAsync(filterContext, next);

        // Assert
        Assert.True(nextCalled);
        Assert.NotNull(httpContext.Items["Session"]);
    }

    [Fact]
    public async Task InvokeAsync_WithoutSession_ReturnsUnauthorized()
    {
        // Arrange
        var httpContext = CreateHttpContextWithoutSession();
        var filter = new RequireSessionFilter();
        var nextCalled = false;

        var next = new EndpointFilterDelegate(async (ctx) =>
        {
            nextCalled = true;
            return Results.Ok();
        });

        var filterContext = CreateFilterContext(httpContext);

        // Act
        var result = await filter.InvokeAsync(filterContext, next);

        // Assert
        Assert.False(nextCalled);
        // Verify result is 401 Unauthorized
    }

    [Fact]
    public void GetSession_SessionInItems_ReturnsSession()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expectedSession = new Session { UserId = Guid.NewGuid() };
        httpContext.Items["Session"] = expectedSession;

        // Act
        var session = httpContext.GetSession();

        // Assert
        Assert.Equal(expectedSession, session);
    }

    [Fact]
    public void GetSession_SessionNotInItems_ThrowsException()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            httpContext.GetSession());
    }
}
```

**Integration Tests**:
- Test protected endpoint with valid session
- Test protected endpoint without session (401)
- Test endpoint with .AllowAnonymous() (no session required)

---

## Acceptance Criteria

- [ ] RequireSessionFilter created and tested
- [ ] Session extension methods (GetSession, RequireSession) created
- [ ] 10+ endpoints refactored to use filter (proof of concept)
- [ ] All existing tests pass
- [ ] Unit tests for RequireSessionFilter
- [ ] Integration tests verify filter works

---

## Success Metrics

**If full adoption** (all 64 endpoints):
- ✅ 64 manual validations → 1 filter + 64 one-liners
- ✅ Cleaner endpoint code
- ✅ Centralized session validation logic
- ✅ Easier to extend (e.g., add session logging)

**Partial adoption** (proof of concept):
- ✅ Pattern established for future endpoints
- ✅ Coexists with existing extension method

---

## Migration Strategy

**Phase 1 - Proof of Concept** (this Quick Win):
- Create filter
- Refactor 10-15 endpoints
- Verify it works well

**Phase 2 - Gradual Adoption** (optional):
- Refactor remaining endpoints incrementally
- Can coexist with `TryGetActiveSession()` approach
- No breaking changes

**Phase 3 - Deprecation** (far future, optional):
- Once all endpoints use filter
- Can deprecate `TryGetActiveSession()` extension

---

## When to Use This

**Use RequireSessionFilter when**:
- Creating new protected endpoints
- Refactoring existing endpoints
- Want cleaner endpoint code

**Keep using TryGetActiveSession() when**:
- Endpoint has conditional authentication (optional session)
- Need custom error handling
- Endpoint is rarely used (not worth refactoring)

---

## Files Changed

**New Files** (1):
- ✅ `apps/api/src/Api/Filters/RequireSessionFilter.cs` (~60 LOC)

**Modified Files** (10-64):
- Various endpoint files (depends on adoption)

**New Test Files** (1):
- ✅ `tests/Api.Tests/Filters/RequireSessionFilterTests.cs` (~100 LOC)

**Total LOC Change**: Minimal (mostly code organization)

---

## Estimated Timeline

| Task | Time |
|------|------|
| Choose approach | 10min |
| Create endpoint filter | 30min |
| Refactor 10-15 endpoints (POC) | 1.5h |
| Testing | 1h |
| **Total** | **3h** |

---

## Optional: Full Adoption

If you decide to refactor all 64 endpoints:
- **Effort**: +4 hours
- **Total**: 7 hours
- **Benefit**: Consistent session handling across entire API

---

## Related Issues

- None (standalone improvement)

---

## References

- Current Extension: `apps/api/src/Api/Routing/CookieHelpers.cs`
- Duplication: 64 instances of `TryGetActiveSession()`
