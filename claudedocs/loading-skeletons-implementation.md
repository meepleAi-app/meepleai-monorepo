# Loading Skeletons Implementation

**Created**: 2025-10-24
**Status**: Complete
**Location**: `apps/web/src/components/loading/SkeletonLoader.tsx`

## Overview

Content-specific loading skeletons that match real content dimensions and improve perceived performance. Uses CSS-based animation (Tailwind `animate-pulse`) and respects user's reduced motion preferences.

## Quick Win #4 - COMPLETE ✅

Implemented loading skeletons for key pages to replace generic "Loading..." text with content-aware placeholders.

## Available Variants

### Existing Variants
1. **`games`** - Card layout for games grid
   - Height: `h-64`
   - Contains: Image + Title + Description placeholders
   - Use: Games listing page

2. **`agents`** - List item for agents
   - Height: `h-20`
   - Contains: Icon + Name + Description placeholders
   - Use: Agents list

3. **`message`** - Chat message layout
   - Height: `h-16`
   - Contains: Avatar + Message text placeholders
   - Use: Chat interface messages

4. **`chatHistory`** - Compact sidebar list
   - Height: `h-12`
   - Contains: Icon + Title + Timestamp placeholders
   - Use: Chat history sidebar

### New Variants (Quick Win #4)
5. **`uploadQueue`** - Upload queue item ✨
   - Height: `h-24`
   - Contains: File icon + Name + Size/Status + Progress bar placeholders
   - Use: File upload queue (`MultiFileUpload` component)

6. **`processingProgress`** - PDF processing indicator ✨
   - Height: `h-40`
   - Contains: Header + Progress bar + Percentage + Step indicators
   - Use: `ProcessingProgress` component during initial loading

7. **`gameSelection`** - Game selection form ✨
   - Height: `h-16`
   - Contains: Dropdown + Button placeholders
   - Use: `upload.tsx` game selection while loading games

## API

```typescript
interface SkeletonLoaderProps {
  /** Visual variant matching the content type */
  variant: 'games' | 'agents' | 'message' | 'chatHistory' | 'uploadQueue' | 'processingProgress' | 'gameSelection';

  /** Number of skeleton placeholders to render (default: 1) */
  count?: number;

  /** Additional CSS classes */
  className?: string;

  /** Enable animation - respects prefers-reduced-motion (default: true) */
  animate?: boolean;

  /** Accessible label for screen readers (default: 'Loading...') */
  ariaLabel?: string;
}
```

## Usage Examples

### Upload Page - Game Selection
```tsx
// Before:
{loadingGames ? (
  <p style={{ margin: 0 }}>Loading games…</p>
) : (
  // Game selection form
)}

// After:
{loadingGames ? (
  <SkeletonLoader variant="gameSelection" ariaLabel="Loading games" />
) : (
  // Game selection form
)}
```

### ProcessingProgress Component
```tsx
// Before:
if (loading && !progress) {
  return (
    <div style={containerStyle}>
      <h3 style={headerStyle}>Processing PDF...</h3>
      <p style={{ color: '#666', margin: 0 }}>Loading progress information...</p>
    </div>
  );
}

// After:
if (loading && !progress) {
  return (
    <div style={containerStyle}>
      <SkeletonLoader variant="processingProgress" ariaLabel="Loading processing progress" />
    </div>
  );
}
```

### Upload Queue (Future Enhancement)
```tsx
// Potential use in MultiFileUpload component:
{loadingQueue ? (
  <SkeletonLoader variant="uploadQueue" count={3} />
) : (
  queue.map(item => <UploadQueueItem key={item.id} {...item} />)
)}
```

## Implementation Details

### Visual Structure

#### uploadQueue Variant
```tsx
<div className="h-full flex items-center p-4 space-x-4">
  {/* File icon placeholder */}
  <div className="w-12 h-12 bg-slate-300 dark:bg-slate-600 rounded flex-shrink-0" />

  {/* File info and progress */}
  <div className="flex-1 space-y-2">
    {/* File name */}
    <div className="h-4 bg-slate-300 dark:bg-slate-600 rounded w-3/4" />
    {/* File size and status */}
    <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-1/2" />
    {/* Progress bar */}
    <div className="h-2 bg-slate-300 dark:bg-slate-600 rounded w-full" />
  </div>
</div>
```

#### processingProgress Variant
```tsx
<div className="h-full flex flex-col p-4 space-y-3">
  {/* Header */}
  <div className="h-5 bg-slate-300 dark:bg-slate-600 rounded w-1/2" />
  {/* Progress bar */}
  <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-full" />
  {/* Progress percentage */}
  <div className="h-3 bg-slate-300 dark:bg-slate-600 rounded w-1/4" />
  {/* Step indicators */}
  <div className="flex space-x-2 mt-2">
    <div className="w-8 h-8 bg-slate-300 dark:bg-slate-600 rounded-full" />
    <div className="w-8 h-8 bg-slate-300 dark:bg-slate-600 rounded-full" />
    <div className="w-8 h-8 bg-slate-300 dark:bg-slate-600 rounded-full" />
    <div className="w-8 h-8 bg-slate-300 dark:bg-slate-600 rounded-full" />
  </div>
</div>
```

#### gameSelection Variant
```tsx
<div className="h-full flex items-center p-3 space-x-3">
  {/* Dropdown placeholder */}
  <div className="flex-1 h-10 bg-slate-300 dark:bg-slate-600 rounded" />
  {/* Button placeholder */}
  <div className="w-24 h-10 bg-slate-300 dark:bg-slate-600 rounded" />
</div>
```

