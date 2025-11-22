# MeepleAI Design System

**Version**: 1.0.0
**Status**: 🚧 In Development
**Last Updated**: 2025-11-13

---

## 📚 Table of Contents

1. [Overview](#overview)
2. [Design Tokens](#design-tokens)
3. [Components](#components)
4. [Patterns](#patterns)
5. [Accessibility](#accessibility)
6. [Implementation Guide](#implementation-guide)
7. [Migration Guide](#migration-guide)

---

## Overview

The MeepleAI Design System provides a comprehensive set of design tokens, components, and patterns for building consistent, accessible, and performant user interfaces.

### Principles

1. **Consistency**: Every UI element follows the same visual language
2. **Accessibility**: WCAG 2.1 AA compliance minimum
3. **Performance**: Optimized for speed and bundle size
4. **Developer Experience**: Easy to use, hard to misuse
5. **Maintainability**: Token-based system for easy updates

### Technology Stack

- **CSS Framework**: Tailwind CSS 4
- **Component Library**: Shadcn/UI (Radix UI primitives)
- **Design Tokens**: CSS Custom Properties
- **Dark Mode**: CSS class-based (`class` strategy)
- **Icons**: Lucide React

---

## Design Tokens

All design tokens are defined in `src/styles/design-tokens.css` and follow a systematic naming convention.

### Spacing System

Based on a **4px base unit** (0.25rem):

```css
/* Direct values */
--space-1: 0.25rem;   /* 4px */
--space-2: 0.5rem;    /* 8px */
--space-4: 1rem;      /* 16px */
--space-6: 1.5rem;    /* 24px */
--space-8: 2rem;      /* 32px */

/* Semantic names (recommended) */
--space-xs: var(--space-2);   /* 8px */
--space-sm: var(--space-3);   /* 12px */
--space-md: var(--space-4);   /* 16px */
--space-lg: var(--space-6);   /* 24px */
--space-xl: var(--space-8);   /* 32px */
```

**Usage**:
```tsx
// ✅ DO: Use semantic spacing with Tailwind classes
<div className="p-6 gap-4">

// ❌ DON'T: Use inline styles with magic numbers
<div style={{ padding: '24px', gap: '16px' }}>
```

### Typography

System font stack for optimal performance and native feel:

```css
--font-sans: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
--font-mono: ui-monospace, SFMono-Regular, Monaco, Consolas, monospace;
```

**Font Scale**:

| Token | Size | Usage |
|-------|------|-------|
| `--text-xs` | 12px | Helper text, badges |
| `--text-sm` | 14px | Body text (default) |
| `--text-base` | 16px | Emphasized body |
| `--text-lg` | 18px | Subheadings |
| `--text-xl` | 20px | Small headings |
| `--text-2xl` | 24px | Headings |
| `--text-3xl` | 30px | Section titles |
| `--text-4xl` | 36px | Page titles |
| `--text-5xl` | 48px | Hero text |
| `--text-7xl` | 72px | Landing page hero |

**Font Weights**:
- `--font-normal`: 400 (body text)
- `--font-medium`: 500 (emphasis)
- `--font-semibold`: 600 (headings)
- `--font-bold`: 700 (strong emphasis)

**Line Heights**:
- `--leading-tight`: 1.25 (headings)
- `--leading-normal`: 1.5 (body)
- `--leading-relaxed`: 1.625 (long-form content)

### Colors

Colors use **HSL format** for easy manipulation and dark mode support.

#### Semantic Colors

```css
/* Primary (Brand Blue) */
--primary: 221 83% 53%;              /* #0070f3 */
--primary-foreground: 210 40% 98%;

/* Secondary (Brand Green) */
--secondary: 142 76% 36%;            /* #34a853 */
--secondary-foreground: 210 40% 98%;

/* Accent (Brand Orange) */
--accent: 36 100% 50%;               /* #ff9800 */
--accent-foreground: 240 5.9% 10%;

/* Status colors */
--color-success: 142 76% 36%;        /* Green */
--color-warning: 36 100% 50%;        /* Orange */
--color-error: 0 84.2% 60.2%;        /* Red */
--color-info: 221 83% 53%;           /* Blue */
```

#### Game-Specific Colors

For game badges and categorization:

```css
--color-game-gloomhaven: 220 70% 50%;    /* Deep blue */
--color-game-wingspan: 180 50% 50%;      /* Teal */
--color-game-terraforming: 15 70% 50%;   /* Mars orange */
--color-game-catan: 30 80% 50%;          /* Warm orange */
--color-game-azul: 210 80% 60%;          /* Azure */
--color-game-chess: 0 0% 20%;            /* Dark gray */
--color-game-generic: 260 60% 50%;       /* Purple */
```

**Usage**:
```tsx
// ✅ DO: Use semantic color classes
<Button variant="primary">Submit</Button>
<Badge variant="success">Completed</Badge>

// ❌ DON'T: Hardcode colors
<button style={{ background: '#0070f3' }}>Submit</button>
```

### Shadows

Elevation system using box-shadow:

```css
--shadow-xs: subtle shadow (hover states)
--shadow-sm: small elevation (cards at rest)
--shadow-md: medium elevation (cards on hover)
--shadow-lg: high elevation (dropdowns)
--shadow-xl: very high elevation (modals)
--shadow-2xl: maximum elevation (modal backdrops)
```

**Usage**:
```tsx
<Card className="shadow-sm hover:shadow-md transition-shadow">
```

### Border Radius

```css
--radius-sm: 0.375rem;   /* 6px - tags, pills */
--radius-md: 0.5rem;     /* 8px - buttons, inputs */
--radius-lg: 0.75rem;    /* 12px - small cards */
--radius-xl: 1rem;       /* 16px - cards */
--radius-2xl: 1.5rem;    /* 24px - modals */
--radius-full: 9999px;   /* Fully rounded (avatars) */
```

### Transitions

```css
--transition-fast: 150ms cubic-bezier(0.4, 0, 0.2, 1);
--transition-base: 200ms cubic-bezier(0.4, 0, 0.2, 1);
--transition-slow: 300ms cubic-bezier(0.4, 0, 0.2, 1);
```

**Usage**:
```tsx
<button className="transition-colors duration-200 hover:bg-primary/90">
```

### Z-Index Layers

Systematic z-index scale to prevent conflicts:

```css
--z-base: 0;                  /* Normal flow */
--z-dropdown: 1000;           /* Dropdowns */
--z-sticky: 1020;             /* Sticky headers */
--z-fixed: 1030;              /* Fixed elements */
--z-modal-backdrop: 1040;     /* Modal backdrops */
--z-modal: 1050;              /* Modals */
--z-popover: 1060;            /* Popovers/tooltips */
--z-notification: 1080;       /* Notifications */
--z-toast: 1090;              /* Toast messages */
```

---

## Components

### Component Hierarchy

```
Layout Components
├── PageLayout (page wrapper)
├── ContentWrapper (max-width container)
├── Sidebar
└── Header

Core Components (Shadcn/UI)
├── Button
├── Card
├── Input
├── Select
├── Dialog/Modal
├── Dropdown
├── Badge
├── Avatar
└── Skeleton

Custom Components
├── ChatMessage
├── GameSelector
├── PdfPreview
├── UploadQueue
├── ErrorDisplay
└── LoadingButton
```

### Component Variants

#### Button

```tsx
// Variants
<Button variant="default">Default</Button>
<Button variant="destructive">Delete</Button>
<Button variant="outline">Outline</Button>
<Button variant="ghost">Ghost</Button>

// Sizes
<Button size="sm">Small</Button>
<Button size="default">Default</Button>
<Button size="lg">Large</Button>

// States
<Button disabled>Disabled</Button>
<LoadingButton isLoading>Loading</LoadingButton>
```

**Design Specs**:
- Height: 40px (default), 36px (sm), 48px (lg)
- Padding: 16px horizontal (default)
- Border radius: `--radius-md`
- Transition: `--transition-fast`
- Minimum width: 80px
- Touch target: 44x44px minimum

#### Card

```tsx
<Card className="p-6 shadow-sm hover:shadow-md transition-shadow">
  <CardHeader>
    <CardTitle>Title</CardTitle>
    <CardDescription>Description</CardDescription>
  </CardHeader>
  <CardContent>
    {/* Content */}
  </CardContent>
  <CardFooter>
    {/* Actions */}
  </CardFooter>
</Card>
```

**Design Specs**:
- Border radius: `--radius-xl` (16px)
- Padding: 24px (default)
- Border: 1px solid `--color-border`
- Background: `--color-card`
- Shadow: `--shadow-sm` at rest, `--shadow-md` on hover

#### Input

```tsx
<Input
  type="text"
  placeholder="Enter text..."
  className="w-full"
/>
```

**Design Specs**:
- Height: 40px (default)
- Padding: 12px horizontal
- Border radius: `--radius-md`
- Border: 1px solid `--color-input`
- Focus ring: 2px `--color-ring`, 2px offset

### Component States

All interactive components support these states:

1. **Default**: Neutral, at rest
2. **Hover**: On mouse over (desktop)
3. **Active**: On click/press
4. **Focus**: Keyboard navigation (focus-visible)
5. **Disabled**: Non-interactive
6. **Loading**: Processing state

---

## Patterns

### Layout Patterns

#### Page Layout

```tsx
<div className="min-h-screen flex flex-col">
  <Header />
  <main className="flex-1 p-6">
    <div className="max-w-[56rem] mx-auto">
      {/* Content */}
    </div>
  </main>
  <Footer />
</div>
```

#### Split View (Chat)

```tsx
<div className="flex h-screen">
  <Sidebar className="w-80 border-r" />
  <main className="flex-1 flex flex-col">
    <Header />
    <MessageList className="flex-1 overflow-auto" />
    <MessageInput className="border-t" />
  </main>
</div>
```

#### Wizard/Stepper

```tsx
<div className="max-w-2xl mx-auto p-6">
  <WizardSteps current={step} total={4} />
  <div className="mt-8">
    {/* Step content */}
  </div>
  <div className="mt-6 flex justify-between">
    <Button variant="outline">Back</Button>
    <Button>Next</Button>
  </div>
</div>
```

### Responsive Patterns

#### Mobile-First Approach

```tsx
// ✅ DO: Mobile-first responsive design
<div className="w-full md:w-80">
<div className="flex-col md:flex-row">

// ❌ DON'T: Desktop-first
<div className="w-80 sm:w-full">
```

#### Touch Targets

All interactive elements must meet **44x44px minimum** (WCAG 2.1 Level AA):

```tsx
<button className="min-w-11 min-h-11 p-2">
  <Icon className="w-5 h-5" />
</button>
```

#### Mobile Navigation

```tsx
// Desktop: Persistent sidebar
// Mobile: Drawer overlay
<Sheet>
  <SheetTrigger asChild>
    <Button variant="ghost" size="icon" className="md:hidden">
      <Menu />
    </Button>
  </SheetTrigger>
  <SheetContent side="left">
    <Sidebar />
  </SheetContent>
</Sheet>

{/* Desktop sidebar */}
<aside className="hidden md:block w-80">
  <Sidebar />
</aside>
```

---

## Accessibility

### WCAG 2.1 AA Requirements

#### Color Contrast

- **Normal text** (< 18px): Minimum 4.5:1 contrast ratio
- **Large text** (≥ 18px or 14px bold): Minimum 3:1 contrast ratio
- **UI components**: Minimum 3:1 contrast ratio

All colors in the design system meet these requirements.

#### Keyboard Navigation

All interactive elements must be keyboard accessible:

```tsx
// ✅ DO: Use semantic HTML
<button onClick={...}>Click me</button>
<a href="/page">Link</a>

// ❌ DON'T: Make non-interactive elements clickable
<div onClick={...}>Click me</div>
```

#### Focus Indicators

```tsx
// Focus visible (keyboard only, not mouse)
<button className="focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">
```

#### ARIA Labels

```tsx
// Icon-only buttons
<Button aria-label="Close dialog">
  <X className="w-4 h-4" />
</Button>

// Loading states
<div role="status" aria-live="polite" aria-busy="true">
  Loading...
</div>

// Error alerts
<div role="alert" aria-live="assertive">
  Error: {message}
</div>
```

#### Screen Reader Support

```tsx
// Hide decorative elements
<Icon aria-hidden="true" />

// Announce dynamic content
<div aria-live="polite" aria-atomic="true">
  {statusMessage}
</div>

// Skip links
<a href="#main-content" className="sr-only focus:not-sr-only">
  Skip to main content
</a>
```

---

## Implementation Guide

### 1. Import Design Tokens

In your `_app.tsx` or root layout:

```tsx
import '@/styles/design-tokens.css';
import '@/styles/globals.css';
```

### 2. Use Tailwind Classes (Preferred)

```tsx
// ✅ DO: Use Tailwind utility classes
<div className="p-6 bg-card rounded-xl shadow-sm">
  <h2 className="text-2xl font-semibold mb-4">Title</h2>
  <p className="text-sm text-muted-foreground">Description</p>
</div>
```

### 3. Use CSS Variables (When Needed)

```tsx
// For dynamic values
<div style={{ height: `calc(100vh - var(--size-header))` }}>

// For component-specific tokens
<div className="px-[var(--card-padding-md)]">
```

### 4. Avoid Inline Styles

```tsx
// ❌ DON'T: Inline styles with magic numbers
<div style={{ padding: '24px', borderRadius: '16px' }}>

// ✅ DO: Use Tailwind classes
<div className="p-6 rounded-xl">
```

### 5. Component Composition

```tsx
// Build complex UIs by composing simple components
function GameCard({ game }) {
  return (
    <Card className="p-6 hover:shadow-lg transition-shadow">
      <div className="flex items-start gap-4">
        <Avatar src={game.image} alt={game.name} />
        <div className="flex-1">
          <CardTitle>{game.name}</CardTitle>
          <CardDescription>{game.description}</CardDescription>
        </div>
        <Badge variant="secondary">{game.playerCount} players</Badge>
      </div>
      <CardFooter className="mt-4 pt-4 border-t">
        <Button className="w-full">View Details</Button>
      </CardFooter>
    </Card>
  );
}
```

---

## Migration Guide

### From Inline Styles to Design Tokens

**Before**:
```tsx
<div style={{
  padding: '24px',
  backgroundColor: '#f8f9fa',
  borderRadius: '16px',
  border: '1px solid #dadce0',
  boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
}}>
```

**After**:
```tsx
<div className="p-6 bg-card rounded-xl border border-border shadow-sm">
```

### From Magic Numbers to Tokens

Create a migration map:

| Old Value | New Token | Tailwind Class |
|-----------|-----------|----------------|
| `padding: 24px` | `--space-6` | `p-6` |
| `margin: 16px` | `--space-4` | `m-4` |
| `gap: 12px` | `--space-3` | `gap-3` |
| `border-radius: 8px` | `--radius-md` | `rounded-md` |
| `width: 320px` | `--size-sidebar` | `w-80` |
| `height: 100vh` | - | `h-screen` or `h-dvh` |
| `font-size: 14px` | `--text-sm` | `text-sm` |
| `font-weight: 600` | `--font-semibold` | `font-semibold` |

### Priority Migration Order

1. **Critical** (Sprint 1):
   - Upload page (remove 200+ inline styles)
   - Chat components (standardize spacing)
   - Landing page (consistent button styles)

2. **Important** (Sprint 2):
   - Admin pages
   - Modal/Dialog components
   - Form components

3. **Nice-to-have** (Sprint 3+):
   - Minor utility components
   - Edge case styles

---

## Examples

### Example 1: Refactored Upload Page

**Before**:
```tsx
<div style={{
  padding: '40px',
  maxWidth: '900px',
  margin: '0 auto',
  fontFamily: 'system-ui, sans-serif'
}}>
  <h1 style={{ marginBottom: '10px' }}>PDF Import Wizard</h1>
</div>
```

**After**:
```tsx
<div className="max-w-[56rem] mx-auto p-10 font-sans">
  <h1 className="mb-2.5">PDF Import Wizard</h1>
</div>
```

### Example 2: Chat Sidebar

**Before**:
```tsx
<aside style={{
  width: sidebarCollapsed ? 0 : 320,
  minWidth: sidebarCollapsed ? 0 : 320,
  background: '#f8f9fa',
  borderRight: '1px solid #dadce0'
}}>
```

**After**:
```tsx
<aside className={cn(
  "bg-sidebar border-r border-sidebar-border transition-all",
  sidebarCollapsed ? "w-0 min-w-0" : "w-80 min-w-80"
)}>
```

### Example 3: Error Display

**Before**:
```tsx
{errorMessage && (
  <div style={{
    padding: '16px',
    backgroundColor: '#ffebee',
    borderRadius: '4px',
    marginBottom: '20px',
    fontSize: '14px'
  }}>
    {errorMessage}
  </div>
)}
```

**After**:
```tsx
{errorMessage && (
  <Alert variant="destructive" className="mb-5">
    <AlertCircle className="h-4 w-4" />
    <AlertTitle>Error</AlertTitle>
    <AlertDescription>{errorMessage}</AlertDescription>
  </Alert>
)}
```

---

## Resources

- **Tailwind CSS**: https://tailwindcss.com/docs
- **Shadcn/UI**: https://ui.shadcn.com
- **Radix UI**: https://radix-ui.com
- **WCAG Guidelines**: https://www.w3.org/WAI/WCAG21/quickref/
- **Design Tokens**: `apps/web/src/styles/design-tokens.css`

---

## Maintenance

### Adding New Tokens

1. Define in `design-tokens.css`
2. Document in this guide
3. Add examples
4. Update Storybook (when available)

### Updating Colors

When changing semantic colors:

1. Update both `:root` and `.dark` variants
2. Test contrast ratios with WebAIM tool
3. Update all affected components
4. Test in both light and dark modes

### Versioning

Design system follows semantic versioning:

- **Major** (1.0.0 → 2.0.0): Breaking changes
- **Minor** (1.0.0 → 1.1.0): New tokens/components
- **Patch** (1.0.0 → 1.0.1): Bug fixes, documentation

---

**Questions?** Open an issue with label `design-system` on GitHub.
