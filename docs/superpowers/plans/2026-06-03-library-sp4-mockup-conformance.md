# Library SP4 Mockup Conformance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allineare la pagina `/library` (LibraryHub) alla spec visuale del mockup canonico `admin-mockups/design_files/sp4-library-desktop.jsx`, riusando i 7 componenti già esistenti (Epic #1585) senza riscrivere business logic.

**Architecture:** Opt B consolidation — 3 PR incrementali precedute da Phase 0 root-cause. Ogni PR è autonomamente shippabile e produce un sottoinsieme di chrome (PR1), content surface (PR2) o overlay (PR3) visualmente conforme. Si riusano i design tokens canonici già caricati (`design-tokens-canonical.css` → `tokens.css` mockup) e non si introducono nuovi primitive. La parent branch è `main-dev` (rif. CLAUDE.md ADR-054).

**Tech Stack:** Next.js 16 App Router · React 19 · Tailwind 4 + shadcn/ui · CSS custom properties (`--c-game`, `--bg-card`, `--f-display`) · Vitest · Playwright (E2E manual smoke) · Mockup canonico in `admin-mockups/design_files/sp4-library-desktop.jsx`.

---

## Mockup Reference Map

Tutti i componenti target sono già implementati. Mappa rapida `componente FE → sezione mockup JSX`:

| FE Component | File | Mockup Section | Mockup Lines |
|---|---|---|---|
| `LibraryHeroDesktop` | `apps/web/src/components/features/library/LibraryHeroDesktop.tsx` | `const LibraryHero` | 35–129 |
| `LibraryTabs` | `apps/web/src/components/features/library/LibraryTabs.tsx` | `const EntityTabBar` | 134–191 |
| `CrossEntityFilters` | `apps/web/src/components/features/library/CrossEntityFilters.tsx` | `const CrossEntityFilters` | 218–310 |
| `LibraryHybridGrid` (+ `MeepleCard` consumers) | `apps/web/src/components/features/library/LibraryHybridGrid.tsx` | `LibraryGrid` + `MeepleCardGrid/List/Compact` | 657–890 |
| `RecentActivityRail` | `apps/web/src/components/features/library/RecentActivityRail.tsx` | `const RecentActivityRail` | 949–1017 |
| `BulkSelectionBar` | `apps/web/src/components/features/library/BulkSelectionBar.tsx` | `const BulkSelectionBar` | 895–944 |
| `AdvancedFiltersDrawer` | `apps/web/src/components/features/library/AdvancedFiltersDrawer/` | `const AdvancedFiltersDrawer` | 312–637 |
| Tokens reference | n/a | `admin-mockups/design_files/tokens.css` | tutto |

Convenzione mockup → semantic tokens:
- `var(--bg)` → `bg-background`
- `var(--bg-card)` → `bg-card`
- `var(--bg-muted)` → `bg-muted`
- `var(--text)` → `text-foreground`
- `var(--text-sec)` → `text-foreground/80` (o `text-muted-foreground` se più appropriato)
- `var(--text-muted)` → `text-muted-foreground`
- `var(--border)` → `border-border`
- `var(--border-light)` → `border-border/60`
- `var(--border-strong)` → `border-border-strong`
- `entityHsl('game')` → `bg-entity-game` / `text-entity-game` / `border-entity-game` (utilities Tailwind già esistenti)
- `var(--f-display)`, `var(--f-mono)` → da verificare se class utility `font-display` / `font-mono` mappa esattamente

Le entity utilities `bg-entity-game`, `text-entity-agent`, etc. sono già garantite da Tier 2 token canonicalization (CLAUDE.md). Per gradienti con alpha (`entityHsl('game', 0.1)`) usare inline `style={{ background: 'hsl(var(--c-game) / 0.1)' }}` oppure utilities `bg-entity-game/10`.

---

## File Structure

### Componenti FE da modificare (markup re-skin, NO logic change)

```
apps/web/src/components/features/library/
├── LibraryHeroDesktop.tsx          # PR1 — hero ridisegnato con eyebrow + gradient + 3 CTA
├── LibraryTabs.tsx                  # PR1 — entity-colored active + animated indicator + icon per tab
├── CrossEntityFilters.tsx           # PR1 — 2-row layout, search shortcut, filter chips, view toggle
├── LibraryHybridGrid.tsx            # PR2 — grid wrapper invariato, ma assicurarsi consumi MeepleCard.grid riallineato
├── RecentActivityRail.tsx           # PR2 — timeline con connecting line + kbd shortcuts box
├── BulkSelectionBar.tsx             # PR3 — dark floating bar, count chip, actions
├── EmptyLibrary.tsx                 # PR2 — first-run state allineato (illustrazione 96 + 3 BGG suggestions)
└── AdvancedFiltersDrawer/           # PR3 — 7 sezioni accordion con icone
```

### MeepleCard primitive (verificare drift)

```
apps/web/src/components/ui/data-display/meeple-card/
├── meeple-card.tsx (o variant grid/list/compact)  # PR2 — verifica top accent bar, cover gradient, entity badge, 3-dot menu
```

### Test impattati (aggiornare selettori DOM se cambiano)

```
apps/web/src/components/features/library/__tests__/
├── LibraryHeroDesktop.test.tsx     # PR1
├── LibraryTabs.test.tsx             # PR1
├── CrossEntityFilters.test.tsx     # PR1
├── LibraryHybridGrid.test.tsx      # PR2
├── RecentActivityRail.test.tsx     # PR2
├── BulkSelectionBar.test.tsx       # PR3
└── (AdvancedFiltersDrawer tests)   # PR3

apps/web/src/app/(authenticated)/library/_components/__tests__/
└── LibraryHub.test.tsx              # tutti i PR (integration test, snapshot DOM)
```

### Investigation artifacts (Phase 0)

```
claudedocs/mockup-verification/library-hub/
├── 2026-06-03-phase0-root-cause.md          # Phase 0 — diagnosi tema + card placeholder
├── 2026-06-03-pr1-pre-screenshot.png        # PR1 baseline
├── 2026-06-03-pr1-post-screenshot.png       # PR1 verification
├── 2026-06-03-pr2-pre-screenshot.png        # PR2 baseline
├── 2026-06-03-pr2-post-screenshot.png       # PR2 verification
└── 2026-06-03-pr3-pre-screenshot.png        # PR3 baseline (drawer aperto + bulk attivo)
└── 2026-06-03-pr3-post-screenshot.png       # PR3 verification
```

Pattern P164 nuova memoria (visual gate rimosso → screenshot reference repo-resident).

---

## Phase 0: Root Cause Investigation

Prima di toccare codice, isolare i 2 segnali anomali nello screenshot baseline (dark theme + card placeholder grandi BB/TM/SI/7W).

### Task 0.1: Verifica root cause tema dark

**Files:**
- Read: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx`
- Read: `apps/web/src/styles/design-tokens-canonical.css`
- Read: `apps/web/src/styles/token-bridge.css`
- Read: `apps/web/src/app/layout.tsx` (root layout, theme provider)
- Create: `claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md`

- [ ] **Step 1: Riproduci lo screenshot in locale**

Run dev:
```bash
cd apps/web && pnpm dev
```
Apri `http://localhost:3000/library`. Verifica:
- `document.documentElement.getAttribute('data-theme')` in browser console → `light` o `dark`?
- `localStorage.getItem('theme')` → vuoto, `light`, o `dark`?

- [ ] **Step 2: Reset preferenza tema utente**

In browser console:
```js
localStorage.removeItem('theme');
document.documentElement.setAttribute('data-theme', 'light');
location.reload();
```

Se la pagina torna chiara → root cause = preferenza utente persistita (NON regressione). Documentare in `phase0-root-cause.md` sezione "Tema".

Se la pagina rimane scura nonostante `data-theme="light"` → drift tokens canonici o regressione `next-themes` config. Approfondire leggendo `apps/web/src/app/layout.tsx` per ThemeProvider.

- [ ] **Step 3: Documenta in phase0-root-cause.md**

Crea file con sezioni:
```markdown
# Phase 0 — Library SP4 Mockup Conformance Root Cause

**Data**: 2026-06-03
**Owner**: <name>
**Status**: investigation

## Tema dark vs light

**Baseline screenshot**: `screenshot/library.png` mostra tema dark.

**Riproduzione locale**:
- `data-theme` letto dal DOM: <valore>
- `localStorage.theme`: <valore>
- Esito reset: <preferenza utente | regressione tokens | regressione next-themes>

**Decisione**: <continuare con scope re-skin only | aggiungere fix tema in PR1>

## Card placeholder grandi BB/TM/SI/7W

(vedi Task 0.2)
```

