# /implementa - Full Issue Implementation Workflow

> **Comando**: `/implementa <Issue N>`
> **Alias di**: `/sc:implement --uc Issue <Issue N>` con workflow completo e session management

## Descrizione

Workflow end-to-end di implementazione issue con:

- **Session management** automatico (Serena) con checkpoint e recovery
- **Validation gates** a ogni fase critica (compilazione, test, DoD, code review)
- **Selezione automatica** agenti/MCP/skill ottimali per ogni step
- **Code review pre-merge** con fix automatico problemi confidence >= 70
- **Branch lifecycle** completo: creazione, implementazione, PR, merge, cleanup

## Argomenti

- `$ARGUMENTS` - Numero issue GitHub (es: `2372`, `#2372`, `Issue 2372`)

## Workflow Phases

### Phase 0: Session Initialization 🔄

**Obiettivo**: Caricare contesto progetto e verificare stato pulito

1. **Load session**: `/sc:load` per caricare memoria progetto (Serena)
2. **Verify git**: `git status && git branch` per assicurare workspace pulito
3. **Check branch**: Assicurati di essere su `main-dev` o branch padre corretto

**Validation Gate**: ❌ Ferma se workspace sporco o branch sbagliato

---

### Phase 1: Preparation 📋

**Obiettivo**: Comprendere requisiti e contesto tecnico

1. **Fetch issue**: `mcp__MCP_DOCKER__issue_read` per leggere issue #N dal repo GitHub
2. **Parse requirements**: Estrai descrizione, DoD (Definition of Done), task list, labels
3. **Explore codebase**: Agent `Explore --quick` per trovare patterns/componenti esistenti rilevanti
4. **Checkpoint**: `write_memory("issue_<N>_analysis", {requirements, patterns, complexity})`

**Output**: Comprensione chiara di cosa implementare e dove nel codebase

---

### Phase 2: Planning 🧠

**Obiettivo**: Pianificare implementazione ottimale con opzioni valutate

1. **Deep analysis** (se complesso): `mcp__sequential-thinking__sequentialthinking` per analisi multi-step
2. **Generate options**: Pianifica 2-3 opzioni implementazione con pro/contro/trade-offs
3. **Evaluate**: Assegna confidence score (0-100%) a ogni opzione
4. **Decision**:
   - Se confidence >= 90%: Scegli automaticamente migliore opzione
   - Se confidence < 90%: Usa `AskUserQuestion` per chiedere conferma utente
5. **Task breakdown**: `TodoWrite` con tasks atomici (min 3 per issue complessa)
6. **Checkpoint**: `write_memory("issue_<N>_plan", {chosen_option, tasks, rationale})`

**Validation Gate**: ❌ Ferma se nessuna opzione ha confidence >= 70%

**Output**: Piano implementazione approvato con task breakdown

---

### Phase 3: Branch Setup 🌿

**Obiettivo**: Creare branch isolato per implementazione

1. **Sync parent**: `git checkout main-dev && git pull` per sincronizzare
2. **Create branch**: `git checkout -b <tipo>/issue-<id>-<breve-desc>`
   - **Tipi validi**:
     - `feature/` - Nuove funzionalità
     - `fix/` - Bug fix
     - `docs/` - Documentazione
     - `test/` - Test o miglioramenti test
     - `refactor/` - Refactoring senza cambio funzionalità
     - `chore/` - Manutenzione (deps, config, build)
   - **Esempio**: `feature/issue-2373-add-complexity-filter`
3. **Checkpoint**: `write_memory("issue_<N>_branch", branch_name)`

**Validation Gate**: ❌ Ferma se branch già esistente o nome malformato

**Output**: Branch pulito e pronto per implementazione

---

### Phase 4: Implementation 💻

**Obiettivo**: Implementare soluzione seguendo principi architetturali

