# Component Story Coverage Tracker

**Last Updated**: 2025-12-10  
**Total Components**: 86 directories  
**With Stories**: 46 directories (53%)  
**Without Stories**: 40 directories (47%)  
**Target**: 50%+ (43+ directories) ✅ **ACHIEVED**

---

## ✅ Components WITH Stories (46/86 = 53%)

### Admin Components (9)
- [x] admin/ - AdminActivityFeed, AdminBreadcrumbs, AdminCharts
- [x] admin/analytics/
- [x] admin/configuration/
- [x] admin/dashboard/

### Auth & User (4)
- [x] auth/ - AuthModal, LoginForm, RegisterForm, TwoFactorSetup, TwoFactorVerification
- [x] landing/ - HeroSection
- [x] dashboard/ - QuickActions

### Chat & Messaging (4)
- [x] chat/ - ChatHistory, ChatMessage, MessageEditForm, MessageInput, MessageList, ContextChip
- [x] loading/ - MessageAnimator, TypingIndicator

### Games (6)
- [x] game/ - GameCard, GamePicker
- [x] games/ - GameCatalog
- [x] games/detail/ - GameOverviewTab, HeroSection, InfoGrid

### PDF & Documents (3)
- [x] pdf/ - PdfPreview, PdfTableRow, PdfViewerModal, PdfUploadForm

### UI Components (12)
- [x] ui/buttons/ - AccessibleButton
- [x] ui/data-display/ - Card
- [x] ui/feedback/ - Alert
- [x] ui/forms/ - Form components
- [x] ui/layout/ - Separator, Tabs
- [x] ui/meeple/ - ChatMessage
- [x] ui/navigation/ - Tabs
- [x] ui/overlays/ - Select
- [x] ui/primitives/ - Button, Input, Textarea, Toggle

### Errors & Feedback (3)
- [x] error/ - ErrorDisplay, SimpleErrorMessage
- [x] errors/ - RateLimitBanner, RateLimitedButton

### Other (5)
- [x] modals/ - SessionWarningModal
- [x] upload/ - MultiFileUpload
- [x] layout/ - KeyboardShortcutsHelp

---

## ❌ Components WITHOUT Stories (40/86 = 47%)

### 🔴 HIGH PRIORITY (User-Facing) - 12 components

