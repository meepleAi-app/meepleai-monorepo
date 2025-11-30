# Issue #021: Cleanup Legacy Comments and Deprecation Markers

**Priority:** 🟢 LOW
**Category:** Code Quality / Documentation
**Estimated Effort:** 1-2 days
**Sprint:** LONG-TERM (Post-Beta)

## Summary

18+ files contain `@deprecated` markers, legacy code comments, and obsolete notices. While most are properly managed, they add noise and should be cleaned up once migrations are complete.

## Deprecation Markers

### 1. Frontend API Client (4 deprecated properties)

**File:** `apps/web/src/lib/api/index.ts:138-156`

```typescript
/**
 * @deprecated Use feature-specific clients instead
 */
export const api = {
  auth: authClient,      // @deprecated
  games: gamesClient,    // @deprecated
  chat: chatClient,      // @deprecated
  upload: uploadClient,  // @deprecated
}
```

**Status:** Backward compatibility layer for old import patterns

**Action:**
1. [ ] Find all usages: `grep -r "api.auth" apps/web/src`
2. [ ] Migrate to feature-specific clients:
   ```typescript
   // Before
   import { api } from '@/lib/api'
   api.auth.login(...)

   // After
   import { authClient } from '@/lib/api'
   authClient.login(...)
   ```
3. [ ] Remove deprecated properties
4. [ ] Update tests

**Priority:** MEDIUM (improves code organization)

---

### 2. ChatProvider Hook

**File:** `apps/web/src/components/chat/ChatProvider.tsx:690`

```typescript
/**
 * @deprecated Use useChatContext from '@/hooks/useChatContext' instead
 */
export const useChatContextLegacy = () => { /* ... */ }
```

**Action:**
1. [ ] Verify zero usages of `useChatContextLegacy`
2. [ ] Delete function if unused
3. [ ] Or: migrate remaining usages

**Priority:** LOW

---

### 3. Upload Test Fixtures

**File:** `apps/web/src/__tests__/fixtures/upload-mocks.ts:85-128`

**Multiple `@deprecated` wrappers:**
```typescript
/**
 * @deprecated Use createMockFile() instead
 */
export const mockPdfFile = createMockFile({ /* ... */ })

// ... 4-5 more deprecated mock exports
```

**Action:**
1. [ ] Find test files using deprecated mocks
2. [ ] Update to use factory functions (`createMockFile()`)
3. [ ] Remove deprecated exports

**Priority:** LOW (test code only)

---

### 4. Backend Obsolete Attributes

**File:** `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/TesseractOcrAdapter.cs:207`

```csharp
// SYSLIB0032: HandleProcessCorruptedStateExceptions is obsolete in .NET 9+
[HandleProcessCorruptedStateExceptions]
```

**Issue:** .NET 9 deprecated this attribute

**Action:**
1. [ ] Research .NET 9 replacement for corrupted state exception handling
2. [ ] Update to new pattern or remove if no longer needed
3. [ ] Test OCR error handling still works
4. [ ] Suppress warning if intentional: `#pragma warning disable SYSLIB0032`

**Priority:** LOW (still works but generates warnings)

---

## Legacy Pattern Comments

### 5. Backward Compatibility Headers

**File:** `apps/api/src/Api/Middleware/ApiKeyAuthenticationMiddleware.cs`

```csharp
// Support legacy X-API-Key header (line ~80)
var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
          ?? context.Request.Headers["X-API-Key"].FirstOrDefault(); // legacy
```

**Action:**
1. [ ] Check if any clients still use `X-API-Key` (uppercase)
2. [ ] Add logging to track usage
3. [ ] Set deprecation timeline (6 months)
4. [ ] Remove legacy header support
5. [ ] Document in API changelog

**Priority:** LOW (backward compatibility)

---

### 6. Security Headers Middleware

**File:** `apps/api/src/Api/Middleware/SecurityHeadersMiddleware.cs`

**Multiple legacy comments:**
```csharp
// Line 77: "Browser XSS filter (legacy support)"
context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

// Line 204: "Synchronous XHR (deprecated, performance issue)"
"sync-xhr" // Still in CSP

// Line 221: Legacy security headers
```

**Action:**
1. [ ] Verify browser support for these headers still needed
2. [ ] Check modern alternatives (CSP already covers XSS)
3. [ ] Remove deprecated headers
4. [ ] Update CSP to remove `sync-xhr` if unused
5. [ ] Test in target browsers (Chrome, Firefox, Safari)

**Priority:** LOW (security best practices, but legacy)

---

