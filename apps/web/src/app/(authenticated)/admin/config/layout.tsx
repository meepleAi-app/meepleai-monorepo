/**
 * Admin Config Hub Layout
 * Issue #5040 — Admin Route Consolidation
 *
 * Consolidates: configuration, feature-flags, rate-limits, tier-limits,
 *               n8n-templates, wizard
 */

'use client';

import { type ReactNode } from 'react';

import { Flag, Gauge, Settings, Sliders, Wand2, Workflow } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function AdminConfigLayout({ children }: { children: ReactNode }) {
  useSetNavConfig({
    miniNav: [
      { id: 'general', label: 'Generale', href: '/admin/config', icon: Settings },
      { id: 'limits', label: 'Limiti', href: '/admin/config?tab=limits', icon: Sliders },
      { id: 'flags', label: 'Feature Flags', href: '/admin/config?tab=flags', icon: Flag },
      { id: 'rate-limits', label: 'Rate Limits', href: '/admin/config?tab=rate-limits', icon: Gauge },
      { id: 'n8n', label: 'n8n', href: '/admin/config?tab=n8n', icon: Workflow },
      { id: 'wizard', label: 'Wizard', href: '/admin/config?tab=wizard', icon: Wand2 },
    ],
  });

  return <>{children}</>;
}
