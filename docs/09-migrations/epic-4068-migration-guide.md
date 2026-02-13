# Epic #4068: Migration Guide

**Migrating from Legacy MeepleCard to Permission-Aware System with Tags, Tooltips, and Agent Metadata**

---

## Overview

Epic #4068 introduces breaking changes and new features:

**Breaking Changes**:
- `User.IsSuspended` replaced by `User.Status` enum
- Permission checks now required for tier-locked features
- MeepleCard API expanded (new props: tags, agentMetadata)

**New Features**:
- Permission system (tier/role/state-based)
- Vertical tag system (left edge strip)
- Smart tooltip positioning
- Agent-specific metadata display
- Collection limit indicators

**Migration Effort**: ~2-3 hours for typical app

---

## Migration Checklist

### Phase 1: Database Migration (5 minutes)

**Step 1**: Apply migration
```bash
cd apps/api/src/Api
dotnet ef migrations add Epic4068_PermissionSystem
dotnet ef database update
```

**Step 2**: Verify migration success
```bash
docker exec -it meepleai-postgres psql -U postgres -d meepleai \\
  -c "SELECT column_name, data_type FROM information_schema.columns WHERE table_name='Users' AND column_name IN ('Tier', 'Role', 'Status');"
```

**Expected Output**:
```
 column_name | data_type
-------------+-----------
 Tier        | text
 Role        | text
 Status      | integer
```

**Step 3**: Seed default data (if needed)
```sql
-- Set default tier for existing users (if NULL)
UPDATE "Users" SET "Tier" = 'free' WHERE "Tier" IS NULL;

-- Set default status for existing users
UPDATE "Users" SET "Status" = 0 WHERE "Status" IS NULL; -- 0 = Active
```

---

### Phase 2: Backend Code Updates (30 minutes)

**Update 1**: Import new namespaces

```csharp
// Add to files using permissions
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
```

**Update 2**: Replace IsSuspended with Status

```csharp
// ❌ OLD
if (user.IsSuspended)
    return Results.Forbid();

// ✓ NEW
if (user.Status != UserAccountStatus.Active)
    return Results.Forbid();

// Or more specific
if (user.Status == UserAccountStatus.Banned)
    return Results.Forbid(); // Banned users always denied

if (user.Status == UserAccountStatus.Suspended)
    return Results.Unauthorized(); // Suspended temporary
```

**Update 3**: Add permission checks to endpoints

```csharp
// ❌ OLD: No permission check
app.MapPost("/games/{id}/bulk-action", async (Guid id, IMediator m) =>
{
    return await m.Send(new BulkActionCommand(id));
});

// ✓ NEW: Check permission first
app.MapPost("/games/{id}/bulk-action", async (Guid id, HttpContext ctx, IMediator m) =>
{
    var userId = ctx.User.GetUserId();
    var permCheck = await m.Send(new CheckPermissionQuery(userId, "bulk-action"));

    if (!permCheck.HasAccess)
        return Results.Json(new { error = "Upgrade to Pro for bulk actions" }, statusCode: 403);

    return await m.Send(new BulkActionCommand(id));
});
```

**Update 4**: Add PermissionRegistry to DI

```csharp
// Program.cs
builder.Services.AddSingleton<PermissionRegistry>();
```

**Update 5**: Register permission endpoints

```csharp
// Program.cs (after existing endpoint registrations)
v1Api.MapPermissionEndpoints(); // Epic #4068
```

---

### Phase 3: Frontend Code Updates (45 minutes)

**Update 1**: Install dependencies (if new)

```bash
cd apps/web
pnpm install
# Verify lucide-react, @tanstack/react-query already installed
```

**Update 2**: Add PermissionProvider to root layout

```typescript
// app/layout.tsx
import { PermissionProvider } from '@/contexts/PermissionContext';

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <QueryClientProvider client={queryClient}>
          <PermissionProvider> {/* ← Add this */}
            {children}
          </PermissionProvider>
        </QueryClientProvider>
      </body>
    </html>
  );
}
```

**Update 3**: Update MeepleCard usage sites

```bash
# Find all MeepleCard usages
grep -r "MeepleCard" apps/web/src/app --include="*.tsx"

# For each usage site:
```

