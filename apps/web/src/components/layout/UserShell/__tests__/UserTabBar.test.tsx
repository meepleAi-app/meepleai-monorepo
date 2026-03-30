import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/dashboard'),
}));

import { usePathname } from 'next/navigation';
import { UserTabBar } from '../UserTabBar';

describe('UserTabBar', () => {
  it('renders 4 tab links', () => {
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /home/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /libreria/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /chat/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /profilo/i })).toBeInTheDocument();
  });

  it('renders the contextual FAB link', () => {
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /azione principale/i })).toBeInTheDocument();
  });

  it('FAB links to /sessions/new when no section CTA (dashboard)', () => {
    vi.mocked(usePathname).mockReturnValue('/dashboard');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /azione principale/i })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
  });

  it('FAB links to /catalog on /library', () => {
    vi.mocked(usePathname).mockReturnValue('/library');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /azione principale/i })).toHaveAttribute(
      'href',
      '/catalog'
    );
  });

  it('FAB links to /sessions/new on /sessions', () => {
    vi.mocked(usePathname).mockReturnValue('/sessions');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /azione principale/i })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
  });

  it('FAB links to /agents/new on /agents', () => {
    vi.mocked(usePathname).mockReturnValue('/agents');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /azione principale/i })).toHaveAttribute(
      'href',
      '/agents/new'
    );
  });

  it('marks home tab active on /dashboard', () => {
    vi.mocked(usePathname).mockReturnValue('/dashboard');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /home/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('marks chat tab active on /chat', () => {
    vi.mocked(usePathname).mockReturnValue('/chat');
    render(<UserTabBar />);
    expect(screen.getByRole('link', { name: /chat/i })).toHaveAttribute('aria-selected', 'true');
  });
});
