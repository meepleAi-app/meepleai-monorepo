# Issue #2541: Integration Test Performance Optimization - Fixes Summary

## Overview
Issue #2541 aimed to optimize integration test performance by preventing Docker container hijacking through `SharedTestcontainersFixture`. During implementation, several test failures were discovered and resolved.

## Fixes Applied

### 1. DocumentVersion Test Helper Format Issue
**File**: `tests/Api.Tests/Helpers/DocumentVersionHelper.cs`

**Problem**: The `CreateVersion` helper was generating `DocumentVersion` objects with timestamps in a format incompatible with the domain entity constructor.

**Solution**: Modified timestamp generation to use proper `DateTime` UTC format instead of string timestamps.

**Status**: ✅ **FIXED**

---

### 2. LoginCommandHandler Exception Type Assertions
**Files**:
- `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/LoginCommandHandlerTests.cs`

**Problem**: Multiple tests were asserting for `DomainException` when the actual exception thrown was `ValidationException` (which inherits from `DomainException`).

**Solution**: Updated assertions to expect the more specific `ValidationException` type, which aligns with the actual implementation behavior.

**Tests Fixed**: 7 tests including:
- `Handle_WithEmptyEmail_ThrowsValidationException`
- `Handle_MalformedEmail_ThrowsValidationException` (4 variations)
- `Handle_ExcessivelyLongEmail_ThrowsValidationException`
- `Handle_WithInvalidEmail_ThrowsValidationException`

**Status**: ✅ **FIXED**

---

### 3. MediatR Resolution in CreateGameWithBggIntegrationTests
**File**: `tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/CreateGameWithBggIntegrationTests.cs`

**Problem**: `IMediator` was not being registered in the DI container, causing `NullReferenceException` when tests tried to send MediatR requests.

**Solution**: Added MediatR registration in test setup:
```csharp
_serviceCollection.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**Status**: ✅ **FIXED**

---

### 4. Entity Tracking Conflicts in Repositories
**Files**:
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Repositories/GameRepository.cs`
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/SharedGameRepository.cs`

**Problem**: When calling `UpdateAsync` followed by `DeleteAsync` in the same unit of work, EF Core encountered entity tracking conflicts because both methods tried to track the same entity instance.

**Solution**: Modified `DeleteAsync` methods to check if entity is already tracked before attempting to remove:
```csharp
public async Task DeleteAsync(Game game, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(game);

    // Check if entity is already tracked to prevent identity conflicts
    var tracked = _dbContext.Games.Local.FirstOrDefault(e => e.Id == game.Id);

    if (tracked != null)
    {
        _dbContext.Games.Remove(tracked);
    }
    else
    {
        _dbContext.Games.Attach(game);
        _dbContext.Games.Remove(game);
    }

    await Task.CompletedTask;
}
```

**Status**: ✅ **FIXED**

---

### 5. DocumentCollectionRepository UpdateAsync Method
**File**: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/DocumentCollectionRepository.cs`

**Problem**: The `UpdateAsync` method was creating a new entity with `MapToPersistence()` and calling `DbContext.Update(entity)`, but this approach didn't work correctly because:
1. The new entity had uninitialized navigation properties
2. EF Core wasn't detecting changes properly
3. Updates weren't being persisted to the database

**Solution**: Implemented proper update pattern using:
- `Attach()` for untracked entities
- `Entry.Property().IsModified = true` for specific properties
- Direct property updates for tracked entities

```csharp
public Task UpdateAsync(DocumentCollection collection, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(collection);

    var tracked = DbContext.ChangeTracker.Entries<DocumentCollectionEntity>()
        .FirstOrDefault(e => e.Entity.Id == collection.Id);

    if (tracked != null)
    {
        UpdateEntityProperties(tracked.Entity, collection);
    }
    else
    {
        var entity = new DocumentCollectionEntity { Id = collection.Id };
        DbContext.DocumentCollections.Attach(entity);

        var entry = DbContext.Entry(entity);
        UpdateEntityProperties(entity, collection);

        // Mark only the properties we actually update as modified
        entry.Property(e => e.Name).IsModified = true;
        entry.Property(e => e.Description).IsModified = true;
        entry.Property(e => e.UpdatedAt).IsModified = true;
        entry.Property(e => e.DocumentsJson).IsModified = true;
    }

    return Task.CompletedTask;
}
```

