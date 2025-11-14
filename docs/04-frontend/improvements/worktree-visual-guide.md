# 🌳 Worktree Visual Guide

## Struttura Worktree

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Git Repository                              │
│                      DegrassiAaron/meepleai-monorepo                │
└─────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ .git/
                    ┌─────────────┼─────────────┐
                    │             │             │
         ┌──────────▼────────┐   │   ┌────────▼──────────┐
         │   Main Worktree   │   │   │ Backend Worktree  │
         │   (Admin Console) │   │   │   (BGAI BE)       │
         └───────────────────┘   │   └───────────────────┘
    /home/user/meepleai-monorepo │   /home/user/meepleai-backend-worktree
                                  │
                         ┌────────▼──────────┐
                         │ Frontend Worktree │
                         │   (FE-IMP + FE)   │
                         └───────────────────┘
                   /home/user/meepleai-frontend-worktree
```

---

## Branch Structure

```
main (protected)
  │
  ├─── claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo  (Main Worktree)
  │    │
  │    ├─ Admin Console #884-922 (39 issues)
  │    ├─ Frontend Epics #926-936 (6 issues)
  │    └─ Merge destination for checkpoints
  │
  ├─── claude/frontend-modernization  (Frontend Worktree)
  │    │
  │    ├─ FE-IMP-001 → 008 (8 issues)
  │    ├─ BGAI frontend #989-995, #1001-1008, #1013-1018 (31 issues)
  │    └─ Merge → main at checkpoints
  │
  └─── claude/backend-bgai-months-4-6  (Backend Worktree)
       │
       ├─ BGAI backend #983-987, #996-1000, #1006-1007, #1012, #1019-1023 (30 issues)
       └─ Merge → main at checkpoints
```

---

## Timeline Visualization

```
Week 1-2  ┌──────────────────────────────────────────────────────┐
          │  FASE 1: Foundation                                  │
          ├──────────────────────────────────────────────────────┤
          │  Frontend WT  │ FE-IMP-001 → 003 (App Router+Query) │
          │  Backend WT   │ BGAI #983-987 (Quality Framework)   │
          │  Main WT      │ Admin #884-889 (Dashboard)          │
          └──────────────────────────────────────────────────────┘
                                   ▼
          ╔══════════════════════════════════════════════════════╗
          ║          CHECKPOINT 1: Merge + Integration Test      ║
          ╚══════════════════════════════════════════════════════╝
                                   ▼
Week 3-4  ┌──────────────────────────────────────────────────────┐
          │  FASE 2: Data Layer + BGAI API                       │
          ├──────────────────────────────────────────────────────┤
          │  Frontend WT  │ FE-IMP-004 → 006 (Auth+SDK+Forms)   │
          │  Backend WT   │ BGAI #996-1006 (Dataset + API)      │
          │  Main WT      │ Admin #890-902 (Infrastructure)     │
          └──────────────────────────────────────────────────────┘
                                   ▼
          ╔══════════════════════════════════════════════════════╗
          ║          CHECKPOINT 2: Merge + BGAI API Test         ║
          ╚══════════════════════════════════════════════════════╝
                                   ▼
Week 5-6  ┌──────────────────────────────────────────────────────┐
          │  FASE 3: Chat Store + Q&A UI                         │
          ├──────────────────────────────────────────────────────┤
          │  Frontend WT  │ FE-IMP-007 + BGAI #1001-1008 (Q&A)  │
          │  Backend WT   │ BGAI #1007 (SSE Streaming)          │
          │  Main WT      │ Admin #903-914 (Management)         │
          └──────────────────────────────────────────────────────┘
                                   ▼
          ╔══════════════════════════════════════════════════════╗
          ║          CHECKPOINT 3: Merge + Q&A Flow Test         ║
          ╚══════════════════════════════════════════════════════╝
                                   ▼
