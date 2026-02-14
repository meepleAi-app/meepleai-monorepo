# Epic #4068: Caching Strategy

**Multi-layer caching for permission system performance**

---

## Caching Layers

```
┌──────────────────────────────────────────────────────────┐
│ Layer 1: PermissionRegistry (In-Memory, Singleton)       │
│ Purpose: Feature → Permission mapping                     │
│ TTL: Application lifetime (never expires)                │
│ Hit Rate: 100% (static configuration)                    │
└──────────────────────────────────────────────────────────┘
                           ↓
┌──────────────────────────────────────────────────────────┐
│ Layer 2: HybridCache (Memory + Redis, Per-User)         │
│ Purpose: User ID → Permissions response                  │
│ TTL: 5 minutes                                           │
│ Hit Rate Target: > 90%                                   │
└──────────────────────────────────────────────────────────┘
                           ↓
┌──────────────────────────────────────────────────────────┐
│ Layer 3: React Query (Browser Memory, Per-User)         │
│ Purpose: Frontend permission cache                       │
│ Stale Time: 5 minutes                                    │
│ Garbage Collection: 10 minutes                           │
│ Hit Rate Target: > 95%                                   │
└──────────────────────────────────────────────────────────┘
                           ↓
┌──────────────────────────────────────────────────────────┐
│ Layer 4: Database (Source of Truth)                      │
│ Purpose: Authoritative user tier/role/status             │
│ Query Time: ~5-20ms (with indexes)                       │
│ Only accessed on cache miss                              │
└──────────────────────────────────────────────────────────┘
```

---

## Layer 1: PermissionRegistry (In-Memory)

### Implementation

```csharp
// Application Services/PermissionRegistry.cs (already implemented)
public sealed class PermissionRegistry
{
    // Static dictionary (initialized once, never changes)
    private readonly Dictionary<string, Permission> _permissions;

    public PermissionRegistry()
    {
        // Loaded on app startup, cached for lifetime
        _permissions = new Dictionary<string, Permission> { /* ... */ };
    }

    public Permission? GetPermission(string featureName)
    {
        // O(1) dictionary lookup, no database/network
        return _permissions.GetValueOrDefault(featureName);
    }
}

// Registration: Singleton (single instance per application)
builder.Services.AddSingleton<PermissionRegistry>();
```

**Performance**:
- Lookup time: ~0.01ms (hash table)
- Memory: ~10KB (10 features)
- Hit rate: 100% (never misses)

**Cache Invalidation**: Never (static configuration, requires app restart to change)

**When to Invalidate**: Deploy new version with updated features

---

## Layer 2: HybridCache (Backend, Per-User)

### Implementation

```csharp
// Add HybridCache to DI
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024; // 1MB max per entry
    options.MaximumKeyLength = 512;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5), // Epic #4068: 5-minute TTL
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

// Use in query handler
public class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsQuery, GetUserPermissionsResponse>
{
    private readonly IHybridCacheService _cache;
    private readonly MeepleAiDbContext _context;
    private readonly PermissionRegistry _registry;

    public async Task<GetUserPermissionsResponse> Handle(GetUserPermissionsQuery request, CancellationToken ct)
    {
        var cacheKey = $"permissions:{request.UserId}";

        // Try cache first (memory + Redis)
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                // Cache miss: Query database
                var user = await _context.Users
                    .Where(u => u.Id == request.UserId)
                    .Select(u => new { u.Tier, u.Role, u.Status })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException("User", request.UserId);

                var permissionContext = new PermissionContext(user.Tier, user.Role, user.Status);
                var accessibleFeatures = _registry.GetAccessibleFeatures(permissionContext);

                return new GetUserPermissionsResponse(
                    user.Tier.Value,
                    user.Role.Value,
                    user.Status,
                    user.Tier.GetLimits(),
                    accessibleFeatures);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                Flags = HybridCacheEntryFlags.DisableLocalCacheRead // Use Redis only (for consistency across servers)
            },
            cancellationToken: ct);
    }
}
```

**Performance**:
- Cache hit: ~2ms (Redis lookup)
- Cache miss: ~20ms (database query + cache write)
- Hit rate target: >90%

