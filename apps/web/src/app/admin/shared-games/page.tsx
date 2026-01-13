/**
 * Admin Shared Game Catalog Page - Server Component Wrapper
 * Issue #2372: SharedGameCatalog Phase 3 Frontend Admin UI
 *
 * Security:
 * - RequireRole: Admin/Editor access
 * - All operations use CQRS commands/queries via sharedGamesClient
 * - Session validated at middleware level
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { SharedGamesClient } from './client';

export const metadata: Metadata = {
  title: 'Shared Game Catalog | MeepleAI Admin',
  description:
    'Manage the shared game catalog with CRUD operations, soft delete workflow, and BGG integration',
};

export default function SharedGamesPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <SharedGamesClient />
    </RequireRole>
  );
}
