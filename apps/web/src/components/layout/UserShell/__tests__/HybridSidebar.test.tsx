import { render, screen, within } from '@testing-library/react';
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

  it('renders all navigation section items', () => {
    render(<HybridSidebar />);
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /libreria/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sessioni/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /giocatori/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /storico partite/i })).toBeInTheDocument();
  });

  it('renders AI section items', () => {
    render(<HybridSidebar />);
    expect(screen.getByRole('link', { name: /chat rag/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /agenti/i })).toBeInTheDocument();
  });

  it('does not render wishlist as a nav item', () => {
    render(<HybridSidebar />);
    expect(screen.queryByRole('link', { name: /wishlist/i })).not.toBeInTheDocument();
  });

  it('does not render documenti as a nav item', () => {
    render(<HybridSidebar />);
    expect(screen.queryByRole('link', { name: /documenti/i })).not.toBeInTheDocument();
  });

  it('marks active item based on pathname', () => {
    render(<HybridSidebar />);
    const dashboardLink = screen.getByRole('link', { name: /dashboard/i });
    expect(dashboardLink).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark inactive items as current', () => {
    render(<HybridSidebar />);
    const sessionLink = screen.getByRole('link', { name: /sessioni/i });
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
    expect(screen.getByText('AI')).toBeInTheDocument();
  });

  it('renders settings at the bottom', () => {
    render(<HybridSidebar />);
    expect(screen.getByRole('link', { name: /impostazioni/i })).toBeInTheDocument();
  });

  it('has correct data-testid', () => {
    render(<HybridSidebar />);
    expect(screen.getByTestId('hybrid-sidebar')).toBeInTheDocument();
  });

  it('renders emoji icons instead of SVG icons', () => {
    render(<HybridSidebar />);
    const dashboardLink = screen.getByRole('link', { name: /dashboard/i });
    expect(within(dashboardLink).getByRole('img')).toBeInTheDocument();
  });
});
