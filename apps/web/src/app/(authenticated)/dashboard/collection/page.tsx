/**
 * Collection Dashboard Page - Issue #3476
 *
 * User's personal game collection dashboard with:
 * - Hero Stats (Total Games, PDFs, Chats, Reading Time)
 * - Collection Grid (games with sort/filter)
 * - Activity Feed (recent actions)
 *
 * Features:
 * - Server Component with metadata
 * - Client-side interactivity
 * - Role-based access control
 * - Responsive design
 *
 * @see docs/features/dashboard-requirements.md
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { CollectionDashboardClient } from './collection-client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'La Mia Collezione | MeepleAI',
  description:
    'Gestisci la tua collezione personale di giochi da tavolo. Visualizza statistiche, organizza giochi, traccia partite e chat AI.',
  openGraph: {
    title: 'La Mia Collezione | MeepleAI',
    description:
      'Gestisci la tua collezione personale di giochi da tavolo. Visualizza statistiche, organizza giochi, traccia partite e chat AI.',
    type: 'website',
  },
};

export const dynamic = 'force-dynamic';

export default function CollectionPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <CollectionDashboardClient />
    </RequireRole>
  );
}
