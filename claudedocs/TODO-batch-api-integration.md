# TODO: Batch API Integration

**Priority**: High
**Estimated Time**: 20-30 minutes
**Status**: Ready for implementation

## Context

Backend batch API is complete and deployed. Frontend hook exists but components still make individual API calls, causing N+1 problem.

**Current**: 20 games = 20 individual `/api/v1/library/games/{id}/status` calls
**Target**: 20 games = 1 `/api/v1/library/games/batch-status` call

## Implementation Steps

### 1. Update GameGrid Component (5 min)

**File**: `apps/web/src/app/(public)/games/components/GameGrid.tsx`

```typescript
// Add at component top (after games prop)
const gameIds = games.map(g => g.id);
const { data: batchStatuses } = useBatchGameStatus(gameIds);

// Pass to cards (line ~134)
<MeepleGameCatalogCard
  game={game}
  libraryStatus={batchStatuses?.results[game.id]} // NEW PROP
  variant="grid"
  flippable
  onClick={() => handleGameClick(game.id)}
/>
```

### 2. Update MeepleGameCatalogCard (10 min)

**File**: `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx`

**Changes**:
```typescript
// 1. Add prop (line ~40)
export interface MeepleGameCatalogCardProps {
  game: SharedGame | SharedGameDetail;
  variant?: MeepleCardVariant;
  onClick?: (gameId: string) => void;
  flippable?: boolean;
  className?: string;
  libraryStatus?: GameStatusSimple; // NEW - from batch API
}

// 2. Use prop instead of hook (line ~107)
// OLD: const { data: status } = useGameInLibraryStatus(game.id);
// NEW: Use libraryStatus prop if available, fallback to hook
const { data: fetchedStatus } = useGameInLibraryStatus(
  game.id,
  { enabled: !libraryStatus } // Skip if batch provided
);
const status = libraryStatus || fetchedStatus;

// 3. Update logic (line ~108)
const inLibrary = status?.inLibrary || false;
```

### 3. Update GameCollectionGrid (Dashboard) (5 min)

**File**: `apps/web/src/components/dashboard-v2/game-collection-grid.tsx`

```typescript
// Add batch hook
const gameIds = games.map(g => g.id);
const { data: batchStatuses } = useBatchGameStatus(gameIds);

// Update MeepleCard (line ~46)
<MeepleCard
  key={game.id}
  entity="game"
  variant="grid"
  // ... other props ...
  // Add library status from batch
  badge={batchStatuses?.results[game.id]?.inLibrary ? 'In Libreria' : game.badge}
/>
```

### 4. Test & Verify (5 min)

**Steps**:
1. Open http://localhost:3000/games
2. Open DevTools → Network tab
3. Filter by "status"
4. Refresh page
5. **Verify**: See ONLY 1 call to `batch-status`, NOT 20+ individual calls

**Expected**:
- Before: 20 individual status calls (~200ms total)
- After: 1 batch call (~15ms total)
- **Improvement**: ~92% faster

### 5. Deploy to Docker (5 min)

```bash
cd infra
docker compose build web
docker compose up -d web

# Verify
docker logs meepleai-web --tail=20
docker ps --filter name=meepleai-web
```

## Files to Modify

1. `apps/web/src/app/(public)/games/components/GameGrid.tsx` (add batch hook, pass prop)
2. `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx` (accept prop, conditional hook)
3. `apps/web/src/components/dashboard-v2/game-collection-grid.tsx` (add batch hook, use status)

## Testing Checklist

- [ ] Catalog page: Only 1 batch-status call
- [ ] Dashboard: Only 1 batch-status call
- [ ] "Add to Library" button still works
- [ ] "In Libreria" badge shows correctly
- [ ] No console errors
- [ ] Network tab shows batch call with all game IDs
- [ ] Response contains all game statuses

## Success Criteria

✅ **Performance**: <20ms total for library status checks (was 160-240ms)
✅ **API Calls**: 1 call for N games (was N calls)
✅ **Functionality**: All features work (add, badge, status)
✅ **No Regressions**: Existing behavior preserved

## Rollback Plan

If issues occur:
```bash
git revert 36c0821f6  # Revert batch API commit
git push origin main-dev
cd infra && docker compose build web api && docker compose up -d web api
```

## Related Documentation

- Spec: `claudedocs/batch-game-status-api-spec.md`
- Session: `memory/session-2026-02-18-meeplecard-v2.md`
- Commit: 36c0821f6

---

**Created**: 2026-02-18
**Status**: Ready for implementation
**Next Session**: Start here! 🚀
