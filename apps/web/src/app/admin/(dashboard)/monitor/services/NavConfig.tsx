'use client';

/**
 * ServicesNavConfig — Registers ActionBar actions for /admin/monitor/services
 * Issue #132 — Enhanced ServiceHealthMatrix
 */

import { useEffect } from 'react';

import { RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function ServicesNavConfig() {
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
