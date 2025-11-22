# Development Utilities

Scripts for improving developer experience and productivity.

## Scripts

### 💻 **open-dual-vscode.{sh,ps1}**
**Purpose:** Open VS Code with backend and frontend workspaces side-by-side

**What it does:**
- Opens `apps/api/` in one VS Code window
- Opens `apps/web/` in another VS Code window
- Enables parallel backend/frontend development

**Usage:**
```bash
# Bash (Linux/macOS)
bash tools/development/open-dual-vscode.sh

# PowerShell (Windows)
.\tools\development\open-dual-vscode.ps1
```

**Who:** Full-stack developers working on both backend and frontend
**When:** Starting development session
**Requirements:** VS Code installed in PATH (`code` command available)

**Alternative:** Use VS Code workspaces feature instead

---

### 📊 **analyze-complexity.ps1**
**Purpose:** Analyze code complexity metrics (cyclomatic complexity, LOC)

**What it does:**
- Scans C# and TypeScript files
- Calculates cyclomatic complexity per method/function
- Identifies complex code that needs refactoring
- Generates report with hotspots

**Usage:**
```powershell
# Analyze entire codebase
.\tools\development\analyze-complexity.ps1

# Analyze specific directory
.\tools\development\analyze-complexity.ps1 -Path apps/api/src

# Set complexity threshold
.\tools\development\analyze-complexity.ps1 -Threshold 15

# Generate HTML report
.\tools\development\analyze-complexity.ps1 -Html
```

**Output:**
```
Complexity Analysis Report
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
High Complexity (>15):
  UserService.CreateUser()     CC: 23
  RagService.Search()           CC: 18

Medium Complexity (10-15):
  AuthController.Login()        CC: 12
  ...
```

**Who:** Tech leads, developers during refactoring
**When:** Code quality reviews, before major refactoring
**Requirements:** PowerShell 5.1+

**Tools used:**
- C#: Built-in analysis (no external tools required)
- TypeScript: Analyzes AST complexity

---

## Recommended Actions

✅ **KEEP:** `open-dual-vscode.{sh,ps1}` - Useful for full-stack developers
✅ **KEEP:** `analyze-complexity.ps1` - Useful for code quality initiatives

---

**Last Updated:** 2025-11-22
**Maintained by:** Development team
