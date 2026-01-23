# Morphllm Automation Patterns - Site-Wide Redesign

**Purpose**: Document automated pattern replacements for bulk theme updates across 250+ components

**Tool**: Morphllm MCP Server
**Efficiency**: 95% time reduction (50 hours → 2.5 hours)
**Scope**: ~200 components with pattern-based styling

---

## Pattern 1: Card Background & Glass Effect

**Target**: All components using Card base class
**Count**: ~80 components
**Wave**: 2, 4, 5, 7

### Pattern Definition

**Search Pattern**:
```
className="bg-white border-gray-200
className="bg-white border-gray-300
className="bg-card border-border
```

**Replace With**:
```tsx
className="bg-card/90 backdrop-blur-glass border-border
  dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]
  shadow-[0_1px_3px_rgba(139,90,60,0.05)]
  dark:shadow-[0_4px_12px_rgba(0,0,0,0.3)]"
```

### Morphllm Command

```typescript
{
  instruction: "Update all Card component backgrounds to use theme tokens with glass morphism effect in light mode and solid dark background in dark mode. Apply backdrop-blur-glass utility, update border colors, and add theme-aware shadows.",
  path: "apps/web/src/components/**/*Card*.tsx",
  code_edit: `
// Apply to all className strings containing bg-white or bg-card

// FROM:
className="bg-white border-gray-200 rounded-lg shadow-sm"

// TO:
className="bg-card/90 backdrop-blur-glass border-border
  dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]
  rounded-lg
  shadow-[0_1px_3px_rgba(139,90,60,0.05)]
  dark:shadow-[0_4px_12px_rgba(0,0,0,0.3)]"
  `
}
```

### Files Affected (Examples)
```
apps/web/src/components/admin/StatCard.tsx
apps/web/src/components/admin/KPICard.tsx
apps/web/src/components/admin/ServiceCard.tsx
apps/web/src/components/games/GameCard.tsx
apps/web/src/components/library/UserGameCard.tsx
... (75+ more)
```

---

## Pattern 2: Text Color Semantic Tokens

**Target**: All text color classes
**Count**: ~150 instances
**Wave**: 2, 4, 5, 6, 7

### Pattern Definition

**Search Patterns**:
```
text-gray-900 → Primary text
text-gray-700 → Secondary headings
text-gray-600 → Secondary text
text-gray-500 → Tertiary/muted text
text-gray-400 → Placeholder text
```

**Replace With**:
```tsx
text-gray-900 → text-foreground dark:text-[#e8e4d8]
text-gray-700 → text-foreground/90 dark:text-[#e8e4d8]/90
text-gray-600 → text-muted-foreground dark:text-[#999]
text-gray-500 → text-muted-foreground/80 dark:text-[#666]
text-gray-400 → text-muted-foreground/60 dark:text-[#666]/60
```

### Morphllm Command

```typescript
{
  instruction: "Replace all hardcoded gray text colors with semantic theme tokens that support dark mode",
  path: "apps/web/src/components/**/*.tsx",
  code_edit: `
// ... existing code ...

// Replace text color patterns:
// text-gray-900 → text-foreground dark:text-[#e8e4d8]
// text-gray-600 → text-muted-foreground dark:text-[#999]
// text-gray-500 → text-muted-foreground/80 dark:text-[#666]

// ... existing code ...
  `
}
```

---

## Pattern 3: Hover Transform & Shadow

**Target**: Interactive cards with hover states
**Count**: ~60 components
**Wave**: 2, 4, 5, 7

### Pattern Definition

**Search Pattern**:
```
hover:shadow-lg
hover:shadow-md
hover:scale-105
```

**Replace With**:
```tsx
hover:-translate-y-1
hover:shadow-[0_8px_32px_rgba(139,90,60,0.15)]
hover:border-[#d2691e]
dark:hover:shadow-[0_6px_20px_rgba(210,105,30,0.3)]
transition-all duration-300
```

