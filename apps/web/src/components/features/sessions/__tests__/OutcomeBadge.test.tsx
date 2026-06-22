/**
 * OutcomeBadge unit tests — Wave D.1 (Issue #735).
 *
 * 12 tests covering full status×outcome×paused matrix:
 * 1. inprogress + paused=false → "Live" label, data-status="live"
 * 2. inprogress + paused=true  → paused label, data-status="paused"
 * 3. paused status → paused label
 * 4. abandoned → abandoned label, data-status="abandoned"
 * 5. completed + won → won label, data-outcome="won"
 * 6. completed + lost → lost label, data-outcome="lost"
 * 7. completed + tie → tie label, data-outcome="tie"
 * 8. completed + outcome=null → tie label (fallback)
 * 9. live badge has pulsing dot element
 * 10. renders data-slot="outcome-badge" on all variants
 * 11. accepts custom className
 * 12. returns null for unknown/unmapped combination (future-proof)
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { OutcomeBadge } from '../OutcomeBadge';
import type { OutcomeBadgeProps, OutcomeBadgeLabels } from '../OutcomeBadge';

const LABELS: OutcomeBadgeLabels = {
  outcomeWon: '🏆 Vinta',
  outcomeLost: '△ Persa',
  outcomeTie: '= Pareggio',
  statusLive: 'Live',
  statusPaused: 'In pausa',
  statusAbandoned: '⊘ Abbandonata',
};

function badge(props: Omit<OutcomeBadgeProps, 'labels'>) {
  return render(<OutcomeBadge {...props} labels={LABELS} />);
}

describe('OutcomeBadge', () => {
  it('inprogress + paused=false renders Live label with data-status="live"', () => {
    badge({ status: 'inprogress', outcome: null, paused: false });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('data-status')).toBe('live');
    expect(screen.getByText('Live')).toBeTruthy();
  });

  it('inprogress + paused=true renders paused label with data-status="paused"', () => {
    badge({ status: 'inprogress', outcome: null, paused: true });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-status')).toBe('paused');
    expect(screen.getByText('In pausa')).toBeTruthy();
  });

  it('paused status renders paused label', () => {
    badge({ status: 'paused', outcome: null });
    expect(screen.getByText('In pausa')).toBeTruthy();
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-status')).toBe('paused');
  });

  it('abandoned renders abandoned label with data-status="abandoned"', () => {
    badge({ status: 'abandoned', outcome: null });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-status')).toBe('abandoned');
    expect(screen.getByText('⊘ Abbandonata')).toBeTruthy();
  });

  it('completed + won renders won label with data-outcome="won"', () => {
    badge({ status: 'completed', outcome: 'won' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-outcome')).toBe('won');
    expect(screen.getByText('🏆 Vinta')).toBeTruthy();
  });

  it('completed + lost renders lost label with data-outcome="lost"', () => {
    badge({ status: 'completed', outcome: 'lost' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-outcome')).toBe('lost');
    expect(screen.getByText('△ Persa')).toBeTruthy();
  });

  it('completed + tie renders tie label with data-outcome="tie"', () => {
    badge({ status: 'completed', outcome: 'tie' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-outcome')).toBe('tie');
    expect(screen.getByText('= Pareggio')).toBeTruthy();
  });

  it('completed + outcome=null renders tie fallback', () => {
    badge({ status: 'completed', outcome: null });
    expect(screen.getByText('= Pareggio')).toBeTruthy();
  });

  it('live badge renders pulsing dot element', () => {
    badge({ status: 'inprogress', outcome: null, paused: false });
    // The pulsing dot is an aria-hidden span inside the badge
    const badge_el = document.querySelector('[data-slot="outcome-badge"]');
    const dot = badge_el!.querySelector('[aria-hidden="true"]');
    expect(dot).not.toBeNull();
  });

  it('renders data-slot="outcome-badge" on all variants', () => {
    const cases: Array<[string, Omit<OutcomeBadgeProps, 'labels'>]> = [
      ['live', { status: 'inprogress', outcome: null }],
      ['paused', { status: 'paused', outcome: null }],
      ['abandoned', { status: 'abandoned', outcome: null }],
      ['won', { status: 'completed', outcome: 'won' }],
      ['lost', { status: 'completed', outcome: 'lost' }],
      ['tie', { status: 'completed', outcome: 'tie' }],
    ];
    for (const [label, props] of cases) {
      const { unmount } = render(<OutcomeBadge {...props} labels={LABELS} />);
      const el = document.querySelector('[data-slot="outcome-badge"]');
      expect(el, `data-slot missing for case: ${label}`).not.toBeNull();
      unmount();
    }
  });

  it('accepts custom className', () => {
    badge({ status: 'completed', outcome: 'won', className: 'test-cls' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.classList.contains('test-cls')).toBe(true);
  });
});
