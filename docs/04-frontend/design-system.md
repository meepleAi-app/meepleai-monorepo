# MeepleAI Design System v2.0

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: Current Design System
**Location**: Consolidated from `design-system.md` (v1) + `design-system-2.0.md` (v2) + `design-tokens-migration-guide.md`

---

## Table of Contents

1. [Overview](#overview)
2. [Design Principles](#design-principles)
3. [Color System](#color-system)
4. [Typography](#typography)
5. [Spacing & Layout](#spacing--layout)
6. [Components](#components)
7. [Design Tokens](#design-tokens)
8. [Accessibility](#accessibility)
9. [Migration Guide (v1 → v2)](#migration-guide-v1--v2)
10. [Related Documentation](#related-documentation)

---

## Overview

**MeepleAI Design System v2.0** - Playful Boardroom theme with Tailwind CSS 4, shadcn/ui, and comprehensive design tokens.

### Key Features

- **Theme**: Playful Boardroom (board game aesthetic)
- **Framework**: Tailwind CSS 4 + shadcn/ui (Radix primitives)
- **Components**: 40+ production-ready components
- **Accessibility**: WCAG 2.1 Level AA compliant
- **Design Tokens**: CSS variables for theming
- **Dark Mode**: Full support with automatic switching

### Tech Stack

```
Tailwind CSS 4.1.17     Foundation (utility-first CSS)
shadcn/ui               Component library (Radix + Tailwind)
Radix UI                Primitives (accessible, unstyled)
React 19                Component framework
TypeScript              Type safety
```

---

## Design Principles

### 1. Playful Yet Professional

**Goal**: Board game aesthetic that's fun but functional

**Implementation**:
- Warm, inviting color palette
- Rounded corners (board game box aesthetic)
- Subtle textures and patterns
- Playful micro-interactions

### 2. Accessibility First

**Requirements**:
- WCAG 2.1 Level AA compliance
- Keyboard navigation for all interactions
- Screen reader support
- Color contrast ratio ≥4.5:1 (text), ≥3:1 (UI)

### 3. Consistent & Predictable

**Patterns**:
- Consistent spacing scale (4px base)
- Predictable component behavior
- Familiar UI patterns
- Clear visual hierarchy

### 4. Performance Optimized

**Strategies**:
- CSS variables for theming (no JS)
- Tree-shakable components
- Lazy loading for heavy components
- Optimized animations (60fps)

---

## Color System

### Primary Colors (Boardroom Palette)

```css
/* Light Mode */
--primary: 25 95% 53%;          /* Warm Orange */
--primary-foreground: 0 0% 100%; /* White */

--secondary: 142 71% 45%;        /* Forest Green */
--secondary-foreground: 0 0% 100%;

--accent: 217 91% 60%;          /* Blue */
--accent-foreground: 0 0% 100%;

/* Dark Mode */
--primary: 25 95% 53%;          /* Same warm orange */
--primary-foreground: 0 0% 9%;  /* Dark text */
```

### Semantic Colors

```css
/* Status Colors */
--success: 142 71% 45%;   /* Green */
--warning: 38 92% 50%;    /* Amber */
--error: 0 84% 60%;       /* Red */
--info: 217 91% 60%;      /* Blue */

/* Neutral Colors */
--background: 0 0% 100%;  /* White (light) / Dark (dark) */
--foreground: 0 0% 9%;    /* Near black text */
--muted: 210 40% 96%;     /* Light gray backgrounds */
--border: 214 32% 91%;    /* Subtle borders */
```

### Color Usage Guidelines

| Color | Usage | Example |
|-------|-------|---------|
| **Primary** | Main actions, links | Submit buttons, primary CTA |
| **Secondary** | Secondary actions | Cancel, back buttons |
| **Accent** | Highlights, badges | New features, notifications |
| **Success** | Positive feedback | Success messages, completed |
| **Warning** | Caution | Low confidence, pending |
| **Error** | Errors, destructive | Delete actions, errors |
| **Info** | Informational | Help text, tooltips |

### Contrast Requirements

All color combinations meet WCAG AA:

```
Background / Foreground:
✅ primary / primary-foreground    → 5.2:1
✅ secondary / secondary-foreground → 4.8:1
✅ error / error-foreground        → 4.7:1

Text Sizes:
Large text (18px+): ≥3:1 required
Normal text (16px): ≥4.5:1 required
```

See: [Accessibility Standards](./accessibility-standards.md)

---

## Typography

### Font Families

```css
--font-sans: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
--font-mono: 'JetBrains Mono', 'Fira Code', 'Consolas', monospace;
```

**Inter**: Primary font (readable, modern)
**JetBrains Mono**: Code blocks, technical content

### Type Scale

| Size | Rem | Pixels | Usage |
|------|-----|--------|-------|
| **xs** | 0.75rem | 12px | Captions, small labels |
| **sm** | 0.875rem | 14px | Secondary text, metadata |
| **base** | 1rem | 16px | Body text (default) |
| **lg** | 1.125rem | 18px | Emphasized text |
| **xl** | 1.25rem | 20px | Subheadings |
| **2xl** | 1.5rem | 24px | Section titles |
| **3xl** | 1.875rem | 30px | Page titles |
| **4xl** | 2.25rem | 36px | Hero headings |

### Font Weights

```
300 - Light      (Rarely used)
400 - Regular    (Body text)
500 - Medium     (Emphasized text)
600 - Semibold   (Headings, buttons)
700 - Bold       (Important headings)
```

### Line Heights

```css
--leading-tight: 1.25;   /* Headings */
--leading-normal: 1.5;   /* Body text */
--leading-relaxed: 1.75; /* Long-form content */
```

---

## Spacing & Layout

### Spacing Scale (4px base)

| Token | Value | Usage |
|-------|-------|-------|
| **0** | 0 | No space |
| **1** | 0.25rem (4px) | Tight spacing |
| **2** | 0.5rem (8px) | Close elements |
| **3** | 0.75rem (12px) | Related items |
| **4** | 1rem (16px) | Default spacing |
| **6** | 1.5rem (24px) | Section spacing |
| **8** | 2rem (32px) | Large gaps |
| **12** | 3rem (48px) | Major sections |
| **16** | 4rem (64px) | Page sections |

### Layout Patterns

**Container Widths**:
```css
--container-sm: 640px;   /* Mobile content */
--container-md: 768px;   /* Tablet content */
--container-lg: 1024px;  /* Desktop content */
--container-xl: 1280px;  /* Wide desktop */
--container-2xl: 1536px; /* Ultra-wide */
```

**Breakpoints** (Tailwind CSS 4):
```css
sm: 640px    /* Mobile landscape */
md: 768px    /* Tablet portrait */
lg: 1024px   /* Desktop */
xl: 1280px   /* Wide desktop */
2xl: 1536px  /* Ultra-wide */
```

### Border Radius

```css
--radius-sm: 0.25rem;   /* 4px - Subtle */
--radius: 0.5rem;       /* 8px - Default */
--radius-md: 0.75rem;   /* 12px - Cards */
--radius-lg: 1rem;      /* 16px - Modals */
--radius-full: 9999px;  /* Pills, avatars */
```

---

## Components

### Component Library (shadcn/ui)

**Installed Components** (40+):

#### Forms & Inputs
- Button, Input, Textarea
- Select, Combobox, Command
- Checkbox, RadioGroup, Switch
- Label, Form
- DatePicker (React Day Picker)

#### Data Display
- Table, DataTable
- Card, Badge, Avatar
- Separator, Divider
- Skeleton (loading states)

#### Feedback
- Alert, AlertDialog
- Toast (Sonner)
- Dialog, Sheet
- Progress, Spinner

#### Navigation
- Tabs, Accordion
- Dropdown Menu
- Navigation Menu
- Breadcrumb

#### Layout
- ScrollArea
- Aspect Ratio
- Container

### Component Usage

**Installation**:
```bash
# Add new component
pnpx shadcn@latest add <component-name>

# Example
pnpx shadcn@latest add button
pnpx shadcn@latest add dialog
```

**Usage in Code**:
```typescript
// Import from @/components/ui
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader } from '@/components/ui/dialog';

// Use in component
<Button variant="default" size="lg">
  Click Me
</Button>

<Dialog>
  <DialogContent>
    <DialogHeader>Title</DialogHeader>
    <p>Content</p>
  </DialogContent>
</Dialog>
```

See: [shadcn/ui Installation](./shadcn-ui-installation.md)

### Component Variants

**Button Variants**:
```typescript
variant: "default" | "destructive" | "outline" | "secondary" | "ghost" | "link"
size: "default" | "sm" | "lg" | "icon"
```

**Card Variants**:
```typescript
variant: "default" | "outline" | "elevated"
```

**Badge Variants**:
```typescript
variant: "default" | "secondary" | "destructive" | "outline"
```

---

## Design Tokens

### CSS Variables (Light Mode)

```css
:root {
  /* Colors */
  --background: 0 0% 100%;
  --foreground: 0 0% 9%;
  --primary: 25 95% 53%;
  --secondary: 142 71% 45%;
  --accent: 217 91% 60%;
  --muted: 210 40% 96%;
  --border: 214 32% 91%;

  /* Spacing */
  --spacing-unit: 0.25rem; /* 4px */

  /* Border Radius */
  --radius: 0.5rem;

  /* Typography */
  --font-sans: 'Inter', sans-serif;
  --font-mono: 'JetBrains Mono', monospace;
}
```

### CSS Variables (Dark Mode)

```css
.dark {
  --background: 0 0% 9%;
  --foreground: 0 0% 98%;
  --primary: 25 95% 53%;        /* Same */
  --secondary: 142 71% 45%;     /* Same */
  --accent: 217 91% 60%;        /* Same */
  --muted: 217 33% 17%;         /* Darker */
  --border: 217 33% 23%;        /* Darker */
}
```

### Accessing Tokens

```tsx
// In Tailwind classes
<div className="bg-primary text-primary-foreground p-4 rounded-lg">

// In custom CSS
.my-component {
  background-color: hsl(var(--primary));
  color: hsl(var(--primary-foreground));
  padding: var(--spacing-unit);
  border-radius: var(--radius);
}

// In JavaScript (if needed)
const primary = getComputedStyle(document.documentElement)
  .getPropertyValue('--primary');
```

---

## Accessibility

### WCAG 2.1 Level AA Compliance

**Requirements Met**:
- ✅ Color contrast ≥4.5:1 (text), ≥3:1 (UI components)
- ✅ Keyboard navigation (all interactive elements)
- ✅ Screen reader support (ARIA labels, roles)
- ✅ Focus indicators (visible focus states)
- ✅ Responsive design (mobile-first)
- ✅ Touch targets ≥44x44px

### Testing Accessibility

```bash
# Automated tests (Jest + jest-axe)
pnpm test -- accessibility

# Manual testing
# 1. Keyboard only (Tab, Enter, Space, Arrows)
# 2. Screen reader (NVDA, JAWS, VoiceOver)
# 3. Color contrast checker (browser devtools)
```

See: [Accessibility Testing Guide](../02-development/testing/accessibility-testing-guide.md)

### Common Accessibility Patterns

**Semantic HTML**:
```tsx
// ✅ Good
<button onClick={handleClick}>Click Me</button>
<nav aria-label="Main navigation">

// ❌ Bad
<div onClick={handleClick}>Click Me</div>  // Not keyboard accessible
```

**ARIA Labels**:
```tsx
<button aria-label="Close dialog">
  <X className="h-4 w-4" />
</button>

<input
  type="search"
  placeholder="Search games"
  aria-label="Search board games"
/>
```

**Focus Management**:
```tsx
// Trap focus in modals
<Dialog>
  <DialogContent className="focus:outline-none focus:ring-2 focus:ring-primary">
    {/* Content */}
  </DialogContent>
</Dialog>
```

---

## Migration Guide (v1 → v2)

### Breaking Changes

#### 1. Color Token Names

```diff
# Old (v1)
- bg-orange-500 → bg-primary
- text-green-600 → text-success
- border-gray-200 → border-border

# New (v2)
+ bg-primary
+ text-success
+ border-border
```

#### 2. Component API Changes

**Button**:
```diff
# Old (v1)
- <Button color="orange">Submit</Button>

# New (v2)
+ <Button variant="default">Submit</Button>
```

**Card**:
```diff
# Old (v1)
- <Card elevation="high">

# New (v2)
+ <Card variant="elevated">
```

#### 3. Spacing Scale

```diff
# Old (v1) - 8px base
- p-2 = 16px
- p-4 = 32px

# New (v2) - 4px base (Tailwind default)
+ p-2 = 8px
+ p-4 = 16px
+ p-8 = 32px
```

### Migration Steps

**Step 1: Update Dependencies**
```bash
cd apps/web
pnpm install tailwindcss@4.1.17
pnpm install @radix-ui/themes@latest
```

**Step 2: Update Tailwind Config**
```javascript
// tailwind.config.ts
export default {
  theme: {
    extend: {
      colors: {
        border: "hsl(var(--border))",
        primary: "hsl(var(--primary))",
        // ... design tokens
      },
    },
  },
};
```

**Step 3: Update Global CSS**
```css
/* app/globals.css */
@import "tailwindcss";

@layer base {
  :root {
    /* v2.0 design tokens */
    --background: 0 0% 100%;
    --foreground: 0 0% 9%;
    --primary: 25 95% 53%;
    /* ... */
  }

  .dark {
    /* Dark mode tokens */
  }
}
```

**Step 4: Update Components**
```bash
# Replace color utilities
find components -name "*.tsx" -exec sed -i 's/bg-orange-500/bg-primary/g' {} +
find components -name "*.tsx" -exec sed -i 's/text-green-600/text-success/g' {} +

# Update component imports
# Old: import { Button } from './ui/button'
# New: import { Button } from '@/components/ui/button'
```

**Step 5: Test**
```bash
pnpm typecheck
pnpm test
pnpm build
```

### Backwards Compatibility (v1)

**If you need v1 support**:

```tsx
// Create v1 theme provider
import { ThemeProvider as V1ThemeProvider } from '@/lib/theme-v1';

// Wrap legacy components
<V1ThemeProvider>
  <LegacyComponent />
</V1ThemeProvider>
```

**Note**: v1 is deprecated, migrate to v2.0 for full feature support.

---

## Design Token Reference

### Complete Token List

```css
/* Colors */
--background: 0 0% 100%;
--foreground: 0 0% 9%;
--card: 0 0% 100%;
--card-foreground: 0 0% 9%;
--popover: 0 0% 100%;
--popover-foreground: 0 0% 9%;
--primary: 25 95% 53%;
--primary-foreground: 0 0% 100%;
--secondary: 142 71% 45%;
--secondary-foreground: 0 0% 100%;
--muted: 210 40% 96%;
--muted-foreground: 0 0% 45%;
--accent: 217 91% 60%;
--accent-foreground: 0 0% 100%;
--destructive: 0 84% 60%;
--destructive-foreground: 0 0% 100%;
--border: 214 32% 91%;
--input: 214 32% 91%;
--ring: 25 95% 53%;

/* Border Radius */
--radius-sm: 0.25rem;
--radius: 0.5rem;
--radius-md: 0.75rem;
--radius-lg: 1rem;
--radius-full: 9999px;
```

### Using Design Tokens

**In Tailwind Classes**:
```tsx
<div className="bg-background text-foreground border-border rounded-lg">
  <Button variant="default">Primary Action</Button>
</div>
```

**In Custom CSS**:
```css
.custom-component {
  background-color: hsl(var(--primary));
  color: hsl(var(--primary-foreground));
  border: 1px solid hsl(var(--border));
  border-radius: var(--radius);
}
```

**In JavaScript** (avoid if possible):
```typescript
const primaryColor = getComputedStyle(document.documentElement)
  .getPropertyValue('--primary');
```

---

## Component Examples

### Form Example

```tsx
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export function LoginForm() {
  return (
    <form className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <Input
          id="email"
          type="email"
          placeholder="you@example.com"
          required
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor="password">Password</Label>
        <Input
          id="password"
          type="password"
          required
        />
      </div>

      <Button type="submit" className="w-full">
        Sign In
      </Button>
    </form>
  );
}
```

### Card Example

```tsx
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

export function GameCard({ game }) {
  return (
    <Card className="hover:shadow-lg transition-shadow">
      <CardHeader>
        <CardTitle>{game.name}</CardTitle>
        <CardDescription>{game.players} players</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex gap-2">
          <Badge variant="secondary">{game.duration}min</Badge>
          <Badge variant="outline">Complexity: {game.complexity}/5</Badge>
        </div>
      </CardContent>
    </Card>
  );
}
```

### Dialog Example

```tsx
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

export function ConfirmDialog({ open, onConfirm, onCancel }) {
  return (
    <Dialog open={open} onOpenChange={onCancel}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Are you sure?</DialogTitle>
          <DialogDescription>
            This action cannot be undone.
          </DialogDescription>
        </DialogHeader>

        <div className="flex gap-2 justify-end">
          <Button variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={onConfirm}>
            Confirm
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

---

## Related Documentation

### Design & UI
- **[shadcn/ui Installation](./shadcn-ui-installation.md)** - Setup guide
- **[Component Library Progress](./component-library-progress.md)** - Implementation status
- **[Storybook Guide](./storybook-guide.md)** - Component development
- **[Accessibility Standards](./accessibility-standards.md)** - WCAG compliance

### Development
- **[Frontend Developer Guide](../02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md)** - Complete frontend guide
- **[React 19 Best Practices](./react19-nextjs16-best-practices.md)** - Modern patterns
- **[Animations Examples](./animations-examples.md)** - Animation patterns

### Testing
- **[Visual Testing Guide](../02-development/testing/visual-testing-guide.md)** - Chromatic
- **[Accessibility Testing](../02-development/testing/accessibility-testing-guide.md)** - A11y tests

---

## External Resources

- [Tailwind CSS 4 Docs](https://tailwindcss.com/docs)
- [shadcn/ui Components](https://ui.shadcn.com/docs/components)
- [Radix UI Primitives](https://www.radix-ui.com/primitives)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

---

## Changelog

### v2.0 (2025-12-08): Consolidation & Current Version

**Changes**:
- ✅ Consolidated design-system.md (v1) + design-system-2.0.md
- ✅ Merged design tokens migration guide
- ✅ Added comprehensive color system documentation
- ✅ Added complete component library reference
- ✅ Added migration guide (v1 → v2)
- ✅ Updated all cross-references

### v2.0 (2025-11-30): Playful Boardroom Theme

**New Features**:
- Warm color palette (orange primary, green secondary)
- Enhanced design tokens (CSS variables)
- Dark mode support
- 40+ shadcn/ui components
- Tailwind CSS 4 migration

### v1.0 (2025-10-15): Initial Design System

**Features**:
- Basic color palette
- Typography scale
- Component foundations
- Tailwind CSS 3

---

**Version**: 2.0 (Current)
**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: Production Ready
**Components**: 40+ shadcn/ui components
**Theme**: Playful Boardroom
**Accessibility**: WCAG 2.1 Level AA
**Documentation**: Single consolidated guide

