# Epic #4068: Real-Time Permission Updates

**WebSocket/SSE integration for instant permission synchronization**

---

## Overview

**Problem**: Permission changes (tier upgrade, role assignment) don't reflect immediately in frontend (cached for 5 minutes).

**Solution**: WebSocket or SSE to push permission changes to connected clients in real-time.

**Use Cases**:
- User upgrades tier via Stripe → Immediately unlock Pro features (no page refresh)
- Admin suspends user → Immediately lock account (no 5-minute delay)
- Admin assigns role → Immediately show new features

---

## Architecture

```
┌─────────────┐        ┌──────────────┐        ┌─────────────┐
│   Stripe    │ ─────> │   Backend    │ ─────> │   Frontend  │
│  Webhook    │        │  (SignalR)   │        │  (useEffect)│
└─────────────┘        └──────────────┘        └─────────────┘
                              |
                              v
                       PermissionChanged
                       WebSocket Event
                              |
                              v
                   ┌──────────────────────┐
                   │ QueryClient.invalidate│
                   │ Refetch permissions   │
                   └──────────────────────┘
```

---

## Backend Implementation (SignalR)

### Permission Hub

```csharp
// Hubs/PermissionHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time permission updates (Epic #4068)
/// </summary>
[Authorize]
public class PermissionHub : Hub
{
    private readonly ILogger<PermissionHub> _logger;

    public PermissionHub(ILogger<PermissionHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} connected to PermissionHub", userId);

        // Subscribe to user's permission changes
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} disconnected from PermissionHub", userId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can request immediate permission refresh
    /// </summary>
    public async Task RequestRefresh()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} requested permission refresh", userId);

        // Fetch latest permissions and send to client
        // (Implementation depends on permission service)
        await Clients.Caller.SendAsync("PermissionsRefreshed");
    }
}
```

---

### Permission Change Event Handler

```csharp
// BoundedContexts/Authentication/Domain/Events/UserTierChangedEventHandler.cs
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Notify clients when user tier changes (Epic #4068)
/// </summary>
public class UserTierChangedEventHandler : INotificationHandler<UserTierChangedEvent>
{
    private readonly IHubContext<PermissionHub> _hubContext;
    private readonly ILogger<UserTierChangedEventHandler> _logger;

    public UserTierChangedEventHandler(
        IHubContext<PermissionHub> hubContext,
        ILogger<UserTierChangedEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(UserTierChangedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "User {UserId} tier changed: {OldTier} → {NewTier}",
            notification.UserId,
            notification.OldTier,
            notification.NewTier);

        // Notify user's connected clients
        await _hubContext.Clients
            .User(notification.UserId.ToString())
            .SendAsync("TierChanged", new
            {
                oldTier = notification.OldTier.Value,
                newTier = notification.NewTier.Value,
                timestamp = DateTime.UtcNow
            }, ct);

        _logger.LogInformation("Notified user {UserId} of tier change", notification.UserId);
    }
}

// Similar handlers for RoleChangedEvent, UserSuspendedEvent, etc.
```

---

### Program.cs Configuration

```csharp
// Register SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Map hub endpoint
app.MapHub<PermissionHub>("/hubs/permissions");
```

---

## Frontend Implementation (React)

### SignalR Connection Hook

