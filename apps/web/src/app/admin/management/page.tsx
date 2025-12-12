/**
 * Admin Management Integration Page - Server Component Wrapper
 * Issue #903: FASE 3 Enhanced Management - Final Integration
 *
 * Comprehensive management interface combining:
 * - API Keys Management (#908, #909, #910)
 * - User Management with Bulk Operations (#905, #906)
 * - User Activity Timeline (#911)
 * - Bulk Action Bar (#912)
 *
 * Security:
 * - RequireRole: Admin-only access
 * - All operations use CQRS commands/queries
 * - Session validated at middleware level
 */

import { Metadata } from 'next';
import { RequireRole } from '@/components/auth/RequireRole';
import { ManagementPageClient } from './client';

export const metadata: Metadata = {
  title: 'System Management | MeepleAI Admin',
  description:
    'Comprehensive management interface for API keys, users, and system activity monitoring',
};

export default function ManagementPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <ManagementPageClient />
    </RequireRole>
  );
}
