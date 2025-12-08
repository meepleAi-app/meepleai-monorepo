# Design System Overview - Quick Reference

**Date**: 2025-11-13
**Status**: Production Ready
**Version**: 1.0

---

## 🎨 What is This?

A comprehensive design token system for MeepleAI frontend, providing consistent spacing, typography, colors, and component patterns. All tokens are defined in `apps/web/src/styles/design-tokens.css` and work seamlessly with Tailwind CSS 4.

---

## 🚀 Quick Start (30 Seconds)

### 1. Import is Already Active

Design tokens are automatically imported in `globals.css`:

```css
@import "tailwindcss";
@import "./design-tokens.css"; /* ← Already done! */
```

### 2. Use Tailwind Classes

Instead of inline styles, use Tailwind classes that reference design tokens:

{% raw %}
```tsx
// ❌ Before
<div style={{ padding: 24, background: '#f8f9fa', borderRadius: 8 }}>

// ✅ After
<div className="p-6 bg-sidebar rounded-md">
```
{% endraw %}

### 3. Reference This Guide

Keep this page open while coding - it has all the token mappings you need.

---

## 📏 Spacing System

**Base Unit**: 4px (0.25rem)

### Common Spacing Values

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `p-1` | 4px | `--space-1` | Tight padding (badges) |
| `p-2` | 8px | `--space-2` | Small padding (buttons) |
| `p-3` | 12px | `--space-3` | Compact padding (cards) |
| `p-4` | 16px | `--space-4` | Standard padding (containers) |
| `p-6` | 24px | `--space-6` | Comfortable padding (sections) |
| `p-8` | 32px | `--space-8` | Large padding (page layouts) |
| `p-12` | 48px | `--space-12` | Extra large (hero sections) |

### Spacing Utilities

```tsx
// Padding
<div className="p-4">         {/* All sides: 16px */}
<div className="px-6 py-4">   {/* Horizontal: 24px, Vertical: 16px */}
<div className="pt-8 pb-4">   {/* Top: 32px, Bottom: 16px */}

// Margin
<div className="m-4">         {/* All sides: 16px */}
<div className="mx-auto">     {/* Center horizontally */}
<div className="mt-6 mb-4">   {/* Top: 24px, Bottom: 16px */}

// Gap (for flex/grid)
<div className="flex gap-4">  {/* 16px gap between items */}
<div className="grid gap-6">  {/* 24px gap in grid */}

// Space between (flex children)
<div className="flex flex-col space-y-4">  {/* 16px vertical space */}
<div className="flex space-x-3">           {/* 12px horizontal space */}
```

### Real-World Examples

```tsx
// Card component
<div className="bg-card border border-border rounded-lg p-6 space-y-4">
  <h2 className="text-lg font-semibold">Card Title</h2>
  <p className="text-sm text-muted-foreground">Card content</p>
</div>

// Button group
<div className="flex gap-2">
  <button className="px-4 py-2 bg-primary text-white rounded-md">Primary</button>
  <button className="px-4 py-2 bg-secondary text-white rounded-md">Secondary</button>
</div>

// Form layout
<form className="space-y-6">
  <div className="space-y-2">
    <label className="text-sm font-medium">Email</label>
    <input className="w-full px-3 py-2 border rounded-md" />
  </div>
  <button className="w-full px-4 py-2 bg-primary text-white rounded-md">
    Submit
  </button>
</form>
```

---

## 🔤 Typography System

### Font Sizes

| Tailwind | Size | Token | Use Case |
|----------|------|-------|----------|
| `text-xs` | 12px | `--text-xs` | Captions, labels |
| `text-sm` | 14px | `--text-sm` | Body text (small) |
| `text-base` | 16px | `--text-base` | Body text (default) |
| `text-lg` | 18px | `--text-lg` | Large body, card titles |
| `text-xl` | 20px | `--text-xl` | Section headings |
| `text-2xl` | 24px | `--text-2xl` | Page headings |
| `text-3xl` | 30px | `--text-3xl` | Large headings |
| `text-4xl` | 36px | `--text-4xl` | Hero headings |

