/**
 * AI Requests Monitoring Page
 *
 * Moved from the legacy admin dashboard (admin-client.tsx).
 * Provides detailed AI request monitoring with charts, filtering,
 * pagination, and CSV export.
 *
 * Linked from Admin Hub → Overview → AI Usage card.
 */

import type { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { AdminClient } from '../admin-client';

export const metadata: Metadata = {
  title: 'AI Requests Monitor | Admin',
  description: 'Monitor AI requests, latency, token usage, and feedback',
};

export default function AiRequestsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminClient />
    </RequireRole>
  );
}
