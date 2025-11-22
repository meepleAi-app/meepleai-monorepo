# 🎨 Design System & Frontend Refactor - Quick Start

**Status**: ✅ Complete and Ready to Use
**Created**: 2025-11-13

---

## 🎯 What Was Created

### 1. Complete Design System

**Location**: `apps/web/src/styles/design-tokens.css`

A comprehensive token system covering:
- ✅ Spacing (4px base unit, semantic names)
- ✅ Typography (system fonts, scale, weights)
- ✅ Colors (semantic + game-specific)
- ✅ Shadows & effects
- ✅ Border radius
- ✅ Transitions & animations
- ✅ Z-index layers
- ✅ Component-specific tokens
- ✅ Dark mode variants

**Documentation**: `docs/frontend/design-system.md` (800+ lines)

### 2. GitHub Issues Roadmap

**Location**: `.github-issues-templates/`

15 GitHub issues organized in 3 sprints:
- 🔴 **Sprint 1** (4 issues): Critical refactoring - ~5 days
- 🟡 **Sprint 2** (5 issues): Important improvements - ~3.5 days
- 🟢 **Sprint 3** (6 issues): Nice-to-have features - ~7 days

**Total Effort**: 3-4 weeks (1 developer)

---

## 🚀 Quick Start (5 Minutes)

### Step 1: Verify Design System is Active

The design tokens are already imported in `globals.css`:

```bash
# Check that design tokens are imported
cat apps/web/src/styles/globals.css | head -10
# Should see: @import "./design-tokens.css";
```

✅ **Already done!** The import is in place.

### Step 2: Create GitHub Issues

```bash
cd .github-issues-templates

# Preview what will be created (DRY RUN)
./create-issues.sh --dry-run

# Create all 15 issues
./create-issues.sh

# Or create by sprint
./create-issues.sh --sprint 1  # Only Sprint 1 (4 issues)
```

### Step 3: Create Project Board (Optional)

```bash
gh project create \
  --title "Frontend Refactor" \
  --body "MeepleAI frontend refactoring roadmap"
```

### Step 4: Start Working

```bash
# View issues
gh issue list --label "sprint-1"

# Assign to yourself
gh issue list --label "sprint-1" --json number --jq '.[].number' | \
  xargs -I {} gh issue edit {} --add-assignee @me

# Pick first issue
gh issue view 1  # Unify Login Flow
```

---

## 📖 Using the Design System

### Instead of Inline Styles

**Before** ❌:
```tsx
<div style={{ padding: 24, background: '#f8f9fa', borderRadius: 8 }}>
```

**After** ✅:
```tsx
<div className="p-6 bg-sidebar rounded-md">
```

### Common Patterns

```tsx
// Spacing
<div className="p-6 gap-4 space-y-3">

// Layout
<div className="flex flex-col items-center justify-between">

// Colors
<div className="bg-card text-foreground border border-border">

// Responsive
<div className="w-full md:w-80 lg:w-96">

// States
<button className="hover:bg-primary/90 focus-visible:ring-2 disabled:opacity-50">

// Dark mode (automatic)
<div className="bg-background dark:bg-card">
```

### Design Tokens Reference

| Need | Tailwind Class | Token |
|------|----------------|-------|
| Padding 24px | `p-6` | `--space-6` |
| Gap 16px | `gap-4` | `--space-4` |
| Width 320px | `w-80` | `--size-sidebar` |
| Rounded 8px | `rounded-md` | `--radius-md` |
| Shadow small | `shadow-sm` | `--shadow-sm` |
| Primary color | `bg-primary` | `--color-primary` |
| Transition | `transition-all duration-200` | `--transition-base` |

**Full reference**: `docs/frontend/design-system.md`

---

## 📋 Roadmap Summary

### Sprint 1: Critical (Start Here!)

| Issue | Description | Effort |
|-------|-------------|--------|
| #01 | **Unify Login Flow** - Remove auth duplication | 4h |
| #02 | **Refactor Upload Page** - 1564 lines → 400 lines | 2d |
| #03 | **Fix ChatProvider** - Split into 4 contexts | 1.5d |
| #04 | **Standardize Styling** - Remove 200+ inline styles | 1d |

**Impact**: Foundation for all other improvements

### Sprint 2: Important

| Issue | Description | Effort |
|-------|-------------|--------|
| #05 | Mobile improvements (drawer, touch targets) | 1d |
| #06 | Performance (memoization, virtualization) | 1d |
| #07 | Accessibility (WCAG 2.1 AA) | 0.5d |
| #08 | Error handling unified | 0.5d |
| #09 | Loading states unified | 0.5d |

**Impact**: Better UX and performance

### Sprint 3: Nice-to-have

| Issue | Description | Effort |
|-------|-------------|--------|
| #10 | Storybook setup | 2d |
| #11 | Component tests (95%+ coverage) | 1d |
| #12 | Landing page polish | 0.5d |
| #13 | Keyboard shortcuts (Cmd+K) | 1d |
| #14 | Advanced search | 1.5d |
| #15 | Theme customization | 1d |

**Impact**: Polish and DX improvements

---

## 🎯 Expected Improvements

| Metric | Before | After | Gain |
|--------|--------|-------|------|
| Largest file | 1564 lines | ~400 lines | **75% smaller** |
| Inline styles | 200+ | ~10 | **95% reduction** |
| ChatProvider | 639 lines | 250 lines | **61% smaller** |
| Bundle size | 450 KB | 350 KB | **22% smaller** |
| Re-renders | Baseline | -80% | **5x faster** |
| Mobile score | 6/10 | 9/10 | **50% better** |

