# Visual Regression Tests

Visual regression tests for MeepleAI admin dashboard components.

## Quick Commands

```bash
# Run visual tests (desktop Chrome only - fast)
pnpm test:e2e:visual

# Run with UI mode (inspect failures)
pnpm test:e2e:visual:ui

# Update baselines after intentional changes
pnpm test:e2e:visual:update

# Run on all browsers/viewports (comprehensive)
pnpm test:e2e:visual:all
```

## What Gets Tested

- Admin dashboard overview (desktop, tablet, mobile)
- MetricsGrid components
- Charts (all 4 chart types)
- Navigation & Top Bar
- Interactive states (hover, loading)
- Background texture system
- Accessibility (reduced motion)

## Baselines Location

Screenshots stored in: `e2e/visual/__screenshots__/`

**Important**: Baselines are committed to git for team collaboration.

## Documentation

Full documentation: [docs/05-testing/visual-regression.md](../../docs/05-testing/visual-regression.md)

## Issue

Created in: Issue #2906
Part of: Epic #2845 - MeepleAI Design System Integration