1. **Per ogni task in TodoWrite**:
   a. **Select optimal tool**:
   - Symbol operations → `mcp__serena__*` (find_symbol, replace_symbol_body, etc.)
   - Bulk pattern edits → `mcp__morphllm-fast-apply__edit_file`
   - UI components → `mcp__magic__21st_magic_component_builder`
   - Complex files → Read → Sequential analysis → Edit
     b. **Implement**: Esegui implementazione seguendo:
   - **Backend**: CQRS (Command/Query), DDD (Domain entities), MediatR pattern
   - **Frontend**: React best practices, Zustand state, React Query data fetching
     c. **Update progress**:
   - `TaskUpdate` con status: `pending` → `in_progress` → `completed`
   - `mcp__MCP_DOCKER__issue_write` per aggiornare checkbox task su GitHub issue
     d. **Verify compilation**: Dopo ogni task, verifica che codice compili senza errori
2. **Architecture compliance**:
   - Backend: No direct service injection in endpoints, solo `IMediator.Send()`
   - Frontend: TypeScript strict, proper error handling, accessibility
3. **Checkpoint**: `write_memory("issue_<N>_implementation", {completed_tasks, files_changed})`

**Validation Gate**: ❌ Ferma se errori di compilazione (mostra errori, chiedi come procedere)

**Output**: Implementazione completa e compilante

---

### Phase 5: Testing & Verification ✅

**Obiettivo**: Validare implementazione con test automatici

1. **Run test suites**:
   - **Backend**: `cd apps/api/src/Api && dotnet test`
     - Target coverage: >= 90%
     - Check: Unit tests (domain logic) + Integration tests (handlers, DB)
   - **Frontend**: `cd apps/web && pnpm test && pnpm typecheck && pnpm lint`
     - Target coverage: >= 85%
     - Check: Component tests (Vitest) + Type safety + ESLint rules
2. **Verify build**: No warning in output (mantieni codebase pulita)
3. **Error handling** (se test falliscono):
   a. ❌ **NON skippare** - non commentare test, non disabilitare validazioni
   b. **Isolate**: Replica errore in test case isolato
   c. **Analyze**: Usa `sequential-thinking` per root cause analysis
   d. **Guard**: Crea validazioni/guardie per prevenire regressioni
   e. **Re-test**: Esegui di nuovo test suite completa
4. **Checkpoint**: `write_memory("issue_<N>_tests", {coverage, pass_rate, errors_fixed})`

**Validation Gate**: ❌ Ferma se:

- Test coverage < target (90% backend, 85% frontend)
- Qualsiasi test fallisce
- Build genera nuovi warning

**Output**: Test suite verde con coverage adeguata

---

### Phase 6: Code Review Pre-Merge 🔍

**Obiettivo**: Validare qualità codice PRIMA del merge

1. **Commit changes**:

   ```bash
   git add .
   git commit -m "<tipo>(<scope>): <descrizione>

   - Task 1 completato
   - Task 2 completato

   Refs: #<issue-number>"
   ```

2. Push branch: git push -u origin <branch-name>
3. Create PR draft:
   gh pr create --draft \
    --title "Issue #<N>: <titolo-chiaro>" \
    --body "$(cat <<'EOF'

## Summary

  <Descrizione implementazione>

## Changes

- <Cambio 1>
- <Cambio 2>

## Testing

- [x] Unit tests passed
- [x] Integration tests passed
- [x] Manual testing completed

Closes #<issue-number>
EOF
)" 4. Automated code review: /code-review:code-review <PR-URL> - Esegue 5 agenti paralleli: security, performance, quality, architecture, testing - Ogni issue ha confidence score (0-100%) 5. Fix loop (max 3 iterazioni):
a. Filter: Considera SOLO problemi con confidence >= 70%
b. Fix: Applica fix automatico per ogni problema >= 70
c. Test: Re-run test suite dopo ogni fix
d. Re-review: Esegui di nuovo /code-review:code-review
e. Iterate: Ripeti finché tutti problemi >= 70 sono risolti 6. Escalation (se loop > 3 iterazioni): - Usa AskUserQuestion per chiedere: - a) Ignorare problemi rimanenti (se tutti < 70 score) - b) Continuare fixing manualmente - c) Richiedere review umana 7. Mark ready: gh pr ready quando tutti problemi >= 70 risolti