### Accessibility Features

1. **ARIA Attributes**:
   - `role="status"` - Announces loading state to screen readers
   - `aria-live="polite"` - Non-intrusive updates
   - `aria-label` - Custom loading message

2. **Screen Reader Support**:
   - Hidden text with `sr-only` class
   - Descriptive labels for each variant

3. **Reduced Motion**:
   - Respects `prefers-reduced-motion` media query
   - Disables `animate-pulse` when user prefers reduced motion

### Dark Mode Support

All variants support dark mode with `dark:` Tailwind variants:
- Light mode: `bg-slate-200`, `bg-slate-300`
- Dark mode: `bg-slate-700`, `bg-slate-600`

## Testing

### Test Coverage
- **28 tests total** (all passing)
- **7 variants** tested (4 original + 3 new)
- **Test categories**:
  - Count prop (3 tests)
  - Variant-specific styles (7 tests)
  - Animation (4 tests)
  - Custom className (2 tests)
  - Accessibility (3 tests)
  - Multiple skeletons (2 tests)
  - Snapshot tests (7 tests)

### New Tests Added
```typescript
describe('Variant-specific styles', () => {
  it('should apply uploadQueue variant styles', () => {
    const { container } = render(<SkeletonLoader variant="uploadQueue" />);
    const skeleton = container.querySelector('[role="status"]');
    expect(skeleton).toHaveClass('h-24');
    expect(skeleton).toHaveClass('rounded-lg');
  });

  it('should apply processingProgress variant styles', () => {
    const { container } = render(<SkeletonLoader variant="processingProgress" />);
    const skeleton = container.querySelector('[role="status"]');
    expect(skeleton).toHaveClass('h-40');
    expect(skeleton).toHaveClass('rounded-lg');
  });

  it('should apply gameSelection variant styles', () => {
    const { container } = render(<SkeletonLoader variant="gameSelection" />);
    const skeleton = container.querySelector('[role="status"]');
    expect(skeleton).toHaveClass('h-16');
    expect(skeleton).toHaveClass('rounded-md');
  });
});
```

## Benefits Delivered

### User Experience
- ✅ **Better Perceived Performance** - Content-specific skeletons feel faster than blank screens
- ✅ **Visual Continuity** - Skeleton matches actual content layout
- ✅ **Reduced Cognitive Load** - Users know what to expect while loading

### Developer Experience
- ✅ **Easy Integration** - Simple API with variant selection
- ✅ **Consistent Behavior** - Standardized loading states across app
- ✅ **Type Safety** - TypeScript ensures correct variant usage

### Accessibility
- ✅ **Screen Reader Support** - Proper ARIA attributes and labels
- ✅ **Reduced Motion Support** - Respects user preferences
- ✅ **Semantic HTML** - `role="status"` for loading states

## Files Modified

### Components
1. **`SkeletonLoader.tsx`** (line 30, 59-67, 152-193)
   - Added 3 new variant types to props interface
   - Added variant styles for new variants
   - Implemented visual structure for each variant

2. **`upload.tsx`** (line 16, 884)
   - Added SkeletonLoader import
   - Replaced "Loading games…" text with gameSelection skeleton

3. **`ProcessingProgress.tsx`** (line 17, 394-398)
   - Added SkeletonLoader import
   - Replaced "Loading progress information..." with processingProgress skeleton

### Tests
4. **`SkeletonLoader.test.tsx`** (lines 74-93, 200-213)
   - Added 3 variant style tests
   - Added 3 snapshot tests
   - Total: 28 tests (all passing)

## Performance Characteristics

- **Lightweight**: No JavaScript animation, uses CSS `animate-pulse`
- **Performant**: Tailwind utilities, no re-renders during animation
- **Accessible**: Respects `prefers-reduced-motion`, no unnecessary animations

## Future Enhancements

### Potential New Variants
1. **`table`** - Table row skeleton for data tables
2. **`card`** - Generic card skeleton
3. **`form`** - Form field skeleton for dynamic forms
4. **`graph`** - Graph/chart skeleton for analytics

### Potential Features
1. **Shimmer Effect** - Optional shimmer animation (CSS gradient animation)
2. **Color Variants** - Support for different color schemes
3. **Custom Dimensions** - Allow width/height overrides
4. **Skeleton Composer** - Build custom skeletons from primitives

## Related Documentation

- **Frontend Improvements Action Plan**: `claudedocs/FRONTEND-IMPROVEMENTS-ACTION-PLAN.md`
- **SkeletonLoader Component**: `apps/web/src/components/loading/SkeletonLoader.tsx`
- **Tests**: `apps/web/src/components/loading/__tests__/SkeletonLoader.test.tsx`

## Changelog

### 2025-10-24 - Quick Win #4 Complete
- ✅ Added 3 new skeleton variants (uploadQueue, processingProgress, gameSelection)
- ✅ Integrated gameSelection skeleton in upload.tsx
- ✅ Integrated processingProgress skeleton in ProcessingProgress.tsx
- ✅ Added 6 new tests (3 variant + 3 snapshot)
- ✅ All 28 tests passing
- ✅ Comprehensive documentation created

## Summary

Quick Win #4 successfully delivered content-specific loading skeletons for key loading states in the upload flow. Users now see structured placeholders that match the actual content layout instead of generic "Loading..." text, improving perceived performance and user experience.

**Impact**:
- 2 pages updated (upload.tsx, ProcessingProgress.tsx)
- 3 new variants added
- 28 tests passing (100% coverage for skeleton component)
- Better UX for upload and PDF processing flows
