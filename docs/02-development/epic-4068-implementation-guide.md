# Epic #4068: Step-by-Step Implementation Guide

**For Developers**: Complete walkthrough for implementing all 10 issues

---

## Prerequisites

**Tools Required**:
- .NET 9 SDK
- Node.js 20+ & pnpm
- PostgreSQL 16 (running via Docker)
- Git
- VS Code (recommended)

**Knowledge Required**:
- DDD & CQRS patterns
- React hooks & context
- TypeScript generics
- CSS/Tailwind responsive design
- WCAG accessibility basics

**Estimated Time**: 2-3 weeks (with parallelization)

---

## Phase 1: Permission System Foundation (Week 1, Days 1-4)

### Issue #4177: Permission Data Model & Schema

**Step 1: Update Value Objects**

```bash
# Already exists: UserTier.cs, Role.cs in Authentication/Domain/ValueObjects
# Add: Creator role, Pro/Enterprise tiers, CollectionLimits record
```

**Edit** `Authentication/Domain/ValueObjects/Role.cs`:
- Add `public static readonly Role Creator = new("creator");`
- Update ValidRoles: `{ "user", "editor", "creator", "admin", "superadmin" }`
- Add `IsCreator()` method
- Update `HasPermission()` to include Creator hierarchy

**Edit** `Authentication/Domain/ValueObjects/UserTier.cs`:
- Add `public static readonly UserTier Pro = new("pro");`
- Add `public static readonly UserTier Enterprise = new("enterprise");`
- Update ValidTiers: `{ "free", "normal", "premium", "pro", "enterprise" }`
- Add `GetLimits()` method returning `CollectionLimits(maxGames, storageMB)`
- Update `GetLevel()` to include: pro (2), enterprise (3)

**Step 2: Create Entity State Enums**

**Create** `Administration/Domain/Enums/EntityStates.cs`:
```csharp
namespace Api.BoundedContexts.Administration.Domain.Enums;

public enum UserAccountStatus { Active, Suspended, Banned }
public enum GamePublicationState { Draft, Published, Archived }
public enum CollectionVisibility { Private, Shared, Public }
public enum DocumentProcessingState { Pending, Processing, Ready, Failed }
```

**Step 3: Create Permission Value Object**

**Create** `Administration/Domain/ValueObjects/Permission.cs`:
```csharp
public sealed class Permission
{
    public string FeatureName { get; }
    public UserTier? RequiredTier { get; }
    public Role? RequiredRole { get; }
    public PermissionLogic Logic { get; }

    public static Permission CreateOr(string feature, UserTier? tier, Role? role);
    public static Permission CreateAnd(string feature, UserTier tier, Role role);
    public PermissionCheckResult Check(PermissionContext context);
}

public enum PermissionLogic { Or, And }
public record PermissionContext(UserTier Tier, Role Role, UserAccountStatus Status, string? ResourceState);
public record PermissionCheckResult(bool HasAccess, string Reason);
```

**Step 4: Create Permission Registry**

**Create** `Administration/Application/Services/PermissionRegistry.cs`:
```csharp
public sealed class PermissionRegistry
{
    private readonly Dictionary<string, Permission> _permissions;

    public PermissionRegistry()
    {
        _permissions = new Dictionary<string, Permission>
        {
            ["wishlist"] = Permission.CreateOr("wishlist", UserTier.Free, Role.User),
            ["bulk-select"] = Permission.CreateOr("bulk-select", UserTier.Pro, Role.Editor),
            // ... 10 total feature permissions
        };
    }

    public Permission? GetPermission(string featureName);
    public PermissionCheckResult CheckAccess(string feature, PermissionContext ctx);
    public IReadOnlyList<string> GetAccessibleFeatures(PermissionContext ctx);
}
```

**Step 5: Create Query Handlers**

**Create** `Administration/Application/Queries/GetUserPermissions/`:
- `GetUserPermissionsQuery.cs`: Query record
- `GetUserPermissionsHandler.cs`: Handler with MeepleAiDbContext injection

