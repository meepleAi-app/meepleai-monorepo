# Plan: Play Sessions Epic #3887

## Hypothesis

**What**: Implement comprehensive play session tracking system for MeepleAI
**Why**: Enable users to record game sessions, track statistics, and build social features

**Approach**: Systematic 5-phase implementation following DDD + CQRS patterns
- Phase 1: Domain model foundation (PlaySession aggregate)
- Phase 2: Write operations (Commands + Validators)
- Phase 3: Read operations (Queries + Statistics)
- Phase 4: Security layer (Permissions + Authorization)
- Phase 5: User interface (Frontend + Dashboard)

## Expected Outcomes (Quantitative)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Backend Test Coverage** | ≥90% | dotnet test /p:CollectCoverage=true |
| **Frontend Test Coverage** | ≥85% | pnpm test:coverage |
| **Implementation Time** | 6 days | Actual vs estimated per issue |
| **Query Performance** | <500ms (p95) | Integration test benchmarks |
| **Zero Security Issues** | 100% | Security audit + penetration tests |

## Architecture Decisions

### 1. Hybrid Player Model (ADR-001)
**Decision**: SessionPlayer entity with optional User FK
**Rationale**:
- Supports registered users (UserId not null)
- Supports external guests (UserId null, DisplayName only)
- Flexibility for social gaming scenarios

**Alternatives Considered**:
- Player as separate aggregate → Too complex for MVP
- Player as value object → Insufficient identity for scoring

### 2. Multi-Dimensional Scoring (ADR-002)
**Decision**: SessionScore value object with dimension/value/unit
**Rationale**:
- Game-specific scoring (from Game.ScoringMetadata if available)
- Session-configurable scoring (for non-catalog games)
- Extensible for future scoring schemes

**Examples**:
- Board games: points, ranking, wins
- Card games: chips, hands won, total value
- Cooperative: team score, mission success

### 3. Optional Game Association (ADR-003)
**Decision**: Nullable GameId FK with required GameName
**Rationale**:
- Users can track sessions for games not in catalog
- Maintains data quality when catalog game exists
- Foreign key with ON DELETE SET NULL for catalog cleanup

### 4. Permission Model (ADR-004)
**Decision**: Creator-centric with group sharing
**Rationale**:
- Private by default (user privacy)
- Group visibility for family/friend stats
- Players auto-granted view permission

**Pattern**: Follows PrivateGames approach (#3570-#3580)

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Multi-dimensional scoring complexity** | High | Start with 3 core dimensions (points, ranking, wins), extensible design |
| **Guest player data quality** | Medium | Require DisplayName validation, optional email for linking |
| **Statistics query performance** | Medium | Add indexes, use projection, consider caching |
| **Group feature not implemented** | High | Verify Group entity exists, implement if needed |
| **Frontend state management complexity** | Medium | Use Zustand + React Query, optimistic updates |

## Dependencies & Verification

### External Dependencies (Verify Before Start)
- ✅ Game entity exists (`GameManagement/Domain/Game.cs`)
- ✅ User entity exists (`Administration/Domain/User.cs`)
- ❓ Group entity exists (VERIFY: needed for group sessions)
- ✅ CQRS infrastructure (MediatR, FluentValidation)
- ✅ Test infrastructure (Testcontainers, xUnit)

### Technical Patterns to Follow
- Domain model: `Game.cs`, `PrivateGame.cs` as references
- CQRS: `CreateGameCommand`, `GetGameQuery` as templates
- Permissions: `PrivateGamePermissionChecker` as pattern
- Frontend: `MeepleCard` component for session display

## Branch Strategy

```yaml
Parent Branch: backend-dev
Feature Branches:
  - feature/issue-3888-play-session-domain
  - feature/issue-3889-play-session-commands
  - feature/issue-3890-play-session-queries
  - feature/issue-3891-play-session-permissions
  - feature/issue-3892-play-session-ui (from frontend-dev)

Merge Strategy:
  - Each feature → backend-dev (after review)
  - #3892 → frontend-dev (frontend work)
  - backend-dev → main-dev (after epic complete)
  - frontend-dev → main-dev (after epic complete)
```

## Quality Gates (Pre-Implementation)

**Before Starting Each Issue**:
1. ✅ Read CLAUDE.md and project patterns
2. ✅ Check existing implementations for similar features
3. ✅ Verify dependencies are complete
4. ✅ Create PDCA/do.md for trial-and-error logging

**During Implementation**:
1. ✅ Update PDCA/do.md with errors, solutions, learnings
2. ✅ Checkpoint every 30 minutes (write_memory)
3. ✅ Run code-reviewer agent before PR
4. ✅ Run tests continuously (unit → integration → E2E)

**After Each Issue**:
1. ✅ Create PDCA/check.md with metrics
2. ✅ Document patterns in docs/patterns/ if reusable
3. ✅ Update MEMORY.md with key learnings
4. ✅ Clean up temporary files and branches

## Next Actions

**Immediate (Phase 1 - Issue #3888)**:
1. Verify Group entity exists in codebase
2. Create feature branch from backend-dev
3. Activate backend-architect agent
4. Load context7 MCP for DDD patterns
5. Begin domain model implementation

**Validation Checkpoint**:
- Check if Group aggregate exists before proceeding
- Verify Game entity structure for scoring metadata
- Review PrivateGame implementation as reference pattern

---

**Created**: 2026-02-08
**PM Agent**: Orchestration ready
**Status**: ✅ Plan Complete - Ready for execution
