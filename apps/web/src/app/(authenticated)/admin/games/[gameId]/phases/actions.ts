'use server';

/**
 * Server Actions — Phase Templates Admin
 *
 * Game Session Flow v2.0 — Task 13
 */

import { revalidatePath } from 'next/cache';

import { api } from '@/lib/api';

export interface PhaseInput {
  phaseName: string;
  phaseOrder: number;
  description?: string;
}

export async function savePhaseTemplatesAction(
  gameId: string,
  phases: PhaseInput[]
): Promise<{ success: boolean; error?: string }> {
  try {
    await api.games.upsertPhaseTemplates(gameId, phases);
    revalidatePath(`/admin/games/${gameId}/phases`);
    return { success: true };
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Errore durante il salvataggio';
    return { success: false, error: message };
  }
}

export async function suggestPhaseTemplatesAction(gameId: string): Promise<{
  success: boolean;
  suggestions?: Array<{
    phaseName: string;
    phaseOrder: number;
    description: string;
    rationale: string;
  }>;
  error?: string;
}> {
  try {
    const suggestions = await api.games.suggestPhaseTemplates(gameId);
    return { success: true, suggestions };
  } catch (err) {
    const message =
      err instanceof Error ? err.message : 'Errore durante la generazione suggerimenti';
    return { success: false, error: message };
  }
}
