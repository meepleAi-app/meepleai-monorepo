# EDIT-05: Quick Reference Guide

**Issue**: #413 | **Effort**: 1 week | **Priority**: Medium

## Quick Command Reference

### Phase 1: Backend (Days 1-2)
```bash
# Step 1: Branch setup
git checkout -b edit-05-enhanced-comments

# Step 2-3: Database schema (use backend-architect agent)
# - Enhance RuleSpecCommentEntity with Serena MCP
# - Run migration
dotnet ef migrations add EnhanceCommentsWithThreading --project src/Api

# Step 4-5: Service + API (use backend-architect agent)
# - Create IRuleCommentService with Serena insert_after_symbol
# - Add endpoints to Program.cs v1Api group
```

### Phase 2: Frontend (Days 2-3)
```bash
# Step 6-7: New components (use frontend-architect + Magic MCP)
# Magic MCP prompts:
# - "mention input with autocomplete, debounced 300ms, keyboard navigation"
# - "inline comment indicator button with count badge, unresolved state"

# Step 8-9: Enhance existing (use frontend-architect + Context7 MCP)
# - CommentItem: threading, resolution, mentions
# - CommentThread: filtering, line numbers, optimistic UI

# Step 10: Types
# - Edit apps/web/src/lib/api.ts (native edit)
```

### Phase 3: Testing (Days 3-4)
```bash
# Step 11-12: Backend tests (use quality-engineer agent)
cd apps/api
dotnet test /p:CollectCoverage=true

# Step 13: Frontend tests (use quality-engineer agent)
cd apps/web
pnpm test:coverage

# Step 14: E2E tests (use quality-engineer + Playwright MCP)
pnpm test:e2e

# Step 15: Verify coverage
pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml
```

### Phase 4: Delivery (Days 5-7)
```bash
# Step 16: Documentation (use technical-writer agent)
# - Output: docs/issue/edit-05-enhanced-comments-implementation.md

# Step 17: PR creation (use /sc:git)
gh pr create --title "EDIT-05: Enhanced comments with threading" \
  --body "Closes #413. All acceptance criteria satisfied."

# Step 18: Code review (use security-engineer + quality-engineer)
# Manual review process

# Step 19: Merge
gh pr merge --squash --delete-branch

# Step 20: Update local tracking
# Edit docs/LISTA_ISSUE.md → mark EDIT-05 as ✅ COMPLETED

# Step 21: Close GitHub issue
gh issue edit 413 --add-label "status:completed"
gh issue close 413 --comment "✅ Complete in PR #XXX"

# Step 22: Knowledge capture (use pm-agent)
# Serena write_memory("edit-05-learnings", content)
```

## Agent Quick Lookup

| Task | Best Agent | Why |
|------|------------|-----|
| Database schema | backend-architect | Entity design patterns |
| Service layer | backend-architect + Sequential | Complex logic reasoning |
| API endpoints | backend-architect | API design patterns |
| React components | frontend-architect + Magic | UI generation + patterns |
| Component enhancement | frontend-architect + Context7 | Official React patterns |
| Unit tests | quality-engineer | Edge case detection |
| E2E tests | quality-engineer + Playwright | Browser automation |
| Documentation | technical-writer | Clear technical docs |
| Code review | security-engineer + quality-engineer | Dual review |
| Issue closure | pm-agent | Knowledge capture |

## MCP Tool Quick Lookup

| Operation | MCP Tool | Command |
|-----------|----------|---------|
| Find entity | Serena | `find_symbol("RuleSpecCommentEntity")` |
| Replace entity | Serena | `replace_symbol_body(name, body)` |
| Add service method | Serena | `insert_after_symbol(name, body)` |
| Generate UI component | Magic | `21st_magic_component_builder(prompt)` |
| React patterns | Context7 | Auto-activated for React |
| Complex reasoning | Sequential | Multi-step analysis |
| Browser automation | Playwright | `browser_click`, `browser_type` |
| Save learnings | Serena | `write_memory(name, content)` |

## Success Checklist

### Pre-Merge
- [ ] Backend coverage ≥ 90%
- [ ] Frontend coverage ≥ 90%
- [ ] E2E tests passing
- [ ] Security review approved
- [ ] Quality review approved
- [ ] CI/CD green
- [ ] Documentation complete

### Post-Merge
- [ ] PR merged to main
- [ ] Branch deleted
- [ ] LISTA_ISSUE.md updated (✅ COMPLETED)
- [ ] GitHub issue #413 closed
- [ ] Knowledge captured in Serena memory

## Troubleshooting

### Coverage Below 90%
→ Use quality-engineer agent to identify gaps and add tests

### Security Concerns
→ Use security-engineer agent for SQL injection, XSS analysis

### Performance Issues
→ Check indexes: `(RuleSpecId, LineNumber)`, `(ParentCommentId)`

### Merge Conflicts
→ Coordinate with EDIT-03, EDIT-04 (editor UI changes)

---

**Full Implementation Strategy**: `edit-05-implementation-strategy.md`
**GitHub Issue**: #413
**Related**: EDIT-02 (#278), EPIC-04
