/**
 * AI Models Management Page (Issue #2521)
 *
 * Admin dashboard for AI model runtime configuration, cost tracking, and usage monitoring.
 *
 * Route: /admin/ai-models
 * Authorization: Admin only
 *
 * Features:
 * - View all available AI models with usage stats
 * - Configure model parameters (temperature, maxTokens)
 * - Set primary model for responses
 * - Real-time cost tracking (daily/monthly)
 * - Budget alerts
 * - Test models with sample prompts
 * - Export usage reports (CSV/JSON)
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { AiModelsClient } from './client';

export const metadata: Metadata = {
  title: 'AI Models Management - Admin - MeepleAI',
  description: 'Manage AI models, configure parameters, and monitor costs',
};

export default function AiModelsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AiModelsClient />
    </RequireRole>
  );
}
