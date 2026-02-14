# Epic #4068: Best Practices & Patterns

**Proven patterns for permission system, tags, tooltips, and agent metadata**

---

## Permission System Best Practices

### Practice 1: Always Check Permissions Backend

**❌ Anti-pattern**: Frontend-only permission checks

```typescript
// INSECURE: Frontend hides button, but API endpoint unprotected
function DeleteButton({ gameId }: Props) {
  const { isAdmin } = usePermissions();

  if (!isAdmin()) return null; // Hidden from non-admins

  return <button onClick={() => fetch(`/api/games/${gameId}`, { method: 'DELETE' })}>Delete</button>;
}

// API endpoint (VULNERABLE)
app.MapDelete("/api/games/{id}", async (Guid id, IMediator m) =>
{
    // No permission check! Any authenticated user can call this
    return await m.Send(new DeleteGameCommand(id));
});
```

**✓ Best practice**: Backend validates every request

```typescript
// Frontend: UX only
function DeleteButton({ gameId }: Props) {
  const { canAccess } = usePermissions();

  if (!canAccess('quick-action.delete')) return null;

  return <button onClick={handleDelete}>Delete</button>;
}

// Backend: Enforcement
app.MapDelete("/api/games/{id}", async (Guid id, HttpContext ctx, IMediator m) =>
{
    var userId = ctx.User.GetUserId();
    var permCheck = await m.Send(new CheckPermissionQuery(userId, "quick-action.delete"));

    if (!permCheck.HasAccess)
        return Results.Forbid(); // 403 Forbidden

    return await m.Send(new DeleteGameCommand(id, userId));
});
```

**Why**: Attackers can bypass frontend checks via curl/Postman. Backend is source of truth.

---

### Practice 2: Cache Permission Checks, Not Results

**❌ Anti-pattern**: Cache permission decisions directly

```typescript
// BAD: Stale permissions after tier change
const canBulkSelect = useMemo(() => checkPermission('bulk-select'), []); // Cached forever!

// User upgrades to Pro
// canBulkSelect still false (cache not updated)
```

**✓ Best practice**: Cache source data, compute permissions fresh

```typescript
// GOOD: React Query caches API response, permissions computed fresh
const { data } = useQuery({
  queryKey: ['permissions', 'me'],
  queryFn: getUserPermissions,
  staleTime: 5 * 60 * 1000 // Cache API response
});

// Computed fresh every render (cheap, already in memory)
const canBulkSelect = data?.accessibleFeatures.includes('bulk-select') ?? false;

// When user upgrades, invalidate cache
queryClient.invalidateQueries({ queryKey: ['permissions'] });
// Next access refetches, canBulkSelect updates automatically
```

**Why**: Source data changes less frequently than permission checks. Cache source, derive results.

---

### Practice 3: Fail Secure (Deny by Default)

**❌ Anti-pattern**: Optimistic permissions

```typescript
// BAD: Assume access while loading
const { canAccess, loading } = usePermissions();

return canAccess('feature') || loading ? <Feature /> : <Denied />;
// Shows feature during loading (before permissions fetched)
```

**✓ Best practice**: Deny during loading

```typescript
// GOOD: Show nothing (or skeleton) while loading
const { canAccess, loading } = usePermissions();

if (loading) return <Skeleton />;

return canAccess('feature') ? <Feature /> : <Denied />;
// Only shows feature after confirming access
```

**Why**: Security principle - deny by default. Don't grant access speculatively.

---

### Practice 4: Use Permission Gates for Composition

**❌ Anti-pattern**: Inline permission checks everywhere

```typescript
function Dashboard() {
  const { canAccess } = usePermissions();

  return (
    <div>
      <h1>Dashboard</h1>

      {canAccess('analytics.view') && (
        <div>
          <h2>Analytics</h2>
          <AnalyticsWidget />
        </div>
      )}

      {canAccess('agent.create') && (
        <div>
          <h2>Create Agent</h2>
          <AgentForm />
        </div>
      )}

      {/* Repeated pattern 10+ times */}
    </div>
  );
}
```

**✓ Best practice**: Extract PermissionGate for reuse

```typescript
function Dashboard() {
  return (
    <div>
      <h1>Dashboard</h1>

      <PermissionGate feature="analytics.view">
        <Section title="Analytics">
          <AnalyticsWidget />
        </Section>
      </PermissionGate>

      <PermissionGate feature="agent.create">
        <Section title="Create Agent">
          <AgentForm />
        </Section>
      </PermissionGate>
    </div>
  );
}
```

**Why**: DRY principle, easier to audit permissions, consistent UX.

---

### Practice 5: Graceful Degradation with Fallbacks

**❌ Anti-pattern**: Hide features without explanation

```typescript
{canAccess('bulk-select') && <BulkSelectButton />}
// User sees nothing, doesn't know feature exists
```

**✓ Best practice**: Show locked features with upgrade path

