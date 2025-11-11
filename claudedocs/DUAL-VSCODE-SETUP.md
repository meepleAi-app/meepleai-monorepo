# Dual VS Code Setup - Backend + Frontend Worktrees 🎨

**Status**: ✅ READY
**Updated**: 2025-01-15

---

## 🎯 Configurazione Finale

### 3 Worktree Attivi

```
📁 D:/Repositories/meepleai-monorepo/           (Main - Coordination)
   Branch: main
   Purpose: Merges, releases, coordination only

📁 D:/Repositories/meepleai-monorepo-backend/   (Backend Development)
   Branch: backend-dev
   Purpose: Backend API, Services, Migrations
   VS Code Instance: #1 (Backend)

📁 D:/Repositories/meepleai-monorepo-frontend/  (Frontend Development)
   Branch: frontend-dev
   Purpose: Frontend UI, Components, Pages
   VS Code Instance: #2 (Frontend)
```

**Verifica**: `git worktree list`

---

## 🚀 Apri 2 VS Code Instances

### Opzione 1: Da Terminale (Raccomandato)

```bash
# Terminal 1 - Backend VS Code
code D:/Repositories/meepleai-monorepo-backend

# Terminal 2 - Frontend VS Code
code D:/Repositories/meepleai-monorepo-frontend
```

### Opzione 2: Da VS Code UI

1. **Apri VS Code** → File → Open Folder → `D:/Repositories/meepleai-monorepo-backend`
2. **Apri 2° VS Code** → File → New Window → Open Folder → `D:/Repositories/meepleai-monorepo-frontend`

### Opzione 3: Da Windows Explorer

1. Naviga a `D:/Repositories/meepleai-monorepo-backend`
2. Click destro → "Open with Code"
3. Naviga a `D:/Repositories/meepleai-monorepo-frontend`
4. Click destro → "Open with Code"

**🎉 Risultato**: 2 finestre VS Code completamente indipendenti!

---

## 🎨 VS Code Workspace Configuration

### Backend Instance (Worktree Backend)

**Folder**: `D:/Repositories/meepleai-monorepo-backend`

#### Workspace Settings (.vscode/settings.json)

```json
{
  "files.exclude": {
    "apps/web": true,
    "node_modules": true,
    "**/*.js.map": true
  },
  "search.exclude": {
    "apps/web": true,
    "**/node_modules": true,
    "**/.next": true
  },
  "terminal.integrated.cwd": "${workspaceFolder}/apps/api",
  "editor.formatOnSave": true,
  "[csharp]": {
    "editor.defaultFormatter": "ms-dotnettools.csharp",
    "editor.formatOnSave": true
  },
  "dotnet.defaultSolution": "apps/api/Api.sln"
}
```

**Recommended Extensions**:
- C# Dev Kit (ms-dotnettools.csdevkit)
- .NET Core Test Explorer
- REST Client
- GitLens
- Error Lens

**Color Theme** (per differenziare):
- Backend: Dark+ (default dark)
- Visual cue: Status bar color (optional)

---

### Frontend Instance (Worktree Frontend)

**Folder**: `D:/Repositories/meepleai-monorepo-frontend`

#### Workspace Settings (.vscode/settings.json)

```json
{
  "files.exclude": {
    "apps/api": true,
    "**/*.dll": true,
    "**/bin": true,
    "**/obj": true
  },
  "search.exclude": {
    "apps/api": true,
    "**/node_modules": true,
    "**/.next": true
  },
  "terminal.integrated.cwd": "${workspaceFolder}/apps/web",
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": "explicit"
  },
  "[typescript]": {
    "editor.defaultFormatter": "esbenp.prettier-vscode"
  },
  "[typescriptreact]": {
    "editor.defaultFormatter": "esbenp.prettier-vscode"
  },
  "typescript.tsdk": "apps/web/node_modules/typescript/lib",
  "tailwindCSS.experimental.classRegex": [
    ["cva\\(([^)]*)\\)", "[\"'`]([^\"'`]*).*?[\"'`]"]
  ]
}
```

**Recommended Extensions**:
- ESLint (dbaeumer.vscode-eslint)
- Prettier (esbenp.prettier-vscode)
- Tailwind CSS IntelliSense
- Jest Runner
- Error Lens
- Auto Rename Tag

**Color Theme** (per differenziare):
- Frontend: One Dark Pro (o Light theme)
- Visual cue: Different sidebar icon color

---

## 💻 Terminal Layout Raccomandato

### Backend VS Code (Instance #1)

**Terminal Layout** (Split 2 panels):

```
┌─────────────────────────────────────────┐
│ Terminal 1: dotnet run (API :8080)      │
│ Terminal 2: dotnet test --watch         │
└─────────────────────────────────────────┘
```

**Commands**:
```bash
# Terminal 1 - API Server
cd apps/api
dotnet run

