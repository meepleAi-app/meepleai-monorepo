# Validator Coverage Phase 2 — Design Spec

**Goal**: Raise FluentValidation command validator coverage from ~61% to 95%+ on business commands by adding ~156 validators across 12 bounded contexts, fixing pre-existing build warnings, and introducing zero new warnings.

**Branch**: `feature/validator-coverage-phase2` from `frontend-dev`

**Scope**: Commands only (not queries). This project is a **single assembly** (`Api.csproj`), so one `AddValidatorsFromAssemblyContaining` call discovers all validators. Per-context DI registration lines are documentary only.

---

## Scope

### All bounded contexts — status

| Context | Total Cmds | Have Validator | Missing | In Scope? |
|---------|-----------|----------------|---------|-----------|
| GameManagement | 93 | 42 | 51 | Yes — 51 new |
| KnowledgeBase | 91 | 70 | 21 | Yes — ~20 new (1 excluded) |
| SharedGameCatalog | 62 | 51 | 11 | Yes — 11 new |
| Administration | 60 | 56 | 4 | Partial — 2 new, 2 excluded |
| UserLibrary | 34 | 34 | 0 | No — already 100% |
| SystemConfiguration | 33 | 15 | 18 | Yes — ~17 new (1 excluded) |
| DocumentProcessing | 33 | 25 | 8 | Yes — ~6 new (2 excluded) |
| SessionTracking | 28 | 11 | 17 | Yes — 17 new |
| UserNotifications | 24 | 7 | 17 | Yes — 17 new (1 excluded) |
| BusinessSimulations | 7 | 7 | 0 | No — already 100% (co-located validators inside command files) |
| WorkflowIntegration | 7 | 2 | 5 | Yes — 5 new |
| Authentication | 56 | 56 | 0 | No — already 100% |
| AgentMemory | 5 | 5 | 0 | No — already 100% |
| DatabaseSync | 5 | 0 | 5 | No — internal admin tool |
| EntityRelationships | 3 | 3 | 0 | No — already 100% (co-located in EntityLinkValidators.cs) |
| GameToolkit | 2 | 21 | 0 | No — already covered (ToolkitValidators.cs contains 21 validators) |
| GameToolbox | 0 | 0 | 0 | No — no commands |
| Gamification | 0 | 0 | 0 | No — queries only |
| **TOTAL** | **615** | **394** | **~156 in scope** | |

### Excluded commands (~15)

Internal/infrastructure commands without user-facing input:

- **Administration**: `SeedAdminUserCommand`, `SeedTestUserCommand`, `SeedE2ETestUsersCommand`, `SimulateErrorCommand`, `MigrateStorageCommand`
- **KnowledgeBase**: `SeedAgentDefinitionsCommand`
- **SystemConfiguration**: `SeedAiModelsCommand`
- **DatabaseSync**: all 5 commands (internal admin tool)
- **DocumentProcessing**: `CleanupOrphansCommand`, `PurgeStaleDocumentsCommand`
- **UserNotifications**: `RetryAllDeadLettersCommand` (internal ops)

### Already complete (no work needed)

- **Authentication** — 100% (56/56)
- **AgentMemory** — 100% (5/5)
- **UserLibrary** — 100% (34/34)
- **BusinessSimulations** — 100% (7/7, co-located validators inside command files)
- **EntityRelationships** — 100% (3/3, co-located in EntityLinkValidators.cs)
- **GameToolkit** — covered (21 validators in ToolkitValidators.cs)
- **GameToolbox** — 0 commands (queries only)
- **Gamification** — 0 commands (queries only)

---

## Validator Categories

### Category A — GUID-only (~60 commands)

Commands with only ID/Guid properties. One `NotEmpty()` rule per property.

```csharp
using FluentValidation;

namespace Api.BoundedContexts.{Context}.Application.{Subfolder};

internal sealed class {CommandName}Validator : AbstractValidator<{CommandName}>
{
    public {CommandName}Validator()
    {
        RuleFor(x => x.{GuidProperty})
            .NotEmpty().WithMessage("{PropertyName} is required");
    }
}
```

### Category B — Business input (~80 commands)

Commands with strings, numbers, dates, enums. Rules: length limits, range checks, format validation.

```csharp
internal sealed class {CommandName}Validator : AbstractValidator<{CommandName}>
{
    public {CommandName}Validator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be positive");
    }
}
```

### Category C — Complex (~25 commands)

Commands with lists, nested objects, cross-field validation, or async DB checks.

```csharp
internal sealed class {CommandName}Validator : AbstractValidator<{CommandName}>
{
    public {CommandName}Validator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Items are required")
            .Must(items => items.Count <= 100)
            .WithMessage("Cannot exceed 100 items");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");
    }
}
```

---

## Conventions

| Rule | Standard |
|------|----------|
| **Naming** | `{CommandName}Validator` |
| **Access** | `internal sealed class` always |
| **File pattern** | One validator per file preferred for new validators. Multi-validator files grouped by subdomain exist in ~21 files across the codebase (GameManagement, SessionTracking, GameToolkit, etc.). Follow the existing pattern in each context: if the subdomain already groups validators in a single file, add to that file. |
| **Location** | Follow existing context convention (see table below) |
| **Namespace** | Match the file's directory path |
| **Warnings** | Zero — no new warnings introduced |
| **Nullable** | Correct annotations; no `#pragma warning disable` |

### Validator location per context

Each agent MUST check existing validator locations before creating files. The table reflects the dominant pattern per context:

