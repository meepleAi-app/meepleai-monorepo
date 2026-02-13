# Epic #4068: Styling & Customization Guide

**Tailwind classes, CSS variables, theme customization, responsive design**

---

## Tag System Styling

### Base Tag Strip Styles

```typescript
// components/ui/tags/TagStrip.tsx (default styles)
const baseClasses = cn(
  // Positioning
  'absolute top-0 bottom-0',
  position === 'left' ? 'left-0 border-r' : 'right-0 border-l',

  // Layout
  'flex flex-col items-center gap-1 p-1',

  // Sizing (responsive)
  stripWidth, // w-8 (desktop), w-7 (tablet), w-6 (mobile)

  // Visual effects
  'bg-gradient-to-b from-black/20 via-black/10 to-black/5',
  'backdrop-blur-sm',
  'border-white/10',

  // Z-index
  'z-10'
);
```

### Customizing Tag Strip

**Custom Width**:
```tsx
// Override variant width
<TagStrip
  tags={tags}
  className="w-10" // 40px instead of default 32px
/>

// Or responsive custom widths
<TagStrip
  tags={tags}
  className="w-6 sm:w-8 md:w-10 lg:w-12"
/>
// 24px → 32px → 40px → 48px
```

**Custom Background**:
```tsx
// Different gradient
<div className="relative">
  <TagStrip tags={tags} />
  <style jsx>{`
    [aria-label="Entity tags"] {
      background: linear-gradient(to bottom,
        hsl(var(--primary) / 0.2),
        hsl(var(--primary) / 0.05)
      );
    }
  `}</style>
</div>

// Or solid background
<TagStrip
  tags={tags}
  className="bg-black/30" // Replace gradient
/>
```

**Custom Border**:
```tsx
// Thicker border
<TagStrip tags={tags} className="border-white/30 border-r-2" />

// Colored border (matches entity color)
<TagStrip
  tags={tags}
  style={{ borderRightColor: `hsl(${entityColors[entity].hsl})` }}
/>
```

**Custom Position** (outside left/right):
```tsx
// Top horizontal strip (custom implementation)
<div className="absolute top-0 left-0 right-0 h-8 flex flex-row gap-1 p-1 bg-black/20">
  {tags.slice(0, 5).map(tag => <TagBadge key={tag.id} tag={tag} variant="desktop" />)}
</div>
```

---

### Customizing Tag Badges

**Custom Sizes**:
```tsx
// Small tags
<TagBadge
  tag={tag}
  variant="mobile"
  className="w-5 h-5" // 20px instead of 24px
/>

// Large tags
<TagBadge
  tag={tag}
  variant="desktop"
  className="px-2 py-1.5 text-xs" // Larger padding
/>
```

**Custom Colors (Theming)**:
```css
/* globals.css: CSS variables for tag colors */
:root {
  /* Game tags */
  --tag-new-bg: 142 76% 36%;
  --tag-new-fg: 0 0% 100%;
  --tag-sale-bg: 0 84% 60%;
  --tag-sale-fg: 0 0% 100%;

  /* Agent tags */
  --tag-rag-bg: 38 92% 50%;
  --tag-vision-bg: 262 83% 58%;
}

/* Dark theme adjustments */
.dark {
  --tag-new-bg: 142 76% 45%; /* Lighter in dark mode */
  --tag-sale-bg: 0 84% 70%;
}
```

```typescript
// Use CSS variables in presets
export const GAME_TAG_PRESETS = {
  new: {
    label: 'New',
    icon: Sparkles,
    bgColor: 'hsl(var(--tag-new-bg))',
    color: 'hsl(var(--tag-new-fg))'
  },
  // ...
};
```

**Custom Icons**:
```tsx
// Use custom icon library (e.g., Heroicons instead of Lucide)
import { SparklesIcon } from '@heroicons/react/24/solid';

const customTag: Tag = {
  id: 'custom',
  label: 'Custom',
  icon: SparklesIcon, // Works with any React component
  bgColor: 'hsl(280 70% 50%)'
};
```

