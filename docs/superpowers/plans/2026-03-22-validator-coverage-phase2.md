# Validator Coverage Phase 2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raise FluentValidation command validator coverage from ~61% to 95%+ across ~10 bounded contexts (~120 new validators).

**CRITICAL WARNING — Co-located validators:** Some commands have validators defined inside the command file itself (not in a separate `*Validator.cs` file). Discovery scripts MUST grep inside command files for `class.*Validator.*AbstractValidator` to avoid creating duplicates. Duplicate validators cause FluentValidation DI ambiguity errors at startup.

**Architecture:** Each MediatR command flows through `ValidationBehavior<TRequest, TResponse>` which auto-discovers validators via DI. This is a single assembly (`Api.csproj`) — one `AddValidatorsFromAssemblyContaining<T>(includeInternalTypes: true)` call scans everything. Adding a validator file is sufficient for it to be picked up at runtime.

**Tech Stack:** .NET 9, FluentValidation, MediatR, xUnit

**Spec:** `docs/superpowers/specs/2026-03-22-validator-coverage-phase2-design.md`

**Important conventions:**
- All validators: `internal sealed class`
- Naming: `{CommandName}Validator`
- Namespace: must match file directory path
- Zero warnings policy: no new warnings, fix pre-existing ones
- **Read before write**: always read the command record definition first to get exact property names/types
- **Follow existing location patterns**: see per-context table in spec

---

## Task 0: Setup

- [ ] **Step 1: Create feature branch**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout frontend-dev && git pull
git checkout -b feature/validator-coverage-phase2
git config branch.feature/validator-coverage-phase2.parent frontend-dev
```

- [ ] **Step 2: Restore NuGet packages**

```bash
dotnet restore apps/api/src/Api
```

- [ ] **Step 3: Verify clean build baseline**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -5
```

Expected: Build succeeded. Note any pre-existing warnings for Phase 3.

- [ ] **Step 4: Count current validators**

```bash
echo "Commands:" && find apps/api/src/Api/BoundedContexts -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f | wc -l
echo "Validators:" && find apps/api/src/Api/BoundedContexts -name "*Validator*.cs" -type f | wc -l
```

Record baseline numbers.

---

## Task 1 (W1): GameManagement — 51 validators

**Context:** `apps/api/src/Api/BoundedContexts/GameManagement/`
**Validator location:** `Application/Validators/{Subdomain}/` — use subdirectories matching the command's domain area.
**Existing subdirs:** GameNight/, GameNights/, GameReviews/, LiveSessions/, Playlists/, PlayRecords/, RuleConflictFAQs/, Session/, SessionSnapshot/, ToolState/

This is the largest context. The agent must:
1. Find all command files missing validators
2. Read each command record definition
3. Create validators in the correct subdomain subdirectory
4. If a multi-validator file already exists for that subdomain, add to it instead of creating a new file

