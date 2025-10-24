# SimpleErrorMessage Component

**Created**: 2025-10-24
**Location**: `apps/web/src/components/SimpleErrorMessage.tsx`
**Tests**: `apps/web/src/components/__tests__/SimpleErrorMessage.test.tsx`

## Overview

Lightweight, reusable component for inline error/warning/info/success messages. Provides consistent styling and accessibility across the application.

## When to Use

### Use SimpleErrorMessage For:
- ✅ Simple inline error/warning/info messages
- ✅ Form validation feedback
- ✅ API error responses displayed inline
- ✅ Status messages that don't require retry functionality
- ✅ Messages that can be dismissed by the user

### Use ErrorDisplay For:
- ❌ Complex errors requiring retry functionality
- ❌ Errors with correlation IDs and technical details
- ❌ Categorized errors (network, validation, auth, etc.)
- ❌ Errors with actionable suggestions

## API

```typescript
interface SimpleErrorMessageProps {
  /** Error message to display. If null/undefined, component renders nothing. */
  message: string | null | undefined;

  /** Visual variant of the message (default: 'error') */
  variant?: 'error' | 'warning' | 'info' | 'success';

  /** Optional callback when dismiss button is clicked */
  onDismiss?: () => void;

  /** Additional CSS classes to apply */
  className?: string;
}
```

## Usage Examples

### Basic Error Message
```tsx
import { SimpleErrorMessage } from '@/components/SimpleErrorMessage';

function MyForm() {
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  return (
    <form>
      <SimpleErrorMessage message={errorMessage} />
      {/* Form fields */}
    </form>
  );
}
```

### Warning with Dismiss Button
```tsx
function MyPage() {
  const [warning, setWarning] = useState<string | null>(
    'Your session will expire in 5 minutes'
  );

  return (
    <SimpleErrorMessage
      message={warning}
      variant="warning"
      onDismiss={() => setWarning(null)}
    />
  );
}
```

### Info Message with Custom Styling
```tsx
function Dashboard() {
  return (
    <SimpleErrorMessage
      message="New features available! Check the changelog."
      variant="info"
      className="mb-4"
    />
  );
}
```

### Success Message
```tsx
function UploadPage() {
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleUpload = async () => {
    // ... upload logic
    setSuccessMessage('File uploaded successfully!');
  };

  return (
    <SimpleErrorMessage
      message={successMessage}
      variant="success"
      onDismiss={() => setSuccessMessage(null)}
    />
  );
}
```

## Variants

### Error (Default)
- **Color**: Red (`text-red-400`)
- **Background**: Semi-transparent red (`bg-red-500/10`)
- **Border**: Red (`border-red-500/30`)
- **Use Case**: Errors, failures, critical issues

### Warning
- **Color**: Yellow (`text-yellow-400`)
- **Background**: Semi-transparent yellow (`bg-yellow-500/10`)
- **Border**: Yellow (`border-yellow-500/30`)
- **Use Case**: Warnings, cautions, non-critical issues

### Info
- **Color**: Blue (`text-blue-400`)
- **Background**: Semi-transparent blue (`bg-blue-500/10`)
- **Border**: Blue (`border-blue-500/30`)
- **Use Case**: Informational messages, tips, guidance

### Success
- **Color**: Green (`text-green-400`)
- **Background**: Semi-transparent green (`bg-green-500/10`)
- **Border**: Green (`border-green-500/30`)
- **Use Case**: Success confirmations, completions

## Accessibility

The component follows WCAG 2.1 Level AA guidelines:

- **ARIA Role**: `role="alert"` for screen reader announcements
- **Live Region**: `aria-live="polite"` for non-intrusive updates
- **Dismiss Button**: Clear `aria-label="Dismiss message"` for screen readers
- **Keyboard Support**: Full keyboard navigation with visible focus indicators
- **Color Contrast**: All variants meet WCAG AA contrast requirements
- **Focus Management**: Dismiss button has `focus:ring-2` for clear focus state

## Testing

Comprehensive test suite with **26 tests** covering:

### Rendering
- ✅ Renders message when provided
- ✅ Returns null for null/undefined/empty messages
- ✅ Proper DOM structure

### Accessibility
- ✅ `role="alert"` present
- ✅ `aria-live="polite"` present
- ✅ Dismiss button has accessible label
- ✅ Focus management and keyboard navigation

### Variants
- ✅ All 4 variants apply correct styles
- ✅ Default variant is 'error'

### Dismiss Functionality
- ✅ Dismiss button renders when `onDismiss` provided
- ✅ Dismiss button hidden when `onDismiss` not provided
- ✅ `onDismiss` callback invoked on click
- ✅ Hover states work correctly

### Custom Styling
- ✅ Custom `className` applied
- ✅ Default classes preserved with custom classes

### Content
- ✅ Multiline messages render correctly
- ✅ Long messages (500+ chars) render correctly
- ✅ Special characters escaped (XSS protection)

### Edge Cases
- ✅ Rapid variant changes
- ✅ Dynamic `onDismiss` addition/removal
- ✅ Message changes without unmounting

