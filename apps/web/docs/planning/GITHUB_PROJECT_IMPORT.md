# GitHub Project Import Guide

**Target Project**: https://github.com/users/DegrassiAaron/projects/2
**Total Issues to Import**: 155 open issues
**Last Updated**: 2025-11-12 20:50

## Prerequisites

### 1. Update GitHub CLI Permissions

```bash
gh auth refresh -s project -h github.com
```

This will open a browser to grant `project` scope to your GitHub CLI token.

### 2. Verify Access

```bash
gh project view 2 --owner DegrassiAaron
```

Should display project details without errors.

---

## Automatic Import (Recommended)

### Option A: Bulk Import Script

Save this as `import-to-project.sh`:

```bash
#!/bin/bash

PROJECT_NUMBER=2
OWNER="DegrassiAaron"
REPO="DegrassiAaron/meepleai-monorepo"

echo "Fetching project ID..."
PROJECT_ID=$(gh api graphql -f query='
  query {
    user(login: "'$OWNER'") {
      projectV2(number: '$PROJECT_NUMBER') {
        id
      }
    }
  }
' --jq '.data.user.projectV2.id')

echo "Project ID: $PROJECT_ID"
echo "Fetching open issues..."

# Get all open issues
gh issue list --repo $REPO --limit 200 --state open --json number,title,labels,milestone | \
jq -r '.[] | @json' | while read -r issue; do
  ISSUE_NUM=$(echo "$issue" | jq -r '.number')
  TITLE=$(echo "$issue" | jq -r '.title')
  MILESTONE=$(echo "$issue" | jq -r '.milestone.title // "No Milestone"')

  # Determine track
  LABELS=$(echo "$issue" | jq -r '.labels[].name' | tr '\n' ',')

  if echo "$LABELS" | grep -q "frontend"; then
    TRACK="Frontend"
  elif echo "$LABELS" | grep -q "backend"; then
    TRACK="Backend"
  elif echo "$LABELS" | grep -q "testing"; then
    TRACK="Testing"
  else
    TRACK="Other"
  fi

  # Determine priority
  if echo "$TITLE" | grep -qi "\[p1\]"; then
    PRIORITY="P1-Critical"
  elif echo "$LABELS" | grep -q "bug"; then
    PRIORITY="P2-High"
  else
    PRIORITY="P3-Normal"
  fi

  echo "Adding issue #$ISSUE_NUM: $TITLE"
  echo "  Milestone: $MILESTONE | Track: $TRACK | Priority: $PRIORITY"

  # Add to project (this requires project scope)
  gh api graphql -f query='
    mutation {
      addProjectV2ItemById(input: {
        projectId: "'$PROJECT_ID'"
        contentId: "issue_node_id_here"
      }) {
        item {
          id
        }
      }
    }
  ' || echo "  [ERROR] Failed to add issue #$ISSUE_NUM"

  sleep 0.5  # Rate limiting
done

echo "Import complete!"
```

Make executable and run:
```bash
chmod +x import-to-project.sh
./import-to-project.sh
```

---

## Manual Import (CSV Method)

### Step 1: Download CSV

The CSV file is generated at: `docs/planning/github-project-import.csv`

Columns:
- Issue # (number)
- Title
- Status (Todo/In Progress/Done)
- Priority (P1-Critical, P2-High, P3-Normal, P4-Low)
- Milestone
- Track (Frontend/Backend/Testing/Other)
- URL (direct link to issue)

### Step 2: Import to GitHub Project

1. Go to https://github.com/users/DegrassiAaron/projects/2
2. Click "+ Add item" → "Add from repository"
3. Select "DegrassiAaron/meepleai-monorepo"
4. Filter by "is:open" to see all open issues
5. Bulk select issues by milestone or label
6. Click "Add selected items"

### Step 3: Configure Project Fields

**Create Custom Fields**:

1. **Priority** (Single select)
   - P1-Critical (🔴)
   - P2-High (🟠)
   - P3-Normal (🟡)
   - P4-Low (⚪)