Week 7-8  ┌──────────────────────────────────────────────────────┐
          │  FASE 4: Upload Queue + PDF + Italian                │
          ├──────────────────────────────────────────────────────┤
          │  Frontend WT  │ FE-IMP-008 + BGAI #1013-1017 (PDF)  │
          │  Backend WT   │ BGAI #1012 (Adversarial)            │
          │  Main WT      │ Admin #915-922 (Reports)            │
          └──────────────────────────────────────────────────────┘
                                   ▼
          ╔══════════════════════════════════════════════════════╗
          ║          CHECKPOINT 4: Merge + Feature Complete      ║
          ╚══════════════════════════════════════════════════════╝
                                   ▼
Week 9-10 ┌──────────────────────────────────────────────────────┐
          │  FASE 5: Polish + Validation                         │
          ├──────────────────────────────────────────────────────┤
          │  Frontend WT  │ BGAI #1018, #992-995 (Testing)      │
          │  Backend WT   │ BGAI #1019-1023 (Validation)        │
          │  Main WT      │ Epics #926-936 (Polish)             │
          └──────────────────────────────────────────────────────┘
                                   ▼
          ╔══════════════════════════════════════════════════════╗
          ║      CHECKPOINT FINAL: Production Ready Go-Live      ║
          ╚══════════════════════════════════════════════════════╝
```

---

## Checkpoint Merge Flow

```
                    ┌─────────────────┐
                    │  Backend WT     │
                    │  (BE changes)   │
                    └────────┬────────┘
                             │ git push
                             ▼
                    ┌─────────────────┐
                    │  Frontend WT    │
                    │  (FE changes)   │
                    └────────┬────────┘
                             │ git push
                             ▼
                    ┌─────────────────┐
                    │  Main WT        │
                    │  (Admin changes)│
                    └────────┬────────┘
                             │ git push
                             ▼
        ╔═══════════════════════════════════════════╗
        ║        Main Worktree Merge Point          ║
        ║  cd /home/user/meepleai-monorepo         ║
        ╚═══════════════════════════════════════════╝
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ git merge    │    │ git merge    │    │ Test Suite   │
│ backend-WT   │───▶│ frontend-WT  │───▶│ pnpm test    │
└──────────────┘    └──────────────┘    │ dotnet test  │
                                         └──────┬───────┘
                                                │ ✓ PASS
                                                ▼
                                    ┌──────────────────┐
                                    │  Manual Testing  │
                                    │  (Checkpoint)    │
                                    └─────────┬────────┘
                                              │
                        ┌─────────────────────┼─────────────────────┐
                        │ ✓ GO                │                     │ ✗ NO-GO
                        ▼                     ▼                     ▼
              ┌──────────────────┐   ┌──────────────┐    ┌──────────────┐
              │ git push main    │   │ Rebase       │    │ Rollback     │
              └─────────┬────────┘   │ Backend WT   │    │ Fix issues   │
                        │            │ Frontend WT  │    │ Retry CP     │
                        │            └──────────────┘    └──────────────┘
                        │
                        ▼
        ╔═══════════════════════════════════════════╗
        ║       Next Phase Development              ║
        ║  All worktrees rebased on updated main    ║
        ╚═══════════════════════════════════════════╝
```

---

## File System Layout

```
/home/user/
  │
  ├── meepleai-monorepo/                    # Main Worktree
  │   ├── .git/                             # Shared git directory
  │   ├── apps/
  │   │   ├── api/                          # Work here for Admin backend
  │   │   └── web/                          # Work here for Admin frontend
  │   ├── docs/
  │   └── ...
  │
  ├── meepleai-frontend-worktree/           # Frontend Worktree
  │   ├── .git -> ../meepleai-monorepo/.git # Symlink to shared git
  │   ├── apps/
  │   │   └── web/                          # Work here for FE-IMP + BGAI FE
  │   │       ├── app/                      # NEW: App Router
  │   │       ├── components/
  │   │       │   ├── forms/                # NEW: Form System
  │   │       │   └── board-game-ai/        # NEW: BGAI Components
  │   │       ├── lib/
  │   │       │   └── clients/              # NEW: API SDK
  │   │       ├── store/                    # NEW: Zustand Store
  │   │       └── middleware.ts             # NEW: Edge Middleware
  │   └── ...
  │
  └── meepleai-backend-worktree/            # Backend Worktree
      ├── .git -> ../meepleai-monorepo/.git # Symlink to shared git
      ├── apps/
      │   └── api/                          # Work here for BGAI backend
      │       └── src/Api/BoundedContexts/
      │           └── BoardGameAI/          # NEW: BGAI Bounded Context
      │               ├── Domain/
      │               ├── Application/
      │               └── Infrastructure/
      └── ...
