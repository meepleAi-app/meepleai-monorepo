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

- Test suite completa, coverage validation, BLOCCO se RED

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
**Agents:** `data-analyst-deep-think`, `deep-think-developer`
**MCP:** `github_*` (CI status), `sequential_start/step` (plan fix)

- Monitorare CI, analizzare failure, pianificare fix, re-push (max 3 tentativi)

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
**Skills:** testing (for test patterns)

```bash
# BEFORE writing tests, get patterns from skill
Skill("testing")  # → Get unit test structure and best practices

# Then write context-specific tests
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
**Skills:** webapp-testing (for E2E tests, frontend only)

```bash
dotnet test --verbosity normal  # Backend
pnpm test                        # Frontend unit tests

# If frontend feature AND DoD requires E2E
Skill("webapp-testing")  # → Get Playwright E2E patterns
pnpm test:e2e           # Run E2E tests

# BLOCCO se RED ❌
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
**Skills:** development (for docs structure, if DoD requires docs update)

```bash
# If DoD requires documentation update
Skill("development")  # → Get documentation best practices

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
**Agents:** data-analyst-deep-think, deep-think-developer
**MCP:** github_*, sequential_*

**If FAILURE:**
1. Analyze logs (data-analyst-deep-think)
2. Plan fix (strategic-advisor + sequential)
3. Implement fix (deep-think-developer)
4. Re-push and re-monitor
5. Max 3 attempts

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

## Skill Selection Logic (Automatic)

Skills complement Agents and MCP tools by providing battle-tested patterns, boilerplate code, and best practices. Use skills automatically based on task context.

### Testing Phase (Phase 3 & 5)

**Trigger**: Durante test generation o test execution
**Skill**: `testing`

**Backend C# (xUnit):**
```bash
# Get xUnit + Testcontainers patterns
Skill("testing")
# → Returns: pytest patterns (adapt to xUnit)
# → Use: Arrange-Act-Assert structure, test naming conventions
```

**Frontend TypeScript (Jest/Vitest):**
```bash
# Get Jest/Vitest patterns
Skill("testing")
# → Returns: Unit test structure, mocking patterns
# → Use: describe/it blocks, expect matchers, component testing
```

**E2E Testing (Frontend only):**
```bash
# Get Playwright E2E patterns
Skill("webapp-testing")
# → Returns: Page object model, selectors, assertions
# → Use: User workflows, accessibility testing, visual regression
```

**When to Use**:
- ✅ Need test structure/patterns (ALWAYS use skill first)
- ✅ Need mocking/stubbing examples
- ✅ Need E2E test scaffolding (frontend)
- ❌ Business logic assertions (Agent handles context-specific asserts)

### Code Generation (Phase 4)

**Trigger**: Durante implementation per boilerplate code
**Skill**: `development`

**Use Cases**:
```bash
# CLI application structure
Skill("development")  # Section: Creating a CLI application

# Project setup (dependencies, virtual env)
Skill("development")  # Section: Setting up a project

# Git workflow patterns
Skill("development")  # Section: Version control
```

**When to Use**:
- ✅ Boilerplate code (CLI, API client, utilities)
- ✅ Project scaffolding (dependencies, configuration)
- ✅ Generic patterns (error handling, logging)
- ❌ Business logic specifica (Agent scrive codice specifico del dominio)
- ❌ Complex architecture decisions (Agent + strategic-advisor)

### Documentation Update (Phase 8)

**Trigger**: DoD richiede docs update
**Skill**: `development`

```bash
# Get documentation best practices
Skill("development")  # Section: Documentation
# → Returns: README structure, docstring conventions, comment style
# → Use: Populate template with feature-specific content
```

**When to Use**:
- ✅ README updates (structure, formatting)
- ✅ API documentation (docstring conventions)
- ✅ Troubleshooting guides (template structure)
- ❌ Technical writing content (Agent writes domain-specific docs)

### Skill Priority Rules

**Decision Flow**:
```
1. Is task about TEST patterns/structure? → Skill("testing" or "webapp-testing")
2. Is task about BOILERPLATE code? → Skill("development")
3. Is task about DOCUMENTATION structure? → Skill("development")
4. Otherwise → Use Agent (deep-think-developer, typescript-expert-developer)
```

**Skill vs Agent**:
- **Skill**: Generic patterns, best practices, boilerplate (framework-agnostic)
- **Agent**: Domain logic, business rules, context-specific code (project-aware)

**Skill vs MCP**:
- **Skill**: Code patterns, project setup, testing structure
- **MCP**: External services (Context7 docs, Magic UI gen, Sequential reasoning, GitHub ops)

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

### Success (with --wait + auto-fix)
```
✅ WORKFLOW COMPLETE (with auto-fix): <issue-id>