### 7. Configuration Service Backward Compat

**File:** `apps/api/src/Api/Services/ConfigurationService.cs:101`

```csharp
// This method is kept for backward compatibility but should eventually be removed
public async Task<string> GetConfigurationValueAsync(string key)
{
    // Old non-typed API
}
```

**Action:**
1. [ ] Find all usages of `GetConfigurationValueAsync`
2. [ ] Migrate to typed methods (`GetStringAsync`, `GetIntAsync`, etc.)
3. [ ] Mark method `[Obsolete]` with message
4. [ ] Schedule removal (2-3 months)

**Priority:** LOW

---

### 8. Unversioned Endpoints

**File:** `apps/api/src/Api/Program.cs:278`

```csharp
// Unversioned endpoints (keep for backwards compatibility)
app.MapGroup("/api")
   .MapAuthenticationEndpoints()
   .WithTags("Authentication (Unversioned)");
```

**Action:**
1. [ ] Track usage of unversioned endpoints (add metrics)
2. [ ] Add `Sunset` header to responses:
   ```
   Sunset: Wed, 31 Dec 2025 23:59:59 GMT
   ```
3. [ ] Communicate deprecation to API consumers
4. [ ] Remove when usage drops to zero

**Priority:** LOW (API versioning strategy)

---

## Tasks by Phase

### Phase 1: Audit (0.5 days)
- [ ] Create comprehensive list of all deprecation markers
- [ ] Check git history for context
- [ ] Categorize by priority
- [ ] Identify safe-to-remove vs. needs-migration

### Phase 2: Frontend Cleanup (1 day)
- [ ] Migrate off deprecated API client properties
- [ ] Remove deprecated ChatProvider hook if unused
- [ ] Update upload test fixtures
- [ ] Remove deprecation markers

### Phase 3: Backend Cleanup (0.5 days)
- [ ] Fix .NET 9 obsolete attribute warning
- [ ] Add proper `[Obsolete]` attributes to methods
- [ ] Update ConfigurationService usages
- [ ] Document API deprecation timeline

### Phase 4: Header/Middleware Cleanup (1 day)
- [ ] Research modern security header best practices
- [ ] Remove X-XSS-Protection if safe
- [ ] Update CSP to remove sync-xhr
- [ ] Test in multiple browsers
- [ ] Document changes

---

## Search Commands

```bash
# Find all deprecation markers
grep -r "@deprecated" apps/web/src --include="*.ts" --include="*.tsx"
grep -r "\[Obsolete\]" apps/api/src --include="*.cs"

# Find legacy comments
grep -r "legacy" apps/ --include="*.ts" --include="*.tsx" --include="*.cs" -i
grep -r "backward compatibility" apps/ --include="*.cs" -i
grep -r "kept for compatibility" apps/ --include="*.cs" -i

# Find TODO related to deprecation
grep -r "TODO.*deprecat" apps/ -i
grep -r "TODO.*remove" apps/ -i
```

---

## Success Criteria

- [ ] All `@deprecated` markers documented with migration path
- [ ] Obsolete methods marked with `[Obsolete]` attribute
- [ ] Legacy security headers removed or justified
- [ ] API deprecation timeline communicated
- [ ] Documentation updated
- [ ] No increase in technical debt

---

## Migration Timeline

| Item | Timeline | Blocker? |
|------|----------|----------|
| API client properties | 1-2 months | Frontend migration needed |
| Test fixtures | ASAP | Safe to do now |
| .NET 9 attribute | ASAP | Generates warnings |
| Security headers | 3-6 months | Browser testing needed |
| Unversioned API | 6-12 months | External clients exist |
| ConfigurationService | 2-3 months | Internal migration only |

---

## Related Issues

- Issue #012: Remove Backward Compatibility Layers (overlaps)
- Issue #010: Resolve Backend TODOs
- Issue #011: Frontend API implementations

## References

- Frontend deprecations: `apps/web/src/lib/api/index.ts`
- Backend: Multiple files (see search commands)
- Security headers: MDN Web Docs on CSP and deprecated headers
- .NET 9 changes: Microsoft migration guide
- Legacy code analysis: Sections 1.A, 4.C, 8

## Notes

**Low Priority Rationale:**
- Deprecation markers don't affect functionality
- Mostly code quality and maintainability
- Should be done during low-activity periods
- Can be batched with other refactoring

**Benefits:**
- Cleaner codebase
- Reduced confusion for new developers
- Fewer compiler warnings
- Improved code discoverability

**Risk:** Very low - mostly removing comments and unused code
