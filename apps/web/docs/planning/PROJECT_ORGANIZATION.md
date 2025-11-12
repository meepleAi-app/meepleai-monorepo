# GitHub Project Organization Guide

**Project**: [Meeple AI Planning](https://github.com/users/DegrassiAaron/projects/2)
**Status**: ✅ 157 issues already imported
**Last Updated**: 2025-11-12 21:05

## ✅ Current Status

**Good news!** All issues are already in the GitHub Project. Now we need to organize them properly.

### Current State
- **Total Items**: 157 issues
- **Project ID**: `PVT_kwHOAOeY484BH9dA`
- **Custom Fields**: Priority (P0/P1/P2), Status (Todo/In progress/Done), Size (XS/S/M/L/XL)

### What's Missing
- **Track field** (Frontend/Backend/Testing) - needs to be created manually
- **Priority assignments** - most issues need priority set
- **Size estimates** - issues need sizing
- **Sprint/Iteration** - milestone mapping to sprints

---

## 🎯 Organization Strategy

### Phase 1: Create Track Field (Manual)

Go to project settings → Fields → Add field:

**Field Name**: `Track`
**Type**: Single select
**Options**:
- 🔵 Frontend
- 🟢 Backend
- 🟡 Testing
- 📝 Documentation
- ⚙️ DevOps
- ⚫ Other

---

### Phase 2: Bulk Update by Milestone

Use GitHub's bulk edit feature to organize issues:

#### MVP Sprint 1 (7 issues)

**Filter**: `milestone:"MVP Sprint 1"`

**Bulk Update**:
- Status: Todo
- Priority: P1 (high priority sprint)
- Specific assignments:
  - #848 (Settings Pages): Track=Frontend, Size=L
  - #849 (User Profile Service): Track=Backend, Size=M

#### FASE 1: Dashboard Overview (16 issues)

**Filter**: `milestone:"FASE 1: Dashboard Overview"`

**High Parallelization** (6 FE + 5 BE + 4 Test):
- Frontend issues (#883, #889, #890, #893, #894, #895): Track=Frontend, Priority=P1
- Backend issues (#884, #885, #886, #891, #892): Track=Backend, Priority=P1
- Testing issues (#887, #888, #896, #897): Track=Testing, Priority=P2

#### Month 6: Italian UI (14 issues)

**Filter**: `milestone:"Month 6: Italian UI"`

**Current Focus**:
- All issues: Priority=P0 (current sprint)
- Frontend issues: Track=Frontend
- Backend issues: Track=Backend
- Testing issues: Track=Testing

---

### Phase 3: Bulk Priority Assignment

Use these filters to assign priorities:

**P0 (Critical - Current Sprint)**:
```
is:open milestone:"Month 6: Italian UI"
```
Set all to Priority=P0

**P1 (High - Next Sprint)**:
```
is:open milestone:"MVP Sprint 1" OR milestone:"FASE 1"
```
Set to Priority=P1

**P2 (Normal - Backlog)**:
```
is:open milestone:"Month 5" OR milestone:"Month 4" OR milestone:"FASE 2"
```
Set to Priority=P2

---

## 📋 Quick Bulk Edit Commands

### Using GitHub Web UI

1. Go to project: https://github.com/users/DegrassiAaron/projects/2
2. Click filter icon
3. Enter filter (e.g., `milestone:"MVP Sprint 1"`)
4. Select all issues (checkbox at top)
5. Click "..." → "Set field value"
6. Choose field and value
7. Apply

---

### Recommended Bulk Edits

#### Edit 1: Set Current Sprint Priority
```
Filter: milestone:"Month 6: Italian UI"
Action: Set Priority → P0
Count: ~14 issues
```

#### Edit 2: Set High Priority Milestones
```
Filter: milestone:"FASE 1: Dashboard Overview"
Action: Set Priority → P1
Count: ~16 issues
```

#### Edit 3: Size Frontend Issues
```
Filter: label:frontend
Action: Set Size → M (default, adjust individually later)
Count: ~52 issues
```

#### Edit 4: Size Backend Issues
```
Filter: label:backend
Action: Set Size → M (default)
Count: ~38 issues
```

#### Edit 5: Size Testing Issues
```
Filter: label:testing
Action: Set Size → S (tests usually smaller)
Count: ~37 issues
```

---

## 🎨 Project Views Setup

### View 1: Sprint Board

**Create new view**: "Sprint Board"

**Configuration**:
- Layout: Board
- Group by: Status
- Filter: `milestone:"Month 6: Italian UI"` (current sprint)
- Show fields: Priority, Track, Size, Assignee
- Sort: Priority (P0 first)

**Columns**:
1. Todo (not started)
2. In progress (being worked on)
3. Done (completed)

---

### View 2: Team Tracks

**Create new view**: "Team Tracks"

**Configuration**:
- Layout: Board
- Group by: Track (when created)
- Filter: `is:open status:!"Done"`
- Show fields: Priority, Milestone, Size, Assignee
- Sort: Priority, then Milestone

**Purpose**: Separate views for Frontend, Backend, Testing teams

---

### View 3: Timeline (Gantt)

**Create new view**: "Timeline"

**Configuration**:
- Layout: Roadmap
- Group by: Milestone
- Color by: Track (when available)
- Date range: Next 6 months
- Show fields: Priority, Size, Assignee

**Purpose**: Visual timeline of milestone progress

---

### View 4: Priority Matrix

**Create new view**: "Priority Matrix"

**Configuration**:
- Layout: Table
- Group by: Priority
- Filter: `is:open`
- Show all fields
- Sort: Milestone, then Issue number

**Purpose**: Quick prioritization and assignment

---

## ⚡ Automation Rules

### Rule 1: Auto-Status on PR

```yaml
Trigger: Pull request opened
Condition: PR links to issue
Action: Set Status to "In progress"
```

### Rule 2: Auto-Complete

```yaml
Trigger: Issue closed
Action: Set Status to "Done"
```

### Rule 3: New Issues

```yaml
Trigger: Issue created with label "board-game-ai"
Action: Add to project
```

---

## 📊 Data Quality Checklist

After bulk updates, verify:

- [ ] All current sprint issues (Month 6) have Priority=P0
- [ ] High-priority milestones (FASE 1, MVP Sprints) have Priority=P1
- [ ] All issues have Size assigned (at least default M)
- [ ] Track field created and populated for all issues
- [ ] Sprint Board view shows current work correctly
- [ ] Team Tracks view separates FE/BE/Testing
- [ ] Timeline view shows milestone progression

---

## 🔄 Maintenance Tasks

### Weekly
- [ ] Update priorities as sprints progress
- [ ] Move completed items to Done
- [ ] Review and assign new issues
- [ ] Update size estimates based on actuals

### Per Milestone
- [ ] Bulk update next sprint to P0
- [ ] Move current sprint items to Done
- [ ] Review backlog priorities
- [ ] Update timeline view

---

## 📈 Metrics to Track

### Sprint Velocity
- Issues completed per 2-week sprint
- By track (FE, BE, Testing separately)
- By size (story points)

### Cycle Time
- Time from Todo → In Progress
- Time from In Progress → Done
- Total cycle time per issue

### Parallelization Efficiency
- FE/BE issues worked simultaneously
- Blocked issues waiting for dependencies
- Integration success rate

---

## 🚀 Quick Start Actions

**Immediate (Today)**:
1. Create Track field (Project Settings → Fields)
2. Add options: Frontend, Backend, Testing, Documentation, DevOps, Other
3. Filter `milestone:"Month 6"` → Set all to Priority=P0
4. Create Sprint Board view

**This Week**:
5. Bulk assign Track based on labels
6. Bulk assign Size (default M, adjust critical issues)
7. Create Team Tracks view
8. Set up automation rules

**Next Week**:
9. Create Timeline/Gantt view
10. Set up saved filters for common queries
11. Train team on project usage
12. Start tracking velocity metrics

---

## 📖 References

- Current planning docs: `docs/planning/`
- Issue distribution: See `PROJECT_SUMMARY.md`
- Parallel capacity: See `development-calendar.md`
- Milestone breakdown: See `visual-roadmap.md`

---

**Generated**: 2025-11-12 21:05
**All 157 issues imported** ✅
**Next**: Organize with bulk edits and create views
