/**
 * Admin Dashboard Navigation Config Tests - Issue #4844
 *
 * Covers:
 * - isSectionActive: baseRoute and additionalRoutes matching
 * - getActiveSection: /admin/pdfs maps to knowledge-base
 * - isSidebarItemActive: activePattern and prefix matching
 * - DASHBOARD_SECTIONS: PDFs item present in knowledge-base
 */

import { describe, it, expect } from 'vitest';

import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  getSection,
  getSidebarItems,
  isSectionActive,
  isSidebarItemActive,
} from '@/config/admin-dashboard-navigation';

// ─── Section helpers ──────────────────────────────────────────────────────────

describe('getSection', () => {
  it('returns section by id', () => {
    expect(getSection('overview')?.id).toBe('overview');
    expect(getSection('knowledge-base')?.id).toBe('knowledge-base');
  });

  it('returns undefined for unknown id', () => {
    expect(getSection('nonexistent')).toBeUndefined();
  });
});

describe('getSidebarItems', () => {
  it('returns items for known section', () => {
    const items = getSidebarItems('knowledge-base');
    expect(items.length).toBeGreaterThan(0);
  });

  it('returns empty array for unknown section', () => {
    expect(getSidebarItems('nonexistent')).toEqual([]);
  });
});

// ─── isSectionActive ─────────────────────────────────────────────────────────

describe('isSectionActive', () => {
  const kb = DASHBOARD_SECTIONS.find((s) => s.id === 'knowledge-base')!;
  const overview = DASHBOARD_SECTIONS.find((s) => s.id === 'overview')!;

  describe('baseRoute matching', () => {
    it('matches exact baseRoute', () => {
      expect(isSectionActive(kb, '/admin/knowledge-base')).toBe(true);
    });

    it('matches sub-routes under baseRoute', () => {
      expect(isSectionActive(kb, '/admin/knowledge-base/vectors')).toBe(true);
      expect(isSectionActive(kb, '/admin/knowledge-base/settings')).toBe(true);
    });

    it('does not match unrelated routes', () => {
      expect(isSectionActive(kb, '/admin/users')).toBe(false);
      expect(isSectionActive(kb, '/admin/agents')).toBe(false);
    });
  });

  describe('additionalRoutes matching (issue #4844)', () => {
    it('matches /admin/pdfs exactly', () => {
      expect(isSectionActive(kb, '/admin/pdfs')).toBe(true);
    });

    it('matches sub-paths of /admin/pdfs', () => {
      expect(isSectionActive(kb, '/admin/pdfs/123')).toBe(true);
    });

    it('does not falsely match /admin/pdfstuff', () => {
      expect(isSectionActive(kb, '/admin/pdfstuff')).toBe(false);
    });
  });

  describe('other sections unaffected', () => {
    it('overview section is active for /admin/overview', () => {
      expect(isSectionActive(overview, '/admin/overview')).toBe(true);
    });

    it('overview section is not active for /admin/pdfs', () => {
      expect(isSectionActive(overview, '/admin/pdfs')).toBe(false);
    });
  });
});

// ─── getActiveSection ─────────────────────────────────────────────────────────

describe('getActiveSection', () => {
  it('returns overview for /admin/overview', () => {
    expect(getActiveSection('/admin/overview')?.id).toBe('overview');
  });

  it('returns knowledge-base for /admin/knowledge-base', () => {
    expect(getActiveSection('/admin/knowledge-base')?.id).toBe('knowledge-base');
  });

  it('returns knowledge-base for /admin/knowledge-base/vectors', () => {
    expect(getActiveSection('/admin/knowledge-base/vectors')?.id).toBe('knowledge-base');
  });

  it('returns knowledge-base for /admin/pdfs (additionalRoutes, issue #4844)', () => {
    expect(getActiveSection('/admin/pdfs')?.id).toBe('knowledge-base');
  });

  it('returns knowledge-base for /admin/pdfs/some-doc-id', () => {
    expect(getActiveSection('/admin/pdfs/some-doc-id')?.id).toBe('knowledge-base');
  });

  it('returns agents for /admin/agents', () => {
    expect(getActiveSection('/admin/agents')?.id).toBe('agents');
  });

  it('returns undefined for unknown route', () => {
    expect(getActiveSection('/totally/unknown')).toBeUndefined();
  });

  it('does not return knowledge-base for /admin/pdfstuff (no prefix false-positive)', () => {
    expect(getActiveSection('/admin/pdfstuff')).toBeUndefined();
  });
});

// ─── isSidebarItemActive ──────────────────────────────────────────────────────

describe('isSidebarItemActive', () => {
  const kbItems = getSidebarItems('knowledge-base');
  const pdfsItem = kbItems.find((i) => i.href === '/admin/pdfs');
  const overviewItem = kbItems.find((i) => i.href === '/admin/knowledge-base');

  it('pdfs item exists in knowledge-base sidebar (issue #4844)', () => {
    expect(pdfsItem).toBeDefined();
    expect(pdfsItem?.label).toBe('Documents');
  });

  it('pdfs item is active on /admin/pdfs', () => {
    expect(isSidebarItemActive(pdfsItem!, '/admin/pdfs')).toBe(true);
  });

  it('pdfs item is active on sub-path /admin/pdfs/abc (prefix matching)', () => {
    expect(isSidebarItemActive(pdfsItem!, '/admin/pdfs/abc')).toBe(true);
  });

  it('pdfs item is NOT active on /admin/knowledge-base', () => {
    expect(isSidebarItemActive(pdfsItem!, '/admin/knowledge-base')).toBe(false);
  });

  it('overview item is active on /admin/knowledge-base', () => {
    expect(isSidebarItemActive(overviewItem!, '/admin/knowledge-base')).toBe(true);
  });

  it('overview item is NOT active on /admin/knowledge-base/vectors (activePattern exact)', () => {
    expect(isSidebarItemActive(overviewItem!, '/admin/knowledge-base/vectors')).toBe(false);
  });
});

// ─── DASHBOARD_SECTIONS structure ────────────────────────────────────────────

describe('DASHBOARD_SECTIONS structure', () => {
  it('has 5 sections', () => {
    expect(DASHBOARD_SECTIONS).toHaveLength(5);
  });

  it('knowledge-base section has additionalRoutes with /admin/pdfs', () => {
    const kb = DASHBOARD_SECTIONS.find((s) => s.id === 'knowledge-base')!;
    expect(kb.additionalRoutes).toContain('/admin/pdfs');
  });

  it('knowledge-base sidebar contains Documents item pointing to /admin/pdfs', () => {
    const kb = DASHBOARD_SECTIONS.find((s) => s.id === 'knowledge-base')!;
    const pdfsItem = kb.sidebarItems.find((i) => i.href === '/admin/pdfs');
    expect(pdfsItem).toBeDefined();
    expect(pdfsItem?.label).toBe('Documents');
  });
});
