# ADR-050: Permission System Architecture (Epic #4068)

**Status**: Accepted
**Date**: 2026-02-12
**Context**: Epic #4068 - MeepleCard Enhancements

## Context

MeepleAI needs a comprehensive permission system to support tier-based monetization, role-based access control, and state-based authorization for features and content.

## Decision

Implement a flexible permission system with:

### 1. Tier-Based Access (Subscription Model)

- **Free**: 50 games, 100MB storage, basic features (wishlist)
- **Normal**: 100 games, 500MB storage, + drag-drop, advanced filters
- **Pro/Premium**: 500 games, 5GB storage, + bulk select, agent creation, analytics
- **Enterprise**: Unlimited, all features

### 2. Role-Based Access (Authorization Model)

- **User**: Own resources only (read/write MY games)
- **Editor**: Moderate public content (flag, suggest edits)
- **Creator**: Publish verified content (official rulebooks)
- **Admin**: Full system access (manage users, delete content)
- **SuperAdmin**: Unrestricted access (system configuration)

### 3. Permission Logic: Tier OR Role

User has access if they meet **either** condition:
- Has required tier OR higher
- Has required role OR higher

**Rationale**: More flexible than AND logic. Allows admin with Free tier to access pro features via role elevation.

### 4. State-Based Authorization

Permission checks can include resource state:
- **Game**: draft (creator only) | published (all) | archived (admin only)
- **Collection**: private (owner only) | shared (link holders) | public (all)
- **Document**: pending/processing (owner only) | ready (permitted users)
- **User Account**: active (allow) | suspended (deny most) | banned (deny all)

### 5. Feature Permission Registry

Centralized registry mapping feature names to permission requirements:

```csharp
"wishlist" → CreateOr(tier: Free, role: User)  // Everyone
"bulk-select" → CreateOr(tier: Pro, role: Editor)  // Pro OR Editor
"quick-action.delete" → CreateAnd(tier: Free, role: Admin)  // Admin ONLY
```

## Implementation Architecture

### Backend (C# / .NET)

**Value Objects**:
- `UserTier`: Free | Normal | Premium/Pro | Enterprise (with `HasLevel()` method)
- `Role`: user | editor | creator | admin | superadmin (with `HasPermission()` method)

**Enums**:
- `UserAccountStatus`: Active | Suspended | Banned
- `GamePublicationState`, `CollectionVisibility`, `DocumentProcessingState`

**Domain Model**:
- `Permission` value object: CreateOr() / CreateAnd() factory methods
- `PermissionContext` record: (tier, role, status, resourceState?)
- `PermissionCheckResult` record: (hasAccess, reason)

**Application Services**:
- `PermissionRegistry`: Central registry of all feature permissions
- Query handlers: GetUserPermissions, CheckPermission

**API Endpoints**:
- `GET /api/v1/permissions/me` → User's permissions & accessible features
- `GET /api/v1/permissions/check?feature=X&state=Y` → Check specific permission

### Frontend (TypeScript / React)

**Context & Hooks**:
- `<PermissionProvider>`: React context wrapping app root
- `usePermissions()`: Hook returning tier, role, canAccess(), hasTier(), isAdmin()

**Components**:
- `<PermissionGate feature="X">`: Conditional rendering wrapper
- `<TierGate minimum="Pro">`: Tier-based rendering
- Permission-aware MeepleCard: Auto-filters features based on user permissions

**Integration**:
- React Query for caching (5min stale time)
- Optimistic updates for better UX
- Graceful degradation (default to Free/User if API fails)

## Consequences

### Positive

✅ **Monetization Enabler**: Clear tier differentiation drives upgrades
✅ **Flexible Authorization**: OR logic allows power users (admins) with low tiers
✅ **Scalable**: Easy to add new features to registry
✅ **Secure**: State-based checks prevent unauthorized access to draft/private content
✅ **Performant**: Frontend caching reduces API calls, backend registry is singleton

### Negative

⚠️ **Complexity**: OR/AND logic requires careful testing
⚠️ **Migration**: Existing users need default tier/role assignment
⚠️ **Cache Invalidation**: Permission changes require cache clearing
⚠️ **Cross-Bounded-Context**: Administration domain depends on Authentication value objects

### Risks & Mitigations

**Risk**: Permission cache desync after tier upgrade
**Mitigation**: Refetch permissions on tier change event, 5min stale time

**Risk**: Frontend shows feature but backend denies
**Mitigation**: Double-check permissions backend, show clear error messages

**Risk**: Circular dependencies between bounded contexts
**Mitigation**: Use shared kernel for common types, keep Permission in Administration

## Alternatives Considered

### Alternative 1: Tier AND Role (Rejected)

**Logic**: User needs tier AND role
**Rejected**: Too restrictive. Admin with Free tier couldn't access admin features.

### Alternative 2: Flat Permission List (Rejected)

**Logic**: Each user has explicit permission list
**Rejected**: Harder to manage, less hierarchical, more DB storage

### Alternative 3: CASL Library (Deferred)

**Option**: Use CASL authorization library
**Decision**: Start with simple custom implementation, migrate to CASL if complexity grows

## Implementation Timeline

**Phase 1** (Week 1): Backend foundation
- Issue #4177: Permission model, enums, value objects, API endpoints

**Phase 2** (Week 2): Frontend integration
- Issue #4178: React hooks, context, gates
- Issue #4179: MeepleCard permission integration

**Phase 3** (Week 3): Testing & refinement
- Issue #4185: E2E tests, accessibility audit, documentation

## Related ADRs

- ADR-041: MeepleCard Universal System (Epic #3820)
- ADR-034: DDD Bounded Contexts (Architecture decision)
- ADR-022: Subscription Tiers (Business model)

## References

- Epic: #4068
- Issues: #4177, #4178, #4179
- Specs: `claudedocs/epic-4068-*.md`
- CASL Library: https://casl.js.org/
