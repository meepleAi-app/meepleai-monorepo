# Hide BGG mentions from user-facing UI — Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Eliminare i 6 residui BGG user-facing identificati dall'audit post-PR #1433, con admin-gating del gamebook upload wizard.

**Architecture:** Modifiche atomiche per superficie; admin-gating tramite hook `useAdminRole` esistente; rinominazioni i18n value-only (chiavi stabili).

**Tech Stack:** Next.js 16, React 19, react-intl (it.json/en.json), Vitest, Tailwind.

---

### Task 1: Branch setup

**Files:** none

- [ ] **Step 1.1:** Verifica HEAD su main-dev pulito

```bash
git checkout main-dev && git pull --ff-only
```

- [ ] **Step 1.2:** Crea branch phase-2

```bash
git checkout -b feature/hide-bgg-user-facing-phase-2
git config branch.feature/hide-bgg-user-facing-phase-2.parent main-dev
```

- [ ] **Step 1.3:** Commit spec + plan

```bash
git add docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-phase-2-design.md docs/superpowers/plans/2026-05-22-hide-bgg-user-facing-phase-2.md
git commit -m "docs(spec,plan): hide-bgg-user-facing Phase 2"
```

---

### Task 2: i18n value rename — `metaRating` & `specsBgg` (C1+C2)

**Files:**
- Modify: `apps/web/src/locales/it.json:2335,2374`
- Modify: `apps/web/src/locales/en.json:2285,2324`

- [ ] **Step 2.1:** it.json `metaRating` — `"BGG {value}"` → `"Community {value}"`

- [ ] **Step 2.2:** it.json `specsBgg` — `"BGG ID"` → `"Catalog ID"`

- [ ] **Step 2.3:** en.json `metaRating` — `"BGG {value}"` → `"Community {value}"`

- [ ] **Step 2.4:** en.json `specsBgg` — `"BGG ID"` → `"Catalog ID"`

- [ ] **Step 2.5:** Verifica

```bash
grep -n "BGG" apps/web/src/locales/it.json apps/web/src/locales/en.json | grep -v "tabBgg\|bggLoading\|actionCardBgg\|searchInfo\|bggId"
```
Expected: solo i match nel contesto `gamebook.upload` (sarà coperto da Task 4).

- [ ] **Step 2.6:** Commit

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "fix(i18n): rename BGG rating/ID labels to Community/Catalog ID"
```

---

### Task 3: GameChatTab low-confidence alternative — "Cerca online" (C4)

**Files:**
- Modify: `apps/web/src/components/features/game-chat/GameChatTab.tsx:103`
- Modify: `apps/web/src/components/features/game-chat/__tests__/GameChatTab.test.tsx`

- [ ] **Step 3.1:** Aggiorna `GameChatTab.tsx` linea 99-105

Replace:
```tsx
    const lowConfAlts: DisclaimerAlternative[] =
      msg.isLowQuality && !showOoc
        ? [
            { label: 'Verifica nel manuale', kind: 'kb' },
            { label: 'Cerca su BGG', kind: 'external', url: 'https://boardgamegeek.com/' },
          ]
        : [];
```

With:
```tsx
    const lowConfAlts: DisclaimerAlternative[] =
      msg.isLowQuality && !showOoc
        ? [
            { label: 'Verifica nel manuale', kind: 'kb' },
            {
              label: 'Cerca online',
              kind: 'external',
              url: `https://www.google.com/search?q=${encodeURIComponent(`${gameTitle} regole`)}`,
            },
          ]
        : [];
