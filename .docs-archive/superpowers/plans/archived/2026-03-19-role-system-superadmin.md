# Role System & SuperAdmin Governance — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix critical role system bugs (Creator missing from enum/hierarchy, inverted permissions) and enforce SuperAdmin-only role governance where Creator is automatic from paid tier and Editor/Admin are assigned exclusively by SuperAdmin.

**Architecture:** The Role value object (`Role.cs`) is the domain authority with 5 roles. The infrastructure `UserRole` enum and `SessionValidationExtensions.HasSufficientRole()` must be synchronized. Role assignment endpoints must require SuperAdmin session. Frontend must mirror the role hierarchy.

**Tech Stack:** .NET 9 (C#), Next.js 16 (TypeScript), xUnit, Vitest

**Note:** No EF Core migration needed — `User.Role` is stored as string via `HasConversion<string>()`. The `UserRole` enum is only used for in-memory authorization checks.

---

## Role Model (Source of Truth)

| Role | Level | How Obtained | Assigned By |
|------|-------|-------------|-------------|
| User | 0 | Registration (free tier) | Automatic |
| Creator | 1 | Any paid tier (Normal+) | Automatic (derived from tier) |
| Editor | 2 | Manual promotion | SuperAdmin only |
| Admin | 3 | Manual promotion | SuperAdmin only |
| SuperAdmin | 4 | Seed only | Nobody (immutable) |

**Key rule:** A paying user IS a Creator. The `Creator` role is never manually assigned — it's the effective minimum role for any user with `UserTier >= Normal`.

---

### Task 1: Fix `UserRole` enum — add Creator

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/Authentication/UserRole.cs`

- [ ] **Step 1: Add Creator to enum**

```csharp
public enum UserRole
{
    Admin = 0,
    Editor = 1,
    User = 2,
    SuperAdmin = 3,
    Creator = 4  // Epic #4068 — between User and Editor in hierarchy (level, not enum value)
}
```

Note: Value `4` preserves backward compatibility (no shifts). The *numeric enum value* is NOT the hierarchy level — `HasSufficientRole()` handles hierarchy separately. No DB migration needed.

- [ ] **Step 2: Verify build compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/Authentication/UserRole.cs
git commit -m "fix(auth): add Creator to UserRole enum — was missing since Epic #4068"
```

---

### Task 2: Fix `HasSufficientRole` hierarchy in SessionValidationExtensions

**Files:**
- Modify: `apps/api/src/Api/Extensions/SessionValidationExtensions.cs:123-136`

- [ ] **Step 1: Fix RoleLevel to include Creator**

Replace the `HasSufficientRole` method:

```csharp
/// <summary>
/// Checks if the user's role meets or exceeds the required role level.
/// Hierarchy: SuperAdmin (4) > Admin (3) > Editor (2) > Creator (1) > User (0)
/// </summary>
private static bool HasSufficientRole(UserRole userRole, UserRole requiredRole)
{
    static int RoleLevel(UserRole role) => role switch
    {
        UserRole.SuperAdmin => 4,
        UserRole.Admin => 3,
        UserRole.Editor => 2,
        UserRole.Creator => 1,
        UserRole.User => 0,
        _ => -1
    };

    return RoleLevel(userRole) >= RoleLevel(requiredRole);
}
```

- [ ] **Step 2: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Extensions/SessionValidationExtensions.cs
git commit -m "fix(auth): add Creator to role hierarchy — was falling to -1 (denied all)"
```

---

### Task 3: Fix `Role.cs` — error message + Creator/Editor permission hierarchy

**Files:**
- Modify: `apps/api/src/Api/SharedKernel/Domain/ValueObjects/Role.cs`

Three fixes in this file:

- [ ] **Step 1: Fix Role.Parse error message (line 35)**

Change from:
```csharp
throw new ValidationException(nameof(Role), $"Invalid role: {value}. Valid roles are: user, editor, admin, superadmin");
```
To:
```csharp
throw new ValidationException(nameof(Role), $"Invalid role: {value}. Valid roles are: user, creator, editor, admin, superadmin");
```

- [ ] **Step 2: Fix Creator permission — remove Editor from Creator's HasPermission (line 56)**

The current code gives Creator permission for Editor-level requirements, which is WRONG (Creator is BELOW Editor).

Change line 56 from:
```csharp
if (IsCreator() && (requiredRole.IsCreator() || requiredRole.IsEditor() || requiredRole.IsUser())) return true;
```
To:
```csharp
if (IsCreator() && (requiredRole.IsCreator() || requiredRole.IsUser())) return true;
```

- [ ] **Step 3: Fix Editor permission — add Creator to Editor's HasPermission (line 59)**

Editor is ABOVE Creator in hierarchy, so Editor must have Creator-level permissions.

Change line 59 from:
```csharp
if (IsEditor() && (requiredRole.IsEditor() || requiredRole.IsUser())) return true;
```
To:
```csharp
if (IsEditor() && (requiredRole.IsEditor() || requiredRole.IsCreator() || requiredRole.IsUser())) return true;
```

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Domain/ValueObjects/Role.cs
git commit -m "fix(auth): correct Creator/Editor permission hierarchy in Role.HasPermission

- Creator only has creator + user permissions (was wrongly including editor)
- Editor now includes creator permissions (was missing)
- Fix Role.Parse error message to include 'creator'
Hierarchy: SuperAdmin > Admin > Editor > Creator > User"
```

---

### Task 4: Restrict role assignment to SuperAdmin only

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs:384-414`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/ChangeUserRoleCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminUserEndpoints.cs`

- [ ] **Step 1: Update `User.AssignRole` — require SuperAdmin requester (lines 384-399)**

```csharp
public void AssignRole(Role newRole, Role requesterRole)
{
    ArgumentNullException.ThrowIfNull(newRole);
    ArgumentNullException.ThrowIfNull(requesterRole);

    // Only SuperAdmin can assign roles
    if (!requesterRole.IsSuperAdmin())
        throw new DomainException("Only the SuperAdmin can assign roles");

    // Cannot assign SuperAdmin role (it's seeded, not assignable)
    if (newRole.IsSuperAdmin())
        throw new DomainException("SuperAdmin role cannot be assigned — it is created during system seed only");

    // Cannot change a SuperAdmin's role (immutable)
    if (this.Role.IsSuperAdmin())
        throw new DomainException("SuperAdmin role is immutable and cannot be changed");

    var oldRole = this.Role;
    Role = newRole;
    AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole));
}
```

Note: Use `this.Role` explicitly to disambiguate from the `Role` type.

- [ ] **Step 2: Protect `UpdateRole` against SuperAdmin mutation (lines 405-414)**

```csharp
public void UpdateRole(Role newRole)
{
    ArgumentNullException.ThrowIfNull(newRole);

    // SuperAdmin role is immutable
    if (this.Role.IsSuperAdmin())
        throw new DomainException("SuperAdmin role is immutable and cannot be changed");

    // Cannot promote to SuperAdmin via UpdateRole
    if (newRole.IsSuperAdmin())
        throw new DomainException("SuperAdmin role cannot be assigned — it is created during system seed only");

    if (this.Role == newRole)
        return; // No change

    var oldRole = this.Role;
    Role = newRole;
    AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole));
}
```

- [ ] **Step 3: Update `ChangeUserRoleCommandHandler` — add Creator to AllowedRoles + use AssignRole with requester**

In `ChangeUserRoleCommandHandler.cs`:

Change line 14:
```csharp
private static readonly string[] AllowedRoles = { "Admin", "Editor", "Creator", "User" };
```

The handler currently calls `user.UpdateRole(newRole)` (line 43) which has NO requester check. Change it to use `user.AssignRole()` with the requester's role for domain-level enforcement. This requires passing the requester's role from the command. If the command doesn't carry requester info, the endpoint-level `RequireSuperAdminSession` guard is the sole protection — acceptable since `UpdateRole` now guards against SuperAdmin mutation.

- [ ] **Step 4: Change role change endpoint to RequireSuperAdminSession**

In `AdminUserEndpoints.cs`, `HandleChangeUserRole` method (around line 955):
```csharp
// Change from:
var (authorized, session, error) = context.RequireAdminSession();
// To:
var (authorized, session, error) = context.RequireSuperAdminSession();
```

`RequireSuperAdminSession()` already exists in `SessionValidationExtensions.cs:267-271`.

- [ ] **Step 5: Change bulk role change handler to RequireSuperAdminSession**

In `AdminUserEndpoints.cs`, `HandleBulkRoleChange` handler (around line 411):
```csharp
// Change from:
var (authorized, session, error) = context.RequireAdminSession();
// To:
var (authorized, session, error) = context.RequireSuperAdminSession();
```

Note: The bulk route registration at line 124 has NO route-level authorization filter — only the handler body checks auth. This is fine; just change the handler call.

- [ ] **Step 6: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/ChangeUserRoleCommandHandler.cs
git add apps/api/src/Api/Routing/AdminUserEndpoints.cs
git commit -m "feat(auth): restrict role assignment to SuperAdmin only

- User.AssignRole requires SuperAdmin requester (was Admin)
- User.UpdateRole protects SuperAdmin from mutation
- SuperAdmin role cannot be assigned or changed (immutable)
- Role change endpoints require SuperAdmin session
- Add Creator to allowed roles list"
```

---

### Task 5: Frontend — add `ROLE_HIERARCHY` and fix types

**Files:**
- Modify: `apps/web/src/types/permissions.ts`
- Modify: `apps/web/src/contexts/PermissionContext.tsx`

- [ ] **Step 1: Update permissions.ts — add ROLE_HIERARCHY, Pending status, role helpers**

Update `UserAccountStatus` type (line 7):
```typescript
export type UserAccountStatus = 'Active' | 'Suspended' | 'Banned' | 'Pending';
```

Add after `TIER_HIERARCHY` (after line 48):
```typescript
export const ROLE_HIERARCHY: Record<UserRole, number> = {
  user: 0,
  creator: 1,
  editor: 2,
  admin: 3,
  superadmin: 4,
};

export function hasMinimumRole(userRole: UserRole, requiredRole: UserRole): boolean {
  return ROLE_HIERARCHY[userRole] >= ROLE_HIERARCHY[requiredRole];
}

export function isSuperAdmin(role: UserRole): boolean {
  return role === 'superadmin';
}

export function isEditor(role: UserRole): boolean {
  return role === 'editor' || role === 'admin' || role === 'superadmin';
}

export function isCreator(role: UserRole): boolean {
  return role === 'creator' || isEditor(role);
}
```

Note: `isAdmin()`, `isEditor()`, `isCreator()` all use "has at least this role" semantics, matching the existing `isAdmin()` convention on line 50.

- [ ] **Step 2: Update PermissionContext.tsx — add hasRole and isSuperAdmin**

Update the import line (line 10):
```typescript
import { hasMinimumTier, isAdmin, hasMinimumRole, isSuperAdmin as isSuperAdminFn } from '@/types/permissions';
```

Update the interface:
```typescript
interface PermissionContextValue {
  tier: UserTier;
  role: UserRole;
  canAccess: (feature: string) => boolean;
  hasTier: (tier: UserTier) => boolean;
  hasRole: (role: UserRole) => boolean;
  isAdmin: () => boolean;
  isSuperAdmin: () => boolean;
  loading: boolean;
}
```

Update safe defaults (around line 36):
```typescript
const safeDefaults: PermissionContextValue = {
  tier: 'free',
  role: 'user',
  canAccess: () => false,
  hasTier: () => false,
  hasRole: () => false,
  isAdmin: () => false,
  isSuperAdmin: () => false,
  loading: false,
};
```

Update value construction (around line 48):
```typescript
const value: PermissionContextValue = {
  tier: data?.tier ?? 'free',
  role: data?.role ?? 'user',
  canAccess: feature => data?.accessibleFeatures?.includes(feature) ?? false,
  hasTier: tier => hasMinimumTier(data?.tier ?? 'free', tier),
  hasRole: role => hasMinimumRole(data?.role ?? 'user', role),
  isAdmin: () => isAdmin(data?.role ?? 'user'),
  isSuperAdmin: () => isSuperAdminFn(data?.role ?? 'user'),
  loading: isLoading,
};
```

- [ ] **Step 3: Verify frontend build**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/types/permissions.ts apps/web/src/contexts/PermissionContext.tsx
git commit -m "feat(frontend): add ROLE_HIERARCHY, hasMinimumRole, isSuperAdmin

- Add Pending to UserAccountStatus
- Add ROLE_HIERARCHY constant mirroring backend
- Add hasMinimumRole(), isSuperAdmin(), isEditor(), isCreator() helpers
- Add hasRole() and isSuperAdmin() to PermissionContext
- Import alias isSuperAdminFn to avoid naming collision"
```

---

### Task 6: Backend tests — Creator role + SuperAdmin protection

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/ValueObjects/RoleTests.cs`

- [ ] **Step 1: Write Creator role + hierarchy tests**

Add to `RoleTests.cs`:

```csharp
[Fact]
public void Parse_WithCreator_ShouldReturnCreatorRole()
{
    var role = Role.Parse("creator");
    Assert.Equal("creator", role.Value);
    Assert.True(role.IsCreator());
}

[Fact]
public void Creator_HasPermission_ForCreatorAndUser_Only()
{
    var creator = Role.Creator;
    Assert.True(creator.HasPermission(Role.Creator));
    Assert.True(creator.HasPermission(Role.User));
    // Creator does NOT have Editor/Admin/SuperAdmin permissions
    Assert.False(creator.HasPermission(Role.Editor));
    Assert.False(creator.HasPermission(Role.Admin));
    Assert.False(creator.HasPermission(Role.SuperAdmin));
}

[Fact]
public void Editor_HasPermission_ForCreator()
{
    // Editor is ABOVE Creator: SuperAdmin > Admin > Editor > Creator > User
    var editor = Role.Editor;
    Assert.True(editor.HasPermission(Role.Creator));
    Assert.True(editor.HasPermission(Role.Editor));
    Assert.True(editor.HasPermission(Role.User));
    Assert.False(editor.HasPermission(Role.Admin));
}

[Fact]
public void Admin_HasPermission_ForCreator()
{
    var admin = Role.Admin;
    Assert.True(admin.HasPermission(Role.Creator));
    Assert.True(admin.HasPermission(Role.Editor));
    Assert.True(admin.HasPermission(Role.User));
    Assert.True(admin.HasPermission(Role.Admin));
    Assert.False(admin.HasPermission(Role.SuperAdmin));
}

[Fact]
public void SuperAdmin_HasPermission_ForAllRoles()
{
    var superAdmin = Role.SuperAdmin;
    Assert.True(superAdmin.HasPermission(Role.User));
    Assert.True(superAdmin.HasPermission(Role.Creator));
    Assert.True(superAdmin.HasPermission(Role.Editor));
    Assert.True(superAdmin.HasPermission(Role.Admin));
    Assert.True(superAdmin.HasPermission(Role.SuperAdmin));
}
```

- [ ] **Step 2: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RoleTests" --no-restore`
Expected: All pass (including new tests, since Task 3 already fixed Role.cs)

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/ValueObjects/RoleTests.cs
git commit -m "test(auth): add Creator role + full hierarchy permission tests

Covers: Creator permissions (creator+user only), Editor includes Creator,
Admin includes all below, SuperAdmin has all permissions"
```

---

### Task 7: Frontend tests — role hierarchy

**Files:**
- Create: `apps/web/src/__tests__/types/permissions.test.ts`

- [ ] **Step 1: Write role hierarchy tests**

```typescript
import { hasMinimumRole, isAdmin, isSuperAdmin, isCreator, isEditor, ROLE_HIERARCHY } from '@/types/permissions';

describe('ROLE_HIERARCHY', () => {
  it('should order roles correctly', () => {
    expect(ROLE_HIERARCHY.user).toBeLessThan(ROLE_HIERARCHY.creator);
    expect(ROLE_HIERARCHY.creator).toBeLessThan(ROLE_HIERARCHY.editor);
    expect(ROLE_HIERARCHY.editor).toBeLessThan(ROLE_HIERARCHY.admin);
    expect(ROLE_HIERARCHY.admin).toBeLessThan(ROLE_HIERARCHY.superadmin);
  });
});

describe('hasMinimumRole', () => {
  it('superadmin has all roles', () => {
    expect(hasMinimumRole('superadmin', 'user')).toBe(true);
    expect(hasMinimumRole('superadmin', 'creator')).toBe(true);
    expect(hasMinimumRole('superadmin', 'editor')).toBe(true);
    expect(hasMinimumRole('superadmin', 'admin')).toBe(true);
    expect(hasMinimumRole('superadmin', 'superadmin')).toBe(true);
  });

  it('user cannot access creator features', () => {
    expect(hasMinimumRole('user', 'creator')).toBe(false);
  });

  it('creator has user access but not editor', () => {
    expect(hasMinimumRole('creator', 'user')).toBe(true);
    expect(hasMinimumRole('creator', 'editor')).toBe(false);
  });

  it('editor has creator access', () => {
    expect(hasMinimumRole('editor', 'creator')).toBe(true);
  });
});

describe('role helpers', () => {
  it('isAdmin checks admin and superadmin', () => {
    expect(isAdmin('admin')).toBe(true);
    expect(isAdmin('superadmin')).toBe(true);
    expect(isAdmin('editor')).toBe(false);
    expect(isAdmin('creator')).toBe(false);
    expect(isAdmin('user')).toBe(false);
  });

  it('isSuperAdmin only matches superadmin', () => {
    expect(isSuperAdmin('superadmin')).toBe(true);
    expect(isSuperAdmin('admin')).toBe(false);
  });

  it('isCreator checks creator and above', () => {
    expect(isCreator('creator')).toBe(true);
    expect(isCreator('editor')).toBe(true);
    expect(isCreator('admin')).toBe(true);
    expect(isCreator('superadmin')).toBe(true);
    expect(isCreator('user')).toBe(false);
  });

  it('isEditor checks editor and above', () => {
    expect(isEditor('editor')).toBe(true);
    expect(isEditor('admin')).toBe(true);
    expect(isEditor('superadmin')).toBe(true);
    expect(isEditor('creator')).toBe(false);
    expect(isEditor('user')).toBe(false);
  });
});
```

- [ ] **Step 2: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run src/__tests__/types/permissions.test.ts`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/types/permissions.test.ts
git commit -m "test(frontend): add role hierarchy and helper function tests"
```

---

### Task 8: Update usePermissions test to cover new methods

**Files:**
- Modify: `apps/web/src/__tests__/hooks/usePermissions.test.tsx`

- [ ] **Step 1: Add tests for `hasRole` and `isSuperAdmin`**

Add test cases for the new `hasRole()` and `isSuperAdmin()` methods on the permission context. Follow existing test patterns in the file.

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm test -- --run src/__tests__/hooks/usePermissions.test.tsx`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/hooks/usePermissions.test.tsx
git commit -m "test(frontend): add hasRole and isSuperAdmin to usePermissions tests"
```

---

## Summary of Changes

### Backend (5 files modified)
1. `UserRole.cs` — Add `Creator = 4`
2. `SessionValidationExtensions.cs` — Fix `HasSufficientRole` hierarchy (Creator at level 1)
3. `Role.cs` — Fix error message + fix Creator permissions (remove Editor) + fix Editor permissions (add Creator)
4. `User.cs` — SuperAdmin protection in both `AssignRole` (require SuperAdmin requester) + `UpdateRole` (guard mutation)
5. `ChangeUserRoleCommandHandler.cs` — Add Creator to allowed roles
6. `AdminUserEndpoints.cs` — Role change endpoints require SuperAdmin session

### Frontend (2 files modified, 1 created)
1. `permissions.ts` — Add `ROLE_HIERARCHY`, `hasMinimumRole`, `isSuperAdmin`, `isCreator`, `isEditor`, `Pending` status
2. `PermissionContext.tsx` — Add `hasRole()`, `isSuperAdmin()` to context with proper import alias

### Tests (3 files)
1. `RoleTests.cs` — Creator permissions + full hierarchy tests
2. `permissions.test.ts` — Frontend role hierarchy tests
3. `usePermissions.test.tsx` — Updated for new context methods

### Review Issues Addressed
- ✅ CRITICAL #1: Creator line 56 inverted hierarchy — fixed in Task 3 Step 2
- ✅ CRITICAL #3: `this.Role.IsSuperAdmin()` disambiguation — fixed in Task 4 Steps 1-2
- ✅ IMPORTANT #4: Handler uses UpdateRole not AssignRole — documented in Task 4 Step 3, UpdateRole now guards SuperAdmin
- ✅ IMPORTANT #5: RequireSuperAdminSession already exists — clarified in Task 4 Step 4
- ✅ IMPORTANT #6: Bulk route has no filter — noted in Task 4 Step 5
- ✅ IMPORTANT #7: Test assertions match corrected code — fixed in Task 6
- ✅ IMPORTANT #8: Import alias shown explicitly — fixed in Task 5 Step 2
- ✅ SUGGESTION #9: Task ordering fixed — code fixes before tests
- ✅ SUGGESTION #10: No migration needed — noted in header
- ✅ SUGGESTION #11: isCreator semantic matches isAdmin convention — documented in Task 5 Step 1
