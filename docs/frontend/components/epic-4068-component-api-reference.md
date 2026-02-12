# Epic #4068: Component API Reference

**Complete API documentation for all new components**

---

## TagStrip Component

**Path**: `components/ui/tags/TagStrip.tsx`
**Epic #4068**: Issue #4181

### Props

```typescript
interface TagStripProps {
  tags: Tag[];
  maxVisible?: number;
  variant?: 'desktop' | 'tablet' | 'mobile';
  position?: 'left' | 'right';
}
```

| Prop | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `tags` | `Tag[]` | - | ✓ | Array of tags to display |
| `maxVisible` | `number` | `3` | | Max tags visible before overflow |
| `variant` | `'desktop' \| 'tablet' \| 'mobile'` | `'desktop'` | | Responsive variant |
| `position` | `'left' \| 'right'` | `'left'` | | Strip position on card |

### Tag Interface

```typescript
interface Tag {
  id: string;                // Unique identifier
  label: string;             // Display text
  icon?: LucideIcon;         // Optional icon component
  color?: string;            // Text color (HSL format: "0 0% 100%")
  bgColor?: string;          // Background color (HSL format: "142 76% 36%")
  tooltip?: string;          // Optional tooltip text
}
```

### Behavior

**Rendering**:
- Displays up to `maxVisible` tags vertically
- Shows `+N` overflow badge if more tags exist
- Staggered fade-in animation (50ms delay per tag)

**Responsive**:
- Desktop: 32px width, full text labels
- Tablet: 28px width, abbreviated text
- Mobile: 24px width, icon-only mode

**Accessibility**:
- `role="list"` on container
- `role="listitem"` on each tag
- `role="status"` on tag badges
- `aria-label` for screen readers

### Usage Examples

**Basic**:
```tsx
import { TagStrip } from '@/components/ui/tags/TagStrip';
import { Sparkles, Tag as TagIcon } from 'lucide-react';

<TagStrip
  tags={[
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    { id: 'sale', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)' }
  ]}
  maxVisible={3}
  variant="desktop"
  position="left"
/>
```

**With Presets**:
```tsx
import { createTagsFromKeys } from '@/lib/tags/utils';

const tags = createTagsFromKeys('game', ['new', 'sale', 'owned']);
<TagStrip tags={tags} maxVisible={3} />
```

**Responsive**:
```tsx
const isMobile = useMediaQuery('(max-width: 768px)');
const isTablet = useMediaQuery('(max-width: 1024px)');

const variant = isMobile ? 'mobile' : isTablet ? 'tablet' : 'desktop';

<TagStrip tags={tags} variant={variant} />
```

**Right Edge**:
```tsx
<TagStrip tags={tags} position="right" />
// Useful for RTL layouts or alternative designs
```

### Styling

**CSS Classes** (Tailwind):
- Container: `absolute top-0 bottom-0 w-8 left-0 border-r z-10`
- Background: `bg-gradient-to-b from-black/20 to-black/5 backdrop-blur-sm`
- Gap: `gap-1` (4px between tags)

**Customization**:
```tsx
// Custom width (override variant default)
<div className="relative">
  <TagStrip tags={tags} />
  <style jsx>{`
    [aria-label="Entity tags"] {
      width: 40px; /* Custom width */
    }
  `}</style>
</div>
```

---

## TagBadge Component

**Path**: `components/ui/tags/TagBadge.tsx`

### Props

```typescript
interface TagBadgeProps {
  tag: Tag;
  variant: 'desktop' | 'tablet' | 'mobile';
  showText?: boolean;
}
```

| Prop | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `tag` | `Tag` | - | ✓ | Tag data |
| `variant` | `'desktop' \| 'tablet' \| 'mobile'` | - | ✓ | Size variant |
| `showText` | `boolean` | `true` | | Show label text |

### Behavior

**Rendering**:
- Icon + text (desktop/tablet) or icon-only (mobile)
- Tooltip on hover if `tag.tooltip` provided
- Rounded corners, padding responsive

