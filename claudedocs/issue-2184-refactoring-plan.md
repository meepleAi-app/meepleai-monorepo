# Issue #2184 - Refactoring Plan for Remaining Catch Blocks

## Current Status (After Partial Implementation)

### ✅ Completed
- InfisicalSecretsClient.cs: 4 catch with specific exceptions + pragma (HttpRequestException, TaskCanceledException)
- Build passes: 0 errors, 0 warnings

### ⏳ Remaining Work
- **~110 catch blocks** across 70 files need pragma + justification
- Categorized by pattern: CQRS (44), Infrastructure (30), Background (9), Event (4), Middleware (2), Endpoint (2)

## Approach: Incremental Manual Application

**Strategy**: Apply pragmas category by category with build validation after each batch

### Batch 1: CQRS Command Handlers (Priority: HIGH)
**Files**: ~45 handlers in `BoundedContexts/*/Application/Commands/**/*.cs`
**Pattern**: COMMAND HANDLER PATTERN
**Template**:
```csharp
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ValidationException, DomainException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result<T> pattern.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Handler}", nameof(HandlerName));
            return Result.Failure("An unexpected error occurred");
        }
#pragma warning restore CA1031
```

### Batch 2: CQRS Query Handlers (Priority: HIGH)
**Files**: ~20 handlers in `BoundedContexts/*/Application/Queries/**/*.cs` and `Application/Handlers/*QueryHandler.cs`
**Pattern**: Same as Command Handlers (CQRS boundary)

### Batch 3: Infrastructure Services (Priority: MEDIUM)
**Files**: ~15 services in `BoundedContexts/*/Infrastructure/Services/*.cs` and `Infrastructure/External/*.cs`
**Pattern**: INFRASTRUCTURE SERVICE PATTERN
**Template**:
```csharp
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: INFRASTRUCTURE SERVICE PATTERN - Resilience boundary
        // Infrastructure service failures handled gracefully. Specific exceptions caught separately.
        // Generic catch prevents service failures from cascading to application layer.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected infrastructure error in {Service}", nameof(ServiceName));
            return ErrorResult; // or fail-open
        }
#pragma warning restore CA1031
```

### Batch 4: Background Tasks & Jobs (Priority: LOW)
**Files**: ~9 tasks in `Infrastructure/BackgroundTasks/*.cs` and `Infrastructure/Scheduling/*.cs`
**Pattern**: BACKGROUND TASK PATTERN

### Batch 5: Event Handlers (Priority: LOW)
**Files**: ~4 handlers in `Application/EventHandlers/*.cs`
**Pattern**: EVENT HANDLER PATTERN

### Batch 6: Middleware & Endpoints (Priority: MEDIUM)
**Files**: ~5 files in `Middleware/*.cs` and `Routing/*Endpoints.cs`
**Pattern**: MIDDLEWARE BOUNDARY or API ENDPOINT

## Validation Process (After Each Batch)

1. **Build Check**:
   ```bash
   dotnet build src/Api/Api.csproj
   # Verify 0 errors
   ```

2. **Warning Count**:
   ```bash
   dotnet build 2>&1 | grep "warning CA1031" | wc -l
   # Should decrease with each batch
   ```

3. **Smoke Test**:
   ```bash
   dotnet test --filter "Category=Smoke"
   # Verify no regressions
   ```

## Risk Mitigation

- **Incremental**: Apply in small batches (5-10 files at a time)
- **Validation**: Build + basic tests after each batch
- **Rollback Ready**: Git checkpoint after each successful batch
- **Pattern Consistency**: Use exact templates, no variations

## Success Criteria

- [ ] Zero CA1031 warnings
- [ ] Zero S2139 warnings
- [ ] Build passes (0 errors)
- [ ] Test suite passes (≥90% coverage maintained)
- [ ] All pragma blocks have detailed justifications
- [ ] Patterns match documentation in generic-catch-analysis.md

## Next Steps

Use refactoring-expert agent or manual Edit tool for safe, incremental application.