```typescript
// ❌ OLD: Unconditional features
<MeepleCard
  entity="game"
  title={game.title}
  showWishlist
  selectable
  draggable
/>

// ✓ NEW: Permission-aware
import { usePermissions } from '@/contexts/PermissionContext';

function GameCard({ game }: Props) {
  const { canAccess } = usePermissions();

  return (
    <MeepleCard
      entity="game"
      title={game.title}
      showWishlist={canAccess('wishlist')}
      selectable={canAccess('bulk-select')}
      draggable={canAccess('drag-drop')}
    />
  );
}
```

**Update 4**: Add tags to relevant cards

```typescript
// Game cards: Add status tags
<MeepleCard
  entity="game"
  tags={[
    game.isNew && { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    game.onSale && { id: 'sale', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)' },
    game.owned && { id: 'owned', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)' }
  ].filter(Boolean)}
  maxVisibleTags={3}
/>

// Or use presets
import { createTagsFromKeys } from '@/lib/tags/utils';

<MeepleCard
  entity="game"
  tags={createTagsFromKeys('game', game.tagKeys)} // ['new', 'sale', 'owned']
/>
```

**Update 5**: Add agent metadata to agent cards

```typescript
// ❌ OLD: Basic agent card
<MeepleCard
  entity="agent"
  title={agent.name}
  subtitle={agent.description}
/>

// ✓ NEW: With metadata
<MeepleCard
  entity="agent"
  title={agent.name}
  subtitle={agent.strategy}
  agentMetadata={{
    status: agent.status as AgentStatus,
    model: {
      name: agent.modelName,
      temperature: agent.temperature,
      maxTokens: agent.maxTokens
    },
    invocationCount: agent.invocationCount,
    lastExecuted: agent.lastExecuted,
    capabilities: agent.capabilities
  }}
/>
```

---

### Phase 4: Update Tests (30 minutes)

**Update 1**: Mock PermissionProvider in tests

```typescript
// ❌ OLD: Test without provider (now throws)
render(<MyComponent />);

// ✓ NEW: Wrap in provider
import { PermissionProvider } from '@/contexts/PermissionContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const testQueryClient = new QueryClient({
  defaultOptions: { queries: { retry: false } }
});

render(
  <QueryClientProvider client={testQueryClient}>
    <PermissionProvider>
      <MyComponent />
    </PermissionProvider>
  </QueryClientProvider>
);
```

**Update 2**: Mock permission API responses

```typescript
// Setup mock server
import { rest } from 'msw';
import { setupServer } from 'msw/node';

const server = setupServer(
  rest.get('/api/v1/permissions/me', (req, res, ctx) => {
    return res(ctx.json({
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop']
    }));
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

**Update 3**: Update snapshot tests

```bash
# Regenerate snapshots (UI changed with tags/metadata)
pnpm test -- --updateSnapshot

# Review changes
git diff apps/web/src/**/__snapshots__/
```

---

### Phase 5: Update Documentation (20 minutes)

**Update 1**: CHANGELOG.md

```markdown
## [Unreleased]

### Added - Epic #4068: MeepleCard Enhancements