| Context | Primary Location | Notes |
|---------|-----------------|-------|
| Administration | `Application/Validators/` | Flat structure |
| BusinessSimulations | `Application/Validators/` | New directory — create it |
| DocumentProcessing | `Application/Validators/` | Flat structure |
| EntityRelationships | `Application/Validators/` | Flat structure |
| GameManagement | `Application/Validators/{Subdomain}/` | **Subdirectories**: GameNight/, GameNights/, GameReviews/, LiveSessions/, Playlists/, PlayRecords/, RuleConflictFAQs/, Session/, SessionSnapshot/, ToolState/. Place new validators in the matching subdomain directory. Create new subdirectory if command belongs to a new domain area not yet represented. |
| GameToolkit | `Application/Validators/` | Already has `ToolkitValidators.cs` with 21 validators — no new work needed |
| KnowledgeBase | `Application/Validators/` | Flat structure |
| SessionTracking | `Application/Commands/` | Co-located with command files |
| SharedGameCatalog | `Application/Commands/` | **Co-located with commands** (~95% of existing validators are here, not in Validators/) |
| SystemConfiguration | Mixed: `Application/Commands/{Folder}/` and `Application/Validators/` | Follow the location of the nearest existing validator for the same domain area. Default to `Application/Validators/` if no nearby pattern exists. |
| UserNotifications | `Application/Validators/` | Flat structure |
| WorkflowIntegration | `Application/Validators/` | Flat structure |

### Critical rule for agents

**Read before write**: Before creating any validator, the agent MUST:
1. Read the command record definition to get exact property names and types
2. Check where existing validators for that context live (verify the table above)
3. If the context has subdirectories (GameManagement), place the file in the correct subdomain folder

---

## DI Registration

This is a **single assembly** project. One `AddValidatorsFromAssemblyContaining<T>(includeInternalTypes: true)` call in `ApplicationServiceExtensions.AddFluentValidation()` scans the entire assembly and discovers all validators automatically.

The existing per-context registration lines are **documentary** (they show which contexts have validators) but are not functionally required. When adding validators to a new context, add a documentary registration line for consistency. Do not add lines for contexts that already have one.

---

## Parallelization Strategy

### Work streams

| Agent | Contexts | Estimated Validators |
|-------|----------|---------------------|
| **W1** | GameManagement (51) | ~51 |
| **W2** | KnowledgeBase (~20) + SharedGameCatalog (11) | ~31 |
| **W3** | SessionTracking (17) + UserNotifications (~17) + SystemConfiguration (~17) | ~51 |
| **W4** | DocumentProcessing (~6) + WorkflowIntegration (5) + Administration (2) | ~13 |

### Execution phases

```
Phase 0: dotnet restore apps/api/src/Api
         Create branch feature/validator-coverage-phase2 from frontend-dev

Phase 1: [W1] [W2] [W3] [W4] — parallel execution
         Each agent: read commands → create validators → build verify → commit per context

Phase 2: DI registration update (sequential, single file)
         Add documentary lines for new contexts in ApplicationServiceExtensions.cs

Phase 3: Full build + fix ALL warnings (pre-existing + new)
         dotnet build → fix warnings → dotnet test → coverage count
         Separate commit: "fix: resolve pre-existing build warnings"

Phase 4: Push + PR to frontend-dev

Phase 5: Code review (dispatch reviewer agent)

Phase 6: Fix review findings → push

Phase 7: Merge PR + cleanup branch (local + remote)

Phase 8: Update issue tracking (local + GitHub)
```

### Conflict avoidance

- Each agent works on separate context directories — no file conflicts
- `ApplicationServiceExtensions.cs` modified only in Phase 2 by a single sequential step
- Each agent runs `dotnet build apps/api/src/Api --no-restore` after each context

---

## Quality Gates

### Per-context (each agent, after each context)

1. `dotnet build apps/api/src/Api --no-restore` — 0 errors, 0 warnings
2. Commit: `feat(validation): add validators for {Context} commands`

### Full verification (Phase 3)

1. `dotnet build apps/api/src/Api` — 0 errors, 0 warnings (including pre-existing)
2. `dotnet test tests/Api.Tests` — all existing tests pass
3. Coverage count script:
   ```bash
   echo "Commands:" && find apps/api/src/Api/BoundedContexts -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" | wc -l
   echo "Validators:" && find apps/api/src/Api/BoundedContexts -name "*Validator.cs" | wc -l
   ```
4. Fix pre-existing warnings in separate commit: `fix: resolve pre-existing build warnings`

### Code review checklist (Phase 5)

- [ ] `internal sealed class` on all new validators
- [ ] Property names match command record definitions exactly
- [ ] No duplicate validators
- [ ] Location convention respected per context (especially GameManagement subdirs, SharedGameCatalog co-location)
- [ ] DI documentary lines added for new contexts
- [ ] Zero warnings (new or pre-existing)
- [ ] All existing tests pass

---

## Test Strategy

| Category | Unit Tests | Rationale |
|----------|-----------|-----------|
| A (GUID-only) | No | `NotEmpty()` on GUIDs — trivial, negative ROI |
| B (Business) | Selective | Only for non-obvious rules: regex patterns, range limits |
| C (Complex) | Yes | Cross-field, async, conditional logic needs coverage |

All existing tests must continue to pass. No test modifications unless fixing a pre-existing bug.

---

## Success Criteria

- Validator coverage reaches 95%+ of business commands
- Zero build warnings (new or pre-existing)
- All existing tests pass
- Single PR merged to `frontend-dev`
- Branch cleaned up after merge (local + remote)
