/**
 * Admin Analytics Hub Layout
 * Issue #5040 — Admin Route Consolidation
 *
 * Consolidates: usage-stats, ai-usage, audit-log, reports, api-keys
 */

'use client';

import { type ReactNode } from 'react';

import { BarChart2, FileText, Key, LayoutDashboard, Shield } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminAnalyticsLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'overview', label: 'Overview', href: '/admin/analytics', icon: LayoutDashboard },
      { id: 'ai-usage', label: 'AI Usage', href: '/admin/analytics?tab=ai-usage', icon: BarChart2 },
      { id: 'audit', label: 'Audit Log', href: '/admin/analytics?tab=audit', icon: Shield },
      { id: 'reports', label: 'Reports', href: '/admin/analytics?tab=reports', icon: FileText },
      { id: 'api-keys', label: 'API Keys', href: '/admin/analytics?tab=api-keys', icon: Key },
    ],
  });

  return <>{children}</>;
}
