# n8n Template Library Guide (N8N-04)

**Feature ID**: N8N-04
**Status**: Implemented
**Version**: 1.0.0

## Overview

The n8n Template Library provides 12+ pre-built workflow templates for common automation scenarios. Users can browse templates, configure parameters, and import directly into their n8n instance through the admin UI.

## Features

### Template Categories

**Integration** (4 templates):
- BGG Game Metadata Sync
- Slack New User Registration Alert
- Discord Game Upload Notifications
- Weekly Analytics Export to Google Sheets

**Automation** (4 templates):
- PDF Processing Complete Email
- Database Backup to S3
- Daily Usage Report Email
- Welcome Email Sequence

**Monitoring** (2 templates):
- API Error Alert System
- System Health Monitor

**Data Processing** (1 template):
- Automated PDF Processing Pipeline

### Key Capabilities

1. **Template Browsing**: Category filtering, search, metadata display
2. **Parameter Configuration**: Dynamic form generation based on template parameters
3. **Validation**: Client-side and server-side parameter validation
4. **n8n Integration**: Direct workflow import via n8n REST API
5. **Security**: Parameter encryption for sensitive values (API keys, passwords)

## Architecture

### Backend Components

**N8nTemplateService** (`apps/api/src/Api/Services/N8nTemplateService.cs`):
- `GetTemplatesAsync(category?)` - List templates with optional filtering
- `GetTemplateAsync(id)` - Retrieve template details with workflow definition
- `ImportTemplateAsync(id, parameters, userId)` - Import to n8n with parameter substitution
- `ValidateTemplate(json)` - Validate template JSON structure

**API Endpoints** (`Program.cs`, lines 3801-3904):
- `GET /api/v1/n8n/templates` - List all templates
- `GET /api/v1/n8n/templates/{id}` - Get template details
- `POST /api/v1/n8n/templates/{id}/import` - Import template (requires auth)
- `POST /api/v1/n8n/templates/validate` - Validate template (admin only)

### Frontend Components

**Template Gallery** (`apps/web/src/pages/admin/n8n-templates.tsx`):
- Template grid with category filters
- Template cards showing metadata and tags
- Import modal with parameter form
- Real-time validation and error handling

### Template Storage

Templates are stored as JSON files in `infra/n8n/templates/`:
- File naming: `{template-id}.json`
- Schema validation on load
- Parameter placeholder support: `{{paramName}}`

## Template Schema

```json
{
  "id": "unique-template-id",
  "name": "Human-Readable Name",
  "version": "1.0.0",
  "description": "What this template does",
  "category": "integration|automation|monitoring|data-processing",
  "author": "MeepleAI",
  "tags": ["tag1", "tag2"],
  "icon": "📊",
  "screenshot": null,
  "documentation": "docs/n8n-templates/{id}.md",
  "parameters": [
    {
      "name": "paramName",
      "type": "string|number|boolean|select",
      "label": "User-Facing Label",
      "description": "Help text",
      "required": true,
      "default": null,
      "options": ["option1", "option2"],
      "sensitive": false
    }
  ],
  "workflow": {
    "nodes": [
      {
        "id": "unique-node-id",
        "name": "Node Name",
        "type": "n8n-nodes-base.nodeType",
        "typeVersion": 1,
        "position": [x, y],
        "parameters": {
          "param": "{{paramName}}"
        }
      }
    ],
    "connections": {
      "nodeId": {
        "main": [[{"node": "targetNodeId", "type": "main", "index": 0}]]
      }
    },
    "settings": {
      "executionOrder": "v1"
    },
    "staticData": null
  }
}
```

## Usage Guide

### For Users

**1. Browse Templates**:
- Navigate to Admin Panel → n8n Templates
- Filter by category (Integration, Automation, Monitoring, Data Processing)
- Click on a template card to view details

**2. Configure Parameters**:
- Review template description and parameter requirements
- Fill in all required parameters (marked with *)
- Sensitive parameters (API keys, passwords) are masked

**3. Import to n8n**:
- Click "Import Template" button
- Workflow is created in your n8n instance (inactive by default)
- Success message displays the workflow ID

