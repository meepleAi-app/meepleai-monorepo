/**
 * Admin Game Sessions Monitoring Page - Server Component Wrapper
 * Issue #3948: Global game sessions monitoring
 *
 * Security:
 * - RequireRole: Admin only
 * - View all play records across all users
 * - Monitor gameplay statistics and trends
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { GameSessionsClient } from './client';

export const metadata: Metadata = {
  title: 'Game Sessions Monitoring | MeepleAI Admin',
  description: 'Monitor and analyze game sessions across all users',
};

export default function GameSessionsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <GameSessionsClient />
    </RequireRole>
  );
}
