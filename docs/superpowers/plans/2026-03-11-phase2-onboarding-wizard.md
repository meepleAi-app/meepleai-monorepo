# Phase 2: Onboarding Wizard — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** New invited users see a 3-step wizard (add game → create agent → chat with agent) after setting their password. Each step is skippable, the entire wizard is skippable.

**Architecture:** Add `OnboardingCompleted`, `OnboardingSkipped` flags to User entity. New `CompleteOnboardingCommand`. Frontend wizard at `/onboarding` with Next.js middleware guard. Uses existing endpoints for game addition, agent creation, and chat.

**Tech Stack:** .NET 9, EF Core, MediatR, Next.js 16, React 19, Zustand (wizard state), Zod

**Spec:** `docs/superpowers/specs/2026-03-11-admin-invite-onboarding-design.md` — Phase 2

**Depends on:** Phase 1 (MustChangePassword flag, invited user flow, `/change-password` page)

---

## File Structure

### Backend — Modified Files
| File | Change |
|------|--------|
| `Authentication/Domain/Entities/User.cs` | Add `OnboardingCompleted`, `OnboardingSkipped`, `OnboardingCompletedAt`, `SkippedSteps` |

### Backend — New Files
| File | Responsibility |
|------|---------------|
| `Authentication/Application/Commands/Onboarding/CompleteOnboardingCommand.cs` | Command + handler |
| `Authentication/Application/Validators/CompleteOnboardingCommandValidator.cs` | FluentValidation |
| `Routing/OnboardingEndpoints.cs` | POST /api/v1/auth/complete-onboarding |

### Frontend — New Files
| File | Responsibility |
|------|---------------|
| `app/(auth)/onboarding/page.tsx` | Next.js page wrapper |
| `app/(auth)/onboarding/_content.tsx` | Wizard container with step navigation |
| `app/(auth)/onboarding/layout.tsx` | Onboarding layout (no sidebar/nav) |
| `components/onboarding/OnboardingWizard.tsx` | Main wizard component with step management |
| `components/onboarding/steps/AddGameStep.tsx` | Step 1: search + add game |
| `components/onboarding/steps/CreateAgentStep.tsx` | Step 2: simplified agent creation |
| `components/onboarding/steps/TryAgentStep.tsx` | Step 3: mini-chat with agent |
| `components/onboarding/OnboardingProgress.tsx` | Progress dots (3 steps) |
| `components/onboarding/SkipWizardDialog.tsx` | Confirmation dialog for skipping all |
| `components/onboarding/OnboardingReminderBanner.tsx` | Dismissible banner for skipped wizard |
| `hooks/useOnboardingGuard.ts` | Client-side redirect logic |
| `apps/web/src/middleware.ts` | Redirect to /onboarding if not completed |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `app/(auth)/reset-password/_content.tsx` | Redirect to /onboarding after forced password change |

### Tests
| File | Scope |
|------|-------|
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommandHandlerTests.cs` | Unit: handler |
| `apps/web/src/__tests__/onboarding/OnboardingWizard.test.tsx` | Frontend: wizard flow |
| `apps/web/src/__tests__/onboarding/AddGameStep.test.tsx` | Frontend: step 1 |
| `apps/web/src/__tests__/onboarding/CreateAgentStep.test.tsx` | Frontend: step 2 |
| `apps/web/src/__tests__/onboarding/TryAgentStep.test.tsx` | Frontend: step 3 |

### Migration
| File | Description |
|------|-------------|
| `Infrastructure/Migrations/{timestamp}_AddOnboardingFieldsToUser.cs` | Auto-generated |

---

## Chunk 1: Backend (User entity + Command + Endpoint)

### Task 1: Add onboarding fields to User entity

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`

- [ ] **Step 1: Read User.cs**
- [ ] **Step 2: Add properties**