**Accessibility**:
- `role="status"` for semantic meaning
- `aria-label={tag.label}` for screen readers

### Usage

```tsx
import { TagBadge } from '@/components/ui/tags/TagBadge';
import { Sparkles } from 'lucide-react';

const tag = {
  id: 'new',
  label: 'New',
  icon: Sparkles,
  bgColor: 'hsl(142 76% 36%)',
  color: 'hsl(0 0% 100%)',
  tooltip: 'Recently added to catalog'
};

<TagBadge tag={tag} variant="desktop" />
```

---

## TagOverflow Component

**Path**: `components/ui/tags/TagOverflow.tsx`

### Props

```typescript
interface TagOverflowProps {
  hiddenTags: Tag[];
  count: number;
  variant: 'desktop' | 'tablet' | 'mobile';
}
```

### Behavior

- Renders circular `+N` badge
- Tooltip shows list of hidden tags
- Returns `null` if count === 0

### Usage

```tsx
import { TagOverflow } from '@/components/ui/tags/TagOverflow';

const visibleTags = tags.slice(0, 3);
const hiddenTags = tags.slice(3);

<TagOverflow
  hiddenTags={hiddenTags}
  count={hiddenTags.length}
  variant="desktop"
/>
```

---

## AgentStatusBadge Component

**Path**: `components/ui/agent/AgentStatusBadge.tsx`
**Epic #4068**: Issue #4184

### Props

```typescript
interface AgentStatusBadgeProps {
  status: AgentStatus;
  showLabel?: boolean;
  showTooltip?: boolean;
  className?: string;
}
```

| Prop | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `status` | `AgentStatus` | - | ✓ | Agent status enum |
| `showLabel` | `boolean` | `true` | | Show status text |
| `showTooltip` | `boolean` | `true` | | Show description tooltip |
| `className` | `string` | - | | Additional CSS classes |

### AgentStatus Enum

```typescript
enum AgentStatus {
  Active = 'active',     // Green ● - Running
  Idle = 'idle',         // Gray ○ - Ready but not processing
  Training = 'training', // Yellow ◐ - Being trained
  Error = 'error'        // Red ✕ - Error state
}
```

### Status Configuration

| Status | Color | Icon | Label | Description |
|--------|-------|------|-------|-------------|
| Active | `hsl(142 76% 36%)` | ● | Active | Processing requests |
| Idle | `hsl(0 0% 50%)` | ○ | Idle | Ready but inactive |
| Training | `hsl(38 92% 50%)` | ◐ | Training | Being trained/fine-tuned |
| Error | `hsl(0 84% 60%)` | ✕ | Error | Encountered error |

### Usage

```tsx
import { AgentStatusBadge } from '@/components/ui/agent/AgentStatusBadge';
import { AgentStatus } from '@/types/agent';

<AgentStatusBadge status={AgentStatus.Active} />
// Renders: "● Active"

<AgentStatusBadge status={AgentStatus.Error} showLabel={false} />
// Renders: "✕" (icon only)
```

---

## AgentModelInfo Component

**Path**: `components/ui/agent/AgentModelInfo.tsx`

### Props

```typescript
interface AgentModelInfoProps {
  model: AgentModel;
  variant?: 'badge' | 'inline';
  className?: string;
}
```

### AgentModel Interface

```typescript
interface AgentModel {
  name: string;           // Model name (e.g., "GPT-4o-mini")
  temperature?: number;   // 0.0 - 2.0
  maxTokens?: number;     // Max tokens per response
  parameters?: Record<string, unknown>; // Additional params
}
```

### Usage

```tsx
<AgentModelInfo
  model={{
    name: 'GPT-4o-mini',
    temperature: 0.7,
    maxTokens: 2000
  }}
  variant="badge"
/>
// Renders: [🧠 GPT-4o-mini] with tooltip showing parameters
```

