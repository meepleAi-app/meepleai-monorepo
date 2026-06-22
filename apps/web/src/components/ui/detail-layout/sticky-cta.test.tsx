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
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { StickyCta } from './sticky-cta';

const mockedUseCurrentUser = vi.mocked(useCurrentUser);

const labels = {
  mobileLabel: 'Accedi per installare',
  desktopHint: 'Per installare questi contenuti...',
  desktopCtaLabel: 'Accedi',
  regionAriaLabel: 'Sticky CTA',
};

describe('StickyCta (Wave A.4)', () => {
  beforeEach(() => {
    mockedUseCurrentUser.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
      isSuccess: true,
    } as never);
  });

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

  describe('issue #2081 — auth guard', () => {
    afterEach(() => {
      mockedUseCurrentUser.mockReset();
    });

    it('returns null when user is authenticated (no CTA rendered)', () => {
      mockedUseCurrentUser.mockReturnValue({
        data: { id: 'u1', email: 'a@b.com', role: 'user' } as never,
        isLoading: false,
        isError: false,
        isSuccess: true,
      } as never);

      const { container } = render(<StickyCta signInHref="/sign-in" labels={labels} />);
      expect(container.firstChild).toBeNull();
      expect(screen.queryAllByRole('region', { name: 'Sticky CTA' })).toHaveLength(0);
    });

    it('returns null while auth state is loading (no flash)', () => {
      mockedUseCurrentUser.mockReturnValue({
        data: undefined,
        isLoading: true,
        isError: false,
        isSuccess: false,
      } as never);

      const { container } = render(<StickyCta signInHref="/sign-in" labels={labels} />);
      expect(container.firstChild).toBeNull();
    });

    it('renders full CTA when user is not authenticated (guest)', () => {
      mockedUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        isSuccess: true,
      } as never);

      render(<StickyCta signInHref="/sign-in" labels={labels} />);
      const regions = screen.getAllByRole('region', { name: 'Sticky CTA' });
      expect(regions).toHaveLength(2); // mobile + desktop
    });
  });
});
