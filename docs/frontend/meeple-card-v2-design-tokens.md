# MeepleCard v2 Design Tokens

**Epic #4604** | **Created**: 2026-02-17

Comprehensive design token reference for MeepleCard v2 visual redesign.

## Color System

### Entity Colors (HSL)

All 7 entity types use consistent HSL color format for easy theme customization:

```css
/* CSS Variables (design-tokens.css) */
--color-entity-game: 25 95% 45%;      /* Orange */
--color-entity-player: 262 83% 58%;   /* Purple */
--color-entity-session: 240 60% 55%;  /* Indigo */
--color-entity-agent: 38 92% 50%;     /* Amber */
--color-entity-document: 210 40% 55%; /* Slate */
--color-entity-chat: 220 80% 55%;     /* Blue */
--color-entity-event: 350 89% 60%;    /* Rose */
```

**Usage Examples**:
- **Border accent**: Left edge 4px → 6px on hover
- **Entity badge**: Top-left pill with entity name
- **Glow rings**: `outline-2` with `hsla(var(--color-entity-game), 0.4)`
- **Gradient overlays**: Hero/featured variants use entity color gradients
- **Flip card header**: Entity-colored header with diagonal stripe pattern

### Shadows - Warm Toned

v2 shadows use warm brown tones instead of neutral grays for a premium, organic feel:

```css
/* Light Mode */
--shadow-warm-sm: 0 1px 3px rgba(180,130,80,0.06), 0 1px 2px rgba(180,130,80,0.04);
--shadow-warm-md: 0 4px 12px rgba(180,130,80,0.08), 0 2px 4px rgba(180,130,80,0.04);
--shadow-warm-lg: 0 10px 30px rgba(180,130,80,0.12), 0 4px 8px rgba(180,130,80,0.06);
--shadow-warm-xl: 0 20px 50px rgba(180,130,80,0.16), 0 8px 16px rgba(180,130,80,0.08);
--shadow-warm-2xl: 0 25px 60px rgba(180,130,80,0.2), 0 12px 24px rgba(180,130,80,0.1);

/* Dark Mode */
--shadow-warm-sm: 0 1px 3px rgb(0 0 0 / 0.2);
--shadow-warm-md: 0 4px 12px rgb(0 0 0 / 0.3);
--shadow-warm-lg: 0 10px 30px rgb(0 0 0 / 0.4);
--shadow-warm-xl: 0 20px 50px rgb(0 0 0 / 0.5);
--shadow-warm-2xl: 0 25px 60px rgb(0 0 0 / 0.6);
```

**Application**:
- Grid: `shadow-warm-sm` → `shadow-warm-xl` on hover
- Featured: `shadow-warm-md` → `shadow-warm-xl` on hover
- Hero: `shadow-warm-xl` → `shadow-warm-2xl` on hover
- Carousel center: `shadow-warm-2xl` always

## Typography

### Font Families

```css
--font-heading: var(--font-quicksand);  /* Quicksand 500-700 */
--font-body: var(--font-nunito);        /* Nunito 400-800 */
```

**Usage**:
- **Card titles**: `font-quicksand font-bold` (700 weight)
- **Body text**: `font-nunito` (400-600 weight)
- **Entity badges**: `font-quicksand font-bold text-[10px] uppercase tracking-wider`
- **Metadata**: `font-nunito font-semibold text-xs`

### Text Shadows (Hero Variant)

```css
/* Hero title */
text-shadow: 0 2px 8px rgba(0,0,0,0.3);
```

## Layout & Spacing

### Border Radius

```css
--radius: 16px;     /* Cards */
--radius-sm: 12px;  /* Smaller elements */
--radius-xs: 8px;   /* Chips, badges */
```

### Card Padding

| Variant | Content Padding | Notes |
|---------|----------------|-------|
| Grid | `p-4` (1rem) | Compact vertical |
| List | `p-3` (0.75rem) | Horizontal layout |
| Compact | `p-2` (0.5rem) | Minimal |
| Featured | `px-5 py-4` | More spacious |
| Hero | `p-6` (1.5rem) | Maximum breathing room |

### Aspect Ratios

| Variant | Cover Aspect | Rationale |
|---------|-------------|-----------|
| Grid | `7/10` | Board game card proportions |
| List | `64x64px` | Square thumbnail |
| Compact | `40x40px` | Minimal square |
| Featured | `16/9` | Widescreen promo |
| Hero | `absolute inset-0` | Full background |

## Visual Effects

### Glassmorphism

```css
/* Card background */
background: rgba(255, 255, 255, 0.9);
backdrop-filter: blur(12px) saturate(180%);

/* Dark mode */
background: rgba(30, 25, 20, 0.7);
```