- [ ] **Step 4: Commit Phase 0 root cause doc (no code changes)**

```bash
git add claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md
git commit -m "docs(library): #1585-followup phase 0 root cause investigation"
```

### Task 0.2: Verifica root cause card placeholder grandi

**Files:**
- Read: `apps/web/src/components/ui/data-display/meeple-card/` (entry point del primitive)
- Read: `apps/web/src/lib/cover-utils.ts` (cover fallback logic + BLOCKED_IMAGE_HOSTS)
- Read: `apps/web/src/components/features/library/LibraryHybridGrid.tsx`
- Update: `claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md`

- [ ] **Step 1: Trova MeepleCard entry point**

Run:
```bash
ls apps/web/src/components/ui/data-display/meeple-card/
```

Identifica file principale e prop `coverUrl`/`imageUrl`/`coverEmoji`/`coverGradient`.

- [ ] **Step 2: Ispeziona placeholder logic**

Read MeepleCard primitive + `cover-utils.ts`. Localizza:
- `shouldUsePlaceholder()` o equivalente
- `BLOCKED_IMAGE_HOSTS` (BGG runtime block)
- Gradient placeholder (es. linear-gradient con entity color + iniziali title)

Confronta con mockup `MeepleCardGrid` (jsx:657–749):
- Cover height: 100px (mockup) vs <attuale>
- Background: `entity.cover` come gradient (es. `linear-gradient(135deg, hsl(140 50% 45%), hsl(120 60% 40%))`)
- Overlay emoji centro 38px (mockup linea 680)
- NO lettere giganti come fallback — il mockup usa **emoji per ogni gioco** (mappato in `DS.games[].coverEmoji`)

- [ ] **Step 3: Diagnosi**

Le card baseline mostrano lettere giganti (BB, TM, SI, 7W) → suggerisce uno di:
- (a) `MeepleCard` primitive fallback usa iniziali del titolo invece di emoji
- (b) Cover image URL non risolto e fallback diverso dal mockup
- (c) Drift markup primitive vs `MeepleCardGrid` mockup (top accent bar + entity badge + 3-dot menu)

Documenta in `phase0-root-cause.md` sezione "Card placeholder":
```markdown
## Card placeholder grandi BB/TM/SI/7W

**Root cause**:
- MeepleCard primitive: <file path>
- Placeholder fallback: <descrizione attuale>
- Gap vs mockup `MeepleCardGrid` (jsx:657–749):
  - Top accent bar 3px entity-colored: <presente | mancante>
  - Cover 100h con emoji + entity badge top-left: <presente | mancante>
  - 3-dot menu top-right (hover-visible): <presente | mancante>
  - Status dot + badge nel footer: <presente | mancante>

**Decisione scope PR2**:
- [ ] MeepleCard primitive necessita modifica markup
- [ ] Solo `LibraryHybridGrid` wrapper passa props diverse
- [ ] Entrambi
```

- [ ] **Step 4: Commit Phase 0 update**

```bash
git add claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md
git commit -m "docs(library): #1585-followup phase 0 card placeholder root cause"
```

### Task 0.3: Decision gate — proceed to PR1

- [ ] **Step 1: Capture baseline screenshots**

Con dev server live, screenshot in `claudedocs/mockup-verification/library-hub/`:
- `2026-06-03-pr1-pre-screenshot.png` — vista corrente con tema forzato light
- `2026-06-03-pr2-pre-screenshot.png` — stessa vista per PR2 (cards visibili)
- `2026-06-03-pr3-pre-screenshot.png` — selezionare 3 card per attivare bulk bar + aprire drawer

- [ ] **Step 2: Commit baseline screenshots**

```bash
git add claudedocs/mockup-verification/library-hub/2026-06-03-pr*-pre-screenshot.png
git commit -m "docs(library): #1585-followup baseline screenshots pr1/pr2/pr3"
```

- [ ] **Step 3: Decisione scope**

Se phase 0 ha rivelato che MeepleCard primitive necessita modifica → confermare con utente prima di PR2 (potrebbe impattare altri 10+ surface in v2-migration-matrix).

Se solo wrapper component necessita re-skin → procedi.

---

## PR 1 — Header Chrome (Hero + Tabs + Filters)

Scope: tre componenti above-the-fold. Ship insieme perché compongono visivamente la "fascia superiore" della pagina.

### Task 1.1: Branch hygiene

- [ ] **Step 1: Verifica HEAD su main-dev clean**

Pattern P76 obbligatorio (CLAUDE.md "Branch Hygiene Rule"):
```bash
git branch --show-current
```
Output atteso: `main-dev`. Se diverso, `git checkout main-dev` prima di procedere.

```bash
git status
```
Output atteso: clean (Phase 0 commits inclusi sono ok).

```bash
git pull --ff-only origin main-dev
```
Deve succeed senza merge.

- [ ] **Step 2: Crea feature branch**

```bash
git checkout -b feature/library-sp4-pr1-header-chrome
git config branch.feature/library-sp4-pr1-header-chrome.parent main-dev
```

### Task 1.2: LibraryHeroDesktop re-skin

**Files:**
- Read: `apps/web/src/components/features/library/LibraryHeroDesktop.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 35–129)
- Modify: `apps/web/src/components/features/library/LibraryHeroDesktop.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/LibraryHeroDesktop.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` (aggiungi `onImportBgg` se necessario)

**Acceptance criteria (estratti dal mockup)**:
- Container: padding `24px 32px` desktop, gradient background `linear-gradient(135deg, hsl(var(--c-game)/0.1) 0%, hsl(var(--c-agent)/0.06) 50%, hsl(var(--c-kb)/0.08) 100%)`, `border-b border-border/60`, `relative overflow-hidden` (mockup linee 36–41)
- Decorative blob in alto-destra (mockup linee 42–47): `position absolute, top -40 right -40, w-60 h-60 rounded-full, radial-gradient(circle, hsl(var(--c-game)/0.1) 0%, transparent 70%), pointer-events-none, aria-hidden`
- Layout flex: 1 colonna text+eyebrow, 1 cluster stats inline, 1 action bar end
- **Eyebrow pill** (NEW, mockup linee 56–65):
  ```tsx
  <div className="mb-2 inline-flex items-center gap-1.5 rounded-full border border-border bg-card px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-muted-foreground">
    <span aria-hidden="true">📚</span>Library · power-user view
  </div>
  ```
- **Titolo** (mockup linee 66–71): `font-display text-[38px] font-extrabold leading-[1.05] tracking-[-0.02em] m-0` con testo da `labels.title`. Compact variant: 26px (mobile)
- **Subtitle** (mockup linee 72–75): `text-[14.5px] font-medium text-muted-foreground` con testo da `labels.subtitle`
- **Stats inline** (mockup linee 79–100): pill formato `flex items-center gap-1.5 rounded-md border border-entity-{type}/20 bg-card px-3 py-1.5`, contenuto `{emoji} {count font-mono font-extrabold text-entity-{type}} {label font-display text-[11.5px] font-bold text-muted-foreground}`
- **Action bar** (mockup linee 102–127, solo non-compact):
  - "+ Aggiungi gioco" primary: `bg-entity-game text-white border-0 rounded-md px-4 py-2 font-display text-[13px] font-extrabold shadow-[0_4px_14px_hsl(var(--c-game)/0.4)]`
  - "↓ Importa BGG" secondary: `bg-card text-foreground border border-border-strong rounded-md px-3.5 py-2 font-display text-[13px] font-bold`
  - "↗" Export icon-only: `bg-card text-foreground border border-border-strong rounded-md px-3 py-2 min-w-[38px]` con `aria-label="Esporta"`

**Props interface update**:
```ts
export interface LibraryHeroDesktopLabels {
  title: string;
  subtitle: string;
  ctaAdd: string;
  ctaImportBgg: string;      // NEW
  ctaExportAriaLabel: string; // NEW
  eyebrow: string;            // NEW (es. "Library · power-user view")
}

