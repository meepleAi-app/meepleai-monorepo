# A11y Color Contrast Fix Guide

## Issue Detected

E2E tests detected a **serious** color contrast accessibility violation in a modal component.

```
Violations found in modal: [
  {
    id: 'color-contrast',
    impact: 'serious',
    description: 'Ensure the contrast between foreground and background colors meets WCAG 2 AA minimum contrast ratio thresholds',
    nodes: 1
  }
]
```

## WCAG 2 AA Requirements

### Contrast Ratios
- **Normal Text**: Minimum 4.5:1
- **Large Text** (18pt+ or 14pt+ bold): Minimum 3:1
- **UI Components & Graphics**: Minimum 3:1

## How to Fix

### 1. Identify the Violating Component

The violation is in a modal. Check:
- Auth modals (`AuthModal.tsx`)
- Error modals (`ErrorModal.tsx`)
- Session modals (`SessionWarningModal.tsx`)
- Export modals (`ExportChatModal.tsx`)

### 2. Use Contrast Checker Tools

**Online Tools**:
- WebAIM Contrast Checker: https://webaim.org/resources/contrastchecker/
- Coolors Contrast Checker: https://coolors.co/contrast-checker

**Browser DevTools**:
- Chrome/Edge: Inspect element → Accessibility pane
- Firefox: Inspect element → Accessibility pane

### 3. Common Fixes

#### Update Tailwind Classes

**Before** (Low contrast):
```tsx
<p className="text-gray-400 bg-gray-100">
  Low contrast text
</p>
```

**After** (WCAG 2 AA compliant):
```tsx
<p className="text-gray-700 bg-white">
  High contrast text
</p>
```

#### Update CSS Variables

**Before**:
```css
:root {
  --foreground: hsl(0 0% 70%);  /* Too light */
  --background: hsl(0 0% 100%);
}
```

**After**:
```css
:root {
  --foreground: hsl(0 0% 20%);  /* Dark enough for 4.5:1 */
  --background: hsl(0 0% 100%);
}
```

### 4. Test the Fix

#### Manual Testing
```bash
cd apps/web
pnpm test:a11y  # Run accessibility tests
```

#### E2E Testing
```bash
pnpm test:e2e:auth  # Test auth modals
```

#### Storybook Visual Testing
```bash
pnpm storybook  # View component in isolation
# Use Storybook a11y addon to check contrast
```

## Recommended Color Combinations

### Dark Mode
```tsx
// Text on dark backgrounds
className="text-white bg-gray-900"           // 21:1 ratio
className="text-gray-100 bg-gray-800"        // 15.8:1 ratio
className="text-gray-200 bg-slate-900"       // 13.6:1 ratio
```

### Light Mode
```tsx
// Text on light backgrounds
className="text-gray-900 bg-white"           // 21:1 ratio
className="text-gray-800 bg-gray-50"         // 16.7:1 ratio
className="text-slate-700 bg-white"          // 10.2:1 ratio
```

### Interactive Elements
```tsx
// Buttons and links (minimum 3:1 for UI components)
className="text-blue-700 hover:text-blue-900"
className="bg-blue-600 text-white"
className="border-2 border-gray-700"
```

## Next Steps

1. **Find the exact component**: Review E2E test output for specific modal
2. **Identify the element**: Use browser DevTools to find the low-contrast element
3. **Update colors**: Use WCAG 2 AA compliant color combinations
4. **Test**: Run `pnpm test:a11y` to verify the fix
5. **Document**: Update this file with the specific fix applied

## Related Issues

- Issue #843: E2E test performance and accessibility testing
- Storybook A11y addon: Automatically checks contrast in component stories

## References

- [WCAG 2.1 Contrast Guidelines](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html)
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Tailwind CSS Colors](https://tailwindcss.com/docs/customizing-colors)
