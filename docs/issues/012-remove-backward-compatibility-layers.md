# Issue #012: Remove Backward Compatibility Layers

**Priority:** 🟡 MEDIUM
**Category:** Backend + Frontend / Technical Debt
**Estimated Effort:** 3-4 days
**Sprint:** MEDIUM-TERM (3-6 months)
**Prerequisites:** Frontend must migrate to new DTOs first

## Summary

8 backward compatibility conversion layers exist between DDD DTOs and legacy model formats. These were needed during migration but should be removed once all clients use new DTOs.

## Backward Compatibility Mappings

### Backend: DTO → Legacy Model Conversions

| Endpoint File | Lines | Conversions | Impact |
|--------------|-------|-------------|--------|
| `AuthenticationEndpoints.cs` | 60, 120, 171 | 3 AuthResponse mappings | Login, Register, Refresh |
| `RuleSpecEndpoints.cs` | 72, 406 | 2 RuleSpec conversions | Game rules CRUD |
| `TwoFactorEndpoints.cs` | 129 | 1 2FA mapping | Two-factor auth |
| `PdfEndpoints.cs` | 424 | 1 PDF DTO conversion | Document processing |
| `AdminMiscEndpoints.cs` | 40 | 1 Admin DTO conversion | Admin operations |

**Total:** 8 conversion points across 5 endpoint files

### Frontend: Zustand Migration

**File:** `apps/web/src/store/chat/compatibility.ts`
```typescript
/**
 * Gradual migration path from React Context to direct Zustand usage
 * TODO: Remove this file when all components migrated
 */
export const useChatContext = () => useChatStore()
```

