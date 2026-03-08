# GameToolkit Evolution — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Evolve the GameToolkit system across 5 phases: AI-powered generation, template marketplace, real-time widget sync, whiteboard optimization, and session analytics.

**Architecture:** The GameToolkit system has two layers — an admin-managed `GameToolkit` aggregate (dice/card/timer/counter tools + templates) and a user-level `Toolkit` dashboard (6 generic widgets). Both persist via EF Core to PostgreSQL with JSONB configs. Live sessions use SSE streaming. AI integration leverages the existing `ILlmService` + RAG pipeline.

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs, MediatR, EF Core, FluentValidation) | Next.js 16 (React 19, Zustand, Zod, Tailwind 4, shadcn/ui) | PostgreSQL 16 (JSONB) | Qdrant (vectors) | SSE streaming

**Parent Branch:** `main-dev`

---

## Phase Overview

| Phase | Scope | Issues | Branch |
|-------|-------|--------|--------|
| P0 | AI Toolkit Generation | 9 tasks | `feature/toolkit-ai-generation` |
| P1 | Template Marketplace | 10 tasks | `feature/toolkit-templates` |
| P2 | Widget Real-Time Sync | 6 tasks | `feature/toolkit-widget-sync` |
| P3 | Whiteboard Storage Fix | 2 tasks | `feature/toolkit-whiteboard-fix` |
| P4 | Session Analytics | 7 tasks | `feature/toolkit-session-analytics` |

---

# Phase 0 — AI Toolkit Generation

**Goal:** An admin/editor can click "Generate with AI" on any game with KB cards, and the LLM reads the game rules to auto-populate a GameToolkit draft (dice, counters, scoring, turn order, timers).

**Key Integration Points:**
- `ILlmService.GenerateJsonAsync<T>()` — structured JSON output from LLM
- `ITextChunkSearchService` — retrieve game rule chunks for context
- `GameToolkit.SetAgentConfig()` — already exists, stores AI config as string
- `GameToolkit.AddDiceTool/AddCounterTool/SetScoringTemplate/SetTurnTemplate` — existing mutators

---

### Task P0-1: Define the AI generation response DTO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/AiToolkitSuggestionDto.cs`

**Step 1: Create the DTO that the LLM will return as structured JSON**

```csharp
namespace Api.BoundedContexts.GameToolkit.Application.DTOs;

/// <summary>
/// Structured response from LLM for auto-generating a GameToolkit.
/// Used with ILlmService.GenerateJsonAsync&lt;AiToolkitSuggestionDto&gt;().
/// </summary>
internal sealed record AiToolkitSuggestionDto(
    string SuggestedName,
    List<AiDiceToolSuggestion> DiceTools,
    List<AiCounterToolSuggestion> CounterTools,
    List<AiTimerToolSuggestion> TimerTools,
    AiScoringTemplateSuggestion? ScoringTemplate,
    AiTurnTemplateSuggestion? TurnTemplate,
    AiOverrideSuggestion Overrides,
    string Reasoning
);

internal sealed record AiDiceToolSuggestion(
    string Name,
    string DiceType,  // "D4"|"D6"|"D8"|"D10"|"D12"|"D20"|"Custom"
    int Quantity,
    string[]? CustomFaces,
    string? Color
);

internal sealed record AiCounterToolSuggestion(
    string Name,
    int MinValue,
    int MaxValue,
    int DefaultValue,
    bool IsPerPlayer,
    string? Icon,
    string? Color
);

internal sealed record AiTimerToolSuggestion(
    string Name,
    int DurationSeconds,
    string TimerType,  // "CountDown"|"CountUp"|"Chess"
    bool IsPerPlayer,
    int? WarningThresholdSeconds
);

internal sealed record AiScoringTemplateSuggestion(
    string[] Dimensions,
    string DefaultUnit,
    string ScoreType  // "Points"|"Ranking"
);

internal sealed record AiTurnTemplateSuggestion(
    string TurnOrderType,  // "RoundRobin"|"Custom"|"Free"
    string[]? Phases
);

internal sealed record AiOverrideSuggestion(
    bool OverridesTurnOrder,
    bool OverridesScoreboard,
    bool OverridesDiceSet
);
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add AI toolkit suggestion DTOs"
```

---

### Task P0-2: Create the AI generation command + validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/GenerateToolkitFromKbCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Validators/ToolkitValidators.cs`

**Step 1: Create the command**

```csharp
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Reads KB cards for a game and uses the LLM to generate a draft GameToolkit.
/// Returns the suggestion DTO (not yet persisted — caller reviews and applies).
/// </summary>
internal record GenerateToolkitFromKbCommand(
    Guid GameId,
    Guid UserId
) : IRequest<AiToolkitSuggestionDto>;
```

**Step 2: Add validator to ToolkitValidators.cs**

```csharp
internal sealed class GenerateToolkitFromKbCommandValidator
    : AbstractValidator<GenerateToolkitFromKbCommand>
{
    public GenerateToolkitFromKbCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add GenerateToolkitFromKbCommand + validator"
```

---

### Task P0-3: Implement the AI generation handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/GenerateToolkitFromKbHandler.cs`

**Step 1: Write the handler**

This handler:
1. Fetches KB chunks for the game using `ITextChunkSearchService`
2. Builds a system prompt instructing the LLM to analyze game rules
3. Calls `ILlmService.GenerateJsonAsync<AiToolkitSuggestionDto>()`
4. Returns the suggestion (not persisted yet)

