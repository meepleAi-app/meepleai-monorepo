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

## API Endpoints

The UI uses the following API endpoints:

- `GET /api/v1/admin/configurations` - List all configurations
- `GET /api/v1/admin/configurations/{id}` - Get single configuration
- `PUT /api/v1/admin/configurations/{id}` - Update configuration
- `POST /api/v1/admin/configurations/cache/invalidate` - Clear cache

For full API documentation, see `/api/docs` (Swagger UI in development).

## Related Documentation

- [Backend Configuration System](../technic/dynamic-configuration-architecture.md)
- [CONFIG-01: Backend Foundation](../issue/config-01-implementation-summary.md)
- [Feature Flags Guide](./feature-flags.md)
- [Rate Limiting Guide](./rate-limiting.md)