### Font Weights

| Tailwind | Weight | Token | Use Case |
|----------|--------|-------|----------|
| `font-normal` | 400 | `--font-weight-normal` | Body text |
| `font-medium` | 500 | `--font-weight-medium` | Emphasis |
| `font-semibold` | 600 | `--font-weight-semibold` | Headings |
| `font-bold` | 700 | `--font-weight-bold` | Strong emphasis |

### Line Heights

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `leading-tight` | 1.25 | `--line-height-tight` | Headings |
| `leading-normal` | 1.5 | `--line-height-normal` | Body text |
| `leading-relaxed` | 1.625 | `--line-height-relaxed` | Long-form content |

### Typography Examples

```tsx
// Page heading
<h1 className="text-3xl font-bold leading-tight">
  Welcome to MeepleAI
</h1>

// Section heading
<h2 className="text-xl font-semibold mb-4">
  Recent Games
</h2>

// Body text
<p className="text-base leading-normal text-foreground">
  Ask questions about board game rules in Italian or English.
</p>

// Small text / caption
<span className="text-xs text-muted-foreground">
  Last updated 5 minutes ago
</span>

// Card title
<h3 className="text-lg font-medium">Catan</h3>

// Button text
<button className="text-sm font-medium px-4 py-2">
  Click Me
</button>
```

---

## 🎨 Color System

### Semantic Colors

Use semantic color names, not specific shades. They adapt to light/dark mode automatically.

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `bg-background` | `--background` | Page background |
| `bg-foreground` | `--foreground` | Text color |
| `bg-card` | `--card` | Card backgrounds |
| `bg-card-foreground` | `--card-foreground` | Text on cards |
| `bg-popover` | `--popover` | Dropdown, tooltip backgrounds |
| `bg-primary` | `--primary` | Primary actions |
| `bg-secondary` | `--secondary` | Secondary actions |
| `bg-accent` | `--accent` | Highlights |
| `bg-muted` | `--muted` | Subtle backgrounds |
| `bg-destructive` | `--destructive` | Errors, danger |

### Border Colors

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `border-border` | `--border` | Default borders |
| `border-input` | `--input` | Input borders |
| `border-ring` | `--ring` | Focus rings |

### Text Colors

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `text-foreground` | `--foreground` | Primary text |
| `text-muted-foreground` | `--muted-foreground` | Secondary text |
| `text-primary` | `--primary` | Primary links/actions |
| `text-destructive` | `--destructive` | Error text |

### Game-Specific Colors

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `bg-game-catan` | `--game-catan` | Catan theme |
| `bg-game-wingspan` | `--game-wingspan` | Wingspan theme |
| `bg-game-azul` | `--game-azul` | Azul theme |

### Color Examples

```tsx
// Card with semantic colors
<div className="bg-card text-card-foreground border border-border rounded-lg p-6">
  <h2>Card Content</h2>
</div>

// Button variants
<button className="bg-primary text-primary-foreground px-4 py-2 rounded-md">
  Primary Action
</button>

<button className="bg-secondary text-secondary-foreground px-4 py-2 rounded-md">
  Secondary Action
</button>

<button className="bg-destructive text-destructive-foreground px-4 py-2 rounded-md">
  Delete
</button>

// Muted text
<p className="text-muted-foreground text-sm">
  Optional secondary information
</p>

// Accent highlight
<span className="bg-accent text-accent-foreground px-2 py-1 rounded">
  New
</span>

// Game-themed card
<div className="bg-game-catan text-white p-4 rounded-lg">
  Catan Rules
</div>
```

### Dark Mode

Colors automatically adapt to dark mode via HSL format:

```css
:root {
  --background: 0 0% 100%;  /* White in light mode */
}

.dark {
  --background: 222 47% 11%;  /* Dark in dark mode */
}
```

