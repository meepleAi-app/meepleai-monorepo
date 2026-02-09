/**
 * Admin Game Session Detail Page - Server Component Wrapper
 * Issue #3948: Game session detail view for admins
 *
 * Security:
 * - RequireRole: Admin only
 * - View detailed play record information
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { GameSessionDetailClient } from './client';

export const metadata: Metadata = {
  title: 'Session Details | MeepleAI Admin',
  description: 'View detailed information about a game session',
};

export default function GameSessionDetailPage({ params }: { params: { sessionId: string } }) {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <GameSessionDetailClient sessionId={params.sessionId} />
    </RequireRole>
  );
}
