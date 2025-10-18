---
description: Workflow automatizzato completo per lavorare su issue con BDD, test, code review e PR
---

# Work - Workflow Issue Completo

Workflow end-to-end automatizzato per issue con approccio BDD, testing, code review e gestione PR.

**USAGE:** `/work <issue-id|issue-number> [--wait]`

**FLAGS:**
- `--wait`: Monitora CI fino a completamento (con auto-fix su failure)
- Default: Termina dopo creazione PR

---

## Agent & MCP Strategy

### Phase 1: Discovery & Analysis
**Agents:** `Explore` (very thorough), `doc-researcher-optimizer`
**MCP:** `memory_recall`, `github_search_code`, `resolve-library-id`, `get-library-docs` (Context7)

- **Explore**: Esplorazione approfondita codebase (file correlati, pattern, architettura)
- **doc-researcher-optimizer**: Ricerca documentazione framework via Context7 (best practices up-to-date)
- **Context7 MCP**: Documentazione librerie/framework per versioni specifiche (React, ASP.NET Core, Next.js)

### Phase 2: BDD Planning
**Agents:** `strategic-advisor`, `system-architect`
**MCP:** `sequential_start`, `sequential_step`

- **strategic-advisor**: Decision-making strategico, trade-off analysis
- **system-architect**: Design architetturale, interfacce, integration points

### Phase 3: Test-First Implementation
**Agents:** `typescript-expert-developer` (frontend), `deep-think-developer` (backend)
**MCP:** NO MCP (agenti scrivono test direttamente)

- Scrivere test BDD prima del codice (RED phase)
- Unit, integration, E2E tests

### Phase 4: Implementation
**Agents:** `typescript-expert-developer`, `deep-think-developer`
**MCP:** `track_progress` (memory bank), **`magic_generate` (SOLO per UI components)**

