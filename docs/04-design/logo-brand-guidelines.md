# MeepleAI Logo - Brand Guidelines

**Version**: 1.0
**Date**: 2025-12-19
**Status**: Production Ready

---

## 📦 Logo Assets

### File Locations
```
apps/web/public/logo/
├── meepleai-logo-icon.svg           # Icon only (100x100)
├── meepleai-logo-full.svg           # Icon + wordmark (300x100)
├── meepleai-logo-monochrome.svg     # B&W version (100x100)
└── meepleai-logo-white.svg          # White for dark BG (100x100)
```

### Usage by Context

| Context | File | Notes |
|---------|------|-------|
| **Favicon** | `meepleai-logo-icon.svg` | 16x16, 32x32, 48x48 |
| **Navbar** | `meepleai-logo-full.svg` | With wordmark |
| **Social Media** | `meepleai-logo-icon.svg` | Square profile pics |
| **Hero Section** | `meepleai-logo-full.svg` | Large display |
| **Print/Merch** | `meepleai-logo-monochrome.svg` | Single color |
| **Dark Mode** | `meepleai-logo-white.svg` | Inverted colors |

---

## 🎨 Color Palette

### Primary Gradient
```css
/* Blue to Purple Gradient */
--logo-gradient-start: #3B82F6;  /* Blue-500 */
--logo-gradient-end: #8B5CF6;    /* Purple-500 */

/* CSS Usage */
background: linear-gradient(135deg, #3B82F6, #8B5CF6);
```

### Solid Colors (quando gradient non disponibile)
```css
--logo-primary: #3B82F6;     /* Blue-500 */
--logo-secondary: #8B5CF6;   /* Purple-500 */
--logo-accent: #10B981;      /* Green-500 (AI dot) */
```

### Monochrome
```css
--logo-dark: #1e293b;        /* Slate-900 */
--logo-white: #ffffff;       /* White */
```

---

## 📏 Clear Space & Sizing

### Minimum Clear Space
**Rule**: Mantenere spazio libero minimo pari all'altezza della testa del meeple (12px alla scala 100x100).

```
┌─────────────────────┐
│       [12px]        │  ← Clear space top
│   ┌───────────┐     │
│   │  MEEPLE   │     │
│   └───────────┘     │
│       [12px]        │  ← Clear space bottom
└─────────────────────┘
  [12px]        [12px]
  ↑             ↑
  Clear L       Clear R
```

### Minimum Sizes

| Format | Min Width | Min Height | Notes |
|--------|-----------|------------|-------|
| **Digital** | 32px | 32px | Below this, details lost |
| **Print** | 0.5 inch | 0.5 inch | ~36pt at 72dpi |
| **Favicon** | 16px | 16px | Simplified if needed |

### Recommended Sizes

| Context | Size | Format |
|---------|------|--------|
| Favicon | 16x16, 32x32, 48x48 | PNG/ICO |
| Mobile App Icon | 512x512 | PNG |
| Navbar | 64px height | SVG |
| Hero Section | 128-256px | SVG |
| Social Media Profile | 400x400 | PNG |
| Open Graph | 1200x630 | PNG |

---

## ✅ Do's

