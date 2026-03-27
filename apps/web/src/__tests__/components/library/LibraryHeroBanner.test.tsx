/**
 * LibraryHeroBanner Component Tests
 *
 * Test Coverage:
 * - Discovery variant renders "Scopri nuovi giochi" with catalog link
 * - hide prop returns null
 * - className forwarding
 *
 * Note: Session variant will be added when sessions scheduling API is ready.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LibraryHeroBanner } from '@/components/library/LibraryHeroBanner';

// Mock next/link to render a plain anchor
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('LibraryHeroBanner', () => {
  it('returns null when hide is true', () => {
    const { container } = render(<LibraryHeroBanner hide />);
    expect(container.innerHTML).toBe('');
  });

  it('renders discovery variant with title and subtitle', () => {
    render(<LibraryHeroBanner />);

    expect(screen.getByText('Scopri nuovi giochi')).toBeInTheDocument();
    expect(
      screen.getByText('Esplora il catalogo e arricchisci la tua collezione')
    ).toBeInTheDocument();
  });

  it('renders catalog link', () => {
    render(<LibraryHeroBanner />);

    const link = screen.getByRole('link', { name: /Esplora Catalogo/i });
    expect(link).toHaveAttribute('href', '/library?tab=public');
  });

  it('has correct aria-label on region', () => {
    render(<LibraryHeroBanner />);
    expect(screen.getByRole('region', { name: 'Esplora il catalogo' })).toBeInTheDocument();
  });

  it('forwards className', () => {
    const { container } = render(<LibraryHeroBanner className="mt-4" />);
    const banner = container.firstElementChild;
    expect(banner!.className).toContain('mt-4');
  });
});
