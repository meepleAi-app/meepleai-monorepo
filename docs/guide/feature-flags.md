# Feature Flags Guide

**Version**: 1.0
**Last Updated**: 2025-10-25
**Related Issue**: #473 CONFIG-05

## Overview

The MeepleAI feature flags system enables runtime feature toggling without deployment. Built on CONFIG-01's dynamic configuration infrastructure, it provides fine-grained control over feature availability with role-based access support.

## Key Benefits

- **Zero-Downtime Deployment**: Enable/disable features without restarting services
- **Gradual Rollouts**: Enable features for specific roles before general availability
- **A/B Testing**: Test features with subsets of users
- **Emergency Kill Switches**: Quickly disable problematic features in production
- **Role-Based Access**: Restrict features to Admin, Editor, or specific user roles

## Architecture

### System Components

```
┌─────────────────────────────────────────┐
│     Application Endpoints               │
│  (Streaming, Setup, PDF, Chat, n8n)    │
└──────────────┬──────────────────────────┘
               │ IsEnabledAsync()
               ▼
┌─────────────────────────────────────────┐
│      IFeatureFlagService                │
│  - IsEnabledAsync(name, role?)          │
│  - EnableFeatureAsync()                 │
│  - DisableFeatureAsync()                │
│  - GetAllFeatureFlagsAsync()            │
└──────────────┬──────────────────────────┘
               │ GetValueAsync<bool?>()
               ▼
┌─────────────────────────────────────────┐
│     IConfigurationService (CONFIG-01)   │
│  - Category: "FeatureFlags"             │
│  - HybridCache (5-min TTL)             │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│    system_configurations table          │
│  - Key: "Features.{Name}[.{Role}]"     │
│  - Value: "true" | "false"             │
│  - Category: "FeatureFlags"             │
└─────────────────────────────────────────┘
```

### Hierarchy Rules

1. **Role-Specific Flag** (highest priority)
   - Key format: `Features.{FeatureName}.{Role}`
   - Example: `Features.RagEvaluation.Admin`
   - Overrides global flag for that role

2. **Global Flag** (medium priority)
   - Key format: `Features.{FeatureName}`
   - Example: `Features.ChatExport`
   - Applies to all users unless role-specific override exists

3. **Default: Disabled** (lowest priority, fail-safe)
   - If flag not found in database: returns `false`
   - Prevents accidental enabling of unknown features

## Available Feature Flags

| Feature Flag | Default | Role Restriction | Description |
|--------------|---------|------------------|-------------|
| `Features.StreamingResponses` | ✅ Enabled | None | Server-Sent Events (SSE) streaming for chat responses |
| `Features.SetupGuideGeneration` | ✅ Enabled | None | AI-powered game setup guide generation |
| `Features.PdfUpload` | ✅ Enabled | None | PDF rulebook upload functionality |
| `Features.ChatExport` | ✅ Enabled | None | Chat export to markdown, PDF, or text formats |
| `Features.MessageEditDelete` | ✅ Enabled | None | Message editing and deletion in chat |
| `Features.N8nIntegration` | ✅ Enabled | None | n8n webhook-based agent orchestration |
| `Features.RagEvaluation.Admin` | ❌ Disabled | Admin only | RAG offline evaluation endpoints |
| `Features.AdvancedAdmin.Admin` | ❌ Disabled | Admin only | Advanced admin features and dashboards |

## Usage in Code

### Basic Feature Check

```csharp
public class MyService
{
    private readonly IFeatureFlagService _featureFlags;

    public async Task<IResult> MyEndpoint(HttpContext context)
    {
        // Check if feature is enabled globally
        if (!await _featureFlags.IsEnabledAsync("Features.MyFeature"))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "This feature is currently unavailable" },
                statusCode: 403);
        }

        // Feature is enabled, proceed with normal logic
        // ...
    }
}
```

### Role-Based Feature Check

```csharp
// Check if feature is enabled for a specific role
if (!await _featureFlags.IsEnabledAsync("Features.AdvancedReports", UserRole.Admin))
{
    return Results.Json(
        new { error = "feature_disabled", message = "Advanced reports are only available to administrators" },
        statusCode: 403);
}
```

### In Minimal API Endpoints

```csharp
v1Api.MapPost("/my-endpoint", async (
    IFeatureFlagService featureFlags,
    HttpContext context) =>
{
    // Check feature flag early in the request pipeline
    if (!await featureFlags.IsEnabledAsync("Features.MyFeature"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "Feature unavailable", featureName = "Features.MyFeature" },
            statusCode: 403);
    }

    // ... endpoint logic
});
```

## Admin Management API

### List All Feature Flags

**Endpoint**: `GET /api/v1/admin/features`
**Authorization**: Admin only
**Response**:
```json
{
  "features": [
    {
      "featureName": "Features.ChatExport",
      "isEnabled": true,
      "roleRestriction": null,
      "description": "Enable chat export (markdown, PDF, text)"
    },
    {
      "featureName": "Features.RagEvaluation",
      "isEnabled": false,
      "roleRestriction": "Admin",
      "description": "Enable RAG evaluation endpoints (Admin only)"
    }
  ]
}
```

