# Issues #4066-#4069: Final Specifications

**Epic**: #4068 - MeepleCard Enhancements

---

## Issue #4066: Tag System Integration

**Area**: Tag System Redesign (2/2)
**Estimate**: 2 giorni
**Priority**: P2-Medium
**Depends on**: #4065

### Acceptance Criteria
- [ ] `<TagStrip>` component integrated in MeepleCard
- [ ] Tags positioned on left edge all variants (grid/list/compact/featured/hero)
- [ ] Responsive behavior: desktop (32px) → tablet (28px) → mobile (24px icon-only)
- [ ] Animation: tags fade-in on card render (staggered 50ms delay each)
- [ ] Tag overflow: "+N" badge with tooltip showing hidden tags

### Implementation
```typescript
// MeepleCard with tags
<MeepleCard
  entity="game"
  title="Wingspan"
  tags={[
    { id: '1', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)' },
    { id: '2', label: 'Sale', icon: Tag, bgColor: 'hsl(0 84% 60%)' },
    { id: '3', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)' },
    { id: '4', label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)' }
  ]}
  maxVisibleTags={3}
/>

// Renders as:
// ┌─────────────┐
// │█ New  │     │  ← visible tag 1
// │█ Sale │ IMG │  ← visible tag 2
// │█ +2   │     │  ← overflow (+2 hidden: Owned, Wishlist)
// └─────────────┘
```

### Testing
- [ ] All 5 layout variants support tags
- [ ] Tag strip doesn't break card layout
- [ ] Overflow tooltip shows all hidden tags
- [ ] Mobile icon-only mode readable

---

## Issue #4067: Collection Limit UI

**Area**: Collection Limit Management
**Estimate**: 2-3 giorni
**Priority**: P2-Medium
**Depends on**: #4061 (Permission System)

### Acceptance Criteria
- [ ] Progress bar component for game count limit
- [ ] Storage quota indicator for PDF/image total size
- [ ] Color coding: green (<75%), yellow (75-90%), red (>90%)
- [ ] Warning icon when approaching limit (>75%)
- [ ] Tooltip shows exact numbers: "450 / 500 games (90%)"

### Tier-Based Limits
```typescript
const COLLECTION_LIMITS: Record<UserTier, CollectionLimits> = {
  Free: {
    maxGames: 50,
    storageQuotaMB: 100
  },
  Normal: {
    maxGames: 100,
    storageQuotaMB: 500
  },
  Pro: {
    maxGames: 500,
    storageQuotaMB: 5000 // 5GB
  },
  Enterprise: {
    maxGames: Infinity,
    storageQuotaMB: Infinity
  }
};
```

### UI Mockup
```
┌──────────────────────────────────┐
│ Your Collection                  │
│                                  │
│ ┌──────────────────────────────┐ │
│ │████████████████░░░░░░ 450/500│ │  ← Game count (90% - red)
│ └──────────────────────────────┘ │
│ ⚠️ Approaching game limit        │
│                                  │
│ ┌──────────────────────────────┐ │
│ │███████░░░░░░░░░░░░░░ 1.2/5GB │ │  ← Storage quota (24% - green)
│ └──────────────────────────────┘ │
│                                  │
│ [Upgrade to Pro] [Manage]        │
└──────────────────────────────────┘
```

### Testing
- [ ] Progress bar updates in real-time
- [ ] Color transitions at 75% and 90% thresholds
- [ ] Tooltip shows exact numbers
- [ ] Upgrade CTA appears when >90%

---

## Issue #4068: Agent-Specific Metadata Display

**Area**: Agent Type Support Enhancement
**Estimate**: 3 giorni
**Priority**: P2-Medium

### Acceptance Criteria
- [ ] Status badge: Active (green), Idle (gray), Training (yellow), Error (red)
- [ ] Model info display: name + parameters (temp, max_tokens)
- [ ] Invocation count with formatting (1.2K, 342)
- [ ] Capabilities tags: RAG, Vision, Code Execution, Function Calling
- [ ] Last execution timestamp (relative: "2 hours ago")

### Agent Card Example
```typescript
<MeepleCard
  entity="agent"
  title="Azul Rules Expert"
  subtitle="RAG Strategy · GPT-4o-mini"
  metadata={[
    { icon: Activity, value: "Active", color: "green" },
    { icon: MessageSquare, value: "342 invocations" },
    { icon: Clock, value: "2 hours ago" }
  ]}
  tags={[
    { id: 'rag', label: 'RAG', icon: Brain },
    { id: 'vision', label: 'Vision', icon: Eye }
  ]}
  agentStatus="active"
  agentModel={{
    name: "GPT-4o-mini",
    temperature: 0.7,
    maxTokens: 2000
  }}
  invocationCount={342}
  lastExecuted="2025-02-12T10:00:00Z"
/>
```

### Visual Mockup
```
┌────────────────────────────────┐
│█ RAG   ┌─────────────────────┐ │
│█       │                     │ │
│█ Vision│   Agent Avatar      │ │
│█       │   (AI icon)         │ │
│█       └─────────────────────┘ │
│█                               │
│█ ● Active  Azul Rules Expert   │  ← Status badge (green dot)
│█           RAG Strategy        │
│█           GPT-4o-mini         │
│█                               │
│█ [💬 342] [🕐 2h ago]         │  ← Metadata
└────────────────────────────────┘
```

