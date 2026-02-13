# Epic #4068: Troubleshooting Guide

**Common issues, symptoms, root causes, and solutions**

---

## Permission System Issues

### Issue 1: "usePermissions must be within PermissionProvider"

**Symptom**: React error when component calls `usePermissions()`

**Root Cause**: Component not wrapped in `<PermissionProvider>`

**Solution**:
```typescript
// app/layout.tsx
export default function RootLayout({ children }) {
  return (
    <QueryClientProvider>
      <PermissionProvider> {/* ← Add this wrapper */}
        {children}
      </PermissionProvider>
    </QueryClientProvider>
  );
}
```

**Verification**:
```bash
# Check React component tree
# PermissionProvider should be ancestor of all components using usePermissions
```

---

### Issue 2: Permission Checks Return Stale Data

**Symptom**: User upgraded to Pro but still sees Free tier limitations

**Root Cause**: React Query cache not invalidated after tier change

**Solution**:
```typescript
// After tier upgrade
queryClient.invalidateQueries({ queryKey: ['permissions', 'me'] });
await queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });

// Or listen to WebSocket event
useEffect(() => {
  socket.on('tier-changed', () => {
    queryClient.invalidateQueries({ queryKey: ['permissions'] });
  });
}, []);
```

**Prevention**:
```typescript
// Reduce staleTime if freshness critical
useQuery({
  queryKey: ['permissions', 'me'],
  staleTime: 1 * 60 * 1000 // 1 minute instead of 5
});
```

---

### Issue 3: 403 Forbidden on Permission Endpoint

**Symptom**: `GET /api/v1/permissions/me` returns 403

**Root Cause**: User not authenticated or token expired

**Solution**:
```bash
# Check token validity
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/auth/verify

# If expired, refresh token
POST /api/v1/auth/refresh
{ "refreshToken": "..." }

# Update token in client
```

**Debugging**:
```csharp
// Add logging to endpoint
app.MapGet("/permissions/me", async (HttpContext ctx, IMediator m) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? false)
        Log.Warning("Unauthenticated permission request from IP: {IP}", ctx.Connection.RemoteIpAddress);

    var userId = ctx.User.GetUserId(); // May throw if user ID claim missing
    return await m.Send(new GetUserPermissionsQuery(userId));
});
```

---

### Issue 4: PermissionRegistry Not Found

**Symptom**: DI error on startup: "Unable to resolve service for type 'PermissionRegistry'"

**Root Cause**: Service not registered in DI container

**Solution**:
```csharp
// Program.cs
builder.Services.AddSingleton<Api.BoundedContexts.Administration.Application.Services.PermissionRegistry>();
```

**Verification**:
```bash
dotnet run
# Check startup logs for "PermissionRegistry registered"
```

---

## Tag System Issues

### Issue 5: Tags Not Visible on Card

**Symptom**: Tags passed to MeepleCard but not rendered

**Root Cause 1**: Z-index conflict (card content covers tag strip)

**Solution**:
```typescript
// Verify TagStrip has z-10 or higher
<div className="... z-10">
  <TagStrip />
</div>
```

**Root Cause 2**: Tags array empty or undefined

**Debugging**:
```typescript
console.log('Tags:', props.tags); // Should be array with objects
console.log('Tags length:', props.tags?.length); // Should be > 0
```

**Solution**:
```typescript
// Validate tags before passing
{tags && tags.length > 0 && <TagStrip tags={tags} />}
```

---

### Issue 6: Tag Overflow Counter Shows Wrong Number

**Symptom**: +2 badge shows but only 1 tag hidden

**Root Cause**: maxVisible not applied correctly

**Solution**:
```typescript
// TagStrip.tsx
const visibleTags = tags.slice(0, maxVisible); // ✓ Correct
const hiddenTags = tags.slice(maxVisible); // ✓ Correct
const hiddenCount = hiddenTags.length; // ✓ Correct

// ❌ WRONG:
const hiddenCount = tags.length - maxVisible; // Can be negative!
```

**Verification**:
```typescript
// Unit test
expect(hiddenTags.length).toBe(Math.max(0, tags.length - maxVisible));
```

---

### Issue 7: Tag Icons Not Showing