**Tests Fixed**: 3 tests
- `UpdateAsync_ExistingCollection_PersistsChanges`
- `UpdateAsync_RemoveDocument_PersistsRemoval`
- `UpdateAsync_ConcurrentUpdates_LastWriteWins`

**Status**: ✅ **FIXED**

---

### 6. Test User Creation in FindByUserIdAsync Test
**File**: `tests/Api.Tests/Integration/DocumentProcessing/DocumentCollectionRepositoryIntegrationTests.cs`

**Problem**: The test `FindByUserIdAsync_OrdersByCreatedAtDescending` was using a hardcoded `user2Id` (GUID ending in `...002`) that wasn't created in the test setup, causing foreign key constraint violations.

**Solution**: Added user creation before using the user ID:
```csharp
var user2 = new UserEntity
{
    Id = user2Id,
    Email = "user2@test.com",
    DisplayName = "User 2",
    Role = "User"
};
_dbContext!.Users.Add(user2);
await _dbContext.SaveChangesAsync(TestCancellationToken);
```

**Status**: ✅ **FIXED**

---

## Test Results

### DocumentCollectionRepository Tests
**All 22 tests now pass** (100% success rate):
```
Superato!  - Non superati: 0. Superati: 22. Ignorati: 0. Totale: 22
```

Tests include:
- CRUD operations (Add, Get, Update, Delete)
- Query methods (FindByGameId, FindByUserId, Exists)
- Complex scenarios (concurrent updates, document management, ordering)
- Edge cases (max documents, empty collections, null handling)

---

## Remaining Issues

### LoginCommandHandler Mock Verification
**Severity**: Medium

Some tests are still failing due to Mock verification issues where the test expects `GetByEmailAsync` to never be called, but it's being called during validation.

**Examples**:
- `Handle_ExcessivelyShortPassword_ThrowsValidationException`
- Other validation edge case tests

**Recommendation**: These tests may need adjustment to reflect the actual handler behavior where `GetByEmailAsync` is called during the validation phase.

---

### GameRepository/SharedGameRepository Integration Tests
**Severity**: Medium

Some integration tests are failing with SQL migration errors:
```
column "added_at" does not exist
```

**Recommendation**: These appear to be database schema migration issues that need investigation. The column might be missing from the migration scripts or the database state is inconsistent.

---

## Performance Impact

The fixes applied do not negatively impact test performance. The `DocumentCollectionRepository` test suite completes in ~20-21 seconds, which is acceptable for integration tests using Testcontainers.

---

## Security Notes

During fix implementation, no security vulnerabilities were introduced. All fixes maintain:
- Proper input validation
- Entity tracking integrity
- Transaction safety
- Data consistency

---

## Recommendations

1. **Continue monitoring**: Watch for regressions in the fixed tests
2. **Address remaining issues**: Prioritize fixing the LoginCommandHandler mock verifications and migration errors
3. **Documentation**: Update test documentation to reflect the correct exception types expected
4. **Code review**: Have the repository update pattern reviewed for consistency across all repositories

---

## Follow-up Issue

**Issue #2554**: Fix Remaining Test Failures (473/6067 tests failing)
- https://github.com/DegrassiAaron/meepleai-monorepo/issues/2554
- Tracks resolution of remaining test failures identified during Issue #2541
- Categorized by priority: Migration errors (HIGH), HTTP tests (MEDIUM), Mock verifications (LOW)

---

**Date**: 2026-01-17
**Issue**: #2541
**Follow-up**: #2554
**Author**: Claude Code (AI Assistant)