# Terminal 2 - Test Watcher (Ctrl+Shift+5 per split)
cd apps/api
dotnet test --watch
```

---

### Frontend VS Code (Instance #2)

**Terminal Layout** (Split 2 panels):

```
┌─────────────────────────────────────────┐
│ Terminal 1: pnpm dev (Frontend :3000)   │
│ Terminal 2: pnpm test --watch           │
└─────────────────────────────────────────┘
```

**Commands**:
```bash
# Terminal 1 - Dev Server
cd apps/web
pnpm dev

# Terminal 2 - Test Watcher
cd apps/web
pnpm test --watch
```

---

## 🔄 Workflow Completo con 2 VS Code

### Scenario: Sviluppo Admin Dashboard

#### Setup Iniziale (1 volta, 5 minuti)

```bash
# Backend VS Code
code D:/Repositories/meepleai-monorepo-backend

# Frontend VS Code
code D:/Repositories/meepleai-monorepo-frontend

# ✅ Hai ora 2 finestre VS Code aperte!
```

#### Mattina (9:00-12:00): Backend Development

**Backend VS Code (Finestra #1)**:

```bash
# Terminal 1
cd D:/Repositories/meepleai-monorepo-backend
git checkout -b feature/admin-dashboard-api
cd apps/api
dotnet run  # :8080, lascia running

# Terminal 2 (split terminal)
dotnet test --watch  # Feedback automatico

# Sviluppa in editor
# File: src/Api/Services/AdminDashboardService.cs
# Hot reload attivo, test automatici!
```

**Frontend VS Code (Finestra #2)**:
- Puoi lasciarlo aperto (nessun impatto)
- Oppure chiudere per focus

#### Pomeriggio (13:00-18:00): Frontend Development

**Switcha Focus a Frontend VS Code (Alt+Tab)**:

```bash
# Terminal 1 (Frontend VS Code)
cd D:/Repositories/meepleai-monorepo-frontend
cd apps/web
pnpm dev  # :3000, chiama API :8080

# Terminal 2 (split)
pnpm test --watch  # Test automatici

# Sviluppa in editor
# File: src/components/AdminLayout.tsx
# Hot reload Next.js attivo, API backend risponde!
```

**Backend VS Code (Finestra #1)**:
- API continua running in background!
- Non devi fare nulla, già pronto per frontend

#### Sera (18:00-19:00): Commit Separati

**Backend VS Code (Finestra #1)**:
```bash
# Source Control panel (Ctrl+Shift+G)
git add src/Api/Services/AdminDashboardService.cs
git commit -m "feat(backend): AdminDashboardService #877"
git push origin feature/admin-dashboard-api
```

**Frontend VS Code (Finestra #2)**:
```bash
# Source Control panel (Ctrl+Shift+G)
git add src/components/AdminLayout.tsx
git commit -m "feat(frontend): AdminLayout component #881"
git push origin frontend-dev
```

**🎉 Fine Giornata**:
- ✅ Backend implementato e committato
- ✅ Frontend implementato e committato
- ✅ Zero conflitti (branch separati)
- ✅ Zero context switching overhead

---

## 🎨 Visual Differentiation (Opzionale ma Utile)

### Backend VS Code (Finestra #1)

**Status Bar Color** (settings.json):
```json
{
  "workbench.colorCustomizations": {
    "statusBar.background": "#004494",
    "statusBar.foreground": "#ffffff"
  }
}
```

**Window Title**:
```json
{
  "window.title": "🔧 Backend - ${rootName}${separator}${activeEditorShort}"
}
```

### Frontend VS Code (Finestra #2)

**Status Bar Color** (settings.json):
```json
{
  "workbench.colorCustomizations": {
    "statusBar.background": "#34a853",
    "statusBar.foreground": "#ffffff"
  }
}
```

**Window Title**:
```json
{
  "window.title": "🎨 Frontend - ${rootName}${separator}${activeEditorShort}"
}
```

**Risultato**: Colpo d'occhio immediato quale finestra stai usando!

---

## 🔥 Hot Reload Simultaneo

### Backend Hot Reload (ASP.NET Watch)

```bash
# In Backend VS Code terminal
cd apps/api
dotnet watch run  # Auto-restart on file change