**Custom Shapes**:
```tsx
// Circular tags
<TagBadge
  tag={tag}
  variant="desktop"
  className="rounded-full" // Instead of default rounded
/>

// Hexagonal tags (advanced CSS)
<TagBadge
  tag={tag}
  variant="desktop"
  className="hexagon" // Custom class
/>

// CSS
.hexagon {
  clip-path: polygon(30% 0%, 70% 0%, 100% 50%, 70% 100%, 30% 100%, 0% 50%);
}
```

---

## Tooltip System Styling

### Tooltip Base Styles

```typescript
// components/ui/overlays/SmartTooltip.tsx
const tooltipClasses = cn(
  'absolute z-50',
  'rounded-lg border border-border',
  'bg-popover px-3 py-2',
  'text-sm text-popover-foreground',
  'shadow-md',
  'animate-in fade-in-0 zoom-in-95'
);
```

### Customizing Tooltip Appearance

**Custom Size**:
```tsx
<SmartTooltip
  trigger={<button>Info</button>}
  content={<p>Details</p>}
  className="max-w-xs" // Limit width
/>

<SmartTooltip
  trigger={<button>Info</button>}
  content={<LargeContent />}
  className="max-w-2xl p-6" // Large tooltip
/>
```

**Custom Colors**:
```tsx
// Light tooltip
<SmartTooltip
  trigger={<button>Info</button>}
  content={<p>Details</p>}
  className="bg-white text-gray-900 border-gray-200"
/>

// Colored tooltip (warning)
<SmartTooltip
  trigger={<button>Warning</button>}
  content={<p>Important notice</p>}
  className="bg-yellow-100 text-yellow-900 border-yellow-300"
/>

// Glassmorphism tooltip
<SmartTooltip
  trigger={<button>Info</button>}
  content={<p>Details</p>}
  className="bg-white/70 backdrop-blur-md border-white/20"
/>
```

**Custom Arrow**:
```css
/* Customize arrow (inside tooltip content) */
.tooltip-content::before {
  content: '';
  position: absolute;
  bottom: -8px;
  left: 50%;
  transform: translateX(-50%);
  width: 0;
  height: 0;
  border-left: 8px solid transparent;
  border-right: 8px solid transparent;
  border-top: 8px solid hsl(var(--popover));
}
```

---

### Tooltip Animation Customization

**Custom Entrance**:
```tsx
// Slide in from top
<div className="animate-in slide-in-from-top-4 duration-300" />

// Bounce entrance
<div className="animate-in zoom-in-95 duration-200 animate-bounce" />

// Fade + scale
<div className="animate-in fade-in-0 zoom-in-90 duration-500" />
```

**Exit Animations**:
```tsx
const [isOpen, setIsOpen] = useState(false);
const [isExiting, setIsExiting] = useState(false);

const handleClose = () => {
  setIsExiting(true);
  setTimeout(() => {
    setIsOpen(false);
    setIsExiting(false);
  }, 200); // Match animation duration
};

return (
  <>
    {isOpen && (
      <div className={cn('tooltip', isExiting && 'animate-out fade-out-0 zoom-out-95')}>
        {content}
      </div>
    )}
  </>
);
```

---

## Agent Metadata Styling

### Status Badge Customization

**Custom Status Colors**:
```typescript
// Override default config
const CUSTOM_STATUS_CONFIG = {
  [AgentStatus.Active]: { color: 'hsl(120 100% 40%)', icon: '✓', label: 'Running' },
  [AgentStatus.Idle]: { color: 'hsl(0 0% 60%)', icon: '-', label: 'Standby' },
  [AgentStatus.Training]: { color: 'hsl(30 100% 50%)', icon: '⟳', label: 'Learning' },
  [AgentStatus.Error]: { color: 'hsl(0 100% 50%)', icon: '!', label: 'Failed' }
};

<AgentStatusBadge
  status={agent.status}
  customConfig={CUSTOM_STATUS_CONFIG}
/>
```

**Status Badge Sizes**:
```tsx
// Small badge
<AgentStatusBadge
  status={status}
  className="text-[10px] px-1.5 py-0.5"
/>

// Large badge
<AgentStatusBadge
  status={status}
  className="text-sm px-3 py-1"
/>
```

