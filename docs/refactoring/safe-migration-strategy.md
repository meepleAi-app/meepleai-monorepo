# Safe Migration Strategy: String to Guid IDs

## Strategy Overview

Instead of writing 1000+ line manual migrations, we'll use EF Core's automatic migration generation by updating entities incrementally.

## Step-by-Step Approach

### Step 1: Update Core Entities First (UserEntity)
1. Change `UserEntity.Id` from `string` to `Guid`
2. Change `UserEntity.Role` from `UserRole` enum to `string`
3. Generate migration: `dotnet ef migrations add DDD_ConvertUserEntityToGuid`
4. Review generated migration
5. Apply and test with data preservation logic

### Step 2: Update Auth-Related Entities
1. Update `UserSessionEntity`, `ApiKeyEntity`, `OAuthAccountEntity` (IDs + foreign keys)
2. Generate migration: `dotnet ef migrations add DDD_ConvertAuthEntitiesToGuid`
3. Apply and test

### Step 3: Update Domain Entities (Games, RuleSpecs, Chats, PDFs)
1. Update `GameEntity`, `RuleSpecEntity`, `ChatEntity`, `PdfDocumentEntity`
2. Update all foreign keys referencing users
3. Generate migration: `dotnet ef migrations add DDD_ConvertDomainEntitiesToGuid`
4. Apply and test

### Step 4: Update Admin/Config Entities
1. Update remaining entities
2. Generate final migration: `dotnet ef migrations add DDD_ConvertRemainingEntitiesToGuid`
3. Apply and test

## Data Preservation Strategy

For each migration, we'll:
1. Use EF Core migration with custom SQL for data conversion
2. Convert string IDs to deterministic Guids: `md5(string_id)::uuid`
3. Test on local database before committing
4. Verify all relationships preserved
5. Check seed data still works

## Timeline

| Step | Duration | Risk |
|------|----------|------|
| Step 1: UserEntity | 2-3 hours | Medium |
| Step 2: Auth entities | 3-4 hours | Medium |
| Step 3: Domain entities | 4-6 hours | High |
| Step 4: Admin entities | 2-3 hours | Low |
| Testing & Validation | 4-6 hours | - |
| **Total** | **1.5-2 days** | **Medium** |

## Risk Mitigation

1. **Backup database** before each step
2. **Incremental migrations** (easier to debug)
3. **Test after each step** (catch issues early)
4. **Deterministic Guid generation** (same string → same Guid)
5. **Reversible** (can rollback each migration independently)

## Current Status

- [ ] Step 1: UserEntity conversion
- [ ] Step 2: Auth entities conversion
- [ ] Step 3: Domain entities conversion
- [ ] Step 4: Admin entities conversion
- [ ] Full test suite passing

Let's start with Step 1!