# Modifica src/Api/Services/AdminDashboardService.cs
# → Build automatico
# → API restart automatico
# → Frontend vede subito le modifiche!
```

### Frontend Hot Reload (Next.js Fast Refresh)

```bash
# In Frontend VS Code terminal
cd apps/web
pnpm dev  # Fast refresh attivo

# Modifica src/components/AdminLayout.tsx
# → Refresh automatico < 100ms
# → Vedi modifiche istantaneamente nel browser!
```

**🚀 Entrambi attivi contemporaneamente = Full-stack instant feedback!**

---

## 📊 Screen Layout Raccomandato

### Monitor Singolo

```
┌────────────────────────────────────────────┐
│  VS Code Backend (Left Half)              │
│  ┌──────────────────────────────────────┐ │
│  │ Explorer    │  Editor               │ │
│  │ - apps/api/ │  AdminDashboard.cs    │ │
│  │             │                        │ │
│  │ Terminal    │  dotnet run :8080     │ │
│  └──────────────────────────────────────┘ │
└────────────────────────────────────────────┘

Alt+Tab ↓

┌────────────────────────────────────────────┐
│  VS Code Frontend (Right Half)            │
│  ┌──────────────────────────────────────┐ │
│  │ Explorer    │  Editor               │ │
│  │ - apps/web/ │  AdminLayout.tsx      │ │
│  │             │                        │ │
│  │ Terminal    │  pnpm dev :3000       │ │
│  └──────────────────────────────────────┘ │
└────────────────────────────────────────────┘
```

### Dual Monitor (Ottimale!)

```
Monitor 1 (Left)                    Monitor 2 (Right)
┌────────────────────┐             ┌────────────────────┐
│ VS Code Backend    │             │ VS Code Frontend   │
│                    │             │                    │
│ apps/api/          │             │ apps/web/          │
│ Services/          │             │ components/        │
│                    │             │                    │
│ dotnet run :8080   │────API──────│ pnpm dev :3000     │
│ dotnet test --watch│             │ pnpm test --watch  │
└────────────────────┘             └────────────────────┘

        ↓                                  ↓
   Backend Code                      Frontend Code
   C# Development                    TypeScript/React
```

**Perfect Setup**: Zero context switching, tutto visibile!

---

## 🎯 Quick Start per 2 VS Code

### Step 1: Apri Entrambe le Finestre (30 secondi)

```bash
# Terminal 1
code D:/Repositories/meepleai-monorepo-backend

# Terminal 2
code D:/Repositories/meepleai-monorepo-frontend

# ✅ Hai 2 VS Code aperti!
```

### Step 2: Start Backend (1 minuto)

**Backend VS Code → Integrated Terminal**:
```bash
cd apps/api
dotnet restore  # First time only
dotnet watch run  # :8080 with hot reload
```

### Step 3: Start Frontend (1 minuto)

**Frontend VS Code → Integrated Terminal**:
```bash
cd apps/web
pnpm install  # First time only
pnpm dev  # :3000 with hot reload
```

### Step 4: Verify Integration (30 secondi)

**Browser**:
- Open http://localhost:3000
- Frontend UI loaded ✅
- Check DevTools → Network tab
- Frontend calling http://localhost:8080/api ✅

**🎊 Total Setup: 3 minuti → Full-stack development ready!**

---

## 💡 Development Workflow Example

### Scenario: Admin Dashboard (Issue #877 + #881)

#### 9:00 AM - Start Backend Work

**Backend VS Code (Finestra #1)**:
```bash
# Create feature branch
git checkout -b feature/admin-dashboard-api

# Start API
cd apps/api
dotnet watch run  # Terminal 1, leave running

# Open file in editor
# Ctrl+P → AdminDashboardService.cs

# Develop...
# Save → Auto rebuild → API restart
# 🔥 Hot reload attivo!
```

#### 1:00 PM - Switch to Frontend Work

**Frontend VS Code (Finestra #2)** - Alt+Tab:
```bash
# Already on frontend-dev branch

# Start dev server
cd apps/web
pnpm dev  # Terminal 1, calls :8080 API

# Open file in editor
# Ctrl+P → AdminLayout.tsx