**Quick Action Buttons**:
```css
background: rgba(255, 255, 255, 0.85);
backdrop-filter: blur(8px);
border: 1px solid rgba(255, 255, 255, 0.6);
```

### Hover Transforms

| Element | Default | Hover | Transition |
|---------|---------|-------|------------|
| Grid card | `translate-y-0` | `-translate-y-1.5` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Featured card | `translate-y-0` | `-translate-y-2` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Hero card | `scale-1` | `scale-[1.01]` | `350ms cubic-bezier(0.4,0,0.2,1)` |
| Cover image | `scale-100` | `scale-105` | `500ms` |
| Quick action | `scale-100` | `scale-110` | `300ms ease-out` |

### Entity Glow Rings

```css
/* Applied on hover to all non-compact variants */
outline: 2px solid;
outline-offset: 2px;
outline-color: hsla(var(--color-entity-TYPE), 0.4);
```

Example for game entity:
```css
outline-color: hsla(25, 95%, 45%, 0.4);  /* Orange @ 40% */
```

### Shimmer Effect

Applied to cover images on hover:

```css
@keyframes mc-shimmer {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}

/* Overlay */
background: linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%);
animation: mc-shimmer 0.8s ease-out forwards;
```

## Animations

### Tag Pulse (for "new" tags)

```css
@keyframes mc-badge-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.08); }
}

animation: mc-badge-pulse 2s ease-in-out infinite;
```

### Quick Actions Float-Up

Staggered animation for quick action buttons reveal:

```css
@keyframes mc-float-up {
  0% { opacity: 0; transform: translateY(12px); }
  100% { opacity: 1; transform: translateY(0); }
}

/* Applied with delays: 80ms, 40ms, 0ms (reverse order) */
animation: mc-float-up 0.35s ease-out both;
```

## 3D Card Flip

### Flip Container

```css
perspective: 1200px;
cursor: pointer;
```

### Flip Animation

```tsx
// Framer Motion
animate={{ rotateY: flipped ? 180 : 0 }}
transition={{ duration: 0.7, ease: [0.4, 0, 0.2, 1] }}
```

### Card Faces

```css
/* Front & Back */
backface-visibility: hidden;
-webkit-backface-visibility: hidden;
transform-style: preserve-3d;

/* Back */
transform: rotateY(180deg);
box-shadow: var(--shadow-warm-lg);
border: 1px solid hsla(ENTITY_COLOR, 0.15);
```

### Card Back Header

Entity-colored header with diagonal stripe pattern:

```tsx
<div
  style={{ backgroundColor: `hsl(${entityColor})` }}
  className="relative overflow-hidden px-5 pb-3 pt-5"
>
  {/* Pattern overlay */}
  <div
    className="absolute inset-0 opacity-[0.12]"
    style={{
      backgroundImage: 'repeating-linear-gradient(45deg, transparent, transparent 10px, currentColor 10px, currentColor 11px)',
    }}
  />
  <h2 className="relative z-[1] font-quicksand text-lg font-bold text-white">
    {title}
  </h2>
</div>
```

## Carousel Scaling

| Position | Scale | Opacity | Blur | Notes |
|----------|-------|---------|------|-------|
| Center (focus) | `1.1` | `1.0` | `0px` | Enhanced prominence |
| Side (±1) | `0.85` | `0.6` | `2px` | Fade background |
| Far (±2+) | `0.5` | `0` | `0px` | Hidden |

**Center Card Enhancements**:
- Shadow: `var(--shadow-warm-2xl)`
- Quick actions: Always visible (not just hover)
- Info button: Always visible
- Entity glow ring: Inherited from card hover

## Responsive Behavior

All visual effects respect `prefers-reduced-motion`:

```css
@media (prefers-reduced-motion: reduce) {
  .animate-mc-shimmer,
  .animate-mc-float-up,
  .animate-mc-badge-pulse {
    animation: none !important;
  }

  .hover-card:hover {
    transform: none !important;
  }
}
```

## Dark Mode Specifics

Dark mode automatically adjusts:
- Shadows: Neutral black instead of warm brown
- Glassmorphism: `rgba(30, 25, 20, 0.7)` backdrop
- Entity colors: Same HSL values (work in both modes)
- Text: Uses semantic `--card-foreground` token
- Borders: Uses semantic `--border` token

## Migration Notes

**Breaking Changes**: None - all changes are visual enhancements.

**New Props**:
- FlipCard: `entityColor`, `entityName`, `title` (optional)
- TagBadge: `animated` (optional, auto-enabled for "new" tags)

**Deprecated**: None

**Consumer Site Impact**: Zero code changes required, visual improvements apply automatically.

## References

- **Mockup**: `apps/web/src/components/ui/meeple-card-v2-mockup.html`
- **PR**: #4619
- **Epic**: #4604
- **Design System**: `apps/web/src/styles/design-tokens.css`
