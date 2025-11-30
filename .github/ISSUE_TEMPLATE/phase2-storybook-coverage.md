---
name: "[P2] Phase 2: Storybook Coverage - Visual Testing Enablement"
about: Create 50+ Storybook stories to enable visual regression testing
title: "[P2] Phase 2: Storybook Coverage - Visual Testing Enablement"
labels: 'enhancement, frontend, testing, priority-medium, area/ui'
assignees: ''
---

## Priority: P2 - Medium
**Scope**: Create comprehensive Storybook coverage to enable visual regression testing

## Description

Phase 2 of visual testing implementation: Create stories for UI components to reach 50%+ coverage threshold required for blocking visual regression tests.

**Context**:
- ✅ Phase 1 Complete: Infrastructure setup, CI integration, documentation
- ❌ Current Coverage: 0% (0 stories exist)
- 🎯 Target Coverage: 50%+ (50+ stories)
- 🔒 Outcome: Enable blocking mode for visual regression tests

## Prerequisites

✅ Phase 1 complete:
- CI/CD workflow configured
- Chromatic integration active
- npm scripts available
- Documentation complete

## Scope

### 1. shadcn/ui Components (Priority 1) - 20 stories

**Location**: `apps/web/src/components/ui/*.tsx`

Components to cover:
- [ ] Button (variants: default, destructive, outline, ghost, link, sizes)
- [ ] Input (states: default, disabled, error, with icon)
- [ ] Card (variants: default, with header, with footer, interactive)
- [ ] Select (states: default, disabled, with placeholder, multi-select)
- [ ] Dialog (variants: default, with form, confirmation, alert)
- [ ] Dropdown Menu (nested, with icons, with separators)
- [ ] Progress (variants: default, with label, indeterminate)
- [ ] Skeleton (shapes: text, circular, rectangular)
- [ ] Switch (states: default, disabled, with label)
- [ ] Table (variants: default, sortable, with pagination)
- [ ] Textarea (states: default, disabled, with counter)
- [ ] Toggle (variants: default, outline, with icon)
- [ ] Alert (variants: default, destructive, warning, info)
- [ ] Label (variants: default, required, with tooltip)
- [ ] Separator (orientations: horizontal, vertical)
- [ ] Tabs (variants: default, with icons, vertical)
- [ ] Checkbox (states: default, disabled, indeterminate)
- [ ] Badge (variants: default, secondary, destructive, outline)
- [ ] Avatar (variants: default, with fallback, with image, sizes)
- [ ] Sheet (variants: default, with form, with scrollable content)

### 2. Custom Shared Components (Priority 2) - 20 stories

**Location**: `apps/web/src/components/**/*.tsx`

Components to cover:
- [ ] GameCard (states: default, loading, error, interactive)
- [ ] GameList (variants: grid, list, empty state)
- [ ] GameSearchBar (states: default, with results, loading)
- [ ] PdfPreview (states: default, loading, error, paginated)
- [ ] PdfTable (variants: default, sortable, with filters)
- [ ] PdfTableRow (states: default, selected, hover, actions)
- [ ] ChatMessage (variants: user, assistant, system, error)
- [ ] CitationList (states: default, loading, empty, expanded)
- [ ] UploadQueue (states: default, uploading, completed, error)
- [ ] UploadQueueItem (states: pending, uploading, success, error)
- [ ] UploadSummary (states: default, with stats, empty)
- [ ] ErrorDisplay (variants: default, with retry, with details)
- [ ] ErrorBoundary (fallback UI variants)
- [ ] LoadingButton (states: default, loading, disabled)
- [ ] ThemeSwitcher (variants: default, compact)
- [ ] CommandPalette (states: default, with results, empty)
- [ ] CommentThread (states: default, collapsed, with replies)
- [ ] DiffViewer (modes: split, unified, syntax highlighted)
- [ ] VersionTimeline (variants: compact, detailed, with filters)
- [ ] WizardSteps (states: default, with validation, completed)

### 3. Form Components (Priority 3) - 10 stories

**Location**: `apps/web/src/components/forms/*.tsx`

Components to cover:
- [ ] Form (variants: default, with validation, multi-step)
- [ ] FormField (states: default, error, disabled, required)
- [ ] FormControl (variants: default, with label, with description)
- [ ] FormLabel (variants: default, required, with tooltip)
- [ ] FormDescription (states: default, with icon)
- [ ] FormError (variants: default, inline, with icon)
- [ ] AccessibleFormInput (states: all accessibility states)
- [ ] RegisterForm (states: default, submitting, error, success)
- [ ] OAuthButtons (variants: default, loading, error)
- [ ] DemoCredentialsHint (variants: default, expanded)

## Story Structure Template