```tsx
<PermissionGate
  feature="bulk-select"
  fallback={
    <Card className="border-dashed">
      <Lock className="text-muted-foreground" />
      <h3>Bulk Selection</h3>
      <p>Select multiple games for batch operations.</p>
      <Badge>Pro Feature</Badge>
      <Button onClick={() => router.push('/upgrade')}>Upgrade to Pro</Button>
    </Card>
  }
>
  <BulkSelectToolbar />
</PermissionGate>
```

**Why**: Users discover Pro features, clear upgrade value proposition.

---

## Tag System Best Practices

### Practice 6: Limit Visible Tags (Max 3)

**❌ Anti-pattern**: Show all tags (cluttered UI)

```tsx
<TagStrip tags={allTags} maxVisible={allTags.length} />
// 8 tags stacked = 200px+ strip (overwhelms card)
```

**✓ Best practice**: Max 3 visible + overflow

```tsx
<TagStrip tags={allTags} maxVisible={3} />
// Shows top 3, "+5" badge for remaining
```

**Why**: Information hierarchy - most important tags first, overflow for additional context.

---

### Practice 7: Prioritize Tags by Importance

**❌ Anti-pattern**: Random tag order

```tsx
const tags = [
  { id: 'owned', ... },
  { id: 'sale', ... },
  { id: 'new', ... }
]; // Arbitrary order

<TagStrip tags={tags} />
// Renders: Owned, Sale, New (not prioritized)
```

**✓ Best practice**: Sort by priority

```tsx
import { sortTagsByPriority } from '@/lib/tags/utils';

const sorted = sortTagsByPriority(tags, 'game');
// Returns: New, Sale, Owned (critical first)

<TagStrip tags={sorted} maxVisible={3} />
```

**Priority logic**:
- Time-sensitive first: `exclusive`, `new`, `preorder`, `sale`
- User-specific last: `owned`, `wishlisted`

---

### Practice 8: Use Entity-Specific Presets

**❌ Anti-pattern**: Manual tag creation

```tsx
const tags = [
  { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' },
  { id: 'sale', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' }
]; // Tedious, error-prone
```

**✓ Best practice**: Use preset utilities

```tsx
import { createTagsFromKeys } from '@/lib/tags/utils';

const tags = createTagsFromKeys('game', ['new', 'sale']);
// Automatically applies preset colors, icons, tooltips
```

**Why**: Consistency, less code, automatic updates when presets change.

---

### Practice 9: Responsive Tag Variants

**❌ Anti-pattern**: Fixed variant regardless of screen size

```tsx
<TagStrip tags={tags} variant="desktop" />
// On mobile: 32px strip too wide, labels truncated
```

**✓ Best practice**: Responsive variant

```tsx
import { useMediaQuery } from '@/hooks/useMediaQuery';

function ResponsiveTagStrip({ tags }: Props) {
  const isMobile = useMediaQuery('(max-width: 768px)');
  const isTablet = useMediaQuery('(max-width: 1024px)');

  const variant = isMobile ? 'mobile' : isTablet ? 'tablet' : 'desktop';

  return <TagStrip tags={tags} variant={variant} />;
}
```

**Or use Tailwind responsive classes**:
```tsx
// Conditional rendering (simpler)
<>
  <TagStrip tags={tags} variant="desktop" className="hidden lg:block" />
  <TagStrip tags={tags} variant="tablet" className="hidden md:block lg:hidden" />
  <TagStrip tags={tags} variant="mobile" className="block md:hidden" />
</>
```

---

## Tooltip Best Practices

### Practice 10: Debounce Tooltip Positioning

**❌ Anti-pattern**: Recalculate on every scroll event

```typescript
useEffect(() => {
  window.addEventListener('scroll', updatePosition); // Fires 60+ times/second
}, []);
```

**✓ Best practice**: Debounce to reduce calculations

```typescript
const debouncedUpdate = useMemo(() =>
  debounce(updatePosition, 100),
  [updatePosition]
);

useEffect(() => {
  window.addEventListener('scroll', debouncedUpdate, { passive: true });
  return () => window.removeEventListener('scroll', debouncedUpdate);
}, [debouncedUpdate]);
```

**Why**: 100ms debounce = ~10 calculations/second instead of 60+ (6x fewer).

---

### Practice 11: Tooltip Content Accessibility

**❌ Anti-pattern**: Tooltip missing ARIA attributes

```tsx
<div onMouseEnter={() => setShowTooltip(true)}>
  Hover me
</div>

{showTooltip && <div>Tooltip content</div>}
// Screen reader doesn't announce tooltip
```

**✓ Best practice**: Proper ARIA linkage

```tsx
const tooltipId = useId();

<div
  role="button"
  aria-describedby={showTooltip ? tooltipId : undefined}
  onMouseEnter={() => setShowTooltip(true)}
>
  Hover me
</div>

{showTooltip && (
  <div
    id={tooltipId}
    role="tooltip"
    aria-live="polite"
  >
    Tooltip content
  </div>
)}
```

**Why**: Screen readers announce tooltip content, users know additional info available.

---

### Practice 12: Mobile Touch Tooltips

