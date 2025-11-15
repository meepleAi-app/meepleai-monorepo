# Storybook Documentation - MeepleAI

## Overview

Storybook v10.0.7 is configured for interactive component development and documentation. This setup includes **32 documented components** with **~150 story variants** showcasing all UI states.

## Quick Start

```bash
# Development server (http://localhost:6006)
pnpm storybook

# Build static site
pnpm build-storybook

# Output: storybook-static/
```

## What's Documented

### UI Components (22 Shadcn/UI)
- **Forms**: Button, Input, Textarea, Select, Checkbox, Switch, Label
- **Layout**: Card, Separator, Sheet, Tabs, Dialog
- **Display**: Alert, Avatar, Badge, Progress, Skeleton, Table
- **Interactive**: Dropdown Menu, Toggle, Toggle Group, Sonner (toasts)

### Custom Components (10 Components)
- **Loading**: LoadingButton, MessageAnimator, SkeletonLoader, TypingIndicator
- **Chat**: AgentSelector, GameSelector, ChatHistoryItem
- **Diff**: DiffCodeBlock, DiffToolbar
- **Game**: GamePicker

## Features

### ✅ Dark Mode Support
- Auto light/dark theme toggle via `@storybook/addon-themes`
- Integrated with project's `next-themes` setup
- All stories tested in both modes

### ♿ Accessibility Testing
- `@storybook/addon-a11y` enabled for all stories
- WCAG compliance checks built-in
- Color contrast validation (configurable)

### 📚 Auto Documentation
- TypeScript props automatically extracted
- JSDoc comments → component docs
- Interactive controls for all props
- Usage examples for each variant

### 🎨 Component Variants
Each component includes multiple stories:
- **Default** state
- **Size variants** (small, default, large)
- **Style variants** (primary, secondary, destructive, outline)
- **State variants** (disabled, loading, error)
- **Interactive examples** (forms, real-world usage)

## Configuration

### Main Config (`.storybook/main.ts`)
```typescript
{
  stories: ['../src/**/*.stories.@(js|jsx|mjs|ts|tsx)'],
  addons: [
    '@chromatic-com/storybook',    // Visual testing
    '@storybook/addon-docs',        // Documentation
    '@storybook/addon-a11y',        // Accessibility
    '@storybook/addon-themes',      // Dark mode
    '@storybook/addon-onboarding',  // First-time setup
    '@storybook/addon-vitest',      // Testing integration
  ],
  framework: '@storybook/nextjs',   // Next.js 16 support
  docs: { autodocs: 'tag' },        // Auto-generate docs
}
```

### Preview Config (`.storybook/preview.tsx`)
- Tailwind CSS imported globally
- Dark mode decorator configured
- Accessibility rules customized
- Default layout: `centered`

## Writing Stories

### CSF 3 Format (Component Story Format)
```typescript
import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './button';

const meta = {
  title: 'UI/Button',
  component: Button,
  parameters: { layout: 'centered' },
  tags: ['autodocs'],
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    children: 'Click me',
  },
};

export const Destructive: Story = {
  args: {
    variant: 'destructive',
    children: 'Delete',
  },
};
```

### Best Practices
1. **Use TypeScript** for type safety
2. **Add JSDoc comments** for prop documentation
3. **Include realistic examples** (not just basic props)
4. **Test all states** (loading, error, disabled)
5. **Mock dependencies** (providers, API calls)
6. **Use decorators** for context providers

### Provider Mocking Example
```typescript
const meta = {
  component: YourComponent,
  decorators: [
    (Story) => (
      <MockProvider>
        <Story />
      </MockProvider>
    ),
  ],
} satisfies Meta<typeof YourComponent>;
```

## Deploy

### Vercel (Recommended for Next.js)
```bash
# Automatic deployment on PR merge
# Storybook will be built and deployed to:
# https://<your-app>-storybook.vercel.app

# Manual deploy:
vercel --prod storybook-static/
```

### Chromatic (Visual Testing)
```bash
# Install Chromatic
pnpm add -D chromatic

# Deploy + visual regression testing
npx chromatic --project-token=<YOUR_TOKEN>

# CI/CD integration
# Add CHROMATIC_PROJECT_TOKEN to GitHub Secrets
```

## CI/CD Integration

### GitHub Actions (Suggested Workflow)
```yaml
name: Storybook CI

on: [push, pull_request]

jobs:
  storybook:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v2
      - run: pnpm install
      - run: pnpm build-storybook
      - uses: chromaui/action@v1
        with:
          projectToken: ${{ secrets.CHROMATIC_PROJECT_TOKEN }}
```

## Troubleshooting

### Issue: `@storybook/test` peer dependency warning
**Cause**: `@storybook/test@8.6.14` vs `storybook@10.0.7` mismatch
**Impact**: Minimal - works with warnings
**Fix**: Wait for `@storybook/test@10.x` release or ignore

### Issue: TypeScript check memory limit
**Cause**: Large codebase + TypeScript checking
**Fix**: Disable `typescript.check` in `main.ts` if needed:
```typescript
typescript: {
  check: false, // Disable if memory issues occur
}
```

### Issue: Stories not showing up
**Check**:
1. File naming: `*.stories.tsx` ✅
2. Default export present ✅
3. File in `src/` directory ✅
4. Cache cleared: `rm -rf node_modules/.cache`

## Performance

- **Dev server startup**: ~30-60 seconds (first run)
- **Hot reload**: <2 seconds per story change
- **Build time**: ~2-3 minutes (32 components)
- **Output size**: ~15-20MB (storybook-static/)

## Coverage

| Category | Components | Stories | Variants |
|----------|-----------|---------|----------|
| **Shadcn UI** | 22 | 22 | ~60 |
| **Custom** | 10 | 10 | ~91 |
| **TOTAL** | **32** | **32** | **~151** |

## Next Steps

**To expand Storybook coverage:**
1. Add stories for complex chat components (Message, MessageInput)
2. Document timeline components (TimelineEventItem, TimelineFilters)
3. Add PDF components (PdfUploadForm, PdfTable)
4. Document editor components (RichTextEditor, EditorToolbar)
5. Create integration stories (full workflows, multi-component)

## Resources

- [Storybook Docs](https://storybook.js.org/docs)
- [Next.js Integration](https://storybook.js.org/docs/get-started/frameworks/nextjs)
- [Component Story Format (CSF)](https://storybook.js.org/docs/api/csf)
- [Chromatic Docs](https://www.chromatic.com/docs)

---

**Status**: Production-ready ✅
**Version**: Storybook 10.0.7
**Framework**: Next.js 16.0.1 + React 19.2.0
**Last Updated**: 2025-11-15
