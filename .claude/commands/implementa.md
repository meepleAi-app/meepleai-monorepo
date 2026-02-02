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
- **Progress tracking** with checkbox updates

## Arguments

- `<Issue N>` - GitHub issue number (e.g., `2372`, `#2372`)
- `--base-branch <branch>` - Branch to create feature branch from (default: current branch)
- `--pr-target <branch>` - Target branch for PR merge (default: parent branch auto-detected)

## Windows PowerShell Best Practices

**CRITICO**: Usa sempre `pwsh -c "comando"` su Windows per evitare problemi di output vuoto.

```bash
# ✅ CORRETTO
pwsh -c "git diff --name-only main-dev...HEAD"
pwsh -c "docker logs meepleai-api --tail=50"
pwsh -c "cd apps/web; pnpm test; pnpm typecheck"

# ❌ EVITA (output vuoto su Windows)
git diff --name-only main-dev...HEAD | grep pattern
docker logs meepleai-api | findstr error
cd apps/web && pnpm test && pnpm lint
```

**Regola**: Se un comando non produce output atteso, wrappa sempre in `pwsh -c`.

## Configuration

```bash
# Default: usa branch corrente come base, PR verso parent auto-detected
/implementa 2373

# Custom base branch (crea feature branch da questo)
/implementa 2373 --base-branch feature/parent-epic

# Custom PR target (PR mergia qui)
/implementa 2373 --pr-target staging

# Entrambi custom
/implementa 2373 --base-branch develop --pr-target main
```

## Branch Detection Logic

**Base Branch** (--base-branch o default):

```bash
# Se non specificato, usa branch corrente
current_branch=$(pwsh -c "git branch --show-current")
base_branch=${base_branch:-$current_branch}
```

**PR Target** (--pr-target o default):

```bash
# Se non specificato, auto-detect parent branch
if [ -z "$pr_target" ]; then
  pr_target=$(pwsh -c "git show-branch | grep '*' | grep -v \"$current_branch\" | head -1 | sed 's/.*\[\(.*\)\].*/\1/' | sed 's/\^.*//'")
fi
# Fallback: main-dev se non trovato
pr_target=${pr_target:-main-dev}
```

## Workflow Phases

### Phase 0: Session Init 🔄

1. `/sc:load` - Load project context
2. `git status && git branch` - Verify clean workspace
3. Detect base branch and PR target
4. **Update GitHub Issue**: Mark checkbox `[ ] Started` → `[x] Started`

```bash
# Update issue checkbox on GitHub
gh issue edit $issue_number --add-label "status:in-progress"
gh api repos/{owner}/{repo}/issues/$issue_number \
  --field body="$(gh issue view $issue_number --json body -q .body | sed 's/\[ \] Started/[x] Started/')"
```

**Gate**: ❌ Stop if dirty workspace

---

### Phase 1: Preparation 📋

1. Fetch issue: `mcp__MCP_DOCKER__issue_read`
2. Parse: title, DoD, tasks, labels
3. Explore codebase: `Agent Explore --quick`
4. Checkpoint: `write_memory("issue_<N>_analysis")`
5. **Update checkbox**: `[ ] Analysis` → `[x] Analysis`

**Gate**: Understand requirements + find patterns

---

### Phase 2: Planning 🧠

1. **Complex issues**: Use `sequential-thinking`
2. Generate 2-3 options with confidence scores
3. **Decision**:
   - confidence ≥90%: Auto-select
   - confidence <90%: `AskUserQuestion`
4. `TodoWrite` with atomic tasks (min 3)
5. Checkpoint: `write_memory("issue_<N>_plan")`
6. **Update checkbox**: `[ ] Planning` → `[x] Planning`

**Gate**: ❌ Stop if all options <70% confidence

---

### Phase 3: Branch Setup 🌿

**Base Branch**: `--base-branch` or current branch

1. Sync: `git checkout <base-branch> && git pull`
2. Create: `git checkout -b <type>/issue-<id>-<desc>`
   - **Types**: feature|fix|docs|test|refactor|chore
3. Checkpoint: `write_memory("issue_<N>_branch")`
4. **Update checkbox**: `[ ] Branch Created` → `[x] Branch Created`

**Gate**: ❌ Stop if branch exists or malformed

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
   - **GitHub checkbox**: Update per ogni task completato
4. Verify: Compilation passes
5. Checkpoint: `write_memory("issue_<N>_implementation")`
6. **Update checkbox**: `[ ] Implementation` → `[x] Implementation`