```typescript
// Example: apps/web/src/components/ui/button.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './button';

const meta: Meta<typeof Button> = {
  title: 'UI/Button',
  component: Button,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'ghost', 'link'],
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
    },
  },
};

export default meta;
type Story = StoryObj<typeof Button>;

// Basic variants
export const Default: Story = {
  args: {
    children: 'Button',
    variant: 'default',
  },
};

export const Destructive: Story = {
  args: {
    children: 'Delete',
    variant: 'destructive',
  },
};

export const Outline: Story = {
  args: {
    children: 'Outline',
    variant: 'outline',
  },
};

// States
export const Disabled: Story = {
  args: {
    children: 'Disabled',
    disabled: true,
  },
};

export const Loading: Story = {
  args: {
    children: 'Loading',
    disabled: true,
  },
  render: (args) => <Button {...args}>Loading...</Button>,
};

// Sizes
export const Small: Story = {
  args: {
    children: 'Small',
    size: 'sm',
  },
};

export const Large: Story = {
  args: {
    children: 'Large',
    size: 'lg',
  },
};

// With icons
export const WithIcon: Story = {
  args: {
    children: (
      <>
        <Icon className="mr-2" />
        With Icon
      </>
    ),
  },
};
```

## Best Practices

### Data Consistency
✅ **Do**:
- Use fixed data (no `Math.random()`, `Date.now()`)
- Mock external dependencies (API calls, localStorage)
- Use consistent test data across stories

❌ **Don't**:
- Use dynamic dates or randomized content
- Make real API calls in stories
- Depend on global state

### Story Organization
✅ **Do**:
- One component per story file
- Cover all variants and states
- Use descriptive story names
- Include accessibility scenarios

❌ **Don't**:
- Mix multiple components in one story file
- Skip error states
- Ignore responsive breakpoints

### Component Isolation
✅ **Do**:
- Mock external dependencies
- Use Storybook decorators for providers
- Test component in isolation

❌ **Don't**:
- Require full application context
- Depend on route parameters
- Use production API endpoints

## Acceptance Criteria

### Coverage Target
- [ ] 50+ stories created across priority 1-3 components
- [ ] All stories render without errors in Storybook
- [ ] Coverage tracking dashboard implemented
- [ ] No console errors or warnings in stories

### Quality Gates
- [ ] All stories follow naming conventions (`ComponentName.stories.tsx`)
- [ ] All stories use TypeScript
- [ ] All stories have autodocs enabled
- [ ] All stories use argTypes for controls

### Documentation
- [ ] README added to `.storybook/` with team guidelines
- [ ] Contributing guide for new stories
- [ ] Examples of common patterns (forms, modals, etc.)
- [ ] Migration guide from Phase 1 to Phase 2

### CI/CD Integration
- [ ] Storybook builds successfully in CI
- [ ] Chromatic detects all new stories
- [ ] No visual regression failures on baseline setup
- [ ] Coverage metrics tracked in CI

### Team Enablement
- [ ] Team training session completed
- [ ] Story writing guidelines shared
- [ ] Example stories reviewed in team meeting
- [ ] Q&A session with frontend team

## Estimated Effort

**Total**: 2-3 sprints (4-6 weeks)

**Breakdown**:
- Priority 1 (shadcn/ui): 1 sprint (20 components)
- Priority 2 (Custom): 1 sprint (20 components)
- Priority 3 (Forms): 0.5 sprint (10 components)
- Documentation & Training: 0.5 sprint

**Per Component**: ~1-2 hours (including testing)

## Success Metrics

### Quantitative
- Story count: 50+ stories
- Coverage: 50%+ of UI components
- Build time: Storybook builds in <2 minutes
- Zero errors in Storybook console

### Qualitative
- Team comfortable writing stories
- Visual regression catches real bugs
- PR review process includes visual checks
- Documentation used by team

## Dependencies

**Required**:
- Phase 1 complete ✅
- Chromatic token configured ✅
- Frontend team availability
- Design system documentation (for reference)

**Blockers**:
- None identified

## Next Steps (Phase 3)

After 50%+ coverage achieved:
1. Enable blocking mode (`exitZeroOnChanges: false`)
2. Update CI workflow to fail on visual changes
3. Establish visual review SLA (24-48 hours)
4. Monitor false positive rate (<5% target)
5. Expand coverage to 80%+ (optional)

## Resources

- [Visual Testing Guide](../../../docs/02-development/testing/visual-testing-guide.md)
- [Chromatic Documentation](https://www.chromatic.com/docs/)
- [Storybook Best Practices](https://storybook.js.org/docs/writing-stories)
- [shadcn/ui Components](https://ui.shadcn.com/)

## Notes

- **Non-Blocking Mode**: Visual tests remain non-blocking until Phase 2 complete
- **Incremental Delivery**: Stories can be added incrementally, no need to wait for all 50
- **Team Collaboration**: Encourage pair programming for first few stories
- **Quality over Speed**: Focus on quality stories that provide value

---

**Phase**: 2 of 3
**Previous**: Phase 1 - Infrastructure Setup ✅
**Next**: Phase 3 - Enable Blocking Mode
**Tracking**: Visual Testing Epic