**No additional classes needed** - just use semantic names!

---

## 📐 Layout & Sizing

### Width & Height

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `w-80` | 320px | `--size-sidebar` | Sidebar width |
| `w-96` | 384px | `--size-drawer` | Drawer width |
| `max-w-3xl` | 896px | `--size-content-max` | Content max width |
| `w-full` | 100% | - | Full width |
| `h-10` | 40px | `--size-button` | Button height |
| `h-screen` | 100vh | - | Full viewport height |

### Container Sizing Examples

```tsx
// Sidebar
<aside className="w-80 h-screen bg-sidebar border-r border-border">
  Sidebar content
</aside>

// Main content area
<main className="max-w-3xl mx-auto px-6 py-8">
  Main content
</main>

// Full-width hero
<section className="w-full bg-primary text-white py-20">
  Hero content
</section>

// Card grid
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
  <div className="bg-card p-6 rounded-lg">Card 1</div>
  <div className="bg-card p-6 rounded-lg">Card 2</div>
  <div className="bg-card p-6 rounded-lg">Card 3</div>
</div>

// Centered modal
<div className="fixed inset-0 flex items-center justify-center">
  <div className="bg-card rounded-lg p-6 w-96 max-w-full">
    Modal content
  </div>
</div>
```

---

## 🔲 Border Radius

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `rounded-sm` | 4px | `--radius-sm` | Subtle rounding |
| `rounded` | 6px | `--radius-base` | Default rounding |
| `rounded-md` | 8px | `--radius-md` | Cards, buttons |
| `rounded-lg` | 12px | `--radius-lg` | Large cards |
| `rounded-xl` | 16px | `--radius-xl` | Modals |
| `rounded-2xl` | 24px | `--radius-2xl` | Hero sections |
| `rounded-full` | 9999px | - | Circles, pills |

### Border Radius Examples

```tsx
// Button
<button className="px-4 py-2 bg-primary text-white rounded-md">
  Click Me
</button>

// Card
<div className="bg-card p-6 rounded-lg border border-border">
  Card content
</div>

// Avatar
<img src="/avatar.jpg" className="w-10 h-10 rounded-full" />

// Pill badge
<span className="px-3 py-1 bg-accent text-xs rounded-full">
  Badge
</span>

// Input
<input className="w-full px-3 py-2 border rounded-md" />
```

---

## 🌫️ Shadows

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `shadow-sm` | `--shadow-sm` | Subtle elevation (cards) |
| `shadow` | `--shadow-base` | Standard elevation |
| `shadow-md` | `--shadow-md` | Moderate elevation (dropdowns) |
| `shadow-lg` | `--shadow-lg` | High elevation (modals) |
| `shadow-xl` | `--shadow-xl` | Very high elevation |

### Shadow Examples

```tsx
// Card with subtle shadow
<div className="bg-card rounded-lg p-6 shadow-sm">
  Card content
</div>

// Floating button
<button className="fixed bottom-6 right-6 bg-primary text-white p-4 rounded-full shadow-lg">
  +
</button>

// Dropdown menu
<div className="absolute bg-popover rounded-md shadow-md p-2">
  <button className="w-full px-4 py-2 text-left hover:bg-accent">Option 1</button>
  <button className="w-full px-4 py-2 text-left hover:bg-accent">Option 2</button>
</div>

// Modal
<div className="fixed inset-0 flex items-center justify-center bg-black/50">
  <div className="bg-card rounded-xl p-6 shadow-xl max-w-md">
    Modal content
  </div>
</div>
```

---

## 🎭 Transitions & Animations

### Transition Duration

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `duration-100` | 100ms | `--transition-fast` | Quick feedback |
| `duration-200` | 200ms | `--transition-base` | Standard (default) |
| `duration-300` | 300ms | `--transition-slow` | Smooth animations |

### Transition Timing