export interface LibraryHeroDesktopProps {
  labels: LibraryHeroDesktopLabels;
  stats: readonly LibraryHeroStat[];
  onAddGame: () => void;
  onImportBgg: () => void;    // NEW
  onExport?: () => void;      // NEW (optional, può essere noop iniziale)
}
```

- [ ] **Step 1: Read current LibraryHeroDesktop.tsx**

```bash
cat apps/web/src/components/features/library/LibraryHeroDesktop.tsx
```

Identifica struttura attuale, props interface, classnames, tokens usati.

- [ ] **Step 2: Read mockup LibraryHero (jsx:35–129)**

Punto di riferimento principale. Leggere e annotare:
- Gradient esatto (3 stop)
- Eyebrow pill markup
- Stat pill markup
- Action bar markup (CTA primaria entity-game-colored + secondaria + icon export)

- [ ] **Step 3: Aggiungi failing test per eyebrow pill**

In `apps/web/src/components/features/library/__tests__/LibraryHeroDesktop.test.tsx`:
```tsx
it('renders eyebrow pill with provided label', () => {
  render(
    <LibraryHeroDesktop
      labels={{
        title: 'La tua libreria',
        subtitle: '...',
        ctaAdd: '+ Aggiungi gioco',
        ctaImportBgg: '↓ Importa BGG',
        ctaExportAriaLabel: 'Esporta',
        eyebrow: 'Library · power-user view',
      }}
      stats={[]}
      onAddGame={() => {}}
      onImportBgg={() => {}}
    />
  );
  expect(screen.getByText(/Library · power-user view/)).toBeInTheDocument();
});

it('renders the Importa BGG secondary CTA', () => {
  const onImportBgg = vi.fn();
  render(<LibraryHeroDesktop {...defaultProps} onImportBgg={onImportBgg} />);
  fireEvent.click(screen.getByRole('button', { name: /Importa BGG/i }));
  expect(onImportBgg).toHaveBeenCalledOnce();
});

it('renders the Export icon button with accessible label', () => {
  render(<LibraryHeroDesktop {...defaultProps} />);
  expect(screen.getByRole('button', { name: 'Esporta' })).toBeInTheDocument();
});
```

- [ ] **Step 4: Run failing tests**

```bash
cd apps/web && pnpm test -- --run src/components/features/library/__tests__/LibraryHeroDesktop.test.tsx
```
Expected: FAIL su eyebrow + Importa BGG + Esporta (testid/role assenti).

- [ ] **Step 5: Implementa nuovo markup LibraryHeroDesktop**

Riscrivi component preservando:
- Prop interface estesa (vedi sopra)
- `data-slot="library-hero-desktop"` per integration test selectors
- Accessibilità: `<h1>` per titolo, `aria-label` per export icon, `aria-hidden` per emoji decorative

Implementa markup dal mockup (jsx:35–129) usando Tailwind classes + inline style per gradients alpha-composable. Preserva `useTranslation()` flow nel chiamante (vedi step 7).

- [ ] **Step 6: Run tests, verify pass**

```bash
pnpm test -- --run src/components/features/library/__tests__/LibraryHeroDesktop.test.tsx
```
Expected: ALL PASS.

- [ ] **Step 7: Update LibraryHub.tsx per nuova interfaccia**

In `LibraryHub.tsx:222-244` estendi `heroLabels`:
```tsx
const heroLabels = useMemo<LibraryHeroDesktopLabels>(
  () => ({
    title: t('pages.library.hero.title'),
    subtitle: t('pages.library.hero.subtitle'),
    ctaAdd: t('pages.library.hero.cta.add'),
    ctaImportBgg: t('pages.library.hero.cta.importBgg'),
    ctaExportAriaLabel: t('pages.library.hero.cta.exportAriaLabel'),
    eyebrow: t('pages.library.hero.eyebrow'),
  }),
  [t]
);

const handleImportBgg = useCallback(
  () => router.push('/library?action=import-bgg'),
  [router]
);
```

Wire in JSX:
```tsx
<LibraryHeroDesktop
  labels={heroLabels}
  stats={heroStatRows}
  onAddGame={handleAddGame}
  onImportBgg={handleImportBgg}
/>
```

- [ ] **Step 8: Aggiungi nuove chiavi i18n**

Files:
- `apps/web/src/locales/it.json`
- `apps/web/src/locales/en.json`

In sezione `pages.library.hero`:
```json
{
  "title": "La tua libreria",
  "subtitle": "Tutti i tuoi giochi, agenti e documenti in un posto.",
  "eyebrow": "Library · power-user view",
  "cta": {
    "add": "+ Aggiungi gioco",
    "importBgg": "↓ Importa BGG",
    "exportAriaLabel": "Esporta"
  }
}
```

Inglese: tradurre coerentemente.

- [ ] **Step 9: Run i18n MESSAGES test**

```bash
pnpm test -- --run src/locales/__tests__
```
Expected: PASS (no missing keys).

- [ ] **Step 10: Commit task 1.2**

```bash
git add apps/web/src/components/features/library/LibraryHeroDesktop.tsx \
        apps/web/src/components/features/library/__tests__/LibraryHeroDesktop.test.tsx \
        apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx \
        apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(library): #1585-followup LibraryHeroDesktop reskin to sp4 mockup"
```

### Task 1.3: LibraryTabs re-skin

**Files:**
- Read: `apps/web/src/components/features/library/LibraryTabs.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 134–191)
- Modify: `apps/web/src/components/features/library/LibraryTabs.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/LibraryTabs.test.tsx`

**Acceptance criteria (mockup `EntityTabBar`)**:
- Container: `flex items-center gap-0.5 border-b border-border bg-background overflow-x-auto px-8` (desktop) o `px-4` (mobile), `scrollbar-none`, `role="tablist"` (mockup linee 145–152)
- Tab button (mockup linee 156–180):
  - `inline-flex items-center gap-1.5 px-3.5 py-3 font-display text-[13px] whitespace-nowrap flex-shrink-0 cursor-pointer rounded-t-md border-0`
  - Active: `bg-entity-{ent}/10 text-entity-{ent} font-extrabold`. Inactive: `bg-transparent text-muted-foreground font-bold`
  - Layout: `{icon emoji} {label} {count pill}`
  - Count pill: `px-1.5 py-px rounded-full font-mono text-[9px] font-extrabold tabular-nums`, active `bg-entity-{ent} text-white`, inactive `bg-muted text-muted-foreground`
- **Animated indicator bar** (mockup linee 183–188):
  - `span absolute bottom-[-1px] h-0.5 rounded-t-sm` con `left` e `width` derivati da `refs.current[active].offsetLeft/offsetWidth`
  - Background `hsl(var(--c-{accent}))` dove `accent = active === 'all' ? 'game' : active`
  - Transition: `left 0.3s cubic-bezier(.4,0,.2,1), width 0.3s cubic-bezier(.4,0,.2,1), background 0.3s`
- Tab config richiede `icon` prop NUOVO per ogni tab

**Mapping tab → icon** (da mockup linee 1249–1256, riusare):
```ts
const TAB_ICONS: Record<HybridHubTab, string> = {
  all: '⌗',
  games: '🎲',
  agents: '🤖',
  kb: '📚',
  sessions: '🎯',
  chat: '💬',
};
```

**Mapping tab → entity color**:
```ts
const TAB_ENTITY: Record<HybridHubTab, 'game' | 'agent' | 'kb' | 'session' | 'chat'> = {
  all: 'game',     // accent default
  games: 'game',
  agents: 'agent',
  kb: 'kb',
  sessions: 'session',
  chat: 'chat',
};
```

- [ ] **Step 1: Read current LibraryTabs.tsx**

Identifica `LibraryTabConfig` type, prop signature, eventuali sub-component.

- [ ] **Step 2: Read mockup EntityTabBar (jsx:134–191)**

Annotare:
- useRef + useEffect per measure indicator
- Schema interaction (click → onChange + indicator riposizionato)

- [ ] **Step 3: Aggiungi failing test per icon + active indicator**

```tsx
it('renders each tab with its entity icon', () => {
  render(
    <LibraryTabs
      tabs={[
        { key: 'all', label: 'Tutti', count: 14, icon: '⌗' },
        { key: 'games', label: 'Giochi', count: 9, icon: '🎲' },
      ]}
      active="all"
      onChange={() => {}}
    />
  );
  expect(screen.getByRole('tab', { name: /⌗ Tutti/ })).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: /🎲 Giochi/ })).toBeInTheDocument();
});

it('renders an animated indicator span aligned to the active tab', () => {
  const { container } = render(<LibraryTabs {...defaultProps} active="games" />);
  const indicator = container.querySelector('[data-slot="library-tabs-indicator"]');
  expect(indicator).toBeInTheDocument();
});

it('applies entity-game color to active state for "games" tab', () => {
  render(<LibraryTabs {...defaultProps} active="games" />);
  const gamesTab = screen.getByRole('tab', { name: /Giochi/, selected: true });
  expect(gamesTab.className).toMatch(/text-entity-game/);
});
```