- [ ] **Step 1: Discover all commands missing validators in GameManagement**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
for cmd in $(find apps/api/src/Api/BoundedContexts/GameManagement -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  # Check for separate validator files AND co-located validators inside command/multi-validator files
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/GameManagement/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read each missing command's record definition**

For every command file found in Step 1 that lacks a validator, read the command record to identify:
- All properties (name, type)
- Category: A (GUID-only), B (business input), or C (complex)

Use `grep -n "record" <file>` to quickly extract the record definition.

- [ ] **Step 3: Determine correct subdirectory for each validator**

Map each command to its subdomain by checking where similar commands/validators live. Examples:
- `AcquireEditorLockCommand` → check if related to LiveSessions or Session
- `CreateRuleCommentCommand` → likely RuleConflictFAQs/ or new RuleComments/
- `PublishGameNightCommand` → GameNight/ or GameNights/

If unsure, check existing validators in the same domain area.

- [ ] **Step 4: Create all Category A (GUID-only) validators**

For each GUID-only command, create a validator file. Template:

```csharp
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.{Subdomain};

internal sealed class {CommandName}Validator : AbstractValidator<{CommandName}>
{
    public {CommandName}Validator()
    {
        RuleFor(x => x.{GuidProperty})
            .NotEmpty().WithMessage("{PropertyName} is required");
    }
}
```

Ensure:
- `using` statement for the command's namespace if different from validator namespace
- All Guid properties get `NotEmpty()` rules
- Namespace matches directory path exactly

- [ ] **Step 5: Create all Category B (business input) validators**

For commands with string/int/date/enum properties, add appropriate rules:
- Strings: `NotEmpty()` + `MaximumLength(N)` where N matches domain constraints
- Numbers: `GreaterThan(0)` or appropriate range
- Dates: `Must(d => d > DateTime.UtcNow)` for future dates if applicable
- Enums: `IsInEnum()` if strongly typed

- [ ] **Step 6: Create all Category C (complex) validators**

For commands with lists, nested objects, or cross-field validation:
- Lists: `NotEmpty()` + `Must(x => x.Count <= N)`
- Cross-field: `GreaterThan(x => x.OtherField)`
- Conditional: `When(x => x.HasValue, () => ...)`

- [ ] **Step 7: Build to verify compilation**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

Expected: 0 errors, 0 warnings. If warnings appear, fix them before committing.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/
git commit -m "feat(validation): add validators for GameManagement commands"
```

---

## Task 2 (W2): KnowledgeBase + SharedGameCatalog — ~31 validators

### Task 2A: KnowledgeBase — ~20 validators

**Context:** `apps/api/src/Api/BoundedContexts/KnowledgeBase/`
**Validator location:** `Application/Validators/` (flat structure)
**Excluded:** `SeedAgentDefinitionsCommand`

- [ ] **Step 1: Discover missing validators in KnowledgeBase**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
for cmd in $(find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/KnowledgeBase/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read each missing command record definition**

Read each command file. Skip `SeedAgentDefinitionsCommand` (excluded). Categorize as A/B/C.

- [ ] **Step 3: Create all validators**

Place in `Application/Validators/`. Follow multi-validator file pattern if a grouped file already exists for that domain area.

- [ ] **Step 4: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/
git commit -m "feat(validation): add validators for KnowledgeBase commands"
```

### Task 2B: SharedGameCatalog — 11 validators

**Context:** `apps/api/src/Api/BoundedContexts/SharedGameCatalog/`
**Validator location:** `Application/Commands/` (co-located with commands, ~95% of existing validators are here)

- [ ] **Step 1: Discover missing validators in SharedGameCatalog**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/SharedGameCatalog -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/SharedGameCatalog/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read each missing command record definition**

- [ ] **Step 3: Create validators co-located with commands**

Place each validator in the same directory as its command file. Use the command's namespace.

- [ ] **Step 4: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/
git commit -m "feat(validation): add validators for SharedGameCatalog commands"
```

---

## Task 3 (W3): SessionTracking + UserNotifications + SystemConfiguration — ~51 validators

### Task 3A: SessionTracking — 17 validators

**Context:** `apps/api/src/Api/BoundedContexts/SessionTracking/`
**Validator location:** `Application/Commands/` (co-located with commands)
**Note:** Some multi-validator files exist (ChatCommandValidators.cs, DeckCommandValidators.cs, etc.). If a grouped file exists for the subdomain, add to it.

- [ ] **Step 1: Discover missing validators**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/SessionTracking -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  # Also check inside multi-validator files
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/SessionTracking/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read missing command definitions and check for existing multi-validator files**

For commands like `DrawCardsCommand`, `DiscardCardsCommand`, `ShuffleDeckCommand` — check if `DeckCommandValidators.cs` exists and add there.

- [ ] **Step 3: Create validators (new files or additions to multi-validator files)**

- [ ] **Step 4: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/
git commit -m "feat(validation): add validators for SessionTracking commands"
```

### Task 3B: UserNotifications — ~17 validators

**Context:** `apps/api/src/Api/BoundedContexts/UserNotifications/`
**Validator location:** `Application/Validators/` (flat structure)
**Excluded:** `RetryAllDeadLettersCommand`

- [ ] **Step 1: Discover missing validators**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/UserNotifications -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/UserNotifications/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read missing command definitions. Skip `RetryAllDeadLettersCommand`.**

- [ ] **Step 3: Create validators in `Application/Validators/`**

Key rules for this context:
- Email-related commands: validate email format with `.EmailAddress()` where applicable
- Slack commands: validate workspace ID, channel ID as NotEmpty
- Template commands: validate template name max length, content not empty
- Notification preference commands: validate preference keys

- [ ] **Step 4: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/
git commit -m "feat(validation): add validators for UserNotifications commands"
```

### Task 3C: SystemConfiguration — ~17 validators

**Context:** `apps/api/src/Api/BoundedContexts/SystemConfiguration/`
**Validator location:** Mixed — `Application/Commands/{Folder}/` and `Application/Validators/`. Follow nearest existing validator for the same domain area.
**Excluded:** `SeedAiModelsCommand`

- [ ] **Step 1: Discover missing validators**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/SystemConfiguration -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/SystemConfiguration/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read missing command definitions. Skip `SeedAiModelsCommand`.**

- [ ] **Step 3: For each validator, find the nearest existing validator in the same subdirectory**

If the command is in `Application/Commands/FeatureFlags/`, check if validators already exist there. If yes, follow that pattern. If no nearby validator, place in `Application/Validators/`.

- [ ] **Step 4: Create validators**

Key rules:
- Config commands: validate key/value not empty, max lengths
- Feature flag commands: validate flag name, tier validation
- Cache commands: validate cache key format
- Import/Export: validate format strings ("csv", "json", "xlsx")

- [ ] **Step 5: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SystemConfiguration/
git commit -m "feat(validation): add validators for SystemConfiguration commands"
```

---

## Task 4 (W4): Small contexts — ~21 validators

### Task 4A: DocumentProcessing — ~6 validators

**Context:** `apps/api/src/Api/BoundedContexts/DocumentProcessing/`
**Validator location:** `Application/Validators/`
**Excluded:** `CleanupOrphansCommand`, `PurgeStaleDocumentsCommand`

- [ ] **Step 1: Discover missing validators**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/DocumentProcessing -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/DocumentProcessing/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

- [ ] **Step 2: Read missing command definitions. Skip excluded commands (`CleanupOrphansCommand`, `PurgeStaleDocumentsCommand`).**

- [ ] **Step 3: Create validators**

- [ ] **Step 4: Build and commit**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/
git commit -m "feat(validation): add validators for DocumentProcessing commands"
```

### ~~Task 4B: BusinessSimulations~~ — SKIPPED (already 100% covered via co-located validators)

All 7 commands have validators co-located inside their command files. No new work needed.

### Task 4B: WorkflowIntegration — 5 validators

**Context:** `apps/api/src/Api/BoundedContexts/WorkflowIntegration/`
**Validator location:** `Application/Validators/`

- [ ] **Step 1: Read all 5 missing command definitions**

- [ ] **Step 2: Create validators**

Key rules:
- N8n config commands: validate config name, webhook URL format
- Workflow error logging: validate error message, workflow ID
- Delete: validate ID not empty

- [ ] **Step 3: Build and commit**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
git add apps/api/src/Api/BoundedContexts/WorkflowIntegration/
git commit -m "feat(validation): add validators for WorkflowIntegration commands"
```

### ~~Task 4D: EntityRelationships~~ — SKIPPED (already 100% covered, validators in EntityLinkValidators.cs)

### Task 4C: Administration — 2 validators

**Context:** `apps/api/src/Api/BoundedContexts/Administration/`
**Validator location:** `Application/Validators/`
**Excluded:** `SeedAdminUserCommand`, `SeedTestUserCommand`, `SeedE2ETestUsersCommand`, `SimulateErrorCommand`, `MigrateStorageCommand`

- [ ] **Step 1: Identify the 2 non-excluded missing commands**

```bash
for cmd in $(find apps/api/src/Api/BoundedContexts/Administration -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f); do
  name=$(basename "$cmd" .cs)
  found=$(grep -rl "class ${name}Validator" apps/api/src/Api/BoundedContexts/Administration/ 2>/dev/null | wc -l)
  if [ "$found" -eq 0 ]; then
    echo "MISSING: $name → $cmd"
  fi
done
```

Filter out excluded commands from the list.

- [ ] **Step 2: Read command definitions, create validators**

- [ ] **Step 3: Build and commit**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
git add apps/api/src/Api/BoundedContexts/Administration/
git commit -m "feat(validation): add remaining validators for Administration commands"
```

---

## Task 5: DI Registration Update

**File:** `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs`

Currently registered contexts (lines 198-233): Authentication, KnowledgeBase, Administration, SessionTracking, GameToolkit, EntityRelationships, BusinessSimulations, DatabaseSync, WorkflowIntegration.

Contexts with validators but NO documentary registration line:
- GameManagement
- SharedGameCatalog
- DocumentProcessing
- SystemConfiguration
- UserNotifications
- UserLibrary (already 100%)
- AgentMemory (already 100%)

Note: BusinessSimulations, DatabaseSync, and WorkflowIntegration are already registered — do NOT add duplicate lines.

- [ ] **Step 1: Read current AddFluentValidation method**

```bash
grep -n "AddValidatorsFrom\|AddFluentValidation" apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs
```

- [ ] **Step 2: Add documentary registration lines for missing contexts**

Add after the existing registration lines, following the same pattern:

```csharp
        // Register validators from GameManagement bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.GameManagement.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from SharedGameCatalog bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.SharedGameCatalog.Application.Commands.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from DocumentProcessing bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.DocumentProcessing.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from SystemConfiguration bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.SystemConfiguration.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from UserNotifications bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.UserNotifications.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from UserLibrary bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.UserLibrary.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);

        // Register validators from AgentMemory bounded context
        services.AddValidatorsFromAssemblyContaining<BoundedContexts.AgentMemory.Application.Validators.{FirstValidatorName}>(
            includeInternalTypes: true);
```

Replace `{FirstValidatorName}` with an actual validator class name from each context. Find it with:
```bash
find apps/api/src/Api/BoundedContexts/{Context} -name "*Validator*.cs" -type f | head -1
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build apps/api/src/Api --no-restore 2>&1 | tail -20
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs
git commit -m "feat(validation): add documentary DI registration for all validator contexts"
```

---

## Task 6: Full Verification & Warning Fixes

- [ ] **Step 1: Full build — check for ALL warnings**

```bash
dotnet build apps/api/src/Api 2>&1 | grep -i "warning\|error" | sort | uniq -c | sort -rn | head -30
```

- [ ] **Step 2: Fix all warnings (pre-existing + new)**

Common fixes:
- CS8604 (nullable): add null checks or `!` operator where safe
- CS0168 (unused variable): remove or use the variable
- CS8618 (non-nullable field): initialize in constructor
- Unused `using` statements: remove them

- [ ] **Step 3: Rebuild to confirm zero warnings**

```bash
dotnet build apps/api/src/Api 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Run all tests**

```bash
dotnet test tests/Api.Tests --no-restore 2>&1 | tail -30
```

Expected: All tests pass. If any fail, investigate — the cause is either a pre-existing issue or a new validator interfering with existing test data.

- [ ] **Step 5: Count final coverage**

```bash
echo "Command files:" && find apps/api/src/Api/BoundedContexts -name "*Command.cs" ! -name "*Handler*" ! -name "*Validator*" -type f | wc -l
echo "Validator classes (includes co-located):" && grep -rl "class.*Validator.*AbstractValidator" apps/api/src/Api/BoundedContexts/ | wc -l
```

Target: validator classes / command files ratio >= 95% for business commands. Note: some commands may have validators co-located in their own file or in multi-validator files, so the validator count may exceed the file count.

- [ ] **Step 6: Commit warning fixes separately**

```bash
git add -A
git commit -m "fix: resolve pre-existing build warnings across bounded contexts"
```

---

## Task 7: PR, Review, Merge & Cleanup

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/validator-coverage-phase2
```

- [ ] **Step 2: Create PR to frontend-dev**

```bash
gh pr create --base frontend-dev --title "feat(validation): raise command validator coverage to 95%+" --body "$(cat <<'EOF'
## Summary

- Added ~120 FluentValidation command validators across 10 bounded contexts
- Raised coverage from ~61% to 95%+ on business commands
- Fixed pre-existing build warnings
- Added documentary DI registration lines for all validator contexts
- Zero new warnings introduced

## Contexts covered

| Context | New Validators |
|---------|---------------|
| GameManagement | ~48 |
| KnowledgeBase | ~20 |
| SessionTracking | ~14 |
| UserNotifications | ~17 |
| SystemConfiguration | ~17 |
| SharedGameCatalog | ~10 |
| DocumentProcessing | ~6 |
| WorkflowIntegration | ~5 |
| Administration | ~2 |

## Excluded (internal/infra commands)

Seed commands, DatabaseSync, CleanupOrphans, PurgeStaleDocuments, SimulateError, MigrateStorage, RetryAllDeadLetters.
BusinessSimulations and EntityRelationships already had 100% coverage via co-located validators.

## Test plan

- [ ] `dotnet build` — 0 errors, 0 warnings
- [ ] `dotnet test` — all existing tests pass
- [ ] Validator coverage count >= 95% business commands
- [ ] No duplicate validators
- [ ] All validators use `internal sealed class`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Code review**

Dispatch code reviewer agent against the PR. Review checklist:
- `internal sealed class` on all new validators
- Property names match command record definitions
- No duplicate validators
- Location convention respected per context
- Zero warnings

- [ ] **Step 4: Fix review findings and push**

```bash
git add -A && git commit -m "fix: address code review findings"
git push
```

- [ ] **Step 5: Merge PR**

```bash
gh pr merge --squash --delete-branch
```

- [ ] **Step 6: Cleanup local branch**

```bash
git checkout frontend-dev && git pull
git branch -D feature/validator-coverage-phase2
git remote prune origin
```

---

## Parallelization Guide

Tasks 1-4 are independent and can run in parallel as separate agents:

| Agent | Task | Contexts |
|-------|------|----------|
| W1 | Task 1 | GameManagement (~48 validators) |
| W2 | Task 2 (2A + 2B) | KnowledgeBase + SharedGameCatalog (~31 validators) |
| W3 | Task 3 (3A + 3B + 3C) | SessionTracking + UserNotifications + SystemConfiguration (~38 validators) |
| W4 | Task 4 (4A + 4B + 4C) | DocumentProcessing + WorkflowIntegration + Administration (~13 validators) |

**Skipped contexts** (already 100% via co-located validators): BusinessSimulations, EntityRelationships

**Build serialization note**: Agents should not run `dotnet build` simultaneously — the shared `bin/` and `obj/` directories may cause transient failures. Stagger build steps or use `--output /tmp/build_{agent}` to isolate.

After all 4 agents complete → Task 5 (DI) → Task 6 (verification) → Task 7 (PR/merge). These run sequentially.
