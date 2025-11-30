# Storybook Configuration

This directory contains the Storybook configuration for the MeepleAI frontend components.

## Files

- **`main.ts`**: Main Storybook configuration
  - Story file patterns
  - Addon configurations
  - Framework setup (Next.js)
  - TypeScript settings
  - Documentation autogeneration

- **`preview.tsx`**: Preview configuration
  - Global styles (Tailwind CSS)
  - Dark mode setup
  - Accessibility settings
  - Default parameters

## Configuration Details

### Framework
- **Storybook**: v10.0.7
- **Framework**: `@storybook/nextjs` (Next.js 16.0.1)
- **Builder**: Webpack 5
- **Compiler**: SWC

### Addons
| Addon | Purpose |
|-------|---------|
| `@chromatic-com/storybook` | Visual testing and regression |
| `@storybook/addon-docs` | Auto-generated documentation |
| `@storybook/addon-a11y` | Accessibility testing |
| `@storybook/addon-themes` | Dark/light mode switching |
| `@storybook/addon-onboarding` | First-time user experience |
| `@storybook/addon-vitest` | Vitest integration |

### TypeScript
- **Type checking**: Enabled during build
- **React docgen**: `react-docgen-typescript`
- **Prop extraction**: Automatic for documented components
- **Node modules filter**: Excludes third-party props

## Customization

### Adding New Addons
```bash
cd apps/web
pnpm add -D @storybook/addon-<name>
```

Then update `main.ts`:
```typescript
addons: [
  // ... existing addons
  '@storybook/addon-<name>',
],
```

### Modifying Theme
Edit `preview.tsx` to adjust dark mode behavior:
```typescript
decorators: [
  withThemeByClassName({
    themes: {
      light: 'light',
      dark: 'dark',
    },
    defaultTheme: 'dark', // Change default theme
  }),
],
```

### Adjusting Story Patterns
Modify `main.ts` to change where stories are discovered:
```typescript
stories: [
  '../src/**/*.stories.@(js|jsx|mjs|ts|tsx)',
  '../stories/**/*.stories.mdx', // Add MDX stories
],
```

## Troubleshooting

### Cache Issues
```bash
rm -rf node_modules/.cache storybook-static
pnpm storybook
```

### Type Errors
Temporarily disable type checking:
```typescript
// main.ts
typescript: {
  check: false, // Disable if build fails on types
}
```

### Addon Compatibility
Check `pnpm storybook doctor` for version mismatches.

## Visual Testing (Chromatic)

**Status**: ✅ Phase 1 Complete (Infrastructure)

### Current State
- **Coverage**: 0% (no stories yet)
- **Mode**: Non-blocking (CI runs but doesn't block merge)
- **Phase 2**: [Issue #1823](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1823) - Create 50+ stories
- **Phase 3**: Enable blocking mode at 50%+ coverage

### Usage
```bash
# Run visual regression tests locally
pnpm test:visual

# Debug visual tests
pnpm test:visual:debug

# CI automatically runs on PR
```

### Documentation
- **Primary**: [Visual Testing Guide](../../../docs/02-development/testing/visual-testing-guide.md)
- **Setup**: [CHROMATIC.md](./CHROMATIC.md) (legacy, use Visual Testing Guide)

## Resources

- [Storybook Next.js Docs](https://storybook.js.org/docs/get-started/frameworks/nextjs)
- [Addon Catalog](https://storybook.js.org/integrations)
- [CSF Format](https://storybook.js.org/docs/api/csf)
- [Visual Testing Guide](../../../docs/02-development/testing/visual-testing-guide.md)
- [Chromatic Documentation](https://www.chromatic.com/docs/)
