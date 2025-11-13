# Component Migration Audit - Issue #930

**Date**: 2025-11-12
**Epic**: #926 (Frontend Improvement Roadmap)
**Status**: In Progress
**Auditor**: Claude Code (Automated Analysis)

## Executive Summary

- **Total Component Files**: 170 files analyzed
- **Files Using Custom Buttons**: 5 files with `.btn-*` classes
- **Files Using Custom Cards**: 4 files with `.card` class
- **Files with Form Elements**: 56 files with `<button>`, `<input>`, `<textarea>`, `<select>`
- **Existing shadcn/ui Usage**: 2 files (ThemeSwitcher.tsx, shadcn-demo.tsx)
- **react-hot-toast Usage**: 6 files
- **Migration Scope**: 20-30 components across ~40-50 files

## Current shadcn/ui Components Installed

✅ **Installed**:
1. Button (`components/ui/button.tsx`)
2. Card (`components/ui/card.tsx`)
3. Dialog (`components/ui/dialog.tsx`)
4. Dropdown Menu (`components/ui/dropdown-menu.tsx`)
5. Input (`components/ui/input.tsx`)
6. Select (`components/ui/select.tsx`)

❌ **Not Installed** (Required):
7. Toast
8. Avatar
9. Badge
10. Table
11. Textarea (if needed)
12. Checkbox (if needed)
13. Radio Group (if needed)

## High-Priority Components (Week 4)

### 1. **Buttons** → shadcn Button
**Priority**: 🔴 Critical
**Complexity**: Low
**Files Affected**: 5 direct + ~40 with `<button>` elements

**Custom Button Classes Found**:
- `btn-primary` (used in: index.tsx, board-game-ai.tsx)
- `btn-secondary` (used in: index.tsx)
- `btn-secondary-accessible` (used in: index.tsx, board-game-ai.tsx)
- Various inline button styles across components

**Migration Strategy**:
1. Replace `.btn-primary` → `<Button>` (default variant)
2. Replace `.btn-secondary` → `<Button variant="secondary">`
3. Replace `.btn-secondary-accessible` → `<Button variant="outline">` or custom variant
4. Update `AccessibleButton` component to use shadcn Button internally
5. Migrate inline `<button>` elements in:
   - Chat components (MessageActions, MessageInput, MessageEditForm)
   - Admin components (CategoryConfigTab, FeatureFlagsTab)
   - Upload components (MultiFileUpload, UploadQueue)
   - Timeline components
   - Diff components (DiffNavigationControls, DiffToolbar)

**Testing Requirements**:
- All button variants render correctly
- Accessibility maintained (ARIA labels, keyboard nav)
- Loading states work (LoadingButton component)
- Icon buttons work
- Button groups work

---

### 2. **Cards** → shadcn Card
**Priority**: 🔴 Critical
**Complexity**: Low
**Files Affected**: 4 direct + ~15-20 with card-like containers

**Custom Card Usage Found**:
- `.card` utility class (index.tsx, board-game-ai.tsx, reset-password.tsx)
- Container divs with card-like styling

**Migration Strategy**:
1. Replace `.card` class → `<Card>` with `<CardHeader>`, `<CardContent>`, `<CardFooter>`
2. Identify components that should use Card:
   - PromptVersionCard → Use Card components
   - Chat message containers
   - Admin stat cards (AdminCharts)
   - Timeline event cards
   - Upload summary cards

**Testing Requirements**:
- Card layouts preserved
- Nested cards work
- Responsive behavior maintained
- Shadows and borders correct

---

### 3. **Inputs** → shadcn Input
**Priority**: 🔴 Critical
**Complexity**: Medium
**Files Affected**: ~30 files with `<input>` elements

**Current Input Usage**:
- `AccessibleFormInput` component (custom, highly used)
- Inline `<input>` elements across forms
- Search inputs (DiffSearchInput, BggSearchModal)
- File inputs (MultiFileUpload)

**Migration Strategy**:
1. **Update `AccessibleFormInput`** to use shadcn Input internally (preserve accessibility features)
2. Migrate standalone inputs:
   - Chat: MessageInput, MessageEditForm
   - Admin: CategoryConfigTab, FeatureFlagsTab, configuration pages
   - Timeline: TimelineFilters, VersionTimelineFilters
   - Diff: DiffSearchInput
   - Auth: login, reset-password pages
   - Search: BggSearchModal