2. **Track** (Single select)
   - Frontend (🔵)
   - Backend (🟢)
   - Testing (🟡)
   - Documentation (📝)
   - DevOps (⚙️)
   - Other (⚫)

3. **Size** (Single select)
   - XS (1 point)
   - S (2 points)
   - M (3 points)
   - L (5 points)
   - XL (8 points)

4. **Sprint** (Iteration field)
   - Duration: 2 weeks
   - Start date: 2025-11-12

### Step 4: Bulk Update Fields

Use GitHub Project's bulk edit feature:

1. Select all issues from "MVP Sprint 1"
2. Set Sprint → Sprint 1
3. Set Status → Todo
4. Repeat for each milestone

---

## Semi-Automatic Import (GitHub CLI)

### Add Issues to Project

```bash
#!/bin/bash

# Get project ID
PROJECT_ID=$(gh api graphql -f query='
  query {
    user(login: "DegrassiAaron") {
      projectV2(number: 2) {
        id
      }
    }
  }
' --jq '.data.user.projectV2.id')

echo "Project ID: $PROJECT_ID"

# Add issues by milestone
MILESTONES=("MVP Sprint 1" "MVP Sprint 2" "Month 6: Italian UI" "FASE 1: Dashboard Overview")

for MS in "${MILESTONES[@]}"; do
  echo "Processing milestone: $MS"

  gh issue list --repo DegrassiAaron/meepleai-monorepo \
    --limit 50 \
    --state open \
    --milestone "$MS" \
    --json number | \
  jq -r '.[].number' | while read ISSUE_NUM; do
    echo "  Adding #$ISSUE_NUM to project..."

    # Get issue node ID
    ISSUE_ID=$(gh api repos/DegrassiAaron/meepleai-monorepo/issues/$ISSUE_NUM --jq '.node_id')

    # Add to project
    gh api graphql -f query='
      mutation {
        addProjectV2ItemById(input: {
          projectId: "'$PROJECT_ID'"
          contentId: "'$ISSUE_ID'"
        }) {
          item {
            id
          }
        }
      }
    ' --silent && echo "    ✓ Added" || echo "    ✗ Failed"

    sleep 0.3
  done
done
```

---

## Import by Category

### MVP Sprints (33 issues)

```bash
gh issue list --repo DegrassiAaron/meepleai-monorepo --limit 50 \
  --search "milestone:\"MVP Sprint 1\" OR milestone:\"MVP Sprint 2\" OR milestone:\"MVP Sprint 3\" OR milestone:\"MVP Sprint 4\" OR milestone:\"MVP Sprint 5\"" \
  --state open
```

- MVP Sprint 1: 7 issues (1 FE, 1 BE, 3 Test)
- MVP Sprint 2: 5 issues (2 FE, 3 BE)
- MVP Sprint 3: 6 issues (2 FE, 3 BE, 1 Test)
- MVP Sprint 4: 5 issues (3 FE, 2 BE)
- MVP Sprint 5: 5 issues (1 FE, 3 BE, 1 Test)

### Infrastructure FASE (49 issues)

```bash
gh issue list --repo DegrassiAaron/meepleai-monorepo --limit 60 \
  --search "milestone:\"FASE 1\" OR milestone:\"FASE 2\" OR milestone:\"FASE 3\" OR milestone:\"FASE 4\"" \
  --state open
```

- FASE 1 Dashboard: 16 issues (6 FE, 5 BE, 4 Test)
- FASE 2 Monitoring: 13 issues (5 FE, 4 BE, 3 Test)
- FASE 3 Management: 12 issues (5 FE, 3 BE, 3 Test)
- FASE 4 Advanced: 8 issues (2 FE, 3 BE, 2 Test)

### Board Game AI Months (41 issues)

```bash
gh issue list --repo DegrassiAaron/meepleai-monorepo --limit 50 \
  --search "label:board-game-ai milestone:\"Month 3\" OR milestone:\"Month 4\" OR milestone:\"Month 5\" OR milestone:\"Month 6\"" \
  --state open
```

- Month 3: 13 issues (Backend heavy)
- Month 4: 11 issues (Mixed)
- Month 5: 14 issues (Mixed)
- Month 6: 14 issues (Italian UI)

