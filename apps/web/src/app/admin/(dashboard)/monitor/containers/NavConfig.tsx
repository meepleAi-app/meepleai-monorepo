'use client';

/**
 * ContainersNavConfig — Registers ActionBar actions for /admin/monitor/containers
 * Issue #143
 */

import { useEffect } from 'react';

import { RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function ContainersNavConfig() {
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
