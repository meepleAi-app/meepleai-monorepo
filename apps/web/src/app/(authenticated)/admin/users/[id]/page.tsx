/**
 * Admin User Detail Page - Server Component Wrapper
 * Gap Analysis: Missing UI for GET /admin/users/{userId}/activity
 *
 * Security:
 * - RequireRole: Admin only
 * - All operations use CQRS commands/queries via adminClient
 * - Session validated at middleware level
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { UserDetailClient } from './client';

export const metadata: Metadata = {
  title: 'User Detail | MeepleAI Admin',
  description: 'View user details and activity timeline',
};

interface UserDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function UserDetailPage({ params }: UserDetailPageProps) {
  const { id } = await params;

  return (
    <RequireRole allowedRoles={['Admin']}>
      <UserDetailClient userId={id} />
    </RequireRole>
  );
}