Validation Gate: ❌ Ferma se PR score complessivo < 80%

Output: PR pronta per merge con alta qualità codice

---

Phase 7: DoD Verification ✔️

Obiettivo: Verificare completamento Definition of Done

1. Fetch DoD: Recupera lista DoD dall'issue originale (già letta in Phase 1)
2. Check each DoD:


    - Implementazione completata ✅
    - Test passati ✅
    - Documentazione aggiornata (se richiesta) ✅
    - Code review superata ✅
    - Nessun warning nuovo ✅
    - Performance verificata (se DoD richiede) ✅

3. Update issue: mcp**MCP_DOCKER**issue_write per aggiornare checkbox DoD su GitHub

Validation Gate: ❌ Ferma se qualsiasi DoD non è ✅

- Lista DoD mancanti
- Chiedi con AskUserQuestion se:
  - a) Completare DoD mancanti
  - b) Modificare DoD nell'issue (se non più rilevanti)
  - c) Creare sub-issue per DoD complessi

Output: Tutti i DoD verificati e completati

---

Phase 8: Merge & Closure 🎯

Obiettivo: Validare build/test finali e integrare implementazione

1. **Final Build Validation** (MANDATORY before merge):
   ```bash
   # Backend
   cd apps/api/src/Api && dotnet build --no-restore

   # Frontend
   cd apps/web && pnpm build
   ```

2. **Final Test Run** (MANDATORY before merge):
   ```bash
   # Backend - full test suite
   cd apps/api/src/Api && dotnet test --no-build

   # Frontend - full test suite + type check
   cd apps/web && pnpm test && pnpm typecheck
   ```

**Validation Gate**: ❌ STOP merge se:
- Build backend o frontend fallisce
- Qualsiasi test fallisce
- TypeScript ha errori

**Se validazione fallisce**:
```
❌ STOP workflow
📋 Show build/test errors
🤔 AskUserQuestion: "Final validation failed before merge. Options?"
   a) Fix errors and retry validation
   b) Abort merge and investigate
   c) Force merge anyway (NOT RECOMMENDED - requires explicit user approval)
```

3. Merge PR (SOLO dopo validazione ✅):
   ```bash
   gh pr merge --squash --delete-branch
   ```
   # O usa --merge se convenzione progetto richiede merge commit

2. Close issue:

# Via MCP

mcp**MCP_DOCKER**issue*write(
method: "update",
issue_number: <N>,
state: "closed",
state_reason: "completed"
) 3. Branch cleanup:
git checkout main-dev
git pull # Sincronizza con merge appena fatto
git branch -D <branch-implementazione> # Elimina locale
git remote prune origin # Rimuove tracking remoti obsoleti 4. Session save: /sc:save per persistere learnings e patterns scoperti 5. Checkpoint finale: write_memory("issue*<N>\_completed", {merge_sha, closed_at, learnings})

Output: Issue chiusa, codice integrato, branch pulito

---

