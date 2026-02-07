# Bounded Context Documentation - Team Instructions

**Issue**: #3794 - Complete Bounded Context API Documentation
**Strategy**: Sequential Approach - Week 1-2 Focus
**Reference**: `authentication-COMPLETE.md` (example implementation)

---

## 🎯 Obiettivo

Documentare **completamente** tutti i commands/queries implementati nei 10 bounded contexts seguendo il template standard.

**Livello Dettaglio**: **COMPLETE**
- Lista completa commands/queries + endpoints
- Request/Response schema con esempi JSON
- Regole validazione e error handling
- Domain events raised
- Integration points
- Usage examples (curl + response)
- Performance characteristics
- Testing strategy

---

## 📋 Team Assignment (Week 1-2)

### Parallelization Strategy (5 Team Members)

| Team Member | Bounded Contexts | File Count | Estimate |
|-------------|------------------|------------|----------|
| **Member A** | Authentication + GameManagement | 122 files | 3-4 days |
| **Member B** | KnowledgeBase + UserLibrary | 211 files | 4-5 days |
| **Member C** | Administration + DocumentProcessing | 280 files | 5-6 days |
| **Member D** | SharedGameCatalog + SystemConfiguration | 234+ files | 5-6 days |
| **Member E** | UserNotifications + WorkflowIntegration | TBD files | 3-4 days |

**Coordination**:
- Daily standup per alignment formatting/stile
- Peer review tra team members prima del commit
- Template adherence verification

---

## 📚 Risorse Disponibili

### 1. Template Standard
**File**: `docs/templates/bounded-context-template.md`

**Sezioni Richieste**:
- Responsabilità (bullet list)
- Domain Model (entities, value objects con code samples)
- Application Layer - Commands (table completa + dettagli)
- Application Layer - Queries (table completa + dettagli)
- Domain Events (table + code samples)
- Integration Points (inbound/outbound dependencies)
- Security & Authorization (auth methods, rate limiting)
- Common Usage Examples (curl + JSON response)
- Performance Characteristics (caching, indexes, targets)
- Testing Strategy (unit, integration, E2E)
- Code Location (directory structure)
- Related Documentation (ADRs, altri contexts)

### 2. Reference Implementation
**File**: `docs/09-bounded-contexts/authentication-COMPLETE.md`

**Cosa guardare**:
- ✅ Formato tabelle commands/queries (column order, info completeness)
- ✅ Dettaglio per command (Purpose, Request/Response schema, Validation, Side effects, Errors, Events)
- ✅ Usage examples (bash curl + JSON response completi)
- ✅ Security section structure (authentication methods, rate limiting, password policy)
- ✅ Performance section (caching strategy, indexes SQL)

**Usa come guida** per:
- Livello di dettaglio appropriato
- Formatting consistency
- Esempio completeness

---

## 🔧 Workflow per Team Member

### Step 1: Setup (15 min)
1. Assign yourself to Issue #3794
2. Create feature branch: `git checkout -b docs/issue-3794-{context-name}`
3. Review template + authentication example

### Step 2: Analysis (2-4 hours per context)
1. List all commands:
   ```bash
   find apps/api/src/Api/BoundedContexts/{Context}/Application/Commands -name "*.cs" | grep -v "Handler\|Validator"
   ```

2. List all queries:
   ```bash
   find apps/api/src/Api/BoundedContexts/{Context}/Application/Queries -name "*.cs" | grep -v "Handler\|Validator"
   ```

3. Find routing file:
   ```bash
   find apps/api/src/Api/Routing -name "*{Context}*Endpoints.cs"
   ```

4. Map commands/queries → endpoints:
   - Read routing file completely
   - Note HTTP method + path for each command/query
   - Identify auth requirements (None, Cookie, API Key, Admin)

### Step 3: Documentation (1-2 days per context)

**Use template sections**:

1. **Copy template** → `{context-name}-COMPLETE.md`

2. **Domain Model**:
   - Read `Domain/Entities/*.cs` files
   - Copy class structure with key properties
   - Include domain methods

3. **Commands Table**:
   - List ALL commands with: name, HTTP method, endpoint, auth, request DTO, response DTO
   - Group by category (e.g., "Session Management", "API Keys", "OAuth")

