# Context Engineering Migration Rollback Testing

**Issue**: #3986
**Parent**: #3493 (PostgreSQL Schema Extensions)

## Overview

Migration rollback tests verify that EF Core migrations for the Context Engineering schema tables can be applied and rolled back cleanly without data corruption, orphaned constraints, or schema inconsistencies.

## Context Engineering Tables

| Table | Migration | FK Dependencies | Cascade Behavior |
|-------|-----------|-----------------|------------------|
| `conversation_memory` | InitialCreate | `users.Id` | CASCADE on user delete |
| `agent_game_state_snapshots` | InitialCreate | `agent_sessions.Id` | CASCADE on session delete |
| `strategy_patterns` | InitialCreate | None (standalone) | N/A |

All three tables are created in the `20260208111903_InitialCreate` migration.

## Test Scenarios

### 1. Clean Rollback
- **Purpose**: Verify tables are properly created and removed during migration lifecycle
- **Tests**:
  - `CleanRollback_MigrateToZero_RemovesAllContextEngineeringTables` - Full rollback removes all 3 tables
  - `CleanRollback_ReapplyAfterRollback_RecreatesTablesSuccessfully` - Apply/rollback/reapply cycle works
  - `CleanRollback_VerifiesIndexesRestoredAfterReapply` - Indexes are restored after reapply
  - `CleanRollback_MigrationHistory_ReflectsRollbackState` - `__EFMigrationsHistory` is updated
  - `CleanRollback_PgvectorExtension_RemovedAfterFullRollback` - Vector columns removed

### 2. Rollback with Data
- **Purpose**: Verify data integrity during rollback (data is removed with tables)
- **Tests**:
  - `RollbackWithData_DataIsRemovedCleanly` - Rollback with existing data succeeds
  - `RollbackWithData_ReapplyCreatesEmptyTables` - Reapply after data rollback creates empty tables
  - `RollbackWithData_NoOrphanedSequencesOrConstraints` - No orphaned DB objects remain
  - `RollbackWithData_LargeDataset_CompletesWithinTimeout` - 100-row rollback within 30s

### 3. Partial Rollback
- **Purpose**: Test rolling back to intermediate migration points
- **Tests**:
  - `PartialRollback_ToPostInitialMigration_PreservesContextEngineeringTables` - Rolling back later migrations keeps CE tables
  - `PartialRollback_ToInitialCreate_PreservesContextEngineeringTables` - Rolling back to InitialCreate keeps CE tables
  - `PartialRollback_FromInitialCreateToZero_RemovesContextEngineeringTables` - Rolling back past InitialCreate removes CE tables

### 4. Foreign Key Constraints
- **Purpose**: Verify FK cascade behavior and constraint enforcement
- **Tests**:
  - `ForeignKeyConstraints_ConversationMemory_CascadeOnUserDelete` - Deleting user cascades to memories
  - `ForeignKeyConstraints_ConversationMemory_RejectsInvalidUserId` - FK violation throws DbUpdateException
  - `ForeignKeyConstraints_RollbackPreservesFKIntegrity` - Partial rollback preserves FK constraints

## Running the Tests

```bash
# Run all rollback tests
dotnet test --filter "Issue=3986"

# Run specific scenario
dotnet test --filter "FullyQualifiedName~ContextEngineeringMigrationRollbackTests.CleanRollback"
dotnet test --filter "FullyQualifiedName~ContextEngineeringMigrationRollbackTests.RollbackWithData"
dotnet test --filter "FullyQualifiedName~ContextEngineeringMigrationRollbackTests.PartialRollback"
dotnet test --filter "FullyQualifiedName~ContextEngineeringMigrationRollbackTests.ForeignKeyConstraints"

# Run all integration tests (includes rollback tests)
dotnet test --filter "Category=Integration"
```

**Prerequisites**: Docker must be running (Testcontainers starts PostgreSQL automatically).

## Manual Rollback Playbook

### Rolling Back in Development

```bash
cd apps/api/src/Api

# Check current migration state
dotnet ef migrations list

# Rollback to specific migration
dotnet ef database update 20260208111903_InitialCreate

# Rollback everything (empty database)
dotnet ef database update 0

# Reapply all migrations
dotnet ef database update
```

### Rolling Back in Production

1. **Take a database backup** before any rollback
2. **Verify no active connections** to affected tables
3. Run rollback command against production connection string
4. **Verify schema state** with information_schema queries
5. **Test application** after rollback to confirm functionality

### Verifying Schema After Rollback

```sql
-- Check if Context Engineering tables exist
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('conversation_memory', 'agent_game_state_snapshots', 'strategy_patterns');

-- Check migration history
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- Check for orphaned constraints
SELECT constraint_name, table_name, constraint_type
FROM information_schema.table_constraints
WHERE table_schema = 'public'
AND table_name IN ('conversation_memory', 'agent_game_state_snapshots', 'strategy_patterns');

-- Check pgvector extension
SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';
```

## Architecture Notes

- **Isolated databases**: Each test class creates its own database via `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync()` to prevent test interference
- **IMigrator service**: Tests use `dbContext.GetInfrastructure().GetRequiredService<IMigrator>()` for programmatic migration control
- **FK workaround**: `agent_game_state_snapshots` requires a complex FK chain (Agent -> GameSession -> User -> Game -> Typology). Test data insertion temporarily disables triggers for this table
- **pgvector**: The `vector(1536)` column type requires the pgvector PostgreSQL extension, which is included in the Testcontainers PostgreSQL image