| Tailwind | Token | Use Case |
|----------|-------|----------|
| `ease-in-out` | `--transition-ease` | Default easing |

### Animation Examples

```tsx
// Button hover effect
<button className="bg-primary text-white px-4 py-2 rounded-md
                   transition-colors duration-200
                   hover:bg-primary/90">
  Hover Me
</button>

// Scale on hover
<div className="transform transition-transform duration-200 hover:scale-105">
  <img src="/game.jpg" className="rounded-lg" />
</div>

// Fade in/out
<div className="opacity-0 transition-opacity duration-300 data-[state=open]:opacity-100">
  Tooltip content
</div>

// Slide in sidebar
<aside className="fixed left-0 top-0 h-screen w-80
                  transform transition-transform duration-300
                  -translate-x-full md:translate-x-0">
  Sidebar
</aside>
```

---

## 📱 Responsive Design

### Breakpoints

| Breakpoint | Min Width | Use Case |
|------------|-----------|----------|
| `sm:` | 640px | Small tablets |
| `md:` | 768px | Tablets |
| `lg:` | 1024px | Laptops |
| `xl:` | 1280px | Desktops |
| `2xl:` | 1536px | Large desktops |

### Mobile-First Approach

Default styles are mobile, then use breakpoints to adapt:

```tsx
// Mobile: single column, tablet: 2 cols, desktop: 3 cols
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
  <div>Item 1</div>
  <div>Item 2</div>
  <div>Item 3</div>
</div>

// Mobile: full width, tablet+: fixed width
<div className="w-full md:w-96">
  Content
</div>

// Mobile: stack, desktop: horizontal
<div className="flex flex-col lg:flex-row gap-4">
  <aside className="lg:w-80">Sidebar</aside>
  <main className="flex-1">Main</main>
</div>

// Hide on mobile, show on desktop
<div className="hidden lg:block">
  Desktop only content
</div>

// Show on mobile, hide on desktop
<div className="block lg:hidden">
  Mobile only content
</div>
```

---

## 🎯 Z-Index Layers

| Tailwind | Value | Token | Use Case |
|----------|-------|-------|----------|
| `z-0` | 0 | `--z-base` | Base layer |
| `z-10` | 10 | `--z-dropdown` | Dropdowns |
| `z-20` | 20 | `--z-sticky` | Sticky headers |
| `z-30` | 30 | `--z-modal-backdrop` | Modal backdrops |
| `z-40` | 40 | `--z-modal` | Modals |
| `z-50` | 50 | `--z-popover` | Popovers, tooltips |
| `z-100` | 100 | `--z-toast` | Toast notifications |

### Z-Index Examples

```tsx
// Sticky header
<header className="sticky top-0 z-20 bg-background border-b">
  Navigation
</header>

// Modal
<div className="fixed inset-0 z-40 flex items-center justify-center">
  <div className="fixed inset-0 z-30 bg-black/50" /> {/* Backdrop */}
  <div className="relative z-40 bg-card rounded-lg p-6"> {/* Content */}
    Modal content
  </div>
</div>

// Toast notification
<div className="fixed top-4 right-4 z-100 bg-card shadow-lg rounded-lg p-4">
  Notification
</div>

// Dropdown
<div className="relative">
  <button>Menu</button>
  <div className="absolute top-full left-0 z-10 bg-popover shadow-md rounded-md">
    Dropdown items
  </div>
</div>
```

---

## 🧩 Component Patterns

### Button

```tsx
// Primary button
<button className="px-4 py-2 bg-primary text-primary-foreground
                   rounded-md font-medium transition-colors
                   hover:bg-primary/90 focus:outline-none focus:ring-2
                   focus:ring-ring focus:ring-offset-2
                   disabled:opacity-50 disabled:pointer-events-none">
  Primary Button
</button>

// Secondary button
<button className="px-4 py-2 bg-secondary text-secondary-foreground
                   rounded-md font-medium transition-colors
                   hover:bg-secondary/80">
  Secondary Button
</button>

// Ghost button
<button className="px-4 py-2 text-foreground rounded-md font-medium
                   transition-colors hover:bg-accent hover:text-accent-foreground">
  Ghost Button
</button>

// Outline button
<button className="px-4 py-2 border border-input bg-background
                   rounded-md font-medium transition-colors
                   hover:bg-accent hover:text-accent-foreground">
  Outline Button
</button>
```