```csharp
public bool OnboardingCompleted { get; private set; } = true; // true for existing users
public bool OnboardingSkipped { get; private set; }
public DateTime? OnboardingCompletedAt { get; private set; }
public string[]? SkippedSteps { get; private set; }

public void CompleteOnboarding(bool skipped, string[]? skippedSteps = null)
{
    OnboardingCompleted = true;
    OnboardingSkipped = skipped;
    OnboardingCompletedAt = DateTime.UtcNow;
    SkippedSteps = skippedSteps;
}
```

- [ ] **Step 3: Verify build**: `cd apps/api/src/Api && dotnet build --no-restore`
- [ ] **Step 4: Commit**

### Task 2: Create CompleteOnboardingCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Onboarding/CompleteOnboardingCommand.cs`

- [ ] **Step 1: Write test**

```csharp
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class CompleteOnboardingCommandTests
{
    [Fact]
    public async Task Handle_WithValidUser_ShouldMarkOnboardingCompleted()
    {
        // Setup mocks, create handler, execute, assert OnboardingCompleted = true
    }

    [Fact]
    public async Task Handle_WithSkippedSteps_ShouldSetOnboardingSkipped()
    {
        // Execute with skipped=true, assert OnboardingSkipped = true
    }
}
```

- [ ] **Step 2: Write command + handler**

```csharp
internal record CompleteOnboardingCommand(
    Guid UserId,
    bool Skipped = false,
    string[]? SkippedSteps = null
) : ICommand<bool>;

internal class CompleteOnboardingCommandHandler : ICommandHandler<CompleteOnboardingCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteOnboardingCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(CompleteOnboardingCommand command, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, ct).ConfigureAwait(false);
        if (user == null) throw new NotFoundException($"User {command.UserId} not found");

        user.CompleteOnboarding(command.Skipped, command.SkippedSteps);
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
```

- [ ] **Step 3: Run tests**
- [ ] **Step 4: Commit**

### Task 3: Create endpoint + migration

- [ ] **Step 1: Write OnboardingEndpoints.cs**

```csharp
internal static class OnboardingEndpoints
{
    public static RouteGroupBuilder MapOnboardingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/complete-onboarding", HandleCompleteOnboarding)
            .WithName("CompleteOnboarding")
            .WithTags("Auth", "Onboarding")
            .Produces(StatusCodes.Status200OK);
        return group;
    }

    private static async Task<IResult> HandleCompleteOnboarding(
        CompleteOnboardingRequest request, HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAuthenticatedSession();
        if (!authorized) return error!;

        await mediator.Send(new CompleteOnboardingCommand(
            session!.User!.Id, request.Skipped, request.SkippedSteps), ct).ConfigureAwait(false);
        return Results.Ok();
    }
}

internal record CompleteOnboardingRequest(bool Skipped = false, string[]? SkippedSteps = null);
```

- [ ] **Step 2: Generate migration**: `dotnet ef migrations add AddOnboardingFieldsToUser`
- [ ] **Step 3: Register endpoint**: add `v1Api.MapOnboardingEndpoints()` in `Program.cs`
- [ ] **Step 4: Commit**

### Task 3b: Create CompleteOnboardingCommandValidator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/CompleteOnboardingCommandValidator.cs`

- [ ] **Step 1: Write validator**

```csharp
using FluentValidation;

internal class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
```

- [ ] **Step 2: Run tests**: `cd apps/api && dotnet test --filter "BoundedContext=Authentication"`
- [ ] **Step 3: Commit**

---

## Chunk 2: Frontend Wizard

### Task 4: Create Next.js middleware for onboarding guard

**Files:**
- Create: `apps/web/src/middleware.ts`

- [ ] **Step 1: Write middleware**

```typescript
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const PUBLIC_PATHS = [
  '/login', '/register', '/reset-password', '/accept-invite',
  '/logout', '/api/', '/onboarding', '/_next/', '/favicon',
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Skip public paths
  if (PUBLIC_PATHS.some(p => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Check onboarding status from cookie/header
  const onboardingCompleted = request.cookies.get('onboarding_completed')?.value;
  if (onboardingCompleted === 'false') {
    return NextResponse.redirect(new URL('/onboarding', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
};
```

