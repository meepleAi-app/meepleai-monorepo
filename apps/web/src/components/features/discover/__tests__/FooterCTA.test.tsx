/**
 * Issue #1483 — FooterCTA unit tests.
 *
 * Gradient footer section at the bottom of /discover with title, optional description,
 * primary CTA link, and optional secondary CTA link.
 * Uses Next.js Link (mocked as <a> in tests).
 *
 * Contract:
 *   - data-slot="discover-footer-cta" on the root section
 *   - title is rendered in an h2
 *   - description is optional — rendered as <p> when present, absent otherwise
 *   - primaryCta renders a link with the given label and href
 *   - secondaryCta renders a second link when provided, absent when omitted
 */

import { render, screen } from '@testing-library/react';
import { type ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { FooterCTA } from '../FooterCTA';

describe('FooterCTA', () => {
  it('renders data-slot="discover-footer-cta" on the root section', () => {
    const { container } = render(
      <FooterCTA
        title="Crea il tuo agente"
        primaryCta={{ label: 'Inizia ora', href: '/agents/new' }}
      />
    );
    const root = container.querySelector('[data-slot="discover-footer-cta"]');
    expect(root).not.toBeNull();
    expect(root?.tagName.toLowerCase()).toBe('section');
  });

  it('renders the title in an h2', () => {
    render(
      <FooterCTA title="Esplora la community" primaryCta={{ label: 'Scopri', href: '/discover' }} />
    );
    expect(
      screen.getByRole('heading', { level: 2, name: 'Esplora la community' })
    ).toBeInTheDocument();
  });

  it('renders description when provided', () => {
    render(
      <FooterCTA
        title="Footer"
        description="Unisciti alla community di MeepleAI."
        primaryCta={{ label: 'Vai', href: '/community' }}
      />
    );
    expect(screen.getByText('Unisciti alla community di MeepleAI.')).toBeInTheDocument();
  });

  it('does not render description when omitted', () => {
    render(<FooterCTA title="Footer" primaryCta={{ label: 'Vai', href: '/community' }} />);
    // No paragraph element for description
    expect(screen.queryByText(/Unisciti/)).toBeNull();
  });

  it('renders primary CTA link with correct label and href', () => {
    render(<FooterCTA title="Footer" primaryCta={{ label: 'Inizia gratis', href: '/register' }} />);
    const link = screen.getByRole('link', { name: 'Inizia gratis' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA link when provided', () => {
    render(
      <FooterCTA
        title="Footer"
        primaryCta={{ label: 'Inizia', href: '/start' }}
        secondaryCta={{ label: 'Scopri di più', href: '/about' }}
      />
    );
    const secondaryLink = screen.getByRole('link', { name: 'Scopri di più' });
    expect(secondaryLink).toBeInTheDocument();
    expect(secondaryLink).toHaveAttribute('href', '/about');
  });

  it('does not render secondary CTA when omitted', () => {
    render(<FooterCTA title="Footer" primaryCta={{ label: 'Inizia', href: '/start' }} />);
    const links = screen.getAllByRole('link');
    // Only the primary CTA link
    expect(links).toHaveLength(1);
    expect(links[0]).toHaveAttribute('href', '/start');
  });

  it('renders both CTA links when both are provided', () => {
    render(
      <FooterCTA
        title="Footer"
        primaryCta={{ label: 'Primario', href: '/primary' }}
        secondaryCta={{ label: 'Secondario', href: '/secondary' }}
      />
    );
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(2);
    expect(links[0]).toHaveAttribute('href', '/primary');
    expect(links[1]).toHaveAttribute('href', '/secondary');
  });
});