🔧 Auto-Fix Applied:
- Attempt 1: [issue] → Fixed with [solution]
- Attempt 2: CI passed

📊 Summary:
- Commits: 4 (3 feature + 1 CI fix)
- Agents: [..., data-analyst-deep-think (CI analysis)]
- CI: ✅ Passed on attempt 2/3
- Duration: Xm Ys (including fix)
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

### Example 3: Full-Stack Feature
```bash
/work CHAT-01 --wait  # "Streaming responses (API + UI)"

Agents: Explore, doc-researcher-optimizer, strategic-advisor, system-architect,
        deep-think-developer, typescript-expert-developer, data-analyst-deep-think

MCP Used:
  - Context7: ASP.NET Core SSE + React EventSource docs
  - sequential: Planning architecture
  - magic_generate: Generate streaming UI components (progress, stop button)
  - track_progress: Save milestones
  - github_*: PR creation, CI monitoring
MCP NOT Used:
  - magic for backend C# (deep-think writes)
  - magic for React hooks (typescript-expert writes)

Auto-Fix: Test timeout → Conditional CI timeout
Result: PR #458 created, CI passed on attempt 2/3
```

### Example 4: Frontend Feature with E2E Testing and Skill Usage
```bash
/work UI-166 --wait  # "Create influencer management UI"

Agents: Explore, doc-researcher-optimizer, typescript-expert-developer

Skills Used:
  - testing: Get unit test structure patterns (Phase 3)
    → Provided: Jest describe/it blocks, expect matchers, component testing patterns
    → Applied: 15 unit tests for influencer CRUD operations

  - webapp-testing: Get Playwright E2E patterns (Phase 5)
    → Provided: Page object model, user workflow patterns, accessibility testing
    → Applied: 3 E2E tests (create, edit, delete influencer flows)

  - development: Documentation best practices (Phase 8)
    → Provided: README structure, API docs conventions
    → Applied: Updated docs/guides/influencers.md with new UI features

MCP Used:
  - Context7: Next.js 14 docs, TanStack Query patterns
  - magic_generate: Form components (InfluencerForm, PersonaEditor)
  - github_*: PR creation, CI monitoring

Workflow:
  Phase 3: Skill("testing") → Write 15 unit tests (RED)
  Phase 4: magic_generate → UI components + typescript-expert → hooks/logic
  Phase 5: Skill("webapp-testing") → Write 3 E2E tests → All green ✅
  Phase 8: Skill("development") → Update docs structure

Result:
  - PR #XXX created
  - 15 unit tests + 3 E2E tests (100% coverage on UI)
  - Documentation updated (influencers.md)
  - CI passed on first attempt
  - DoD: 6/6 complete (including E2E requirement)
```

**Key Takeaway**: Skills provided **generic patterns** (test structure, E2E flows, docs format), while Agents/MCP handled **domain-specific** implementation (influencer business logic, specific UI components).

---

## Integration with Existing Commands

| Command | Purpose | Scope | Output |
|---------|---------|-------|--------|
| `/issue` | BDD workflow | Discovery → Implementation | Code commits (no PR) |
| `/work` | **Full automation** | `/issue` + Tests + Review + PR + CI | PR created (± CI passed) |
| `/close-issue` | Close existing | Code Review + CI + Merge | Issue closed |

**Recommended Flow:**
1. `/work <issue-id> [--wait]` → Implementa e crea PR
2. Human review su PR (opzionale se --wait usato)
3. `/close-issue <issue-id>` → Merge e chiude

---

**Version:** 1.3 (Skill Integration)
**Author:** MeepleAI Development Team
**Last Updated:** 2025-10-19

**Key Changes from v1.2:**
- ✅ Added comprehensive "Skill Selection Logic" section
- ✅ Integrated `testing` skill in Phase 3 for test patterns
- ✅ Integrated `webapp-testing` skill in Phase 5 for E2E tests
- ✅ Integrated `development` skill in Phase 8 for documentation
- ✅ Added Example 4 demonstrating full skill usage workflow
- ✅ Clear decision flow: Skill (patterns) → Agent (implementation)

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