```csharp
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal sealed class GenerateToolkitFromKbHandler
    : IRequestHandler<GenerateToolkitFromKbCommand, AiToolkitSuggestionDto>
{
    private readonly ILlmService _llmService;
    private readonly ITextChunkSearchService _textChunkSearch;
    private readonly ILogger<GenerateToolkitFromKbHandler> _logger;

    public GenerateToolkitFromKbHandler(
        ILlmService llmService,
        ITextChunkSearchService textChunkSearch,
        ILogger<GenerateToolkitFromKbHandler> logger)
    {
        _llmService = llmService;
        _textChunkSearch = textChunkSearch;
        _logger = logger;
    }

    public async Task<AiToolkitSuggestionDto> Handle(
        GenerateToolkitFromKbCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Retrieve game rule chunks via FTS (broad query to get all relevant content)
        var chunks = await _textChunkSearch.FullTextSearchAsync(
            request.GameId,
            "game rules components resources scoring turns dice cards timer setup",
            limit: 30,
            cancellationToken).ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            _logger.LogWarning(
                "No KB chunks found for game {GameId}, returning minimal suggestion",
                request.GameId);

            return new AiToolkitSuggestionDto(
                SuggestedName: "Game Toolkit",
                DiceTools: [new("Standard Dice", "D6", 2, null, null)],
                CounterTools: [],
                TimerTools: [],
                ScoringTemplate: new(["Points"], "points", "Points"),
                TurnTemplate: new("RoundRobin", null),
                Overrides: new(false, false, false),
                Reasoning: "No game rules found in knowledge base. Provided generic defaults."
            );
        }

        // 2. Build context from chunks
        var rulesContext = string.Join("\n\n---\n\n",
            chunks.Select(c => c.Content));

        // 3. System prompt
        const string systemPrompt = """
            You are a board game toolkit configurator. Given game rules text, extract the
            mechanical components needed for a digital game session toolkit.

            Analyze the rules and return a JSON object with these fields:
            - suggestedName: A short name for the toolkit (e.g., "Catan Toolkit")
            - diceTools: Array of dice configs. Each has: name, diceType (D4|D6|D8|D10|D12|D20|Custom),
              quantity, customFaces (string[] if Custom), color (hex or null)
            - counterTools: Array of resource counters. Each has: name, minValue, maxValue, defaultValue,
              isPerPlayer (true if each player tracks independently), icon (emoji), color (hex or null)
            - timerTools: Array of timers. Each has: name, durationSeconds, timerType (CountDown|CountUp|Chess),
              isPerPlayer, warningThresholdSeconds (or null)
            - scoringTemplate: {dimensions: string[] (scoring categories), defaultUnit: string, scoreType: "Points"|"Ranking"} or null
            - turnTemplate: {turnOrderType: "RoundRobin"|"Custom"|"Free", phases: string[] or null} or null
            - overrides: {overridesTurnOrder: bool, overridesScoreboard: bool, overridesDiceSet: bool}
              Set to true if you provide a corresponding custom tool/template
            - reasoning: Brief explanation of why you chose these components

            Rules:
            - Only include components explicitly mentioned or strongly implied by the rules
            - If the game uses standard dice, include them. If not, omit diceTools.
            - For resources, identify all trackable quantities (money, wood, ore, cards, etc.)
            - For scoring, identify all ways to earn points
            - Keep names short and descriptive
            - Return ONLY valid JSON, no markdown
            """;

        var userPrompt = $"""
            Analyze these game rules and generate the toolkit configuration:

            {rulesContext}
            """;

        // 4. Call LLM for structured JSON
        var suggestion = await _llmService.GenerateJsonAsync<AiToolkitSuggestionDto>(
            systemPrompt,
            userPrompt,
            RequestSource.AgentTask,
            cancellationToken).ConfigureAwait(false);

        if (suggestion is null)
        {
            _logger.LogWarning("LLM returned null for toolkit generation, game {GameId}", request.GameId);
            return new AiToolkitSuggestionDto(
                SuggestedName: "Game Toolkit",
                DiceTools: [new("Standard Dice", "D6", 2, null, null)],
                CounterTools: [],
                TimerTools: [],
                ScoringTemplate: null,
                TurnTemplate: null,
                Overrides: new(false, false, false),
                Reasoning: "AI generation failed. Provided generic defaults."
            );
        }

        _logger.LogInformation(
            "Generated toolkit suggestion for game {GameId}: {Name} with {Dice} dice, {Counters} counters, {Timers} timers",
            request.GameId,
            suggestion.SuggestedName,
            suggestion.DiceTools.Count,
            suggestion.CounterTools.Count,
            suggestion.TimerTools.Count);

        return suggestion;
    }
}
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(toolkit): implement AI toolkit generation handler with LLM + KB chunks"
```

---

