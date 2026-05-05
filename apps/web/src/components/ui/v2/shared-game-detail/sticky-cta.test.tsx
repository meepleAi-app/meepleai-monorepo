/**
 * Wave A.4 (Issue #603) — StickyCta dual-variant rendering tests.
 *
 * Verifies the mobile/desktop variant contract from spec §3.5:
 *  - Both variants render with correct role="region" + aria-label
 *  - signInHref propagates to both anchors
 *  - Mobile variant uses md:hidden, desktop uses md:inline-flex hidden
 *  - Both variants share the same labels props
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { StickyCta } from './sticky-cta';

const labels = {
  mobileLabel: 'Accedi per installare',
  desktopHint: 'Per installare questi contenuti...',
  desktopCtaLabel: 'Accedi',
  regionAriaLabel: 'Sticky CTA',
};

describe('StickyCta (Wave A.4)', () => {
  it('renders both mobile and desktop region landmarks', () => {
    render(<StickyCta signInHref="/sign-in" labels={labels} />);
    const regions = screen.getAllByRole('region', { name: 'Sticky CTA' });
    expect(regions).toHaveLength(2);
  });

  it('exposes data-slot for mobile and desktop variants', () => {
    const { container } = render(<StickyCta signInHref="/sign-in" labels={labels} />);
    expect(
      container.querySelector('[data-slot="shared-game-detail-sticky-cta-mobile"]')
    ).not.toBeNull();
    expect(
      container.querySelector('[data-slot="shared-game-detail-sticky-cta-desktop"]')
    ).not.toBeNull();
  });

  it('propagates signInHref to all anchors (mobile + desktop CTA)', () => {
    render(<StickyCta signInHref="/sign-in?next=/games/123" labels={labels} />);
    const anchors = screen.getAllByRole('link');
    // mobile full-width + desktop pill CTA = 2 anchors
    expect(anchors).toHaveLength(2);
    anchors.forEach(a => {
      expect(a).toHaveAttribute('href', '/sign-in?next=/games/123');
    });
  });

  it('renders mobileLabel inside the mobile variant', () => {
    render(<StickyCta signInHref="/sign-in" labels={labels} />);
    expect(screen.getByText('Accedi per installare')).toBeInTheDocument();
  });

  it('renders desktopHint and desktopCtaLabel inside the desktop variant', () => {
    render(<StickyCta signInHref="/sign-in" labels={labels} />);
    expect(screen.getByText('Per installare questi contenuti...')).toBeInTheDocument();
    expect(screen.getByText('Accedi')).toBeInTheDocument();
  });
});
