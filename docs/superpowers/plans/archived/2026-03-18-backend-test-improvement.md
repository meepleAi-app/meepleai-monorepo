# Backend & Test Codebase Improvement — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce structural debt across backend and tests — split giant files, standardize handler organization, unify assertion library, close coverage gaps, consolidate repositories, and decompose User entity.

**Architecture:** Bottom-up surgical approach across 6 phases. Phases 1+3 parallelizable on different BCs. Phase 2 follows Phase 1 per-BC. Phase 4+5 after 1-3. Phase 6 last. All PRs target `main-dev`.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, FluentValidation, xUnit, FluentAssertions 8.8.0, EF Core 9, PostgreSQL 16, Testcontainers

**Spec:** `docs/superpowers/specs/2026-03-18-backend-test-improvement-design.md`

---

## Phase 1: File Splitting

### Task 1.1: Split SharedGameCatalogEndpoints.cs (3,346 lines)

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameSearchEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameAdminEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameUserEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameShareRequestEndpoints.cs`

- [ ] **Step 1: Create subdirectory and identify split boundaries**

```bash
# From repo root
ls apps/api/src/Api/Routing/
```

Open `SharedGameCatalogEndpoints.cs` and identify the `Map*` method groups. Each group becomes a separate file. The main orchestrator method stays in the original file.

- [ ] **Step 2: Create SharedGameSearchEndpoints.cs**

Extract `MapPublicEndpoints` (line ~64) and related handler methods into a new static class. Keep the same namespace.

```csharp
// apps/api/src/Api/Routing/SharedGameCatalog/SharedGameSearchEndpoints.cs
namespace Api.Routing;

internal static class SharedGameSearchEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Move MapPublicEndpoints content here
        // All MapGet for search, browse, trending
    }

    // Move all private handler methods referenced by these endpoints
}
```

- [ ] **Step 3: Create SharedGameAdminEndpoints.cs**

Extract `MapAdminEndpoints` (line ~172) and `MapAdminShareRequestEndpoints` (line ~556) with their handlers.

- [ ] **Step 4: Create SharedGameUserEndpoints.cs**

Extract `MapUserGameEndpoints` (line ~128) and related user-facing endpoints.

- [ ] **Step 5: Create SharedGameShareRequestEndpoints.cs**

Extract `MapUserShareRequestEndpoints` and remaining endpoint groups.

- [ ] **Step 6: Reduce original file to orchestrator**

```csharp
// apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs (reduced to ~30 lines)
namespace Api.Routing;

internal static class SharedGameCatalogEndpoints
{
    public static RouteGroupBuilder MapSharedGameCatalogEndpoints(this RouteGroupBuilder group)
    {
        SharedGameSearchEndpoints.Map(group);
        SharedGameAdminEndpoints.Map(group);
        SharedGameUserEndpoints.Map(group);
        SharedGameShareRequestEndpoints.Map(group);
        return group;
    }
}
```

- [ ] **Step 7: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeds. Zero functional changes — `Program.cs` still calls `MapSharedGameCatalogEndpoints()`.

- [ ] **Step 8: Run related tests**

```bash
cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog"
```

Expected: All tests pass (no behavior change).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalog/ apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs
git commit -m "refactor(routing): split SharedGameCatalogEndpoints into 4 focused files"
```

### Task 1.2: Split UserLibraryEndpoints.cs (2,161 lines)

**Files:**
- Modify: `apps/api/src/Api/Routing/UserLibraryEndpoints.cs`
- Create: `apps/api/src/Api/Routing/UserLibrary/LibraryCoreEndpoints.cs`
- Create: `apps/api/src/Api/Routing/UserLibrary/LibraryCollectionEndpoints.cs`
- Create: `apps/api/src/Api/Routing/UserLibrary/LibraryPdfEndpoints.cs`
- Create: `apps/api/src/Api/Routing/UserLibrary/LibraryLabelEndpoints.cs`

- [ ] **Step 1: Identify split boundaries**

Open `UserLibraryEndpoints.cs`. Map the 9 functional groups identified:
1. Core Library Management (Get/Add/Remove/Update) → `LibraryCoreEndpoints.cs`
2. Agent Configuration + Game Detail + Toolkit → `LibraryCoreEndpoints.cs`
3. Custom PDF Management → `LibraryPdfEndpoints.cs`
4. Label Management → `LibraryLabelEndpoints.cs`
5. Collection Management + Bulk ops → `LibraryCollectionEndpoints.cs`
6. Sharing + Ownership → `LibraryCoreEndpoints.cs`

- [ ] **Step 2: Create LibraryCoreEndpoints.cs**

Extract core library CRUD, agent config, game detail, toolkit, sharing, and ownership endpoints.

- [ ] **Step 3: Create LibraryCollectionEndpoints.cs**

Extract collection management and bulk operations.

- [ ] **Step 4: Create LibraryPdfEndpoints.cs**

Extract custom PDF upload/removal/status endpoints.

- [ ] **Step 5: Create LibraryLabelEndpoints.cs**

Extract label CRUD endpoints.

- [ ] **Step 6: Reduce original to orchestrator**

Same pattern as Task 1.1 Step 6.

