# MeepleAI Alpha Invite-Only System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the alpha invite-only system by filling 8 integration gaps: OAuth gating, rejection emails, frontend conditional rendering, invitation expiry UX, admin notifications, AgentDefinition lifecycle, rate limiting, and integration tests.

**Architecture:** 85% of the system already exists. This plan wires existing components together and adds missing pieces. All backend changes follow DDD + CQRS patterns. Frontend changes are conditional UI based on config/feature flags. The `AccessRequestApprovedEventHandler` already auto-sends invitations on approval — this is correct behavior.

**Tech Stack:** .NET 9 (MediatR, FluentValidation, EF Core) | Next.js 16 (React 19, Zustand) | xUnit + Testcontainers

**Decisions (from spec-panel review):**
1. Approval auto-sends invitation (current behavior, no change needed)
2. Rejection sends email with optional reason
3. OAuth: soft redirect to access request form (not hard 403)
4. AgentDefinition lifecycle: Draft → Testing → Published
5. Excel batch import targets SharedGameCatalog (already exists — `BulkImportGamesCommand`)
6. Duplicate access requests: generic message, don't reveal registration status (already works)

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs` | Modify | Add `oauth_login` feature flag |
| `apps/api/src/Api/Routing/OAuthEndpoints.cs` | Modify | Check `OAuth:Enabled` config, redirect to frontend access-request when disabled |
| `apps/api/src/Api/Routing/AccessRequestEndpoints.cs` | Modify | Add `oauth-enabled` status to registration-mode response |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs` | Modify | Send rejection email after reject |
| `apps/api/src/Api/Services/IEmailService.cs` | Modify | Add `SendAccessRequestRejectedEmailAsync` |
| `apps/api/src/Api/Services/Email/EmailService.Auth.cs` | Modify | Implement rejection email |
| `apps/web/src/components/auth/OAuthButtons.tsx` | Modify | Hide when OAuth disabled |
| `apps/web/src/app/(auth)/register/_content.tsx` | Modify | Pass OAuth enabled state |
| `apps/web/src/lib/api/clients/accessRequestsClient.ts` | Modify | Add oauthEnabled to registration mode response |
| `apps/web/src/app/(auth)/invitation-expired/page.tsx` | Create | Expired invitation landing page |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/AgentDefinitionStatus.cs` | Create | Draft/Testing/Published enum |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs` | Modify | Add Status field + lifecycle methods |
| `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs` | Modify | Add `AccessRequest` rate limit policy |
| `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequestCreatedEvent.cs` | Verify | Ensure it carries email for notification |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs` | Modify | Add Slack notification to existing handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs` | Modify | Add `_status` column mapping |
| `tests/Api.Tests/BoundedContexts/Authentication/` | Create/Modify | Alpha flow integration tests |

---

### Task 1: OAuth Feature Flag + Backend Soft Redirect

**Goal:** When `OAuth:Enabled` config is `false`, OAuth login endpoint redirects to frontend access-request page instead of OAuth provider.

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs`
- Modify: `apps/api/src/Api/Routing/OAuthEndpoints.cs:33-88`
- Modify: `apps/api/src/Api/Routing/AccessRequestEndpoints.cs` (registration-mode response)

- [ ] **Step 1: Add `oauth_login` feature flag to seeder**

Modify `apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs`, add to `DefaultFeatureFlags` array:

```csharp
new("oauth_login", "Enable OAuth login (Google, Discord, GitHub) — disable for invite-only alpha", false, false, false, false),
```

Note: `false` globally = disabled in alpha by default.

- [ ] **Step 2: Add OAuth check to OAuth login endpoint**

Modify `apps/api/src/Api/Routing/OAuthEndpoints.cs`, in `MapLoginEndpoints`, add config check before rate limiting (after line 39).

**IMPORTANT**: `IConfigurationService.GetValueAsync<T>` has NO `CancellationToken` parameter. Signature is `GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null)`. Use the same pattern as `GetRegistrationModeQueryHandler`:

```csharp
// Alpha gate: check if OAuth is enabled (stored as "true"/"false" string in FeatureFlags category)
var oauthEnabled = await configService.GetValueAsync<bool>("oauth_login", false).ConfigureAwait(false);

