# Onboarding Completion Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add onboarding completion tracking to User entity, wire InterestsStep to existing backend command, create middleware guard for incomplete onboarding, and build reminder banner for skipped users.

**Architecture:** The User domain entity gets three new flags (OnboardingCompleted, OnboardingSkipped, OnboardingCompletedAt). A new CompleteOnboardingCommand persists completion state. The existing SaveUserInterestsCommand (already has handler+validator) gets an HTTP endpoint. Frontend OnboardingWizard calls both endpoints on finish. A Next.js middleware redirects unonboarded users, and a banner reminds skippers.

**Tech Stack:** .NET 9 (CQRS/MediatR, EF Core, FluentValidation) | Next.js 16 (App Router, React 19) | Tailwind 4 + shadcn/ui

**Issues:** #323 (backend flags + command), #325 (middleware guard), #326 (reminder banner)

---

## Chunk 1: Backend — Onboarding Flags + Commands + Endpoints

### Task 1: Add onboarding flags to User entity

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs:58-59` (add properties after Interests)
- Modify: `apps/api/src/Api/Infrastructure/Entities/Authentication/UserEntity.cs:55-56` (add persistence properties)

- [ ] **Step 1: Add onboarding properties to User domain entity**

Open `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`. After line 59 (the `Interests` property), add:

```csharp
// Issue #323: Onboarding completion tracking
public bool OnboardingCompleted { get; private set; }
public bool OnboardingSkipped { get; private set; }
public DateTime? OnboardingCompletedAt { get; private set; }
```

Then add two domain methods after `UpdateInterests()` (around line 666):

```csharp
/// <summary>
/// Marks the user's onboarding as completed.
/// Issue #323: Onboarding completion tracking.
/// </summary>
public void CompleteOnboarding()
{
    OnboardingCompleted = true;
    OnboardingSkipped = false;
    OnboardingCompletedAt = DateTime.UtcNow;
}

/// <summary>
/// Marks the user's onboarding as skipped.
/// Issue #323: Onboarding completion tracking.
/// </summary>
public void SkipOnboarding()
{
    OnboardingCompleted = true;
    OnboardingSkipped = true;
    OnboardingCompletedAt = DateTime.UtcNow;
}
```

- [ ] **Step 2: Add onboarding properties to UserEntity persistence model**

Open `apps/api/src/Api/Infrastructure/Entities/Authentication/UserEntity.cs`. After line 56 (the `Interests` property), add:

```csharp
// Issue #323: Onboarding completion tracking
public bool OnboardingCompleted { get; set; }
public bool OnboardingSkipped { get; set; }
public DateTime? OnboardingCompletedAt { get; set; }
```

- [ ] **Step 3: Add OnboardingCompleted to UserDto**

Open `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/UserDto.cs`. Add to the `UserDto` record (after `VerificationGracePeriodEndsAt`):

```csharp
    bool OnboardingCompleted = false,                    // Issue #323
    bool OnboardingSkipped = false                       // Issue #323
```

- [ ] **Step 4: Create EF Core migration**

Run:
```bash
cd apps/api/src/Api && dotnet ef migrations add AddOnboardingFlags
```

Expected: Migration file created with `OnboardingCompleted` (bool, default false), `OnboardingSkipped` (bool, default false), `OnboardingCompletedAt` (DateTime?, nullable) columns added to Users table.

- [ ] **Step 5: Verify build compiles**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded. 0 Warning(s). 0 Error(s).

**Note:** The `GetUserProfileQuery` handler and `SessionStatusDto` mapper will need to include the new fields wherever they map `User` → `UserDto`. Search for where `UserDto` is constructed and ensure `OnboardingCompleted` and `OnboardingSkipped` are passed through. Check the query handler that builds `UserDto` from the User entity.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs
git add apps/api/src/Api/Infrastructure/Entities/Authentication/UserEntity.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/UserDto.cs
git add apps/api/src/Api/Migrations/
git commit -m "feat(auth): add onboarding completion flags to User entity (#323)"
```

### Task 2: Create CompleteOnboardingCommand + Handler + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommandValidator.cs`