- [ ] **Step 2: Commit**

### Task 5: Create OnboardingWizard component

**Files:**
- Create: `apps/web/src/components/onboarding/OnboardingWizard.tsx`
- Create: `apps/web/src/components/onboarding/OnboardingProgress.tsx`

- [ ] **Step 1: Write OnboardingProgress**

```tsx
'use client';

interface OnboardingProgressProps {
  currentStep: number;
  totalSteps: number;
  stepLabels: string[];
}

export function OnboardingProgress({ currentStep, totalSteps, stepLabels }: OnboardingProgressProps) {
  return (
    <div className="flex items-center justify-center gap-3 py-4">
      {Array.from({ length: totalSteps }, (_, i) => (
        <div key={i} className="flex items-center gap-2">
          <div
            className={`h-3 w-3 rounded-full transition-all ${
              i < currentStep ? 'bg-amber-500' :
              i === currentStep ? 'bg-amber-500 ring-4 ring-amber-500/20' :
              'bg-gray-200'
            }`}
          />
          <span className={`text-sm font-nunito ${
            i === currentStep ? 'text-foreground font-medium' : 'text-muted-foreground'
          }`}>
            {stepLabels[i]}
          </span>
          {i < totalSteps - 1 && <div className="h-px w-8 bg-gray-200" />}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Write OnboardingWizard**

```tsx
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { OnboardingProgress } from './OnboardingProgress';
import { AddGameStep } from './steps/AddGameStep';
import { CreateAgentStep } from './steps/CreateAgentStep';
import { TryAgentStep } from './steps/TryAgentStep';
import { SkipWizardDialog } from './SkipWizardDialog';
import { Button } from '@/components/ui/primitives/button';
import { createApiClient } from '@/lib/api';
import { X } from 'lucide-react';

const STEP_LABELS = ['Aggiungi gioco', 'Crea agente', 'Prova agente'];

