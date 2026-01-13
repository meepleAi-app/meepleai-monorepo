/**
 * Admin Edit Game Page - Server Component Wrapper
 * Issue #2372: SharedGameCatalog Phase 3 Frontend Admin UI
 *
 * Security:
 * - RequireRole: Admin/Editor access for editing games
 * - Uses GameForm component for consistent validation
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { EditGameClient } from './client';

export const metadata: Metadata = {
  title: 'Modifica Gioco | MeepleAI Admin',
  description: 'Modifica i dettagli di un gioco nel catalogo condiviso',
};

export default function EditGamePage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <EditGameClient />
    </RequireRole>
  );
}