---

## 📁 File Structure

```
meepleai-monorepo/
├── apps/web/src/
│   └── styles/
│       ├── design-tokens.css          ← NEW! Complete token system
│       └── globals.css                ← UPDATED (imports tokens)
├── docs/frontend/
│   └── design-system.md               ← NEW! 800-line guide
├── .github-issues-templates/
│   ├── README.md                      ← Instructions
│   ├── SUMMARY.md                     ← Roadmap overview
│   ├── create-issues.sh               ← Automation script
│   ├── sprint-1-critical/             ← 4 detailed issues
│   ├── sprint-2-important/            ← 5 concise issues
│   └── sprint-3-nice-to-have/         ← 6 concise issues
└── DESIGN-SYSTEM-QUICKSTART.md        ← This file
```

---

## 🛠️ Commands Cheat Sheet

### View Documentation

```bash
# Design system guide
cat docs/frontend/design-system.md

# Token definitions
cat apps/web/src/styles/design-tokens.css

# Roadmap summary
cat .github-issues-templates/SUMMARY.md
```

### Create Issues

```bash
cd .github-issues-templates

# Preview
./create-issues.sh --dry-run

# Create all
./create-issues.sh

# Create specific sprint
./create-issues.sh --sprint 1
./create-issues.sh --sprint 2
./create-issues.sh --sprint 3
```

### Manage Issues

```bash
# List all
gh issue list --label "frontend,refactor"

# List by sprint
gh issue list --label "sprint-1"
gh issue list --label "sprint-2"
gh issue list --label "sprint-3"

# List by priority
gh issue list --label "priority-critical"
gh issue list --label "priority-high"
gh issue list --label "priority-medium"

# View specific issue
gh issue view 1

# Assign to yourself
gh issue edit 1 --add-assignee @me
```

### Development

```bash
# Start dev server
cd apps/web
pnpm dev

# Run tests
pnpm test

# Run linter
pnpm lint

# Type check
pnpm typecheck

# Build
pnpm build
```

---

## ✅ Checklist

### Before You Start

- [ ] Read `docs/frontend/design-system.md`
- [ ] Run `./create-issues.sh --dry-run` to preview
- [ ] Create GitHub issues: `./create-issues.sh`
- [ ] Create project board (optional)
- [ ] Assign Sprint 1 issues to yourself

### During Development

- [ ] Use Tailwind classes instead of inline styles
- [ ] Reference design tokens (`docs/frontend/design-system.md`)
- [ ] Write tests for new components
- [ ] Run `pnpm test` before committing
- [ ] Create PR with issue reference

### After Each Issue

- [ ] Run full test suite
- [ ] Test in light + dark mode
- [ ] Test on mobile (Chrome DevTools)
- [ ] Update documentation if needed
- [ ] Close issue when merged

---

## 🎓 Learn More

### Design System

- **Tailwind CSS 4**: https://tailwindcss.com/docs
- **Shadcn/UI**: https://ui.shadcn.com
- **Radix UI**: https://radix-ui.com
- **Design Tokens**: `apps/web/src/styles/design-tokens.css`

### Testing

- **React Testing Library**: https://testing-library.com/react
- **Jest**: https://jestjs.io
- **Playwright**: https://playwright.dev

### Accessibility

- **WCAG 2.1**: https://www.w3.org/WAI/WCAG21/quickref/
- **axe DevTools**: https://www.deque.com/axe/devtools/
- **WebAIM**: https://webaim.org/resources/contrastchecker/

---

## 💡 Tips

### Using Design Tokens

1. **Always prefer semantic tokens**:
   - ✅ `bg-sidebar` (semantic)
   - ❌ `bg-gray-100` (specific shade)

2. **Use spacing scale consistently**:
   - ✅ `p-6 gap-4 space-y-3`
   - ❌ `p-[23px]` (arbitrary value)

3. **Responsive first**:
   - ✅ `w-full md:w-80`
   - ❌ `w-80 sm:w-full`

### Working on Issues

1. **Read the full issue** before starting
2. **Follow the implementation plan** exactly
3. **Write tests** as you go
4. **Test in both themes** (light + dark)
5. **Test on mobile** (Chrome DevTools)

### Common Mistakes to Avoid

- ❌ Adding new inline styles
- ❌ Hardcoding colors (#fff, rgb(), etc.)
- ❌ Using magic numbers (23px, 0.73rem)
- ❌ Skipping tests
- ❌ Not testing in dark mode

---

## 🐛 Troubleshooting

### Design tokens not working?

```bash
# Check import in globals.css
grep "design-tokens" apps/web/src/styles/globals.css

# Restart dev server
cd apps/web
pnpm dev
```

### Script not executable?

```bash
chmod +x .github-issues-templates/create-issues.sh
```

### GitHub CLI not authenticated?

```bash
gh auth login
```

### Can't create issues?

Make sure you have write access to the repository.

---

## 📞 Need Help?

1. **Design System Questions**: Check `docs/frontend/design-system.md`
2. **Issue Questions**: Read issue template carefully
3. **Technical Questions**: Create GitHub issue with `question` label
4. **Bugs**: Create GitHub issue with `bug` label

---

## 🎉 Ready to Go!

You now have:
- ✅ Complete design system with tokens
- ✅ Comprehensive documentation (800+ lines)
- ✅ 15 GitHub issues ready to create
- ✅ Automated creation script
- ✅ Clear roadmap (3-4 weeks)

**Next step**:

```bash
cd .github-issues-templates
./create-issues.sh
```

Happy coding! 🚀
