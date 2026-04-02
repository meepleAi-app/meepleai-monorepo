# Issue #4062: MeepleCard Permission Integration

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Permission System (3/3)
**Estimate**: 2-3 giorni
**Priority**: P1-High
**Depends on**: #4061

---

## 📋 Acceptance Criteria

### AC1: Feature Conditional Rendering
- [ ] `showWishlist` only if `canAccess('wishlist')`
- [ ] `selectable` only if `canAccess('bulk-select')`
- [ ] `draggable` only if `canAccess('drag-drop')`
- [ ] Quick actions filtered by role/tier permissions

### AC2: Quick Actions Permission Filter
- [ ] Each quick action checks `adminOnly` flag
- [ ] Filter actions based on user role
- [ ] Hide admin actions for non-admin users
- [ ] Show upgrade prompt for tier-locked actions

### AC3: Tooltip Permission-Aware Content
- [ ] Tooltip shows tier name badge (Free/Normal/Pro/Enterprise)
- [ ] Locked features show "Upgrade to Pro" message
- [ ] Permission-denied actions show reason

### AC4: Permission Props
- [ ] Add `permissions` prop to MeepleCardProps (optional)
- [ ] Auto-use `usePermissions()` if not provided
- [ ] Allow override for testing/preview modes

---

## 🔧 Implementation

### MeepleCard Permission Integration
```typescript
export function MeepleCard(props: MeepleCardProps) {
  const { canAccess, tier, role } = usePermissions();

  // Override permissions for testing/preview
  const effectivePermissions = props.permissions ?? { canAccess, tier, role };

  // Feature visibility
  const showWishlist = props.showWishlist && effectivePermissions.canAccess('wishlist');
  const enableDrag = props.draggable && effectivePermissions.canAccess('drag-drop');
  const enableSelect = props.selectable && effectivePermissions.canAccess('bulk-select');

  // Filter quick actions
  const filteredQuickActions = props.quickActions?.filter(action => {
    if (action.adminOnly && effectivePermissions.role !== 'admin' && effectivePermissions.role !== 'superAdmin') {
      return false;
    }
    if (action.requiresTier && !effectivePermissions.canAccess(`quick-action.${action.label}`)) {
      return false;
    }
    return !action.hidden;
  });

  return (
    <div className={meepleCardVariants({ variant: props.variant })}>
      {/* Tier badge */}
      {showTierBadge && (
        <TierBadge tier={effectivePermissions.tier} />
      )}

      {/* Conditional features */}
      {showWishlist && <WishlistButton {...wishlistProps} />}
      {enableSelect && <BulkSelectCheckbox {...selectProps} />}
      {enableDrag && <DragHandle {...dragProps} />}

      {/* Permission-aware quick actions */}
      {filteredQuickActions && (
        <QuickActionsMenu
          actions={filteredQuickActions}
          onLockedAction={showUpgradePrompt}
        />
      )}

      {/* Card content */}
      {/* ... */}
    </div>
  );
}
```

### Permission-Aware Tooltip
```typescript
function PermissionTooltip({ feature, children }: { feature: string; children: ReactNode }) {
  const { canAccess, tier } = usePermissions();
  const hasAccess = canAccess(feature);

  return (
    <Tooltip>
      <TooltipTrigger>{children}</TooltipTrigger>
      <TooltipContent>
        {hasAccess ? (
          <span>Click to {feature}</span>
        ) : (
          <div className="flex flex-col gap-2">
            <span className="text-amber-500">🔒 Pro Feature</span>
            <span className="text-sm">Upgrade to Pro to unlock {feature}</span>
            <button className="btn-sm btn-primary">Upgrade Now</button>
          </div>
        )}
      </TooltipContent>
    </Tooltip>
  );
}
```

---

## ✅ Testing Checklist

### Unit Tests
- [ ] Wishlist hidden when `canAccess('wishlist')` = false
- [ ] Bulk select disabled for Free tier
- [ ] Drag-drop disabled for Free tier
- [ ] Admin actions filtered for non-admin users

### Integration Tests
- [ ] Full permission flow: Free user sees limited features
- [ ] Pro user sees all standard features
- [ ] Admin sees all features including admin actions

### Visual Tests
- [ ] Tier badge displays correctly
- [ ] Locked features show upgrade prompt
- [ ] Permission tooltips render properly

---

## 🔗 Dependencies

**Blocked by**: #4061 (Permission Hooks)
**Blocks**: None (parallel with tooltip/tag work)
