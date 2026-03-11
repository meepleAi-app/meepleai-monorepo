/**
 * Admin Dashboard Navigation Config Tests - Issue #4844
 *
 * Covers:
 * - isSectionActive: baseRoute + additionalRoutes matching
 * - getActiveSection: route-to-section mapping
 * - isSidebarItemActive: activePattern and prefix matching
 * - DASHBOARD_SECTIONS: Documents item present in content section
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
    expect(getSection('content')?.id).toBe('content');
  });

  it('returns undefined for unknown id', () => {
    expect(getSection('nonexistent')).toBeUndefined();
  });
});

describe('getSidebarItems', () => {
  it('returns items for known section', () => {
    const items = getSidebarItems('content');
    expect(items.length).toBeGreaterThan(0);
  });

  it('returns empty array for unknown section', () => {
    expect(getSidebarItems('nonexistent')).toEqual([]);
  });
});

// ─── isSectionActive ─────────────────────────────────────────────────────────

describe('isSectionActive', () => {
  const content = DASHBOARD_SECTIONS.find(s => s.id === 'content')!;
  const overview = DASHBOARD_SECTIONS.find(s => s.id === 'overview')!;

  describe('baseRoute matching', () => {
    it('matches exact baseRoute', () => {
      expect(isSectionActive(content, '/admin/shared-games')).toBe(true);
    });

    it('matches sub-routes under baseRoute', () => {
      expect(isSectionActive(content, '/admin/shared-games/all')).toBe(true);
      expect(isSectionActive(content, '/admin/shared-games/new')).toBe(true);
    });

    it('matches additionalRoutes for knowledge-base', () => {
      expect(isSectionActive(content, '/admin/knowledge-base')).toBe(true);
      expect(isSectionActive(content, '/admin/knowledge-base/documents')).toBe(true);
    });

    it('does not match unrelated routes', () => {
      expect(isSectionActive(content, '/admin/users')).toBe(false);
      expect(isSectionActive(content, '/admin/agents')).toBe(false);
    });
  });

  describe('other sections unaffected', () => {
    it('overview section is active for /admin/overview', () => {
      expect(isSectionActive(overview, '/admin/overview')).toBe(true);
    });

    it('overview section is not active for /admin/shared-games', () => {
      expect(isSectionActive(overview, '/admin/shared-games')).toBe(false);
    });
  });
});

// ─── getActiveSection ─────────────────────────────────────────────────────────

describe('getActiveSection', () => {
  it('returns overview for /admin/overview', () => {
    expect(getActiveSection('/admin/overview')?.id).toBe('overview');
  });

  it('returns content for /admin/knowledge-base (additionalRoute)', () => {
    expect(getActiveSection('/admin/knowledge-base')?.id).toBe('content');
  });

  it('returns content for /admin/knowledge-base/vectors', () => {
    expect(getActiveSection('/admin/knowledge-base/vectors')?.id).toBe('content');
  });

  it('returns content for /admin/knowledge-base/documents', () => {
    expect(getActiveSection('/admin/knowledge-base/documents')?.id).toBe('content');
  });

  it('returns ai for /admin/agents', () => {
    expect(getActiveSection('/admin/agents')?.id).toBe('ai');
  });

  it('returns system for /admin/monitor', () => {
    expect(getActiveSection('/admin/monitor')?.id).toBe('system');
  });

  it('returns system for /admin/config (additionalRoute)', () => {
    expect(getActiveSection('/admin/config')?.id).toBe('system');
  });

  it('returns undefined for unknown route', () => {
    expect(getActiveSection('/totally/unknown')).toBeUndefined();
  });

  it('does not return content for /admin/pdfstuff (no prefix false-positive)', () => {
    expect(getActiveSection('/admin/pdfstuff')).toBeUndefined();
  });
});

// ─── isSidebarItemActive ──────────────────────────────────────────────────────

describe('isSidebarItemActive', () => {
  const contentItems = getSidebarItems('content');
  const documentsItem = contentItems.find(i => i.href === '/admin/knowledge-base/documents');
  const kbOverviewItem = contentItems.find(i => i.href === '/admin/knowledge-base');

  it('documents item exists in content sidebar', () => {
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

  it('kb overview item is active on /admin/knowledge-base', () => {
    expect(isSidebarItemActive(kbOverviewItem!, '/admin/knowledge-base')).toBe(true);
  });

  it('kb overview item is NOT active on /admin/knowledge-base/vectors (activePattern exact)', () => {
    expect(isSidebarItemActive(kbOverviewItem!, '/admin/knowledge-base/vectors')).toBe(false);
  });
});

// ─── DASHBOARD_SECTIONS structure ────────────────────────────────────────────

describe('DASHBOARD_SECTIONS structure', () => {
  it('has 6 sections', () => {
    expect(DASHBOARD_SECTIONS).toHaveLength(6);
  });

  it('content section has additionalRoutes including knowledge-base', () => {
    const content = DASHBOARD_SECTIONS.find(s => s.id === 'content')!;
    expect(content.additionalRoutes).toContain('/admin/knowledge-base');
  });

  it('content sidebar contains Documents item pointing to /admin/knowledge-base/documents', () => {
    const content = DASHBOARD_SECTIONS.find(s => s.id === 'content')!;
    const documentsItem = content.sidebarItems.find(
      i => i.href === '/admin/knowledge-base/documents'
    );
    expect(documentsItem).toBeDefined();
    expect(documentsItem?.label).toBe('Documents');
  });
});
