# MeepleCard Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ridisegnare MeepleCard con frame "Warm Heritage MTG", dimensioni fisse 5:7, Symbol Strip e metriche entity-specific.

**Architecture:** Nuovo componente `SymbolStrip` inserito nella anatomia della card. Design tokens CSS aggiunti a `meeple-card-styles.ts`. Tutti i variant aggiornati con dimensioni fisse. Componenti mana (ManaCostBar, ManaLinkFooter) preservati invariati.

**Tech Stack:** React 19, TypeScript, Tailwind CSS 4, CVA (class-variance-authority), Vitest

**Spec:** `docs/superpowers/specs/2026-04-02-meeplecard-redesign-design.md`

---

## File Map

### Nuovi file
- `apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.tsx` — chip identità + metric pills
- `apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx` — test unitari

### File modificati
- `apps/web/src/components/ui/data-display/meeple-card-styles.ts` — token frame + CVA dimensioni
- `apps/web/src/components/ui/data-display/meeple-card/types.ts` — nuove props symbol strip
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` — frame + dimensioni fisse
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx` — frame + dimensioni fisse
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx` — frame + dimensioni fisse
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx` — frame + dimensioni fisse
- `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx` — thumbnail fissa
- `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx` — test dimensioni + frame
- `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardList.test.tsx` — test thumbnail

### File NON modificati (preservati)
- `ManaCostBar.tsx`, `ManaLinkFooter.tsx`, `ManaSymbol.tsx`, `mana-config.ts`
- `MeepleCard.tsx` (router) — nessuna modifica necessaria

---

## Task 1: Design tokens e CVA dimensioni

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

Prima di toccare qualsiasi variante, definiamo i token condivisi.

- [ ] **Step 1: Leggi il file corrente**

```bash
# Leggi le prime 80 righe per capire la struttura esistente
# apps/web/src/components/ui/data-display/meeple-card-styles.ts
```

Cerca: `entityColors`, `cardVariants`, eventuali `cva(` calls.

- [ ] **Step 2: Aggiungi token frame e mappa dimensioni**

Aggiungi dopo le import esistenti e prima di `entityColors`:

```typescript
// ─── Warm Heritage MTG Frame Tokens ────────────────────────────────────────

export const CARD_FRAME = {
  outerBorderGradient: 'linear-gradient(180deg, #c8963a 0%, #e8b84b 50%, #c8963a 100%)',
  innerBorder: 'rgba(200, 150, 58, 0.35)',
  background: 'radial-gradient(ellipse at center, #1a1208 0%, #0d0905 100%)',
  framePadding: '3px',
  cornerRadiusOuter: '8px',
  cornerRadiusInner: '5px',
} as const;

// ─── Fixed Card Dimensions (5:7 aspect ratio) ──────────────────────────────

export const CARD_DIMENSIONS = {
  grid:     { width: 200, height: 280 },
  compact:  { width: 160, height: 224 },
  featured: { width: 280, height: 392 },
  hero:     { width: 360, height: 504 },
  list:     { width: 80,  height: 112 },
  expanded: { width: 320, height: 448 },
} as const;

// ─── Section Heights (px, within grid card) ────────────────────────────────

export const CARD_SECTION_HEIGHTS = {
  titleBar:    32,
  symbolStrip: 26,
  footer:      28,
  bottomBar:   24,
  // artBox e textBox: spazio rimanente calcolato dinamicamente
} as const;
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(meeple-card): add Warm Heritage MTG frame tokens and fixed card dimensions"
```

---

## Task 2: Nuove props in types.ts

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 1: Leggi l'interfaccia MeepleCardProps corrente**

```bash
# Leggi apps/web/src/components/ui/data-display/meeple-card/types.ts
# Cerca il blocco MeepleCardProps e le sezioni di feature flags
```

- [ ] **Step 2: Aggiungi il blocco Symbol Strip props**

Trova il commento di sezione più vicino alla fine di `MeepleCardProps` e aggiungi dopo:

```typescript
  // ─── Symbol Strip — Identity Chips ─────────────────────────────────────
  /** Primo chip identità (es. "Strategia", "Euro", "Worker Placement") */
  identityChip1?: string;
  /** Secondo chip identità (es. "Cooperativo", "Esperto") */
  identityChip2?: string;

  // ─── Symbol Strip — Metric Pills (entity-specific) ──────────────────────
  // Game metrics
  /** Es. "2-4" o "1-6" */
  playerCountDisplay?: string;
  /** Es. "45min" o "2h" */
  playTimeDisplay?: string;

  // Player metrics
  gamesPlayed?: number;
  /** Win rate 0-100 */
  winRate?: number;

  // Session metrics
  winnerScore?: string;
  sessionDate?: string;

  // Agent metrics
  conversationCount?: number;
  /** Accuracy 0-100 */
  agentAccuracy?: number;
  linkedKbCount?: number;

  // KnowledgeBase metrics
  pageCount?: number;
  chunkCount?: number;

  // ─── Bottom Bar ──────────────────────────────────────────────────────────
  bottomStatLabel?: string;
  bottomStatValue?: string;
```

- [ ] **Step 3: Verifica TypeScript**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```

Expected: nessun errore nuovo.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts
git commit -m "feat(meeple-card): add symbol strip and metric props to MeepleCardProps"
```

---

## Task 3: Componente SymbolStrip

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx`

- [ ] **Step 1: Scrivi i test failing**

```typescript
// apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx
import { render, screen } from '@testing-library/react';
import { SymbolStrip } from './SymbolStrip';