export function OnboardingWizard() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(0);
  const [skippedSteps, setSkippedSteps] = useState<string[]>([]);
  const [skipDialogOpen, setSkipDialogOpen] = useState(false);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [createdAgentId, setCreatedAgentId] = useState<string | null>(null);

  const completeOnboarding = async (skipped: boolean) => {
    const api = createApiClient();
    await api.auth.completeOnboarding({ skipped, skippedSteps });
    router.push('/dashboard');
  };

  const skipStep = () => {
    setSkippedSteps(prev => [...prev, STEP_LABELS[currentStep]]);
    if (currentStep < 2) {
      setCurrentStep(prev => prev + 1);
    } else {
      completeOnboarding(false);
    }
  };

  const nextStep = () => {
    if (currentStep < 2) {
      setCurrentStep(prev => prev + 1);
    } else {
      completeOnboarding(false);
    }
  };

  const skipAll = () => {
    completeOnboarding(true);
  };

  return (
    <div className="min-h-screen bg-white/70 backdrop-blur-md">
      <div className="max-w-2xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <h1 className="font-quicksand text-2xl font-bold">Benvenuto su MeepleAI</h1>
          <Button variant="ghost" size="sm" onClick={() => setSkipDialogOpen(true)}>
            <X className="h-4 w-4 mr-1" /> Salta il wizard
          </Button>
        </div>

        <OnboardingProgress currentStep={currentStep} totalSteps={3} stepLabels={STEP_LABELS} />

        {/* Step content */}
        <div className="mt-8">
          {currentStep === 0 && (
            <AddGameStep
              onGameSelected={(gameId) => setSelectedGameId(gameId)}
              onNext={nextStep}
              onSkip={skipStep}
            />
          )}
          {currentStep === 1 && (
            <CreateAgentStep
              gameId={selectedGameId}
              onAgentCreated={(agentId) => setCreatedAgentId(agentId)}
              onNext={nextStep}
              onSkip={skipStep}
            />
          )}
          {currentStep === 2 && (
            <TryAgentStep
              agentId={createdAgentId}
              onComplete={() => completeOnboarding(false)}
              onSkip={skipStep}
            />
          )}
        </div>

        <SkipWizardDialog
          open={skipDialogOpen}
          onOpenChange={setSkipDialogOpen}
          onConfirm={skipAll}
        />
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Commit**

### Task 6: Create step components

**Files:**
- Create: `apps/web/src/components/onboarding/steps/AddGameStep.tsx`
- Create: `apps/web/src/components/onboarding/steps/CreateAgentStep.tsx`
- Create: `apps/web/src/components/onboarding/steps/TryAgentStep.tsx`
- Create: `apps/web/src/components/onboarding/SkipWizardDialog.tsx`

- [ ] **Step 1: Write AddGameStep** — Search SharedGame catalog, click to add to collection. Uses existing `api.games` or `api.sharedGames` client.

- [ ] **Step 2: Write CreateAgentStep** — Simplified agent form. Auto-populates name from game. Uses existing `api.agentDefinitions.create()`.

- [ ] **Step 3: Write TryAgentStep** — Mini-chat inline. Pre-filled suggestions. Uses existing chat SSE endpoint.

- [ ] **Step 4: Write SkipWizardDialog** — Confirmation: "Puoi trovare queste funzionalita nella dashboard quando vuoi"

- [ ] **Step 5: Write tests**
- [ ] **Step 6: Commit**

### Task 6b: Add completeOnboarding to frontend auth API client

**Files:**
- Modify: `apps/web/src/lib/api/auth.ts` (or wherever the auth client is defined)

- [ ] **Step 1: Add `completeOnboarding` method**

```typescript
async completeOnboarding(data: { skipped: boolean; skippedSteps: string[] }): Promise<void> {
  await this.httpClient.post('/api/v1/auth/complete-onboarding', data);
}
```

- [ ] **Step 2: Verify types and build**: `cd apps/web && pnpm typecheck`
- [ ] **Step 3: Commit**

### Task 7: Create onboarding page + layout

- [ ] **Step 1: Write page.tsx and _content.tsx**
- [ ] **Step 2: Write layout.tsx** (minimal, no sidebar)
- [ ] **Step 3: Update reset-password redirect** to go to `/onboarding` when `mode=forced`
- [ ] **Step 4: Run full build**: `pnpm build`
- [ ] **Step 5: Commit**

### Task 7b: Create OnboardingReminderBanner component

**Files:**
- Create: `apps/web/src/components/onboarding/OnboardingReminderBanner.tsx`

- [ ] **Step 1: Write component** — Dismissible banner shown on dashboard if wizard was skipped. Dismiss state stored in localStorage (`onboarding_banner_dismissed`).

```tsx
'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/primitives/button';
import { X } from 'lucide-react';

const STORAGE_KEY = 'onboarding_banner_dismissed';

export function OnboardingReminderBanner() {
  const router = useRouter();
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const dismissed = localStorage.getItem(STORAGE_KEY);
    if (!dismissed) setVisible(true);
  }, []);

  const dismiss = () => {
    localStorage.setItem(STORAGE_KEY, 'true');
    setVisible(false);
  };

  if (!visible) return null;

  return (
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 flex items-center justify-between">
      <p className="font-nunito text-sm text-amber-900">
        Non hai completato il setup — riprendi da dove eri rimasto
      </p>
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={() => router.push('/onboarding')}>
          Riprendi
        </Button>
        <Button variant="ghost" size="icon" onClick={dismiss}>
          <X className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Add banner to dashboard page** — Conditionally render when `onboardingSkipped` is true in user session.
- [ ] **Step 3: Commit**

### Task 8: Run full test suite

- [ ] **Step 1: Backend tests**: `cd apps/api && dotnet test --filter "BoundedContext=Authentication"`
- [ ] **Step 2: Frontend tests**: `cd apps/web && pnpm test -- --run`
- [ ] **Step 3: Fix any issues and commit**