### Enable/Disable Feature Flag

**Endpoint**: `PUT /api/v1/admin/features/{featureName}`
**Authorization**: Admin only
**Request Body**:
```json
{
  "enabled": true,
  "role": "Admin"  // Optional: for role-specific toggling
}
```

**Examples**:

```bash
# Enable feature globally
curl -X PUT https://api.example.com/api/v1/admin/features/Features.ChatExport \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'

# Disable feature for all users
curl -X PUT https://api.example.com/api/v1/admin/features/Features.StreamingResponses \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'

# Enable feature only for Admin role
curl -X PUT https://api.example.com/api/v1/admin/features/Features.AdvancedReports \
  -H "Content-Type: application/json" \
  -d '{"enabled": true, "role": "Admin"}'
```

## Adding New Feature Flags

### Step 1: Define Feature Flag Key

**Naming Convention**: `Features.{FeatureName}` (PascalCase)

**Examples**:
- `Features.ExperimentalSearch` - Global feature
- `Features.BetaFeatures.Admin` - Admin-only feature
- `Features.ExportToCsv.Editor` - Editor-only feature

### Step 2: Seed Initial Value (Optional)

Add to migration or use admin API:

**Via Migration** (recommended for permanent flags):
```csharp
migrationBuilder.Sql($@"
    INSERT INTO system_configurations (
        ""Id"", ""Key"", ""Value"", ""ValueType"", ""Description"",
        ""Category"", ""IsActive"", ""RequiresRestart"", ""Environment"",
        ""CreatedAt"", ""UpdatedAt"", ""CreatedBy"", ""UpdatedBy""
    ) VALUES (
        gen_random_uuid(),
        'Features.MyNewFeature',
        'false',
        'Boolean',
        'Description of my new feature',
        'FeatureFlags',
        true,
        false,
        'Production',
        NOW(),
        NOW(),
        'system',
        'system'
    );
");
```

**Via Admin API** (recommended for dynamic flags):
```bash
curl -X PUT /api/v1/admin/features/Features.MyNewFeature \
  -d '{"enabled": false}'
```

### Step 3: Protect Endpoint

```csharp
v1Api.MapPost("/my-feature-endpoint", async (
    IFeatureFlagService featureFlags,
    // ... other dependencies
    ) =>
{
    // Add feature check at start of endpoint
    if (!await featureFlags.IsEnabledAsync("Features.MyNewFeature"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "My feature is currently unavailable", featureName = "Features.MyNewFeature" },
            statusCode: 403);
    }

    // ... endpoint implementation
});
```

### Step 4: Document the Flag

Update the "Available Feature Flags" table in this document.

## Best Practices

### When to Use Feature Flags

✅ **Good Use Cases**:
- **New Features**: Gradual rollout to production
- **Experimental Features**: Test with limited audience before GA
- **High-Risk Changes**: Ability to quickly rollback without deployment
- **Role-Specific Features**: Admin-only or Editor-only functionality
- **External Integrations**: Disable third-party features during outages

❌ **Avoid For**:
- **Configuration Parameters**: Use CONFIG-01's general configuration system
- **Performance Tuning**: Use CONFIG-03 (AI/LLM) or CONFIG-04 (RAG) for parameter tuning
- **Permanent Feature Removal**: Remove code instead of keeping disabled flag indefinitely

### Naming Conventions

- **Prefix**: Always start with `Features.`
- **Case**: Use PascalCase (e.g., `Features.ChatExport`, not `features.chat-export`)
- **Role Suffix**: Append role for role-specific flags (e.g., `Features.RagEvaluation.Admin`)
- **Descriptive**: Name should clearly indicate what feature it controls

### Error Responses

Always use consistent error format when feature is disabled:

```json
{
  "error": "feature_disabled",
  "message": "User-friendly explanation of why feature is unavailable",
  "featureName": "Features.ExactFeatureName"
}
```

### Audit Logging

All feature flag state changes are automatically logged with:
- User ID who made the change
- Timestamp of change
- Feature name and new state
- Role restriction (if applicable)

**Example log**:
```
[INFO] Admin a1b2c3d4 enabled feature Features.BetaSearch for Admin role
[INFO] Admin a1b2c3d4 disabled feature Features.ExperimentalRanking
```

### Performance Considerations

- **First Check**: ~5-10ms (database lookup via ConfigurationService)
- **Cached Checks**: <1ms (L1+L2 HybridCache, 5-min TTL)
- **Recommendation**: Feature flag checks add minimal latency (~1-2ms average)

## Troubleshooting

### Feature Flag Not Taking Effect

**Symptom**: Changed feature flag via admin API, but endpoint still shows old behavior.

