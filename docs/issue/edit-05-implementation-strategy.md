# EDIT-05: Enhanced Comments System - Implementation Strategy

**Issue**: #413
**Epic**: EPIC-04 (Editor & Versioning)
**Priority**: Medium
**Effort**: M (1 week)
**Status**: Planning

## Executive Summary

Implementation of inline annotations, comment threading, user mentions, and resolution tracking for the MeepleAI editor. This document outlines the optimal agent, command, and skill selection for each implementation phase.

---

## Implementation Roadmap

### Phase 1: Backend Architecture (Steps 1-5)

#### Step 1: Branch Setup
- **Tool**: Native git commands
- **Command**: `git checkout -b edit-05-enhanced-comments`
- **Rationale**: Standard git workflow, no specialized agent needed

#### Step 2: Database Schema Enhancement
- **Primary Agent**: `backend-architect`
- **MCP Tools**: Serena (`find_symbol`, `replace_symbol_body`)
- **Sequential Support**: Complex schema design reasoning
- **Task**: Enhance `RuleSpecCommentEntity` with:
  - `LineNumber` (nullable int) - Inline annotation support
  - `ParentCommentId` (nullable Guid) - Threading support
  - `IsResolved`, `ResolvedByUserId`, `ResolvedAt` - Resolution tracking
  - `MentionedUserIds` (List<Guid>) - User mentions
- **Serena Commands**:
  ```
  find_symbol("RuleSpecCommentEntity", include_body=true)
  replace_symbol_body("RuleSpecCommentEntity", enhanced_schema)
  ```
- **Why backend-architect**: Expertise in database design patterns, entity relationships, and indexing strategies

#### Step 3: EF Core Migration
- **Tool**: Native .NET CLI
- **Command**: `dotnet ef migrations add EnhanceCommentsWithThreading --project src/Api`
- **Follow-up**: Review migration SQL, add indexes for performance:
  - `(RuleSpecId, LineNumber)` - Fast line lookup
  - `(ParentCommentId)` - Thread traversal
  - `(IsResolved)` - Filtering
  - `(UserId)` - User's comments

#### Step 4: Service Layer Implementation
- **Primary Agent**: `backend-architect`
- **MCP Tools**:
  - Serena `insert_after_symbol` for service methods
  - Sequential MCP for complex service logic patterns
- **Task**: Create `IRuleCommentService` and `RuleCommentService` with:
  - `CreateCommentAsync()` - With mention extraction
  - `ReplyToCommentAsync()` - Threading support
  - `GetCommentsForLineAsync()` - Inline annotations
  - `ResolveCommentAsync()` / `UnresolveCommentAsync()`
  - `ExtractMentionedUsersAsync()` - @username parsing
- **Why Sequential MCP**: Complex mention extraction regex patterns, parent-child relationship logic

#### Step 5: API Endpoints
- **Primary Agent**: `backend-architect`
- **MCP Tools**: Serena `insert_after_symbol` in `Program.cs`
- **Task**: Add versioned endpoints to v1Api group:
  - `POST /api/v1/rulespecs/{id}/comments` - Create comment
  - `POST /api/v1/comments/{id}/replies` - Reply to comment
  - `GET /api/v1/rulespecs/{id}/lines/{line}/comments` - Get line comments
  - `POST /api/v1/comments/{id}/resolve` - Mark resolved
  - `POST /api/v1/comments/{id}/unresolve` - Reopen
  - `GET /api/v1/users/search?query={q}` - User autocomplete
- **Why backend-architect**: API design patterns, authorization rules, response shaping

---

### Phase 2: Frontend Development (Steps 6-9)

#### Step 6: MentionInput Component
- **Primary Agent**: `frontend-architect`
- **MCP Tools**: Magic MCP (`21st_magic_component_builder`)
- **Context7 Support**: React hooks patterns for autocomplete
- **Task**: Create `MentionInput.tsx` with:
  - Textarea with @mention detection
  - Autocomplete dropdown (debounced 300ms)
  - User search API integration
  - Keyboard navigation (arrow keys, Enter to select)
- **Magic MCP Prompt**: "Create React mention input component with autocomplete, debounced search, keyboard navigation, accessible dropdown"
- **Why Magic MCP**: UI component generation with design system integration, accessibility built-in