### Status Badge Implementation
```typescript
const AGENT_STATUS_CONFIG = {
  active: {
    color: 'hsl(142 76% 36%)',
    icon: '●',
    label: 'Active'
  },
  idle: {
    color: 'hsl(0 0% 50%)',
    icon: '○',
    label: 'Idle'
  },
  training: {
    color: 'hsl(38 92% 50%)',
    icon: '◐',
    label: 'Training'
  },
  error: {
    color: 'hsl(0 84% 60%)',
    icon: '✕',
    label: 'Error'
  }
};
```

### Testing
- [ ] All 4 status states render correctly
- [ ] Invocation count formats large numbers (1.2K, 3.4M)
- [ ] Relative timestamps update ("just now" → "1 min ago")
- [ ] Model tooltip shows full parameters

---

## Issue #4069: Integration Testing & Documentation

**Area**: Testing & Documentation
**Estimate**: 2-3 giorni
**Priority**: P1-High (Quality Gate)
**Depends on**: All previous issues

### Acceptance Criteria

#### AC1: E2E Test Coverage
- [ ] Permission system: Free/Normal/Pro/Enterprise user flows
- [ ] Tooltip positioning: viewport edge cases, mobile touch
- [ ] Tag system: overflow, responsive, entity-specific
- [ ] Collection limits: progress bars, warning states
- [ ] Agent metadata: all status states, invocation counts

#### AC2: Accessibility Audit
- [ ] WCAG 2.1 AA compliance (axe-core 0 violations)
- [ ] Keyboard navigation complete flows
- [ ] Screen reader compatibility (NVDA, VoiceOver)
- [ ] Lighthouse accessibility score ≥ 95
- [ ] Color contrast ratios verified

#### AC3: Visual Regression Tests
- [ ] Snapshot tests per variant (grid/list/compact/featured/hero)
- [ ] Snapshot tests per entity type (game/agent/document/etc.)
- [ ] Responsive snapshots (mobile/tablet/desktop)
- [ ] Chromatic visual diff tests

#### AC4: Documentation Updates
- [ ] `docs/frontend/components/meeple-card.md` updated
- [ ] Permission system usage guide
- [ ] Tag system examples
- [ ] Accessibility best practices
- [ ] Storybook stories for all features

#### AC5: Performance Testing
- [ ] Tooltip positioning < 16ms (60fps)
- [ ] Card render with all features < 100ms
- [ ] Permission checks cached (no redundant API calls)
- [ ] Large card grids (100+ cards) performant

### Test Scenarios

#### Scenario 1: Free User Limitations
```gherkin
Given a user with Free tier
When they view a game card
Then wishlist button is visible
And bulk select checkbox is hidden
And drag handle is hidden
And quick actions show only "View" and "Share"
And tooltip shows "Upgrade to Normal for bulk selection"
```

#### Scenario 2: Tooltip Edge Case
```gherkin
Given a card near viewport bottom edge
When user hovers tooltip trigger
Then tooltip flips to appear above trigger
And tooltip stays within viewport bounds
And tooltip content is fully visible
```

#### Scenario 3: Tag Overflow
```gherkin
Given a game with 5 tags
When card renders with maxVisibleTags=3
Then 3 tags are visible in left strip
And "+2" badge appears
When user hovers "+2" badge
Then tooltip shows "Owned, Wishlist"
```

#### Scenario 4: Collection Limit Warning
```gherkin
Given a Pro user with 475/500 games (95%)
When they view their collection
Then progress bar is red
And warning icon is visible
And tooltip shows "475 / 500 games (95%)"
And "Upgrade to Enterprise" CTA appears
```

### Documentation Checklist
- [ ] README updated with new features
- [ ] CHANGELOG.md entry created
- [ ] API reference docs generated
- [ ] Migration guide from old card system
- [ ] Troubleshooting section added

### Performance Benchmarks
- [ ] Initial render: < 100ms (all features enabled)
- [ ] Permission check: < 5ms (cached)
- [ ] Tooltip position calc: < 16ms
- [ ] Tag rendering: < 10ms per tag
- [ ] Bundle size impact: < 15KB gzipped

---

## 🔗 Dependency Summary

```
#4060 (Permission Model)
  ↓
#4061 (Permission Hooks)
  ↓
#4062 (MeepleCard Perms) ────┐
                              │
#4063 (Tooltip Position) ────┤
  ↓                           │
#4064 (Tooltip A11y) ────────┤
                              │
#4065 (Tag Component) ───────┤
  ↓                           │
#4066 (Tag Integration) ─────┤
                              │
#4067 (Collection Limits) ───┤
                              │
#4068 (Agent Metadata) ──────┤
                              ↓
                         #4069 (Testing & Docs)
```

**Critical Path**: #4060 → #4061 → #4062 → #4069 (15-17 giorni)
**Parallel Track**: #4063 → #4064, #4065 → #4066, #4067, #4068 (può procedere in parallelo)

**Total Estimate**: 22-29 giorni (4.5-6 settimane con parallelizzazione)
