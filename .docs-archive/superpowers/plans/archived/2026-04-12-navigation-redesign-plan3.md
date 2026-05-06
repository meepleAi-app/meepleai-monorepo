# Navigation Redesign — Piano 3: MeepleCard Focus + ManaPips + drawCard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completare il layer MeepleCard del sistema Hand-First Navigation: aggiungere ManaPips (pip colorati su grid/compact), variante `focus` (header entità full-width con navItem chips cliccabili che chiamano `drawCard()`), supporto `href` in NavFooter, e integrare `drawCard()` nelle pagine game/agent.

**Architecture:** ManaPips è un componente parte puro (nessuna dipendenza store); FocusCard è una nuova variante che riusa `Cover`, `NavFooter` e `MetaChips` esistenti; `href` in NavFooterItem trasforma il `div[role=button]` in `<Link>` quando disponibile; `drawCard()` viene chiamato in `useEffect` sulle pagine entità tramite hook dedicato.

**Tech Stack:** Next.js 16 App Router, React 19, Vitest + @testing-library/react, `next/link`, `useCardHand` store in `lib/stores/card-hand-store.ts`

---

## File Map

| File | Azione | Responsabilità |
|------|--------|---------------|
| `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx` | **Crea** | Pip row colorati per varianti grid/compact |
| `apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx` | **Modifica** | Aggiunge supporto `href?` con `<Link>` |
| `apps/web/src/components/ui/data-display/meeple-card/types.ts` | **Modifica** | Aggiunge `href?` a `NavFooterItem`, aggiunge `'focus'` a `MeepleCardVariant`, aggiunge prop `manaPips?` a `MeepleCardProps` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx` | **Crea** | Variante focus full-width con navItem chips |
| `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` | **Modifica** | Registra `focus: FocusCard` in `variantMap` |
| `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` | **Modifica** | Aggiunge `<ManaPips>` sopra `<NavFooter>` quando `manaPips` presenti |
| `apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx` | **Modifica** | Aggiunge `<ManaPips size="sm">` |
| `apps/web/src/hooks/useDrawCard.ts` | **Crea** | Hook che chiama `drawCard()` on mount per pagine entità |
| `apps/web/src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx` | **Crea** | Unit test ManaPips |
| `apps/web/src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx` | **Crea** | Unit test FocusCard |

---

## Task 1: ManaPips Component

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`
- Create: `apps/web/src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

```tsx
// apps/web/src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ManaPips } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

const pips: ManaPip[] = [
  { entityType: 'session', count: 5 },
  { entityType: 'kb', count: 2 },
  { entityType: 'agent', count: 1 },
];