**Symptom**: Tag badge shows label but no icon

**Root Cause**: Icon not imported or passed incorrectly

**Solution**:
```typescript
// ✓ Correct import
import { Sparkles } from 'lucide-react';

// ✓ Pass icon component (not string)
const tag = { id: 'new', label: 'New', icon: Sparkles };

// ❌ WRONG: Icon as string
const tag = { id: 'new', label: 'New', icon: 'Sparkles' };
```

**Debugging**:
```typescript
console.log('Icon type:', typeof tag.icon); // Should be 'function'
console.log('Icon:', tag.icon); // Should be React component function
```

---

## Tooltip Positioning Issues

### Issue 8: Tooltip Positioned Incorrectly

**Symptom**: Tooltip appears in wrong location or off-screen

**Root Cause**: DOMRect read before element fully rendered

**Solution**:
```typescript
// Wait for next frame before reading dimensions
useEffect(() => {
  requestAnimationFrame(() => {
    updatePosition();
  });
}, []);
```

**Root Cause 2**: Scroll offset not accounted for

**Solution**:
```typescript
// getBoundingClientRect() already accounts for scroll
// DON'T add window.scrollY manually

const triggerRect = trigger.getBoundingClientRect(); // ✓ Includes scroll offset

// ❌ WRONG:
const triggerRect = trigger.getBoundingClientRect();
const adjustedTop = triggerRect.top + window.scrollY; // Double-counting!
```

---

### Issue 9: Tooltip Doesn't Update on Scroll

**Symptom**: Tooltip stays in old position when scrolling

**Root Cause**: Event listener not attached or removed too early

**Solution**:
```typescript
useEffect(() => {
  const handler = () => updatePosition();
  window.addEventListener('scroll', handler, { passive: true });

  return () => {
    window.removeEventListener('scroll', handler); // ✓ Cleanup
  };
}, []); // ✓ Empty deps (attach once)

// ❌ WRONG: Missing cleanup
useEffect(() => {
  window.addEventListener('scroll', () => updatePosition());
  // No return = listener never removed = memory leak
}, []);
```

---

### Issue 10: Tooltip Performance Lag (> 16ms)

**Symptom**: Janky scrolling, dropped frames

**Root Cause**: No debouncing, too many calculations

**Solution**:
```typescript
// Add debouncing
const debouncedUpdate = useCallback(() => {
  if (timeoutRef.current) clearTimeout(timeoutRef.current);
  timeoutRef.current = setTimeout(updatePosition, 100);
}, [updatePosition]);

window.addEventListener('scroll', debouncedUpdate); // Not updatePosition directly
```

**Profiling**:
```typescript
console.time('position-calc');
calculateOptimalPosition(trigger, tooltip, viewport);
console.timeEnd('position-calc');
// Expected: < 16ms
```

---

## Agent Metadata Issues

### Issue 11: Agent Status Not Updating

**Symptom**: Agent shows "Idle" but actually Active

**Root Cause**: Stale data from API, no real-time updates

**Solution**:
```typescript
// Option 1: Poll more frequently
useQuery({
  queryKey: ['agent', agentId],
  queryFn: getAgent,
  refetchInterval: 30000 // Poll every 30 seconds
});

// Option 2: WebSocket updates (better)
useEffect(() => {
  socket.on(`agent:${agentId}:status`, (newStatus) => {
    queryClient.setQueryData(['agent', agentId], old => ({ ...old, status: newStatus }));
  });
}, [agentId]);
```

---

### Issue 12: Invocation Count Format Wrong

**Symptom**: Shows "1200" instead of "1.2K"

**Root Cause**: Formatter not applied

**Solution**:
```typescript
// Check formatter usage
import { formatInvocationCount } from '@/lib/agent/formatters';

// ✓ Correct
<span>{formatInvocationCount(metadata.invocationCount)}</span>

// ❌ WRONG: Raw number
<span>{metadata.invocationCount}</span>
```

---

## Collection Limits Issues

### Issue 13: Progress Bar Color Not Changing

**Symptom**: Bar stays green even at 95%

**Root Cause**: Color threshold logic error