- [ ] **Step 4: Run failing tests**

```bash
pnpm test -- --run src/components/features/library/__tests__/LibraryTabs.test.tsx
```
Expected: FAIL.

- [ ] **Step 5: Implementa nuovo markup LibraryTabs**

- Estendi `LibraryTabConfig` con `icon: string` field
- Implementa useRef map per misurare offset/width
- Implementa indicator span con `data-slot="library-tabs-indicator"`
- Mappa entity color via classname template `text-entity-${ent}` + `bg-entity-${ent}/10`

- [ ] **Step 6: Run tests, verify pass**

```bash
pnpm test -- --run src/components/features/library/__tests__/LibraryTabs.test.tsx
```
Expected: ALL PASS.

- [ ] **Step 7: Update LibraryHub.tsx tabsConfig**

In `LibraryHub.tsx:246-260` aggiungi icon a ogni tab config:
```tsx
const TAB_ICONS: Record<HybridHubTab, string> = {
  all: '⌗', games: '🎲', agents: '🤖',
  kb: '📚', sessions: '🎯', chat: '💬',
};

const tabsConfig = useMemo<readonly LibraryTabConfig<HybridHubTab>[]>(() => {
  // ...countFor invariato
  return HUB_TABS.map(tk => ({
    key: tk,
    label: t(`pages.library.hubTabs.${tk}`),
    count: countFor(tk),
    icon: TAB_ICONS[tk],
  }));
}, [t, hub.totalCounts]);
```

- [ ] **Step 8: Commit task 1.3**

```bash
git add apps/web/src/components/features/library/LibraryTabs.tsx \
        apps/web/src/components/features/library/__tests__/LibraryTabs.test.tsx \
        apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx
git commit -m "feat(library): #1585-followup LibraryTabs entity-aware indicator + icons"
```

### Task 1.4: CrossEntityFilters re-skin

**Files:**
- Read: `apps/web/src/components/features/library/CrossEntityFilters.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 197–310)
- Modify: `apps/web/src/components/features/library/CrossEntityFilters.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` (rimuovere toolbar duplicata)

**Acceptance criteria (mockup `CrossEntityFilters`)**:
- Container: `flex flex-col gap-2 bg-background/80 backdrop-blur-md border-b border-border/60 px-8 py-3` (desktop) o `px-4 py-2.5` compact (mockup linee 218–224)
- **Row 1 — Search** (mockup linee 226–247):
  - Container relative
  - Icon `⌕` posizionata absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground text-sm pointer-events-none
  - Input: `w-full pl-8 pr-3 py-2.5 rounded-md border border-border bg-card text-foreground font-body text-[13px]` con placeholder "Cerca in libreria... (premi /)"
  - **Keyboard hint `kbd`** absolute right-3 top-1/2 -translate-y-1/2: `px-1.5 py-px rounded bg-muted border border-border font-mono text-[10px] font-bold text-muted-foreground` content "/"
  - Listener globale `/` per focus search (use `useEffect` con `keydown` su `document`, evita conflitto con typing in altri input)
- **Row 2 — Filter chips + view toggle** (mockup linee 250–308):
  - Container `flex items-center gap-1.5 overflow-x-auto scrollbar-none`
  - 4 `<FilterChip>` con label uppercase mono + value (es. "STATO Tutti", "GIOCO Tutti", "DATA Sempre", "SORT Recenti")
  - FilterChip schema (mockup linee 197–216):
    ```tsx
    <button className={cn(
      "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 font-display text-[12px] font-bold whitespace-nowrap flex-shrink-0 cursor-pointer",
      hasValue
        ? "bg-entity-game/10 border border-entity-game/30 text-entity-game"
        : "bg-card border border-border text-muted-foreground"
    )}>
      <span className="font-mono text-[9px] uppercase tracking-[0.06em] font-extrabold">{label}</span>
      <span>{value}</span>
      <span aria-hidden className="text-[10px] opacity-60">▾</span>
    </button>
    ```
  - Divider `<span className="w-px h-[22px] bg-border flex-shrink-0 mx-1" />`
  - Button "⚙ Filtri avanzati" con badge count se `activeFilterCount > 0`
  - `<div className="flex-1" />` spacer
  - View toggle radiogroup ▦/☰/≡ (mockup linee 283–308) con active state `bg-entity-game/10 text-entity-game`

**Props update**: aggiungere `view`/`onViewChange` se non già presenti (probabilmente sì).

- [ ] **Step 1: Read current CrossEntityFilters.tsx + LibraryHub.tsx:489-550 toolbar**

L'attuale `LibraryHub.tsx:496-550` duplica search input + sort + view toggle FUORI da `CrossEntityFilters`. Il mockup mette tutto DENTRO `CrossEntityFilters`. Refactor:
- Spostare search input dentro `CrossEntityFilters`
- Rimuovere sort dropdown dal `LibraryHub` toolbar (il mockup ha "SORT Recenti" chip invece)
- Spostare view toggle dentro `CrossEntityFilters`
- Eliminare il div `data-slot="library-toolbar"` separato

- [ ] **Step 2: Read mockup CrossEntityFilters (jsx:218–310)**

Annotare:
- 2-row layout sticky
- Backdrop blur per glass effect
- Keyboard shortcut listener

- [ ] **Step 3: Aggiungi failing test per filter chips + keyboard shortcut**

```tsx
it('renders the 4 cross-entity filter chips', () => {
  render(<CrossEntityFilters {...defaultProps} />);
  expect(screen.getByRole('button', { name: /STATO/ })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /GIOCO/ })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /DATA/ })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /SORT/ })).toBeInTheDocument();
});