**Cause**: HybridCache TTL (5 minutes).

**Solution**:
1. Wait 5 minutes for cache to expire, OR
2. Restart API service to clear cache immediately

### Feature Flag Not Found

**Symptom**: `IsEnabledAsync()` returns false for a feature you expect to exist.

**Diagnosis**:
```bash
# Check database for feature flag
psql -d meepleai -c "SELECT * FROM system_configurations WHERE \"Key\" LIKE 'Features.%';"

# Check via admin API
curl https://api.example.com/api/v1/admin/features
```

**Solution**:
- Create flag via admin API: `PUT /api/v1/admin/features/{name}`
- Or add to migration seed data

### Role-Specific Flag Not Working

**Symptom**: Admin user cannot access feature despite `Features.X.Admin` being enabled.

**Diagnosis**: Verify role hierarchy is correct.

**Solution**:
- Check that `UserRole.Admin` is being passed to `IsEnabledAsync()`
- Verify role-specific key format: `Features.{Name}.{Role}` (exact case match)
- Check database: `SELECT * FROM system_configurations WHERE \"Key\" = 'Features.X.Admin';`

### Migration Did Not Seed Flags

**Symptom**: `GET /api/v1/admin/features` returns empty list or missing expected flags.

**Diagnosis**:
```bash
# Check if migration was applied
dotnet ef migrations list --project apps/api/src/Api

# Check database directly
psql -d meepleai -c "SELECT COUNT(*) FROM system_configurations WHERE \"Category\" = 'FeatureFlags';"
```

**Solution**:
```bash
# Apply migration
cd apps/api/src/Api
dotnet ef database update
```

## Integration with CONFIG-01

Feature flags are a specialized use case of CONFIG-01's general configuration system:

- **Storage**: `system_configurations` table with `Category = "FeatureFlags"`
- **Caching**: Inherits HybridCache (L1 in-memory + L2 Redis, 5-min TTL)
- **Admin UI**: Will be managed via CONFIG-06 admin frontend (future)
- **Audit Trail**: All changes logged via `configuration_history` table

## Security

### Access Control

- **Admin Only**: Only users with `UserRole.Admin` can manage feature flags
- **Read Access**: Feature flag checks are internal (service layer), not exposed to end users
- **Audit Trail**: All enable/disable operations logged with user ID

### Validation

- **Key Validation**: Feature flag keys must start with `Features.`
- **Value Validation**: Only boolean values ("true"/"false") accepted
- **Role Validation**: Role must be valid `UserRole` enum value (Admin, Editor, User)

## Future Enhancements (CONFIG-06)

Planned features for frontend admin UI:

- **Visual Dashboard**: Toggle switches for each feature flag
- **Real-Time Preview**: See which endpoints are affected by each flag
- **Percentage Rollouts**: Enable feature for X% of users
- **Time-Based Activation**: Schedule feature launches (e.g., enable at specific date/time)
- **User-Specific Overrides**: Enable features for beta testers
- **Analytics**: Track feature usage and adoption rates
- **Hot Reload**: Apply changes without 5-minute cache delay

## Testing

### Unit Tests

**File**: `apps/api/tests/Api.Tests/FeatureFlagServiceTests.cs`
**Tests**: 22 comprehensive unit tests

**Coverage**:
- Global flag checks (enabled/disabled/not found)
- Role-based hierarchy (role-specific > global > default)
- Enable/disable operations (update existing, create new)
- GetAllFeatureFlagsAsync (parsing, role restriction extraction)
- Edge cases (invalid boolean values, concurrent access)

### Integration Tests

**File**: `apps/api/tests/Api.Tests/FeatureFlagEndpointIntegrationTests.cs`
**Tests**: 10 BDD-style integration tests with Testcontainers

**Coverage**:
- Admin endpoints (list flags, enable/disable, non-admin 403)
- Endpoint protection (streaming, PDF upload, setup guide return 403 when disabled)
- Feature flag persistence across requests
- Role-based access (admin vs user)
- Error response format validation
- Migration seed data verification
- Cache integration

### Running Tests

```bash
# Unit tests (fast, ~100ms)
cd apps/api
dotnet test --filter "FullyQualifiedName~FeatureFlagServiceTests"

# Integration tests (slower, ~7s due to Testcontainers)
dotnet test --filter "FullyQualifiedName~FeatureFlagEndpointIntegrationTests"

# All feature flag tests
dotnet test --filter "FullyQualifiedName~FeatureFlag"
```

## Related Documentation

- **CONFIG-01**: [Dynamic Configuration Infrastructure](../technic/config-01-design.md)
- **CONFIG-06**: Frontend Admin UI (planned)
- **Issue #473**: [CONFIG-05 GitHub Issue](https://github.com/DegrassiAaron/meepleai-monorepo/issues/473)

---

**Generated with Claude Code**
**Co-Authored-By**: Claude <noreply@anthropic.com>
