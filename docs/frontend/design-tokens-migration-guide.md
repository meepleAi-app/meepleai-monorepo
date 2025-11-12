# Design Tokens Migration Guide

**Issue**: [#928 - Design tokens migration to CSS variables](https://github.com/yourusername/meepleai-monorepo/issues/928)
**Date**: 2025-11-12
**Status**: ✅ Complete

## Overview

This guide documents the migration from hardcoded Tailwind colors to CSS variables using HSL format for full theming support.

## What Changed

### 1. Color System Architecture

**Before**:
- Hardcoded hex colors in `tailwind.config.js`
- Mixed OKLCH (shadcn/ui) and hex (brand colors)
- No centralized theme management

**After**:
- HSL CSS variables in `globals.css`
- Centralized theme tokens with light/dark mode support
- Full Tailwind v4 `@theme` directive integration

### 2. File Changes

#### `globals.css`
- **Migrated `@theme` section**: Converted from hex to HSL CSS variables
- **Added `:root` brand colors**: Complete HSL shade system (50-900)
- **Enhanced `.dark` mode**: Brand color adjustments for dark backgrounds
- **Semantic tokens**: shadcn/ui tokens converted from OKLCH to HSL

#### `tailwind.config.js`
- **Removed**: 47 lines of hardcoded color definitions
- **Added**: `darkMode: ['class']` configuration
- **Kept**: Animation and keyframe definitions

#### New Component
- **`ThemeSwitcher.tsx`**: Animated light/dark mode toggle with next-themes integration

## Color Mapping

### Brand Colors (HSL Format)

#### Primary (Blue #0070f3)
```css
:root {
  --primary-500: 211 100% 48%;  /* #0070f3 */
  --primary-600: 216 100% 40%;  /* #0052cc */
}

.dark {
  --primary-500: 211 100% 58%;  /* Lighter for dark bg */
  --primary-600: 211 100% 50%;
}
```

#### Secondary (Green #34a853)
```css
:root {
  --secondary-500: 135 53% 43%;  /* #34a853 */
}

.dark {
  --secondary-500: 135 53% 53%;  /* Lighter for dark bg */
}
```

#### Accent (Orange #ff9800)
```css
:root {
  --accent-500: 36 100% 50%;  /* #ff9800 */
}

.dark {
  --accent-500: 36 100% 60%;  /* Lighter for dark bg */
}
```

### Usage in Components

**Before**:
```tsx
<button className="bg-primary-500 hover:bg-primary-600">
  Click me
</button>
```

**After** (same syntax, now uses CSS variables):
```tsx
<button className="bg-primary-500 hover:bg-primary-600">
  Click me
</button>
```

The migration is **fully backward compatible**. All existing `primary-500`, `secondary-600`, etc. classes continue to work.

## Adding the Theme Switcher

### 1. Wrap your app with ThemeProvider

```tsx
// pages/_app.tsx
import { ThemeProvider } from '@/components/ThemeSwitcher';

export default function App({ Component, pageProps }) {
  return (
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
      <Component {...pageProps} />
    </ThemeProvider>
  );
}
```

### 2. Add ThemeSwitcher component to your layout

```tsx
import { ThemeSwitcher } from '@/components/ThemeSwitcher';

export default function Layout({ children }) {
  return (
    <div>
      <nav>
        {/* Your navigation */}
        <ThemeSwitcher />
      </nav>
      <main>{children}</main>
    </div>
  );
}
```

## Creating Custom Themes

### Adding New Brand Colors

1. Define HSL values in `:root` and `.dark`:

```css
/* globals.css */
:root {
  --custom-50: 280 100% 97%;
  --custom-100: 280 95% 93%;
  /* ... */
  --custom-500: 280 100% 50%;
  /* ... */
  --custom-900: 280 64% 23%;
}

.dark {
  --custom-500: 280 100% 60%;  /* Adjusted for dark mode */
}
```

2. Register in `@theme` directive:

```css
@theme {
  --color-custom-50: hsl(var(--custom-50));
  --color-custom-100: hsl(var(--custom-100));
  /* ... */
  --color-custom-500: hsl(var(--custom-500));
  /* ... */
  --color-custom-900: hsl(var(--custom-900));
}
```

3. Use in components:

```tsx
<div className="bg-custom-500 text-custom-50">
  Custom themed content
</div>
```

## WCAG Contrast Requirements

### Light Mode
- **Normal text** (< 18pt): 4.5:1 minimum contrast
- **Large text** (≥ 18pt): 3:1 minimum contrast

### Dark Mode
- Brand colors are **10% lighter** than light mode for better visibility
- Example: `--primary-500: 211 100% 48%` → `211 100% 58%` in dark mode

### Testing Contrast

Use browser DevTools or online tools:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- Chrome DevTools: Inspect → Accessibility → Contrast ratio

## Migration Checklist

- [x] Audit current color palette in `tailwind.config.js`
- [x] Define semantic color tokens in HSL format
- [x] Migrate `@theme` section to CSS variables
- [x] Create `:root` with brand color shades (50-900)
- [x] Add `.dark` mode color overrides
- [x] Update `tailwind.config.js` to use CSS variables
- [x] Create `ThemeSwitcher` component
- [x] Install `next-themes` dependency
- [x] Test build compilation
- [x] Verify visual rendering on key pages
- [x] WCAG 2.1 AA contrast validation

## Benefits

1. **Centralized Theming**: All colors managed in one place (`globals.css`)
2. **Dark Mode Support**: Native light/dark theme switching
3. **Maintainability**: Update colors without touching components
4. **Accessibility**: Easier to ensure WCAG compliance across themes
5. **Flexibility**: Easy to add new themes (e.g., high contrast, custom brands)
6. **Backward Compatible**: Existing Tailwind classes work unchanged

## Performance Impact

- **Build time**: No change (CSS variables resolved at runtime)
- **Bundle size**: -47 lines in config, +180 lines CSS variables = Net +133 lines
- **Runtime**: Negligible (CSS variable lookup is native browser feature)

## Troubleshooting

### Colors not applying
1. Check if CSS variables are defined in `:root` or `.dark`
2. Verify `@theme` directive references the correct variable names
3. Ensure `darkMode: ['class']` is set in `tailwind.config.js`

### Theme switcher not working
1. Verify `ThemeProvider` wraps your app
2. Check `next-themes` is installed: `pnpm list next-themes`
3. Ensure `.dark` class is applied to `<html>` element in browser DevTools

### Build errors
1. Run `pnpm typecheck` to identify TypeScript issues
2. Check Tailwind v4 compatibility with `@theme` directive
3. Verify CSS variable syntax: `hsl(var(--variable-name))`

## References

- [shadcn/ui Theming Documentation](https://ui.shadcn.com/docs/theming)
- [Tailwind CSS v4 @theme Directive](https://tailwindcss.com/docs/v4/css-directives#theme)
- [next-themes GitHub](https://github.com/pacocoursey/next-themes)
- [HSL Color Format](https://developer.mozilla.org/en-US/docs/Web/CSS/color_value/hsl)
- [WCAG 2.1 Contrast Guidelines](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html)

## Support

For questions or issues related to this migration:
- **GitHub Issue**: [#928](https://github.com/yourusername/meepleai-monorepo/issues/928)
- **Documentation**: `docs/frontend/`
- **Team**: @frontend-team

---

**Migration Author**: Claude Code AI
**Reviewed by**: [Pending]
**Version**: 1.0.0