- [ ] **Step 1: Create CompleteOnboardingCommand**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Marks a user's onboarding as completed or skipped.
/// Issue #323: Onboarding completion tracking.
/// </summary>
internal record CompleteOnboardingCommand(
    Guid UserId,
    bool Skipped
) : ICommand;
```

- [ ] **Step 2: Create CompleteOnboardingCommandHandler**

```csharp
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Handles marking onboarding as completed or skipped.
/// Issue #323: Onboarding completion tracking.
/// </summary>
internal sealed class CompleteOnboardingCommandHandler : ICommandHandler<CompleteOnboardingCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteOnboardingCommandHandler> _logger;

    public CompleteOnboardingCommandHandler(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        ILogger<CompleteOnboardingCommandHandler> logger)
    {
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CompleteOnboardingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userRepo.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", command.UserId.ToString());

        if (command.Skipped)
            user.SkipOnboarding();
        else
            user.CompleteOnboarding();

        await _userRepo.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Onboarding {Status} for user {UserId}",
            command.Skipped ? "skipped" : "completed", command.UserId);
    }
}
```

- [ ] **Step 3: Create CompleteOnboardingCommandValidator**

```csharp
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Validates CompleteOnboardingCommand.
/// Issue #323: Onboarding completion tracking.
/// </summary>
internal sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
```

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Onboarding/
git commit -m "feat(auth): add CompleteOnboardingCommand with handler and validator (#323)"
```

### Task 3: Register HTTP endpoints for onboarding + interests

**Files:**
- Modify: `apps/api/src/Api/Routing/UserProfileEndpoints.cs:29-43` (add new endpoint mappings)

- [ ] **Step 1: Add endpoint methods to UserProfileEndpoints.cs**

In `UserProfileEndpoints.cs`, add to `MapUserProfileEndpoints()` method (after line 40, before `return group;`):

```csharp
        // Issue #323: Onboarding completion
        MapCompleteOnboardingEndpoint(group);
        // Issue #124: Save user interests from onboarding wizard
        MapSaveUserInterestsEndpoint(group);
```

Then add the two private methods before the closing `}` of the class (before the payload records at line 529):

```csharp
    private static void MapCompleteOnboardingEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/users/onboarding/complete", async (
            [FromBody] CompleteOnboardingPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new Api.BoundedContexts.Authentication.Application.Commands.Onboarding.CompleteOnboardingCommand(
                UserId: session!.User!.Id,
                Skipped: payload.Skipped
            );

            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Onboarding {Status} for user {UserId}",
                payload.Skipped ? "skipped" : "completed", session.User.Id);

            return Results.Json(new { ok = true, message = payload.Skipped ? "Onboarding skipped" : "Onboarding completed" });
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("CompleteOnboarding")
        .WithTags("User Profile", "Onboarding")
        .WithSummary("Mark onboarding as completed or skipped")
        .WithDescription(@"Marks the authenticated user's onboarding wizard as completed or skipped.

**Issue**: #323 - Onboarding completion tracking.

**Authorization**: Requires active session (cookie-based authentication).

**Request Body**: CompleteOnboardingPayload with skipped flag.

**Response**: Success confirmation.")
        .Produces(200)
        .Produces(401);
    }

    private static void MapSaveUserInterestsEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/users/interests", async (
            [FromBody] SaveUserInterestsPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new Api.BoundedContexts.Authentication.Application.Commands.UserProfile.SaveUserInterestsCommand(
                UserId: session!.User!.Id,
                Interests: payload.Interests
            );

            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Saved {Count} interests for user {UserId}",
                payload.Interests?.Count ?? 0, session.User.Id);

            return Results.Json(new { ok = true, message = "Interests saved successfully" });
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("SaveUserInterests")
        .WithTags("User Profile", "Onboarding")
        .WithSummary("Save user's board game interests")
        .WithDescription(@"Saves the user's board game category interests from the onboarding wizard.

**Issue**: #124 - Invitation system onboarding wizard.

**Authorization**: Requires active session (cookie-based authentication).

**Request Body**: SaveUserInterestsPayload with list of interest category IDs.
Valid categories: Strategy, Party, Cooperative, Family, Thematic, Abstract, Card, Dice, Miniatures.

**Response**: Success confirmation.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }
```

Add the payload records at the bottom of the file (after existing payload records):

```csharp
/// <summary>
/// Payload for completing onboarding. Issue #323.
/// </summary>
internal record CompleteOnboardingPayload(bool Skipped = false);

/// <summary>
/// Payload for saving user interests. Issue #124.
/// </summary>
internal record SaveUserInterestsPayload(List<string> Interests);
```

- [ ] **Step 2: Add using directive**

Add at the top of `UserProfileEndpoints.cs` (with existing usings):

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Onboarding;
```

Wait — this isn't needed since we use the fully qualified name in the endpoint. The `SessionStatusDto` using is already imported. Skip this step.

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/UserProfileEndpoints.cs
git commit -m "feat(auth): register onboarding completion and interests endpoints (#323)"
```

### Task 4: Ensure UserDto mapping includes onboarding flags

