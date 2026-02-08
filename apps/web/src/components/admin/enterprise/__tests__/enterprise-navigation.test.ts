/**
 * Enterprise Navigation Config Tests
 * Issue #3689 - Layout Base & Navigation System
 */

import { describe, it, expect } from 'vitest';

import {
  ENTERPRISE_SECTIONS,
  getEnterpriseSectionByRoute,
  getEnterpriseSectionById,
  getDefaultTab,
} from '@/config/enterprise-navigation';

describe('enterprise-navigation config', () => {
  it('defines exactly 7 sections', () => {
    expect(ENTERPRISE_SECTIONS).toHaveLength(7);
  });

  it('each section has required fields', () => {
    for (const section of ENTERPRISE_SECTIONS) {
      expect(section.id).toBeDefined();
      expect(section.label).toBeDefined();
      expect(section.icon).toBeDefined();
      expect(section.route).toBeDefined();
      expect(section.description).toBeDefined();
      expect(section.tabs).toBeDefined();
      expect(section.tabs.length).toBeGreaterThan(0);
    }
  });

  it('each tab has required fields', () => {
    for (const section of ENTERPRISE_SECTIONS) {
      for (const tab of section.tabs) {
        expect(tab.id).toBeDefined();
        expect(tab.label).toBeDefined();
        expect(tab.icon).toBeDefined();
      }
    }
  });

  it('section IDs are unique', () => {
    const ids = ENTERPRISE_SECTIONS.map((s) => s.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('section routes are unique', () => {
    const routes = ENTERPRISE_SECTIONS.map((s) => s.route);
    expect(new Set(routes).size).toBe(routes.length);
  });

  it('tab IDs are unique within each section', () => {
    for (const section of ENTERPRISE_SECTIONS) {
      const tabIds = section.tabs.map((t) => t.id);
      expect(new Set(tabIds).size).toBe(tabIds.length);
    }
  });

  describe('getEnterpriseSectionByRoute', () => {
    it('returns section for valid route', () => {
      const section = getEnterpriseSectionByRoute('overview');
      expect(section?.id).toBe('overview');
    });

    it('returns undefined for invalid route', () => {
      expect(getEnterpriseSectionByRoute('nonexistent')).toBeUndefined();
    });
  });

  describe('getEnterpriseSectionById', () => {
    it('returns section for valid id', () => {
      const section = getEnterpriseSectionById('resources');
      expect(section?.route).toBe('resources');
    });

    it('returns undefined for invalid id', () => {
      expect(getEnterpriseSectionById('nonexistent')).toBeUndefined();
    });
  });

  describe('getDefaultTab', () => {
    it('returns first tab of section', () => {
      expect(getDefaultTab('overview')).toBe('dashboard');
      expect(getDefaultTab('resources')).toBe('tokens');
      expect(getDefaultTab('operations')).toBe('services');
      expect(getDefaultTab('ai-platform')).toBe('ai-lab');
      expect(getDefaultTab('users')).toBe('users');
      expect(getDefaultTab('business')).toBe('usage-stats');
      expect(getDefaultTab('simulations')).toBe('cost-calculator');
    });

    it('returns dashboard for invalid section', () => {
      expect(getDefaultTab('nonexistent')).toBe('dashboard');
    });
  });

  describe('section content per spec', () => {
    it('Overview has 3 tabs: Dashboard, Alerts, Quick Actions', () => {
      const section = getEnterpriseSectionById('overview')!;
      const tabIds = section.tabs.map((t) => t.id);
      expect(tabIds).toEqual(['dashboard', 'alerts', 'quick-actions']);
    });

    it('Resources has 5 tabs: Tokens, Database, Cache, Vectors, Services', () => {
      const section = getEnterpriseSectionById('resources')!;
      const tabIds = section.tabs.map((t) => t.id);
      expect(tabIds).toEqual(['tokens', 'database', 'cache', 'vectors', 'services']);
    });

    it('Operations has 5 tabs', () => {
      const section = getEnterpriseSectionById('operations')!;
      expect(section.tabs).toHaveLength(5);
    });

    it('AI Platform has 5 tabs', () => {
      const section = getEnterpriseSectionById('ai-platform')!;
      expect(section.tabs).toHaveLength(5);
    });

    it('Users & Content has 4 tabs', () => {
      const section = getEnterpriseSectionById('users')!;
      expect(section.tabs).toHaveLength(4);
    });

    it('Business has 3 tabs', () => {
      const section = getEnterpriseSectionById('business')!;
      expect(section.tabs).toHaveLength(3);
    });

    it('Simulations has 3 tabs', () => {
      const section = getEnterpriseSectionById('simulations')!;
      expect(section.tabs).toHaveLength(3);
    });
  });
});