if (!oauthEnabled)
{
    // Soft redirect to access request page
    var frontendUrl = context.RequestServices.GetRequiredService<IConfiguration>()["FrontendUrl"] ?? "http://localhost:3000";
    return Results.Redirect($"{frontendUrl}/register?oauth_disabled=true");
}
```

- [ ] **Step 3: Extend registration-mode response via CQRS (not direct service injection)**

**IMPORTANT — CQRS compliance**: Do NOT inject `IConfigurationService` into the endpoint. The endpoint delegates to `GetRegistrationModeQuery` via `IMediator`. Extend the CQRS chain:

1. Add `OauthEnabled` to `RegistrationModeDto` (keep existing `PublicRegistrationEnabled` field):

```csharp
// In the DTO file:
public bool OauthEnabled { get; init; }
```

2. Add the OAuth config lookup inside `GetRegistrationModeQueryHandler.Handle()`:

```csharp
var oauthEnabled = await _configService.GetValueAsync<bool>("oauth_login", false).ConfigureAwait(false);
// Include in response DTO alongside existing publicRegistrationEnabled
```

3. The endpoint stays untouched — it just forwards the query result.

- [ ] **Step 4: Run tests and verify**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Core/FeatureFlagSeeder.cs
git add apps/api/src/Api/Routing/OAuthEndpoints.cs
git add apps/api/src/Api/Routing/AccessRequestEndpoints.cs
git add tests/Api.Tests/
git commit -m "feat(auth): add OAuth feature flag gate with soft redirect for alpha"
```

---

### Task 2: Access Request Rejection Email

**Goal:** When admin rejects an access request, send an email to the requester with optional reason.

**Files:**
- Modify: `apps/api/src/Api/Services/IEmailService.cs`
- Modify: `apps/api/src/Api/Services/Email/EmailService.Auth.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs`

- [ ] **Step 1: Write failing test for rejection email**