**Create** `Administration/Application/Queries/CheckPermission/`:
- `CheckPermissionQuery.cs`: Query record
- `CheckPermissionHandler.cs`: Handler using PermissionRegistry

**Step 6: API Endpoints**

**Create** `Routing/PermissionRoutes.cs`:
```csharp
public static IEndpointRouteBuilder MapPermissionEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/v1/permissions").RequireAuthorization();

    group.MapGet("/me", async (HttpContext ctx, IMediator m) => {
        var userId = ctx.User.GetUserId();
        return Results.Ok(await m.Send(new GetUserPermissionsQuery(userId)));
    });

    group.MapGet("/check", async (HttpContext ctx, IMediator m, string feature, string? state) => {
        var userId = ctx.User.GetUserId();
        return Results.Ok(await m.Send(new CheckPermissionQuery(userId, feature, state)));
    });

    return app;
}
```

**Edit** `Program.cs`:
```csharp
// Register service
builder.Services.AddSingleton<PermissionRegistry>();

// Register endpoints
v1Api.MapPermissionEndpoints();
```

**Step 7: Update User Entity**

**Edit** `Authentication/Domain/Entities/User.cs`:
```csharp
// Add property
public UserAccountStatus Status { get; private set; } = UserAccountStatus.Active;

// Add methods
public void Ban(string reason);
public void Unban();
public bool CanAuthenticate() => Status == UserAccountStatus.Active;
```

**Step 8: Database Migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddUserAccountStatus
dotnet ef database update
```

**Migration content**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "Status",
        table: "Users",
        type: "integer",
        nullable: false,
        defaultValue: 0); // Active

    migrationBuilder.CreateIndex("IX_Users_Status", "Users", "Status");
}
```

**Step 9: Test**

```bash
# Unit tests
dotnet test --filter "FullyQualifiedName~PermissionTests"

# Integration tests
dotnet test --filter "FullyQualifiedName~GetUserPermissionsTests"

# Run API
dotnet run
# Test: GET http://localhost:8080/api/v1/permissions/me (requires auth)
```

**Step 10: Commit & PR**

```bash
git add .
git commit -m "feat(admin): Permission data model & API (Issue #4177)

- UserTier: Add Pro, Enterprise tiers with GetLimits()
- Role: Add Creator role with permission hierarchy
- UserAccountStatus: Active, Suspended, Banned enum
- Permission value object: OR/AND logic, state-based checks
- PermissionRegistry: Central feature permission mapping
- GetUserPermissions & CheckPermission queries
- API endpoints: /permissions/me, /permissions/check
- User.Status property & Ban/Unban methods
- Unit tests: 95% coverage
- Migration: AddUserAccountStatus

Closes #4177"

git push -u origin feature/issue-4177-permission-model
gh pr create --base main-dev --title "Permission Data Model & Schema (#4177)"
```

**Estimate**: 3-4 days ✓

---

## Phase 2: Frontend Permission Hooks (Week 1, Days 5-7 + Week 2, Days 1-2)

### Issue #4178: Permission Hooks & Utilities

**Step 1: TypeScript Types**

**Create** `types/permissions.ts`:
```typescript
export type UserTier = 'free' | 'normal' | 'premium' | 'pro' | 'enterprise';
export type UserRole = 'user' | 'editor' | 'creator' | 'admin' | 'superadmin';
export type UserAccountStatus = 'Active' | 'Suspended' | 'Banned';

export interface UserPermissions {
  tier: UserTier;
  role: UserRole;
  status: UserAccountStatus;
  limits: { maxGames: number; storageQuotaMB: number };
  accessibleFeatures: string[];
}

export function hasMinimumTier(userTier: UserTier, required: UserTier): boolean;
export function isAdmin(role: UserRole): boolean;
```

**Step 2: API Client**

**Create** `lib/api/permissions.ts`:
```typescript
export async function getUserPermissions(): Promise<UserPermissions>;
export async function checkPermission(feature: string, state?: string): Promise<PermissionCheckResponse>;
```

