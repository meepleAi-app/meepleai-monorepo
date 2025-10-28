# Admin Configuration Management Guide

## Overview

The Admin Configuration Management UI provides a comprehensive interface for viewing and modifying system configurations without requiring database access or server redeployment.

**Access**: `/admin/configuration` (Admin role required)

## Features

### 🚩 Feature Flags
- Toggle features on/off at runtime
- Real-time preview of active features
- Confirmation dialogs for critical features
- Visual indicators for enabled/disabled states

### ⚡ Rate Limiting
- Configure API rate limits per role
- Adjust burst sizes and request thresholds
- Role-based configuration overrides

### 🤖 AI / LLM
- Adjust AI model parameters (temperature, max tokens)
- Model selection and configuration
- Response quality tuning

### 🔍 RAG (Retrieval-Augmented Generation)
- Configure vector search parameters
- Adjust chunk size and overlap
- ⚠️ Changes to ChunkSize/VectorDimensions require re-indexing

## Getting Started

### Accessing the UI

1. Login as an admin user
2. Navigate to Admin Panel
3. Click "Configuration" or visit `/admin/configuration`

### Understanding the Interface

**Tab Navigation**: Four main categories
- Feature Flags: Runtime feature toggles
- Rate Limiting: API throttling settings
- AI/LLM: AI model parameters
- RAG: Vector search configuration

**Restart Banner**: Sticky reminder at the top indicates that configuration changes require server restart

**Stats Footer**: Shows total configurations, active count, and category count

## Common Tasks

### Toggle a Feature Flag

1. Navigate to "Feature Flags" tab
2. Find the desired feature
3. Click the toggle switch
4. Confirm if prompted (for critical features)
5. ✅ Success toast appears
6. Feature status updates in real-time preview

### Edit a Configuration Value

1. Navigate to the appropriate tab (Rate Limiting, AI/LLM, or RAG)
2. Find the configuration to edit
3. Click "Edit" button
4. Modify the value in the input field
5. Click "Save" (or "Cancel" to abort)
6. Confirm if it's a destructive change
7. ✅ Configuration updated

### Invalidate Cache

1. Click "Clear Cache" button in the header
2. Cached configurations are invalidated
3. Next requests will fetch fresh values from database

### Reload Configurations

1. Click "Reload" button in the header
2. All configurations are fetched from database
3. UI updates with latest values

## Configuration Categories

### Feature Flags
| Key | Description | Default | Restart Required |
|-----|-------------|---------|------------------|
| Features:RagCaching | Enable AI response caching | true | No |
| Features:StreamingResponses | Enable SSE streaming for QA | true | Yes |
| Features:SetupGuide | Enable setup guide generation | true | No |
| Features:AdvancedSearch | Enable advanced vector search | false | No |

### Rate Limiting
| Key | Description | Default | Type |
|-----|-------------|---------|------|
| RateLimiting:RequestsPerMinute | Global requests per minute | 60 | integer |
| RateLimiting:Admin:RequestsPerMinute | Admin role requests per minute | 120 | integer |
| RateLimiting:Editor:RequestsPerMinute | Editor role requests per minute | 90 | integer |
| RateLimiting:User:RequestsPerMinute | User role requests per minute | 60 | integer |

### AI/LLM
| Key | Description | Range | Default |
|-----|-------------|-------|---------|
| Ai:DefaultTemperature | AI response creativity | 0.0 - 1.0 | 0.7 |
| Ai:DefaultMaxTokens | Maximum response length | 1 - 32000 | 4000 |
| Ai:DefaultModel | AI model to use | - | gpt-4 |
| Ai:TopP | Nucleus sampling parameter | 0.0 - 1.0 | 1.0 |

### RAG
| Key | Description | Range | Destructive |
|-----|-------------|-------|-------------|
| Rag:TopK | Number of search results | 1 - 50 | No |
| Rag:MinScore | Minimum similarity score | 0.0 - 1.0 | No |
| Rag:ChunkSize | Text chunk size | 100 - 2000 | ⚠️ Yes |
| Rag:ChunkOverlap | Overlap between chunks | 0 - 500 | ⚠️ Yes |
| Rag:VectorDimensions | Embedding vector size | - | ⚠️ Yes |

## Important Notes

### ⚠️ Destructive Changes

Changes to the following keys require **re-indexing all vector documents**:
- `Rag:ChunkSize`
- `Rag:ChunkOverlap`
- `Rag:VectorDimensions`