---

## Project Views Configuration

### Recommended Views

**1. Sprint Board**
- Group by: Status
- Filter: Milestone = current sprint
- Show: Priority, Track, Assignee

**2. Timeline (Gantt)**
- Layout: Timeline
- Group by: Milestone
- Color by: Track
- Date field: Milestone due date

**3. Team Tracks**
- Group by: Track
- Filter: Status != Done
- Show: Priority, Milestone, Assignee

**4. Priority Matrix**
- Group by: Priority
- Sort by: Issue number
- Show: Milestone, Track, Status

---

## Data Organization Strategy

### Milestones as Sprints

Map GitHub milestones to Project iterations:

| GitHub Milestone | Project Sprint | Duration |
|------------------|----------------|----------|
| MVP Sprint 1 | Sprint 1 | 2025-11-12 → 2025-11-25 |
| MVP Sprint 2 | Sprint 2 | 2025-11-26 → 2025-12-09 |
| MVP Sprint 3 | Sprint 3 | 2025-12-10 → 2025-12-23 |
| FASE 1 | Sprint 6 | 2026-01-21 → 2026-02-03 |
| Month 6 | Sprint 9 | 2026-03-04 → 2026-03-17 |

### Status Workflow

```
Todo → In Progress → In Review → Done
  ↓         ↓            ↓
 (start)  (PR open)   (merged)
```

### Priority Guidelines

- **P1-Critical**: Blocking, immediate attention needed
- **P2-High**: Important bugs, high-value features
- **P3-Normal**: Standard work items
- **P4-Low**: Nice-to-have, backlog items

---

## Troubleshooting

### Permission Errors

If you get "INSUFFICIENT_SCOPES" errors:

```bash
# Refresh with project scope
gh auth refresh -s project -h github.com

# Verify scopes
gh auth status
```

### Rate Limiting

If hitting rate limits:

```bash
# Check rate limit status
gh api rate_limit

# Add delays between API calls
sleep 0.5  # in scripts
```

### Bulk Operations

For bulk operations, use GitHub's web UI:
1. Go to Issues tab
2. Use filters: `is:open milestone:"MVP Sprint 1"`
3. Select all → Add to project

---

## Post-Import Configuration

### 1. Set Up Automation

In Project settings, add workflows:

**Auto-add issues**:
- Trigger: Issue opened with label `board-game-ai`
- Action: Add to project

**Auto-set status**:
- Trigger: Pull request linked
- Action: Status → In Review

**Auto-close**:
- Trigger: Issue closed
- Action: Status → Done

### 2. Configure Board Layout

**Columns**:
1. Backlog (Status: Todo, no Sprint assigned)
2. Current Sprint (Status: Todo, Sprint = current)
3. In Progress
4. In Review
5. Done (last 2 weeks)

### 3. Create Saved Filters

**Frontend Team**:
```
is:open label:frontend
```

**Backend Team**:
```
is:open label:backend
```

**Current Sprint**:
```
is:open milestone:"MVP Sprint 1"
```

**High Priority**:
```
is:open sort:priority-desc
```

---

## Quick Start Checklist

- [ ] Update gh CLI permissions (`gh auth refresh -s project`)
- [ ] Verify project access (`gh project view 2 --owner DegrassiAaron`)
- [ ] Create custom fields (Priority, Track, Size)
- [ ] Add issues by milestone (use bulk select in web UI)
- [ ] Configure project views (Sprint Board, Timeline, Team Tracks)
- [ ] Set up automation rules
- [ ] Test workflow with a few issues
- [ ] Bulk import remaining issues
- [ ] Configure saved filters for teams

---

## Support Resources

- [GitHub Projects Documentation](https://docs.github.com/en/issues/planning-and-tracking-with-projects)
- [GitHub CLI Projects](https://cli.github.com/manual/gh_project)
- [GraphQL API for Projects](https://docs.github.com/en/graphql/reference/objects#projectv2)

---

**Generated**: 2025-11-12 20:50
**Script Location**: `docs/planning/import-to-project.sh` (to be created after auth)