### Card

```tsx
<div className="bg-card text-card-foreground border border-border
                rounded-lg shadow-sm overflow-hidden">
  <div className="p-6 space-y-4">
    <h3 className="text-lg font-semibold">Card Title</h3>
    <p className="text-sm text-muted-foreground">
      Card description or content goes here.
    </p>
  </div>
  <div className="border-t border-border px-6 py-4 bg-muted/50">
    <button className="text-sm font-medium text-primary">Action</button>
  </div>
</div>
```

### Input

```tsx
<div className="space-y-2">
  <label className="text-sm font-medium text-foreground">
    Email
  </label>
  <input
    type="email"
    className="w-full px-3 py-2 bg-background border border-input
               rounded-md text-sm placeholder:text-muted-foreground
               focus:outline-none focus:ring-2 focus:ring-ring
               focus:ring-offset-2 disabled:opacity-50"
    placeholder="you@example.com"
  />
  <p className="text-xs text-muted-foreground">
    We'll never share your email.
  </p>
</div>
```

### Badge

```tsx
// Default badge
<span className="inline-flex items-center px-2.5 py-0.5 rounded-full
                 text-xs font-medium bg-primary text-primary-foreground">
  Badge
</span>

// Secondary badge
<span className="inline-flex items-center px-2.5 py-0.5 rounded-full
                 text-xs font-medium bg-secondary text-secondary-foreground">
  Secondary
</span>

// Outline badge
<span className="inline-flex items-center px-2.5 py-0.5 rounded-full
                 text-xs font-medium border border-border text-foreground">
  Outline
</span>
```

### Alert

```tsx
// Info alert
<div className="bg-blue-50 dark:bg-blue-950 border border-blue-200
                dark:border-blue-800 rounded-md p-4">
  <div className="flex gap-3">
    <InfoIcon className="w-5 h-5 text-blue-600 dark:text-blue-400" />
    <div className="flex-1 space-y-1">
      <h4 className="text-sm font-medium text-blue-900 dark:text-blue-100">
        Information
      </h4>
      <p className="text-sm text-blue-800 dark:text-blue-200">
        This is an informational message.
      </p>
    </div>
  </div>
</div>

// Error alert
<div className="bg-destructive/10 border border-destructive/20 rounded-md p-4">
  <div className="flex gap-3">
    <AlertIcon className="w-5 h-5 text-destructive" />
    <div className="flex-1 space-y-1">
      <h4 className="text-sm font-medium text-destructive">Error</h4>
      <p className="text-sm text-destructive/90">
        Something went wrong. Please try again.
      </p>
    </div>
  </div>
</div>
```

---

## ♿ Accessibility Patterns

### Focus States

Always include visible focus states:

```tsx
<button className="focus:outline-none focus:ring-2 focus:ring-ring
                   focus:ring-offset-2">
  Accessible Button
</button>

<a href="#" className="focus:outline-none focus:underline focus:ring-2
                       focus:ring-ring focus:ring-offset-2 rounded">
  Accessible Link
</a>
```

### Touch Targets

Minimum 44x44px for mobile:

```tsx
// Good - 44px height minimum
<button className="px-4 py-2 min-h-[44px]">
  Touch-friendly
</button>

// Bad - too small
<button className="px-2 py-1 text-xs">
  Too small
</button>
```

### Screen Reader Text

Hide visually but keep for screen readers:

```tsx
<button>
  <span className="sr-only">Close modal</span>
  <XIcon className="w-5 h-5" />
</button>
```

