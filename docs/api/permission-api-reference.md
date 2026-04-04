# Permission API Reference (Epic #4068)

**Base URL**: `/api/v1/permissions`
**Authentication**: Required (Bearer token)
**Rate Limit**: 100 requests/minute per user

---

## Endpoints

### GET /me

**Description**: Get current user's permission context and accessible features

**Authentication**: Required

**Request**:
```http
GET /api/v1/permissions/me HTTP/1.1
Host: localhost:8080
Authorization: Bearer eyJhbGc...
```

**Response 200 OK**:
```json
{
  "tier": "pro",
  "role": "user",
  "status": "Active",
  "limits": {
    "maxGames": 500,
    "storageQuotaMB": 5000
  },
  "accessibleFeatures": [
    "wishlist",
    "bulk-select",
    "drag-drop",
    "collection.manage",
    "document.upload",
    "agent.create",
    "analytics.view",
    "filters.advanced"
  ]
}
```

**Response Fields**:
- `tier` (string): User's subscription tier
  - Values: `"free"`, `"normal"`, `"premium"`, `"pro"`, `"enterprise"`
- `role` (string): User's role
  - Values: `"user"`, `"editor"`, `"creator"`, `"admin"`, `"superadmin"`
- `status` (string): Account status
  - Values: `"Active"`, `"Suspended"`, `"Banned"`
- `limits` (object): Tier-based collection limits
  - `maxGames` (number): Max games in collection
  - `storageQuotaMB` (number): Max storage quota in MB
- `accessibleFeatures` (string[]): List of features user can access

**Error Responses**:

`401 Unauthorized`:
```json
{
  "type": "https://httpstatuses.io/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authorization header missing or invalid"
}
```

`404 Not Found`:
```json
{
  "type": "https://httpstatuses.io/404",
  "title": "Not Found",
  "status": 404,
  "detail": "User with identifier '{userId}' was not found"
}
```

**Caching**: Frontend should cache for 5 minutes (staleTime: 5min)

**Example Usage**:

```typescript
// TypeScript
const response = await fetch('/api/v1/permissions/me', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const permissions = await response.json();

console.log('User tier:', permissions.tier);
console.log('Can bulk select:', permissions.accessibleFeatures.includes('bulk-select'));
```

```csharp
// C# (for server-side scenarios)
var userId = httpContext.User.GetUserId();
var permissions = await mediator.Send(new GetUserPermissionsQuery(userId));
```

---

### GET /check

**Description**: Check if user has access to specific feature (optionally with resource state)

**Authentication**: Required

**Request**:
```http
GET /api/v1/permissions/check?feature=bulk-select&state=published HTTP/1.1
Host: localhost:8080
Authorization: Bearer eyJhbGc...
```

**Query Parameters**:
- `feature` (required, string): Feature name to check
  - Examples: `"wishlist"`, `"bulk-select"`, `"quick-action.delete"`
- `state` (optional, string): Resource state for state-based checks
  - Examples: `"draft"`, `"published"`, `"archived"` (for games)

**Response 200 OK**:
```json
{
  "hasAccess": true,
  "reason": "Tier sufficient",
  "details": {
    "userTier": "Pro",
    "userRole": "User",
    "userStatus": "Active",
    "required": {
      "tier": "Pro",
      "role": "User",
      "states": null
    },
    "logic": "Or"
  }
}
```

**Response Fields**:
- `hasAccess` (boolean): True if user has access, false otherwise
- `reason` (string): Explanation for result
  - Values: `"Tier sufficient"`, `"Role sufficient"`, `"Neither tier nor role sufficient"`, `"User account is suspended"`, etc.
- `details` (object): Detailed permission context
  - `userTier` (string): User's tier display name
  - `userRole` (string): User's role display name
  - `userStatus` (string): Account status
  - `required` (object): Requirements for this feature
    - `tier` (string | null): Required tier (if any)
    - `role` (string | null): Required role (if any)
    - `states` (string[] | null): Allowed states (if any)
  - `logic` (string): Permission logic (`"Or"` or `"And"`)

**Examples**:

**Example 1**: Pro user checking bulk-select (allowed)
```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=bulk-select"
```

Response:
```json
{
  "hasAccess": true,
  "reason": "Tier sufficient",
  "details": {
    "userTier": "Pro",
    "userRole": "User",
    "userStatus": "Active",
    "required": { "tier": "Pro", "role": "Editor", "states": null },
    "logic": "Or"
  }
}
```

**Example 2**: Free user checking bulk-select (denied)
```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=bulk-select"
```

