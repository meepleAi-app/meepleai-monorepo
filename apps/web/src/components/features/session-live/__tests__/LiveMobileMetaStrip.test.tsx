/**
 * LiveMobileMetaStrip unit tests — #3146 Slice 1 (session-live mobile parity).
 *
 * The strip surfaces the session-state chips (turn / elapsed / derived start
 * time) on the mobile layout (<lg), where the LiveTopBar chips are hidden. It
 * is the always-visible mobile counterpart to the topbar chips.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { LiveMobileMetaStripProps } from '../LiveMobileMetaStrip';
import { LiveMobileMetaStrip } from '../LiveMobileMetaStrip';

function renderStrip(overrides: Partial<LiveMobileMetaStripProps> = {}) {
  const props: LiveMobileMetaStripProps = {
    turnLabel: 'Turno 3/8',
    elapsedLabel: '02:34:56',
    startedAtLabel: '▶ Ora di inizio 5 lug, 20:35 · derivata',
    labels: {
      elapsedAriaLabel: 'Tempo trascorso',
      startedAtAriaLabel: 'Ora di inizio della sessione (derivata)',
    },
    ...overrides,
  };
  return render(<LiveMobileMetaStrip {...props} />);
}

describe('LiveMobileMetaStrip (#3146 Slice 1)', () => {
  it('renders turn, elapsed and derived start-time', () => {
    renderStrip();
    expect(screen.getByText('Turno 3/8')).toBeInTheDocument();
    const elapsed = document.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]');
    expect(elapsed?.textContent).toBe('02:34:56');
    const started = document.querySelector('[data-slot="live-mobile-meta-strip-started-at"]');
    expect(started?.textContent).toBe('▶ Ora di inizio 5 lug, 20:35 · derivata');
  });

  it('is a mobile-only strip (hidden at lg+)', () => {
    renderStrip();
    const strip = document.querySelector('[data-slot="live-mobile-meta-strip"]');
    expect(strip).not.toBeNull();
    expect(strip?.className).toContain('lg:hidden');
  });

  it('attaches aria-labels to the elapsed and start-time chips', () => {
    renderStrip();
    expect(
      document
        .querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')
        ?.getAttribute('aria-label')
    ).toBe('Tempo trascorso');
    expect(
      document
        .querySelector('[data-slot="live-mobile-meta-strip-started-at"]')
        ?.getAttribute('aria-label')
    ).toBe('Ora di inizio della sessione (derivata)');
  });

  it('omits the elapsed chip when elapsedLabel is absent', () => {
    renderStrip({ elapsedLabel: undefined });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')).toBeNull();
    // …but still renders the others.
    expect(screen.getByText('Turno 3/8')).toBeInTheDocument();
  });

  it('omits the turn chip when turnLabel is absent/empty', () => {
    renderStrip({ turnLabel: '' });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-turn"]')).toBeNull();
  });

  it('renders nothing when all fields are absent (no empty chrome)', () => {
    const { container } = renderStrip({
      turnLabel: '',
      elapsedLabel: undefined,
      startedAtLabel: undefined,
    });
    expect(container.querySelector('[data-slot="live-mobile-meta-strip"]')).toBeNull();
  });
});
