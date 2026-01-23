# Parallel Branch Strategy - Site-Wide Redesign

**Purpose**: Execute Waves 4-7 in parallel on `main-dev` and `frontend-dev` to achieve 25% time reduction

**Branches Involved**:
- `main-dev` - Development trunk for backend + admin features
- `frontend-dev` - Development trunk for frontend + public features
- `feature/issue-XXXX-site-wide-redesign` - Feature branch (exists on both trunks)
- `integration/site-wide-redesign` - Integration branch (Wave 8-9)

---

## Branch Workflow

### Phase 1: Shared Foundation (Wave 1-3)

**Goal**: Complete foundation work together before splitting

**Process**:
```bash
# Start from main-dev
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-XXXX-site-wide-redesign

# Complete Wave 1: Foundation
# - Setup theme system
# - Create ThemeProvider, ThemeToggle
# - Update globals.css, tailwind.config.js

git add .
git commit -m "feat(theme): Wave 1 - theme system foundation"
git push origin feature/issue-XXXX-site-wide-redesign

# Complete Wave 2: UI Primitives
# - Update 20 shadcn/ui components
# - Run Morphllm automations

git add .
git commit -m "feat(ui): Wave 2 - primitives themed with Morphllm"
git push origin feature/issue-XXXX-site-wide-redesign

# Complete Wave 3: Layouts
# - Update TopNav, BottomNav, layouts

git add .
git commit -m "feat(layout): Wave 3 - global layouts themed"
git push origin feature/issue-XXXX-site-wide-redesign
```

**Checkpoint**: After Wave 3, foundation is stable

---

### Phase 2: Sync to frontend-dev

**Goal**: Replicate foundation work to frontend-dev branch

**Process**:
```bash
# Switch to frontend-dev
git checkout frontend-dev
git pull origin frontend-dev

# Create feature branch on frontend-dev
git checkout -b feature/issue-XXXX-site-wide-redesign

# Cherry-pick foundation commits from main-dev
git fetch origin main-dev
git log origin/main-dev --oneline | grep "Wave [1-3]"

# Cherry-pick Wave 1-3 commits
git cherry-pick <commit-wave-1>
git cherry-pick <commit-wave-2>
git cherry-pick <commit-wave-3>

# Resolve conflicts if any (should be minimal)
git push origin feature/issue-XXXX-site-wide-redesign
```

**Validation**:
```bash
# Verify foundation works on frontend-dev
cd apps/web
pnpm install        # Ensure dependencies synced
pnpm typecheck      # Should pass
pnpm test           # Should pass
pnpm dev            # Start dev server, test theme toggle
```

---

### Phase 3: Parallel Execution (Wave 4-7)

**Goal**: Work simultaneously on different component domains

#### Developer A - Admin Components (main-dev)

**Branch**: `feature/issue-XXXX-site-wide-redesign` (on main-dev)
**Waves**: 4 (Admin components)
**Duration**: 3-4 days

**Work Scope**:
```
apps/web/src/
├── app/admin/                    ← All admin pages
└── components/admin/             ← Admin-specific components
    ├── DashboardHeader.tsx
    ├── MetricsGrid.tsx
    ├── ActivityFeed.tsx
    ├── ChartsSection.tsx
    ├── ServiceHealthMatrix.tsx
    └── ... (35+ more)
```

**Daily Commits**:
```bash
# Day 1: Dashboard core
git add apps/web/src/components/admin/{DashboardHeader,MetricsGrid,KPICard,StatCard}.tsx
git commit -m "feat(admin): Wave 4 Day 1 - dashboard core components themed"
git push

# Day 2: Management pages
git add apps/web/src/app/admin/{management,users,configuration}
git commit -m "feat(admin): Wave 4 Day 2 - management pages themed"
git push

# Day 3: Charts and analytics
git add apps/web/src/components/admin/charts
git commit -m "feat(admin): Wave 4 Day 3 - charts themed"
git push

# Day 4: Specialized admin
git add apps/web/src/app/admin/{prompts,api-keys,alerts}
git commit -m "feat(admin): Wave 4 Day 4 - specialized admin themed"
git push
```

