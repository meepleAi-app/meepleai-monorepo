# EDIT-05 Step 7: InlineCommentIndicator Component

**Status**: ✅ COMPLETED
**Branch**: edit-05-enhanced-comments
**Component**: `apps/web/src/components/InlineCommentIndicator.tsx`

## Implementation Summary

Created a visual indicator component for displaying inline comments on specific lines in the rich text editor.

## Component Features

### 1. Visual States

**Base State**:
- 32x32px circular button with MessageCircle SVG icon
- Two color schemes based on resolution status:
  - **Resolved**: Light gray background (#f5f5f5), gray border (#ddd), gray icon (#666)
  - **Unresolved**: Yellow background (#fff3cd), orange border/icon (#ff9800)

**Interactive States**:
- Hover: 1.1x scale transform + shadow
- Focus: Blue outline ring (accessibility)
- All transitions: 0.2s ease

### 2. Count Badge

**Display Logic**: Only shows when `commentCount > 1`

**Visual Design**:
- 16x16px circle positioned at top-right (-4px offset)
- Background color:
  - Blue (#0070f3) for resolved comments
  - Orange (#ff9800) for unresolved comments
- White text, 10px bold font
- Displays actual count number

### 3. Unresolved Indicator

**Display Logic**: Only shows when `hasUnresolved = true`

**Visual Design**:
- 8x8px red dot (#d93025) at bottom-right (-2px offset)
- 2px white border for contrast
- Pulsing animation (2s infinite):
  ```css
  @keyframes pulse {
    0%, 100% { opacity: 1; transform: scale(1); }
    50% { opacity: 0.7; transform: scale(1.1); }
  }
  ```

### 4. Hover Tooltip

**Behavior**:
- Appears after 500ms hover delay
- Shows preview of first comment (truncated to 100 chars)
- Positioned above button with arrow pointing down
- Cleans up timer on unmount/mouse leave

**Visual Design**:
- Max width: 300px
- Dark background (#333), white text (12px)
- 8px padding, 4px border-radius
- CSS arrow triangle (6px borders)
- z-index: 1000, pointer-events: none

### 5. Click Interaction

**Handler**: `onClick()` prop callback
- Opens comment thread sidebar
- Passed down from parent component

## Props Interface

```typescript
interface InlineCommentIndicatorProps {
  lineNumber: number;       // Line number for ARIA label
  commentCount: number;     // Total comments on this line
  hasUnresolved: boolean;   // Whether any comments are unresolved
  onClick: () => void;      // Handler to open thread sidebar
  previewText?: string;     // First comment text for tooltip
}
```

## Accessibility Features

### ARIA Attributes
- `role="button"` - Semantic button role
- `aria-label` - Descriptive label: "View {count} comment(s) on line {lineNumber}"
- `aria-pressed="false"` - Button state (can be updated to "true" when thread open)
- `title` - Native browser tooltip with preview text fallback

### Keyboard Support
- **Enter** key: Activate button (open thread)
- **Space** key: Activate button (open thread)
- **Tab**: Focus navigation with visible focus ring

### Focus Management
- Blue outline ring on keyboard focus (0 0 0 3px rgba(0,112,243,0.3))
- Removed on blur
- Maintains separate hover/focus styles

## Technical Implementation

### State Management
- `showTooltip` - Controls tooltip visibility
- `tooltipTimer` - NodeJS.Timeout for 500ms delay
- Cleanup in useEffect on unmount

### Event Handlers
- `handleMouseEnter` - Start 500ms timer for tooltip
- `handleMouseLeave` - Cancel timer, hide tooltip
- `handleClick` - Call onClick prop
- `handleKeyDown` - Handle Enter/Space keys

### Styling Approach
- Inline styles (consistent with CommentItem, CommentForm)
- Dynamic styles via inline conditionals (resolved/unresolved)
- CSS keyframes in `<style>` tag for pulse animation
- Hover effects via onMouseOver/onMouseOut

### Icon Choice
- SVG inline (MessageCircle from lucide-react design)
- 16x16px viewBox scaled from 24x24
- Stroke-based design (no fill)
- 2px stroke width, rounded caps/joins

## UX Considerations

### Visual Hierarchy
1. **Unresolved state** most prominent (orange + pulsing dot)
2. **Count badge** secondary (visible but not distracting)
3. **Resolved state** subtle (gray, blends with UI)

### Feedback Mechanisms
- Immediate hover scale/shadow for interactivity
- 500ms tooltip delay prevents accidental popups
- Pulsing animation draws attention to unresolved items
- Count badge provides at-a-glance information

### Performance
- Timer cleanup prevents memory leaks
- pointerEvents: none on tooltip (doesn't interfere with mouse)
- Minimal re-renders (only on prop changes)

### Mobile Considerations
- 32px touch target meets accessibility standards
- Hover states degrade gracefully on touch devices
- Native title attribute provides fallback tooltip

## Integration Points

### Parent Component Responsibilities
1. Track line-level comment data (count, resolved status)
2. Provide onClick handler to open thread sidebar
3. Pass previewText (first comment content)
4. Position indicator relative to code lines

### Sidebar Integration
- `onClick()` should:
  - Open/focus comment thread sidebar
  - Scroll to relevant line's comments
  - Update `aria-pressed` state (if implemented)

## Testing Recommendations

### Unit Tests (Jest)
```typescript
describe('InlineCommentIndicator', () => {
  test('renders with correct count badge');
  test('shows unresolved dot when hasUnresolved=true');
  test('hides tooltip on unmount (cleanup)');
  test('calls onClick on click');
  test('calls onClick on Enter key');
  test('shows tooltip after 500ms hover');
  test('applies resolved/unresolved styles');
});
```

### E2E Tests (Playwright)
```typescript
test('inline comment indicator interaction', async ({ page }) => {
  // Hover over indicator → verify tooltip appears
  // Click indicator → verify sidebar opens
  // Verify unresolved visual states
  // Keyboard navigation → Enter key activation
});
```

## Verification

✅ TypeScript compilation successful (Next.js build passes)
✅ All required visual states implemented
✅ Accessibility features complete (ARIA, keyboard, focus)
✅ Hover tooltip with 500ms delay and cleanup
✅ Pulsing animation for unresolved indicator
✅ Inline styles consistent with existing components

## Next Steps

**Step 8**: Integrate InlineCommentIndicator into editor layout
- Position indicators at line gutters
- Wire up onClick to open thread sidebar
- Pass line-level comment data as props
- Handle sidebar state management

## Files Created

- `apps/web/src/components/InlineCommentIndicator.tsx` (213 lines)
