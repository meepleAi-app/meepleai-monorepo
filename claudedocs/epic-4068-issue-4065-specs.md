# Issue #4065: Vertical Tag Component

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Tag System Redesign (1/2)
**Estimate**: 3-4 giorni
**Priority**: P2-Medium

---

## 📋 Acceptance Criteria

### AC1: Left Edge Vertical Strip Layout
- [ ] Tag strip posizionata lungo bordo sinistro card
- [ ] Larghezza fissa: 32px (desktop), 24px (mobile)
- [ ] Altezza: 100% della card height
- [ ] Z-index sopra image ma sotto hover overlay

### AC2: Tag Rendering (Max 3 + Counter)
- [ ] Mostra fino a 3 tag visibili
- [ ] Overflow: badge circolare "+N" per rimanenti
- [ ] Tag stacking verticale dall'alto verso il basso
- [ ] Gap tra tag: 4px

### AC3: Entity-Specific Tag Types
- [ ] Game tags: tipologia, preferiti, stato collezione
- [ ] Agent tags: capabilities (RAG, Vision, Code), status
- [ ] Document tags: format (PDF, DOCX), size, processing status
- [ ] Custom tags: user-defined con color picker

### AC4: Responsive Design
- [ ] Desktop: 32px strip, full text labels
- [ ] Tablet: 28px strip, abbreviated text
- [ ] Mobile: 24px strip, icon-only mode

---

## 🎨 Visual Mockup - Left Edge Vertical Strip

### Desktop View (Grid Variant)
```
┌─────────────────────────────────┐
│█ New    ┌─────────────────────┐ │
│█        │                     │ │
│█ Sale   │   Card Image        │ │
│█        │                     │ │
│█ +2     │                     │ │
│█        └─────────────────────┘ │
│█                                │
│█        Title: Wingspan         │
│█        Subtitle: Stonemaier    │
│█                                │
│█        [★★★★☆ 8.2]            │
└─────────────────────────────────┘
 ↑
 32px strip
```

### Mobile View (Grid Variant)
```
┌───────────────────────┐
│█  ┌─────────────────┐ │
│█  │                 │ │
│█  │  Card Image     │ │
│█  │                 │ │
│█  └─────────────────┘ │
│█                      │
│█  Title               │
│█  [★★★★☆]            │
└───────────────────────┘
 ↑
 24px strip (icon-only)
```

### List Variant with Tags
```
┌──────────────────────────────────────────┐
│█ New  [Image] Title: Wingspan           │
│█      64x64   Stonemaier Games · 2019   │
│█ Sale         [★★★★☆ 8.2] [👥 1-5]     │
└──────────────────────────────────────────┘
 ↑
 32px strip
```

### Tag Strip Anatomy
```
┌──────┐  ← 32px width
│      │
│ NEW  │  ← Tag 1 (16px height)
│      │
├──────┤  ← 4px gap
│      │
│ 🏷️  │  ← Tag 2 (icon variant)
│      │
├──────┤
│      │
│ +2   │  ← Overflow counter
│      │
└──────┘
```

---

## 🔧 Implementation

### Tag Component
```typescript
interface Tag {
  id: string;
  label: string;
  icon?: LucideIcon;
  color?: string; // HSL format
  bgColor?: string; // HSL format
}

interface TagStripProps {
  tags: Tag[];
  maxVisible?: number; // default: 3
  variant?: 'desktop' | 'tablet' | 'mobile';
  entityType: MeepleEntityType;
}

export function TagStrip({ tags, maxVisible = 3, variant = 'desktop', entityType }: TagStripProps) {
  const visibleTags = tags.slice(0, maxVisible);
  const hiddenCount = Math.max(0, tags.length - maxVisible);

  const stripWidth = {
    desktop: 'w-8', // 32px
    tablet: 'w-7',  // 28px
    mobile: 'w-6',  // 24px
  }[variant];

  return (
    <div className={cn(
      'absolute left-0 top-0 bottom-0',
      'flex flex-col items-center gap-1 p-1',
      stripWidth,
      'bg-gradient-to-b from-black/20 to-black/10',
      'backdrop-blur-sm',
      'border-r border-white/10'
    )}>
      {visibleTags.map(tag => (
        <TagBadge
          key={tag.id}
          tag={tag}
          variant={variant}
        />
      ))}

      {hiddenCount > 0 && (
        <div className={cn(
          'flex items-center justify-center',
          'w-6 h-6 rounded-full',
          'bg-white/20 backdrop-blur-sm',
          'text-xs font-semibold text-white'
        )}>
          +{hiddenCount}
        </div>
      )}
    </div>
  );
}
```

### TagBadge Component
```typescript
function TagBadge({ tag, variant }: { tag: Tag; variant: 'desktop' | 'tablet' | 'mobile' }) {
  const showText = variant !== 'mobile';
  const Icon = tag.icon;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn(
              'flex items-center justify-center',
              'rounded px-1 py-0.5',
              'text-[10px] font-medium',
              showText ? 'w-full' : 'w-6 h-6'
            )}
            style={{
              backgroundColor: tag.bgColor ?? 'hsl(0 0% 100% / 0.2)',
              color: tag.color ?? 'hsl(0 0% 100%)'
            }}
          >
            {Icon && <Icon className="w-3 h-3" />}
            {showText && <span className="truncate">{tag.label}</span>}
          </div>
        </TooltipTrigger>
        <TooltipContent side="right">
          {tag.label}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
```

### Entity-Specific Tag Presets
```typescript
const GAME_TAG_PRESETS: Record<string, Tag> = {
  new: {
    id: 'new',
    label: 'New',
    icon: Sparkles,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(142 76% 36%)'
  },
  sale: {
    id: 'sale',
    label: 'Sale',
    icon: Tag,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(0 84% 60%)'
  },
  owned: {
    id: 'owned',
    label: 'Owned',
    icon: Check,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(221 83% 53%)'
  },
  wishlisted: {
    id: 'wishlisted',
    label: 'Wishlist',
    icon: Heart,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(350 89% 60%)'
  }
};

const AGENT_TAG_PRESETS: Record<string, Tag> = {
  rag: {
    id: 'rag',
    label: 'RAG',
    icon: Brain,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(38 92% 50%)'
  },
  vision: {
    id: 'vision',
    label: 'Vision',
    icon: Eye,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(262 83% 58%)'
  },
  code: {
    id: 'code',
    label: 'Code',
    icon: Code2,
    color: 'hsl(0 0% 100%)',
    bgColor: 'hsl(210 40% 55%)'
  }
};
```

---

## ✅ Testing Checklist

### Unit Tests
- [ ] Renders max 3 visible tags
- [ ] Shows "+N" badge when tags > maxVisible
- [ ] Tag colors apply correctly
- [ ] Icon-only mode on mobile

### Visual Tests
- [ ] Tag strip aligns to left edge
- [ ] Tags stack vertically with correct gap
- [ ] Overflow counter displays correctly
- [ ] Responsive breakpoints work

### Integration Tests
- [ ] Entity-specific tags load correctly
- [ ] Custom tags with color picker
- [ ] Tooltip shows full tag label on hover

---

## 🔗 Dependencies

**Blocked by**: None (independent)
**Blocks**: #4066 (Tag Integration)

---

## 📚 Resources

- **Lucide Icons**: https://lucide.dev/
- **Tailwind Gradients**: https://tailwindcss.com/docs/gradient-color-stops
- **CSS Backdrop Filter**: https://developer.mozilla.org/en-US/docs/Web/CSS/backdrop-filter