**Gate**: ❌ Stop on compilation errors

---

### Phase 5: Testing ✅

**Backend**:

```bash
pwsh -c "cd apps/api/src/Api; dotnet test /p:CollectCoverage=true"
```

Target: ≥90% coverage

**Frontend**:
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
```bash
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
**Update checkbox**: `[ ] Testing` → `[x] Testing`

**Gate**: ❌ Stop if tests fail OR coverage <target OR new warnings

---

### Phase 6: Code Review Pre-Merge 🔍

**PR Target**: `--pr-target` or auto-detected parent branch

1. **Commit**:

```bash
git add .
git commit -m "<type>(<scope>): <desc>

Refs: #<N>"
```

2. **Push**: `git push -u origin <branch>`

3. **Create PR** (draft to PR target):
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
```bash
# CRITICO: usa --base con PR target auto-detected o specificato
current_branch=$(pwsh -c "git branch --show-current")

gh pr create --draft --base "$pr_target" \
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
7. **Update checkbox**: `[ ] PR Created` → `[x] PR Created`
8. **Update checkbox**: `[ ] Code Review` → `[x] Code Review`

**Gate**: ❌ Stop if final score <80%

---

### Phase 7: DoD Verification ✔️

1. Check each DoD from issue
2. Update: `mcp__MCP_DOCKER__issue_write` (DoD checkboxes)
3. **If missing DoD**: `AskUserQuestion` (complete | update | sub-issue)
4. **Update checkbox**: `[ ] DoD Verified` → `[x] DoD Verified`

**Gate**: ❌ Stop if any DoD not ✅

---

### Phase 8: Merge & Closure 🎯

**MANDATORY Pre-Merge Validation**:

**Backend**:

```bash
pwsh -c "cd apps/api/src/Api; dotnet build --no-restore; dotnet test --no-build"
```

**Frontend**:
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
```bash
pwsh -c "cd apps/web; pnpm build; pnpm test; pnpm typecheck"
```

**Gate**: ❌ STOP if build/test/typecheck fails

- If fails: `AskUserQuestion` (fix | abort | force)

**Merge** (only after validation ✅):

```bash
# Merge verso PR target specificato o auto-detected
gh pr merge --squash --delete-branch --base "$pr_target"
```

**Cleanup**:

```bash
git checkout "$base_branch" && git pull
git branch -D "$feature_branch"
pwsh -c "git remote prune origin"
```

**Close issue**: `mcp__MCP_DOCKER__issue_write(state: closed)`
**Update checkbox**: `[ ] Merged` → `[x] Merged`
**Update checkbox**: `[ ] Cleanup` → `[x] Cleanup`
**Save**: `/sc:save`
**Checkpoint**: `write_memory("issue_<N>_completed")`

---

## Progress Tracking Checkbox System

**GitHub Issue Template Structure:**
<<<<<<< HEAD
```markdown
## Progress
=======

```markdown
## Progress

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
- [ ] Started
- [ ] Analysis
- [ ] Planning
- [ ] Branch Created
- [ ] Implementation
- [ ] Testing
- [ ] PR Created
- [ ] Code Review
- [ ] DoD Verified
- [ ] Merged
- [ ] Cleanup

## Definition of Done
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
- [ ] DoD Item 1
- [ ] DoD Item 2
```

**Automatic Updates During Workflow:**

<<<<<<< HEAD
| Phase | Checkbox Updated | Command |
|-------|-----------------|---------|
| 0 | `[x] Started` | `gh api` update issue body |
| 1 | `[x] Analysis` | `gh api` update issue body |
| 2 | `[x] Planning` | `gh api` update issue body |
| 3 | `[x] Branch Created` | `gh api` update issue body |
| 4 | `[x] Implementation` | `gh api` update issue body |
| 5 | `[x] Testing` | `gh api` update issue body |
| 6 | `[x] PR Created` | `gh api` update issue body |
| 6 | `[x] Code Review` | `gh api` update issue body |
| 7 | `[x] DoD Verified` | `gh api` update issue body |
| 8 | `[x] Merged` | `gh api` update issue body |
| 8 | `[x] Cleanup` | `gh api` update issue body |

**Update Helper Function:**
=======
| Phase | Checkbox Updated     | Command                    |
| ----- | -------------------- | -------------------------- |
| 0     | `[x] Started`        | `gh api` update issue body |
| 1     | `[x] Analysis`       | `gh api` update issue body |
| 2     | `[x] Planning`       | `gh api` update issue body |
| 3     | `[x] Branch Created` | `gh api` update issue body |
| 4     | `[x] Implementation` | `gh api` update issue body |
| 5     | `[x] Testing`        | `gh api` update issue body |
| 6     | `[x] PR Created`     | `gh api` update issue body |
| 6     | `[x] Code Review`    | `gh api` update issue body |
| 7     | `[x] DoD Verified`   | `gh api` update issue body |
| 8     | `[x] Merged`         | `gh api` update issue body |
| 8     | `[x] Cleanup`        | `gh api` update issue body |

**Update Helper Function:**

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
```bash
update_checkbox() {
  local issue_number=$1
  local checkbox_label=$2

  current_body=$(gh issue view $issue_number --json body -q .body)
  updated_body=$(echo "$current_body" | sed "s/\[ \] $checkbox_label/[x] $checkbox_label/")

  gh api repos/{owner}/{repo}/issues/$issue_number \
    --method PATCH \
    --field body="$updated_body"
}