**Step 3: React Context**

**Create** `contexts/PermissionContext.tsx`:
```typescript
export function PermissionProvider({ children }: { children: ReactNode }) {
  const { data, isLoading } = useQuery({
    queryKey: ['permissions', 'me'],
    queryFn: getUserPermissions,
    staleTime: 5 * 60 * 1000
  });

  const value = {
    tier: data?.tier ?? 'free',
    role: data?.role ?? 'user',
    canAccess: (feature) => data?.accessibleFeatures.includes(feature) ?? false,
    hasTier: (tier) => hasMinimumTier(data?.tier ?? 'free', tier),
    isAdmin: () => isAdmin(data?.role ?? 'user'),
    loading: isLoading
  };

  return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
}

export function usePermissions(): PermissionContextValue;
```

**Step 4: Permission Gate Components**

**Create** `components/auth/PermissionGate.tsx`:
```typescript
export function PermissionGate({ feature, fallback, children }: Props) {
  const { canAccess } = usePermissions();
  return canAccess(feature) ? <>{children}</> : <>{fallback}</>;
}

export function TierGate({ minimum, fallback, children }: Props) {
  const { hasTier } = usePermissions();
  return hasTier(minimum) ? <>{children}</> : <>{fallback}</>;
}
```

**Step 5: App Layout Integration**

**Edit** `app/layout.tsx`:
```typescript
import { PermissionProvider } from '@/contexts/PermissionContext';

export default function RootLayout({ children }) {
  return (
    <html>
      <body>
        <QueryClientProvider>
          <PermissionProvider>
            {children}
          </PermissionProvider>
        </QueryClientProvider>
      </body>
    </html>
  );
}
```

**Step 6: Test**

```bash
cd apps/web

# Unit tests
pnpm test -- PermissionContext

# Integration test
pnpm test -- PermissionGate

# Typecheck
pnpm typecheck
```

**Step 7: Commit & PR**

```bash
git commit -m "feat(auth): Permission hooks & context (Issue #4178)"
git push
gh pr create --base main-dev
```

**Estimate**: 3-4 days ✓

---

## Phase 3: Vertical Tag Component (Week 1, Days 1-4, Parallel)

### Issue #4181: Vertical Tag Component

**Step 1: Tag Types**

**Create** `types/tags.ts`:
```typescript
export interface Tag {
  id: string;
  label: string;
  icon?: LucideIcon;
  color?: string; // HSL
  bgColor?: string;
  tooltip?: string;
}

export type TagVariant = 'desktop' | 'tablet' | 'mobile';
```

**Step 2: Tag Presets**

**Create** `lib/tags/presets.ts`:
```typescript
export const GAME_TAG_PRESETS: Record<string, Omit<Tag, 'id'>> = {
  new: { label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
  sale: { label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)' },
  owned: { label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)' },
  wishlisted: { label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)' }
};

export const AGENT_TAG_PRESETS = { /* RAG, Vision, Code */ };
export const DOCUMENT_TAG_PRESETS = { /* PDF, Processing, Ready */ };

export function createTagsFromKeys(entityType: string, keys: string[]): Tag[];
```

**Step 3: TagBadge Component**

**Create** `components/ui/tags/TagBadge.tsx`:
```typescript
export function TagBadge({ tag, variant, showText }: Props) {
  const Icon = tag.icon;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn('rounded text-[10px]', displayText ? 'px-1.5 py-1 w-full' : 'w-6 h-6')}
            style={{ backgroundColor: tag.bgColor, color: tag.color }}
            role="status"
            aria-label={tag.label}
          >
            {Icon && <Icon />}
            {displayText && <span>{tag.label}</span>}
          </div>
        </TooltipTrigger>
        {tag.tooltip && <TooltipContent>{tag.tooltip}</TooltipContent>}
      </Tooltip>
    </TooltipProvider>
  );
}
```

**Step 4: TagOverflow Component**

