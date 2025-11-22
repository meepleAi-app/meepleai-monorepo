# Admin Console - GitHub Issues Creation Scripts

Automated scripts to create all 49 GitHub issues for Admin Console implementation.

## 📋 Available Scripts

### 1. Bash Script (Linux/Mac) ⭐ **RECOMMENDED**
```bash
# Make executable
chmod +x tools/create-admin-console-issues.sh

# Dry run (preview without creating)
./tools/create-admin-console-issues.sh --dry-run

# Create issues
./tools/create-admin-console-issues.sh
```

### 2. PowerShell Script (Windows)
```powershell
# Dry run
pwsh tools/create-admin-console-issues.ps1 -DryRun

# Create issues
pwsh tools/create-admin-console-issues.ps1
```

### 3. Node.js Script (Cross-platform)
```bash
# Install dependencies (if needed)
npm install

# Dry run
node tools/create-admin-console-issues.js --dry-run

# Create issues
node tools/create-admin-console-issues.js
```

---

## 🔧 Prerequisites

### All Scripts Require:

1. **GitHub CLI (gh)** installed and authenticated
   ```bash
   # Install (choose your platform)
   # macOS
   brew install gh

   # Windows
   winget install GitHub.cli

   # Linux
   curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | sudo dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg
   echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null
   sudo apt update
   sudo apt install gh

   # Authenticate
   gh auth login
   ```

2. **Repository access** (must have write permission to create issues)

---

## 📊 What Gets Created

### Labels (15 total)
- `admin-console` - All admin console issues
- `fase-1-dashboard`, `fase-2-infrastructure`, `fase-3-management`, `fase-4-advanced` - Phase labels
- `backend`, `frontend`, `testing` - Task type labels
- `mvp` - MVP features (FASE 1-2)
- `epic` - Epic issues
- `component`, `reusable` - UI component labels
- `performance`, `security`, `email` - Special concern labels

### Milestones (4 total)
- **FASE 1: Dashboard Overview** (Due: 2 weeks from today)
- **FASE 2: Infrastructure Monitoring** (Due: 4 weeks from today)
- **FASE 3: Enhanced Management** (Due: 6 weeks from today)
- **FASE 4: Advanced Features** (Due: 7 weeks from today)

### Issues (49 total)
- **FASE 1**: 16 issues (1 epic + 6 backend + 6 frontend + 3 testing)
- **FASE 2**: 13 issues (1 epic + 5 backend + 5 frontend + 2 integration)
- **FASE 3**: 12 issues (1 epic + 4 backend + 5 frontend + 2 testing)
- **FASE 4**: 8 issues (1 epic + 3 backend + 2 frontend + 1 testing)

---

## 🚀 Usage Instructions

### Step 1: Dry Run (Preview)

Always run with `--dry-run` first to see what would be created:

```bash
# Bash
./tools/create-admin-console-issues.sh --dry-run

# PowerShell
pwsh tools/create-admin-console-issues.ps1 -DryRun

# Node.js
node tools/create-admin-console-issues.js --dry-run
```

**Output**:
```
========================================
Admin Console GitHub Issues Creator
========================================

DRY RUN MODE - No issues will be created

✓ GitHub CLI authenticated

STEP 1: Creating Labels...
  [DRY RUN] Would ensure label: admin-console
  [DRY RUN] Would ensure label: fase-1-dashboard
  ...

STEP 2: Creating Milestones...
  [DRY RUN] Would ensure milestone: FASE 1: Dashboard Overview
  ...

STEP 3: Creating Issues...
  [DRY RUN] Would create: #1 - FASE 1: Dashboard Overview
  [DRY RUN] Would create: #2 - AdminDashboardService.cs
  ...
```

### Step 2: Create Issues

If dry run looks good, run without `--dry-run`:

```bash
./tools/create-admin-console-issues.sh
```

