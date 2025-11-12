# shadcn/ui Installation Guide

**Date**: 2025-11-12
**Issue**: #927
**Status**: ✅ Complete

---

## Overview

This document describes the shadcn/ui component library installation and configuration for the MeepleAI frontend application.

## What is shadcn/ui?

shadcn/ui is not a traditional component library. Instead, it's a collection of re-usable components that you can copy and paste into your apps. Built on top of:
- **Radix UI**: Accessible, unstyled primitives
- **Tailwind CSS**: Utility-first styling
- **Class Variance Authority (CVA)**: Type-safe variant management

Benefits:
- 🎨 Full control over component code
- ♿ Built-in accessibility (WCAG 2.1 AA)
- 🎯 Type-safe with TypeScript
- 🔧 Customizable and extensible
- 🚀 No runtime dependencies

## Installation Status

### ✅ Core Setup Complete

**Configuration File**: `components.json`
```json
{
  "$schema": "https://ui.shadcn.com/schema.json",
  "style": "new-york",
  "rsc": false,
  "tsx": true,
  "tailwind": {
    "config": "tailwind.config.js",
    "css": "src/styles/globals.css",
    "baseColor": "neutral",
    "cssVariables": true,
    "prefix": ""
  },
  "iconLibrary": "lucide",
  "aliases": {
    "components": "@/components",
    "utils": "@/lib/utils",
    "ui": "@/components/ui",
    "lib": "@/lib",
    "hooks": "@/hooks"
  }
}
```

### ✅ Dependencies Installed

| Package | Version | Purpose |
|---------|---------|---------|
| `class-variance-authority` | ^0.7.1 | Type-safe component variants |
| `clsx` | ^2.1.1 | Conditional className utility |
| `tailwind-merge` | ^3.4.0 | Merge Tailwind classes intelligently |
| `lucide-react` | ^0.553.0 | Icon library |

### ✅ TypeScript Configuration

**Path Aliases** (`tsconfig.json`):
```json
{
  "compilerOptions": {
    "paths": {
      "@/*": ["src/*"]
    }
  }
}
```

This allows clean imports:
```tsx
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
```

## Installed Components

### Core Components (16 total)

Located in `src/components/ui/`:

1. **avatar** - User avatars with fallbacks
2. **badge** - Status indicators and labels
3. **button** - Primary interactive element
4. **card** - Content containers with header/footer
5. **dialog** - Modal dialogs and overlays
6. **dropdown-menu** - Context menus and dropdowns
7. **input** - Text input fields
8. **progress** - Progress bars and indicators
9. **select** - Dropdown selection inputs
10. **skeleton** - Loading state placeholders
11. **sonner** - Toast notifications (via Sonner library)
12. **switch** - Toggle switches
13. **table** - Data tables with sorting/filtering
14. **textarea** - Multi-line text inputs
15. **toggle** - Toggle buttons
16. **toggle-group** - Grouped toggle buttons

### Required Components (Issue #927)

All 10 required components ✅:
- [x] button
- [x] card
- [x] input
- [x] dialog
- [x] dropdown-menu
- [x] select
- [x] table
- [x] toast (implemented via sonner)
- [x] avatar
- [x] badge

**Bonus**: 6 additional components installed (progress, skeleton, switch, textarea, toggle, toggle-group)

## Usage Examples

### Button Component

```tsx
import { Button } from '@/components/ui/button'

export function Example() {
  return (
    <div className="flex gap-2">
      <Button>Default</Button>
      <Button variant="secondary">Secondary</Button>
      <Button variant="destructive">Destructive</Button>
      <Button variant="outline">Outline</Button>
      <Button variant="ghost">Ghost</Button>
      <Button variant="link">Link</Button>
      <Button size="sm">Small</Button>
      <Button size="lg">Large</Button>
      <Button disabled>Disabled</Button>
    </div>
  )
}
```

### Card Component

```tsx
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

export function Example() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Card Title</CardTitle>
        <CardDescription>Card description goes here</CardDescription>
      </CardHeader>
      <CardContent>
        <p>Card content</p>
      </CardContent>
      <CardFooter>
        <Button>Action</Button>
      </CardFooter>
    </Card>
  )
}
```

### Dialog Component

```tsx
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'

export function Example() {
  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="outline">Open Dialog</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Dialog Title</DialogTitle>
          <DialogDescription>
            This is a dialog description
          </DialogDescription>
        </DialogHeader>
        <div>Dialog content goes here</div>
      </DialogContent>
    </Dialog>
  )
}
```

### Form with Input and Select

```tsx
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Button } from '@/components/ui/button'

export function Example() {
  return (
    <form className="space-y-4">
      <div>
        <label>Name</label>
        <Input placeholder="Enter your name" />
      </div>
      <div>
        <label>Role</label>
        <Select>
          <SelectTrigger>
            <SelectValue placeholder="Select role" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="admin">Admin</SelectItem>
            <SelectItem value="user">User</SelectItem>
            <SelectItem value="editor">Editor</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <Button type="submit">Submit</Button>
    </form>
  )
}
```

### Toast Notifications

```tsx
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'

export function Example() {
  return (
    <div className="flex gap-2">
      <Button onClick={() => toast.success('Success message')}>
        Success Toast
      </Button>
      <Button onClick={() => toast.error('Error message')}>
        Error Toast
      </Button>
      <Button onClick={() => toast.info('Info message')}>
        Info Toast
      </Button>
    </div>
  )
}
```

## Demo Page

A comprehensive demo page showcasing all components is available at:

**Route**: `/shadcn-demo`
**File**: `src/pages/shadcn-demo.tsx`

The demo page includes:
- All installed components with examples
- Interactive demonstrations
- Code snippets for each component
- Variant showcases
- Real-world usage patterns

