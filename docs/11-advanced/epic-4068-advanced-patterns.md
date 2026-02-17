# Epic #4068: Advanced Integration Patterns

**Complex scenarios, TypeScript generics, custom hooks, advanced state management**

---

## Advanced Permission Patterns

### Pattern 1: Resource-Level Permissions

**Scenario**: Check permissions per-resource (not just per-feature)

```typescript
// Hook: useResourcePermission
export function useResourcePermission(resourceType: string, resourceId: string, action: string) {
  const { tier, role, status } = usePermissions();

  return useQuery({
    queryKey: ['permission', 'resource', resourceType, resourceId, action],
    queryFn: async () => {
      // Fetch resource to get its state
      const resource = await getResource(resourceType, resourceId);

      // Check permission with resource state
      const result = await checkPermission(
        `${resourceType}.${action}`,
        resource.state
      );

      return result.hasAccess;
    },
    enabled: !!resourceId && status === 'Active'
  });
}

// Usage
function GameEditButton({ game }: Props) {
  const { data: canEdit, isLoading } = useResourcePermission('game', game.id, 'edit');

  if (isLoading) return <Skeleton />;

  if (!canEdit) {
    return (
      <Tooltip content="You cannot edit this game (draft state, not creator)">
        <button disabled>Edit</button>
      </Tooltip>
    );
  }

  return <button onClick={handleEdit}>Edit</button>;
}
```

**Use Cases**:
- Draft games (only creator can edit)
- Private collections (only owner can modify)
- Processing documents (no actions until ready)

---

### Pattern 2: Conditional Permission Requirements

**Scenario**: Different permissions based on context

```typescript
// Permission varies by game source
function useGameEditPermission(game: Game) {
  const { canAccess, role } = usePermissions();

  // User-created games: Normal tier OR creator role
  if (!game.isFromBGG) {
    return canAccess('game.edit.custom');
  }

  // BGG-sourced games: Admin only (prevent catalog pollution)
  if (game.isFromBGG && game.publicationState === 'published') {
    return role === 'admin' || role === 'superadmin';
  }

  // Draft BGG games: Creator can edit
  if (game.isFromBGG && game.publicationState === 'draft') {
    return role === 'creator' || role === 'admin';
  }

  return false;
}

// Usage
function GameCard({ game }: Props) {
  const canEdit = useGameEditPermission(game);

  return (
    <MeepleCard
      quickActions={[
        canEdit && { label: 'Edit', onClick: handleEdit }
      ].filter(Boolean)}
    />
  );
}
```

---

### Pattern 3: Cascading Permission Checks

**Scenario**: Parent permission required before child permission

```typescript
// Hook: useNestedPermissions
export function useNestedPermissions(parent: string, child: string) {
  const { canAccess } = usePermissions();

  const canAccessParent = canAccess(parent);
  const canAccessChild = canAccess(child);

  return {
    canAccessParent,
    canAccessChild,
    canAccessBoth: canAccessParent && canAccessChild,
    denialReason: !canAccessParent ? `Requires ${parent}` : !canAccessChild ? `Requires ${child}` : null
  };
}

// Usage: Collection management requires both collection access AND bulk operations
function BulkCollectionManager() {
  const { canAccessBoth, denialReason } = useNestedPermissions('collection.manage', 'bulk-select');

  if (!canAccessBoth) {
    return <Alert>{denialReason} - Upgrade to Pro for full access</Alert>;
  }

  return <BulkCollectionToolbar />;
}
```

---

### Pattern 4: Dynamic Permission Registry

**Scenario**: Add/remove features at runtime (admin configuration)

```typescript
// Client-side dynamic registry
export class DynamicPermissionRegistry {
  private features = new Map<string, FeatureConfig>();

  registerFeature(feature: string, config: FeatureConfig) {
    this.features.set(feature, config);
  }

  unregisterFeature(feature: string) {
    this.features.delete(feature);
  }

  checkAccess(feature: string, context: PermissionContext): boolean {
    const config = this.features.get(feature);
    if (!config) return false; // Unknown features denied

    return config.check(context);
  }
}

// Usage: Admin panel to manage features
function FeatureManagementPanel() {
  const [features, setFeatures] = useState<FeatureConfig[]>([]);

  const addFeature = () => {
    const newFeature: FeatureConfig = {
      name: 'custom-feature',
      requiredTier: 'pro',
      requiredRole: 'user',
      logic: 'OR'
    };

    registry.registerFeature(newFeature.name, newFeature);
    setFeatures([...features, newFeature]);
  };

  return (
    <div>
      <h2>Manage Features</h2>
      {features.map(f => (
        <FeatureRow key={f.name} feature={f} onRemove={() => registry.unregisterFeature(f.name)} />
      ))}
      <Button onClick={addFeature}>Add Feature</Button>
    </div>
  );
}
```

**Note**: Backend PermissionRegistry is static (compiled). Dynamic registry client-side only for admin previews.

---