it('renders search input with / keyboard hint', () => {
  render(<CrossEntityFilters {...defaultProps} />);
  expect(screen.getByPlaceholderText(/premi \//)).toBeInTheDocument();
  expect(screen.getByText('/', { selector: 'kbd' })).toBeInTheDocument();
});

it('focuses search input when / pressed globally', () => {
  render(<CrossEntityFilters {...defaultProps} />);
  fireEvent.keyDown(document, { key: '/' });
  const search = screen.getByPlaceholderText(/premi \//);
  expect(search).toHaveFocus();
});

it('renders view toggle radiogroup with grid/list/compact', () => {
  render(<CrossEntityFilters {...defaultProps} view="grid" />);
  expect(screen.getByRole('radio', { name: /Grid/i, checked: true })).toBeInTheDocument();
  expect(screen.getByRole('radio', { name: /List/i, checked: false })).toBeInTheDocument();
  expect(screen.getByRole('radio', { name: /Compact/i, checked: false })).toBeInTheDocument();
});
```

- [ ] **Step 4: Run failing tests**

```bash
pnpm test -- --run src/components/features/library/__tests__/CrossEntityFilters.test.tsx
```
Expected: FAIL.

- [ ] **Step 5: Implementa nuovo markup CrossEntityFilters**

- Aggiungi `search`/`onSearchChange`/`view`/`onViewChange` props (se mancano)
- Implementa sub-component `FilterChip` interno
- Aggiungi useRef + useEffect per global `/` listener (verifica `event.target` non sia altro input/textarea)
- Tutti i 4 chip per ora sono stub click handler (apriranno popover in future PR — qui no-op o aprono drawer)

- [ ] **Step 6: Update LibraryHub.tsx — rimuovi toolbar separata**

In `LibraryHub.tsx`:
- Rimuovi `<div data-slot="library-toolbar" ...>` interno (linee 496-550)
- Passa `search`, `setSearch`, `view`, `setView` a `<CrossEntityFilters>`:
```tsx
<CrossEntityFilters
  tab={tab}
  gameStateFilter={gameStateFilter}
  onGameStateFilterChange={setGameStateFilter}
  onMoreFilters={() => setDrawerOpen(true)}
  activeFiltersCount={activeFiltersCount}
  search={query}
  onSearchChange={setQuery}
  view={view}
  onViewChange={setView}
  sortKey={sortKey}
  onSortChange={setSortKey}
/>
```

- [ ] **Step 7: Run tests, verify pass**

```bash
pnpm test -- --run src/components/features/library/__tests__/CrossEntityFilters.test.tsx
pnpm test -- --run src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx
```
Expected: ALL PASS. Eventuali test LibraryHub che referenziavano `library-search-input` o `library-sort-select` separati vanno aggiornati per cercare quegli slot dentro `CrossEntityFilters`.

- [ ] **Step 8: Capture PR1 post-screenshot**

Run dev server, navigare a `/library`, screenshot:
```
claudedocs/mockup-verification/library-hub/2026-06-03-pr1-post-screenshot.png
```

- [ ] **Step 9: Commit task 1.4**

```bash
git add apps/web/src/components/features/library/CrossEntityFilters.tsx \
        apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx \
        apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx \
        apps/web/src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx \
        claudedocs/mockup-verification/library-hub/2026-06-03-pr1-post-screenshot.png
git commit -m "feat(library): #1585-followup CrossEntityFilters 2-row layout with chips and /-shortcut"
```

### Task 1.5: PR1 verification + open

- [ ] **Step 1: Full FE test suite**

```bash
cd apps/web && pnpm test:coverage -- --run
pnpm typecheck
pnpm lint
```
Expected: PASS. Coverage non scenda sotto baseline (85%).

- [ ] **Step 2: Manual smoke test in browser**

Dev server `http://localhost:3000/library`. Verificare:
- Hero: gradient + eyebrow pill + stats inline + 3 CTA ✓
- Tab bar: icon visibili per tab + animated indicator si muove cliccando ✓
- Filtri: 2 righe (search con `/` hint + chips row + view toggle) ✓
- Tema light di default ✓

- [ ] **Step 3: Push e apri PR**

```bash
git push -u origin feature/library-sp4-pr1-header-chrome
gh pr create --base main-dev --title "feat(library): #1585-followup PR1 header chrome reskin to sp4 mockup" \
  --body "$(cat <<'EOF'
## Summary
PR1 della consolidation Opt B per allineare /library al mockup canonico `admin-mockups/design_files/sp4-library-desktop.jsx`.

Componenti re-skinned:
- `LibraryHeroDesktop` — eyebrow pill, gradient background, stats inline, 3 CTA (Add/Import BGG/Export)
- `LibraryTabs` — animated indicator bar, icon per tab, entity-colored active state
- `CrossEntityFilters` — 2-row layout, search con `/` keyboard shortcut, 4 filter chips (STATO/GIOCO/DATA/SORT), view toggle inline

Out of scope (PR2/PR3):
- Grid + MeepleCard re-skin (PR2)
- RecentActivityRail timeline (PR2)
- AdvancedFiltersDrawer 7-section accordion (PR3)
- BulkSelectionBar visual update (PR3)

## Mockup references
- Hero: `sp4-library-desktop.jsx:35-129`
- Tabs: `sp4-library-desktop.jsx:134-191`
- Filters: `sp4-library-desktop.jsx:218-310`

## Phase 0 root cause
Vedi `claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md` per investigazione tema dark + card placeholder.

## Verification
- Pre-screenshot: `claudedocs/mockup-verification/library-hub/2026-06-03-pr1-pre-screenshot.png`
- Post-screenshot: `claudedocs/mockup-verification/library-hub/2026-06-03-pr1-post-screenshot.png`

## Test plan
- [x] Unit tests aggiornati (LibraryHeroDesktop, LibraryTabs, CrossEntityFilters)
- [x] LibraryHub integration test aggiornato per nuovi slot
- [x] i18n MESSAGES test (nuove chiavi `hero.eyebrow`, `hero.cta.importBgg`, `hero.cta.exportAriaLabel`)
- [x] Manual smoke `/library` desktop 1440px

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## PR 2 — Content Surface (Grid + Cards + Rail + Empty)

Scope: la parte centrale e laterale della pagina. Ship insieme perché il grid governa il viewport, le card sono il contenuto, il rail occupa la colonna destra, l'empty state è l'alternativa al grid.

**Pre-requisito**: PR1 mergiata in `main-dev`.

### Task 2.1: Branch hygiene PR2

- [ ] **Step 1: Sync main-dev e crea branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/library-sp4-pr2-content-surface
git config branch.feature/library-sp4-pr2-content-surface.parent main-dev
```

### Task 2.2: MeepleCard primitive verification + reskin (conditional)

> **🚨 Phase 0 decision (2026-06-03)**: Task 2.2 is DEFERRED to follow-up issue https://github.com/meepleAi-app/meepleai-monorepo/issues/1856 per plan-anticipated escalation (72 consumers + 5 structural gaps). PR2 will skip directly from Task 2.1 to Task 2.3. See `claudedocs/mockup-verification/library-hub/2026-06-03-phase0-root-cause.md` for full rationale.

**Pre-decisione**: Se Phase 0 Task 0.2 ha concluso che MeepleCard primitive necessita modifica, eseguire questa task. Se no-op, skippare.

**Files:**
- Read: `apps/web/src/components/ui/data-display/meeple-card/` (find entry)
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 657–749 grid, 751–814 list, 816–841 compact)
- Modify: MeepleCard primitive file/i
- Modify: relativi unit test

**Acceptance criteria (mockup `MeepleCardGrid`)**:
- `<article tabIndex={0}>` container relative, `bg-card border border-border rounded-lg overflow-hidden flex flex-col cursor-pointer`
- Top accent bar 3px (mockup linee 670–674): absolute top-0 left-0 right-0 h-[3px] z-[2] con `bg-entity-{ent}`
- Cover (mockup linee 676–722):
  - Height 100px, background `entity.cover` (gradient HSL entity-colored), flex center, emoji 38px con `drop-shadow`
  - Entity badge top-left (mockup linee 682–693): pill bianco semi-trasparente con emoji + label uppercase mono entity-colored
  - 3-dot menu top-right (mockup linee 710–721, hover-visible via CSS class `.mai-card-menu`)
- Body (mockup linee 724–746):
  - `p-3 flex flex-col gap-1 flex-1`
  - h3 title `font-display text-[13.5px] font-extrabold leading-[1.2] line-clamp-2`
  - subtitle `text-[11px] text-muted-foreground line-clamp-2 leading-[1.4]`
  - `flex-1` spacer
  - Footer status: `flex items-center gap-1.5 pt-1.5 border-t border-border/60` con StatusDot + badge mono uppercase

**Status dot mapping** (mockup linee 642–655):
```ts
const STATUS_COLOR: Record<string, string> = {
  owned: 'hsl(140 50% 45%)',
  active: 'hsl(140 50% 45%)',
  indexed: 'hsl(140 50% 45%)',
  wishlist: 'hsl(38 90% 55%)',
  setup: 'hsl(38 90% 55%)',
  processing: 'hsl(38 90% 55%)',
  archived: 'var(--text-muted)',
  failed: 'hsl(var(--c-danger))',
  inprogress: 'hsl(var(--c-session))',
  paused: 'var(--text-muted)',
  completed: 'hsl(140 50% 45%)',
  idle: 'var(--text-muted)',
};
```

- [ ] **Step 1: Read MeepleCard primitive**

```bash
ls apps/web/src/components/ui/data-display/meeple-card/
cat apps/web/src/components/ui/data-display/meeple-card/<entry-file>.tsx
```

- [ ] **Step 2: Confronta con mockup `MeepleCardGrid` (jsx:657–749)**

Identifica gap esatto rispetto a:
- Top accent bar
- Entity badge top-left con emoji + label
- 3-dot menu hover-visible
- Status dot + badge nel footer

Documenta gap atomicamente in `claudedocs/mockup-verification/library-hub/2026-06-03-meeple-card-gap.md`.

- [ ] **Step 3: Decision gate**

Se gap è ampio (>50 LOC change) E MeepleCard è consumato da altre 10+ surface (vedi `v2-migration-matrix.md`):
- **STOP**: questo è change cross-cutting, richiede issue dedicato. Documenta + apri issue follow-up. Salta a Task 2.3 (wrapper-only).

Se gap è ristretto (es. solo footer status missing):
- Procedi con reskin.

- [ ] **Step 4 (conditional): Reskin MeepleCard primitive con TDD**

Per ogni gap atomico (es. top accent bar):
```tsx
// Test
it('renders a top accent bar with entity color', () => {
  render(<MeepleCard entity="game" {...defaultProps} />);
  const accent = screen.getByTestId('meeple-card-accent-bar');
  expect(accent).toHaveClass('bg-entity-game');
});

// Implementation
<div
  aria-hidden
  data-testid="meeple-card-accent-bar"
  className="absolute top-0 left-0 right-0 h-[3px] z-[2] bg-entity-game"
/>
```

Commit ogni gap atomico separatamente.

- [ ] **Step 5: Commit (conditional)**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/
git commit -m "feat(meeple-card): #1585-followup align grid variant to sp4 mockup"
```

### Task 2.3: LibraryHybridGrid wrapper alignment

**Files:**
- Read: `apps/web/src/components/features/library/LibraryHybridGrid.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 846–890)
- Modify: `apps/web/src/components/features/library/LibraryHybridGrid.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/LibraryHybridGrid.test.tsx`

**Acceptance criteria**:
- Grid view (mockup linee 874–890): `grid grid-cols-4 gap-3` desktop. Mobile compact: `grid-cols-[repeat(auto-fill,minmax(150px,1fr))] gap-2`
- List view (mockup linee 847–858): `flex flex-col gap-1.5` con MeepleCardList per item
- Compact view (mockup linee 859–873): wrapper `bg-card border border-border rounded-lg overflow-hidden` con MeepleCardCompact stacked

- [ ] **Step 1: Read current LibraryHybridGrid.tsx**

- [ ] **Step 2: Read mockup LibraryGrid (jsx:846–890)**

- [ ] **Step 3: Aggiungi test per spacing/layout corretto per view**

```tsx
it('renders grid view as 4-col grid on desktop', () => {
  const { container } = render(
    <LibraryHybridGrid items={mockItems} view="grid" {...defaultProps} />
  );
  const grid = container.querySelector('[data-slot="library-hybrid-grid-container"]');
  expect(grid?.className).toMatch(/grid-cols-4|md:grid-cols-4|lg:grid-cols-4/);
});

it('renders compact view as borderless stacked rows', () => {
  const { container } = render(
    <LibraryHybridGrid items={mockItems} view="compact" {...defaultProps} />
  );
  const container_ = container.querySelector('[data-slot="library-hybrid-grid-container"]');
  expect(container_?.className).toMatch(/border|rounded/);
});
```

- [ ] **Step 4: Run failing tests**

```bash
pnpm test -- --run src/components/features/library/__tests__/LibraryHybridGrid.test.tsx
```

- [ ] **Step 5: Implementa allineamento layout**

Aggiungi `data-slot="library-hybrid-grid-container"` + classnames per view mode (grid/list/compact).

- [ ] **Step 6: Verify tests pass**

- [ ] **Step 7: Commit task 2.3**

```bash
git add apps/web/src/components/features/library/LibraryHybridGrid.tsx \
        apps/web/src/components/features/library/__tests__/LibraryHybridGrid.test.tsx
git commit -m "feat(library): #1585-followup LibraryHybridGrid layout alignment for grid/list/compact"
```

### Task 2.4: RecentActivityRail re-skin

**Files:**
- Read: `apps/web/src/components/features/library/RecentActivityRail.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 949–1024)
- Modify: `apps/web/src/components/features/library/RecentActivityRail.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx`

**Acceptance criteria (mockup `RecentActivityRail`)**:
- Sidebar `<aside>` `w-[280px] flex-shrink-0 bg-card border-l border-border px-4 py-4 overflow-y-auto` (mockup linee 950–954)
- Header (mockup linee 955–969):
  - `flex items-center justify-between mb-3.5`
  - h3 `font-display text-[13px] font-extrabold m-0 flex items-center gap-1.5` con `🕐 Ultime modifiche`
  - Collapse button `w-6 h-6 rounded-sm bg-transparent border border-border text-muted-foreground text-[11px]`
- **Timeline** (mockup linee 970–1003):
  - Per ogni `ActivityItem`:
    - Container `flex gap-2.5 mb-2.5`
    - Left rail: circle 22×22 con entity-colored background `bg-entity-{kind}/[0.14] text-entity-{kind} border border-entity-{kind}/30 rounded-full`, contenuto emoji entity. Sotto: linea verticale `w-[1.5px] flex-1 bg-border min-h-3.5 mt-0.5` (eccetto ultimo item)
    - Right content: 
      - `at` timestamp: `font-mono text-[9px] text-muted-foreground font-bold uppercase tracking-[0.06em]`
      - body: `<strong>{who}</strong> {what} <a className="text-entity-{kind} font-bold no-underline">{refTitle}</a>`
- **Keyboard shortcuts box** (mockup linee 1004–1015):
  - `mt-3 px-3 py-2.5 rounded-md bg-muted text-[11px] text-muted-foreground leading-relaxed`
  - Strong "⌨ Shortcuts"
  - Lista `<kbd>/</kbd> focus search`, `<kbd>f</kbd> filtri avanzati`, `<kbd>?</kbd> tutte le scorciatoie`

- [ ] **Step 1: Read current RecentActivityRail.tsx**

- [ ] **Step 2: Read mockup RecentActivityRail (jsx:949–1017)**

- [ ] **Step 3: Aggiungi failing test per timeline + kbd box**

```tsx
it('renders timeline circles with entity color per kind', () => {
  render(<RecentActivityRail items={[
    { id: '1', kind: 'game', who: 'Mario', what: 'ha aggiunto', ref: 'g-1', at: '5 min fa', refTitle: 'Catan' },
  ]} />);
  const circle = screen.getByText('🎲').closest('div');
  expect(circle?.className).toMatch(/bg-entity-game/);
});

it('renders connecting line between items except last', () => {
  const { container } = render(<RecentActivityRail items={twoItems} />);
  const lines = container.querySelectorAll('[data-slot="activity-rail-connector"]');
  expect(lines.length).toBe(1); // one connector between 2 items
});

it('renders keyboard shortcuts box at the bottom', () => {
  render(<RecentActivityRail items={[]} />);
  expect(screen.getByText(/Shortcuts/i)).toBeInTheDocument();
  expect(screen.getByText('focus search')).toBeInTheDocument();
});
```

- [ ] **Step 4: Run failing tests**

```bash
pnpm test -- --run src/components/features/library/__tests__/RecentActivityRail.test.tsx
```

- [ ] **Step 5: Implementa nuovo markup RecentActivityRail**

Markup conforme al mockup, mantenendo prop `items` + `isLoading` + `error` esistenti.

- [ ] **Step 6: Verify tests pass**

- [ ] **Step 7: Commit task 2.4**

```bash
git add apps/web/src/components/features/library/RecentActivityRail.tsx \
        apps/web/src/components/features/library/__tests__/RecentActivityRail.test.tsx
git commit -m "feat(library): #1585-followup RecentActivityRail timeline + kbd shortcuts box"
```

### Task 2.5: EmptyLibrary re-skin (first-run)

**Files:**
- Read: `apps/web/src/components/features/library/EmptyLibrary.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 1029–1105 first-run, 1107–1134 no-results)
- Modify: `apps/web/src/components/features/library/EmptyLibrary.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/EmptyLibrary.test.tsx`

