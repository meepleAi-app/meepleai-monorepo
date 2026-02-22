'use client';

/**
 * AdminConfigNavConfig — Registers ActionBar actions for /admin/config hub
 * Issue #5052 — Admin Config Hub Page Migration
 *
 * No MiniNav (admin uses contextual sidebar for section navigation).
 * ActionBar: Save Changes (primary) · Reset to Defaults
 *
 * Include in admin/config/page.tsx:
 *   <AdminConfigNavConfig />
 */

import { useEffect } from 'react';

import { RotateCcw, Save } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AdminConfigNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [],
      actionBar: [
        {
          id: 'save-config',
          label: 'Save Changes',
          icon: Save,
          variant: 'primary',
          onClick: () => router.push('/admin/config?action=save'),
        },
        {
          id: 'reset-config',
          label: 'Reset Defaults',
          icon: RotateCcw,
          onClick: () => router.push('/admin/config?action=reset'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