Response:
```json
{
  "hasAccess": false,
  "reason": "Neither tier nor role sufficient",
  "details": {
    "userTier": "Free",
    "userRole": "User",
    "userStatus": "Active",
    "required": { "tier": "Pro", "role": "Editor", "states": null },
    "logic": "Or"
  }
}
```

**Example 3**: State-based check (draft game access)
```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=view-game&state=draft"
```

Response (creator):
```json
{
  "hasAccess": true,
  "reason": "Role sufficient",
  "details": {
    "userTier": "Normal",
    "userRole": "Creator",
    "userStatus": "Active",
    "required": { "tier": null, "role": "Creator", "states": ["draft", "published"] },
    "logic": "Or"
  }
}
```

Response (regular user):
```json
{
  "hasAccess": false,
  "reason": "Resource state 'draft' not allowed",
  "details": {
    "userTier": "Normal",
    "userRole": "User",
    "userStatus": "Active",
    "required": { "tier": null, "role": "Creator", "states": ["published"] },
    "logic": "Or"
  }
}
```

**Error Responses**:

`400 Bad Request` (missing feature parameter):
```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Feature parameter required",
  "errors": {
    "feature": ["The feature field is required."]
  }
}
```

`404 Not Found` (unknown feature):
```json
{
  "hasAccess": false,
  "reason": "Feature 'unknown-feature' not found in registry",
  "details": { /* ... */ }
}
```

---

## Feature Permission Registry

