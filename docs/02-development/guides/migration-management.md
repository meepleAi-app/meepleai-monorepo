# Entity Framework Core Migration Management

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Context**: Database Schema Evolution

---

## 🎯 Overview

Critical guidelines for managing EF Core migrations in the MeepleAI project to ensure safe database upgrades and avoid breaking existing installations.

---

## ⚠️ Critical Rules

### 1. **NEVER Delete Existing Migrations**

Once a migration has been:
- Committed to `main` branch
- Deployed to any environment (dev, staging, prod)
- Applied to any database

**It must NEVER be deleted or recreated.**

**Why?** EF Core tracks applied migrations in the `__EFMigrationsHistory` table. Deleting migrations breaks the upgrade path for existing databases.

### 2. **NEVER Recreate Initial Migrations**

Recreating migrations with new timestamps (e.g., replacing `20251111100655_DDD_InitialGuidSchema` with `20251113174329_InitialSchema`) causes:

```
❌ Error: relation "users" already exists
❌ Error: table "__EFMigrationsHistory" shows migration not applied
❌ Result: Existing databases CANNOT upgrade
```

### 3. **Add New Migrations Only**

To change the schema:

```bash
# ✅ CORRECT: Add a new migration
dotnet ef migrations add AddNewFeature --project src/Api

# ❌ WRONG: Delete old migrations and recreate
rm Migrations/*.cs
dotnet ef migrations add InitialSchema --project src/Api
```

---

## 📜 Current Migration History

**Do NOT modify these migrations:**

| Timestamp | Migration Name | Description |
|-----------|----------------|-------------|
| `20251111100655` | `DDD_InitialGuidSchema` | Initial database schema (DDD Phase 1) |
| `20251111122334` | `DDD_Phase2_GameManagementExtendGameEntity` | Extended Game entity |
| `20251111124027` | `DDD_Phase2_AddGameSessionEntity` | Added GameSession entity |
| `20251111131119` | `DDD_Phase3_AddChatThreadEntity` | Added ChatThread entity |
| `20251113055726` | `AddLlmCostTracking` | Added LLM cost tracking tables |

**Total**: 5 migrations (baseline + 4 incremental)

---

## 🔄 Proper Migration Workflow

### Adding a New Migration

1. **Make model changes** in your entity classes
2. **Create migration**:
   ```bash
   cd apps/api/src/Api
   dotnet ef migrations add YourFeatureName
   ```
3. **Review generated code** in `Migrations/` directory
4. **Test migration**:
   ```bash
   # Test upgrade
   dotnet ef database update

   # Test rollback
   dotnet ef database update PreviousMigrationName
   ```
5. **Commit migration files** to git:
   ```bash
   git add Migrations/20251113120000_YourFeatureName*
   git commit -m "feat(migrations): Add YourFeatureName migration"
   ```

### Fixing a Bad Migration (Before Commit)

If you created a migration with errors **and haven't committed it yet**:

```bash
# Remove the last migration
dotnet ef migrations remove

# Fix your model
# Then recreate the migration
dotnet ef migrations add YourFeatureName
```

### Fixing a Bad Migration (After Commit/Deploy)

If the migration is already committed or deployed, **DO NOT remove it**. Instead:

```bash
# Create a corrective migration
dotnet ef migrations add FixYourFeatureName
```

---

## 🔍 Migration Best Practices

### 1. Descriptive Names

```bash
# ✅ GOOD
dotnet ef migrations add AddUserEmailVerification
dotnet ef migrations add CreatePdfDocumentsTable
dotnet ef migrations add AddLlmCostTracking

# ❌ BAD
dotnet ef migrations add Update
dotnet ef migrations add Fix
dotnet ef migrations add NewChanges
```

### 2. One Logical Change Per Migration

```bash
# ✅ GOOD: Separate migrations for distinct features
dotnet ef migrations add AddUserEmailColumn
dotnet ef migrations add AddUserPhoneColumn

# ❌ BAD: Unrelated changes in one migration
dotnet ef migrations add AddColumnsAndTablesAndIndexes
```

### 3. Test Both Up and Down

Always verify:
- `Up()` applies cleanly
- `Down()` rolls back correctly
- No data loss on rollback (where applicable)

```bash
# Apply migration
dotnet ef database update

# Rollback one step
dotnet ef database update PreviousMigrationName

# Reapply
dotnet ef database update
```

### 4. Review Generated SQL

```bash
# Generate SQL script without applying
dotnet ef migrations script

# Generate SQL for specific migration range
dotnet ef migrations script FromMigration ToMigration
```

---

## 🚨 Common Mistakes and Fixes

### Mistake 1: Deleting All Migrations

**Symptom**: Fresh database works, existing databases fail with "relation already exists"

**Fix**: Restore original migrations from git:
```bash
git checkout main -- apps/api/src/Api/Migrations/
```

### Mistake 2: Recreating Initial Migration

**Symptom**: New timestamp on initial migration, existing databases cannot upgrade

**Fix**:
```bash
# 1. Restore old migrations
git show <commit-before-deletion>:apps/api/src/Api/Migrations/ > /tmp/old-migrations

# 2. Delete new recreated migrations
rm apps/api/src/Api/Migrations/202511131*

# 3. Restore from git history (see Restoration section below)
```

### Mistake 3: Conflicting Migrations from Multiple Branches

**Symptom**: Two migrations with similar timestamps, merge conflicts

