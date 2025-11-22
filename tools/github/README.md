# GitHub Issue & Label Management

Scripts for bulk creating GitHub issues, managing labels, and automating issue triage.

## Issue Creation Scripts

### 📋 **create-frontend-issues.sh**
**Purpose:** Create 8 frontend improvement issues (FE-IMP-001 to FE-IMP-008)
**Epic:** Frontend architecture improvements
**Issues created:** App Router, Server Actions, TanStack Query, Auth, API SDK, Forms, Chat Store, Upload Queue
**Usage:** `bash tools/github/create-frontend-issues.sh`
**Who:** Project managers starting Frontend Improvement epic

### 🎮 **create-bgai-issues.sh**
**Purpose:** Create Board Game AI (BGAI) feature issues
**Epic:** AI-powered game rules assistant
**Issues created:** PDF extraction, RAG pipeline, chat interface, admin tools
**Usage:** `bash tools/github/create-bgai-issues.sh`
**Who:** Product owners planning BGAI features

### 🔧 **create-api-improvement-issues.sh**
**Purpose:** Create backend/API improvement issues
**Epic:** Backend architecture enhancements
**Issues created:** DDD refactoring, CQRS handlers, repository patterns
**Usage:** `bash tools/github/create-api-improvement-issues.sh`
**Who:** Backend tech lead planning refactoring sprint

### 👨‍💼 **create-admin-console-issues.{sh,ps1,js}**
**Purpose:** Create admin console feature issues (3 versions: Bash, PowerShell, Node.js)
**Epic:** Admin dashboard and monitoring
**Issues created:** User management, analytics, system health, configuration
**Usage:**
- Bash: `bash tools/github/create-admin-console-issues.sh`
- PowerShell: `.\tools\github\create-admin-console-issues.ps1`
- Node.js: `node tools/github/create-admin-console-issues.js`
**Who:** Admin features team lead

### 🚀 **create-all-bgai-issues.sh**
**Purpose:** Create ALL BGAI-related issues in batch (master script)
**Issues created:** 30+ issues across BGAI phases (Month 1-6)
**Usage:** `bash tools/github/create-all-bgai-issues.sh`
**Who:** Project manager initializing BGAI project
**Warning:** Creates many issues - use once per major milestone

### 🏷️ **create-bgai-labels.sh**
**Purpose:** Create BGAI-specific labels (phases, priorities, components)
**Labels:** `bgai:phase-1`, `bgai:pdf`, `bgai:rag`, `bgai:chat`, etc.
**Usage:** `bash tools/github/create-bgai-labels.sh`
**Who:** Repository admin setting up BGAI taxonomy

### 📊 **generate-mvp-issues.{sh,ps1}**
**Purpose:** Generate MVP (Minimum Viable Product) issues from roadmap
**Source:** Reads from planning documents
**Issues created:** MVP scope broken down into actionable issues
**Usage:**
- Bash: `bash tools/github/generate-mvp-issues.sh`
- PowerShell: `.\tools\github\generate-mvp-issues.ps1`
**Who:** Product manager defining MVP scope

---

## Issue Management Scripts

### 🔒 **lock-closed-issues.{sh,ps1}**
**Purpose:** Lock closed issues to prevent spam and off-topic comments

**What it does:**
1. Fetches all closed issues (via GitHub CLI)
2. Filters issues closed > 30 days ago
3. Locks conversations with "resolved" reason
4. Adds comment explaining lock

**Usage:**
```bash
# Dry run - show which issues would be locked
bash tools/github/lock-closed-issues.sh --dry-run

# Lock closed issues
bash tools/github/lock-closed-issues.sh

# Lock issues closed > 60 days ago
bash tools/github/lock-closed-issues.sh --days 60
```

**Who uses it:** Repository maintainers
**When:** Monthly cleanup, before major releases
**Requirements:** GitHub CLI (`gh`), push access

---

### 🔍 **run-issue-triage.sh**
**Purpose:** Automated issue triage and prioritization

**What it does:**
1. Analyzes untriaged issues (`status:needs-triage` label)
2. Suggests labels based on title/body keywords
3. Assigns priority based on severity keywords
4. Generates triage report

