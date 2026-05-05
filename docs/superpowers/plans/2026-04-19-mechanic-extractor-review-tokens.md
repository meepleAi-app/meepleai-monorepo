# Mechanic Extractor — Review Page & Token Tracking

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Review Page route (preview + PDF export), token tracking on AI assist calls, RowVersion concurrency on MechanicDraft, and the copyright firewall test.

**Architecture:** The Mechanic Extractor already has a complete CQRS backend (5 endpoints) and a working editor page. This plan adds 4 incremental features: (1) a frontend-only review page that reads the same `GetMechanicDraftQuery`, (2) token/cost fields persisted per draft, (3) RowVersion for optimistic concurrency, (4) a DI-level copyright firewall test.

**Tech Stack:** .NET 9 (EF Core, MediatR, FluentValidation) | Next.js (React 19, Tailwind 4, shadcn/ui) | PostgreSQL 16 | Vitest

---

## File Map

### Backend — New/Modified

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraft.cs` | Add `TotalTokensUsed`, `EstimatedCostUsd`, `RowVersion` fields |
| Modify | `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs` | Add persistence properties |
| Modify | `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs` | Map new columns + RowVersion |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicDraftDto.cs` | Add `TotalTokensUsed`, `EstimatedCostUsd` |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/AiAssistMechanicDraftCommandHandler.cs` | Return tokens/cost in DTO, persist to draft |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/AiAssistMechanicDraftCommand.cs` | No change needed (GameTitle already present) |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/SaveMechanicDraftCommandHandler.cs` | Catch `DbUpdateConcurrencyException` |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/AcceptMechanicDraftCommandHandler.cs` | Catch `DbUpdateConcurrencyException` |
| Modify | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicDraftRepository.cs` | Map new fields in ToDomain/ToEntity |
| Create | EF Migration `AddMechanicDraftTokensAndRowVersion` | Add columns `total_tokens_used`, `estimated_cost_usd`, `row_version` |

### Backend — Tests

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraftTests.cs` | Test token tracking + RowVersion |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/CopyrightFirewallTests.cs` | DI reflection check |

### Frontend — New/Modified

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/lib/api/schemas/mechanic-extractor.schemas.ts` | Add `totalTokensUsed`, `estimatedCostUsd`, review route |
| Create | `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx` | Review page matching mockup |
| Create | `apps/web/src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx` | Review page tests |
| Modify | `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx` | Add "Preview" link, show token count |

---

## Task 1: Domain — Token Tracking Fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraft.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraftTests.cs`

- [ ] **Step 1: Add fields to MechanicDraft domain entity**

Add after the `_status` field (line ~37):

```csharp
private int _totalTokensUsed;
private decimal _estimatedCostUsd;
private byte[] _rowVersion = Array.Empty<byte>();
```

Add public properties after the existing properties:

```csharp
public int TotalTokensUsed => _totalTokensUsed;
public decimal EstimatedCostUsd => _estimatedCostUsd;
public byte[] RowVersion => _rowVersion;
```

Update the internal constructor to accept and assign the new fields:

```csharp
internal MechanicDraft(
    Guid id,
    Guid sharedGameId,
    Guid pdfDocumentId,
    Guid createdBy,
    string gameTitle,
    string summaryNotes,
    string mechanicsNotes,
    string victoryNotes,
    string resourcesNotes,
    string phasesNotes,
    string questionsNotes,
    string summaryDraft,
    string mechanicsDraft,
    string victoryDraft,
    string resourcesDraft,
    string phasesDraft,
    string questionsDraft,
    DateTime createdAt,
    DateTime lastModified,
    MechanicDraftStatus status,
    int totalTokensUsed = 0,
    decimal estimatedCostUsd = 0m,
    byte[]? rowVersion = null) : base(id)
{
    // ... existing assignments ...
    _totalTokensUsed = totalTokensUsed;
    _estimatedCostUsd = estimatedCostUsd;
    _rowVersion = rowVersion ?? Array.Empty<byte>();
}
```