Create: `tests/Api.Tests/BoundedContexts/Authentication/AccessRequest/RejectAccessRequestWithEmailTests.cs`

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.AccessRequest;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class RejectAccessRequestWithEmailTests
{
    [Fact]
    public void RejectAccessRequestCommand_ShouldAccept_OptionalReason()
    {
        var cmd = new RejectAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Not in alpha group");
        cmd.Reason.Should().Be("Not in alpha group");
    }

    [Fact]
    public void RejectAccessRequestCommand_ShouldAccept_NullReason()
    {
        var cmd = new RejectAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid(), null);
        cmd.Reason.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it compiles**

Run: `cd apps/api/src/Api && dotnet test --filter "RejectAccessRequestWithEmailTests" -v minimal`

- [ ] **Step 3: Add `SendAccessRequestRejectedEmailAsync` to IEmailService**

Modify `apps/api/src/Api/Services/IEmailService.cs`, add method:

```csharp
/// <summary>
/// Sends rejection notification to access request submitter.
/// </summary>
Task SendAccessRequestRejectedEmailAsync(
    string toEmail,
    string? reason,
    CancellationToken ct = default);
```

- [ ] **Step 4: Implement in EmailService.Auth.cs**

Add to `apps/api/src/Api/Services/Email/EmailService.Auth.cs`.

**NOTE**: Do NOT call `SendRawEmailAsync` directly — it bypasses the email queue and sends synchronously via SMTP. Follow the same pattern as `SendInvitationEmailAsync` and other queued methods in the codebase. Check how existing auth email methods (e.g., `SendPasswordResetEmailAsync`) are implemented — they likely use a queue-based approach via the `EmailProcessorJob`.

```csharp
public async Task SendAccessRequestRejectedEmailAsync(
    string toEmail,
    string? reason,
    CancellationToken ct = default)
{
    var reasonBlock = string.IsNullOrWhiteSpace(reason)
        ? ""
        : $"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>";

    var body = $@"
        <h2>Access Request Update</h2>
        <p>Thank you for your interest in MeepleAI.</p>
        <p>Unfortunately, your access request could not be approved at this time.</p>
        {reasonBlock}
        <p>You may submit a new request in the future.</p>
        <p>— The MeepleAI Team</p>";

    // Use the same delivery mechanism as other auth emails (queue-based if available)
    // Check SendPasswordResetEmailAsync or SendInvitationEmailAsync for the correct pattern
    await SendRawEmailAsync(
        toEmail,
        "MeepleAI — Access Request Update",
        body,
        ct).ConfigureAwait(false);
}
```

- [ ] **Step 5: Call email from RejectAccessRequestCommandHandler**

Modify `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs`.

After the existing `_repository.UpdateAsync()` call, add:

```csharp
// Send rejection notification (best-effort, don't fail the rejection)
try
{
    await _emailService.SendAccessRequestRejectedEmailAsync(
        accessRequest.Email,
        request.Reason,
        cancellationToken).ConfigureAwait(false);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to send rejection email to {Email}", accessRequest.Email);
}
```

Inject `IEmailService` and `ILogger` via constructor. Check existing constructor and add missing dependencies.

- [ ] **Step 6: Build and test**

Run: `cd apps/api/src/Api && dotnet build && dotnet test --filter "RejectAccessRequest" -v minimal`
Expected: Build succeeds, tests pass

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Services/IEmailService.cs
git add apps/api/src/Api/Services/Email/EmailService.Auth.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs
git add tests/Api.Tests/
git commit -m "feat(auth): send rejection email with optional reason on access request denial"
```

---

### Task 3: Frontend OAuth Conditional Rendering

**Goal:** Hide OAuth buttons when `oauthEnabled=false` in registration-mode response. Show access request form message when user arrives from OAuth redirect.

**Files:**
- Modify: `apps/web/src/lib/api/clients/accessRequestsClient.ts`
- Modify: `apps/web/src/app/(auth)/register/_content.tsx`
- Modify: `apps/web/src/components/auth/OAuthButtons.tsx`

- [ ] **Step 1: Extend RegistrationMode interface (don't replace)**

Modify `apps/web/src/lib/api/clients/accessRequestsClient.ts`. The existing interface is:

```typescript
export interface RegistrationMode {
  publicRegistrationEnabled: boolean;
}
```

**IMPORTANT**: Keep `publicRegistrationEnabled` — existing code depends on it. Extend with optional field:

```typescript
export interface RegistrationMode {
  publicRegistrationEnabled: boolean;
  oauthEnabled?: boolean;
}
```

The `getRegistrationMode()` method should keep the same return type.

- [ ] **Step 2: Update register page to use oauthEnabled**

Modify `apps/web/src/app/(auth)/register/_content.tsx`:

- Add `oauthEnabled` state
- Read from the existing `getRegistrationMode()` response (which now includes `oauthEnabled`)
- Pass to `OAuthButtons` as prop
- Existing code reads `result.publicRegistrationEnabled` — DO NOT change this

```typescript
const [oauthEnabled, setOauthEnabled] = useState(false);

// In the useEffect fetching registration mode:
const result = await api.accessRequests.getRegistrationMode();
setRegistrationMode(result.publicRegistrationEnabled ? 'public' : 'invite-only');
setOauthEnabled(result.oauthEnabled ?? false);
```

- [ ] **Step 3: Conditionally render OAuth buttons**

Modify `apps/web/src/components/auth/OAuthButtons.tsx`. Keep the `export default` (not named export). Add `hidden` prop:

```typescript
// Keep existing export default function signature, add hidden prop:
export default function OAuthButtons({ onOAuthLogin, hidden = false }: OAuthButtonsProps) {
  if (hidden) return null;
  // ... existing rendering unchanged
}
```

Update the `OAuthButtonsProps` interface (or create one if inline) to include `hidden?: boolean`.

- [ ] **Step 4: Handle oauth_disabled query param on register page**

In `_content.tsx`, add:

```typescript
const searchParams = useSearchParams();
const oauthDisabled = searchParams.get('oauth_disabled') === 'true';

// Show message if redirected from OAuth
{oauthDisabled && (
  <div role="alert" className="text-sm text-amber-600 bg-amber-50 p-3 rounded-md mb-4">
    OAuth login is not available during alpha. Please request access below.
  </div>
)}
```

- [ ] **Step 5: Build frontend**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/clients/accessRequestsClient.ts
git add apps/web/src/app/\(auth\)/register/_content.tsx
git add apps/web/src/components/auth/OAuthButtons.tsx
git commit -m "feat(web): hide OAuth buttons and show access request form in alpha mode"
```

---

### Task 4: Invitation Expiry UX

**Goal:** When a user clicks an expired invitation link, show a friendly page with option to request new access.

**Files:**
- Create: `apps/web/src/app/(auth)/invitation-expired/page.tsx`
- Modify: `apps/web/src/app/(auth)/register/_content.tsx` (or the invitation validation flow)

The existing flow: user clicks invitation email → lands on `/register?token=<token>` → frontend calls `POST /auth/validate-invitation` → if expired, currently shows... what? We need to check.

- [ ] **Step 1: Identify current expiry behavior**

Read `apps/web/src/app/(auth)/register/_content.tsx` to understand what happens when invitation validation fails. The backend `ValidateInvitationTokenQuery` handler likely returns an error for expired tokens.

Check: How does the frontend handle the validation error? Does it show a message or crash?

- [ ] **Step 2: Create invitation-expired page**

Create `apps/web/src/app/(auth)/invitation-expired/page.tsx`:

```tsx
'use client';

import { AuthLayout } from '@/components/auth/AuthLayout';
import { Button } from '@/components/ui/button';
import Link from 'next/link';

export default function InvitationExpiredPage() {
  return (
    <AuthLayout title="Invitation Expired">
      <div className="text-center space-y-4">
        <p className="text-muted-foreground">
          This invitation link has expired or is no longer valid.
        </p>
        <p className="text-sm text-muted-foreground">
          Invitations are valid for 7 days. You can request new access below.
        </p>
        <div className="flex flex-col gap-2 pt-4">
          <Button asChild>
            <Link href="/register">Request Access</Link>
          </Button>
          <Button variant="ghost" asChild>
            <Link href="/login">Back to Login</Link>
          </Button>
        </div>
      </div>
    </AuthLayout>
  );
}
```

- [ ] **Step 3: Redirect to expiry page on invalid token**

In the invitation validation flow (wherever `ValidateInvitationTokenQuery` is called on the frontend), add redirect logic:

```typescript
// If validation fails with expired/invalid token
if (validationError) {
  router.push('/invitation-expired');
  return;
}
```

Find the exact location — it may be in `_content.tsx` or a separate invitation acceptance component.

- [ ] **Step 4: Build frontend**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(auth\)/invitation-expired/page.tsx
git add apps/web/src/app/\(auth\)/register/_content.tsx
git commit -m "feat(web): add invitation-expired page with re-request access option"
```

---

### Task 5: Admin Notification for New Access Requests

**Goal:** When a new access request is submitted, notify admin/superadmin users via in-app notification and Slack alert.

**Files:**
- Verify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequestCreatedEvent.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs` (already exists!)
- Uses: `apps/api/src/Api/Services/AlertingService.cs` (existing multi-channel alert dispatch)

- [ ] **Step 1: Verify AccessRequestCreatedEvent has email field**

Read `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequestCreatedEvent.cs`.
It should carry `AccessRequestId` and `Email`. If not, add them.

- [ ] **Step 2: Modify existing event handler to add Slack notification**

**IMPORTANT**: `AccessRequestCreatedEventHandler.cs` ALREADY EXISTS. Do NOT create a new file — modify the existing one.

**IMPORTANT**: Do NOT inject `IAlertChannel` directly — multiple implementations exist (Email, Slack, PagerDuty). Inject `IAlertingService` instead, which handles multi-channel dispatch, throttling, and audit. This follows the pattern used throughout the codebase (see `AlertingService.cs`).

Read the existing handler first, then add the Slack notification call:

```csharp
private readonly IAlertingService _alertingService;

// Add to constructor parameters:
// IAlertingService alertingService

// In HandleEventAsync, add:
try
{
    await _alertingService.SendAlertAsync(
        "access_request",
        "info",
        $"New access request from {domainEvent.Email}",
        new Dictionary<string, object>
        {
            ["email"] = domainEvent.Email,
            ["requestId"] = domainEvent.AccessRequestId
        },
        cancellationToken).ConfigureAwait(false);
}
catch (Exception ex)
{
    Logger.LogWarning(ex, "Failed to send access request notification for {Email}", domainEvent.Email);
}
```

- [ ] **Step 3: Verify DI registration**

`IAlertingService` should already be registered. Verify by checking `AlertingService` registration in DI setup. MediatR auto-discovers event handlers via assembly scanning.

- [ ] **Step 4: Build and test**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs
git commit -m "feat(auth): notify admins via Slack when new access request is submitted"
```

---

### Task 6: AgentDefinition Lifecycle (Draft → Testing → Published)

**Goal:** Add `Status` field to `AgentDefinition` with lifecycle: `Draft → Testing → Published`. Users can only see/use Published definitions. Admins see all.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/AgentDefinitionStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AgentDefinition/CreateAgentDefinitionCommandHandler.cs`
- Modify: Admin endpoints and user-facing queries to filter by status

- [ ] **Step 1: Create AgentDefinitionStatus enum**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/AgentDefinitionStatus.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Lifecycle status for agent definitions.
/// Only Published definitions are visible to non-admin users.
/// </summary>
public enum AgentDefinitionStatus
{
    /// <summary>Agent definition is being configured by admin.</summary>
    Draft = 0,

    /// <summary>Agent is being tested in sandbox mode.</summary>
    Testing = 1,

    /// <summary>Agent is available for users to create instances from.</summary>
    Published = 2
}
```

- [ ] **Step 2: Add Status field to AgentDefinition entity**

Modify `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs`:

Add private field and public property:

```csharp
private AgentDefinitionStatus _status;

/// <summary>
/// Gets the lifecycle status (Draft, Testing, Published).
/// Only Published definitions are visible to users.
/// </summary>
public AgentDefinitionStatus Status => _status;
```

Add lifecycle methods:

```csharp
/// <summary>Moves definition to Testing status.</summary>
public void StartTesting()
{
    if (_status == AgentDefinitionStatus.Published)
        throw new InvalidOperationException("Cannot move Published definition back to Testing. Deactivate first.");

    _status = AgentDefinitionStatus.Testing;
    _updatedAt = DateTime.UtcNow;
    AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, "Status changed to Testing"));
}