# Usage
update_checkbox 2373 "Analysis"
update_checkbox 2373 "Implementation"
```

---

## MCP/Tool Matrix

<<<<<<< HEAD
| Phase | Tool/MCP |
|-------|----------|
| 0 | `/sc:load`, `git`, `gh api` (checkbox update) |
| 1 | `mcp__MCP_DOCKER__issue_read`, `Agent Explore`, `write_memory`, `gh api` |
| 2 | `sequential-thinking`, `AskUserQuestion`, `TodoWrite`, `write_memory`, `gh api` |
| 3 | `git`, `write_memory`, `gh api` |
| 4 | `serena__*`, `morphllm__edit_file`, `magic__*`, `TaskUpdate`, `gh api` |
| 5 | `pwsh` (dotnet test, pnpm), `sequential-thinking`, `write_memory`, `gh api` |
| 6 | `git`, `gh pr`, `/code-review:code-review`, `write_memory`, `gh api` |
| 7 | `issue_write`, `AskUserQuestion`, `gh api` |
| 8 | `pwsh` (dotnet, pnpm), `gh pr merge`, `issue_write`, `/sc:save`, `gh api` |
=======
| Phase | Tool/MCP                                                                        |
| ----- | ------------------------------------------------------------------------------- |
| 0     | `/sc:load`, `git`, `gh api` (checkbox update)                                   |
| 1     | `mcp__MCP_DOCKER__issue_read`, `Agent Explore`, `write_memory`, `gh api`        |
| 2     | `sequential-thinking`, `AskUserQuestion`, `TodoWrite`, `write_memory`, `gh api` |
| 3     | `git`, `write_memory`, `gh api`                                                 |
| 4     | `serena__*`, `morphllm__edit_file`, `magic__*`, `TaskUpdate`, `gh api`          |
| 5     | `pwsh` (dotnet test, pnpm), `sequential-thinking`, `write_memory`, `gh api`     |
| 6     | `git`, `gh pr`, `/code-review:code-review`, `write_memory`, `gh api`            |
| 7     | `issue_write`, `AskUserQuestion`, `gh api`                                      |
| 8     | `pwsh` (dotnet, pnpm), `gh pr merge`, `issue_write`, `/sc:save`, `gh api`       |
>>>>>>> a317f4777 (docs(claude): update implementa command documentation)

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
- Tracking: `TodoWrite` + `TaskUpdate` + **GitHub checkbox updates**
- Validation: Never bypass gates
- Parallel: Use parallel tool calls (Read, Grep)
- **PowerShell**: Use `pwsh -c` for all commands on Windows

**❌ NEVER**:

- Skip errors or disable tests
- Merge without code review
- Ignore DoD or allow new warnings
- Use direct bash pipes on Windows (use `pwsh -c`)

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
🔄 Phase 0: Session Init ✅ (Branch: main-dev → feature/issue-2373-auth, PR target: main-dev)
📋 Phase 1: Preparation ✅ (patterns: FilterComponent) [✓ Checkbox updated]
🧠 Phase 2: Planning ✅ (Option 1, confidence: 95%) [✓ Checkbox updated]
🌿 Phase 3: Branch ✅ (feature/issue-2373-auth) [✓ Checkbox updated]
💻 Phase 4: Implementation ✅ (3/3 tasks) [✓ Checkbox updated]
✅ Phase 5: Testing ✅ (92% backend, 87% frontend) [✓ Checkbox updated]
🔍 Phase 6: Code Review ✅ (score: 85%) [✓ Checkboxes updated]
✔️ Phase 7: DoD ✅ (4/4) [✓ Checkbox updated]
🎯 Phase 8: Merge → main-dev ✅ [✓ Checkboxes updated]

✅ SUCCESS: Issue #2373 completed
📊 Stats: 15 files, 42 tests, 91% coverage, 85% review score
🌿 Branch: feature/issue-2373-auth → main-dev (merged & deleted)
```

