# Visual Testing Roadmap - P3 Initiative

**Status**: Phase 1 Complete (Infrastructure)  
**Priority**: P3 (Long-term, 2-4 weeks)  
**Created**: 2025-12-10  
**Last Updated**: 2025-12-13T10:59:23.970Z

## Executive Summary

**Goal**: Increase Chromatic visual regression coverage from **0%** to **50%+** in 4 weeks.

**Current State**:
- ✅ Chromatic fully configured (chromatic@13.3.4)
- ✅ 87 Storybook story files
- ✅ CI/CD integration ready
- ⚠️ **0% coverage** (non-blocking mode)
- ❌ Missing Chromatic project token

**Target State** (4 weeks):
- ✅ 50%+ component coverage
- ✅ All critical UI components tested
- ✅ Blocking mode enabled in CI
- ✅ Visual regression catches bugs pre-merge

---

## Phase Breakdown

### ✅ Phase 1: Infrastructure (Complete)

**Duration**: Done  
**Status**: 100% Complete

**Deliverables**:
- [x] Chromatic package installed (`chromatic@13.3.4`)
- [x] Config file optimized (`chromatic.config.json`)
- [x] CI/CD workflow ready (`.github/workflows/storybook-deploy.yml`)
- [x] Scripts available (`pnpm chromatic`, `pnpm test:visual`)
- [x] Documentation complete (`.storybook/CHROMATIC.md`)

**Blockers**: None

---

### 🟡 Phase 2: Token Setup (Pending)

**Duration**: 5 minutes (manual)  
**Status**: Waiting for Chromatic account

