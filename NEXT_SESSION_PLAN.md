# Next Session Plan - MeepleCard Integration Completion

**Context**: Epic #4029 complete, Games catalog integrated. Library, Dashboard, Admin still use legacy cards.

## Priority Queue

### Option A: Complete Library Integration (Recommended)
**Issue**: #4040 (Library domain)
**Effort**: 3-4 days
**Impact**: High - users see quick actions in their personal library

**Challenge**: UserGameCard is complex custom component (~400 lines) with:
- Multiple action callbacks (onConfigureAgent, onUploadPdf, onEditNotes, onRemove, onAskAgent)
- Favorite toggle
- Bulk selection
- Agent/PDF status indicators
- Grid/List view modes

**Strategy**:
1. Create new `MeepleLibraryGameCard` adapter (similar to MeepleGameCatalogCard)
2. Use MeepleCard + useEntityActions as base
3. Add library-specific features (favorite, bulk select, status badges)
4. Integrate custom actions via `moreActions` (not quick actions)
5. Replace UserGameCard usage in LibraryPageClient
6. Test thoroughly (visual regression + user flows)

### Option B: Quick Wins - Dashboard + Admin
**Issue**: #4040 (Dashboard + Admin domains)
**Effort**: 2-3 days
**Impact**: Medium - admin users see quick actions

Simpler than Library because:
- Dashboard/GameCard is smaller (~200 lines)
- Admin GameCard variants are simpler
- Less custom features to preserve

### Option C: Legacy Cleanup
**Issue**: #4041
**Effort**: 4-5 days
**Blocked**: Requires #4040 complete first

Cannot proceed until all pages use MeepleCard.

## Recommended Approach

**Session 1**: Library domain (#4040-B)
- Create MeepleLibraryGameCard adapter
- Integrate with LibraryPageClient
- Preserve all existing features
- Visual regression testing

**Session 2**: Dashboard + Admin (#4040-C + #4040-D)
- Update Dashboard/GameCard
- Update Admin GameCard variants
- Quick integration (simpler than Library)

**Session 3**: Legacy cleanup (#4041)
- Remove 37 legacy files
- Update exports
- Final documentation

**Total**: ~3 sessions to complete integration + cleanup

## Files to Focus On (Next Session)

### Priority 1: Library
- `components/library/UserGameCard.tsx` (~400 lines) - Needs full refactor
- `app/(authenticated)/library/LibraryPageClient.tsx` - Uses UserGameCard
- `components/library/PrivateGameCard.tsx` - If still used
- `components/library/SharedLibraryGameCard.tsx` - If still used

### Priority 2: Dashboard
- `components/dashboard/GameCard.tsx` (~200 lines)
- `components/dashboard/LibrarySnapshot.tsx`
- `components/dashboard/Dashboard.tsx`

### Priority 3: Admin
- `app/(authenticated)/admin/shared-games/client.tsx`
- `app/(authenticated)/admin/shared-games/_components/GameCard.tsx`

## Quick Start Commands (Next Session)

```bash
# Load context
/sc:load
list_memories()
read_memory("epic_4029_final_status")

# Start implementation
/implementa 4040 --focus library

# Or create sub-issue
gh issue create --title "Issue #4040-B: Integrate MeepleCard in Library pages"
/implementa 4040-B
```

## Current State

**Live in Production**:
- ✅ Games catalog: http://localhost:3000/games (quick actions working!)
- ⏳ Library: http://localhost:3000/library (still uses UserGameCard)
- ⏳ Dashboard: http://localhost:3000/dashboard (still uses Dashboard/GameCard)

**System Ready**:
- ✅ MeepleCard components tested and deployed
- ✅ useEntityActions hook available
- ✅ Documentation complete
- ✅ Migration patterns documented

## Success Criteria for Completion

- [ ] Library pages use MeepleCard + quick actions
- [ ] Dashboard uses MeepleCard + quick actions
- [ ] Admin pages use MeepleCard + quick actions
- [ ] All 37 legacy GameCard files removed
- [ ] Zero legacy references in codebase
- [ ] All tests passing
- [ ] Documentation updated

**Current**: 1/4 domains complete (Games ✅)
**Remaining**: 3/4 domains (Library, Dashboard, Admin)

---

**Estimated Total Effort**: 10-15 days
**Estimated Sessions**: 3 sessions @ ~5 days each
**Current Progress**: Epic complete + 25% integration
