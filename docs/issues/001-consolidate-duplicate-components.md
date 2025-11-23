# Issue #001: Consolidate Duplicate React Components

**Priority:** 🔴 CRITICAL
**Category:** Frontend / Code Quality
**Estimated Effort:** 2-3 days
**Sprint:** IMMEDIATE (1-2 sprints)

## Summary

9 React components exist in duplicate locations (root `/components/` and organized subdirectories). This creates import ambiguity, maintenance burden, and increases bundle size.

## Affected Components

| Component | Root Path | Subdirectory Path | Status |
|-----------|-----------|-------------------|--------|
| ErrorBoundary.tsx | `/components/ErrorBoundary.tsx` | `/components/errors/ErrorBoundary.tsx` | IDENTICAL (219 lines) |
| RouteErrorBoundary.tsx | `/components/RouteErrorBoundary.tsx` | `/components/errors/RouteErrorBoundary.tsx` | DUPLICATE |
| PromptEditor.tsx | `/components/PromptEditor.tsx` | `/components/prompt/PromptEditor.tsx` | DIFFERENT IMPLEMENTATIONS |
| PromptVersionCard.tsx | `/components/PromptVersionCard.tsx` | `/components/prompt/PromptVersionCard.tsx` | DUPLICATE |
| CommentThread.tsx | `/components/CommentThread.tsx` | `/components/comments/CommentThread.tsx` | DUPLICATE |
| CommentItem.tsx | `/components/CommentItem.tsx` | `/components/comments/CommentItem.tsx` | DUPLICATE |
| InlineCommentIndicator.tsx | `/components/InlineCommentIndicator.tsx` | `/components/comments/InlineCommentIndicator.tsx` | DUPLICATE |
| ErrorDisplay.tsx | `/components/ErrorDisplay.tsx` | `/components/errors/ErrorDisplay.tsx` | DUPLICATE |
| SimpleErrorMessage.tsx | `/components/SimpleErrorMessage.tsx` | `/components/errors/SimpleErrorMessage.tsx` | DUPLICATE |

## Upload-Related Duplicates

| Component | Root | Subdirectory |
|-----------|------|--------------|
| UploadQueue.tsx | `/components/UploadQueue.tsx` | `/components/upload/UploadQueue.tsx` |
| UploadQueueItem.tsx | `/components/UploadQueueItem.tsx` | `/components/upload/UploadQueueItem.tsx` |
| UploadSummary.tsx | `/components/UploadSummary.tsx` | `/components/upload/UploadSummary.tsx` |

## Impact

- **Import Confusion:** Tests and components import from inconsistent paths
- **Maintenance Burden:** Changes must be synchronized across duplicates
- **Bundle Size:** Duplicate code unnecessarily increases build size
- **Code Quality:** Violates DRY principle

## Tasks

### 1. Analysis Phase
- [ ] Identify all import statements for each duplicate component
- [ ] Verify which version (root vs subdirectory) is actively used
- [ ] Check for implementation differences (especially PromptEditor.tsx)
- [ ] Create comprehensive import map

### 2. Decision Phase
- [ ] For identical files: Keep subdirectory version (better organization)
- [ ] For different implementations: Review and merge differences or choose canonical version
- [ ] Document decision rationale in this issue

### 3. Consolidation Phase
- [ ] Update all imports to point to subdirectory versions:
  - `from '@/components/ErrorBoundary'` → `from '@/components/errors/ErrorBoundary'`
  - `from '@/components/UploadQueue'` → `from '@/components/upload/UploadQueue'`
  - etc.
- [ ] Verify no circular dependencies introduced
- [ ] Run `pnpm typecheck` to catch import errors

### 4. Cleanup Phase
- [ ] Delete root-level duplicate files (after all imports updated)
- [ ] Run full test suite: `pnpm test`
- [ ] Verify bundle size reduction
- [ ] Update component documentation if needed

### 5. Testing Phase
- [ ] Test all affected pages manually
- [ ] Verify upload functionality works
- [ ] Test error boundary behavior
- [ ] Check comment threads
- [ ] Verify prompt editor functionality

## Files to Modify

**Components to delete (root-level):**
```
apps/web/src/components/ErrorBoundary.tsx
apps/web/src/components/RouteErrorBoundary.tsx
apps/web/src/components/PromptEditor.tsx
apps/web/src/components/PromptVersionCard.tsx
apps/web/src/components/CommentThread.tsx
apps/web/src/components/CommentItem.tsx
apps/web/src/components/InlineCommentIndicator.tsx
apps/web/src/components/ErrorDisplay.tsx
apps/web/src/components/SimpleErrorMessage.tsx
apps/web/src/components/UploadQueue.tsx
apps/web/src/components/UploadQueueItem.tsx
apps/web/src/components/UploadSummary.tsx
```

**Tests to update:**
- Search for imports: `grep -r "from '@/components/ErrorBoundary'" apps/web/src`
- Update all test files importing from root paths

## Success Criteria

- [ ] Zero duplicate component files remain
- [ ] All imports use consistent subdirectory paths
- [ ] All tests pass (90%+ coverage maintained)
- [ ] No TypeScript errors
- [ ] Bundle size reduced
- [ ] Manual testing confirms functionality

## Related Issues

- Issue #002: Update Component Imports (follow-up)
- See: Frontend Code Organization ADR (if exists)

## References

- Root components: `apps/web/src/components/`
- Organized subdirectories: `apps/web/src/components/{errors,upload,comments,prompt}/`
- Legacy code analysis report: Section 2 (Duplicate Components)

## Notes

**IMPORTANT:** For `PromptEditor.tsx` - verify if root and subdirectory versions have different implementations. If so, review carefully before consolidation.
