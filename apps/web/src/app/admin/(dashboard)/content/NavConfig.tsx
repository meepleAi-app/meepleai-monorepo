'use client';

/**
 * AdminContentNavConfig — Registers ActionBar actions for /admin/content hub
 * Issue #5049 — Admin Content Hub Page Migration
 *
 * No MiniNav (admin uses contextual sidebar for section navigation).
 * ActionBar: Add Game (primary) · New FAQ · Approve Submissions
 *
 * Include in admin/content/page.tsx:
 *   <AdminContentNavConfig />
 */

import { useEffect } from 'react';

import { CheckCircle, HelpCircle, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AdminContentNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [],
      actionBar: [
        {
          id: 'add-game',
          label: 'Add Game',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/admin/games/new'),
        },
        {
          id: 'new-faq',
          label: 'New FAQ',
          icon: HelpCircle,
          onClick: () => router.push('/admin/faqs/new'),
        },
        {
          id: 'approve',
          label: 'Approve Submissions',
          icon: CheckCircle,
          onClick: () => router.push('/admin/shared-games/approval-queue'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
