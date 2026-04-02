# Issue #4061: Permission Hooks & Utilities

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Permission System (2/3)
**Estimate**: 3-4 giorni
**Priority**: P1-High
**Depends on**: #4060

---

## 📋 Acceptance Criteria

### AC1: usePermissions Hook
- [ ] Hook `usePermissions()` returns:
  ```typescript
  {
    tier: UserTier,
    role: UserRole,
    status: UserAccountStatus,
    canAccess: (feature: string, resource?: Resource) => boolean,
    hasFeature: (featureName: string) => boolean,
    checkPermission: (permission: Permission) => PermissionCheckResult
  }
  ```
- [ ] Cache permission checks per session
- [ ] Auto-refresh on user tier/role change

### AC2: Permission Context Provider
- [ ] `<PermissionProvider>` wraps app root
- [ ] Fetches user permissions on mount
- [ ] Provides context to all children
- [ ] Loading state mentre fetch in corso

### AC3: Utility Functions
- [ ] `canAccess(feature, resource): boolean` - simple check
- [ ] `hasFeature(featureName): boolean` - tier-based feature check
- [ ] `hasTier(tier): boolean` - tier comparison
- [ ] `hasRole(role): boolean` - role comparison
- [ ] `isActive(): boolean` - account status check

### AC4: React Components
- [ ] `<PermissionGate>` component per conditional rendering:
  ```tsx
  <PermissionGate feature="wishlist" fallback={<UpgradePrompt />}>
    <WishlistButton />
  </PermissionGate>
  ```
- [ ] `<TierGate>` per tier-specific content
- [ ] `<RoleGate>` per role-specific content

### AC5: TypeScript Types
- [ ] Esporta tutti i types da #4060
- [ ] Type guards: `isTier()`, `isRole()`, `isState()`

---

## 🔧 API Examples

### usePermissions Hook Usage
```typescript
function GameCard({ gameId }) {
  const { canAccess, hasFeature, tier, role } = usePermissions();

  const showWishlist = canAccess('wishlist');
  const showBulkSelect = canAccess('bulk-select');
  const canDelete = canAccess('quick-actions.delete', { type: 'game', id: gameId });

  return (
    <MeepleCard
      showWishlist={showWishlist}
      selectable={showBulkSelect}
      quickActions={[
        { label: 'View', onClick: handleView },
        canDelete && { label: 'Delete', onClick: handleDelete, destructive: true }
      ].filter(Boolean)}
    />
  );
}
```

### PermissionGate Component
```typescript
<PermissionGate
  feature="bulk-select"
  tier="Pro"
  fallback={<UpgradeToPro feature="Bulk Selection" />}
>
  <BulkSelectToolbar />
</PermissionGate>
```

### TierGate Component
```typescript
<TierGate minimum="Pro" fallback={<UpgradePrompt />}>
  <AdvancedFilters />
</TierGate>
```

---

## 📊 Implementation Details

### Permission Context
```typescript
interface PermissionContextValue {
  // User info
  tier: UserTier;
  role: UserRole;
  status: UserAccountStatus;

  // Permission checks
  canAccess: (feature: string, resource?: Resource) => boolean;
  hasFeature: (featureName: string) => boolean;
  hasTier: (tier: UserTier) => boolean;
  hasRole: (role: UserRole) => boolean;
  isActive: () => boolean;

  // Advanced checks
  checkPermission: (permission: Permission) => PermissionCheckResult;

  // Loading state
  loading: boolean;
  error?: Error;

  // Refresh
  refresh: () => Promise<void>;
}

const PermissionContext = createContext<PermissionContextValue | null>(null);

export function usePermissions(): PermissionContextValue {
  const context = useContext(PermissionContext);
  if (!context) {
    throw new Error('usePermissions must be used within PermissionProvider');
  }
  return context;
}
```

### PermissionProvider Implementation
```typescript
export function PermissionProvider({ children }: { children: ReactNode }) {
  const [userPermissions, setUserPermissions] = useState<UserPermissions | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error>();

  useEffect(() => {
    fetchUserPermissions()
      .then(setUserPermissions)
      .catch(setError)
      .finally(() => setLoading(false));
  }, []);

  const canAccess = useCallback((feature: string, resource?: Resource) => {
    if (!userPermissions) return false;
    // Logic using #4060 permission model
    return checkFeatureAccess(feature, userPermissions, resource);
  }, [userPermissions]);

  const value = {
    tier: userPermissions?.tier ?? 'Free',
    role: userPermissions?.role ?? 'user',
    status: userPermissions?.status ?? 'active',
    canAccess,
    hasFeature: (name) => canAccess(name),
    hasTier: (tier) => compareTiers(userPermissions?.tier, tier) >= 0,
    hasRole: (role) => compareRoles(userPermissions?.role, role) >= 0,
    isActive: () => userPermissions?.status === 'active',
    checkPermission: (permission) => checkPermissionDetailed(permission, userPermissions),
    loading,
    error,
    refresh: fetchUserPermissions,
  };

  return (
    <PermissionContext.Provider value={value}>
      {children}
    </PermissionContext.Provider>
  );
}
```

---

## ✅ Testing Checklist

### Unit Tests
- [ ] `usePermissions()` throws outside provider
- [ ] `canAccess()` returns correct boolean per feature
- [ ] `hasFeature()` checks tier requirements
- [ ] `hasTier()` compares tier hierarchy correctly
- [ ] `hasRole()` compares role hierarchy correctly

### Component Tests
- [ ] `<PermissionGate>` shows children when permitted
- [ ] `<PermissionGate>` shows fallback when denied
- [ ] `<TierGate>` checks minimum tier
- [ ] `<RoleGate>` checks minimum role

### Integration Tests
- [ ] Permission context fetches user data on mount
- [ ] Permission refresh updates context
- [ ] Multiple consumers get consistent permissions

### Performance Tests
- [ ] Permission checks cached (no redundant API calls)
- [ ] Context re-renders minimized
- [ ] Large component trees performant

---

## 🔗 Dependencies

**Blocked by**: #4060 (Permission Model)
**Blocks**: #4062 (MeepleCard Integration)

---

## 📚 Resources

- **React Context Docs**: https://react.dev/reference/react/useContext
- **Zustand (if needed)**: https://zustand-demo.pmnd.rs/
- **React Query**: https://tanstack.com/query/latest