- [ ] **Step 7: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build
cd apps/api/src/Api && dotnet test --filter "BoundedContext=UserLibrary"
git add apps/api/src/Api/Routing/UserLibrary/ apps/api/src/Api/Routing/UserLibraryEndpoints.cs
git commit -m "refactor(routing): split UserLibraryEndpoints into 4 focused files"
```

### Task 1.3: Split SessionTrackingEndpoints.cs (2,161 lines)

**Files:**
- Modify: `apps/api/src/Api/Routing/SessionTrackingEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionCommandEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionCardDeckEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionNotesEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionToolsEndpoints.cs`
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs`

- [ ] **Step 1: Identify split boundaries from 5 functional groups**

1. Core Session Commands (Create/Update/Finalize/Dice) → `SessionCommandEndpoints.cs`
2. Card Deck Management → `SessionCardDeckEndpoints.cs`
3. Private Notes → `SessionNotesEndpoints.cs`
4. Random Tools (Timer/Coin/Wheel) → `SessionToolsEndpoints.cs`
5. Query Endpoints (Get/Scoreboard/Details/History/Stream) → `SessionQueryEndpoints.cs`

- [ ] **Step 2-5: Create each file, extract corresponding endpoints and handlers**

Follow same pattern as Tasks 1.1/1.2.

- [ ] **Step 6: Reduce original to orchestrator, build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=SessionTracking"
git add apps/api/src/Api/Routing/SessionTracking/ apps/api/src/Api/Routing/SessionTrackingEndpoints.cs
git commit -m "refactor(routing): split SessionTrackingEndpoints into 5 focused files"
```

### Task 1.4: Split AdminUserEndpoints.cs (1,560 lines)

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminUserEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Admin/AdminUserSearchEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Admin/AdminUserCrudEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Admin/AdminUserTierEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Admin/AdminUserActivityEndpoints.cs`

- [ ] **Step 1: Identify split from 12+ groups**

Group by functional area:
1. Search + CRUD → `AdminUserCrudEndpoints.cs`
2. Tier + Level + Badges → `AdminUserTierEndpoints.cs`
3. Activity + Stats + Detail + Role History → `AdminUserActivityEndpoints.cs`
4. Bulk + Quick Actions + Impersonation + Invitations → `AdminUserSearchEndpoints.cs`

- [ ] **Step 2-5: Create files, extract, reduce orchestrator**

- [ ] **Step 6: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=Administration"
git commit -m "refactor(routing): split AdminUserEndpoints into 4 focused files"
```

### Task 1.5: Split PdfEndpoints.cs (1,354 lines)

**Files:**
- Modify: `apps/api/src/Api/Routing/PdfEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Pdf/PdfUploadEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs`
- Create: `apps/api/src/Api/Routing/Pdf/PdfProcessingEndpoints.cs`

- [ ] **Step 1: Split into 3 files from 9 groups**

1. Upload (Standard + Private + Chunked + BGG) → `PdfUploadEndpoints.cs`
2. Retrieval + Lifecycle → `PdfRetrievalEndpoints.cs`
3. Processing State + Actions + Admin List → `PdfProcessingEndpoints.cs`

- [ ] **Step 2-4: Create files, extract, reduce orchestrator**

- [ ] **Step 5: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=DocumentProcessing"
git commit -m "refactor(routing): split PdfEndpoints into 3 focused files"
```

### Task 1.6: Split EmailService.cs (2,744 lines)

**Files:**
- Modify: `apps/api/src/Api/Services/EmailService.cs`
- Create: `apps/api/src/Api/Services/Email/EmailTemplateService.cs`
- Create: `apps/api/src/Api/Services/Email/EmailSenderService.cs`
- Create: `apps/api/src/Api/Services/Email/EmailStorageService.cs`

- [ ] **Step 1: Analyze EmailService internal structure**

Read the file and identify method groups by responsibility:
- Template rendering methods → `EmailTemplateService`
- SMTP/sending methods → `EmailSenderService`
- Email record storage/retrieval → `EmailStorageService`

- [ ] **Step 2: Define interfaces for each new service**

```csharp
public interface IEmailTemplateService { /* template rendering methods */ }
public interface IEmailSenderService { /* send methods */ }
public interface IEmailStorageService { /* storage/retrieval methods */ }
```

- [ ] **Step 3: Extract EmailTemplateService.cs**

Move all template-related methods. Update DI registration.

- [ ] **Step 4: Extract EmailSenderService.cs**

Move SMTP/sending logic. Inject `IEmailTemplateService` where needed.

- [ ] **Step 5: Extract EmailStorageService.cs**

Move storage/retrieval. Update any handlers that inject `IEmailService` to inject the specific sub-service they need.

- [ ] **Step 6: Update DI registration**

```csharp
// In the relevant DI extension
services.AddScoped<IEmailTemplateService, EmailTemplateService>();
services.AddScoped<IEmailSenderService, EmailSenderService>();
services.AddScoped<IEmailStorageService, EmailStorageService>();
```

- [ ] **Step 7: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "refactor(services): split EmailService into Template/Sender/Storage"
```

### Task 1.7: Split UploadPdfCommandHandler.cs (1,803 lines)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingOrchestrator.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfChunkingService.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfEmbeddingService.cs`

- [ ] **Step 1: Analyze handler to identify extraction boundaries**

Read the handler. Identify:
- Orchestration logic (sequence of operations) → stays in handler (thin)
- PDF chunking/splitting logic → `PdfChunkingService`
- Embedding generation logic → `PdfEmbeddingService`
- Complex multi-step orchestration → `PdfProcessingOrchestrator`