**Solution**:
```typescript
// ✓ Correct: >= for thresholds
const getColor = (percent: number) => {
  if (percent >= 90) return 'bg-red-500';
  if (percent >= 75) return 'bg-yellow-500';
  return 'bg-green-500';
};

// ❌ WRONG: > (misses 90 exactly)
if (percent > 90) return 'bg-red-500';
```

**Test**:
```typescript
expect(getColor(90)).toBe('bg-red-500');
expect(getColor(89.9)).toBe('bg-yellow-500');
expect(getColor(75)).toBe('bg-yellow-500');
expect(getColor(74.9)).toBe('bg-green-500');
```

---

### Issue 14: Storage Quota Not Updating

**Symptom**: Quota shows old value after PDF upload

**Root Cause**: Query not refetched after mutation

**Solution**:
```typescript
// In upload mutation
const uploadMutation = useMutation({
  mutationFn: uploadPdf,
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['collection', 'stats'] });
  }
});
```

---

## Build & Compilation Issues

### Issue 15: "UserAccountStatus not found"

**Symptom**: C# compilation error

**Root Cause**: Missing using statement

**Solution**:
```csharp
using Api.BoundedContexts.Administration.Domain.Enums;
```

---

### Issue 16: "MeepleAiDbContext not found"

**Symptom**: C# compilation error in query handler