---

## AgentStatsDisplay Component

**Path**: `components/ui/agent/AgentStatsDisplay.tsx`

### Props

```typescript
interface AgentStatsDisplayProps {
  metadata: Pick<AgentMetadata, 'invocationCount' | 'lastExecuted' | 'avgResponseTime'>;
  layout?: 'horizontal' | 'vertical';
  showIcons?: boolean;
  className?: string;
}
```

### Behavior

**Formatting**:
- Invocation count: `342`, `1.2K`, `3.4M` (K/M abbreviations)
- Last executed: `"just now"`, `"2 minutes ago"`, `"1 hour ago"`
- Avg response: `45ms`, `1.2s`, `2.5m`

### Usage

```tsx
<AgentStatsDisplay
  metadata={{
    invocationCount: 1234,
    lastExecuted: '2026-02-12T10:00:00Z',
    avgResponseTime: 450
  }}
  layout="horizontal"
  showIcons
/>
// Renders: [💬 1.2K] [🕐 2 hours ago]
```

---

## PermissionGate Component

**Path**: `components/auth/PermissionGate.tsx`
**Epic #4068**: Issue #4178

### Props

```typescript
interface PermissionGateProps {
  feature: string;
  fallback?: ReactNode;
  children: ReactNode;
}
```

| Prop | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `feature` | `string` | - | ✓ | Feature name to check |
| `fallback` | `ReactNode` | `null` | | Rendered if permission denied |
| `children` | `ReactNode` | - | ✓ | Rendered if permission granted |

### Behavior

- Checks `canAccess(feature)` from `usePermissions()`
- Renders `children` if allowed, `fallback` if denied
- Subscribes to permission context (re-renders on permission change)

### Usage

```tsx
import { PermissionGate } from '@/components/auth/PermissionGate';

// Basic: Hide feature if no access
<PermissionGate feature="bulk-select">
  <BulkSelectToolbar />
</PermissionGate>

// With fallback: Show upgrade prompt
<PermissionGate
  feature="analytics.view"
  fallback={
    <Card>
      <p>Analytics available in Pro tier</p>
      <Button onClick={() => router.push('/upgrade')}>Upgrade</Button>
    </Card>
  }
>
  <AnalyticsDashboard />
</PermissionGate>

// Nested gates
<PermissionGate feature="collection.manage">
  <PermissionGate feature="advanced-filters">
    <AdvancedFilterPanel />
  </PermissionGate>
</PermissionGate>
```

---

## TierGate Component

**Path**: `components/auth/PermissionGate.tsx`

### Props

```typescript
interface TierGateProps {
  minimum: UserTier;
  fallback?: ReactNode;
  children: ReactNode;
}
```

### Behavior

- Checks `hasTier(minimum)` (hierarchical comparison)
- Renders children if user tier ≥ minimum

### Usage

```tsx
// Show feature only for Pro+ users
<TierGate minimum="pro" fallback={<UpgradePrompt />}>
  <ProFeatureDashboard />
</TierGate>

// Multiple tier levels
<div>
  <TierGate minimum="normal">
    <NormalFeature />
  </TierGate>

  <TierGate minimum="pro">
    <ProFeature />
  </TierGate>

  <TierGate minimum="enterprise">
    <EnterpriseFeature />
  </TierGate>
</div>
```

---

## CollectionLimitIndicator Component

**Path**: `components/dashboard/CollectionLimitIndicator.tsx`
**Epic #4068**: Issue #4183

### Props

```typescript
interface CollectionLimitIndicatorProps {
  gameCount: number;
  storageMB: number;
}
```

### Behavior

**Color Coding**:
- Green: < 75% of limit
- Yellow: 75-90% of limit
- Red: > 90% of limit

**Warning Icon**: Shows `<AlertTriangle>` when > 75%

**Limits**: Retrieved from `usePermissions().limits` (tier-based)

### Usage

