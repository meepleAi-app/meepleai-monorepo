# Sequenza Operativa Immediata - Issue Implementation

**Start Date**: 2026-01-23
**Priority**: Site-Wide Dual-Theme Design System (#2965)
**Next 30 days**: Foundation + Design System Core

---

## ⚡ Immediate Actions (Today - Day 4)

### **FASE 0: Foundation Setup** (3-4 giorni)

**Obiettivo**: Preparare infrastruttura per #2965

#### **Giorno 1-2: Storybook & Component Extraction**

**Issue #2924**: Storybook Setup and Foundation
```bash
cd apps/web
pnpm add -D @storybook/nextjs@latest @storybook/addon-themes
pnpm exec storybook@latest init
# Configure for Next.js 14 App Router + dark mode addon
```

**Deliverable**:
- [ ] Storybook running on http://localhost:6006
- [ ] Dark mode addon configured
- [ ] 0 stories baseline ready

---

**Issue #2930**: Extract Reusable Components from Admin Dashboard
```bash
# Audit existing admin components
# Extract 20+ primitives to src/components/ui/
# Examples: Button, Input, Card, Badge, Table, etc.
```

**Components to extract** (priority):
1. Button (primary, secondary, ghost variants)
2. Input / Textarea
3. Card (base component)
4. Badge (status indicators)
5. Table (TanStack foundation)
6. Alert / AlertDialog
7. Checkbox / Radio / Toggle
8. Avatar
9. Progress / Skeleton
10. Sonner (toast notifications)

**Deliverable**:
- [ ] 20 components in `/components/ui`
- [ ] Initial Storybook stories for each
- [ ] No breaking changes to existing pages

---

#### **Giorno 3: Design System Documentation**

**Issue #2931**: Design System Documentation - Typography, Colors, Spacing

Create `docs/design-system/README.md`:
```markdown
# MeepleAI Design System

## Color Palette

### Light Mode (Glass Morphism)
- Primary Background: linear-gradient(135deg, #fef3e2, #f8f6f0, #fce8d8)
- Card Background: rgba(255, 255, 255, 0.7)
- Accent: #d2691e (MeepleAI Cream)
- Text Primary: #2d2d2d

### Dark Mode (Professional)
- Primary Background: #1a1a1a
- Card Background: #2d2d2d
- Accent: #fbbf24 (Amber for visibility)
- Text Primary: #e8e4d8

## Typography
- Font Family: Inter (system fallback)
- Headings: Bold (600-700)
- Body: Regular (400)
- Code: JetBrains Mono

## Spacing
- Base unit: 4px (0.25rem)
- Scale: 4, 8, 12, 16, 24, 32, 48, 64, 96px

## Effects
- Blur: 8px, 12px, 16px (desktop only)
- Shadows: Subtle elevations
- Transitions: 150ms ease-in-out
```

**Deliverable**:
- [ ] Design system docs complete
- [ ] Figma/design asset references linked
- [ ] CSS variable naming conventions documented

---

#### **Giorno 4: Code Cleanup**

**Issue #2803**: Consolidate ActivityTimeline and ActivityFeed components

Merge duplicate implementations:
```bash
# Find duplicates
grep -r "ActivityTimeline\|ActivityFeed" apps/web/src

# Consolidate into single component
# Update all import references
# Add Storybook story
```

**Deliverable**:
- [ ] Single unified component
- [ ] All references updated
- [ ] No visual regressions

---

**✅ CHECKPOINT 1** (End of Day 4):
- [ ] Storybook operational
- [ ] 20+ components extracted
- [ ] Design system documented
- [ ] No duplicates
- [ ] **Ready to start #2965 Wave 1**

---

## 🎨 Prossimi Step (Day 5-22): Design System Implementation

### **FASE 1: Site-Wide Dual-Theme Design System** (#2965)

#### **Wave 1: Foundation** (Day 5-6)

**Branch Setup**:
```bash
# main-dev branch
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-2965-site-wide-redesign

# frontend-dev branch
git checkout frontend-dev
git pull origin frontend-dev
git checkout -b feature/issue-2965-site-wide-redesign
```

**Tasks**:
1. Install `next-themes`
   ```bash
   pnpm add next-themes
   ```

2. Create `src/styles/theme-variables.css`:
   ```css
   :root {
     /* Light mode variables */
     --bg-primary: linear-gradient(135deg, #fef3e2, #f8f6f0, #fce8d8);
     --card-bg: rgba(255, 255, 255, 0.7);
     --text-primary: #2d2d2d;
     --accent: #d2691e;
   }

   [data-theme="dark"] {
     /* Dark mode variables */
     --bg-primary: #1a1a1a;
     --card-bg: #2d2d2d;
     --text-primary: #e8e4d8;
     --accent: #fbbf24;
   }
   ```

3. Create `src/components/providers/ThemeProvider.tsx`
4. Create `src/components/ui/ThemeToggle.tsx`
5. Update `src/app/layout.tsx` with provider
6. Update `tailwind.config.ts` for dark mode

**Deliverable**:
- [ ] Theme toggle visible in UI
- [ ] Theme persists in localStorage
- [ ] CSS variables accessible everywhere

---

#### **Wave 2: UI Primitives** (Day 7-9)

**Morphllm Automation**:
```bash
# Pattern 1: Card backgrounds
# FROM: bg-white border
# TO: bg-card/90 backdrop-blur-glass dark:bg-[#2d2d2d] border-border/50

# Pattern 2: Text colors
# FROM: text-gray-600
# TO: text-muted-foreground dark:text-[#999]

# Use mcp__morphllm__edit_file for bulk transformation
```

**Components to update** (20 total):
- Button, Input, Textarea, Checkbox, Radio, Toggle, Label, Slider
- Card, Badge, Table, Accordion, Avatar
- Alert, AlertDialog, Progress, Skeleton, Sonner

**Deliverable**:
- [ ] All 20 primitives dual-themed
- [ ] Storybook stories show both variants
- [ ] Visual regression tests baseline

---

#### **Wave 3: Global Layouts** (Day 10-11)

**Components** (10):
- TopNav, BottomNav, AdminSidebar
- PublicLayout, AuthLayout, ChatLayout, AdminLayout
- PublicHeader, PublicFooter, CommandPalette

**Theme Toggle Integration**:
- Add to TopNav user dropdown
- Add to mobile BottomNav settings
- Keyboard shortcut: `Cmd+Shift+T` (toggle theme)

**Deliverable**:
- [ ] Navigation themed correctly
- [ ] Layout wrappers apply backgrounds
- [ ] Theme toggle accessible

---

**🔄 SYNC POINT 1** (Day 11):
```bash
# Test on both branches
git checkout feature/issue-2965-site-wide-redesign

# Verify:
# 1. Theme toggle works on all foundation pages
# 2. No console errors
# 3. localStorage persistence works
# 4. All primitives render in both themes

# If all tests pass → proceed to Wave 4-7
```

---

#### **Wave 4-7: Parallel Implementation** (Day 12-21)

**Split branches**:
- **main-dev**: Admin components (Wave 4) + Backend features
- **frontend-dev**: Public pages (Wave 5) + Chat (Wave 6) + Features (Wave 7)

**Daily sync**:
```bash
# Share foundation updates
git fetch origin
git merge origin/feature/issue-2965-site-wide-redesign --no-commit
# Resolve conflicts immediately
```

**Deliverables** (Day 21):
- [ ] All admin components themed (main-dev)
- [ ] All public pages themed (frontend-dev)
- [ ] Chat interface themed (frontend-dev)
- [ ] 55+ feature components themed (both)

---

#### **Wave 8-9: Polish & Testing** (Day 22)

**Integration Branch**:
```bash
git checkout -b integration/phase1-design-system
git merge feature/issue-2965-site-wide-redesign (from main-dev)
git merge feature/issue-2965-site-wide-redesign (from frontend-dev)
# Resolve conflicts
```

**Tasks**:
- Glass effects finalization
- Mobile optimizations (disable blur <768px)
- Framer Motion transitions
- Chromatic visual regression
- E2E theme switching test
- Accessibility audit
- Performance budgets

**Deliverables**:
- [ ] Lighthouse >90
- [ ] All tests passing
- [ ] Chromatic approved
- [ ] WCAG AA compliant

---

**🎉 FASE 1 COMPLETE** (Day 22)

**Merge to main branches**:
```bash
git checkout main-dev
git merge integration/phase1-design-system

git checkout frontend-dev
git merge integration/phase1-design-system

# Tag release
git tag -a v2.0.0-design-system -m "Site-Wide Dual-Theme Design System"
git push origin --tags
```

---

## 📊 Quick Reference: Issue Dependencies

### **Foundation Dependencies** (Must complete first)
```
#2924 (Storybook) → #2930 (Components) → #2931 (Docs) → #2803 (Cleanup)
    ↓
#2965 Wave 1 (Foundation)
```

### **Design System Flow**
```
#2965 Wave 1-3 (Foundation & Layouts)
    ↓
#2965 Wave 4 (Admin) ← main-dev
#2965 Wave 5-6 (Public/Chat) ← frontend-dev
#2965 Wave 7 (Features) ← both branches
    ↓
#2965 Wave 8-9 (Polish/Testing) ← integration
```

### **Backend Feature Dependencies** (main-dev)
```
Editor Dashboard: #2892 → #2893
User Management: #2884 → #2885, #2886
Profile/Settings: #2878 → #2879, #2880
Shared Catalog: #2871 → #2872
Personal Library: #2863 → #2864, #2865
User Dashboard: #2855, #2856
```

### **Frontend Feature Dependencies** (frontend-dev)
```
Editor Dashboard: #2894 → #2895 → #2896 → #2897
User Management: #2887 → #2888, #2890 → #2891
Profile/Settings: #2881 → #2882 → #2883
Shared Catalog: #2873 → #2874 → #2875 → #2876 → #2877
Personal Library: #2866 → #2867, #2868, #2869 → #2870
User Dashboard: #2857, #2858 → #2859 → #2860, #2861 → #2862
```

### **Epic Dependencies**
```
#2965 (Design System) → #2823 (Game Detail)
                     → #2759 (Test Coverage)
```

---

## ✅ Daily Checklist (Template)

**Morning** (9:00):
- [ ] Review yesterday's progress
- [ ] Check Serena memory for context
- [ ] Update TodoWrite with today's tasks
- [ ] Verify branch is up to date

**During Work**:
- [ ] Commit frequently (every 1-2 hours)
- [ ] Update Serena checkpoint every 30 min
- [ ] Run tests before commits
- [ ] Document blockers immediately

**Evening** (18:00):
- [ ] Final commit with summary
- [ ] Update PDCA docs/do.md
- [ ] Write Serena session summary
- [ ] Plan tomorrow's tasks

---

## 🚀 Command Quick Reference

**Branch Management**:
```bash
# Check current status
git status
git branch

# Sync with remote
git fetch origin
git pull origin <branch>

# Create feature branch
git checkout -b feature/issue-XXXX-description

# Daily merge from parent
git merge origin/main-dev --no-commit
```

**Testing**:
```bash
# Frontend
cd apps/web
pnpm typecheck
pnpm lint
pnpm test
pnpm test:coverage

# Backend
cd apps/api/src/Api
dotnet test
dotnet test /p:CollectCoverage=true
```

**Storybook**:
```bash
cd apps/web
pnpm storybook  # http://localhost:6006
pnpm build-storybook  # Static build
```

**Serena Memory**:
```bash
# Via Claude Code with Serena MCP
write_memory("session/checkpoint", progress_summary)
write_memory("execution/issue-XXXX/do", implementation_log)
```

---

**Created**: 2026-01-23
**Last Updated**: 2026-01-23
**Next Review**: Daily checkpoints