**Cache Invalidation**:

```csharp
// Invalidate on tier/role/status change
public class UserTierChangedEventHandler : INotificationHandler<UserTierChangedEvent>
{
    private readonly IHybridCacheService _cache;

    public async Task Handle(UserTierChangedEvent notification, CancellationToken ct)
    {
        var cacheKey = $"permissions:{notification.UserId}";

        // Remove from cache
        await _cache.RemoveAsync(cacheKey, ct);

        _logger.LogInformation("Invalidated permission cache for user {UserId}", notification.UserId);

        // Also send WebSocket update
        // ... (as shown in WebSocket integration)
    }
}
```

---

## Layer 3: React Query (Frontend)

### Configuration

```typescript
// lib/react-query/queryClient.ts
import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Epic #4068: Permission cache config
      staleTime: 5 * 60 * 1000, // 5 minutes (matches backend cache)
      gcTime: 10 * 60 * 1000, // 10 minutes garbage collection
      refetchOnWindowFocus: true, // Refetch when user returns to tab
      refetchOnReconnect: true, // Refetch after network reconnect
      retry: 3, // Retry failed requests 3 times
      retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000), // Exponential backoff
    },
  },
});
```

### Permission Query

```typescript
// hooks/api/usePermissionsQuery.ts
import { useQuery } from '@tanstack/react-query';
import { getUserPermissions } from '@/lib/api/permissions';

export function useUserPermissionsQuery() {
  return useQuery({
    queryKey: ['permissions', 'me'],
    queryFn: getUserPermissions,

    // Epic #4068 cache config
    staleTime: 5 * 60 * 1000, // 5 min (matches backend HybridCache)
    gcTime: 10 * 60 * 1000,

    // Refetch strategies
    refetchOnMount: true, // Refetch when component mounts (if stale)
    refetchOnWindowFocus: true, // Refetch when user returns to tab
    refetchOnReconnect: true, // Refetch after network reconnect

    // Error handling
    retry: (failureCount, error) => {
      // Don't retry 401/403 (auth errors)
      if (error.status === 401 || error.status === 403) return false;
      return failureCount < 3;
    },

    // Placeholder data while loading (fail-safe defaults)
    placeholderData: {
      tier: 'free',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 50, storageQuotaMB: 100 },
      accessibleFeatures: ['wishlist']
    }
  });
}
```

**Performance**:
- Cache hit: ~0ms (instant from memory)
- Cache miss: ~100ms (API roundtrip + backend cache hit)
- Deep cache miss: ~120ms (API + backend database)

**Hit Rate**:
- First load: 0% (must fetch from API)
- Navigation: >95% (cached in memory)
- Return to tab: Refetch (backend cache hit, ~100ms)
- After 5 minutes: Refetch (stale)

---

### Cache Invalidation Strategies

**Manual Invalidation** (user-initiated):
```typescript
function LogoutButton() {
  const queryClient = useQueryClient();

  const handleLogout = async () => {
    // Clear ALL cached data
    queryClient.clear();

    await logout();
    router.push('/login');
  };

  return <button onClick={handleLogout}>Logout</button>;
}
```

**WebSocket Invalidation** (server-pushed):
```typescript
// In usePermissionSync hook
connection.on('TierChanged', () => {
  queryClient.invalidateQueries({ queryKey: ['permissions'] });
  queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });
});
```

**Time-Based Invalidation** (automatic):
```typescript
// React Query automatically refetches after 5 minutes (staleTime)
// No manual intervention needed
```

**Focus/Reconnect Invalidation** (user action):
```typescript
// Configured in queryClient defaults (refetchOnWindowFocus: true)
// When user alt-tabs back, React Query refetches stale queries
```

---

## Cache Warming Strategies

### Pre-fetch Permissions on Login

```typescript
// In login flow
async function handleLogin(email: string, password: string) {
  const { accessToken, refreshToken } = await login(email, password);

  // Store tokens
  setTokens(accessToken, refreshToken);

  // Pre-fetch permissions (warm cache)
  await queryClient.prefetchQuery({
    queryKey: ['permissions', 'me'],
    queryFn: getUserPermissions
  });

  // Redirect (permissions already cached)
  router.push('/games');
}

// Result: First page load instant (no permission loading spinner)
```