```typescript
// hooks/usePermissionSync.ts
import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';

/**
 * Establishes WebSocket connection for real-time permission updates (Epic #4068)
 */
export function usePermissionSync(token: string | null) {
  const queryClient = useQueryClient();
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!token) return;

    // Build connection
    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/permissions`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 1s, 2s, 4s, 8s, max 30s
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .configureLogging(LogLevel.Information)
      .build();

    // Event: Tier changed
    connection.on('TierChanged', (data: { oldTier: string; newTier: string; timestamp: string }) => {
      console.log(`[PermissionSync] Tier changed: ${data.oldTier} → ${data.newTier}`);

      // Invalidate permission cache
      queryClient.invalidateQueries({ queryKey: ['permissions'] });

      // Refetch immediately
      queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });

      // Show notification
      toast.success(`Upgraded to ${data.newTier} tier!`, {
        description: 'New features are now available.',
        action: {
          label: 'Explore',
          onClick: () => router.push('/features')
        }
      });
    });

    // Event: Role changed
    connection.on('RoleChanged', (data: { oldRole: string; newRole: string }) => {
      console.log(`[PermissionSync] Role changed: ${data.oldRole} → ${data.newRole}`);

      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });

      toast.info(`Your role was updated to ${data.newRole}`);
    });

    // Event: Account suspended
    connection.on('AccountSuspended', (data: { reason?: string }) => {
      console.warn(`[PermissionSync] Account suspended: ${data.reason}`);

      // Clear all data and redirect
      queryClient.clear();
      localStorage.clear();

      toast.error('Your account has been suspended', {
        description: data.reason || 'Contact support for details',
        duration: Infinity // Don't auto-dismiss
      });

      router.push('/account/suspended');
    });

    // Event: Permissions refreshed (manual refresh)
    connection.on('PermissionsRefreshed', () => {
      queryClient.refetchQueries({ queryKey: ['permissions', 'me'] });
    });

    // Start connection
    connection.start()
      .then(() => {
        console.log('[PermissionSync] Connected to permission updates');
        connectionRef.current = connection;
      })
      .catch(err => {
        console.error('[PermissionSync] Connection failed:', err);
      });

    // Cleanup on unmount
    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
        console.log('[PermissionSync] Disconnected');
      }
    };
  }, [token, queryClient]);

  return {
    requestRefresh: () => {
      connectionRef.current?.invoke('RequestRefresh');
    }
  };
}
```

---

### App Layout Integration

```tsx
// app/layout.tsx
import { usePermissionSync } from '@/hooks/usePermissionSync';
import { useAuth } from '@/hooks/useAuth';

function PermissionSyncProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth(); // Get auth token
  usePermissionSync(token); // Establish WebSocket connection

  return <>{children}</>;
}