**Registered Features** (as of Epic #4068):

| Feature Name | Required Tier | Required Role | Logic | Description |
|--------------|---------------|---------------|-------|-------------|
| `wishlist` | Free | User | OR | Add games to wishlist |
| `bulk-select` | Pro | Editor | OR | Select multiple cards |
| `drag-drop` | Normal | User | OR | Drag to reorder |
| `quick-action.delete` | Free | Admin | AND | Delete via quick action menu |
| `quick-action.edit` | Normal | Creator | OR | Edit via quick action menu |
| `collection.manage` | Normal | User | OR | Create/edit collections |
| `document.upload` | Normal | User | OR | Upload PDF rulebooks |
| `agent.create` | Pro | Creator | OR | Create AI agents |
| `analytics.view` | Pro | Admin | OR | View analytics dashboard |
| `filters.advanced` | Normal | User | OR | Use advanced filters |

**Adding New Features**:

```csharp
// PermissionRegistry.cs constructor
_permissions.Add("new-feature", Permission.CreateOr("new-feature", UserTier.Normal, Role.User));
```

**Testing New Features**:
```bash
# Backend unit test
dotnet test --filter "FullyQualifiedName~PermissionRegistryTests.GetPermission_ReturnsNewFeature"

# API test
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=new-feature"
```

---

## Client Libraries

### TypeScript Client

```typescript
import { getUserPermissions, checkPermission } from '@/lib/api/permissions';

// Get all permissions
const permissions = await getUserPermissions();
console.log(permissions.tier); // "pro"

// Check specific feature
const result = await checkPermission('bulk-select');
if (result.hasAccess) {
  // User can bulk select
}

// Check with state
const draftAccess = await checkPermission('view-game', 'draft');
if (!draftAccess.hasAccess) {
  console.log('Reason:', draftAccess.reason); // "Resource state 'draft' not allowed"
}
```

### React Hooks

```typescript
import { usePermissions } from '@/contexts/PermissionContext';

function MyComponent() {
  const { tier, role, canAccess, hasTier, isAdmin, loading } = usePermissions();

  if (loading) return <Spinner />;

  return (
    <div>
      <p>Tier: {tier}</p> {/* "pro" */}
      <p>Role: {role}</p> {/* "user" */}

      {canAccess('bulk-select') && <BulkSelectButton />}
      {hasTier('pro') && <ProFeatures />}
      {isAdmin() && <AdminPanel />}
    </div>
  );
}
```

### C# Client (Server-to-Server)

```csharp
public class GameService
{
    private readonly IMediator _mediator;

    public async Task<bool> CanUserDeleteGame(Guid userId)
    {
        var result = await _mediator.Send(
            new CheckPermissionQuery(userId, "quick-action.delete"));

        return result.HasAccess;
    }

    public async Task<List<string>> GetUserFeatures(Guid userId)
    {
        var permissions = await _mediator.Send(
            new GetUserPermissionsQuery(userId));

        return permissions.AccessibleFeatures;
    }
}
```

---

## Rate Limiting

**Limits**:
- 100 requests/minute per user (across both endpoints)
- 1000 requests/minute globally

**Response when rate limited** (429 Too Many Requests):
```json
{
  "type": "https://httpstatuses.io/429",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Try again in 34 seconds.",
  "retryAfter": 34
}
```

**Client Handling**:
```typescript
async function checkPermissionWithRetry(feature: string, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await checkPermission(feature);
    } catch (error) {
      if (error.status === 429 && i < maxRetries - 1) {
        const retryAfter = error.retryAfter || 10;
        await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
        continue;
      }
      throw error;
    }
  }
}
```

---

## Webhooks (Future Enhancement)

**Permission Change Events** (not yet implemented, planned for v1.6):

```json
POST https://your-app.com/webhooks/permission-changed
{
  "event": "user.tier.changed",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "data": {
    "oldTier": "free",
    "newTier": "pro",
    "changedAt": "2026-02-12T10:00:00Z",
    "changedBy": "admin-user-id"
  }
}
```

**Use Cases**:
- Invalidate permission caches in frontend
- Trigger welcome email for tier upgrade
- Update analytics dashboards
- Notify connected WebSocket clients

---

## Monitoring & Observability

### Metrics Endpoint

**GET /metrics** (Prometheus format):
```
# HELP permission_check_total Total permission checks
# TYPE permission_check_total counter
permission_check_total{feature="wishlist",result="allowed"} 15234
permission_check_total{feature="bulk-select",result="denied"} 432

# HELP permission_check_duration_seconds Duration of permission checks
# TYPE permission_check_duration_seconds histogram
permission_check_duration_seconds_bucket{le="0.005"} 9542
permission_check_duration_seconds_bucket{le="0.01"} 9987
permission_check_duration_seconds_bucket{le="0.025"} 10000
```

### Logging

**Info Level**: Successful permission checks
```
2026-02-12 10:00:00 [INF] Permission check: feature=bulk-select, user=pro/user, result=allowed
```

**Warning Level**: Denied permission checks
```
2026-02-12 10:00:05 [WRN] Permission denied: feature=bulk-select, user=free/user, reason="Neither tier nor role sufficient"
```

**Error Level**: Failures (should be rare)
```
2026-02-12 10:00:10 [ERR] Permission check failed: feature=bulk-select, user=xxx, error="User not found"
```

---

## Versioning

**Current**: v1 (Epic #4068)

**Future Versions**:
- v1.1: Add batch permission check endpoint (`POST /check/batch`)
- v1.2: Add permission cache endpoint (`GET /cache/{userId}`)
- v2.0: Breaking changes (remove deprecated fields)

**Deprecation Policy**:
- Deprecated fields marked in response (with `_deprecated: true`)
- Deprecated endpoints: 6-month notice before removal
- Version in URL (future): `/api/v2/permissions/me`

---

## Security Considerations

**Token Validation**:
- Bearer token signature verified
- Token expiration checked (15min for access tokens)
- User ID claim extracted from token

**Authorization**:
- All endpoints require authentication (`.RequireAuthorization()`)
- Tier/role read from database (not JWT claims alone)
- Permission checks logged for security monitoring

**Input Validation**:
- Feature name: Whitelist validation against PermissionRegistry
- State: Optional, validated if provided
- User ID: GUID format validation

**Output Sanitization**:
- Generic error messages (don't leak internal details)
- Sensitive info logged server-side only (not returned in response)

---

## Client Best Practices

**1. Cache Aggressively**
```typescript
// React Query: 5-minute cache
useQuery({
  queryKey: ['permissions', 'me'],
  queryFn: getUserPermissions,
  staleTime: 5 * 60 * 1000,
  gcTime: 10 * 60 * 1000
});
```

**2. Batch Checks (when v1.1 available)**
```typescript
// Instead of multiple /check calls
const features = ['wishlist', 'bulk-select', 'drag-drop'];
const results = await Promise.all(features.map(f => checkPermission(f)));

// Better: Single batch request (v1.1+)
const results = await batchCheckPermissions(features);
```

**3. Handle Errors Gracefully**
```typescript
try {
  const permissions = await getUserPermissions();
} catch (error) {
  if (error.status === 401) {
    // Redirect to login
    router.push('/login');
  } else {
    // Default to safe fallback (Free/User)
    return { tier: 'free', role: 'user', accessibleFeatures: ['wishlist'] };
  }
}
```

**4. Optimistic UI Updates**
```typescript
// Show feature immediately, verify async
setShowFeature(true); // Optimistic

checkPermission('new-feature').then(result => {
  if (!result.hasAccess) {
    setShowFeature(false); // Rollback if denied
    showUpgradePrompt();
  }
});
```

---

## Integration Examples

### Example 1: Feature-Locked Button

```typescript
function BulkSelectButton() {
  const { canAccess } = usePermissions();
  const [checking, setChecking] = useState(false);

  const handleClick = async () => {
    setChecking(true);
    const result = await checkPermission('bulk-select');
    setChecking(false);

    if (!result.hasAccess) {
      toast.error(`This feature requires ${result.details.required.tier} tier`);
      return;
    }

    // Proceed with bulk select
    enableBulkSelectMode();
  };

  return (
    <button
      onClick={handleClick}
      disabled={checking}
      className={!canAccess('bulk-select') ? 'opacity-50' : ''}
    >
      {checking ? 'Checking...' : 'Bulk Select'}
      {!canAccess('bulk-select') && <Lock className="ml-2" />}
    </button>
  );
}
```

### Example 2: Admin Panel Guard

```typescript
function AdminPanel() {
  const { isAdmin, role } = usePermissions();

  if (!isAdmin()) {
    return (
      <div>
        <h1>Access Denied</h1>
        <p>Admin or SuperAdmin role required.</p>
        <p>Your role: {role}</p>
      </div>
    );
  }

  return <AdminDashboard />;
}
```

### Example 3: Dynamic Menu Items

```typescript
function QuickActionsMenu({ gameId }: Props) {
  const { canAccess, isAdmin } = usePermissions();

  const actions = useMemo(() => [
    { label: 'View', onClick: () => router.push(`/games/${gameId}`), visible: true },
    { label: 'Edit', onClick: handleEdit, visible: canAccess('quick-action.edit') },
    { label: 'Delete', onClick: handleDelete, visible: isAdmin(), destructive: true }
  ].filter(a => a.visible), [gameId, canAccess, isAdmin]);

  return (
    <DropdownMenu>
      {actions.map(action => (
        <DropdownMenuItem
          key={action.label}
          onClick={action.onClick}
          className={action.destructive ? 'text-red-600' : ''}
        >
          {action.label}
        </DropdownMenuItem>
      ))}
    </DropdownMenu>
  );
}
```

---

## Testing the API

### Manual Testing with curl

```bash
# 1. Get JWT token
TOKEN=$(curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  | jq -r '.accessToken')

# 2. Get permissions
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:8080/api/v1/permissions/me \
  | jq .

# 3. Check feature
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=bulk-select" \
  | jq .

# 4. Check with state
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/permissions/check?feature=view-game&state=draft" \
  | jq .
```

### Automated Integration Tests

```csharp
[Fact]
public async Task GetMyPermissions_ReturnsUserPermissions()
{
    // Arrange
    var client = _factory.CreateClient();
    var token = await GetAuthToken(client, "pro@example.com");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Act
    var response = await client.GetAsync("/api/v1/permissions/me");

    // Assert
    response.EnsureSuccessStatusCode();
    var permissions = await response.Content.ReadFromJsonAsync<UserPermissions>();
    Assert.Equal("pro", permissions.Tier);
    Assert.Contains("bulk-select", permissions.AccessibleFeatures);
}

[Fact]
public async Task CheckPermission_WithSufficientTier_ReturnsAllowed()
{
    var client = _factory.CreateClient();
    var token = await GetAuthToken(client, "pro@example.com");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync("/api/v1/permissions/check?feature=bulk-select");

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<PermissionCheckResponse>();
    Assert.True(result.HasAccess);
}
```

---

## Performance Considerations

**Endpoint Latency**:
- `/me`: ~5-20ms (single SELECT query + registry lookup)
- `/check`: ~2-10ms (cached in PermissionRegistry singleton)

**Optimization**:
- PermissionRegistry: Singleton (initialized once)
- Database query: Indexed SELECT on Tier/Role/Status
- Response caching: Consider HybridCache for /me (future)

**Scaling**:
- 10K concurrent users: ~100 req/sec to /me (peak)
- Database: Single SELECT per request (lightweight)
- No N+1 queries (projection used)

---

## API Changelog

**v1.0 (2026-02-12)** - Initial release (Epic #4068)
- `GET /api/v1/permissions/me`: Get user permissions
- `GET /api/v1/permissions/check`: Check feature access
- PermissionRegistry: 10 initial features

**Planned v1.1** (2026-03-15):
- `POST /api/v1/permissions/check/batch`: Batch permission checks
- `GET /api/v1/permissions/features`: List all registered features
- Webhook events for permission changes

**Planned v2.0** (2026-06-01):
- Breaking: Remove deprecated IsSuspended field
- Breaking: Require state parameter for state-based features
- New: Resource-level permission checks (`/check/{resourceType}/{resourceId}`)

---

## References

- Epic #4068: https://github.com/meepleAi-app/meepleai-monorepo/issues/4068
- Issue #4177: Permission Data Model & Schema
- ADR-050: Permission System Architecture
- OpenAPI Spec: http://localhost:8080/scalar/v1 (after `dotnet run`)
