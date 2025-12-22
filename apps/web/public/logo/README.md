# MeepleAI Logo Assets

Production-ready SVG logo files for all contexts.

## 📦 Available Files

```
logo/
├── meepleai-logo-icon.svg           # Icon only (100x100)
├── meepleai-logo-full.svg           # Icon + wordmark (300x100)
├── meepleai-logo-monochrome.svg     # B&W version (100x100)
└── meepleai-logo-white.svg          # White for dark BG (100x100)
```

## 🚀 Quick Usage

### React/Next.js
```tsx
import Image from 'next/image'

// Full logo with wordmark
<Image src="/logo/meepleai-logo-full.svg" alt="MeepleAI" width={300} height={100} />

// Icon only
<Image src="/logo/meepleai-logo-icon.svg" alt="MeepleAI" width={64} height={64} />
```

### HTML
```html
<!-- Navbar -->
<img src="/logo/meepleai-logo-full.svg" alt="MeepleAI" height="64" />

<!-- Favicon -->
<link rel="icon" href="/logo/meepleai-logo-icon.svg" />
```

## 🎨 Design Specs

**Colors**
- Gradient: #3B82F6 → #8B5CF6
- AI Accent: White (80%) or #10B981

**Typography**
- Primary: Fredoka Bold (700)
- Alternative: Outfit Extra-Bold (800)

**Minimum Sizes**
- Digital: 32px
- Print: 0.5 inch

## 📋 Complete Guidelines

See `/docs/04-design/logo-brand-guidelines.md` for:
- Clear space requirements
- Do's and Don'ts
- Responsive behavior
- Print specifications
- Accessibility guidelines

---

**Version**: 1.0 | **Date**: 2025-12-19 | **Status**: ✅ Production Ready