**Animated Status**:
```tsx
// Pulsing animation for active agents
<AgentStatusBadge
  status={AgentStatus.Active}
  className={status === AgentStatus.Active ? 'animate-pulse' : ''}
/>

// Custom pulse CSS
@keyframes status-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.agent-status-active {
  animation: status-pulse 2s ease-in-out infinite;
}
```

---

### Model Info Customization

**Compact Model Display**:
```tsx
<AgentModelInfo
  model={agent.model}
  variant="inline"
  className="text-xs text-muted-foreground"
/>
// Renders: GPT-4o-mini (inline text, not badge)
```

**Detailed Model Tooltip**:
```tsx
// Custom tooltip content
<Tooltip>
  <TooltipTrigger>
    <Brain className="w-4 h-4" />
  </TooltipTrigger>
  <TooltipContent className="max-w-sm">
    <h4 className="font-semibold">{model.name}</h4>

    <dl className="grid grid-cols-2 gap-2 mt-2 text-xs">
      <dt>Temperature:</dt>
      <dd className="font-mono">{model.temperature}</dd>

      <dt>Max Tokens:</dt>
      <dd className="font-mono">{model.maxTokens.toLocaleString()}</dd>

      <dt>Top P:</dt>
      <dd className="font-mono">{model.topP ?? 1.0}</dd>

      <dt>Frequency Penalty:</dt>
      <dd className="font-mono">{model.frequencyPenalty ?? 0}</dd>
    </dl>

    {model.systemPrompt && (
      <details className="mt-2">
        <summary className="cursor-pointer text-xs">System Prompt</summary>
        <pre className="text-[10px] bg-muted p-2 rounded mt-1 overflow-auto max-h-32">
          {model.systemPrompt}
        </pre>
      </details>
    )}
  </TooltipContent>
</Tooltip>
```

---

## Responsive Design Patterns

### Pattern 28: Breakpoint-Aware Components

```typescript
// Hook: useResponsiveVariant
export function useResponsiveVariant() {
  const [variant, setVariant] = useState<'desktop' | 'tablet' | 'mobile'>('desktop');

  useEffect(() => {
    const updateVariant = () => {
      if (window.innerWidth < 768) {
        setVariant('mobile');
      } else if (window.innerWidth < 1024) {
        setVariant('tablet');
      } else {
        setVariant('desktop');
      }
    };

    updateVariant();
    window.addEventListener('resize', updateVariant);
    return () => window.removeEventListener('resize', updateVariant);
  }, []);

  return variant;
}

// Usage
function ResponsiveGameCard({ game }: Props) {
  const variant = useResponsiveVariant();

  return (
    <MeepleCard
      entity="game"
      title={game.title}
      tags={createTagsFromKeys('game', game.tagKeys)}
      maxVisibleTags={variant === 'mobile' ? 2 : 3} // Fewer tags on mobile
    >
      {/* Tag strip auto-adjusts via variant */}
    </MeepleCard>
  );
}
```

---

### Pattern 29: Container Query Support

```css
/* Use container queries for tag strip (future CSS feature) */
@container (max-width: 300px) {
  [aria-label="Entity tags"] {
    width: 20px; /* Extra narrow for small containers */
  }

  [role="status"] {
    font-size: 8px; /* Smaller text */
  }
}
```

```tsx
// Enable container queries in Tailwind
// tailwind.config.ts
export default {
  theme: {
    extend: {
      containers: {
        card: '300px'
      }
    }
  }
};

// Usage
<div className="@container">
  <MeepleCard
    entity="game"
    tags={tags}
    className="@[200px]:w-full @[300px]:w-1/2"
  />
</div>
```

---

## Dark Mode Styling

### Tag Colors in Dark Mode

```css
/* globals.css */
:root {
  /* Light mode tag colors */
  --tag-new: 142 76% 36%;
  --tag-sale: 0 84% 60%;
}

.dark {
  /* Dark mode: Increase lightness for visibility */
  --tag-new: 142 76% 55%;
  --tag-sale: 0 84% 75%;
}
```