The UI will show a confirmation dialog before saving these changes. Re-indexing may take significant time depending on document count.

### 🔄 Restart Required

Most configuration changes require a server restart to take effect. The sticky banner at the top reminds you of this requirement.

**To restart the server**:
```bash
# Docker Compose
cd infra && docker compose restart api

# Direct dotnet
cd apps/api/src/Api && dotnet run
```

### 🔒 Inactive Configurations

Configurations marked as "Inactive" cannot be toggled or edited until activated in the database. This provides a safety mechanism for experimental or deprecated settings.

## Security Considerations

- ❌ **Do NOT** store sensitive data (API keys, passwords) in the configuration system
- ✅ All configuration changes are logged in the audit trail
- ✅ Only Admin role can access this UI
- ✅ Destructive changes require explicit confirmation

## Troubleshooting

### Configuration Not Taking Effect

**Problem**: Changed a configuration but the system still uses old value

**Solution**:
1. Verify the configuration is marked as "Active"
2. Check if "Requires Restart" is yes
3. Restart the API server
4. Clear configuration cache via UI

### Validation Errors

**Problem**: Cannot save configuration value

**Solutions**:
- Check value is within allowed range (e.g., Temperature 0.0-1.0)
- Verify value type matches (integer for counts, float for decimals)
- Ensure value is not empty
- Check for syntax errors in JSON values

### Access Denied (403)

**Problem**: Cannot access `/admin/configuration` page

**Solution**:
- Verify you are logged in as Admin role
- Check session is still valid
- Clear browser cookies and login again

### Changes Not Visible

**Problem**: UI shows old values after update

**Solutions**:
1. Click "Reload" button to refresh from database
2. Click "Clear Cache" to invalidate cached values
3. Hard refresh page (Ctrl+F5)

## Best Practices

1. **Test in staging first**: Always test configuration changes in non-production environment
2. **Document changes**: Use the description field to explain why a change was made
3. **Backup before major changes**: Export configurations before making significant changes
4. **Monitor after changes**: Check system logs and metrics after configuration updates
5. **Small incremental changes**: Change one thing at a time for easier troubleshooting

## Troubleshooting

### Configuration Not Taking Effect

**Problem**: Changed a configuration but the system still uses old value

**Solution**:
1. Verify the configuration is marked as "Active" (check `IsActive` field)
2. Check if "Requires Restart" is yes → Restart the API server
3. Clear configuration cache via UI or endpoint
4. Verify environment matches (Development/Staging/Production)
5. Check logs for configuration loading errors

**Verification**:
```sql
-- Check active configuration
SELECT "Key", "Value", "IsActive", "Environment", "UpdatedAt"
FROM system_configurations
WHERE "Key" = 'RateLimit:Admin:MaxTokens'
  AND "IsActive" = true;
```

### Validation Errors

**Problem**: Cannot save configuration value

**Solutions**:
- **Out of Range**: Check value is within allowed range (e.g., Temperature 0.0-1.0)
- **Type Mismatch**: Verify value type matches (integer for counts, double for decimals)
- **Empty Value**: Ensure value is not empty or null
- **JSON Syntax**: Check for syntax errors in JSON values

**Common Validation Rules**:

| Configuration | Type | Valid Range | Example |
|---------------|------|-------------|---------|
| `AI:Temperature` | double | 0.0 - 2.0 | 0.7 |
| `AI:MaxTokens` | int | 1 - 32000 | 4000 |
| `RateLimit:*:MaxTokens` | int | > 0 | 100 |
| `Rag:TopK` | int | 1 - 50 | 5 |
| `Rag:MinScore` | double | 0.0 - 1.0 | 0.7 |

### Access Denied (403)

**Problem**: Cannot access `/admin/configuration` page

**Solution**:
- Verify you are logged in as Admin role (not Editor or User)
- Check session is still valid (not expired)
- Clear browser cookies and login again
- Verify user role in database:

```sql
SELECT "Email", "Role" FROM users WHERE "Email" = 'your-email@example.com';
```

### Changes Not Visible

**Problem**: UI shows old values after update

**Solutions**:
1. Click "Reload" button to refresh from database
2. Click "Clear Cache" to invalidate cached values
3. Hard refresh page (Ctrl+F5 or Cmd+Shift+R)
4. Check browser console for JavaScript errors
5. Verify API endpoint is responding (check Network tab)

### Performance Issues

**Problem**: Configuration UI is slow or timing out