#### Step 7: InlineCommentIndicator Component
- **Primary Agent**: `frontend-architect`
- **MCP Tools**: Magic MCP (`21st_magic_component_builder`)
- **Task**: Create `InlineCommentIndicator.tsx`:
  - Comment bubble icon with count badge
  - Visual distinction for unresolved comments (red dot)
  - Hover tooltip showing comment preview
  - Click handler to open thread sidebar
- **Magic MCP Prompt**: "Create inline comment indicator button with bubble icon, count badge, unresolved state visual, hover preview"
- **Why Magic MCP**: Icon-based UI components with state variations

#### Step 8: Enhance CommentItem
- **Primary Agent**: `frontend-architect`
- **MCP Tools**: Context7 MCP (React patterns)
- **Task**: Enhance existing `CommentItem.tsx`:
  - Display nested replies (visual indentation)
  - Reply button and reply form
  - Resolve/unresolve toggle
  - Render @mentions as clickable links
  - Visual distinction for resolved comments (opacity, strikethrough)
- **Context7 Support**: React component composition patterns, conditional rendering
- **Why Context7**: Official React patterns for component enhancement vs full rebuild

#### Step 9: Enhance CommentThread
- **Primary Agent**: `frontend-architect`
- **MCP Tools**: Context7 MCP (React hooks, state management)
- **Task**: Enhance `CommentThread.tsx`:
  - Line number filtering (show comments for specific line)
  - Show/hide resolved comments toggle
  - Thread hierarchy rendering (parent → children)
  - Reply state management
  - Optimistic UI updates
- **Context7 Support**: `useCallback`, `useMemo` optimization patterns
- **Why Context7**: Complex state management patterns, performance optimization

#### Step 10: TypeScript API Types
- **Tool**: Native Edit/Write
- **Task**: Update `apps/web/src/lib/api.ts`:
  - Extend `RuleSpecComment` interface with new fields
  - Add `CreateReplyRequest`, `ResolveCommentRequest` types
  - Add `UserSearchResult` type
- **Rationale**: Simple type additions, no agent needed

---

### Phase 3: Quality Assurance (Steps 11-15)

#### Step 11: Backend Unit Tests
- **Primary Agent**: `quality-engineer`
- **MCP Tools**: Sequential MCP for test strategy
- **Command**: `/sc:test --focus backend --coverage`
- **Task**: Create `RuleCommentServiceTests.cs`:
  - Comment creation with mention extraction
  - Reply threading (parent-child relationships)
  - Line number filtering
  - Resolution state changes
  - Edge cases: null line numbers, invalid parent IDs
- **Coverage Target**: 90%+ (method, branch, line)
- **Why quality-engineer**: Systematic edge case detection, coverage analysis

#### Step 12: Backend Integration Tests
- **Primary Agent**: `quality-engineer`
- **Tools**: Testcontainers (Postgres, Qdrant)
- **Task**: Create `RuleCommentEndpointsTests.cs`:
  - End-to-end comment creation flow
  - Threading: create → reply → reply chain
  - Resolution workflow
  - User search with partial matches
  - Authorization checks (Editor/Admin only)
- **Why quality-engineer**: Integration test patterns, auth testing

#### Step 13: Frontend Unit Tests
- **Primary Agent**: `quality-engineer`
- **Tools**: Jest + React Testing Library
- **Task**: Test files for all new/enhanced components:
  - `MentionInput.test.tsx` - Autocomplete behavior, debouncing
  - `InlineCommentIndicator.test.tsx` - Visual states, click handlers
  - `CommentItem.test.tsx` - Enhanced threading, resolution display
  - `CommentThread.test.tsx` - Filtering, threading, optimistic updates
- **Coverage Target**: 90%+
- **Why quality-engineer**: React testing best practices, user interaction testing

#### Step 14: E2E Tests
- **Primary Agent**: `quality-engineer`
- **MCP Tools**: Playwright MCP
- **Playwright Commands**:
  - `browser_navigate` to editor page
  - `browser_snapshot` to analyze comment UI
  - `browser_click` for interactions
  - `browser_type` for mention input