**Acceptance criteria (mockup `EmptyLibrary kind="first-run"`)**:
- `padding 48px 24px text-center bg-card border border-dashed border-border-strong rounded-xl flex flex-col items-center`
- Illustration 96×96 radial gradient circle con emoji 📚 44px
- h2 `font-display text-xl font-extrabold` titolo
- p subtitle `text-[13.5px] text-muted-foreground` max-w-[380px]
- 2 CTA: primary "+ Aggiungi gioco" entity-game-colored + secondary "↓ Importa BGG"
- Suggestions box: 3-col grid con 3 BGG game cards (icon + title + rating + add button)

- [ ] **Step 1: Read current EmptyLibrary.tsx**

- [ ] **Step 2: Read mockup EmptyLibrary first-run (jsx:1029–1105)**

- [ ] **Step 3: Verifica se "suggestions" è già nello scope FE**

I 3 suggerimenti richiedono BGG hot-games API. Se non esposta nel BE attuale, ridurre scope a placeholder estetico (3 card statiche mock) con comment per follow-up issue.

- [ ] **Step 4: Aggiungi failing test**

```tsx
it('renders illustration, title, subtitle, 2 CTAs for empty kind', () => {
  render(<EmptyLibrary kind="empty" labels={emptyLabels} onAddGame={() => {}} onImportBgg={() => {}} onClearFilters={() => {}} onRetry={() => {}} />);
  expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Aggiungi.*primo gioco/i })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /Importa.*BGG/i })).toBeInTheDocument();
});
```

- [ ] **Step 5: Implementa nuovo markup EmptyLibrary**

Estendi labels interface se serve `ctaImportBgg`. Aggiungi `onImportBgg` callback.

- [ ] **Step 6: Verify tests pass**

