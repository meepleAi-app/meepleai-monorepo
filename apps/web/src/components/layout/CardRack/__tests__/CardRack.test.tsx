import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { CardRack } from '../CardRack';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

describe('CardRack', () => {
  it('renders as a nav element with correct aria-label', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    expect(nav).toBeInTheDocument();
  });

  it('renders all primary navigation items', () => {
    render(<CardRack />);
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Scopri' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Chat AI' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Serate' })).toBeInTheDocument();
  });

  it('renders bottom navigation items', () => {
    render(<CardRack />);
    expect(screen.getByRole('link', { name: 'Agenti' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Badge' })).toBeInTheDocument();
  });

  it('is hidden on mobile (has hidden md:flex classes)', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    expect(nav.className).toContain('hidden');
    expect(nav.className).toContain('md:flex');
  });

  it('uses card-rack-width CSS variable by default (collapsed)', () => {
    render(<CardRack />);
    const nav = screen.getByRole('navigation', { name: /sidebar/i });
    expect(nav.className).toContain('w-[var(--card-rack-width,64px)]');
  });
});