## Advanced Tag Patterns

### Pattern 5: Computed Tags from Entity State

**Scenario**: Auto-generate tags based on entity properties

```typescript
// Hook: useComputedTags
export function useComputedTags(entity: Game | Agent | Document): Tag[] {
  return useMemo(() => {
    const tags: Tag[] = [];

    if ('releaseDate' in entity) {
      // Game entity
      const game = entity as Game;

      // New: Released in last 30 days
      if (isWithinDays(game.releaseDate, 30)) {
        tags.push(createTagFromPreset('game', 'new'));
      }

      // Sale: Discounted
      if (game.discountPercent > 0) {
        tags.push(createTagFromPreset('game', 'sale'));
      }

      // Owned: In user's collection
      if (game.isInCollection) {
        tags.push(createTagFromPreset('game', 'owned'));
      }
    } else if ('modelName' in entity) {
      // Agent entity
      const agent = entity as Agent;

      // Capabilities as tags
      agent.capabilities.forEach(capability => {
        tags.push(createTagFromPreset('agent', capability.toLowerCase()));
      });
    }

    return tags;
  }, [entity]);
}

// Usage
function GameCard({ game }: Props) {
  const tags = useComputedTags(game);

  return <MeepleCard entity="game" title={game.title} tags={tags} />;
  // Tags automatically reflect game state (new, sale, owned)
}
```

---

### Pattern 6: Interactive Tag Filtering

**Scenario**: Click tag to filter catalog

```tsx
function GameCatalog() {
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  const filteredGames = games.filter(game => {
    if (selectedTags.length === 0) return true;
    return selectedTags.every(tag => game.tagKeys?.includes(tag));
  });

  const handleTagClick = (tagId: string) => {
    setSelectedTags(prev =>
      prev.includes(tagId)
        ? prev.filter(t => t !== tagId) // Remove filter
        : [...prev, tagId] // Add filter
    );
  };

  return (
    <div>
      {/* Tag filter chips */}
      <div className="flex gap-2 mb-4">
        {['new', 'sale', 'owned'].map(tagKey => (
          <button
            key={tagKey}
            onClick={() => handleTagClick(tagKey)}
            className={cn(
              'px-3 py-1 rounded-full text-sm',
              selectedTags.includes(tagKey) ? 'bg-primary text-primary-foreground' : 'bg-muted'
            )}
          >
            {tagKey}
            {selectedTags.includes(tagKey) && <X className="ml-1 w-3 h-3" />}
          </button>
        ))}
      </div>

      {/* Filtered games */}
      <div className="grid grid-cols-4 gap-4">
        {filteredGames.map(game => (
          <MeepleCard
            key={game.id}
            entity="game"
            title={game.title}
            tags={createTagsFromKeys('game', game.tagKeys)}
          />
        ))}
      </div>
    </div>
  );
}
```

---

### Pattern 7: Tag-Based Sorting

**Scenario**: Sort games by tag priority (New first, then Sale, etc.)

```typescript
function sortGamesByTags(games: Game[]): Game[] {
  const tagPriority: Record<string, number> = {
    exclusive: 0,
    new: 1,
    preorder: 2,
    sale: 3,
    owned: 4,
    wishlisted: 5
  };

  return [...games].sort((a, b) => {
    // Get highest-priority tag for each game
    const aPriority = Math.min(...(a.tagKeys?.map(k => tagPriority[k] ?? 999) ?? [999]));
    const bPriority = Math.min(...(b.tagKeys?.map(k => tagPriority[k] ?? 999) ?? [999]));

    return aPriority - bPriority;
  });
}

// Usage
const sortedGames = sortGamesByTags(games);
// Order: Exclusive games first, then New, then Sale, etc.
```

---

## Advanced Tooltip Patterns

### Pattern 8: Context-Aware Tooltip Content

**Scenario**: Tooltip content varies by permission state

```typescript
function PermissionAwareTooltip({ feature, children }: { feature: string; children: ReactNode }) {
  const { canAccess, tier, role } = usePermissions();
  const [permissionInfo, setPermissionInfo] = useState<PermissionCheckResponse | null>(null);

  useEffect(() => {
    if (!canAccess(feature)) {
      checkPermission(feature).then(setPermissionInfo);
    }
  }, [feature, canAccess]);

  const tooltipContent = canAccess(feature)
    ? `Click to ${feature.replace('-', ' ')}`
    : (
        <div className="max-w-xs">
          <p className="font-semibold">Feature Locked</p>
          <p className="text-sm">
            {permissionInfo?.details.required.tier && `Requires ${permissionInfo.details.required.tier} tier`}
            {permissionInfo?.details.required.role && ` or ${permissionInfo.details.required.role} role`}
          </p>
          <Button size="sm" className="mt-2" onClick={() => router.push('/upgrade')}>
            Upgrade Now
          </Button>
        </div>
      );

  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent>{tooltipContent}</TooltipContent>
    </Tooltip>
  );
}

// Usage
<PermissionAwareTooltip feature="bulk-select">
  <button>Bulk Select</button>
</PermissionAwareTooltip>
// Tooltip shows "Click to bulk select" (if allowed) or upgrade prompt (if denied)
```

