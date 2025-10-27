# Configuration Management API Reference

> **Base URL**: `http://localhost:8080/api/v1`
> **Authentication**: Session Cookie (Admin role required)
> **Content-Type**: `application/json`

## Overview

The Configuration Management API provides 14 endpoints for managing system-wide dynamic configuration. All endpoints require an active admin session.

**Common Response Codes**:
- `200 OK` - Successful request
- `201 Created` - Resource created successfully
- `204 No Content` - Successful deletion
- `400 Bad Request` - Validation error or invalid input
- `401 Unauthorized` - Not logged in or session expired
- `403 Forbidden` - Not an admin user
- `404 Not Found` - Configuration not found
- `409 Conflict` - Duplicate key/environment combination

---

## Endpoints Summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/admin/configurations` | List all configurations with filters |
| GET | `/admin/configurations/{id}` | Get single configuration by ID |
| GET | `/admin/configurations/key/{key}` | Get configuration by key |
| POST | `/admin/configurations` | Create new configuration |
| PUT | `/admin/configurations/{id}` | Update existing configuration |
| DELETE | `/admin/configurations/{id}` | Delete configuration |
| PATCH | `/admin/configurations/{id}/toggle` | Toggle active status |
| POST | `/admin/configurations/bulk-update` | Update multiple configurations |
| POST | `/admin/configurations/validate` | Validate configuration value |
| GET | `/admin/configurations/export` | Export configurations |
| POST | `/admin/configurations/import` | Import configurations |
| GET | `/admin/configurations/{id}/history` | Get configuration history |
| POST | `/admin/configurations/{id}/rollback/{version}` | Rollback to previous version |
| GET | `/admin/configurations/categories` | List all categories |

---

## 1. List Configurations

### `GET /api/v1/admin/configurations`

List all configurations with optional filtering and pagination.

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `category` | string | No | - | Filter by category (e.g., "RateLimit", "AI") |
| `environment` | string | No | - | Filter by environment ("Development", "Production", "All") |
| `activeOnly` | boolean | No | `true` | If true, return only active configurations |
| `page` | integer | No | `1` | Page number (1-based) |
| `pageSize` | integer | No | `50` | Items per page (max 100) |

**Example Request**:

