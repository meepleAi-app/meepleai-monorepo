'use client';

/**
 * AdminAiNavConfig — Registers ActionBar actions for /admin/ai hub
 * Issue #5048 — Admin AI Hub Page Migration
 *
 * No MiniNav (admin uses contextual sidebar for section navigation).
 * ActionBar: New Agent (primary) · New Definition · New Prompt
 *
 * Include in admin/ai/page.tsx:
 *   <AdminAiNavConfig />
 */

import { useEffect } from 'react';

import { Bot, FileText, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function AdminAiNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [],
      actionBar: [
        {
          id: 'new-agent',
          label: 'New Agent',
          icon: Bot,
          variant: 'primary',
          onClick: () => router.push('/admin/agents/new'),
        },
        {
          id: 'new-definition',
          label: 'New Definition',
          icon: FileText,
          onClick: () => router.push('/admin/agents/definitions/create'),
        },
        {
          id: 'new-prompt',
          label: 'New Prompt',
          icon: Plus,
          onClick: () => router.push('/admin/prompts/new'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