- [ ] **Step 2: Extract PdfChunkingService**

Create interface + implementation. Move chunking methods.

- [ ] **Step 3: Extract PdfEmbeddingService**

Create interface + implementation. Move embedding methods.

- [ ] **Step 4: Extract PdfProcessingOrchestrator**

This orchestrates the full pipeline (validate → chunk → embed → store). Inject chunking and embedding services.

- [ ] **Step 5: Thin the handler**

Handler becomes ~100-200 lines: validate command → call orchestrator → return result.

```csharp
public async Task<UploadPdfResponse> Handle(UploadPdfCommand request, CancellationToken ct)
{
    // Validation (via FluentValidation pipeline)
    // Orchestrate
    var result = await _orchestrator.ProcessAsync(request.File, request.UserId, request.Options, ct);
    return new UploadPdfResponse(result.DocumentId, result.Status);
}
```

- [ ] **Step 6: Update DI, build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=DocumentProcessing"
git commit -m "refactor(handlers): extract orchestrator and services from UploadPdfCommandHandler"
```

### Task 1.8: Split PlaygroundChatCommandHandler.cs (1,175 lines) and SendAgentMessageCommandHandler.cs (920 lines)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/PlaygroundChatCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/SendAgentMessageCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ChatOrchestrationService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ChatContextBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/AgentMessageOrchestrator.cs`

- [ ] **Step 1: Analyze both handlers, identify shared logic**

Both likely share context-building and RAG retrieval patterns. Extract shared logic first.

- [ ] **Step 2: Extract ChatContextBuilder.cs**

Context assembly logic shared between playground and agent chat.

- [ ] **Step 3: Extract ChatOrchestrationService.cs**

Playground-specific orchestration (model selection, streaming, response assembly).

- [ ] **Step 4: Extract AgentMessageOrchestrator.cs**

Agent-specific orchestration (agent selection, tool calling, memory).

- [ ] **Step 5: Thin both handlers, update DI, build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=KnowledgeBase"
git commit -m "refactor(handlers): extract orchestrators from Chat and Agent handlers"
```

### Task 1.9: Split MeepleAiMetrics.cs (1,600 lines)

**Files:**
- Modify: `apps/api/src/Api/Observability/MeepleAiMetrics.cs`
- Create: `apps/api/src/Api/Observability/Metrics/AuthMetrics.cs`
- Create: `apps/api/src/Api/Observability/Metrics/GameMetrics.cs`
- Create: `apps/api/src/Api/Observability/Metrics/RagMetrics.cs`
- Create: `apps/api/src/Api/Observability/Metrics/PdfMetrics.cs`
- Create: `apps/api/src/Api/Observability/Metrics/UserMetrics.cs`

- [ ] **Step 1: Analyze metric groups**

Read the file. Group counters/histograms/gauges by domain.

- [ ] **Step 2: Create partial class files per domain**

```csharp
// apps/api/src/Api/Observability/Metrics/AuthMetrics.cs
namespace Api.Observability;

public static partial class MeepleAiMetrics
{
    public static class Auth
    {
        // Move all auth-related counters, histograms here
    }
}
```

- [ ] **Step 3: Repeat for Game, Rag, Pdf, User domains**

- [ ] **Step 4: Reduce original file to shared setup only**

Original file keeps `CreateMeter()`, shared configuration, and the partial class declaration.

- [ ] **Step 5: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "refactor(observability): split MeepleAiMetrics into domain-specific partial files"
```

### Task 1.10: PR for Phase 1

- [ ] **Step 1: Create PR**

```bash
git push -u origin main-dev
# If working on a feature branch:
# gh pr create --title "refactor: Phase 1 — split giant files" --base main-dev --body "..."
```

---

## Phase 2: Handler & Organization Standardization

### Task 2.1: Standardize KnowledgeBase BC handler layout

**Target pattern:** Commands and their handlers co-located in `Commands/`, queries and their handlers co-located in `Queries/`.

**Current state:** Handlers in dedicated `Handlers/` folder (~140 files), commands in `Commands/`, queries in `Queries/`.

- [ ] **Step 1: List all handlers to move**

```bash
find apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers -name "*Handler.cs" | wc -l
find apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers -name "*CommandHandler.cs" -o -name "*QueryHandler.cs" | head -20
```