```typescript
// Use CSS variables in tag presets
export const GAME_TAG_PRESETS = {
  new: { bgColor: 'hsl(var(--tag-new))', color: 'hsl(0 0% 100%)' },
  sale: { bgColor: 'hsl(var(--tag-sale))', color: 'hsl(0 0% 100%)' }
};
```

### Tooltip Dark Mode

```tsx
// Auto-adapts via Tailwind's dark: prefix
const tooltipClasses = cn(
  'bg-popover text-popover-foreground', // Uses theme colors
  'border-border', // Theme-aware border
  'dark:bg-gray-800 dark:text-gray-100' // Explicit dark mode (if needed)
);
```

---

## Accessibility-Focused Styling

### High Contrast Mode

```css
/* Ensure visibility in Windows High Contrast */
@media (prefers-contrast: high) {
  [aria-label="Entity tags"] {
    background: CanvasText; /* System color */
    border: 2px solid WindowText;
  }

  [role="status"] {
    background: Highlight;
    color: HighlightText;
    border: 1px solid WindowText;
  }
}
```

### Focus Indicators

```css
/* Enhanced focus ring for better visibility */
.focus-ring {
  @apply focus-visible:outline-none;
  @apply focus-visible:ring-2;
  @apply focus-visible:ring-ring;
  @apply focus-visible:ring-offset-2;
  @apply focus-visible:ring-offset-background;
}

/* High visibility focus (WCAG AAA) */
.focus-ring-high-contrast {
  @apply focus-visible:ring-4;
  @apply focus-visible:ring-primary;
  @apply focus-visible:ring-offset-4;
}
```

### Reduced Motion

```css
/* Respect prefers-reduced-motion */
@media (prefers-reduced-motion: reduce) {
  [class*="animate-in"] {
    animation: none !important;
  }

  .tag-strip [style*="animation-delay"] {
    animation-delay: 0s !important; /* No stagger */
  }

  [class*="transition"] {
    transition: none !important;
  }
}
```

```typescript
// JavaScript: Detect reduced motion preference
const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

<TagStrip
  tags={tags}
  className={prefersReducedMotion ? 'no-animations' : ''}
/>
```

---

## Collection Limit UI Styling

### Progress Bar Customization

**Custom Colors**:
```tsx
function CollectionLimitIndicator({ gameCount, storageMB }: Props) {
  const percent = (gameCount / limits.maxGames) * 100;

  // Custom color thresholds
  const getColor = (p: number) => {
    if (p >= 95) return 'bg-red-600';
    if (p >= 85) return 'bg-orange-500';
    if (p >= 70) return 'bg-yellow-500';
    return 'bg-emerald-500';
  };

  return <Progress value={percent} className={getColor(percent)} />;
}
```

**Gradient Progress Bar**:
```css
/* Progress bar with gradient */
.progress-bar-gradient {
  background: linear-gradient(to right,
    hsl(142 76% 36%) 0%,   /* Green start */
    hsl(38 92% 50%) 75%,   /* Yellow middle */
    hsl(0 84% 60%) 100%    /* Red end */
  );
}
```

```tsx
<Progress
  value={percent}
  className="progress-bar-gradient"
/>
```

**Animated Fill**:
```css
@keyframes fill-progress {
  from { width: 0%; }
  to { width: var(--progress-value); }
}

.progress-fill {
  animation: fill-progress 1s ease-out;
  width: var(--progress-value);
}
```

```tsx
<div className="h-2 bg-gray-200 rounded-full overflow-hidden">
  <div
    className="progress-fill h-full bg-green-500"
    style={{ '--progress-value': `${percent}%` } as React.CSSProperties}
  />
</div>
```

---

## MeepleCard Integration Styling

### Permission-Locked Overlay

