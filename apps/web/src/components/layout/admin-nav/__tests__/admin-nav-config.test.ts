import { describe, it, expect } from 'vitest';

import { ADMIN_NAV_GROUPS } from '../admin-nav-config';

const VALID_ROLES = ['superadmin', 'admin', 'editor', 'user'];

describe('ADMIN_NAV_GROUPS', () => {
  it('declares exactly the four groups A, B, C, D in order', () => {
    expect(ADMIN_NAV_GROUPS.map(g => g.id)).toEqual(['A', 'B', 'C', 'D']);
  });

  it('every group has a non-empty label and at least one item', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      expect(group.label.length).toBeGreaterThan(0);
      expect(group.items.length).toBeGreaterThan(0);
    }
  });

  it('every item has a non-empty label and an /admin href', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      for (const item of group.items) {
        expect(item.label.length).toBeGreaterThan(0);
        expect(item.href.startsWith('/admin')).toBe(true);
      }
    }
  });

  it('every declared minRole is a valid UserRole', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      for (const item of group.items) {
        if (item.minRole !== undefined) {
          expect(VALID_ROLES).toContain(item.minRole);
        }
      }
    }
  });

  it('has no duplicate hrefs across all groups', () => {
    const hrefs = ADMIN_NAV_GROUPS.flatMap(g => g.items.map(i => i.href));
    expect(new Set(hrefs).size).toBe(hrefs.length);
  });
});
