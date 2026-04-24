import { render, within } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MeepleCard } from '../MeepleCard';

import type { ConnectionChipProps, NavFooterItem } from '../types';

/**
 * Parity guard between the canonical `connections` rendering path and the
 * navItems → connections adapter path (`__useConnectionsForNavItems`).
 *
 * If this test fails, the adapter has drifted from the canonical renderer.
 * The two demos in /dev/meeple-card (Step 1.6 section) must continue to look
 * visually equivalent.
 *
 * Note (Task 4 migration): Demo B now uses a hand-built `NavFooterItem[]`
 * instead of `buildGameNavItems()` because the game builder now produces
 * `ConnectionChipProps[]` directly. The adapter path is still exercised by
 * other builders (session, agent) until Task 5/6 migrations.
 */
describe('ConnectionSource renderer parity (Demo A ≡ Demo B)', () => {
  const sharedConnections: ConnectionChipProps[] = [
    { entityType: 'session', count: 5, label: 'Sessioni' },
    { entityType: 'agent', count: 2, label: 'Agenti' },
    { entityType: 'kb', count: 1, label: 'KB' },
  ];

  // Legacy NavFooterItem[] shape — exercises the adapter path intentionally.
  // Icons are deliberately null (adapter preserves them via iconOverride; render
  // path falls back to default Lucide icons for each entityType).
  const legacyNavItems: NavFooterItem[] = [
    { icon: null, label: 'KB', entity: 'kb', count: 1 },
    { icon: null, label: 'Agent', entity: 'agent', count: 2 },
    {
      icon: null,
      label: 'Chat',
      entity: 'chat',
      count: 0,
      showPlus: true,
      onPlusClick: () => {},
    },
    { icon: null, label: 'Sessioni', entity: 'session', count: 5 },
  ];

  function renderDemoA() {
    return render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        subtitle="Stonemaier Games"
        connectionsVariant="footer"
        connections={sharedConnections}
      />
    );
  }

  function renderDemoB() {
    return render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        subtitle="Stonemaier Games"
        __useConnectionsForNavItems
        navItems={legacyNavItems}
      />
    );
  }

  it('both paths render exactly one ConnectionChipStrip', () => {
    const a = renderDemoA();
    const b = renderDemoB();

    expect(within(a.container).queryAllByTestId('connection-chip-strip')).toHaveLength(1);
    expect(within(b.container).queryAllByTestId('connection-chip-strip')).toHaveLength(1);
  });

  it('both paths render the same number of count badges (count > 0 chips)', () => {
    const a = renderDemoA();
    const b = renderDemoB();

    // Demo A: 3 chips with count > 0 (5, 2, 1)
    // Demo B: 3 chips with count > 0 via adapter (kb=1, agent=2, session=5)
    //         + 1 chat chip with count=0 (renders plus, no badge)
    const aBadges = within(a.container).queryAllByTestId('connection-chip-badge');
    const bBadges = within(b.container).queryAllByTestId('connection-chip-badge');

    expect(aBadges).toHaveLength(3);
    expect(bBadges).toHaveLength(3);
  });

  it('both paths render footer-variant strip (data-strip-variant="footer")', () => {
    const a = renderDemoA();
    const b = renderDemoB();

    expect(within(a.container).getByTestId('connection-chip-strip')).toHaveAttribute(
      'data-strip-variant',
      'footer'
    );
    expect(within(b.container).getByTestId('connection-chip-strip')).toHaveAttribute(
      'data-strip-variant',
      'footer'
    );
  });

  it('neither path renders the legacy NavFooter when adapter or connections are active', () => {
    const a = renderDemoA();
    const b = renderDemoB();

    expect(within(a.container).queryByTestId('nav-footer')).toBeNull();
    expect(within(b.container).queryByTestId('nav-footer')).toBeNull();
  });
});
