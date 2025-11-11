# Database Migration Plan for DDD Alignment

## Goal
Align database schema with DDD domain model for pure domain-driven design.

## Changes Required

### 1. ID Type Migration: string → Guid

**Tables Affected**:
- `users` (Id, referenced by all auth tables)
- `user_sessions` (Id, UserId FK)
- `api_keys` (Id, UserId FK, RevokedBy FK)
- `oauth_accounts` (Id, UserId FK)
- `user_backup_codes` (Id, UserId FK)
- `temp_sessions` (Id, UserId FK)
- All other tables with user FKs (games, rule_specs, chat_messages, etc.)

**Migration Steps**:
1. Add new Guid columns (id_guid, user_id_guid)
2. Populate with new Guids or convert from string
3. Update foreign key relationships
4. Drop old string columns
5. Rename new columns to original names

**Risk**: HIGH - Affects 15+ tables, all user relationships

### 2. Role Type Migration: UserRole enum → string

**Tables Affected**:
- `users` (Role column)

**Migration Steps**:
1. Add new string column (role_string)
2. Populate: Admin → "admin", Editor → "editor", User → "user"
3. Drop old enum column
4. Rename new column to "role"

**Risk**: LOW - Single table, straightforward mapping

### 3. Scopes Migration: string[] → string (comma-separated)

**Tables Affected**:
- `api_keys` (Scopes column)

**Migration Steps**:
1. Add new string column (scopes_string)
2. Populate: Join array with commas
3. Drop old array column
4. Rename new column to "scopes"

**Risk**: LOW - Single table, simple transformation

## Estimated Timeline

| Migration | Complexity | Time | Risk |
|-----------|------------|------|------|
| UserRole enum → string | Low | 1 hour | Low |
| Scopes array → string | Low | 1 hour | Low |
| ID string → Guid | Very High | 2-3 days | HIGH |

**Total**: 3-4 days for all migrations + testing

## Recommendation

Given alpha phase and time constraints:

### Phase 1: Low-Risk Migrations First (2 hours)
1. ✅ Migrate UserRole enum → string
2. ✅ Migrate Scopes array → string
3. ✅ Test with existing data
4. ✅ Verify no data loss

### Phase 2: High-Risk ID Migration (Deferred)
- **Defer to post-alpha** or beta phase
- Requires extensive testing
- Risk of data corruption
- Affects entire system

### Pragmatic Solution for Alpha

**Compromise**: Adjust domain to use `string` IDs temporarily
- Keep domain value objects (Email, PasswordHash, Role, SessionToken)
- Use `string` for entity IDs (align with current DB)
- After alpha, migrate DB to Guids if needed

**Benefits**:
- ✅ Complete Phase 2 quickly (1 day)
- ✅ Prove DDD value without risky migrations
- ✅ Can refactor IDs later (non-breaking internal change)
- ✅ Focus on splitting services (main goal)

## Decision

Do you want to:
1. **Quick wins** (2 hours): Migrate Role + Scopes only, defer IDs
2. **Full purism** (3-4 days): Migrate everything including IDs
3. **Pragmatic** (1 day): Keep string IDs in domain, proceed with Phase 2