---

### Pattern 9: Multi-Directional Tooltip Strategy

**Scenario**: Different preferred placements for different contexts

```typescript
function useContextualTooltip(context: 'navbar' | 'sidebar' | 'grid' | 'list') {
  const preferredPlacement = {
    navbar: 'bottom' as const, // Navbar at top, tooltip below
    sidebar: 'right' as const,  // Sidebar at left, tooltip right
    grid: 'top' as const,       // Grid cards, tooltip above (less overlap)
    list: 'right' as const      // List items, tooltip right
  }[context];

  return useSmartTooltip({
    preferredPlacement,
    detectCollisions: context === 'navbar' // Navbar needs collision detection
  });
}

// Usage
function NavbarItem({ label, info }: Props) {
  const { position, triggerRef, tooltipRef } = useContextualTooltip('navbar');

  return (
    <>
      <button ref={triggerRef}>{label}</button>
      {showTooltip && <div ref={tooltipRef} style={position}>{info}</div>}
    </>
  );
}
```

---

### Pattern 10: Tooltip Performance Monitoring

**Scenario**: Track tooltip positioning performance in production

```typescript
export function useSmartTooltipWithMetrics(options: UseSmartTooltipOptions = {}) {
  const { position, triggerRef, tooltipRef, recalculate } = useSmartTooltip(options);

  const updatePositionWithMetrics = useCallback(() => {
    const start = performance.now();

    recalculate();

    const duration = performance.now() - start;

    // Log slow positioning (> 16ms)
    if (duration > 16) {
      console.warn(`Slow tooltip positioning: ${duration.toFixed(2)}ms`);

      // Send to analytics (optional)
      analytics.track('tooltip_slow_positioning', {
        duration,
        trigger: triggerRef.current?.tagName,
        tooltipSize: tooltipRef.current?.getBoundingClientRect()
      });
    }

    // Metric for monitoring
    PerformanceMetrics.tooltipPositionDuration.record(duration);
  }, [recalculate]);

  return { position, triggerRef, tooltipRef, updatePosition: updatePositionWithMetrics };
}
```

---

## Advanced Agent Metadata Patterns

### Pattern 11: Agent Status State Machine

**Scenario**: Manage agent lifecycle with state transitions

```typescript
type AgentStatusTransition =
  | { from: AgentStatus.Idle; to: AgentStatus.Active }
  | { from: AgentStatus.Active; to: AgentStatus.Idle }
  | { from: AgentStatus.Idle; to: AgentStatus.Training }
  | { from: AgentStatus.Training; to: AgentStatus.Active }
  | { from: AgentStatus.Active; to: AgentStatus.Error }
  | { from: AgentStatus.Error; to: AgentStatus.Idle };

function isValidTransition(from: AgentStatus, to: AgentStatus): boolean {
  const validTransitions: Record<AgentStatus, AgentStatus[]> = {
    [AgentStatus.Idle]: [AgentStatus.Active, AgentStatus.Training],
    [AgentStatus.Active]: [AgentStatus.Idle, AgentStatus.Error],
    [AgentStatus.Training]: [AgentStatus.Active, AgentStatus.Error],
    [AgentStatus.Error]: [AgentStatus.Idle]
  };

  return validTransitions[from]?.includes(to) ?? false;
}

// Usage: Validate status changes
function updateAgentStatus(agentId: string, newStatus: AgentStatus) {
  const agent = getAgent(agentId);

  if (!isValidTransition(agent.status, newStatus)) {
    throw new Error(`Invalid transition: ${agent.status} → ${newStatus}`);
  }

  // Update status
  agent.status = newStatus;
}
```

---

### Pattern 12: Agent Performance Trending

**Scenario**: Track agent performance over time

```typescript
interface AgentPerformanceHistory {
  timestamp: string;
  invocationCount: number;
  avgResponseTime: number;
  successRate: number;
}

function AgentPerformanceChart({ agentId }: Props) {
  const { data: history } = useQuery({
    queryKey: ['agent', agentId, 'performance-history'],
    queryFn: () => getAgentPerformanceHistory(agentId, { days: 30 })
  });

  return (
    <Card>
      <h3>Performance Trend (30 days)</h3>

      {/* Line chart: Invocations per day */}
      <Line
        data={{
          labels: history.map(h => new Date(h.timestamp).toLocaleDateString()),
          datasets: [{
            label: 'Invocations',
            data: history.map(h => h.invocationCount)
          }]
        }}
      />

      {/* Metrics summary */}
      <dl className="grid grid-cols-3 gap-4 mt-4">
        <div>
          <dt>Total Invocations</dt>
          <dd>{formatInvocationCount(history.reduce((sum, h) => sum + h.invocationCount, 0))}</dd>
        </div>
        <div>
          <dt>Avg Response Time</dt>
          <dd>{formatResponseTime(average(history.map(h => h.avgResponseTime)))}</dd>
        </div>
        <div>
          <dt>Success Rate</dt>
          <dd>{(average(history.map(h => h.successRate)) * 100).toFixed(1)}%</dd>
        </div>
      </dl>
    </Card>
  );
}
```

