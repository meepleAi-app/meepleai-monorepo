/**
 * Wave A.4 follow-up (Issue #615) — NotFoundState rendering tests.
 *
 * Verifies the dedicated 404 surface contract:
 *  - role="status" for assistive tech (non-error, informational)
 *  - h2 title + description + anchor CTA
 *  - data-slot for query stability
 *  - Default backHref='/shared-games' + override
 *  - Optional className passthrough
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { NotFoundState } from './not-found-state';

const baseLabels = {
  title: 'Gioco non trovato',
  description: 'Il gioco potrebbe essere stato rimosso o non è più disponibile.',
  backLabel: 'Torna al catalogo',
};

describe('NotFoundState (Wave A.4 follow-up)', () => {
  it('renders title and description text', () => {
    render(<NotFoundState labels={baseLabels} />);
    expect(
      screen.getByRole('heading', { level: 2, name: 'Gioco non trovato' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Il gioco potrebbe essere stato rimosso o non è più disponibile.')
    ).toBeInTheDocument();
  });

  it('exposes role="status" for non-disruptive assistive announcement', () => {
    render(<NotFoundState labels={baseLabels} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders back CTA as anchor with default href=/shared-games', () => {
    render(<NotFoundState labels={baseLabels} />);
    const link = screen.getByRole('link', { name: 'Torna al catalogo' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/shared-games');
  });

  it('honors backHref override', () => {
    render(<NotFoundState labels={baseLabels} backHref="/custom/path" />);
    const link = screen.getByRole('link', { name: 'Torna al catalogo' });
    expect(link).toHaveAttribute('href', '/custom/path');
  });

  it('exposes data-slot attribute', () => {
    const { container } = render(<NotFoundState labels={baseLabels} />);
    const root = container.querySelector('[data-slot="shared-game-detail-not-found-state"]');
    expect(root).not.toBeNull();
  });

  it('passes through optional className', () => {
    const { container } = render(<NotFoundState labels={baseLabels} className="my-custom-class" />);
    const root = container.querySelector('[data-slot="shared-game-detail-not-found-state"]');
    expect(root?.className).toContain('my-custom-class');
  });
});
