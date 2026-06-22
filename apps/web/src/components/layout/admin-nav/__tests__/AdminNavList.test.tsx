import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { AdminNavGroup } from '../admin-nav-config';
import { AdminNavList } from '../AdminNavList';

const GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: () => null,
    items: [{ label: 'Dashboard', href: '/admin/overview', icon: () => null }],
  },
];

describe('AdminNavList', () => {
  it('renders group label and item link', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/overview" />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute(
      'href',
      '/admin/overview'
    );
  });

  it('marks the active item with aria-current', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/overview" />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('does not mark a non-active item', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/users" />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).not.toHaveAttribute('aria-current');
  });

  it('calls onNavigate when a link is clicked', () => {
    const onNavigate = vi.fn();
    render(<AdminNavList groups={GROUPS} pathname="/admin/users" onNavigate={onNavigate} />);
    screen.getByRole('link', { name: /Dashboard/i }).click();
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });
});
