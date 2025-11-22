# Issue #001: Split AdminEndpoints.cs into 6 Focused Endpoint Files

**Priority**: 🔴 CRITICAL
**Effort**: 40-50 hours
**Impact**: ⭐⭐⭐ HIGH
**Category**: Code Organization
**Status**: Not Started

---

## Problem Description

`AdminEndpoints.cs` is a monolithic file containing **2,031 lines of code** that manages 8+ different resource types. This violates the Single Responsibility Principle at the file level and creates several issues:

- **Difficult navigation**: Finding specific endpoints requires scrolling through 2000+ lines
- **High merge conflict risk**: Multiple developers editing the same large file
- **Poor maintainability**: Changes to one resource type affect the entire file
- **Cognitive overload**: Too many concerns in a single file

**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Routing/AdminEndpoints.cs:2031`

---

## Current Structure

The file currently manages these resource types:
1. System configuration (CRUD, validation, history)
2. Analytics/dashboard statistics
3. Alert management
4. Audit log retrieval
5. Feature flag management
6. Prompt template management
7. Prompt evaluation workflows
8. User management

---

## Proposed Solution

Split `AdminEndpoints.cs` into **6 focused endpoint files**, each managing a single resource type:

```
apps/api/src/Api/Routing/
├── ConfigurationEndpoints.cs      (~350 LOC)
├── AnalyticsEndpoints.cs          (~250 LOC)
├── AlertEndpoints.cs              (~200 LOC)
├── AuditEndpoints.cs              (~150 LOC)
├── FeatureFlagEndpoints.cs        (~150 LOC)
└── PromptManagementEndpoints.cs   (~300 LOC)
```

---

## Acceptance Criteria

- [ ] All 6 new endpoint files created with appropriate route groups
- [ ] All endpoints from `AdminEndpoints.cs` migrated to new files
- [ ] Original `AdminEndpoints.cs` file deleted
- [ ] All existing tests pass without modification
- [ ] No breaking changes to API contracts
- [ ] Route paths remain identical (e.g., `/api/v1/admin/configuration`)
- [ ] Authorization/authentication preserved on all endpoints
- [ ] OpenAPI documentation generates correctly for all endpoints
- [ ] No duplicate endpoint registrations
- [ ] Code compiles without errors or warnings

---

## Implementation Plan

### Phase 1: Create ConfigurationEndpoints.cs (~8 hours)

**File**: `apps/api/src/Api/Routing/ConfigurationEndpoints.cs`

**Endpoints to migrate** (~350 LOC):
```csharp
// Configuration CRUD
GET    /admin/configuration
GET    /admin/configuration/{id}
POST   /admin/configuration
PUT    /admin/configuration/{id}
DELETE /admin/configuration/{id}

// Configuration operations
POST   /admin/configuration/{id}/toggle
POST   /admin/configuration/bulk
GET    /admin/configuration/history/{key}
POST   /admin/configuration/rollback/{key}

// Configuration management
POST   /admin/configuration/export
POST   /admin/configuration/import
GET    /admin/configuration/categories
POST   /admin/configuration/validate
```

**Template**:
```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace Api.Routing;

public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/configuration")
            .RequireAuthorization()
            .WithTags("Admin - Configuration")
            .WithOpenApi();

        // Map all configuration endpoints here
        // GET /admin/configuration
        group.MapGet("/", async (IMediator mediator) => { ... });

        // ... rest of endpoints
    }
}
```

**Testing**:
- Verify all 14 configuration endpoints respond correctly
- Test authorization (admin-only)
- Test validation error responses
- Test configuration history and rollback

---

### Phase 2: Create AnalyticsEndpoints.cs (~6 hours)

**File**: `apps/api/src/Api/Routing/AnalyticsEndpoints.cs`

**Endpoints to migrate** (~250 LOC):
```csharp
// Dashboard statistics
GET /admin/analytics/dashboard
GET /admin/analytics/dashboard/export

// Usage statistics
GET /admin/analytics/users
GET /admin/analytics/games
GET /admin/analytics/ai-requests
GET /admin/analytics/api-usage