---

## Advanced TypeScript Patterns

### Pattern 13: Generic Permission Hook

**Scenario**: Type-safe permission hook for specific features

```typescript
// Generic hook with type inference
export function useFeaturePermission<T extends string>(
  feature: T
): {
  canAccess: boolean;
  requiredTier?: UserTier;
  requiredRole?: UserRole;
  upgradeUrl: string;
} {
  const { canAccess } = usePermissions();
  const [permInfo, setPermInfo] = useState<PermissionCheckResponse | null>(null);

  useEffect(() => {
    checkPermission(feature).then(setPermInfo);
  }, [feature]);

  return {
    canAccess: canAccess(feature),
    requiredTier: permInfo?.details.required.tier as UserTier | undefined,
    requiredRole: permInfo?.details.required.role as UserRole | undefined,
    upgradeUrl: permInfo?.details.required.tier ? `/upgrade?tier=${permInfo.details.required.tier}` : '/upgrade'
  };
}

// Usage with type inference
const bulkSelectPerm = useFeaturePermission('bulk-select');
//    ^? Type: { canAccess: boolean; requiredTier?: 'free' | 'normal' | 'pro' | 'enterprise'; ... }

if (!bulkSelectPerm.canAccess) {
  router.push(bulkSelectPerm.upgradeUrl); // Type-safe upgrade URL
}
```

---

### Pattern 14: Discriminated Union for Entity-Specific Props

**Scenario**: Type-safe MeepleCard props based on entity type

```typescript
// Discriminated union for entity-specific props
type MeepleCardEntityProps =
  | { entity: 'game'; tags?: Tag[]; agentMetadata?: never }
  | { entity: 'agent'; tags?: Tag[]; agentMetadata: AgentMetadata }
  | { entity: 'player'; tags?: never; agentMetadata?: never };

export interface MeepleCardProps extends MeepleCardEntityProps {
  title: string;
  variant?: MeepleCardVariant;
  // ... common props
}

// Usage (TypeScript enforces correct props)
<MeepleCard entity="agent" title="Expert" agentMetadata={metadata} />
// ✓ Correct: agentMetadata required for agent entity

<MeepleCard entity="game" title="Wingspan" tags={tags} />
// ✓ Correct: tags allowed, agentMetadata not allowed

<MeepleCard entity="agent" title="Expert" />
// ❌ TypeScript error: agentMetadata required for agent entity
```

---

### Pattern 15: Branded Types for Type Safety

**Scenario**: Prevent mixing feature names and resource types

```typescript
// Branded types
type FeatureName = string & { readonly __brand: 'FeatureName' };
type ResourceState = string & { readonly __brand: 'ResourceState' };

function createFeatureName(name: string): FeatureName {
  // Validate against known features
  if (!KNOWN_FEATURES.includes(name)) {
    throw new Error(`Unknown feature: ${name}`);
  }
  return name as FeatureName;
}

function createResourceState(state: string): ResourceState {
  // Validate against known states
  if (!KNOWN_STATES.includes(state)) {
    throw new Error(`Unknown state: ${state}`);
  }
  return state as ResourceState;
}

// Type-safe API
async function checkPermission(
  feature: FeatureName,
  state?: ResourceState
): Promise<PermissionCheckResponse> {
  // ...
}

// Usage
const feature = createFeatureName('bulk-select'); // Type: FeatureName
const state = createResourceState('published'); // Type: ResourceState

checkPermission(feature, state); // ✓ Type-safe

checkPermission('bulk-select', 'published'); // ❌ TypeScript error (needs branded types)
```

---

## Advanced State Management

### Pattern 16: Zustand Permission Store

**Scenario**: Global permission state with Zustand (alternative to React Context)

```typescript
// stores/permissionStore.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface PermissionStore {
  permissions: UserPermissions | null;
  loading: boolean;
  error: Error | null;

  fetchPermissions: () => Promise<void>;
  canAccess: (feature: string) => boolean;
  hasTier: (tier: UserTier) => boolean;
  invalidate: () => void;
}

export const usePermissionStore = create<PermissionStore>()(
  persist(
    (set, get) => ({
      permissions: null,
      loading: false,
      error: null,

      fetchPermissions: async () => {
        set({ loading: true, error: null });
        try {
          const permissions = await getUserPermissions();
          set({ permissions, loading: false });
        } catch (error) {
          set({ error: error as Error, loading: false });
        }
      },

      canAccess: (feature) => {
        const { permissions } = get();
        return permissions?.accessibleFeatures.includes(feature) ?? false;
      },

      hasTier: (tier) => {
        const { permissions } = get();
        if (!permissions) return false;
        return hasMinimumTier(permissions.tier, tier);
      },

      invalidate: () => {
        get().fetchPermissions(); // Refetch
      }
    }),
    {
      name: 'permission-storage',
      partialize: (state) => ({ permissions: state.permissions }) // Only persist permissions
    }
  )
);

// Usage (simpler than React Context)
function MyComponent() {
  const canBulkSelect = usePermissionStore(state => state.canAccess('bulk-select'));

  return canBulkSelect ? <BulkSelectButton /> : null;
}
```