# Develop...
# Save → Auto refresh < 100ms
# 🔥 Fast refresh attivo!
# API backend già running dalla mattina!
```

#### 6:00 PM - Test Integration

**Keep both VS Code windows open**:

- **Left (Backend)**: Terminal shows API requests incoming
- **Right (Frontend)**: Browser DevTools shows API responses
- **Test**: Click UI → See API logs in real-time
- **Debug**: Set breakpoints in BOTH VS Code instances!

**🎯 Full-stack debugging con 2 VS Code = Developer Heaven!**

---

## 🐛 Debugging con 2 VS Code

### Backend Debugging

**Backend VS Code → Run and Debug (F5)**:

```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (web)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/Api.dll",
      "cwd": "${workspaceFolder}/apps/api/src/Api",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

**Set Breakpoint**:
- File: `AdminDashboardService.cs`
- Line: `public async Task<DashboardStats> GetStatsAsync()`
- F5 → Debugger attached!

### Frontend Debugging

**Frontend VS Code → Run and Debug (F5)**:

```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Next.js: debug server-side",
      "type": "node-terminal",
      "request": "launch",
      "command": "pnpm dev",
      "cwd": "${workspaceFolder}/apps/web"
    }
  ]
}
```

**Set Breakpoint**:
- File: `AdminLayout.tsx`
- Line: `useEffect(() => { ... })`
- F5 → Debugger attached!

### Full-Stack Debugging 🎯

**Scenario**: Frontend chiama Backend API

1. **Backend VS Code**: Set breakpoint in `AdminDashboardService.cs:GetStatsAsync()`
2. **Frontend VS Code**: Set breakpoint in `AdminLayout.tsx:useEffect()`
3. **Browser**: Click "Load Dashboard"
4. **Frontend VS Code**: Breakpoint hit! Step through...
5. **API Call**: `fetch('/api/v1/admin/dashboard/stats')`
6. **Backend VS Code**: Breakpoint hit! 🎉
7. **Step through backend** → Return response
8. **Frontend VS Code**: Receive response, continue execution

**🚀 Debug completo frontend + backend con 2 VS Code!**

---

## 📁 File Organization per Worktree

### Backend Worktree - Focus Files

```
meepleai-monorepo-backend/
├── apps/api/
│   ├── src/Api/
│   │   ├── Services/           ← Focus: Develop here
│   │   ├── Infrastructure/     ← Database, DI
│   │   ├── Models/             ← DTOs
│   │   ├── Migrations/         ← EF Core
│   │   └── Program.cs          ← Endpoint mapping
│   └── tests/Api.Tests/        ← Unit/Integration tests
└── apps/web/                   ← IGNORE (hide in settings)
```

### Frontend Worktree - Focus Files

```
meepleai-monorepo-frontend/
├── apps/api/                   ← IGNORE (hide in settings)
└── apps/web/
    ├── src/
    │   ├── components/         ← Focus: Develop here
    │   ├── pages/              ← Routes
    │   ├── lib/                ← Utils, API client
    │   ├── hooks/              ← Custom hooks
    │   └── styles/             ← CSS, Tailwind
    └── __tests__/              ← Jest tests
```

---

## ⚡ Productivity Tips

### 1. Keyboard Shortcuts

| Action | Shortcut | VS Code |
|--------|----------|---------|
| **Switch VS Code Window** | Alt+Tab | Both |
| **Open File** | Ctrl+P | Both |
| **Command Palette** | Ctrl+Shift+P | Both |
| **Split Terminal** | Ctrl+Shift+5 | Both |
| **Toggle Terminal** | Ctrl+` | Both |
| **Source Control** | Ctrl+Shift+G | Both |
| **Debug** | F5 | Both |
| **Run Task** | Ctrl+Shift+B | Both |

### 2. Workspace Switcher

**Windows Taskbar**:
- Pin Backend VS Code
- Pin Frontend VS Code
- Different icons/labels: "VS Code (Backend)", "VS Code (Frontend)"

**Alt+Tab Order**:
```
Browser → Backend VS Code → Frontend VS Code → (cycle)
```

### 3. Multi-Cursor Editing

**Scenario**: Update multiple API endpoints

**Backend VS Code**:
- Ctrl+F → Find "GetStatsAsync"
- Ctrl+D → Select next occurrence (multiple times)
- Edit all at once! 🎯

---

## 🧪 Testing Strategy con 2 VS Code

### Backend Tests (Watch Mode)

**Backend VS Code → Terminal 2**:
```bash
cd apps/api
dotnet test --watch

