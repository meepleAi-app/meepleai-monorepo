# Issue #002: Remove Deprecated Profile Page

**Priority:** 🔴 CRITICAL
**Category:** Frontend / Technical Debt
**Estimated Effort:** 0.5 days
**Sprint:** IMMEDIATE (1-2 sprints)
**Related:** SPRINT-1, Issue #848

## Summary

The `/profile` page was deprecated in SPRINT-1 and replaced with `/settings`. It currently only redirects to the settings page. The page, tests, and all references should be completely removed.

## Current Behavior

- **Route:** `/profile`
- **Action:** Hard redirect to `/settings` using `useRouter.replace('/settings')`
- **Status:** Deprecated (SPRINT-1, Issue #848)

## Impact

- **Maintenance Burden:** 156 lines of test code only verify redirect behavior
- **Dead Code:** Entire page serves no purpose except navigation
- **User Confusion:** Duplicate URLs for same functionality
- **SEO Issues:** Multiple URLs for same content

## Files to Remove

```
apps/web/src/app/profile/page.tsx
apps/web/src/__tests__/pages/profile.test.tsx
```

## Tasks

### 1. Analysis Phase
- [ ] Search for all references to `/profile` route in codebase
  ```bash
  grep -r "/profile" apps/web/src
  grep -r "href.*profile" apps/web/src
  grep -r "router.push.*profile" apps/web/src
  ```
- [ ] Check for any internal links pointing to `/profile`
- [ ] Verify no external documentation references `/profile`

### 2. Update Navigation/Links
- [ ] Search for navigation components with `/profile` links
- [ ] Update all links to point to `/settings` instead
- [ ] Check menu items, sidebars, breadcrumbs

### 3. Add Redirect Rule
- [ ] Add redirect in `next.config.js` or `middleware.ts`:
  ```typescript
  // Redirect old /profile route to /settings
  {
    source: '/profile',
    destination: '/settings',
    permanent: true // 308 permanent redirect
  }
  ```
- [ ] Verify redirect works at framework level (not React Router)

### 4. Remove Files
- [ ] Delete `apps/web/src/app/profile/page.tsx`
- [ ] Delete `apps/web/src/__tests__/pages/profile.test.tsx`

### 5. Testing
- [ ] Test redirect: `curl -I http://localhost:3000/profile`
- [ ] Verify returns 308 redirect to `/settings`
- [ ] Test in browser (should not see React component mount)
- [ ] Run full test suite: `pnpm test`
- [ ] Verify coverage maintained (should increase slightly)

### 6. Documentation
- [ ] Update any docs referencing `/profile` route
- [ ] Add entry to CHANGELOG.md:
  ```markdown
  ### Removed
  - `/profile` page (deprecated in SPRINT-1 #848, use `/settings` instead)
  ```
- [ ] Check README.md for route listings

## Success Criteria

- [ ] `/profile` page files deleted
- [ ] Permanent redirect configured at framework level
- [ ] All internal links updated to `/settings`
- [ ] Tests pass with maintained coverage
- [ ] No console errors or warnings
- [ ] Manual verification of redirect behavior

## Migration Notes

**For users:**
- Old bookmarks to `/profile` will automatically redirect to `/settings`
- No action required

**For developers:**
- Use `/settings` for all profile-related links
- Tab-based navigation in settings page provides organized access

## References

- Original deprecation: SPRINT-1, Issue #848
- Current file: `apps/web/src/app/profile/page.tsx`
- Test file: `apps/web/src/__tests__/pages/profile.test.tsx`
- Replacement: `apps/web/src/app/settings/page.tsx`
- Legacy code analysis: Section 3 (Deprecated Pages & Routes)

## Related Issues

- Issue #001: Consolidate Duplicate Components
- SPRINT-1 documentation

## Notes

**Lines saved:** ~200+ lines (page + tests)
**Risk level:** LOW (page only redirects, no functionality loss)
