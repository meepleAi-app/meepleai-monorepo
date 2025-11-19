# GitHub Project Setup Guide

Instructions for creating a GitHub Project to track the Code Review implementation tasks.

## 📋 Project Details

**Name**: Code Review - Backend-Frontend Improvements
**Description**: Track implementation of security, validation, and DX improvements from 2025-01-19 code review
**Visibility**: Repository (visible to repo collaborators)

## 🎯 Project Structure

### Columns

1. **📋 Backlog**
   - All unstarted issues
   - Sorted by priority (Critical → Medium)

2. **🏗️ In Progress**
   - Currently being worked on
   - Limit: 2-3 issues max (avoid context switching)

3. **👀 In Review**
   - Pull Request created
   - Awaiting code review

4. **✅ Done**
   - Merged to main
   - Deployed (if applicable)

### Views

**View 1: By Sprint**
- Group by: `sprint` label
- Sort by: Priority

**View 2: By Priority**
- Group by: `priority` label
- Sort by: Created date

**View 3: By Area**
- Group by: `area` label (backend/frontend)
- Filter: Open issues only

## 🚀 Option 1: GitHub CLI (Automated)

```bash
# Create project (requires GitHub CLI v2.20+)
gh project create \
  --owner DegrassiAaron \
  --title "Code Review - Backend-Frontend Improvements" \
  --body "Track implementation of security, validation, and DX improvements from 2025-01-19 code review" \
  --field "Priority" \
  --field "Sprint" \
  --field "Area"

# Get project number (will be displayed after creation)
PROJECT_NUMBER=X  # Replace with actual number

# Add issues to project (after creating issues)
gh project item-add $PROJECT_NUMBER --owner DegrassiAaron --url https://github.com/DegrassiAaron/meepleai-monorepo/issues/1
gh project item-add $PROJECT_NUMBER --owner DegrassiAaron --url https://github.com/DegrassiAaron/meepleai-monorepo/issues/2
# ... repeat for all 8 issues
```

## 🖱️ Option 2: GitHub Web UI (Manual)

### Step 1: Create Project

1. Go to https://github.com/DegrassiAaron/meepleai-monorepo/projects
2. Click **"New project"**
3. Select **"Table"** template
4. Name: `Code Review - Backend-Frontend Improvements`
5. Description:
   ```
   Track implementation of security, validation, and DX improvements from 2025-01-19 code review.

   Related ADRs:
   - ADR-010: Security Headers Middleware
   - ADR-011: CORS Whitelist Headers
   - ADR-012: FluentValidation CQRS
   - ADR-013: NSwag TypeScript Generation

   8 issues total, estimated 39-52 hours over 5 sprints.
   ```
6. Visibility: **Repository**
7. Click **"Create project"**

### Step 2: Configure Columns

1. Rename default columns:
   - **Todo** → **📋 Backlog**
   - **In Progress** → **🏗️ In Progress**
   - **Done** → **✅ Done**

2. Add new column:
   - Click **"+"** next to columns
   - Name: **👀 In Review**
   - Position: Between "In Progress" and "Done"

3. Configure automation (optional):
   - Click column settings (⚙️)
   - **Backlog**: Auto-add newly created issues
   - **In Progress**: Auto-move when issue assigned
   - **In Review**: Auto-move when PR created
   - **Done**: Auto-move when issue closed

### Step 3: Add Custom Fields

1. Click **"+ New field"**
2. Add **Priority** field:
   - Type: Single select
   - Options:
     - 🔴 Critical
     - 🟡 High
     - 🟢 Medium
     - 🔵 Low

3. Add **Sprint** field:
   - Type: Single select
   - Options:
     - Sprint 1
     - Sprint 2
     - Sprint 3
     - Sprint 4
     - Sprint 5

4. Add **Area** field:
   - Type: Single select
   - Options:
     - Backend
     - Frontend
     - Both

5. Add **Estimated Hours** field:
   - Type: Number
   - Range: 0-100

### Step 4: Add Issues to Project

After creating issues (via `.github/ISSUES_TEMPLATES_CR/create-issues.sh`):

1. Go to project
2. Click **"+ Add item"**
3. Search for issue by number or title
4. Click to add
5. Repeat for all 8 issues

### Step 5: Set Field Values

For each issue in project:

**Issue #1: SecurityHeadersMiddleware**
- Priority: 🔴 Critical
- Sprint: Sprint 1
- Area: Backend
- Estimated Hours: 5