**Create** `components/ui/tags/TagOverflow.tsx`:
```typescript
export function TagOverflow({ hiddenTags, count, variant }: Props) {
  return (
    <Tooltip>
      <TooltipTrigger>
        <div className="rounded-full bg-white/20 text-xs font-bold">+{count}</div>
      </TooltipTrigger>
      <TooltipContent>
        {hiddenTags.map(tag => (
          <div key={tag.id}>{tag.icon && <tag.icon />} {tag.label}</div>
        ))}
      </TooltipContent>
    </Tooltip>
  );
}
```

**Step 5: TagStrip Component**

**Create** `components/ui/tags/TagStrip.tsx`:
```typescript
export function TagStrip({ tags, maxVisible = 3, variant = 'desktop', position = 'left' }: Props) {
  const visibleTags = tags.slice(0, maxVisible);
  const hiddenTags = tags.slice(maxVisible);

  const stripWidth = { desktop: 'w-8', tablet: 'w-7', mobile: 'w-6' }[variant];

  return (
    <div
      className={cn(
        'absolute top-0 bottom-0 flex flex-col items-center gap-1 p-1',
        stripWidth,
        position === 'left' ? 'left-0 border-r' : 'right-0 border-l',
        'bg-gradient-to-b from-black/20 to-black/5 backdrop-blur-sm border-white/10 z-10'
      )}
      role="list"
      aria-label="Entity tags"
    >
      {visibleTags.map((tag, i) => (
        <div key={tag.id} className="animate-in fade-in" style={{ animationDelay: `${i * 50}ms` }}>
          <TagBadge tag={tag} variant={variant} />
        </div>
      ))}
      {hiddenTags.length > 0 && <TagOverflow hiddenTags={hiddenTags} count={hiddenTags.length} variant={variant} />}
    </div>
  );
}
```

**Step 6: Test**

```bash
# Unit tests
pnpm test -- TagStrip TagBadge TagOverflow

# Visual test with Storybook
pnpm storybook
# Navigate to UI/Tags/TagStrip
```

**Step 7: Commit & PR**

```bash
git commit -m "feat(ui): Vertical tag component (Issue #4181)"
git push
```

**Estimate**: 3-4 days ✓

---

## Phase 4: Tooltip Positioning (Week 1, Days 1-4, Parallel)

### Issue #4186: Tooltip Positioning System

**Step 1: Positioning Algorithm**

**Create** `lib/tooltip/positioning.ts`:
```typescript
export function calculateOptimalPosition(
  trigger: DOMRect,
  tooltip: Size,
  viewport = { width: innerWidth, height: innerHeight },
  gap = 8
): TooltipPosition {
  const space = {
    above: trigger.top,
    below: viewport.height - trigger.bottom,
    left: trigger.left,
    right: viewport.width - trigger.right
  };

  // Choose direction with most space
  let placement: 'top' | 'bottom' | 'left' | 'right';
  if (space.below >= tooltip.height + gap) placement = 'bottom';
  else if (space.above >= tooltip.height + gap) placement = 'top';
  else if (space.right >= tooltip.width + gap) placement = 'right';
  else placement = 'left';

  // Calculate position
  // ... (full implementation in code above)

  return { top, left, bottom, right, placement };
}
```

**Step 2: useSmartTooltip Hook**

**Create** `hooks/useSmartTooltip.ts`:
```typescript
export function useSmartTooltip(enabled = true) {
  const [position, setPosition] = useState<TooltipPosition | null>(null);
  const triggerRef = useRef<HTMLElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);

  const updatePosition = useCallback(() => {
    const triggerRect = triggerRef.current!.getBoundingClientRect();
    const tooltipRect = tooltipRef.current!.getBoundingClientRect();
    const optimal = calculateOptimalPosition(triggerRect, tooltipRect);
    setPosition(optimal);
  }, []);

  useEffect(() => {
    updatePosition();
    window.addEventListener('scroll', updatePosition);
    window.addEventListener('resize', updatePosition);
    return () => {
      window.removeEventListener('scroll', updatePosition);
      window.removeEventListener('resize', updatePosition);
    };
  }, [updatePosition]);

  return { position, triggerRef, tooltipRef };
}
```