**Files:**
- Search for where `UserDto` is constructed from `User` entity (likely in a mapper or query handler)

- [ ] **Step 1: Find and update UserDto construction**

Search for all places where `new UserDto(` is used:

```bash
cd apps/api/src/Api && grep -rn "new UserDto(" --include="*.cs"
```

For each location, add `OnboardingCompleted: user.OnboardingCompleted, OnboardingSkipped: user.OnboardingSkipped` to the constructor call. Since `UserDto` uses default values (`= false`), existing code that doesn't pass these fields will still compile, but update the primary mapper(s) for correctness.

- [ ] **Step 2: Verify build and commit**

Run: `cd apps/api/src/Api && dotnet build`

```bash
git add -A
git commit -m "feat(auth): include onboarding flags in UserDto mappings (#323)"
```

---

## Chunk 2: Frontend — Wire Wizard to API + Middleware Guard + Banner

### Task 5: Add API client methods for onboarding

**Files:**
- Modify: `apps/web/src/lib/api/clients/authClient.ts:419` (add after preferences section)

- [ ] **Step 1: Add onboarding API methods to authClient**

Open `apps/web/src/lib/api/clients/authClient.ts`. After the `updatePreferences` method (around line 419), add:

```typescript
    // ========== Onboarding (Issue #323) ==========

    /**
     * Mark onboarding as completed or skipped
     * POST /api/v1/users/onboarding/complete
     */
    async completeOnboarding(skipped: boolean = false): Promise<{ ok: boolean; message: string }> {
      return httpClient.post('/api/v1/users/onboarding/complete', { skipped }, z.object({
        ok: z.boolean(),
        message: z.string(),
      }));
    },

    /**
     * Save user interests from onboarding wizard
     * POST /api/v1/users/interests
     */
    async saveInterests(interests: string[]): Promise<{ ok: boolean; message: string }> {
      return httpClient.post('/api/v1/users/interests', { interests }, z.object({
        ok: z.boolean(),
        message: z.string(),
      }));
    },
```

Ensure `z` (from `zod`) is already imported at the top of the file. It should be since other methods use schema validation.

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/clients/authClient.ts
git commit -m "feat(web): add onboarding API client methods (#323)"
```

### Task 6: Wire OnboardingWizard to call completion API

**Files:**
- Modify: `apps/web/src/components/onboarding/OnboardingWizard.tsx:87-95` (handleFinish and handleSkipWizard)
- Modify: `apps/web/src/components/onboarding/InterestsStep.tsx:60-63` (wire API call)

- [ ] **Step 1: Update OnboardingWizard handleFinish and handleSkipWizard**

Open `apps/web/src/components/onboarding/OnboardingWizard.tsx`.

Add import at top (after existing imports):
```typescript
import { api } from '@/lib/api';
```

Replace `handleFinish` (lines 87-89):
```typescript
  const handleFinish = useCallback(async () => {
    try {
      await api.auth.completeOnboarding(false);
    } catch {
      // Non-critical — proceed even if tracking fails
    }
    router.push('/dashboard');
  }, [router]);
```

Replace `handleSkipWizard` (lines 91-95):
```typescript
  const handleSkipWizard = useCallback(async () => {
    if (state.passwordCompleted) {
      try {
        await api.auth.completeOnboarding(true);
      } catch {
        // Non-critical — proceed even if tracking fails
      }
      router.push('/dashboard');
    }
  }, [router, state.passwordCompleted]);
```

Note: redirect to `/dashboard` instead of `/chat` — dashboard is the standard post-auth landing.

- [ ] **Step 2: Wire InterestsStep to save interests via API**

Open `apps/web/src/components/onboarding/InterestsStep.tsx`.

Add import at top:
```typescript
import { api } from '@/lib/api';
```

Replace lines 60-63 in `handleSubmit`:
```typescript
      // Issue #323: Save interests to backend
      await api.auth.saveInterests(Array.from(selected));
      toast.success(`${selected.size} interests saved!`);
      onComplete();
```

- [ ] **Step 3: Verify frontend build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/onboarding/OnboardingWizard.tsx
git add apps/web/src/components/onboarding/InterestsStep.tsx
git commit -m "feat(web): wire onboarding wizard to completion and interests APIs (#323)"
```

### Task 7: Create Next.js middleware for onboarding guard (#325)

**Files:**
- Create: `apps/web/src/middleware.ts`

- [ ] **Step 1: Create middleware file**

Create `apps/web/src/middleware.ts`:

