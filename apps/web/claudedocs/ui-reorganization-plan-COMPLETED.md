# UI Components Reorganization Plan

**Date**: 2025-12-09
**Current**: 65 files in `src/components/ui/` (flat structure)
**Target**: 7 category-based subdirectories
**Imports to Update**: 216

---

## Category Mapping (65 files → 7 directories)

### 1. `ui/primitives/` (15 files)
Basic atomic components:
```
- button.tsx, button.stories.tsx, button-redesign.tsx
- input.tsx, input.stories.tsx, input-redesign.tsx
- label.tsx, label.stories.tsx
- checkbox.tsx, checkbox.stories.tsx
- textarea.tsx, textarea.stories.tsx, textarea-redesign.tsx
- toggle.tsx, toggle.stories.tsx
- toggle-group.tsx
```

### 2. `ui/feedback/` (10 files)
User feedback and alerts:
```
- alert.tsx, alert.stories.tsx
- alert-dialog.tsx, alert-dialog.stories.tsx
- confirm-dialog.tsx
- sonner.tsx (toast notifications)
- skeleton.tsx, skeleton.stories.tsx
- progress.tsx, progress.stories.tsx
```

### 3. `ui/navigation/` (10 files)
Navigation and wayfinding:
```
- tabs.tsx, tabs.stories.tsx
- breadcrumb-redesign.tsx
- dropdown-menu.tsx, dropdown-menu.stories.tsx
- sheet.tsx, sheet.stories.tsx (side panels)
- separator.tsx, separator.stories.tsx
```

### 4. `ui/data-display/` (14 files)
Content presentation:
```
- card.tsx, card.stories.tsx
- table.tsx, table.stories.tsx
- badge.tsx, badge.stories.tsx
- avatar.tsx
- accordion.tsx
- confidence-badge.tsx, confidence-badge.stories.tsx
- rating-stars.tsx, rating-stars.stories.tsx
- citation-link.tsx, citation-link.stories.tsx
```

### 5. `ui/overlays/` (6 files)
Modal and overlay components:
```
- dialog.tsx, dialog.stories.tsx
- tooltip.tsx
- select.tsx, select.stories.tsx, select-redesign.tsx
```

### 6. `ui/forms/` (4 files)
Form-specific components:
```
- form.tsx, form.stories.tsx
- switch.tsx, switch.stories.tsx
```

### 7. `ui/meeple/` (6 files)
MeepleAI custom components:
```
- meeple-avatar.tsx, meeple-avatar.stories.tsx
- meeple-logo.tsx
- chat-message.tsx, chat-message.stories.tsx
- motion-button.tsx
- UIProvider.tsx
```

---

## Migration Strategy

### Phase 1: Create Directory Structure (1 min)
```bash
cd apps/web/src/components/ui
mkdir -p primitives feedback navigation data-display overlays forms meeple
```

### Phase 2: Move Files to Categories (5 min)
```bash
# Use git mv to preserve history
# Example for primitives:
git mv button.tsx primitives/
git mv button.stories.tsx primitives/
git mv button-redesign.tsx primitives/
# ... repeat for all 65 files
```

### Phase 3: Update Import Paths (10 min)
Use morphllm or find/replace to update 216 imports:
```typescript
// From:
import { Button } from '@/components/ui/button'

// To:
import { Button } from '@/components/ui/primitives/button'
```

### Phase 4: Create Barrel Exports (3 min)
Add `index.ts` in each category:
```typescript
// ui/primitives/index.ts
export * from './button';
export * from './input';
export * from './label';
// ...
```

### Phase 5: Optional - Root Barrel (2 min)
Update `ui/index.ts` to re-export all categories:
```typescript
// ui/index.ts
export * from './primitives';
export * from './feedback';
// ... (allows @/components/ui imports to still work)
```

---

## Import Update Pattern

**Pattern 1 - Direct Imports** (216 occurrences):
```typescript
// Before:
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

// After (with barrel exports):
import { Button } from '@/components/ui/primitives';
import { Card } from '@/components/ui/data-display';

// OR (if root barrel exists):
import { Button, Card } from '@/components/ui';
```

**Recommended**: Use root barrel export to minimize breaking changes

---

## Risk Mitigation

### Safety Checks
1. ✅ Use `git mv` to preserve history
2. ✅ Create barrel exports to maintain backward compat
3. ✅ Run `pnpm typecheck` after each phase
4. ✅ Run `pnpm test` at the end
5. ✅ Commit after each successful phase

### Rollback Plan
```bash
git reset --hard HEAD~1  # If phase fails
```

---

## Estimated Timeline

- Phase 1 (directories): 1 min
- Phase 2 (move files): 5 min
- Phase 3 (update imports): 10 min
- Phase 4 (barrel exports): 3 min
- Phase 5 (root barrel): 2 min
- Validation (typecheck + tests): 5 min

**Total**: ~25 minutes

---

## Benefits

**Before**: 70 files flat structure (now 65 after cleanup)
**After**: 7 organized categories with ~9 files each

- ✅ Easier navigation and discovery
- ✅ Clear component purpose from directory name
- ✅ Scalable for future additions
- ✅ Follows Atomic Design principles
- ✅ Better cognitive load (9 items vs 65)

---

## Success Criteria

- [ ] All 65 files moved to categories
- [ ] Zero TypeScript errors
- [ ] All 216 imports updated
- [ ] Tests pass (90%+ coverage maintained)
- [ ] Storybook still works
- [ ] Git history preserved

---

**Ready to Execute**: Awaiting approval to proceed