Update `Create()` factory to pass defaults (0, 0m, null) to the constructor.

Add a new method for tracking tokens:

```csharp
/// <summary>
/// Tracks token usage from an AI assist call. Accumulates across calls.
/// </summary>
public void TrackTokenUsage(int tokensUsed, decimal costUsd)
{
    if (tokensUsed < 0)
        throw new ArgumentException("Token count cannot be negative", nameof(tokensUsed));
    if (costUsd < 0)
        throw new ArgumentException("Cost cannot be negative", nameof(costUsd));

    _totalTokensUsed += tokensUsed;
    _estimatedCostUsd += costUsd;
    _lastModified = DateTime.UtcNow;
}
```

- [ ] **Step 2: Write failing tests for token tracking**

Add to `MechanicDraftTests.cs`:

```csharp
[Fact]
public void TrackTokenUsage_AccumulatesAcrossCalls()
{
    var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game", Guid.NewGuid());

    draft.TrackTokenUsage(100, 0.05m);
    draft.TrackTokenUsage(200, 0.10m);

    Assert.Equal(300, draft.TotalTokensUsed);
    Assert.Equal(0.15m, draft.EstimatedCostUsd);
}

[Fact]
public void TrackTokenUsage_NegativeTokens_Throws()
{
    var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game", Guid.NewGuid());

    var ex = Assert.Throws<ArgumentException>(() => draft.TrackTokenUsage(-1, 0m));
    Assert.Contains("negative", ex.Message);
}

[Fact]
public void TrackTokenUsage_NegativeCost_Throws()
{
    var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game", Guid.NewGuid());

    var ex = Assert.Throws<ArgumentException>(() => draft.TrackTokenUsage(0, -0.01m));
    Assert.Contains("negative", ex.Message);
}

[Fact]
public void Create_InitializesTokenFieldsToZero()
{
    var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game", Guid.NewGuid());

    Assert.Equal(0, draft.TotalTokensUsed);
    Assert.Equal(0m, draft.EstimatedCostUsd);
}
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicDraftTests" --no-build`
Expected: All new tests PASS

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraft.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicDraftTests.cs
git commit -m "feat(mechanic-extractor): add token tracking and RowVersion fields to MechanicDraft domain"
```

---

## Task 2: Persistence — Entity, Config, Migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicDraftRepository.cs`
- Create: EF Migration

- [ ] **Step 1: Add properties to MechanicDraftEntity**

Add to `MechanicDraftEntity.cs` after `Status`:

```csharp
public int TotalTokensUsed { get; set; }
public decimal EstimatedCostUsd { get; set; }

/// <summary>
/// Optimistic concurrency token.
/// </summary>
public byte[] RowVersion { get; set; } = Array.Empty<byte>();
```

- [ ] **Step 2: Update EF configuration**

Add to `MechanicDraftEntityConfiguration.cs` `Configure()` method, before the `// Indexes` section:

```csharp
builder.Property(d => d.TotalTokensUsed)
    .HasColumnName("total_tokens_used")
    .HasDefaultValue(0)
    .IsRequired();

builder.Property(d => d.EstimatedCostUsd)
    .HasColumnName("estimated_cost_usd")
    .HasColumnType("numeric(12,6)")
    .HasDefaultValue(0m)
    .IsRequired();

builder.Property(d => d.RowVersion)
    .HasColumnName("row_version")
    .IsRowVersion();
```

- [ ] **Step 3: Update repository mapping**

In `MechanicDraftRepository.cs`, update the `ToDomain()` method to pass new fields:

```csharp
totalTokensUsed: entity.TotalTokensUsed,
estimatedCostUsd: entity.EstimatedCostUsd,
rowVersion: entity.RowVersion
```

Update `ToEntity()` to map back:

```csharp
TotalTokensUsed = domain.TotalTokensUsed,
EstimatedCostUsd = domain.EstimatedCostUsd,
RowVersion = domain.RowVersion,
```

- [ ] **Step 4: Create EF migration**

Run:
```bash
cd apps/api/src/Api && dotnet ef migrations add AddMechanicDraftTokensAndRowVersion
```