MCP/Agent/Tool Integration Matrix
┌───────┬──────────────────┬──────────────────────────────────────────────────────────────┐
│ Phase │ Step │ Tool/MCP/Agent │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 0 │ Session init │ mcp**serena**activate_project, /sc:load │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 1 │ Fetch issue │ mcp**MCP_DOCKER**issue_read │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 1 │ Explore codebase │ Agent Explore --quick │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 1 │ Checkpoint │ mcp**serena**write_memory │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 2 │ Complex analysis │ mcp**sequential-thinking**sequentialthinking │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 2 │ Ask user │ AskUserQuestion │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 2 │ Task breakdown │ TodoWrite │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 2 │ Checkpoint │ mcp**serena**write_memory │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 3 │ Branch ops │ Bash (git commands) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 3 │ Checkpoint │ mcp**serena**write_memory │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ Symbol ops │ mcp**serena**find_symbol, replace_symbol_body, rename_symbol │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ Bulk edits │ mcp**morphllm-fast-apply**edit_file │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ UI components │ mcp**magic**21st_magic_component_builder │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ File read │ Read │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ File edit │ Edit │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ Update tasks │ TaskUpdate │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ Update issue │ mcp**MCP_DOCKER**issue_write │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 4 │ Checkpoint │ mcp**serena**write_memory │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 5 │ Run tests │ Bash (dotnet test, pnpm test) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 5 │ Root cause │ mcp**sequential-thinking**sequentialthinking │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 5 │ Checkpoint │ mcp**serena**write_memory │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 6 │ Git ops │ Bash (git add/commit/push) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 6 │ Create PR │ Bash (gh pr create) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 6 │ Code review │ Skill /code-review:code-review │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 6 │ Mark ready │ Bash (gh pr ready) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 7 │ Update DoD │ mcp**MCP_DOCKER**issue_write │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 7 │ Ask user │ AskUserQuestion │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Final build │ Bash (dotnet build, pnpm build) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Final tests │ Bash (dotnet test, pnpm test, pnpm typecheck) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Merge │ Bash (gh pr merge) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Close issue │ mcp**MCP_DOCKER**issue_write │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Cleanup │ Bash (git branch -D, remote prune) │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Session save │ /sc:save │
├───────┼──────────────────┼──────────────────────────────────────────────────────────────┤
│ 8 │ Checkpoint │ mcp**serena**write_memory │
└───────┴──────────────────┴──────────────────────────────────────────────────────────────┘

---

Regole Critiche di Esecuzione

🔴 FERMA Workflow Se

- Phase 0: Workspace sporco o branch sbagliato
- Phase 2: Nessuna opzione con confidence >= 70%
- Phase 3: Branch già esistente o nome malformato
- Phase 4: Errori di compilazione (mostra errori, chiedi utente)
- Phase 5: Test falliscono o coverage < target
- Phase 6: PR score < 80% dopo 3 iterazioni code review
- Phase 7: DoD mancanti (lista, chiedi come procedere)
- Phase 8: Build o test falliscono nella validazione finale pre-merge

✅ SEMPRE Esegui

- Session management: /sc:load all'inizio, /sc:save alla fine
- Checkpoint frequenti: write_memory() dopo ogni phase per recovery
- TodoWrite + TaskUpdate: Tracking sistematico progresso
- Validation gates: Non bypassare mai validazioni critiche
- Parallel operations: Usa tool call paralleli dove possibile (Read, Grep)

❌ MAI Fare

- Skip errori: Replica, analizza root cause, crea guardie
- Disable tests: Non commentare, non skippare validazioni
- Merge senza code review: Code review SEMPRE prima del merge
- Ignora DoD: Tutti DoD devono essere ✅ prima del merge
- Nuovi warning: Mantieni codebase pulita

🤝 Chiedi Utente Quando

- Confidenza < 90%: Decisioni implementazione non chiare
- Code review loop > 3: Problemi persistenti o molti problemi score < 70
- DoD mancanti: Non tutti DoD completabili o rilevanti
- Compito > 1 giorno stima: Considera creare sub-issue

---

Gestione Errori e Recovery

Compilazione Fail

❌ STOP workflow
📋 Show compilation errors
🤔 AskUserQuestion: "Compilation failed. How to proceed?"
a) Fix automatically (if errors are clear)
b) Show errors and wait for manual fix
c) Abort and save checkpoint

Test Fail

