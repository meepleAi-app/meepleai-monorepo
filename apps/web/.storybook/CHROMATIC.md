# Chromatic Visual Testing - Setup Guide

## Overview

Chromatic provides **visual regression testing** for Storybook components, enabling teams to catch unintended UI changes before they reach production.

## Status: ✅ Fully Configured

All files and configurations are in place. Only the **Chromatic project token** is required to start using it.

## Quick Setup (5 minutes)

### 1. Create Chromatic Account
Visit [chromatic.com](https://www.chromatic.com/) and sign in with GitHub.

### 2. Create Project
1. Click "Add Project"
2. Select this repository
3. Copy the **Project Token**

### 3. Configure GitHub Secret
```bash
# Go to: Settings → Secrets and variables → Actions
# Click: New repository secret
Name: CHROMATIC_PROJECT_TOKEN
Value: <paste-your-token>
```

### 4. Update Project ID (Optional)
Edit `chromatic.config.json`:
```json
{
  "projectId": "Project:your-project-id"
}
```

### 5. Test Locally (Optional)
```bash
# Set token
export CHROMATIC_PROJECT_TOKEN=<your-token>

# Run visual tests
cd apps/web
pnpm chromatic
```

## What's Already Configured

### ✅ Package Installed
```json
// package.json
"chromatic": "^13.3.4"
```

### ✅ Scripts Available
```bash
pnpm chromatic        # Local visual testing
pnpm chromatic:ci     # CI mode (exits after upload)
```

### ✅ Config File
`chromatic.config.json` with optimized settings:
- ✅ Only test changed components
- ✅ Auto-accept changes on main branch
- ✅ Skip Dependabot PRs
- ✅ Compression enabled
- ✅ External files excluded

### ✅ Storybook Addon
```typescript
// .storybook/main.ts
addons: [
  '@chromatic-com/storybook',  // ← Chromatic integration
  // ...
]
```

### ✅ GitHub Actions Workflow
`.github/workflows/storybook-deploy.yml`:
- Triggers on component changes
- Builds Storybook
- Uploads to Chromatic
- Posts PR comments

## How It Works

### Automatic (CI/CD)
1. Developer creates PR with component changes
2. GitHub Actions builds Storybook
3. Chromatic compares screenshots with baseline
4. PR comment shows visual changes
5. Team reviews and approves/rejects

### Manual (Local)
```bash
# Test visual changes locally
pnpm chromatic

# View results
# → Opens browser with Chromatic dashboard
```

## Features

### Visual Regression Testing
- **Pixel-perfect diffs**: Catch 1px changes
- **Smart baselines**: Automatically updates on main
- **Multi-browser**: Test Chrome, Firefox, Safari
- **Responsive**: Test different viewports

### UI Review
- **Collaborate**: Team reviews visual changes
- **Annotate**: Comment on specific components
- **Approve/Reject**: Control what merges
- **History**: Track component evolution

### Performance
- **Incremental**: Only tests changed stories
- **Parallel**: Runs stories concurrently
- **Fast**: ~2-5 minutes for 150 stories
- **Cached**: Reuses builds when possible

## Usage Examples

### Scenario 1: New Component
```bash
# 1. Create component + story
# 2. Push to PR branch
# 3. Chromatic captures baseline
# 4. Team reviews in PR
# 5. Merge → Baseline set
```

### Scenario 2: Component Update
```bash
# 1. Modify existing component
# 2. Push to PR branch
# 3. Chromatic shows diff vs baseline
# 4. If intended: Approve in Chromatic
# 5. If bug: Fix and re-run
```

### Scenario 3: Breaking Change Detection
```bash
# 1. Update shared utility (e.g., theme)
# 2. Chromatic shows ALL affected components
# 3. Review impact across app
# 4. Decide: Accept or revert
```

## Troubleshooting

### Issue: "Missing project token"
**Solution**: Add `CHROMATIC_PROJECT_TOKEN` to GitHub Secrets (see Quick Setup step 3)

### Issue: "Build failed - No stories found"
**Solution**: Ensure Storybook builds locally first:
```bash
pnpm build-storybook
```

### Issue: "Too many visual changes"
**Solution**: Approve baseline in Chromatic UI, or run locally to debug:
```bash
pnpm chromatic --auto-accept-changes
```

### Issue: "Slow uploads"
**Solution**: Already optimized with:
- `onlyChanged: true` (only changed files)
- `zip: true` (compression enabled)
- `untraced` (excludes node_modules, .next, etc.)

## Best Practices

### 1. Review Visual Changes Carefully
- ✅ **Do**: Check cross-browser rendering
- ✅ **Do**: Test responsive breakpoints
- ❌ **Don't**: Auto-approve without review

### 2. Keep Stories Isolated
- ✅ **Do**: One component per story
- ✅ **Do**: Mock external dependencies
- ❌ **Don't**: Include API calls in stories

### 3. Use Consistent Data
- ✅ **Do**: Use fixed dates, text, images
- ❌ **Don't**: Use `Math.random()` or `Date.now()`

### 4. Organize Stories Logically
```typescript
// ✅ Good
export default {
  title: 'Components/Button',
  component: Button,
}

// ❌ Bad
export default {
  title: 'Button',
  component: Button,
}
```

## Configuration Reference

### chromatic.config.json
```json
{
  "buildScriptName": "build-storybook",
  "projectId": "Project:meepleai",
  "onlyChanged": true,              // Test only changed stories
  "exitZeroOnChanges": true,        // Don't fail CI on changes
  "exitOnceUploaded": true,         // Exit after upload (faster CI)
  "autoAcceptChanges": "main",      // Auto-approve main branch
  "skip": "dependabot/**",          // Skip dependency PRs
  "zip": true                       // Compress uploads
}
```

### Available CLI Options
```bash
# Build options
--build-script-name        # Custom build command
--storybook-build-dir      # Pre-built Storybook directory

# Testing options
--only-changed             # Test only changed stories
--only-story-names         # Test specific stories
--untraced                 # Ignore file patterns

# CI options
--exit-zero-on-changes     # Don't fail on visual changes
--exit-once-uploaded       # Exit after upload
--auto-accept-changes      # Auto-approve changes

# Debug options
--debug                    # Verbose logging
--dry-run                  # Simulate without uploading
```

## Resources

- [Chromatic Docs](https://www.chromatic.com/docs/)
- [Storybook + Chromatic Guide](https://storybook.js.org/tutorials/design-systems-for-developers/react/en/review/)
- [Visual Testing Best Practices](https://www.chromatic.com/blog/visual-testing-best-practices/)
- [GitHub Action Docs](https://github.com/chromaui/action)

## Support

For issues or questions:
1. Check [Chromatic Status](https://status.chromatic.com/)
2. Review [GitHub Issues](https://github.com/chromaui/chromatic-cli/issues)
3. Ask in Storybook Discord: [discord.gg/storybook](https://discord.gg/storybook)

---

**Status**: Production-ready ✅
**Version**: chromatic@13.3.4
**Last Updated**: 2025-11-19
