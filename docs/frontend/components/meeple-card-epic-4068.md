# MeepleCard Epic #4068 Enhancements

**Status**: Implementation In Progress
**Timeline**: 2-3 weeks (parallelized) | 5-7 weeks (sequential)

## New Features

### 1. Permission System (#4177, #4178, #4179)

**Tiers**: Free → Normal → Pro/Premium → Enterprise

**Features by Tier**:
- **Free**: Wishlist only
- **Normal**: + Drag & Drop, Collection Management, Advanced Filters
- **Pro**: + Bulk Select, Agent Creation, Analytics
- **Enterprise**: Unlimited everything

**Usage**:
```tsx
import { usePermissions } from '@/contexts/PermissionContext';

function MyComponent() {
  const { canAccess, tier } = usePermissions();

  return (
    <MeepleCard
      showWishlist={canAccess('wishlist')}
      selectable={canAccess('bulk-select')}
      draggable={canAccess('drag-drop')}
    />
  );
}
```

### 2. Vertical Tag System (#4181, #4182)

**Position**: Left edge vertical strip (32px desktop → 24px mobile)
**Max Tags**: 3 visible + "+N" overflow counter

**Usage**:
```tsx
<MeepleCard
  entity="game"
  title="Wingspan"
  tags={[
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    { id: 'sale', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)' }
  ]}
  maxVisibleTags={3}
/>
```

### 3. Smart Tooltip Positioning (#4186, #4180)

**Features**:
- Viewport boundary detection with auto-flip
- WCAG 2.1 AA compliant (keyboard nav, screen reader, mobile touch)
- Performance: <16ms calculation (60fps)

### 4. Collection Limits UI (#4183)

**Display**: Progress bars for game count & storage quota
**Colors**: Green (<75%) → Yellow (75-90%) → Red (>90%)
**Warnings**: Icon when approaching limit

### 5. Agent Metadata (#4184)

**Status Badge**: Active ● | Idle ○ | Training ◐ | Error ✕
**Model Info**: Name + parameters in tooltip
**Stats**: Invocation count, last executed, avg response time

## Implementation Progress

| Issue | Component | Status |
|-------|-----------|--------|
| #4177 | Permission Model | Backend WIP |
| #4178 | Permission Hooks | Frontend ✓ |
| #4179 | MeepleCard Perms | Pending |
| #4181 | TagStrip | ✓ |
| #4182 | Tag Integration | Pending |
| #4183 | Collection Limits | ✓ |
| #4184 | Agent Metadata | ✓ |
| #4186 | Tooltip Position | ✓ |
| #4180 | Tooltip A11y | Pending |
| #4185 | Testing & Docs | Pending |

## Testing Requirements

**Backend**: ≥90% coverage (unit + integration)
**Frontend**: ≥85% coverage (unit + E2E)
**Accessibility**: WCAG 2.1 AA (axe-core 0 violations, Lighthouse ≥95)
**Performance**: Tooltip <16ms, card render <100ms

## Migration Guide

### Permission-Aware Features
```tsx
// Before
<MeepleCard showWishlist selectable draggable />

// After (with permissions)
const { canAccess } = usePermissions();
<MeepleCard
  showWishlist={canAccess('wishlist')}
  selectable={canAccess('bulk-select')}
  draggable={canAccess('drag-drop')}
/>
```

### Adding Tags
```tsx
<MeepleCard
  entity="game"
  tags={createTagsFromKeys('game', ['new', 'sale', 'owned'])}
/>
```

### Agent Cards with Full Metadata
```tsx
<MeepleCard
  entity="agent"
  title="Rules Expert"
  agentMetadata={{
    status: AgentStatus.Active,
    model: { name: 'GPT-4o-mini', temperature: 0.7 },
    invocationCount: 342,
    capabilities: ['RAG', 'Vision']
  }}
/>
```