export default function RootLayout({ children }) {
  return (
    <QueryClientProvider>
      <PermissionProvider>
        <PermissionSyncProvider>
          {children}
        </PermissionSyncProvider>
      </PermissionProvider>
    </QueryClientProvider>
  );
}
```

---

## Alternative: Server-Sent Events (SSE)

**If SignalR too heavy, use SSE** (one-way: server → client):

### Backend SSE Endpoint

```csharp
// Routing/PermissionRoutes.cs
app.MapGet("/api/v1/permissions/stream", async (HttpContext ctx, CancellationToken ct) =>
{
    var userId = ctx.User.GetUserId();

    ctx.Response.Headers.Add("Content-Type", "text/event-stream");
    ctx.Response.Headers.Add("Cache-Control", "no-cache");
    ctx.Response.Headers.Add("Connection", "keep-alive");

    await ctx.Response.WriteAsync($"data: {{\"connected\":true,\"userId\":\"{userId}\"}}\n\n", ct);
    await ctx.Response.Body.FlushAsync(ct);

    // Keep connection open, send events on permission changes
    // (Implementation: Subscribe to domain events, write to stream)

    while (!ct.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct); // Keep-alive ping
        await ctx.Response.WriteAsync($"data: {{\"type\":\"ping\"}}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
})
.RequireAuthorization()
.WithTags("Permissions");
```

---

### Frontend SSE Hook

```typescript
// hooks/usePermissionSSE.ts
export function usePermissionSSE(token: string | null) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!token) return;

    const eventSource = new EventSource(
      `${process.env.NEXT_PUBLIC_API_URL}/api/v1/permissions/stream`,
      { withCredentials: true }
    );

    eventSource.onmessage = (event) => {
      const data = JSON.parse(event.data);

      if (data.type === 'TierChanged') {
        queryClient.invalidateQueries({ queryKey: ['permissions'] });
        toast.success(`Upgraded to ${data.newTier}!`);
      } else if (data.type === 'RoleChanged') {
        queryClient.invalidateQueries({ queryKey: ['permissions'] });
        toast.info(`Role updated to ${data.newRole}`);
      } else if (data.type === 'AccountSuspended') {
        queryClient.clear();
        router.push('/account/suspended');
      }
    };

    eventSource.onerror = (error) => {
      console.error('[PermissionSSE] Error:', error);
      eventSource.close();
    };

    return () => eventSource.close();
  }, [token, queryClient]);
}
```

**Comparison**:

| Feature | SignalR | SSE |
|---------|---------|-----|
| **Direction** | Bi-directional | Server → Client only |
| **Reconnect** | Automatic | Automatic |
| **Binary Data** | ✅ Yes | ❌ Text only |
| **Browser Support** | ✅ All modern | ✅ All modern |
| **Complexity** | Higher | Lower |
| **Use Case** | Chat, collaborative editing | Notifications, live updates |

**Recommendation**: SSE for Epic #4068 (simpler, sufficient for one-way permission updates)

---

## Webhook Integration (Stripe → Tier Update → WebSocket)

### Complete Flow

**Step 1**: Stripe webhook receives payment

```csharp
// Webhooks/StripeWebhookHandler.cs
app.MapPost("/webhooks/stripe", async (HttpContext ctx, IMediator mediator, IHubContext<PermissionHub> hub) =>
{
    var signature = ctx.Request.Headers["Stripe-Signature"];
    var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

    // Verify signature
    var stripeEvent = EventUtility.ConstructEvent(json, signature, stripeWebhookSecret);

    if (stripeEvent.Type == "checkout.session.completed")
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        var userId = Guid.Parse(session.Metadata["userId"]);
        var newTier = session.Metadata["tier"]; // "pro" or "enterprise"

        // Update user tier
        var command = new UpdateUserTierCommand(userId, UserTier.Parse(newTier));
        await mediator.Send(command);

        // Emit domain event (handled by UserTierChangedEventHandler)
        // Handler sends WebSocket message to user

        _logger.LogInformation("User {UserId} upgraded to {Tier} via Stripe", userId, newTier);
    }

    return Results.Ok();
})
.AllowAnonymous(); // Stripe webhook, no auth
```

**Step 2**: Domain event emitted

```csharp
// User.UpdateTier()
public void UpdateTier(UserTier newTier, Role requesterRole)
{
    // ... validation ...

    var oldTier = Tier;
    Tier = newTier;

    AddDomainEvent(new UserTierChangedEvent(Id, oldTier, newTier)); // ← Event emitted
}
```

**Step 3**: Event handler pushes WebSocket message

```csharp
// UserTierChangedEventHandler (already shown above)
public async Task Handle(UserTierChangedEvent notification, CancellationToken ct)
{
    await _hubContext.Clients
        .User(notification.UserId.ToString())
        .SendAsync("TierChanged", new { oldTier, newTier }, ct);
}
```

**Step 4**: Frontend receives event and updates

```typescript
// usePermissionSync (already shown above)
connection.on('TierChanged', (data) => {
  queryClient.invalidateQueries({ queryKey: ['permissions'] });
  toast.success(`Upgraded to ${data.newTier}!`);
});
```

**Complete Flow Timing**:
- Stripe webhook → Backend: ~200ms
- Backend → WebSocket push: ~10ms
- Frontend receives: ~50ms
- Frontend refetch: ~100ms
- **Total**: ~360ms (vs 5 minutes without WebSocket)

---

## Frontend Optimistic Updates

### Optimistic Tier Upgrade

```typescript
function UpgradeFlow() {
  const { refetch } = usePermissions();
  const queryClient = useQueryClient();

  const upgradeMutation = useMutation({
    mutationFn: async (tier: UserTier) => {
      const session = await createStripeCheckoutSession({ tier });
      window.location.href = session.url; // Redirect to Stripe
    },

    onMutate: async (tier) => {
      // Optimistic update (show Pro features immediately)
      queryClient.setQueryData(['permissions', 'me'], old => ({
        ...old,
        tier,
        accessibleFeatures: [...old.accessibleFeatures, 'bulk-select', 'agent.create']
      }));

      toast.loading('Processing upgrade...', { id: 'upgrade' });
    },

    onSuccess: () => {
      toast.success('Upgrade complete!', { id: 'upgrade' });
    },

    onError: () => {
      // Rollback optimistic update
      refetch();
      toast.error('Upgrade failed', { id: 'upgrade' });
    }
  });

  return (
    <button onClick={() => upgradeMutation.mutate('pro')}>
      Upgrade to Pro
    </button>
  );
}
```

---

## Connection State Management

### Robust WebSocket Reconnection

```typescript
export function usePermissionSync(token: string | null) {
  const [connectionState, setConnectionState] = useState<'disconnected' | 'connecting' | 'connected'>('disconnected');
  const reconnectAttemptsRef = useRef(0);
  const MAX_RECONNECT_ATTEMPTS = 10;

  useEffect(() => {
    if (!token) return;

    setConnectionState('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/permissions`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff with jitter
          const baseDelay = Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          const jitter = Math.random() * 1000; // 0-1s random jitter
          return baseDelay + jitter;
        }
      })
      .configureLogging(LogLevel.Information)
      .build();

    // Connection established
    connection.onclose((error) => {
      setConnectionState('disconnected');
      console.warn('[PermissionSync] Connection closed:', error);

      reconnectAttemptsRef.current++;

      if (reconnectAttemptsRef.current > MAX_RECONNECT_ATTEMPTS) {
        toast.error('Lost connection to server. Please refresh the page.', {
          duration: Infinity,
          action: {
            label: 'Refresh',
            onClick: () => window.location.reload()
          }
        });
      }
    });

    connection.onreconnecting((error) => {
      setConnectionState('connecting');
      console.log('[PermissionSync] Reconnecting...', error);
    });

    connection.onreconnected(() => {
      setConnectionState('connected');
      console.log('[PermissionSync] Reconnected');
      reconnectAttemptsRef.current = 0; // Reset counter

      // Refetch permissions after reconnect (may have missed events)
      queryClient.invalidateQueries({ queryKey: ['permissions'] });

      toast.success('Connection restored');
    });

    // Event handlers (TierChanged, RoleChanged, etc.)
    // ... (shown in previous examples)

    // Start connection
    connection.start()
      .then(() => {
        setConnectionState('connected');
        console.log('[PermissionSync] Connected');
      })
      .catch(err => {
        setConnectionState('disconnected');
        console.error('[PermissionSync] Failed to connect:', err);
      });

    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, [token, queryClient]);

  return { connectionState };
}
```

---

### Connection Status Indicator

```tsx
function PermissionSyncIndicator() {
  const { token } = useAuth();
  const { connectionState } = usePermissionSync(token);

  if (connectionState === 'connected') return null; // Hide when connected

  return (
    <div className={cn(
      'fixed bottom-4 right-4 px-4 py-2 rounded-lg shadow-lg',
      connectionState === 'connecting' && 'bg-yellow-100 text-yellow-900',
      connectionState === 'disconnected' && 'bg-red-100 text-red-900'
    )}>
      {connectionState === 'connecting' && (
        <>
          <Loader2 className="inline animate-spin mr-2" />
          Connecting...
        </>
      )}
      {connectionState === 'disconnected' && (
        <>
          <WifiOff className="inline mr-2" />
          Disconnected - some features may not update
          <button onClick={() => window.location.reload()} className="ml-2 underline">
            Refresh
          </button>
        </>
      )}
    </div>
  );
}
```

---

## Monitoring WebSocket Connections

### Metrics

```csharp
// Track SignalR metrics
public class PermissionHubMetrics
{
    private static readonly Counter ConnectionsTotal = Metrics.CreateCounter(
        "signalr_connections_total",
        "Total SignalR connections",
        new CounterConfiguration { LabelNames = new[] { "hub" } });