4. **Commands Detail**:
   - For each command: Purpose, Request schema (JSON), Response schema (JSON), Validation rules, Side effects, Error codes, Domain events
   - Use authentication-COMPLETE.md as formatting example

5. **Queries Table**:
   - Same as commands (name, method, endpoint, auth, query params, response)

6. **Queries Detail**:
   - For each query: Purpose, Query parameters, Response schema, Filters, Sorting options, Caching info

7. **Domain Events**:
   - List events raised by commands
   - Include payload structure + subscribers

8. **Integration Points**:
   - Inbound: Who depends on this context?
   - Outbound: What does this context depend on?
   - Event-driven communication diagram (Mermaid)

9. **Security Section**:
   - Authentication methods used
   - Authorization levels
   - Rate limiting rules
   - Security features specific to context

10. **Usage Examples**:
    - 3-5 common operations
    - Include full curl command + JSON response
    - Show error scenarios

11. **Performance Section**:
    - Caching strategy per query
    - Database indexes (copy from migrations or DB schema)
    - Performance targets (if known)

12. **Testing Strategy**:
    - Reference existing test files
    - List test categories
    - Include example test code

### Step 4: Review & Commit (2-4 hours)

1. **Self-Review Checklist**:
   - [ ] All commands documented (compare count with file list)
   - [ ] All queries documented (compare count with file list)
   - [ ] Request/Response schemas have JSON examples
   - [ ] At least 3 usage examples included
   - [ ] Domain events listed
   - [ ] Integration points clarified
   - [ ] No TODOs or placeholders left
   - [ ] Markdown renders correctly (preview in GitHub/VSCode)

2. **Peer Review**:
   - Tag another team member for review
   - Check consistency with template + authentication example
   - Verify technical accuracy

3. **Commit & Push**:
   ```bash
   git add docs/09-bounded-contexts/{context-name}-COMPLETE.md
   git commit -m "docs(bounded-contexts): complete {Context} API documentation (#3794)"
   git push -u origin docs/issue-3794-{context-name}
   ```

4. **Create PR**:
   ```bash
   gh pr create \
     --title "docs({context}): complete API documentation (#3794)" \
     --body "Part of #3794 - Complete documentation for {Context} bounded context"
   ```

---

## 🎯 Quality Standards

### Completeness Checklist
- ✅ Every command has: Purpose, Schema, Validation, Side effects, Errors, Events
- ✅ Every query has: Purpose, Parameters, Response, Filters, Caching
- ✅ Schemas use realistic JSON examples (not just types)
- ✅ Usage examples show complete curl + response
- ✅ Security section covers all auth methods used
- ✅ Performance section includes caching + indexes

### Consistency Checklist
- ✅ Table formatting matches template
- ✅ Code blocks use correct language hints (csharp, json, bash, sql)
- ✅ Mermaid diagrams render correctly
- ✅ Cross-references use correct relative paths
- ✅ Status section at bottom shows: Status, Last Updated, Metrics

### Accuracy Checklist
- ✅ Command/Query names match code exactly
- ✅ Endpoints match routing file
- ✅ Auth requirements verified from code
- ✅ DTOs match Application/DTOs/*.cs files
- ✅ Domain events match Domain/Events/*.cs files

---

## 🤝 Collaboration

### Daily Sync (15 min)
- Share progress (which commands documented)
- Align on formatting questions
- Identify shared patterns (e.g., pagination, auth)

### Shared Patterns Document
Create `docs/templates/common-patterns.md` for reusable snippets:
- Pagination response format
- Error response format
- Auth header examples
- Rate limiting format

### Questions & Support
- **Template Questions**: Check authentication-COMPLETE.md reference
- **Technical Questions**: Ask context domain expert or check code
- **Formatting Questions**: Ask peer reviewer or use template

---

## 🚀 Success Criteria

**Week 1-2 Goal**: All 10 bounded contexts documented to COMPLETE detail level

**Deliverables**:
- 10 new `{context}-COMPLETE.md` files
- Each file: 200-400 lines (similar to authentication example)
- All commands/queries mapped to endpoints
- Complete usage examples
- Ready for developer onboarding

**Outcome**: Close Issue #3794, unblock Week 3 work (Issue #3795 AgentTypology POC)

---

**Questions?** Refer to authentication-COMPLETE.md or ask in team channel.