**Usage:**
```bash
# Run triage analysis
bash tools/github/run-issue-triage.sh

# Auto-apply suggested labels
bash tools/github/run-issue-triage.sh --apply

# Generate report only
bash tools/github/run-issue-triage.sh --report > triage-report.md
```

**Output:** `triage-summary.txt`, `issue-triage-analysis.md`
**Who uses it:** Issue triagers, project managers
**When:** Weekly during planning meetings
**Requirements:** GitHub CLI, write access

---

### 📌 **assign-infrastructure-milestones.sh**
**Purpose:** Bulk assign infrastructure issues to milestones

**What it does:**
- Finds issues with `area:infra` label
- Assigns to appropriate milestone based on priority
- Groups by epic/phase

**Usage:**
```bash
# Assign infra issues to milestones
bash tools/github/assign-infrastructure-milestones.sh

# Dry run
bash tools/github/assign-infrastructure-milestones.sh --dry-run
```

**Who uses it:** Infrastructure team lead
**When:** Sprint planning, milestone organization

---

### 📍 **phase-2-issue-labels.sh**
**Purpose:** Apply phase-2 specific labels to issues

**What it does:**
- Adds `phase-2` label to relevant issues
- Updates priorities for phase 2 scope
- Links issues to Phase 2 project board

**Usage:** `bash tools/github/phase-2-issue-labels.sh`
**Who uses it:** Project manager transitioning to Phase 2
**When:** Once at start of Phase 2

---

### 📅 **defer-fase-issues.sh**
**Purpose:** Defer non-critical issues to future phases

**What it does:**
- Identifies low-priority issues in current milestone
- Moves to "Backlog" or future milestone
- Adds `deferred` label
- Comments with reason for deferral

**Usage:**
```bash
# Defer low-priority issues
bash tools/github/defer-fase-issues.sh

# Defer to specific milestone
bash tools/github/defer-fase-issues.sh --milestone "Phase 3"
```

**Who uses it:** Scrum master managing scope
**When:** Sprint planning, when capacity is limited

---

## Workflow

### 1. Project Initialization
```bash
# Create all BGAI issues and labels
bash tools/github/create-bgai-labels.sh
bash tools/github/create-all-bgai-issues.sh
```

### 2. Sprint Planning
```bash
# Triage new issues
bash tools/github/run-issue-triage.sh

# Assign to milestones
bash tools/github/assign-infrastructure-milestones.sh

# Defer low-priority items
bash tools/github/defer-fase-issues.sh
```

### 3. Maintenance
```bash
# Lock old closed issues (monthly)
bash tools/github/lock-closed-issues.sh

# Remove duplicates
bash tools/cleanup/cleanup-duplicate-issues.sh
```

---

## Requirements

- **GitHub CLI (`gh`):** All scripts require authenticated `gh` CLI
- **Repository access:** Write permissions needed for creating/modifying issues
- **Bash:** Most scripts are Bash (some have PowerShell alternatives)

**Setup:**
```bash
# Install GitHub CLI
# macOS: brew install gh
# Linux: https://github.com/cli/cli#installation

# Authenticate
gh auth login

# Verify access
gh issue list --repo DegrassiAaron/meepleai-monorepo
```

---

## Best Practices

1. **Always dry-run first** when modifying existing issues
2. **Coordinate with team** before bulk operations
3. **Check rate limits** - GitHub API has limits (5000 requests/hour)
4. **Document** major issue creation events in project wiki
5. **Review output** of triage scripts before applying labels

---

## Troubleshooting

**Rate limit exceeded:**
```bash
# Check rate limit status
gh api rate_limit

# Wait or use different auth token
```

**Permission denied:**
```bash
# Verify authentication
gh auth status

# Ensure you have push access
gh repo view DegrassiAaron/meepleai-monorepo
```

**Script hangs:**
```bash
# Some scripts process many issues - be patient
# Or use --limit flag if available
```

---

**Last Updated:** 2025-11-22
**Maintained by:** Project management team
**See also:** `README-admin-console-issues.md`, `TRIAGE_QUICKREF.md` in tools/