### Task P0-4: Create the "apply suggestion" command

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ApplyAiToolkitSuggestionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/ApplyAiToolkitSuggestionHandler.cs`

**Step 1: Create command**

```csharp
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Takes a reviewed AiToolkitSuggestionDto and applies it to create/update a GameToolkit.
/// If toolkitId is null, creates a new GameToolkit. Otherwise updates existing draft.
/// </summary>
internal record ApplyAiToolkitSuggestionCommand(
    Guid GameId,
    Guid UserId,
    Guid? ToolkitId,
    AiToolkitSuggestionDto Suggestion
) : IRequest<GameToolkitDto>;
```

**Step 2: Create handler**

The handler maps AI suggestion DTOs to domain tool configs and calls the existing `GameToolkit` mutators:

```csharp
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal sealed class ApplyAiToolkitSuggestionHandler
    : IRequestHandler<ApplyAiToolkitSuggestionCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApplyAiToolkitSuggestionHandler> _logger;

    public ApplyAiToolkitSuggestionHandler(
        IGameToolkitRepository repo,
        IUnitOfWork unitOfWork,
        ILogger<ApplyAiToolkitSuggestionHandler> logger)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GameToolkitDto> Handle(
        ApplyAiToolkitSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        var s = request.Suggestion;
        Domain.Entities.GameToolkit toolkit;

        if (request.ToolkitId.HasValue)
        {
            // Update existing draft
            toolkit = await _repo.GetByIdAsync(request.ToolkitId.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new Api.Middleware.Exceptions.NotFoundException("GameToolkit", request.ToolkitId.Value);

            toolkit.UpdateDetails(s.SuggestedName);
        }
        else
        {
            // Create new
            toolkit = new Domain.Entities.GameToolkit(
                Guid.NewGuid(),
                request.GameId,
                s.SuggestedName,
                request.UserId);
            await _repo.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
        }

        // Apply override flags
        toolkit.UpdateOverrideFlags(
            s.Overrides.OverridesTurnOrder,
            s.Overrides.OverridesScoreboard,
            s.Overrides.OverridesDiceSet);

        // Apply dice tools
        foreach (var dice in s.DiceTools)
        {
            if (!Enum.TryParse<DiceType>(dice.DiceType, true, out var diceType))
                diceType = DiceType.D6;

            toolkit.AddDiceTool(new Domain.Entities.DiceToolConfig(
                dice.Name, diceType, dice.Quantity, dice.CustomFaces,
                isInteractive: true, dice.Color));
        }

        // Apply counter tools
        foreach (var counter in s.CounterTools)
        {
            toolkit.AddCounterTool(new Domain.Entities.CounterToolConfig(
                counter.Name, counter.MinValue, counter.MaxValue, counter.DefaultValue,
                counter.IsPerPlayer, counter.Icon, counter.Color));
        }

        // Apply timer tools
        foreach (var timer in s.TimerTools)
        {
            if (!Enum.TryParse<TimerType>(timer.TimerType, true, out var timerType))
                timerType = TimerType.CountDown;

            toolkit.AddTimerTool(new Domain.Entities.TimerToolConfig(
                timer.Name, timer.DurationSeconds, timerType,
                autoStart: false, color: null, timer.IsPerPlayer,
                timer.WarningThresholdSeconds));
        }

        // Apply scoring template
        if (s.ScoringTemplate is not null)
        {
            if (!Enum.TryParse<ScoreType>(s.ScoringTemplate.ScoreType, true, out var scoreType))
                scoreType = ScoreType.Points;

            toolkit.SetScoringTemplate(new Domain.Entities.ScoringTemplateConfig(
                s.ScoringTemplate.Dimensions, s.ScoringTemplate.DefaultUnit, scoreType));
        }

        // Apply turn template
        if (s.TurnTemplate is not null)
        {
            if (!Enum.TryParse<TurnOrderType>(s.TurnTemplate.TurnOrderType, true, out var turnType))
                turnType = TurnOrderType.RoundRobin;

            toolkit.SetTurnTemplate(new Domain.Entities.TurnTemplateConfig(
                turnType, s.TurnTemplate.Phases ?? []));
        }

        // Store the AI reasoning as agent config
        toolkit.SetAgentConfig(System.Text.Json.JsonSerializer.Serialize(new
        {
            generatedAt = DateTime.UtcNow,
            reasoning = s.Reasoning,
            source = "ai-generation"
        }));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Applied AI toolkit suggestion to GameToolkit {ToolkitId} for game {GameId}",
            toolkit.Id, request.GameId);

        return ToolkitMapper.ToDto(toolkit);
    }
}
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add ApplyAiToolkitSuggestion command + handler"
```

---

### Task P0-5: Add API endpoints for AI generation

**Files:**
- Modify: `apps/api/src/Api/Routing/GameToolkitRoutes.cs`

**Step 1: Read the existing routes file first, then add two new endpoints**

Add inside `MapGameToolkitEndpoints()`:

```csharp
// AI Generation endpoints
group.MapPost("/{id:guid}/generate-from-kb", async (
    Guid id,
    IMediator mediator,
    HttpContext context,
    CancellationToken ct) =>
{
    var userId = context.GetUserId();
    var result = await mediator.Send(
        new GenerateToolkitFromKbCommand(id, userId), ct);
    return Results.Ok(result);
})
.WithName("GenerateToolkitFromKb")
.WithDescription("Uses AI to analyze game KB and suggest toolkit configuration")
.RequireAuthorization();

group.MapPost("/{id:guid}/apply-ai-suggestion", async (
    Guid id,
    [FromBody] ApplyAiSuggestionRequest request,
    IMediator mediator,
    HttpContext context,
    CancellationToken ct) =>
{
    var userId = context.GetUserId();
    var result = await mediator.Send(
        new ApplyAiToolkitSuggestionCommand(id, userId, id, request.Suggestion), ct);
    return Results.Ok(result);
})
.WithName("ApplyAiToolkitSuggestion")
.WithDescription("Applies a reviewed AI suggestion to a GameToolkit draft")
.RequireAuthorization();
```

Also add a second endpoint variant that creates a NEW toolkit from a game ID (no existing toolkit needed):

```csharp
group.MapPost("/generate-for-game/{gameId:guid}", async (
    Guid gameId,
    IMediator mediator,
    HttpContext context,
    CancellationToken ct) =>
{
    var userId = context.GetUserId();
    var result = await mediator.Send(
        new GenerateToolkitFromKbCommand(gameId, userId), ct);
    return Results.Ok(result);
})
.WithName("GenerateToolkitFromKbForGame")
.RequireAuthorization();
```

**Step 2: Add the request body record** (in ToolkitDtos.cs or inline):

```csharp
internal record ApplyAiSuggestionRequest(AiToolkitSuggestionDto Suggestion);
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add AI generation API endpoints"
```

---

### Task P0-6: Backend unit tests for AI generation

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Handlers/GenerateToolkitFromKbHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Application/Handlers/ApplyAiToolkitSuggestionHandlerTests.cs`

**Step 1: Test the generation handler**

```csharp
public class GenerateToolkitFromKbHandlerTests
{
    // Test: Returns minimal suggestion when no KB chunks found
    // Test: Builds correct prompt from chunks and calls LLM
    // Test: Returns fallback when LLM returns null
    // Test: Logs appropriate information
}
```

**Step 2: Test the apply handler**