/// <summary>Publishes the definition, making it available to users.</summary>
public void Publish()
{
    if (_status == AgentDefinitionStatus.Draft)
        throw new InvalidOperationException("Cannot publish directly from Draft. Move to Testing first.");

    _status = AgentDefinitionStatus.Published;
    _isActive = true;
    _updatedAt = DateTime.UtcNow;
    AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, "Status changed to Published"));
}

/// <summary>Moves definition back to Draft.</summary>
public void Unpublish()
{
    _status = AgentDefinitionStatus.Draft;
    _updatedAt = DateTime.UtcNow;
    AddDomainEvent(new AgentDefinitionUpdatedEvent(Id, "Status changed to Draft"));
}
```

Update `Create()` factory method to set `_status = AgentDefinitionStatus.Draft` (line 240, change from `_isActive = true` to `_isActive = false`).

Update internal constructor to accept `AgentDefinitionStatus status` parameter.

**Transition rules:**
- `Draft → Testing`: `StartTesting()` ✅
- `Testing → Published`: `Publish()` ✅
- `Published → Draft`: `Unpublish()` ✅ (skipping Testing on way down is fine)
- `Draft → Published`: blocked (must go through Testing)
- `Published → Testing`: blocked (must Unpublish first)

- [ ] **Step 3: Update EF Core entity configuration**

**IMPORTANT**: `AgentDefinitionConfiguration.cs` uses explicit property mapping — EF Core will NOT auto-discover the new `_status` field. Add before running migration:

Modify `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs`:

```csharp
builder.Property<int>("_status")
    .HasColumnName("status")
    .HasDefaultValue(0)
    .IsRequired();