❌ STOP workflow (NO skip!)
🔍 Isolate: Replica errore in test isolato
🧠 Analyze: Use sequential-thinking for root cause
🛡️ Guard: Crea validazioni per prevenire regressioni
♻️ Re-test: Esegui test suite completa
✅ Continue se tutti test passano

Code Review Loop > 3 Iterazioni

⚠️ PAUSE loop
📊 Show remaining issues con confidence scores
🤔 AskUserQuestion: "Code review stuck after 3 iterations. Options?"
a) Ignore issues with score < 70 and proceed
b) Continue fixing manually (show issues)
c) Request human review (mark PR for manual review)

DoD Mancanti

❌ STOP merge
📋 List missing DoD items
🤔 AskUserQuestion: "Missing DoD items. How to proceed?"
a) Complete missing DoD now
b) Update issue DoD (if no longer relevant)
c) Create sub-issue for complex DoD (continue with current implementation)

Recovery da Checkpoint

# Se workflow interrotto, recupera stato:

/sc:load
list*memories() # Mostra checkpoint disponibili
read_memory("issue*<N>\_<last_phase>") # Recupera ultimo stato

# Riprendi da phase successiva

---

Esempi di Utilizzo

Esempio 1: Feature Semplice

/implementa 2373

# Issue: "Add complexity filter to game search"

# Output:

# Phase 0: Session Init ✅

# Phase 1: Preparation ✅ (patterns found: FilterComponent.tsx)

# Phase 2: Planning ✅ (Option 1 chosen: extend existing FilterBar, confidence: 95%)

# Phase 3: Setup ✅ (branch: feature/issue-2373-add-complexity-filter)

# Phase 4: Implementation ✅ (3/3 tasks completed)

# Phase 5: Testing ✅ (coverage: 92% backend, 87% frontend)

# Phase 6: Code Review ✅ (2 issues fixed, score: 85%)

# Phase 7: DoD Verification ✅ (4/4 DoD completed)

# Phase 8: Merge & Closure ✅

# ✅ Issue #2373 completed: https://github.com/.../pull/XYZ

Esempio 2: Bug Fix con Test Fail

/implementa 2401

# Issue: "Fix null reference in game details API"

# ...

# Phase 5: Testing ❌

# Error: NullReferenceException in GameDetailsHandlerTests

# 🔍 Isolating error...

# 🧠 Root cause: Missing null check for optional Description field

# 🛡️ Adding guard: if (game.Description == null) return empty string

# ♻️ Re-running tests... ✅ All tests passed

# Phase 5: Testing ✅ (after fix)

# ...

Esempio 3: Code Review Loop

/implementa 2450

# Issue: "Refactor authentication middleware"

# ...

# Phase 6: Code Review 🔄

# Iteration 1: 5 issues (3 score >= 70) → Fixed 3

# Iteration 2: 3 issues (2 score >= 70) → Fixed 2

# Iteration 3: 2 issues (1 score 75, 1 score 65)

# 🤔 AskUserQuestion: "Code review stuck. Options?"

# User: "a) Ignore score < 70"

# Phase 6: Code Review ✅ (final score: 82%)

# ...

---

Output Template Atteso

🔄 Phase 0: Session Initialization
✅ Session loaded (project: meepleai-monorepo)
✅ Git status: clean
✅ Current branch: main-dev

📋 Phase 1: Preparation
✅ Issue #<N> fetched: "<titolo>"
✅ DoD items: X
✅ Patterns found: <ComponentName>, <ServiceName>
✅ Complexity: <low|medium|high>

🧠 Phase 2: Planning
📊 Option 1: <descrizione> (confidence: X%)
📊 Option 2: <descrizione> (confidence: Y%)
✅ Selected: Option Z (confidence: Z%)
✅ TodoWrite created: N tasks

🌿 Phase 3: Branch Setup
✅ Branch created: <tipo>/issue-<N>-<desc>