**Why**: Zustand is lighter than Context, has built-in persistence, selective subscriptions (performance).

---

### Pattern 17: Permission-Based Routing Guards

**Scenario**: Protect routes based on permissions (Next.js proxy)

```typescript
// proxy.ts
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const PROTECTED_ROUTES: Record<string, { requiredTier?: UserTier; requiredRole?: UserRole }> = {
  '/analytics': { requiredTier: 'pro' },
  '/admin': { requiredRole: 'admin' },
  '/agents/create': { requiredTier: 'pro' },
  '/collection/bulk': { requiredTier: 'pro' }
};

export async function middleware(request: NextRequest) {
  const pathname = request.nextUrl.pathname;

  // Check if route is protected
  const protection = PROTECTED_ROUTES[pathname];
  if (!protection) return NextResponse.next(); // Unprotected route

  // Get permissions from cookie or API
  const permissions = await getServerPermissions(request);

  // Tier check
  if (protection.requiredTier && !hasMinimumTier(permissions.tier, protection.requiredTier)) {
    return NextResponse.redirect(new URL(`/upgrade?tier=${protection.requiredTier}&redirect=${pathname}`, request.url));
  }

  // Role check
  if (protection.requiredRole && !hasMinimumRole(permissions.role, protection.requiredRole)) {
    return NextResponse.redirect(new URL('/unauthorized', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/analytics/:path*', '/admin/:path*', '/agents/create', '/collection/bulk']
};
```

---

## Advanced Collection Limit Patterns

### Pattern 18: Quota-Aware File Upload

**Scenario**: Check storage quota before upload, show remaining space

```tsx
function PdfUploadForm() {
  const { limits } = usePermissions();
  const { data: collection } = useCollectionQuery();

  const remainingStorageMB = limits.storageQuotaMB - (collection?.storageMB ?? 0);
  const canUpload = (fileSizeMB: number) => fileSizeMB <= remainingStorageMB;

  const handleFileSelect = (file: File) => {
    const fileSizeMB = file.size / 1024 / 1024;

    if (!canUpload(fileSizeMB)) {
      toast.error(
        `File too large (${fileSizeMB.toFixed(1)}MB). You have ${remainingStorageMB.toFixed(1)}MB remaining.`
      );
      return;
    }

    // Proceed with upload
    uploadPdf(file);
  };

  return (
    <div>
      <input type="file" accept=".pdf" onChange={(e) => handleFileSelect(e.target.files[0])} />

      <p className="text-sm text-muted-foreground mt-2">
        Storage: {collection?.storageMB ?? 0}MB / {limits.storageQuotaMB}MB used
        ({remainingStorageMB.toFixed(1)}MB available)
      </p>

      {remainingStorageMB < 100 && (
        <Alert variant="warning">
          <AlertTriangle />
          <p>Low storage space. Upgrade to {tier === 'free' ? 'Normal' : 'Pro'} for more.</p>
        </Alert>
      )}
    </div>
  );
}
```

---

### Pattern 19: Optimistic Limit Updates

**Scenario**: Update limit indicators immediately (before API confirms)

```typescript
function useOptimisticCollectionMutation() {
  const queryClient = useQueryClient();
  const { limits } = usePermissions();

  return useMutation({
    mutationFn: addGameToCollection,

    onMutate: async (gameId) => {
      // Cancel ongoing refetches
      await queryClient.cancelQueries({ queryKey: ['collection', 'stats'] });

      // Snapshot current state
      const previousStats = queryClient.getQueryData<CollectionStats>(['collection', 'stats']);

      // Optimistically update
      queryClient.setQueryData<CollectionStats>(['collection', 'stats'], old => ({
        ...old!,
        gameCount: old!.gameCount + 1 // Increment immediately
      }));

      return { previousStats };
    },

    onError: (err, gameId, context) => {
      // Rollback on error
      queryClient.setQueryData(['collection', 'stats'], context?.previousStats);
      toast.error('Failed to add game');
    },

    onSettled: () => {
      // Refetch to sync with server
      queryClient.invalidateQueries({ queryKey: ['collection', 'stats'] });
    }
  });
}

// Usage
function AddToCollectionButton({ gameId }: Props) {
  const addMutation = useOptimisticCollectionMutation();

  return (
    <button
      onClick={() => addMutation.mutate(gameId)}
      disabled={addMutation.isPending}
    >
      {addMutation.isPending ? 'Adding...' : 'Add to Collection'}
    </button>
  );
}
// Collection count updates immediately, then confirms from server
```