```

- [ ] **Step 4: Create EF migration with data migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddAgentDefinitionStatus`

**IMPORTANT — Data migration**: Existing active definitions (`is_active = true`) must be set to `Published` (2), not left as `Draft` (0). Otherwise all currently active agents become invisible to users.

In the generated migration `Up()` method, add after the column creation:

```csharp
// Migrate existing active definitions to Published status
migrationBuilder.Sql("UPDATE \"AgentDefinitions\" SET \"status\" = 2 WHERE \"is_active\" = true");
```

- [ ] **Step 4: Update CreateAgentDefinitionCommandHandler**

Modify handler so newly created definitions start as `Draft`:
- Already handled by `Create()` factory method setting `_status = Draft`
- Verify the handler maps the new field in the DTO response

- [ ] **Step 5: Add admin commands for lifecycle transitions**

Create commands in `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AgentDefinition/`:

```csharp
// PublishAgentDefinitionCommand.cs
internal record PublishAgentDefinitionCommand(Guid Id) : ICommand<Unit>;

// StartTestingAgentDefinitionCommand.cs
internal record StartTestingAgentDefinitionCommand(Guid Id) : ICommand<Unit>;

// UnpublishAgentDefinitionCommand.cs
internal record UnpublishAgentDefinitionCommand(Guid Id) : ICommand<Unit>;
```