**Solutions**:
- **Database Connection**: Check PostgreSQL is running and responsive
- **Redis Connection**: Verify Redis is running for HybridCache
- **Large Result Set**: Use category or environment filters to reduce data
- **Network Latency**: Check API server connectivity
- **Session Expiration**: Re-login if session has expired

**Diagnosis**:
```bash
# Check database connection
docker compose ps postgres

# Check Redis connection
docker compose ps redis

# Test API endpoint
curl -b cookies.txt http://localhost:8080/api/v1/admin/configurations?pageSize=5

# View API logs
docker compose logs -f api | grep "admin/configurations"
```

## Security Considerations

### What NOT to Store in Configuration System

**❌ Never store**:
- API keys (use environment variables or secrets manager)
- Passwords (use secure credential storage)
- Private keys or certificates
- Database connection strings (use environment variables)
- OAuth client secrets
- Encryption keys

**✅ Safe to store**:
- Feature flags (boolean toggles)
- Rate limits (numeric thresholds)
- AI model parameters (temperature, max_tokens)
- RAG search parameters (TopK, MinScore)
- Timeout values (seconds)
- UI configuration (theme, language)

### Audit Trail

All configuration changes are logged with:
- **Who**: `CreatedByUserId`, `UpdatedByUserId`
- **What**: `Key`, `Value`, `PreviousValue`
- **When**: `CreatedAt`, `UpdatedAt`
- **Version**: `Version` number (incremented on each change)

**Query Audit Trail**:
```sql
SELECT
    sc."Key",
    sc."Value",
    sc."PreviousValue",
    sc."Version",
    sc."UpdatedAt",
    u."Email" AS "UpdatedBy"
FROM system_configurations sc
LEFT JOIN users u ON sc."UpdatedByUserId" = u."Id"
WHERE sc."Category" = 'RateLimit'
ORDER BY sc."UpdatedAt" DESC
LIMIT 20;
```

### Access Control

- **Admin UI**: Admin role required (enforced by ActiveSession middleware)
- **API Endpoints**: Admin role required (403 Forbidden for non-admins)
- **Database Direct Access**: Requires PostgreSQL credentials (restricted)
- **Redis Cache**: Internal access only (not exposed publicly)

## Backup & Restore Procedures

### Manual Backup (Export)

**Via Admin UI**:
1. Navigate to `/admin/configuration`
2. Click "Export" button
3. Select environment (Production/Development)
4. Choose "Active Only" or "All"
5. Download JSON file

**Via API**:
```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations/export?environment=Production&activeOnly=true" \
  -H "Cookie: session=<session-cookie>" \
  -o backup-prod-$(date +%Y%m%d).json
```

**Via SQL**:
```sql
COPY (
    SELECT * FROM system_configurations
    WHERE "Environment" = 'Production' AND "IsActive" = true
) TO '/tmp/config-backup.csv' WITH CSV HEADER;
```

### Manual Restore (Import)

**Via Admin UI**:
1. Navigate to `/admin/configuration`
2. Click "Import" button
3. Upload JSON file
4. Choose "Overwrite Existing" option
5. Confirm import

**Via API**:
```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations/import" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d @backup-prod-20251027.json
```

**Via SQL** (manual recovery):
```sql
-- Restore specific configuration
UPDATE system_configurations
SET "Value" = '100',
    "Version" = "Version" + 1,
    "UpdatedAt" = NOW()
WHERE "Key" = 'RateLimit:Admin:MaxTokens'
  AND "Environment" = 'Production';
```

### Automated Backup Strategy

**Recommended Approach**:
1. Daily automated exports to external storage (S3, Azure Blob, etc.)
2. Retention policy: 30 days
3. Separate backups per environment
4. Include in database backup strategy

**Example Backup Script** (bash):
```bash
#!/bin/bash
# config-backup.sh

DATE=$(date +%Y%m%d)
ENVIRONMENTS=("Production" "Staging" "Development")

for ENV in "${ENVIRONMENTS[@]}"; do
    curl -X GET "http://localhost:8080/api/v1/admin/configurations/export?environment=$ENV&activeOnly=true" \
      -H "Cookie: session=$SESSION_COOKIE" \
      -o "config-backup-$ENV-$DATE.json"

    # Upload to S3 or cloud storage
    # aws s3 cp "config-backup-$ENV-$DATE.json" s3://backups/configs/
done
```

## Performance Tuning Guidelines

### Cache Optimization

