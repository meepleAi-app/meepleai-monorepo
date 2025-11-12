# Shadcn/UI Installation Guide

**Issue**: #988 (BGAI-046)
**Status**: ✅ Completed
**Date**: 2025-11-12

## Overview

This document describes the installation and configuration of [shadcn/ui](https://ui.shadcn.com/) component library in the MeepleAI Next.js web application.

## What is Shadcn/UI?

Shadcn/ui is a collection of re-usable, accessible components built with:
- **Radix UI**: Unstyled, accessible component primitives
- **Tailwind CSS**: Utility-first CSS framework
- **class-variance-authority**: Type-safe variants
- **TypeScript**: Full type safety

Unlike traditional component libraries, shadcn/ui components are copied directly into your project, giving you full ownership and customization control.

## Installation Summary

### Components Installed

Core components available in `apps/web/src/components/ui/`:

- ✅ **Button** - Multiple variants (default, secondary, destructive, outline, ghost, link) and sizes
- ✅ **Card** - Content container with Header, Title, Description, Content, Footer subcomponents
- ✅ **Input** - Text input field with consistent styling
- ✅ **Select** - Dropdown selection with Trigger, Content, Item subcomponents
- ✅ **Dialog** - Modal dialog with Trigger, Content, Header, Footer subcomponents

### Dependencies Added

| Package | Version | Purpose |
|---------|---------|---------|
| `class-variance-authority` | Latest | Type-safe component variants |
| `clsx` | Latest | Conditional className utility |
| `tailwind-merge` | Latest | Merge Tailwind classes without conflicts |
| `lucide-react` | Latest | Icon library (Radix UI dependency) |
| `@radix-ui/react-*` | Latest | Accessible component primitives |

### Configuration Files

1. **components.json** - Shadcn/UI configuration
   ```json
   {
     "style": "new-york",
     "rsc": false,
     "tsx": true,
     "tailwind": {
       "config": "tailwind.config.js",
       "css": "src/styles/globals.css",
       "baseColor": "neutral",
       "cssVariables": true
     }
   }
   ```

2. **src/lib/utils.ts** - Utility function for className merging
   ```typescript
   import { clsx, type ClassValue } from "clsx"
   import { twMerge } from "tailwind-merge"

   export function cn(...inputs: ClassValue[]) {
     return twMerge(clsx(inputs))
   }
   ```

3. **src/styles/globals.css** - CSS variables for theming
   - Added `@layer base` section with shadcn color variables
   - Light and dark mode support with OKLCH color space
   - Integrated with existing MeepleAI color palette

## Usage Examples

### Button Component

```tsx
import { Button } from '@/components/ui/button';

// Variants
<Button variant="default">Default</Button>
<Button variant="secondary">Secondary</Button>
<Button variant="destructive">Destructive</Button>
<Button variant="outline">Outline</Button>
<Button variant="ghost">Ghost</Button>
<Button variant="link">Link</Button>

// Sizes
<Button size="sm">Small</Button>
<Button size="default">Default</Button>
<Button size="lg">Large</Button>
<Button size="icon">🎲</Button>
```

### Card Component

```tsx
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter
} from '@/components/ui/card';

<Card>
  <CardHeader>
    <CardTitle>Card Title</CardTitle>
    <CardDescription>Card description text</CardDescription>
  </CardHeader>
  <CardContent>
    <p>Main content goes here</p>
  </CardContent>
  <CardFooter>
    <Button>Action</Button>
  </CardFooter>
</Card>
```

### Input Component

```tsx
import { Input } from '@/components/ui/input';

const [value, setValue] = useState('');

<Input
  type="text"
  placeholder="Enter text..."
  value={value}
  onChange={(e) => setValue(e.target.value)}
/>
```

### Select Component

```tsx
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem
} from '@/components/ui/select';

<Select value={value} onValueChange={setValue}>
  <SelectTrigger>
    <SelectValue placeholder="Select option..." />
  </SelectTrigger>
  <SelectContent>
    <SelectItem value="option1">Option 1</SelectItem>
    <SelectItem value="option2">Option 2</SelectItem>
    <SelectItem value="option3">Option 3</SelectItem>
  </SelectContent>
</Select>
```

### Dialog Component

```tsx
import {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter
} from '@/components/ui/dialog';

<Dialog>
  <DialogTrigger asChild>
    <Button>Open Dialog</Button>
  </DialogTrigger>
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Dialog Title</DialogTitle>
      <DialogDescription>
        Dialog description text
      </DialogDescription>
    </DialogHeader>
    <div className="py-4">
      {/* Dialog content */}
    </div>
    <DialogFooter>
      <Button variant="outline">Cancel</Button>
      <Button>Confirm</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

## Demo Page

Visit `/shadcn-demo` to see all components in action:
- **Development**: http://localhost:3000/shadcn-demo
- **Production**: https://meepleai.dev/shadcn-demo

The demo page (`apps/web/src/pages/shadcn-demo.tsx`) demonstrates:
- All button variants and sizes
- Card layouts with headers, content, and footers
- Input field with state management
- Select dropdown with board game options
- Dialog modal with trigger button

## Adding New Components

To add additional shadcn/ui components:

```bash
cd apps/web
pnpm dlx shadcn@latest add <component-name>
```

Examples:
```bash
pnpm dlx shadcn@latest add table      # Data table
pnpm dlx shadcn@latest add form       # Form with validation
pnpm dlx shadcn@latest add toast      # Toast notifications
pnpm dlx shadcn@latest add dropdown   # Dropdown menu
```

Browse available components: https://ui.shadcn.com/docs/components

## Theme Integration

Shadcn/UI is configured to work with MeepleAI's existing theme:

### Color Mapping
- **Primary**: Maps to MeepleAI blue (`#0056b3`)
- **Secondary**: Maps to MeepleAI green (`#34a853`)
- **Accent**: Maps to MeepleAI orange (`#ff9800`)
- **Background**: Dark slate (`#0f172a`)
- **Foreground**: White with opacity

### Dark Mode Support
Shadcn components support dark mode out of the box:
```css
@custom-variant dark (&:is(.dark *));
```

Add `className="dark"` to any parent element to enable dark mode for its children.

## Compatibility

✅ **Next.js 16.0.1** - Fully compatible with App Router and Pages Router
✅ **React 19.2.0** - Works with latest React features
✅ **Tailwind CSS 4.1.17** - Integrated with Tailwind v4 configuration
✅ **TypeScript 5.9.3** - Full type safety and IntelliSense support

## Build & Test Results

### Build
```bash
cd apps/web
pnpm build
```
**Status**: ✅ Build successful in 5.7s
**Routes**: 31 pages including `/shadcn-demo`

### Tests
```bash
cd apps/web
pnpm test
```
**Status**: ✅ 4024 tests passing
**Coverage**: 90%+ maintained (no regressions)

### Type Checking
```bash
cd apps/web
pnpm typecheck
```
**Status**: ⚠️ 2 pre-existing errors in e2e tests (unrelated to shadcn)

## Troubleshooting

### Import Errors
If you see import errors for `@/components/ui/*`, verify:
1. Path alias in `tsconfig.json`: `"@/*": ["src/*"]`
2. Component exists in `src/components/ui/`
3. Run `pnpm install` to ensure dependencies are installed

### Styling Issues
If components don't render correctly:
1. Verify `globals.css` imports Tailwind: `@import "tailwindcss";`
2. Check CSS variables are present in `@layer base`
3. Ensure `tailwind.config.js` includes shadcn content paths

### Type Errors
If TypeScript complains about component props:
1. Check Radix UI dependencies are installed
2. Restart TypeScript server in VS Code: `Ctrl+Shift+P` → "Restart TS Server"
3. Run `pnpm typecheck` to verify no conflicts

## Additional Resources

- **Official Docs**: https://ui.shadcn.com/docs
- **Component Gallery**: https://ui.shadcn.com/docs/components
- **Radix UI Primitives**: https://www.radix-ui.com/primitives
- **Tailwind CSS**: https://tailwindcss.com/docs
- **GitHub**: https://github.com/shadcn-ui/ui

## Maintenance

### Updating Components
To update shadcn components:
1. Check for updates: Visit https://ui.shadcn.com/docs/changelog
2. Re-run init if needed: `pnpm dlx shadcn@latest init`
3. Re-add modified components: `pnpm dlx shadcn@latest add <component>`

### Custom Components
Create custom variants by extending existing components:
```tsx
import { Button, buttonVariants } from '@/components/ui/button';

// Add custom variant
const customButton = cn(
  buttonVariants({ variant: "default" }),
  "bg-gradient-to-r from-primary-500 to-secondary-500"
);
```

## Credits

- **Library**: [shadcn/ui](https://ui.shadcn.com/) by [@shadcn](https://twitter.com/shadcn)
- **Primitives**: [Radix UI](https://www.radix-ui.com/)
- **Styling**: [Tailwind CSS](https://tailwindcss.com/)
- **Implementation**: Issue #988 - BGAI-046
- **Date**: November 12, 2025