---

### Pre-fetch Common Permission Checks

```typescript
// Pre-fetch frequently checked permissions
useEffect(() => {
  if (permissions) {
    // Pre-fetch batch of common features
    const commonFeatures = ['wishlist', 'bulk-select', 'drag-drop'];

    commonFeatures.forEach(feature => {
      queryClient.prefetchQuery({
        queryKey: ['permission', 'check', feature],
        queryFn: () => checkPermission(feature)
      });
    });
  }
}, [permissions]);
```

---

## Cache Performance Monitoring

### Metrics Collection

```csharp
// Track cache hit/miss rates
public class PermissionCacheMetrics
{
    private static readonly Counter CacheHits = Metrics.CreateCounter(
        "permission_cache_hit_total",
        "Permission cache hits");

    private static readonly Counter CacheMisses = Metrics.CreateCounter(
        "permission_cache_miss_total",
        "Permission cache misses");

    private static readonly Histogram CacheAccessDuration = Metrics.CreateHistogram(
        "permission_cache_access_duration_ms",
        "Permission cache access duration",
        new HistogramConfiguration
        {
            Buckets = new[] { 1, 2, 5, 10, 25, 50, 100 }
        });

    public static void TrackHit() => CacheHits.Inc();
    public static void TrackMiss() => CacheMisses.Inc();
    public static void TrackDuration(double ms) => CacheAccessDuration.Observe(ms);
}

// In GetUserPermissionsHandler
public async Task<GetUserPermissionsResponse> Handle(...)
{
    var sw = Stopwatch.StartTime();

    var result = await _cache.GetOrCreateAsync(
        cacheKey,
        async ct =>
        {
            PermissionCacheMetrics.TrackMiss(); // Cache miss
            return await FetchFromDatabase(ct);
        },
        options,
        ct);

    if (result != null) // Cache hit (factory not called)
    {
        PermissionCacheMetrics.TrackHit();
    }

    PermissionCacheMetrics.TrackDuration(sw.Elapsed.TotalMilliseconds);

    return result;
}
```

---

### Grafana Cache Dashboard

```json
{
  "title": "Epic #4068: Cache Performance",
  "panels": [
    {
      "title": "Permission Cache Hit Rate",
      "targets": [
        {
          "expr": "rate(permission_cache_hit_total[5m]) / (rate(permission_cache_hit_total[5m]) + rate(permission_cache_miss_total[5m]))"
        }
      ],
      "thresholds": [
        { "value": 0.8, "color": "green" },
        { "value": 0.6, "color": "yellow" },
        { "value": 0, "color": "red" }
      ]
    },
    {
      "title": "Cache Access Duration (p95)",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, rate(permission_cache_access_duration_ms_bucket[5m]))"
        }
      ],
      "alert": { "threshold": 10 }
    },
    {
      "title": "Cache Size (Redis Memory)",
      "targets": [
        { "expr": "redis_memory_used_bytes{instance='permission-cache'}" }
      ]
    },
    {
      "title": "Cache Evictions (LRU)",
      "targets": [
        { "expr": "rate(redis_evicted_keys_total[5m])" }
      ],
      "alert": { "threshold": 100 }
    }
  ]
}
```

---

## Cache Invalidation Patterns

### Pattern 1: Event-Driven Invalidation

```csharp
// On any permission-related change, invalidate cache
public class UserTierChangedEventHandler : INotificationHandler<UserTierChangedEvent>
{
    private readonly IHybridCacheService _cache;

    public async Task Handle(UserTierChangedEvent e, CancellationToken ct)
    {
        await _cache.RemoveAsync($"permissions:{e.UserId}", ct);
    }
}

// Similarly for: RoleChangedEvent, UserSuspendedEvent, UserUnsuspendedEvent
```

---

### Pattern 2: Tag-Based Invalidation