- **Task**: Create `e2e/comments-enhanced.spec.ts`:
  - Add inline comment on line 42
  - Reply to comment (thread depth 3)
  - Mention user with autocomplete
  - Resolve comment thread
  - Filter resolved/unresolved
- **Why Playwright MCP**: Real browser automation, accessibility testing

#### Step 15: Coverage Verification
- **Primary Agent**: `quality-engineer`
- **Commands**:
  - Backend: `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover`
  - Frontend: `pnpm test:coverage`
  - Report: `pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml`
- **Quality Gates**: Both backend and frontend ≥ 90%
- **Why quality-engineer**: Coverage analysis, gap identification

---

### Phase 4: Documentation & Delivery (Steps 16-22)

#### Step 16: Implementation Documentation
- **Primary Agent**: `technical-writer`
- **Output**: `docs/issue/edit-05-enhanced-comments-implementation.md`
- **Content Structure**:
  - Architecture overview (database, service, API, UI)
  - API endpoint documentation (request/response examples)
  - Component usage guide
  - Testing strategy and coverage report
  - Migration guide (database changes)
  - Performance considerations (indexing, debouncing)
- **Why technical-writer**: Clear, comprehensive technical documentation

#### Step 17: Pull Request Creation
- **Agent**: Native + `/sc:git`
- **Command**: `/sc:git` for intelligent commit messages
- **PR Creation**: `gh pr create --title "EDIT-05: Enhanced comments with threading and inline annotations" --body "$(cat PR_TEMPLATE)"`
- **PR Template Content**:
  - Summary of changes (database, service, API, UI, tests)
  - Breaking changes: None (backward compatible)
  - Migration required: Yes (EF Core migration)
  - Test coverage: 90%+ backend and frontend
  - Screenshots: Comment threading UI, inline annotations
  - Related issues: Closes #413
- **Why /sc:git**: Intelligent commit message generation, follows conventional commits

#### Step 18: Code Review
- **Primary Agents**: `security-engineer` + `quality-engineer`
- **Security Review Focus**:
  - SQL injection risk in mention extraction regex
  - XSS prevention in comment text rendering
  - Authorization checks on all endpoints
  - User input validation (comment text length, mention format)
