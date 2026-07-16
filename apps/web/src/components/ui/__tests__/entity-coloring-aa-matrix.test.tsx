/**
 * Entity coloring AA matrix — issue #2955 Fase 3 (test hardening)
 *
 * A single comprehensive regression matrix. Every primitive restored in Fase 1+2
 * is rendered in its ENTITY-ACTIVE state across ALL NINE canonical EntityType
 * values and asserts, per combination:
 *   (a) jest-axe `toHaveNoViolations`, and
 *   (b) the expected registered `*-entity-*` / `text-entity-*-text` utility is
 *       present on the design-sensitive element.
 * For `kb` it additionally asserts the class resolves to the registered TEAL
 * `-kb` token and NEVER the slate `-document` token (which lives only in
 * `@layer tokens` and is absent from `@theme inline`).
 *
 * This is COVERAGE of already-correct behaviour — it must pass on a clean tree.
 * A failure here is a real regression in per-entity coloring or accessibility.
 */
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { Btn } from '@/components/ui/btn/btn';
import { EntityTableView } from '@/components/ui/data-display/entity-list-view/components/entity-table-view';
import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer/drawer';
import { EntityCard } from '@/components/ui/entity-card/entity-card';
import { EntityPip } from '@/components/ui/entity-pip/entity-pip';
import type { EntityType } from '@/components/ui/entity-tokens';
import { NotificationCard } from '@/components/ui/notification-card/notification-card';
import { SettingsRow } from '@/components/ui/settings-row/settings-row';
import { StepProgress, type Step } from '@/components/ui/step-progress/step-progress';
import { ToggleSwitch } from '@/components/ui/toggle-switch/toggle-switch';

expect.extend(toHaveNoViolations);

/** The 9 canonical EntityType values (kb → teal `-kb`, never slate `-document`). */
const ENTITIES: EntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
];

/**
 * Matches an exact entity utility token and NOT a longer sibling token. This is
 * critical because `tool` is a prefix of `toolkit`: a naive `/bg-entity-tool/`
 * would wrongly match `bg-entity-toolkit`. Entity tokens are always followed by
 * a space, `/` (opacity), `-text`, or end-of-string in a className, so a
 * negative lookahead for a following lowercase letter disambiguates them.
 */
const token = (util: string, entity: string): RegExp => new RegExp(`${util}${entity}(?![a-z])`);

/** The unregistered slate token that kb must NEVER resolve to. */
const DOCUMENT = /entity-document/;