```

- [ ] **Step 3.2:** Cerca + aggiorna asserzioni test

```bash
grep -n "Cerca su BGG\|boardgamegeek" apps/web/src/components/features/game-chat/__tests__/GameChatTab.test.tsx
```
Per ogni occorrenza: sostituire `'Cerca su BGG'` con `'Cerca online'`; sostituire `'https://boardgamegeek.com/'` con `expect.stringContaining('google.com/search')`.

- [ ] **Step 3.3:** Run test

```bash
cd apps/web && pnpm exec vitest run src/components/features/game-chat/__tests__/GameChatTab.test.tsx
```
Expected: all green.

- [ ] **Step 3.4:** Commit

```bash
git add apps/web/src/components/features/game-chat/GameChatTab.tsx apps/web/src/components/features/game-chat/__tests__/GameChatTab.test.tsx
git commit -m "fix(game-chat): replace 'Cerca su BGG' low-conf alternative with 'Cerca online' (Google)"
```

---

### Task 4: Gamebook upload wizard — admin-only BGG (C3a + C3b)

**Files:**
- Modify: `apps/web/src/components/features/gamebook/NoResultsPanel.tsx`
- Modify: `apps/web/src/components/features/gamebook/__tests__/NoResultsPanel.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx`
- Modify: `apps/web/src/app/(authenticated)/gamebook/upload/_components/__tests__/GamebookUploadView.test.tsx`

#### 4a — NoResultsPanel optional BGG card

- [ ] **Step 4.1:** Aggiungi prop `showBggCard?: boolean` (default `false`) all'interface `NoResultsPanelProps` in `NoResultsPanel.tsx`

```tsx
export interface NoResultsPanelProps {
  readonly query: string;
  readonly onCreateNew: () => void;
  readonly onSearchBgg?: () => void;
  readonly onAddPrivate: () => void;
  readonly labels: NoResultsPanelLabels;
  readonly className?: string;
  /**
   * When true, renders the "Search BGG" ActionCard. Defaults to false:
   * admin-only flow per spec 2026-05-22-hide-bgg-user-facing-phase-2.
   */
  readonly showBggCard?: boolean;
}
```

- [ ] **Step 4.2:** Aggiungi destructuring `showBggCard = false` ai props

- [ ] **Step 4.3:** Wrap il secondo `<ActionCard icon={<span>🌐</span>} ...>` (BGG) in:

```tsx
{showBggCard && onSearchBgg && (
  <ActionCard
    icon={<span>🌐</span>}
    title={labels.actionCardBgg.title}
    description={labels.actionCardBgg.description}
    onClick={onSearchBgg}
  />
)}
```

- [ ] **Step 4.4:** Aggiorna `NoResultsPanel.test.tsx` per riflettere il default off

Cerca i test esistenti e:
- Per i test che asseriscono presenza BGG card → pass `showBggCard={true}` esplicito
- Aggiungi un nuovo test: `it('does not render BGG card by default', ...)` che verifica assenza di "Cerca su BoardGameGeek" senza `showBggCard`

- [ ] **Step 4.5:** Run test

```bash
cd apps/web && pnpm exec vitest run src/components/features/gamebook/__tests__/NoResultsPanel.test.tsx
```
Expected: all green.

#### 4b — GamebookUploadView admin-gating

- [ ] **Step 4.6:** In `GamebookUploadView.tsx`, importa `useAdminRole`

```tsx
import { useAdminRole } from '@/hooks/useAdminRole';
```

- [ ] **Step 4.7:** Nel componente, aggiungi check:

```tsx
const { isAdminOrAbove, isLoading: isAdminLoading } = useAdminRole();
const showBggIntegration = isAdminOrAbove && !isAdminLoading;
```

- [ ] **Step 4.8:** Trova la rendering del tab "BGG" (linea ~529 `tabsBgg`) e wrappa la sua emissione con `showBggIntegration && ...`. Stessa cosa per il loading state che mostra "Cerco su BoardGameGeek..." e per il search-info testuale "Cerca su BGG ↑" (condizionale).

- [ ] **Step 4.9:** Trova il rendering di `<NoResultsPanel ...>` e passa `showBggCard={showBggIntegration}` + `onSearchBgg={showBggIntegration ? handleSearchBgg : undefined}`.

- [ ] **Step 4.10:** Aggiorna `GamebookUploadView.test.tsx`:
- Mock di default `useAdminRole` → `isAdminOrAbove: false`
- Test esistenti che asseriscono presenza BGG → wrappare con un setup admin
- Nuovo test: `it('hides BGG tab and ActionCard for non-admin users')`
- Nuovo test: `it('shows BGG tab for admin users when useAdminRole returns isAdminOrAbove=true')`

Mock pattern:
```tsx
vi.mock('@/hooks/useAdminRole', () => ({
  useAdminRole: vi.fn(() => ({ isAdminOrAbove: false, isLoading: false })),
}));
```

- [ ] **Step 4.11:** Run test

```bash
cd apps/web && pnpm exec vitest run src/app/'(authenticated)'/gamebook/upload/_components/__tests__/GamebookUploadView.test.tsx
```
Expected: all green.

- [ ] **Step 4.12:** Commit

```bash
git add apps/web/src/components/features/gamebook/NoResultsPanel.tsx apps/web/src/components/features/gamebook/__tests__/NoResultsPanel.test.tsx apps/web/src/app/'(authenticated)'/gamebook/upload/_components/GamebookUploadView.tsx apps/web/src/app/'(authenticated)'/gamebook/upload/_components/__tests__/GamebookUploadView.test.tsx
git commit -m "fix(gamebook): admin-gate BGG search in /gamebook/upload wizard"
```

---

### Task 5: Dev playground social card — rimuovi BGG (C5)

**Files:**
- Modify: `apps/web/src/app/(public)/dev/meeple-card/page.tsx:1474-1483`

- [ ] **Step 5.1:** Rimuovi l'oggetto `{ platform: 'BGG', handle: 'marcogames', icon: '🎲', href: 'https://boardgamegeek.com' }` dall'array `links`. Mantieni Twitter + Discord.

- [ ] **Step 5.2:** Verifica

```bash
grep -n "BGG\|boardgamegeek" apps/web/src/app/'(public)'/dev/meeple-card/page.tsx
```
Expected: empty.

- [ ] **Step 5.3:** Commit

```bash
git add apps/web/src/app/'(public)'/dev/meeple-card/page.tsx
git commit -m "fix(dev): remove BGG social card from /dev/meeple-card playground"
```

---

### Task 6: Contact help text — generic provider (C6)

**Files:**
- Modify: `apps/web/src/locales/it.json:1186`
- Modify: `apps/web/src/locales/en.json:1136`

- [ ] **Step 6.1:** it.json — sostituisci `"e link BoardGameGeek"` con `"e link al database giochi di riferimento"`

- [ ] **Step 6.2:** en.json — sostituisci `"and BoardGameGeek link"` con `"and a link to your reference game database"`

- [ ] **Step 6.3:** Verifica

```bash
grep -n "BoardGameGeek\|boardgamegeek" apps/web/src/locales/it.json apps/web/src/locales/en.json
```
Expected: solo le occorrenze gamebook (Task 7 ne tratta i restanti tramite admin-gating; lo `bggLoadingTitle` e `actionCardBgg` rimangono come i18n keys ma sono renderizzati solo per admin).

- [ ] **Step 6.4:** Commit

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "fix(i18n): replace 'BoardGameGeek link' with generic 'reference database' in contact help"
```

