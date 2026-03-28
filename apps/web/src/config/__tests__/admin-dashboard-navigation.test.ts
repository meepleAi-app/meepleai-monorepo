import { describe, expect, it } from 'vitest';

import { DASHBOARD_SECTIONS, getActiveSection, getSection } from '../admin-dashboard-navigation';

describe('admin-dashboard-navigation', () => {
  describe('DASHBOARD_SECTIONS', () => {
    it('should have 6 sections total', () => {
      expect(DASHBOARD_SECTIONS).toHaveLength(6);
    });

    it('should have unique section ids', () => {
      const ids = DASHBOARD_SECTIONS.map(s => s.id);
      expect(new Set(ids).size).toBe(ids.length);
    });

    it('should include all expected section ids', () => {
      const ids = DASHBOARD_SECTIONS.map(s => s.id);
      expect(ids).toEqual(
        expect.arrayContaining(['overview', 'content', 'ai', 'users', 'system', 'analytics'])
      );
    });
  });

  describe('existing sections still work', () => {
    it('should resolve overview section', () => {
      const section = getActiveSection('/admin/overview');
      expect(section).toBeDefined();
      expect(section!.id).toBe('overview');
    });

    it('should resolve agents routes to ai section', () => {
      const section = getActiveSection('/admin/agents/pipeline');
      expect(section).toBeDefined();
      expect(section!.id).toBe('ai');
    });

    it('should resolve knowledge-base routes to content section', () => {
      const section = getActiveSection('/admin/knowledge-base');
      expect(section).toBeDefined();
      expect(section!.id).toBe('content');
    });
  });

  describe('merged sections', () => {
    it('should resolve /admin/monitor to system section', () => {
      const section = getActiveSection('/admin/monitor');
      expect(section).toBeDefined();
      expect(section!.id).toBe('system');
    });

    it('should resolve /admin/config to system section', () => {
      const section = getActiveSection('/admin/config');
      expect(section).toBeDefined();
      expect(section!.id).toBe('system');
    });

    it('should resolve /admin/analytics to analytics section', () => {
      const section = getActiveSection('/admin/analytics');
      expect(section).toBeDefined();
      expect(section!.id).toBe('analytics');
    });

    it('should resolve /admin/ai to ai section', () => {
      const section = getActiveSection('/admin/ai');
      expect(section).toBeDefined();
      expect(section!.id).toBe('ai');
    });

    it('should resolve /admin/content to content section', () => {
      const section = getActiveSection('/admin/content');
      expect(section).toBeDefined();
      expect(section!.id).toBe('content');
    });
  });

  describe('section sidebar items', () => {
    it('system section should have Alerts sidebar item', () => {
      const section = getSection('system');
      expect(section).toBeDefined();
      const alertsItem = section!.sidebarItems.find(i => i.label === 'Alerts');
      expect(alertsItem).toBeDefined();
    });

    it('system section should have Feature Flags sidebar item', () => {
      const section = getSection('system');
      expect(section).toBeDefined();
      const flagsItem = section!.sidebarItems.find(i => i.label === 'Feature Flags');
      expect(flagsItem).toBeDefined();
    });

    it('analytics section should have Audit Log sidebar item', () => {
      const section = getSection('analytics');
      expect(section).toBeDefined();
      const auditItem = section!.sidebarItems.find(i => i.label === 'Audit Log');
      expect(auditItem).toBeDefined();
    });

    it('ai section should have Mission Control sidebar item', () => {
      const section = getSection('ai');
      expect(section).toBeDefined();
      const agentsItem = section!.sidebarItems.find(i => i.label === 'Mission Control');
      expect(agentsItem).toBeDefined();
    });

    it('content section should have All Games sidebar item', () => {
      const section = getSection('content');
      expect(section).toBeDefined();
      const gamesItem = section!.sidebarItems.find(i => i.label === 'All Games');
      expect(gamesItem).toBeDefined();
    });
  });

  describe('section groups', () => {
    it('system should be in core group', () => {
      expect(getSection('system')!.group).toBe('core');
    });

    it('analytics should be in core group', () => {
      expect(getSection('analytics')!.group).toBe('core');
    });

    it('content should be in core group', () => {
      expect(getSection('content')!.group).toBe('core');
    });

    it('ai should be in ai group', () => {
      expect(getSection('ai')!.group).toBe('ai');
    });
  });
});