💻 Phase 4: Implementation
✅ Task 1/N: <descrizione> (files: X changed)
✅ Task 2/N: <descrizione> (files: Y changed)
...
✅ All tasks completed
✅ Compilation: PASS
✅ Issue updated on GitHub

✅ Phase 5: Testing & Verification
✅ Backend tests: PASS (coverage: X%)
✅ Frontend tests: PASS (coverage: Y%)
✅ Build: 0 warnings

🔍 Phase 6: Code Review Pre-Merge
✅ PR created (draft): <url>
🔄 Code review iteration 1: X issues found
✅ Fixed X issues (score >= 70)
✅ Code review iteration 2: Y issues found
✅ Fixed Y issues (score >= 70)
✅ Final score: Z%
✅ PR marked ready for review

✔️ Phase 7: DoD Verification
✅ DoD 1: Implementation ✅
✅ DoD 2: Tests ✅
✅ DoD 3: Documentation ✅
✅ DoD 4: Code Review ✅
✅ All DoD verified

🎯 Phase 8: Merge & Closure
✅ Backend build: PASS
✅ Frontend build: PASS
✅ Backend tests: PASS (final validation)
✅ Frontend tests + typecheck: PASS (final validation)
✅ PR merged: <url>
✅ Issue #<N> closed
✅ Branch cleaned (local + remote)
✅ Session saved

✅ SUCCESS: Issue #<N> completata
📊 Stats: - Files changed: X - Tests added: Y - Coverage: Z% - Code review score: W% - Time: <duration>

---

Prompt di Esecuzione Completo

Quando utente esegue: /implementa <N>

Sistema esegue:

CRITICAL RULES:

- ❌ STOP at validation gates if fail
- ❌ NEVER skip errors or disable tests
- ✅ ALWAYS checkpoint after each phase
- ✅ ALWAYS ask user if confidence < 90%
- ✅ Code review BEFORE merge (not after!)

EXECUTION FLOW:

=== PHASE 0: SESSION INIT ===

1. Execute: /sc:load
2. Verify: git status (fail if dirty)
3. Verify: git branch (fail if not main-dev/parent)

=== PHASE 1: PREPARATION ===

1. Fetch: mcp**MCP_DOCKER**issue_read(issue_number: $N)
2. Parse: Extract {title, description, DoD[], tasks[], labels[]}
3. Explore: Agent Explore --quick for patterns
4. Checkpoint: write*memory("issue*$N_analysis", results)

=== PHASE 2: PLANNING ===

1. IF complex: mcp\_\_sequential-thinking (breakdown problem)
2. Generate: 2-3 implementation options with {approach, pros[], cons[], confidence%}
3. Select:
   IF max(confidence) >= 90%: auto-select best
   ELSE: AskUserQuestion(options) → user selects
4. Breakdown: TodoWrite with atomic tasks (min 3 for complex)
5. Checkpoint: write*memory("issue*$N_plan", {option, tasks})
   GATE: STOP if all confidence < 70%

=== PHASE 3: BRANCH SETUP ===

1. Sync: git checkout main-dev && git pull
2. Create: git checkout -b <tipo>/issue-$N-<desc>
   Types: feature|fix|docs|test|refactor|chore
3. Checkpoint: write*memory("issue*$N_branch", branch_name)
   GATE: STOP if branch exists or malformed name

=== PHASE 4: IMPLEMENTATION ===
FOR EACH task in TodoWrite: 1. Select tool: - Symbol ops → mcp**serena**\* - Bulk edits → mcp**morphllm-fast-apply**edit*file - UI → mcp**magic**21st_magic_component_builder 2. Implement following architecture: - Backend: CQRS, DDD, MediatR only - Frontend: React, Zustand, React Query 3. Update: TaskUpdate(status: in_progress → completed) 4. Update: mcp**MCP_DOCKER**issue_write (checkbox on GitHub) 5. Verify: Compilation passes 5. Checkpoint: write_memory("issue*$N_implementation", results)
GATE: STOP if compilation errors (show, ask user)

