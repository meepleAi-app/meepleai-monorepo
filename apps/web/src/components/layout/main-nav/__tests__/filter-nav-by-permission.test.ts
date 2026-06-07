import { describe, it, expect } from 'vitest';
import type { ComponentType } from 'react';

import { filterNavByPermission } from '../filter-nav-by-permission';
import type { MainNavItem } from '../main-nav-config';

const NullIcon: ComponentType<{ className?: string }> = () => null;

const ITEMS: ReadonlyArray<MainNavItem> = [
  { id: 'dashboard', label: 'Dashboard', defaultHref: '/dashboard', icon: NullIcon },
  { id: 'library', label: 'Library', defaultHref: '/library', icon: NullIcon, gameRelated: true },
  { id: 'profile', label: 'Profile', defaultHref: '/profile', icon: NullIcon },
];

describe('filterNavByPermission', () => {
  it('returns all items when the user is authenticated (MVP)', () => {
    const result = filterNavByPermission(ITEMS, { authenticated: true });
    expect(result.map(i => i.id)).toEqual(['dashboard', 'library', 'profile']);
  });

  it('returns no items when the user is not authenticated', () => {
    const result = filterNavByPermission(ITEMS, { authenticated: false });
    expect(result).toEqual([]);
  });

  it('does not mutate the input items array (pure)', () => {
    const before = [...ITEMS];
    filterNavByPermission(ITEMS, { authenticated: true });
    expect(ITEMS).toEqual(before);
  });

  it('returns the same iteration order as the input', () => {
    const result = filterNavByPermission(ITEMS, { authenticated: true });
    expect(result.map(i => i.id)).toEqual(ITEMS.map(i => i.id));
  });
});
