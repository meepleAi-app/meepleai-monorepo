import { describe, expect, it } from 'vitest';

import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  isSidebarItemActive,
} from '@/config/admin-dashboard-navigation';

describe('AI Admin Sidebar Navigation', () => {
  const aiSection = DASHBOARD_SECTIONS.find(s => s.id === 'ai');

  it('has exactly 7 sidebar items', () => {
    expect(aiSection).toBeDefined();
    expect(aiSection!.sidebarItems).toHaveLength(7);
  });

  it('contains the expected labels', () => {
    const labels = aiSection!.sidebarItems.map(i => i.label);
    expect(labels).toEqual([
      'Mission Control',
      'RAG Inspector',
      'RAG Playground',
      'Agent Definitions',
      'Configuration',
      'Usage & Costs',
      'Analytics',
    ]);
  });

  it('contains the expected routes', () => {
    const routes = aiSection!.sidebarItems.map(i => i.href);
    expect(routes).toEqual([
      '/admin/agents',
      '/admin/agents/inspector',
      '/admin/agents/playground',
      '/admin/agents/definitions',
      '/admin/agents/config',
      '/admin/agents/usage',
      '/admin/agents/analytics',
    ]);
  });

  it('resolves /admin/agents/inspector to AI section', () => {
    expect(getActiveSection('/admin/agents/inspector')?.id).toBe('ai');
  });

  it('resolves /admin/agents/config to AI section', () => {
    expect(getActiveSection('/admin/agents/config')?.id).toBe('ai');
  });

  it('marks Mission Control active for /admin/agents', () => {
    const mc = aiSection!.sidebarItems[0];
    expect(isSidebarItemActive(mc, '/admin/agents')).toBe(true);
    expect(isSidebarItemActive(mc, '/admin/agents/inspector')).toBe(false);
  });
});