**Testing Requirements**:
- All input types work (text, email, password, number, file)
- Accessibility maintained (labels, errors, hints)
- Validation states work
- Auto-complete works
- File upload works

---

### 4. **Dialogs/Modals** → shadcn Dialog
**Priority**: 🔴 Critical
**Complexity**: Medium
**Files Affected**: 4 modal components + `AccessibleModal`

**Current Modal Components**:
1. `AccessibleModal` (custom, WCAG 2.1 AA compliant - **HIGH PRIORITY**)
2. `ErrorModal`
3. `SessionWarningModal`
4. `BggSearchModal`
5. `ExportChatModal`

**Migration Strategy**:
1. **Update `AccessibleModal`** to use shadcn Dialog internally (CRITICAL - preserve all accessibility features)
2. Migrate specific modals:
   - ErrorModal → Dialog with error variant
   - SessionWarningModal → Dialog with warning variant
   - BggSearchModal → Dialog with form content
   - ExportChatModal → Dialog with export options

**Testing Requirements**:
- WCAG 2.1 AA compliance maintained
- Focus trap works
- Escape key closes modal
- Click outside closes modal (configurable)
- Scroll lock works
- Nested modals work (if needed)
- Animation smooth

## Medium-Priority Components (Week 5)

### 5. **Dropdowns** → shadcn DropdownMenu
**Priority**: 🟡 Medium
**Complexity**: Low
**Files Affected**: ThemeSwitcher (already using it)

**Current Usage**:
- ThemeSwitcher already uses shadcn DropdownMenu ✅
- Potential candidates: MessageActions (context menu)

**Migration Strategy**:
1. Audit for custom dropdown implementations
2. Convert to shadcn DropdownMenu
3. Add keyboard navigation

---

### 6. **Select** → shadcn Select
**Priority**: 🟡 Medium
**Complexity**: Medium
**Files Affected**: ~10 files with `<select>` elements

**Current Select Usage**:
- GameSelector (chat)
- AgentSelector (chat)
- Role selector (auth/registration)
- Category selectors (admin)
- Filter selectors (timeline, admin analytics)

**Migration Strategy**:
1. Replace native `<select>` with shadcn Select
2. Maintain multi-select capability (if needed)
3. Add search functionality where needed

---

### 7. **Tables** → shadcn Table
**Priority**: 🟡 Medium
**Complexity**: Medium
**Files Affected**: Admin pages (users, analytics, prompts, bulk-export)

**Current Table Usage**:
- Admin user management
- Admin analytics tables
- Prompt version lists
- Bulk export results
- Timeline event lists

**Migration Strategy**:
1. Replace custom table implementations
2. Add sorting, filtering, pagination
3. Maintain responsive behavior (mobile collapse)

---

### 8. **Toast** → shadcn Toast (replace react-hot-toast)
**Priority**: 🟡 Medium
**Complexity**: Medium
**Files Affected**: 6 files

**Current Toast Usage** (react-hot-toast):
1. `pages/admin/configuration.tsx`
2. `components/admin/CategoryConfigTab.tsx`
3. `components/admin/FeatureFlagsTab.tsx`
4. Tests: 3 test files

**Migration Strategy**:
1. Install shadcn Toast + Sonner (recommended)
2. Create toast utility wrapper
3. Replace all `toast()` calls
4. Update `Toast.tsx` component
5. Remove react-hot-toast dependency

---

### 9. **Avatar** → shadcn Avatar
**Priority**: 🟢 Low
**Complexity**: Low
**Files Affected**: Chat components, user profile

**Potential Usage**:
- User avatars in chat (Message component)
- Profile page
- Admin user management
- Comment threads

**Migration Strategy**:
1. Install shadcn Avatar
2. Add to user display components
3. Support initials fallback
4. Support image loading states

---

### 10. **Badge** → shadcn Badge
**Priority**: 🟢 Low
**Complexity**: Low
**Files Affected**: Status indicators, labels

**Potential Usage**:
- Feature flags status
- User roles
- Processing status
- Version labels
- Search mode indicators

