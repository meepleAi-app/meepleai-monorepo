# Rate Limiting Configuration Guide (CONFIG-02)

## Overview

MeepleAI uses a dynamic rate limiting system that reads configuration from the database, providing runtime adjustability without application redeployment.

## Configuration Approach

### Fallback Chain

The `RateLimitService` implements a four-level fallback chain for rate limit resolution:

1. **Database (Role-Specific)**: `RateLimit.MaxTokens.admin`
2. **Database (Global)**: `RateLimit.MaxTokens`
3. **appsettings.json**: `RateLimiting:MaxTokens:admin`
4. **Hardcoded Defaults**: Embedded in code

This ensures backward compatibility while enabling database-driven configuration.

## Configuration Keys

All rate limit configurations use the category `RateLimit`.

### Global Feature Flag

| Key | Type | Description | Default |
|-----|------|-------------|---------|
| `RateLimit.Enabled` | boolean | Enable/disable rate limiting globally | `true` |

### Role-Based Limits

#### MaxTokens (Burst Capacity)

| Key | Type | Description | Default |
|-----|------|-------------|---------|
| `RateLimit.MaxTokens.admin` | integer | Maximum tokens for admin users | 1000 |
| `RateLimit.MaxTokens.editor` | integer | Maximum tokens for editor users | 500 |
| `RateLimit.MaxTokens.user` | integer | Maximum tokens for regular users | 100 |
| `RateLimit.MaxTokens.anonymous` | integer | Maximum tokens for unauthenticated users | 60 |

#### RefillRate (Tokens/Second)

| Key | Type | Description | Default |
|-----|------|-------------|---------|
| `RateLimit.RefillRate.admin` | double | Token refill rate for admin users | 10.0 |
| `RateLimit.RefillRate.editor` | double | Token refill rate for editor users | 5.0 |
| `RateLimit.RefillRate.user` | double | Token refill rate for regular users | 1.0 |
| `RateLimit.RefillRate.anonymous` | double | Token refill rate for unauthenticated users | 1.0 |

## Validation Rules

The system enforces the following validation:

- **Positive Values**: All limits must be > 0
- **MaxTokens Upper Bound**: Capped at 100,000
- **RefillRate Upper Bound**: Capped at 1,000.0

Invalid configurations automatically fallback to hardcoded defaults.

## Migration from appsettings.json

### Current Approach (appsettings.json)

```json
{
  "RateLimiting": {
    "MaxTokens": {
      "admin": 1000,
      "editor": 500
    }
  }
}
```

### New Approach (Database)

Configurations are automatically seeded during migration `20251025122453_AddRateLimitDefaultConfigurations`.

No action required for new deployments. Existing appsettings.json configurations continue to work as fallback.

## Managing Rate Limits

### Via Admin API (CONFIG-06)

Future enhancement. Currently requires direct database updates or admin endpoints from CONFIG-01.

### Direct Database Update

```sql
-- Update admin MaxTokens
UPDATE system_configurations
SET value = '2000',
    version = version + 1,
    updated_at = NOW(),
    updated_by_user_id = '<admin-user-id>'
WHERE configuration_key = 'RateLimit.MaxTokens.admin';

-- Invalidate cache (if using CONFIG-01 cache invalidation endpoint)
POST /api/v1/admin/configurations/cache/invalidate
```

**Important**: Application restart required for configuration changes to take effect.

## Common Scenarios

### Temporary Limit Increase (Events)

1. Increase limits via database/API
2. Restart application
3. After event, restore original limits
4. Restart application

### Role-Specific Customization

```sql
-- Give editors higher limits during content migration
UPDATE system_configurations
SET value = '1000'
WHERE configuration_key = 'RateLimit.MaxTokens.editor';
```

### Disable Rate Limiting (Testing)

```sql
UPDATE system_configurations
SET value = 'false'
WHERE configuration_key = 'RateLimit.Enabled';
```

## Troubleshooting

### Configuration Not Applied

**Symptom**: Changes to database don't affect rate limiting

**Solution**:
1. Verify configuration is active: `is_active = true`
2. Check logs for configuration source: "from DB role-specific"
3. Restart application (configuration read at startup)

### Unexpected Fallback to Defaults

**Symptom**: Logs show "using hardcoded default" despite database config

**Causes**:
- Invalid value (negative, zero)
- Database configuration inactive (`is_active = false`)
- `ConfigurationService` not available
- Configuration key typo

**Debug**:
```sql
-- Check configuration exists and is active
SELECT * FROM system_configurations
WHERE configuration_key LIKE 'RateLimit.%'
AND is_active = true;
```

### Rate Limit Still Enforced After Disabling

**Symptom**: Requests blocked despite `RateLimit.Enabled = false`

**Solution**:
- Verify database value: `SELECT value FROM system_configurations WHERE configuration_key = 'RateLimit.Enabled';`
- Restart application
- Check fallback sources (appsettings.json may override)

## Architecture

### Token Bucket Algorithm

Rate limiting uses a token bucket algorithm:
- **MaxTokens**: Bucket capacity (burst size)
- **RefillRate**: Tokens added per second

Example: `MaxTokens=100, RefillRate=1.0`
- User can make 100 requests instantly (burst)
- Then 1 request/second sustained rate

### Distributed Rate Limiting

- Implementation: Redis-based
- Atomic operations: Lua scripting
- Fail-open: Allow requests if Redis unavailable

## Related Issues

- CONFIG-01: Database-driven configuration system (foundation)
- CONFIG-03: AI/LLM dynamic configuration (similar pattern)
- CONFIG-04: RAG dynamic configuration (similar pattern)
- CONFIG-05: Hot-reload configuration (future enhancement)
- CONFIG-06: Frontend admin UI (future enhancement)

---

**Generated with Claude Code**
**Co-Authored-By:** Claude <noreply@anthropic.com>
