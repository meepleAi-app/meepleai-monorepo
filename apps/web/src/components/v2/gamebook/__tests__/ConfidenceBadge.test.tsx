/**
 * ConfidenceBadge unit tests — SP6 Phase C.2.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot="confidence-badge" identity
 *   - data-level attribute matches prop
 *   - 3 levels render correct glyph (✓ / ◐ / ⚠)
 *   - role="img" + aria-label from labels prop
 *   - sm/md sizes apply correct classes
 *   - palette colors applied via inline style
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ConfidenceBadge, type ConfidenceBadgeLabels } from '../ConfidenceBadge';

const LABELS: ConfidenceBadgeLabels = {
  high: 'Alta confidenza',
  medium: 'Media confidenza',
  low: 'Bassa confidenza',
};

describe('ConfidenceBadge', () => {
  it('renders data-slot="confidence-badge"', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    expect(document.querySelector('[data-slot="confidence-badge"]')).not.toBeNull();
  });

  it('renders data-level matching prop (high)', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.getAttribute('data-level')).toBe('high');
  });

  it('renders data-level matching prop (medium)', () => {
    render(<ConfidenceBadge level="medium" labels={LABELS} />);
    expect(document.querySelector('[data-level="medium"]')).not.toBeNull();
  });

  it('renders data-level matching prop (low)', () => {
    render(<ConfidenceBadge level="low" labels={LABELS} />);
    expect(document.querySelector('[data-level="low"]')).not.toBeNull();
  });

  it('renders ✓ glyph for high', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.textContent).toBe('✓');
  });

  it('renders ◐ glyph for medium', () => {
    render(<ConfidenceBadge level="medium" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.textContent).toBe('◐');
  });

  it('renders ⚠ glyph for low', () => {
    render(<ConfidenceBadge level="low" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.textContent).toBe('⚠');
  });

  it('exposes role="img" with aria-label from labels.high', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    expect(screen.getByRole('img', { name: 'Alta confidenza' })).toBeInTheDocument();
  });

  it('exposes role="img" with aria-label from labels.medium', () => {
    render(<ConfidenceBadge level="medium" labels={LABELS} />);
    expect(screen.getByRole('img', { name: 'Media confidenza' })).toBeInTheDocument();
  });

  it('exposes role="img" with aria-label from labels.low', () => {
    render(<ConfidenceBadge level="low" labels={LABELS} />);
    expect(screen.getByRole('img', { name: 'Bassa confidenza' })).toBeInTheDocument();
  });

  it('uses sm size by default (h-4 w-4)', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.className).toContain('h-4');
    expect(badge?.className).toContain('w-4');
  });

  it('applies md size class when size="md"', () => {
    render(<ConfidenceBadge level="high" size="md" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.className).toContain('h-5');
    expect(badge?.className).toContain('w-5');
  });

  it('applies inline-style backgroundColor and color from palette', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]') as HTMLElement;
    expect(badge.style.backgroundColor).toBeTruthy();
    expect(badge.style.color).toBeTruthy();
  });

  it('applies className when provided', () => {
    render(<ConfidenceBadge level="high" labels={LABELS} className="extra-class" />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.classList.contains('extra-class')).toBe(true);
  });

  it('exposes title attribute equal to aria-label', () => {
    render(<ConfidenceBadge level="medium" labels={LABELS} />);
    const badge = document.querySelector('[data-slot="confidence-badge"]');
    expect(badge?.getAttribute('title')).toBe('Media confidenza');
  });
});