```csharp
// Invalidate all users with specific tier
public async Task InvalidateByTier(UserTier tier)
{
    // Get all user IDs with this tier
    var userIds = await _db.Users
        .Where(u => u.Tier == tier)
        .Select(u => u.Id)
        .ToListAsync();

    // Invalidate each (parallel)
    await Parallel.ForEachAsync(userIds, async (userId, ct) =>
    {
        await _cache.RemoveAsync($"permissions:{userId}", ct);
    });

    _logger.LogInformation("Invalidated permission cache for {Count} {Tier} users", userIds.Count, tier);
}
```

**Use Case**: Feature added/removed from tier (e.g., Normal tier now includes agent.create)

---

### Pattern 3: Time-To-Live (TTL) Expiration

```csharp
// Automatic expiration after 5 minutes
new HybridCacheEntryOptions
{
    Expiration = TimeSpan.FromMinutes(5)
}

// No manual invalidation needed, expires naturally
```

**Tradeoff**:
- ✅ Pro: Simple, no invalidation logic bugs
- ⚠️ Con: Stale data for up to 5 minutes

**Mitigation**: Combine with event-driven invalidation (best of both)

---

## Frontend Cache Optimization

### Optimistic Updates

```typescript
// Update cache optimistically, confirm with server
const upgradeMutation = useMutation({
  mutationFn: upgradeTier,

  onMutate: async (newTier) => {
    // Cancel ongoing refetches
    await queryClient.cancelQueries({ queryKey: ['permissions', 'me'] });

    // Snapshot current value
    const previous = queryClient.getQueryData(['permissions', 'me']);

    // Optimistically update
    queryClient.setQueryData(['permissions', 'me'], (old) => ({
      ...old,
      tier: newTier,
      limits: TIER_LIMITS[newTier],
      accessibleFeatures: [...old.accessibleFeatures, ...NEW_FEATURES[newTier]]
    }));

    return { previous };
  },

  onError: (err, variables, context) => {
    // Rollback on error
    queryClient.setQueryData(['permissions', 'me'], context.previous);
  },

  onSettled: () => {
    // Refetch to sync with server (confirm optimistic update)
    queryClient.invalidateQueries({ queryKey: ['permissions'] });
  }
});
```

---

### Partial Cache Updates

```typescript
// Update specific fields without refetching entire object
queryClient.setQueryData(['permissions', 'me'], (old) => ({
  ...old,
  tier: 'pro' // Only update tier, keep other fields
}));

// Or use immer for immutable updates
import { produce } from 'immer';

queryClient.setQueryData(['permissions', 'me'], (old) =>
  produce(old, draft => {
    draft.tier = 'pro';
    draft.accessibleFeatures.push('bulk-select', 'agent.create');
  })
);
```

---

## Cache Warming on App Start

### Pre-fetch Critical Data

```tsx
// app/providers.tsx
export function AppProviders({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient(/* config */);

  useEffect(() => {
    // Pre-fetch permissions on app load (warm cache)
    queryClient.prefetchQuery({
      queryKey: ['permissions', 'me'],
      queryFn: getUserPermissions
    });

    // Pre-fetch other critical data
    queryClient.prefetchQuery({ queryKey: ['user', 'profile'], queryFn: getUserProfile });
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}
```

**Result**: Permissions cached before user navigates to first page (instant access)

---

## Redis Configuration for Permission Cache

### Docker Compose

```yaml
# docker-compose.yml
services:
  redis-permission-cache:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:
      - "6379:6379"
    volumes:
      - redis-permission-data:/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 3

volumes:
  redis-permission-data:
```

---

### Redis Tuning

```redis
# redis.conf (production)

# Memory
maxmemory 256mb
maxmemory-policy allkeys-lru # Evict least recently used when memory full

# Persistence (optional, permission cache can be rebuilt)
save "" # Disable RDB snapshots (permission cache is ephemeral)
appendonly no # Disable AOF (faster, acceptable data loss)

# Performance
tcp-backlog 511
timeout 300 # Close idle connections after 5 minutes
tcp-keepalive 60

# Slow log
slowlog-log-slower-than 10000 # Log commands > 10ms
slowlog-max-len 128
```

---

## Cache Eviction Policies