```tsx
import { CollectionLimitIndicator } from '@/components/dashboard/CollectionLimitIndicator';

<CollectionLimitIndicator
  gameCount={userCollection.games.length}
  storageMB={userCollection.totalStorageMB}
/>
```

**Renders**:
```
Games
█████████████████████░░ 475 / 500
⚠️ Approaching limit

Storage
████░░░░░░░░░░░░░░░░ 1.2GB / 5GB
```

---

## usePermissions Hook

**Path**: `contexts/PermissionContext.tsx`
**Epic #4068**: Issue #4178

### Return Value

```typescript
interface PermissionContextValue {
  tier: UserTier;
  role: UserRole;
  status: UserAccountStatus;
  limits: CollectionLimits;
  accessibleFeatures: string[];
  canAccess: (feature: string) => boolean;
  hasFeature: (featureName: string) => boolean; // Alias for canAccess
  hasTier: (tier: UserTier) => boolean;
  hasRole: (role: UserRole) => boolean;
  isActive: () => boolean;
  isAdmin: () => boolean;
  loading: boolean;
  error: Error | null;
  refetch: () => Promise<void>;
}
```

### Methods

**canAccess(feature: string): boolean**
- Check if user can access feature
- Returns `true` if feature in `accessibleFeatures`
- Returns `false` if loading or feature not accessible

**hasTier(tier: UserTier): boolean**
- Hierarchical tier comparison
- Example: `hasTier('normal')` returns `true` for normal, pro, enterprise

**isAdmin(): boolean**
- Check if user has admin privileges
- Returns `true` for admin or superAdmin roles

### Usage

```tsx
import { usePermissions } from '@/contexts/PermissionContext';

function GameCatalog() {
  const {
    tier,
    role,
    canAccess,
    hasTier,
    isAdmin,
    loading,
    limits
  } = usePermissions();

  if (loading) return <Spinner />;

  return (
    <div>
      {/* Conditional rendering */}
      {canAccess('wishlist') && <WishlistButton />}
      {canAccess('bulk-select') && <BulkSelectToolbar />}

      {/* Tier checks */}
      {hasTier('pro') && <ProFeatures />}
      {hasTier('enterprise') && <EnterpriseFeatures />}

      {/* Role checks */}
      {isAdmin() && <AdminPanel />}

      {/* Display limits */}
      <p>You can add {limits.maxGames} games to your collection</p>

      {/* Current tier badge */}
      <Badge>{tier}</Badge>
    </div>
  );
}
```

---

## useSmartTooltip Hook

**Path**: `hooks/useSmartTooltip.ts`
**Epic #4068**: Issue #4186

### Return Value

```typescript
interface UseSmartTooltipReturn {
  position: TooltipPosition | null;
  isPositioning: boolean;
  triggerRef: RefObject<HTMLElement>;
  tooltipRef: RefObject<HTMLDivElement>;
  recalculate: () => void;
}
```

### TooltipPosition Interface

```typescript
interface TooltipPosition {
  top?: number;           // CSS top value (px)
  bottom?: number;        // CSS bottom value (px)
  left?: number;          // CSS left value (px)
  right?: number;         // CSS right value (px)
  placement: 'top' | 'bottom' | 'left' | 'right'; // Chosen direction
  arrowOffset?: number;   // Arrow position for centering
}
```

### Options

```typescript
interface UseSmartTooltipOptions {
  enabled?: boolean;                    // Enable positioning (default: true)
  debounceMs?: number;                  // Scroll/resize debounce (default: 100ms)
  gap?: number;                         // Trigger-tooltip gap (default: 8px)
  viewportPadding?: number;             // Padding from edges (default: 8px)
  preferredPlacement?: 'top' | 'bottom' | 'left' | 'right';
  detectCollisions?: boolean;           // Collision detection (default: false)
}
```

### Usage

