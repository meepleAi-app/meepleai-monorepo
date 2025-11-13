# VS Code Configuration Summary

**Created**: 2025-01-15
**Status**: COMPLETE

---

## Configuration Files Created

### Backend Worktree (.vscode/ in meepleai-monorepo-backend)

| File | Purpose |
|------|---------|
| `settings.json` | Hide frontend, set terminal to apps/api, blue status bar |
| `launch.json` | Debug .NET Core API with F5 |
| `tasks.json` | Build/Test/Run tasks (Ctrl+Shift+B) |
| `extensions.json` | Recommended extensions (C# Dev Kit, etc.) |

### Frontend Worktree (.vscode/ in meepleai-monorepo-frontend)

| File | Purpose |
|------|---------|
| `settings.json` | Hide backend, set terminal to apps/web, green status bar |
| `launch.json` | Debug Next.js with F5 |
| `tasks.json` | Dev/Build/Test/Lint tasks |
| `extensions.json` | Recommended extensions (ESLint, Prettier, etc.) |

---

## Key Features Configured

### Backend VS Code

- **Status Bar**: Blue (#004494) - Visual ID for backend
- **Window Title**: "Backend - [filename]"
- **Hidden Folders**: apps/web, node_modules, .next
- **Terminal Path**: Automatically opens in apps/api/
- **Format on Save**: Enabled for C#
- **Default Solution**: apps/api/Api.sln
- **Build Task**: Ctrl+Shift+B runs dotnet build
- **Debug**: F5 launches API with debugger attached

### Frontend VS Code

- **Status Bar**: Green (#34a853) - Visual ID for frontend
- **Window Title**: "Frontend - [filename]"
- **Hidden Folders**: apps/api, bin, obj, *.csproj
- **Terminal Path**: Automatically opens in apps/web/
- **Format on Save**: Enabled for TS/JS/JSON (Prettier)
- **ESLint**: Auto-fix on save
- **Tailwind IntelliSense**: Class completion
- **Build Task**: Ctrl+Shift+B runs pnpm build
- **Debug**: F5 launches Next.js with debugger

---

## How to Use

### Open Both VS Code Instances

```bash
# Option 1: Manual (Recommended)
code D:\Repositories\meepleai-monorepo-backend
code D:\Repositories\meepleai-monorepo-frontend

# Option 2: Script (if PowerShell works)
powershell.exe -ExecutionPolicy Bypass -File .\tools\open-dual-vscode.ps1
```

### Install Recommended Extensions

When you open each VS Code instance, you'll see a popup:
- "This workspace has extension recommendations"
- Click "Install All" or "Show Recommendations"

**Backend Extensions**:
- C# Dev Kit
- .NET Core Test Explorer
- REST Client
- GitLens
- Error Lens

**Frontend Extensions**:
- ESLint
- Prettier
- Tailwind CSS IntelliSense
- Jest Runner
- Error Lens
- GitLens

### Verify Configuration

**Backend VS Code**:
1. Open terminal (Ctrl+`)
2. Should automatically be in `apps/api/`
3. Status bar should be blue
4. Window title shows "Backend - ..."
5. Explorer sidebar should NOT show `apps/web/`

**Frontend VS Code**:
1. Open terminal (Ctrl+`)
2. Should automatically be in `apps/web/`
3. Status bar should be green
4. Window title shows "Frontend - ..."
5. Explorer sidebar should NOT show `apps/api/`

---

## Keyboard Shortcuts

### Common (Both VS Code)

| Action | Shortcut |
|--------|----------|
| Command Palette | Ctrl+Shift+P |
| Quick Open File | Ctrl+P |
| Toggle Terminal | Ctrl+` |
| Split Terminal | Ctrl+Shift+5 |
| Source Control | Ctrl+Shift+G |
| Run Task | Ctrl+Shift+B |
| Debug | F5 |
| Toggle Sidebar | Ctrl+B |

### Backend Specific

| Action | Shortcut | Task |
|--------|----------|------|
| Build | Ctrl+Shift+B | dotnet build |
| Test | Ctrl+Shift+T | dotnet test |
| Run | F5 | dotnet run (debug) |

### Frontend Specific

| Action | Shortcut | Task |
|--------|----------|------|
| Build | Ctrl+Shift+B | pnpm build |
| Dev Server | Ctrl+Shift+T then type "dev" | pnpm dev |
| Test | Ctrl+Shift+T then type "test" | pnpm test |
| Lint | Ctrl+Shift+T then type "lint" | pnpm lint |

---

## Testing the Setup

### Backend VS Code Test

```bash
# In integrated terminal (should already be in apps/api/)
pwd  # Should show: .../meepleai-monorepo-backend/apps/api

# Build
dotnet build  # Should succeed

# Run
dotnet run  # Should start on :8080

# Test
dotnet test  # Should run all tests
```

### Frontend VS Code Test

```bash
# In integrated terminal (should already be in apps/web/)
pwd  # Should show: .../meepleai-monorepo-frontend/apps/web

# Install (first time)
pnpm install

# Dev server
pnpm dev  # Should start on :3000

# Test
pnpm test  # Should run Jest tests
```

### Integration Test

1. **Backend VS Code**: `dotnet run` (Terminal 1)
2. **Frontend VS Code**: `pnpm dev` (Terminal 1)
3. **Browser**: http://localhost:3000
4. **DevTools Network**: Should see calls to http://localhost:8080/api

SUCCESS: Full-stack development ready!

---

## Troubleshooting

### Issue: Terminal doesn't open in correct directory

**Fix Backend**:
- Press Ctrl+Shift+P
- Type "Terminal: Kill All"
- Open new terminal (Ctrl+`)
- Should now be in apps/api/

**Fix Frontend**:
- Same steps
- Should now be in apps/web/

### Issue: Status bar color not showing

VS Code sometimes caches colors. Fix:
- Ctrl+Shift+P
- "Developer: Reload Window"
- Status bar color should appear

### Issue: Extensions not working

**Backend (C# Dev Kit)**:
- Ctrl+Shift+P
- "OmniSharp: Restart OmniSharp"
- Wait for analysis to complete

**Frontend (ESLint)**:
- Ctrl+Shift+P
- "ESLint: Restart ESLint Server"
- Check output panel for errors

---

## Next Steps

1. Open both VS Code instances
2. Install recommended extensions (click the popup)
3. Verify terminal paths are correct
4. Test build/run commands
5. Start developing!

**First Task**: Issue #877 (AdminDashboardService)
**Reference**: claudedocs/calendario-sviluppo-1-persona-2025.md

---

**Configuration Status**: COMPLETE
**Ready to Code**: YES
