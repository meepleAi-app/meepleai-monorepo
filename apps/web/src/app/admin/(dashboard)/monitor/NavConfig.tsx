'use client';

/**
 * AdminMonitorNavConfig — Registers ActionBar actions for /admin/monitor hub
 * Issue #5053 — Admin Monitor Hub Page Migration
 *
 * No MiniNav (admin uses contextual sidebar for section navigation).
 * ActionBar: Refresh (primary) · Clear Cache · Run Tests
 *
 * Include in admin/monitor/page.tsx:
 *   <AdminMonitorNavConfig />
 */

import { useEffect } from 'react';

import { FlaskConical, RefreshCw, Trash2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AdminMonitorNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [],
      actionBar: [
        {
          id: 'refresh',
          label: 'Refresh',
          icon: RefreshCw,
          variant: 'primary',
          onClick: () => router.push('/admin/monitor?action=refresh'),
        },
        {
          id: 'clear-cache',
          label: 'Clear Cache',
          icon: Trash2,
          onClick: () => router.push('/admin/monitor?action=clear-cache'),
        },
        {
          id: 'run-tests',
          label: 'Run Tests',
          icon: FlaskConical,
          onClick: () => router.push('/admin/monitor?action=run-tests'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