- [ ] **Step 7: Update LibraryHub.tsx — passa `onImportBgg` a EmptyLibrary**

- [ ] **Step 8: Commit task 2.5**

```bash
git add apps/web/src/components/features/library/EmptyLibrary.tsx \
        apps/web/src/components/features/library/__tests__/EmptyLibrary.test.tsx \
        apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx
git commit -m "feat(library): #1585-followup EmptyLibrary first-run reskin with import BGG CTA"
```

### Task 2.6: PR2 verification + open

- [ ] **Step 1: Full FE test suite**

```bash
cd apps/web && pnpm test:coverage -- --run && pnpm typecheck && pnpm lint
```

- [ ] **Step 2: Manual smoke test**

`/library` desktop:
- Grid 4-col card con top accent bar entity-colored + emoji cover + entity badge ✓
- Right rail 280px con timeline + kbd shortcuts ✓
- Empty state (clear library or use `?state=empty` override): illustration + 2 CTA + suggestions ✓
- Loading skeleton (override `?state=loading`): cards skeleton ✓

- [ ] **Step 3: Capture post-screenshot**

```
claudedocs/mockup-verification/library-hub/2026-06-03-pr2-post-screenshot.png
```

- [ ] **Step 4: Push + PR**

```bash
git add claudedocs/mockup-verification/library-hub/2026-06-03-pr2-post-screenshot.png
git commit -m "docs(library): #1585-followup pr2 post-screenshot verification"
git push -u origin feature/library-sp4-pr2-content-surface
gh pr create --base main-dev --title "feat(library): #1585-followup PR2 content surface reskin to sp4 mockup" \
  --body "$(cat <<'EOF'
## Summary
PR2 della consolidation Opt B per `/library` mockup conformance.

Componenti re-skinned:
- `LibraryHybridGrid` — layout grid 4-col, list, compact alignment
- `MeepleCard` (conditional, vedi Phase 0) — top accent bar, cover gradient, entity badge, 3-dot menu, status footer
- `RecentActivityRail` — timeline con connecting line + entity-colored circles + kbd shortcuts box
- `EmptyLibrary` — first-run con illustration + 2 CTA + suggestions

## Mockup references
- Grid: `sp4-library-desktop.jsx:846-890`
- Cards: `sp4-library-desktop.jsx:657-841`
- Rail: `sp4-library-desktop.jsx:949-1017`
- Empty: `sp4-library-desktop.jsx:1029-1134`

## Verification
- Post-screenshot: `claudedocs/mockup-verification/library-hub/2026-06-03-pr2-post-screenshot.png`

## Test plan
- [x] Unit tests aggiornati
- [x] State override `?state=empty|loading|filtered-empty|error` verificati visually

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## PR 3 — Interactive Overlays (Drawer + Bulk)

Scope: i due componenti che si attivano on-demand. Ship insieme perché entrambi sono overlay/floating e completano la conformità mockup.

**Pre-requisito**: PR2 mergiata in `main-dev`.

### Task 3.1: Branch hygiene PR3

- [ ] **Step 1: Sync e branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/library-sp4-pr3-overlays
git config branch.feature/library-sp4-pr3-overlays.parent main-dev
```

### Task 3.2: BulkSelectionBar re-skin

**Files:**
- Read: `apps/web/src/components/features/library/BulkSelectionBar.tsx`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 895–944)
- Modify: `apps/web/src/components/features/library/BulkSelectionBar.tsx`
- Modify: `apps/web/src/components/features/library/__tests__/BulkSelectionBar.test.tsx`

**Acceptance criteria (mockup `BulkSelectionBar`)**:
- `<div role="region" aria-label="Selezione multipla">` floating bottom center
- Style: `absolute bottom-4 left-1/2 -translate-x-1/2 px-3.5 py-2.5 rounded-2xl bg-foreground text-background shadow-[0_12px_32px_rgba(0,0,0,0.3)] flex items-center gap-2.5 z-30 max-w-[720px]`
- Animation: `mai-slide-up 0.25s var(--ease-spring)` → equivalente Tailwind `animate-in slide-in-from-bottom`
- Count chip: `px-2 py-0.5 rounded-full bg-entity-game text-white font-mono text-[11px] font-extrabold tabular-nums`
- Label: `font-display text-[12.5px] font-bold flex-1 whitespace-nowrap` content "selezionati" (compact: "sel.")
- 3 action buttons (Archivia/Tag/Esporta) con icon + label hidden compact:
  - `px-2.5 py-1.5 rounded-md bg-white/12 text-inherit border-0 font-display text-[11.5px] font-bold inline-flex items-center gap-1.5`
- Close button "✕": `px-2.5 py-1.5 rounded-md bg-transparent border border-white/20 text-inherit text-xs`

- [ ] **Step 1: Read current BulkSelectionBar.tsx**

- [ ] **Step 2: Read mockup BulkSelectionBar (jsx:895–944)**

- [ ] **Step 3: Aggiungi failing test per styling + actions**

```tsx
it('renders the dark floating bar at bottom center', () => {
  render(<BulkSelectionBar selectedCount={3} labels={bulkLabels} onExitSelectMode={() => {}} onArchive={() => {}} />);
  const bar = screen.getByRole('region', { name: /Selezione multipla|selected/i });
  expect(bar.className).toMatch(/bg-foreground|bg-zinc-900|bg-neutral-900/);
  expect(bar.className).toMatch(/bottom-4|fixed bottom/);
});

it('renders count chip with the selected count', () => {
  render(<BulkSelectionBar selectedCount={3} labels={bulkLabels} {...callbacks} />);
  const chip = screen.getByText('3');
  expect(chip.className).toMatch(/bg-entity-game/);
});
```

- [ ] **Step 4: Run failing tests**

- [ ] **Step 5: Implementa nuovo markup BulkSelectionBar**

- [ ] **Step 6: Verify tests pass**

- [ ] **Step 7: Commit task 3.2**

```bash
git add apps/web/src/components/features/library/BulkSelectionBar.tsx \
        apps/web/src/components/features/library/__tests__/BulkSelectionBar.test.tsx
git commit -m "feat(library): #1585-followup BulkSelectionBar floating dark bar with entity-game count chip"
```

### Task 3.3: AdvancedFiltersDrawer re-skin

**Files:**
- Read: `apps/web/src/components/features/library/AdvancedFiltersDrawer/`
- Read: `admin-mockups/design_files/sp4-library-desktop.jsx` (lines 312–637)
- Modify: file(s) sotto `AdvancedFiltersDrawer/`
- Modify: relativi unit test

**Acceptance criteria (mockup `AdvancedFiltersDrawer`)**:
- Backdrop: `absolute inset-0 bg-black/40 z-50 animate-in fade-in duration-200`
- Aside: `absolute top-0 right-0 bottom-0 w-[400px] bg-background border-l border-border shadow-[-12px_0_40px_rgba(0,0,0,0.18)] z-51 flex flex-col animate-in slide-in-from-right duration-250`
- Mobile compact: `w-full` con `animate-in slide-in-from-bottom`
- Header (mockup linee 577–599):
  - icon box `w-8 h-8 rounded-md bg-entity-agent/[0.12] text-entity-agent flex center text-sm` con ⚙
  - title "Filtri avanzati" `font-display text-sm font-extrabold`
  - subtitle `font-mono text-[10px] text-muted-foreground font-bold uppercase tracking-[0.06em]` content "{count} attivi · scope: library"
  - close button `w-7 h-7 rounded-md bg-muted border-0 text-foreground text-sm`
- Body: `flex-1 overflow-y-auto` con 7 `<DrawerSection>` accordion
- 7 sezioni (mockup linee 319–388):
  1. Stato (chips-multi: Posseduto/Wishlist/In setup/Archiviato)
  2. Tipo entità (chips-multi: Giochi/Agenti/KB/Sessioni/Chat)
  3. Gioco (select-multi con search placeholder)
  4. Periodo (period-quick radio: 7d/30d/1y/Sempre/Range personalizzato)
  5. Tag (chips-multi: Family/Strategy/Coop/Engine builder/...)
  6. Rating (range slider 1–10 step 0.5, default [6,10])
  7. Complessità (chips-multi: Light/Medium/Heavy/Extra heavy)
- Footer (mockup linee 611–633):
  - `px-4 py-3 border-t border-border bg-card flex gap-2`
  - Reset secondary `px-3.5 py-2 rounded-md bg-transparent text-muted-foreground border border-border-strong font-display text-[12.5px] font-bold`
  - Applica primary `flex-1 px-3.5 py-2 rounded-md bg-entity-agent text-white border-0 font-display text-[12.5px] font-extrabold shadow-[0_3px_10px_hsl(var(--c-agent)/0.35)]` con label "Applica" + count