### LRU (Least Recently Used)

```
# Default: allkeys-lru
# When memory full, evict least recently accessed keys

# Example:
# User A: Last access 10 minutes ago
# User B: Last access 1 minute ago
# User C: Last access 30 seconds ago

# If memory full, evict User A's cache (oldest)
```

**Why**: Frequently accessed users (active users) stay cached, inactive users evicted.

---

### Proactive Eviction

```csharp
// Evict suspended/banned users (no need to cache)
public async Task SuspendUser(Guid userId, string reason)
{
    var user = await _db.Users.FindAsync(userId);
    user.Suspend(reason);

    await _db.SaveChangesAsync();

    // Proactively evict cache (user can't access anyway)
    await _cache.RemoveAsync($"permissions:{userId}");

    _logger.LogInformation("Suspended user {UserId}, cache evicted", userId);
}
```

**Why**: Suspended users won't access system, no point caching their permissions.

---

## Testing Cache Behavior

### Unit Test: Cache Hit/Miss

```csharp
[Fact]
public async Task Handle_SecondCall_HitsCache()
{
    // Arrange
    var query = new GetUserPermissionsQuery(userId);

    // Act: First call (cache miss)
    var result1 = await _handler.Handle(query, ct);

    // Act: Second call (should hit cache)
    var sw = Stopwatch.StartNew();
    var result2 = await _handler.Handle(query, ct);
    sw.Stop();

    // Assert
    Assert.Equal(result1.Tier, result2.Tier);
    Assert.True(sw.ElapsedMilliseconds < 5, "Second call should be cached (<5ms)");
}
```

---

### Integration Test: Cache Invalidation

```csharp
[Fact]
public async Task TierChange_InvalidatesCache()
{
    // Arrange
    var user = await _db.Users.FindAsync(userId);

    // Get permissions (cache)
    var permissions1 = await _mediator.Send(new GetUserPermissionsQuery(userId));
    Assert.Equal("free", permissions1.Tier);

    // Act: Change tier
    user.UpdateTier(UserTier.Pro, Role.SuperAdmin);
    await _db.SaveChangesAsync();
    await _mediator.Publish(new UserTierChangedEvent(userId, UserTier.Free, UserTier.Pro));

    // Wait for event handler to invalidate cache
    await Task.Delay(100);

    // Assert: Next query fetches new data (not cached old data)
    var permissions2 = await _mediator.Send(new GetUserPermissionsQuery(userId));
    Assert.Equal("pro", permissions2.Tier); // Updated (cache was invalidated)
}
```

---

### E2E Test: Optimistic Update

```typescript
test('Optimistic tier upgrade updates UI immediately', async ({ page }) => {
  await page.goto('/upgrade');

  // Verify Free tier badge
  await expect(page.locator('text=Free tier')).toBeVisible();

  // Click upgrade (optimistic update)
  await page.click('text=Upgrade to Pro');

  // UI updates immediately (before API confirms)
  await expect(page.locator('text=Pro tier')).toBeVisible({ timeout: 500 });

  // Wait for API confirmation
  await page.waitForResponse(response =>
    response.url().includes('/api/v1/payments/upgrade') && response.status() === 200
  );

  // UI still shows Pro (optimistic update confirmed)
  await expect(page.locator('text=Pro tier')).toBeVisible();
});
```

---

## Cache Sizing & Capacity Planning

### Memory Requirements

**Per-User Cache Entry**:
```json
{
  "tier": "pro",                          // ~10 bytes
  "role": "user",                         // ~10 bytes
  "status": "Active",                     // ~10 bytes
  "limits": {                             // ~30 bytes
    "maxGames": 500,
    "storageQuotaMB": 5000
  },
  "accessibleFeatures": [                 // ~200 bytes (10 features × ~20 bytes)
    "wishlist",
    "bulk-select",
    // ... 8 more
  ]
}

// Total: ~260 bytes per user
```

**Redis Capacity**:
- 256MB Redis = ~1M cached users
- 512MB Redis = ~2M cached users
- 1GB Redis = ~4M cached users

**Recommendation**: 256MB Redis sufficient for <1M active users