    private static readonly Gauge ActiveConnections = Metrics.CreateGauge(
        "signalr_active_connections",
        "Active SignalR connections",
        new GaugeConfiguration { LabelNames = new[] { "hub" } });

    public static void TrackConnection(string hubName)
    {
        ConnectionsTotal.WithLabels(hubName).Inc();
        ActiveConnections.WithLabels(hubName).Inc();
    }

    public static void TrackDisconnection(string hubName)
    {
        ActiveConnections.WithLabels(hubName).Dec();
    }
}

// In PermissionHub
public override async Task OnConnectedAsync()
{
    PermissionHubMetrics.TrackConnection("permission");
    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception? ex)
{
    PermissionHubMetrics.TrackDisconnection("permission");
    await base.OnDisconnectedAsync(ex);
}
```

---

### Grafana Dashboard

```json
{
  "panels": [
    {
      "title": "Active Permission Hub Connections",
      "targets": [
        { "expr": "signalr_active_connections{hub='permission'}" }
      ],
      "type": "stat"
    },
    {
      "title": "Permission Change Events (Last Hour)",
      "targets": [
        { "expr": "rate(signalr_messages_sent_total{hub='permission',method='TierChanged'}[1h])" },
        { "expr": "rate(signalr_messages_sent_total{hub='permission',method='RoleChanged'}[1h])" },
        { "expr": "rate(signalr_messages_sent_total{hub='permission',method='AccountSuspended'}[1h])" }
      ],
      "legend": ["Tier Changes", "Role Changes", "Suspensions"]
    },
    {
      "title": "WebSocket Connection Errors",
      "targets": [
        { "expr": "rate(signalr_connection_errors_total{hub='permission'}[5m])" }
      ],
      "alert": { "threshold": 0.01 }
    }
  ]
}
```

---

## Load Testing WebSocket Connections

### k6 WebSocket Test

```javascript
// tests/load/epic-4068-websocket-load-test.js
import ws from 'k6/ws';
import { check } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 500 },  // 500 concurrent WebSocket connections
    { duration: '5m', target: 500 },  // Hold for 5 minutes
    { duration: '1m', target: 1000 }, // Spike to 1000
    { duration: '1m', target: 0 },    // Ramp down
  ],
};

