'use client';

/**
 * LogsNavConfig — Registers ActionBar actions for /admin/monitor/logs
 * Issue #140
 */

import { useEffect } from 'react';

import { RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function LogsNavConfig() {
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
          onClick: () => router.refresh(),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