**Root Cause**: Wrong class name (ApplicationDbContext doesn't exist)

**Solution**:
```csharp
// ✓ Correct
using Api.Infrastructure;

private readonly MeepleAiDbContext _context;

// ❌ WRONG
private readonly ApplicationDbContext _context;
```

---

### Issue 17: TypeScript Error "Property 'tags' does not exist"

**Symptom**: TS error on MeepleCard props

**Root Cause**: Props interface not updated

**Solution**:
```typescript
// meeple-card.tsx
export interface MeepleCardProps {
  // ... existing props
  tags?: Tag[]; // ← Add this
  maxVisibleTags?: number;
  agentMetadata?: AgentMetadata;
}
```

---

## Runtime Errors

### Issue 18: "Cannot read property 'includes' of undefined"

**Symptom**: Runtime error in canAccess()

**Root Cause**: accessibleFeatures undefined (API failed or loading)

**Solution**:
```typescript
// ✓ Defensive
const canAccess = (feature: string) =>
  data?.accessibleFeatures?.includes(feature) ?? false;

// ❌ UNSAFE
const canAccess = (feature: string) =>
  data.accessibleFeatures.includes(feature);
```

**Verification**:
```typescript
// Test with undefined data
const ctx = { data: undefined, canAccess };
expect(ctx.canAccess('wishlist')).toBe(false); // Should not throw
```

---

### Issue 19: "Maximum update depth exceeded"

**Symptom**: React error with usePermissions

**Root Cause**: Infinite re-render loop (state update triggers effect that updates state)

**Solution**:
```typescript
// ❌ WRONG: Effect updates state, deps include state
const [permissions, setPermissions] = useState();

useEffect(() => {
  fetchPermissions().then(setPermissions);
}, [permissions]); // ← Creates infinite loop

// ✓ Correct: Use React Query (handles this)
const { data } = useQuery({
  queryKey: ['permissions', 'me'],
  queryFn: getUserPermissions
});
```

---

## Test Failures

### Issue 20: "Tooltip not visible" Test Failure

**Symptom**: `expect(tooltip).toBeVisible()` fails

**Root Cause**: Animation delay or async rendering

**Solution**:
```typescript
// ✓ Correct: Wait for visibility
import { waitFor } from '@testing-library/react';

await waitFor(() => {
  expect(screen.getByRole('tooltip')).toBeVisible();
});

// ❌ WRONG: No wait
expect(screen.getByRole('tooltip')).toBeVisible(); // Fails if animation pending
```

---

### Issue 21: "axe-core: color-contrast violation"

**Symptom**: Accessibility test fails on tag badges

**Root Cause**: Insufficient contrast ratio (<4.5:1)

**Solution**:
```typescript
// Check contrast with online tool: https://webaim.org/resources/contrastchecker/

// ❌ BAD: Light text on light background (2.3:1)
bgColor: 'hsl(142 76% 76%)', // Light green
color: 'hsl(0 0% 100%)'      // White

// ✓ GOOD: Light text on dark background (7.8:1)
bgColor: 'hsl(142 76% 36%)', // Dark green
color: 'hsl(0 0% 100%)'      // White
```

**Verification**:
```bash
pnpm exec playwright test --grep @a11y
# Should pass with 0 violations
```

---

## Performance Issues

### Issue 22: Tooltip Positioning Slow (> 16ms)

**Symptom**: Laggy tooltip, choppy animations

**Root Cause**: Too many getBoundingClientRect() calls

**Solution**:
```typescript
// ✓ Optimize: Read DOM once per frame
const rafId = requestAnimationFrame(() => {
  const triggerRect = triggerRef.current!.getBoundingClientRect();
  const tooltipRect = tooltipRef.current!.getBoundingClientRect();
  // Calculate (no more DOM reads)
  const position = calculateOptimalPosition(triggerRect, tooltipRect);
  setPosition(position);
});

// ❌ SLOW: Multiple getBoundingClientRect in loop
for (const direction of ['top', 'bottom', 'left', 'right']) {
  const rect = triggerRef.current!.getBoundingClientRect(); // Called 4 times!
}
```

**Profiling**:
```javascript
// Chrome DevTools > Performance
// Record > Hover tooltip > Stop
// Check "Recalculate Style" and "Layout" times
// Target: < 16ms total
```

---

### Issue 23: Large Grid Laggy (> 1s render)

**Symptom**: Grid with 100+ cards slow to render

**Root Cause**: All cards rendered eagerly, no virtualization

**Solution**:
```bash
pnpm add react-window
```

```typescript
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
```

**Alternative**: Server-side pagination
```typescript
const { data } = useQuery({
  queryKey: ['games', page],
  queryFn: () => getGames({ page, limit: 50 })
});
```

---

## Database Issues

### Issue 24: Migration Fails "Column Already Exists"

**Symptom**: `dotnet ef database update` fails with duplicate column error

**Root Cause**: Migration applied manually or column added outside migration

**Solution**:
```bash
# Check migration history
dotnet ef migrations list

# If migration already applied
dotnet ef migrations remove

# If column exists but no migration
# Create empty migration, modify Down() to remove column
dotnet ef migrations add FixUserAccountStatus

# Edit migration:
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Check if column exists before adding
    migrationBuilder.Sql(@"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                          WHERE table_name='Users' AND column_name='Status') THEN
                ALTER TABLE ""Users"" ADD COLUMN ""Status"" integer NOT NULL DEFAULT 0;
            END IF;
        END$$;
    ");
}
```

---

### Issue 25: "User.Status throws NullReferenceException"

**Symptom**: Runtime error accessing user.Status

**Root Cause**: Migration not applied, column doesn't exist

**Solution**:
```bash
# Apply migration
cd apps/api/src/Api
dotnet ef database update

# Verify column exists
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c "\\d \"Users\""
# Should show "Status | integer | not null | default 0"
```

---

## API Issues

### Issue 26: CORS Error on /permissions/me

**Symptom**: Browser blocks request: "CORS policy: No 'Access-Control-Allow-Origin'"

**Root Cause**: Frontend origin not whitelisted

**Solution**:
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Add frontend origin
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

app.UseCors();
```

---

### Issue 27: "Feature Not Found" for Valid Feature

**Symptom**: Permission check returns "Feature 'wishlist' not found"

**Root Cause**: Typo in feature name or not in registry

**Solution**:
```csharp
// PermissionRegistry.cs - verify feature name exactly matches
["wishlist"] = Permission.CreateOr("wishlist", ...); // ✓ Matches

// ❌ WRONG: Mismatch
["wishList"] = Permission.CreateOr("wishlist", ...); // Key ≠ featureName
```

**Debugging**:
```csharp
// Add logging
public Permission? GetPermission(string featureName)
{
    if (!_permissions.ContainsKey(featureName))
        Log.Warning("Feature not in registry: {Feature}. Available: {Available}",
            featureName, string.Join(", ", _permissions.Keys));

    return _permissions.GetValueOrDefault(featureName);
}
```

---

## Frontend Build Issues

### Issue 28: "Module not found: @/types/permissions"

**Symptom**: Next.js build fails

**Root Cause**: TypeScript path alias not configured

**Solution**:
```json
// tsconfig.json
{
  "compilerOptions": {
    "paths": {
      "@/*": ["./src/*"]
    }
  }
}
```

**Verification**:
```bash
pnpm typecheck
# Should resolve @/types/* imports
```

---

### Issue 29: "Type 'Tag' is not assignable to type 'Tag'"

**Symptom**: TypeScript error with identical types

**Root Cause**: Duplicate type definitions (e.g., Tag defined in two files)

**Solution**:
```bash
# Find duplicate definitions
grep -r "export interface Tag" apps/web/src/

# Remove duplicates, keep canonical definition in types/tags.ts
```

---

## Deployment Issues

### Issue 30: Permission Features Work Locally But Not in Production

**Symptom**: Permissions work on localhost but fail in deployed env

**Root Cause 1**: Environment variable not set

**Solution**:
```bash
# Verify env vars in production
# Backend: DATABASE_URL, JWT_SECRET
# Frontend: NEXT_PUBLIC_API_URL

# Check .env.production file
cat .env.production
```

**Root Cause 2**: Database migration not applied

**Solution**:
```bash
# Apply migrations in production
dotnet ef database update --connection "$PROD_CONNECTION_STRING"
```

**Root Cause 3**: CORS configuration

**Solution**:
```csharp
// Use production frontend origin
policy.WithOrigins(
    "http://localhost:3000", // Dev
    "https://app.meepleai.com" // Prod ← Add this
);
```

---

## General Debugging Tips

### Enable Verbose Logging

```csharp
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Api.BoundedContexts.Administration": "Debug",
      "Default": "Information"
    }
  }
}
```

### Use React DevTools

```bash
# Install React DevTools browser extension
# Components tab: Inspect PermissionContext value
# Profiler tab: Find slow renders
```

### Check Network Tab

```bash
# Browser DevTools > Network
# Filter: /permissions
# Verify:
# - GET /permissions/me returns 200
# - Response includes tier, role, accessibleFeatures
# - Request includes Authorization header
```

### Database Query Logging

```csharp
// Enable EF Core query logging
builder.Services.AddDbContext<MeepleAiDbContext>(options =>
{
    options.UseNpgsql(connectionString)
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging(); // Dev only!
});
```

---

## Quick Diagnostic Commands

```bash
# Backend health
curl http://localhost:8080/health

