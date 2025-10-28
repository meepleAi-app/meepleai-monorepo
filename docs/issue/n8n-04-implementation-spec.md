# N8N-04: Workflow Templates Library - Implementation Specification

**Issue**: #417 N8N-04 - Create workflow templates library
**Status**: Ready for Implementation
**Effort**: L (3-5 days)
**Priority**: Medium

---

## Implementation Strategy with Optimal Agent/Tool Selection

### Phase 1: Backend Service (Days 1-2)
**Agents**: backend-architect, devops-architect
**MCP**: Serena (pattern discovery), Sequential (service design)
**Files**: 5 backend files + 10+ template JSONs

### Phase 2: Frontend Gallery (Day 3)
**Agents**: frontend-architect, Magic MCP
**MCP**: Magic (UI components), Playwright (E2E)
**Files**: 3 frontend files (page, components)

### Phase 3: Testing & Docs (Days 4-5)
**Agents**: quality-engineer, technical-writer
**Tools**: Jest, Playwright, native docs
**Files**: Test files + documentation

---

## Backend Implementation (backend-architect)

### Files to Create

**1. Services/IN8nTemplateService.cs** (Interface)
```csharp
public interface IN8nTemplateService
{
    Task<List<WorkflowTemplateDto>> GetTemplatesAsync(string? category = null, CancellationToken ct = default);
    Task<WorkflowTemplateDto?> GetTemplateAsync(string templateId, CancellationToken ct = default);
    Task<string> ImportTemplateAsync(string templateId, Dictionary<string, string> parameters, string userId, CancellationToken ct = default);
    bool ValidateTemplate(string templateJson);
}
```

**2. Services/N8nTemplateService.cs** (Implementation - ~200 lines)
- File-based template loading from `infra/n8n/templates/`
- JSON deserialization with error handling
- Parameter substitution engine ({{paramName}} replacement)
- Template validation logic
- Integration with N8nConfigService (if exists) or direct n8n API
- Caching with HybridCache (5-min TTL)

**3. Program.cs** (API Endpoints - ~80 lines)
```csharp
// N8N-04: Workflow template endpoints
v1Api.MapGet("/n8n/templates", async (
    IN8nTemplateService templateService,
    string? category = null,
    CancellationToken ct = default) =>
{
    var templates = await templateService.GetTemplatesAsync(category, ct);
    return Results.Ok(templates);
})
.RequireAuthorization()
.WithName("GetN8nTemplates")
.WithTags("N8N");

v1Api.MapGet("/n8n/templates/{id}", async (
    string id,
    IN8nTemplateService templateService,
    CancellationToken ct = default) =>
{
    var template = await templateService.GetTemplateAsync(id, ct);
    return template == null ? Results.NotFound() : Results.Ok(template);
})
.RequireAuthorization()
.WithName("GetN8nTemplate")
.WithTags("N8N");

v1Api.MapPost("/n8n/templates/{id}/import", async (
    HttpContext context,
    string id,
    ImportTemplateRequest request,
    IN8nTemplateService templateService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
        return Results.Unauthorized();

    try
    {
        var workflowId = await templateService.ImportTemplateAsync(id, request.Parameters, session.User.UserId, ct);
        return Results.Ok(new { workflowId, message = "Template imported successfully" });
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = "Template not found" });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("ImportN8nTemplate")
.WithTags("N8N");

v1Api.MapPost("/n8n/templates/validate", async (
    HttpContext context,
    [FromBody] string templateJson,
    IN8nTemplateService templateService) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
        return Results.Unauthorized();

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var isValid = templateService.ValidateTemplate(templateJson);
    return Results.Ok(new { valid = isValid });
})
.WithName("ValidateN8nTemplate")
.WithTags("N8N");
```

**4. DI Registration in Program.cs**:
```csharp
// N8N-04: Workflow template service
builder.Services.AddScoped<IN8nTemplateService, N8nTemplateService>();
```

---

## Template JSON Files (devops-architect)

**Directory**: `infra/n8n/templates/`

**Template Schema**:
```json
{
  "id": "template-id",
  "name": "Template Name",
  "version": "1.0.0",
  "description": "Description",
  "category": "integration|automation|monitoring|data-processing",
  "icon": "📊",
  "screenshot": null,
  "documentation": "docs/n8n-templates/template-id.md",
  "tags": ["tag1", "tag2"],
  "parameters": [
    {
      "name": "paramName",
      "type": "string|number|boolean|select",
      "label": "Parameter Label",
      "description": "Parameter description",
      "required": true|false,
      "default": "value",
      "options": ["opt1", "opt2"],
      "sensitive": true|false
    }
  ],
  "workflow": {
    "nodes": [...],
    "connections": {...}
  }
}
```

**Templates to Create** (10 minimum):
1. `integration-bgg-sync.json` - BGG metadata sync
2. `automation-email-notifications.json` - Email on events
3. `automation-backup.json` - Database backup to S3
4. `integration-slack-notifications.json` - Slack alerts
5. `data-processing-pdf-pipeline.json` - PDF processing
6. `automation-daily-reports.json` - Daily usage reports
7. `monitoring-error-alerting.json` - Error monitoring
8. `automation-user-onboarding.json` - Welcome emails
9. `automation-cache-warming.json` - Cache pre-loading
10. `integration-data-export.json` - Analytics export

---

## Frontend Implementation (frontend-architect + Magic MCP)

### Files to Create

**1. pages/admin/n8n-templates.tsx** (~350 lines)
- Template gallery with card grid
- Category filter tabs
- Search by name/tags
- Template preview modal
- Import wizard with parameter form
- Success/error toast notifications