### Type Safety
- ✅ Accepts all valid props
- ✅ Works with minimal props

## Migration Guide

### Before (Inline Error Display)
```tsx
// Old pattern: inconsistent styling, duplicate code
{errorMessage && (
  <div
    role="alert"
    aria-live="polite"
    className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg"
  >
    {errorMessage}
  </div>
)}
```

### After (SimpleErrorMessage)
```tsx
// New pattern: consistent, reusable, accessible
<SimpleErrorMessage message={errorMessage} />
```

### Files to Migrate
Based on codebase analysis, these files use inline error display patterns:

1. **`pages/chat.tsx`** (line ~1046)
   - Current: Inline div with `role="alert"`
   - Migration: Replace with `<SimpleErrorMessage message={error} />`

2. **`pages/editor.tsx`** (line ~300)
   - Current: Inline div with red background
   - Migration: Replace with `<SimpleErrorMessage message={errorMessage} />`

3. **`pages/versions.tsx`** (line ~276)
   - Current: Inline div with `role="alert"`
   - Migration: Replace with `<SimpleErrorMessage message={error} />`

4. **`pages/reset-password.tsx`** (lines ~345, 435, 459)
   - Current: Multiple inline divs with inconsistent styling
   - Migration: Replace all with `<SimpleErrorMessage message={...} />`

5. **`pages/index.tsx`** (line ~424)
   - Current: Inline div with red border
   - Migration: Replace with `<SimpleErrorMessage message={error} />`

6. **`pages/setup.tsx`** (line ~502)
   - Current: Inline div with `role="alert"`
   - Migration: Replace with `<SimpleErrorMessage message={error} />`

7. **`pages/chess.tsx`** (line ~407)
   - Current: Inline styles with red background
   - Migration: Replace with `<SimpleErrorMessage message={error} />`

## Benefits

### Consistency
- ✅ Uniform error display across entire application
- ✅ Predictable UX for users
- ✅ Single source of truth for error styling

### Accessibility
- ✅ WCAG 2.1 Level AA compliance
- ✅ Screen reader support
- ✅ Keyboard navigation
- ✅ Proper ARIA attributes

### Maintainability
- ✅ Single component to update for styling changes
- ✅ Centralized accessibility improvements
- ✅ Type-safe API with TypeScript
- ✅ Comprehensive test coverage

### Developer Experience
- ✅ Simple API - just pass `message` prop
- ✅ Optional variants for different message types
- ✅ Optional dismiss functionality
- ✅ Custom styling support via `className`

## Implementation Details

### Styling Architecture
- **Framework**: Tailwind CSS
- **Color Opacity**: Uses `/10` and `/30` opacity for backgrounds and borders
- **Spacing**: Consistent `px-4 py-3` padding
- **Typography**: `text-sm` with `leading-relaxed` for readability
- **Rounding**: `rounded-lg` for modern look
- **Hover States**: `opacity-70` → `opacity-100` transition on dismiss button

### Component Structure
```tsx
<div role="alert" aria-live="polite" className="...">
  <div className="flex items-start justify-between gap-3">
    <p className="flex-1 text-sm leading-relaxed">{message}</p>
    {onDismiss && (
      <button aria-label="Dismiss message">
        <svg>{/* X icon */}</svg>
      </button>
    )}
  </div>
</div>
```

### Performance Considerations
- ✅ Renders `null` when no message (no DOM overhead)
- ✅ Minimal re-renders (only when props change)
- ✅ Lightweight (no heavy dependencies)
- ✅ Tree-shakeable with ES modules

## Future Enhancements

Potential future improvements:

1. **Auto-dismiss Timer**
   ```tsx
   <SimpleErrorMessage
     message="Saved!"
     variant="success"
     autoDismissMs={3000}
   />
   ```

2. **Icon Support**
   ```tsx
   <SimpleErrorMessage
     message="Error"
     variant="error"
     showIcon={true}  // Display error/warning/info icon
   />
   ```

3. **Animation**
   - Slide-in/fade-in entrance animation
   - Slide-out/fade-out exit animation
   - Using Framer Motion or CSS transitions

4. **Multiple Messages**
   - Stack multiple messages vertically
   - Unique keys for each message
   - Queue management

## Related Components

- **ErrorDisplay**: Complex error display with retry/correlation ID support
- **ErrorModal**: Modal-style error display (existing)
- **Toast**: Future component for temporary notifications

## Related Documentation

- **Frontend Improvements Action Plan**: `claudedocs/FRONTEND-IMPROVEMENTS-ACTION-PLAN.md`
- **Centralized Types**: `claudedocs/centralized-types-structure.md`
- **Component Testing Guide**: (future documentation)

## Changelog

### 2025-10-24 - Initial Release
- ✅ Created SimpleErrorMessage component
- ✅ Added comprehensive test suite (26 tests, 100% passing)
- ✅ Added documentation
- ✅ Support for 4 variants (error, warning, info, success)
- ✅ WCAG 2.1 Level AA accessibility
- ✅ Optional dismiss functionality
- ✅ Custom styling support