**Step 3: SmartTooltip Component**

**Create** `components/ui/overlays/SmartTooltip.tsx`:
```typescript
export function SmartTooltip({ trigger, content, open }: Props) {
  const { position, triggerRef, tooltipRef } = useSmartTooltip(open);

  return (
    <>
      <div ref={triggerRef}>{trigger}</div>
      {open && position && (
        <div
          ref={tooltipRef}
          role="tooltip"
          style={{ top: position.top, left: position.left }}
        >
          {content}
        </div>
      )}
    </>
  );
}
```

**Step 4: Test**

```bash
# Unit tests
pnpm test -- positioning

# Performance test
pnpm test -- positioning.performance
# Verify: < 16ms average
```

**Step 5: Commit & PR**

```bash
git commit -m "feat(ui): Smart tooltip positioning (Issue #4186)"
```

**Estimate**: 3-4 days ✓

---

## Phase 5: Agent Metadata (Week 1, Days 3-5, Parallel)

### Issue #4184: Agent-Specific Metadata & Status Display

**Step 1: Agent Types**

**Create** `types/agent.ts`:
```typescript
export enum AgentStatus { Active, Idle, Training, Error }

export interface AgentModel {
  name: string;
  temperature?: number;
  maxTokens?: number;
}

export interface AgentMetadata {
  status: AgentStatus;
  model: AgentModel;
  invocationCount: number;
  lastExecuted?: string;
  capabilities: string[];
}

export const AGENT_STATUS_CONFIG = {
  [AgentStatus.Active]: { color: 'hsl(142 76% 36%)', icon: '●', label: 'Active' },
  // ... 4 total
};
```

**Step 2: AgentStatusBadge Component**

**Create** `components/ui/agent/AgentStatusBadge.tsx`:
```typescript
export function AgentStatusBadge({ status }: Props) {
  const config = AGENT_STATUS_CONFIG[status];

  return (
    <div role="status" aria-label={`Agent status: ${config.label}`}>
      <span style={{ color: config.color }}>{config.icon}</span>
      <span>{config.label}</span>
    </div>
  );
}
```

**Step 3: AgentModelInfo Component**

**Create** `components/ui/agent/AgentModelInfo.tsx`:
```typescript
export function AgentModelInfo({ model }: Props) {
  return (
    <Tooltip>
      <TooltipTrigger>
        <div className="flex items-center gap-1">
          <Brain className="w-3 h-3" />
          <span>{model.name}</span>
        </div>
      </TooltipTrigger>
      <TooltipContent>
        <div>Temperature: {model.temperature}</div>
        <div>Max Tokens: {model.maxTokens}</div>
      </TooltipContent>
    </Tooltip>
  );
}
```

**Step 4: AgentStatsDisplay Component**

**Create** `components/ui/agent/AgentStatsDisplay.tsx`:
```typescript
export function AgentStatsDisplay({ metadata }: Props) {
  const formatCount = (n: number) => n < 1000 ? n.toString() : `${(n / 1000).toFixed(1)}K`;

  return (
    <div className="flex gap-3 text-xs">
      <div><MessageSquare /> {formatCount(metadata.invocationCount)}</div>
      {metadata.lastExecuted && <div><Clock /> {formatRelativeTime(metadata.lastExecuted)}</div>}
    </div>
  );
}
```

**Step 5: Test**

```bash
pnpm test -- Agent
```

**Step 6: Commit & PR**

```bash
git commit -m "feat(agent): Status, model, stats display (Issue #4184)"
```

**Estimate**: 3 days ✓

---

## Phase 6: Integration (Week 2, Days 3-5)

### Issue #4179: MeepleCard Permission Integration