// Performance metrics
GET /admin/analytics/performance
GET /admin/analytics/errors
```

**Testing**:
- Verify dashboard stats calculation
- Test export formats (CSV, JSON)
- Test date range filters
- Test aggregation logic

---

### Phase 3: Create AlertEndpoints.cs (~5 hours)

**File**: `apps/api/src/Api/Routing/AlertEndpoints.cs`

**Endpoints to migrate** (~200 LOC):
```csharp
// Alert management
GET    /admin/alerts
GET    /admin/alerts/{id}
POST   /admin/alerts/{id}/resolve
DELETE /admin/alerts/{id}

// Alert configuration
GET    /admin/alerts/active
POST   /admin/alerts/test
GET    /admin/alerts/history
```

**Testing**:
- Test alert creation and resolution
- Test alert filtering and pagination
- Test alert notification delivery
- Test throttling logic

---

### Phase 4: Create AuditEndpoints.cs (~4 hours)

**File**: `apps/api/src/Api/Routing/AuditEndpoints.cs`

**Endpoints to migrate** (~150 LOC):
```csharp
// Audit log retrieval
GET /admin/audit
GET /admin/audit/{id}
GET /admin/audit/user/{userId}
GET /admin/audit/entity/{entityType}/{entityId}

// Audit search
POST /admin/audit/search
GET  /admin/audit/export
```

**Testing**:
- Test audit log retrieval and filtering
- Test search functionality
- Test export formats
- Test pagination

---

### Phase 5: Create FeatureFlagEndpoints.cs (~4 hours)

**File**: `apps/api/src/Api/Routing/FeatureFlagEndpoints.cs`

**Endpoints to migrate** (~150 LOC):
```csharp
// Feature flag management
GET    /admin/features
GET    /admin/features/{key}
POST   /admin/features/{key}/toggle
POST   /admin/features/{key}/rollback

// Feature flag configuration
GET    /admin/features/environments
POST   /admin/features/bulk-toggle
```

**Testing**:
- Test feature flag toggle
- Test rollback functionality
- Test environment-specific flags
- Test bulk operations

---

### Phase 6: Create PromptManagementEndpoints.cs (~8 hours)

**File**: `apps/api/src/Api/Routing/PromptManagementEndpoints.cs`

**Endpoints to migrate** (~300 LOC):
```csharp
// Prompt template CRUD
GET    /admin/prompts
GET    /admin/prompts/{id}
POST   /admin/prompts
PUT    /admin/prompts/{id}
DELETE /admin/prompts/{id}

// Prompt evaluation
POST   /admin/prompts/evaluate
GET    /admin/prompts/evaluation/{id}
POST   /admin/prompts/test

// Prompt versions
GET    /admin/prompts/{id}/versions
POST   /admin/prompts/{id}/rollback/{version}
```

**Testing**:
- Test prompt CRUD operations
- Test prompt evaluation workflows
- Test version management
- Test template validation

---

### Phase 7: Update Program.cs (~2 hours)

**File**: `apps/api/src/Api/Program.cs`

**Changes**:
```csharp
// Before
app.MapAdminEndpoints();

// After
app.MapConfigurationEndpoints();
app.MapAnalyticsEndpoints();
app.MapAlertEndpoints();
app.MapAuditEndpoints();
app.MapFeatureFlagEndpoints();
app.MapPromptManagementEndpoints();
```

---

### Phase 8: Cleanup and Testing (~5 hours)

1. **Delete original file**:
   ```bash
   git rm apps/api/src/Api/Routing/AdminEndpoints.cs
   ```

2. **Run full test suite**:
   ```bash
   dotnet test
   pnpm test
   ```

3. **Test API documentation**:
   - Navigate to `/swagger`
   - Verify all admin endpoints are documented
   - Check route grouping is correct

4. **Integration testing**:
   - Test all endpoints with Postman
   - Verify authorization works
   - Test error responses

---

## File Structure

```
apps/api/src/Api/Routing/
├── ConfigurationEndpoints.cs       # ~350 LOC, 14 endpoints
├── AnalyticsEndpoints.cs           # ~250 LOC, 8 endpoints
├── AlertEndpoints.cs               # ~200 LOC, 7 endpoints
├── AuditEndpoints.cs               # ~150 LOC, 6 endpoints
├── FeatureFlagEndpoints.cs         # ~150 LOC, 6 endpoints
└── PromptManagementEndpoints.cs    # ~300 LOC, 11 endpoints