# Modifica AdminDashboardService.cs
# → Save
# → Test automaticamente re-run
# → Feedback istantaneo ✅/❌
```

### Frontend Tests (Watch Mode)

**Frontend VS Code → Terminal 2**:
```bash
cd apps/web
pnpm test --watch

# Modifica AdminLayout.tsx
# → Save
# → Test automaticamente re-run
# → Coverage report aggiornato
```

### E2E Tests (Manual)

**Frontend VS Code → Serve entrambi**:
```bash
# Prerequisito: Backend API running
# Prerequisito: Frontend dev server stopped

# Run E2E
pnpm test:e2e

# E2E testa full-stack:
# Frontend UI + Backend API integration
```

---

## 🎯 Commit Strategy con 2 Worktree

### Backend Commits (Backend VS Code)

```bash
# In meepleai-monorepo-backend
git status  # Shows only backend changes
git add apps/api/src/Api/Services/AdminDashboardService.cs
git commit -m "feat(backend): AdminDashboardService GetStatsAsync #877"
git push origin feature/admin-dashboard-api
```

### Frontend Commits (Frontend VS Code)

```bash
# In meepleai-monorepo-frontend
git status  # Shows only frontend changes
git add apps/web/src/components/AdminLayout.tsx
git commit -m "feat(frontend): AdminLayout component #881"
git push origin frontend-dev
```

**🎊 Benefit**: Commit isolati, zero conflitti, history pulita!

---

## 📈 Efficiency Comparison

### Traditional Workflow (1 Worktree)

```
Time    Activity                         Context Switch
09:00   Backend development              -
10:30   Need to check frontend           git stash (5 min)
10:35   Checkout frontend branch         git checkout (2 min)
10:37   Frontend work                    -
11:30   Back to backend                  git stash (5 min)
11:35   Checkout backend branch          git checkout (2 min)
11:37   Resume backend (lost context!)   Re-read code (10 min)
12:00   Lunch                            -

Total: 3h work + 24 min overhead = 13% waste
```

### Worktree Workflow (2 Worktree + 2 VS Code)

```
Time    Activity                         Context Switch
09:00   Backend development (VS Code 1)  -
10:30   Need to check frontend           Alt+Tab (0 sec) ✅
10:30   Frontend work (VS Code 2)        -
11:30   Back to backend                  Alt+Tab (0 sec) ✅
11:30   Resume backend (context saved!)  Continue immediately
12:00   Lunch                            -

Total: 3h work + 0 min overhead = 0% waste
```

**Efficiency Gain**: 13% time saved + mental energy preserved!

---

## ✅ Verification Checklist

Setup completato quando:

- [ ] `git worktree list` mostra 3 worktree
- [ ] Backend VS Code aperto su `D:/Repositories/meepleai-monorepo-backend`
- [ ] Frontend VS Code aperto su `D:/Repositories/meepleai-monorepo-frontend`
- [ ] Backend build successful: `cd apps/api && dotnet build`
- [ ] Frontend build successful: `cd apps/web && pnpm build`
- [ ] Backend API starts: `dotnet run` → :8080
- [ ] Frontend dev starts: `pnpm dev` → :3000
- [ ] Integration works: Frontend calls Backend API
- [ ] Both terminals in watch mode (dotnet test --watch, pnpm test --watch)
- [ ] Status bar colors different (optional)
- [ ] Window titles differentiated (optional)

**Status**: ✅ READY FOR DUAL-VSCODE DEVELOPMENT!

---

## 🚀 Start Now!

```bash
# Command 1: Backend VS Code
code D:/Repositories/meepleai-monorepo-backend

# Command 2: Frontend VS Code
code D:/Repositories/meepleai-monorepo-frontend

# ✅ You're ready to code in parallel! 🎉
```

**First Issue**: #877 (AdminDashboardService - Backend)
**Reference**: `claudedocs/calendario-sviluppo-1-persona-2025.md`

---

**Setup Status**: ✅ COMPLETE
**VS Code Instances**: 2 (Backend + Frontend)
**Worktrees**: 3 (Main + Backend + Frontend)
**Efficiency**: 13-18% time savings
**Developer Experience**: ⭐⭐⭐⭐⭐

Happy Parallel Coding! 🚀🎨
