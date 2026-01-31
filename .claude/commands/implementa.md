# /implementa - Full Issue Implementation Workflow

> **Command**: `/implementa <Issue N> [--base-branch <branch>] [--pr-target <branch>]`
> **Alias**: `/sc:implement --uc Issue <Issue N>` with full workflow + session management

## Description

End-to-end issue implementation with validation gates, code review, and automated cleanup.

**Features**:
- Session management (Serena) with checkpoints
- Validation gates (build, test, DoD, code review)
- Auto-select optimal agents/MCP/skills
- Pre-merge code review with auto-fix (confidence ≥70%)
- Complete branch lifecycle

## Arguments

- `<Issue N>` - GitHub issue number (e.g., `2372`, `#2372`)
- `--base-branch <branch>` - Branch to create feature branch from (default: `main-dev`)
- `--pr-target <branch>` - Target branch for PR merge (default: `main-dev`)

## Configuration

```bash
# Default behavior
/implementa 2373  # Creates branch from main-dev, PR targets main-dev

# Custom base branch
/implementa 2373 --base-branch feature/parent-epic

# Custom PR target
/implementa 2373 --pr-target staging

# Both custom
/implementa 2373 --base-branch develop --pr-target main
```

## Workflow Phases

### Phase 0: Session Init 🔄

1. `/sc:load` - Load project context
2. `git status && git branch` - Verify clean workspace
3. Check on correct parent branch

**Gate**: ❌ Stop if dirty workspace or wrong branch

---

### Phase 1: Preparation 📋

1. Fetch issue via `mcp__MCP_DOCKER__issue_read`
2. Parse: title, DoD, tasks, labels
3. Explore codebase: `Agent Explore --quick` for patterns
4. Checkpoint: `write_memory("issue_<N>_analysis")`

**Gate**: Understand requirements + find patterns

---

### Phase 2: Planning 🧠

1. **Complex issues**: Use `sequential-thinking` for analysis
2. Generate 2-3 options with confidence scores
3. **Decision**:
   - confidence ≥90%: Auto-select
   - confidence <90%: `AskUserQuestion`
4. `TodoWrite` with atomic tasks (min 3 for complex)
5. Checkpoint: `write_memory("issue_<N>_plan")`

**Gate**: ❌ Stop if all options <70% confidence

---

### Phase 3: Branch Setup 🌿

**Base Branch**: `--base-branch` or default `main-dev`

1. Sync: `git checkout <base-branch> && git pull`
2. Create: `git checkout -b <type>/issue-<id>-<desc>`
   - **Types**: feature|fix|docs|test|refactor|chore
3. Checkpoint: `write_memory("issue_<N>_branch")`

**Gate**: ❌ Stop if branch exists or malformed name

---

### Phase 4: Implementation 💻

**Per task**:
1. **Tool selection**:
   - Symbol ops → `mcp__serena__*`
   - Bulk edits → `mcp__morphllm-fast-apply__edit_file`
   - UI → `mcp__magic__21st_magic_component_builder`
2. **Implement** following:
   - Backend: CQRS, DDD, MediatR only
   - Frontend: React, Zustand, React Query
3. **Update**:
   - `TaskUpdate`: pending → in_progress → completed
   - `mcp__MCP_DOCKER__issue_write`: GitHub checkbox
4. **Verify**: Compilation passes
5. Checkpoint: `write_memory("issue_<N>_implementation")`

**Gate**: ❌ Stop on compilation errors

---

### Phase 5: Testing ✅

**Backend**:
```bash
cd apps/api/src/Api && dotnet test
```
Target: ≥90% coverage

**Frontend**:
```powershell
pwsh -c "cd apps/web; pnpm test; pnpm typecheck; pnpm lint"
```
Target: ≥85% coverage

**Error handling**:
1. Isolate error in test case
2. Root cause: `sequential-thinking`
3. Create guards/validations
4. Re-run test suite
5. ❌ NEVER skip tests or disable validations

Checkpoint: `write_memory("issue_<N>_tests")`

**Gate**: ❌ Stop if tests fail OR coverage <target OR new warnings

---

