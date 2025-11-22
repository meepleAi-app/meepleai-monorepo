# Issue #005: Split AuthEndpoints.cs into 4 Focused Files

**Priority**: 🟠 HIGH
**Effort**: 20-30 hours
**Impact**: ⭐⭐ MEDIUM
**Category**: Code Organization
**Status**: Not Started

---

## Problem Description

`AuthEndpoints.cs` is a **1,077 LOC file** managing 5-6 different authentication concerns, creating similar issues to AdminEndpoints (Issue #001):

- Difficult to navigate and find specific endpoints
- Multiple authentication concerns mixed together
- Moderate merge conflict risk
- OAuth, 2FA, password, and session logic all in one file

**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AuthEndpoints.cs:1077`

---

## Proposed Solution

Split into **4 focused endpoint files** by authentication concern:

```
apps/api/src/Api/Routing/
├── AuthenticationEndpoints.cs    (~300 LOC) - Login, logout, registration, session
├── OAuthEndpoints.cs              (~250 LOC) - OAuth flows (Google, Discord, GitHub)
├── TwoFactorEndpoints.cs          (~200 LOC) - 2FA setup, verify, backup codes
└── PasswordEndpoints.cs           (~200 LOC) - Password reset, change
```

**Total**: ~950 LOC (down from 1,077, ~120 LOC dead code removed)

---

## Acceptance Criteria

- [ ] 4 new endpoint files created with appropriate route groups
- [ ] All endpoints from `AuthEndpoints.cs` migrated
- [ ] Original `AuthEndpoints.cs` file deleted
- [ ] All existing tests pass
- [ ] No breaking changes to API contracts
- [ ] Route paths remain identical
- [ ] Authorization/authentication preserved
- [ ] OpenAPI documentation correct
- [ ] No duplicate registrations

---

## Implementation Plan

### Phase 1: AuthenticationEndpoints.cs (~6 hours)

**File**: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`

**Endpoints** (~300 LOC):
```csharp
// Registration
POST   /auth/register

// Login/Logout
POST   /auth/login
POST   /auth/logout

// Session management
GET    /auth/me
GET    /auth/sessions
DELETE /auth/sessions/{sessionId}

// API Key authentication (cookie-based)
POST   /auth/apikey/login
POST   /auth/apikey/logout

// API Key management (CRUD)
GET    /auth/apikeys
GET    /auth/apikeys/{keyId}
POST   /auth/apikeys
DELETE /auth/apikeys/{keyId}
PUT    /auth/apikeys/{keyId}/rotate
```

**Template**:
```csharp
namespace Api.Routing;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        // Registration
        group.MapPost("/register", async (
            RegisterRequest request,
            IMediator mediator) =>
        {
            var command = new RegisterCommand(
                request.Email,
                request.Password,
                request.DisplayName);

            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .AllowAnonymous();

        // Login
        group.MapPost("/login", async (
            LoginRequest request,
            IMediator mediator,
            HttpContext context) =>
        {
            var command = new LoginCommand(
                request.Email,
                request.Password,
                request.TotpCode);

            var result = await mediator.Send(command);

            // Set session cookie
            context.Response.Cookies.Append("session", result.SessionToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromDays(30)
                });

            return Results.Ok(result);
        })
        .AllowAnonymous();

        // ... rest of endpoints
    }
}
```

---

### Phase 2: OAuthEndpoints.cs (~6 hours)

**File**: `apps/api/src/Api/Routing/OAuthEndpoints.cs`

**Endpoints** (~250 LOC):
```csharp
// OAuth flows
GET    /auth/oauth/{provider}/login        # Initiate OAuth
GET    /auth/oauth/{provider}/callback     # OAuth callback

// OAuth account management
GET    /auth/oauth/accounts                # List linked accounts
POST   /auth/oauth/{provider}/link         # Link OAuth account
DELETE /auth/oauth/{accountId}/unlink      # Unlink OAuth account
```

**Template**:
```csharp
namespace Api.Routing;

public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/oauth")
            .WithTags("Authentication - OAuth")
            .WithOpenApi();

        // Initiate OAuth login
        group.MapGet("/{provider}/login", async (
            string provider,
            IMediator mediator) =>
        {
            var command = new InitiateOAuthLoginCommand(provider);
            var result = await mediator.Send(command);

            // Redirect to OAuth provider
            return Results.Redirect(result.AuthorizationUrl);
        })
        .AllowAnonymous();

        // OAuth callback
        group.MapGet("/{provider}/callback", async (
            string provider,
            string code,
            string state,
            IMediator mediator,
            HttpContext context) =>
        {
            var command = new HandleOAuthCallbackCommand(
                provider, code, state);

            var result = await mediator.Send(command);

            // Set session cookie
            context.Response.Cookies.Append("session", result.SessionToken,
                new CookieOptions { /* ... */ });

            // Redirect to frontend
            return Results.Redirect($"{frontendUrl}/auth/callback");
        })
        .AllowAnonymous();

        // List linked OAuth accounts
        group.MapGet("/accounts", async (
            HttpContext context,
            IMediator mediator) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new ListOAuthAccountsQuery(session.UserId);
            var result = await mediator.Send(query);

            return Results.Ok(result);
        })
        .RequireAuthorization();

        // ... link/unlink endpoints
    }
}
```

---

### Phase 3: TwoFactorEndpoints.cs (~5 hours)

**File**: `apps/api/src/Api/Routing/TwoFactorEndpoints.cs`

**Endpoints** (~200 LOC):
```csharp
// 2FA setup
POST   /auth/2fa/setup                 # Generate TOTP secret
POST   /auth/2fa/enable                # Enable 2FA (verify TOTP)
POST   /auth/2fa/disable               # Disable 2FA

// 2FA verification
POST   /auth/2fa/verify                # Verify TOTP code during login

// Backup codes
GET    /auth/2fa/backup-codes          # Get backup codes
POST   /auth/2fa/backup-codes/regenerate # Regenerate backup codes
```

**Template**:
```csharp
namespace Api.Routing;

public static class TwoFactorEndpoints
{
    public static void MapTwoFactorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/2fa")
            .WithTags("Authentication - 2FA")
            .WithOpenApi();

        // Setup 2FA
        group.MapPost("/setup", async (
            HttpContext context,
            IMediator mediator) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new GenerateTotpSetupCommand(session.UserId);
            var result = await mediator.Send(command);

            return Results.Ok(new
            {
                Secret = result.Secret,
                QrCodeUrl = result.QrCodeUrl,
                BackupCodes = result.BackupCodes
            });
        })
        .RequireAuthorization();

        // Enable 2FA
        group.MapPost("/enable", async (
            Enable2FARequest request,
            HttpContext context,
            IMediator mediator) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new Enable2FACommand(
                session.UserId,
                request.TotpCode);

            await mediator.Send(command);

            return Results.Ok(new { Message = "2FA enabled successfully" });
        })
        .RequireAuthorization();

        // ... rest of 2FA endpoints
    }
}
```

---

### Phase 4: PasswordEndpoints.cs (~4 hours)

**File**: `apps/api/src/Api/Routing/PasswordEndpoints.cs`

**Endpoints** (~200 LOC):
```csharp
// Password reset
POST   /auth/password/reset-request     # Request password reset
POST   /auth/password/reset              # Reset password with token
GET    /auth/password/reset/validate    # Validate reset token

// Password change
POST   /auth/password/change             # Change password (authenticated)
```

**Template**:
```csharp
namespace Api.Routing;

public static class PasswordEndpoints
{
    public static void MapPasswordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/password")
            .WithTags("Authentication - Password")
            .WithOpenApi();

        // Request password reset
        group.MapPost("/reset-request", async (
            PasswordResetRequest request,
            IMediator mediator) =>
        {
            var command = new RequestPasswordResetCommand(request.Email);
            await mediator.Send(command);

            // Always return success to prevent email enumeration
            return Results.Ok(new
            {
                Message = "If the email exists, a reset link has been sent"
            });
        })
        .AllowAnonymous();

        // Reset password
        group.MapPost("/reset", async (
            ResetPasswordRequest request,
            IMediator mediator) =>
        {
            var command = new ResetPasswordCommand(
                request.Token,
                request.NewPassword);

            await mediator.Send(command);

            return Results.Ok(new { Message = "Password reset successfully" });
        })
        .AllowAnonymous();

        // Change password
        group.MapPost("/change", async (
            ChangePasswordRequest request,
            HttpContext context,
            IMediator mediator) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var command = new ChangePasswordCommand(
                session.UserId,
                request.CurrentPassword,
                request.NewPassword);

            await mediator.Send(command);

            return Results.Ok(new { Message = "Password changed successfully" });
        })
        .RequireAuthorization();

        // ... rest of password endpoints
    }
}
```

---

### Phase 5: Update Program.cs (~2 hours)

**File**: `apps/api/src/Api/Program.cs`

**Changes**:
```csharp
// Before
app.MapAuthEndpoints();

// After
app.MapAuthenticationEndpoints();
app.MapOAuthEndpoints();
app.MapTwoFactorEndpoints();
app.MapPasswordEndpoints();
```

---

### Phase 6: Testing (~5 hours)

**Integration Tests**:
- Test all authentication endpoints
- Test OAuth flows (mock OAuth providers)
- Test 2FA setup and verification
- Test password reset flow
- Verify authorization on protected endpoints

---

## Success Metrics

- ✅ 4 focused endpoint files created
- ✅ Average file size: ~240 LOC (down from 1,077)
- ✅ Zero breaking changes
- ✅ All tests pass
- ✅ Swagger documentation complete
- ✅ Code review approved

---

## Dependencies

**Blocks**: None

**Blocked by**: None

**Related Issues**:
- Issue #001: Split AdminEndpoints (same pattern)

---

## Estimated Timeline

**Total Effort**: 20-30 hours

| Phase | Hours |
|-------|-------|
| AuthenticationEndpoints.cs | 6h |
| OAuthEndpoints.cs | 6h |
| TwoFactorEndpoints.cs | 5h |
| PasswordEndpoints.cs | 4h |
| Update Program.cs | 2h |
| Testing | 5h |
| **Buffer** | 2-10h |

---

## References

- Current File: `apps/api/src/Api/Routing/AuthEndpoints.cs:1077`
- Analysis: `docs/02-development/backend-codebase-analysis.md`