---

## CSS Customization Patterns

### Pattern 20: Custom Tag Colors (Theming)

**Scenario**: Theme-specific tag colors

```typescript
// Tailwind config: Define CSS variables
// tailwind.config.ts
export default {
  theme: {
    extend: {
      colors: {
        tag: {
          new: 'var(--tag-new)',
          sale: 'var(--tag-sale)',
          owned: 'var(--tag-owned)'
        }
      }
    }
  }
};

// CSS variables by theme
// app/globals.css
:root {
  --tag-new: hsl(142 76% 36%);
  --tag-sale: hsl(0 84% 60%);
  --tag-owned: hsl(221 83% 53%);
}

.dark {
  --tag-new: hsl(142 76% 45%); /* Lighter in dark mode */
  --tag-sale: hsl(0 84% 70%);
  --tag-owned: hsl(221 83% 63%);
}

// Usage: Tags automatically themed
const GAME_TAG_PRESETS = {
  new: { label: 'New', bgColor: 'var(--tag-new)' },
  sale: { label: 'Sale', bgColor: 'var(--tag-sale)' },
  owned: { label: 'Owned', bgColor: 'var(--tag-owned)' }
};
```

---

### Pattern 21: Responsive Tag Strip Width

**Scenario**: Custom breakpoints for tag strip

```tsx
// Use Tailwind responsive classes
<TagStrip
  tags={tags}
  className="w-6 sm:w-7 md:w-8 lg:w-10"
/>
// 24px (mobile) → 28px (sm) → 32px (md) → 40px (lg)

// Or custom component with useMediaQuery
function ResponsiveTagStrip({ tags }: Props) {
  const width = useBreakpointValue({
    base: 24,
    sm: 28,
    md: 32,
    lg: 40
  });

  return <TagStrip tags={tags} width={`${width}px`} />;
}
```

---

## Integration with External Systems

### Pattern 22: Sync Permissions with Payment Provider

**Scenario**: Stripe webhook updates user tier

```typescript
// API endpoint: Stripe webhook handler
app.MapPost("/webhooks/stripe", async (HttpContext ctx, IMediator m) =>
{
    var signature = ctx.Request.Headers["Stripe-Signature"];
    var json = await ctx.Request.ReadAsStringAsync();

    // Verify webhook signature
    var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

    if (stripeEvent.Type == "checkout.session.completed")
    {
        var session = stripeEvent.Data.Object as Session;
        var userId = Guid.Parse(session.Metadata["userId"]);
        var newTier = session.Metadata["tier"]; // "pro" or "enterprise"

        // Update user tier
        await m.Send(new UpdateUserTierCommand(userId, UserTier.Parse(newTier)));

        // Invalidate permission cache (if using HybridCache)
        await cache.RemoveAsync($"permissions:{userId}");

        // Emit event for frontend (WebSocket/SSE)
        await hub.Clients.User(userId.ToString()).SendAsync("TierChanged", newTier);
    }

    return Results.Ok();
});

// Frontend: Listen for tier change
useEffect(() => {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/permissions')
    .build();

  connection.on('TierChanged', (newTier) => {
    queryClient.invalidateQueries({ queryKey: ['permissions'] });
    toast.success(`Upgraded to ${newTier}!`);
  });

  connection.start();

  return () => connection.stop();
}, []);
```

---

### Pattern 23: Analytics Integration

**Scenario**: Track permission-related user behavior

```typescript
// Track permission denials (identify friction points)
export function usePermissionAnalytics() {
  const { canAccess } = usePermissions();

  const trackDenial = useCallback((feature: string) => {
    analytics.track('permission_denied', {
      feature,
      timestamp: new Date().toISOString()
    });
  }, []);

  const canAccessWithTracking = useCallback((feature: string): boolean => {
    const hasAccess = canAccess(feature);

    if (!hasAccess) {
      trackDenial(feature);
    }

    return hasAccess;
  }, [canAccess, trackDenial]);

  return { canAccess: canAccessWithTracking };
}

// Usage
function BulkSelectButton() {
  const { canAccess } = usePermissionAnalytics();

  return canAccess('bulk-select') ? (
    <button>Bulk Select</button>
  ) : (
    <UpgradePrompt /> // Analytics tracked permission denial
  );
}

// Dashboard: Top denied features
// SELECT feature, COUNT(*) FROM analytics WHERE event='permission_denied' GROUP BY feature ORDER BY COUNT DESC LIMIT 10;
// Result: "bulk-select" most denied → prioritize in upgrade messaging
```

---

## Complex Integration Examples

### Example 1: Multi-Tenant Permission System

**Scenario**: Users belong to organizations, permissions vary by org

