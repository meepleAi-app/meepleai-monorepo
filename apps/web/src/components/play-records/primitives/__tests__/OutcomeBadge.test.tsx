/**
 * OutcomeBadge (play-records) — Task 0.4 (Issue #1488).
 *
 * Presentational badge for a PlayRecord's outcome from the current user's POV.
 * Distinct from `components/features/sessions/OutcomeBadge` — sessions uses
 * `status × outcome × paused` matrix; play-records uses a single variant union
 * mapping directly to perspective.kind from `derivePerspective`.
 *
 * 6 variants:
 *   - won            (emerald — entity-tinted toolkit 🏆)
 *   - lost           (rose — entity-tinted event)
 *   - tie            (toolkit-tinted, 🤝)
 *   - cooperative    (toolkit-tinted, neutral — no winners pattern)
 *   - inprogress     (session-tinted, pulsing dot, role="status")
 *   - planned        (event-tinted, 📅 icon)
 *
 * Pure component — all i18n strings injected via `labels` prop. No
 * `useTranslations` here; the call-site supplies the catalog.
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { OutcomeBadge, type OutcomeBadgeProps } from '../OutcomeBadge';

const LABELS: OutcomeBadgeProps['labels'] = {
  won: '🏆 Vittoria',
  lost: '△ Sconfitta',
  tie: '🤝 Pareggio',
  cooperative: '🤝 Cooperativa',
  inprogress: 'In corso',
  planned: '📅 Pianificata',
};

function badge(variant: OutcomeBadgeProps['variant'], extra?: Partial<OutcomeBadgeProps>) {
  return render(<OutcomeBadge variant={variant} labels={LABELS} {...extra} />);
}

describe('OutcomeBadge (play-records)', () => {
  it('renders "won" variant with data-variant="won" + label', () => {
    badge('won');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el).not.toBeNull();
    expect(el!.getAttribute('data-variant')).toBe('won');
    expect(screen.getByText('🏆 Vittoria')).toBeTruthy();
  });

  it('renders "lost" variant', () => {
    badge('lost');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-variant')).toBe('lost');
    expect(screen.getByText('△ Sconfitta')).toBeTruthy();
  });

  it('renders "tie" variant', () => {
    badge('tie');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-variant')).toBe('tie');
    expect(screen.getByText('🤝 Pareggio')).toBeTruthy();
  });

  it('renders "cooperative" variant (EC-1)', () => {
    badge('cooperative');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-variant')).toBe('cooperative');
    expect(screen.getByText('🤝 Cooperativa')).toBeTruthy();
  });

  it('renders "inprogress" variant with pulsing dot + role="status"', () => {
    badge('inprogress');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-variant')).toBe('inprogress');
    expect(el!.getAttribute('role')).toBe('status');
    // pulsing dot is an aria-hidden span
    const dot = el!.querySelector('[aria-hidden="true"]');
    expect(dot).not.toBeNull();
    expect(screen.getByText('In corso')).toBeTruthy();
  });

  it('renders "planned" variant (EC-7)', () => {
    badge('planned');
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('data-variant')).toBe('planned');
    expect(screen.getByText('📅 Pianificata')).toBeTruthy();
  });

  it('accepts custom className', () => {
    badge('won', { className: 'test-class' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.classList.contains('test-class')).toBe(true);
  });

  it('all variants render data-slot="outcome-badge"', () => {
    const variants: OutcomeBadgeProps['variant'][] = [
      'won',
      'lost',
      'tie',
      'cooperative',
      'inprogress',
      'planned',
    ];
    for (const v of variants) {
      const { unmount } = render(<OutcomeBadge variant={v} labels={LABELS} />);
      const el = document.querySelector('[data-slot="outcome-badge"]');
      expect(el, `data-slot missing for variant: ${v}`).not.toBeNull();
      unmount();
    }
  });

  it('uses aria-label when provided (a11y override)', () => {
    badge('won', { 'aria-label': 'Custom label' });
    const el = document.querySelector('[data-slot="outcome-badge"]');
    expect(el!.getAttribute('aria-label')).toBe('Custom label');
  });
});