### Correct Usage
1. ✅ **Use provided SVG files** for all digital applications
2. ✅ **Maintain aspect ratio** when scaling
3. ✅ **Use gradient version** as primary logo
4. ✅ **Use monochrome** when color not available
5. ✅ **Use white version** on dark backgrounds (#0F172A or darker)
6. ✅ **Respect clear space** requirements
7. ✅ **Center align** logo in containers
8. ✅ **Use AI accent dot** (white/blue) for brand consistency

### Examples
```html
<!-- ✅ Correct: Navbar usage -->
<img src="/logo/meepleai-logo-full.svg" alt="MeepleAI" height="64" />

<!-- ✅ Correct: Favicon -->
<link rel="icon" href="/logo/meepleai-logo-icon.svg" />

<!-- ✅ Correct: Dark background -->
<div style="background: #0F172A;">
  <img src="/logo/meepleai-logo-white.svg" alt="MeepleAI" />
</div>
```

---

## ❌ Don'ts

### Incorrect Usage
1. ❌ **DO NOT stretch** or distort proportions
2. ❌ **DO NOT rotate** the logo
3. ❌ **DO NOT change colors** (except approved variants)
4. ❌ **DO NOT add effects** (shadows, glows, 3D) unless specified
5. ❌ **DO NOT place on busy backgrounds** without clear space
6. ❌ **DO NOT outline** or add borders
7. ❌ **DO NOT rearrange** icon and wordmark
8. ❌ **DO NOT remove AI accent dot**
9. ❌ **DO NOT use low-resolution** raster versions when SVG available
10. ❌ **DO NOT combine** with other logos without approval

### Examples
```html
<!-- ❌ Wrong: Distorted aspect ratio -->
<img src="/logo/meepleai-logo-icon.svg" width="200" height="50" />

<!-- ❌ Wrong: Added effects -->
<img src="/logo/meepleai-logo-icon.svg" style="filter: drop-shadow(0 0 10px red);" />

<!-- ❌ Wrong: Busy background without clear space -->
<div style="background: url(pattern.jpg);">
  <img src="/logo/meepleai-logo-icon.svg" />
</div>
```

---

## 🎭 Logo Variants

### When to Use Each Variant

**Icon Only (`meepleai-logo-icon.svg`)**
- Favicon
- App icons
- Social media avatars
- Small UI elements (<100px)
- Square contexts

**Full Logo (`meepleai-logo-full.svg`)**
- Primary brand usage
- Navbar
- Hero sections
- Marketing materials
- Horizontal layouts

**Monochrome (`meepleai-logo-monochrome.svg`)**
- Print materials
- Merchandise (t-shirts, mugs)
- Embroidery
- Stamps/seals
- Single-color contexts

**White (`meepleai-logo-white.svg`)**
- Dark mode UI
- Dark backgrounds (#0F172A, #1e293b)
- Video overlays
- Photography with dark tones

---

## 🔤 Typography Pairing

### Primary Font: Fredoka
```css
font-family: 'Fredoka', sans-serif;
font-weight: 700; /* Bold for headings */
font-weight: 600; /* Semi-bold for subheadings */
font-weight: 400; /* Regular for body */
```

**Use for**: Headings, playful UI, friendly tone

**Fallback**: `'Outfit', 'Quicksand', sans-serif`

### Secondary Font: Outfit
```css
font-family: 'Outfit', sans-serif;
font-weight: 800; /* Extra-bold for large headings */
font-weight: 600; /* Semi-bold for UI */
font-weight: 400; /* Regular for body */
```

**Use for**: Modern contexts, tech audience

**Fallback**: `'Inter', system-ui, sans-serif`

### Logo Wordmark Specs
- **Font**: Fredoka Bold (700) or Outfit Extra-Bold (800)
- **Size**: Proportional to icon (48px when icon is 80px tall)
- **Color**: Same gradient as icon
- **Letter-spacing**: Normal (0)
- **Case**: Title Case ("MeepleAI")

---

## 📱 Responsive Behavior

### Breakpoints

**Mobile (<640px)**
```html
<!-- Use icon only to save space -->
<img src="/logo/meepleai-logo-icon.svg" height="48" />
```

**Tablet (640px - 1024px)**
```html
<!-- Full logo, medium size -->
<img src="/logo/meepleai-logo-full.svg" height="56" />
```

**Desktop (>1024px)**
```html
<!-- Full logo, standard size -->
<img src="/logo/meepleai-logo-full.svg" height="64" />
```

### CSS Example
```css
.logo {
  height: 48px;
}

@media (min-width: 640px) {
  .logo {
    height: 56px;
  }
}

@media (min-width: 1024px) {
  .logo {
    height: 64px;
  }
}
```

---

## 🌐 Web Implementation

### React Component Example
```tsx
// components/Logo.tsx
interface LogoProps {
  variant?: 'icon' | 'full' | 'monochrome' | 'white'
  size?: number
  className?: string
}

export function Logo({
  variant = 'full',
  size = 64,
  className
}: LogoProps) {
  const logoSrc = `/logo/meepleai-logo-${variant}.svg`

  return (
    <img
      src={logoSrc}
      alt="MeepleAI"
      height={size}
      className={className}
      style={{ height: size, width: 'auto' }}
    />
  )
}

// Usage
<Logo variant="full" size={64} />
<Logo variant="icon" size={32} />
```

### Next.js Image Optimization
```tsx
import Image from 'next/image'

export function Logo() {
  return (
    <Image
      src="/logo/meepleai-logo-full.svg"
      alt="MeepleAI"
      width={300}
      height={100}
      priority
    />
  )
}
```

---

## 🎨 Background Combinations

### Approved Backgrounds

**Light Backgrounds**
- ✅ White (#ffffff)
- ✅ Light Gray (#f8fafc)
- ✅ Light Blue (#f0f9ff)
- ✅ Very light gradients

**Use**: Standard color logo

**Dark Backgrounds**
- ✅ Slate-900 (#0f172a)
- ✅ Slate-800 (#1e293b)
- ✅ Black (#000000)
- ✅ Dark gradients

**Use**: White variant logo

### Background Contrast Requirements
- **Minimum contrast ratio**: 4.5:1 (WCAG AA)
- **Recommended contrast ratio**: 7:1 (WCAG AAA)
- **Test tool**: Use WebAIM Contrast Checker

---

## 🖨️ Print Specifications

### Color Print
- **Format**: Vector (SVG converted to PDF/EPS)
- **Color Mode**: CMYK conversion from RGB
- **Gradient**: Maintain smooth transition
- **Resolution**: Vector (scalable)

**CMYK Approximations**:
```
Blue (#3B82F6):   C:76 M:49 Y:0  K:0
Purple (#8B5CF6): C:45 M:64 Y:0  K:0
Green (#10B981):  C:64 M:0  Y:50 K:0
```

### Single-Color Print
- **Use**: Monochrome variant
- **Pantone**: Pantone Cool Gray 11 C (closest match to #1e293b)
- **Accent**: Pantone 3405 C (closest match to #10B981)

### Embroidery
- **Use**: Monochrome variant
- **Minimum size**: 2 inches width
- **Thread count**: Use filled stitch for all areas
- **Colors**: 2-thread (slate + green accent)

---

## 📐 Technical Specifications

### SVG Specifications
```xml
<!-- Standard viewBox -->
viewBox="0 0 100 100"  <!-- Icon -->
viewBox="0 0 300 100"  <!-- Full logo -->

<!-- Gradient definition -->
<linearGradient id="meeple-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
  <stop offset="0%" style="stop-color:#3B82F6" />
  <stop offset="100%" style="stop-color:#8B5CF6" />
</linearGradient>
```

### Accessibility
- **Alt text**: "MeepleAI" or "MeepleAI logo"
- **ARIA label**: `aria-label="MeepleAI home"`
- **Semantic HTML**: Use `<img>` with proper alt text
- **Link wrapping**: Wrap in `<a>` if clickable

```html
<a href="/" aria-label="MeepleAI home">
  <img src="/logo/meepleai-logo-full.svg" alt="MeepleAI" height="64" />
</a>
```

---

## 📋 Checklist for Designers

Before using the logo, verify:

- [ ] Using latest SVG files from `/apps/web/public/logo/`
- [ ] Correct variant for context (icon/full/monochrome/white)
- [ ] Aspect ratio maintained (not stretched/distorted)
- [ ] Minimum clear space respected (12px at 100px scale)
- [ ] Minimum size requirements met (32px digital, 0.5" print)
- [ ] Sufficient contrast with background (4.5:1 minimum)
- [ ] No unauthorized modifications (colors, effects, rotation)
- [ ] Proper alt text provided for accessibility
- [ ] SVG used for web, proper format for print

---

## 🔄 Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-19 | Initial brand guidelines release |

---

## 📞 Contact

For logo usage questions or special requests:
- **Email**: design@meepleai.com
- **Documentation**: `/docs/04-design/`

---

**Note**: These guidelines ensure consistent brand representation across all MeepleAI touchpoints. When in doubt, consult this document or contact the design team.
