/**
 * Admin Dashboard Navigation Config Tests - Issue #4844
 *
 * Covers:
 * - isSectionActive: baseRoute matching
 * - getActiveSection: route-to-section mapping
 * - isSidebarItemActive: activePattern and prefix matching
 * - DASHBOARD_SECTIONS: Documents item present in knowledge-base
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
  const kb = DASHBOARD_SECTIONS.find(s => s.id === 'knowledge-base')!;
  const overview = DASHBOARD_SECTIONS.find(s => s.id === 'overview')!;

  describe('baseRoute matching', () => {
    it('matches exact baseRoute', () => {
      expect(isSectionActive(kb, '/admin/knowledge-base')).toBe(true);
    });

    it('matches sub-routes under baseRoute', () => {
      expect(isSectionActive(kb, '/admin/knowledge-base/vectors')).toBe(true);
      expect(isSectionActive(kb, '/admin/knowledge-base/settings')).toBe(true);
    });

    it('matches documents sub-route under baseRoute', () => {
      expect(isSectionActive(kb, '/admin/knowledge-base/documents')).toBe(true);
    });

    it('does not match unrelated routes', () => {
      expect(isSectionActive(kb, '/admin/users')).toBe(false);
      expect(isSectionActive(kb, '/admin/agents')).toBe(false);
    });
  });

  describe('other sections unaffected', () => {
    it('overview section is active for /admin/overview', () => {
      expect(isSectionActive(overview, '/admin/overview')).toBe(true);
    });

    it('overview section is not active for /admin/knowledge-base', () => {
      expect(isSectionActive(overview, '/admin/knowledge-base')).toBe(false);
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

  it('returns knowledge-base for /admin/knowledge-base/documents', () => {
    expect(getActiveSection('/admin/knowledge-base/documents')?.id).toBe('knowledge-base');
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
  const documentsItem = kbItems.find(i => i.href === '/admin/knowledge-base/documents');
  const overviewItem = kbItems.find(i => i.href === '/admin/knowledge-base');

  it('documents item exists in knowledge-base sidebar', () => {
    expect(documentsItem).toBeDefined();
    expect(documentsItem?.label).toBe('Documents');
  });

  it('documents item is active on /admin/knowledge-base/documents', () => {
    expect(isSidebarItemActive(documentsItem!, '/admin/knowledge-base/documents')).toBe(true);
  });

  it('documents item is active on sub-path /admin/knowledge-base/documents/abc (prefix matching)', () => {
    expect(isSidebarItemActive(documentsItem!, '/admin/knowledge-base/documents/abc')).toBe(true);
  });

  it('documents item is NOT active on /admin/knowledge-base', () => {
    expect(isSidebarItemActive(documentsItem!, '/admin/knowledge-base')).toBe(false);
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

  it('knowledge-base section has no additionalRoutes', () => {
    const kb = DASHBOARD_SECTIONS.find(s => s.id === 'knowledge-base')!;
    expect(kb.additionalRoutes).toBeUndefined();
  });

  it('knowledge-base sidebar contains Documents item pointing to /admin/knowledge-base/documents', () => {
    const kb = DASHBOARD_SECTIONS.find(s => s.id === 'knowledge-base')!;
    const documentsItem = kb.sidebarItems.find(i => i.href === '/admin/knowledge-base/documents');
    expect(documentsItem).toBeDefined();
    expect(documentsItem?.label).toBe('Documents');
  });
});