**Default Cache Settings**:
- L1 (in-memory): 5 minutes TTL
- L2 (Redis): 5 minutes TTL
- Cache invalidation: Manual via UI or automatic on update

**When to Adjust**:
- **Increase TTL** (10-15 minutes): Rarely changing configurations, performance priority
- **Decrease TTL** (1-2 minutes): Frequently updated configurations, consistency priority
- **Disable Cache**: Development/testing environments only

**How to Adjust** (code change required):
```csharp
// ConfigurationService.cs line 22
private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(10); // Increased from 5
```

### Database Optimization

**Index Usage**:
- Queries by `Key` and `Environment` → Use composite index (fast)
- Queries by `Category` → Use category index (fast)
- Queries by `Description` → Full table scan (slow, avoid)

**Pagination**:
- Use `pageSize` parameter to limit results (default: 50, max: 100)
- Filter by `category` or `environment` to reduce result set

**Connection Pooling**:
- Default pool size: 100 connections
- Monitor active connections: `SELECT count(*) FROM pg_stat_activity;`

### API Performance

**Request Latency** (typical):
- Cached request: < 1ms
- Database query: 5-10ms
- Bulk operations: 50-100ms

**Rate Limiting**:
- Admin: 120 requests/minute
- Consider increasing for automated scripts

## Common Scenarios

### Scenario 1: Enable New Feature Flag

**Steps**:
1. Navigate to "Feature Flags" tab
2. Find feature (e.g., "Features:AdvancedSearch")
3. Toggle switch to enable
4. ✅ Feature is now active system-wide
5. Monitor application logs for feature usage

### Scenario 2: Adjust Rate Limit for High-Traffic Users

**Steps**:
1. Navigate to "Rate Limiting" tab
2. Find "RateLimit:Admin:MaxTokens"
3. Click "Edit"
4. Change value from 100 to 200
5. Click "Save"
6. Restart API server (if RequiresRestart = true)
7. Verify new limit is applied

### Scenario 3: Tune AI Model Temperature

**Steps**:
1. Navigate to "AI/LLM" tab
2. Find "AI:Temperature"
3. Click "Edit"
4. Change value (e.g., 0.3 → 0.7 for more creative responses)
5. Click "Save"
6. Test AI responses to verify change

### Scenario 4: Rollback Bad Configuration

**Steps**:
1. Identify problematic configuration
2. Navigate to configuration detail page
3. View "History" tab
4. Click "Rollback" on previous version
5. Confirm rollback
6. Verify system behavior restored

### Scenario 5: Migrate Configuration Between Environments

**Steps**:
1. Export Production configurations (JSON)
2. Edit JSON file:
   - Change `environment` field from "Production" to "Staging"
   - Optionally adjust values for staging
3. Import to Staging environment
4. Test thoroughly before promoting back to Production

## API Endpoints

The UI uses the following API endpoints:

- `GET /api/v1/admin/configurations` - List all configurations
- `GET /api/v1/admin/configurations/{id}` - Get single configuration
- `GET /api/v1/admin/configurations/key/{key}` - Get by key
- `POST /api/v1/admin/configurations` - Create configuration
- `PUT /api/v1/admin/configurations/{id}` - Update configuration
- `DELETE /api/v1/admin/configurations/{id}` - Delete configuration
- `PATCH /api/v1/admin/configurations/{id}/toggle` - Toggle active status
- `POST /api/v1/admin/configurations/bulk-update` - Bulk update
- `POST /api/v1/admin/configurations/validate` - Validate value
- `GET /api/v1/admin/configurations/export` - Export configurations
- `POST /api/v1/admin/configurations/import` - Import configurations
- `GET /api/v1/admin/configurations/{id}/history` - View history
- `POST /api/v1/admin/configurations/{id}/rollback/{version}` - Rollback
- `GET /api/v1/admin/configurations/categories` - List categories

For complete API documentation with request/response examples, see [Configuration API Reference](../api/configuration-endpoints.md).

## Related Documentation

- **Architecture**: [Dynamic Configuration Architecture](../technic/dynamic-configuration-architecture.md)
- **API Reference**: [Configuration Endpoints](../api/configuration-endpoints.md)
- **AI/LLM Configuration**: [CONFIG-03 Implementation Guide](../issue/config-03-ai-llm-configuration-guide.md)
- **Database Schema**: [Database Schema Documentation](../database-schema.md)
- **Feature Flags**: [Feature Flag Service](./feature-flags.md)
- **Rate Limiting**: [Rate Limiting Guide](./rate-limiting.md)
