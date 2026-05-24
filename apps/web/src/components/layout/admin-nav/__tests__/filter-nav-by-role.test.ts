import { describe, it, expect } from 'vitest';

import type { AuthUser } from '@/types/auth';

import type { AdminNavGroup } from '../admin-nav-config';
import { filterNavByRole } from '../filter-nav-by-role';

function user(role: string): AuthUser {
  return { id: 'u1', email: 'a@b.c', role };
}

const GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: () => null,
    items: [
      { label: 'Dashboard', href: '/admin/overview', icon: () => null }, // default admin
      { label: 'Secrets', href: '/admin/secrets', icon: () => null, minRole: 'superadmin' },
    ],
  },
  {
    id: 'B',
    label: 'Editor Tools',
    icon: () => null,
    items: [{ label: 'Editor', href: '/admin/editor', icon: () => null, minRole: 'editor' }],
  },
];

describe('filterNavByRole', () => {
  it('superadmin sees every item', () => {
    const result = filterNavByRole(GROUPS, user('superadmin'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/overview', '/admin/secrets', '/admin/editor']);
  });

  it('admin sees admin+editor items but not superadmin-only', () => {
    const result = filterNavByRole(GROUPS, user('admin'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/overview', '/admin/editor']);
  });

  it('editor sees only editor item; admin-default item is hidden', () => {
    const result = filterNavByRole(GROUPS, user('editor'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/editor']);
  });

  it('drops groups that become empty after filtering', () => {
    const result = filterNavByRole(GROUPS, user('editor'));
    expect(result.map(g => g.id)).toEqual(['B']);
  });

  it('returns no groups for a null user', () => {
    expect(filterNavByRole(GROUPS, null)).toEqual([]);
  });

  it('treats an item without minRole as requiring admin', () => {
    const result = filterNavByRole(GROUPS, user('user'));
    expect(result).toEqual([]);
  });
});