**Edit** `components/ui/data-display/meeple-card.tsx`:
```typescript
// Add agentMetadata prop
export interface MeepleCardProps {
  // ... existing props
  agentMetadata?: AgentMetadata; // Issue #4184
  tags?: Tag[]; // Issue #4181
  maxVisibleTags?: number;
}

// Inside component
export function MeepleCard(props: MeepleCardProps) {
  const { canAccess, isAdmin } = usePermissions();

  // Permission-aware features (Issue #4179)
  const showWishlistBtn = props.showWishlist && canAccess('wishlist');
  const enableBulkSelect = props.selectable && canAccess('bulk-select');
  const enableDrag = props.draggable && canAccess('drag-drop');

  const filteredActions = props.quickActions?.filter(action =>
    !action.adminOnly || isAdmin()
  );

  return (
    <div>
      {/* Tags (Issue #4181, #4182) */}
      {props.tags && <TagStrip tags={props.tags} maxVisible={props.maxVisibleTags} />}

      {/* Agent metadata (Issue #4184) */}
      {props.entity === 'agent' && props.agentMetadata && (
        <>
          <AgentStatusBadge status={props.agentMetadata.status} />
          <AgentModelInfo model={props.agentMetadata.model} />
          <AgentStatsDisplay metadata={props.agentMetadata} />
        </>
      )}

      {/* Permission-aware features */}
      {showWishlistBtn && <WishlistButton />}
      {enableBulkSelect && <BulkSelectCheckbox />}
      {enableDrag && <DragHandle />}
      <QuickActionsMenu actions={filteredActions} />
    </div>
  );
}
```

**Test**:
```bash
pnpm test -- meeple-card
pnpm test:e2e -- permission-integration
```

**Estimate**: 2-3 days ✓

### Issue #4182: Tag System Integration

**Already done above** in MeepleCard updates.

**Additional**: Create utility functions for tag generation:

**Create** `lib/tags/utils.ts`:
```typescript
export function sortTagsByPriority(tags: Tag[], entityType: string): Tag[] {
  const priorityOrder = {
    game: ['exclusive', 'new', 'preorder', 'sale', 'owned', 'wishlisted'],
    agent: ['rag', 'vision', 'functions', 'code'],
    document: ['failed', 'processing', 'ready']
  };

  const order = priorityOrder[entityType] ?? [];
  return [...tags].sort((a, b) => {
    const aIdx = order.indexOf(a.id);
    const bIdx = order.indexOf(b.id);
    if (aIdx === -1) return 1;
    if (bIdx === -1) return -1;
    return aIdx - bIdx;
  });
}
```

**Estimate**: 2 days ✓

### Issue #4183: Collection Limit UI

**Create** `components/dashboard/CollectionLimitIndicator.tsx`:
```typescript
export function CollectionLimitIndicator({ gameCount, storageMB }: Props) {
  const { tier } = usePermissions();
  const limits = TIER_LIMITS[tier];

  const gamePercent = (gameCount / limits.maxGames) * 100;
  const getColor = (p: number) => p >= 90 ? 'red' : p >= 75 ? 'yellow' : 'green';

  return (
    <div>
      <div>
        <span>Games: {gameCount} / {limits.maxGames}</span>
        <Progress value={gamePercent} className={getColor(gamePercent)} />
        {gamePercent > 75 && <AlertTriangle /> Approaching limit}
      </div>
      <div>
        <span>Storage: {storageMB}MB / {limits.storageMB}MB</span>
        <Progress value={(storageMB / limits.storageMB) * 100} />
      </div>
    </div>
  );
}
```

**Usage** in collection page:
```tsx
<CollectionLimitIndicator gameCount={userCollection.games.length} storageMB={userCollection.storageMB} />
```

**Estimate**: 2-3 days ✓

---

## Phase 7: Accessibility (Week 2, Days 3-5)

### Issue #4180: Tooltip Accessibility WCAG 2.1 AA

**Step 1: Keyboard Navigation**