```typescript
import { NextResponse } from 'next/server';

import type { NextRequest } from 'next/server';

/**
 * Next.js Middleware — Onboarding Guard
 * Issue #325: Redirect users who haven't completed onboarding.
 *
 * Strategy: Check the session cookie for onboarding status.
 * If user is authenticated but hasn't completed onboarding,
 * redirect to /accept-invite (the onboarding entry point).
 *
 * Routes excluded from guard:
 * - /accept-invite (the onboarding page itself)
 * - /api/* (API routes)
 * - /_next/* (Next.js internals)
 * - /login, /register, /forgot-password (auth pages)
 * - / (public landing page)
 * - /games (public catalog)
 * - Static assets
 */

const PUBLIC_PATHS = [
  '/',
  '/login',
  '/register',
  '/forgot-password',
  '/reset-password',
  '/games',
  '/accept-invite',
  '/verify-email',
];

const EXCLUDED_PREFIXES = [
  '/api/',
  '/_next/',
  '/static/',
  '/favicon',
  '/og-image',
  '/twitter-card',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Skip excluded prefixes
  if (EXCLUDED_PREFIXES.some(prefix => pathname.startsWith(prefix))) {
    return NextResponse.next();
  }

  // Skip public paths
  if (PUBLIC_PATHS.includes(pathname)) {
    return NextResponse.next();
  }

  // Check for onboarding status from cookie
  // The session validation API sets this cookie after login
  const onboardingComplete = request.cookies.get('onboarding_completed')?.value;

  // If cookie is explicitly 'false', redirect to onboarding
  // If cookie is missing, user hasn't logged in via the new flow yet — don't block
  if (onboardingComplete === 'false') {
    const onboardingUrl = new URL('/accept-invite', request.url);
    onboardingUrl.searchParams.set('redirect', pathname);
    return NextResponse.redirect(onboardingUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - favicon.ico (favicon file)
     */
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
```

**Important design note:** The middleware uses a cookie-based approach rather than making an API call on every request (which would be too slow). The `onboarding_completed` cookie should be set by the session validation endpoint when it returns user data. This is a pragmatic approach — the backend `SessionStatusDto` already returns `UserDto` which will now include `OnboardingCompleted`. The frontend auth provider should set this cookie when it receives session data.

- [ ] **Step 2: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/middleware.ts
git commit -m "feat(web): add onboarding middleware guard (#325)"
```

### Task 8: Build OnboardingReminderBanner component (#326)

**Files:**
- Create: `apps/web/src/components/onboarding/OnboardingReminderBanner.tsx`
- Modify: `apps/web/src/components/layout/AppShellClient.tsx` (add banner rendering)

- [ ] **Step 1: Create OnboardingReminderBanner component**

Create `apps/web/src/components/onboarding/OnboardingReminderBanner.tsx`:

```tsx
'use client';

/**
 * OnboardingReminderBanner
 * Issue #326: Reminder banner for users who skipped onboarding wizard.
 *
 * Shows a subtle, dismissible banner at the top of the app shell
 * encouraging users to complete their profile setup.
 */

import { useState } from 'react';

import { X } from 'lucide-react';

interface OnboardingReminderBannerProps {
  onDismiss?: () => void;
}