```tsx
// Dim card if feature locked
function PermissionAwareMeepleCard({ game, feature }: Props) {
  const { canAccess } = usePermissions();
  const hasAccess = canAccess(feature);

  return (
    <div className="relative">
      <MeepleCard
        entity="game"
        title={game.title}
        className={cn(!hasAccess && 'opacity-50 grayscale')}
      />

      {!hasAccess && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="text-center text-white">
            <Lock className="mx-auto mb-2" />
            <p className="font-semibold">Pro Feature</p>
            <Button variant="secondary" size="sm">Upgrade</Button>
          </div>
        </div>
      )}
    </div>
  );
}
```

---

## Tailwind Config Extensions

### Custom Tag Utilities

```javascript
// tailwind.config.ts
export default {
  theme: {
    extend: {
      spacing: {
        tag: '32px', // Tag strip width
        'tag-sm': '24px',
        'tag-lg': '40px'
      },

      zIndex: {
        tag: '10',
        tooltip: '50'
      },

      backdropBlur: {
        tag: '4px',
        tooltip: '12px'
      },

      colors: {
        tag: {
          new: 'hsl(142 76% 36%)',
          sale: 'hsl(0 84% 60%)',
          owned: 'hsl(221 83% 53%)',
          wishlisted: 'hsl(350 89% 60%)'
        },
        agent: {
          active: 'hsl(142 76% 36%)',
          idle: 'hsl(0 0% 50%)',
          training: 'hsl(38 92% 50%)',
          error: 'hsl(0 84% 60%)'
        }
      }
    }
  },

  plugins: [
    // Custom tag plugin
    function({ addComponents }) {
      addComponents({
        '.tag-strip': {
          position: 'absolute',
          top: 0,
          bottom: 0,
          width: '32px',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: '4px',
          padding: '4px',
          background: 'linear-gradient(to bottom, hsl(0 0% 0% / 0.2), hsl(0 0% 0% / 0.05))',
          backdropFilter: 'blur(4px)',
          borderRight: '1px solid hsl(0 0% 100% / 0.1)',
          zIndex: 10
        },

        '.tag-badge': {
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          borderRadius: '4px',
          padding: '4px 6px',
          fontSize: '10px',
          fontWeight: 600
        }
      });
    }
  ]
};
```

**Usage**:
```tsx
<div className="tag-strip">
  <div className="tag-badge bg-tag-new text-white">New</div>
  <div className="tag-badge bg-tag-sale text-white">Sale</div>
</div>
```

---

## Storybook Styling

### Story Decorators for Visual Context

```typescript
// TagStrip.stories.tsx
export const Default: Story = {
  args: { tags: mockTags, maxVisible: 3 },

  decorators: [
    (Story) => (
      <div className="relative w-64 h-96 bg-gradient-to-br from-slate-100 to-slate-200 dark:from-slate-800 dark:to-slate-900 rounded-2xl overflow-hidden">
        <Story />
      </div>
    )
  ]
};

// Multiple contexts
export const OnLightBackground: Story = {
  args: { tags: mockTags },
  decorators: [(Story) => <div className="bg-white p-8"><Story /></div>]
};

export const OnDarkBackground: Story = {
  args: { tags: mockTags },
  decorators: [(Story) => <div className="bg-gray-900 p-8"><Story /></div>]
};

export const OnImageBackground: Story = {
  args: { tags: mockTags },
  decorators: [(Story) => (
    <div className="relative h-96" style={{ backgroundImage: 'url(/game-background.jpg)' }}>
      <Story />
    </div>
  )]
};
```

---

### Storybook Controls for Live Customization

```typescript
// AgentStatusBadge.stories.tsx
export default {
  title: 'UI/Agent/AgentStatusBadge',
  component: AgentStatusBadge,
  argTypes: {
    status: {
      control: 'select',
      options: Object.values(AgentStatus)
    },
    showLabel: {
      control: 'boolean'
    },
    showTooltip: {
      control: 'boolean'
    },
    className: {
      control: 'text'
    }
  }
} satisfies Meta<typeof AgentStatusBadge>;

// Users can toggle showLabel, change status, etc. in Storybook UI
```

---

## CSS-in-JS Patterns

### Styled Components (if using)