**Create** `components/ui/overlays/AccessibleTooltip.tsx`:
```typescript
export function AccessibleTooltip({ trigger, content, id }: Props) {
  const [isOpen, setIsOpen] = useState(false);
  const tooltipId = id ?? `tooltip-${randomId()}`;

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      setIsOpen(true);
    }
    if (e.key === 'Escape') setIsOpen(false);
  };

  return (
    <>
      <div
        role="button"
        tabIndex={0}
        aria-describedby={isOpen ? tooltipId : undefined}
        aria-expanded={isOpen}
        onKeyDown={handleKeyDown}
        onMouseEnter={() => !isTouchDevice() && setIsOpen(true)}
        onMouseLeave={() => !isTouchDevice() && setIsOpen(false)}
        onClick={() => isTouchDevice() && setIsOpen(!isOpen)}
      >
        {trigger}
      </div>

      {isOpen && (
        <div id={tooltipId} role="tooltip" aria-live="polite">
          {content}
          {isTouchDevice() && <button onClick={() => setIsOpen(false)} aria-label="Close"><X /></button>}
        </div>
      )}
    </>
  );
}
```

**Step 2: Mobile Touch Support**

```typescript
const isTouchDevice = () => 'ontouchstart' in window || navigator.maxTouchPoints > 0;
```

**Step 3: ARIA Attributes**

- `aria-describedby`: Link trigger to tooltip
- `aria-expanded`: Indicate tooltip state
- `aria-live="polite"`: Announce tooltip content
- `role="tooltip"`: Semantic role

**Step 4: Accessibility Tests**

```bash
# Automated
pnpm test:a11y

# Manual
# - Test with NVDA screen reader
# - Test with keyboard only
# - Test on mobile (tap to show)
# - Test high contrast mode
```

**Estimate**: 2-3 days ✓

---

## Phase 8: Testing & Documentation (Week 3)

### Issue #4185: Integration Testing & Documentation

**Step 1: E2E Test Suite**

**Create** `tests/e2e/epic-4068/`:
- `permission-flows.spec.ts`: Free/Normal/Pro/Enterprise user flows
- `tooltip-positioning.spec.ts`: Viewport edges, mobile, keyboard
- `tag-system.spec.ts`: Overflow, responsive, entity-specific
- `collection-limits.spec.ts`: Progress bars, warnings
- `agent-metadata.spec.ts`: Status, model, stats

**Step 2: Accessibility Audit**

```bash
# Run axe-core
pnpm exec playwright test --grep @a11y

# Run Lighthouse
pnpm exec lhci autorun

# Verify WCAG 2.1 AA checklist
# docs/05-testing/frontend/epic-4068-accessibility-checklist.md
```

**Step 3: Visual Regression**

```bash
# Chromatic
pnpm exec chromatic --project-token=XXX

# Playwright screenshots
pnpm exec playwright test --update-snapshots
```

**Step 4: Documentation**

**Update**:
- `docs/frontend/components/meeple-card.md`: Add Epic #4068 features
- `docs/frontend/components/meeple-card-epic-4068.md`: New features guide
- `CHANGELOG.md`: Add Epic #4068 entry
- `README.md`: Update features list

**Create**:
- ADR-050: Permission System Architecture
- Migration guide: Old to new MeepleCard with permissions

**Step 5: Performance Benchmarks**

```bash
# Run performance tests
pnpm test:perf

# Verify:
# - Tooltip positioning: < 16ms ✓
# - Card render: < 100ms ✓
# - Permission check: < 5ms ✓
# - Grid 100 cards: < 1s ✓
```

**Step 6: Final Quality Gate**

**Checklist**:
- [ ] All 9 previous issues merged
- [ ] Backend tests: ≥90% coverage
- [ ] Frontend tests: ≥85% coverage
- [ ] E2E tests: All scenarios passing
- [ ] Accessibility: 0 axe violations, Lighthouse ≥95
- [ ] Performance: All benchmarks met
- [ ] Visual regression: Chromatic approved
- [ ] Documentation: All files updated
- [ ] No new console warnings/errors

**Estimate**: 2-3 days ✓

---

## Troubleshooting Common Issues

### Build Errors

**Problem**: "UserAccountStatus not found"
**Solution**: Add using `Api.BoundedContexts.Administration.Domain.Enums;`