```tsx
import { useSmartTooltip } from '@/hooks/useSmartTooltip';

function TooltipExample() {
  const [isOpen, setIsOpen] = useState(false);
  const { position, triggerRef, tooltipRef, recalculate } = useSmartTooltip({
    enabled: isOpen,
    preferredPlacement: 'bottom',
    gap: 12
  });

  return (
    <>
      <button
        ref={triggerRef}
        onMouseEnter={() => setIsOpen(true)}
        onMouseLeave={() => setIsOpen(false)}
      >
        Hover me
      </button>

      {isOpen && position && (
        <div
          ref={tooltipRef}
          style={{
            position: 'absolute',
            top: position.top,
            left: position.left
          }}
        >
          Tooltip content
        </div>
      )}
    </>
  );
}
```

**Force Recalculate** (after content changes):
```tsx
useEffect(() => {
  if (tooltipContent.length > 100) {
    recalculate(); // Tooltip grew, reposition
  }
}, [tooltipContent, recalculate]);
```

---

## PermissionProvider Component

**Path**: `contexts/PermissionContext.tsx`

### Props

```typescript
interface PermissionProviderProps {
  children: ReactNode;
}
```

### Behavior

- Fetches user permissions on mount via `GET /permissions/me`
- Caches for 5 minutes (React Query staleTime)
- Refetches on window focus
- Provides permission context to all children

### Setup

```tsx
// app/layout.tsx (root layout)
import { PermissionProvider } from '@/contexts/PermissionContext';

export default function RootLayout({ children }) {
  return (
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>
        {children}
      </PermissionProvider>
    </QueryClientProvider>
  );
}
```

**⚠️ Required**: PermissionProvider must be child of QueryClientProvider

---

## MeepleCard New Props (Epic #4068)

**Path**: `components/ui/data-display/meeple-card.tsx`

### New Props Added

```typescript
export interface MeepleCardProps {
  // ... existing props (entity, title, variant, etc.)

  // Epic #4068 - Issue #4181, #4182: Tag System
  tags?: Tag[];
  maxVisibleTags?: number;
  tagPosition?: 'left' | 'right';

  // Epic #4068 - Issue #4184: Agent Metadata
  agentMetadata?: AgentMetadata;
  showAgentStatus?: boolean;
  showAgentModel?: boolean;
  showAgentStats?: boolean;
}
```

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `tags` | `Tag[]` | `undefined` | Tags to display in vertical strip |
| `maxVisibleTags` | `number` | `3` | Max tags before overflow |
| `tagPosition` | `'left' \| 'right'` | `'left'` | Tag strip position |
| `agentMetadata` | `AgentMetadata` | `undefined` | Agent-specific metadata (for agent entity) |
| `showAgentStatus` | `boolean` | `true` | Show agent status badge (if agentMetadata provided) |
| `showAgentModel` | `boolean` | `true` | Show model info (if agentMetadata provided) |
| `showAgentStats` | `boolean` | `true` | Show invocation stats (if agentMetadata provided) |

### AgentMetadata Interface

```typescript
interface AgentMetadata {
  status: AgentStatus;
  model: AgentModel;
  invocationCount: number;
  lastExecuted?: string;        // ISO 8601 timestamp
  avgResponseTime?: number;      // Milliseconds
  capabilities: string[];        // ['RAG', 'Vision', 'Code', ...]
}
```

### Usage Examples

**Game Card with Tags**:
```tsx
<MeepleCard
  entity="game"
  title="Wingspan"
  subtitle="Stonemaier Games"
  imageUrl="/games/wingspan.jpg"
  tags={[
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    { id: 'sale', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)' }
  ]}
  maxVisibleTags={3}
/>
```

**Agent Card with Full Metadata**:
```tsx
<MeepleCard
  entity="agent"
  title="Azul Rules Expert"
  subtitle="RAG Strategy"
  agentMetadata={{
    status: AgentStatus.Active,
    model: {
      name: 'GPT-4o-mini',
      temperature: 0.7,
      maxTokens: 2000
    },
    invocationCount: 342,
    lastExecuted: '2026-02-12T10:00:00Z',
    avgResponseTime: 450,
    capabilities: ['RAG', 'Vision']
  }}
/>
```