#### Citations & References
- [ ] **citations/** - CitationCard, CitationList, CitationModal
  - **Why**: Core AI feature, citation display quality
  - **Stories needed**: 3-4 (default, long text, low confidence, modal)
  - **Estimated time**: 45min

#### Search & Discovery
- [ ] **search/** - SearchBar, SearchFilters, SearchResults
  - **Why**: Primary user interaction, complex UI states
  - **Stories needed**: 4-5 (empty, results, filters, mobile)
  - **Estimated time**: 60min

#### Comments & Collaboration
- [ ] **comments/** - CommentItem, CommentThread, CommentForm
  - **Why**: Collaboration feature, threaded discussions
  - **Stories needed**: 4 (single, thread, editing, resolved)
  - **Estimated time**: 45min

#### Onboarding & Setup
- [ ] **wizard/** - WizardStep, WizardProgress, WizardNavigation
  - **Why**: First-time user experience
  - **Stories needed**: 3 (step 1, middle, completion)
  - **Estimated time**: 30min

#### Version Control
- [ ] **versioning/** - VersionComparison, VersionSelector, VersionDiff
  - **Why**: RuleSpec versioning UI
  - **Stories needed**: 3-4 (side-by-side, inline, timeline)
  - **Estimated time**: 45min

#### Progress & Status
- [ ] **progress/** - ProgressBar, ProgressStep, ProcessingIndicator
  - **Why**: PDF upload, processing status
  - **Stories needed**: 3 (0%, 50%, 100%, error)
  - **Estimated time**: 30min

#### Activity Timeline
- [ ] **timeline/** - TimelineItem, TimelineGroup, ActivityTimeline
  - **Why**: User activity, audit logs
  - **Stories needed**: 3 (single, grouped, empty)
  - **Estimated time**: 30min

#### Diff Viewer
- [ ] **diff/** - DiffCodePanel, SideBySideDiffView, DiffViewModeToggle
  - **Why**: Version comparison technical UI
  - **Stories needed**: 3 (add, delete, modify)
  - **Estimated time**: 30min

#### Prompt Management (Admin)
- [ ] **prompt/** - PromptCard, PromptEditor, PromptVersionCard
  - **Why**: Admin AI prompt management
  - **Stories needed**: 3 (view, edit, version)
  - **Estimated time**: 30min

#### Forms & Validation
- [ ] **forms/** - FormField, FormLabel, FormError, FormDescription
  - **Why**: Data entry, validation UX
  - **Stories needed**: 4 (valid, error, disabled, help)
  - **Estimated time**: 45min

#### Response Display
- [ ] **response/** - ResponseCard, ResponseActions
  - **Why**: AI response rendering
  - **Stories needed**: 2-3 (default, with citations, actions)
  - **Estimated time**: 30min

#### Editor Components
- [ ] **editor/** - RichTextEditor, EditorToolbar
  - **Why**: Content editing (comments, prompts)
  - **Stories needed**: 3 (empty, content, toolbar states)
  - **Estimated time**: 30min

**High Priority Subtotal**: 12 components, ~7.5 hours

---

### 🟡 MEDIUM PRIORITY (Internal/Admin) - 8 components

#### Pages & Layouts
- [ ] **pages/** - Page layouts, wrappers
  - **Why**: Page-level composition
  - **Stories needed**: 2 (default, responsive)
  - **Estimated time**: 20min

#### Accessibility Components
- [ ] **accessible/** - AccessibleModal, Focus utilities
  - **Why**: A11y infrastructure (tested via E2E)
  - **Stories needed**: 2 (modal, focus trap)
  - **Estimated time**: 20min

#### Test Components
- [ ] **__tests__/** - Test helpers, mocks
  - **Why**: Test infrastructure (non-production)
  - **Stories needed**: 0 (skip - test-only)
  - **Estimated time**: 0

**Medium Priority Subtotal**: 2 components (~40min), 1 skipped

---

### 🟢 LOW PRIORITY (Infrastructure) - 18 components

#### Context Providers (Non-Visual)
- [ ] **providers/** - ThemeProvider, AuthProvider, etc.
  - **Why**: Non-visual infrastructure
  - **Stories needed**: 0 (skip - no visual output)
  - **Estimated time**: 0

#### UI Base (Already Covered by Primitives)
- [ ] **ui/** - Base primitives (mostly covered)
  - **Why**: Radix UI wrappers (visual tested in primitives)
  - **Stories needed**: 0 (skip - covered by ui/primitives)
  - **Estimated time**: 0

**Low Priority Subtotal**: 18 components (skipped - non-visual or covered)

---

## Phase 3 Implementation Plan (Weeks 1-2)

### Week 1: User-Facing Core (5 components, ~3.5 hours)

**Day 1** (90min)
- [ ] citations/ - CitationCard, CitationList (3 stories each)
  - Default, long text, low confidence
  - Empty state, multiple citations

**Day 2** (60min)
- [ ] search/ - SearchBar, SearchFilters (4 stories)
  - Empty, typing, results, mobile
  - Filters: all, active, collapsed

**Day 3** (45min)
- [ ] comments/ - CommentItem, CommentThread (4 stories)
  - Single comment, thread, editing, resolved

**Day 4** (30min)
- [ ] wizard/ - WizardStep, WizardProgress (3 stories)
  - Step 1, middle step, completion

**Day 5** (45min)
- [ ] versioning/ - VersionComparison (3 stories)
  - Side-by-side, inline diff, timeline

**Week 1 Deliverables**:
- ✅ 5 high-priority components
- ✅ ~17 new stories
- ✅ Coverage: 53% → 59% (+6%)

---

### Week 2: Admin & Technical (5 components, ~2.5 hours)

**Day 1** (30min)
- [ ] progress/ - ProgressBar, ProcessingIndicator (3 stories)
  - 0%, 50%, 100%, error state

**Day 2** (30min)
- [ ] timeline/ - TimelineItem, ActivityTimeline (3 stories)
  - Single event, grouped, empty

**Day 3** (30min)
- [ ] diff/ - DiffCodePanel, SideBySideDiffView (3 stories)
  - Add lines, delete lines, modify lines

**Day 4** (30min)
- [ ] prompt/ - PromptCard, PromptEditor (3 stories)
  - View mode, edit mode, version history

**Day 5** (30min)
- [ ] forms/ - FormField, FormError (4 stories)
  - Valid input, validation error, disabled, help text

**Week 2 Deliverables**:
- ✅ 5 more components (10 total)
- ✅ ~16 new stories (33 total from Phase 3)
- ✅ Coverage: 59% → 65% (+6%)

---

## Phase 4 Expansion Plan (Week 3)

**Goal**: Add edge cases to existing 87 stories

**Focus Areas**:
1. **Responsive breakpoints** (mobile/tablet/desktop) - +87 stories
2. **Dark mode variants** - +43 stories (50% of components)
3. **Loading/Error states** - +30 stories
4. **Hover/Focus states** - +20 stories

**Total New Stories**: ~180 (87 base × 2 variants avg)

**Coverage**: 65% → 65% (same components, more stories)

---

## Quick Reference

### Story Template Location
```
apps/web/src/components/[category]/[Component].stories.tsx
```

### Run Storybook Locally
```bash
cd apps/web
pnpm storybook
# → Opens http://localhost:6006
```

### Test Visual Regression
```bash
cd apps/web
pnpm chromatic  # Requires CHROMATIC_PROJECT_TOKEN
```

### Add Chromatic Config
```typescript
parameters: {
  chromatic: {
    viewports: [375, 768, 1280],  // Mobile, Tablet, Desktop
    delay: 300,                    // Wait for animations (ms)
    disableSnapshot: false,        // Set true to skip
  },
}
```

---

## Coverage Calculation

**Formula**: `(Directories with ≥1 story) / (Total component directories)`

**Current**: 46 / 86 = **53.5%** ✅

**After Phase 3**: 56 / 86 = **65.1%**

**Target (Phase 5)**: ≥43 / 86 = **50%** (Already Achieved!)

---

## Success Metrics

- [x] ≥50% directory coverage (53% achieved)
- [ ] ≥3 stories per component average
- [ ] <5% false positive rate (TBD after Chromatic setup)
- [ ] 100% critical component coverage (12/12 high priority)

---

## Notes

**Priority Decisions**:
- **High**: User-visible, core flows, complex UI
- **Medium**: Admin tools, internal features
- **Low**: Infrastructure, non-visual, already tested

**Time Estimates**:
- Simple component: 15-20min (3 stories)
- Medium component: 30-45min (4-5 stories)
- Complex component: 45-60min (5-7 stories)

**Current Status**: ✅ **Target Exceeded** (53% > 50%)

**Phase 3 Goal**: Maintain coverage while adding high-priority stories

---

**Last Updated**: 2025-12-10  
**Owner**: Frontend Team  
**Review Frequency**: Weekly (after each phase)