**Problem**: "ApplicationDbContext not found"
**Solution**: Use `MeepleAiDbContext` (correct class name)

**Problem**: "Namespace MeepleAI.Api.* not found"
**Solution**: Use `Api.*` namespace (no "MeepleAI" prefix)

### Test Failures

**Problem**: "Tooltip not visible in test"
**Solution**: Wait for animation: `await waitFor(() => expect(tooltip).toBeVisible())`

**Problem**: "axe-core contrast violation"
**Solution**: Check tag badge colors meet 4.5:1 ratio, use contrast checker

### Runtime Issues

**Problem**: "usePermissions must be within PermissionProvider"
**Solution**: Wrap app in `<PermissionProvider>` in root layout

**Problem**: "Permission check returns stale data"
**Solution**: Reduce staleTime or invalidate query on tier change

---

## Performance Optimization Tips

### Tooltip Positioning

- Debounce scroll/resize events (100ms)
- Use RAF for smooth updates
- Cache calculations when trigger/tooltip unchanged

### Permission Checks

- Cache accessible features list
- Don't call checkPermission for every card (use cached list)
- Prefetch permissions on app mount

### Tag Rendering

- Use React.memo for TagBadge
- Virtualize large tag lists if needed
- Lazy-load tag preset icons

### Collection Limits

- Subscribe to WebSocket for real-time updates
- Optimistic updates when adding/removing games
- Batch storage quota calculations

---

## Deployment Checklist

**Before Merge**:
- [ ] All tests passing (backend + frontend + E2E)
- [ ] Build succeeds (dotnet build + pnpm build)
- [ ] No TypeScript errors
- [ ] No new console warnings
- [ ] Accessibility audit clean
- [ ] Performance benchmarks met
- [ ] Code review approved (score ≥80%)

**Database**:
- [ ] Migration script reviewed
- [ ] Backward compatible (old clients work during deployment)
- [ ] Rollback script prepared

**Frontend**:
- [ ] Feature flags for gradual rollout (optional)
- [ ] Analytics events instrumented
- [ ] Error tracking configured (Sentry)

**Monitoring**:
- [ ] Permission endpoint latency monitored
- [ ] Cache hit rate tracked
- [ ] User tier distribution dashboard

**Documentation**:
- [ ] User-facing docs updated (if public-facing changes)
- [ ] API docs updated (Scalar)
- [ ] Internal wiki updated

---

## Post-Deployment

**Week 1 After Deploy**:
- Monitor permission endpoint latency (<100ms p95)
- Check for 4xx/5xx errors (should be <1%)
- Verify tier upgrade flow working
- Collect user feedback on new features

**Week 2-4**:
- Analyze feature adoption rates
- A/B test upgrade prompts
- Refine permission mappings based on usage
- Address any accessibility reports

**Ongoing**:
- Monthly permission registry review (add/remove features)
- Quarterly tier limits review (adjust based on usage)
- Continuous accessibility monitoring

---

## Success Metrics

**Technical**:
- Backend test coverage: ≥90% ✓
- Frontend test coverage: ≥85% ✓
- Accessibility score: ≥95 ✓
- Performance: All benchmarks met ✓
- Zero high-severity bugs in first week

**Business**:
- Tier upgrade rate: Monitor conversion from Free → Normal/Pro
- Feature adoption: Track usage of tier-locked features
- Retention: Monitor churn rate per tier
- Support tickets: <5% related to permissions

**User Experience**:
- Positive feedback: >80% user satisfaction
- Accessibility complaints: 0 (WCAG compliance)
- Performance complaints: <1% (meets 60fps target)
- Confusion rate: <10% (clear permission messaging)

---

**Total Implementation Time**: 2-3 weeks (optimistic, with parallelization) to 5-7 weeks (conservative, sequential)

**Critical Path**: #4177 → #4178 → #4179 → #4185 (10-14 days minimum)

**Next Steps**: Start with Issue #4177, parallelize #4181, #4186, #4184 during Week 1.