#### Developer B - Public Components (frontend-dev)

**Branch**: `feature/issue-XXXX-site-wide-redesign` (on frontend-dev)
**Waves**: 5 (Public pages), 6 (Chat), 7 (Features)
**Duration**: 8-10 days total

**Work Scope**:
```
apps/web/src/
├── app/(public)/                 ← Public pages
├── app/(chat)/                   ← Chat pages
└── components/
    ├── chat/                     ← Chat components
    ├── games/                    ← Game components
    ├── library/                  ← Library components
    ├── dashboard/                ← User dashboard
    ├── landing/                  ← Landing page
    └── ... (80+ more)
```

**Daily Commits**:
```bash
# Wave 5 Day 1: Landing and auth
git add apps/web/src/components/landing apps/web/src/components/auth
git commit -m "feat(public): Wave 5 Day 1 - landing and auth themed"
git push

# Wave 5 Day 2: Dashboard and games
git add apps/web/src/app/\(public\)/{dashboard,games}
git commit -m "feat(public): Wave 5 Day 2 - dashboard and games themed"
git push

# Wave 6: Chat interface
git add apps/web/src/components/chat apps/web/src/app/\(chat\)
git commit -m "feat(chat): Wave 6 - chat interface themed"
git push

# Wave 7: Feature components
git add apps/web/src/components/{library,sessions,pdf}
git commit -m "feat(features): Wave 7 - feature components themed"
git push
```

---

### Phase 4: Integration (Wave 8-9)

**Goal**: Merge parallel work and complete polish + testing

**Process**:
```bash
# Create integration branch from main-dev
git checkout main-dev
git pull origin main-dev
git checkout -b integration/site-wide-redesign

# Merge admin work (main-dev feature branch)
git merge feature/issue-XXXX-site-wide-redesign --no-ff
# Commit: "chore(merge): integrate admin components from main-dev"

# Merge public work (frontend-dev feature branch)
git fetch origin frontend-dev
git merge origin/feature/issue-XXXX-site-wide-redesign --no-ff
# Commit: "chore(merge): integrate public components from frontend-dev"

# Resolve conflicts
# - Check for file conflicts (should be minimal due to clear ownership)
# - Review merged code
# - Run full test suite

# Complete Wave 8: Polish & Effects
git add .
git commit -m "feat(ui): Wave 8 - glass effects and mobile optimizations"

# Complete Wave 9: Testing
# - Update all tests
# - Run Chromatic
# - Run Playwright E2E

git add .
git commit -m "test(ui): Wave 9 - comprehensive test suite updated"
```

**Final Merge to Trunks**:
```bash
# Merge to main-dev
git checkout main-dev
git merge integration/site-wide-redesign --no-ff
git push origin main-dev

# Merge to frontend-dev
git checkout frontend-dev
git merge integration/site-wide-redesign --no-ff
git push origin frontend-dev

# Create PR to main (production)
gh pr create --base main --head main-dev \
  --title "feat(ui): Site-Wide Dual-Theme Design System" \
  --body "Closes #XXXX"
```

---

## Conflict Resolution Strategy

### Expected Conflicts

**Minimal Risk** (Clear ownership):
- Admin components: Only in main-dev feature branch
- Public components: Only in frontend-dev feature branch
- UI primitives: Completed in Wave 2 before split (no conflicts)

**Potential Conflicts** (Shared files):
```
Shared Files:
├── globals.css                  ← Both may add theme rules
├── tailwind.config.js           ← Both may add utilities
├── theme-variables.css          ← Both may add tokens
└── components/ui/primitives/    ← Foundation (completed before split)
```

### Resolution Protocol

**globals.css**:
```bash
# Accept both changes (merge sections)
<<<<<<< main-dev
/* Admin-specific global styles */
.admin-table { ... }
=======
/* Public-specific global styles */
.landing-hero { ... }
>>>>>>> frontend-dev

# Resolved:
/* Admin-specific global styles */
.admin-table { ... }

/* Public-specific global styles */
.landing-hero { ... }
```