- **Permission System**: Tier/role/state-based authorization (Issues #4177, #4178, #4179)
  - Tiers: Free, Normal, Pro, Enterprise with feature limits
  - Roles: user, editor, creator, admin, superAdmin
  - API: `GET /api/v1/permissions/me`, `GET /api/v1/permissions/check`

- **Vertical Tag System**: Left edge tag strip (Issues #4181, #4182)
  - Max 3 visible tags + overflow counter
  - Entity-specific presets (game, agent, document)
  - Responsive: 32px (desktop) → 24px (mobile icon-only)

- **Smart Tooltip Positioning**: Auto-flip, WCAG 2.1 AA (Issues #4186, #4180)
  - Viewport boundary detection
  - Mobile touch support
  - Keyboard navigation (Tab/Enter/Escape)
  - Performance: <16ms positioning (60fps)

- **Collection Limit UI**: Progress indicators (Issue #4183)
  - Game count & storage quota bars
  - Color-coded warnings (75% yellow, 90% red)

- **Agent Metadata**: Enhanced agent cards (Issue #4184)
  - Status badge (Active/Idle/Training/Error)
  - Model info tooltip (name, temperature, maxTokens)
  - Invocation stats (count, last executed, avg time)

### Changed

- `User.IsSuspended` → `User.Status` enum (Active/Suspended/Banned)
- MeepleCard: Permission-aware feature rendering
- Role: Added "creator" (between editor and admin)
- UserTier: Added "enterprise" (above pro)

### Deprecated

- Direct feature access without permission checks (will be removed in v2.0)

### Fixed

- Tooltip positioning at viewport edges
- Tag overflow on small screens
- Permission cache stale data after tier change
```

**Update 2**: Component documentation

```bash
# Update docs/frontend/components/meeple-card.md
# Add Epic #4068 sections (already created in meeple-card-epic-4068.md)
```

**Update 3**: API documentation

```bash
# Regenerate Scalar docs
dotnet run
# Navigate to http://localhost:8080/scalar/v1
# Verify /permissions endpoints documented
```

---

## Rollback Plan

**If deployment issues**:

### Step 1: Revert Frontend

```bash
git revert <commit-hash>
pnpm build
docker build -t meepleai-web:rollback .
docker tag meepleai-web:rollback meepleai-web:latest
docker compose up -d web
```

### Step 2: Revert Backend

```bash
git revert <commit-hash>
dotnet build
docker build -t meepleai-api:rollback .
docker compose up -d api
```

### Step 3: Rollback Database (CAREFUL!)

```bash
# Identify migration name
dotnet ef migrations list

# Rollback to previous migration
dotnet ef database update <previous-migration-name>

# ⚠️ WARNING: Data loss if users already assigned new tiers!
# Safer: Keep migration, revert code only
```

**Recommendation**: Keep database changes, revert application code only. Database migration is additive (adds columns, doesn't break old code).

---

## Gradual Rollout Strategy

**Week 1**: Internal testing (admins only)
```csharp
// Feature flag
if (user.Role.IsAdmin() || user.Email.EndsWith("@meepleai.com"))
{
    // Enable Epic #4068 features
}
```

**Week 2**: Beta users (10%)
```csharp
if (user.IsBetaTester || user.Id.GetHashCode() % 10 == 0)
{
    // Enable for 10% of users
}
```

**Week 3**: General availability (100%)
```csharp
// Remove feature flag, enable for all
```

---

## Breaking Change Migration

### IsSuspended → Status

**Impact**: Low (internal API, not user-facing)

**Migration**:
```csharp
// Update all usages
// Find: grep -r "IsSuspended" apps/api/src/

// Before
if (user.IsSuspended) { /* ... */ }

// After
if (user.Status != UserAccountStatus.Active) { /* ... */ }

// Or more specific
if (user.Status == UserAccountStatus.Suspended) { /* ... */ }
```

**Backward Compatibility** (temporary):
```csharp
// Keep IsSuspended property (deprecated) for migration period
[Obsolete("Use Status property instead. Will be removed in v2.0")]
public bool IsSuspended
{
    get => Status != UserAccountStatus.Active;
    private set => Status = value ? UserAccountStatus.Suspended : UserAccountStatus.Active;
}
```

**Remove deprecated property** in v2.0 (after all code updated).

---

### GameCard → MeepleCard (Epic #3820, relevant for #4068)

**Impact**: Already migrated in Epic #3820, but relevant context

**If still using GameCard**:
```typescript
// Before (deprecated)
import { GameCard } from '@/components/games/GameCard';

<GameCard
  game={game}
  variant="grid"
  onClick={handleClick}
  showRating
/>

// After
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Users, Clock } from 'lucide-react';

<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={`${game.publisher} · ${game.yearPublished}`}
  imageUrl={game.imageUrl}
  rating={game.averageRating}
  ratingMax={10}
  metadata={[
    { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
    { icon: Clock, value: `${game.playTimeMinutes}m` }
  ]}
  onClick={handleClick}
/>
```

**Automated migration** (codemod):
```bash
npx jscodeshift -t scripts/codemods/gamecard-to-meeplecard.ts apps/web/src
```

---

## Data Migration Scripts

### Script 1: Assign Default Tiers

```csharp
// Run once after migration
public class AssignDefaultTiersScript
{
    public async Task Execute(MeepleAiDbContext context)
    {
        var usersWithoutTier = await context.Users
            .Where(u => u.Tier == null)
            .ToListAsync();

        foreach (var user in usersWithoutTier)
        {
            // Business logic: Existing users get Normal tier (grandfathered)
            user.UpdateTier(UserTier.Normal, Role.SuperAdmin);
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"Assigned tiers to {usersWithoutTier.Count} users");
    }
}
```

**Run**:
```bash
# Add to seed data or run as admin command
dotnet run -- migrate-tiers
```

---

### Script 2: Migrate Existing Collections

```csharp
// Set visibility for existing collections
public class MigrateCollectionVisibilityScript
{
    public async Task Execute(MeepleAiDbContext context)
    {
        var collections = await context.Collections
            .Where(c => c.Visibility == null) // Old collections without visibility
            .ToListAsync();

        foreach (var collection in collections)
        {
            // Default: Private (safest assumption)
            collection.SetVisibility(CollectionVisibility.Private);
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"Migrated {collections.Count} collections to Private visibility");
    }
}
```

---

## Frontend Migration Patterns

### Pattern 1: Wrap Features in PermissionGate

```typescript
// ❌ OLD: Feature always visible
<BulkSelectToolbar onSelect={handleBulkSelect} />

// ✓ NEW: Conditionally visible
import { PermissionGate } from '@/components/auth/PermissionGate';
import { UpgradePrompt } from '@/components/upgrade/UpgradePrompt';

<PermissionGate feature="bulk-select" fallback={<UpgradePrompt feature="Bulk Selection" />}>
  <BulkSelectToolbar onSelect={handleBulkSelect} />
</PermissionGate>
```

### Pattern 2: Conditional Actions in Menus

```typescript
// ❌ OLD: All actions shown
const actions = [
  { label: 'View', onClick: handleView },
  { label: 'Edit', onClick: handleEdit },
  { label: 'Delete', onClick: handleDelete }
];

// ✓ NEW: Filter by permissions
const { canAccess, isAdmin } = usePermissions();

const actions = [
  { label: 'View', onClick: handleView }, // Always visible
  canAccess('quick-action.edit') && { label: 'Edit', onClick: handleEdit },
  isAdmin() && { label: 'Delete', onClick: handleDelete }
].filter(Boolean);
```

### Pattern 3: Tier-Based Feature Disclosure

```typescript
// Show upgrade prompt instead of feature
function BulkSelectSection() {
  const { canAccess, tier } = usePermissions();

  if (!canAccess('bulk-select')) {
    return (
      <Card>
        <h3>Bulk Selection</h3>
        <p>Select multiple games for batch operations.</p>
        <p className="text-muted-foreground">Available in Pro tier</p>
        <Button onClick={() => router.push('/upgrade')}>
          Upgrade to Pro
        </Button>
      </Card>
    );
  }

  return <BulkSelectToolbar />;
}
```

---

## Testing After Migration

### Smoke Tests

```bash
# 1. Backend builds
cd apps/api/src/Api
dotnet build
# Expected: Build succeeded, 0 errors

# 2. Backend runs
dotnet run
# Expected: Listening on http://localhost:8080

# 3. Permission endpoint accessible
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/permissions/me
# Expected: 200 OK, JSON response

# 4. Frontend builds
cd apps/web
pnpm build
# Expected: Build succeeded, 0 TypeScript errors

# 5. Frontend runs
pnpm dev
# Expected: Ready on http://localhost:3000

# 6. Permission context loads
# Open browser, navigate to /games
# Open DevTools console, check for errors
# Expected: No "PermissionProvider" errors
```

### Regression Tests

```bash
# Run full test suite
cd apps/api/src/Api && dotnet test
cd apps/web && pnpm test

# E2E critical paths
pnpm test:e2e -- tests/e2e/critical-paths/

# Expected: All tests pass (no regressions)
```

### User Acceptance Testing

**Test Scenarios**:
1. Free user: Login, view games, add to wishlist (✓), try bulk select (✗ shows upgrade prompt)
2. Pro user: Login, bulk select games, drag to reorder, view analytics
3. Admin: Login, access admin panel, suspend user, delete game
4. Suspended user: Login blocked, redirect to suspension notice

---

## Deprecation Timeline

**v1.5 (Current)**: Epic #4068 released, old patterns deprecated
- `User.IsSuspended` marked `[Obsolete]`
- GameCard marked `[Obsolete]` (already from Epic #3820)

**v1.6 (1 month)**: Warnings in logs for deprecated usage
```csharp
[Obsolete("Use Status property. IsSuspended will be removed in v2.0", error: false)]
public bool IsSuspended { get; }
```

**v2.0 (3 months)**: Remove deprecated code
```csharp
// IsSuspended property removed
// GameCard component removed
// Direct feature access without permissions: compile error
```

---

## Common Migration Mistakes

### Mistake 1: Forgetting to Wrap in PermissionProvider

**Symptom**: "usePermissions must be within PermissionProvider" error

**Fix**: Add `<PermissionProvider>` in app/layout.tsx (see Phase 3, Update 2)

### Mistake 2: Using Sync Permission Checks

```typescript
// ❌ WRONG: Synchronous check (no data loaded yet)
const MyComponent = () => {
  const { canAccess } = usePermissions();
  const showFeature = canAccess('feature'); // Might be false during loading!

  return showFeature ? <Feature /> : null;
};

// ✓ CORRECT: Handle loading state
const MyComponent = () => {
  const { canAccess, loading } = usePermissions();

  if (loading) return <Skeleton />;

  return canAccess('feature') ? <Feature /> : null;
};
```

### Mistake 3: Hardcoding Tier Checks

```typescript
// ❌ WRONG: Hardcode tier
const isPro = user.tier === 'pro';

// ✓ CORRECT: Use hasTier (handles hierarchy)
const { hasTier } = usePermissions();
const isPro = hasTier('pro'); // True for pro, premium, enterprise
```

### Mistake 4: Not Invalidating Cache After Tier Change

```typescript
// ❌ WRONG: Tier changed but cache not cleared
await updateUserTier(userId, 'pro');
// User still sees Free tier features (cached)

// ✓ CORRECT: Invalidate + refetch
await updateUserTier(userId, 'pro');
queryClient.invalidateQueries({ queryKey: ['permissions'] });
await queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });
```

---

## Validation Checklist

**Before Marking Migration Complete**:

- [ ] Database migration applied successfully
- [ ] All users have tier/role/status assigned (no NULLs)
- [ ] Backend builds without errors
- [ ] Backend tests pass (≥90% coverage maintained)
- [ ] Frontend builds without TypeScript errors
- [ ] Frontend tests pass (≥85% coverage maintained)
- [ ] Permission endpoints return correct data (`/me`, `/check`)
- [ ] PermissionProvider wraps app in root layout
- [ ] No runtime errors in browser console
- [ ] Free/Pro/Admin user flows tested manually
- [ ] Performance benchmarks met (tooltip <16ms, render <100ms)
- [ ] Accessibility audit clean (0 axe violations)
- [ ] Documentation updated (CHANGELOG, README, component docs)
- [ ] Rollback plan documented and tested

---

## Post-Migration Monitoring

**Week 1 After Migration**:
- Monitor permission API errors (alert if >1%)
- Check tier distribution (verify grandfathering worked)
- Review user feedback/support tickets
- Fix any migration-related bugs (priority: critical)

**Week 2-4**:
- Analyze feature adoption (which tier-locked features most used)
- Measure upgrade conversion rate (Free → Pro)
- Refine permission mappings if needed
- Optimize permission cache hit rate

---

## Migration Support

**If issues during migration**:
1. Check troubleshooting guide: `docs/08-troubleshooting/epic-4068-common-issues.md`
2. Review specs: `claudedocs/epic-4068-*.md`
3. Search closed issues: https://github.com/.../issues?q=is%3Aissue+epic%3A4068+is%3Aclosed
4. Contact team: #epic-4068-migration channel

**Provide**:
- Migration phase (database, backend, frontend, testing)
- Error message + stack trace
- Environment (dev, staging, production)
- User impact (how many users affected)
- Urgency (blocking deployment? users impacted?)

---

## Success Criteria

**Migration complete when**:
- ✅ All 10 issues deployed to production
- ✅ All tests passing (backend + frontend + E2E)
- ✅ Zero high-severity bugs reported in first 48 hours
- ✅ Permission system functioning (verified with test accounts)
- ✅ Tier upgrade flow working (verified with test purchase)
- ✅ Performance metrics met (p95 latency <100ms)
- ✅ Accessibility compliance verified (WCAG 2.1 AA)
- ✅ User feedback positive (>80% satisfaction)

**Timeline**: 2-3 weeks implementation + 1 week stabilization = **3-4 weeks total**

---

## Resources

- Epic #4068: https://github.com/DegrassiAaron/meepleai-monorepo/issues/4068
- Implementation Guide: `docs/02-development/epic-4068-implementation-guide.md`
- Troubleshooting: `docs/08-troubleshooting/epic-4068-common-issues.md`
- ADR-050: Permission System Architecture