### ARIA Labels

```tsx
<button aria-label="Search games">
  <SearchIcon className="w-5 h-5" />
</button>

<input
  type="search"
  aria-label="Search"
  placeholder="Search games..."
/>
```

---

## 🎨 Dark Mode

Design tokens automatically support dark mode. No extra classes needed!

### How It Works

```css
/* Light mode (default) */
:root {
  --background: 0 0% 100%;  /* White */
  --foreground: 222 47% 11%;  /* Dark gray */
}

/* Dark mode (automatically applied) */
.dark {
  --background: 222 47% 11%;  /* Dark gray */
  --foreground: 210 40% 98%;  /* Light gray */
}
```

### Usage

Just use semantic color names - they adapt automatically:

```tsx
// Works in both light and dark mode
<div className="bg-background text-foreground">
  Content
</div>

<div className="bg-card text-card-foreground border border-border">
  Card
</div>
```

### Testing Dark Mode

```tsx
// Toggle dark mode
<button onClick={() => document.documentElement.classList.toggle('dark')}>
  Toggle Theme
</button>
```

---

## 📦 Common Component Combos

### Form with Validation

```tsx
<form className="space-y-6 max-w-md">
  <div className="space-y-2">
    <label className="text-sm font-medium">Email</label>
    <input
      className="w-full px-3 py-2 border border-input rounded-md
                 focus:ring-2 focus:ring-ring"
      type="email"
    />
    <p className="text-xs text-destructive">Invalid email address</p>
  </div>

  <button className="w-full px-4 py-2 bg-primary text-primary-foreground
                     rounded-md font-medium hover:bg-primary/90">
    Submit
  </button>
</form>
```

### Card Grid

```tsx
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
  {games.map(game => (
    <div key={game.id} className="bg-card border border-border rounded-lg
                                   shadow-sm hover:shadow-md transition-shadow">
      <img src={game.image} className="w-full h-48 object-cover rounded-t-lg" />
      <div className="p-4 space-y-2">
        <h3 className="text-lg font-semibold">{game.title}</h3>
        <p className="text-sm text-muted-foreground">{game.description}</p>
        <div className="flex items-center justify-between pt-2">
          <span className="text-xs text-muted-foreground">
            {game.players} players
          </span>
          <button className="text-sm font-medium text-primary">
            View Rules
          </button>
        </div>
      </div>
    </div>
  ))}
</div>
```

### Modal

```tsx
<div className="fixed inset-0 z-40 flex items-center justify-center p-4">
  {/* Backdrop */}
  <div className="fixed inset-0 z-30 bg-black/50" onClick={onClose} />

  {/* Modal */}
  <div className="relative z-40 bg-card rounded-xl shadow-xl
                  max-w-md w-full max-h-[90vh] overflow-y-auto">
    {/* Header */}
    <div className="flex items-center justify-between p-6 border-b border-border">
      <h2 className="text-xl font-semibold">Modal Title</h2>
      <button
        onClick={onClose}
        className="text-muted-foreground hover:text-foreground transition-colors"
      >
        <XIcon className="w-5 h-5" />
      </button>
    </div>

    {/* Content */}
    <div className="p-6 space-y-4">
      <p className="text-sm text-muted-foreground">
        Modal content goes here.
      </p>
    </div>

    {/* Footer */}
    <div className="flex justify-end gap-3 p-6 border-t border-border">
      <button
        onClick={onClose}
        className="px-4 py-2 border border-input rounded-md font-medium
                   hover:bg-accent transition-colors"
      >
        Cancel
      </button>
      <button className="px-4 py-2 bg-primary text-primary-foreground
                         rounded-md font-medium hover:bg-primary/90 transition-colors">
        Confirm
      </button>
    </div>
  </div>
</div>
```

---

## 🚫 What NOT to Do

### ❌ Don't Use Inline Styles

