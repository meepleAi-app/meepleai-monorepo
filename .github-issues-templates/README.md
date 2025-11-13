# GitHub Issues - Frontend Refactor Roadmap

This directory contains GitHub issue templates for the MeepleAI frontend refactoring roadmap.

## How to Use

Each `.md` file in this directory is a complete GitHub issue. To create the issues:

### Option 1: GitHub CLI (Recommended)

```bash
# From the repository root
cd .github-issues-templates

# Create all Sprint 1 issues
for file in sprint-1-critical/*.md; do
  gh issue create --body-file "$file" --label "frontend,refactor,sprint-1,priority-critical"
done

# Create all Sprint 2 issues
for file in sprint-2-important/*.md; do
  gh issue create --body-file "$file" --label "frontend,refactor,sprint-2,priority-high"
done

# Create all Sprint 3 issues
for file in sprint-3-nice-to-have/*.md; do
  gh issue create --body-file "$file" --label "frontend,refactor,sprint-3,priority-medium"
done
```

### Option 2: Manual Creation

1. Go to https://github.com/YOUR_USERNAME/meepleai-monorepo/issues/new
2. Copy the content from each `.md` file
3. Paste into the issue body
4. Add appropriate labels
5. Click "Submit new issue"

## Issue Organization

Issues are organized by sprint priority:

- **Sprint 1 (Critical)**: 🔴 Must-fix issues blocking progress
- **Sprint 2 (Important)**: 🟡 High-impact improvements
- **Sprint 3 (Nice-to-have)**: 🟢 Polish and enhancements

## Labels

Recommended labels for these issues:

- `frontend` - Frontend-related issue
- `refactor` - Code refactoring
- `design-system` - Design system related
- `accessibility` - Accessibility improvements
- `performance` - Performance optimization
- `dx` - Developer experience
- `sprint-1` / `sprint-2` / `sprint-3` - Sprint assignment
- `priority-critical` / `priority-high` / `priority-medium` - Priority level

## Estimated Effort

Each issue includes an effort estimate:

- **2h** - Quick fix
- **4h** - Half day
- **1d** - One day
- **2d** - Two days
- **1w** - One week

## Dependencies

Some issues depend on others. Check the "Dependencies" section in each issue.

## Progress Tracking

Create a GitHub Project board to track progress:

1. Go to https://github.com/YOUR_USERNAME/meepleai-monorepo/projects
2. Create new project: "Frontend Refactor"
3. Add these views:
   - **By Sprint**: Group by sprint label
   - **By Status**: Group by status (Todo, In Progress, Done)
   - **By Priority**: Group by priority label

---

**Total Issues**: 15
**Total Estimated Effort**: ~3-4 weeks (1 developer)