### Morphllm Command

```typescript
{
  instruction: "Add consistent hover effects with theme-aware shadows and subtle lift transform. Use MeepleAI shadow colors.",
  path: "apps/web/src/components/**/*Card*.tsx",
  code_edit: `
// ... existing code ...

// Update hover states on interactive cards:
className="...
  hover:-translate-y-1
  hover:shadow-[0_8px_32px_rgba(139,90,60,0.15)]
  hover:border-[#d2691e]
  dark:hover:shadow-[0_6px_20px_rgba(210,105,30,0.3)]
  transition-all duration-300"

// ... existing code ...
  `
}
```

---

## Pattern 4: Icon Container Backgrounds

**Target**: Icon wrapper divs
**Count**: ~40 components
**Wave**: 2, 4, 5

### Pattern Definition

**Search Pattern**:
```
bg-gray-100 (icon containers)
bg-gray-50 (icon containers)
bg-blue-100 (colored icons)
bg-green-100 (status icons)
```

**Replace With**:
```tsx
// Default icons
bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)]

// Success icons
bg-green-100 dark:bg-[rgba(22,163,74,0.15)]

// Warning icons
bg-yellow-100 dark:bg-[rgba(251,191,36,0.15)]

// Danger icons
bg-red-100 dark:bg-[rgba(220,38,38,0.15)]
```

### Morphllm Command

```typescript
{
  instruction: "Update icon container backgrounds to use MeepleAI cream (#fef3e2) in light mode and semi-transparent warm overlays in dark mode",
  path: "apps/web/src/components/**/*.tsx",
  code_edit: `
// ... existing code ...

// Icon containers - default variant:
className="... bg-[#fef3e2] dark:bg-[rgba(210,105,30,0.15)] ..."

// Success variant:
className="... bg-green-100 dark:bg-[rgba(22,163,74,0.15)] ..."

// ... existing code ...
  `
}
```

---

## Pattern 5: Badge Color Variants

**Target**: Badge components
**Count**: ~30 components
**Wave**: 2, 4, 5, 7

### Pattern Definition

**Search Pattern**:
```
bg-blue-100 text-blue-800
bg-green-100 text-green-800
bg-yellow-100 text-yellow-800
bg-red-100 text-red-800
bg-purple-100 text-purple-800
```

**Replace With**:
```tsx
// Default
bg-accent/10 text-accent dark:bg-accent/20 dark:text-accent-foreground

// Success
bg-green-100 text-green-700 dark:bg-green-500/20 dark:text-green-400

// Warning
bg-yellow-100 text-yellow-700 dark:bg-yellow-500/20 dark:text-yellow-400

// Danger
bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400

// Info
bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-400
```

### Morphllm Command

```typescript
{
  instruction: "Update badge backgrounds and text colors to use theme-aware accent colors with semantic variants",
  path: "apps/web/src/components/**/*Badge*.tsx",
  code_edit: `
// ... existing code ...

// Badge variants - use semantic tokens:
// bg-blue-100 text-blue-800 → bg-accent/10 text-accent dark:bg-accent/20
// bg-green-100 text-green-800 → bg-green-100 dark:bg-green-500/20 dark:text-green-400

// ... existing code ...
  `
}
```

---

## Pattern 6: Gradient Text Headings

**Target**: Page titles and section headings
**Count**: ~20 headings
**Wave**: 5, 7

### Pattern Definition

**Search Pattern**:
```
text-3xl font-bold text-gray-900
text-2xl font-semibold text-gray-900
font-quicksand text-4xl font-bold
```

**Replace With**:
```tsx
text-3xl font-bold
bg-gradient-to-r from-[#d2691e] to-[#f59e0b]
bg-clip-text text-transparent
dark:from-[#fbbf24] dark:to-[#d2691e]
```

### Morphllm Command

```typescript
{
  instruction: "Apply gradient text effect to all page titles and major section headings with theme-aware gradient colors",
  path: "apps/web/src/app/**/page.tsx",
  code_edit: `
// ... existing code ...

// Page titles - add gradient:
<h1 className="text-3xl font-bold
  bg-gradient-to-r from-[#d2691e] to-[#f59e0b]
  bg-clip-text text-transparent
  dark:from-[#fbbf24] dark:to-[#d2691e]">
  {title}
</h1>

// ... existing code ...
  `
}
```

---

## Pattern 7: Form Input Backgrounds

**Target**: Input, Textarea, Select components
**Count**: ~50 instances
**Wave**: 2, 4, 5

### Pattern Definition

**Search Pattern**:
```
bg-white border-gray-300
bg-gray-50 border-gray-200
```

**Replace With**:
```tsx
bg-background border-border
dark:bg-[#2d2d2d] dark:border-[rgba(210,105,30,0.2)]
focus:border-primary dark:focus:border-[#fbbf24]
focus:ring-primary/20 dark:focus:ring-[#fbbf24]/20
```

---

## Execution Strategy

### Step 1: Prepare Morphllm Patterns

Create pattern definition files:
```
docs/pdca/site-wide-redesign/morphllm/
├── pattern-1-cards.json
├── pattern-2-text.json
├── pattern-3-hover.json
├── pattern-4-icons.json
├── pattern-5-badges.json
├── pattern-6-gradients.json
└── pattern-7-inputs.json
```

### Step 2: Dry Run

Run each pattern in dry-run mode first:
```bash
# Test pattern without applying
morphllm apply --pattern pattern-1-cards.json --dry-run
```

### Step 3: Execute Patterns

Apply patterns sequentially:
```bash
# Pattern 1: Card backgrounds
morphllm apply --pattern pattern-1-cards.json

# Pattern 2: Text colors
morphllm apply --pattern pattern-2-text.json

# ... continue for all 7 patterns
```

### Step 4: Manual Review

Review changes before committing:
```bash
git diff                          # Review all changes
git add apps/web/src/components   # Stage if correct
git commit -m "feat(ui): apply theme patterns via Morphllm"
```

### Step 5: Test & Validate

Run tests after each pattern:
```bash
pnpm test                  # Unit tests
pnpm typecheck             # TypeScript
pnpm lint                  # ESLint
pnpm chromatic             # Visual regression
```

---

## Manual Updates Required

### Components Requiring Manual Work

Some components have complex logic requiring manual updates:

**Complex Styling** (20 components):
- `ChartsSection.tsx` - Recharts theme configuration
- `DiffViewer.tsx` - Code syntax highlighting
- `CodeBlock.tsx` - Prism theme switching
- `RichTextEditor.tsx` - Editor theme
- `GrafanaEmbed.tsx` - iframe styling

**Dynamic Styling** (10 components):
- Components with runtime-generated styles
- Components with conditional className logic
- Components using CSS-in-JS

**Third-Party Integration** (5 components):
- Components wrapping external libraries
- Radix UI customizations beyond Tailwind

---

## Validation Checklist

After Morphllm automation:

- [ ] Run TypeScript check: `pnpm typecheck`
- [ ] Run ESLint: `pnpm lint`
- [ ] Visual inspection: Check 5-10 components manually
- [ ] Test theme switching: Verify dark/light both work
- [ ] Run unit tests: `pnpm test`
- [ ] Check Storybook: Verify stories render
- [ ] Chromatic: Approve or reject visual changes
- [ ] Git diff review: Ensure no unintended changes

---

## Learnings & Optimizations

(To be filled during execution)

### What Worked Well
- (Pattern X automated Y components successfully)

### What Required Manual Intervention
- (Components that couldn't be automated and why)

### Pattern Improvements
- (How patterns could be refined for future use)

---

**Status**: 📋 Template ready for execution
**Execution**: Wave 2, 4, 5, 7 (during component updates)