Create handlers that load from repository, call domain method, save.

- [ ] **Step 6: Add admin endpoints for lifecycle**

Add to `apps/api/src/Api/Routing/AdminAgentDefinitionEndpoints.cs`:

```csharp
group.MapPost("/{id:guid}/start-testing", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new StartTestingAgentDefinitionCommand(id));
    return Results.NoContent();
});

group.MapPost("/{id:guid}/publish", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new PublishAgentDefinitionCommand(id));
    return Results.NoContent();
});

group.MapPost("/{id:guid}/unpublish", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new UnpublishAgentDefinitionCommand(id));
    return Results.NoContent();
});
```

- [ ] **Step 7: Filter user-facing queries by Published status**

Find user-facing agent definition queries (not admin queries). Any query that returns agent definitions to regular users must filter by `Status == Published`. Admin queries return all.

Check `CreateAgentWithSetupCommand` handler — if it references `AgentDefinition`, ensure it only uses Published ones.

- [ ] **Step 8: Build and test**

Run: `cd apps/api/src/Api && dotnet build && dotnet test --filter "AgentDefinition" -v minimal`

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/AgentDefinitionStatus.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AgentDefinition/
git add apps/api/src/Api/Routing/AdminAgentDefinitionEndpoints.cs
git commit -m "feat(kb): add AgentDefinition lifecycle (Draft → Testing → Published)"
```

---

### Task 7: Rate Limit on Access Request Endpoint

**Goal:** Prevent abuse of the access request endpoint with IP-based rate limiting.

**Files:**
- Modify: `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`
- Modify: `apps/api/src/Api/Routing/AccessRequestEndpoints.cs`

- [ ] **Step 1: Add `AccessRequest` rate limit policy**

Modify `apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs`.

Add in both branches (disabled/enabled):

**Disabled branch** (for tests):
```csharp
options.AddPolicy("AccessRequest", _ => RateLimitPartition.GetNoLimiter("no-limit"));
```

**Enabled branch** (use `AddPolicy` + `RateLimitPartition.GetSlidingWindowLimiter` — same pattern as all 17 existing policies):
```csharp
options.AddPolicy("AccessRequest", httpContext =>
    RateLimitPartition.GetSlidingWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0
        }));