**Renders**:
```
┌────────────────────────────┐
│█ RAG   ┌────────────────┐ │
│█       │  Agent Avatar  │ │
│█ Vision│                │ │
│█       └────────────────┘ │
│                           │
│ ● Active  Azul Rules Expert│
│           RAG Strategy    │
│           🧠 GPT-4o-mini   │
│                           │
│ 💬 342  🕐 2 hours ago    │
└────────────────────────────┘
```

**Permission-Aware Card**:
```tsx
function GameCard({ game }: Props) {
  const { canAccess } = usePermissions();

  return (
    <MeepleCard
      entity="game"
      title={game.title}
      showWishlist={canAccess('wishlist')}
      selectable={canAccess('bulk-select')}
      draggable={canAccess('drag-drop')}
      quickActions={[
        { label: 'View', onClick: handleView },
        canAccess('quick-action.edit') && { label: 'Edit', onClick: handleEdit },
        canAccess('quick-action.delete') && { label: 'Delete', onClick: handleDelete, destructive: true }
      ].filter(Boolean)}
    />
  );
}
```

---

## Utility Functions

### createTagsFromKeys()

**Path**: `lib/tags/utils.ts`

```typescript
function createTagsFromKeys(entityType: string, tagKeys: string[]): Tag[]
```

**Parameters**:
- `entityType`: `"game"` | `"agent"` | `"document"`
- `tagKeys`: Array of tag IDs (e.g., `['new', 'sale', 'owned']`)

**Returns**: Array of Tag objects with preset styles

**Example**:
```tsx
const tags = createTagsFromKeys('game', ['new', 'sale']);
// Returns:
// [
//   { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', ... },
//   { id: 'sale', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)', ... }
// ]
```

---

### sortTagsByPriority()

```typescript
function sortTagsByPriority(tags: Tag[], entityType: string): Tag[]
```

**Priority Order**:
- Game: `['exclusive', 'new', 'preorder', 'sale', 'owned', 'wishlisted']`
- Agent: `['rag', 'vision', 'functions', 'code', 'multiTurn']`
- Document: `['failed', 'processing', 'ready', 'large', 'pdf']`

**Example**:
```tsx
const unsorted = [
  { id: 'owned', ... },
  { id: 'new', ... },
  { id: 'sale', ... }
];

const sorted = sortTagsByPriority(unsorted, 'game');
// Returns: [{ id: 'new' }, { id: 'sale' }, { id: 'owned' }]
// Order: new → sale → owned (matches priority)
```

---

### formatInvocationCount()

**Path**: `lib/agent/formatters.ts`

```typescript
function formatInvocationCount(count: number): string
```

**Examples**:
- `342` → `"342"`
- `1234` → `"1.2K"`
- `3456789` → `"3.5M"`

---

### formatRelativeTime()

```typescript
function formatRelativeTime(isoTimestamp: string): string
```

**Examples**:
- 5 seconds ago → `"just now"`
- 2 minutes ago → `"2 minutes ago"`
- 1 hour ago → `"1 hour ago"`
- 3 days ago → `"3 days ago"`
- > 30 days → `"Jan 15, 2026"` (formatted date)

---

### hasMinimumTier()

```typescript
function hasMinimumTier(userTier: UserTier, requiredTier: UserTier): boolean
```

**Hierarchy**: Free (0) < Normal (1) < Pro (2) < Enterprise (3)

**Examples**:
```typescript
hasMinimumTier('pro', 'normal'); // true (pro ≥ normal)
hasMinimumTier('normal', 'pro'); // false (normal < pro)
hasMinimumTier('free', 'free'); // true (equal)
```

---

### isAdmin()

```typescript
function isAdmin(role: UserRole): boolean
```

**Returns**: `true` for `admin` or `superadmin` roles