export function OnboardingReminderBanner({ onDismiss }: OnboardingReminderBannerProps) {
  const [dismissed, setDismissed] = useState(false);

  if (dismissed) return null;

  const handleDismiss = () => {
    setDismissed(true);
    // Persist dismissal in localStorage to avoid showing again this session
    try {
      localStorage.setItem('onboarding_banner_dismissed', 'true');
    } catch {
      // localStorage not available
    }
    onDismiss?.();
  };

  return (
    <div
      className="flex items-center justify-between gap-4 border-b border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/50 dark:text-amber-200"
      role="status"
      aria-label="Onboarding reminder"
      data-testid="onboarding-reminder-banner"
    >
      <p>
        <span className="font-medium">Complete your profile setup</span>
        {' — '}
        <a
          href="/accept-invite"
          className="underline hover:text-amber-700 dark:hover:text-amber-100"
        >
          Finish the onboarding wizard
        </a>
        {' '}
        to get personalized game recommendations.
      </p>
      <button
        type="button"
        onClick={handleDismiss}
        className="shrink-0 rounded p-1 hover:bg-amber-100 dark:hover:bg-amber-900"
        aria-label="Dismiss onboarding reminder"
        data-testid="dismiss-onboarding-banner"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
```

- [ ] **Step 2: Export from onboarding index**

Open `apps/web/src/components/onboarding/index.ts` and add:

```typescript
export { OnboardingReminderBanner } from './OnboardingReminderBanner';
```

- [ ] **Step 3: Integrate banner into AppShellClient**

Open `apps/web/src/components/layout/AppShellClient.tsx`. This requires:

1. Import the banner and the auth/user context hook
2. Conditionally render the banner above the main content when user has `onboardingSkipped === true` and hasn't dismissed it

Add import:
```typescript
import { OnboardingReminderBanner } from '@/components/onboarding';
```

Add the banner rendering just before the `<main>` tag. The exact placement depends on the component structure — place it inside the content area, before the main content wrapper, so it appears as a top bar within the authenticated layout.

**Conditional rendering logic:** The banner should only show when the authenticated user's `onboardingSkipped` flag is `true`. Read this from the user session/auth state that's already available in the app shell context.

- [ ] **Step 4: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/OnboardingReminderBanner.tsx
git add apps/web/src/components/onboarding/index.ts
git add apps/web/src/components/layout/AppShellClient.tsx
git commit -m "feat(web): add onboarding reminder banner for skipped users (#326)"
```

---

## Chunk 3: Tests + Issue Closure

### Task 9: Write backend unit tests

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommandHandlerTests.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommandValidatorTests.cs`

- [ ] **Step 1: Write handler tests**

Test cases:
1. `Handle_WhenUserExists_ShouldCompleteOnboarding` — verify `OnboardingCompleted=true`, `OnboardingSkipped=false`, `OnboardingCompletedAt` set
2. `Handle_WhenSkipped_ShouldSkipOnboarding` — verify `OnboardingSkipped=true`
3. `Handle_WhenUserNotFound_ShouldThrowNotFoundException`

Follow existing test patterns in `tests/Api.Tests/BoundedContexts/Authentication/`. Use `[Trait("Category", TestCategories.Unit)]` on all tests.

- [ ] **Step 2: Write validator tests**

Test cases:
1. `Validate_WhenUserIdEmpty_ShouldFail`
2. `Validate_WhenUserIdValid_ShouldPass`

- [ ] **Step 3: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "CompleteOnboarding"`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Onboarding/
git commit -m "test(auth): add CompleteOnboarding command handler and validator tests (#323)"
```

### Task 10: Write frontend tests

**Files:**
- Create: `apps/web/src/components/onboarding/__tests__/OnboardingReminderBanner.test.tsx`

- [ ] **Step 1: Write banner tests**

Test cases:
1. `renders banner with reminder text`
2. `dismiss button hides banner`
3. `dismiss sets localStorage flag`
4. `has correct accessibility attributes`
5. `links to onboarding page`

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm test -- --run OnboardingReminderBanner`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/onboarding/__tests__/OnboardingReminderBanner.test.tsx
git commit -m "test(web): add OnboardingReminderBanner tests (#326)"
```

### Task 11: Close issues and create PR

- [ ] **Step 1: Run full test suite**

```bash
cd apps/api/src/Api && dotnet build
cd apps/web && pnpm build && pnpm test --run
```

- [ ] **Step 2: Push branch and create PR**

```bash
git push -u origin feature/issue-323-onboarding-completion
```

Create PR to `main-dev` (parent branch):

```
Title: feat(auth): onboarding completion tracking, middleware guard, reminder banner (#323, #325, #326)

Body:
## Summary
- Add OnboardingCompleted/OnboardingSkipped flags to User entity with EF migration
- Create CompleteOnboardingCommand + handler + validator (CQRS)
- Register HTTP endpoints: POST /users/onboarding/complete, POST /users/interests
- Wire OnboardingWizard to call completion API on finish/skip
- Wire InterestsStep to save interests via existing SaveUserInterestsCommand
- Add Next.js middleware guard for unonboarded users
- Build dismissible OnboardingReminderBanner for skipped users
- Full test coverage for new backend commands and frontend components

## Issues
Closes #323, #325, #326
Completes Epic #311

## Test Plan
- [ ] Backend: CompleteOnboardingCommand handler + validator tests pass
- [ ] Frontend: OnboardingReminderBanner component tests pass
- [ ] Build: Both `dotnet build` and `pnpm build` succeed
- [ ] E2E: Onboarding wizard completes and marks user as onboarded
```

- [ ] **Step 3: Close issues on GitHub**

After PR is merged:
```bash
gh issue close 323 -c "Implemented: onboarding flags + CompleteOnboardingCommand + endpoint"
gh issue close 325 -c "Implemented: Next.js middleware guard for unonboarded users"
gh issue close 326 -c "Implemented: OnboardingReminderBanner component"
gh issue close 311 -c "Epic complete: all 4 issues resolved (#323-#326)"
```