```

This matches the IP-based partitioning pattern of `AuthLogin` and all other policies in the file. Do NOT use `AddSlidingWindowLimiter` — that's a different API.

- [ ] **Step 2: Apply to access request endpoint**

Modify `apps/api/src/Api/Routing/AccessRequestEndpoints.cs`, on the `POST /auth/request-access` endpoint, add:

```csharp
.RequireRateLimiting("AccessRequest")
```

- [ ] **Step 3: Build and verify**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Extensions/RateLimitingServiceExtensions.cs
git add apps/api/src/Api/Routing/AccessRequestEndpoints.cs
git commit -m "feat(auth): add rate limiting to access request endpoint (5/hour per IP)"
```

---

### Task 8: Alpha Flow Integration Tests

**Goal:** Create integration tests that verify the complete alpha invite-only flow end-to-end.

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/Authentication/AlphaFlow/AlphaInviteOnlyFlowTests.cs`

- [ ] **Step 1: Create alpha flow test class**

Create `tests/Api.Tests/BoundedContexts/Authentication/AlphaFlow/AlphaInviteOnlyFlowTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.AlphaFlow;

/// <summary>
/// Integration tests for the alpha invite-only registration flow.
/// Verifies: access request → approval → invitation → registration pipeline.
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Authentication")]
[Collection("Database")]
public class AlphaInviteOnlyFlowTests
{
    [Fact]
    public async Task WhenRegistrationDisabled_RequestAccess_Returns202()
    {
        // Arrange: ensure registration is invite-only
        // Act: POST /auth/request-access with email
        // Assert: 202 Accepted (always, for privacy)
    }

    [Fact]
    public async Task WhenAccessRequestApproved_InvitationIsCreated()
    {
        // Arrange: create access request, get admin session
        // Act: POST /admin/access-requests/{id}/approve
        // Assert: InvitationToken created for email (via event handler)
    }

    [Fact]
    public async Task WhenAccessRequestRejected_EmailIsSent()
    {
        // Arrange: create access request, mock email service
        // Act: POST /admin/access-requests/{id}/reject with reason
        // Assert: rejection email sent, access request status = Rejected
    }

    [Fact]
    public async Task WhenOAuthDisabled_OAuthLoginRedirectsToRegister()
    {
        // Arrange: set oauth_login config to false
        // Act: GET /auth/oauth/google/login
        // Assert: 302 redirect to frontend /register?oauth_disabled=true
    }

    [Fact]
    public async Task WhenInvitationAccepted_UserCanRegister()
    {
        // Arrange: create invitation, accept it
        // Act: POST /auth/activate-account with valid token + user details
        // Assert: user created, invitation status = Accepted
    }

    [Fact]
    public async Task WhenInvitationExpired_ValidationFails()
    {
        // Arrange: create invitation with past expiry
        // Act: POST /auth/validate-invitation with expired token
        // Assert: validation fails with appropriate error
    }

