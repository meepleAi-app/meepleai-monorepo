/**
 * EntityTableView — per-entity coloring regression tests (Issue #2955 Fase 2)
 *
 * Asserts the design-sensitive primitive uses CANONICAL entity tokens:
 *   - Row accent border → `border-entity-<e>` (base token, borders need only 3:1)
 *   - Badge pill → soft tint `bg-entity-<e>/10` + AA label `text-entity-<e>-text`
 *
 * Guards against regressions:
 *   - No hardcoded inline HSL (`border-l-[hsl(...)]`) on the row accent.
 *   - kb resolves to the registered TEAL `-kb` / `-kb-text`, NEVER `-document`.
 *   - Badge is no longer the flat `bg-muted` neutral.
 */
import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { EntityTableView } from './entity-table-view';

import type { MeepleEntityType } from '../../meeple-card';

interface Row {
  id: string;
  name: string;
  tag: string;
}

const ITEMS: Row[] = [{ id: 'r1', name: 'Catan', tag: 'NEW' }];

const renderItem = (item: Row) => ({
  id: item.id,
  title: item.name,
  badge: item.tag,
});

function renderTable(entity: MeepleEntityType) {
  return render(
    <EntityTableView entity={entity} displayItems={ITEMS} items={ITEMS} renderItem={renderItem} />
  );
}

// The 9 canonical entities → their expected literal canonical classes.
const CANONICAL: Array<{
  entity: MeepleEntityType;
  border: RegExp;
  badgeBg: RegExp;
  text: RegExp;
}> = [
  {
    entity: 'game',
    border: /border-entity-game(?!-)/,
    badgeBg: /bg-entity-game\/10/,
    text: /text-entity-game-text/,
  },
  {
    entity: 'player',
    border: /border-entity-player(?!-)/,
    badgeBg: /bg-entity-player\/10/,
    text: /text-entity-player-text/,
  },
  {
    entity: 'session',
    border: /border-entity-session(?!-)/,
    badgeBg: /bg-entity-session\/10/,
    text: /text-entity-session-text/,
  },
  {
    entity: 'agent',
    border: /border-entity-agent(?!-)/,
    badgeBg: /bg-entity-agent\/10/,
    text: /text-entity-agent-text/,
  },
  {
    entity: 'kb',
    border: /border-entity-kb(?!-)/,
    badgeBg: /bg-entity-kb\/10/,
    text: /text-entity-kb-text/,
  },
  {
    entity: 'chat',
    border: /border-entity-chat(?!-)/,
    badgeBg: /bg-entity-chat\/10/,
    text: /text-entity-chat-text/,
  },
  {
    entity: 'event',
    border: /border-entity-event(?!-)/,
    badgeBg: /bg-entity-event\/10/,
    text: /text-entity-event-text/,
  },
  {
    entity: 'toolkit',
    border: /border-entity-toolkit(?!-)/,
    badgeBg: /bg-entity-toolkit\/10/,
    text: /text-entity-toolkit-text/,
  },
  {
    entity: 'tool',
    border: /border-entity-tool(?!-)/,
    badgeBg: /bg-entity-tool\/10/,
    text: /text-entity-tool-text/,
  },
];

describe('EntityTableView — canonical per-entity row border (#2955 Fase 2)', () => {
  it.each(CANONICAL)(
    'row border uses canonical border-entity-$entity token',
    ({ entity, border }) => {
      renderTable(entity);
      const layout = screen.getByTestId('table-layout');
      expect(layout.className).toMatch(border);
    }
  );

  it('row border carries NO hardcoded inline HSL (arbitrary value or style attr)', () => {
    renderTable('agent');
    const layout = screen.getByTestId('table-layout');
    expect(layout.className).not.toMatch(/hsl\(/);
    expect(layout.className).not.toMatch(/border-l-\[/);
    // The old design applied colour via an arbitrary class; nothing should set it inline now.
    expect(layout.getAttribute('style')).toBeNull();
  });
});

describe('EntityTableView — canonical per-entity badge pill (#2955 Fase 2)', () => {
  it.each(CANONICAL)(
    'badge for $entity uses soft tint bg-entity-$entity/10 + AA -text label',
    ({ entity, badgeBg, text }) => {
      renderTable(entity);
      const badge = screen.getByText('NEW');
      expect(badge.className).toMatch(badgeBg);
      expect(badge.className).toMatch(text);
    }
  );

  it('badge is no longer the flat neutral bg-muted', () => {
    renderTable('game');
    const badge = screen.getByText('NEW');
    expect(badge.className).not.toMatch(/bg-muted/);
    expect(badge.className).not.toMatch(/text-muted-foreground/);
  });
});

describe('EntityTableView — kb uses registered teal token, never -document (#2955)', () => {
  it('kb row border resolves to -kb (teal), never -document, no inline HSL', () => {
    renderTable('kb');
    const layout = screen.getByTestId('table-layout');
    expect(layout.className).toMatch(/border-entity-kb(?!-)/);
    expect(layout.className).not.toMatch(/entity-document/);
    expect(layout.className).not.toMatch(/hsl\(/);
  });

  it('kb badge uses -kb tint + -kb-text, never -document', () => {
    renderTable('kb');
    const badge = screen.getByText('NEW');
    expect(badge.className).toMatch(/bg-entity-kb\/10/);
    expect(badge.className).toMatch(/text-entity-kb-text/);
    expect(badge.className).not.toMatch(/entity-document/);
  });
});

describe('EntityTableView — gameNightEvent reuses canonical event token (#1929 WP2)', () => {
  it('maps gameNightEvent border + badge to canonical event token', () => {
    renderTable('gameNightEvent');
    const layout = screen.getByTestId('table-layout');
    expect(layout.className).toMatch(/border-entity-event(?!-)/);
    const badge = screen.getByText('NEW');
    expect(badge.className).toMatch(/bg-entity-event\/10/);
    expect(badge.className).toMatch(/text-entity-event-text/);
  });
});

describe('EntityTableView — a11y (#2955 Fase 2)', () => {
  it('has no axe AA violations with an entity-coloured badge', async () => {
    const { container } = renderTable('game');
    expect(await axe(container)).toHaveNoViolations();
  });
});