=== PHASE 5: TESTING ===

1. Backend: dotnet test (target: >= 90% coverage)
2. Frontend: pnpm test && pnpm typecheck && pnpm lint (target: >= 85%)
3. Verify: 0 new warnings
4. IF tests fail:
   a. Isolate error in test case
   b. Root cause: mcp\_\_sequential-thinking
   c. Create guards/validations
   d. Re-run tests
   e. STOP if still failing (ask user)
5. Checkpoint: write*memory("issue*$N_tests", {coverage, status})
   GATE: STOP if tests fail OR coverage < target

=== PHASE 6: CODE REVIEW PRE-MERGE ===

1. Commit: git add . && git commit -m "<tipo>(<scope>): <desc>\n\nRefs: #$N"
2. Push: git push -u origin <branch>
3. PR: gh pr create --draft --title "Issue #$N: <title>" --body "<summary>"
4. Review: /code-review:code-review <PR-URL>
5. Fix loop (max 3 iterations):
   a. Filter: issues with confidence >= 70 only
   b. Fix: Apply automated fixes
   c. Test: Re-run test suite
   d. Re-review: /code-review:code-review
   e. IF iteration > 3: AskUserQuestion(options)
6. Ready: gh pr ready
   GATE: STOP if final score < 80%

=== PHASE 7: DOD VERIFICATION ===

1. Check: Each DoD item from issue
2. Update: mcp**MCP_DOCKER**issue_write (DoD checkboxes)
3. IF missing DoD:
   AskUserQuestion: complete | update DoD | create sub-issue
   GATE: STOP if any DoD not ✅ (unless user approves)

=== PHASE 8: MERGE & CLOSURE ===

1. Final Build Validation (MANDATORY):
   - Backend: cd apps/api/src/Api && dotnet build --no-restore
   - Frontend: cd apps/web && pnpm build
   GATE: STOP if build fails
2. Final Test Run (MANDATORY):
   - Backend: dotnet test --no-build
   - Frontend: pnpm test && pnpm typecheck
   GATE: STOP if tests fail or typecheck errors
   IF validation fails: AskUserQuestion(fix | abort | force)
3. Merge: gh pr merge --squash --delete-branch (ONLY after validation ✅)
4. Close: mcp**MCP_DOCKER**issue_write(state: closed, reason: completed)
6. Cleanup:
   - git checkout main-dev && git pull
   - git branch -D <branch>
   - git remote prune origin
7. Save: /sc:save
8. Checkpoint: write_memory("issue_$N_completed", final_stats)

=== OUTPUT ===
Report progress after each phase with ✅/❌/🔄 status
Final summary with stats: files changed, tests added, coverage, score, time

---

Versione: 2.1 (2026-01-28)
Changelog:

v2.1 (2026-01-28):
- ✅ **Validazione build+test finale PRIMA del merge** in Phase 8
- ✅ GATE obbligatorio: build backend/frontend + test suite completa pre-merge
- ✅ Gestione fallimento validazione finale con opzioni utente

v2.0 (2026-01-24):
- ✅ Aggiunto session management (Phase 0, /sc:load, /sc:save)
- ✅ Aggiunto checkpoint system con Serena memory
- ✅ Validation gates a ogni fase critica
- ✅ Code review PRIMA del merge (non dopo)
- ✅ Nuova Phase 7: DoD Verification
- ✅ Gestione errori strutturata (compilation, test, code review, DoD)
- ✅ Branch naming chiaro (<tipo>/issue-<id>-<desc>)
- ✅ MCP integration matrix completa
- ✅ Recovery capability da checkpoint
- ✅ Eliminata duplicazione steps
- ✅ TodoWrite + TaskUpdate integration
- ✅ AskUserQuestion per decisioni < 90% confidence