- [ ] **Step 1: Read current AdvancedFiltersDrawer/ entry + sub-components**

```bash
ls apps/web/src/components/features/library/AdvancedFiltersDrawer/
```

- [ ] **Step 2: Read mockup AdvancedFiltersDrawer + DrawerSection (jsx:312–637)**

Annotare le 7 kind di sezione e il render specifico:
- `chips-multi`
- `select-multi`
- `period-quick`
- `range`

- [ ] **Step 3: Diff sezioni attuali vs mockup**

Verifica che ogni sezione esista. Se manca (es. "Complessità"), aggiungere alla schema interna del drawer.

- [ ] **Step 4: Aggiungi failing test per ogni sezione mancante o markup drift**

Per ognuna delle 7 sezioni:
```tsx
it('renders the {kind} section with correct title and icon', () => {
  render(<AdvancedFiltersDrawer open={true} ... />);
  expect(screen.getByRole('button', { name: /{Section title}/i })).toBeInTheDocument();
});
```

Per il rating range:
```tsx
it('renders the rating range with default [6, 10]', () => {
  render(<AdvancedFiltersDrawer open={true} ... />);
  expect(screen.getByText('6')).toBeInTheDocument();
  expect(screen.getByText('10')).toBeInTheDocument();
});
```

- [ ] **Step 5: Implementa nuovo markup AdvancedFiltersDrawer (atomic commits per sezione)**

Re-skin sezione per sezione, commit dopo ognuna se logica:
```bash
git commit -m "feat(library): #1585-followup AdvancedFiltersDrawer section 'Stato' chips-multi reskin"
git commit -m "feat(library): #1585-followup AdvancedFiltersDrawer section 'Periodo' period-quick reskin"
# ... etc
```

- [ ] **Step 6: Verify all tests pass**

```bash
pnpm test -- --run src/components/features/library/__tests__/AdvancedFiltersDrawer
```

- [ ] **Step 7: Final commit drawer (se non già atomici)**

```bash
git add apps/web/src/components/features/library/AdvancedFiltersDrawer/
git commit -m "feat(library): #1585-followup AdvancedFiltersDrawer 7-section accordion reskin"
```

### Task 3.4: PR3 verification + open

- [ ] **Step 1: Full FE test suite**

```bash
cd apps/web && pnpm test:coverage -- --run && pnpm typecheck && pnpm lint
```

- [ ] **Step 2: Manual smoke test**

`/library`:
- Selezionare 3 card → BulkSelectionBar appare in basso con count 3 e azioni ✓
- Click "⚙ Filtri avanzati" → drawer 400px da destra con 7 sezioni accordion ✓
- Toggle filtri → count badge nel header e nel footer aggiornati ✓
- Apply → drawer si chiude, chips nei filtri cross-entity aggiornati ✓

- [ ] **Step 3: Capture post-screenshot**

Screenshot con drawer aperto + 3 card selezionate:
```
claudedocs/mockup-verification/library-hub/2026-06-03-pr3-post-screenshot.png
```

- [ ] **Step 4: Push + PR**

```bash
git add claudedocs/mockup-verification/library-hub/2026-06-03-pr3-post-screenshot.png
git commit -m "docs(library): #1585-followup pr3 post-screenshot verification"
git push -u origin feature/library-sp4-pr3-overlays
gh pr create --base main-dev --title "feat(library): #1585-followup PR3 overlays reskin to sp4 mockup" \
  --body "$(cat <<'EOF'
## Summary
PR3 finale della consolidation Opt B per `/library` mockup conformance.

Componenti re-skinned:
- `BulkSelectionBar` — floating dark bar bottom center, count chip entity-game, 3 actions (Archivia/Tag/Esporta) + close
- `AdvancedFiltersDrawer` — 400px side panel, 7 sezioni accordion (Stato/Entity/Gioco/Periodo/Tag/Rating/Complessità), header con icon + count, footer Reset + Applica

## Mockup references
- BulkSelectionBar: `sp4-library-desktop.jsx:895-944`
- AdvancedFiltersDrawer: `sp4-library-desktop.jsx:312-637`

## Verification
- Post-screenshot (drawer + bulk active): `claudedocs/mockup-verification/library-hub/2026-06-03-pr3-post-screenshot.png`

## Epic closure
Con il merge di PR3, l'allineamento mockup-vs-implementation per `/library` SP4 è completo. La 3-PR sequence chiude il gap visivo identificato nello screenshot baseline.

## Test plan
- [x] Unit tests aggiornati per BulkSelectionBar + AdvancedFiltersDrawer
- [x] Manual smoke: drawer apre/chiude, applica filtri, count badge updates
- [x] Manual smoke: bulk selection mode, 3 card selezionate, actions visibili

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Final Cleanup (after PR3 merge)

### Task 4.1: Memory + matrix bookkeeping

- [ ] **Step 1: Update v2-migration-matrix.md**

`docs/for-developers/frontend/v2-migration-matrix.md` — flippare a `done` le righe Library-related se ancora `pending`.

- [ ] **Step 2: Update mockup conformance audit**

Se esiste `docs/for-developers/audits/` audit live correlato, aggiungere riga "Library hub conformed 2026-06-XX (PR1+PR2+PR3)".

- [ ] **Step 3: Memoria di sessione**

Aggiornare entry `library-hybrid-hub-1585.md` (o crearne uno nuovo `library-sp4-mockup-conformance.md`) con outcome.

- [ ] **Step 4: Commit cleanup**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b chore/library-sp4-bookkeeping
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(library): #1585-followup bookkeeping post-conformance"
git push -u origin chore/library-sp4-bookkeeping
gh pr create --base main-dev --title "chore(library): #1585-followup bookkeeping after sp4 mockup conformance" \
  --body "Matrix + memory bookkeeping after PR1/PR2/PR3 ship.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

---

## Self-Review

### Spec coverage

- ✅ Eyebrow pill hero — Task 1.2
- ✅ Hero gradient + 3 CTA (Add/Import BGG/Export) — Task 1.2
- ✅ Hero stats inline pills entity-colored — Task 1.2
- ✅ Tab bar entity-colored active + animated indicator + icons — Task 1.3
- ✅ Search input con `/` keyboard shortcut — Task 1.4
- ✅ Filter chips row (STATO/GIOCO/DATA/SORT) — Task 1.4
- ✅ View toggle ▦/☰/≡ inline in filters — Task 1.4
- ✅ Grid 4-col layout — Task 2.3
- ✅ MeepleCard top accent + cover gradient + entity badge + 3-dot + status footer — Task 2.2 (conditional)
- ✅ RecentActivityRail timeline + kbd box — Task 2.4
- ✅ EmptyLibrary first-run con illustration + 2 CTA + suggestions — Task 2.5
- ✅ BulkSelectionBar floating dark bar — Task 3.2
- ✅ AdvancedFiltersDrawer 7 sezioni — Task 3.3
- ✅ Phase 0 root cause tema + cards — Task 0.1, 0.2

### Placeholder scan

Nessun "TODO" / "implement later" / "similar to Task N" trovato. Mockup references esatte (file:lines) per ogni componente. Acceptance criteria estratti dal mockup con classnames specifici.

### Type consistency

- `LibraryHeroDesktopLabels` esteso con `ctaImportBgg`, `ctaExportAriaLabel`, `eyebrow` — riusato consistentemente in Task 1.2
- `LibraryTabConfig` esteso con `icon: string` — riusato in Task 1.3
- `CrossEntityFilters` props estese con `search`, `onSearchChange`, `view`, `onViewChange`, `sortKey`, `onSortChange` — riusato in Task 1.4
- `EmptyLibrary` esteso con `onImportBgg` callback + `ctaImportBgg` label — riusato in Task 2.5
- `STATUS_COLOR` mapping definito in Task 2.2, riusato in Task 2.4 (StatusDot logic)

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md`. Two execution options:

**1. Subagent-Driven (recommended)** — Dispatch fresh subagent per task, review between tasks, fast iteration. Best fit for this plan because each Task is bite-sized (<5 min steps each), and inter-task review catches drift early (specialmente Task 0.2 → Task 2.2 conditional gate).

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints. Più rischioso perché context accumula (3 PR + Phase 0).

Quale approccio preferisci?
