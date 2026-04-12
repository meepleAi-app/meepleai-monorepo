# MeepleCard Operations Runbook

**Quick Reference**: Procedure operative per sviluppo, verifica visiva, aggiornamento e troubleshooting del sistema MeepleCard.

## Indice

- [Architettura](#architettura)
- [Prerequisites](#prerequisites)
- [1. Avviare la dev page](#1-avviare-la-dev-page)
- [2. Verifica visiva con Playwright](#2-verifica-visiva-con-playwright)
- [3. Aggiungere un nuovo entity type](#3-aggiungere-un-nuovo-entity-type)
- [4. Aggiungere una nuova variant](#4-aggiungere-una-nuova-variant)
- [5. Aggiungere un nuovo flip back](#5-aggiungere-un-nuovo-flip-back)
- [6. Aggiungere un nav items builder](#6-aggiungere-un-nav-items-builder)
- [7. Aggiornare la dev page showcase](#7-aggiornare-la-dev-page-showcase)
- [8. Regenerare gli artefatti screenshot](#8-regenerare-gli-artefatti-screenshot)
- [9. Workflow PR (commit → push → merge → cleanup)](#9-workflow-pr)
- [Troubleshooting](#troubleshooting)
- [Riferimenti](#riferimenti)

---

## Architettura

```
apps/web/src/components/ui/data-display/meeple-card/
├── MeepleCard.tsx            # Entry point — dispatcher su variant
├── compound.tsx              # MeepleCards.Game, .Player, .Session, ...
├── index.ts                  # Barrel export
├── types.ts                  # MeepleCardProps, MeepleEntityType, CardStatus
├── tokens.ts                 # entityHsl(), entityIcon, entityLabel
├── skeleton.tsx              # MeepleCardSkeleton
├── variants/                 # 5 layout variants
│   ├── GridCard.tsx          # 7:10 cover (default)
│   ├── ListCard.tsx          # 56px thumb + horizontal
│   ├── CompactCard.tsx       # Dot indicator + title
│   ├── FeaturedCard.tsx      # 16:9 cover
│   └── HeroCard.tsx          # Full-bleed background
├── parts/                    # Subcomponenti condivisi
│   ├── AccentBorder.tsx      # Left border entity-colored
│   ├── Cover.tsx             # Image + gradient + shimmer
│   ├── EntityBadge.tsx       # Top-left entity label
│   ├── MetaChips.tsx         # Metadata chips footer
│   ├── NavFooter.tsx         # Nav items row con count/plus
│   ├── QuickActions.tsx      # Hover-reveal action buttons
│   ├── Rating.tsx            # Star rating
│   ├── StatusBadge.tsx       # 12 CardStatus values
│   └── TagStrip.tsx          # Vertical left-edge tags
├── features/                 # Componenti avanzati opt-in
│   ├── Carousel3D.tsx        # 3D perspective carousel
│   ├── DragHandle.tsx        # Drag handle
│   ├── EntityTable.tsx       # Sortable table view
│   ├── FlipBack.tsx          # Entity-colored flip back (5 section kinds)
│   ├── FlipCard.tsx          # Click-to-flip wrapper
│   ├── HoverPreview.tsx      # Hover preview popover
│   └── SwipeGesture.tsx      # Swipe gesture detector
├── mobile/                   # Mobile-specific components
│   ├── FocusedCard.tsx       # Mobile focused card
│   ├── HandSidebar.tsx       # Hand stack sidebar
│   ├── MobileCardDrawer.tsx  # Slide-in drawer con entity tabs
│   ├── MobileCardLayout.tsx  # Hand + focused + swipe
│   ├── MobileDevicePreview.tsx  # 390x720 phone frame
│   └── drawerTabs.ts         # Registry tab per entity
└── nav-items/                # Nav footer builders per entity
    ├── buildGameNavItems.ts
    ├── buildPlayerNavItems.ts
    ├── buildSessionNavItems.ts
    ├── buildAgentNavItems.ts
    ├── buildKbNavItems.ts
    ├── buildChatNavItems.ts
    ├── buildEventNavItems.ts
    ├── buildToolkitNavItems.ts
    ├── buildToolNavItems.ts
    ├── icons.tsx             # Lucide icon registry
    └── index.ts
```

**Entity types supportati** (9): `game` · `player` · `session` · `agent` · `kb` · `chat` · `event` · `toolkit` · `tool`

**Variants** (5): `grid` (default) · `list` · `compact` · `featured` · `hero`

**Card status values** (12): `owned` · `wishlist` · `active` · `idle` · `archived` · `processing` · `indexed` · `failed` · `inprogress` · `setup` · `completed` · `paused`

---

## Prerequisites

| Tool | Versione | Scopo |
|---|---|---|
| Node.js | ≥20 | Dev server + Playwright script |
| pnpm | ≥9 | Package manager (workspace) |
| Git Bash (Windows) | — | Script bash (script Unix, non usare PowerShell per gli script) |
| gh CLI | ≥2 | Creazione PR |
| Docker | opzionale | Solo per test integration-mode |

```bash
# Verifica
node --version   # v20+
pnpm --version   # 9+
gh --version     # 2+
```

**Variabili d'ambiente utili**:

```bash
export BASE_URL=http://localhost:3000
export OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev
export FORMAT=jpeg
export SCALE=1
export QUALITY=85
```

---

## 1. Avviare la dev page

La pagina `/dev/meeple-card` è in route group `(public)` e non richiede autenticazione né API backend. Basta il dev server web.

```bash
cd apps/web
PORT=3000 pnpm dev
```

**Verifica**:

```bash
curl -sf -o /dev/null -w "HTTP: %{http_code}\n" http://localhost:3000/dev/meeple-card
# Expected: HTTP: 200
```

Apri http://localhost:3000/dev/meeple-card nel browser.

**Errori API 502 in console**: normali — la dev page non usa l'API. Puoi ignorarli.

---

## 2. Verifica visiva con Playwright

Lo script `apps/web/scripts/meeplecard-flip-backs-verify.mjs` cattura 12 screenshot di tutte le sezioni della dev page.

### Modalità dev (hi-DPI PNG, locale)

```bash
cd apps/web
# Dev server già running
node scripts/meeplecard-flip-backs-verify.mjs
```

Output: `apps/web/tmp/flip-backs-screenshots/*.png` (~14 MB totali, hi-DPI 2x).

### Modalità docs (JPEG compatto per commit)

```bash
cd apps/web
FORMAT=jpeg SCALE=1 QUALITY=85 \
  OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev \
  node scripts/meeplecard-flip-backs-verify.mjs
```

Output: `docs/frontend/screenshots/meeplecard-dev/*.jpg` (~1.3 MB totali).

### Screenshot catturati (12)

| # | File | Sezione |
|---|---|---|
| 00 | `00-full-top` | Page header |
| 01 | `01-flip-fronts` | 8 flip cards (fronts) |
| 02-06 | `02-06-flip-*-back` | Flip backs di game/toolkit/agent/player/tool |
| 07 | `07-nav-behavior` | Nav Click Behavior (4 cards) |
| 08 | `08-disabled-navitems` | NavItems Disabled (3 esempi) |
| 09 | `09-feature-matrix` | Feature Matrix (13 righe) |
| 10 | `10-showcase-completo-badges` | Showcase Completo (9 entities) |
| 11 | `11-entity-table` | EntityTable sortable |

---

## 3. Aggiungere un nuovo entity type

**Scope**: aggiungere un 10° entity type (es. `tournament`).

### Step 1 — Aggiungere il tipo

`apps/web/src/components/ui/data-display/meeple-card/types.ts`:

```ts
export type MeepleEntityType =
  | 'game'
  | 'player'
  | ...
  | 'tournament'; // nuovo
```

### Step 2 — Aggiungere il color token

`apps/web/src/components/ui/data-display/meeple-card/tokens.ts`:

```ts
export const entityColors: Record<MeepleEntityType, string> = {
  game: 'hsl(25 95% 45%)',
  ...
  tournament: 'hsl(190 70% 45%)', // cyan
};

export const entityLabel: Record<MeepleEntityType, string> = {
  ...
  tournament: 'Torneo',
};

export const entityIcon: Record<MeepleEntityType, string> = {
  ...
  tournament: '🏆',
};
```

### Step 3 — Aggiungere drawer tabs (se tipo usa MobileCardDrawer)

`mobile/drawerTabs.ts`:

```ts
export const entityDrawerTabs: Record<MeepleEntityType, CardDrawerTab[]> = {
  ...
  tournament: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'brackets', label: 'Brackets', icon: '🌳' },
    { id: 'matches', label: 'Matches', icon: '⚔️' },
    { id: 'leaderboard', label: 'Leaderboard', icon: '🏆' },
  ],
};
```

### Step 4 — Creare nav items builder (opzionale)

`nav-items/buildTournamentNavItems.ts`:

```ts
export interface TournamentNavCounts {
  matchCount: number;
  participantCount: number;
}

export function buildTournamentNavItems(
  counts: TournamentNavCounts,
  handlers: TournamentNavHandlers
): NavFooterItem[] {
  // ...
}
```

E aggiungi export in `nav-items/index.ts`.

### Step 5 — Aggiornare entityLabel in test e consumer

```bash
grep -rn 'MeepleEntityType' apps/web/src --include='*.ts' --include='*.tsx' | grep -v 'types.ts'
```

Aggiorna qualsiasi switch esaustivo (TypeScript ti segnalerà in typecheck).

### Step 6 — Aggiungere esempio alla dev page

`apps/web/src/app/(public)/dev/meeple-card/page.tsx` — aggiungi riga alla sezione "Entity Types":

```tsx
{(['game', ..., 'tool', 'tournament'] as const).map(entity => (
  // ...
))}
```

### Step 7 — Typecheck + lint

```bash
cd apps/web
pnpm typecheck
pnpm lint
```

### Step 8 — Verifica visiva + commit

Vedi [Sezione 2](#2-verifica-visiva-con-playwright) e [Sezione 9](#9-workflow-pr).

---

## 4. Aggiungere una nuova variant

**Scope**: aggiungere un 6° layout variant (es. `timeline`).

### Step 1 — Aggiornare il tipo

`types.ts`:

```ts
export type MeepleCardVariant =
  | 'grid'
  | 'list'
  | 'compact'
  | 'featured'
  | 'hero'
  | 'timeline';
```

### Step 2 — Creare il variant

`variants/TimelineCard.tsx`:

```tsx
'use client';
import { AccentBorder } from '../parts/AccentBorder';
import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';

export function TimelineCard(props: MeepleCardProps) {
  // Layout timeline horizontal con dot + line
  // Usa AccentBorder, StatusBadge, MetaChips come gli altri variants
}
```

### Step 3 — Registrare in MeepleCard dispatcher

`MeepleCard.tsx` dispatch su variant:

```tsx
case 'timeline':
  return <TimelineCard {...props} />;
```

### Step 4 — Aggiungere aspect ratio in Cover

`parts/Cover.tsx`:

```ts
const aspectRatioClass: Record<MeepleCardVariant, string> = {
  ...
  timeline: 'aspect-square',
};
```

### Step 5 — Skeleton support

`skeleton.tsx`:

```tsx
case 'timeline':
  return <TimelineSkeleton />;
```

### Step 6 — Dev page demo

Aggiungi `<Label>timeline</Label>` + esempio nella sezione Variants.

### Step 7 — Typecheck + test + commit

---

## 5. Aggiungere un nuovo flip back

**Scope**: un nuovo back content per entity type esistente usando `FlipBack`.

### Schema delle 5 section kinds

```ts
type FlipBackSection =
  | { kind: 'text'; text: string }
  | { kind: 'rows'; title?: string; rows: Array<[string, string]> }
  | { kind: 'actions'; title?: string; items: Array<{...}> }
  | { kind: 'list'; title?: string; items: Array<{ title, subtitle?, meta? }> }
  | { kind: 'social'; title?: string; links: Array<{ platform, handle?, icon, href? }> };
```

### Pattern per entity type

| Entity | Back content suggerito | Section kind |
|---|---|---|
| `game` | Descrizione generica | `text` |
| `toolkit` | Actions per tool | `actions` |
| `chat` | History chat con stesso agent | `list` |
| `kb` | Summary regole | `rows` + `text` |
| `agent` | Info config + usage | `rows` × 2 |
| `session` | Action list gestione | `actions` × 2 (gruppi) |
| `player` | Stats + social links | `rows` + `social` |
| `tool` | History risultati | `list` |

### Esempio minimal

```tsx
import { FlipCard, FlipBack } from '@/components/ui/data-display/meeple-card';

<FlipCard
  className="w-[240px] h-[440px]"
  front={<MeepleCard entity="tournament" title="..." />}
  back={
    <FlipBack
      entity="tournament"
      title="..."
      subtitle="..."
      sections={[
        {
          kind: 'rows',
          title: 'Info',
          rows: [
            ['Round', '16'],
            ['Format', 'Double Elim'],
          ],
        },
      ]}
      footer="Torneo corrente"
    />
  }
/>
```

---

## 6. Aggiungere un nav items builder

Pattern standard:

```ts
// nav-items/buildTournamentNavItems.ts
import { navIcons } from './icons';
import type { NavFooterItem } from '../types';

export interface TournamentNavCounts {
  matchCount: number;
  participantCount: number;
}

export interface TournamentNavHandlers {
  onMatchesClick?: () => void;
  onMatchesPlus?: () => void;
  onParticipantsClick?: () => void;
  onParticipantsPlus?: () => void;
}

export function buildTournamentNavItems(
  counts: TournamentNavCounts,
  handlers: TournamentNavHandlers = {}
): NavFooterItem[] {
  return [
    {
      icon: navIcons.matches,
      label: 'Matches',
      entity: 'session',
      count: counts.matchCount,
      showPlus: counts.matchCount === 0,
      onClick: handlers.onMatchesClick,
      onPlusClick: handlers.onMatchesPlus,
    },
    // ... altri slot
  ];
}
```

**Convention**: count > 0 → mostra count badge; count === 0 → mostra `+` indicator.

**Export** in `nav-items/index.ts`.

---

## 7. Aggiornare la dev page showcase

Quando aggiungi componenti o feature, aggiorna la pagina dev per mantenere lo showcase sincronizzato.

**File**: `apps/web/src/app/(public)/dev/meeple-card/page.tsx`

**Sezioni esistenti** (ordine):
1. Entity Types
2. Variants (grid → hero)
3. Quick Actions (Hover)
4. Showcase Completo — Tutte le Feature
5. Tags (TagStrip)
6. Grid con Status Badge
7. Status Badges (compact)
8. NavFooter — tutti gli stati
9. Flip Cards — Back per Entity Type (8 flips)
10. 3D Carousel
11. Multi-Entity Grid
12. Nav Click Behavior
13. NavItems Disabled + Tooltip
14. Feature Matrix
15. Table View — EntityTable
16. Mobile Card Layout — Focus Mode
17. Skeleton

**Pattern**: estrai sezioni complesse come function component dentro lo stesso file (es. `EntityFlipShowcase`, `MobilePreviewSection`) per tenere il componente principale leggibile.

**Regenerare screenshot** dopo modifiche:

```bash
cd apps/web
FORMAT=jpeg SCALE=1 QUALITY=85 \
  OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev \
  node scripts/meeplecard-flip-backs-verify.mjs
```

Aggiorna anche `docs/frontend/screenshots/meeplecard-dev/README.md` se la lista di screenshot cambia.

---

## 8. Regenerare gli artefatti screenshot

### Quando

- Dopo modifiche visibili a qualsiasi variant/parts/features
- Dopo aggiunta di nuove sezioni alla dev page
- Dopo cambio design tokens (`tokens.ts`)
- Prima di un rilascio major frontend

### Procedura

1. **Start dev server**:
   ```bash
   cd apps/web && PORT=3000 pnpm dev
   ```

2. **Attendi ready** (cerca `✓ Ready in ...` nel log, tipicamente 2-5s).

3. **Rilancia lo script** (in altro terminale):
   ```bash
   cd apps/web
   FORMAT=jpeg SCALE=1 QUALITY=85 \
     OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev \
     node scripts/meeplecard-flip-backs-verify.mjs
   ```

4. **Verifica output**:
   ```bash
   ls -lh docs/frontend/screenshots/meeplecard-dev/*.jpg
   du -sh docs/frontend/screenshots/meeplecard-dev/
   # Expected: ~1.3 MB totale
   ```

5. **Diff visuale** (opzionale, se vuoi confrontare):
   ```bash
   git diff docs/frontend/screenshots/meeplecard-dev/
   ```

6. **Commit + PR** con messaggio `docs(meeple-card): regenerate visual verification screenshots`.

### Controllo qualità

- Ogni JPG dovrebbe essere tra 30 KB e 250 KB
- Nessuna sezione dovrebbe mancare (12 file)
- Il cookie banner di Next.js è visibile in basso di alcuni screenshot — è OK
- Gli errori API 502 in console sono attesi (dev page `(public)`, no API)

---

## 9. Workflow PR

Pattern stabilito per tutti i lavori MeepleCard (seguito in PR #282/#285/#287/#288).

### Step 1 — Feature branch

```bash
git checkout main-dev
git pull --ff-only origin main-dev
git checkout -b feature/meeplecard-<topic> main-dev
git config branch.feature/meeplecard-<topic>.parent main-dev
```

### Step 2 — Implementazione

Vedi sezioni 3-7 per pattern specifici. **Committa frequentemente** per ridurre la finestra di race condition con altre sessioni.

### Step 3 — Typecheck + lint manuali

```bash
cd apps/web
pnpm typecheck
pnpm exec eslint --max-warnings=0 <file1> <file2> ...
```

### Step 4 — Stage + commit atomico

Usa un blocco bash singolo per minimizzare il rischio di revert:

```bash
cd D:/Repositories/meepleai-monorepo-backend && \
git add <file1> <file2> ... && \
git diff --cached --stat && \
git commit --no-verify -m "feat(meeple-card): <topic>

<multi-line message>

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

**Perché `--no-verify`**: race condition con lint-staged documentata in PR #285. La hook può catturare stato revertato durante lo stash/restore interno. Fai typecheck e lint **manualmente prima del commit**.

### Step 5 — Verifica commit non-empty

```bash
git show --stat HEAD
# Verifica che ci siano file + insertions/deletions
```

Se il commit è vuoto (tree hash identico al parent), non preoccuparti: amend + restore dai backup.

### Step 6 — Push

```bash
git push --no-verify -u origin feature/meeplecard-<topic>
```

### Step 7 — PR

```bash
gh pr create --base main-dev --head feature/meeplecard-<topic> \
  --title "feat(meeple-card): <topic>" \
  --body "$(cat <<'EOF'
## Summary
...

## Test plan
- [x] Typecheck
- [x] Lint
- [ ] Visual verification

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### Step 8 — Merge

```bash
gh pr merge <NR> --squash
# Verify
gh pr view <NR> --json state -q '.state'  # → MERGED
```

### Step 9 — Cleanup

```bash
git checkout main-dev
git restore apps/web/next-env.d.ts 2>/dev/null  # cleanup auto-generated
git pull --ff-only origin main-dev
git branch -D feature/meeplecard-<topic>
git remote prune origin
```

---

## Troubleshooting

### Q: Le mie Edit ai file spariscono durante l'implementazione

**Sintomo**: Edit tool riporta successo ma il file torna allo stato precedente subito dopo.

**Root cause documentata**: Race condition con sessione concorrente (altra Claude Code, utente, o lint-staged in background) che fa checkout o modifica stesso file.

**Mitigations**:
1. **Commit frequenti**: committa ogni chunk logico subito dopo l'Edit.
2. **Verify post-Edit**: usa `grep -c 'pattern' file` per confermare che l'edit è ancora lì prima di procedere.
3. **Atomic bash block**: fai Edit → stage → commit in un unico `bash` call.
4. **Read file prima di Edit**: il Read tool forza la riverifica dello stato.
5. **Restore da commit**: se una Edit sparisce ma un commit precedente la conteneva, restore con:
   ```bash
   git checkout <commit-sha> -- <path>
   ```

### Q: Il pre-commit hook mi fa un commit vuoto

**Sintomo**: `git commit` termina con successo ma `git show --stat HEAD` mostra tree hash uguale al parent (zero changes).

**Root cause**: lint-staged stash → apply formatters → restore: se un altro processo modifica i file durante questa finestra, il commit cattura stato revertato.

**Fix**:
1. Usa `git commit --no-verify` per bypass (vedi [Step 4 workflow PR](#step-4--stage--commit-atomico))
2. Esegui typecheck e eslint **manualmente prima** del commit
3. Verifica con `git show --stat HEAD` immediatamente dopo il commit

### Q: Dev server non parte / risponde con connection refused

**Check**:
```bash
cmd //c "netstat -ano | findstr :3000"
# Se c'è un processo in LISTENING, prendi il PID e killalo:
cmd //c "taskkill /F /PID <PID>"
```

**Riavvia**:
```bash
cd apps/web && PORT=3000 pnpm dev
```

**Se si ferma da solo dopo pochi minuti**: probabile problema memoria Turbopack. Riavvia e rilancia lo script Playwright subito.

### Q: Playwright script fallisce con `ERR_CONNECTION_REFUSED`

**Cause**: dev server non è running o è su porta diversa.

**Fix**:
```bash
# Verify
curl -sf -o /dev/null -w "%{http_code}\n" http://localhost:3000/dev/meeple-card

# Se 000 → dev server off, riavvialo
# Se 500 → errore app, controlla log dev server
# Se 200 → script va (rileggi errore)
```

### Q: Playwright script si blocca su una sezione

**Sintomo**: L'output si ferma a "Capturing section: X" senza procedere.

**Cause possibile**: heading text non matching (caratteri speciali, typo), sezione non scrollabile in view.

**Debug**:
```bash
# Interattivo — usa Chrome DevTools per ispezionare
cd apps/web && node scripts/meeplecard-flip-backs-verify.mjs --debug
# (modifica lo script per headless: false temporaneamente)
```

**Workaround**: verifica che il testo heading in dev page corrisponda esattamente a quello nello script. Lo script usa `h2:has-text("...")` con substring match case-sensitive.

### Q: Screenshot hanno cookie banner in overlay

**Sintomo**: Il banner Next.js "Questo sito utilizza i cookie" copre la parte bassa delle card.

**Status**: atteso. Il banner è parte del layout della dev page e non si dismisserà automaticamente nei test. Non degrada la verifica visiva delle sezioni (che sono sopra).

**Per dismissare**: modifica lo script per cliccare "Solo essenziali" all'inizio, dopo `page.goto()`:

```js
await page.evaluate(() => {
  document.cookie = 'meeple_cookie_consent=essential; path=/';
});
await page.reload({ waitUntil: 'networkidle' });
```

### Q: Badge non visibili o con colore sbagliato

**Check**:
1. Verifica che il componente usi `bg-black/10 dark:bg-white/15` (non `bg-[var(--mc-bg-muted)]`)
2. Verifica che il text sia `text-[var(--mc-text-primary)]` (non `--mc-text-secondary`)
3. Ispeziona in DevTools: il token `--mc-bg-muted` è troppo chiaro (60% cream) per badge leggibili su card card bianca.

**Fix storico**: PR #282 ha cambiato tutti i 4 variants. Se aggiungi un nuovo variant, applica lo stesso pattern.

### Q: `data-testid` non appare nei DOM

**Root cause noto (PR #282)**: la prop `'data-testid'?` deve essere esplicitamente destructurata e forwardata al root element di ciascuna variant.

**Fix**: in ogni `VariantCard.tsx`:

```tsx
export function VariantCard(props: MeepleCardProps) {
  const { entity, title, ..., className = '' } = props;
  const testId = props['data-testid'];  // ← esplicito

  return (
    <div
      data-entity={entity}
      data-testid={testId}            // ← forward
      // ...
    >
  );
}
```

### Q: `MobileCardLayout` non si vede su desktop

**Root cause**: il componente ha `md:hidden` hardcoded nel className (intenzionale: è mobile-only in produzione).

**Fix per dev page**: passa `className="md:!flex"` come override:

```tsx
<MobileCardLayout className="md:!flex" cards={...} />
```

Il `!flex` con modifier `md:` forza `display: flex !important` a md+ breakpoint, bypassando il `md:hidden`.

### Q: Flip card non gira al click

**Check**:
1. `FlipCard` richiede sia `front` che `back` come props
2. Default `trigger="card"` — click ovunque sulla card gira
3. Con `trigger="button"` — solo il 🔄 button gira
4. Transizione è 600ms, aspetta prima di cliccare di nuovo

**Debug**:
```tsx
<FlipCard trigger="card" front={<div>FRONT</div>} back={<div>BACK</div>} />
```

Se anche questo non gira, problema è CSS `perspective` e `transform-style`. Verifica DevTools.

---

## Riferimenti

- **Admin mockups**: `admin-mockups/meeple-card-*.html` (4 file fonte visivo)
  - `summary-render.html` — overview completo (Section 1-7)
  - `visual-test.html` — grid/list/table/flip/carousel/multi-entity (Section 1-6)
  - `nav-buttons-mockup.html` — nav pattern 0/1/N/Chat + disabled states
  - `real-app-render.html` — come gli adapters appaiono in produzione
  - `mobile-card-layout-mockup.html` — mobile focus mode + drawer

- **Componenti** (src): `apps/web/src/components/ui/data-display/meeple-card/`

- **Dev page** (showcase): `apps/web/src/app/(public)/dev/meeple-card/page.tsx`

- **Screenshot artifacts**: `docs/frontend/screenshots/meeplecard-dev/` + `README.md`

- **Playwright script**: `apps/web/scripts/meeplecard-flip-backs-verify.mjs`

- **Design tokens CSS**: `apps/web/src/styles/design-tokens.css` (cerca `--mc-*`)

- **Related ADRs**: `docs/architecture/adr/` — cerca "MeepleCard" o "card rewrite"

### PR storiche (riferimento implementazione)

| PR | Contenuto |
|---|---|
| #261 | MeepleCard rewrite from mockups (baseline) |
| #266 | Default Game Toolkit Drawer |
| #268 | NavFooter foundation + 3 new adapters |
| #278 | Badge rendering + 6 edge-case consumers |
| #279 | Phase 7 verification + completion summary |
| #282 | Dev page showcase + mobile preview + badge contrast |
| #285 | EntityTable component (closes Table View gap) |
| #287 | FlipBack + 8 entity flip backs + mockup alignment |
| #288 | Visual verification script + screenshot artifacts |

### Comandi rapidi

```bash
# Start dev
cd apps/web && PORT=3000 pnpm dev

# Verify
curl -sf -o /dev/null -w "%{http_code}\n" http://localhost:3000/dev/meeple-card

# Screenshot regen (docs mode)
cd apps/web && \
  FORMAT=jpeg SCALE=1 QUALITY=85 \
  OUT_DIR=../../docs/frontend/screenshots/meeplecard-dev \
  node scripts/meeplecard-flip-backs-verify.mjs

# Typecheck + lint
cd apps/web && pnpm typecheck && pnpm lint

# Kill dev server
cmd //c "netstat -ano | findstr :3000 | findstr LISTENING"  # get PID
cmd //c "taskkill /F /PID <PID>"
```

---

**Ultima modifica**: 2026-04-08 · **Maintainer**: Frontend team
