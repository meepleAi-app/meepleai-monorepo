# Hide BGG mentions from user-facing UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every BGG/BoardGameGeek mention from the non-admin UI while leaving the admin proxy/wizard and `Game.bggId` DTO field intact.

**Architecture:** Pure UI hide — 3 surgical edits + 1 orphan-component deletion + 1 barrel-export cleanup. Backend untouched. `bggId` stays in the DTO; only the UI rendering paths that print it are removed.

**Tech Stack:** Next.js 16 / React 19 / TypeScript / Vitest. No backend changes.

**Spec:** `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-design.md`

---

## File Structure (recap)

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx` | Drop the `ID: ${bggId}` subtitle path |
| Modify | `apps/web/src/components/shared-games/SharedGameDetailModal.tsx` | Drop the "View on BGG" link block |
| Modify | `apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx` | Flip the BGG-link test from positive to negative assertion |
| Modify | `apps/web/src/app/(authenticated)/settings/services/page.tsx` | Drop the `id: 'bgg'` entry from the services array |
| Delete | `apps/web/src/components/games/BggSearchPanel.tsx` | Orphan duplicate of the admin version |
| Delete | `apps/web/src/__tests__/components/games/BggSearchPanel.test.tsx` | Test of the orphan |
| Modify | `apps/web/src/components/games/index.ts` | Drop the `BggSearchPanel` barrel re-export |

---

### Task 1: Remove BGG ID subtitle from catalog card

**Files:**
- Modify: `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx:194-195`

- [ ] **Step 1: Locate the offending block**

Run: `grep -n "bggId" apps/web/src/components/catalog/MeepleGameCatalogCard.tsx`
Expected: line 194 `if (game.bggId) {` and line 195 `subtitleParts.push(\`ID: ${game.bggId}\`);`

- [ ] **Step 2: Remove the block via Edit tool**

Replace:

```typescript
  if (game.bggId) {
    subtitleParts.push(`ID: ${game.bggId}`);
  }
```

with: (empty — delete the 3-line block; leave the surrounding `subtitleParts` accumulation intact)

- [ ] **Step 3: Verify no leftover bggId reference in this file**

Run: `grep -n "bggId\|BGG" apps/web/src/components/catalog/MeepleGameCatalogCard.tsx`
Expected: no output (file is clean)

- [ ] **Step 4: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/catalog/MeepleGameCatalogCard.tsx
git commit -m "fix(catalog): hide BGG ID subtitle from catalog card

Part of hide-bgg-user-facing (spec 2026-05-22). BGG ID is a leaked
internal identifier — admin wizard still uses bggId via DTO but UI
no longer surfaces it.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 2: Remove "View on BGG" link from shared-game detail modal

**Files:**
- Modify: `apps/web/src/components/shared-games/SharedGameDetailModal.tsx:~345`

- [ ] **Step 1: Read the BGG-link block to capture exact indentation**

Run: `grep -n "boardgamegeek\|bggId" apps/web/src/components/shared-games/SharedGameDetailModal.tsx`
Expected: ~lines 345-348 containing `{game.bggId && (` + an `<a href="https://boardgamegeek.com/boardgame/${game.bggId}">` block

- [ ] **Step 2: Read the surrounding context (3 lines before + 3 after)**

Use Read tool with `offset: 340, limit: 15` to see exact whitespace before/after.

- [ ] **Step 3: Remove the block via Edit tool**

Locate the exact block (use the surrounding lines as anchor), then replace it with an empty string. The block looks roughly like:

```tsx
{game.bggId && (
  <a
    href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
    ...
  >View on BGG</a>
)}
```

Adjust the `old_string` to the exact bytes from the Read above. `new_string` is empty.

- [ ] **Step 4: Verify no remaining BGG link in this file**

Run: `grep -n "boardgamegeek\|BGG" apps/web/src/components/shared-games/SharedGameDetailModal.tsx`
Expected: no output. (Comments referring to BGG-style rating scale are not in this file — if any do remain, leave them; they have zero UI impact.)

- [ ] **Step 5: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 6: Commit (do NOT commit yet — Task 3 updates the matching test first)**

Skip the commit here; bundle the modal edit + the test update in a single commit at the end of Task 3.

---

### Task 3: Flip the BGG-link test from positive to negative assertion

**Files:**
- Modify: `apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx:~546`

- [ ] **Step 1: Read the offending test**

Run: `grep -n "BGG\|bggId\|boardgamegeek" apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx`
Expected: line ~14 (header comment), line ~57 (`bggId: 13` fixture), ~546 (`it('shows BGG link when bggId exists'...)`), ~554 (`expect.stringContaining('boardgamegeek.com/boardgame/13')`).

- [ ] **Step 2: Read the exact test block (10-15 lines around line 546)**

Use Read tool with `offset: 543, limit: 18` to capture.

- [ ] **Step 3: Rewrite the test to assert the link is NOT rendered**

Replace the test body. The new test must:
- Render `<SharedGameDetailModal>` with the same fixture (`bggId: 13`).
- Assert that NO anchor with `href` containing `boardgamegeek.com` is in the DOM.
- Keep the test description honest: rename the `it(...)` from `'shows BGG link when bggId exists'` to `'does not render a BGG link even when bggId is present'`.

Concretely the new test body looks like:

```tsx
it('does not render a BGG link even when bggId is present', async () => {
  render(<SharedGameDetailModal {...defaultProps} game={gameWithBgg} />);

  // BGG mentions are hidden from user-facing UI (spec 2026-05-22).
  const bggLinks = screen.queryAllByRole('link').filter(el =>
    (el as HTMLAnchorElement).href.includes('boardgamegeek.com')
  );
  expect(bggLinks).toHaveLength(0);
});
```

(Adjust `defaultProps`/`gameWithBgg` to the names already used in the file — verify in the Read output from Step 2.)

- [ ] **Step 4: Also update the file header comment**

Around line 14: `* - Action buttons (Add to Collection, Share, BGG link)` →
`* - Action buttons (Add to Collection, Share)`

- [ ] **Step 5: Run the test**

Run: `cd apps/web && pnpm exec vitest run "src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx"`
Expected: all tests pass (the flipped test should now assert absence — `bggLinks` length is 0).

- [ ] **Step 6: Commit Tasks 2 + 3 together**

```bash
git add apps/web/src/components/shared-games/SharedGameDetailModal.tsx \
        apps/web/src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx
git commit -m "fix(shared-games): remove 'View on BGG' link from detail modal

User-facing pages no longer surface BGG/BoardGameGeek mentions
(spec 2026-05-22). Admin wizard still uses bggId via the DTO.

Test flipped from 'shows BGG link' → 'does not render a BGG link
even when bggId is present'.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 4: Remove BoardGameGeek entry from user settings/services page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/settings/services/page.tsx:~39-43`

- [ ] **Step 1: Read the services array context**

Use Read tool with `offset: 30, limit: 25` to see the full services array (Google, Discord, BoardGameGeek).

- [ ] **Step 2: Remove the BGG entry via Edit tool**

Remove the object element that contains `id: 'bgg'` and `label: 'BoardGameGeek'`. Preserve the surrounding array structure (the Google/Discord entries must remain intact). Trim any leading or trailing comma so the array stays syntactically valid.

- [ ] **Step 3: Update the page header comment**

The header comment at line 4 mentions BoardGameGeek. Edit:

```
 * Stub per integrazioni con servizi esterni (Google, Discord, BoardGameGeek).
```

→

```
 * Stub per integrazioni con servizi esterni (Google, Discord).
```

- [ ] **Step 4: Verify no leftover BGG references in this file**

Run: `grep -n "BGG\|BoardGameGeek\|bgg" apps/web/src/app/\(authenticated\)/settings/services/page.tsx`
Expected: no output

- [ ] **Step 5: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/app/(authenticated)/settings/services/page.tsx"
git commit -m "fix(settings): remove BoardGameGeek service stub from user settings

Part of hide-bgg-user-facing (spec 2026-05-22). The integration card
was a stub anyway — BGG is admin-only via the catalog wizard.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: Delete orphan BggSearchPanel duplicate

**Files:**
- Delete: `apps/web/src/components/games/BggSearchPanel.tsx`
- Delete: `apps/web/src/__tests__/components/games/BggSearchPanel.test.tsx`
- Modify: `apps/web/src/components/games/index.ts`

- [ ] **Step 1: Confirm the file is genuinely orphan**

Run:

```bash
grep -rln "from.*@/components/games/BggSearchPanel\|from.*@/components/games['\"].*BggSearchPanel" apps/web/src --include="*.ts" --include="*.tsx"
```

Expected: no output (apart from the test of the orphan itself, which is also being deleted). The `@/components/admin/shared-games/BggSearchPanel` admin version is a separate file and continues to be the canonical one.

- [ ] **Step 2: Delete both files via git rm**

```bash
git rm apps/web/src/components/games/BggSearchPanel.tsx \
       apps/web/src/__tests__/components/games/BggSearchPanel.test.tsx
```

- [ ] **Step 3: Read components/games/index.ts**

Use Read tool on `apps/web/src/components/games/index.ts` to see exact export statement (likely line 4: `export { BggSearchPanel } from './BggSearchPanel';`).

- [ ] **Step 4: Remove the export line via Edit**

Old: `export { BggSearchPanel } from './BggSearchPanel';`
New: (empty — delete the entire line, including any trailing newline)

- [ ] **Step 5: Verify build still passes**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 6: Verify the ADMIN BggSearchPanel still has consumers (sanity)**

Run: `grep -rln "from.*'@/components/admin/shared-games/BggSearchPanel'" apps/web/src --include="*.ts" --include="*.tsx"`
Expected: at least 1 hit (the admin shared-games new page).

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/games/index.ts \
        apps/web/src/components/games/BggSearchPanel.tsx \
        apps/web/src/__tests__/components/games/BggSearchPanel.test.tsx
git commit -m "chore(games): delete orphan BggSearchPanel duplicate

components/games/BggSearchPanel.tsx and its test were the orphan
duplicates — admin-side path (components/admin/shared-games/...) is
the canonical one and remains untouched.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 6: Final verification

**Files:**
- None (verification only)

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 2: Lint the touched files**

```bash
cd apps/web && pnpm exec eslint \
  "src/components/catalog/MeepleGameCatalogCard.tsx" \
  "src/components/shared-games/SharedGameDetailModal.tsx" \
  "src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx" \
  "src/app/(authenticated)/settings/services/page.tsx" \
  "src/components/games/index.ts"
```
Expected: 0 errors

- [ ] **Step 3: Run tests on touched areas + admin BggSearchPanel (sanity)**

```bash
cd apps/web && pnpm exec vitest run \
  "src/components/catalog" \
  "src/components/shared-games/__tests__" \
  "src/app/(authenticated)/settings/services" \
  "src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx"
```
Expected: all tests pass. The admin BggSearchPanel test must still pass — that proves the admin path is intact.

- [ ] **Step 4: Full BGG sweep (user-side) — final audit**

```bash
grep -rln "BGG\|BoardGameGeek\|bggId\|boardgamegeek" apps/web/src \
  --include="*.ts" --include="*.tsx" \
  | grep -v "admin\|bggClient\|types/bgg\|test\|spec\|useImportBggTags\|adminContentClient\|adminMechanicExtractor\|useBggSearch\|useSearchBgg" \
  | head -20
```

Expected: at most a handful of survivors that are PURE comments (e.g., `// BGG-style rating scale`) or in admin-only paths. Anything that renders to the screen is gone.

- [ ] **Step 5: Push the branch**

```bash
git push -u origin feature/hide-bgg-user-facing
```

- [ ] **Step 6: Open the PR**

```bash
gh pr create --base main-dev \
  --title "fix(ui): hide BGG mentions from user-facing UI (admin path untouched)" \
  --body "..."
```

Body should reference `docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-design.md` and list the modified files.

---

## Self-Review

**Spec coverage:**
- Catalog card BGG ID subtitle → Task 1 ✓
- "View on BGG" link in detail modal → Task 2 + test in Task 3 ✓
- Settings services BoardGameGeek entry → Task 4 ✓
- Orphan `components/games/BggSearchPanel.tsx` → Task 5 ✓
- Admin path untouched (verified by sanity-running admin BggSearchPanel test) → Task 6 ✓
- Backend untouched → no backend task (intentional) ✓

**Placeholder scan:** none — every code edit has explicit file path, exact lines, before/after blocks, and the commands to run.

**Type consistency:** the only edit that touches symbols is Task 3 (rename a test description); no shared types are renamed across tasks.
