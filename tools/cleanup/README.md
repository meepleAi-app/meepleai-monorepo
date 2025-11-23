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

### 🔧 **fix-vscode-cache-windows.ps1**
**Purpose:** Fix VS Code cache permission errors on Windows

**What it does:**
1. Validates Windows environment and VS Code installation
2. Checks for Administrator privileges (warns if insufficient)
3. Prompts for confirmation if VS Code is running (warns about unsaved work)
4. Closes all VS Code related processes (main process + helpers)
5. Cleans VS Code cache directories (Cache, GPUCache, CachedData, Code Cache)
6. Cleans Chromium/Electron cache directories
7. Removes lock files that prevent VS Code from starting
8. Provides detailed summary and recommendations

**Common errors this fixes:**
- `ERROR:net\disk_cache\cache_util_win.cc:20] Unable to move the cache: Accesso negato. (0x5)`
- `ERROR:disk_cache.cc:216] Unable to create cache`
- `ERROR:gpu_disk_cache.cc:723] Gpu Cache Creation failed: -2`
- `Error mutex already exists`

**Usage:**
```powershell
# Dry run - show what would be deleted
.\tools\cleanup\fix-vscode-cache-windows.ps1 -DryRun

# Fix cache issues (with confirmation prompt)
.\tools\cleanup\fix-vscode-cache-windows.ps1

# Skip confirmation prompt (automated use)
.\tools\cleanup\fix-vscode-cache-windows.ps1 -Yes

# Verbose output
.\tools\cleanup\fix-vscode-cache-windows.ps1 -Verbose
```

**Output:**
```
🔧 Fix VS Code Cache Errors (Windows)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️  WARNING: VS Code is currently running

This script will:
  1. Close all VS Code instances (may lose unsaved work)
  2. Delete cache directories (safe, but regenerated on restart)
  3. Remove lock files

💡 Recommendation: Save all your work in VS Code before continuing!

Continue? (y/N): y

Step 1: Closing VS Code instances
  ✓ Closed Code (PID: 12345)
  ✓ Closed Code Helper (PID: 12346)

Step 2: Cleaning VS Code cache directories
  Deleting: VS Code Cache (245 MB)
  ✓ Deleted successfully

Step 3: Cleaning Chromium/Electron cache
  Deleting: Chromium GPU Cache (12 MB)

Step 4: Removing lock files
  ✓ Removed lock file: SingletonLock
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Cache cleanup complete!
  Space freed: 257 MB
  VS Code processes closed: 2
```

**Who uses it:** Windows developers experiencing VS Code cache/permission errors
**When:**
- VS Code shows cache permission errors on startup
- "Access Denied (0x5)" errors in VS Code console
- "Unable to create cache" or "GPU Cache Creation failed" errors
- VS Code won't start or hangs during startup
- After antivirus software blocks VS Code cache access

**Requirements:** PowerShell 5.1+, Windows 10/11
**Safety:**
- Prompts for confirmation before closing VS Code
- Only removes cache files (preserves settings and extensions)
- Dry-run mode available for preview
- Graceful process termination (tries CloseMainWindow before Kill)
**Alternative (Linux/macOS):** Not needed - cache permission issues are Windows-specific

**Prevention tips:**
- Always close VS Code properly (File > Exit, not Task Manager)
- Add VS Code directories to antivirus exclusions
- Avoid running multiple VS Code instances from different terminals

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