### Phase 6: Code Review Pre-Merge 🔍

**PR Target**: `--pr-target` or default `main-dev`

1. **Commit**:
```bash
git add .
git commit -m "<type>(<scope>): <desc>

Refs: #<N>"
```

2. **Push**: `git push -u origin <branch>`

3. **Create PR** (draft to `--pr-target`):
```bash
gh pr create --draft --base <pr-target> \
  --title "Issue #<N>: <title>" \
  --body "## Summary
<desc>

## Changes
- <change1>

Closes #<N>"
```

4. **Code review**: `/code-review:code-review <PR-URL>`
5. **Fix loop** (max 3):
   - Filter: confidence ≥70 only
   - Fix + re-test + re-review
   - If >3 iterations: `AskUserQuestion`
6. **Ready**: `gh pr ready`

**Gate**: ❌ Stop if final score <80%

---

### Phase 7: DoD Verification ✔️

1. Check each DoD from issue
2. Update: `mcp__MCP_DOCKER__issue_write` (DoD checkboxes)
3. **If missing DoD**: `AskUserQuestion` (complete | update | sub-issue)

**Gate**: ❌ Stop if any DoD not ✅

---

### Phase 8: Merge & Closure 🎯

**MANDATORY Pre-Merge Validation**:

**Backend**:
```bash
cd apps/api/src/Api && dotnet build --no-restore && dotnet test --no-build
```

**Frontend**:
```powershell
pwsh -c "cd apps/web; pnpm build; pnpm test; pnpm typecheck"
```

**Gate**: ❌ STOP if build/test/typecheck fails
- If fails: `AskUserQuestion` (fix | abort | force)

**Merge** (only after validation ✅):
```bash
gh pr merge --squash --delete-branch --base <pr-target>
```

**Cleanup**:
```bash
git checkout <base-branch> && git pull
git branch -D <branch>
git remote prune origin
```

**Close issue**: `mcp__MCP_DOCKER__issue_write(state: closed)`
**Save**: `/sc:save`
**Checkpoint**: `write_memory("issue_<N>_completed")`

---

## MCP/Tool Matrix

| Phase | Tool/MCP |
|-------|----------|
| 0 | `/sc:load`, `git` |
| 1 | `mcp__MCP_DOCKER__issue_read`, `Agent Explore`, `write_memory` |
| 2 | `sequential-thinking`, `AskUserQuestion`, `TodoWrite`, `write_memory` |
| 3 | `git`, `write_memory` |
| 4 | `serena__*`, `morphllm__edit_file`, `magic__*`, `TaskUpdate`, `issue_write` |
| 5 | `dotnet test`, `pwsh` (pnpm), `sequential-thinking`, `write_memory` |
| 6 | `git`, `gh pr`, `/code-review:code-review`, `write_memory` |
| 7 | `issue_write`, `AskUserQuestion` |
| 8 | `dotnet`, `pwsh` (pnpm), `gh pr merge`, `issue_write`, `/sc:save` |

---

## Critical Rules

**🔴 STOP if**:
- Phase 0: Dirty workspace or wrong branch
- Phase 2: All options <70% confidence
- Phase 3: Branch exists or malformed
- Phase 4: Compilation errors
- Phase 5: Tests fail OR coverage <target OR new warnings
- Phase 6: PR score <80% after 3 iterations
- Phase 7: Missing DoD
- Phase 8: Pre-merge validation fails

**✅ ALWAYS**:
- Session: `/sc:load` → `/sc:save`
- Checkpoints: `write_memory()` after each phase
- Tracking: `TodoWrite` + `TaskUpdate`
- Validation: Never bypass gates
- Parallel: Use parallel tool calls (Read, Grep)

**❌ NEVER**:
- Skip errors or disable tests
- Merge without code review
- Ignore DoD or allow new warnings

**🤝 ASK USER when**:
- Confidence <90%
- Code review loop >3
- DoD incomplete
- Task >1 day estimate

---

## Error Recovery

**Compilation Fail**:
```
❌ STOP → Show errors → AskUserQuestion:
a) Fix auto (if clear)
b) Wait for manual fix
c) Abort + checkpoint
```