- [ ] **Step 2: Move command handlers to Commands/**

For each `*CommandHandler.cs` in `Handlers/`, find its matching `*Command.cs` in `Commands/` and `git mv` the handler next to it.

```bash
# Example for one handler:
git mv apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/AskQuestionCommandHandler.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskQuestionCommandHandler.cs
```

Repeat for ALL command handlers.

- [ ] **Step 3: Move query handlers to Queries/**

Same pattern — move `*QueryHandler.cs` files next to their `*Query.cs` definitions.

- [ ] **Step 4: Handle subdomain modules (Evaluation, GridSearch, ContextEngineering)**

These have their own `Handlers/` folders. Apply same pattern within each subdomain.

- [ ] **Step 5: Remove empty Handlers/ directories**

```bash
find apps/api/src/Api/BoundedContexts/KnowledgeBase -type d -name "Handlers" -empty -delete
```

- [ ] **Step 6: Update namespaces**

After `git mv`, namespaces may need updating. Run build to find errors:

```bash
cd apps/api/src/Api && dotnet build 2>&1 | grep "error CS"
```

Fix namespace declarations in moved files to match new directory.

- [ ] **Step 7: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=KnowledgeBase"
git commit -m "refactor(kb): co-locate handlers with commands/queries"
```

### Task 2.2: Standardize GameManagement BC

**Current state:** `Handlers/` for commands, `QueryHandlers/` for queries — separated.

- [ ] **Step 1: Move command handlers from Handlers/ to Commands/**
- [ ] **Step 2: Move query handlers from QueryHandlers/ to Queries/**
- [ ] **Step 3: Remove empty directories, fix namespaces**
- [ ] **Step 4: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=GameManagement"
git commit -m "refactor(gm): co-locate handlers with commands/queries"
```

### Task 2.3: Standardize Authentication BC

**Current state:** Handlers in dedicated `Handlers/` folder.

Same pattern as Task 2.1. Move to `Commands/` and `Queries/`.

- [ ] **Steps 1-4: Move, fix namespaces, build, test, commit**

```bash
git commit -m "refactor(auth): co-locate handlers with commands/queries"
```

### Task 2.4: Standardize Administration BC (MIXED PATTERN)

**Current state:** Command handlers in `Handlers/`, query handlers ALREADY in `Queries/`. Mixed.

- [ ] **Step 1: Move command handlers from Handlers/ to Commands/**

Only command handlers need moving — query handlers are already co-located.

- [ ] **Step 2: Remove empty Handlers/ directory, fix namespaces**
- [ ] **Step 3: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=Administration"
git commit -m "refactor(admin): co-locate command handlers with commands (complete sweep)"
```

### Task 2.5: Standardize remaining BCs (batch)

**SharedGameCatalog** already has handlers co-located in `Commands/` — skip.

Apply the same pattern to: DocumentProcessing, UserLibrary, SessionTracking, SystemConfiguration, UserNotifications, BusinessSimulations, WorkflowIntegration, Gamification, AgentMemory, EntityRelationships, GameToolkit, GameToolbox.

- [ ] **Step 1: For each BC, check current handler location**

```bash
for bc in DocumentProcessing UserLibrary SessionTracking SystemConfiguration UserNotifications BusinessSimulations WorkflowIntegration Gamification AgentMemory EntityRelationships GameToolkit GameToolbox; do
  echo "=== $bc ==="
  find "apps/api/src/Api/BoundedContexts/$bc/Application" -name "*Handler.cs" -not -path "*/EventHandlers/*" | head -5
done
```

- [ ] **Step 2: Move handlers for each BC that needs it**
- [ ] **Step 3: Build full solution, run all tests**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
```

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(all): standardize handler co-location across remaining BCs"
```

### Task 2.6: PR for Phase 2

- [ ] **Create PR to main-dev**

---

## Phase 3: Test Standardization

### Task 3.1: Quarantine non-bulk-replaceable assertions

**Pre-requisite:** Identify files using Assert patterns that cannot be bulk-replaced.

- [ ] **Step 1: Find quarantine candidates**

```bash
cd apps/api/tests/Api.Tests
grep -rl "Assert\.Collection\b" --include="*.cs" > /tmp/quarantine_collection.txt
grep -rl "Assert\.All\b" --include="*.cs" > /tmp/quarantine_all.txt
grep -rl "Assert\.Raises" --include="*.cs" > /tmp/quarantine_raises.txt
grep -rl "Assert\.Equal.*IEqualityComparer\|Assert\.Equal.*comparer" --include="*.cs" > /tmp/quarantine_comparer.txt
cat /tmp/quarantine_*.txt | sort -u > /tmp/quarantine_all_files.txt
wc -l /tmp/quarantine_all_files.txt
```

- [ ] **Step 2: Record quarantine list**

Save the list. These files get manual migration AFTER the bulk pass.

### Task 3.2: Bulk migrate assertions — KnowledgeBase tests

- [ ] **Step 1: Identify KnowledgeBase test files using Assert.**

```bash
grep -rl "Assert\." apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase --include="*.cs" | \
  grep -v -f /tmp/quarantine_all_files.txt > /tmp/kb_assert_files.txt
wc -l /tmp/kb_assert_files.txt
```

- [ ] **Step 2: Apply bulk regex replacements**

For each file in the list, apply these transforms (using sed, IDE find-replace, or a script):

```
Assert.Equal(EXPECTED, ACTUAL)     → ACTUAL.Should().Be(EXPECTED)
Assert.True(EXPR)                  → EXPR.Should().BeTrue()
Assert.False(EXPR)                 → EXPR.Should().BeFalse()
Assert.Null(EXPR)                  → EXPR.Should().BeNull()
Assert.NotNull(EXPR)               → EXPR.Should().NotBeNull()
Assert.Contains(ITEM, COL)         → COL.Should().Contain(ITEM)
Assert.DoesNotContain(ITEM, COL)   → COL.Should().NotContain(ITEM)
Assert.Empty(COL)                  → COL.Should().BeEmpty()
Assert.NotEmpty(COL)               → COL.Should().NotBeEmpty()
Assert.Single(COL)                 → COL.Should().ContainSingle()
Assert.Throws<T>(ACTION)           → ACTION.Should().Throw<T>()
Assert.ThrowsAsync<T>(ACTION)      → (await ACTION).Should().ThrowAsync<T>()  // needs manual check
Assert.IsType<T>(OBJ)              → OBJ.Should().BeOfType<T>()
Assert.IsAssignableFrom<T>(OBJ)    → OBJ.Should().BeAssignableTo<T>()
Assert.StartsWith(EXP, ACT)        → ACT.Should().StartWith(EXP)
Assert.EndsWith(EXP, ACT)          → ACT.Should().EndWith(EXP)
Assert.InRange(ACT, LOW, HIGH)     → ACT.Should().BeInRange(LOW, HIGH)
```

**Important:** Add `using FluentAssertions;` to files that don't have it. Remove `using Xunit;` only if no other xUnit attributes remain (keep it if `[Fact]`, `[Theory]` present).

- [ ] **Step 3: Build and test KnowledgeBase**

```bash
cd apps/api/src/Api && dotnet build
cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase"
```

If failures > 0: review each failure. Common issues:
- Argument order swapped (xUnit is `Assert.Equal(expected, actual)`, FA is `actual.Should().Be(expected)`)
- Async assertions need `await`
- Generic type assertions differ

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(tests): migrate KnowledgeBase assertions to FluentAssertions"
```

### Task 3.3: Bulk migrate assertions — GameManagement tests

Repeat Task 3.2 pattern for GameManagement BC.

- [ ] **Step 1: Identify files, exclude quarantined**
- [ ] **Step 2: Apply bulk transforms**
- [ ] **Step 3: Build and test**

```bash
dotnet test --filter "BoundedContext=GameManagement"
```

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(tests): migrate GameManagement assertions to FluentAssertions"
```

### Task 3.4: Bulk migrate assertions — remaining BCs

Repeat for: SharedGameCatalog, Authentication, Administration, DocumentProcessing, UserLibrary, SessionTracking, SystemConfiguration, UserNotifications, BusinessSimulations, WorkflowIntegration, Gamification, AgentMemory, EntityRelationships, GameToolkit, GameToolbox.

- [ ] **Step 1: Batch process all remaining BCs**

Process one BC at a time. After each: build + test + commit.

- [ ] **Step 2: Migrate non-BC test files**

Files in `tests/Api.Tests/Integration/`, `tests/Api.Tests/E2E/`, `tests/Api.Tests/Services/`, `tests/Api.Tests/Routing/`, `tests/Api.Tests/Middleware/`.

- [ ] **Step 3: Full test run**

```bash
cd apps/api/src/Api && dotnet test
```

- [ ] **Step 4: Commit remaining**

```bash
git commit -m "refactor(tests): complete FluentAssertions migration for all remaining test files"
```

### Task 3.5: Manually migrate quarantined files

- [ ] **Step 1: Migrate Assert.Collection files**

Replace `Assert.Collection(collection, inspector1, inspector2)` with:
```csharp
collection.Should().SatisfyRespectively(
    item => item.Should()...,
    item => item.Should()...
);
```

- [ ] **Step 2: Migrate Assert.All files**

Replace `Assert.All(collection, action)` with:
```csharp
collection.Should().AllSatisfy(item => { /* action */ });
```

Verify FluentAssertions 8.8.0 supports `AllSatisfy` (it does — added in FA 6.x).

- [ ] **Step 3: Migrate Assert.Raises files**

Keep `Assert.Raises` as-is (no FA equivalent) OR convert to FluentAssertions `Monitor`:
```csharp
using var monitor = obj.Monitor();
obj.DoSomething();
monitor.Should().Raise("EventName");
```

- [ ] **Step 4: Migrate custom comparer files**

Replace `Assert.Equal(expected, actual, comparer)` with:
```csharp
actual.Should().BeEquivalentTo(expected, options => options.Using(comparer));
```

- [ ] **Step 5: Build, full test, commit**

```bash
cd apps/api/src/Api && dotnet test
git commit -m "refactor(tests): manually migrate quarantined assertion patterns"
```

### Task 3.6: Add missing Category traits (53 files)

- [ ] **Step 1: Find files without Category trait**

```bash
cd apps/api/tests/Api.Tests
# Find test classes without [Trait("Category"
find . -name "*Tests.cs" -not -path "*/bin/*" -not -path "*/obj/*" | while read f; do
  grep -L 'Trait.*Category' "$f"