```csharp
public class ApplyAiToolkitSuggestionHandlerTests
{
    // Test: Creates new GameToolkit when ToolkitId is null
    // Test: Updates existing GameToolkit when ToolkitId provided
    // Test: Maps all DiceTools correctly (enum parsing, fallback to D6)
    // Test: Maps CounterTools with isPerPlayer flag
    // Test: Maps TimerTools with type parsing
    // Test: Sets ScoringTemplate when provided
    // Test: Sets TurnTemplate when provided
    // Test: Stores reasoning in AgentConfig
    // Test: Throws NotFoundException for invalid ToolkitId
}
```

**Step 3: Run tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~GenerateToolkitFromKb"
```

**Step 4: Commit**

```bash
git add -A
git commit -m "test(toolkit): add unit tests for AI toolkit generation"
```

---

### Task P0-7: Frontend — Add Zod schema + API client methods

**Files:**
- Modify: `apps/web/src/lib/api/schemas/toolkit.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/libraryClient.ts` (or create a dedicated `gameToolkitClient.ts`)

**Step 1: Add Zod schemas for AI suggestion**

```typescript
// In toolkit.schemas.ts, add:

export const AiDiceToolSuggestionSchema = z.object({
  name: z.string(),
  diceType: z.string(),
  quantity: z.number(),
  customFaces: z.array(z.string()).nullable().optional(),
  color: z.string().nullable().optional(),
});

export const AiCounterToolSuggestionSchema = z.object({
  name: z.string(),
  minValue: z.number(),
  maxValue: z.number(),
  defaultValue: z.number(),
  isPerPlayer: z.boolean(),
  icon: z.string().nullable().optional(),
  color: z.string().nullable().optional(),
});

export const AiTimerToolSuggestionSchema = z.object({
  name: z.string(),
  durationSeconds: z.number(),
  timerType: z.string(),
  isPerPlayer: z.boolean(),
  warningThresholdSeconds: z.number().nullable().optional(),
});

export const AiScoringTemplateSuggestionSchema = z.object({
  dimensions: z.array(z.string()),
  defaultUnit: z.string(),
  scoreType: z.string(),
}).nullable();

export const AiTurnTemplateSuggestionSchema = z.object({
  turnOrderType: z.string(),
  phases: z.array(z.string()).nullable().optional(),
}).nullable();

export const AiToolkitSuggestionSchema = z.object({
  suggestedName: z.string(),
  diceTools: z.array(AiDiceToolSuggestionSchema),
  counterTools: z.array(AiCounterToolSuggestionSchema),
  timerTools: z.array(AiTimerToolSuggestionSchema),
  scoringTemplate: AiScoringTemplateSuggestionSchema,
  turnTemplate: AiTurnTemplateSuggestionSchema,
  overrides: z.object({
    overridesTurnOrder: z.boolean(),
    overridesScoreboard: z.boolean(),
    overridesDiceSet: z.boolean(),
  }),
  reasoning: z.string(),
});

export type AiToolkitSuggestion = z.infer<typeof AiToolkitSuggestionSchema>;
```

**Step 2: Add API client methods**

```typescript
// Add to the game toolkit API client (or libraryClient):

async generateToolkitFromKb(gameId: string): Promise<AiToolkitSuggestion> {
  const res = await this.httpClient.post(
    `/api/v1/game-toolkits/generate-for-game/${gameId}`
  );
  return AiToolkitSuggestionSchema.parse(res);
},

async applyAiSuggestion(
  toolkitId: string,
  suggestion: AiToolkitSuggestion
): Promise<GameToolkitDto> {
  const res = await this.httpClient.post(
    `/api/v1/game-toolkits/${toolkitId}/apply-ai-suggestion`,
    { suggestion }
  );
  return res as GameToolkitDto;
},
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(web): add AI toolkit suggestion Zod schemas + API client methods"
```

---

### Task P0-8: Frontend — AI generation UI in toolkit configurator

**Files:**
- Create: `apps/web/src/components/toolkit/AiToolkitGenerator.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx`

**Step 1: Create the AiToolkitGenerator component**

```tsx
'use client';