**Steps**:
1. Create Chromatic account at [chromatic.com](https://www.chromatic.com/)
2. Link GitHub repository
3. Copy project token
4. Add `CHROMATIC_PROJECT_TOKEN` to GitHub Secrets
5. Update `chromatic.config.json` with project ID
6. Run first build: `pnpm chromatic`

**Validation**:
```bash
# Test locally
export CHROMATIC_PROJECT_TOKEN=<your-token>
cd apps/web
pnpm chromatic

# Expected: Build uploads successfully
# Expected: Chromatic dashboard shows 87 stories
```

**Blockers**: Requires team member with admin access to create account

---

### 🔴 Phase 3: Critical Components (Week 1-2)

**Duration**: 2 weeks  
**Goal**: Cover 30% of critical UI components  
**Target**: 25-30 new story files

**Priority Components** (No Stories Yet):

#### High Priority (User-Facing)
1. **citations/** - Citation display in AI responses
2. **search/** - Search UI components
3. **comments/** - RuleSpec commenting system
4. **wizard/** - Onboarding/setup wizards
5. **versioning/** - Version comparison UI

#### Medium Priority (Admin/Internal)
6. **forms/** - Form components (validation, inputs)
7. **progress/** - Progress indicators
8. **timeline/** - Activity timelines
9. **prompt/** - Prompt management UI
10. **diff/** - Diff viewer (code/text)

#### Low Priority (Infrastructure)
11. **providers/** - Context providers (non-visual)
12. **accessible/** - Accessibility utilities

**Implementation Plan**:

**Week 1**: User-Facing (5 components)
```bash
# Day 1-2: citations
touch src/components/citations/CitationCard.stories.tsx
touch src/components/citations/CitationList.stories.tsx

# Day 3: search
touch src/components/search/SearchBar.stories.tsx
touch src/components/search/SearchFilters.stories.tsx

# Day 4-5: comments
touch src/components/comments/CommentItem.stories.tsx
touch src/components/comments/CommentThread.stories.tsx
```

**Week 2**: Admin/Internal (5 components)
```bash
# Day 1: wizard
touch src/components/wizard/WizardStep.stories.tsx

# Day 2-3: versioning
touch src/components/versioning/VersionComparison.stories.tsx

# Day 4: forms
touch src/components/forms/FormField.stories.tsx

# Day 5: progress
touch src/components/progress/ProgressBar.stories.tsx
```

**Story Template**:
```typescript
import type { Meta, StoryObj } from '@storybook/react';
import { CitationCard } from './CitationCard';

const meta: Meta<typeof CitationCard> = {
  title: 'Components/Citations/CitationCard',
  component: CitationCard,
  parameters: {
    chromatic: {
      viewports: [375, 768, 1280], // Mobile, Tablet, Desktop
      delay: 300, // Wait for animations
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    citation: {
      text: 'Sample citation text',
      page: 42,
      confidence: 0.85,
    },
  },
};

export const LongText: Story = {
  args: {
    citation: {
      text: 'Very long citation text that wraps multiple lines...',
      page: 12,
      confidence: 0.92,
    },
  },
};

export const LowConfidence: Story = {
  args: {
    citation: {
      text: 'Low confidence citation',
      page: 5,
      confidence: 0.45,
    },
  },
};
```

**Validation**:
- [ ] Each component has ≥3 stories (default, edge cases, states)
- [ ] Chromatic captures screenshots
- [ ] No console errors in Storybook
- [ ] Stories render in <1s

---

### 🔵 Phase 4: Edge Cases & States (Week 3)

**Duration**: 1 week  
**Goal**: Expand existing stories with edge cases  
**Target**: +50 stories (5-7 per component)

**Focus**:
- Loading states
- Empty states
- Error states
- Long content (text overflow)
- Responsive breakpoints (mobile/tablet/desktop)
- Dark mode variants
- Hover/focus states
- Disabled states

**Example Expansion** (Button component):
```typescript
// Existing (1 story)
export const Default: Story = { args: { children: 'Click me' } };

// Add 6 more stories (Phase 4)
export const Loading: Story = { args: { loading: true } };
export const Disabled: Story = { args: { disabled: true } };
export const LongText: Story = { args: { children: 'Very long button text...' } };
export const Mobile: Story = { 
  parameters: { viewport: { defaultViewport: 'mobile1' } }
};
export const DarkMode: Story = { 
  parameters: { backgrounds: { default: 'dark' } }
};
export const WithIcon: Story = { 
  args: { icon: <CheckIcon />, children: 'Save' }
};
```

**Validation**:
- [ ] ≥50% coverage (43+ components with stories)
- [ ] Each story captures distinct visual state
- [ ] Chromatic detects 1px changes

---

### 🟢 Phase 5: Enable Blocking Mode (Week 4)

**Duration**: 1 week  
**Goal**: Make visual tests mandatory in CI  
**Target**: 0 regressions slip through

**Steps**:

1. **Update CI Workflow** (`.github/workflows/storybook-deploy.yml`):
```yaml
- name: Run Chromatic
  uses: chromaui/action@v1
  with:
    projectToken: ${{ secrets.CHROMATIC_PROJECT_TOKEN }}
    exitZeroOnChanges: false  # ← Change to false (fail on changes)
    exitOnceUploaded: false   # ← Wait for build completion
```

2. **Update chromatic.config.json**:
```json
{
  "exitZeroOnChanges": false,  // ← Fail CI on changes
  "autoAcceptChanges": false   // ← Require manual approval
}
```

3. **Add Branch Protection**:
   - Require Chromatic check to pass
   - Prevent merge if visual changes unapproved

4. **Team Training**:
   - Document visual review process
   - Create approval SOP
   - Train 2-3 team members as reviewers

**Validation**:
- [ ] CI fails when visual regression detected
- [ ] Team can approve changes in Chromatic UI
- [ ] PR merges only after approval
- [ ] No false positives in 10 test runs

---

## Coverage Goals

| Phase | Stories | Coverage | Status |
|-------|---------|----------|--------|
| Phase 1 | 87 | 0% | ✅ Complete |
| Phase 2 | 87 | 0% | 🟡 Pending Token |
| Phase 3 | 112+ | 30% | 🔴 Not Started |
| Phase 4 | 162+ | 50%+ | 🔵 Not Started |
| Phase 5 | 162+ | 50%+ | 🟢 Not Started |

**Final Target**: ≥50% coverage with blocking enabled

---

## Success Metrics

### Quantitative
- [ ] ≥162 story files (+75 from current 87)
- [ ] ≥50% component coverage (43+ out of 86 dirs)
- [ ] <5% false positive rate
- [ ] <2min average Chromatic build time
- [ ] 100% critical component coverage

### Qualitative
- [ ] Visual bugs caught in PR review
- [ ] Team confident in visual changes
- [ ] No manual visual QA needed for covered components
- [ ] Cross-browser rendering validated

---

## Risk Mitigation

### Risk 1: Chromatic Slow Builds
**Mitigation**: Already optimized with `onlyChanged: true`, `zip: true`

### Risk 2: Too Many False Positives
**Mitigation**: Use `chromatic.disableSnapshot()` for dynamic content:
```typescript
parameters: {
  chromatic: { 
    disableSnapshot: true  // Skip timestamp-based components
  }
}
```

### Risk 3: Team Not Reviewing
**Mitigation**: 
- Block merges without approval
- Assign 2-3 dedicated reviewers
- Add Chromatic dashboard to daily standup

### Risk 4: Cost (Chromatic Pricing)
**Plan**: 
- Free tier: 5,000 snapshots/month
- Estimated usage: ~1,000 snapshots/month (162 stories × 6 PR/week)
- Risk: Low (well within free tier)

---

## Component Priority Matrix

### Critical (Must Have in Phase 3)
- citations (AI response quality)
- search (core user flow)
- comments (collaboration feature)
- wizard (onboarding)
- versioning (diff viewer)

### Important (Phase 4)
- forms (data entry)
- progress (UX feedback)
- timeline (activity history)
- prompt (admin tool)
- diff (technical feature)

### Nice to Have (Future)
- providers (non-visual)
- accessible (utility, tested via E2E)

---

## Resources

**Documentation**:
- [Chromatic Setup Guide](./.storybook/CHROMATIC.md)
- [Storybook Best Practices](https://storybook.js.org/docs/react/writing-stories/introduction)
- [Visual Testing Guide](https://www.chromatic.com/docs/visual-testing-handbook)

**Tools**:
- Chromatic Dashboard: https://chromatic.com (after setup)
- Storybook Local: `pnpm storybook` (port 6006)

**Scripts**:
```bash
# Local development
pnpm storybook              # Start Storybook dev server
pnpm build-storybook        # Build static Storybook
pnpm chromatic              # Run visual tests locally

# CI mode
pnpm test:visual:ci         # Run in CI (non-blocking)
pnpm test:visual:debug      # Debug mode with verbose logs
```

---

## Next Steps (This Week)

### Immediate (Day 1)
1. **Create Chromatic Account** (5 min)
   - Go to [chromatic.com](https://www.chromatic.com/)
   - Sign in with GitHub
   - Link meepleai-monorepo

2. **Add Project Token** (2 min)
   - Copy token from Chromatic
   - Add to GitHub Secrets: `CHROMATIC_PROJECT_TOKEN`

3. **First Build** (10 min)
   ```bash
   export CHROMATIC_PROJECT_TOKEN=<token>
   cd apps/web
   pnpm chromatic
   ```

4. **Validate CI** (5 min)
   - Create test PR
   - Verify Chromatic comment appears
   - Check build passes

### This Week (Days 2-5)
5. **Write 10 Stories** (Phase 3 start)
   - Citations: 2 stories
   - Search: 2 stories
   - Comments: 3 stories
   - Wizard: 2 stories
   - Progress: 1 story

6. **Review First Visual Diff**
   - Make intentional CSS change
   - Trigger Chromatic build
   - Practice approving in UI

---

## Questions & Answers

**Q: Why 50% coverage, not 100%?**  
A: Diminishing returns. Critical 50% catches 80% of visual bugs. 100% coverage requires testing providers, utilities (not visual).

**Q: How long per story?**  
A: ~15-30 min average (setup + 3 states). Experienced developers: 10 min.

**Q: What about animations?**  
A: Use `chromatic.pauseAnimationAtEnd = true` parameter.

**Q: Can we test dark mode?**  
A: Yes! Use `parameters: { backgrounds: { default: 'dark' } }`.

**Q: What if Chromatic is down?**  
A: CI continues (non-blocking initially). Enable blocking only after 2 weeks stable.

---

**Status**: Ready to Start (Phase 2)  
**Owner**: Frontend Team  
**Timeline**: 4 weeks (Phases 2-5)  
**Budget**: $0 (free tier sufficient)

---

**Last Review**: 2025-12-10  
**Next Review**: After Phase 2 completion