---

### Task 7: Verifica + push + PR

**Files:** none (verification)

- [ ] **Step 7.1:** Typecheck completo

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 7.2:** ESLint sui file toccati

```bash
cd apps/web && pnpm exec eslint \
  src/locales/it.json src/locales/en.json \
  src/components/features/game-chat/GameChatTab.tsx \
  src/components/features/gamebook/NoResultsPanel.tsx \
  "src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx" \
  "src/app/(public)/dev/meeple-card/page.tsx"
```
Expected: 0 errors.

- [ ] **Step 7.3:** Vitest mirato su file toccati

```bash
cd apps/web && pnpm exec vitest run \
  src/components/features/game-chat/__tests__/GameChatTab.test.tsx \
  src/components/features/gamebook/__tests__/NoResultsPanel.test.tsx \
  "src/app/(authenticated)/gamebook/upload/_components/__tests__/GamebookUploadView.test.tsx" \
  "src/app/(authenticated)/games/[id]/_components/__tests__/GameDetailView.test.tsx"
```
Expected: all green.

- [ ] **Step 7.4:** Regression check Phase 1 tests

```bash
cd apps/web && pnpm exec vitest run \
  src/components/shared-games/__tests__/SharedGameDetailModal.test.tsx \
  src/components/admin/shared-games/__tests__/BggSearchPanel.test.tsx
```
Expected: 33/33 + 19/19 green (Phase 1 unaffected).