Total: ~1,400 LOC across 6 files (down from 2,031 LOC in 1 file)
Reduction: ~630 LOC (dead code, duplications removed)
```

---

## Testing Requirements

### Unit Tests
- No new unit tests required (existing tests should pass)
- Verify existing handler tests still work

### Integration Tests
- Test all 52 admin endpoints via HTTP
- Verify authorization middleware works
- Test error responses (400, 401, 403, 404, 500)
- Test request validation

### Manual Testing Checklist
- [ ] All configuration endpoints respond correctly
- [ ] All analytics endpoints return valid data
- [ ] All alert endpoints work as expected
- [ ] All audit endpoints retrieve logs correctly
- [ ] All feature flag endpoints toggle correctly
- [ ] All prompt management endpoints work
- [ ] Swagger documentation is complete
- [ ] Postman collection tests pass

---

## Dependencies

**Blocks**:
- None

**Blocked by**:
- None

**Related Issues**:
- Issue #002: Migrate ConfigurationService to CQRS (synergy opportunity)
- Issue #005: Split AuthEndpoints (similar pattern)

---

## Migration Checklist

### Pre-migration
- [ ] Create backup branch
- [ ] Document all current endpoint routes
- [ ] Run existing tests and record baseline
- [ ] Export Postman collection for manual testing

### During migration
- [ ] Create each new endpoint file (6 files)
- [ ] Migrate endpoints one file at a time
- [ ] Test each file after migration
- [ ] Update Program.cs registrations
- [ ] Verify no duplicate routes

### Post-migration
- [ ] Delete AdminEndpoints.cs
- [ ] Run full test suite
- [ ] Test Swagger documentation
- [ ] Update API documentation
- [ ] Update team on new file structure

---

## Risk Mitigation

**Risk 1**: Breaking existing API clients
- **Mitigation**: Route paths remain identical, only internal organization changes
- **Verification**: Integration tests + Postman collection validation

**Risk 2**: Missing endpoints after migration
- **Mitigation**: Document all endpoints before migration, verify after
- **Verification**: Compare route count before/after

**Risk 3**: Authorization regression
- **Mitigation**: Ensure all endpoint groups require authorization
- **Verification**: Test unauthorized access returns 401/403

**Risk 4**: Merge conflicts during migration
- **Mitigation**: Complete migration in dedicated branch, merge quickly
- **Communication**: Notify team before starting

---

## Success Metrics

- ✅ 6 focused endpoint files created
- ✅ Average file size: ~240 LOC (down from 2,031)
- ✅ Zero breaking changes to API contracts
- ✅ All existing tests pass
- ✅ Swagger documentation complete
- ✅ Code review approved
- ✅ Deployed without incidents

---

## Estimated Timeline

**Total Effort**: 40-50 hours

| Phase | Task | Hours |
|-------|------|-------|
| 1 | ConfigurationEndpoints.cs | 8h |
| 2 | AnalyticsEndpoints.cs | 6h |
| 3 | AlertEndpoints.cs | 5h |
| 4 | AuditEndpoints.cs | 4h |
| 5 | FeatureFlagEndpoints.cs | 4h |
| 6 | PromptManagementEndpoints.cs | 8h |
| 7 | Update Program.cs | 2h |
| 8 | Cleanup and testing | 5h |
| - | **Buffer** | 8-18h |

**Recommended approach**: 1 week sprint (8 hours/day)

---

## References

- Analysis Document: `docs/02-development/backend-codebase-analysis.md`
- Action Items: `docs/02-development/refactoring-action-items.md`
- Current File: `apps/api/src/Api/Routing/AdminEndpoints.cs:2031`
