import { afterEach, beforeEach, describe, it, expect, vi } from 'vitest';

import type { UserRole } from '@/types/auth';

import { ADMIN_NAV_GROUPS } from '../admin-nav-config';

const VALID_ROLES: UserRole[] = ['superadmin', 'admin', 'editor', 'user'];

const ANALYSES_HREF = '/admin/knowledge-base/mechanic-extractor/analyses';
const LEGACY_HREF = '/admin/knowledge-base/mechanic-extractor';
const LEGACY_FLAG = 'NEXT_PUBLIC_SHOW_LEGACY_MECHANIC_EXTRACTOR';

/** Re-import the config with a fresh module registry so the module-level flag read re-runs. */
async function loadHrefs(): Promise<string[]> {
  vi.resetModules();
  const mod = await import('../admin-nav-config');
  return mod.ADMIN_NAV_GROUPS.flatMap(g => g.items.map(i => i.href));
}

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

  it('always exposes the AI-first Mechanic Analyses entry', () => {
    const hrefs = ADMIN_NAV_GROUPS.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toContain(ANALYSES_HREF);
  });
});

// #537 ME-M4.2: the deprecated Variant C editor entry is gated behind
// NEXT_PUBLIC_SHOW_LEGACY_MECHANIC_EXTRACTOR. The config reads the flag at module
// load, so each state needs a fresh import via vi.resetModules().
describe('ADMIN_NAV_GROUPS — legacy Mechanic Extractor gate (#537)', () => {
  let original: string | undefined;

  beforeEach(() => {
    original = process.env[LEGACY_FLAG];
  });

  afterEach(() => {
    if (original === undefined) {
      delete process.env[LEGACY_FLAG];
    } else {
      process.env[LEGACY_FLAG] = original;
    }
    vi.resetModules();
  });

  it('hides the legacy editor entry when the flag is unset', async () => {
    delete process.env[LEGACY_FLAG];
    const hrefs = await loadHrefs();
    expect(hrefs).toContain(ANALYSES_HREF);
    expect(hrefs).not.toContain(LEGACY_HREF);
  });

  it("hides the legacy editor entry for non-'true' values", async () => {
    process.env[LEGACY_FLAG] = 'false';
    const hrefs = await loadHrefs();
    expect(hrefs).not.toContain(LEGACY_HREF);
  });

  it("shows the legacy editor entry when the flag is 'true'", async () => {
    process.env[LEGACY_FLAG] = 'true';
    const hrefs = await loadHrefs();
    expect(hrefs).toContain(ANALYSES_HREF);
    expect(hrefs).toContain(LEGACY_HREF);
  });
});