export default function () {
  const token = __ENV.TEST_TOKEN; // Auth token

  const url = `ws://localhost:8080/hubs/permissions?access_token=${token}`;

  const res = ws.connect(url, {}, (socket) => {
    socket.on('open', () => {
      console.log('WebSocket connected');

      // Subscribe to events
      socket.on('message', (data) => {
        const event = JSON.parse(data);

        if (event.type === 'TierChanged') {
          console.log(`Tier changed: ${event.oldTier} → ${event.newTier}`);
          permissionUpdateEvents.add(1);
        }
      });

      // Send heartbeat every 30 seconds
      socket.setInterval(() => {
        socket.send(JSON.stringify({ type: 'ping' }));
      }, 30000);
    });

    socket.on('close', () => {
      console.log('WebSocket closed');
    });

    socket.on('error', (err) => {
      console.error('WebSocket error:', err);
    });

    // Keep connection open for 60 seconds
    socket.setTimeout(() => {
      socket.close();
    }, 60000);
  });

  check(res, {
    'WebSocket connected': (r) => r && r.status === 101,
  });
}
```

**Expected Results**:
- 1000 concurrent connections supported
- < 1% connection errors
- Message latency < 100ms

---

## Security Considerations

### WebSocket Authentication

```csharp
// Verify JWT token on WebSocket connection
builder.Services.AddSignalR().AddHubOptions<PermissionHub>(options =>
{
    options.EnableDetailedErrors = false; // Don't leak errors in production

    // Validate token on connect
    options.AddFilter<PermissionHubAuthFilter>();
});

public class PermissionHubAuthFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var httpContext = invocationContext.Context.GetHttpContext();

        if (httpContext == null || !httpContext.User.Identity?.IsAuthenticated ?? false)
        {
            throw new HubException("Authentication required");
        }

        // Verify user not banned/suspended
        var userId = httpContext.User.GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user?.Status != UserAccountStatus.Active)
        {
            throw new HubException("Account suspended or banned");
        }

        return await next(invocationContext);
    }
}
```

---

### Rate Limiting WebSocket Events

```csharp
// Prevent flood of permission change events
public class UserTierChangedEventHandler : INotificationHandler<UserTierChangedEvent>
{
    private readonly IRateLimiter _rateLimiter;