**Issue #2: CORS Whitelist**
- Priority: 🔴 Critical
- Sprint: Sprint 1
- Area: Backend
- Estimated Hours: 3

**Issue #3: FluentValidation Authentication**
- Priority: 🟡 High
- Sprint: Sprint 2
- Area: Backend
- Estimated Hours: 6

**Issue #4: NSwag Code Generation**
- Priority: 🟡 High
- Sprint: Sprint 3
- Area: Both
- Estimated Hours: 9

**Issue #5: Streaming Hooks Consolidation**
- Priority: 🟢 Medium
- Sprint: Sprint 4
- Area: Frontend
- Estimated Hours: 7

**Issue #6: Rate Limiting UX**
- Priority: 🟢 Medium
- Sprint: Sprint 4
- Area: Frontend
- Estimated Hours: 5

**Issue #7: Retry Logic**
- Priority: 🟢 Medium
- Sprint: Sprint 5
- Area: Frontend
- Estimated Hours: 7

**Issue #8: Request Deduplication**
- Priority: 🟢 Medium
- Sprint: Sprint 5
- Area: Frontend
- Estimated Hours: 5

### Step 6: Create Views

**View 1: Sprint Planning**
1. Click **"+ New view"** → **"Table"**
2. Name: **Sprint Planning**
3. Group by: **Sprint**
4. Sort: **Priority** (descending)
5. Filter: **Status** = Open

**View 2: Priority Board**
1. Click **"+ New view"** → **"Board"**
2. Name: **Priority Board**
3. Group by: **Priority**
4. Column field: **Status**

**View 3: Roadmap**
1. Click **"+ New view"** → **"Roadmap"**
2. Name: **Roadmap**
3. Start date: Issue creation date
4. End date: Custom (set per issue)
5. Group by: **Sprint**

## 📊 Project Dashboard

Once set up, your project dashboard will show:

```
┌────────────────────────────────────────────────────────────┐
│  Code Review - Backend-Frontend Improvements               │
├────────────────────────────────────────────────────────────┤
│  📋 Backlog  │  🏗️ In Progress  │  👀 In Review  │  ✅ Done │
├──────────────┼──────────────────┼────────────────┼──────────┤
│  Issue #1    │                  │                │          │
│  Issue #2    │                  │                │          │
│  Issue #3    │                  │                │          │
│  Issue #4    │                  │                │          │
│  Issue #5    │                  │                │          │
│  Issue #6    │                  │                │          │
│  Issue #7    │                  │                │          │
│  Issue #8    │                  │                │          │
└──────────────┴──────────────────┴────────────────┴──────────┘

Sprint 1 (Critical): 2/8 issues (8 hours)
Sprint 2 (High): 1/8 issues (6 hours)
Sprint 3 (High): 1/8 issues (9 hours)
Sprint 4 (Medium): 2/8 issues (12 hours)
Sprint 5 (Medium): 2/8 issues (12 hours)

Total: 8 issues, 47 hours estimated
```

## 🔗 Integration with Issues

### Linking Issues to Project

In each issue body, add:
```markdown
**Project**: [Code Review - Backend-Frontend Improvements](https://github.com/DegrassiAaron/meepleai-monorepo/projects/X)
**Sprint**: 1
**Estimated Hours**: 5
```

### Automatic Updates

GitHub will automatically:
- Move issues to "In Progress" when assigned
- Move to "In Review" when PR created
- Move to "Done" when issue closed
- Update progress bar

## 📈 Tracking Progress

### Weekly Reviews

Every Friday, review project:
- Move completed items to Done
- Update status of in-progress items
- Plan next sprint

### Burndown Chart

Track progress:
```
Sprint 1: 8 hours / 8 hours (100%)
Sprint 2: 6 hours / 14 hours (43%)
Sprint 3: 0 hours / 23 hours (0%)
...
```

## 🎨 Custom Labels for Filtering

Add these labels to filter in project views:

- `security`: Security-related issues
- `validation`: Input validation
- `dx`: Developer experience
- `type-safety`: Type safety improvements
- `code-review-2025-01-19`: All issues from this review

## 📝 Notes

- **Project URL**: Will be `https://github.com/DegrassiAaron/meepleai-monorepo/projects/X`
- **Project Number**: Assigned automatically by GitHub
- **Access**: All repo collaborators can view and edit
- **Automation**: Configure workflows for auto-status updates

---

**Created**: 2025-01-19
**Source**: Code Review - Backend-Frontend Interactions
**Total Issues**: 8
**Estimated Duration**: 5 sprints (10 weeks part-time)