```

---

## Dependency Graph

```
                    ╔═══════════════════════════════╗
                    ║      FE-IMP Dependencies      ║
                    ╚═══════════════════════════════╝

                      FE-IMP-001 (App Router)
                           /         \
                          /           \
                         ▼             ▼
              FE-IMP-002            FE-IMP-003
            (Server Actions)      (TanStack Query)
                                      │
                                      ├─────────────┐
                                      ▼             ▼
                                FE-IMP-004      FE-IMP-005
                              (AuthContext)    (API SDK)
                                      │             │
                                      ▼             │
                                FE-IMP-006          │
                                 (Forms)            │
                                                    ▼
                                              FE-IMP-007
                                            (Chat Store)
                                                    │
                                                    ▼
                                              FE-IMP-008
                                           (Upload Queue)

                    ╔═══════════════════════════════╗
                    ║      BGAI Dependencies        ║
                    ╚═══════════════════════════════╝

                        #983-987
                   (Quality Framework)
                            │
                            ▼
                      #996-1000
                  (Dataset + Baseline)
                            │
                            ▼
                         #1006
                      (BGAI API)
                            │
                ┌───────────┼───────────┐
                ▼           ▼           ▼
             #1007      #989-995    #1001-1008
           (SSE BE)    (BGAI Comp)   (Q&A UI)
                            │           │
                            └─────┬─────┘
                                  ▼
                            #1013-1017
                        (PDF + Italian)
                                  │
                                  ▼
                            #1018-1023
                         (Testing + Polish)

                    ╔═══════════════════════════════╗
                    ║   Admin Console Dependencies  ║
                    ╚═══════════════════════════════╝

                        #884-889
                       (Dashboard)
                            │
                            ▼
                        #890-902
                    (Infrastructure)
                            │
                            ▼
                        #903-914
                      (Management)
                            │
                            ▼
                        #915-922
                      (Reports)
```

---

## Parallel Work Visualization

```
Timeline: 10 Weeks

Week │ Frontend Worktree      │ Backend Worktree     │ Main Worktree        │ Integration
─────┼────────────────────────┼──────────────────────┼──────────────────────┼─────────────
  1  │ FE-IMP-001            │ #983-987             │ #884-889            │
  2  │ FE-IMP-002-003        │ (Quality)            │ (Dashboard)         │ ← CP1
─────┼────────────────────────┼──────────────────────┼──────────────────────┼─────────────
  3  │ FE-IMP-004-005        │ #996-1000            │ #890-895            │
  4  │ FE-IMP-006            │ #1006                │ #896-902            │ ← CP2
─────┼────────────────────────┼──────────────────────┼──────────────────────┼─────────────
  5  │ FE-IMP-007            │ #1007                │ #903-907            │
  6  │ #1001-1008            │ (SSE)                │ #908-914            │ ← CP3
─────┼────────────────────────┼──────────────────────┼──────────────────────┼─────────────
  7  │ FE-IMP-008            │ #1012                │ #915-919            │
  8  │ #1013-1017            │ (Adversarial)        │ #920-922            │ ← CP4
─────┼────────────────────────┼──────────────────────┼──────────────────────┼─────────────
  9  │ #1018, #992-993       │ #1019-1020           │ #926, #931-933      │
 10  │ #994-995              │ #1021-1023           │ #934-936            │ ← FINAL