done > /tmp/missing_traits.txt
wc -l /tmp/missing_traits.txt
```

- [ ] **Step 2: Add traits based on path**

For each file in the list, add the appropriate trait after the class declaration:

```csharp
// Determine category from path:
// */Integration/* → TestCategories.Integration
// */E2E/* → TestCategories.E2E
// */Security/* → TestCategories.Security
// */Performance/* → TestCategories.Performance
// default → TestCategories.Unit

// Determine BC from path:
// */BoundedContexts/{Name}/* → "{Name}"
```

- [ ] **Step 3: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "refactor(tests): add missing Category and BoundedContext traits to 53 files"
```

### Task 3.7: Consolidate test base classes

- [ ] **Step 1: Create UnitTestBase**

```csharp
// apps/api/tests/Api.Tests/Infrastructure/UnitTestBase.cs
namespace Api.Tests.Infrastructure;

public abstract class UnitTestBase
{
    // Common Moq setup, AutoFixture if used, shared test utilities
    protected readonly Mock<IMediator> MediatorMock = new();
    protected readonly CancellationToken CancellationToken = CancellationToken.None;
}
```

- [ ] **Step 2: Convert BC-specific TestBase classes to static helpers**

For each BC-specific `TestBase.cs` (~149 lines each), extract seeding/setup methods into static extension helpers:

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/GameManagement/TestHelpers/GameManagementTestHelpers.cs
namespace Api.Tests.BoundedContexts.GameManagement.TestHelpers;

