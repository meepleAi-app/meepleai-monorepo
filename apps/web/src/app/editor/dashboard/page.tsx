/**
 * Editor Dashboard Page - Server Component Wrapper
 * Issue #2897: Editor Dashboard E2E Tests
 *
 * Security:
 * - RequireRole: Editor/Admin access
 * - Editors see only their own games
 * - All operations use CQRS commands/queries via sharedGamesClient
 * - Session validated at middleware level
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { EditorDashboardClient } from './client';

export const metadata: Metadata = {
  title: 'My Games Dashboard | MeepleAI Editor',
  description: 'View and manage your game submissions for the shared game catalog',
};

export default function EditorDashboardPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <EditorDashboardClient />
    </RequireRole>
  );
}