- [ ] **Step 7.5:** Sweep BGG user-facing residuo

```bash
grep -rn "BoardGameGeek\|boardgamegeek\.com" apps/web/src --include="*.tsx" --include="*.ts" --include="*.json" | grep -v "/admin/" | grep -v "lib/api/clients/" | grep -v "types/bgg" | grep -v "lib/parsers/bgg-tsv" | grep -v "__tests__/.*bggId:" | grep -v "stories.tsx.*bggId:"
```
Expected: solo entry rimaste in i18n `gamebook.upload.*` (bggLoadingTitle, actionCardBgg, etc.) che sono renderizzate ESCLUSIVAMENTE per admin tramite gating Task 4.

- [ ] **Step 7.6:** Push branch

```bash
git push -u origin feature/hide-bgg-user-facing-phase-2
```

- [ ] **Step 7.7:** Apri PR verso main-dev

```bash
gh pr create --base main-dev --head feature/hide-bgg-user-facing-phase-2 \
  --title "fix(ui): hide BGG mentions Phase 2 — rename labels + admin-gate gamebook upload" \
  --body "$(cat <<EOF
## Summary

Phase 2 (segue PR #1433): elimina 6 superfici BGG user-facing residue identificate dall'audit post-Phase 1.

Spec: \`docs/superpowers/specs/2026-05-22-hide-bgg-user-facing-phase-2-design.md\`
Plan: \`docs/superpowers/plans/2026-05-22-hide-bgg-user-facing-phase-2.md\`

### Decisioni UX (dal product owner)

1. \`/games/[id]\` rating + ID specs → rinomina **"Community"** + **"Catalog ID"**
2. \`/gamebook/upload\` step 1 BGG → **admin-only** (gate \`useAdminRole().isAdminOrAbove\`)
3. \`GameChatTab\` low-conf "Cerca su BGG" → **"Cerca online"** verso Google con query \`{title} regole\`

### Changes

| File | Modifica |
|------|----------|
| \`locales/{it,en}.json\` | \`metaRating\` "BGG {value}" → "Community {value}"; \`specsBgg\` "BGG ID" → "Catalog ID"; contact help "BoardGameGeek link" → "reference database link" |
| \`GameChatTab.tsx\` | Low-conf alt "Cerca su BGG" → "Cerca online" (Google search per game title) |
| \`NoResultsPanel.tsx\` | Aggiunto prop \`showBggCard\` (default false) |
| \`GamebookUploadView.tsx\` | Admin-gating tab BGG + ActionCard via \`useAdminRole\` |
| \`(public)/dev/meeple-card/page.tsx\` | Rimosso social link BGG dalla demo card |

### Test plan

- [x] \`pnpm typecheck\` → 0 errors
- [x] \`pnpm exec eslint <touched>\` → 0 errors
- [x] Vitest GameChatTab/NoResultsPanel/GamebookUploadView/GameDetailView → all green
- [x] Vitest Phase 1 regression (SharedGameDetailModal, admin BggSearchPanel) → 33/33 + 19/19 green
- [ ] Browser smoke: \`/games/[id]\` hero/info, \`/gamebook/upload\` come admin+user, \`/games/[id]\` chat low-conf, \`/dev/meeple-card\`, \`/contact\`
EOF
)"
```

---

## Self-review

✅ **Spec coverage:** ogni AC della spec ha task corrispondente (C1-C6 → Task 2/3/4/5/6; T1-T3 inclusi nei Task 3 e 4; gating regression-safe via Task 4 con test esplicito).
✅ **Placeholder scan:** nessun TBD/TODO/etcetera.
✅ **Type consistency:** `showBggCard?: boolean` definito in NoResultsPanelProps + utilizzato in GamebookUploadView con stesso nome; `isAdminOrAbove` matcha `useAdminRole().isAdminOrAbove`.
✅ **Edge cases:** `isAdminLoading` → default-hide (conservative); `gameTitle` default `'Gioco'` → URL Google ancora valido anche senza title.
