import { render, screen } from '@testing-library/react';
import type { ComponentType } from 'react';
import { describe, it, expect, vi } from 'vitest';

import { MainNavList, isPathActive } from '../MainNavList';
import type { MainNavItem } from '../main-nav-config';

const NullIcon: ComponentType<{ className?: string }> = () => null;

const ITEMS: ReadonlyArray<MainNavItem> = [
  { id: 'dashboard', label: 'Dashboard', defaultHref: '/dashboard', icon: NullIcon },
  { id: 'library', label: 'Library', defaultHref: '/library', icon: NullIcon, gameRelated: true },
  {
    id: 'games',
    label: 'Games',
    defaultHref: '/games?tab=discover',
    icon: NullIcon,
    gameRelated: true,
  },
  {
    id: 'notifications',
    label: 'Notifications',
    defaultHref: '/notifications',
    icon: NullIcon,
    showCounter: true,
  },
];

describe('MainNavList', () => {
  it('renders all items as links pointing to defaultHref', () => {
    render(<MainNavList items={ITEMS} pathname="/dashboard" />);

    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute('href', '/dashboard');
    expect(screen.getByRole('link', { name: /Library/i })).toHaveAttribute('href', '/library');
    expect(screen.getByRole('link', { name: /Games/i })).toHaveAttribute(
      'href',
      '/games?tab=discover'
    );
    expect(screen.getByRole('link', { name: /Notifications/i })).toHaveAttribute(
      'href',
      '/notifications'
    );
  });

  it('marks the active item with aria-current="page"', () => {
    render(<MainNavList items={ITEMS} pathname="/library" />);
    expect(screen.getByRole('link', { name: /Library/i })).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark non-active items', () => {
    render(<MainNavList items={ITEMS} pathname="/library" />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).not.toHaveAttribute('aria-current');
  });

  it('treats a deeper pathname as active for the parent route', () => {
    render(<MainNavList items={ITEMS} pathname="/library/recent" />);
    expect(screen.getByRole('link', { name: /Library/i })).toHaveAttribute('aria-current', 'page');
  });

  it('strips the query string before deciding active state (Games ?tab=discover)', () => {
    render(<MainNavList items={ITEMS} pathname="/games" />);
    expect(screen.getByRole('link', { name: /Games/i })).toHaveAttribute('aria-current', 'page');
  });

  it('renders the counter badge when notificationCount > 0 and item.showCounter is true', () => {
    render(<MainNavList items={ITEMS} pathname="/dashboard" notificationCount={3} />);
    expect(screen.getByLabelText(/3 unread/i)).toHaveTextContent('3');
  });

  it('hides the counter badge when notificationCount is 0', () => {
    render(<MainNavList items={ITEMS} pathname="/dashboard" notificationCount={0} />);
    expect(screen.queryByLabelText(/unread/i)).not.toBeInTheDocument();
  });

  it('hides the counter badge for items where showCounter is not set', () => {
    render(<MainNavList items={ITEMS} pathname="/dashboard" notificationCount={5} />);
    // Library has no showCounter — its link must not include the badge.
    const libraryLink = screen.getByRole('link', { name: /Library/i });
    expect(libraryLink.textContent).not.toMatch(/5/);
  });

  it('calls onNavigate when a link is clicked', () => {
    const onNavigate = vi.fn();
    render(<MainNavList items={ITEMS} pathname="/dashboard" onNavigate={onNavigate} />);
    screen.getByRole('link', { name: /Library/i }).click();
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });

  it('exposes the provided ariaLabel on the nav landmark', () => {
    render(<MainNavList items={ITEMS} pathname="/dashboard" ariaLabel="Main navigation" />);
    expect(screen.getByRole('navigation', { name: /main navigation/i })).toBeInTheDocument();
  });
});

describe('isPathActive', () => {
  it('matches the exact path', () => {
    expect(isPathActive('/library', '/library')).toBe(true);
  });

  it('matches a descendant path', () => {
    expect(isPathActive('/library/recent', '/library')).toBe(true);
  });

  it('does not match an unrelated sibling that happens to share a prefix', () => {
    // /libraryX should NOT be considered descendant of /library because the
    // segment boundary differs (no '/' between).
    expect(isPathActive('/libraryX', '/library')).toBe(false);
  });

  it('strips the query string from the href before comparison', () => {
    expect(isPathActive('/games', '/games?tab=discover')).toBe(true);
    expect(isPathActive('/games/123', '/games?tab=discover')).toBe(true);
  });

  it('returns false when paths differ entirely', () => {
    expect(isPathActive('/profile', '/library')).toBe(false);
  });
});
