# Feature Flags Tier-Based Access Matrix

> Complete reference for tier and role-based feature access control

**Last Updated**: 2026-02-06
**Issue**: #3674 - Feature Flags Verification

---

## Overview

Feature flags enable runtime toggling of functionality based on user **role** and **tier**. The system supports three tiers (Free, Normal, Premium) and three roles (User, Editor, Admin).

### Access Logic

```
UserAccess = RoleAccess AND TierAccess

Hierarchy:
- Role-specific flag > Global flag > Default (false)
- Tier-specific flag > Global flag > Default (true for backward compat)
```

### API Endpoints

| Endpoint | Method | Description | Auth |
|----------|--------|-------------|------|
| `/users/me/features` | GET | Current user's available features | User |
| `/admin/feature-flags` | GET | All feature flags | Admin |
| `/admin/feature-flags/{key}` | GET/POST | Manage specific feature | Admin |
| `/admin/feature-flags/{key}/tier/{tier}/enable` | POST | Enable for tier | Admin |
| `/admin/feature-flags/{key}/tier/{tier}/disable` | POST | Disable for tier | Admin |

---

## Feature Matrix

### Core Features

| Feature Key | Description | Free | Normal | Premium | Admin | Issue |
|-------------|-------------|------|--------|---------|-------|-------|
| `Features.PdfUpload` | PDF document upload and processing | ✅ | ✅ | ✅ | ✅ | #3072 |
| `Features.StreamingResponses` | SSE streaming for AI responses | ✅ | ✅ | ✅ | ✅ | - |
| `Features.SetupGuideGeneration` | AI-generated game setup guides | ✅ | ✅ | ✅ | ✅ | - |
| `Features.N8NIntegration` | Workflow automation integration | ❌ | ✅ | ✅ | ✅ | - |

### Advanced Features (Tier-Gated)

**Recommended Tier Configuration** (not yet implemented, for future use):

| Feature Key | Description | Free | Normal | Premium | Admin |
|-------------|-------------|------|--------|---------|-------|
| `Features.AdvancedRAG` | Multi-source RAG search | ❌ | ✅ | ✅ | ✅ |
| `Features.MultiAgent` | Multi-agent AI workflows | ❌ | ❌ | ✅ | ✅ |
| `Features.PdfOCR` | OCR for scanned PDFs | ❌ | ✅ | ✅ | ✅ |
| `Features.DataExport` | Bulk data export | ❌ | ✅ | ✅ | ✅ |
| `Features.ApiAccess` | REST API access | ❌ | ❌ | ✅ | ✅ |
| `Features.CustomModels` | Custom AI model selection | ❌ | ❌ | ✅ | ✅ |
| `Features.PriorityQueue` | Priority AI request queue | ❌ | ❌ | ✅ | ✅ |

### Admin-Only Features

| Feature Key | Description | Free | Normal | Premium | Admin |
|-------------|-------------|------|--------|---------|-------|
| `Features.AdminDashboard` | Admin analytics dashboard | ❌ | ❌ | ❌ | ✅ |
| `Features.UserManagement` | User CRUD operations | ❌ | ❌ | ❌ | ✅ |
| `Features.SystemConfig` | System configuration access | ❌ | ❌ | ❌ | ✅ |
| `Features.AuditLogs` | Full audit log access | ❌ | ❌ | ❌ | ✅ |

---

## Usage Examples

### Check Feature Programmatically

```csharp
// In a handler or service
var canUseFeature = await _featureFlagService.CanAccessFeatureAsync(user, "Features.AdvancedRAG");

if (!canUseFeature)
{
    throw new ForbiddenException("This feature requires Normal tier or higher");
}
```

### Check in Endpoint

```csharp
// Inline check
if (!await featureFlags.IsEnabledAsync("Features.PdfUpload").ConfigureAwait(false))
{
    return Results.Json(
        new { error = "PDF upload feature is currently disabled" },
        statusCode: 503
    );
}
```

### Admin Configuration

```bash
# Enable advanced RAG for Normal tier
POST /api/v1/admin/feature-flags/Features.AdvancedRAG/tier/normal/enable

# Disable multi-agent for all users (maintenance)
POST /api/v1/admin/feature-flags/Features.MultiAgent/toggle?enabled=false
```

