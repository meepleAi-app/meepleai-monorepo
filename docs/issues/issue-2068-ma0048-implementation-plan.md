# Issue #2068: MA0048 Implementation Plan

## Problem
874 MA0048 warnings where file name doesn't match type name.

## Analysis Results
- **Total warnings**: 874
- **Unique files**: 167
- **Largest offender**: `Models/Contracts.cs` (176 warnings)

## Selected Approach: Option B - Strategic Split + Suppression

### Rationale
1. **DDD Architecture**: Project uses bounded contexts with related types grouped
2. **Cohesion Preservation**: Request/Response/Result records belong together
3. **Pragmatic Balance**: Split where organization improves, suppress where cohesion matters
4. **Manageable Scope**: ~50 file changes vs 800+ in Option A

## Implementation Categories

### Category 1: SPLIT - Middleware (6 files → 12 files)
Files with Options/Validator/Extensions classes that should be separate:
- `SecurityHeadersMiddleware.cs` → + `SecurityHeadersOptions.cs`, `SecurityHeadersOptionsValidator.cs`, `SecurityHeadersMiddlewareExtensions.cs`
- `RateLimitingMiddleware.cs` → + `RateLimitingMiddlewareExtensions.cs`
- `SessionAuthenticationMiddleware.cs` → + `SessionAuthenticationMiddlewareExtensions.cs`

### Category 2: SPLIT - Services with Configs (8 files → 16 files)
Configuration classes that should be separate:
- `AlertingService.cs` → + `AlertingConfiguration.cs`, `EmailConfiguration.cs`, `SlackConfiguration.cs`, `PagerDutyConfiguration.cs`
- `OpenRouterLlmClient.cs` → internal models to separate files
- `QdrantService.cs` → result/response types to separate files

### Category 3: SPLIT - Large Contracts (1 file → ~15 files)
`Models/Contracts.cs` with 176 types - split by domain:
- `QaContracts.cs` - QA request/response
- `RagStreamingContracts.cs` - Streaming event types
- `ExplainContracts.cs` - Explain feature types
- `SetupGuideContracts.cs` - Setup guide types
- `AgentContracts.cs` - Agent types
- Keep `StreamingEventType` enum in original or separate

### Category 4: SUPPRESS - DTOs with Related Records (~100 files)
Add file-level suppression for files with cohesive Request/Response patterns:
```csharp
#pragma warning disable MA0048 // File name must match type name - Contains related DTOs
```

Files:
- All `*Dto.cs` files with Request/Response pairs
- All `*Command.cs` files with Result records
- All `*Query.cs` files with Result records
- Interface files with supporting types

### Category 5: SUPPRESS - Domain/Infrastructure (~50 files)
Files with tightly coupled types that should remain together:
- Service interfaces with result records
- Domain services with value objects
- External model files (SmolDoclingModels, UnstructuredModels)

## Execution Order

1. **Phase 1**: Add MA0048 suppressions to files being kept together (~150 files)
2. **Phase 2**: Split Middleware files (6 → 12 files)
3. **Phase 3**: Split Service configuration classes (8 → 16 files)
4. **Phase 4**: Split large Contracts.cs (1 → 15 files)
5. **Phase 5**: Build verification - ensure 0 warnings
6. **Phase 6**: Run tests - ensure 0 failures

## Suppression Pattern

For files with intentionally grouped types:
```csharp
// At top of file, after usings
#pragma warning disable MA0048 // File name must match type name - Contains related [Request/Response|DTOs|Domain types]

namespace Api.BoundedContexts...;

// ... types ...

#pragma warning restore MA0048
```

## Expected Outcome
- MA0048 warnings: 874 → 0
- New files: ~30-40
- Modified files: ~170
- No functionality changes
- All tests passing

## DoD Checklist
- [ ] MA0048 rule enabled in editorconfig
- [ ] All 874 warnings resolved (split or suppressed)
- [ ] Build succeeds with 0 warnings
- [ ] All tests pass
- [ ] PR created and reviewed