- **Quality Review Focus**:
  - Test coverage ≥ 90%
  - Edge case handling (orphaned comments, circular threads)
  - Performance (N+1 query prevention, indexing)
  - Code style compliance (C# conventions, React patterns)
- **Approval Criteria**: Both security and quality sign-off required
- **Why security-engineer + quality-engineer**: Dual review for security and quality standards

#### Step 19: PR Merge
- **Tool**: Native GitHub CLI
- **Command**: `gh pr merge --squash --delete-branch` (after approval)
- **Pre-merge Checks**:
  - CI/CD green (all tests passing)
  - Code review approved (2+ reviewers)
  - No merge conflicts
  - Branch up-to-date with main
- **Post-merge**: Automatic branch deletion

#### Step 20: Local Issue Tracking Update
- **Tool**: Native Edit
- **File**: `docs/LISTA_ISSUE.md`
- **Change**:
  ```markdown
  | 26 | #413 EDIT-05: Enhanced comments | ✅ COMPLETED | M | Inline annotations, threading, mentions, resolution. PR #XXX. |
  ```
- **Why manual edit**: Local documentation update, simple markdown change

#### Step 21: GitHub Issue Update
- **Tool**: GitHub CLI
- **Commands**:
  ```bash
  gh issue edit 413 --add-label "status:completed"
  gh issue close 413 --comment "✅ Implementation complete in PR #XXX. All acceptance criteria satisfied. Test coverage: Backend 92%, Frontend 94%."
  ```
- **Verification**: Check issue status on GitHub web UI

#### Step 22: Knowledge Capture
- **Primary Agent**: `pm-agent`
- **MCP Tool**: Serena `write_memory`
- **Task**: Document implementation learnings for future reference:
  - **Patterns Used**: EF Core self-referencing relationships, React mention autocomplete
  - **Challenges**: Thread depth performance, mention extraction edge cases
  - **Solutions**: Indexed parent_comment_id, debounced autocomplete
  - **Reusable Code**: MentionInput component (can be extracted to shared lib)
  - **Future Improvements**: Real-time collaboration (WebSocket notifications)
- **Serena Command**: `write_memory("edit-05-enhanced-comments-learnings", markdown_content)`
- **Why pm-agent**: Post-implementation knowledge capture, pattern documentation

---

## Parallelization Opportunities

### Backend + Frontend (Steps 2-5 || 6-9)
- Backend team can implement database + service + API
- Frontend team can start component design with mock API responses
- **Coordination Point**: API contract agreement (request/response types)

### Testing Phases (Steps 11-14)
- Backend unit tests (Step 11) || Frontend unit tests (Step 13)
- Backend integration tests (Step 12) independent of E2E tests (Step 14)
- **Merge Point**: Step 15 (coverage verification waits for all tests)

---

## Risk Mitigation

### Technical Risks
- **Thread Depth Performance**: Limit thread depth to 5 levels, add indexes on parent_comment_id
- **Mention Extraction Accuracy**: Comprehensive regex testing, handle edge cases (@username.with.dots)
- **N+1 Query Problem**: Use `.Include()` for eager loading parent/children comments
- **XSS in Comments**: Sanitize HTML in comment text, render as plain text or markdown

### Process Risks
- **Coverage Below 90%**: quality-engineer agent blocks merge if coverage threshold not met
- **Security Vulnerabilities**: security-engineer agent mandatory review before merge
- **Breaking Changes**: Ensure backward compatibility (existing comments continue working)

---

## Success Metrics

### Code Quality
- ✅ Backend test coverage ≥ 90%
- ✅ Frontend test coverage ≥ 90%
- ✅ Zero critical security vulnerabilities (CodeQL, security-engineer review)
- ✅ CI/CD pipeline green (all tests passing)

### Feature Completeness
- ✅ All 24 acceptance criteria from issue #413 satisfied
- ✅ UI/UX review approved (accessibility, responsive design)
- ✅ Performance benchmarks met (comment thread loads < 200ms for 50+ comments)

### Documentation
- ✅ Implementation guide complete (architecture, API, usage)
- ✅ API documentation updated (Swagger/OpenAPI)
- ✅ Knowledge captured in Serena memory for future reference

### Issue Closure
- ✅ PR merged to main
- ✅ GitHub issue #413 closed with completion notes
- ✅ LISTA_ISSUE.md updated with ✅ COMPLETED status
- ✅ pm-agent knowledge capture complete

---

## Agent Summary Table

| Phase | Steps | Primary Agent | Secondary Tools | Rationale |
|-------|-------|---------------|-----------------|-----------|
| Backend Architecture | 1-5 | backend-architect | Serena, Sequential | Database design, service patterns, API architecture |
| Frontend Development | 6-9 | frontend-architect | Magic, Context7 | React components, hooks, state management |
| Quality Assurance | 11-15 | quality-engineer | Playwright, Sequential | Test strategy, coverage, edge cases |
| Documentation | 16 | technical-writer | Native Write | Clear technical documentation |
| Code Review | 18 | security-engineer + quality-engineer | Native | Security + quality dual review |
| Issue Closure | 19-22 | pm-agent | GitHub CLI, Serena | PR merge, tracking update, knowledge capture |

---

## Estimated Timeline (1 Developer)

- **Day 1**: Backend architecture (Steps 1-5) - 8 hours
- **Day 2**: Frontend components (Steps 6-9) - 8 hours
- **Day 3**: Backend testing (Steps 11-12) - 6 hours
- **Day 4**: Frontend testing + E2E (Steps 13-14) - 6 hours
- **Day 5**: Coverage verification, documentation, PR creation (Steps 15-17) - 4 hours
- **Day 6-7**: Code review, merge, issue closure (Steps 18-22) - 2 hours

**Total**: 5-7 working days (1 week)

---

## Related Documentation

- **Issue**: #413 EDIT-05: Enhanced comments and annotations
- **Epic**: EPIC-04 (Editor & Versioning)
- **Dependencies**: EDIT-02 (#278) - Basic comments system
- **Architecture Docs**: `docs/database-schema.md`, `docs/technic/backend-api.md`
- **Testing Guides**: `docs/code-coverage.md`, `docs/technic/test-02-*.md`

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