public static class GameManagementTestHelpers
{
    public static async Task<Game> SeedTestGame(this MeepleAiDbContext db, Action<Game>? configure = null) { ... }
    public static async Task<GameSession> SeedTestSession(this MeepleAiDbContext db, Guid gameId) { ... }
}
```

- [ ] **Step 3: Update test classes to use new base + helpers**

Tests that inherited BC-specific `TestBase` now inherit `UnitTestBase` (for unit) or `SharedDatabaseTestBase` (for integration) and use static helpers for seeding.

- [ ] **Step 4: Remove old BC-specific TestBase classes**

Only after all tests in that BC are migrated.

- [ ] **Step 5: Build, full test, commit**

```bash
cd apps/api/src/Api && dotnet test
git commit -m "refactor(tests): consolidate test base classes into UnitTestBase + static helpers"
```

### Task 3.8: Audit files without visible assertions

- [ ] **Step 1: Run audit script**

```bash
cd apps/api/tests/Api.Tests
find . -name "*Tests.cs" -not -path "*/bin/*" -not -path "*/obj/*" | while read f; do
  has_should=$(grep -c "\.Should()" "$f" 2>/dev/null || echo 0)
  has_assert=$(grep -c "Assert\." "$f" 2>/dev/null || echo 0)
  has_verify=$(grep -c "\.Verify(" "$f" 2>/dev/null || echo 0)
  has_throw=$(grep -c "ThrowAsync\|Throw<" "$f" 2>/dev/null || echo 0)
  total=$((has_should + has_assert + has_verify + has_throw))
  if [ "$total" -eq 0 ]; then
    echo "NO_ASSERTIONS: $f"
  fi
