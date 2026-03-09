'use client';

/**
 * AgentsNavConfig — Registers MiniNav tabs for /admin/agents/* pages.
 * Issue #5110 — Integrate visual pipeline builder into /admin/agents/builder
 *
 * MiniNav tabs: Overview · Builder · Pipeline · Metrics · Usage · Debug
 * Include in any /admin/agents/* page.tsx:
 *   <AgentsNavConfig />
 */

import { useEffect } from 'react';

import { Activity, Bot, GitBranch, BarChart3, Gauge, Terminal, Layers } from 'lucide-react';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AgentsNavConfig() {
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'overview', label: 'Overview', href: '/admin/agents', icon: Activity },
        { id: 'builder', label: 'Builder', href: '/admin/agents/builder', icon: Bot },
        { id: 'pipeline', label: 'Pipeline', href: '/admin/agents/pipeline', icon: GitBranch },
        { id: 'analytics', label: 'Analytics', href: '/admin/agents/analytics', icon: BarChart3 },
        { id: 'usage', label: 'Usage', href: '/admin/agents/usage', icon: Gauge },
        { id: 'debug', label: 'Debug', href: '/admin/agents/debug', icon: Terminal },
        { id: 'templates', label: 'Templates', href: '/admin/agents/templates', icon: Layers },
      ],
      actionBar: [],
    });
  }, [setNavConfig]);

  return null;
}