```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations?category=RateLimit&page=1&pageSize=20" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
{
  "items": [
    {
      "id": "config-ratelimit-admin-maxtokens-prod",
      "key": "RateLimit:Admin:MaxTokens",
      "value": "150",
      "valueType": "int",
      "description": "Maximum tokens per minute for Admin users",
      "category": "RateLimit",
      "isActive": true,
      "requiresRestart": false,
      "environment": "Production",
      "version": 3,
      "previousValue": "120",
      "createdAt": "2025-10-20T10:00:00Z",
      "updatedAt": "2025-10-25T14:30:00Z",
      "createdByUserId": "demo-admin-001",
      "updatedByUserId": "demo-admin-001",
      "lastToggledAt": null
    }
  ],
  "total": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

---

## 2. Get Configuration by ID

### `GET /api/v1/admin/configurations/{id}`

Retrieve a single configuration by its unique identifier.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |

**Example Request**:

```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations/config-ratelimit-admin-maxtokens-prod" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
{
  "id": "config-ratelimit-admin-maxtokens-prod",
  "key": "RateLimit:Admin:MaxTokens",
  "value": "150",
  "valueType": "int",
  "description": "Maximum tokens per minute for Admin users",
  "category": "RateLimit",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production",
  "version": 3,
  "previousValue": "120",
  "createdAt": "2025-10-20T10:00:00Z",
  "updatedAt": "2025-10-25T14:30:00Z",
  "createdByUserId": "demo-admin-001",
  "updatedByUserId": "demo-admin-001",
  "lastToggledAt": null
}
```

**Error Response** (404 Not Found):

```json
{
  "error": "Configuration not found"
}
```

---

## 3. Get Configuration by Key

### `GET /api/v1/admin/configurations/key/{key}`

Retrieve a configuration by its hierarchical key. Respects environment prioritization.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `key` | string | Yes | Configuration key (URL-encoded if contains special chars) |

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | No | Current | Target environment ("Development", "Production") |

**Example Request**:

```bash
# URL-encode the key if it contains colons
curl -X GET "http://localhost:8080/api/v1/admin/configurations/key/RateLimit%3AAdmin%3AMaxTokens?environment=Production" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
{
  "id": "config-ratelimit-admin-maxtokens-prod",
  "key": "RateLimit:Admin:MaxTokens",
  "value": "150",
  "valueType": "int",
  "description": "Maximum tokens per minute for Admin users",
  "category": "RateLimit",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production",
  "version": 3,
  "previousValue": "120",
  "createdAt": "2025-10-20T10:00:00Z",
  "updatedAt": "2025-10-25T14:30:00Z",
  "createdByUserId": "demo-admin-001",
  "updatedByUserId": "demo-admin-001",
  "lastToggledAt": null
}
```

**Error Response** (404 Not Found):

```json
{
  "error": "Configuration not found"
}
```

---

## 4. Create Configuration

### `POST /api/v1/admin/configurations`

Create a new configuration entry. Validates uniqueness of (Key, Environment) combination.

**Request Body**:

```json
{
  "key": "Email:SmtpTimeout",
  "value": "45",
  "valueType": "int",
  "description": "SMTP connection timeout in seconds (1-300)",
  "category": "Email",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production"
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `key` | string | Yes | Hierarchical configuration key (e.g., "Category:Subcategory:Name") |
| `value` | string | Yes | Configuration value (stored as string, deserialized by valueType) |
| `valueType` | string | Yes | Value type: "string", "int", "long", "double", "bool", "json" |
| `description` | string | Yes | Human-readable description for admin UI |
| `category` | string | Yes | Grouping category (e.g., "RateLimit", "AI", "Email") |
| `isActive` | boolean | No (default: true) | Whether configuration is applied |
| `requiresRestart` | boolean | No (default: false) | Whether changing this requires server restart |
| `environment` | string | Yes | Target environment: "Development", "Staging", "Production", "All" |

**Example Request**:

```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{
    "key": "Email:SmtpTimeout",
    "value": "45",
    "valueType": "int",
    "description": "SMTP connection timeout in seconds (1-300)",
    "category": "Email",
    "isActive": true,
    "requiresRestart": false,
    "environment": "Production"
  }'
```

**Example Response** (201 Created):

```json
{
  "id": "config-email-smtp-timeout-prod",
  "key": "Email:SmtpTimeout",
  "value": "45",
  "valueType": "int",
  "description": "SMTP connection timeout in seconds (1-300)",
  "category": "Email",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production",
  "version": 1,
  "previousValue": null,
  "createdAt": "2025-10-27T10:00:00Z",
  "updatedAt": "2025-10-27T10:00:00Z",
  "createdByUserId": "admin-user-123",
  "updatedByUserId": null,
  "lastToggledAt": null
}
```

**Error Responses**:

**409 Conflict** (duplicate key/environment):

```json
{
  "error": "Configuration with key 'Email:SmtpTimeout' already exists for environment 'Production'"
}
```

**400 Bad Request** (validation failure):

```json
{
  "error": "Configuration validation failed: Value '500' exceeds maximum 300"
}
```

---

## 5. Update Configuration

### `PUT /api/v1/admin/configurations/{id}`

Update an existing configuration. Increments version and stores previous value for rollback.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |

**Request Body** (all fields optional):

```json
{
  "value": "60",
  "valueType": "int",
  "description": "Updated description",
  "category": "Email",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production"
}
```

**Example Request**:

```bash
curl -X PUT "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{
    "value": "60",
    "description": "Increased timeout for external SMTP servers"
  }'
```

**Example Response** (200 OK):

```json
{
  "id": "config-email-smtp-timeout-prod",
  "key": "Email:SmtpTimeout",
  "value": "60",
  "valueType": "int",
  "description": "Increased timeout for external SMTP servers",
  "category": "Email",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production",
  "version": 2,
  "previousValue": "45",
  "createdAt": "2025-10-27T10:00:00Z",
  "updatedAt": "2025-10-27T11:30:00Z",
  "createdByUserId": "admin-user-123",
  "updatedByUserId": "admin-user-123",
  "lastToggledAt": null
}
```

**Error Responses**:

**404 Not Found**:

```json
{
  "error": "Configuration not found"
}
```

**400 Bad Request** (validation failure):

```json
{
  "error": "Configuration validation failed: Value '-10' must be non-negative"
}
```

---

## 6. Delete Configuration

### `DELETE /api/v1/admin/configurations/{id}`

Permanently delete a configuration entry. This operation cannot be undone.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |

**Example Request**:

```bash
curl -X DELETE "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod" \
  -H "Cookie: session=<session-cookie>"
```

**Example Response** (204 No Content):

```
(empty response body)
```

**Error Response** (404 Not Found):

```json
{
  "error": "Configuration not found"
}
```

---

## 7. Toggle Configuration

### `PATCH /api/v1/admin/configurations/{id}/toggle`

Toggle a configuration's active status without modifying other fields.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `isActive` | boolean | Yes | New active status |

**Example Request**:

```bash
curl -X PATCH "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod/toggle?isActive=false" \
  -H "Cookie: session=<session-cookie>"
```

**Example Response** (200 OK):

```json
{
  "id": "config-email-smtp-timeout-prod",
  "key": "Email:SmtpTimeout",
  "value": "60",
  "valueType": "int",
  "description": "SMTP connection timeout in seconds (1-300)",
  "category": "Email",
  "isActive": false,
  "requiresRestart": false,
  "environment": "Production",
  "version": 2,
  "previousValue": "45",
  "createdAt": "2025-10-27T10:00:00Z",
  "updatedAt": "2025-10-27T12:00:00Z",
  "createdByUserId": "admin-user-123",
  "updatedByUserId": "admin-user-123",
  "lastToggledAt": "2025-10-27T12:00:00Z"
}
```

**Error Response** (404 Not Found):

```json
{
  "error": "Configuration not found"
}
```

---

## 8. Bulk Update Configurations

### `POST /api/v1/admin/configurations/bulk-update`

Update multiple configurations atomically in a single transaction. All updates succeed or all fail.

**Request Body**:

```json
{
  "updates": [
    {
      "id": "config-ratelimit-admin-maxtokens-prod",
      "value": "200"
    },
    {
      "id": "config-ratelimit-editor-maxtokens-prod",
      "value": "150"
    },
    {
      "id": "config-ratelimit-user-maxtokens-prod",
      "value": "100"
    }
  ]
}
```

**Field Descriptions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `updates` | array | Yes | Array of configuration updates |
| `updates[].id` | string | Yes | Configuration ID to update |
| `updates[].value` | string | Yes | New value |

**Example Request**:

```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations/bulk-update" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{
    "updates": [
      { "id": "config-ratelimit-admin-maxtokens-prod", "value": "200" },
      { "id": "config-ratelimit-editor-maxtokens-prod", "value": "150" },
      { "id": "config-ratelimit-user-maxtokens-prod", "value": "100" }
    ]
  }'
```

**Example Response** (200 OK):

```json
[
  {
    "id": "config-ratelimit-admin-maxtokens-prod",
    "key": "RateLimit:Admin:MaxTokens",
    "value": "200",
    "valueType": "int",
    "version": 4,
    "updatedAt": "2025-10-27T12:30:00Z"
  },
  {
    "id": "config-ratelimit-editor-maxtokens-prod",
    "key": "RateLimit:Editor:MaxTokens",
    "value": "150",
    "valueType": "int",
    "version": 3,
    "updatedAt": "2025-10-27T12:30:00Z"
  },
  {
    "id": "config-ratelimit-user-maxtokens-prod",
    "key": "RateLimit:User:MaxTokens",
    "value": "100",
    "valueType": "int",
    "version": 2,
    "updatedAt": "2025-10-27T12:30:00Z"
  }
]
```

**Error Response** (400 Bad Request):

```json
{
  "error": "Validation failed for RateLimit:Admin:MaxTokens: Value '999999' exceeds maximum 10000"
}
```

**Note**: If any update fails validation, the entire transaction is rolled back.

---

## 9. Validate Configuration

### `POST /api/v1/admin/configurations/validate`

Validate a configuration value before creating or updating. Does not persist changes.

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `key` | string | Yes | Configuration key |
| `value` | string | Yes | Value to validate |
| `valueType` | string | Yes | Value type (string/int/long/double/bool/json) |

**Example Request**:

```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations/validate?key=Email:SmtpTimeout&value=500&valueType=int" \
  -H "Cookie: session=<session-cookie>"
```

**Example Response** (200 OK - Valid):

```json
{
  "isValid": true,
  "errors": []
}
```

**Example Response** (200 OK - Invalid):

```json
{
  "isValid": false,
  "errors": [
    "Value '500' must be between 1 and 300"
  ]
}
```

---

## 10. Export Configurations

### `GET /api/v1/admin/configurations/export`

Export all configurations for a specific environment as JSON. Useful for backup and migration.

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `environment` | string | Yes | - | Target environment to export |
| `activeOnly` | boolean | No | `true` | If true, export only active configurations |

**Example Request**:

```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations/export?environment=Production&activeOnly=true" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
{
  "configurations": [
    {
      "id": "config-ratelimit-admin-maxtokens-prod",
      "key": "RateLimit:Admin:MaxTokens",
      "value": "200",
      "valueType": "int",
      "description": "Maximum tokens per minute for Admin users",
      "category": "RateLimit",
      "isActive": true,
      "requiresRestart": false,
      "environment": "Production",
      "version": 4,
      "createdAt": "2025-10-20T10:00:00Z",
      "updatedAt": "2025-10-27T12:30:00Z"
    }
  ],
  "exportedAt": "2025-10-27T13:00:00Z",
  "environment": "Production"
}
```

---

## 11. Import Configurations

### `POST /api/v1/admin/configurations/import`

Import configurations from an export file. Can optionally overwrite existing configurations.

**Request Body**:

```json
{
  "configurations": [
    {
      "key": "RateLimit:Admin:MaxTokens",
      "value": "250",
      "valueType": "int",
      "description": "Maximum tokens per minute for Admin users",
      "category": "RateLimit",
      "isActive": true,
      "requiresRestart": false,
      "environment": "Production"
    }
  ],
  "overwriteExisting": true
}
```

**Field Descriptions**:

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `configurations` | array | Yes | - | Array of configurations to import |
| `overwriteExisting` | boolean | No | `false` | If true, update existing configurations; if false, skip existing |

**Example Request**:

```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations/import" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{
    "configurations": [
      {
        "key": "RateLimit:Admin:MaxTokens",
        "value": "250",
        "valueType": "int",
        "description": "Maximum tokens per minute for Admin users",
        "category": "RateLimit",
        "isActive": true,
        "requiresRestart": false,
        "environment": "Production"
      }
    ],
    "overwriteExisting": true
  }'
```

**Example Response** (200 OK):

```json
{
  "importedCount": 15
}
```

**Error Response** (400 Bad Request):

```json
{
  "error": "Import failed: Validation error for RateLimit:Admin:MaxTokens"
}
```

---

## 12. Get Configuration History

### `GET /api/v1/admin/configurations/{id}/history`

Retrieve change history for a specific configuration. Shows version progression and changes.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `limit` | integer | No | `20` | Maximum number of history entries to return |

**Example Request**:

```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations/config-ratelimit-admin-maxtokens-prod/history?limit=10" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
[
  {
    "id": "history-123456",
    "configurationId": "config-ratelimit-admin-maxtokens-prod",
    "key": "RateLimit:Admin:MaxTokens",
    "oldValue": "200",
    "newValue": "250",
    "version": 5,
    "changedAt": "2025-10-27T14:00:00Z",
    "changedByUserId": "admin-user-123",
    "changeReason": "Configuration updated"
  },
  {
    "id": "history-123455",
    "configurationId": "config-ratelimit-admin-maxtokens-prod",
    "key": "RateLimit:Admin:MaxTokens",
    "oldValue": "150",
    "newValue": "200",
    "version": 4,
    "changedAt": "2025-10-27T12:30:00Z",
    "changedByUserId": "admin-user-123",
    "changeReason": "Configuration updated"
  }
]
```

**Note**: Current implementation shows simplified history (current and previous version). Full audit trail requires separate `configuration_history` table (future enhancement).

---

## 13. Rollback Configuration

### `POST /api/v1/admin/configurations/{id}/rollback/{version}`

Rollback a configuration to a previous version. Swaps current value with previous value.

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | string | Yes | Configuration unique identifier |
| `version` | integer | Yes | Version number to rollback to |

**Example Request**:

```bash
curl -X POST "http://localhost:8080/api/v1/admin/configurations/config-ratelimit-admin-maxtokens-prod/rollback/4" \
  -H "Cookie: session=<session-cookie>"
```

**Example Response** (200 OK):

```json
{
  "id": "config-ratelimit-admin-maxtokens-prod",
  "key": "RateLimit:Admin:MaxTokens",
  "value": "200",
  "valueType": "int",
  "description": "Maximum tokens per minute for Admin users",
  "category": "RateLimit",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production",
  "version": 6,
  "previousValue": "250",
  "createdAt": "2025-10-20T10:00:00Z",
  "updatedAt": "2025-10-27T14:30:00Z",
  "createdByUserId": "admin-user-123",
  "updatedByUserId": "admin-user-123",
  "lastToggledAt": null
}
```

**Error Responses**:

**404 Not Found**:

```json
{
  "error": "Configuration not found"
}
```

**400 Bad Request** (no previous value):

```json
{
  "error": "No previous value available for rollback"
}
```

**Note**: Current implementation only supports rollback to immediate previous value. Full version history rollback requires separate `configuration_history` table.

---

## 14. Get Categories

### `GET /api/v1/admin/configurations/categories`

List all unique configuration categories in the system.

**Example Request**:

```bash
curl -X GET "http://localhost:8080/api/v1/admin/configurations/categories" \
  -H "Cookie: session=<session-cookie>" \
  -H "Accept: application/json"
```

**Example Response** (200 OK):

```json
[
  "AI",
  "Email",
  "FeatureFlags",
  "PDF",
  "RateLimit",
  "Rag",
  "TextChunking"
]
```

---

## Authentication

All endpoints require an active admin session. Authentication is handled via session cookies.

**Login Flow**:

1. **Login**: `POST /api/v1/auth/login` with email and password
2. **Session Cookie**: Server returns `Set-Cookie: session=<token>; HttpOnly; Secure`
3. **Subsequent Requests**: Include cookie in requests

**Example Login**:

```bash
# Step 1: Login
curl -X POST "http://localhost:8080/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@meepleai.dev",
    "password": "Demo123!"
  }' \
  -c cookies.txt  # Save cookies to file

# Step 2: Use session cookie
curl -X GET "http://localhost:8080/api/v1/admin/configurations" \
  -b cookies.txt  # Load cookies from file
```

**Session Expiration**:
- Default TTL: 30 days
- Auto-revocation after 30 days of inactivity
- Manual logout: `POST /api/v1/auth/logout`

---

## Error Handling

### Standard Error Format

All error responses follow this JSON structure:

```json
{
  "error": "Human-readable error message"
}
```

### Common Error Scenarios

**401 Unauthorized** (not logged in):

```json
{
  "error": "Unauthorized"
}
```

**403 Forbidden** (not admin):

```json
{
  "error": "Forbidden"
}
```

**400 Bad Request** (validation):

```json
{
  "error": "Configuration validation failed: Value 'abc' is not a valid integer"
}
```

**409 Conflict** (duplicate):

```json
{
  "error": "Configuration with key 'RateLimit:Admin:MaxTokens' already exists for environment 'Production'"
}
```

**404 Not Found**:

```json
{
  "error": "Configuration not found"
}
```

---

## Pagination

Endpoints that return lists support pagination via query parameters.

**Pagination Parameters**:
- `page` (integer, default: 1) - Page number (1-based)
- `pageSize` (integer, default: 50, max: 100) - Items per page

**Pagination Response**:

```json
{
  "items": [...],
  "total": 150,
  "page": 2,
  "pageSize": 50,
  "totalPages": 3
}
```

**Calculating Total Pages**: `totalPages = ceil(total / pageSize)`

---

## Rate Limiting

Configuration API endpoints are subject to rate limiting based on user role.

**Default Limits** (per minute):
- Admin: 120 requests
- Editor: 90 requests
- User: 60 requests

**Rate Limit Headers**:

```http
X-RateLimit-Limit: 120
X-RateLimit-Remaining: 115
X-RateLimit-Reset: 1635774600
```

**429 Too Many Requests**:

```json
{
  "error": "Rate limit exceeded. Try again in 30 seconds."
}
```

---

## Cache Invalidation

After creating, updating, or deleting configurations, the system automatically invalidates the HybridCache (L1 in-memory + L2 Redis).

**Cache Behavior**:
- **Create/Update/Delete**: Automatic cache invalidation for affected key
- **Bulk Update**: Invalidates all affected keys
- **Toggle**: Invalidates cache for toggled configuration
- **Manual Invalidation**: Use `InvalidateCacheAsync(key)` in code

**Cache TTL**: 5 minutes (L1 + L2)

---

## Examples

### Complete Workflow: Create, Update, Rollback

```bash
# 1. Create configuration
curl -X POST "http://localhost:8080/api/v1/admin/configurations" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{
    "key": "Email:SmtpTimeout",
    "value": "30",
    "valueType": "int",
    "description": "SMTP timeout in seconds",
    "category": "Email",
    "isActive": true,
    "requiresRestart": false,
    "environment": "Production"
  }'
# Response: { "id": "config-email-smtp-timeout-prod", "version": 1, ... }

# 2. Update value
curl -X PUT "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d '{ "value": "60" }'
# Response: { "id": "config-email-smtp-timeout-prod", "version": 2, "previousValue": "30", ... }

# 3. Verify history
curl -X GET "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod/history" \
  -H "Cookie: session=<session-cookie>"
# Response: [{ "oldValue": "30", "newValue": "60", "version": 2, ... }]

# 4. Rollback to previous version
curl -X POST "http://localhost:8080/api/v1/admin/configurations/config-email-smtp-timeout-prod/rollback/1" \
  -H "Cookie: session=<session-cookie>"
# Response: { "id": "config-email-smtp-timeout-prod", "version": 3, "value": "30", ... }
```

### Export and Import

```bash
# Export Production configurations
curl -X GET "http://localhost:8080/api/v1/admin/configurations/export?environment=Production" \
  -H "Cookie: session=<session-cookie>" \
  -o production-config.json

# Import to Staging (after editing environment in JSON)
curl -X POST "http://localhost:8080/api/v1/admin/configurations/import" \
  -H "Cookie: session=<session-cookie>" \
  -H "Content-Type: application/json" \
  -d @staging-config.json
```

---

## Related Documentation

- **Architecture**: [docs/technic/dynamic-configuration-architecture.md](../technic/dynamic-configuration-architecture.md)
- **Admin Guide**: [docs/guide/admin-configuration.md](../guide/admin-configuration.md)
- **AI/LLM Config**: [docs/issue/config-03-ai-llm-configuration-guide.md](../issue/config-03-ai-llm-configuration-guide.md)
- **Database Schema**: [docs/database-schema.md](../database-schema.md)

---

**Document Version**: 1.0.0
**Generated with Claude Code**
**Co-Authored-By**: Claude <noreply@anthropic.com>