---

### Eviction Monitoring

```promql
# Alert if evictions too frequent (cache too small)
rate(redis_evicted_keys_total[5m]) > 100

# Alert if memory usage > 90% (increase cache size)
redis_memory_used_bytes / redis_memory_max_bytes > 0.9
```

---

## Cache Consistency

### Strong Consistency (Database Always Authoritative)

```csharp
// Even with cache, backend double-checks critical operations
app.MapDelete("/api/v1/games/{id}", async (Guid id, Guid userId, IMediator m, IHybridCacheService cache) =>
{
    // Get permissions (may be cached)
    var permissions = await cache.GetOrCreateAsync($"permissions:{userId}", ...);

    // Double-check: Query database for latest status
    var user = await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => new { u.Status })
        .FirstAsync();

    if (user.Status != UserAccountStatus.Active)
    {
        // User suspended since cache was populated
        return Results.Forbid();
    }

    // Proceed with delete (permissions checked, status verified)
    return await m.Send(new DeleteGameCommand(id, userId));
});
```

**Why**: Cache may be stale (5-minute TTL), critical operations verify with database.

---

### Eventual Consistency (Acceptable for UX)

```typescript
// Frontend: Acceptable to show Pro features for up to 5 minutes after downgrade
// User experience: Optimistic (assume cache correct)
// Backend: Authoritative (always checks database)

// Example: User downgraded from Pro → Free
// Frontend still shows "bulk-select" button for up to 5 minutes (cached)
// User clicks button
// Backend denies (database shows Free tier)
// Frontend shows error, refetches permissions, updates UI

// Result: Briefly confusing UX, but secure (backend enforces)
```

**Acceptable Trade-Off**: Rare edge case (downgrades uncommon) vs performance benefit

---

## Cache Security

### Prevent Cache Poisoning

```csharp
// Validate data before caching
public async Task<GetUserPermissionsResponse> Handle(...)
{
    var user = await FetchUserFromDatabase();

    // Validate tier/role before caching
    if (!IsValidTier(user.Tier))
    {
        _logger.LogWarning("Invalid tier for user {UserId}: {Tier}", user.Id, user.Tier);
        throw new InvalidOperationException("Invalid user tier");
    }

    if (!IsValidRole(user.Role))
    {
        _logger.LogWarning("Invalid role for user {UserId}: {Role}", user.Id, user.Role);
        throw new InvalidOperationException("Invalid user role");
    }

    // Cache validated data
    return await _cache.SetAsync(cacheKey, validatedPermissions, options);
}
```

---

### Cache Key Security

```csharp
// Use GUIDs for cache keys (not guessable)
var cacheKey = $"permissions:{userId}"; // userId is GUID

// ❌ INSECURE: Predictable keys
var cacheKey = $"permissions:user_{userIndex}"; // Attacker can guess user_1, user_2, ...
```

---

## Summary: Caching Performance Impact

**Without Caching**:
- Every permission check: Database query (~20ms)
- 100 req/sec: 2000ms/sec of database time (unsustainable)

**With Layer 2 (HybridCache)**:
- 90% cache hit rate: 10 database queries/sec, 90 cache hits (~2ms each)
- 100 req/sec: 200ms database + 180ms cache = 380ms total (sustainable)

**With Layer 3 (React Query)**:
- 95% frontend cache hit rate: 0ms (instant from memory)
- 5% API calls: ~100ms each
- User experience: Instant (no loading spinners)

**Total Improvement**: 20ms (uncached) → 2ms (cached) = **10x faster**

---

## Cache Monitoring Checklist

- [ ] Cache hit rate > 90% (HybridCache)
- [ ] Cache access duration p95 < 10ms
- [ ] Frontend cache hit rate > 95% (React Query)
- [ ] No cache evictions (Redis memory sufficient)
- [ ] Cache invalidation working (permission changes reflect < 1 second)
- [ ] No cache poisoning (validated data only)

---

## Resources

- HybridCache Docs: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid
- React Query: https://tanstack.com/query/latest/docs/framework/react/guides/caching
- Redis Best Practices: https://redis.io/docs/management/optimization/