**❌ Anti-pattern**: Hover-only tooltips (don't work on mobile)

```tsx
<div onMouseEnter={() => setShow(true)}>
  Info
</div>
// On mobile: No hover, tooltip never shows
```

**✓ Best practice**: Tap on mobile, hover on desktop

```tsx
const isTouchDevice = 'ontouchstart' in window;

<div
  onMouseEnter={() => !isTouchDevice && setShow(true)}
  onMouseLeave={() => !isTouchDevice && setShow(false)}
  onClick={() => isTouchDevice && setShow(s => !s)}
>
  Info
</div>

{/* Mobile dismiss button */}
{show && isTouchDevice && (
  <button onClick={() => setShow(false)} aria-label="Close">
    <X className="w-4 h-4" />
  </button>
)}
```

**Why**: Touch devices need tap, not hover. Provide dismiss mechanism.

---

## Agent Metadata Best Practices

### Practice 13: Format Large Numbers

**❌ Anti-pattern**: Raw numbers (hard to read)

```tsx
<span>{agent.invocationCount}</span>
// Renders: 123456789 (hard to parse)
```

**✓ Best practice**: Human-readable format

```tsx
import { formatInvocationCount } from '@/lib/agent/formatters';

<span>{formatInvocationCount(agent.invocationCount)}</span>
// Renders: 123.5M (easy to parse)
```

**Formatting Rules**:
- < 1,000: Show exact number (`342`)
- < 1,000,000: Show with K (`1.2K`)
- ≥ 1,000,000: Show with M (`123.5M`)

---

### Practice 14: Real-Time Agent Status

**❌ Anti-pattern**: Stale status (never updates)

```tsx
const { data: agent } = useQuery({
  queryKey: ['agent', agentId],
  queryFn: getAgent
  // No refetchInterval, status stale
});

<AgentStatusBadge status={agent.status} />
// Shows "Active" but agent crashed 5 minutes ago
```

**✓ Best practice**: Poll or use WebSocket

```tsx
// Option 1: Poll every 30 seconds
const { data: agent } = useQuery({
  queryKey: ['agent', agentId],
  queryFn: getAgent,
  refetchInterval: 30000
});

// Option 2: WebSocket updates (better)
useEffect(() => {
  socket.on(`agent:${agentId}:status`, (newStatus) => {
    queryClient.setQueryData(['agent', agentId], old => ({
      ...old,
      status: newStatus
    }));
  });

  return () => socket.off(`agent:${agentId}:status`);
}, [agentId]);

<AgentStatusBadge status={agent.status} />
// Shows real-time status
```

**Why**: Agent status changes frequently, users need current information.

---

### Practice 15: Capability Tags from Agent Config

**❌ Anti-pattern**: Hardcode capabilities

```tsx
<MeepleCard
  entity="agent"
  tags={[
    { id: 'rag', label: 'RAG' },
    { id: 'vision', label: 'Vision' }
  ]}
/>
// Must manually update if agent capabilities change
```

**✓ Best practice**: Derive from agent configuration

```tsx
import { createTagsFromKeys } from '@/lib/tags/utils';

const capabilityTags = createTagsFromKeys('agent', agent.capabilities);
// agent.capabilities = ['RAG', 'Vision', 'Code']
// Auto-generates tags with correct icons/colors

<MeepleCard
  entity="agent"
  tags={capabilityTags}
/>
// Automatically reflects agent's actual capabilities
```

**Why**: Single source of truth, no manual sync required.

---

## Collection Limits Best Practices

### Practice 16: Proactive Warning Before Limit

**❌ Anti-pattern**: Block action when limit reached

```tsx
function AddGameButton({ game }: Props) {
  const handleAdd = async () => {
    const result = await addToCollection(game.id);

    if (result.error === 'LIMIT_REACHED') {
      toast.error('Collection limit reached. Upgrade to add more games.');
    }
  };

  return <button onClick={handleAdd}>Add to Collection</button>;
}
// User adds game, action fails, frustration
```

**✓ Best practice**: Warn before attempt, prevent frustration

```tsx
function AddGameButton({ game }: Props) {
  const { limits } = usePermissions();
  const { data: collection } = useCollectionQuery();

  const isNearLimit = collection.games.length >= limits.maxGames * 0.9;
  const isAtLimit = collection.games.length >= limits.maxGames;

  const handleAdd = async () => {
    if (isAtLimit) {
      toast.error('Collection full. Upgrade to add more games.');
      return; // Prevent API call
    }

    if (isNearLimit) {
      const confirmed = await confirm('You are approaching your collection limit. Continue?');
      if (!confirmed) return;
    }

    await addToCollection(game.id);
    toast.success('Game added!');
  };

  return (
    <button onClick={handleAdd} disabled={isAtLimit}>
      {isAtLimit ? 'Collection Full' : 'Add to Collection'}
      {isAtLimit && <Lock />}
    </button>
  );
}
```

**Why**: Proactive warnings > reactive errors. Users appreciate transparency.

---

### Practice 17: Visible Limit Progress

**❌ Anti-pattern**: Hidden limits (user doesn't know capacity)

```tsx
// Collection page with no limit indication
<div>
  <h1>My Collection ({games.length} games)</h1>
  {games.map(game => <GameCard {...game} />)}
</div>
// User doesn't know they're at 475/500 until hitting limit
```

**✓ Best practice**: Always show limits

```tsx
<div>
  <div className="flex justify-between items-center mb-4">
    <h1>My Collection</h1>
    <CollectionLimitIndicator
      gameCount={games.length}
      storageMB={totalStorageMB}
    />
  </div>

  {/* If near limit, show prominent warning */}
  {games.length / limits.maxGames > 0.9 && (
    <Alert variant="warning">
      <AlertTriangle />
      <AlertTitle>Collection Almost Full</AlertTitle>
      <AlertDescription>
        You've used {games.length} of {limits.maxGames} game slots.
        <Button variant="link" onClick={() => router.push('/upgrade')}>
          Upgrade to Enterprise for unlimited games
        </Button>
      </AlertDescription>
    </Alert>
  )}

  {games.map(game => <GameCard {...game} />)}
</div>
```

**Why**: Transparency builds trust, enables informed decisions.

---

## Accessibility Best Practices

### Practice 18: Keyboard Navigation for All Interactions

**❌ Anti-pattern**: Mouse-only interactions

```tsx
<div onClick={handleAction}>
  Clickable div
</div>
// Not keyboard accessible (div not focusable)
```

**✓ Best practice**: Semantic HTML or ARIA

```tsx
// Option 1: Use <button>
<button onClick={handleAction}>
  Clickable button
</button>
// Automatically focusable, Enter/Space work

// Option 2: Add keyboard support to div
<div
  role="button"
  tabIndex={0}
  onClick={handleAction}
  onKeyDown={(e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      handleAction();
    }
  }}
>
  Clickable div
</div>
```

**Why**: Keyboard users (power users, accessibility) need access.

---

### Practice 19: Sufficient Color Contrast

**❌ Anti-pattern**: Low contrast (WCAG fail)

```tsx
const tag = {
  bgColor: 'hsl(142 76% 76%)', // Light green
  color: 'hsl(0 0% 100%)'      // White
};
// Contrast ratio: 2.3:1 (fails WCAG AA 4.5:1 requirement)
```

**✓ Best practice**: Verify contrast ≥ 4.5:1

```tsx
const tag = {
  bgColor: 'hsl(142 76% 36%)', // Dark green
  color: 'hsl(0 0% 100%)'      // White
};
// Contrast ratio: 7.8:1 (passes WCAG AA)

// Verify with tool: https://webaim.org/resources/contrastchecker/
```

**Why**: Low-vision users, high contrast mode users need readable text.

---

### Practice 20: Focus Visible Indicators

**❌ Anti-pattern**: No focus indicator (keyboard navigation invisible)

```tsx
<button className="focus:outline-none">
  No focus ring
</button>
// User tabs to button, no visual indication
```

**✓ Best practice**: Clear focus indicator

```tsx
<button className="focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">
  With focus ring
</button>
// Keyboard focus shows 2px ring, mouse click doesn't (focus-visible)
```

**Why**: WCAG 2.4.7 Focus Visible (AA) - keyboard users need visual focus indicator.

---

## Performance Best Practices

### Practice 21: Memoize Expensive Computations

**❌ Anti-pattern**: Recompute every render

```tsx
function GameCard({ game }: Props) {
  const metadata = [
    { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
    { icon: Clock, value: `${game.playTime}m` }
  ]; // New array every render

  return <MeepleCard metadata={metadata} />;
  // MeepleCard re-renders (React.memo ineffective - new reference)
}
```

**✓ Best practice**: Memoize stable values

```tsx
function GameCard({ game }: Props) {
  const metadata = useMemo(() => [
    { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
    { icon: Clock, value: `${game.playTime}m` }
  ], [game.minPlayers, game.maxPlayers, game.playTime]);

  return <MeepleCard metadata={metadata} />;
  // MeepleCard skips re-render if metadata unchanged
}
```

**Why**: Stable references enable React.memo optimizations.

---

### Practice 22: Virtualize Large Grids

**❌ Anti-pattern**: Render all cards eagerly

```tsx
<div className="grid grid-cols-4 gap-4">
  {games.map(game => <MeepleCard key={game.id} {...game} />)}
</div>
// 200 cards = 800ms render, laggy scroll
```

**✓ Best practice**: Virtualize if > 50 cards

```tsx
import { FixedSizeGrid } from 'react-window';

<FixedSizeGrid
  columnCount={4}
  columnWidth={300}
  rowCount={Math.ceil(games.length / 4)}
  rowHeight={400}
  height={800}
  width={1200}
>
  {({ columnIndex, rowIndex, style }) => (
    <div style={style}>
      <MeepleCard {...games[rowIndex * 4 + columnIndex]} />
    </div>
  )}
</FixedSizeGrid>
// Only renders visible cards, smooth 60fps scroll
```

**Why**: DOM performance degrades with 50+ elements. Virtualization keeps DOM small.

---

### Practice 23: Lazy Load Images

**❌ Anti-pattern**: Eager load all images

```tsx
<MeepleCard imageUrl="/games/huge-image.jpg" />
// Next.js Image default: loading="lazy" (good!)

// But if using regular img:
<img src="/games/huge-image.jpg" />
// Loads all images immediately (slow)
```

**✓ Best practice**: Explicit lazy loading

```tsx
<Image
  src="/games/huge-image.jpg"
  width={300}
  height={400}
  loading="lazy"
  placeholder="blur"
  blurDataURL={placeholderDataUrl}
/>
```

**Why**: Lazy loading = faster initial page load, lower bandwidth.

---

## Testing Best Practices

### Practice 24: Test Permission Boundaries

**❌ Anti-pattern**: Test only happy path

```typescript
test('Pro user can bulk select', () => {
  // Test Pro user (passes)
});
// Missing: Free user test (boundary case)
```

**✓ Best practice**: Test boundary conditions

```typescript
describe('Bulk Select Permission', () => {
  test('Free tier user cannot bulk select', () => {
    const { canAccess } = renderWithPermissions({ tier: 'free', role: 'user' });
    expect(canAccess('bulk-select')).toBe(false);
  });

  test('Normal tier user cannot bulk select (tier insufficient)', () => {
    const { canAccess } = renderWithPermissions({ tier: 'normal', role: 'user' });
    expect(canAccess('bulk-select')).toBe(false);
  });

  test('Pro tier user CAN bulk select', () => {
    const { canAccess } = renderWithPermissions({ tier: 'pro', role: 'user' });
    expect(canAccess('bulk-select')).toBe(true); // Boundary: Minimum tier
  });

  test('Free tier editor CAN bulk select (role sufficient)', () => {
    const { canAccess } = renderWithPermissions({ tier: 'free', role: 'editor' });
    expect(canAccess('bulk-select')).toBe(true); // OR logic works
  });
});
```

**Why**: Boundary conditions are where bugs hide. Test Free (deny), Pro (allow), Editor (allow via role).

---

### Practice 25: Mock Permission Provider in Tests

**✓ Best practice**: Reusable test utility

```typescript
// tests/utils/renderWithPermissions.tsx
export function renderWithPermissions(
  ui: ReactElement,
  permissions: Partial<UserPermissions> = {}
) {
  const defaultPermissions: UserPermissions = {
    tier: 'free',
    role: 'user',
    status: 'Active',
    limits: { maxGames: 50, storageQuotaMB: 100 },
    accessibleFeatures: ['wishlist'],
    ...permissions
  };

  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  });

  queryClient.setQueryData(['permissions', 'me'], defaultPermissions);

  return render(
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>
        {ui}
      </PermissionProvider>
    </QueryClientProvider>
  );
}

// Usage in tests
test('Shows bulk select for Pro user', () => {
  renderWithPermissions(
    <GameCard game={mockGame} />,
    { tier: 'pro', accessibleFeatures: ['wishlist', 'bulk-select'] }
  );

  expect(screen.getByRole('checkbox')).toBeInTheDocument();
});
```

**Why**: DRY principle, consistent test setup, easy to test different permission states.

---

## Code Organization Best Practices

### Practice 26: Co-locate Related Files

**❌ Anti-pattern**: Scattered files

```
components/
  TagBadge.tsx
  TagOverflow.tsx
  TagStrip.tsx
lib/
  tags.ts
  tag-utils.ts
types/
  tag.ts
  tag-variant.ts
```

**✓ Best practice**: Feature-based organization

```
components/ui/tags/
  TagBadge.tsx
  TagOverflow.tsx
  TagStrip.tsx
  __tests__/
    TagBadge.test.tsx
    TagStrip.test.tsx
  TagStrip.stories.tsx

lib/tags/
  presets.ts
  utils.ts

types/
  tags.ts  # All tag-related types in one file
```

**Why**: Related files together = easier to find, modify, review.

---

### Practice 27: Barrel Exports for Clean Imports

**✓ Best practice**: Index files for convenience

```typescript
// components/ui/tags/index.ts
export { TagStrip } from './TagStrip';
export { TagBadge } from './TagBadge';
export { TagOverflow } from './TagOverflow';
export type { Tag, TagStripProps, TagVariant } from '@/types/tags';

// Usage (clean imports)
import { TagStrip, TagBadge, type Tag } from '@/components/ui/tags';

// Instead of
import { TagStrip } from '@/components/ui/tags/TagStrip';
import { TagBadge } from '@/components/ui/tags/TagBadge';
import type { Tag } from '@/types/tags';
```

---

## Documentation Best Practices

### Practice 28: Document Props with JSDoc

**✓ Best practice**: Comprehensive JSDoc

```typescript
/**
 * Vertical tag strip for MeepleCard left/right edge
 *
 * @param tags - Array of tags to display
 * @param maxVisible - Maximum visible tags (default: 3), remaining shown in overflow badge
 * @param variant - Responsive variant: 'desktop' (32px), 'tablet' (28px), 'mobile' (24px icon-only)
 * @param position - Strip position: 'left' (default) or 'right'
 *
 * @example
 * ```tsx
 * <TagStrip
 *   tags={[
 *     { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' }
 *   ]}
 *   maxVisible={3}
 *   variant="desktop"
 * />
 * ```
 *
 * @see {@link Tag} for tag structure
 * @see Epic #4068 Issue #4181
 */
export function TagStrip({ tags, maxVisible = 3, variant = 'desktop', position = 'left' }: TagStripProps) {
  // ...
}
```

**Why**: IntelliSense shows docs, examples guide usage.

---

## Version Control Best Practices

### Practice 29: Atomic Commits

**❌ Anti-pattern**: Giant commit with all changes

```bash
git commit -m "Add Epic #4068 features"
# 50 files changed, 5000+ lines added
# Impossible to review or revert granularly
```

**✓ Best practice**: Logical atomic commits

```bash
git commit -m "feat(admin): Add UserTier.Enterprise and Role.Creator"
# 2 files changed, 50 lines

git commit -m "feat(admin): Implement Permission value object with OR/AND logic"
# 1 file changed, 120 lines

git commit -m "feat(admin): Create PermissionRegistry service"
# 1 file changed, 80 lines

# ... etc (10-15 commits for full epic)
```

**Why**: Easier code review, easier to revert specific features, clearer history.

---

### Practice 30: Descriptive PR Descriptions

**❌ Anti-pattern**: Minimal PR description

```markdown
## Changes
- Added permission system

Closes #4177
```

**✓ Best practice**: Comprehensive description

```markdown
## Summary
Implements tier/role/state-based permission system for feature access control (Epic #4068, Issue #4177).

## Changes

### Backend
- **UserTier**: Added `Pro` and `Enterprise` tiers, `GetLimits()` method
- **Role**: Added `Creator` role (between Editor and Admin)
- **UserAccountStatus**: New enum (Active/Suspended/Banned) replacing `IsSuspended` bool
- **Permission**: Value object with `CreateOr()`/`CreateAnd()` factory methods, state-based checks
- **PermissionRegistry**: Singleton service mapping 10 features to permission requirements
- **Queries**: GetUserPermissions, CheckPermission with MediatR handlers
- **API**: New endpoints `/api/v1/permissions/me`, `/api/v1/permissions/check`
- **Migration**: AddUserAccountStatus (adds Status column, indexes)

### Frontend
- **Types**: UserTier, UserRole, UserAccountStatus, UserPermissions
- **Context**: PermissionProvider with React Query integration
- **Hook**: usePermissions() with canAccess(), hasTier(), isAdmin()
- **Components**: PermissionGate, TierGate for conditional rendering
- **API Client**: getUserPermissions(), checkPermission() functions

### Tests
- Unit: 95% coverage (Permission, UserTier, Role extensions)
- Integration: GetUserPermissions, CheckPermission query tests
- E2E: Free/Pro/Admin user flows, state-based checks

## Breaking Changes
- `User.IsSuspended` (bool) → `User.Status` (enum) [marked Obsolete, removed in v2.0]

## Migration Required
- Database: `dotnet ef database update` (adds Status column)
- Frontend: Wrap app in `<PermissionProvider>` (see migration guide)

## Performance
- PermissionRegistry: 0.01ms per check (singleton cached)
- /permissions/me: ~15ms p95 (indexed SELECT)
- Frontend cache: 5min stale time (minimal API calls)

## Security
- Backend validates all permission checks (not frontend-only)
- Tier/role read from database (not JWT claims)
- Permission failures logged (security monitoring)
- Rate limiting: 100 req/min per user

## Documentation
- ADR-050: Permission System Architecture
- Implementation Guide: docs/02-development/epic-4068-implementation-guide.md
- API Reference: docs/03-api/permission-api-reference.md
- Migration Guide: docs/09-migrations/epic-4068-migration-guide.md

## Screenshots
[Attach screenshots: Free tier UI, Pro tier UI, Admin panel]

## Testing Instructions
```bash
# Backend tests
cd apps/api/src/Api && dotnet test --filter Epic=4068

# Frontend tests
cd apps/web && pnpm test -- Permission

# E2E tests
pnpm test:e2e -- permission-flows
```

## Deployment Notes
- Migration runs automatically on deploy (AddUserAccountStatus)
- Existing users default to Free tier + User role + Active status
- No downtime required (backward compatible)
- Rollback safe (migration is additive)

Closes #4177
Related: #4068 (Epic), #4178 (next issue in sequence)
```

**Why**: Thorough PR description aids reviewers, documents decisions, helps future debugging.

---

## Integration Best Practices

### Practice 31: Consistent Error Handling

**✓ Best practice**: Unified error handling

```typescript
// lib/api/client.ts
apiClient.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 403) {
      // Permission denied - show upgrade prompt
      toast.error('This feature requires a higher tier.');
      router.push('/upgrade');
    } else if (error.response?.status === 401) {
      // Unauthorized - clear auth and redirect
      queryClient.clear();
      router.push('/login');
    }

    return Promise.reject(error);
  }
);
```

**Why**: Centralized error handling = consistent UX across all permission checks.

---

### Practice 32: Progressive Enhancement

**✓ Best practice**: Core functionality works, enhancements layer on

```tsx
// Base: Functional without permissions (shows all features)
function GameCard({ game }: Props) {
  return (
    <MeepleCard
      entity="game"
      title={game.title}
      showWishlist
      selectable
    />
  );
}

// Enhanced: Permission-aware (if PermissionProvider available)
function GameCard({ game }: Props) {
  const permissions = usePermissions();

  return (
    <MeepleCard
      entity="game"
      title={game.title}
      showWishlist={permissions?.canAccess('wishlist') ?? true} // Default to true
      selectable={permissions?.canAccess('bulk-select') ?? false} // Default to false (safer)
    />
  );
}
```

**Why**: Graceful degradation if permission system fails.

---

## Monitoring Best Practices

### Practice 33: Instrument Permission Checks

**✓ Best practice**: Track permission metrics

```csharp
public PermissionCheckResult Check(PermissionContext ctx)
{
    var sw = Stopwatch.StartNew();
    var result = /* ... permission logic ... */;
    sw.Stop();

    // Metrics
    PermissionMetrics.CheckDuration.Record(sw.ElapsedMilliseconds);
    PermissionMetrics.CheckTotal.Add(1, new KeyValuePair<string, object?>("feature", FeatureName));

    if (!result.HasAccess)
    {
        PermissionMetrics.DeniedTotal.Add(1, new KeyValuePair<string, object?>("reason", result.Reason));
    }

    return result;
}
```

**Dashboard Queries**:
```promql
# Permission check rate
rate(permission_check_total[5m])

# Permission denied rate (alert if > 10%)
rate(permission_denied_total[5m]) / rate(permission_check_total[5m])

# P95 check duration (alert if > 100ms)
histogram_quantile(0.95, permission_check_duration_seconds_bucket)
```

**Why**: Data-driven optimization, early detection of permission issues.

---

## Real-World Scenarios

### Scenario 1: E-Commerce Tier Upsell

**Use Case**: Game marketplace with tiered features

```tsx
function GamePurchaseFlow() {
  const { tier, canAccess } = usePermissions();

  return (
    <div>
      {/* Core feature: Available to all */}
      <BuyGameButton gameId={gameId} />

      {/* Premium feature: Wishlist tracking */}
      {canAccess('wishlist') ? (
        <WishlistButton gameId={gameId} />
      ) : (
        <div className="mt-4 p-4 border-dashed border-2 rounded-lg">
          <h4>Want to save this game for later?</h4>
          <p>Wishlist feature available in Normal tier ($4.99/month)</p>
          <Button>Start Free Trial</Button>
        </div>
      )}

      {/* Advanced feature: Price alerts */}
      <TierGate
        minimum="pro"
        fallback={
          <LockedFeatureCard
            title="Price Drop Alerts"
            description="Get notified when this game goes on sale"
            requiredTier="Pro"
            price="$9.99/month"
          />
        }
      >
        <PriceAlertSettings gameId={gameId} />
      </TierGate>
    </div>
  );
}
```

---

### Scenario 2: Content Moderation Workflow

**Use Case**: User-generated content with editor/admin moderation

```tsx
function GameReviewCard({ review }: Props) {
  const { role, isAdmin } = usePermissions();
  const isEditor = role === 'editor' || role === 'creator' || isAdmin();

  return (
    <Card>
      <p>{review.content}</p>
      <p className="text-muted-foreground">By {review.authorName}</p>

      {/* User can edit own reviews */}
      {review.authorId === currentUserId && (
        <Button variant="ghost" onClick={handleEdit}>Edit</Button>
      )}

      {/* Editor can flag inappropriate content */}
      {isEditor && (
        <Button variant="destructive" onClick={() => flagReview(review.id)}>
          Flag as Inappropriate
        </Button>
      )}

      {/* Admin can delete reviews */}
      {isAdmin() && (
        <Button variant="destructive" onClick={() => deleteReview(review.id)}>
          Delete Review
        </Button>
      )}
    </Card>
  );
}
```

---

### Scenario 3: Dynamic Tier-Based Navigation

**Use Case**: Show/hide menu items based on tier

```tsx
function AppNavigation() {
  const { tier, canAccess } = usePermissions();

  const navItems = [
    { label: 'Games', href: '/games', visible: true },
    { label: 'Collection', href: '/collection', visible: true },
    { label: 'Analytics', href: '/analytics', visible: canAccess('analytics.view'), badge: 'Pro' },
    { label: 'Agents', href: '/agents', visible: canAccess('agent.create'), badge: 'Pro' },
    { label: 'Admin', href: '/admin', visible: isAdmin(), badge: 'Admin' }
  ];

  return (
    <nav>
      {navItems.filter(item => item.visible).map(item => (
        <Link key={item.href} href={item.href}>
          {item.label}
          {item.badge && <Badge variant="secondary">{item.badge}</Badge>}
        </Link>
      ))}
    </nav>
  );
}
```

---

### Scenario 4: Bulk Operations with Permission Filtering

**Use Case**: Bulk actions available only to permitted users

```tsx
function GameLibraryBulkActions() {
  const { canAccess } = usePermissions();
  const [selectedGames, setSelectedGames] = useState<string[]>([]);

  // Available bulk actions (filtered by permissions)
  const bulkActions = [
    { label: 'Add to Collection', action: addToCollection, permission: 'collection.manage' },
    { label: 'Export', action: exportGames, permission: 'export' },
    { label: 'Delete', action: deleteGames, permission: 'quick-action.delete', destructive: true }
  ].filter(action => canAccess(action.permission));

  if (!canAccess('bulk-select')) {
    return (
      <Alert>
        <p>Bulk selection available in Pro tier</p>
        <Button onClick={() => router.push('/upgrade')}>Upgrade</Button>
      </Alert>
    );
  }

  return (
    <div>
      <p>{selectedGames.length} games selected</p>

      <DropdownMenu>
        <DropdownMenuTrigger>Bulk Actions</DropdownMenuTrigger>
        <DropdownMenuContent>
          {bulkActions.map(action => (
            <DropdownMenuItem
              key={action.label}
              onClick={() => action.action(selectedGames)}
              className={action.destructive ? 'text-red-600' : ''}
            >
              {action.label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
```

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Bypassing Permission Gates

**❌ Don't**:
```typescript
// Backdoor: Direct API call bypassing frontend gates
async function secretDeleteGame(gameId: string) {
  // Assume if we can call it, we're allowed (WRONG!)
  await fetch(`/api/games/${gameId}`, { method: 'DELETE' });
}
```

**Why**: Backend validates anyway (returns 403). Frontend bypass wastes API call.

### Anti-Pattern 2: Hardcoded Tier/Role Checks

**❌ Don't**:
```typescript
if (user.tier === 'pro' || user.tier === 'premium' || user.tier === 'enterprise') {
  // Hardcoded tier list (breaks if new tier added)
}
```

**✓ Do**:
```typescript
if (hasTier('pro')) {
  // Uses hierarchy (works for pro, enterprise, future tiers)
}
```

### Anti-Pattern 3: Permission Logic Duplication

**❌ Don't**:
```typescript
// Duplicated logic in multiple components
function ComponentA() {
  const { tier, role } = usePermissions();
  const canEdit = tier === 'pro' || role === 'editor' || role === 'admin';
  // ...
}

function ComponentB() {
  const { tier, role } = usePermissions();
  const canEdit = tier === 'pro' || role === 'editor' || role === 'admin';
  // Duplicated logic
}
```

**✓ Do**:
```typescript
// Centralized permission check
const { canAccess } = usePermissions();
const canEdit = canAccess('quick-action.edit'); // Single source of truth (PermissionRegistry)
```

---

## Summary: Quick Reference

**Permission Checks**:
- ✅ Always validate backend (not frontend-only)
- ✅ Cache API response (not permission decisions)
- ✅ Deny by default (fail secure)
- ✅ Use PermissionGate (not inline checks)
- ❌ Never trust JWT claims alone

**Tags**:
- ✅ Max 3 visible (overflow hidden)
- ✅ Use presets (not manual creation)
- ✅ Sort by priority (important first)
- ✅ Responsive variants (desktop/tablet/mobile)
- ❌ Never show all tags (cluttered)

**Tooltips**:
- ✅ Debounce positioning (100ms)
- ✅ ARIA attributes (aria-describedby)
- ✅ Keyboard navigation (Enter/Escape)
- ✅ Mobile touch support (tap to show)
- ❌ Never block scroll (passive listeners)

**Performance**:
- ✅ Memoize expensive computations
- ✅ Virtualize large grids (> 50 items)
- ✅ Lazy load images
- ✅ React.memo on components
- ❌ Never inline objects as props

**Accessibility**:
- ✅ Semantic HTML (role, aria-*)
- ✅ Keyboard navigation (Tab/Enter/Escape)
- ✅ Color contrast ≥ 4.5:1
- ✅ Focus visible indicators
- ❌ Never rely on color alone

**Testing**:
- ✅ Test boundary conditions
- ✅ Mock PermissionProvider (reusable utility)
- ✅ E2E critical paths (Free/Pro/Admin flows)
- ✅ Accessibility audit (0 axe violations)
- ❌ Never skip tests

---

## Resources

- Epic #4068: https://github.com/DegrassiAaron/meepleai-monorepo/issues/4068
- Implementation Guide: docs/02-development/epic-4068-implementation-guide.md
- API Reference: docs/03-api/permission-api-reference.md
- Component API: docs/frontend/components/epic-4068-component-api-reference.md
- Performance Guide: docs/06-performance/epic-4068-optimization-guide.md
- Security Guide: docs/07-security/epic-4068-permission-security.md
- Troubleshooting: docs/08-troubleshooting/epic-4068-common-issues.md
