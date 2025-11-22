# Cleanup & Maintenance Scripts

Scripts for cleaning caches, removing temporary files, and maintaining repository health.

## Scripts

### 🧹 **cleanup-caches.sh** & **cleanup-caches.ps1**
**Purpose:** Clean all cache directories and build artifacts to free disk space

**What it does:**
1. **Frontend caches:** `.next/`, `node_modules/.cache/`, `apps/web/.turbo/`
2. **Backend artifacts:** `bin/`, `obj/`, `TestResults/`, `.dotnet/`
3. **Docker caches:** Build cache, dangling images, unused volumes
4. **Serena/Claude caches:** `.serena/`, `codeql-db/`, `.claude/`, `.playwright-mcp/`
5. **Package manager caches:** npm/pnpm cache, NuGet packages cache

**Usage:**
```bash
# Dry run - show what would be deleted
bash tools/cleanup/cleanup-caches.sh --dry-run

# Clean everything
bash tools/cleanup/cleanup-caches.sh

# Skip build artifacts
bash tools/cleanup/cleanup-caches.sh --skip-build

# Verbose output
bash tools/cleanup/cleanup-caches.sh --verbose

# Skip confirmation prompt
bash tools/cleanup/cleanup-caches.sh --yes
```

**PowerShell (Windows):**
```powershell
# Dry run
.\tools\cleanup\cleanup-caches.ps1 -DryRun

# Clean everything
.\tools\cleanup\cleanup-caches.ps1

# Skip confirmation
.\tools\cleanup\cleanup-caches.ps1 -Force
```

**Output:**
```
🧹 MeepleAI Cache Cleanup
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 Frontend caches:     1.2 GB freed
 Backend artifacts:   450 MB freed
 Docker caches:       3.5 GB freed
 AI/MCP caches:       800 MB freed
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 Total freed:         5.95 GB
```

**Who uses it:** All developers, DevOps team
**When:**
- Monthly maintenance (recommended)
- Before major refactoring
- When disk space is low
- After switching branches extensively

**Requirements:** Bash or PowerShell, Docker (optional)
**Safety:** Non-destructive - only removes cache/build files, not source code

---

### 🧪 **cleanup-test-processes.ps1**
**Purpose:** Kill hanging test processes (Playwright, Jest, dotnet test) on Windows

**What it does:**
1. Finds processes: `node.exe` (Playwright/Jest), `dotnet.exe`, `testhost.exe`
2. Filters for test-related processes (checks command line arguments)
3. Terminates processes gracefully, then forcefully if needed
4. Prevents port conflicts from zombie test processes

**Usage:**
```powershell
# Kill all hanging test processes
.\tools\cleanup\cleanup-test-processes.ps1

# Dry run - show processes without killing
.\tools\cleanup\cleanup-test-processes.ps1 -WhatIf

# Verbose output
.\tools\cleanup\cleanup-test-processes.ps1 -Verbose
```

**Common scenarios:**
- Playwright browser processes stuck after test failure
- `npm run test:watch` left running in background
- `dotnet test` hung waiting for debugger

**Who uses it:** Windows developers experiencing test hangs
**When:**
- After Ctrl+C interrupted test run
- Port 3000/8080 already in use errors
- "Process is using the file" errors

**Requirements:** PowerShell 5.1+, Administrator privileges (for force-kill)
**Alternative (Linux/macOS):** `pkill -f "node.*test"`, `pkill dotnet`

---

### 🔍 **cleanup-duplicate-issues.sh**
**Purpose:** Find and close duplicate GitHub issues

**What it does:**
1. Fetches all open issues via GitHub CLI (`gh`)
2. Compares issue titles for similarity (fuzzy matching)
3. Identifies duplicates based on:
   - Exact title match
   - Similar title (>80% similarity)
   - Same labels + similar description
4. Generates report of potential duplicates
5. Optionally auto-closes duplicates with comment

**Usage:**
```bash
# Find duplicates (dry run)
bash tools/cleanup/cleanup-duplicate-issues.sh

# Auto-close duplicates
bash tools/cleanup/cleanup-duplicate-issues.sh --close

# Custom similarity threshold (default: 0.8)
bash tools/cleanup/cleanup-duplicate-issues.sh --threshold 0.9
```

**Output:**
```
Found 3 potential duplicate groups:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Group 1 (exact match):
  #427 - Fix PDF upload validation
  #429 - Fix PDF upload validation
  → Close #429 as duplicate of #427

Group 2 (similar):
  #401 - Improve chat performance
  #405 - Optimize chat rendering
  → Manual review recommended
```

**Who uses it:** Repository maintainers, project managers
**When:**
- Monthly issue cleanup
- Before milestone planning
- After bulk issue imports
- When issue count is high

**Requirements:** GitHub CLI (`gh`), repository access
**Safety:** Shows preview before closing, can undo closures

---

## Maintenance Schedule

### Daily
- ❌ Not needed

### Weekly
- ✅ **cleanup-test-processes.ps1** (if tests are hanging)

### Monthly
- ✅ **cleanup-caches.sh** - Free disk space
- ✅ **cleanup-duplicate-issues.sh** - Organize issue tracker

### As Needed
- **cleanup-test-processes.ps1** - When ports are blocked
- **cleanup-duplicate-issues.sh** - Before sprint planning

---

## Automation

### Add to cron (Linux/macOS):
```bash
# Monthly cache cleanup (1st of month, 2 AM)
0 2 1 * * /home/user/meepleai-monorepo/tools/cleanup/cleanup-caches.sh --yes
```

### Add to Task Scheduler (Windows):
```powershell
# Monthly cache cleanup
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" `
  -Argument "-File C:\meepleai-monorepo\tools\cleanup\cleanup-caches.ps1 -Force"
$trigger = New-ScheduledTaskTrigger -Monthly -DaysOfMonth 1 -At 2am
Register-ScheduledTask -TaskName "MeepleAI Cache Cleanup" `
  -Action $action -Trigger $trigger
```

---

## Disk Space Impact

**Typical cleanup results:**
- Frontend caches: 500 MB - 2 GB
- Backend artifacts: 200 MB - 1 GB
- Docker caches: 2 GB - 10 GB
- AI/MCP caches: 500 MB - 2 GB
- **Total: 3 GB - 15 GB** (varies by usage)

**Safe to run:** Yes - only removes regenerable files

---

## Troubleshooting

**Cache cleanup fails on locked files (Windows):**
```powershell
# Close Visual Studio, VS Code, Docker Desktop
# Then retry
.\tools\cleanup\cleanup-caches.ps1
```

**Docker cleanup requires sudo (Linux):**
```bash
# Add user to docker group
sudo usermod -aG docker $USER
# Logout and login again
```

**Duplicate issues script fails:**
```bash
# Ensure GitHub CLI is authenticated
gh auth status

# Login if needed
gh auth login
```

---

## Best Practices

1. **Always dry-run first:**
   ```bash
   bash tools/cleanup/cleanup-caches.sh --dry-run
   ```

2. **Cleanup before disk-intensive operations:**
   - Before pulling large Docker images
   - Before big dependency updates
   - Before recording coverage reports

3. **Don't run during active development:**
   - Wait for builds to finish
   - Close IDEs first
   - Stop Docker containers

---

**Last Updated:** 2025-11-22
**Maintained by:** DevOps team
**See also:** `CLEANUP-SUMMARY-2025-11-11.md` in tools/
