/**
 * Admin Pending Deletes Page - Server Component Wrapper
 * Issue #2372: SharedGameCatalog Phase 3 Frontend Admin UI
 *
 * Security:
 * - RequireRole: Admin-only access for managing delete requests
 * - Editors can request deletions, only Admins can approve/reject
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { PendingDeletesClient } from './client';

export const metadata: Metadata = {
  title: 'Eliminazioni in Attesa | MeepleAI Admin',
  description: 'Gestisci le richieste di eliminazione dei giochi in attesa di approvazione',
};

export default function PendingDeletesPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PendingDeletesClient />
    </RequireRole>
  );
}