// ---------------------------------------------------------------------------
// Btn — primary (per-entity fill, white text) — #2955 Fase 1
// ---------------------------------------------------------------------------
describe('Btn variant="primary" — per-entity fill (#2955 Fase 1)', () => {
  it.each(ENTITIES)('%s → bg-entity fill + no axe violations', async entity => {
    const { container } = render(
      <Btn variant="primary" entity={entity}>
        Label
      </Btn>
    );
    const btn = screen.getByRole('button');
    expect(btn.className).toMatch(token('bg-entity-', entity));
    if (entity === 'kb') expect(btn.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// Btn — outline (per-entity border + AA text-on-tint label) — #2955 Fase 2
// ---------------------------------------------------------------------------
describe('Btn variant="outline" — per-entity border + AA -text label (#2955 Fase 2)', () => {
  it.each(ENTITIES)('%s → border-entity + text-entity-*-text + no axe violations', async entity => {
    const { container } = render(
      <Btn variant="outline" entity={entity}>
        Label
      </Btn>
    );
    const btn = screen.getByRole('button');
    expect(btn.className).toMatch(token('border-entity-', entity));
    expect(btn.className).toMatch(new RegExp(`text-entity-${entity}-text`));
    if (entity === 'kb') expect(btn.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// EntityCard — per-entity left border — #2955
// ---------------------------------------------------------------------------
describe('EntityCard — per-entity left border (#2955)', () => {
  it.each(ENTITIES)('%s → border-l-entity + no axe violations', async entity => {
    const { container } = render(
      <EntityCard entity={entity}>
        <p>Body</p>
      </EntityCard>
    );
    const el = container.querySelector<HTMLElement>(`[data-entity="${entity}"]`);
    expect(el?.className).toMatch(token('border-l-entity-', entity));
    if (entity === 'kb') expect(el?.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// NotificationCard — left border + unread dot fill — #2955
// ---------------------------------------------------------------------------
describe('NotificationCard — left border + unread dot (#2955)', () => {
  it.each(ENTITIES)('%s → border-l-entity + unread dot bg-entity + no axe', async entity => {
    const { container } = render(
      <NotificationCard entity={entity} title="Titolo" timestamp="ora" unread />
    );
    const el = container.querySelector<HTMLElement>(`[data-entity="${entity}"]`);
    expect(el?.className).toMatch(token('border-l-entity-', entity));
    const dot = screen.getByTestId('unread-dot');
    expect(dot.className).toMatch(token('bg-entity-', entity));
    if (entity === 'kb') {
      expect(el?.className).not.toMatch(DOCUMENT);
      expect(dot.className).not.toMatch(DOCUMENT);
    }
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// EntityPip — active ring + fill — #2955
// ---------------------------------------------------------------------------
describe('EntityPip — active ring + fill (#2955)', () => {
  it.each(ENTITIES)('%s → ring-entity (active) + bg-entity + no axe', async entity => {
    const { container } = render(<EntityPip entity={entity} count={2} active />);
    const el = container.querySelector<HTMLElement>(`[data-entity="${entity}"]`);
    expect(el?.className).toMatch(token('ring-entity-', entity));
    expect(el?.className).toMatch(token('bg-entity-', entity));
    if (entity === 'kb') expect(el?.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// StepProgress — completed/current fill + current ring — #2955
// ---------------------------------------------------------------------------
const STEPS: Step[] = [{ label: 'Uno' }, { label: 'Due' }, { label: 'Tre' }];

describe('StepProgress — completed/current fill + current ring (#2955)', () => {
  it.each(ENTITIES)('%s → bg-entity + ring-entity + no axe', async entity => {
    // currentIndex=2 over 3 steps → steps 0,1 completed, step 2 current.
    const { container } = render(<StepProgress steps={STEPS} currentIndex={2} entity={entity} />);
    const current = container.querySelector<HTMLElement>(
      '[data-step-status="current"] [data-step-circle]'
    );
    expect(current?.className).toMatch(token('bg-entity-', entity));
    expect(current?.className).toMatch(token('ring-entity-', entity));
    const completed = container.querySelector<HTMLElement>(
      '[data-step-status="completed"] [data-step-circle]'
    );
    expect(completed?.className).toMatch(token('bg-entity-', entity));
    if (entity === 'kb') {
      const root = container.querySelector('[role="progressbar"]');
      expect(root?.innerHTML).not.toMatch(DOCUMENT);
    }
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// ToggleSwitch — checked track fill + focus ring — #2955
// ---------------------------------------------------------------------------
describe('ToggleSwitch — checked track + ring (#2955)', () => {
  it.each(ENTITIES)('%s → bg-entity track + ring-entity + no axe', async entity => {
    const { container } = render(
      <ToggleSwitch checked onCheckedChange={() => {}} entity={entity} ariaLabel="Attiva" />
    );
    const sw = screen.getByRole('switch');
    expect(sw.className).toMatch(token('bg-entity-', entity));
    expect(sw.className).toMatch(token('ring-entity-', entity));
    if (entity === 'kb') expect(sw.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// SettingsRow — leading icon AA text-on-tint shade — #2955 Fase 2
// (wrapped in <ul> so the semantic <li> root does not trip the axe `listitem` rule)
// ---------------------------------------------------------------------------
describe('SettingsRow — leading icon AA -text shade (#2955 Fase 2)', () => {
  it.each(ENTITIES)('%s → text-entity-*-text icon + no axe', async entity => {
    const { container } = render(
      <ul>
        <SettingsRow label="Impostazione" icon={<span>◆</span>} entity={entity} />
      </ul>
    );
    const icon = screen.getByTestId('settings-row-icon');
    expect(icon.className).toMatch(new RegExp(`text-entity-${entity}-text`));
    if (entity === 'kb') expect(icon.className).not.toMatch(DOCUMENT);
    expect(await axe(container)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// Drawer — desktop (Radix) entity accent strip — #2955 Fase 1
// ---------------------------------------------------------------------------
describe('Drawer — desktop entity accent (#2955 Fase 1)', () => {
  beforeAll(() => {
    // Force the desktop (Radix Dialog) render path deterministically. The
    // breakpoint hook degrades to "mobile" without matchMedia, but side=
    // "desktop-right" already forces desktop mode; this mirrors the proven
    // desktop drawer test setup and removes any ambiguity.
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: vi.fn().mockReturnValue({
        matches: true,
        media: '(min-width: 768px)',
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        onchange: null,
      }),
    });
  });

  it.each(ENTITIES)('%s → bg-entity accent strip + no axe', async entity => {
    render(
      <Drawer open onOpenChange={() => {}} side="desktop-right" entity={entity}>
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>Titolo</DrawerTitle>
            <DrawerDescription>Descrizione</DrawerDescription>
          </DrawerHeader>
          <p>Contenuto</p>
        </DrawerContent>
      </Drawer>
    );
    const dialog = screen.getByRole('dialog');
    // Radix portals the dialog to document.body — scan the dialog subtree (which
    // contains its own aria-labelledby/‑describedby targets) rather than the RTL
    // container, and avoid the body-level focus guards Radix installs.
    const accent = dialog.querySelector<HTMLElement>(`[data-drawer-accent="${entity}"]`);
    expect(accent).not.toBeNull();
    expect(accent?.className).toMatch(token('bg-entity-', entity));
    if (entity === 'kb') expect(accent?.className).not.toMatch(DOCUMENT);
    expect(await axe(dialog)).toHaveNoViolations();
  });
});

// ---------------------------------------------------------------------------
// EntityTableView — row accent border + badge pill — #2955 Fase 2
// ---------------------------------------------------------------------------
interface TableRow {
  id: string;
  name: string;
  tag: string;
}
const TABLE_ITEMS: TableRow[] = [{ id: 'r1', name: 'Catan', tag: 'NEW' }];
const renderRow = (item: TableRow) => ({ id: item.id, title: item.name, badge: item.tag });

describe('EntityTableView — row border + badge pill (#2955 Fase 2)', () => {
  it.each(ENTITIES)(
    '%s → border-l-entity row (on <tr>) + bg-entity/10 + text-entity-*-text badge + no axe',
    async entity => {
      const { container } = render(
        <EntityTableView
          entity={entity}
          displayItems={TABLE_ITEMS}
          items={TABLE_ITEMS}
          renderItem={renderRow}
        />
      );
      const layout = screen.getByTestId('table-layout');
      // Color targets the <tr> via the `[&_tbody_tr]:` descendant variant (same
      // element as `[&_tbody_tr]:border-l-4` width) — a plain `border-entity-*` on
      // this wrapper would be a no-op (no border-width; color doesn't inherit).
      expect(layout.className).toMatch(token('border-l-entity-', entity));
      const badge = screen.getByText('NEW');
      expect(badge.className).toMatch(new RegExp(`bg-entity-${entity}\\/10`));
      expect(badge.className).toMatch(new RegExp(`text-entity-${entity}-text`));
      if (entity === 'kb') {
        expect(layout.className).not.toMatch(DOCUMENT);
        expect(badge.className).not.toMatch(DOCUMENT);
      }
      expect(await axe(container)).toHaveNoViolations();
    }
  );
});
