# Admin Pages - Chromatic Visual Regression Tests

**Issue**: #2333 (sub-issue of #2306 Testing Epic)
**Created**: 2026-01-06
**Pattern**: Based on `api-keys/__tests__/visual/chromatic.stories.tsx` template

## Overview

Created comprehensive Chromatic visual regression tests for 3 Admin pages:
1. Admin Configuration page
2. Alert Dashboard  
3. User Management (Management page)

## Files Created

### 1. Admin Configuration (`configuration/__tests__/visual/chromatic.stories.tsx`)
**Location**: `apps/web/src/app/admin/configuration/__tests__/visual/chromatic.stories.tsx`
**Lines**: 322
**Stories**: 11

**Coverage**:
- ✅ Default view (Feature Flags tab with data)
- ✅ Empty state (no configurations)
- ✅ Loading state
- ✅ Error state
- ✅ Rate Limiting tab
- ✅ AI / LLM tab
- ✅ RAG tab
- ✅ Banner dismissed state
- ✅ Stats footer display
- ✅ Mobile responsive view
- ✅ Tablet responsive view

**Mock Data**: 8 configurations across 4 categories (Features, RateLimiting, AiLlm, Rag)

### 2. Alert Dashboard (`alerts/__tests__/visual/chromatic.stories.tsx`)
**Location**: `apps/web/src/app/admin/alerts/__tests__/visual/chromatic.stories.tsx`
**Lines**: 375
**Stories**: 14

**Coverage**:
- ✅ Default view (active alerts only)
- ✅ No alerts (empty state)
- ✅ All alerts (active + resolved)
- ✅ Loading state
- ✅ Error state
- ✅ Critical alerts only
- ✅ Resolved alerts only
- ✅ Metadata dialog open
- ✅ Resolve alert confirmation
- ✅ Severity badges display (Critical, Error, Warning, Info)
- ✅ Auto-refresh active indicator
- ✅ Channel delivery status in metadata
- ✅ Mobile responsive view
- ✅ Tablet responsive view

**Mock Data**: 5 alerts with varying severities and channel delivery statuses

### 3. User Management (`management/__tests__/visual/chromatic.stories.tsx`)
**Location**: `apps/web/src/app/admin/management/__tests__/visual/chromatic.stories.tsx`
**Lines**: 451
**Stories**: 15

**Coverage**:
- ✅ Default view (API Keys tab)
- ✅ API Keys empty state
- ✅ API Keys loading state
- ✅ Users tab with data
- ✅ Users empty state
- ✅ Users loading state
- ✅ Activity tab with events
- ✅ Activity empty state
- ✅ Bulk selection active (API Keys)
- ✅ Create API Key modal open
- ✅ Filters applied
- ✅ Export confirmation dialog
- ✅ Delete confirmation dialog
- ✅ Mobile responsive view
- ✅ Tablet responsive view

**Mock Data**: 2 API keys with stats, 3 users, 3 activity events

## Pattern Consistency

All stories follow the established template pattern:

```typescript
const meta: Meta<typeof Component> = {
  title: 'Admin/{Page}/Visual Tests',
  component: Component,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};
```

## Key Features

### Mock Data Strategy
- Comprehensive mock datasets covering all UI states
- Realistic data reflecting production scenarios
- Proper TypeScript typing using API schemas

### Interactive Stories
- Uses `play` function with `@storybook/test` for interactions
- Simulates user actions (clicks, tab switches, modal opens)
- Validates expected UI changes with `expect` assertions

### Visual Regression Coverage
- Multiple UI states per component
- Error and loading states
- Empty states
- Modal/dialog interactions
- Responsive breakpoints (mobile, tablet, desktop)

### Responsive Testing
- Mobile viewport: 375x667
- Tablet viewport: 768x1024
- Desktop viewport: default (1024x768)

## Total Coverage

**Files**: 3 chromatic.stories.tsx files
**Total Lines**: 1,148
**Total Stories**: 40 visual test scenarios
**Pages Covered**: 3 admin pages

## Storybook Build

Build command: `pnpm build-storybook`
- ✅ No TypeScript errors
- ✅ No ESLint warnings
- ✅ All stories compile successfully
- ✅ Mock data properly typed

## Next Steps

1. **Run Chromatic**: Upload to Chromatic for baseline snapshots
2. **Review Snapshots**: Verify all UI states render correctly
3. **Integrate CI**: Add Chromatic checks to GitHub Actions
4. **Expand Coverage**: Consider adding more admin pages as needed

## Related Issues

- Parent Issue: #2306 (Testing Epic - Week 2)
- Sub-issue: #2333 (Chromatic Visual Tests)
- Template Source: `api-keys/__tests__/visual/chromatic.stories.tsx`

## Notes

- All mock data uses realistic scenarios (production keys, alert types, user roles)
- Channel delivery status properly mocked for alerts
- User activity timeline includes various action types
- Severity badges cover all levels (critical, error, warning, info)
- Responsive tests ensure mobile-first design works across devices