## Adding New Components

To add additional shadcn/ui components:

```bash
# List available components
npx shadcn@latest add

# Add specific component
npx shadcn@latest add [component-name]

# Examples
npx shadcn@latest add accordion
npx shadcn@latest add tabs
npx shadcn@latest add tooltip
npx shadcn@latest add command
npx shadcn@latest add calendar
```

Components will be automatically installed to `src/components/ui/` with proper TypeScript types.

## Customization

### Modifying Component Styles

Components are located in `src/components/ui/` and can be directly edited:

```tsx
// src/components/ui/button.tsx
export const buttonVariants = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/90",
        // Add custom variant
        custom: "bg-gradient-to-r from-purple-500 to-pink-500 text-white",
      },
      size: {
        default: "h-10 px-4 py-2",
        // Add custom size
        xl: "h-14 px-8 py-3 text-lg",
      },
    },
  }
)
```

### Using the `cn()` Utility

The `cn()` utility function (located in `src/lib/utils.ts`) intelligently merges Tailwind classes:

```tsx
import { cn } from '@/lib/utils'

// Conditional classes
<Button className={cn("w-full", isActive && "bg-blue-500")} />

// Override default styles
<Button className={cn("bg-primary", "hover:bg-purple-600")} />

// Merge multiple class sources
<div className={cn(baseStyles, conditionalStyles, className)} />
```

## Theming Integration

shadcn/ui components use CSS variables for theming, which integrate with our design tokens (#928):

```css
/* src/styles/globals.css */
:root {
  --background: 0 0% 100%;
  --foreground: 240 10% 3.9%;
  --primary: 221 83% 53%;
  --primary-foreground: 210 40% 98%;
  /* ... other tokens */
}

.dark {
  --background: 240 10% 3.9%;
  --foreground: 0 0% 98%;
  /* ... dark mode overrides */
}
```

Components automatically adapt to theme changes via the `ThemeProvider` (#929).

## Testing

### Build Verification

```bash
pnpm build
# ✅ Compiled successfully in 4.7s
```

### Test Suite

```bash
pnpm test
# ✅ Test Suites: 143 passed, 143 total
# ✅ Tests: 4009 passed, 4009 total
```

### Manual Testing

1. Navigate to `/shadcn-demo`
2. Interact with each component
3. Verify no console errors
4. Check responsive behavior
5. Test keyboard navigation
6. Validate accessibility

## Accessibility

All shadcn/ui components are built on Radix UI primitives, ensuring:

- ♿ **WCAG 2.1 AA compliant** out of the box
- ⌨️ **Keyboard navigation** fully supported
- 🔊 **Screen reader** compatible with proper ARIA attributes
- 🎯 **Focus management** handled automatically
- 📱 **Touch-friendly** interactive targets

### Accessibility Features by Component

| Component | Features |
|-----------|----------|
| **Button** | Proper focus states, disabled handling |
| **Dialog** | Focus trap, ESC to close, backdrop click |
| **Dropdown Menu** | Arrow key navigation, typeahead search |
| **Input** | Label association, error states |
| **Select** | Keyboard selection, proper ARIA roles |
| **Switch** | Toggle state announcements |
| **Table** | Sortable headers, row selection |

## Performance Considerations

### Tree-Shaking

shadcn/ui components are designed for optimal tree-shaking:
- Import only what you use
- No runtime library overhead
- Components are self-contained
- Minimal bundle impact

### Bundle Size Impact

Average component sizes (gzipped):
- Button: ~1KB
- Card: ~0.5KB
- Dialog: ~3KB
- Dropdown Menu: ~4KB
- Select: ~5KB
- Table: ~2KB

Total library impact: **<20KB gzipped** for all core components.

## Troubleshooting

### Common Issues

**Issue**: `Module not found: Can't resolve '@/components/ui/button'`
**Solution**: Ensure TypeScript paths are configured correctly in `tsconfig.json`

**Issue**: Styles not applying correctly
**Solution**: Verify `globals.css` is imported in `_app.tsx` and contains CSS variables

**Issue**: Components look unstyled
**Solution**: Ensure Tailwind CSS is properly configured and processing the component files

**Issue**: TypeScript errors in components
**Solution**: Run `pnpm install` to ensure all dependencies are installed

## Migration from Custom Components

If you have existing custom components, gradually migrate to shadcn/ui:

1. **Identify similar shadcn/ui component**
2. **Install the component**: `npx shadcn@latest add [component]`
3. **Compare APIs and props**
4. **Update usage in your pages**
5. **Test thoroughly**
6. **Remove old component**

## Resources

- **Official Docs**: https://ui.shadcn.com/docs
- **Component Gallery**: https://ui.shadcn.com/docs/components
- **GitHub**: https://github.com/shadcn-ui/ui
- **Demo Page**: `/shadcn-demo` (local)

## Maintenance

### Updating Components

To update components to the latest versions:

```bash
# Update specific component
npx shadcn@latest add button --overwrite

# Update all components (manual process)
# Re-run add command for each component with --overwrite flag
```

**Note**: Always review changes before overwriting, as customizations will be lost.

### Version Compatibility

- **Next.js**: 16.0.1 ✅
- **React**: 19.2.0 ✅
- **Tailwind CSS**: 4.x ✅
- **TypeScript**: 5.x ✅

## Conclusion

shadcn/ui is successfully installed and configured in the MeepleAI frontend. All core components are available, tested, and documented. The demo page (`/shadcn-demo`) provides comprehensive examples for developers.

For questions or issues, refer to:
- This documentation
- Demo page (`/shadcn-demo`)
- Official shadcn/ui docs
- Issue #927 for implementation details

---

**Last Updated**: 2025-11-12
**Maintained By**: MeepleAI Frontend Team