**2. components/n8n/TemplateCard.tsx** (~80 lines)
- Template card component
- Icon, name, description display
- Tag badges
- Click to open preview

**3. components/n8n/TemplateImportModal.tsx** (~150 lines)
- Modal with template details
- Dynamic parameter form based on template.parameters
- Type validation (string, number, select)
- Sensitive field handling (password input)
- Import button with loading state

**4. lib/api.ts** (additions)
```typescript
n8nTemplates: {
  async list(category?: string) {
    const url = category
      ? `/api/v1/n8n/templates?category=${category}`
      : '/api/v1/n8n/templates';
    return api.get<WorkflowTemplateDto[]>(url);
  },

  async getById(id: string) {
    return api.get<WorkflowTemplateDto>(`/api/v1/n8n/templates/${id}`);
  },

  async import(id: string, parameters: Record<string, string>) {
    return api.post(`/api/v1/n8n/templates/${id}/import`, { parameters });
  }
}
```

---

## Testing Strategy (quality-engineer)

### Backend Unit Tests (~20 tests)
**File**: `tests/Api.Tests/N8nTemplateServiceTests.cs`
- GetTemplatesAsync: List all, filter by category
- GetTemplateAsync: Valid ID, invalid ID (null)
- ValidateTemplate: Valid JSON, invalid JSON, missing fields
- Parameter substitution: Simple params, nested params, special chars
- ImportTemplateAsync: Success, missing params, invalid template

### Backend Integration Tests (~12 tests)
**File**: `tests/Api.Tests/N8nTemplateEndpointsTests.cs`
- GET /n8n/templates: Auth required, returns list, category filter
- GET /n8n/templates/{id}: Returns details, 404 for invalid
- POST /n8n/templates/{id}/import: Success, validation errors, auth required
- POST /n8n/templates/validate: Admin only, returns valid status

### Frontend Unit Tests (~15 tests)
**File**: `pages/__tests__/admin/n8n-templates.test.tsx`
- Renders template gallery
- Filters by category
- Opens template preview modal
- Submits import form with parameters
- Validates required parameters
- Shows success/error notifications

### E2E Tests (~8 tests)
**File**: `e2e/n8n-templates.spec.ts`
- Admin can view template gallery
- Admin can filter templates by category
- Admin can preview template details
- Admin can import template with parameters
- Form validates required parameters
- Success notification shown after import
- Non-admin user cannot access (403)

---

## Documentation (technical-writer)

### 1. Template Schema Documentation
**File**: `docs/n8n-templates/template-schema.md`
- Complete schema reference
- Field descriptions and constraints
- Parameter types and validation rules
- Example template with annotations

### 2. User Guide
**File**: `docs/guide/n8n-template-user-guide.md`
- How to browse templates
- How to import templates
- Parameter configuration
- Troubleshooting

### 3. Developer Guide
**File**: `docs/guide/n8n-template-developer-guide.md`
- How to create new templates
- Testing templates locally
- Template versioning strategy
- Best practices

### 4. Individual Template READMEs
**Files**: `docs/n8n-templates/{template-id}.md` (10+ files)
- Template purpose and use cases
- Parameter descriptions
- Setup instructions
- Examples

### 5. CLAUDE.md Updates
- Add N8nTemplateService to services list
- Document API endpoints
- Add template gallery reference

---

## Implementation Checklist

### Backend
- [ ] Create IN8nTemplateService interface
- [ ] Implement N8nTemplateService (~200 lines)
- [ ] Create 10+ template JSON files in infra/n8n/templates/
- [ ] Add 4 API endpoints in Program.cs
- [ ] Register service in DI
- [ ] Unit tests (20 tests)
- [ ] Integration tests (12 tests)

### Frontend
- [ ] Create pages/admin/n8n-templates.tsx
- [ ] Create TemplateCard component
- [ ] Create TemplateImportModal component
- [ ] Add API client methods
- [ ] Unit tests (15 tests)
- [ ] E2E tests (8 tests)

### Documentation
- [ ] Template schema documentation
- [ ] User guide (browsing, importing)
- [ ] Developer guide (creating templates)
- [ ] 10+ individual template READMEs
- [ ] CLAUDE.md updates

### Quality
- [ ] All tests passing (55+ total)
- [ ] 90%+ coverage (backend + frontend)
- [ ] Build successful
- [ ] Code review approved

---

## Estimated Effort Breakdown

| Phase | Tasks | Effort | Agent |
|-------|-------|--------|-------|
| Backend Service | Service + DTOs | 4-6h | backend-architect |
| Template JSONs | 10+ templates | 6-8h | devops-architect |
| API Endpoints | 4 endpoints | 2-3h | backend-architect |
| Backend Tests | 32 tests | 4-5h | quality-engineer |
| Frontend Gallery | Page + components | 6-8h | frontend-architect + Magic |
| Frontend Tests | 23 tests | 3-4h | quality-engineer + Playwright |
| Documentation | 5 guides + 10 READMEs | 4-6h | technical-writer |
| **Total** | | **29-40h** | **(3.5-5 days)** |

---

## Success Criteria

✅ 10+ templates available
✅ Template import success rate > 95%
✅ Template loading time < 500ms
✅ 90%+ test coverage
✅ Complete documentation
✅ Admin UI functional and tested

---

**Recommendation**: Implement in sprint dedicated to n8n enhancements. This spec provides complete implementation roadmap with optimal agent/tool selection for each phase.

**Status**: ✅ SPECIFICATION COMPLETE - Ready for implementation