```typescript
import styled from 'styled-components';

const StyledTagStrip = styled.div<{ $position: 'left' | 'right'; $width: number }>`
  position: absolute;
  top: 0;
  bottom: 0;
  ${props => props.$position}: 0;
  width: ${props => props.$width}px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 4px;
  background: linear-gradient(to bottom, rgba(0, 0, 0, 0.2), rgba(0, 0, 0, 0.05));
  backdrop-filter: blur(4px);
  border-${props => props.$position === 'left' ? 'right' : 'left'}: 1px solid rgba(255, 255, 255, 0.1);
  z-index: 10;
`;

// Usage
<StyledTagStrip $position="left" $width={32}>
  {tags.map(tag => <TagBadge key={tag.id} tag={tag} />)}
</StyledTagStrip>
```

---

### Emotion (if using)

```typescript
import { css } from '@emotion/react';

const tagStripStyles = css`
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  width: 32px;
  /* ... */

  @media (max-width: 768px) {
    width: 24px;
  }
`;

<div css={tagStripStyles}>
  <TagStrip tags={tags} />
</div>
```

---

## Print Styles

### Print-Friendly Tags

```css
@media print {
  /* Hide decorative elements */
  [aria-label="Entity tags"] {
    display: none;
  }

  /* Or show simplified version */
  [role="status"] {
    border: 1px solid black;
    background: white !important;
    color: black !important;
    print-color-adjust: exact; /* Force colors in print */
  }

  /* Remove animations */
  * {
    animation: none !important;
    transition: none !important;
  }
}
```

---

## Performance-Optimized Styling

### GPU-Accelerated Animations

```css
/* Use transform/opacity (GPU) instead of top/left (CPU) */

/* ✅ FAST: GPU-accelerated */
.tag-badge-enter {
  transform: translateX(-10px);
  opacity: 0;
  animation: slide-in 300ms ease-out forwards;
}

@keyframes slide-in {
  to {
    transform: translateX(0);
    opacity: 1;
  }
}

/* ❌ SLOW: CPU-bound (triggers layout) */
.tag-badge-enter {
  left: -10px;
  animation: slide-in-slow 300ms ease-out forwards;
}

@keyframes slide-in-slow {
  to { left: 0; }
}
```

---

### will-change for Animation Performance

```css
/* Hint browser to optimize for animation */
.tag-strip {
  will-change: transform;
}

.tooltip {
  will-change: opacity, transform;
}

/* Remove after animation completes */
.tag-strip.animation-complete {
  will-change: auto;
}
```

---

## Utility Class Library

### Custom Tailwind Utilities

```javascript
// tailwind.config.ts
export default {
  plugins: [
    function({ addUtilities }) {
      addUtilities({
        '.tag-strip-left': {
          position: 'absolute',
          left: 0,
          top: 0,
          bottom: 0,
          width: '32px'
        },

        '.tag-strip-right': {
          position: 'absolute',
          right: 0,
          top: 0,
          bottom: 0,
          width: '32px'
        },

        '.tag-badge-sm': {
          padding: '2px 6px',
          fontSize: '9px'
        },

        '.tag-badge-md': {
          padding: '4px 8px',
          fontSize: '10px'
        },

        '.tag-badge-lg': {
          padding: '6px 10px',
          fontSize: '11px'
        }
      });
    }
  ]
};
```

**Usage**:
```tsx
<div className="tag-strip-left">
  <div className="tag-badge-md">New</div>
</div>
```

---

## Real-World Styling Examples

### Example 1: E-Commerce Product Card

```tsx
function ProductCard({ product }: Props) {
  const tags = [
    product.isNew && { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    product.discountPercent > 0 && {
      id: 'sale',
      label: `-${product.discountPercent}%`,
      icon: Tag,
      bgColor: 'hsl(0 84% 60%)'
    },
    product.stock < 10 && {
      id: 'low-stock',
      label: `Only ${product.stock} left`,
      icon: AlertCircle,
      bgColor: 'hsl(38 92% 50%)'
    }
  ].filter(Boolean);

  return (
    <MeepleCard
      entity="game"
      title={product.name}
      subtitle={product.brand}
      imageUrl={product.image}
      tags={tags}
      maxVisibleTags={2}
      badge={product.isBestseller ? 'Bestseller' : undefined}
      className="hover:shadow-2xl transition-shadow"
    />
  );
}
```

