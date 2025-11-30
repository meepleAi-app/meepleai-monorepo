# Visual Testing Guide - Chromatic Integration

**Status**: ✅ Infrastructure Ready (Phase 1 Complete)
**Coverage**: 0% (Phase 2 - In Planning)
**Blocking**: No (Will enable at 50%+ coverage)

---

## Overview

Visual regression testing with Chromatic ensures UI components remain visually consistent across code changes. This guide covers the complete workflow for developers.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Workflow](#workflow)
3. [Development Process](#development-process)
4. [CI/CD Integration](#cicd-integration)
5. [Troubleshooting](#troubleshooting)
6. [Phase Roadmap](#phase-roadmap)

---

## Quick Start

### Prerequisites

1. **Chromatic Account**: Team access to MeepleAI Chromatic project
2. **GitHub Access**: PR permissions for visual review
3. **Local Environment**: Node.js 20+, pnpm 10+

### Run Visual Tests Locally

```bash
# Set your Chromatic token (one-time setup)
export CHROMATIC_PROJECT_TOKEN=<your-token>

# Run visual regression tests
cd apps/web
pnpm test:visual

# Debug mode (verbose output)
pnpm test:visual:debug
```

---

## Workflow

### 1. Developer Creates Component Changes

```bash
# Create feature branch
git checkout -b feature/update-button-component

# Make changes to component
# apps/web/src/components/ui/button.tsx

# Create/update story (if not exists)
# apps/web/src/components/ui/button.stories.tsx
```

### 2. Push Changes & Open PR

```bash
git add .
git commit -m "feat: update button hover state"
git push origin feature/update-button-component

# Open PR on GitHub
```

### 3. CI Runs Visual Tests

GitHub Actions automatically:
- ✅ Builds Storybook
- ✅ Uploads to Chromatic
- ✅ Compares with baseline
- ✅ Comments on PR with results

### 4. Review Visual Changes

**If no visual changes:**
```
✅ Visual Regression Testing
Status: No visual changes detected
```
→ **Action**: None, proceed with merge

**If visual changes detected:**
```
⚠️ Visual Regression Testing
Status: 3 visual change(s) detected - Review required
Chromatic Build: https://chromatic.com/build?appId=...
```
→ **Action**: Click link, review changes in Chromatic UI

### 5. Approve/Reject Changes

**In Chromatic UI:**
1. Review each changed story
2. Compare baseline vs new snapshot
3. **Accept** (intentional change) or **Reject** (bug)

**On Acceptance:**
- Chromatic updates baseline
- Future PRs compare against new baseline

**On Rejection:**
- Fix component locally
- Push new commit
- CI re-runs visual tests

---

## Development Process

### Creating Stories

**Priority**: Focus on reusable UI components first
- shadcn/ui components (Button, Input, Card, etc.)
- Custom shared components (GameCard, ChatMessage, etc.)

**Story Structure:**
```typescript
// apps/web/src/components/ui/button.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './button';

const meta: Meta<typeof Button> = {
  title: 'UI/Button',
  component: Button,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'ghost'],
    },
  },
};

export default meta;
type Story = StoryObj<typeof Button>;

export const Default: Story = {
  args: {
    children: 'Click me',
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
    children: 'Cancel',
    variant: 'outline',
  },
};
```

**Best Practices:**
- ✅ Use fixed data (no `Math.random()`, `Date.now()`)
- ✅ Mock external dependencies (API calls)
- ✅ One component per story file
- ✅ Cover all variants/states
- ❌ No dynamic dates or randomized content

### Local Testing

```bash
# Start Storybook dev server
pnpm storybook

# In another terminal, run visual tests
pnpm test:visual

# View results in browser
# Opens Chromatic dashboard automatically
```

---

## CI/CD Integration

### GitHub Actions Workflow

**File**: `.github/workflows/ci.yml`

**Job**: `ci-visual-regression`

**Triggers**:
- Pull requests (web/** file changes)
- Push to main
- Manual workflow dispatch
- Nightly schedule

**Configuration**:
```yaml
ci-visual-regression:
  needs: changes
  name: Web - Visual Regression Tests (Chromatic)
  runs-on: ubuntu-latest
  if: needs.changes.outputs.web == 'true'
  steps:
    - name: Publish to Chromatic
      uses: chromaui/action@v13
      with:
        projectToken: ${{ secrets.CHROMATIC_PROJECT_TOKEN }}
        onlyChanged: true              # Only test changed stories
        exitZeroOnChanges: true        # NON-BLOCKING (Phase 1)
        exitOnceUploaded: true         # Fast exit for CI
        autoAcceptChanges: false       # Require manual review
```

### Current Behavior (Phase 1)

**Non-Blocking Mode**:
- ✅ Visual tests run automatically
- ✅ PR comments show results
- ⚠️ **Does NOT block merge** on visual changes
- ✅ Team gets visibility without friction

**Rationale**:
- Current Storybook coverage: 0%
- Blocking mode would create false negatives
- Infrastructure must be in place before enforcement

### Future Behavior (Phase 2+)

**Blocking Mode** (when coverage >50%):
```yaml
exitZeroOnChanges: false        # BLOCKING: Fail on unapproved changes
continue-on-error: false        # Block merge on failures
```

---

## Troubleshooting

### Issue: "No stories found"

**Cause**: Storybook build failed or no .stories.tsx files

**Fix**:
```bash
# Verify Storybook builds locally
pnpm build-storybook

# Check for stories
find src -name "*.stories.tsx"
```

### Issue: "Token not found"

**Cause**: `CHROMATIC_PROJECT_TOKEN` not configured

**Fix**:
```bash
# Local: Set environment variable
export CHROMATIC_PROJECT_TOKEN=<your-token>

# CI: Verify GitHub secret exists
# Settings → Secrets → CHROMATIC_PROJECT_TOKEN
```

### Issue: "Too many visual changes"

**Cause**: Global CSS change affecting many components

**Fix**:
1. Review changes in Chromatic UI
2. If intentional (e.g., theme update):
   - Approve all changes in Chromatic
   - Baseline updates automatically
3. If unintentional:
   - Revert CSS change
   - Push new commit

### Issue: "Build timeout"

**Cause**: Storybook build taking >10 minutes

**Fix**:
```bash
# Check for infinite loops in stories
# Check for heavy computations in story args

# Optimize Storybook config
# .storybook/main.ts - disable unnecessary addons
```

### Issue: "Chromatic UI shows no diff but CI fails"

**Cause**: Chromatic API intermittent issue

**Fix**:
```bash
# Re-run CI job
# GitHub PR → Checks → Re-run jobs

# Or push empty commit to trigger new build
git commit --allow-empty -m "chore: re-trigger CI"
git push
```

---

## Phase Roadmap

### ✅ Phase 1: Infrastructure Setup (COMPLETE)

**Deliverables**:
- ✅ CI/CD workflow configured
- ✅ npm scripts available
- ✅ Documentation complete
- ✅ Non-blocking mode active

**Timeline**: Complete (2025-11-30)

### 📋 Phase 2: Storybook Coverage (IN PLANNING)

**Target**: 50%+ component coverage (50+ stories)

**Priorities**:
1. **shadcn/ui components** (20 stories):
   - Button, Input, Card, Select, Dialog, etc.
2. **Custom shared components** (20 stories):
   - GameCard, ChatMessage, PdfPreview, etc.
3. **Form components** (10 stories):
   - FormField, FormControl, FormError, etc.

**Estimated Effort**: 2-3 sprints

**Acceptance Criteria**:
- [ ] 50+ stories created
- [ ] All stories render without errors
- [ ] Coverage tracking dashboard
- [ ] Team training complete

### 🔒 Phase 3: Enable Blocking Mode (FUTURE)

**Prerequisites**:
- Phase 2 complete (50%+ coverage)
- Team comfortable with workflow
- Low false positive rate (<5%)

**Changes**:
- Set `exitZeroOnChanges: false`
- Remove `continue-on-error: true`
- Visual changes BLOCK merge

**Timeline**: After Phase 2 completion

---

## npm Scripts Reference

| Script | Purpose | Use Case |
|--------|---------|----------|
| `pnpm storybook` | Start dev server | Local development |
| `pnpm build-storybook` | Build static files | CI/manual verification |
| `pnpm test:visual` | Run visual tests | Pre-commit local check |
| `pnpm test:visual:ci` | CI mode (fast exit) | GitHub Actions only |
| `pnpm test:visual:debug` | Debug mode | Troubleshooting issues |

---

## Configuration Files

### chromatic.config.json

```json
{
  "buildScriptName": "build-storybook",
  "projectId": "Project:meepleai",
  "onlyChanged": true,              // Only test changed stories
  "exitZeroOnChanges": true,        // Non-blocking (Phase 1)
  "exitOnceUploaded": true,         // Fast CI exit
  "autoAcceptChanges": "main",      // Auto-approve main branch
  "skip": "dependabot/**",          // Skip bot PRs
  "zip": true                       // Compress uploads
}
```

### .storybook/main.ts

```typescript
const config: StorybookConfig = {
  stories: [
    '../src/**/*.mdx',
    '../src/**/*.stories.@(js|jsx|mjs|ts|tsx)',
  ],
  addons: [
    '@chromatic-com/storybook',     // Chromatic integration
    '@storybook/addon-docs',
    '@storybook/addon-a11y',
    // ...
  ],
};
```

---

## Additional Resources

- [Chromatic Documentation](https://www.chromatic.com/docs/)
- [Storybook Best Practices](https://storybook.js.org/docs/writing-stories)
- [Visual Testing Philosophy](https://www.chromatic.com/blog/visual-testing-best-practices/)
- [MeepleAI Testing Strategy](./test-writing-guide.md)

---

## Support & Questions

**Chromatic Issues**:
- Check [Chromatic Status](https://status.chromatic.com/)
- Review [GitHub Issues](https://github.com/chromaui/chromatic-cli/issues)

**Internal Questions**:
- Ask in #frontend Slack channel
- Review PR comments for examples
- Check [test-writing-guide.md](./test-writing-guide.md)

---

**Document Version**: 1.0
**Last Updated**: 2025-11-30
**Maintained By**: Frontend Team
