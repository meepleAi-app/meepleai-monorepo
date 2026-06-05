import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { MainSidebar } from '../MainSidebar';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

describe('MainSidebar', () => {
  beforeEach(() => mockUseCurrentUser.mockReset());

  it('renders all 8 voci for an authenticated user', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);

    expect(screen.getByRole('link', { name: /Dashboard/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Library/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Games/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Game Nights/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Sessions/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Agents/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Notifications/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Profile/i })).toBeInTheDocument();
  });

  it('renders Games entry with ?tab=discover landing href (invariante #20)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    expect(screen.getByRole('link', { name: /Games/i })).toHaveAttribute(
      'href',
      '/games?tab=discover'
    );
  });

  it('does NOT render a standalone Discover voice (invariante #20)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    expect(screen.queryByRole('link', { name: /^Discover$/i })).not.toBeInTheDocument();
  });

  it('shows the Notifications badge when notificationCount > 0', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar notificationCount={5} />);
    expect(screen.getByLabelText(/5 unread/i)).toHaveTextContent('5');
  });

  it('hides the Notifications badge when notificationCount is 0 (MVP default)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    expect(screen.queryByLabelText(/unread/i)).not.toBeInTheDocument();
  });

  it('renders nothing meaningful for a null user (no items)', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<MainSidebar />);
    expect(screen.queryByRole('link', { name: /Dashboard/i })).not.toBeInTheDocument();
  });

  it('is hidden below lg and shown at lg+ (responsive class)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    const aside = screen.getByRole('navigation', { name: /main navigation/i }).closest('aside');
    expect(aside).toHaveClass('hidden');
    expect(aside).toHaveClass('lg:flex');
  });

  it('exposes the navigation landmark with the canonical aria-label', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument();
  });

  it('marks the current pathname item as aria-current="page" (Dashboard mock)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'user' } });
    render(<MainSidebar />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });
});