- **Backend**: NO Magic (deep-think-developer scrive C#/.NET direttamente)
- **Frontend UI**: `magic_generate` SOLO se feature richiede nuovi componenti UI
- **Frontend logic**: typescript-expert-developer scrive hooks/logic direttamente
- `track_progress`: Salvare milestones in memory bank

### Phase 5: Local Testing
**Tools:** `Bash` (dotnet test, pnpm test, playwright)
**Command:** `/debug` (auto-fix se RED)

- Test suite completa, coverage validation
- Auto-fix con `/debug` se RED (max 2 iterazioni)
- BLOCCO se RED dopo auto-fix attempts

### Phase 6: Code Review (Self)
**Agents:** `deep-think-developer`, `typescript-expert-developer`
**MCP:** `memory_recall` (best practices del progetto)

- Analisi code quality, breaking changes, performance
- Recall pattern e convenzioni del progetto da memory bank

### Phase 7: Definition of Done Check
**Agents:** `issue-manager`
**MCP:** `github_list_issues`

- Fetch issue details, validare DoD checklist

### Phase 8: Update Issue DoD (NEW)
**Tools:** `gh issue edit`
**MCP:** `github_*`

- Aggiorna i checkbox DoD nella issue description
- Marca criteri completati con [x]
- Documenta technical debt con [x] o [ ] + nota "⚠️"
- Aggiungi sezione "Implementation Status" con link PR

### Phase 9: PR Creation
**MCP:** `github_create_pr`

- Push branch, creare PR con review inclusa

### Phase 10: CI Monitoring (--wait only)
**Command:** `/debug` (auto-fix se CI fallisce)
**Agents:** `data-analyst-deep-think` (log analysis only)
**MCP:** `github_*` (CI status)

- Monitorare CI, auto-fix con `/debug` se fallisce, re-push (max 3 tentativi)

---

## Workflow Steps (Conciso)

### 0️⃣ SETUP
```bash
gh issue view <issue-id>
git checkout -b feature/<issue-id>
```

### 1️⃣ DISCOVERY
**Agents:** Explore + doc-researcher-optimizer
**MCP:** Context7 per docs framework, memory_recall per pattern esistenti

```
🔍 Output: File impattati, best practices (Context7: ASP.NET/React/Next.js), pattern riutilizzabili
```

### 2️⃣ BDD PLANNING
**Agents:** strategic-advisor + system-architect
**MCP:** sequential (planning step-by-step)

```gherkin
Feature: [Nome]
Scenario: [Happy path]
  Given [contesto]
  When [azione]
  Then [risultato]
```

### 3️⃣ TEST-FIRST (RED)
**Agents:** deep-think-developer, typescript-expert-developer

```bash
git commit -m "test: add BDD tests for <issue-id>"
# Tests must FAIL (RED phase)
```

### 4️⃣ IMPLEMENTATION (GREEN)
**Agents:** deep-think-developer, typescript-expert-developer
**MCP:** magic_generate (SOLO per UI), track_progress

**Backend C#:**
```bash
# deep-think-developer scrive codice direttamente (NO magic)
git commit -m "feat(api): implement <issue-id>"
```

**Frontend UI components:**
```bash
# typescript-expert-developer usa magic_generate PER COMPONENTI UI
# Per hooks/logic: scrive direttamente (NO magic)
git commit -m "feat(web): implement <issue-id> UI"
```

### 5️⃣ LOCAL TESTS (GATE)
```bash
dotnet test --verbosity normal  # Backend
pnpm test                        # Frontend
pnpm test:e2e                   # E2E
# BLOCCO se RED ❌
```

### 5️⃣.5 AUTO-FIX FALLIMENTI (NEW)
**Command:** `/debug` (se tests falliscono)

**Se local tests RED:**
1. Cattura errore completo (stack trace, test name, assertion failure)
2. Esegui `/debug <error_message>`
3. `/debug` genera 2 soluzioni, seleziona la migliore, implementa fix + tests
4. Re-run tests (max 2 iterazioni)
5. Se ancora RED dopo 2 tentativi → BLOCCO (richiede intervento umano)

**Output atteso:**
```
🔧 LOCAL TEST FAILURE DETECTED
━━━━━━━━━━━━━━━━━━━━━━━━━━━━

❌ Failed: ChatServiceTests.SendMessage_ShouldPersistChat

🔍 Auto-fixing with /debug...
⚖️  Solution B (Score: 88): Add null check + validation guard
✅ Fix applied: ChatService.cs, ChatServiceTests.cs
✅ Tests: +3 resilience tests

🔁 Re-running tests...
✅ All tests GREEN

💾 Commit: fix: add null check for chat persistence (auto-debug)
```

### 6️⃣ SELF REVIEW
**Agents:** deep-think-developer, typescript-expert-developer
**MCP:** memory_recall (project best practices)

```markdown
## Code Review
✅ Strengths: [...]
⚠️  Considerations: [...]
🔍 Breaking Changes: [...]
```

### 7️⃣ DOD CHECK
**Agent:** issue-manager
**MCP:** github_list_issues

```
✅ Implementation complete
✅ Tests green (coverage OK)
✅ Code reviewed
✅ Docs updated
✅ No breaking changes
```

### 8️⃣ UPDATE ISSUE DOD
**Tools:** gh issue edit

```bash
# Aggiorna issue body con DoD completati
gh issue edit <issue-id> --body "$(cat <<'EOF'
## Acceptance Criteria
- [x] Export button added ✅
- [x] Modal component ✅
- [ ] Unit tests (⚠️ Technical Debt)
...

## Definition of Done
- [x] Code implemented ✅
- [x] Build passes ✅
- [ ] Tests written (⚠️ Follow-up needed)
...

## ✅ Implementation Status
Merged: PR #XXX
Status: COMPLETED with documented technical debt
EOF
)"
```

### 9️⃣ PR CREATION
**MCP:** github_create_pr

```bash
git push -u origin feature/<issue-id>
gh pr create --title "<issue-id>: <title>" --body "..."
```

### 🔟 CI MONITORING (--wait only)
**Command:** `/debug` (se CI fallisce)
**Agents:** data-analyst-deep-think (analisi logs)
**MCP:** github_*, sequential_*

**If CI FAILURE:**
1. Fetch CI logs via `gh run view --log-failed`
2. **Esegui `/debug <ci_error_message>`** invece di analisi manuale
3. `/debug` genera 2 soluzioni automaticamente, seleziona la migliore
4. `/debug` implementa fix + tests + commit + push
5. Re-monitor CI (max 3 attempts)
6. Se fallisce dopo 3 tentativi → BLOCCO (richiede intervento umano)

**Output atteso:**
```
🔧 CI FAILURE DETECTED (Attempt 1/3)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

❌ Job: ci-api / Build & Test
   Error: CS0246: The type 'StreamingQaService' could not be found

🔍 Auto-fixing with /debug...

📊 Analysis:
   Error Type: CompilationError
   Layer: API
   Service: Program.cs
   Impact: 10/10 (Critical - blocks build)

⚖️  Solutions:
   A (Score: 65): Add missing using statement
   B (Score: 82): Add DI registration + using ✓ SELECTED

🛠️  Fix Applied:
   ✅ Program.cs: Added DI registration for IStreamingQaService
   ✅ Using statement added
   ✅ Tests: +2 integration tests for DI resolution

💾 Commit: fix(api): add StreamingQaService DI registration (auto-debug CI fix)
🚀 Pushed to feature/<issue-id>

🔁 Re-monitoring CI...
⏳ Build in progress...
✅ CI PASSED (Attempt 2/3)
```

---

## Magic (21st.dev) - Usage Rules

**✅ USE Magic when:**
- Feature richiede NUOVI componenti UI
- Form, dashboard, landing page, design system
- Trasformazione componenti tra framework/stili

**❌ DON'T use Magic for:**
- Backend C#/.NET code
- React hooks/logic (no UI)
- Test code
- Code review
- CI log analysis

**Example (correct usage):**
```bash
# ✅ CORRECT: Feature richiede form UI
/work UI-15  # Issue: "Create login form with validation"
# → Agent usa magic_generate per form component

# ❌ WRONG: Feature è backend API
/work API-10  # Issue: "Add streaming endpoint"
# → NO magic_generate (deep-think-developer scrive C# direttamente)
```

---

## Context7 (Upstash) - Usage Rules

**✅ USE Context7 when:**
- Serve documentazione framework up-to-date
- Best practices per libreria specifica
- Sintassi API cambiate tra versioni
- Esempi codice ufficiali

**Tools:**
- `resolve-library-id`: React → `/facebook/react`
- `get-library-docs`: Recupera docs per React 18

**Example (correct usage):**
```bash
# Discovery phase per feature SSE
# doc-researcher-optimizer usa Context7:
resolve-library-id("aspnetcore")
get-library-docs("/dotnet/aspnetcore", topic="server-sent-events")
# → Recupera best practices ASP.NET Core 9 per SSE
```

---

## Agent Selection Logic

**Backend-heavy** (API, Services, DB):
- Discovery: Explore + doc-researcher-optimizer (Context7 per ASP.NET docs)
- Planning: strategic-advisor + system-architect
- Implementation: deep-think-developer (NO magic)
- Review: deep-think-developer

**Frontend-heavy** (UI, React, Next.js):
- Discovery: Explore + doc-researcher-optimizer (Context7 per React/Next docs)
- Planning: strategic-advisor
- Implementation: typescript-expert-developer (magic_generate SOLO per UI components)
- Review: typescript-expert-developer

**Full-stack** (API + UI):
- Discovery: Explore + doc-researcher-optimizer (Context7 per entrambi)
- Planning: strategic-advisor + system-architect
- Implementation: deep-think-developer (backend) + typescript-expert-developer (frontend + UI con magic)
- Review: Both agents

---

## Final Summary Format

### Success (without --wait)
```
✅ WORKFLOW COMPLETE: <issue-id>

📊 Summary:
- Branch: feature/<issue-id>
- Commits: 3
- Agents: Explore, strategic-advisor, deep-think-developer, typescript-expert-developer
- MCP: Context7 (docs), sequential (planning), magic (UI only), memory_recall, github_*
- Tests: All green
- Coverage: Backend X%, Frontend Y%
- PR: #XXX created

🎯 DoD: 5/5 complete
⏱️  Duration: Xm Ys

➡️  Next: Human code review on PR #XXX
```

### Success (with --wait + CI passed)
```
✅ WORKFLOW COMPLETE: <issue-id>

📊 Summary:
- All above + CI: ✅ Passed
- Duration: Xm Ys (including CI)

🎉 PR ready for human review and merge
```

### Success (with --wait + auto-debug)
```
✅ WORKFLOW COMPLETE (with auto-debug): <issue-id>

🔧 Auto-Debug Applied:
- Local Test Fix: StreamingQaServiceTests RED → /debug → Solution B (Score: 88) → GREEN ✅
- CI Fix (Attempt 1): Test timeout → /debug → Solution B (Score: 85) → CI PASSED ✅

📊 Summary:
- Commits: 5 (3 feature + 2 auto-debug fixes)
- Agents: [...] + /debug command (2 executions)
- Tests: +6 resilience tests added by auto-debug
- Coverage: Backend +2.1%, Frontend +1.5%
- CI: ✅ Passed on attempt 2/3
- Duration: Xm Ys (including auto-debug)
- Auto-Debug Issues Created: #XXX (local test fix), #YYY (CI fix)
- Auto-Debug PRs: Fixes squashed into feature PR
```

### Failure
```
❌ WORKFLOW BLOCKED: <issue-id>

🚫 Blocker: [Local tests RED | CI failed after 3 attempts]

Failed: [details]

🔧 Action Required:
- Fix manually
- Re-run: /work <issue-id> [--wait]
```

---

## Best Practices

### DoD Update (Phase 8)
✅ **DO:**
- Mark ALL completed acceptance criteria with [x]
- Document technical debt with [ ] + "⚠️ Technical Debt" note
- Add "Implementation Status" section with PR link
- Include merge status and commit hash
- List known limitations clearly
- Suggest follow-up issues for deferred work

❌ **DON'T:**
- Skip DoD update (users need visibility)
- Mark incomplete items as complete
- Hide technical debt (be transparent)
- Forget to update after merge

**Example DoD Update:**
```markdown
### Frontend
- [x] Export button added ✅
- [x] Modal component ✅
- [ ] Unit tests (⚠️ Technical Debt - Follow-up #XXX)

## ✅ Implementation Status
**Merged:** PR #466 (commit: be00353)
**Status:** COMPLETED with documented technical debt

### Known Technical Debt
- [ ] Unit tests for formatters (High Priority)
- [ ] Integration tests (High Priority)
- [ ] Performance testing 100+ messages (Medium Priority)
```

### MCP Usage
✅ **DO:**
- Use Context7 per docs framework up-to-date
- Use magic_generate SOLO per UI components
- Use memory_recall per pattern esistenti
- Use sequential per planning complesso
- Use github_* per operazioni repository

❌ **DON'T:**
- Use magic per backend/logic code
- Use magic per code review
- Skip Context7 quando serve docs framework
- Use MCP ridondanti

### Agent Usage
✅ **DO:**
- Explore (very thorough) per codebase analysis
- doc-researcher-optimizer + Context7 per framework docs
- deep-think-developer per backend critical
- typescript-expert-developer per frontend type-safe
- data-analyst-deep-think per CI log analysis

❌ **DON'T:**
- Use general-purpose quando esiste specialized agent
- Use wrong agent per linguaggio (TS dev per C#)

---

## Examples

### Example 1: Backend API Feature
```bash
/work API-05  # "Add streaming SSE endpoint"

Agents: Explore, doc-researcher-optimizer, strategic-advisor, deep-think-developer
MCP Used:
  - Context7: ASP.NET Core 9 docs for IAsyncEnumerable
  - sequential: Planning SSE event flow
  - memory_recall: Recall AI-05 cache pattern
  - github_create_pr: Create PR
MCP NOT Used:
  - magic_* (backend code, no UI)

Result: PR #455 created, 100% backend C#
```

### Example 2: Frontend UI Feature
```bash
/work UI-12  # "Create dashboard with cards and charts"

Agents: Explore, doc-researcher-optimizer, typescript-expert-developer
MCP Used:
  - Context7: Next.js 14 docs for App Router
  - magic_generate: Generate dashboard UI components
  - github_create_pr: Create PR
MCP NOT Used:
  - magic for hooks/logic (typescript-expert writes directly)

Result: PR #467 created, UI components + logic
```

### Example 3: Full-Stack Feature (with auto-debug)
```bash
/work CHAT-01 --wait  # "Streaming responses (API + UI)"

Agents: Explore, doc-researcher-optimizer, strategic-advisor, system-architect,
        deep-think-developer, typescript-expert-developer

MCP Used:
  - Context7: ASP.NET Core SSE + React EventSource docs
  - sequential: Planning architecture
  - magic_generate: Generate streaming UI components (progress, stop button)
  - track_progress: Save milestones
  - github_*: PR creation, CI monitoring
MCP NOT Used:
  - magic for backend C# (deep-think writes)
  - magic for React hooks (typescript-expert writes)

Error Recovery:
  1. Local tests: StreamingQaServiceTests.AskStreamAsync_ShouldStreamTokens FAILED
     → /debug executed → Solution: Add CancellationToken handling
     → Re-run → GREEN ✅

  2. CI failure (Attempt 1): Test timeout in CI environment
     → /debug executed → Solution B (Score: 85): Conditional timeout for CI
     → Push fix → Re-monitor
     → CI PASSED (Attempt 2/3) ✅

Result: PR #458 created, CI passed with 2 auto-debug fixes applied
Commits: 5 (3 feature + 2 auto-debug fixes)
```

---

## Integration with Existing Commands

| Command | Purpose | Scope | Output |
|---------|---------|-------|--------|
| `/issue` | BDD workflow | Discovery → Implementation | Code commits (no PR) |
| `/work` | **Full automation** | `/issue` + Tests + **`/debug`** + Review + PR + CI | PR created (± CI passed) |
| `/debug` | **Error auto-fix** | Analysis → 2 Solutions → Auto-fix + Tests + GitHub | Issue + PR with fix |
| `/close-issue` | Close existing | Code Review + CI + Merge | Issue closed |

**Recommended Flow:**
1. `/work <issue-id> [--wait]` → Implementa e crea PR (usa `/debug` se errori)
2. Human review su PR (opzionale se --wait usato)
3. `/close-issue <issue-id>` → Merge e chiude

**Error Recovery Flow:**
- `/work` rileva errore → esegue `/debug` automaticamente → fix + tests → continua workflow

---

**Version:** 1.3 (Auto-debug integration)
**Author:** MeepleAI Development Team
**Last Updated:** 2025-10-18

**Key Changes from v1.2:**
- ✅ Added Phase 5.5: Auto-fix local test failures with `/debug`
- ✅ Updated Phase 10: Auto-fix CI failures with `/debug` (replaces manual analysis)
- ✅ `/debug` generates 2 solutions, selects best automatically, implements fix + tests
- ✅ Max 2 iterations for local tests, max 3 for CI
- ✅ Updated Integration table with `/debug` command

**Key Changes from v1.1:**
- ✅ Added Phase 8: Update Issue DoD (automatic checkbox marking)
- ✅ Auto-marks completed acceptance criteria with [x]
- ✅ Documents technical debt with ⚠️ warnings
- ✅ Adds Implementation Status section to issue

**Key Changes from v1.0:**
- ✅ Removed magic_analyze from generic code analysis
- ✅ Removed magic_generate from backend/logic implementation
- ✅ Added Context7 (Upstash) for framework documentation
- ✅ Clarified Magic usage: ONLY for UI component generation
- ✅ Added Context7 usage: Framework docs, best practices, API reference