import React, { useState } from 'react';
import { Bot, Loader2, Check, X, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import type { AiToolkitSuggestion } from '@/lib/api/schemas/toolkit.schemas';

interface AiToolkitGeneratorProps {
  gameId: string;
  onGenerate: (gameId: string) => Promise<AiToolkitSuggestion>;
  onApply: (suggestion: AiToolkitSuggestion) => Promise<void>;
  onDismiss: () => void;
  isGenerating?: boolean;
  isApplying?: boolean;
}

export function AiToolkitGenerator({
  gameId,
  onGenerate,
  onApply,
  onDismiss,
  isGenerating = false,
  isApplying = false,
}: AiToolkitGeneratorProps) {
  const [suggestion, setSuggestion] = useState<AiToolkitSuggestion | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleGenerate = async () => {
    setError(null);
    try {
      const result = await onGenerate(gameId);
      setSuggestion(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Generation failed');
    }
  };

  const handleApply = async () => {
    if (!suggestion) return;
    try {
      await onApply(suggestion);
      setSuggestion(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to apply suggestion');
    }
  };

  // Before generation — show trigger button
  if (!suggestion && !isGenerating) {
    return (
      <Button
        variant="outline"
        onClick={handleGenerate}
        className="gap-2"
        disabled={isGenerating}
      >
        <Sparkles className="h-4 w-4" />
        Generate with AI
      </Button>
    );
  }

  // During generation — loading
  if (isGenerating) {
    return (
      <Card className="border-dashed border-amber-300 bg-amber-50/50">
        <CardContent className="flex items-center justify-center gap-3 py-8">
          <Loader2 className="h-5 w-5 animate-spin text-amber-600" />
          <span className="text-sm text-amber-700">
            Analyzing game rules and generating toolkit...
          </span>
        </CardContent>
      </Card>
    );
  }

  // After generation — review panel
  return (
    <Card className="border-amber-300 bg-amber-50/30">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Bot className="h-5 w-5 text-amber-600" />
          AI Suggestion: {suggestion?.suggestedName}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Summary */}
        <div className="flex flex-wrap gap-2">
          {(suggestion?.diceTools.length ?? 0) > 0 && (
            <Badge variant="secondary">
              {suggestion!.diceTools.length} dice tool(s)
            </Badge>
          )}
          {(suggestion?.counterTools.length ?? 0) > 0 && (
            <Badge variant="secondary">
              {suggestion!.counterTools.length} counter(s)
            </Badge>
          )}
          {(suggestion?.timerTools.length ?? 0) > 0 && (
            <Badge variant="secondary">
              {suggestion!.timerTools.length} timer(s)
            </Badge>
          )}
          {suggestion?.scoringTemplate && (
            <Badge variant="secondary">Scoring template</Badge>
          )}
          {suggestion?.turnTemplate && (
            <Badge variant="secondary">Turn template</Badge>
          )}
        </div>

        {/* Reasoning */}
        <p className="text-sm text-muted-foreground italic">
          {suggestion?.reasoning}
        </p>

        {/* Detail lists */}
        {(suggestion?.counterTools.length ?? 0) > 0 && (
          <div>
            <p className="text-xs font-semibold text-muted-foreground mb-1">Resources:</p>
            <div className="flex flex-wrap gap-1">
              {suggestion!.counterTools.map(c => (
                <Badge key={c.name} variant="outline" className="text-xs">
                  {c.icon ?? ''} {c.name} ({c.minValue}-{c.maxValue})
                </Badge>
              ))}
            </div>
          </div>
        )}

        {error && (
          <p className="text-sm text-red-600">{error}</p>
        )}

        {/* Actions */}
        <div className="flex gap-2 pt-2">
          <Button
            onClick={handleApply}
            disabled={isApplying}
            className="gap-2"
            size="sm"
          >
            {isApplying ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Check className="h-4 w-4" />
            )}
            Apply Suggestion
          </Button>
          <Button
            variant="ghost"
            onClick={onDismiss}
            size="sm"
            className="gap-2"
          >
            <X className="h-4 w-4" />
            Dismiss
          </Button>
          <Button
            variant="outline"
            onClick={handleGenerate}
            size="sm"
            className="gap-2"
          >
            <Sparkles className="h-4 w-4" />
            Regenerate
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
```

**Step 2: Integrate into toolkit configurator page**

In the configure `client.tsx`, add the `AiToolkitGenerator` component above the tool sections. Wire `onGenerate` to the API client's `generateToolkitFromKb()` and `onApply` to `applyAiSuggestion()`, then refresh the toolkit state.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(web): add AiToolkitGenerator component + integrate in configurator"
```

---

### Task P0-9: Frontend tests + PR

**Files:**
- Create: `apps/web/src/components/toolkit/__tests__/AiToolkitGenerator.test.tsx`

**Step 1: Write component tests**

```typescript
// Test: renders "Generate with AI" button initially
// Test: shows loading state during generation
// Test: displays suggestion summary after generation
// Test: shows reasoning text
// Test: calls onApply when "Apply" clicked
// Test: calls onDismiss when "Dismiss" clicked
// Test: regenerates when "Regenerate" clicked
// Test: shows error message on failure
```

**Step 2: Run all tests**

```bash
cd apps/web && pnpm test -- --run
cd apps/api && dotnet test --filter "FullyQualifiedName~GameToolkit"
```

**Step 3: Commit and create PR**

```bash
git add -A
git commit -m "test(web): add AiToolkitGenerator component tests"
git push -u origin feature/toolkit-ai-generation
# PR to main-dev
```

---

# Phase 1 — Template Marketplace (Admin/Editor with Approval)

**Goal:** Admins can create/manage toolkit templates. Editors can submit templates that require admin approval before becoming available.

**Key Design Decisions:**
- Templates are NOT a new entity — they are published `GameToolkit` records with a new `TemplateStatus` field
- Editors see "Submit for Review" instead of "Publish"
- Admins see a review queue and can approve/reject

---

### Task P1-1: Add TemplateStatus enum + migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Enums/TemplateStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/GameToolkit.cs`

**Step 1: Create enum**

```csharp
namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

public enum TemplateStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected
}
```

**Step 2: Add fields to GameToolkit entity**

```csharp
// Add to GameToolkit.cs properties:
public TemplateStatus TemplateStatus { get; private set; } = TemplateStatus.Draft;
public bool IsTemplate { get; private set; }
public string? ReviewNotes { get; private set; }
public Guid? ReviewedByUserId { get; private set; }
public DateTime? ReviewedAt { get; private set; }

// Add methods:
public void SubmitForReview()
{
    if (TemplateStatus != TemplateStatus.Draft && TemplateStatus != TemplateStatus.Rejected)
        throw new ConflictException("Can only submit drafts or rejected templates for review.");
    TemplateStatus = TemplateStatus.PendingReview;
    IsTemplate = true;
    UpdatedAt = DateTime.UtcNow;
}

public void ApproveTemplate(Guid adminUserId, string? notes = null)
{
    if (TemplateStatus != TemplateStatus.PendingReview)
        throw new ConflictException("Can only approve templates pending review.");
    TemplateStatus = TemplateStatus.Approved;
    ReviewedByUserId = adminUserId;
    ReviewedAt = DateTime.UtcNow;
    ReviewNotes = notes;
    UpdatedAt = DateTime.UtcNow;
}

public void RejectTemplate(Guid adminUserId, string notes)
{
    if (TemplateStatus != TemplateStatus.PendingReview)
        throw new ConflictException("Can only reject templates pending review.");
    TemplateStatus = TemplateStatus.Rejected;
    ReviewedByUserId = adminUserId;
    ReviewedAt = DateTime.UtcNow;
    ReviewNotes = notes;
    UpdatedAt = DateTime.UtcNow;
}

public void MarkAsTemplate()
{
    IsTemplate = true;
    UpdatedAt = DateTime.UtcNow;
}
```

**Step 3: Create migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddTemplateStatusToGameToolkit
```

**Step 4: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add TemplateStatus enum + template workflow to GameToolkit entity"
```

---

### Task P1-2: Template commands + handlers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ToolkitCommands.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Handlers/ToolkitCommandHandlers.cs`

**Step 1: Add commands**

```csharp
internal record SubmitTemplateForReviewCommand(Guid ToolkitId, Guid UserId) : IRequest<GameToolkitDto>;
internal record ApproveTemplateCommand(Guid ToolkitId, Guid AdminUserId, string? Notes) : IRequest<GameToolkitDto>;
internal record RejectTemplateCommand(Guid ToolkitId, Guid AdminUserId, string Notes) : IRequest<GameToolkitDto>;
internal record CloneFromTemplateCommand(Guid TemplateId, Guid GameId, Guid UserId) : IRequest<GameToolkitDto>;
```

**Step 2: Implement handlers for submit/approve/reject/clone**

Each handler follows the same pattern: load entity, call domain method, save, return DTO.
`CloneFromTemplateCommand` creates a new GameToolkit by copying all tools from the template.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add template submit/approve/reject/clone commands + handlers"
```

---

### Task P1-3: Template query + repository methods

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/ToolkitQueries.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Repositories/IGameToolkitRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/GameToolkitRepository.cs`

**Step 1: Add queries**

```csharp
internal record GetApprovedTemplatesQuery(TemplateCategory? Category)
    : IRequest<IReadOnlyList<GameToolkitDto>>;
internal record GetPendingReviewTemplatesQuery()
    : IRequest<IReadOnlyList<GameToolkitDto>>;
```

**Step 2: Add repository methods**

```csharp
Task<IReadOnlyList<GameToolkit>> GetApprovedTemplatesAsync(
    TemplateCategory? category, CancellationToken ct);
Task<IReadOnlyList<GameToolkit>> GetPendingReviewAsync(CancellationToken ct);
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add template queries + repository methods"
```

---

### Task P1-4: Template API endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/GameToolkitRoutes.cs`

**Step 1: Add endpoints**

```
POST /api/v1/game-toolkits/{id}/submit-for-review    → SubmitTemplateForReviewCommand
POST /api/v1/game-toolkits/{id}/approve               → ApproveTemplateCommand (admin)
POST /api/v1/game-toolkits/{id}/reject                 → RejectTemplateCommand (admin)
GET  /api/v1/game-toolkits/templates                   → GetApprovedTemplatesQuery
GET  /api/v1/game-toolkits/templates/pending-review    → GetPendingReviewTemplatesQuery (admin)
POST /api/v1/game-toolkits/clone-from-template/{templateId} → CloneFromTemplateCommand
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(toolkit): add template marketplace API endpoints"
```

---

### Task P1-5: Frontend — Template browser page

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/templates/page.tsx`

**Step 1: Create template browse page**

Shows approved templates in a grid. Each template card shows:
- Name, category badge, tool counts
- "Use This Template" button → calls `cloneFromTemplate(templateId, gameId)`
- Filter by `TemplateCategory`

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add toolkit template browser page"
```

---

### Task P1-6: Frontend — Submit for review UI

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx`

**Step 1:** Add "Submit for Review" button (visible to editors, replaces "Publish" for non-admins). Shows status badge (Draft, Pending, Approved, Rejected + review notes).

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add template submission UI for editors"
```

---

### Task P1-7: Admin — Template review queue page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/templates/page.tsx`

**Step 1:** Admin page showing pending templates with Approve/Reject actions + notes textarea.

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add admin template review queue page"
```

---

### Task P1-8: Frontend — Zod schemas + API client methods for templates

**Files:**
- Modify: `apps/web/src/lib/api/schemas/toolkit.schemas.ts`
- Add methods to API client

**Step 1:** Add `TemplateStatus` schema, template list response schema, and 6 API client methods (getApproved, getPending, submit, approve, reject, clone).

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add template Zod schemas + API client methods"
```

---

### Task P1-9: Backend tests for template workflow

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/TemplateWorkflowTests.cs`

**Step 1:** Test domain methods: SubmitForReview (from Draft/Rejected), ApproveTemplate, RejectTemplate, invalid state transitions throw ConflictException.

**Step 2: Commit**

```bash
git add -A
git commit -m "test(toolkit): add template workflow domain tests"
```

---

### Task P1-10: Frontend tests + PR

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/templates/__tests__/page.test.tsx`

**Step 1:** Test template browser: renders grid, filters by category, clone button calls API.

**Step 2: Commit + PR**

```bash
git add -A
git commit -m "test(web): add template browser page tests"
git push -u origin feature/toolkit-templates
# PR to main-dev
```

---

# Phase 2 — Widget Real-Time Sync

**Goal:** Extend existing SSE infrastructure to sync the most-used toolkit widgets between session participants. Priority: Scores (already done) > Turns > Dice > Resources > Notes.

**Key Pattern:** The existing `useSessionSync` hook connects to `/api/v1/game-sessions/{sessionId}/stream`. Widget state changes are broadcast as SSE events with type `session:widget-state`.

---

### Task P2-1: Backend — Widget state broadcast SSE event type

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/SseEventTypeMapper.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateWidgetStateCommand.cs`

**Step 1:** Add `WidgetStateUpdated` event type to SSE mapper. When `UpdateWidgetStateCommand` is handled, broadcast the updated widget state to all session participants via the existing SSE channel.

**Step 2:** Add `BroadcastWidgetStateAsync` method to the session SSE service that emits:
```json
event: session:widget-state
data: {"widgetType": "TurnManager", "stateJson": "{...}", "updatedBy": "userId"}
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(session): add widget-state SSE event broadcasting"
```

---

### Task P2-2: Backend — Widget state update endpoint

**Files:**
- Check existing: `apps/api/src/Api/Routing/` for session widget state routes
- Add if missing: `PATCH /api/v1/game-sessions/{sessionId}/widget-state/{widgetType}`

**Step 1:** Create or verify endpoint that accepts `{stateJson: string}`, validates the widget type, persists to `ToolkitSessionState`, and broadcasts SSE.

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(session): add widget state update endpoint with SSE broadcast"
```

---

### Task P2-3: Frontend — useWidgetSync hook

**Files:**
- Create: `apps/web/src/lib/hooks/useWidgetSync.ts`

**Step 1: Create the hook**

```typescript
import { useCallback, useEffect, useRef } from 'react';

interface UseWidgetSyncOptions {
  sessionId: string;
  widgetType: string;
  onRemoteUpdate: (stateJson: string, updatedBy: string) => void;
  enabled?: boolean;
}

export function useWidgetSync({
  sessionId,
  widgetType,
  onRemoteUpdate,
  enabled = true,
}: UseWidgetSyncOptions) {
  // Subscribe to SSE session:widget-state events filtered by widgetType
  // Provide broadcastState(stateJson) to push local changes to server
  // Debounce outgoing updates (200ms for turns/scores, 500ms for resources/notes)

  const broadcastState = useCallback(async (stateJson: string) => {
    if (!enabled) return;
    await fetch(
      `/api/v1/game-sessions/${sessionId}/widget-state/${widgetType}`,
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ stateJson }),
      }
    );
  }, [sessionId, widgetType, enabled]);

  return { broadcastState };
}
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add useWidgetSync hook for real-time widget state"
```

---

### Task P2-4: Integrate sync into TurnManager + ScoreTracker widgets

**Files:**
- Modify: `apps/web/src/components/toolkit/TurnManagerWidget.tsx`
- Modify: `apps/web/src/components/toolkit/ScoreTrackerWidget.tsx`

**Step 1:** Add optional `sessionId` prop. When provided, use `useWidgetSync` to:
- Call `broadcastState()` on every `persistState()` / `persist()` call
- Listen for remote updates and apply to local state (merge, not overwrite)

**Step 2:** Ensure idempotency: ignore remote updates that match the last local update (prevent echo).

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(web): add real-time sync to TurnManager + ScoreTracker widgets"
```

---

### Task P2-5: Integrate sync into ResourceManager + NoteManager widgets

**Files:**
- Modify: `apps/web/src/components/toolkit/ResourceManagerWidget.tsx`
- Modify: `apps/web/src/components/toolkit/NoteManagerWidget.tsx`

**Step 1:** Same pattern as P2-4. ResourceManager uses 500ms debounce for counter changes. NoteManager only syncs public notes (private notes stay local).

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add real-time sync to ResourceManager + NoteManager widgets"
```

---

### Task P2-6: Tests + PR

**Files:**
- Create: `apps/web/src/lib/hooks/__tests__/useWidgetSync.test.ts`
- Update existing widget tests for sync props

**Step 1:** Test useWidgetSync: broadcastState calls fetch, SSE events trigger onRemoteUpdate, debouncing works.

**Step 2: Run all tests, commit, PR**

```bash
git add -A
git commit -m "test(web): add useWidgetSync tests + update widget tests for sync"
git push -u origin feature/toolkit-widget-sync
# PR to main-dev
```

---

# Phase 3 — Whiteboard Storage Fix

**Goal:** Prevent whiteboard PNG base64 from bloating the database. Debounce saves and compress output.

---

### Task P3-1: Frontend — Debounce + compress whiteboard saves

**Files:**
- Modify: `apps/web/src/components/toolkit/WhiteboardWidget.tsx`

**Step 1:** Replace immediate `onStateChange` call on `stopDraw` with a debounced save:

```typescript
// Add debounce ref
const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

const debouncedSave = useCallback(() => {
  if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
  saveTimeoutRef.current = setTimeout(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    // Use WebP at 0.5 quality instead of PNG (60-80% smaller)
    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        const reader = new FileReader();
        reader.onloadend = () => {
          onStateChange?.(JSON.stringify({
            imageData: reader.result as string,
            format: 'webp'
          }));
        };
        reader.readAsDataURL(blob);
      },
      'image/webp',
      0.5
    );
  }, 3000); // 3-second debounce
}, [onStateChange]);
```

Replace `onStateChange` in `stopDraw` and `clear` with `debouncedSave()`.

**Step 2: Commit**

```bash
git add -A
git commit -m "fix(web): debounce + compress whiteboard saves (WebP 0.5 quality, 3s debounce)"
```

---

### Task P3-2: Backend — Limit widget state size + cleanup job

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/ToolkitSessionState.cs`

**Step 1:** Add size validation in `UpdateWidgetState`:

```csharp
public void UpdateWidgetState(string widgetType, string stateJson)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(widgetType);
    ArgumentException.ThrowIfNullOrWhiteSpace(stateJson);

    // Limit individual widget state to 500KB
    if (stateJson.Length > 512_000)
        throw new ArgumentException(
            $"Widget state exceeds maximum size (500KB). Current: {stateJson.Length / 1024}KB",
            nameof(stateJson));

    // ... existing logic
}
```

**Step 2: Commit + PR**

```bash
git add -A
git commit -m "fix(session): add 500KB size limit for widget state + whiteboard compression"
git push -u origin feature/toolkit-whiteboard-fix
# PR to main-dev
```

---

# Phase 4 — Session Analytics Dashboard

**Goal:** Users can view their play statistics: win rates, score trends, game frequency, session duration.

---

### Task P4-1: Backend — Session statistics query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionStatisticsDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionStatisticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Handlers/GetSessionStatisticsHandler.cs`

**Step 1: Create DTOs**

```csharp
internal record SessionStatisticsDto(
    int TotalSessions,
    int TotalGamesPlayed,
    TimeSpan AverageSessionDuration,
    List<GamePlayFrequencyDto> MostPlayedGames,
    List<ScoreTrendDto> RecentScoreTrends,
    List<MonthlyPlayCountDto> MonthlyActivity
);

internal record GamePlayFrequencyDto(
    Guid GameId,
    string GameName,
    int PlayCount,
    DateTime LastPlayed
);

internal record ScoreTrendDto(
    Guid SessionId,
    DateTime PlayedAt,
    string GameName,
    int FinalScore,
    int Rank  // 1 = winner
);

internal record MonthlyPlayCountDto(
    int Year,
    int Month,
    int SessionCount
);
```

**Step 2: Create query + handler**

```csharp
internal record GetSessionStatisticsQuery(
    Guid UserId,
    int MonthsBack = 6
) : IRequest<SessionStatisticsDto>;
```

Handler aggregates data from `LiveGameSession` + `ToolkitSessionState` tables using EF Core LINQ group-by queries.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(session): add session statistics query + DTOs"
```

---

### Task P4-2: Backend — Per-game statistics query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameStatisticsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Handlers/GetGameStatisticsHandler.cs`

**Step 1: Create per-game query**

```csharp
internal record GetGameStatisticsQuery(
    Guid UserId,
    Guid GameId
) : IRequest<GameStatisticsDto>;

internal record GameStatisticsDto(
    Guid GameId,
    string GameName,
    int TotalPlays,
    int Wins,
    double WinRate,
    double AverageScore,
    int HighScore,
    TimeSpan AverageSessionDuration,
    List<ScoreTrendDto> ScoreHistory
);
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(session): add per-game statistics query"
```

---

### Task P4-3: Backend — Statistics endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/SessionStatisticsRoutes.cs`
- Modify: `apps/api/src/Api/Program.cs` (register routes)

**Step 1: Add endpoints**

```
GET /api/v1/session-statistics           → GetSessionStatisticsQuery
GET /api/v1/session-statistics/game/{gameId} → GetGameStatisticsQuery
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(session): add statistics API endpoints"
```

---

### Task P4-4: Frontend — Zod schemas + API client

**Files:**
- Create: `apps/web/src/lib/api/schemas/session-statistics.schemas.ts`
- Create: `apps/web/src/lib/api/clients/sessionStatisticsClient.ts`

**Step 1: Add schemas and client**

```typescript
export const SessionStatisticsSchema = z.object({
  totalSessions: z.number(),
  totalGamesPlayed: z.number(),
  averageSessionDuration: z.string(), // TimeSpan as ISO string
  mostPlayedGames: z.array(GamePlayFrequencySchema),
  recentScoreTrends: z.array(ScoreTrendSchema),
  monthlyActivity: z.array(MonthlyPlayCountSchema),
});

// Client methods:
// getStatistics(): Promise<SessionStatistics>
// getGameStatistics(gameId: string): Promise<GameStatistics>
```

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add session statistics Zod schemas + API client"
```

---

### Task P4-5: Frontend — Statistics overview page

**Files:**
- Create: `apps/web/src/app/(authenticated)/toolkit/stats/page.tsx`

**Step 1:** Create dashboard page with:
- KPI cards: Total Sessions, Games Played, Win Rate, Avg Duration
- Monthly activity bar chart (shadcn/ui Recharts)
- Most played games list
- Recent score trends line chart

Use existing design tokens: glassmorphic cards, font-quicksand headings, amber accents.

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add session statistics overview page"
```

---

### Task P4-6: Frontend — Per-game stats section

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` (or create a stats tab)

**Step 1:** Add a "Statistics" section to the game detail page showing:
- Play count, win rate, high score
- Score history chart
- Average session duration

**Step 2: Commit**

```bash
git add -A
git commit -m "feat(web): add per-game statistics section"
```

---

### Task P4-7: Tests + PR

**Files:**
- Create backend tests for statistics handlers
- Create frontend tests for stats page

**Step 1: Backend tests**

```csharp
// Test: Returns correct aggregation for user with sessions
// Test: Returns zeros for user with no sessions
// Test: MonthlyActivity groups correctly across year boundaries
// Test: WinRate calculated correctly (wins / total)
```

**Step 2: Frontend tests**

```typescript
// Test: Renders KPI cards with correct values
// Test: Renders monthly chart
// Test: Shows "No sessions" empty state
// Test: Per-game stats load correctly
```

**Step 3: Run all tests, commit, PR**

```bash
git add -A
git commit -m "test(toolkit): add session statistics tests"
git push -u origin feature/toolkit-session-analytics
# PR to main-dev
```

---

# Dependency Graph

```
P3 (Whiteboard Fix) ─── no dependencies, can start immediately
        │
P0 (AI Generation) ─── needs KB chunks populated for target game
        │
P2 (Widget Sync) ──── independent, can parallelize with P0
        │
P1 (Templates) ─────── depends on P0 (AI-generated toolkits feed templates)
        │
P4 (Analytics) ──────── independent, can parallelize with P1
```

**Recommended execution order:**
1. P3 (smallest, 2 tasks, immediate value)
2. P0 + P2 in parallel (P0 = 9 tasks, P2 = 6 tasks)
3. P1 after P0 (10 tasks, builds on AI generation)
4. P4 any time (7 tasks, fully independent)

---

# Summary

| Phase | Tasks | New Files | Modified Files | Tests |
|-------|-------|-----------|----------------|-------|
| P0 | 9 | ~6 | ~4 | ~15 test cases |
| P1 | 10 | ~5 | ~5 | ~12 test cases |
| P2 | 6 | ~2 | ~5 | ~10 test cases |
| P3 | 2 | 0 | ~2 | ~3 test cases |
| P4 | 7 | ~8 | ~3 | ~12 test cases |
| **Total** | **34** | **~21** | **~19** | **~52 test cases** |
