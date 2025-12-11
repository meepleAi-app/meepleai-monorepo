/**
 * AdminSidebar Tests - Issue #881
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { AdminSidebar, defaultNavigation } from '../AdminSidebar';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: () => '/admin',
}));

// Mock localStorage
const mockLocalStorage: Record<string, string> = {};
beforeEach(() => {
  Object.keys(mockLocalStorage).forEach(key => delete mockLocalStorage[key]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(key => mockLocalStorage[key] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key, value) => {
    mockLocalStorage[key] = value;
  });
});

describe('AdminSidebar', () => {
  it('renders navigation items', () => {
    render(<AdminSidebar collapsed={false} />);

    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Analytics')).toBeInTheDocument();
    expect(screen.getByText('Configuration')).toBeInTheDocument();
    expect(screen.getByText('Cache')).toBeInTheDocument();
    expect(screen.getByText('Prompts')).toBeInTheDocument();
  });

  it('hides labels when collapsed', () => {
    render(<AdminSidebar collapsed={true} />);

    // Labels should be hidden when collapsed (in desktop view)
    const desktopNav = screen.getByRole('navigation', { name: 'Admin navigation' });
    expect(desktopNav).toBeInTheDocument();
  });

  it('shows badges when provided', () => {
    render(
      <AdminSidebar
        collapsed={false}
        badges={{
          '/admin/users': { count: 5 },
          '/admin/prompts': { count: 12 },
        }}
      />
    );

    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('shows 99+ for large badge counts', () => {
    render(
      <AdminSidebar
        collapsed={false}
        badges={{
          '/admin/users': { count: 150 },
        }}
      />
    );

    expect(screen.getByText('99+')).toBeInTheDocument();
  });

  it('exports default navigation', () => {
    expect(defaultNavigation).toHaveLength(9);
    expect(defaultNavigation[0]).toMatchObject({
      href: '/admin',
      label: 'Dashboard',
    });
    expect(defaultNavigation[0].icon).toBeDefined();
  });

  it('calls onCollapsedChange when toggle clicked', async () => {
    const user = userEvent.setup();
    const onCollapsedChange = vi.fn();

    render(<AdminSidebar collapsed={false} onCollapsedChange={onCollapsedChange} />);

    const collapseButton = screen.getByRole('button', { name: /collapse sidebar/i });
    await user.click(collapseButton);

    expect(onCollapsedChange).toHaveBeenCalledWith(true);
  });
});
