'use client';

/**
 * OperationsNavConfig — Registers ActionBar actions for /admin/monitor/operations
 * Issue #126 — Operations Console page
 */

import { useEffect } from 'react';

import { RefreshCw } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function OperationsNavConfig() {
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
