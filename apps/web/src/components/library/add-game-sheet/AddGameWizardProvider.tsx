'use client';

/**
 * AddGameWizardProvider - Global context for opening the AddGameSheet wizard.
 * Issue #4822: MeepleCard Rewire - "Aggiungi alla Collezione" → Open Wizard
 * Epic #4817: User Collection Wizard
 *
 * Place in AppProviders so any component can trigger the wizard via useAddGameWizard().
 */

import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';

import { useQueryClient } from '@tanstack/react-query';

import { libraryKeys } from '@/hooks/queries/useLibrary';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';
import type { WizardEntryPoint, SelectedGameData } from '@/lib/stores/add-game-wizard-store';

import { AddGameSheet } from './AddGameSheet';

// ============================================================================
// Types
// ============================================================================

export interface AddGameWizardContextValue {
  /** Open the wizard with given entry point and optional game data */
  openWizard: (entryPoint: WizardEntryPoint, gameData?: SelectedGameData) => void;
  /** Whether the wizard is currently open */
  isOpen: boolean;
}

// ============================================================================
// Context
// ============================================================================

const AddGameWizardContext = createContext<AddGameWizardContextValue | null>(null);

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to access the AddGameSheet wizard.
 * Must be used within AddGameWizardProvider.
 */
export function useAddGameWizard(): AddGameWizardContextValue {
  const ctx = useContext(AddGameWizardContext);
  if (!ctx) {
    throw new Error('useAddGameWizard must be used within AddGameWizardProvider');
  }
  return ctx;
}

// ============================================================================
// Provider
// ============================================================================

export function AddGameWizardProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  const [open, setOpen] = useState(false);
  const [entryPoint, setEntryPoint] = useState<WizardEntryPoint>({ type: 'fromLibrary' });
  const [gameData, setGameData] = useState<SelectedGameData | undefined>(undefined);

  const openWizard = useCallback((ep: WizardEntryPoint, gd?: SelectedGameData) => {
    setEntryPoint(ep);
    setGameData(gd);
    setOpen(true);
  }, []);

  const handleOpenChange = useCallback((isOpen: boolean) => {
    setOpen(isOpen);
  }, []);

  const handleSuccess = useCallback(
    (_libraryEntryId: string) => {
      // Read gameId from Zustand store at success time to avoid stale closure
      const savedGameId = useAddGameWizardStore.getState().selectedGame?.gameId;
      // Invalidate library caches so UI reflects the new game
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      // Also invalidate the specific game's library status
      if (savedGameId) {
        queryClient.invalidateQueries({ queryKey: ['library-status', savedGameId] });
        queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(savedGameId) });
      }
    },
    [queryClient]
  );

  return (
    <AddGameWizardContext.Provider value={{ openWizard, isOpen: open }}>
      {children}
      <AddGameSheet
        open={open}
        onOpenChange={handleOpenChange}
        entryPoint={entryPoint}
        gameData={gameData}
        onSuccess={handleSuccess}
      />
    </AddGameWizardContext.Provider>
  );
}
