/**
 * Admin Monitor Hub Layout
 * Issue #5040 — Admin Route Consolidation
 *
 * Consolidates: alerts, alert-rules, cache, infrastructure, services,
 *               command-center, testing, bulk-export
 */

'use client';

import { type ReactNode } from 'react';

import { Activity, AlertTriangle, Database, Download, Monitor, Server, Terminal, TestTube } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminMonitorLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'alerts', label: 'Alert', href: '/admin/monitor', icon: AlertTriangle },
      { id: 'cache', label: 'Cache', href: '/admin/monitor?tab=cache', icon: Database },
      { id: 'infra', label: 'Infrastruttura', href: '/admin/monitor?tab=infra', icon: Server },
      { id: 'services', label: 'Servizi', href: '/admin/monitor?tab=services', icon: Activity },
      { id: 'command', label: 'Command Center', href: '/admin/monitor?tab=command', icon: Terminal },
      { id: 'testing', label: 'Testing', href: '/admin/monitor?tab=testing', icon: TestTube },
      { id: 'export', label: 'Export', href: '/admin/monitor?tab=export', icon: Download },
    ],
  });

  return <>{children}</>;
}