```typescript
interface OrgPermissions {
  orgId: string;
  orgTier: UserTier; // Organization tier (separate from user tier)
  userRole: UserRole; // User's role within this org
  accessibleFeatures: string[];
}

export function useOrgPermissions(orgId: string) {
  return useQuery({
    queryKey: ['permissions', 'org', orgId],
    queryFn: () => getOrgPermissions(orgId),
    staleTime: 5 * 60 * 1000
  });
}

// Usage: Check org-level permissions
function OrgGameCatalog({ orgId }: Props) {
  const { data: orgPerms, isLoading } = useOrgPermissions(orgId);

  if (isLoading) return <Spinner />;

  return (
    <div>
      <h1>Organization: {orgPerms.orgName}</h1>
      <Badge>{orgPerms.orgTier} Org</Badge>

      {orgPerms.accessibleFeatures.includes('org.bulk-import') && (
        <BulkImportButton />
      )}

      {/* User role within org */}
      <p>Your role: {orgPerms.userRole}</p>
    </div>
  );
}
```

---

### Example 2: Time-Based Permission Windows

**Scenario**: Features available only during specific time windows (e.g., beta access)

```typescript
interface TimeWindowPermission {
  feature: string;
  startsAt: string; // ISO timestamp
  endsAt: string;
  requiredTier?: UserTier;
}

function useTimeWindowPermissions() {
  const { canAccess, tier } = usePermissions();

  const checkTimeWindow = useCallback((window: TimeWindowPermission): boolean => {
    const now = new Date();
    const starts = new Date(window.startsAt);
    const ends = new Date(window.endsAt);

    // Outside time window
    if (now < starts || now > ends) return false;

    // Within window, check tier
    if (window.requiredTier && !hasMinimumTier(tier, window.requiredTier)) return false;

    // Check base permission
    return canAccess(window.feature);
  }, [canAccess, tier]);

  return { checkTimeWindow };
}

// Usage: Beta feature available only during specific period
function BetaFeatureButton() {
  const { checkTimeWindow } = useTimeWindowPermissions();

  const betaWindow: TimeWindowPermission = {
    feature: 'beta-ai-recommender',
    startsAt: '2026-02-01T00:00:00Z',
    endsAt: '2026-02-28T23:59:59Z',
    requiredTier: 'normal' // Beta access for Normal+ users
  };

  const canAccessBeta = checkTimeWindow(betaWindow);

  return canAccessBeta ? (
    <button>Try Beta AI Recommender</button>
  ) : (
    <div>
      <Lock />
      <p>Beta access: Feb 1-28 for Normal+ tiers</p>
    </div>
  );
}
```

---

### Example 3: Permission Inheritance in Entity Hierarchies

**Scenario**: Collections inherit permissions from parent organization

```typescript
interface EntityPermissions {
  entityId: string;
  entityType: 'org' | 'collection' | 'game';
  ownPermissions: string[]; // Permissions directly on this entity
  inheritedPermissions: string[]; // Permissions from parent entities
  effectivePermissions: string[]; // Combined (own + inherited)
}

function useEntityPermissions(entityType: string, entityId: string) {
  return useQuery({
    queryKey: ['permissions', 'entity', entityType, entityId],
    queryFn: async () => {
      const entity = await getEntity(entityType, entityId);
      const own = await getDirectPermissions(entityType, entityId);

      // Inherit from parent
      const inherited = entity.parentId
        ? await getEffectivePermissions(entity.parentType, entity.parentId)
        : [];

      return {
        entityId,
        entityType,
        ownPermissions: own,
        inheritedPermissions: inherited,
        effectivePermissions: [...new Set([...own, ...inherited])]
      };
    }
  });
}

// Usage
function CollectionSettings({ collectionId }: Props) {
  const { data: perms } = useEntityPermissions('collection', collectionId);

  return (
    <div>
      <h2>Collection Permissions</h2>

      <Section title="Direct Permissions">
        {perms.ownPermissions.map(p => <Badge key={p}>{p}</Badge>)}
      </Section>

      <Section title="Inherited from Organization">
        {perms.inheritedPermissions.map(p => <Badge key={p} variant="secondary">{p}</Badge>)}
      </Section>

      <Section title="Effective Permissions">
        {perms.effectivePermissions.map(p => <Badge key={p} variant="outline">{p}</Badge>)}
      </Section>
    </div>
  );
}
```

---

## Testing Advanced Patterns

### Pattern 24: Permission State Machine Testing

```typescript
describe('Permission State Transitions', () => {
  const transitions: Array<{
    from: { tier: UserTier; role: UserRole };
    to: { tier: UserTier; role: UserRole };
    allowed: boolean;
  }> = [
    { from: { tier: 'free', role: 'user' }, to: { tier: 'pro', role: 'user' }, allowed: true },
    { from: { tier: 'pro', role: 'user' }, to: { tier: 'free', role: 'user' }, allowed: true }, // Downgrade
    { from: { tier: 'free', role: 'user' }, to: { tier: 'free', role: 'admin' }, allowed: false }, // Only admins can promote
    { from: { tier: 'free', role: 'admin' }, to: { tier: 'free', role: 'user' }, allowed: true } // Admin can demote
  ];

  transitions.forEach(({ from, to, allowed }) => {
    test(`${from.tier}/${from.role} → ${to.tier}/${to.role} ${allowed ? 'allowed' : 'denied'}`, async () => {
      const result = await attemptPermissionChange(userId, from, to, requesterRole);
      expect(result.success).toBe(allowed);
    });
  });
});
```

