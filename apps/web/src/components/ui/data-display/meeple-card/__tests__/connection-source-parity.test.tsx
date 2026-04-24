import { render, within } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MeepleCard } from '../MeepleCard';
import { buildGameNavItems } from '../nav-items';

import type { ConnectionChipProps } from '../types';

/**
 * Parity guard between the canonical `connections` rendering path and the
 * navItems → connections adapter path (`__useConnectionsForNavItems`).
 *
 * If this test fails, the adapter has drifted from the canonical renderer.
 * The two demos in /dev/meeple-card (Step 1.6 section) must continue to look
 * visually equivalent.
 */
describe('ConnectionSource renderer parity (Demo A ≡ Demo B)', () => {
  const sharedConnections: ConnectionChipProps[] = [
    { entityType: 'session', count: 5, label: 'Sessioni' },
    { entityType: 'agent', count: 2, label: 'Agenti' },
    { entityType: 'kb', count: 1, label: 'KB' },
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
        navItems={buildGameNavItems(
          { kbCount: 1, agentCount: 2, chatCount: 0, sessionCount: 5 },
          {
            onKbClick: () => {},
            onAgentClick: () => {},
            onSessionClick: () => {},
            onChatPlus: () => {},
          }
        )}
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
    // Demo B: 3 chips with count > 0 from buildGameNavItems (sessions=5, agents=2, kb=1)
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
