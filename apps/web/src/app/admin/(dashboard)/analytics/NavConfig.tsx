'use client';

/**
 * AdminAnalyticsNavConfig — Registers ActionBar actions for /admin/analytics hub
 * Issue #5051 — Admin Analytics Hub Page Migration
 *
 * No MiniNav (admin uses contextual sidebar for section navigation).
 * ActionBar: Export Report (primary) · New API Key
 *
 * Include in admin/analytics/page.tsx:
 *   <AdminAnalyticsNavConfig />
 */

import { useEffect } from 'react';

import { Download, Key } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AdminAnalyticsNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [],
      actionBar: [
        {
          id: 'export-report',
          label: 'Export Report',
          icon: Download,
          variant: 'primary',
          onClick: () => router.push('/admin/analytics?action=export'),
        },
        {
          id: 'new-api-key',
          label: 'New API Key',
          icon: Key,
          onClick: () => router.push('/admin/api-keys/new'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