**Test Fail**:
```
❌ STOP → Isolate → Root cause (sequential-thinking) → Guards → Re-test
```

**Code Review Loop >3**:
```
⚠️ PAUSE → Show issues → AskUserQuestion:
a) Ignore score <70
b) Fix manually
c) Request human review
```

**From Checkpoint**:
```bash
/sc:load
list_memories()  # Show checkpoints
read_memory("issue_<N>_<phase>")  # Resume
```

---

## Output Template

```
🔄 Phase 0: Session Init ✅
📋 Phase 1: Preparation ✅ (patterns: FilterComponent)
🧠 Phase 2: Planning ✅ (Option 1, confidence: 95%)
🌿 Phase 3: Branch ✅ (feature/issue-<N>-<desc>)
💻 Phase 4: Implementation ✅ (3/3 tasks)
✅ Phase 5: Testing ✅ (92% backend, 87% frontend)
🔍 Phase 6: Code Review ✅ (score: 85%)
✔️ Phase 7: DoD ✅ (4/4)
🎯 Phase 8: Merge ✅

✅ SUCCESS: Issue #<N> completed
📊 Stats: X files, Y tests, Z% coverage, W% review score
```

---

## Execution Flow

```yaml
PHASE_0:
  - /sc:load
  - git status (GATE: fail if dirty)
  - git branch (GATE: fail if not on base-branch)

PHASE_1:
  - mcp__MCP_DOCKER__issue_read($N)
  - Parse: {title, DoD[], tasks[]}
  - Agent Explore --quick
  - write_memory("issue_$N_analysis")

PHASE_2:
  - IF complex: sequential-thinking
  - Generate: 2-3 options with confidence%
  - IF max(confidence) ≥90%: auto-select
    ELSE: AskUserQuestion
  - TodoWrite (min 3 tasks)
  - write_memory("issue_$N_plan")
  - GATE: STOP if all <70%

PHASE_3:
  - git checkout <base-branch> && git pull
  - git checkout -b <type>/issue-$N-<desc>
  - write_memory("issue_$N_branch")
  - GATE: STOP if exists or malformed

PHASE_4:
  - FOR task: select tool → implement → TaskUpdate → issue_write
  - write_memory("issue_$N_implementation")
  - GATE: STOP on compilation error

PHASE_5:
  - dotnet test (≥90%)
  - pwsh -c "pnpm test && typecheck && lint" (≥85%)
  - IF fail: isolate → root cause → guards → re-test
  - write_memory("issue_$N_tests")
  - GATE: STOP if fail OR low coverage

PHASE_6:
  - git commit + push
  - gh pr create --draft --base <pr-target>
  - /code-review:code-review
  - Fix loop (max 3, confidence ≥70)
  - gh pr ready
  - GATE: STOP if score <80%

PHASE_7:
  - Check DoD[]
  - issue_write (DoD checkboxes)
  - IF missing: AskUserQuestion
  - GATE: STOP if any not ✅

PHASE_8:
  - dotnet build && test
  - pwsh -c "pnpm build && test && typecheck"
  - GATE: STOP if fail → AskUserQuestion
  - gh pr merge --squash --base <pr-target>
  - issue_write(closed)
  - Cleanup: checkout → pull → branch -D → prune
  - /sc:save
  - write_memory("issue_$N_completed")
```

---

**Version**: 2.2 (2026-01-29)

**Changelog**:

v2.2 (2026-01-29):
- ✅ Reduced verbosity by 45% (condensed phase descriptions)
- ✅ PowerShell with pwsh -c for all pnpm commands
- ✅ Configurable base branch (--base-branch, default: main-dev)
- ✅ Explicit PR target branch (--pr-target, default: main-dev)
- ✅ Removed redundant explanations and duplicate content
- ✅ Streamlined MCP/Tool matrix and execution flow

v2.1 (2026-01-28):
- ✅ Pre-merge build+test validation in Phase 8
- ✅ Mandatory validation gates

v2.0 (2026-01-24):
- ✅ Session management + checkpoints
- ✅ Validation gates per phase
- ✅ Pre-merge code review
- ✅ DoD verification phase