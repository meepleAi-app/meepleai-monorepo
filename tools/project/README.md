# Project Management Tools

Scripts for GitHub Projects, roadmap management, and issue organization.

## Scripts

### 📋 **apply-project-roadmap.py**
**Purpose:** Apply project roadmap to GitHub Project board fields
**What it does:**
- Populates Phase, Branch, Checkpoint fields in GitHub Projects
- Maps issue labels to project board columns
- Assigns issues based on roadmap phases

**Usage:**
```bash
python tools/project/apply-project-roadmap.py
```

**Configuration:**
- Project ID: `PVT_kwHOAOeY484BIC8G` (hardcoded)
- Field IDs for Phase, Branch, Checkpoint
- Phase-to-Checkpoint mappings

**Who:** Project manager syncing roadmap to GitHub Projects
**When:** Sprint planning, after major roadmap changes
**Requirements:** Python 3.9+, GitHub CLI, project admin access

---

### 📊 **generate-project-import.py**
**Purpose:** Generate CSV for bulk importing issues to GitHub Projects
**What it does:**
1. Reads `all_issues_raw.json` from planning docs
2. Extracts issue metadata (title, labels, milestone, priority)
3. Determines status, track, size based on labels
4. Outputs `github-project-import.csv`

**Usage:**
```bash
python tools/project/generate-project-import.py
```

**Output:**
- File: `apps/web/docs/planning/github-project-import.csv`
- Columns: Issue #, Title, Status, Priority, Milestone, Track, Size, Labels, URL

**Who:** Project manager importing issues to new project board
**When:** Project initialization, major reorganization
**Requirements:** Python 3.9+, `all_issues_raw.json` file

**Statistics output:**
- Issues by Milestone
- Issues by Track
- Issues by Priority

---

## Workflow

### Initial Project Setup
```bash
# 1. Generate import CSV
python tools/project/generate-project-import.py

# 2. Import to GitHub Projects (manual via UI)
# - Go to GitHub Projects
# - Import CSV file
# - Map columns

# 3. Apply roadmap fields
python tools/project/apply-project-roadmap.py
```

### Ongoing Maintenance
```bash
# Sync roadmap changes to project board
python tools/project/apply-project-roadmap.py
```

---

**Requirements:**
- Python 3.9+
- GitHub CLI authenticated (`gh auth login`)
- Project admin access

---

**Last Updated:** 2025-11-22
**Maintained by:** Project management team
