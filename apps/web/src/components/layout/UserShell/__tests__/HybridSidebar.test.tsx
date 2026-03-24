import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useSearchParams: () => new URLSearchParams(),
}));

import { HybridSidebar } from '../HybridSidebar';

describe('HybridSidebar', () => {
  it('renders as navigation landmark', () => {
    render(<HybridSidebar />);
    expect(screen.getByRole('navigation', { name: /navigazione principale/i })).toBeInTheDocument();
  });

  it('renders icon buttons for main nav items', () => {
    render(<HybridSidebar />);
    expect(screen.getByLabelText(/dashboard/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/libreria/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/sessioni/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/chat/i)).toBeInTheDocument();
  });

  it('marks active item based on pathname', () => {
    render(<HybridSidebar />);
    const dashboardLink = screen.getByLabelText(/dashboard/i);
    expect(dashboardLink).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark inactive items as current', () => {
    render(<HybridSidebar />);
    const sessionLink = screen.getByLabelText(/sessioni/i);
    expect(sessionLink).not.toHaveAttribute('aria-current');
  });

  it('is hidden on mobile (has hidden and lg:flex classes)', () => {
    render(<HybridSidebar />);
    const nav = screen.getByRole('navigation', { name: /navigazione principale/i });
    expect(nav.className).toContain('hidden');
    expect(nav.className).toContain('lg:flex');
  });

  it('renders section labels', () => {
    render(<HybridSidebar />);
    expect(screen.getByText('Navigazione')).toBeInTheDocument();
    expect(screen.getByText('AI Assistant')).toBeInTheDocument();
    expect(screen.getByText('Collezioni')).toBeInTheDocument();
  });

  it('renders settings at the bottom', () => {
    render(<HybridSidebar />);
    expect(screen.getByLabelText(/impostazioni/i)).toBeInTheDocument();
  });

  it('has correct data-testid', () => {
    render(<HybridSidebar />);
    expect(screen.getByTestId('hybrid-sidebar')).toBeInTheDocument();
  });
});