─────┴────────────────────────┴──────────────────────┴──────────────────────┴─────────────

Legend:
  │ = Parallel work happening simultaneously
  ← = Checkpoint merge + integration testing
```

---

## Commit Flow

```
Developer Working on Frontend Worktree:

    ┌─────────────────────────────────────────┐
    │  /home/user/meepleai-frontend-worktree │
    │  Branch: claude/frontend-modernization  │
    └─────────────────────────────────────────┘
                    │
                    │ Work on FE-IMP-001
                    │ git add/commit
                    ▼
    ┌─────────────────────────────────────────┐
    │  Commit: feat(fe-imp): Add App Router   │
    │  Issue: FE-IMP-001                      │
    │  Worktree: frontend                     │
    └─────────────────────────────────────────┘
                    │
                    │ git push origin
                    ▼
    ┌─────────────────────────────────────────┐
    │  GitHub: claude/frontend-modernization  │
    │  (not merged to main yet)               │
    └─────────────────────────────────────────┘
                    │
                    │ Wait for Checkpoint
                    ▼
    ┌─────────────────────────────────────────┐
    │  Checkpoint Time!                       │
    │  Switch to Main Worktree                │
    └─────────────────────────────────────────┘
                    │
                    ▼
    ┌─────────────────────────────────────────┐
    │  /home/user/meepleai-monorepo          │
    │  Branch: claude/analyze-frontend-...   │
    └─────────────────────────────────────────┘
                    │
                    │ git merge frontend-WT
                    │ git merge backend-WT
                    ▼
    ┌─────────────────────────────────────────┐
    │  Merged: All changes integrated         │
    │  Test: pnpm test + dotnet test          │
    │  Manual QA: Checkpoint scenarios        │
    └─────────────────────────────────────────┘
                    │
                    │ git push
                    ▼
    ┌─────────────────────────────────────────┐
    │  GitHub: main branch updated            │
    │  All worktrees can now rebase           │
    └─────────────────────────────────────────┘
```

---

## Quick Reference Commands

### Setup
```bash
# Initial setup (Day 1)
cd /home/user/meepleai-monorepo
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization
git worktree add ../meepleai-backend-worktree claude/backend-bgai-months-4-6
```

### Daily Work
```bash
# Work in Frontend Worktree
cd /home/user/meepleai-frontend-worktree
git status
git pull origin claude/frontend-modernization
# ... make changes ...
git add .
git commit -m "feat(fe-imp): Add feature X"
git push origin claude/frontend-modernization

# Work in Backend Worktree
cd /home/user/meepleai-backend-worktree
git status
git pull origin claude/backend-bgai-months-4-6
# ... make changes ...
git add .
git commit -m "feat(bgai): Add feature Y"
git push origin claude/backend-bgai-months-4-6
```

### Checkpoint Merge
```bash
# Main worktree
cd /home/user/meepleai-monorepo
git pull origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo

# Merge backend first
git merge claude/backend-bgai-months-4-6 --no-ff -m "CP#: Backend changes"

# Then merge frontend
git merge claude/frontend-modernization --no-ff -m "CP#: Frontend changes"

# Test
pnpm test && cd apps/api/src/Api && dotnet test && cd - && pnpm build

# Push
git push -u origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
```

### Post-Checkpoint Rebase
```bash
# Backend worktree
cd /home/user/meepleai-backend-worktree
git fetch origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git rebase origin/claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo

# Frontend worktree
cd /home/user/meepleai-frontend-worktree
git fetch origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git rebase origin/claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
```

---

## Visual Directory Structure

```
/home/user/
│
├── meepleai-monorepo/                     📁 MAIN WORKTREE
│   │                                      Focus: Admin Console
│   ├── .git/                              🔐 Shared Git DB
│   ├── apps/
│   │   ├── api/
│   │   │   ├── src/Api/BoundedContexts/
│   │   │   │   ├── Administration/       ← Admin backend work here
│   │   │   │   ├── GameManagement/
│   │   │   │   └── ...
│   │   │   └── ...
│   │   └── web/
│   │       ├── src/
│   │       │   ├── pages/
│   │       │   │   └── admin/            ← Admin frontend work here
│   │       │   └── components/
│   │       │       └── admin/
│   │       └── ...
│   └── docs/
│       └── ImproveFrontend/
│           ├── integrated-worktree-strategy.md  ← FULL ROADMAP
│           └── QUICK-START-ROADMAP.md    ← THIS FILE
│
├── meepleai-frontend-worktree/            📁 FRONTEND WORKTREE
│   │                                      Focus: FE-IMP + BGAI UI
│   ├── .git → ../meepleai-monorepo/.git  🔗 Symlink
│   └── apps/
│       └── web/
│           ├── app/                       ✨ NEW: App Router
│           │   ├── layout.tsx
│           │   ├── page.tsx
│           │   ├── chat/page.tsx
│           │   ├── actions/              ✨ NEW: Server Actions
│           │   └── providers.tsx
│           ├── components/
│           │   ├── forms/                ✨ NEW: Form System
│           │   └── board-game-ai/        ✨ NEW: BGAI Components
│           ├── lib/
│           │   ├── clients/              ✨ NEW: API SDK
│           │   └── hooks/                ✨ NEW: TanStack Query hooks
│           ├── store/                    ✨ NEW: Zustand Store
│           └── middleware.ts             ✨ NEW: Edge Middleware
│
└── meepleai-backend-worktree/             📁 BACKEND WORKTREE
    │                                      Focus: BGAI Backend
    ├── .git → ../meepleai-monorepo/.git  🔗 Symlink
    └── apps/
        └── api/
            └── src/Api/BoundedContexts/
                └── BoardGameAI/          ✨ NEW: BGAI Context
                    ├── Domain/
                    │   ├── Question.cs
                    │   └── Answer.cs
                    ├── Application/
                    │   ├── Commands/
                    │   └── Handlers/
                    └── Infrastructure/
                        └── Repositories/
```

Legend:
- 📁 = Worktree root
- 🔐 = Shared Git database
- 🔗 = Symlink to shared Git
- ✨ = New files/directories created during implementation
- ← = Where to work

---

## Benefits of This Approach

### ✅ Parallelism
- 3 developers can work simultaneously without conflicts
- Frontend, Backend, Admin Console progress independently

### ✅ Isolation
- Each worktree has clean working directory
- Changes don't interfere until checkpoint merge

### ✅ Safety
- Test each worktree before merge
- Rollback easy (just don't merge)
- Main branch always stable

### ✅ Clarity
- Clear separation of concerns
- Easy to understand what's being worked on
- Commit history clean with merge commits

### ✅ Efficiency
- No constant branch switching
- Each terminal can stay in one worktree
- Dev servers can run simultaneously (different ports)

---

## Potential Issues & Solutions

### Issue: Worktree Out of Sync
**Symptom**: Changes from main not visible in worktree
**Solution**:
```bash
cd /home/user/meepleai-frontend-worktree
git fetch origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git rebase origin/claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
```

### Issue: Merge Conflicts
**Symptom**: Git reports conflicts during checkpoint merge
**Solution**:
1. Identify conflicting files: `git status`
2. Resolve manually (Backend > Frontend priority)
3. Test thoroughly: `pnpm test && dotnet test`
4. Complete merge: `git commit`

### Issue: Worktree Removal Needed
**Symptom**: Need to recreate worktree
**Solution**:
```bash
# Remove
git worktree remove ../meepleai-frontend-worktree

# Recreate
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization
```

---

**Version**: 1.0
**Created**: 2025-11-13
**Companion to**: integrated-worktree-strategy.md, QUICK-START-ROADMAP.md