Review the generated migration SQL to confirm it adds 3 columns.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicDraftEntity.cs
git add apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicDraftEntityConfiguration.cs
git add "apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicDraftRepository.cs"
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(mechanic-extractor): add token tracking and RowVersion columns to mechanic_drafts"
```

---

## Task 3: Application — Update DTO, Handler, Concurrency

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicDraftDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/AiAssistMechanicDraftCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/SaveMechanicDraftCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/AcceptMechanicDraftCommandHandler.cs`

- [ ] **Step 1: Update MechanicDraftDto**

Replace the record with:

```csharp
public record MechanicDraftDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid CreatedBy,
    string GameTitle,
    string SummaryNotes,
    string MechanicsNotes,
    string VictoryNotes,
    string ResourcesNotes,
    string PhasesNotes,
    string QuestionsNotes,
    string SummaryDraft,
    string MechanicsDraft,
    string VictoryDraft,
    string ResourcesDraft,
    string PhasesDraft,
    string QuestionsDraft,
    DateTime CreatedAt,
    DateTime LastModified,
    string Status,
    int TotalTokensUsed,
    decimal EstimatedCostUsd);
```

Update `AiAssistResultDto` to include token info:

```csharp
public record AiAssistResultDto(
    string Section,
    string GeneratedDraft,
    int TokensUsed,
    decimal CostUsd);
```

- [ ] **Step 2: Update AiAssistMechanicDraftCommandHandler to track tokens**

The handler already gets `result.Usage` and `result.Cost` from the LLM. Add the draft repository to persist tokens. Add `IMechanicDraftRepository` to constructor:

```csharp
private readonly ILlmService _llmService;
private readonly IMechanicDraftRepository _draftRepository;
private readonly ILogger<AiAssistMechanicDraftCommandHandler> _logger;

public AiAssistMechanicDraftCommandHandler(
    ILlmService llmService,
    IMechanicDraftRepository draftRepository,
    ILogger<AiAssistMechanicDraftCommandHandler> logger)
{
    _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
    _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

After the successful LLM call, persist token usage:

```csharp
var tokensUsed = result.Usage?.TotalTokens ?? 0;
var costUsd = result.Cost?.TotalCost ?? 0m;

// Persist token tracking to draft
var draft = await _draftRepository.GetByIdAsync(request.DraftId, cancellationToken)
    .ConfigureAwait(false);
if (draft is not null)
{
    draft.TrackTokenUsage(tokensUsed, costUsd);
    await _draftRepository.UpdateAsync(draft, cancellationToken).ConfigureAwait(false);
}

return new AiAssistResultDto(request.Section, result.Response.Trim(), tokensUsed, costUsd);
```

- [ ] **Step 3: Add concurrency handling to SaveMechanicDraftCommandHandler**

Wrap the update call in a try/catch:

```csharp
try
{
    await _repository.UpdateAsync(draft, cancellationToken).ConfigureAwait(false);
}
catch (DbUpdateConcurrencyException)
{
    throw new ConflictException("Draft was modified by another user. Please reload and try again.");
}
```

Add `using Microsoft.EntityFrameworkCore;` at the top.

- [ ] **Step 4: Add same concurrency handling to AcceptMechanicDraftCommandHandler**

Same try/catch pattern around the update call.

- [ ] **Step 5: Update GetMechanicDraftQueryHandler DTO mapping**

Ensure the query handler maps `TotalTokensUsed` and `EstimatedCostUsd` into the DTO.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/
git commit -m "feat(mechanic-extractor): track AI tokens per draft, add concurrency handling"
```

---