**Migration Strategy**:
1. Install shadcn Badge
2. Replace status indicators
3. Add variant colors (success, warning, error, info)

## Component Inventory by Category

### Accessible Components (High Priority)
| Component | File | Migration | Complexity |
|-----------|------|-----------|------------|
| AccessibleButton | accessible/AccessibleButton.tsx | Update to use shadcn Button | Medium |
| AccessibleFormInput | accessible/AccessibleFormInput.tsx | Update to use shadcn Input | Medium |
| AccessibleModal | accessible/AccessibleModal.tsx | Update to use shadcn Dialog | High |
| AccessibleSkipLink | accessible/AccessibleSkipLink.tsx | Keep as-is | N/A |

### Chat Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| MessageActions | chat/MessageActions.tsx | Buttons, Dropdown | Medium |
| MessageInput | chat/MessageInput.tsx | Input, Button | Low |
| MessageEditForm | chat/MessageEditForm.tsx | Input, Buttons | Low |
| GameSelector | chat/GameSelector.tsx | Select | Low |
| AgentSelector | chat/AgentSelector.tsx | Select | Low |
| ChatHistoryItem | chat/ChatHistoryItem.tsx | Button | Low |

### Admin Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| CategoryConfigTab | admin/CategoryConfigTab.tsx | Inputs, Buttons, Toast | Medium |
| FeatureFlagsTab | admin/FeatureFlagsTab.tsx | Inputs, Buttons, Toast | Medium |
| AdminCharts | AdminCharts.tsx | Cards | Low |

### Modal Components
| Component | File | Migration Target | Complexity |
|-----------|------|------------------|------------|
| ErrorModal | ErrorModal.tsx | Dialog | Low |
| SessionWarningModal | SessionWarningModal.tsx | Dialog | Low |
| BggSearchModal | BggSearchModal.tsx | Dialog with Input | Medium |
| ExportChatModal | ExportChatModal.tsx | Dialog | Low |

### Upload Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| MultiFileUpload | MultiFileUpload.tsx | Input (file), Buttons | Medium |
| UploadQueue | UploadQueue.tsx | Buttons, List | Low |
| UploadQueueItem | UploadQueueItem.tsx | Buttons, Progress | Low |
| UploadSummary | UploadSummary.tsx | Buttons, Card | Low |

### Timeline Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| Timeline | timeline/Timeline.tsx | Buttons, Cards | Medium |
| TimelineFilters | timeline/TimelineFilters.tsx | Inputs, Selects | Medium |
| TimelineEventItem | timeline/TimelineEventItem.tsx | Buttons, Cards | Low |
| VersionTimelineFilters | VersionTimelineFilters.tsx | Inputs, Selects | Medium |

### Diff Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| DiffSearchInput | diff/DiffSearchInput.tsx | Input | Low |
| DiffNavigationControls | diff/DiffNavigationControls.tsx | Buttons | Low |
| DiffToolbar | diff/DiffToolbar.tsx | Buttons, Select | Medium |
| DiffViewModeToggle | diff/DiffViewModeToggle.tsx | Buttons (toggle) | Low |

### Loading Components
| Component | File | Migration | Complexity |
|-----------|------|-----------|------------|
| LoadingButton | loading/LoadingButton.tsx | Update to use shadcn Button | Low |
| Spinner | loading/Spinner.tsx | Keep as-is or use shadcn | Low |
| SkeletonLoader | loading/SkeletonLoader.tsx | Use shadcn Skeleton | Low |

### Other Components
| Component | File | Elements to Migrate | Complexity |
|-----------|------|---------------------|------------|
| PromptVersionCard | PromptVersionCard.tsx | Card, Buttons | Low |
| SearchModeToggle | SearchModeToggle.tsx | Buttons (toggle) | Low |
| ThemeSwitcher | ThemeSwitcher.tsx | Already uses shadcn ✅ | N/A |
| Toast | Toast.tsx | Replace with shadcn Toast | Medium |

## Pages Requiring Updates

