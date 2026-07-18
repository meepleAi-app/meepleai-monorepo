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

  it('omits the elapsed chip when elapsedLabel is absent (siblings survive)', () => {
    renderStrip({ elapsedLabel: undefined });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')).toBeNull();
    // …but still renders the others.
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-turn"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="live-mobile-meta-strip-started-at"]')
    ).not.toBeNull();
  });

  it('omits the turn chip when turnLabel is absent/empty (siblings survive)', () => {
    renderStrip({ turnLabel: '' });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-turn"]')).toBeNull();
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="live-mobile-meta-strip-started-at"]')
    ).not.toBeNull();
  });

  it('omits the start-time chip when startedAtLabel is absent (siblings survive)', () => {
    renderStrip({ startedAtLabel: undefined });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-started-at"]')).toBeNull();
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-turn"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="live-mobile-meta-strip-elapsed"]')).not.toBeNull();
  });

  // Single-field renders — guard against an over-narrowed early-return that
  // would blank a turn-only or start-time-only mobile session.
  it('renders when turn is the only field', () => {
    renderStrip({ turnLabel: 'Turno 1/2', elapsedLabel: undefined, startedAtLabel: undefined });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip"]')).not.toBeNull();
    expect(screen.getByText('Turno 1/2')).toBeInTheDocument();
  });

  it('renders when the derived start-time is the only field', () => {
    renderStrip({
      turnLabel: '',
      elapsedLabel: undefined,
      startedAtLabel: '▶ Ora di inizio 5 lug, 20:35 · derivata',
    });
    expect(document.querySelector('[data-slot="live-mobile-meta-strip"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="live-mobile-meta-strip-started-at"]')?.textContent
    ).toBe('▶ Ora di inizio 5 lug, 20:35 · derivata');
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