describe('SymbolStrip', () => {
  it('renders identity chips when provided', () => {
    render(<SymbolStrip entity="game" identityChip1="Strategia" identityChip2="Euro" />);
    expect(screen.getByText('Strategia')).toBeInTheDocument();
    expect(screen.getByText('Euro')).toBeInTheDocument();
  });

  it('renders game metric pills', () => {
    render(
      <SymbolStrip
        entity="game"
        playerCountDisplay="2-4"
        playTimeDisplay="45min"
      />
    );
    expect(screen.getByText(/2-4/)).toBeInTheDocument();
    expect(screen.getByText(/45min/)).toBeInTheDocument();
  });

  it('renders player metric pills', () => {
    render(
      <SymbolStrip
        entity="player"
        gamesPlayed={42}
        winRate={67}
      />
    );
    expect(screen.getByText(/42/)).toBeInTheDocument();
    expect(screen.getByText(/67%/)).toBeInTheDocument();
  });

  it('renders session metric pills', () => {
    render(
      <SymbolStrip
        entity="session"
        winnerScore="128 pts"
        sessionDate="12 Mar"
      />
    );
    expect(screen.getByText(/128 pts/)).toBeInTheDocument();
    expect(screen.getByText(/12 Mar/)).toBeInTheDocument();
  });

  it('renders agent metric pills with 3 pills', () => {
    render(
      <SymbolStrip
        entity="agent"
        conversationCount={34}
        agentAccuracy={94}
        linkedKbCount={3}
      />
    );
    expect(screen.getByText(/34/)).toBeInTheDocument();
    expect(screen.getByText(/94%/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });

  it('renders kb metric pills', () => {
    render(
      <SymbolStrip
        entity="kb"
        pageCount={48}
        chunkCount={312}
      />
    );
    expect(screen.getByText(/48/)).toBeInTheDocument();
    expect(screen.getByText(/312/)).toBeInTheDocument();
  });

  it('renders nothing when no chips or pills provided', () => {
    const { container } = render(<SymbolStrip entity="game" />);
    // Still renders the container but chips/pills are absent
    expect(container.querySelector('[data-symbol-strip]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui i test — devono fallire**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx 2>&1 | tail -10
```

Expected: FAIL — "Cannot find module './SymbolStrip'"

- [ ] **Step 3: Implementa SymbolStrip**

```tsx
// apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.tsx
import { memo } from 'react';
import { type MeepleEntityType } from '../mana/mana-config';

interface SymbolStripProps {
  entity: MeepleEntityType | string;
  // Identity chips
  identityChip1?: string;
  identityChip2?: string;
  // Game metrics
  playerCountDisplay?: string;
  playTimeDisplay?: string;
  // Player metrics
  gamesPlayed?: number;
  winRate?: number;
  // Session metrics
  winnerScore?: string;
  sessionDate?: string;
  // Agent metrics
  conversationCount?: number;
  agentAccuracy?: number;
  linkedKbCount?: number;
  // KnowledgeBase metrics
  pageCount?: number;
  chunkCount?: number;
}

function MetricPill({ icon, value }: { icon: string; value: string | number }) {
  return (
    <span className="inline-flex items-center gap-0.5 rounded px-1.5 py-0.5 text-[10px] font-mono bg-black/40 border border-white/10 text-white/80 whitespace-nowrap">
      <span>{icon}</span>
      <span>{value}</span>
    </span>
  );
}

function IdentityChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium bg-white/10 border border-white/20 text-white/70 whitespace-nowrap">
      {label}
    </span>
  );
}

function GameMetrics({ playerCountDisplay, playTimeDisplay }: Pick<SymbolStripProps, 'playerCountDisplay' | 'playTimeDisplay'>) {
  return (
    <>
      {playerCountDisplay && <MetricPill icon="👥" value={playerCountDisplay} />}
      {playTimeDisplay && <MetricPill icon="⏱" value={playTimeDisplay} />}
    </>
  );
}

function PlayerMetrics({ gamesPlayed, winRate }: Pick<SymbolStripProps, 'gamesPlayed' | 'winRate'>) {
  return (
    <>
      {gamesPlayed !== undefined && <MetricPill icon="🎮" value={gamesPlayed} />}
      {winRate !== undefined && <MetricPill icon="🏆" value={`${winRate}%`} />}
    </>
  );
}

function SessionMetrics({ winnerScore, sessionDate }: Pick<SymbolStripProps, 'winnerScore' | 'sessionDate'>) {
  return (
    <>
      {winnerScore && <MetricPill icon="🏆" value={winnerScore} />}
      {sessionDate && <MetricPill icon="📅" value={sessionDate} />}
    </>
  );
}

function AgentMetrics({ conversationCount, agentAccuracy, linkedKbCount }: Pick<SymbolStripProps, 'conversationCount' | 'agentAccuracy' | 'linkedKbCount'>) {
  return (
    <>
      {conversationCount !== undefined && <MetricPill icon="💬" value={conversationCount} />}
      {agentAccuracy !== undefined && <MetricPill icon="🎯" value={`${agentAccuracy}%`} />}
      {linkedKbCount !== undefined && <MetricPill icon="📚" value={linkedKbCount} />}
    </>
  );
}

function KbMetrics({ pageCount, chunkCount }: Pick<SymbolStripProps, 'pageCount' | 'chunkCount'>) {
  return (
    <>
      {pageCount !== undefined && <MetricPill icon="📄" value={pageCount} />}
      {chunkCount !== undefined && <MetricPill icon="🔍" value={chunkCount} />}
    </>
  );
}

function EntityMetrics(props: SymbolStripProps) {
  switch (props.entity) {
    case 'game':
      return <GameMetrics playerCountDisplay={props.playerCountDisplay} playTimeDisplay={props.playTimeDisplay} />;
    case 'player':
      return <PlayerMetrics gamesPlayed={props.gamesPlayed} winRate={props.winRate} />;
    case 'session':
      return <SessionMetrics winnerScore={props.winnerScore} sessionDate={props.sessionDate} />;
    case 'agent':
      return <AgentMetrics conversationCount={props.conversationCount} agentAccuracy={props.agentAccuracy} linkedKbCount={props.linkedKbCount} />;
    case 'kb':
      return <KbMetrics pageCount={props.pageCount} chunkCount={props.chunkCount} />;
    default:
      return null;
  }
}

export const SymbolStrip = memo(function SymbolStrip(props: SymbolStripProps) {
  const { identityChip1, identityChip2 } = props;

  return (
    <div
      data-symbol-strip
      className="flex items-center justify-between gap-1 px-2 bg-black/50 border-t border-b border-white/5"
      style={{ height: '26px' }}
    >
      {/* Left: Identity Chips */}
      <div className="flex items-center gap-1 min-w-0 overflow-hidden">
        {identityChip1 && <IdentityChip label={identityChip1} />}
        {identityChip2 && <IdentityChip label={identityChip2} />}
      </div>

      {/* Right: Metric Pills */}
      <div className="flex items-center gap-1 shrink-0">
        <EntityMetrics {...props} />
      </div>
    </div>
  );
});
```

- [ ] **Step 4: Esegui i test — devono passare**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx 2>&1 | tail -15
```

Expected: PASS tutti e 7 i test.

- [ ] **Step 5: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "symbol" | head -10
```

Expected: nessun errore.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.tsx \
        apps/web/src/components/ui/data-display/meeple-card-features/SymbolStrip.test.tsx
git commit -m "feat(meeple-card): add SymbolStrip component with entity-specific metric pills"
```

---

## Task 4: Frame CSS helper

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

Aggiungiamo una funzione che costruisce i className/style del frame, da usare in tutti i variant.

- [ ] **Step 1: Leggi l'attuale `meeple-card-styles.ts` per la sezione entityColors**

```bash
# Cerca entityColors e getEntityColor in meeple-card-styles.ts
```

- [ ] **Step 2: Aggiungi getCardFrameStyle**

Aggiungi dopo le costanti CARD_FRAME e CARD_DIMENSIONS già inserite nel Task 1:

```typescript
/**
 * Restituisce style inline per il frame Warm Heritage MTG.
 * Usa un pseudo-border via box-shadow multipli per il bordo doppio ambra.
 */
export function getCardFrameStyle(
  variant: keyof typeof CARD_DIMENSIONS
): React.CSSProperties {
  const dim = CARD_DIMENSIONS[variant];
  return {
    width: `${dim.width}px`,
    height: `${dim.height}px`,
    background: CARD_FRAME.background,
    borderRadius: CARD_FRAME.cornerRadiusOuter,
    // Bordo doppio: outer gold + inner subtle
    boxShadow: [
      `0 0 0 1px #c8963a`,           // outer gold border
      `0 0 0 2px #0d0905`,           // gap frame
      `0 0 0 3px rgba(200,150,58,0.35)`, // inner amber border
      `0 4px 20px rgba(0,0,0,0.7)`,  // card shadow
    ].join(', '),
    overflow: 'hidden',
    position: 'relative',
    flexShrink: 0,
    aspectRatio: '5/7',
  };
}
```

- [ ] **Step 3: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -20
```

Expected: nessun errore.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git commit -m "feat(meeple-card): add getCardFrameStyle helper for Warm Heritage MTG frame"
```

---

## Task 5: MeepleCardGrid — frame + symbol strip

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx`

- [ ] **Step 1: Leggi MeepleCardGrid corrente**

```bash
# Leggi apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
```

Nota: struttura attuale del render, dove è il title bar, dove è l'art box, eventuali wrapper div.

- [ ] **Step 2: Scrivi test failing per dimensioni e symbol strip**

Apri `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx` e aggiungi:

```typescript
describe('MeepleCardGrid — Warm Heritage MTG', () => {
  it('has fixed width and height', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        identityChip1="Strategia"
        playerCountDisplay="1-5"
        playTimeDisplay="40-70min"
      />
    );
    const card = document.querySelector('[data-card-root]');
    expect(card).toHaveStyle({ width: '200px', height: '280px' });
  });

  it('renders SymbolStrip', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        identityChip1="Strategia"
        playerCountDisplay="1-5"
        playTimeDisplay="40min"
      />
    );
    expect(document.querySelector('[data-symbol-strip]')).toBeInTheDocument();
  });

  it('shows entity pip in title bar (ManaCostBar)', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        relatedTypes={['session', 'kb']}
      />
    );
    expect(document.querySelector('[data-mana-pip="session"]')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Esegui test — devono fallire**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx 2>&1 | tail -15
```

Expected: FAIL.

- [ ] **Step 4: Aggiorna MeepleCardGrid**

Struttura target del componente (adatta al codice esistente — non rimuovere feature flags):

```tsx
import { getCardFrameStyle, CARD_SECTION_HEIGHTS } from '../meeple-card-styles';
import { SymbolStrip } from '../../meeple-card-features/SymbolStrip';

// Nel root div del componente:
// - Aggiungi data-card-root
// - Sostituisci le classi di sizing con getCardFrameStyle('grid')
// - Inserisci <SymbolStrip> dopo l'art box e prima del text box

// Root container (sostituisce il div principale):
<div
  data-card-root
  className="flex flex-col overflow-hidden select-none"
  style={getCardFrameStyle('grid')}
>
  {/* Title Bar — ~32px */}
  <div
    className="relative flex items-center justify-between px-2 shrink-0 bg-black/60 backdrop-blur-sm"
    style={{ height: `${CARD_SECTION_HEIGHTS.titleBar}px` }}
  >
    <span className="text-xs font-bold text-white/90 truncate pr-2 font-quicksand">
      {title}
    </span>
    {/* ManaCostBar esistente — nessuna modifica */}
    {relatedTypes && relatedTypes.length > 0 && (
      <ManaCostBar relatedTypes={relatedTypes} />
    )}
  </div>

  {/* Art Box — ~38% dello spazio flessibile */}
  <div className="relative shrink-0" style={{ height: '104px' }}>
    {/* immagine esistente */}
  </div>

  {/* Symbol Strip — ~26px */}
  <SymbolStrip
    entity={entity}
    identityChip1={identityChip1}
    identityChip2={identityChip2}
    playerCountDisplay={playerCountDisplay}
    playTimeDisplay={playTimeDisplay}
    gamesPlayed={gamesPlayed}
    winRate={winRate}
    winnerScore={winnerScore}
    sessionDate={sessionDate}
    conversationCount={conversationCount}
    agentAccuracy={agentAccuracy}
    linkedKbCount={linkedKbCount}
    pageCount={pageCount}
    chunkCount={chunkCount}
  />

  {/* Text Box — flex grow */}
  <div className="flex-1 overflow-hidden px-2 py-1">
    {/* contenuto testo esistente */}
  </div>

  {/* ManaLinkFooter — ~28px */}
  {linkedEntities && linkedEntities.length > 0 && (
    <div style={{ height: `${CARD_SECTION_HEIGHTS.footer}px` }}>
      <ManaLinkFooter linkedEntities={linkedEntities} sourceEntityId={id} />
    </div>
  )}

  {/* Bottom Bar — ~24px */}
  <div
    className="flex items-center justify-between px-2 shrink-0 bg-black/70 border-t border-white/5"
    style={{ height: `${CARD_SECTION_HEIGHTS.bottomBar}px` }}
  >
    <span className="text-[9px] text-white/40 truncate">
      {publisher}{releaseYear ? ` · ${releaseYear}` : ''}
    </span>
    {bottomStatValue && (
      <span className="text-[9px] text-white/60 shrink-0">
        {bottomStatLabel ? `${bottomStatLabel} ` : ''}{bottomStatValue}
      </span>
    )}
  </div>
</div>
```

**Nota**: le sezioni art box e text box mantengono il codice esistente (immagine, overlay, rating, badge ecc.) — si tratta solo di inserire i wrapper con le altezze corrette.

- [ ] **Step 5: Esegui test**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx 2>&1 | tail -15
```

Expected: PASS.

- [ ] **Step 6: Smoke test visivo**

```bash
cd apps/web && pnpm dev
# Apri http://localhost:3000 → vai in una pagina con GameCard o griglia giochi
# Verifica: card 200×280px, frame ambra, symbol strip visibile
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx \
        apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx
git commit -m "feat(meeple-card): apply Warm Heritage MTG frame and SymbolStrip to grid variant"
```

---

## Task 6: MeepleCardCompact

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx`

- [ ] **Step 1: Leggi MeepleCardCompact**

```bash
# Leggi apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx
```

- [ ] **Step 2: Applica getCardFrameStyle('compact') e SymbolStrip**

Stesso pattern del Task 5. Dimensioni: `160px × 224px`.

Nel compact il testo è più piccolo (`text-[10px]` per il titolo) e la symbol strip può essere omessa se non c'è spazio. Logica: mostra SymbolStrip solo se almeno una prop chips/metrics è presente.

```tsx
// Root container
<div
  data-card-root
  className="flex flex-col overflow-hidden select-none"
  style={getCardFrameStyle('compact')}
>
  {/* stessa struttura del grid, font ridotti */}
</div>
```

- [ ] **Step 3: Esegui test esistenti**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/MeepleCardCompact.test.tsx 2>&1 | tail -10
```

Expected: PASS (o nessun test esistente da aggiornare se già passa).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx
git commit -m "feat(meeple-card): apply frame and dimensions to compact variant"
```

---

## Task 7: MeepleCardFeatured + MeepleCardHero

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx`

- [ ] **Step 1: Applica getCardFrameStyle('featured') a MeepleCardFeatured**

Dimensioni: `280px × 392px`. Stessa struttura Grid, font leggermente più grandi.

```tsx
style={getCardFrameStyle('featured')}
```

- [ ] **Step 2: Applica getCardFrameStyle('hero') a MeepleCardHero**

Dimensioni: `360px × 504px`. Font più grandi, padding aumentato.

```tsx
style={getCardFrameStyle('hero')}
```

- [ ] **Step 3: Test esistenti**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/ 2>&1 | tail -20
```

Expected: tutti PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx \
        apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx
git commit -m "feat(meeple-card): apply frame and dimensions to featured and hero variants"
```

---

## Task 8: MeepleCardList — thumbnail fissa

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardList.test.tsx`

Il variant `list` mostra la card come riga orizzontale: thumbnail `80×112px` a sinistra + contenuto a destra.

- [ ] **Step 1: Scrivi test failing**

```typescript
describe('MeepleCardList — thumbnail', () => {
  it('thumbnail has fixed dimensions', () => {
    render(<MeepleCardList entity="game" title="Wingspan" />);
    const thumb = document.querySelector('[data-card-thumbnail]');
    expect(thumb).toHaveStyle({ width: '80px', height: '112px' });
  });
});
```

- [ ] **Step 2: Esegui — deve fallire**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/MeepleCardList.test.tsx 2>&1 | tail -10
```

- [ ] **Step 3: Aggiorna MeepleCardList**

```tsx
// Thumbnail box (sinistra della riga):
<div
  data-card-thumbnail
  className="shrink-0 overflow-hidden"
  style={{
    ...getCardFrameStyle('list'),
    borderRadius: '6px',
  }}
>
  {/* immagine o placeholder */}
</div>
```

- [ ] **Step 4: Test passa**

```bash
cd apps/web && pnpm test src/components/ui/data-display/meeple-card/__tests__/MeepleCardList.test.tsx 2>&1 | tail -10
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx \
        apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardList.test.tsx
git commit -m "feat(meeple-card): fix list variant thumbnail to 80×112px with frame"
```

---

## Task 9: Test suite e regressioni

**Files:**
- Test: `apps/web/src/components/ui/data-display/meeple-card/__tests__/`

- [ ] **Step 1: Esegui l'intera test suite dei componenti card**

```bash
cd apps/web && pnpm test src/components/ui/data-display/ 2>&1 | tail -30
```

Expected: tutti PASS. Se ci sono fallimenti, correggi prima di procedere.

- [ ] **Step 2: Typecheck globale**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```

Expected: 0 errori.

- [ ] **Step 3: Lint**

```bash
cd apps/web && pnpm lint src/components/ui/data-display/ 2>&1 | tail -15
```

Expected: 0 errori, 0 warning critici.

- [ ] **Step 4: Commit finale con tag**

```bash
git add -p  # review finale
git commit -m "test(meeple-card): verify full test suite passes after Warm Heritage MTG redesign"
```

---

## Task 10: Aggiorna CLAUDE.md — Card Components section

**Files:**
- Modify: `CLAUDE.md` (sezione "Card Components")

- [ ] **Step 1: Aggiorna la sezione Card Components**

Trova e aggiorna il blocco in CLAUDE.md:

```markdown
### Card Components

- Use `MeepleCard` for all entity displays (games, players, collections, events)
- Do NOT use deprecated `GameCard` or `PlayerCard` components
- Entity types: game (orange), player (purple), collection (teal), event (rose)
- Variants: grid (default), list, compact, featured, hero
- **Dimensions are fixed** — grid: 200×280px, compact: 160×224px, featured: 280×392px, hero: 360×504px, list thumbnail: 80×112px
- Frame style: "Warm Heritage MTG" — ornate amber double border, dark parchment background
- Symbol Strip: replaces type line — left identity chips (text only), right entity metric pills
- ManaCostBar: entity type pips top-right of title bar (position of MTG mana cost)
- ManaLinkFooter: clickable instance links between MeepleCards
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md card components section with Warm Heritage MTG specs"
```

---

## Checklist di accettazione finale

Prima di aprire la PR, verifica tutti i criteri della spec:

- [ ] Card grid: 200×280px esatti, frame ambra visibile
- [ ] Card compact: 160×224px esatti
- [ ] Card featured: 280×392px esatti
- [ ] Card hero: 360×504px esatti
- [ ] Card list thumbnail: 80×112px esatti
- [ ] Symbol strip visibile con chip a sx e pills a dx
- [ ] ManaCostBar in title bar top-right
- [ ] ManaLinkFooter tra text box e bottom bar, cliccabile
- [ ] Bottom bar con publisher + stat chiave
- [ ] Art box pulita, nessun overlay angolare
- [ ] `pnpm test` → PASS su tutta la suite data-display
- [ ] `pnpm typecheck` → 0 errori
- [ ] Nessuna regressione sui componenti mana (ManaCostBar, ManaLinkFooter, ManaSymbol)