    public async Task Handle(UserTierChangedEvent notification, CancellationToken ct)
    {
        var key = $"tier-change:{notification.UserId}";

        // Allow max 1 tier change notification per minute
        if (!await _rateLimiter.TryAcquireAsync(key, permitCount: 1, window: TimeSpan.FromMinutes(1)))
        {
            _logger.LogWarning("Rate limit exceeded for tier change notifications: {UserId}", notification.UserId);
            return; // Don't send notification
        }

        // Send WebSocket message
        await _hubContext.Clients.User(notification.UserId.ToString())
            .SendAsync("TierChanged", new { oldTier, newTier }, ct);
    }
}
```

**Why**: Prevent abuse (e.g., attacker rapidly changing tier to DoS WebSocket clients).

---

## Fallback Strategy (If WebSocket Unavailable)

```typescript
export function usePermissionSync(token: string | null) {
  const [wsAvailable, setWsAvailable] = useState(true);

  useEffect(() => {
    // Try WebSocket first
    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/permissions`, { accessTokenFactory: () => token! })
      .build();

    connection.start()
      .then(() => {
        setWsAvailable(true);
        // Setup event handlers...
      })
      .catch(() => {
        setWsAvailable(false);
        console.warn('[PermissionSync] WebSocket unavailable, falling back to polling');
      });

    // Fallback: Poll every 30 seconds if WebSocket fails
    if (!wsAvailable) {
      const pollInterval = setInterval(() => {
        queryClient.invalidateQueries({ queryKey: ['permissions'] });
      }, 30000);

      return () => clearInterval(pollInterval);
    }

    return () => connection.stop();
  }, [token, wsAvailable, queryClient]);
}
```

**Graceful Degradation**:
- Primary: WebSocket (instant updates)
- Fallback: Polling every 30 seconds (acceptable delay)
- Last resort: 5-minute cache (default React Query staleTime)

---

## Testing Real-Time Updates

### Integration Test

```typescript
// tests/e2e/permission-realtime.spec.ts
import { test, expect } from '@playwright/test';

test('Real-time tier upgrade', async ({ page, context }) => {
  // 1. Login as Free user
  await page.goto('/login');
  await page.fill('[name="email"]', 'free@example.com');
  await page.fill('[name="password"]', 'password');
  await page.click('button[type="submit"]');

  await page.goto('/games');

  // 2. Verify Free tier features only
  const card = page.locator('[data-testid="game-card"]').first();
  await expect(card.locator('[aria-label="Add to wishlist"]')).toBeVisible();
  await expect(card.locator('[type="checkbox"]')).not.toBeVisible(); // No bulk select

  // 3. Simulate tier upgrade (backend API call)
  const apiContext = await context.newPage();
  await apiContext.request.post('http://localhost:8080/api/v1/admin/users/upgrade', {
    data: {
      userId: freeUserId,
      newTier: 'pro'
    },
    headers: {
      'Authorization': `Bearer ${adminToken}`
    }
  });

  // 4. Wait for WebSocket event + permission refetch
  await page.waitForTimeout(1000); // Allow time for WebSocket message + refetch

  // 5. Verify Pro features now visible (no page refresh!)
  await expect(card.locator('[type="checkbox"]')).toBeVisible(); // Bulk select appeared

  // 6. Verify toast notification
  await expect(page.locator('text=Upgraded to pro tier!')).toBeVisible();
});
```

---

## Summary: Real-Time Permission Updates

**Backend**:
- SignalR hub: `/hubs/permissions`
- Event handlers: TierChanged, RoleChanged, AccountSuspended
- Domain events: UserTierChangedEvent, RoleChangedEvent
- Webhook integration: Stripe → Backend → WebSocket

**Frontend**:
- usePermissionSync hook: Establishes connection, listens for events
- Auto-reconnect: Exponential backoff with jitter
- Optimistic updates: Show features immediately, confirm via WebSocket
- Fallback: Poll every 30s if WebSocket fails

**Benefits**:
- ✅ Instant permission updates (vs 5-minute cache delay)
- ✅ Better UX (immediate tier upgrade feedback)
- ✅ Real-time account suspension (security)
- ✅ Reduced support tickets (users see upgrades immediately)

**Trade-offs**:
- ⚠️ Additional infrastructure (SignalR/SSE server)
- ⚠️ Connection overhead (~1KB/connection)
- ⚠️ Complexity (reconnection logic, event handling)

**Recommendation**: Implement for production (high value, manageable complexity)

---

## Resources

- SignalR Docs: https://learn.microsoft.com/en-us/aspnet/core/signalr/
- SSE Spec: https://html.spec.whatwg.org/multipage/server-sent-events.html
- k6 WebSocket: https://k6.io/docs/javascript-api/k6-ws/
- React Query Invalidation: https://tanstack.com/query/latest/docs/framework/react/guides/query-invalidation
