/**
 * Enterprise Resources Section Page
 * Issue #3689 - Layout Base & Navigation System
 * Issue #3692 - Token Management System
 *
 * Section 2: Resources & Infrastructure
 * Tabs: Tokens | Database | Cache | Vectors | Services
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { TokensTab } from '@/components/admin/enterprise/tokens';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'resources')!;

export default function ResourcesPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{
        tokens: <TokensTab />,
      }}
      fallback={
        <PlaceholderContent sectionName="Resources & Infrastructure" />
      }
    />
  );
}

function PlaceholderContent({ sectionName }: { sectionName: string }) {
  return (
    <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-8 text-center">
      <h3 className="font-quicksand font-bold text-lg text-zinc-900 dark:text-zinc-100 mb-2">
        {sectionName}
      </h3>
      <p className="text-sm text-zinc-500 dark:text-zinc-400">
        Content for this tab will be implemented in upcoming issues.
      </p>
    </div>
  );
}