    [Fact]
    public async Task DuplicateAccessRequest_ReturnsSame202()
    {
        // Arrange: submit access request for email
        // Act: submit again for same email
        // Assert: 202 (no duplicate created, no error leaked)
    }
}
```

- [ ] **Step 2: Discover existing test infrastructure**

Before implementing test bodies, read existing integration tests in `tests/Api.Tests/BoundedContexts/Authentication/` to understand:
- What base class do integration tests use? (`IClassFixture<WebApplicationFactory<Program>>`?)
- How is admin auth obtained in tests? (Look for admin login helpers)
- How is test data seeded? (In-memory DB? Testcontainers?)
- What assertion patterns are used for HTTP responses?

Key files to read:
- Any existing `*IntegrationTests.cs` in the Authentication tests directory
- The test project's `Usings.cs` or shared fixtures
- Any `TestHelpers/` or `Fixtures/` directory

- [ ] **Step 3: Implement test bodies**

Fill in each test using the discovered test infrastructure. Each test MUST call real endpoints and assert real HTTP status codes and response bodies. No stub test bodies — every test method must have implementation code.

- [ ] **Step 4: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "AlphaInviteOnlyFlowTests" -v normal`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/Authentication/AlphaFlow/
git commit -m "test(auth): add integration tests for alpha invite-only flow"
```

---

## Execution Order

Tasks can be partially parallelized:

```
[Task 1: OAuth flag] ──────────────┐
[Task 2: Rejection email] ─────────┤
[Task 5: Admin notification] ──────┼──→ [Task 8: Integration tests]
[Task 6: AgentDef lifecycle] ──────┤
[Task 7: Rate limiting] ───────────┘
[Task 3: Frontend OAuth] ──────────┤ (depends on Task 1 backend)
[Task 4: Invitation expiry UX] ────┘
```

**Recommended serial order:** 1 → 2 → 7 → 5 → 6 → 3 → 4 → 8

---

## What's Already Working (No Changes Needed)

- ✅ Access request form on register page when invite-only
- ✅ Admin access requests page with approve/reject/bulk
- ✅ Admin invitations page with send/resend/revoke
- ✅ Approve → auto-send invitation (via `AccessRequestApprovedEventHandler`)
- ✅ Invitation accept → registration flow
- ✅ Duplicate access request prevention (silent skip)
- ✅ BGG game search and import
- ✅ SharedGameCatalog bulk import (`BulkImportGamesCommand`)
- ✅ PDF upload and RAG pipeline
- ✅ Agent creation and configuration
- ✅ Admin agent sandbox testing
- ✅ Feature flag system with tier-based access
- ✅ Email service with queue-based delivery

---

## Review Fixes Applied

Issues identified by code reviewer and fixed in this plan:

| # | Severity | Fix |
|---|----------|-----|
| 1 | ❌ CRITICAL | `AccessRequestCreatedEventHandler.cs` — changed from Create to Modify (file exists) |
| 2 | ❌ CRITICAL | Changed `IAlertChannel` to `IAlertingService` (multi-channel DI) |
| 3 | ⚠️ MAJOR | Removed `CancellationToken` from `GetValueAsync` calls (not in signature) |
| 4 | ⚠️ MAJOR | CQRS compliance: extend `RegistrationModeDto` + handler, not endpoint |
| 5 | ⚠️ MAJOR | Keep `publicRegistrationEnabled` field name, extend interface |
| 6 | ⚠️ MAJOR | Added `AgentDefinitionConfiguration.cs` update step |
| 7 | ⚠️ MAJOR | Added data migration for existing active → Published |
| 8 | ⚠️ MAJOR | Documented lifecycle transition rules |
| 9 | ⚠️ MAJOR | Added test infrastructure discovery step |
| 10 | 💡 MINOR | Fixed rate limiting to use `AddPolicy` pattern |
| 11 | 💡 MINOR | Noted queue-based email delivery pattern |
| 12 | 💡 MINOR | Keep `export default` on OAuthButtons |
| 13 | 💡 MINOR | Removed meaningless structural unit test |
