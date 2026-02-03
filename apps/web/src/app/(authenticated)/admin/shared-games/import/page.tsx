/**
 * Admin Bulk Import Page - Server Component Wrapper
 * Issue #2372: SharedGameCatalog Phase 3 Frontend Admin UI
 *
 * Security:
 * - RequireRole: Admin/Editor access for importing games
 * - CSV upload and BGG ID import functionality
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { ImportClient } from './client';

export const metadata: Metadata = {
  title: 'Importazione Giochi | MeepleAI Admin',
  description: 'Importa giochi da CSV o BoardGameGeek nel catalogo condiviso',
};

export default function ImportPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <ImportClient />
    </RequireRole>
  );
}