| Page | File | Elements to Migrate |
|------|------|---------------------|
| Home | pages/index.tsx | Buttons, Cards, Modal |
| Board Game AI | pages/board-game-ai.tsx | Buttons, Cards |
| Chat | pages/chat.tsx | Full chat UI |
| Upload | pages/upload.tsx | File input, Buttons |
| Admin | pages/admin.tsx | Tables, Buttons |
| Admin Configuration | pages/admin/configuration.tsx | Inputs, Toast, Tables |
| Admin Users | pages/admin/users.tsx | Tables, Buttons |
| Admin Prompts | pages/admin/prompts/index.tsx | Tables, Buttons, Cards |
| Login | pages/login.tsx | Inputs, Buttons, Modal |
| Reset Password | pages/reset-password.tsx | Inputs, Buttons, Cards |

## Migration Complexity Assessment

### Low Complexity (1-2 hours each)
- Simple button replacements
- Card wrapper replacements
- Single input fields
- Basic modal replacements

**Estimated**: 10-12 components × 1.5h = 15-18 hours

### Medium Complexity (3-5 hours each)
- AccessibleButton, AccessibleFormInput updates
- Complex forms with validation
- Multi-element modals
- Select components with search
- Table implementations

**Estimated**: 12-15 components × 4h = 48-60 hours

### High Complexity (6-8 hours each)
- AccessibleModal (must preserve all accessibility)
- Toast migration (replace entire library)
- MultiFileUpload (complex state management)

**Estimated**: 3 components × 7h = 21 hours

**Total Estimated Effort**: **84-99 hours** (10-12 working days for single developer)

## Testing Strategy

### Unit Tests
- [ ] Update all existing component tests
- [ ] Add new tests for shadcn integrations
- [ ] Maintain 90%+ coverage threshold

### Integration Tests
- [ ] Test form submissions
- [ ] Test modal interactions
- [ ] Test button states and loading
- [ ] Test keyboard navigation

### Visual Regression Tests
- [ ] Playwright screenshot comparisons
- [ ] Test responsive breakpoints
- [ ] Test light/dark theme consistency

### Accessibility Tests
- [ ] WCAG 2.1 AA compliance
- [ ] Keyboard navigation
- [ ] Screen reader testing
- [ ] Focus management

### E2E Tests
- [ ] Complete user flows (login, chat, upload, admin)
- [ ] Cross-browser testing
- [ ] Mobile device testing

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Breaking accessibility in AccessibleModal | 🔴 High | Thorough testing, keep existing tests, incremental rollout |
| Toast library replacement causing issues | 🟡 Medium | Create abstraction layer, test all toast usages |
| Test failures due to selector changes | 🟡 Medium | Update tests incrementally, use data-testid |
| Visual regressions | 🟡 Medium | Playwright visual comparisons, manual QA |
| Performance degradation | 🟢 Low | Monitor bundle size, lazy load components |

## Rollback Plan

1. **Git Strategy**: Feature branch with incremental commits per component type
2. **Backup**: Keep old components with `-legacy` suffix initially
3. **Feature Flags**: If time permits, add toggles for new vs old components
4. **Monitoring**: Watch for error rates and user feedback after deployment

## Success Criteria

- [ ] 20-30 components successfully migrated
- [ ] All tests passing (90%+ coverage maintained)
- [ ] No visual regressions
- [ ] Accessibility maintained or improved (WCAG 2.1 AA)
- [ ] Documentation updated
- [ ] Zero production issues post-deployment

## Next Steps

1. ✅ Complete component audit (this document)
2. ⏳ Install missing shadcn/ui components (Toast, Avatar, Badge, Table)
3. ⏳ Start high-priority migrations (Week 4):
   - AccessibleButton
   - AccessibleFormInput
   - AccessibleModal
   - Button replacements
   - Card replacements
   - Input replacements
4. ⏳ Continue medium-priority migrations (Week 5)
5. ⏳ Testing and QA
6. ⏳ Documentation updates
7. ⏳ Create PR and deploy

## Notes

- **Accessibility is non-negotiable**: AccessibleModal, AccessibleButton, AccessibleFormInput MUST maintain WCAG 2.1 AA compliance
- **Test coverage must remain above 90%**: Update tests as we migrate
- **Big Bang approach**: All migrations in single PR as decided
- **Direct replacement**: Delete old implementations after migration
- **Issue #1035**: ThemeSwitcher mobile visibility bug created separately (P1)

---

**Last Updated**: 2025-11-12
**Next Review**: After high-priority migrations complete