**Fix**:
```bash
# 1. Keep both migrations
git merge --no-commit other-branch

# 2. If timestamps conflict, rename the newer one
mv 20251113100000_Feature.cs 20251113100001_Feature.cs
# Update class name and timestamp in .cs and .Designer.cs files

# 3. Update snapshot
dotnet ef migrations add --no-build TempForSnapshot
dotnet ef migrations remove
```

---

## 🔧 Restoring Deleted Migrations

If migrations were accidentally deleted, restore them from git history:

### Step 1: Find the commit before deletion
```bash
git log --oneline -- apps/api/src/Api/Migrations/ | head -20
```

### Step 2: Identify the last good commit
```bash
# Example: Find commit before "rimosso le migrazioni"
git show <commit-hash>^:apps/api/src/Api/Migrations/
```

### Step 3: Restore specific migrations
```bash
# Restore individual migration
git show <commit>:apps/api/src/Api/Migrations/20251111100655_DDD_InitialGuidSchema.cs > \
  apps/api/src/Api/Migrations/20251111100655_DDD_InitialGuidSchema.cs

git show <commit>:apps/api/src/Api/Migrations/20251111100655_DDD_InitialGuidSchema.Designer.cs > \
  apps/api/src/Api/Migrations/20251111100655_DDD_InitialGuidSchema.Designer.cs

# Repeat for all deleted migrations
```

### Step 4: Restore snapshot
```bash
git show <commit>:apps/api/src/Api/Migrations/MeepleAiDbContextModelSnapshot.cs > \
  apps/api/src/Api/Migrations/MeepleAiDbContextModelSnapshot.cs
```

### Step 5: Verify restoration
```bash
cd apps/api
dotnet build
dotnet test
```

---

## 📊 Upgrade Scenarios

### Scenario 1: Fresh Database (New Installation)

```bash
# All migrations applied in sequence
dotnet ef database update
```

**Result**: All 5 migrations applied, database fully initialized

### Scenario 2: Existing Database (Already Running)

Database has migrations 1-3 applied, new deployment has migrations 1-5:

```bash
dotnet ef database update
```

**EF Core behavior**:
1. Checks `__EFMigrationsHistory` table
2. Finds migrations 1-3 already applied
3. Applies only migrations 4-5 (incremental)

**Result**: Smooth upgrade from version N to version N+2

### Scenario 3: Rollback

```bash
# Rollback to specific migration
dotnet ef database update 20251111124027_DDD_Phase2_AddGameSessionEntity
```

**EF Core behavior**:
1. Identifies current version (migration 5)
2. Identifies target version (migration 3)
3. Executes `Down()` methods for migrations 4-5 in reverse order

**Result**: Database rolled back to migration 3 state

---

## 🐳 Docker and Production Deployments

### Auto-Apply Migrations on Startup

Current configuration in `Program.cs`:

```csharp
// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    db.Database.Migrate();
}
```

**Pros**:
- Simple deployment
- No manual intervention needed
- Safe for incremental migrations

**Cons**:
- Downtime during migration
- Cannot roll back easily
- All instances must wait for migration

### Alternative: Pre-Deployment Script

For production with zero-downtime deployments:

```bash
# Generate SQL script
dotnet ef migrations script --idempotent --output migration.sql

# Apply manually before deployment
psql -h prod-db -U meepleai -d meepleai_prod -f migration.sql
```

**Pros**:
- Controlled timing
- Can verify before deployment
- Easier rollback

**Cons**:
- Manual step in deployment pipeline
- Requires database access

---

## 🧪 Testing Migrations

### Unit Tests for Migrations

```csharp
[Fact]
public async Task Migration_AddLlmCostTracking_CreatesTable()
{
    // Arrange: Database at previous migration
    await using var context = new MeepleAiDbContext(options);
    await context.Database.MigrateAsync();

    // Act: Apply target migration
    await context.Database.MigrateAsync();

    // Assert: Table exists
    var tableExists = await context.Database
        .ExecuteSqlRawAsync("SELECT 1 FROM llm_cost_logs LIMIT 1");
    Assert.True(tableExists >= 0);
}
```

### Integration Tests with Testcontainers

```csharp
[Fact]
public async Task FullMigrationPath_AppliesSuccessfully()
{
    // Arrange: Postgres container
    await using var postgres = new PostgreSqlBuilder().Build();
    await postgres.StartAsync();

    // Act: Apply all migrations
    await using var context = CreateContext(postgres.GetConnectionString());
    await context.Database.MigrateAsync();

    // Assert: Latest migration applied
    var appliedMigrations = await context.Database
        .GetAppliedMigrationsAsync();
    Assert.Contains("20251113055726_AddLlmCostTracking", appliedMigrations);
}
```

---

## 📚 References

- [EF Core Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Managing Migrations in Production](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Database Schema Evolution Guide](../01-architecture/database-schema-evolution.md)

---

## 🚀 Quick Reference

```bash
# Add new migration
dotnet ef migrations add MigrationName --project src/Api

# Apply migrations
dotnet ef database update --project src/Api

# Generate SQL script
dotnet ef migrations script --project src/Api

# Remove last migration (not committed)
dotnet ef migrations remove --project src/Api

# List migrations
dotnet ef migrations list --project src/Api

# Rollback to specific migration
dotnet ef database update MigrationName --project src/Api
```

---

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Maintainer**: Backend Team