**Status:** Provides `useChatContext` hook as wrapper around Zustand store (Issue #1083)

---

## Migration Strategy

### Phase 1: Frontend Migration (Prerequisites)

Before removing backend conversions, ensure frontend uses new DTOs:

#### 1.1 Authentication DTOs
- [ ] Audit all components using `AuthResponse` model
- [ ] Update to use new DTOs from Authentication context
- [ ] Test login/register/refresh flows
- [ ] Verify no breaking changes

#### 1.2 Game/RuleSpec DTOs
- [ ] Update game list components
- [ ] Update rule spec displays
- [ ] Migrate to new domain models
- [ ] Test CRUD operations

#### 1.3 PDF DTOs
- [ ] Update upload components
- [ ] Update document list displays
- [ ] Migrate to DocumentProcessing DTOs
- [ ] Test upload/download flows

#### 1.4 Admin DTOs
- [ ] Update admin dashboard
- [ ] Migrate to Administration context DTOs
- [ ] Test admin operations

### Phase 2: Remove Backend Conversions (3-4 days)

#### 2.1 Update Endpoint Return Types

**Before (AuthenticationEndpoints.cs:60):**
```csharp
var result = await _mediator.Send(new LoginCommand(...));

// Backward compatibility conversion
return Results.Ok(new AuthResponse
{
    Token = result.Token,
    User = MapToLegacyUserModel(result.User),
    ExpiresAt = result.ExpiresAt
});
```

**After:**
```csharp
var result = await _mediator.Send(new LoginCommand(...));
return Results.Ok(result); // Return DTO directly
```

#### 2.2 Files to Modify

- [ ] `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`
  - Remove 3 mappings (lines 60, 120, 171)
  - Return DTOs directly

- [ ] `apps/api/src/Api/Routing/RuleSpecEndpoints.cs`
  - Remove 2 conversions (lines 72, 406)

- [ ] `apps/api/src/Api/Routing/TwoFactorEndpoints.cs`
  - Remove 1 mapping (line 129)

- [ ] `apps/api/src/Api/Routing/PdfEndpoints.cs`
  - Remove 1 conversion (line 424)

- [ ] `apps/api/src/Api/Routing/AdminMiscEndpoints.cs`
  - Remove 1 conversion (line 40)

#### 2.3 Remove Legacy Models (If Unused)

Check if these models still have other uses:
- [ ] `apps/api/src/Api/Models/AuthResponse.cs`
- [ ] `apps/api/src/Api/Models/RuleSpec.cs` (not V0)
- [ ] Others as identified

If only used for compatibility, delete them.

### Phase 3: Remove Session/Auth Compatibility

**Files:**
- `apps/api/src/Api/Authentication/SessionAuthenticationHandler.cs:96-97`
- `apps/api/src/Api/Middleware/SessionAuthenticationMiddleware.cs:41`

**Issue:** Converts DDD DTO to legacy ActiveSession format

**Action:**
- [ ] Identify where ActiveSession is used
- [ ] Update consumers to use new session DTO
- [ ] Remove conversion logic
- [ ] Test authentication flow

### Phase 4: Frontend Zustand Migration

**File:** `apps/web/src/store/chat/compatibility.ts`

#### 4.1 Find All Usages
```bash
grep -r "useChatContext" apps/web/src --include="*.tsx" --include="*.ts"
```

#### 4.2 Replace Hook Calls

**Before:**
```typescript
import { useChatContext } from '@/store/chat/compatibility'
const { messages, sendMessage } = useChatContext()
```

**After:**
```typescript
import { useChatStore } from '@/store/chat'
const { messages, sendMessage } = useChatStore()
```

#### 4.3 Update Components
- [ ] Update all components using `useChatContext`
- [ ] Change to direct `useChatStore` usage
- [ ] Test chat functionality thoroughly

#### 4.4 Delete Compatibility File
- [ ] Verify zero usages remain
- [ ] Delete `apps/web/src/store/chat/compatibility.ts`
- [ ] Update imports in test files

---

## Testing Strategy

### Backend Tests
- [ ] Update integration tests to expect new DTO formats
- [ ] Verify API contract tests pass
- [ ] Test all affected endpoints manually
- [ ] Check OpenAPI/Swagger schema

### Frontend Tests
- [ ] Update component tests for new response shapes
- [ ] Test authentication flows end-to-end
- [ ] Test game/rule spec displays
- [ ] Test PDF upload/download
- [ ] Verify no console errors

### E2E Tests
- [ ] Login/logout flow
- [ ] Game browsing
- [ ] Document upload
- [ ] Chat functionality
- [ ] Admin operations

---

## Rollout Plan

### Week 1: Analysis
- [ ] Catalog all frontend usages of legacy models
- [ ] Create migration checklist
- [ ] Identify breaking changes
- [ ] Plan communication

### Week 2-3: Frontend Migration
- [ ] Update frontend to use new DTOs
- [ ] Deploy and monitor
- [ ] Fix any issues

### Week 4: Backend Cleanup
- [ ] Remove compatibility conversions
- [ ] Delete legacy models
- [ ] Update documentation
- [ ] Deploy and monitor

### Week 5: Zustand Cleanup
- [ ] Migrate components off compatibility hook
- [ ] Delete compatibility layer
- [ ] Final testing

---

## Breaking Changes

**API Response Format Changes:**

**Before:**
```json
{
  "token": "...",
  "user": {
    "id": "...",
    "username": "..."
  }
}
```

**After (DTO):**
```json
{
  "token": "...",
  "userDto": {
    "userId": "...",
    "displayName": "..."
  }
}
```

**Mitigation:**
- Version API endpoints (v1 → v2)
- OR: Maintain legacy format for external clients
- Update all internal clients first

---

## Success Criteria

- [ ] Zero backend DTO → legacy model conversions
- [ ] Frontend uses DTOs directly everywhere
- [ ] Zustand compatibility layer removed
- [ ] All tests pass (90%+ coverage)
- [ ] No production errors after deployment
- [ ] Documentation updated

## Rollback Plan

If issues occur post-deployment:
1. Revert backend changes (restore conversions)
2. Keep legacy models temporarily
3. Fix frontend issues
4. Retry removal incrementally

---

## Related Issues

- Issue #1083: Zustand migration (original issue)
- Issue #010: Backend TODOs
- DDD Migration: 99% complete (this is final 1%)

## References

- Backend conversions: Legacy code analysis Section 4
- Frontend compatibility: Section 4.A
- Authentication DTOs: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/`
- Session handling: `apps/api/src/Api/Authentication/`
- Zustand store: `apps/web/src/store/chat/`

## Estimated Impact

**Lines removed:**
- Backend: ~100-150 lines (conversions + legacy models)
- Frontend: ~50 lines (compatibility layer)

**Risk level:** MEDIUM
- Requires coordinated frontend + backend deployment
- Potential breaking changes if external clients exist
- Thorough testing required

## Notes

**IMPORTANT:** Do NOT remove backend conversions until frontend fully migrated and deployed. Otherwise runtime errors will occur.

**Alternative Approach:** Keep conversions indefinitely if external API clients exist that depend on legacy format. Document as permanent compatibility layer.
