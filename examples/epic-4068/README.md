# Epic #4068: Complete Examples Repository

**Runnable code examples for all MeepleCard enhancements**

---

## Setup

```bash
cd examples/epic-4068
pnpm install
pnpm dev
```

Navigate to http://localhost:3000 to view examples

---

## Examples Index

### Permission System

**01-basic-permission-check.tsx**
- User permissions display (tier, role, status)
- Feature access checks (canAccess)
- Tier comparison (hasTier)
- Admin role check (isAdmin)
- Permission-aware MeepleCard

**Key Features Demonstrated**:
- usePermissions() hook
- Conditional rendering based on permissions
- Upgrade prompts for locked features
- Hierarchical tier checks

---

### Tag System

**02-tag-system-usage.tsx**
- Tag presets (game, agent, document)
- Custom tag creation
- Tag overflow behavior (max 3 + "+N")
- Responsive variants (desktop/tablet/mobile)
- Tag sorting by priority

**Key Features Demonstrated**:
- createTagsFromKeys() utility
- sortTagsByPriority() utility
- TagStrip component
- Entity-specific tag presets
- Visual tag preview

---

### Agent Metadata

**03-agent-metadata-display.tsx**
- Agent status badges (Active/Idle/Training/Error)
- Model information tooltips
- Invocation statistics display
- Capability tags
- Complete agent cards

**Key Features Demonstrated**:
- AgentStatusBadge component
- AgentModelInfo component
- AgentStatsDisplay component
- formatInvocationCount(), formatRelativeTime()
- Agent entity type integration

---

### Tooltip Positioning

**04-tooltip-positioning-examples.tsx**
- Auto-flip at viewport edges (9 positions)
- Keyboard navigation (Tab/Enter/Escape)
- Mobile touch support (tap to show)
- Large tooltip content handling
- Dynamic tooltip repositioning

**Key Features Demonstrated**:
- SmartTooltip component
- AccessibleTooltip component
- useSmartTooltip() hook
- WCAG 2.1 AA compliance
- Performance (< 16ms positioning)

---

### Collection Limits

**05-collection-limits-ui.tsx**
- Progress bars (games, storage)
- Color-coded warnings (75% yellow, 90% red)
- Tier comparison table
- Upgrade CTAs
- Real-time limit tracking

**Key Features Demonstrated**:
- CollectionLimitIndicator component
- usePermissions() limits access
- Progress component
- Tier-based limit calculation

---

### Complete Integration

**06-complete-integration-example.tsx** (Comprehensive)
- Permission system + Tags + Tooltips + Agent metadata + Collection limits
- Real-world game catalog with all features
- Admin panel with role-based access
- Collection management with limits
- Agent dashboard with metadata

**Key Features Demonstrated**:
- All Epic #4068 features together
- Complex permission flows
- Multi-tier user experiences
- Feature upgrade paths

---

## Running Individual Examples

```bash
# Permission system only
pnpm dev:example permissions

# Tag system only
pnpm dev:example tags

# Tooltip system only
pnpm dev:example tooltips

# Agent metadata only
pnpm dev:example agent

# Collection limits only
pnpm dev:example limits

# All features together
pnpm dev:example complete
```

---

## Example Data

Each example uses mock data defined in `examples/epic-4068/data/`:

**mock-users.ts**: Test users with different tiers/roles
- free@example.com (Free tier, User role)
- normal@example.com (Normal tier, User role)
- pro@example.com (Pro tier, User role)
- enterprise@example.com (Enterprise tier, User role)
- editor@example.com (Free tier, Editor role)
- creator@example.com (Normal tier, Creator role)
- admin@example.com (Free tier, Admin role)

**mock-games.ts**: Games with various tag configurations
**mock-agents.ts**: Agents with different statuses and capabilities
**mock-collections.ts**: Collections at different usage levels

---

## Testing Examples

```bash
# Run tests for all examples
pnpm test:examples

# Test specific example
pnpm test examples/epic-4068/01-basic-permission-check.test.tsx

# Visual regression (Playwright)
pnpm test:visual examples/epic-4068/

# Accessibility audit
pnpm test:a11y examples/epic-4068/
```

---

## Integration with Main App

Examples can be imported into main app:

```typescript
// app/(authenticated)/examples/epic-4068/page.tsx
import BasicPermissionExample from '@/../../examples/epic-4068/01-basic-permission-check';

export default function Epic4068ExamplesPage() {
  return <BasicPermissionExample />;
}
```

---

## Example Structure

```
examples/epic-4068/
├── README.md (this file)
├── package.json
├── tsconfig.json
├── 01-basic-permission-check.tsx
├── 02-tag-system-usage.tsx
├── 03-agent-metadata-display.tsx
├── 04-tooltip-positioning-examples.tsx
├── 05-collection-limits-ui.tsx
├── 06-complete-integration-example.tsx
├── data/
│   ├── mock-users.ts
│   ├── mock-games.ts
│   ├── mock-agents.ts
│   └── mock-collections.ts
├── tests/
│   ├── 01-basic-permission.test.tsx
│   ├── 02-tag-system.test.tsx
│   └── ... (test for each example)
└── screenshots/
    ├── permission-free-tier.png
    ├── permission-pro-tier.png
    ├── tags-game-card.png
    ├── agent-metadata.png
    └── ... (visual references)
```

---

## Documentation

Each example file includes:
- JSDoc comments explaining features
- Inline code comments for clarity
- Usage notes for key components
- Links to relevant issue/docs

---

## Live Demo (Storybook)

Examples are also available in Storybook:

```bash
cd ../../apps/web
pnpm storybook
```

Navigate to **Examples > Epic #4068** section

---

## Contributing Examples

To add a new example:

1. Create `examples/epic-4068/0X-your-example.tsx`
2. Add mock data to `data/` if needed
3. Create test file `tests/0X-your-example.test.tsx`
4. Update this README with example description
5. Add to Storybook stories

---

## Resources

- Epic #4068: https://github.com/DegrassiAaron/meepleai-monorepo/issues/4068
- Implementation Guide: `docs/02-development/epic-4068-implementation-guide.md`
- Component API: `docs/frontend/components/epic-4068-component-api-reference.md`
- Best Practices: `docs/10-best-practices/epic-4068-best-practices.md`