done > /tmp/no_assertions.txt
wc -l /tmp/no_assertions.txt
```

- [ ] **Step 2: Review each flagged file**

For each file: determine if it's a valid pattern (e.g., testing that no exception is thrown) or actually missing assertions.

- [ ] **Step 3: Add assertions or mark as Skip**

Files genuinely missing assertions get proper assertions added. Truly unclear cases get `[Fact(Skip = "Missing assertions - needs review")]`.

- [ ] **Step 4: Commit**

```bash
git commit -m "fix(tests): add missing assertions to flagged test files"
```

### Task 3.9: PR for Phase 3

- [ ] **Create PR to main-dev**

---

## Phase 4: Test Coverage Gaps

### Task 4.1: Coverage baseline — measure current ratios

- [ ] **Step 1: Count source and test files per BC**

```bash
for bc in UserLibrary SessionTracking SystemConfiguration AgentMemory Administration GameManagement WorkflowIntegration; do
  src=$(find "apps/api/src/Api/BoundedContexts/$bc" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" \
    -not -path "*/Infrastructure/Configurations/*" -not -path "*/Infrastructure/Entities/*" | wc -l)
  test=$(find "apps/api/tests/Api.Tests/BoundedContexts/$bc" -name "*Tests.cs" -not -path "*/bin/*" 2>/dev/null | wc -l)
  echo "$bc: src=$src test=$test ratio=$(echo "scale=2; $test/$src" | bc)"
done
```

- [ ] **Step 2: Record baseline in this plan (update above)**

### Task 4.2: Generate tests for UserLibrary BC (Tier 1)

- [ ] **Step 1: List all commands/queries without tests**

```bash
# Find all handlers
find apps/api/src/Api/BoundedContexts/UserLibrary/Application -name "*Handler.cs" | sort
# Find all existing tests
find apps/api/tests/Api.Tests/BoundedContexts/UserLibrary -name "*Tests.cs" | sort
# Diff to find gaps
```

- [ ] **Step 2: For each uncovered handler, create test file**

Follow the template from spec:

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/{HandlerName}Tests.cs
namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddGameToLibraryCommandHandlerTests : UnitTestBase
{
    private readonly Mock<IUserLibraryRepository> _repoMock = new();
    private readonly AddGameToLibraryCommandHandler _sut;

    public AddGameToLibraryCommandHandlerTests()
    {
        _sut = new AddGameToLibraryCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldAddGameToLibrary()
    {
        // Arrange
        var command = new AddGameToLibraryCommand(UserId: Guid.NewGuid(), GameId: Guid.NewGuid());
        _repoMock.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.Handle(command, CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<LibraryEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyInLibrary_ShouldThrowConflictException()
    {
        // Arrange
        var command = new AddGameToLibraryCommand(UserId: Guid.NewGuid(), GameId: Guid.NewGuid());
        _repoMock.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.Handle(command, CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
```

- [ ] **Step 3: Create validator tests for each validator**

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddGameToLibraryCommandValidatorTests
{
    private readonly AddGameToLibraryCommandValidator _sut = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var command = new AddGameToLibraryCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _sut.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        var command = new AddGameToLibraryCommand(Guid.Empty, Guid.NewGuid());
        var result = _sut.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}
```

- [ ] **Step 4: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet test --filter "BoundedContext=UserLibrary"
git commit -m "test(user-library): add handler and validator tests for coverage gaps"
```

### Task 4.3: Generate tests for SessionTracking BC (Tier 1)

Same pattern as Task 4.2. Focus on:
- Session CRUD handlers
- Scoring handlers
- Note handlers
- Activity tracking handlers

- [ ] **Steps 1-4: List gaps, generate tests, build, test, commit**

```bash
git commit -m "test(session-tracking): add handler and validator tests for coverage gaps"
```

### Task 4.4: Generate tests for SystemConfiguration BC (Tier 1)

Focus on:
- Feature flag handlers
- Runtime config handlers
- Seeder tests

- [ ] **Steps 1-4: List gaps, generate tests, build, test, commit**

```bash
git commit -m "test(system-config): add handler and validator tests for coverage gaps"
```

### Task 4.5: Generate tests for AgentMemory BC (Tier 1)

**Caveat**: Small BC (49 source files). Focus on domain logic, not infrastructure/configuration.

- [ ] **Steps 1-4: List gaps, generate tests, build, test, commit**

```bash
git commit -m "test(agent-memory): add handler and validator tests for coverage gaps"
```

### Task 4.6: Generate tests for Tier 2 BCs (Administration, GameManagement, WorkflowIntegration)

- [ ] **Step 1: Batch identify gaps across 3 BCs**
- [ ] **Step 2: Generate tests per BC, one commit each**

```bash
git commit -m "test(admin): add handler and validator tests for coverage gaps"
git commit -m "test(game-mgmt): add handler and validator tests for coverage gaps"
git commit -m "test(workflow): add handler and validator tests for coverage gaps"
```

### Task 4.7: Security test expansion

- [ ] **Step 1: Create security tests for Authentication BC**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Authentication/Security/BruteForceProtectionTests.cs
[Trait("Category", TestCategories.Security)]
[Trait("BoundedContext", "Authentication")]
public class BruteForceProtectionTests : SharedDatabaseTestBase
{
    [Fact]
    public async Task Login_WithMultipleFailedAttempts_ShouldLockAccount() { ... }

    [Fact]
    public async Task Login_WithTamperedToken_ShouldReject() { ... }
}
```

- [ ] **Step 2: Security tests for Administration, KnowledgeBase, DocumentProcessing**

Follow same pattern. Focus on: role escalation, prompt injection, path traversal.

- [ ] **Step 3: Build, test, commit**

```bash
git commit -m "test(security): add security tests for auth, admin, kb, docproc"
```

### Task 4.8: Performance test expansion

- [ ] **Step 1: Create performance tests**

```csharp
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagQueryPerformanceTests : SharedDatabaseTestBase
{
    [Fact]
    public async Task RagQuery_ShouldCompleteWithin5Seconds() { ... }

    [Fact]
    public async Task ConcurrentSearch_10Users_ShouldMaintainLatency() { ... }
}
```

- [ ] **Step 2: Build, test, commit**

```bash
git commit -m "test(performance): add performance tests for rag, catalog, pdf"
```

### Task 4.9: Re-measure coverage ratios

- [ ] **Step 1: Re-run baseline measurement from Task 4.1**
- [ ] **Step 2: Verify all Tier 1 BCs reach 0.30+ ratio**
- [ ] **Step 3: PR for Phase 4**

---

## Phase 5: Repository Consolidation

### Task 5.1: Add ISpecification to SharedKernel

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Domain/Interfaces/ISpecification.cs`
- Modify: `apps/api/src/Api/SharedKernel/Infrastructure/RepositoryBase.cs`

- [ ] **Step 1: Create ISpecification interface**

```csharp
// apps/api/src/Api/SharedKernel/Domain/Interfaces/ISpecification.cs
namespace Api.SharedKernel.Domain.Interfaces;

public interface ISpecification<T>
{
    IQueryable<T> Apply(IQueryable<T> query);
}
```

- [ ] **Step 2: Add QueryAsync to RepositoryBase**

Read `RepositoryBase.cs` first, then add:

```csharp
public virtual async Task<PagedResult<TEntity>> QueryAsync<TEntity>(
    ISpecification<TEntity> spec, int page, int pageSize, CancellationToken ct)
    where TEntity : class
{
    var query = spec.Apply(_dbSet.OfType<TEntity>().AsQueryable());
    var total = await query.CountAsync(ct);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return new PagedResult<TEntity>(items, total, page, pageSize);
}
```

**Note:** Adapt the method signature to match existing `RepositoryBase` conventions (check generic params, `IEntity<TId>` constraints).

- [ ] **Step 3: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "feat(shared-kernel): add ISpecification pattern and QueryAsync to RepositoryBase"
```

### Task 5.2: Identify non-inheriting repositories

- [ ] **Step 1: List all repositories not extending RepositoryBase**

```bash
# Find all repository classes
grep -rl "class.*Repository" apps/api/src/Api/BoundedContexts --include="*.cs" | while read f; do
  if ! grep -q "RepositoryBase" "$f"; then
    echo "$f"
  fi
done > /tmp/repos_to_migrate.txt
wc -l /tmp/repos_to_migrate.txt
```

- [ ] **Step 2: Categorize by BC**

Group the list by bounded context. Prioritize Tier 1 BCs.

### Task 5.3: Migrate UserLibrary repositories

- [ ] **Step 1: For each repo, read current implementation**
- [ ] **Step 2: Inherit RepositoryBase, remove duplicated CRUD methods**

```csharp
// BEFORE
public class LibraryEntryRepository : ILibraryEntryRepository
{
    private readonly MeepleAiDbContext _context;
    public async Task<LibraryEntry?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _context.LibraryEntries.FindAsync(new object[] { id }, ct);
    // ... more boilerplate
}

// AFTER
public class LibraryEntryRepository : RepositoryBase<LibraryEntry, Guid>, ILibraryEntryRepository
{
    public LibraryEntryRepository(MeepleAiDbContext context) : base(context) { }
    // Only custom query methods remain
}
```

- [ ] **Step 3: Update ILibraryEntryRepository to extend IRepository<LibraryEntry, Guid>**

Remove methods that are now inherited.

- [ ] **Step 4: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test --filter "BoundedContext=UserLibrary"
git commit -m "refactor(user-library): migrate repositories to RepositoryBase"
```

### Task 5.4: Migrate SessionTracking, SystemConfiguration, AgentMemory repositories

Repeat Task 5.3 pattern for remaining Tier 1 BCs.

- [ ] **One commit per BC**

### Task 5.5: Migrate SharedGameCatalog and GameManagement repositories

Larger BCs — more repos to migrate.

- [ ] **One commit per BC**

### Task 5.6: PR for Phase 5

- [ ] **Create PR to main-dev**

---

## Phase 6: Entity Decomposition

### Task 6.1: Analyze User.cs current structure

**Files:**
- Read: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`

- [ ] **Step 1: Read and document every property and method**

```bash
# List all properties
grep -n "public.*{ get" apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs
# List all methods
grep -n "public.*(" apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs | grep -v "get\|set"
```

- [ ] **Step 2: Classify each property/method by BC ownership**

Create a mapping table:
- Authentication: Id, Email, PasswordHash, Salt, TwoFactor*, Lockout*, Sessions, OAuth*
- Administration: DisplayName, AvatarUrl, Bio, Role, Permissions
- BusinessSimulations: MonthlyBudget, TokenUsage, BillingPlan
- UserLibrary: NotificationPrefs, ThemePrefs, GamePrefs
- Gamification: Points, Level, Badges

### Task 6.2: Create read-only UserProfile entity (Administration)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/UserProfile.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/UserProfileConfiguration.cs`

- [ ] **Step 1: Create UserProfile as read-only projection of Users table**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/UserProfile.cs
namespace Api.BoundedContexts.Administration.Domain.Entities;

public class UserProfile
{
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public string Role { get; private set; } = string.Empty;

    // NO factory methods, NO setters, NO mutation methods
    // This is READ-ONLY during Steps 1-2
}
```

- [ ] **Step 2: Configure EF mapping to same Users table**

```csharp
// apps/api/src/Api/Infrastructure/EntityConfigurations/UserProfileConfiguration.cs
public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Users");  // Same table as Authentication.User
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("Id");
        builder.Property(u => u.DisplayName).HasColumnName("DisplayName");
        builder.Property(u => u.AvatarUrl).HasColumnName("AvatarUrl");
        builder.Property(u => u.Bio).HasColumnName("Bio");
        builder.Property(u => u.Role).HasColumnName("Role");

        // Mark as read-only: no tracked changes
        builder.HasNoKey();  // OR use .ToView("Users") if EF supports it
        // Alternative: just use AsNoTracking() in all queries
    }
}
```

**Note:** The exact EF configuration depends on existing User entity mapping. Read the existing `UserConfiguration.cs` first and adapt.

- [ ] **Step 3: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "feat(admin): add read-only UserProfile entity mapped to Users table"
```

### Task 6.3: Create read-only UserBudget entity

Same pattern as Task 6.2 for budget-related fields.

- [ ] **Create entity, configure EF, build, test, commit**

```bash
git commit -m "feat(admin): add read-only UserBudget entity mapped to Users table"
```

### Task 6.4: Create read-only UserPreferences and UserGamificationProfile

Same pattern for remaining entities.

- [ ] **Two commits, one per entity**

### Task 6.5: Wire new entities into queries

- [ ] **Step 1: Identify queries that read profile/budget/prefs data via User entity**

```bash
grep -rl "\.DisplayName\|\.AvatarUrl\|\.Bio\|\.Role" apps/api/src/Api/BoundedContexts/Administration --include="*.cs" | head -20
```

- [ ] **Step 2: Migrate Administration queries to use UserProfile**

Query handlers in Administration BC that read DisplayName, Role, etc. should query `UserProfile` (read-only) instead of loading the full `User` aggregate.

- [ ] **Step 3: Build, test, commit**

```bash
cd apps/api/src/Api && dotnet build && dotnet test
git commit -m "refactor(admin): use UserProfile for read queries instead of User aggregate"
```

### Task 6.6: Split MeepleAiMetrics.cs

Already covered in Phase 1 Task 1.9, but if not done there, execute here.

### Task 6.7: PR for Phase 6

- [ ] **Create PR to main-dev**

---

## Completion Checklist

After all 6 phases:

- [ ] No file in `Routing/` exceeds 500 lines
- [ ] No handler exceeds 200 lines
- [ ] No service exceeds 500 lines
- [ ] 100% of test files have Category trait
- [ ] 100% FluentAssertions (zero xUnit Assert.* outside quarantine)
- [ ] All Tier 1 BCs reach 0.30+ test file ratio
- [ ] All ~69 non-inheriting repositories migrated to RepositoryBase
- [ ] User entity has read-only BC projections (Steps 1-2)
- [ ] All handlers co-located with their commands/queries
- [ ] Test base classes consolidated (UnitTestBase + static helpers)
