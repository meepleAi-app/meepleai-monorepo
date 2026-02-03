/**
 * AddGameButton - Client component for conditional "Add Game" button
 *
 * Shows "Aggiungi Gioco" button only for users with Editor or Admin role.
 * Uses useCurrentUser hook to check authentication and role.
 */

'use client';

import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { canEdit } from '@/types/auth';

export function AddGameButton() {
  const { data: user, isLoading } = useCurrentUser();

  // Don't show while loading or if user can't edit
  if (isLoading || !canEdit(user ?? null)) {
    return null;
  }

  return (
    <Link href="/games/add">
      <Button>Aggiungi Gioco</Button>
    </Link>
  );
}