### Frontend Usage

```typescript
// Fetch user's available features
const response = await fetch('/api/v1/users/me/features');
const features = await response.json();

// Check if user can access multi-agent
const canUseMultiAgent = features.find(
  f => f.key === 'Features.MultiAgent'
)?.hasAccess;

if (canUseMultiAgent) {
  // Render multi-agent UI
}
```

---

## Configuration Schema

### Database Structure

Feature flags are stored in `system_configurations` table:

```sql
-- Global feature (all tiers/roles)
Key: 'Features.PdfUpload'
Value: 'true' | 'false'
Category: 'FeatureFlags'

-- Tier-specific feature
Key: 'Features.AdvancedRAG.Tier.premium'
Value: 'true' | 'false'
Category: 'FeatureFlags'

-- Role-specific feature
Key: 'Features.AdminDashboard.Admin'
Value: 'true' | 'false'
Category: 'FeatureFlags'
```

### Hierarchy Resolution

1. **Check tier-specific**: `Features.AdvancedRAG.Tier.normal`
2. **Fallback to global**: `Features.AdvancedRAG`
3. **Default**: `true` (backward compatibility)

4. **Check role-specific**: `Features.AdminDashboard.Admin`
5. **Fallback to global**: `Features.AdminDashboard`
6. **Default**: `false` (secure by default)

### Recommended Defaults

```json
{
  "Features.PdfUpload": true,
  "Features.StreamingResponses": true,
  "Features.SetupGuideGeneration": true,
  "Features.N8NIntegration": true,
  "Features.AdvancedRAG.Tier.free": false,
  "Features.AdvancedRAG.Tier.normal": true,
  "Features.AdvancedRAG.Tier.premium": true,
  "Features.MultiAgent.Tier.free": false,
  "Features.MultiAgent.Tier.normal": false,
  "Features.MultiAgent.Tier.premium": true
}
```

---

## Testing

### Integration Tests

See: `tests/Api.Tests/Integration/FeatureFlagTierAccessTests.cs` (if exists)

**Test Cases**:
1. Free user cannot access premium features
2. Normal user can access normal+ features
3. Premium user can access all features
4. Admin bypasses all restrictions
5. Tier upgrade grants new features immediately
6. Feature disable affects all tiers

### Manual Testing

```bash
# 1. Get your features (as logged-in user)
GET http://localhost:8080/api/v1/users/me/features

# 2. Try to use a premium feature as free user (should fail)
# (Depends on specific endpoints with feature checks)

# 3. Admin enables feature for tier
POST http://localhost:8080/api/v1/admin/feature-flags/Features.AdvancedRAG/tier/free/enable

# 4. Verify free user now has access
GET http://localhost:8080/api/v1/users/me/features
# Should show Features.AdvancedRAG with hasAccess: true
```

---

## Migration Path

### Existing Systems

For codebases already using features without tier gates:

1. **Audit Current Usage**: Grep for `IsEnabledAsync` calls
2. **Add Tier Flags**: Use admin endpoints to configure tier access
3. **Update Code**: Replace `IsEnabledAsync` with `CanAccessFeatureAsync` for tier support
4. **Test**: Verify each tier has expected access

### New Features

1. **Design**: Decide which tiers should access the feature
2. **Configure**: Use admin endpoints to set tier flags
3. **Implement**: Use `CanAccessFeatureAsync(user, featureName)` in code
4. **Document**: Add feature to this matrix
5. **Test**: Verify tier-based access works

---

## Related Documentation

- **Service Implementation**: `apps/api/src/Api/Services/FeatureFlagService.cs`
- **Admin Endpoints**: `apps/api/src/Api/Routing/FeatureFlagEndpoints.cs`
- **User Endpoint**: `apps/api/src/Api/Routing/UserProfileEndpoints.cs:430`
- **Original Issue**: #3073 - Tier-Based Feature Flags
- **Verification Issue**: #3674 - Feature Flags Verification
- **Parent Epic**: #3327 - User Flow Gaps

---

*Generated by PM Agent - /sc:pm*
*Epic #3327 Stage 4 - Issue #3674*
