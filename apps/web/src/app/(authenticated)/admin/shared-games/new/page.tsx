/**
 * Admin New Game Page - Server Component Wrapper
 * Issue #2372: SharedGameCatalog Phase 3 Frontend Admin UI
 *
 * Security:
 * - RequireRole: Admin/Editor access for creating games
 * - Uses GameForm component for consistent validation
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { NewGameClient } from './client';

export const metadata: Metadata = {
  title: 'Nuovo Gioco | MeepleAI Admin',
  description: 'Aggiungi un nuovo gioco al catalogo condiviso',
};

export default function NewGamePage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <NewGameClient />
    </RequireRole>
  );
}
