/**
 * Tier-Strategy Configuration Admin Page
 * Issue #3440: Admin UI for tier-strategy configuration
 *
 * Route: /admin/rag/tier-strategy-config
 * Authorization: Admin only
 *
 * Features:
 * - View tier-strategy access matrix
 * - Configure tier access to RAG strategies
 * - Manage strategy-model mappings
 * - Reset configuration to defaults
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { TierStrategyConfigClient } from './client';

export const metadata: Metadata = {
  title: 'Tier-Strategy Configuration - Admin - MeepleAI',
  description: 'Configure tier access to RAG strategies and strategy-model mappings',
};

export default function TierStrategyConfigPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <TierStrategyConfigClient />
    </RequireRole>
  );
}
