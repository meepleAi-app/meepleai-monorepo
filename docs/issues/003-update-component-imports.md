# Issue #003: Update Component Imports to Subdirectory Paths

**Priority:** 🔴 CRITICAL
**Category:** Frontend / Code Quality
**Estimated Effort:** 1-2 days
**Sprint:** IMMEDIATE (1-2 sprints)
**Depends On:** Issue #001

## Summary

After consolidating duplicate components (Issue #001), all import statements throughout the codebase must be updated to use consistent subdirectory paths. This ensures type safety and prevents runtime errors.

## Current State

Tests and components currently import from inconsistent paths:
```typescript
// ❌ Old root-level imports
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { UploadQueue } from '@/components/UploadQueue'
import { CommentThread } from '@/components/CommentThread'

// ✅ New organized imports
import { ErrorBoundary } from '@/components/errors/ErrorBoundary'
import { UploadQueue } from '@/components/upload/UploadQueue'
import { CommentThread } from '@/components/comments/CommentThread'
```

## Impact

- **Type Safety:** TypeScript won't catch missing components until runtime
- **Test Failures:** Tests will fail if root components deleted first
- **Developer Experience:** Inconsistent import patterns create confusion

## Import Mapping

### Errors Components
```typescript
// Before → After
'@/components/ErrorBoundary' → '@/components/errors/ErrorBoundary'
'@/components/RouteErrorBoundary' → '@/components/errors/RouteErrorBoundary'
'@/components/ErrorDisplay' → '@/components/errors/ErrorDisplay'
'@/components/SimpleErrorMessage' → '@/components/errors/SimpleErrorMessage'
```

### Upload Components
```typescript
'@/components/UploadQueue' → '@/components/upload/UploadQueue'
'@/components/UploadQueueItem' → '@/components/upload/UploadQueueItem'
'@/components/UploadSummary' → '@/components/upload/UploadSummary'
```

### Comments Components
```typescript
'@/components/CommentThread' → '@/components/comments/CommentThread'
'@/components/CommentItem' → '@/components/comments/CommentItem'
'@/components/InlineCommentIndicator' → '@/components/comments/InlineCommentIndicator'
```

### Prompt Components
```typescript
'@/components/PromptEditor' → '@/components/prompt/PromptEditor'
'@/components/PromptVersionCard' → '@/components/prompt/PromptVersionCard'
```

## Tasks

### 1. Search and Catalog
- [ ] Find all import statements for each component:
  ```bash
  # Errors
  grep -r "from '@/components/ErrorBoundary'" apps/web/src
  grep -r "from '@/components/RouteErrorBoundary'" apps/web/src
  grep -r "from '@/components/ErrorDisplay'" apps/web/src
  grep -r "from '@/components/SimpleErrorMessage'" apps/web/src

  # Upload
  grep -r "from '@/components/UploadQueue'" apps/web/src
  grep -r "from '@/components/UploadQueueItem'" apps/web/src
  grep -r "from '@/components/UploadSummary'" apps/web/src

  # Comments
  grep -r "from '@/components/CommentThread'" apps/web/src
  grep -r "from '@/components/CommentItem'" apps/web/src
  grep -r "from '@/components/InlineCommentIndicator'" apps/web/src

  # Prompt
  grep -r "from '@/components/PromptEditor'" apps/web/src
  grep -r "from '@/components/PromptVersionCard'" apps/web/src
  ```
- [ ] Create checklist of all files to update
- [ ] Estimate total import statements to change

### 2. Automated Update (Recommended)
- [ ] Create find/replace script or use VSCode multi-file search & replace
- [ ] Test script on small subset first
- [ ] Apply to all files

**Example script:**
```bash
#!/bin/bash
# update-imports.sh

# Errors
find apps/web/src -type f -name "*.tsx" -o -name "*.ts" | xargs sed -i \
  "s|from '@/components/ErrorBoundary'|from '@/components/errors/ErrorBoundary'|g"

# Upload
find apps/web/src -type f -name "*.tsx" -o -name "*.ts" | xargs sed -i \
  "s|from '@/components/UploadQueue'|from '@/components/upload/UploadQueue'|g"

# ... etc for all components
```

### 3. Manual Update (Alternative)
- [ ] Update imports in test files first
- [ ] Update imports in page components
- [ ] Update imports in other components
- [ ] Update imports in hooks/utilities

### 4. Verification
- [ ] Run TypeScript compiler: `pnpm typecheck`
- [ ] Fix any type errors
- [ ] Run linter: `pnpm lint`
- [ ] Fix any linting issues

### 5. Testing
- [ ] Run full test suite: `pnpm test`
- [ ] Verify all tests pass
- [ ] Check coverage maintained (90%+)
- [ ] Run E2E tests if available

### 6. Build Verification
- [ ] Build frontend: `pnpm build`
- [ ] Verify no build errors
- [ ] Check bundle size (should be same or smaller)
- [ ] Test production build locally

## Files Likely to Update

**Test Files:**
- `apps/web/src/__tests__/**/*.test.tsx`
- `apps/web/src/__tests__/**/*.test.ts`
- `apps/web/src/components/**/__tests__/*.test.tsx`

**Component Files:**
- `apps/web/src/app/**/page.tsx`
- `apps/web/src/app/**/layout.tsx`
- `apps/web/src/components/**/*.tsx`

**Hook Files:**
- `apps/web/src/hooks/**/*.ts`

## Success Criteria

- [ ] All imports use subdirectory paths
- [ ] Zero TypeScript errors
- [ ] All tests pass (90%+ coverage)
- [ ] No linting errors
- [ ] Production build succeeds
- [ ] No console warnings in browser

## Rollback Plan

If issues occur:
1. Revert commit(s)
2. Keep root-level duplicates temporarily
3. Fix import issues incrementally
4. Retry consolidation

## Related Issues

- Issue #001: Consolidate Duplicate Components (prerequisite)
- Issue #002: Remove Deprecated Profile Page

## References

- Component organization: `apps/web/src/components/`
- TypeScript config: `apps/web/tsconfig.json`
- Import alias: `@/` maps to `src/`
- Legacy code analysis: Section 2 (Duplicate Components)

## Notes

**IMPORTANT:** Do NOT delete root-level component files until ALL imports are updated and verified. Otherwise runtime errors will occur.

**Estimated Import Statements:** 50-100+ across codebase

**Tools:**
- VSCode: Multi-file search & replace (Ctrl+Shift+H)
- sed/awk: Batch text replacement
- TypeScript Language Server: Auto-import suggestions