---

## Type Definitions

### UserTier Type

```typescript
type UserTier = 'free' | 'normal' | 'premium' | 'pro' | 'enterprise';
```

**Hierarchy**: `free < normal < premium/pro < enterprise`

**Note**: `'premium'` and `'pro'` are equivalent (level 2)

---

### UserRole Type

```typescript
type UserRole = 'user' | 'editor' | 'creator' | 'admin' | 'superadmin';
```

**Hierarchy**: `user < editor < creator < admin < superadmin`

**Permissions**:
- `user`: Own resources only
- `editor`: + Moderate public content
- `creator`: + Publish verified content
- `admin`: + Full system access
- `superadmin`: + Unrestricted access

---

### CollectionLimits Type

```typescript
interface CollectionLimits {
  maxGames: number;         // Max games in user's collection
  storageQuotaMB: number;   // Max storage quota (PDF/images)
}
```

**Tier Limits**:
- Free: `{ maxGames: 50, storageQuotaMB: 100 }`
- Normal: `{ maxGames: 100, storageQuotaMB: 500 }`
- Pro: `{ maxGames: 500, storageQuotaMB: 5000 }`
- Enterprise: `{ maxGames: 999999, storageQuotaMB: 999999 }`

---

## Constants & Enums

### TIER_HIERARCHY

```typescript
const TIER_HIERARCHY: Record<UserTier, number> = {
  free: 0,
  normal: 1,
  premium: 2,
  pro: 2,
  enterprise: 3
};
```

### AGENT_STATUS_CONFIG

```typescript
const AGENT_STATUS_CONFIG: Record<AgentStatus, { color: string; icon: string; label: string }> = {
  [AgentStatus.Active]: { color: 'hsl(142 76% 36%)', icon: '●', label: 'Active' },
  [AgentStatus.Idle]: { color: 'hsl(0 0% 50%)', icon: '○', label: 'Idle' },
  [AgentStatus.Training]: { color: 'hsl(38 92% 50%)', icon: '◐', label: 'Training' },
  [AgentStatus.Error]: { color: 'hsl(0 84% 60%)', icon: '✕', label: 'Error' }
};
```

### TAG_PRESETS

```typescript
const GAME_TAG_PRESETS: Record<string, Omit<Tag, 'id'>>;
const AGENT_TAG_PRESETS: Record<string, Omit<Tag, 'id'>>;
const DOCUMENT_TAG_PRESETS: Record<string, Omit<Tag, 'id'>>;
```

**Keys**:
- Game: `new`, `sale`, `owned`, `wishlisted`, `preorder`, `exclusive`
- Agent: `rag`, `vision`, `code`, `functions`, `multiTurn`
- Document: `pdf`, `ready`, `processing`, `failed`, `large`

---

## Advanced Usage Patterns

### Pattern 1: Conditional Feature Rendering

```tsx
function FeatureCard({ feature }: Props) {
  const { canAccess, tier, loading } = usePermissions();
  const hasAccess = canAccess(feature.requiredPermission);

  if (loading) return <FeatureCardSkeleton />;

  return (
    <Card>
      <h3>{feature.name}</h3>
      <p>{feature.description}</p>

      {hasAccess ? (
        <Button onClick={feature.action}>Use Feature</Button>
      ) : (
        <div>
          <Lock className="text-muted-foreground" />
          <p>Requires {feature.requiredTier} tier</p>
          <Button variant="outline" onClick={() => router.push('/upgrade')}>
            Upgrade Now
          </Button>
        </div>
      )}
    </Card>
  );
}
```

### Pattern 2: Multi-Level Permission Gates

```tsx
function AdminSection() {
  return (
    <PermissionGate feature="admin-access" fallback={<AccessDenied />}>
      <h1>Admin Panel</h1>

      <TierGate minimum="pro" fallback={<UpgradePrompt />}>
        <AnalyticsDashboard />
      </TierGate>

      <PermissionGate feature="user-management" fallback={null}>
        <UserManagementTable />
      </PermissionGate>
    </PermissionGate>
  );
}
```