---

### Example 2: Dashboard Widget with Compact Tags

```tsx
function DashboardAgentWidget({ agent }: Props) {
  return (
    <MeepleCard
      entity="agent"
      variant="compact"
      title={agent.name}
      agentMetadata={{
        status: agent.status,
        model: { name: agent.modelName },
        invocationCount: agent.invocationCount
      }}
      tags={createTagsFromKeys('agent', agent.capabilities.slice(0, 2))}
      maxVisibleTags={2}
      className="min-w-[280px]" // Minimum width for compact variant
    />
  );
}
```

---

### Example 3: Mobile-Optimized Card Grid

```tsx
function MobileGameGrid({ games }: Props) {
  return (
    <div className="grid grid-cols-2 gap-2 p-2">
      {games.map(game => (
        <MeepleCard
          key={game.id}
          entity="game"
          variant="grid"
          title={game.title}
          imageUrl={game.image}
          tags={createTagsFromKeys('game', game.tags)}
          maxVisibleTags={2} // Only 2 on mobile
          className={cn(
            'text-xs', // Smaller text on mobile
            'min-h-[180px]' // Minimum height
          )}
        />
      ))}
    </div>
  );
}
```

---

## CSS Custom Properties API

### Expose Customization via CSS Variables

```typescript
// Allow consumers to customize via CSS variables
<TagStrip
  tags={tags}
  style={{
    '--tag-strip-width': '40px',
    '--tag-strip-bg-start': 'hsl(0 0% 0% / 0.3)',
    '--tag-strip-bg-end': 'hsl(0 0% 0% / 0.1)',
    '--tag-gap': '6px'
  } as React.CSSProperties}
/>
```

```css
/* Component uses CSS variables */
.tag-strip {
  width: var(--tag-strip-width, 32px);
  background: linear-gradient(
    to bottom,
    var(--tag-strip-bg-start, hsl(0 0% 0% / 0.2)),
    var(--tag-strip-bg-end, hsl(0 0% 0% / 0.05))
  );
  gap: var(--tag-gap, 4px);
}
```

---

## Summary: Styling Checklist

**Tag System**:
- [ ] Custom tag strip width via `className` or CSS var
- [ ] Theme-specific colors via CSS variables
- [ ] Responsive variants (desktop/tablet/mobile)
- [ ] Dark mode adjustments (increase lightness)
- [ ] High contrast mode support
- [ ] Reduced motion preference respected

**Tooltips**:
- [ ] Custom size/color via `className`
- [ ] Theme-aware (bg-popover, text-popover-foreground)
- [ ] Animation customization (entrance/exit)
- [ ] Focus indicators (focus-visible:ring-2)

**Agent Metadata**:
- [ ] Status badge sizes (sm/md/lg)
- [ ] Animated active status (optional pulse)
- [ ] Dark mode status colors
- [ ] Compact vs detailed model info

**Collection Limits**:
- [ ] Progress bar colors (green/yellow/red thresholds)
- [ ] Gradient progress option
- [ ] Animated fill (optional)
- [ ] Warning icon styling

**Accessibility**:
- [ ] Focus rings (2px minimum, high contrast)
- [ ] Color contrast ≥4.5:1 (WCAG AA)
- [ ] High contrast mode (system colors)
- [ ] Reduced motion (disable animations)

**Performance**:
- [ ] GPU-accelerated animations (transform/opacity)
- [ ] will-change hints (sparingly)
- [ ] Avoid layout-triggering properties in animations
- [ ] CSS containment (contain: layout style)

---

## References

- Tailwind CSS: https://tailwindcss.com/docs
- CSS Variables: https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties
- Animations: https://animate.style/
- Accessibility Colors: https://webaim.org/resources/contrastchecker/
- Dark Mode: https://tailwindcss.com/docs/dark-mode