**4. Activate in n8n**:
- Open n8n UI (default: http://localhost:5678)
- Find the imported workflow
- Review and activate

### For Developers

**Creating a New Template**:

1. Create JSON file in `infra/n8n/templates/{template-id}.json`
2. Follow the template schema (see above)
3. Use `{{paramName}}` placeholders for configurable values
4. Test template validation:
   ```bash
   curl -X POST http://localhost:8080/api/v1/n8n/templates/validate \
     -H "Content-Type: application/json" \
     -d @infra/n8n/templates/your-template.json
   ```
5. Create documentation in `docs/n8n-templates/{template-id}.md`

**Parameter Types**:
- `string`: Text input (use `sensitive: true` for passwords/keys)
- `number`: Numeric input with type validation
- `boolean`: Checkbox input
- `select`: Dropdown with predefined options

**Parameter Substitution**:
- Uses regex pattern: `{{paramName}}`
- Applied recursively throughout workflow JSON
- Unreplaced parameters are logged as warnings

## API Reference

### GET /api/v1/n8n/templates

**Description**: List all available workflow templates

**Query Parameters**:
- `category` (optional): Filter by category

**Response**: `200 OK`
```json
[
  {
    "id": "template-id",
    "name": "Template Name",
    "version": "1.0.0",
    "description": "Description",
    "category": "integration",
    "author": "MeepleAI",
    "tags": ["tag1"],
    "icon": "📊",
    "screenshot": null,
    "documentation": null,
    "parameters": [...]
  }
]
```

### GET /api/v1/n8n/templates/{id}

**Description**: Get template details with full workflow definition

**Path Parameters**:
- `id`: Template ID

**Response**: `200 OK`
```json
{
  "id": "template-id",
  ...
  "workflow": {
    "nodes": [...],
    "connections": {...}
  }
}
```

**Error Responses**:
- `404 Not Found`: Template does not exist

### POST /api/v1/n8n/templates/{id}/import

**Description**: Import template to n8n with parameter substitution

**Path Parameters**:
- `id`: Template ID

**Request Body**:
```json
{
  "parameters": {
    "paramName1": "value1",
    "paramName2": "value2"
  }
}
```

**Response**: `200 OK`
```json
{
  "workflowId": "workflow-123",
  "message": "Template imported successfully..."
}
```

**Error Responses**:
- `400 Bad Request`: Missing required parameters or template not found
- `401 Unauthorized`: Not authenticated

### POST /api/v1/n8n/templates/validate

**Description**: Validate template JSON structure (admin only)

**Request Body**:
```json
{
  "templateJson": "{...full template JSON...}"
}
```

**Response**: `200 OK`
```json
{
  "valid": true,
  "errors": null
}
```

**Error Responses**:
- `401 Unauthorized`: Not authenticated
- `403 Forbidden`: Not admin role

## Configuration

### Prerequisites

1. **Active n8n Configuration**:
   - Configure n8n connection in Admin Panel → n8n Configuration
   - Set base URL, API key, and test connection
   - Ensure configuration is marked as "Active"

2. **Environment Variables**:
   ```bash
   N8N_ENCRYPTION_KEY=your-32-byte-encryption-key
   ```

### Template Directory

Templates are loaded from `infra/n8n/templates/` (relative to API project root).

The directory is auto-created on first service initialization if it doesn't exist.

## Security

### Parameter Encryption

Sensitive parameters (API keys, passwords) are:
1. Marked with `sensitive: true` in template definition
2. Displayed as password inputs in the UI
3. Encrypted at rest when stored in n8n config
4. Never logged in plaintext

### Authorization

- **List/Get Templates**: Requires authentication (any role)
- **Import Template**: Requires authentication (any role)
- **Validate Template**: Requires Admin role

## Testing

### Unit Tests (16 tests)

**File**: `apps/api/tests/Api.Tests/Services/N8nTemplateServiceTests.cs`

**Coverage**:
- Template loading and parsing
- Category filtering
- Template validation
- Parameter substitution
- n8n API integration
- Error handling

### Integration Tests (11 tests)

**File**: `apps/api/tests/Api.Tests/N8nTemplateEndpointsTests.cs`

**Coverage**:
- Endpoint authentication
- Role-based authorization
- Template retrieval
- Import workflow
- Validation endpoint

### Running Tests

```bash
# Unit tests only
cd apps/api
dotnet test --filter "FullyQualifiedName~N8nTemplateServiceTests"

# Integration tests only
dotnet test --filter "FullyQualifiedName~N8nTemplateEndpointsTests"

# All N8N-04 tests
dotnet test --filter "FullyQualifiedName~N8nTemplate"
```

## Troubleshooting

### Template Not Loading

**Symptom**: Template doesn't appear in gallery

**Solutions**:
1. Verify template file is in `infra/n8n/templates/`
2. Check JSON is valid: `cat template.json | jq .`
3. Review API logs for parsing errors
4. Ensure file name matches template ID: `{id}.json`

### Import Fails

**Symptom**: "No active n8n configuration found"

**Solution**: Configure n8n in Admin Panel → n8n Configuration

**Symptom**: "Failed to create workflow in n8n"

**Solutions**:
1. Test n8n connection in Admin Panel
2. Verify API key is valid
3. Check n8n is running: `docker compose ps n8n`
4. Review n8n logs: `docker compose logs n8n`

### Parameter Substitution Issues

**Symptom**: Workflow created but placeholders remain

**Solution**: Ensure parameter names in template match exactly (case-sensitive)

**Symptom**: "Missing required parameters"

**Solution**: All parameters marked `required: true` must be provided

## Future Enhancements

- Template marketplace with community contributions
- Template export functionality
- Template versioning with migration support
- Template preview with workflow visualization
- Template testing framework
- A/B testing for template effectiveness

## Related Documentation

- [n8n Integration Guide](./n8n-integration-guide.md) - N8N-01, N8N-03 webhooks
- [n8n Error Handling](./n8n-error-handling.md) - N8N-05 error logging
- [n8n Official Docs](https://docs.n8n.io/) - n8n platform documentation

---

**Generated with Claude Code**
**Co-Authored-By:** Claude <noreply@anthropic.com>