## Task 4: Copyright Firewall Test

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/CopyrightFirewallTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using System.Reflection;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Ensures the Variant C copyright firewall is maintained at the DI level.
/// The AiAssist handler must NEVER have access to PDF storage or blob services.
/// </summary>
public class CopyrightFirewallTests
{
    [Fact]
    public void AiAssistHandler_MustNotInject_BlobOrPdfDependencies()
    {
        var handlerType = typeof(AiAssistMechanicDraftCommandHandler);
        var constructors = handlerType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var parameterTypeNames = constructors
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToList();

        // These types must NEVER appear in AiAssist handler's constructor
        var forbiddenPatterns = new[]
        {
            "IBlobStorageService",
            "IPdfDocumentRepository",
            "IBlobService",
            "IFileStorageService",
            "IPdfStorage",
        };

        foreach (var forbidden in forbiddenPatterns)
        {
            parameterTypeNames.Should().NotContain(
                name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"AiAssist handler must not depend on {forbidden} — this would violate the Variant C copyright firewall");
        }
    }

    [Fact]
    public void AiAssistHandler_OnlyExpectedDependencies()
    {
        var handlerType = typeof(AiAssistMechanicDraftCommandHandler);
        var constructors = handlerType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var parameterTypeNames = constructors
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToList();

        // Allowed: ILlmService, IMechanicDraftRepository, ILogger
        var allowedPrefixes = new[] { "ILlmService", "IMechanicDraftRepository", "ILogger" };

        foreach (var param in parameterTypeNames)
        {
            allowedPrefixes.Should().Contain(
                prefix => param.StartsWith(prefix, StringComparison.Ordinal),
                $"Unexpected dependency '{param}' in AiAssist handler — review for copyright compliance");
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightFirewallTests"`
Expected: PASS (handler only has ILlmService, IMechanicDraftRepository, ILogger)

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/CopyrightFirewallTests.cs
git commit -m "test(mechanic-extractor): add copyright firewall DI reflection tests"
```

---

## Task 5: Frontend — Schema Update + Token Display

**Files:**
- Modify: `apps/web/src/lib/api/schemas/mechanic-extractor.schemas.ts`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx`

- [ ] **Step 1: Update Zod schemas**

In `mechanic-extractor.schemas.ts`, add fields to `MechanicDraftDtoSchema`:

```typescript
export const MechanicDraftDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  createdBy: z.string().uuid(),
  gameTitle: z.string(),
  summaryNotes: z.string(),
  mechanicsNotes: z.string(),
  victoryNotes: z.string(),
  resourcesNotes: z.string(),
  phasesNotes: z.string(),
  questionsNotes: z.string(),
  summaryDraft: z.string(),
  mechanicsDraft: z.string(),
  victoryDraft: z.string(),
  resourcesDraft: z.string(),
  phasesDraft: z.string(),
  questionsDraft: z.string(),
  createdAt: z.string(),
  lastModified: z.string(),
  status: z.string(),
  totalTokensUsed: z.number().default(0),
  estimatedCostUsd: z.number().default(0),
});
```

Update `AiAssistResultDtoSchema`:

```typescript
export const AiAssistResultDtoSchema = z.object({
  section: z.string(),
  generatedDraft: z.string(),
  tokensUsed: z.number().default(0),
  costUsd: z.number().default(0),
});
```

Add review route:

```typescript
export const MECHANIC_EXTRACTOR_ROUTES = {
  draft: '/api/v1/admin/mechanic-extractor/draft',
  aiAssist: '/api/v1/admin/mechanic-extractor/ai-assist',
  acceptDraft: '/api/v1/admin/mechanic-extractor/accept-draft',
  finalize: '/api/v1/admin/mechanic-extractor/finalize',
} as const;

export const MECHANIC_EXTRACTOR_PAGES = {
  editor: '/admin/knowledge-base/mechanic-extractor',
  review: '/admin/knowledge-base/mechanic-extractor/review',
} as const;
```

- [ ] **Step 2: Add token display and review link to editor page**

In `page.tsx`, add after the AI assist button section (inside the right panel):

```tsx
{/* Token Usage */}
{existingDraft && existingDraft.totalTokensUsed > 0 && (
  <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
    <SparklesIcon className="h-3 w-3" />
    <span>{existingDraft.totalTokensUsed} tokens used</span>
    {existingDraft.estimatedCostUsd > 0 && (
      <span>(${existingDraft.estimatedCostUsd.toFixed(4)})</span>
    )}
  </div>
)}
```

Add a "Preview" link button next to the Finalize button:

```tsx
{canFinalize && (
  <div className="mt-4 border-t border-slate-200 pt-4 dark:border-zinc-700 space-y-2">
    <div className="flex gap-2">
      <Button
        variant="outline"
        className="flex-1"
        asChild
      >
        <a href={`/admin/knowledge-base/mechanic-extractor/review?sharedGameId=${selectedGameId}&pdfDocumentId=${selectedPdfId}`}>
          <FileTextIcon className="mr-2 h-4 w-4" />
          Preview & Export
        </a>
      </Button>
      <Button
        onClick={() => finalizeMutation.mutate()}
        disabled={finalizeMutation.isPending}
        className="flex-1 bg-green-600 hover:bg-green-700 text-white"
      >
        {/* ... existing finalize content ... */}
      </Button>
    </div>
  </div>
)}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/mechanic-extractor.schemas.ts
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx"
git commit -m "feat(mechanic-extractor): add token tracking display and review link to editor"
```

---

## Task 6: Frontend — Review Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx`

- [ ] **Step 1: Create the review page**

This page reads the draft via the same `getMechanicDraft` API and renders a print-friendly preview matching the mockup `mechanic-extractor-review.html`.

```tsx
'use client';

import { useSearchParams } from 'next/navigation';
import { Suspense } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeftIcon,
  BookOpenIcon,
  CheckCircleIcon,
  DownloadIcon,
  FileTextIcon,
  HelpCircleIcon,
  ListIcon,
  SparklesIcon,
  TrophyIcon,
  PackageIcon,
  PlayIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { MechanicDraftDto } from '@/lib/api/schemas/mechanic-extractor.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

interface VictoryConditions {
  primary: string;
  alternatives?: string[];
  isPointBased?: boolean;
  targetPoints?: number | null;
}

interface Resource {
  name: string;
  type: string;
  usage?: string;
  isLimited: boolean;
}

interface GamePhase {
  name: string;
  description: string;
  order: number;
  isOptional?: boolean;
}

function safeParseJson<T>(json: string, fallback: T): T {
  if (!json) return fallback;
  try {
    return JSON.parse(json) as T;
  } catch {
    return fallback;
  }
}

function ReviewContent() {
  const searchParams = useSearchParams();
  const sharedGameId = searchParams.get('sharedGameId') ?? '';
  const pdfDocumentId = searchParams.get('pdfDocumentId') ?? '';

  const { data: draft, isLoading } = useQuery({
    queryKey: ['admin', 'mechanic-draft', sharedGameId, pdfDocumentId],
    queryFn: () => adminClient.getMechanicDraft(sharedGameId, pdfDocumentId),
    enabled: !!sharedGameId && !!pdfDocumentId,
  });

  if (isLoading) {
    return (
      <div className="mx-auto max-w-[900px] space-y-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-48 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (!draft) {
    return (
      <div className="flex h-[50vh] items-center justify-center text-muted-foreground">
        <p>Draft non trovato. Seleziona un gioco e un PDF dall&apos;editor.</p>
      </div>
    );
  }

  const mechanics: string[] = safeParseJson(draft.mechanicsDraft, []);
  const victory: VictoryConditions = safeParseJson(draft.victoryDraft, { primary: '' });
  const resources: Resource[] = safeParseJson(draft.resourcesDraft, []);
  const phases: GamePhase[] = safeParseJson(draft.phasesDraft, []);
  const questions: string[] = safeParseJson(draft.questionsDraft, []);

  const completedSections = [
    draft.summaryDraft,
    draft.mechanicsDraft,
    draft.victoryDraft,
    draft.resourcesDraft,
    draft.phasesDraft,
    draft.questionsDraft,
  ].filter(Boolean).length;

  return (
    <div className="mx-auto max-w-[900px] space-y-6 print:max-w-full">
      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            {draft.gameTitle} — Anteprima Analisi
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Revisione finale prima dell&apos;attivazione nella Knowledge Base
          </p>
        </div>
        <Badge
          variant="outline"
          className={completedSections === 6
            ? 'border-green-300 bg-green-50 text-green-800 dark:bg-green-950/30 dark:text-green-300'
            : 'border-amber-300 bg-amber-50 text-amber-800 dark:bg-amber-950/30 dark:text-amber-300'}
        >
          <CheckCircleIcon className="mr-1 h-3 w-3" />
          {completedSections}/6 sezioni completate
        </Badge>
      </div>

      {/* Stats Bar */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard value={completedSections} label="Sezioni completate" />
        <StatCard value={mechanics.length} label="Meccaniche estratte" />
        <StatCard value={resources.length} label="Risorse catalogate" />
        <StatCard value={draft.totalTokensUsed ?? 0} label="Token AI utilizzati" />
      </div>

      {/* Summary */}
      {draft.summaryDraft && (
        <ReviewCard title="Sommario del Gioco" icon={<BookOpenIcon className="h-4 w-4" />}>
          <p className="text-sm leading-relaxed">{draft.summaryDraft}</p>
        </ReviewCard>
      )}

      {/* Mechanics */}
      {mechanics.length > 0 && (
        <ReviewCard
          title="Meccaniche di Gioco"
          icon={<ListIcon className="h-4 w-4" />}
          badge={`${mechanics.length} meccaniche`}
        >
          <div className="flex flex-wrap gap-2">
            {mechanics.map((m, i) => (
              <span
                key={i}
                className="rounded-md border border-amber-200 bg-amber-50 px-2.5 py-1 text-xs font-semibold text-amber-900 dark:border-amber-700 dark:bg-amber-950/30 dark:text-amber-300"
              >
                {m}
              </span>
            ))}
          </div>
        </ReviewCard>
      )}

      {/* Victory */}
      {victory.primary && (
        <ReviewCard title="Condizioni di Vittoria" icon={<TrophyIcon className="h-4 w-4" />}>
          <p className="text-sm">
            <strong>Obiettivo principale:</strong> {victory.primary}
          </p>
          {victory.alternatives && victory.alternatives.length > 0 && (
            <div className="mt-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-amber-600 dark:text-amber-400">
                Fonti di Punti
              </p>
              <ul className="mt-1 list-inside list-disc space-y-0.5 text-sm text-foreground">
                {victory.alternatives.map((alt, i) => (
                  <li key={i}>{alt}</li>
                ))}
              </ul>
            </div>
          )}
        </ReviewCard>
      )}

      {/* Resources */}
      {resources.length > 0 && (
        <ReviewCard
          title="Risorse"
          icon={<PackageIcon className="h-4 w-4" />}
          badge={`${resources.length} risorse`}
        >
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                <th className="py-2 pr-4">Risorsa</th>
                <th className="py-2 pr-4">Tipo</th>
                <th className="py-2 pr-4">Utilizzo</th>
                <th className="py-2">Limitata</th>
              </tr>
            </thead>
            <tbody>
              {resources.map((r, i) => (
                <tr key={i} className="border-b border-slate-100 dark:border-zinc-700/50">
                  <td className="py-2 pr-4 font-medium">{r.name}</td>
                  <td className="py-2 pr-4">{r.type}</td>
                  <td className="py-2 pr-4">{r.usage ?? '—'}</td>
                  <td className="py-2">{r.isLimited ? 'Sì' : 'No'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </ReviewCard>
      )}

      {/* Phases */}
      {phases.length > 0 && (
        <ReviewCard
          title="Fasi di Gioco"
          icon={<PlayIcon className="h-4 w-4" />}
          badge={`${phases.length} fasi`}
        >
          <div className="space-y-3">
            {phases.sort((a, b) => a.order - b.order).map((p) => (
              <div key={p.order} className="flex gap-3">
                <div className="flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-orange-500 text-xs font-bold text-white">
                  {p.order}
                </div>
                <div>
                  <p className="font-semibold text-sm">{p.name}</p>
                  <p className="text-xs text-muted-foreground">{p.description}</p>
                </div>
              </div>
            ))}
          </div>
        </ReviewCard>
      )}

      {/* FAQ */}
      {questions.length > 0 && (
        <ReviewCard
          title="Domande Frequenti"
          icon={<HelpCircleIcon className="h-4 w-4" />}
          badge={`${questions.length} FAQ`}
        >
          <div className="space-y-3">
            {questions.map((q, i) => (
              <div key={i} className="text-sm">
                <p className="font-semibold">{q}</p>
              </div>
            ))}
          </div>
        </ReviewCard>
      )}

      {/* Copyright Footer */}
      <div className="rounded-lg border border-green-200 bg-green-50/50 p-4 text-center text-xs text-green-800 dark:border-green-800 dark:bg-green-950/20 dark:text-green-300 print:border-green-400">
        <strong>&copy; 2026 MeepleAI</strong> — Contenuto originale. Meccaniche di gioco descritte indipendentemente.
        <br />
        <span className="opacity-70">
          Generato con Variant C (human + AI assistant). L&apos;AI non ha mai letto il testo del PDF originale.
        </span>
        {(draft.totalTokensUsed ?? 0) > 0 && (
          <span className="ml-2 opacity-70">
            | {draft.totalTokensUsed} tokens, ${(draft.estimatedCostUsd ?? 0).toFixed(4)}
          </span>
        )}
      </div>

      {/* Action Bar */}
      <div className="flex items-center justify-between border-t pt-4 print:hidden">
        <Button variant="outline" asChild>
          <a href="/admin/knowledge-base/mechanic-extractor">
            <ArrowLeftIcon className="mr-2 h-4 w-4" />
            Torna all&apos;editor
          </a>
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => window.print()}>
            <DownloadIcon className="mr-2 h-4 w-4" />
            Esporta PDF
          </Button>
        </div>
      </div>
    </div>
  );
}

function StatCard({ value, label }: { value: number; label: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 text-center transition-shadow hover:shadow-md dark:border-zinc-700 dark:bg-zinc-800">
      <div className="font-quicksand text-2xl font-bold text-amber-500">{value}</div>
      <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
    </div>
  );
}

function ReviewCard({
  title,
  icon,
  badge,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  badge?: string;
  children: React.ReactNode;
}) {
  return (
    <Card className="bg-white/70 backdrop-blur-md border-slate-200/60 dark:bg-zinc-800/70 dark:border-zinc-700/60 print:shadow-none print:border-slate-300">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-base font-quicksand">
            {icon}
            {title}
          </CardTitle>
          {badge && (
            <Badge variant="outline" className="text-xs border-amber-300 text-amber-700 dark:text-amber-300">
              {badge}
            </Badge>
          )}
        </div>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

export default function MechanicExtractorReviewPage() {
  return (
    <Suspense fallback={<Skeleton className="h-96 w-full" />}>
      <ReviewContent />
    </Suspense>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx"
git commit -m "feat(mechanic-extractor): add review page with stats, sections, and PDF export"
```

---

## Task 7: Frontend Test — Review Page

**Files:**
- Create: `apps/web/src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx`

- [ ] **Step 1: Write review page tests**

```tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(() => ({
    get: vi.fn((key: string) => {
      if (key === 'sharedGameId') return 'game-123';
      if (key === 'pdfDocumentId') return 'pdf-456';
      return null;
    }),
  })),
}));

// Mock admin client
const mockGetMechanicDraft = vi.fn();
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicDraft: mockGetMechanicDraft,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(),
}));

import MechanicExtractorReviewPage from '@/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page';

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('MechanicExtractorReviewPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows draft not found when no data', async () => {
    mockGetMechanicDraft.mockResolvedValue(null);
    renderWithQuery(<MechanicExtractorReviewPage />);

    expect(await screen.findByText(/Draft non trovato/)).toBeInTheDocument();
  });

  it('renders game title and stats when draft exists', async () => {
    mockGetMechanicDraft.mockResolvedValue({
      id: '1',
      sharedGameId: 'game-123',
      pdfDocumentId: 'pdf-456',
      createdBy: 'user-1',
      gameTitle: 'I Coloni di Catan',
      summaryNotes: 'notes',
      mechanicsNotes: 'notes',
      victoryNotes: 'notes',
      resourcesNotes: 'notes',
      phasesNotes: '',
      questionsNotes: '',
      summaryDraft: 'A strategy game...',
      mechanicsDraft: '["Dice Rolling","Trading"]',
      victoryDraft: '{"primary":"10 points"}',
      resourcesDraft: '[]',
      phasesDraft: '[]',
      questionsDraft: '[]',
      createdAt: '2026-04-19T00:00:00Z',
      lastModified: '2026-04-19T00:00:00Z',
      status: 'Draft',
      totalTokensUsed: 150,
      estimatedCostUsd: 0.0021,
    });

    renderWithQuery(<MechanicExtractorReviewPage />);

    expect(await screen.findByText(/I Coloni di Catan/)).toBeInTheDocument();
    expect(screen.getByText('150')).toBeInTheDocument(); // token stat
    expect(screen.getByText('2')).toBeInTheDocument(); // mechanics count
    expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
    expect(screen.getByText('Trading')).toBeInTheDocument();
  });

  it('renders copyright footer', async () => {
    mockGetMechanicDraft.mockResolvedValue({
      id: '1',
      sharedGameId: 'game-123',
      pdfDocumentId: 'pdf-456',
      createdBy: 'user-1',
      gameTitle: 'Test Game',
      summaryNotes: '',
      mechanicsNotes: '',
      victoryNotes: '',
      resourcesNotes: '',
      phasesNotes: '',
      questionsNotes: '',
      summaryDraft: 'Summary text',
      mechanicsDraft: '',
      victoryDraft: '',
      resourcesDraft: '',
      phasesDraft: '',
      questionsDraft: '',
      createdAt: '2026-04-19T00:00:00Z',
      lastModified: '2026-04-19T00:00:00Z',
      status: 'Draft',
      totalTokensUsed: 0,
      estimatedCostUsd: 0,
    });

    renderWithQuery(<MechanicExtractorReviewPage />);

    expect(await screen.findByText(/Contenuto originale/)).toBeInTheDocument();
    expect(screen.getByText(/Variant C/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx`
Expected: All 3 tests PASS

- [ ] **Step 3: Commit**

```bash
git add "apps/web/src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx"
git commit -m "test(mechanic-extractor): add review page rendering tests"
```

---

## Task 8: Build Verification + Manual Test Plan

- [ ] **Step 1: Backend build + tests**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "FullyQualifiedName~MechanicDraft|FullyQualifiedName~CopyrightFirewall"
```

Expected: Build succeeds, all tests pass.

- [ ] **Step 2: Frontend build + tests + lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm vitest run
```

Expected: No type errors, no lint errors, all tests pass.

- [ ] **Step 3: Manual test checklist**

Start the dev stack: `cd infra && make dev-core`

1. Navigate to `/admin/knowledge-base/mechanic-extractor`
2. Select a game with PDFs → verify PDF loads in left panel
3. Write notes (>10 chars) in Summary tab → verify auto-save indicator
4. Click "AI Assist" → verify loading state → verify draft appears with Accept/Reject
5. Accept draft → verify green "Accepted Draft" section
6. Verify token count appears below AI Assist button
7. Click "Preview & Export" link → verify review page opens at `/review?sharedGameId=...&pdfDocumentId=...`
8. On review page: verify stats bar shows correct counts
9. Verify all completed sections render correctly
10. Click "Esporta PDF" → verify browser print dialog opens
11. Click "Torna all'editor" → verify navigation back works
12. Try concurrent edit scenario: open same draft in two tabs, save in both → verify ConflictException message

- [ ] **Step 4: Final commit with all changes**

```bash
git add -A && git status
```

Review staged files — ensure no secrets or temp files. Then create final commit if any unstaged fixes remain.
