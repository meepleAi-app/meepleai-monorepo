/**
 * LandingFooter Tests
 *
 * Unit tests for landing page footer component.
 */

import { render, screen } from '@testing-library/react';
import { LandingFooter } from '../LandingFooter';
import { vi } from 'vitest';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => {
    return <a href={href}>{children}</a>;
  },
}));

describe('LandingFooter', () => {
  it('renders footer element', () => {
    const { container } = render(<LandingFooter />);

    const footer = container.querySelector('footer');
    expect(footer).toBeInTheDocument();
  });

  it('renders CTA heading', () => {
    render(<LandingFooter />);

    expect(screen.getByRole('heading', { name: /pronto a iniziare/i })).toBeInTheDocument();
  });

  it('renders CTA description', () => {
    render(<LandingFooter />);

    expect(screen.getByText(/unisciti a migliaia di giocatori/i)).toBeInTheDocument();
  });

  it('renders primary CTA button linking to /register', () => {
    render(<LandingFooter />);

    const registerButton = screen.getByRole('link', { name: /registrati ora/i });
    expect(registerButton).toBeInTheDocument();
    expect(registerButton).toHaveAttribute('href', '/register');
  });

  it('renders secondary CTA button linking to /login', () => {
    render(<LandingFooter />);

    const loginButton = screen.getByRole('link', { name: /accedi/i });
    expect(loginButton).toBeInTheDocument();
    expect(loginButton).toHaveAttribute('href', '/login');
  });

  it('renders copyright text with current year', () => {
    render(<LandingFooter />);

    const currentYear = new Date().getFullYear();
    expect(
      screen.getByText(`© ${currentYear} MeepleAI. Tutti i diritti riservati.`)
    ).toBeInTheDocument();
  });

  it('renders legal navigation links', () => {
    render(<LandingFooter />);

    const privacyLink = screen.getByRole('link', { name: /privacy/i });
    const termsLink = screen.getByRole('link', { name: /termini/i });
    const apiDocsLink = screen.getByRole('link', { name: /api docs/i });

    expect(privacyLink).toHaveAttribute('href', '/privacy');
    expect(termsLink).toHaveAttribute('href', '/terms');
    expect(apiDocsLink).toHaveAttribute('href', '/api/docs');
  });

  it('applies responsive layout classes', () => {
    const { container } = render(<LandingFooter />);

    const flexContainer = container.querySelector('.flex.flex-col');
    expect(flexContainer).toBeInTheDocument();
  });
});
