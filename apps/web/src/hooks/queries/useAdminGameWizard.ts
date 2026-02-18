/**
 * Admin Game Wizard Hooks
 * React Query hooks for the admin game+PDF+agent wizard flow.
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { getApiBase } from '@/lib/api';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface CreateGameFromWizardResult {
  sharedGameId: string;
  title: string;
  bggId: number;
  status: string;
}

// ─── API Calls ───────────────────────────────────────────────────────────────

async function createGameFromWizard(bggId: number): Promise<CreateGameFromWizardResult> {
  const res = await fetch(`${getApiBase()}/api/v1/admin/games/wizard/create`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ bggId }),
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ detail: 'Game creation failed' }));
    if (res.status === 409) {
      throw new Error(error.detail || 'A game with this BGG ID already exists');
    }
    throw new Error(error.detail || error.message || 'Failed to create game');
  }

  return res.json();
}

// ─── Hooks ───────────────────────────────────────────────────────────────────

/** Mutation to create a game from BGG data via the admin wizard */
export function useCreateGameFromWizard() {
  const queryClient = useQueryClient();

  return useMutation<CreateGameFromWizardResult, Error, number>({
    mutationFn: createGameFromWizard,
    onSuccess: (data) => {
      toast.success(`Game "${data.title}" created successfully`);
      // Invalidate shared games cache so the new game appears in lists
      queryClient.invalidateQueries({ queryKey: ['sharedGames'] });
    },
    onError: (error) => {
      toast.error(error.message || 'Failed to create game');
    },
  });
}