# Permission endpoint (with auth)
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/permissions/me

# Database check
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c "SELECT * FROM \"Users\" LIMIT 1;"

# Frontend build
cd apps/web && pnpm build
# Check for warnings/errors

# Frontend typecheck
pnpm typecheck
# Should show 0 errors

# Test specific component
pnpm test -- TagStrip --verbose

# Check bundle size
pnpm exec next build
# Look for "Epic #4068" in bundle analyzer
```

---

## Getting Help

**If stuck**:
1. Check this troubleshooting guide
2. Review specs: `claudedocs/epic-4068-*.md`
3. Search issue tracker: https://github.com/.../issues?q=epic%3A4068
4. Check ADR-050: Permission System Architecture
5. Ask in team chat with error logs

**Provide**:
- Error message (full stack trace)
- Steps to reproduce
- Expected vs actual behavior
- Environment (OS, browser, .NET version, Node version)
- Relevant code snippet

---

## Prevention Checklist

**Before Committing**:
- [ ] Run all tests: `dotnet test && pnpm test`
- [ ] Typecheck: `pnpm typecheck`
- [ ] Lint: `pnpm lint --fix`
- [ ] Build: `dotnet build && pnpm build`
- [ ] No console errors in browser

**Before Creating PR**:
- [ ] Self-review code diff
- [ ] Test locally end-to-end
- [ ] Check performance (no regressions)
- [ ] Update documentation
- [ ] Add tests for new features

**Before Merging PR**:
- [ ] CI passes (all checks green)
- [ ] Code review approved
- [ ] No merge conflicts
- [ ] Squash commits (clean history)
- [ ] Update CHANGELOG.md