**Output**:
```
✓ Created label: admin-console
✓ Created label: fase-1-dashboard
...
✓ Created milestone: FASE 1: Dashboard Overview
...
✓ Created: #427 - FASE 1: Dashboard Overview
✓ Created: #428 - AdminDashboardService.cs
...

Summary
========================================
✓ Created 49 issues
```

### Step 3: Verify on GitHub

```bash
# List created issues
gh issue list --label admin-console --limit 100

# Or open in browser
gh repo view --web
```

---

## ⚙️ Script Options

### Bash Script
```bash
./tools/create-admin-console-issues.sh [--dry-run]
```

Options:
- `--dry-run` - Preview without creating (optional)

### PowerShell Script
```powershell
pwsh tools/create-admin-console-issues.ps1 [-DryRun] [-Repository <name>]
```

Parameters:
- `-DryRun` - Preview without creating (switch, optional)
- `-Repository <name>` - Repository name (default: meepleai-monorepo)

### Node.js Script
```bash
node tools/create-admin-console-issues.js [--dry-run]
```

Options:
- `--dry-run` - Preview without creating (optional)

---

## 🔍 Troubleshooting

### Error: GitHub CLI not found
```
ERROR: GitHub CLI (gh) is not installed!
```
**Solution**: Install GitHub CLI (see Prerequisites section above)

### Error: Not authenticated
```
ERROR: Not authenticated with GitHub!
```
**Solution**: Run `gh auth login` and follow prompts

### Error: Permission denied
```
ERROR: You do not have permission to create issues in this repository
```
**Solution**: Ensure you have write access to the repository OR fork it first

### Error: Label already exists
```
⚠ Label creation warning (may already exist): admin-console
```
**Solution**: This is just a warning, script will continue (label already exists from previous run)

### Error: Milestone already exists
```
⚠ Milestone creation warning: FASE 1: Dashboard Overview
```
**Solution**: This is just a warning, script will continue (milestone already exists from previous run)

### Error: Rate limit exceeded
```
ERROR: API rate limit exceeded for user
```
**Solution**: Wait 1 hour or authenticate with token that has higher rate limit

---

## 📝 Manual Alternative

If scripts fail, you can create issues manually using the template in:
```
claudedocs/github_issues_admin_console.md
```

Each issue has:
- Title
- Description (with tasks, effort, dependencies)
- Labels
- Milestone
- Issue number reference

Copy-paste each issue template into GitHub's "New Issue" form.

---

## 🎯 Next Steps After Issue Creation

1. **Create GitHub Project Board**
   ```bash
   # Via CLI
   gh project create --title "Admin Console Implementation" --body "Admin Console 7-week implementation roadmap"

   # Or manually at: https://github.com/your-org/meepleai-monorepo/projects
   ```

2. **Add Issues to Project**
   ```bash
   # Add all admin-console issues to project
   gh project item-add <project-id> --owner <owner> --url <issue-url>
   ```

3. **Setup Project Board Columns**
   - Backlog
   - Todo
   - In Progress
   - Review
   - Done

4. **Assign Issues**
   ```bash
   # Assign issue to developer
   gh issue edit <issue-number> --add-assignee <username>
   ```

5. **Start with FASE 1 (MVP)**
   - Move FASE 1 issues (label: `mvp`) to "Todo" column
   - Assign to team members
   - Begin implementation

---

## 📚 Related Documentation

- **Implementation Plan**: `claudedocs/admin_console_implementation_plan.md` (7-week roadmap, 280h)
- **Quick Reference**: `claudedocs/admin_console_quick_reference.md` (exec summary)
- **Issue Details**: `claudedocs/github_issues_admin_console.md` (full issue templates)
- **Integration Plan**: `claudedocs/ddd_admin_integration_plan.md` (DDD + Admin Console)

---

## 🤝 Contributing

If you find issues with the scripts:
1. Check prerequisites (gh CLI installed + authenticated)
2. Run with `--dry-run` first
3. Check error messages for specific issues
4. Report bugs with full error output

---

## 📄 License

Same as main repository.