---

## Execution Flow

```yaml
PHASE_0:
  - /sc:load
  - git status (GATE: fail if dirty)
  - Detect: base_branch (current), pr_target (parent or main-dev)
  - gh api: update checkbox "Started"

PHASE_1:
  - mcp__MCP_DOCKER__issue_read($N)
  - Parse: {title, DoD[], tasks[], checkboxes[]}
  - Agent Explore --quick
  - write_memory("issue_$N_analysis")
  - gh api: update checkbox "Analysis"

PHASE_2:
  - IF complex: sequential-thinking
  - Generate: 2-3 options with confidence%
  - IF max(confidence) ≥90%: auto-select
    ELSE: AskUserQuestion
  - TodoWrite (min 3 tasks)
  - write_memory("issue_$N_plan")
  - gh api: update checkbox "Planning"
  - GATE: STOP if all <70%

PHASE_3:
  - git checkout $base_branch && git pull
  - git checkout -b <type>/issue-$N-<desc>
  - write_memory("issue_$N_branch")
  - gh api: update checkbox "Branch Created"
  - GATE: STOP if exists or malformed

PHASE_4:
  - FOR task: select tool → implement → TaskUpdate
  - gh api: update checkbox per task
  - write_memory("issue_$N_implementation")
  - gh api: update checkbox "Implementation"
  - GATE: STOP on compilation error

PHASE_5:
  - pwsh -c "cd apps/api/src/Api; dotnet test" (≥90%)
  - pwsh -c "cd apps/web; pnpm test && typecheck && lint" (≥85%)
  - IF fail: isolate → root cause → guards → re-test
  - write_memory("issue_$N_tests")
  - gh api: update checkbox "Testing"
  - GATE: STOP if fail OR low coverage

PHASE_6:
  - git commit + push
  - gh pr create --draft --base $pr_target
  - gh api: update checkbox "PR Created"
  - /code-review:code-review
  - Fix loop (max 3, confidence ≥70)
  - gh pr ready
  - gh api: update checkbox "Code Review"
  - GATE: STOP if score <80%

PHASE_7:
  - Check DoD[]
  - issue_write (DoD checkboxes)
  - gh api: update checkbox "DoD Verified"
  - IF missing: AskUserQuestion
  - GATE: STOP if any not ✅

PHASE_8:
  - pwsh -c "cd apps/api/src/Api; dotnet build && test"
  - pwsh -c "cd apps/web; pnpm build && test && typecheck"
  - GATE: STOP if fail → AskUserQuestion
  - gh pr merge --squash --base $pr_target
  - gh api: update checkbox "Merged"
  - issue_write(closed)
  - Cleanup: checkout → pull → branch -D → prune
  - gh api: update checkbox "Cleanup"
  - /sc:save
  - write_memory("issue_$N_completed")
```

---

**Version**: 2.3 (2026-02-02)

**Changelog**:

v2.3 (2026-02-02):
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
- ✅ **PowerShell emphasis**: `pwsh -c` for all commands on Windows
- ✅ **Smart branch detection**: PR always from current branch to auto-detected parent
- ✅ **Progress tracking**: GitHub checkbox updates at every phase
- ✅ **Optimized structure**: Reduced verbosity, clearer flow
- ✅ **Branch flexibility**: Configurable --base-branch and --pr-target with smart defaults

v2.2 (2026-01-29):
<<<<<<< HEAD
=======

>>>>>>> a317f4777 (docs(claude): update implementa command documentation)
- ✅ Reduced verbosity by 45%
- ✅ PowerShell with pwsh -c for all pnpm commands
- ✅ Configurable base branch (--base-branch, default: main-dev)
- ✅ Explicit PR target branch (--pr-target, default: main-dev)

v2.1 (2026-01-28):

- ✅ Pre-merge build+test validation in Phase 8
- ✅ Mandatory validation gates

v2.0 (2026-01-24):

- ✅ Session management + checkpoints
- ✅ Validation gates per phase
- ✅ Pre-merge code review
- ✅ DoD verification phase