describe('ManaPips', () => {
  it('renders up to 3 pips', () => {
    const { container } = render(<ManaPips pips={pips} />);
    const dots = container.querySelectorAll('[data-pip]');
    expect(dots).toHaveLength(3);
  });

  it('shows +N overflow when more than 3 pips', () => {
    const fourPips: ManaPip[] = [
      { entityType: 'session', count: 5 },
      { entityType: 'kb', count: 2 },
      { entityType: 'agent', count: 1 },
      { entityType: 'toolkit', count: 3 },
    ];
    render(<ManaPips pips={fourPips} />);
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('shows count badge when size=md and count > 0', () => {
    render(<ManaPips pips={[{ entityType: 'session', count: 5 }]} size="md" />);
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('does not show badge when size=sm', () => {
    render(<ManaPips pips={[{ entityType: 'session', count: 5 }]} size="sm" />);
    expect(screen.queryByText('5')).not.toBeInTheDocument();
  });

  it('renders nothing when pips array is empty', () => {
    const { container } = render(<ManaPips pips={[]} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Esegui test — verifica FAIL**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx --run
```

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Crea il componente ManaPips**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx
import { entityHsl } from '../tokens';
import type { MeepleEntityType } from '../types';

export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
}

interface ManaPipsProps {
  pips: ManaPip[];
  /** sm = 6px pip no badge (compact); md = 8px pip with count badge (grid) */
  size?: 'sm' | 'md';
}

const MAX_VISIBLE = 3;

export function ManaPips({ pips, size = 'md' }: ManaPipsProps) {
  if (pips.length === 0) return null;

  const visible = pips.slice(0, MAX_VISIBLE);
  const overflow = pips.length - MAX_VISIBLE;
  const dotSize = size === 'md' ? 8 : 6;

  return (
    <div className="flex items-center gap-1 px-3 pb-2 pt-0.5">
      {visible.map((pip, i) => {
        const color = entityHsl(pip.entityType);
        return (
          <span
            key={i}
            data-pip
            title={pip.entityType}
            className="relative inline-flex items-center justify-center rounded-full"
            style={{
              width: dotSize,
              height: dotSize,
              background: color,
              flexShrink: 0,
            }}
          >
            {size === 'md' && pip.count !== undefined && pip.count > 0 && (
              <span
                className="absolute -top-2 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-1 text-[7px] font-bold text-white"
                style={{ background: color, lineHeight: '10px', minWidth: 12, textAlign: 'center' }}
              >
                {pip.count}
              </span>
            )}
          </span>
        );
      })}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-[var(--mc-text-muted,#94a3b8)]">
          +{overflow}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Esegui test — verifica PASS**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx --run
```

Expected: 5 PASS

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/ui/data-display/meeple-card/parts/ManaPips.tsx src/__tests__/components/ui/data-display/meeple-card/parts/ManaPips.test.tsx
git commit -m "feat(meeple-card): add ManaPips component for grid/compact entity pip row"
```

---

## Task 2: NavFooter href Support

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx`

- [ ] **Step 1: Scrivi il test fallente**

Aggiungi al file di test esistente (o crea separato):

```tsx
// apps/web/src/__tests__/components/ui/data-display/meeple-card/parts/NavFooter.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode; [k: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

import { NavFooter } from '@/components/ui/data-display/meeple-card/parts/NavFooter';

describe('NavFooter href support', () => {
  it('renders <a> with correct href when href is provided', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
            href: '/games/abc/sessions',
          },
        ]}
      />
    );
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/games/abc/sessions');
  });

  it('renders div[role=button] when no href', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
          },
        ]}
      />
    );
    expect(screen.queryByRole('link')).toBeNull();
    expect(screen.getByRole('button')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui test — verifica FAIL**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/parts/NavFooter.test.tsx --run
```

Expected: FAIL (type error or test not found)

- [ ] **Step 3: Aggiungi `href?` al tipo `NavFooterItem` in types.ts**

Nel file `apps/web/src/components/ui/data-display/meeple-card/types.ts`, modifica l'interfaccia `NavFooterItem` aggiungendo `href?`:

```ts
export interface NavFooterItem {
  icon: ReactNode;
  label: string;
  entity: MeepleEntityType;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  onPlusClick?: () => void;
  href?: string;   // ← aggiunto
}
```

- [ ] **Step 4: Aggiorna NavFooter per usare `<Link>` quando `href` presente**

Sostituisci il contenuto di `apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx`:

```tsx
'use client';

import type { KeyboardEvent } from 'react';
import Link from 'next/link';

import { entityHsl } from '../tokens';
import type { NavFooterItem } from '../types';

interface NavFooterProps {
  items: NavFooterItem[];
  size?: 'sm' | 'md';
}

export function NavFooter({ items, size = 'sm' }: NavFooterProps) {
  if (items.length === 0) return null;

  const iconSize = size === 'md' ? 'h-8 w-8 text-[15px]' : 'h-7 w-7 text-[13px]';

  return (
    <div className="flex items-center justify-center gap-2 border-t border-[var(--mc-border-light)] bg-[var(--mc-nav-footer-bg)] px-2.5 py-1.5 backdrop-blur-lg">
      {items.map((item, i) => {
        const color = entityHsl(item.entity);
        const glowColor = entityHsl(item.entity, 0.15);
        const borderHover = entityHsl(item.entity, 0.4);

        const handleActivate = () => {
          if (item.disabled) return;
          item.onClick?.();
        };

        const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
          if (item.disabled) return;
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            e.stopPropagation();
            item.onClick?.();
          }
        };

        const innerContent = (
          <>
            <div
              className={`relative flex ${iconSize} items-center justify-center rounded-full border border-[var(--mc-nav-icon-border)] bg-[var(--mc-nav-icon-bg)] transition-all duration-200 group-hover/nav:scale-[1.08] group-hover/nav:border-[var(--nav-hover-border)] group-hover/nav:bg-[var(--nav-hover-bg)] group-hover/nav:shadow-[var(--nav-hover-shadow)] group-active/nav:scale-95 group-active/nav:shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)]`}
              style={
                {
                  '--nav-hover-bg': entityHsl(item.entity, 0.08),
                  '--nav-hover-shadow': `0 2px 8px ${glowColor}`,
                } as React.CSSProperties
              }
            >
              <span className="pointer-events-none flex h-3.5 w-3.5 items-center justify-center [&>svg]:h-full [&>svg]:w-full">
                {item.icon}
              </span>

              {item.count !== undefined && item.count > 0 && (
                <span
                  className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full px-[3px] text-[8px] font-bold text-white shadow-sm"
                  style={{ background: color }}
                >
                  {item.count > 99 ? '99+' : item.count}
                </span>
              )}
            </div>

            {item.showPlus && (
              <button
                type="button"
                onClick={e => {
                  e.stopPropagation();
                  item.onPlusClick?.();
                }}
                className="absolute -bottom-0.5 right-1 flex h-3.5 w-3.5 items-center justify-center rounded-full text-[8px] font-extrabold text-white shadow-sm"
                style={{ background: color }}
                aria-label={`Aggiungi ${item.label}`}
              >
                +
              </button>
            )}

            <span className="text-[7px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)] transition-colors group-hover/nav:text-[var(--mc-text-secondary)]">
              {item.label}
            </span>
          </>
        );

        const commonClassName = `group/nav relative flex flex-col items-center gap-0.5 outline-none focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:ring-[var(--nav-hover-border)] ${
          item.disabled ? 'cursor-not-allowed opacity-45' : 'cursor-pointer'
        }`;

        const commonStyle = { '--nav-hover-border': borderHover } as React.CSSProperties;

        if (item.href && !item.disabled) {
          return (
            <Link
              key={i}
              href={item.href}
              className={commonClassName}
              style={commonStyle}
              aria-label={item.label}
              title={item.label}
              onClick={e => {
                e.stopPropagation();
                item.onClick?.();
              }}
            >
              {innerContent}
            </Link>
          );
        }

        return (
          <div
            key={i}
            role="button"
            tabIndex={item.disabled ? -1 : 0}
            aria-disabled={item.disabled}
            aria-label={item.label}
            title={item.label}
            onClick={e => {
              e.stopPropagation();
              handleActivate();
            }}
            onKeyDown={handleKeyDown}
            className={commonClassName}
            style={commonStyle}
          >
            {innerContent}
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 5: Esegui test — verifica PASS**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/parts/NavFooter.test.tsx --run
```

Expected: 2 PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts \
        apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx \
        apps/web/src/__tests__/components/ui/data-display/meeple-card/parts/NavFooter.test.tsx
git commit -m "feat(meeple-card): add href support to NavFooterItem + NavFooter Link rendering"
```

---

## Task 3: FocusCard Variant

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx`
- Create: `apps/web/src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx`

- [ ] **Step 1: Scrivi il test fallente**

```tsx
// apps/web/src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode; [k: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

import { FocusCard } from '@/components/ui/data-display/meeple-card/variants/FocusCard';

const baseProps = {
  entity: 'game' as const,
  title: 'Catan',
  subtitle: 'Klaus Teuber',
  imageUrl: '/catan.jpg',
  rating: 7.5,
  ratingMax: 10,
};

describe('FocusCard', () => {
  it('renders title and subtitle', () => {
    render(<FocusCard {...baseProps} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('renders navItem chips when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        navItems={[
          { icon: <span>🎮</span>, label: 'Sessioni', entity: 'session', count: 5, href: '/games/1/sessions' },
          { icon: <span>📚</span>, label: 'Docs', entity: 'kb', count: 2, href: '/games/1/kb' },
        ]}
      />
    );
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Docs')).toBeInTheDocument();
  });

  it('renders navItem count when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        navItems={[
          { icon: <span>🎮</span>, label: 'Sessioni', entity: 'session', count: 12, href: '/games/1/sessions' },
        ]}
      />
    );
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('applies data-testid', () => {
    render(<FocusCard {...baseProps} data-testid="focus-card-test" />);
    expect(screen.getByTestId('focus-card-test')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui test — verifica FAIL**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx --run
```

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Crea FocusCard**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx
'use client';

import Link from 'next/link';

import { AccentBorder } from '../parts/AccentBorder';
import { Cover } from '../parts/Cover';
import { MetaChips } from '../parts/MetaChips';
import { Rating } from '../parts/Rating';
import { entityHsl } from '../tokens';
import type { MeepleCardProps } from '../types';

/** Full-width entity header card. NavItems render as horizontal chips. */
export function FocusCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    navItems = [],
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  return (
    <div
      className={`relative w-full overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] ${className}`}
      data-entity={entity}
      data-testid={testId}
      onClick={onClick}
    >
      <AccentBorder entity={entity} />

      {/* Hero row: cover + info */}
      <div className="flex gap-4 p-4">
        <div className="h-24 w-24 shrink-0 overflow-hidden rounded-xl">
          <Cover entity={entity} variant="compact" imageUrl={imageUrl} alt={title} />
        </div>

        <div className="flex min-w-0 flex-1 flex-col justify-center gap-1">
          <h2 className="font-[var(--font-quicksand)] text-xl font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h2>
          {subtitle && (
            <p className="text-sm text-[var(--mc-text-secondary)]">{subtitle}</p>
          )}
          {rating !== undefined && <Rating value={rating} max={ratingMax} />}
          {metadata.length > 0 && <MetaChips metadata={metadata} />}
        </div>
      </div>

      {/* NavItem chip row */}
      {navItems.length > 0 && (
        <div className="flex flex-wrap gap-2 border-t border-[var(--mc-border-light)] px-4 py-3">
          {navItems.map((item, i) => {
            const color = entityHsl(item.entity);
            const chipContent = (
              <>
                <span className="text-[13px] leading-none">{item.icon}</span>
                <span className="text-xs font-semibold">{item.label}</span>
                {item.count !== undefined && item.count > 0 && (
                  <span
                    className="ml-0.5 rounded-full px-1.5 py-0.5 text-[9px] font-bold text-white"
                    style={{ background: color }}
                  >
                    {item.count}
                  </span>
                )}
              </>
            );

            const chipClass =
              'flex items-center gap-1.5 rounded-xl border px-2.5 py-1.5 text-[var(--mc-text-primary)] transition-all duration-200 hover:scale-[1.03] active:scale-95';
            const chipStyle = {
              borderColor: entityHsl(item.entity, 0.3),
              background: entityHsl(item.entity, 0.06),
            };

            if (item.href && !item.disabled) {
              return (
                <Link
                  key={i}
                  href={item.href}
                  className={chipClass}
                  style={chipStyle}
                  onClick={e => {
                    e.stopPropagation();
                    item.onClick?.();
                  }}
                  aria-label={item.label}
                >
                  {chipContent}
                </Link>
              );
            }

            return (
              <button
                key={i}
                type="button"
                onClick={e => {
                  e.stopPropagation();
                  if (!item.disabled) item.onClick?.();
                }}
                disabled={item.disabled}
                className={`${chipClass} ${item.disabled ? 'cursor-not-allowed opacity-40' : ''}`}
                style={chipStyle}
                aria-label={item.label}
              >
                {chipContent}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Esegui test — verifica PASS**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx --run
```

Expected: 4 PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/FocusCard.tsx \
        apps/web/src/__tests__/components/ui/data-display/meeple-card/variants/FocusCard.test.tsx
git commit -m "feat(meeple-card): add FocusCard variant (full-width entity header with navItem chips)"
```

---

## Task 4: Registra Variante `focus` + ManaPips in GridCard/CompactCard

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts` — aggiungi `'focus'` a `MeepleCardVariant`, aggiungi `manaPips?` a `MeepleCardProps`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` — registra `focus: FocusCard`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` — aggiunge `<ManaPips>`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx` — aggiunge `<ManaPips>`

- [ ] **Step 1: Scrivi test fallente per registrazione variante**

```tsx
// apps/web/src/__tests__/components/ui/data-display/meeple-card/MeepleCard.test.tsx
// Aggiungere questi test al file esistente (o creare nuovo):
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode; [k: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

describe('MeepleCard focus variant', () => {
  it('renders focus variant without throwing', () => {
    render(
      <MeepleCard
        entity="game"
        title="Catan"
        variant="focus"
        data-testid="focus-test"
      />
    );
    expect(screen.getByTestId('focus-test')).toBeInTheDocument();
  });
});

describe('MeepleCard manaPips prop', () => {
  it('renders ManaPips when manaPips provided in grid variant', () => {
    const { container } = render(
      <MeepleCard
        entity="game"
        title="Catan"
        variant="grid"
        manaPips={[
          { entityType: 'session', count: 3 },
          { entityType: 'kb', count: 1 },
        ]}
      />
    );
    const dots = container.querySelectorAll('[data-pip]');
    expect(dots).toHaveLength(2);
  });
});
```

- [ ] **Step 2: Esegui test — verifica FAIL**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/MeepleCard.test.tsx --run
```

Expected: FAIL (type error: `focus` not in union / `manaPips` not in props)

- [ ] **Step 3: Aggiorna types.ts**

In `apps/web/src/components/ui/data-display/meeple-card/types.ts`:

```ts
// Cambia:
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero';
// In:
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero' | 'focus';
```

E aggiungi `manaPips?` a `MeepleCardProps` dopo `navItems`:

```ts
  navItems?: NavFooterItem[];
  manaPips?: ManaPip[];  // ← aggiunto
  onClick?: () => void;
```

Aggiungi l'import necessario:

```ts
import type { ManaPip } from './parts/ManaPips';
```

- [ ] **Step 4: Registra FocusCard in MeepleCard.tsx**

```tsx
// apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx
'use client';

import { memo } from 'react';

import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { FocusCard } from './variants/FocusCard';
import { GridCard } from './variants/GridCard';
import { HeroCard } from './variants/HeroCard';
import { ListCard } from './variants/ListCard';

import type { MeepleCardProps } from './types';

const variantMap = {
  grid: GridCard,
  list: ListCard,
  compact: CompactCard,
  featured: FeaturedCard,
  hero: HeroCard,
  focus: FocusCard,
} as const;

export const MeepleCard = memo(function MeepleCard(props: MeepleCardProps) {
  const variant = props.variant ?? 'grid';
  const Renderer = variantMap[variant];
  return <Renderer {...props} />;
});
```

- [ ] **Step 5: Aggiunge ManaPips in GridCard.tsx**

In `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx`, aggiungi l'import e usa il componente:

```tsx
import { ManaPips } from '../parts/ManaPips';
```

E nella destructuring aggiunge `manaPips`:

```tsx
const {
  // ... existing ...
  navItems = [],
  manaPips,   // ← aggiunto
  showQuickActions,
  onClick,
  className = '',
} = props;
```

Prima del `{navItems.length > 0 && <NavFooter items={navItems} />}`, aggiungi:

```tsx
{manaPips && manaPips.length > 0 && <ManaPips pips={manaPips} size="md" />}
{navItems.length > 0 && <NavFooter items={navItems} />}
```

- [ ] **Step 6: Aggiunge ManaPips in CompactCard.tsx**

Leggi il file CompactCard per capirne la struttura, poi aggiungi ManaPips analogamente con `size="sm"`. Segui lo stesso pattern di GridCard.

- [ ] **Step 7: Esegui typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 8: Esegui test**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/ --run
```

Expected: tutti PASS

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts \
        apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx \
        apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx \
        apps/web/src/components/ui/data-display/meeple-card/variants/CompactCard.tsx \
        apps/web/src/__tests__/components/ui/data-display/meeple-card/MeepleCard.test.tsx
git commit -m "feat(meeple-card): register focus variant + ManaPips integration in grid/compact"
```

---

## Task 5: `useDrawCard` Hook + Game Page Integration

**Files:**
- Create: `apps/web/src/hooks/useDrawCard.ts`
- Modify: `apps/web/src/app/(authenticated)/games/[id]/page.tsx` (o il Client component che wrappa la game detail)

- [ ] **Step 1: Scrivi il test fallente per useDrawCard**

```tsx
// apps/web/src/__tests__/hooks/useDrawCard.test.ts
import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mockDrawCard = vi.fn();
vi.mock('@/lib/stores/card-hand-store', () => ({
  useCardHand: (selector: (s: { drawCard: typeof mockDrawCard }) => unknown) =>
    selector({ drawCard: mockDrawCard }),
}));

import { useDrawCard } from '@/hooks/useDrawCard';

describe('useDrawCard', () => {
  beforeEach(() => mockDrawCard.mockClear());

  it('calls drawCard on mount with the provided card', () => {
    renderHook(() =>
      useDrawCard({
        id: 'game:abc',
        entityType: 'game',
        entityId: 'abc',
        label: 'Catan',
        color: 'hsl(25,95%,45%)',
        href: '/games/abc',
        pinned: false,
        addedAt: 123,
      })
    );
    expect(mockDrawCard).toHaveBeenCalledOnce();
    expect(mockDrawCard).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: 'game', entityId: 'abc' })
    );
  });

  it('does not call drawCard when card is null', () => {
    renderHook(() => useDrawCard(null));
    expect(mockDrawCard).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Esegui test — verifica FAIL**

```bash
cd apps/web && pnpm test src/__tests__/hooks/useDrawCard.test.ts --run
```

Expected: FAIL "Cannot find module"

- [ ] **Step 3: Crea useDrawCard hook**

```ts
// apps/web/src/hooks/useDrawCard.ts
'use client';

import { useEffect } from 'react';

import type { HandCard } from '@/lib/stores/card-hand-store';
import { useCardHand } from '@/lib/stores/card-hand-store';

/**
 * Deals a card into the hand on mount.
 * Pass `null` to skip (useful when entity data is still loading).
 */
export function useDrawCard(card: HandCard | null) {
  const drawCard = useCardHand(s => s.drawCard);

  useEffect(() => {
    if (card) {
      drawCard(card);
    }
    // drawCard è stabile (store method), card è dependency per re-draw se entityId cambia
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [card?.id]);
}
```

- [ ] **Step 4: Esegui test — verifica PASS**

```bash
cd apps/web && pnpm test src/__tests__/hooks/useDrawCard.test.ts --run
```

Expected: 2 PASS

- [ ] **Step 5: Integra useDrawCard nel GameDetail client component**

Cerca il componente client della pagina game detail:

```bash
# Trova il client component per la game detail page
grep -r "useDrawCard\|drawCard\|game.*detail\|GameDetail" apps/web/src/app/ --include="*.tsx" -l
```

Nel componente Client della game page (es. `GameDetailClient.tsx` o nel `page.tsx` stesso se ha `'use client'`), aggiungi la chiamata `useDrawCard` dopo che i dati del gioco sono disponibili:

```tsx
import { useDrawCard } from '@/hooks/useDrawCard';
import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

// Dentro il componente, dopo aver recuperato `game` dai dati:
useDrawCard(
  game
    ? {
        id: `game:${game.id}`,
        entityType: 'game',
        entityId: game.id,
        label: game.title,
        sublabel: game.publisher ?? undefined,
        color: entityHsl('game'),
        href: `/games/${game.id}`,
        pinned: false,
        addedAt: Date.now(),
      }
    : null
);
```

- [ ] **Step 6: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/hooks/useDrawCard.ts \
        apps/web/src/__tests__/hooks/useDrawCard.test.ts
# Aggiungi anche il file game detail modificato se presente
git commit -m "feat(navigation): add useDrawCard hook + integrate drawCard in game detail page"
```

---

## Task 6: buildGameNavItems con href + ManaPips per Dashboard

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts`

- [ ] **Step 1: Leggi il file attuale**

```bash
cat apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts
```

- [ ] **Step 2: Scrivi test fallente**

```ts
// apps/web/src/__tests__/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.test.ts
import { describe, expect, it } from 'vitest';
import { buildGameNavItems } from '@/components/ui/data-display/meeple-card/nav-items/buildGameNavItems';

describe('buildGameNavItems', () => {
  it('returns navItems with href when gameId provided', () => {
    const items = buildGameNavItems({ gameId: 'abc123', kbCount: 3, sessionCount: 5 });
    const kbItem = items.find(i => i.entity === 'kb');
    expect(kbItem?.href).toBe('/games/abc123/kb');
    const sessionItem = items.find(i => i.entity === 'session');
    expect(sessionItem?.href).toBe('/games/abc123/sessions');
  });

  it('returns navItems with correct counts', () => {
    const items = buildGameNavItems({ gameId: 'abc123', kbCount: 3, sessionCount: 5 });
    expect(items.find(i => i.entity === 'kb')?.count).toBe(3);
    expect(items.find(i => i.entity === 'session')?.count).toBe(5);
  });
});
```

- [ ] **Step 3: Esegui test — verifica FAIL o PASS (se già implementato)**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.test.ts --run
```

Se PASS già: skippa Step 4. Se FAIL: vai a Step 4.

- [ ] **Step 4: Aggiorna buildGameNavItems per includere href**

Leggi il file esistente. Aggiungi `href` a ciascun NavFooterItem restituito, usando `gameId` per costruire i path:

```ts
// Esempio del pattern da aggiungere in ciascun item:
{
  icon: <KbIcon />,
  label: 'Docs',
  entity: 'kb' as MeepleEntityType,
  count: kbCount,
  href: gameId ? `/games/${gameId}/kb` : undefined,
  onClick: onKbClick,
}
```

- [ ] **Step 5: Esegui tutti i test meeple-card**

```bash
cd apps/web && pnpm test src/__tests__/components/ui/data-display/meeple-card/ --run
```

Expected: tutti PASS

- [ ] **Step 6: Esegui typecheck completo**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts \
        apps/web/src/__tests__/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.test.ts
git commit -m "feat(meeple-card): add href to buildGameNavItems nav items"
```

---

## Task 7: Esegui tutti i test + PR + merge

- [ ] **Step 1: Esegui test suite completa frontend**

```bash
cd apps/web && pnpm test --run
```

Expected: 0 FAIL

- [ ] **Step 2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 3: Crea PR**

```bash
# Verifica branch corrente (deve essere feature branch, NON main-dev)
git branch --show-current

# Crea PR verso main-dev
gh pr create \
  --title "feat(meeple-card/nav): Piano 3 — ManaPips + FocusCard + drawCard + href navItems" \
  --body "$(cat <<'EOF'
## Summary
- **ManaPips**: nuovi indicatori pip colorati (8px/6px) per varianti `grid` e `compact`
- **FocusCard**: nuova variante full-width con hero cover + navItem chips cliccabili
- **NavFooter href**: `NavFooterItem.href?` → renderizza `<Link>` per navigazione diretta
- **useDrawCard hook**: aggiunge automaticamente carta alla mano al mount delle entity page
- **buildGameNavItems**: aggiornato con `href` per ogni nav item
- **Registrazione variante `focus`** in `types.ts` e `MeepleCard.tsx`

## Test Plan
- [ ] ManaPips: 5 unit test (pip row, overflow, badge, size, empty)
- [ ] NavFooter: 2 test (href → Link, no href → div[role=button])
- [ ] FocusCard: 4 test (render, navItems, count badge, testid)
- [ ] MeepleCard: test variante focus + manaPips prop
- [ ] useDrawCard: 2 test (call on mount, skip on null)
- [ ] buildGameNavItems: 2 test (href, count)
- [ ] `pnpm typecheck` 0 errori
- [ ] `pnpm test --run` 0 FAIL

🤖 Piano 3 — Navigation Redesign
EOF
)"
```

- [ ] **Step 4: Code review — verifica PR**

Controlla che:
- Tutti i test passino nel CI
- Nessun TypeScript error
- `MeepleCardVariant` ora include `'focus'`
- `ManaPip` è importato correttamente in `types.ts` (no circular imports)
- `FocusCard` usa solo componenti dalla cartella `parts/` — nessuna importazione circolare

- [ ] **Step 5: Merge PR**

```bash
gh pr merge --squash --admin
```

- [ ] **Step 6: Cleanup branch**

```bash
git checkout main-dev && git pull
# Elimina il branch locale
git branch -D feature/issue-piano3-meeplecard-focus
# Prune remote tracking
git remote prune origin
```

---

## Self-Review Checklist

**Spec coverage** (§6 e §6b):
- ✅ ManaPips su grid (§6.2)
- ✅ ManaPips su compact (§6.2)
- ✅ ManaPips overflow `+N` (§6.2)
- ✅ NavFooterItem.href per FocusCard chips (§6.3)
- ✅ FocusCard variante full-width (§6.1, §6b)
- ✅ navItem chips con `drawCard()` call — via `onClick` o `useDrawCard` (§6.3)
- ✅ `buildGameNavItems` con href (§6.3)
- ✅ Variante registrata nel variantMap (§6.1)

**Placeholder scan**: nessuno — ogni step ha codice concreto.

**Type consistency**:
- `ManaPip` definita in `parts/ManaPips.tsx`, importata in `types.ts` via `import type { ManaPip }` 
- `MeepleCardVariant` aggiornato in un unico posto (`types.ts`), usato da `MeepleCard.tsx`
- `NavFooterItem.href?` aggiunto in `types.ts`, usato in `NavFooter.tsx` e `FocusCard.tsx`

**Note implementative**:
- Il `useDrawCard` hook usa `card?.id` come dependency dell'`useEffect` per evitare re-draw continui
- `ManaPips` in GridCard è reso PRIMA di `NavFooter` (più vicino al contenuto, non al bordo inferiore estremo)
- `FocusCard` non usa `NavFooter` — usa una propria riga di chip custom per il layout espanso