### Pattern 3: Permission-Based Routing

```tsx
// middleware.ts
export function middleware(request: NextRequest) {
  const permissions = await getServerPermissions(request);

  if (request.nextUrl.pathname.startsWith('/admin') && !permissions.role.includes('admin')) {
    return NextResponse.redirect(new URL('/unauthorized', request.url));
  }

  if (request.nextUrl.pathname.startsWith('/pro') && !permissions.tier.includes('pro')) {
    return NextResponse.redirect(new URL('/upgrade', request.url));
  }

  return NextResponse.next();
}
```

### Pattern 4: Optimistic Permission Updates

```tsx
function UpgradeFlow() {
  const { refetch } = usePermissions();
  const [upgrading, setUpgrading] = useState(false);

  const handleUpgrade = async () => {
    setUpgrading(true);

    try {
      // Call payment API
      await upgradeToPro(userId);

      // Optimistically update UI (assume success)
      queryClient.setQueryData(['permissions', 'me'], old => ({
        ...old,
        tier: 'pro'
      }));

      // Refetch to confirm
      await refetch();

      toast.success('Upgraded to Pro!');
    } catch (error) {
      // Rollback optimistic update
      await refetch();
      toast.error('Upgrade failed');
    } finally {
      setUpgrading(false);
    }
  };

  return <Button onClick={handleUpgrade} loading={upgrading}>Upgrade to Pro</Button>;
}
```

---

## Storybook Stories

**TagStrip**:
```bash
pnpm storybook
# Navigate to: UI > Tags > TagStrip
# Stories: Default, WithOverflow, Mobile, AgentTags, RightEdge
```

**AgentStatusBadge**:
```bash
# Navigate to: UI > Agent > AgentStatusBadge
# Stories: Active, Idle, Training, Error, NoLabel
```

**PermissionGate**:
```bash
# Navigate to: Auth > PermissionGate
# Stories: Allowed, Denied, WithFallback, NestedGates
```

---

## TypeScript IntelliSense

**All components have**:
- JSDoc comments for props
- TypeScript generics for type safety
- Exported types for reuse

**Example IntelliSense**:
```typescript
<TagStrip tags={/* Hover for Tag[] type definition */} />

// IntelliSense shows:
// tags: Tag[]
//   Array of tags to display in vertical strip
//
// interface Tag {
//   id: string;
//   label: string;
//   icon?: LucideIcon;
//   color?: string;
//   bgColor?: string;
//   tooltip?: string;
// }
```

---

## Browser Compatibility

**Supported**:
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile Safari (iOS 14+)
- Chrome Android

**Features Used**:
- CSS `backdrop-filter` (glassmorphism) - fallback: solid background
- `IntersectionObserver` (tooltip visibility) - fallback: always position
- `ResizeObserver` (tooltip recalc) - fallback: window.resize event

**Polyfills**: Not needed (modern browsers only)

---

## Accessibility API

**All components follow**:
- WCAG 2.1 AA compliance
- ARIA 1.2 specification
- Keyboard navigation standards

**Screen Reader Support**:
- NVDA (Windows)
- JAWS (Windows)
- VoiceOver (macOS, iOS)
- TalkBack (Android)

**Keyboard Shortcuts**:
- `Tab`: Navigate to next element
- `Enter`/`Space`: Activate button/link
- `Escape`: Close tooltip/modal
- `Arrow keys`: Navigate within menus

---

## References

- Epic #4068: https://github.com/DegrassiAaron/meepleai-monorepo/issues/4068
- Component Source: `apps/web/src/components/ui/`
- Type Definitions: `apps/web/src/types/`
- API Endpoints: `apps/api/src/Api/Routing/PermissionRoutes.cs`
- Storybook: http://localhost:6006 (after `pnpm storybook`)