**tailwind.config.js**:
```bash
# Merge plugin/theme additions
# Both likely just add utilities, no conflicts expected
```

**CSS Variables**:
```bash
# If both add new tokens, merge them
# Follow alphabetical order for consistency
```

---

## Communication Protocol

### Daily Sync (If team)

**Standup Questions**:
1. What wave/components did you complete yesterday?
2. What are you working on today?
3. Any blockers or conflicts discovered?
4. Any shared files modified that need sync?

### Sync Commits

If shared file modified:
```bash
# Developer A notifies Developer B
"Updated globals.css to add admin table styles"

# Developer B pulls latest
git fetch origin main-dev
git cherry-pick <specific-commit> # If needed on frontend-dev
```

---

## Merge Checklist

### Before Creating Integration Branch

**main-dev readiness**:
- [ ] Wave 4 complete (all admin components)
- [ ] All admin tests passing
- [ ] No console errors on admin pages
- [ ] Chromatic approved for admin stories

**frontend-dev readiness**:
- [ ] Waves 5-7 complete (public, chat, features)
- [ ] All public/chat tests passing
- [ ] No console errors on public pages
- [ ] Chromatic approved for public stories

### Integration Branch Validation

After merging both branches:
- [ ] Full test suite passing (`pnpm test`)
- [ ] TypeScript compilation successful (`pnpm typecheck`)
- [ ] ESLint passing (`pnpm lint`)
- [ ] Dev server starts without errors (`pnpm dev`)
- [ ] Production build successful (`pnpm build`)
- [ ] Manual smoke test: Navigate all major pages in both themes
- [ ] No duplicate code or conflicting styles

---

## Rollback Plan

### If Critical Issues Found

**Option 1: Revert Integration**
```bash
# Rollback integration branch
git checkout integration/site-wide-redesign
git revert <problematic-commit>
git push
```

**Option 2: Revert Specific Wave**
```bash
# Rollback just Wave 4 (admin) if issues isolated
git revert <wave-4-commits>
# Keep Waves 5-7 (public) if they're fine
```

**Option 3: Full Rollback**
```bash
# Nuclear option: revert everything
git checkout main-dev
git revert --no-commit <merge-commit>..HEAD
git commit -m "revert: rollback site-wide redesign (critical issues)"
```

### Feature Flag Fallback

If partial rollback needed:
```tsx
// Add feature flag to toggle new theme
const useNewTheme = useFeatureFlag('new-theme-system');

return useNewTheme ? <ThemedComponent /> : <LegacyComponent />;
```

---

## Timeline with Parallelization

### Sequential Timeline (Single developer)
```
Wave 1: 2 days
Wave 2: 3 days
Wave 3: 2 days
Wave 4: 4 days
Wave 5: 4 days
Wave 6: 3 days
Wave 7: 5 days
Wave 8: 2 days
Wave 9: 3 days
─────────────
Total: 28 days
```

### Parallel Timeline (Two developers/branches)
```
Wave 1-3: 7 days (both)
─────────────────────────
Wave 4: 4 days (main-dev)    ┐
Wave 5-7: 12 days (frontend) ┘ → Max = 12 days
─────────────────────────────
Wave 8-9: 5 days (integration)
─────────────────────────────
Total: 24 days
```

### Optimized with Morphllm
```
Wave 1-3: 6 days (Morphllm in Wave 2)
Wave 4-7: 10 days (Morphllm bulk updates)
Wave 8-9: 4 days (automated test updates)
─────────────────────────────
Total: 20 days (33% faster)
```

### Final Estimate
```
Actual Working Days: 12-14 days
Calendar Days: 18-20 days
With Parallelization + Morphllm: Best case 12 days
```

---

**Strategy Status**: ✅ Defined and ready for execution
**Key Success Factor**: Clear file ownership prevents merge conflicts
**Optimization**: Morphllm + parallelization = 66% time reduction vs sequential manual
