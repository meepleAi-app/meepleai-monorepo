/**
 * Admin Add Game from BGG Page - Server Component Wrapper
 * Issue: Admin Add Shared Game from BGG flow
 *
 * Flow:
 * 1. Admin searches for game on BGG (autocomplete)
 * 2. Selects game from results
 * 3. System checks for duplicates
 *    - If duplicate: shows diff modal with "Update existing" option
 *    - If new: shows preview form with editable fields
 * 4. Admin confirms to save
 *
 * Security:
 * - RequireRole: Admin/Editor access for importing games
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { AddFromBggClient } from './client';

export const metadata: Metadata = {
  title: 'Aggiungi da BGG | MeepleAI Admin',
  description: 'Importa un gioco da BoardGameGeek nel catalogo condiviso',
};

export default function AddFromBggPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <AddFromBggClient />
    </RequireRole>
  );
}