---

### Pattern 25: Visual Regression Testing for Permissions

```typescript
// tests/visual/permission-states.spec.ts
import { test, expect } from '@playwright/test';

test.describe('MeepleCard Permission States', () => {
  const tiers: UserTier[] = ['free', 'normal', 'pro', 'enterprise'];

  for (const tier of tiers) {
    test(`MeepleCard visual: ${tier} tier`, async ({ page }) => {
      // Login as user with specific tier
      await loginWithTier(page, tier);

      await page.goto('/storybook/iframe.html?id=ui-meeplecard--permission-aware');

      // Screenshot for each tier
      await expect(page.locator('[data-testid="meeple-card-grid"]')).toHaveScreenshot(`meeplecard-${tier}.png`);
    });
  }
});

// Chromatic: Auto-detects visual changes across tiers
```

---

## Performance Optimization Patterns

### Pattern 26: Permission Check Batching

**Scenario**: Check multiple permissions in single API call (future enhancement)

```typescript
// Future API: POST /permissions/check/batch
async function batchCheckPermissions(features: string[]): Promise<Record<string, boolean>> {
  const response = await apiClient.post('/api/v1/permissions/check/batch', { features });
  return response.data; // { "wishlist": true, "bulk-select": false, ... }
}

// Hook: useBatchPermissions
export function useBatchPermissions(features: string[]) {
  return useQuery({
    queryKey: ['permissions', 'batch', features.join(',')],
    queryFn: () => batchCheckPermissions(features),
    staleTime: 5 * 60 * 1000
  });
}

// Usage (efficient for components checking multiple features)
function GameToolbar() {
  const { data: permissions, isLoading } = useBatchPermissions([
    'wishlist',
    'bulk-select',
    'drag-drop',
    'quick-action.edit',
    'quick-action.delete'
  ]);

  if (isLoading) return <Skeleton />;

  return (
    <div>
      {permissions.wishlist && <WishlistButton />}
      {permissions['bulk-select'] && <BulkSelectButton />}
      {permissions['drag-drop'] && <DragHandle />}
      {/* Single API call for all 5 checks (efficient!) */}
    </div>
  );
}
```

---

### Pattern 27: Memoized Permission Components

```typescript
// Expensive component that checks permissions
const PermissionAwareMeepleCard = React.memo(function PermissionAwareMeepleCard(props: MeepleCardProps) {
  const { canAccess } = usePermissions();

  const filteredActions = useMemo(() =>
    props.quickActions?.filter(action =>
      !action.adminOnly || canAccess('admin-actions')
    ),
    [props.quickActions, canAccess]
  );

  return <MeepleCard {...props} quickActions={filteredActions} />;
}, (prevProps, nextProps) => {
  // Custom comparison (only re-render if specific props change)
  return (
    prevProps.id === nextProps.id &&
    prevProps.title === nextProps.title &&
    prevProps.quickActions === nextProps.quickActions
  );
});

// Usage in large grids (prevents unnecessary re-renders)
{games.map(game => (
  <PermissionAwareMeepleCard key={game.id} {...game} />
))}
```

---

## Summary: Advanced Patterns

**Permission System**:
- Resource-level permissions (per-game, per-collection)
- Cascading checks (parent → child requirements)
- Dynamic registry (admin-configurable features)
- Multi-tenant support (org-level permissions)

**Tags**:
- Computed tags (auto-generated from entity state)
- Interactive filtering (click tag → filter catalog)
- Tag-based sorting (prioritize tagged items)
- Themed tag colors (CSS variables)

**Tooltips**:
- Context-aware content (varies by permission)
- Performance monitoring (track slow positioning)
- Multi-directional strategy (per-context preferred placement)
- Collision detection (avoid navbar/sidebar overlap)

**Agent Metadata**:
- Status state machine (validate transitions)
- Performance trending (historical data charts)
- Real-time updates (WebSocket/SSE)
- Capability-based filtering (show only RAG agents)

**Integration**:
- Payment provider sync (Stripe → tier update)
- Analytics tracking (permission denials → upgrade messaging)
- Routing guards (middleware-level protection)
- Entity hierarchy (permission inheritance)

---

**Next**: See real-world examples in production code
- `apps/web/src/app/(authenticated)/games/page.tsx`: Game catalog with permissions
- `apps/web/src/components/admin/`: Admin panel with role gates
- `apps/api/src/Api/BoundedContexts/Administration/`: Permission backend implementation