{% raw %}
```tsx
// ❌ Bad
<div style={{ padding: 24, background: '#f8f9fa' }}>

// ✅ Good
<div className="p-6 bg-sidebar">
```
{% endraw %}

### ❌ Don't Use Magic Numbers

```tsx
// ❌ Bad
<div className="p-[23px] text-[17px]">

// ✅ Good
<div className="p-6 text-lg">
```

### ❌ Don't Use Specific Color Shades

```tsx
// ❌ Bad - won't adapt to dark mode
<div className="bg-gray-100 text-gray-900">

// ✅ Good - adapts to theme
<div className="bg-sidebar text-foreground">
```

### ❌ Don't Mix Styling Approaches

{% raw %}
```tsx
// ❌ Bad - inconsistent
<div style={{ padding: 24 }} className="bg-card">

// ✅ Good - use Tailwind only
<div className="p-6 bg-card">
```
{% endraw %}

---

## ✅ Best Practices

1. **Use semantic tokens** over specific values
   - ✅ `bg-sidebar` (semantic)
   - ❌ `bg-gray-100` (specific)

2. **Mobile-first responsive**
   - ✅ `w-full md:w-80`
   - ❌ `w-80 sm:w-full`

3. **Consistent spacing scale**
   - ✅ `p-4 gap-4 space-y-4`
   - ❌ `p-[15px] gap-[14px]`

4. **Always add focus states**
   - ✅ `focus:ring-2 focus:ring-ring`
   - ❌ No focus styling

5. **Test in both themes**
   - Always verify light + dark mode
   - Use semantic colors only

6. **Minimum touch targets**
   - Buttons: min 44x44px
   - Links: adequate padding

7. **Use transitions for interactivity**
   - ✅ `transition-colors hover:bg-primary/90`
   - ❌ Instant color changes

---

## 📚 Additional Resources

### Full Documentation

- **Complete Guide**: `docs/04-frontend/design-system.md` (800+ lines)
- **Quick Start**: `DESIGN-SYSTEM-QUICKSTART.md` (this file)
- **Token Definitions**: `apps/web/src/styles/design-tokens.css`

### External Resources

- **Tailwind CSS**: https://tailwindcss.com/docs
- **Shadcn/UI**: https://ui.shadcn.com
- **Radix UI**: https://radix-ui.com
- **WCAG Guidelines**: https://www.w3.org/WAI/WCAG21/quickref/

### Tools

- **Tailwind IntelliSense**: VS Code extension for autocomplete
- **Headless UI**: Unstyled accessible components
- **Tailwind Prettier Plugin**: Auto-sort Tailwind classes

---

## 🎯 Quick Reference Cheat Sheet

### Most Used Patterns

```tsx
// Container
<div className="max-w-3xl mx-auto px-6 py-8">

// Card
<div className="bg-card border border-border rounded-lg p-6 shadow-sm">

// Button
<button className="px-4 py-2 bg-primary text-primary-foreground rounded-md
                   font-medium hover:bg-primary/90 transition-colors">

// Input
<input className="w-full px-3 py-2 border border-input rounded-md
                  focus:ring-2 focus:ring-ring">

// Grid
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">

// Flex
<div className="flex items-center justify-between gap-4">

// Text
<h1 className="text-3xl font-bold">
<p className="text-base text-muted-foreground">
<span className="text-sm font-medium">
```

---

## 💡 Pro Tips

1. **Use VS Code IntelliSense**: Install Tailwind CSS IntelliSense for autocomplete
2. **Extract repeated patterns**: Create reusable components for common patterns
3. **Use `cn()` utility**: Merge classes conditionally (from shadcn)
4. **Check Storybook**: After Sprint 3, all patterns will be documented
5. **When in doubt**: Check existing components for patterns

---

**Version**: 1.0
**Last Updated**: 2025-11-13
**Maintained By**: Design Team

**Questions?** Check `docs/04-frontend/design-system.md` for detailed explanations.
