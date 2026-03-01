/**
 * Add Game Wizard Store Tests
 * Issue #4818: AddGameSheet Drawer + State Machine
 * Epic #4817: User Collection Wizard
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { useAddGameWizardStore, type SelectedGameData, type WizardEntryPoint } from '../add-game-wizard-store';

const testGame: SelectedGameData = {
  gameId: 'game-123',
  title: 'Catan',
  imageUrl: 'https://example.com/catan.jpg',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  source: 'catalog',
  categories: ['Strategy'],
  mechanics: ['Trading'],
};

describe('useAddGameWizardStore', () => {
  beforeEach(() => {
    useAddGameWizardStore.getState().reset();
  });

  describe('initialize', () => {
    it('should initialize with step 1 for fromLibrary entry point', () => {
      const entryPoint: WizardEntryPoint = { type: 'fromLibrary' };
      useAddGameWizardStore.getState().initialize(entryPoint);

      const state = useAddGameWizardStore.getState();
      expect(state.currentStep).toBe(1);
      expect(state.entryPoint).toEqual(entryPoint);
      expect(state.selectedGame).toBeNull();
      expect(state.isDirty).toBe(false);
    });

    it('should initialize with step 2 for fromGameCard entry point', () => {
      const entryPoint: WizardEntryPoint = { type: 'fromGameCard', sharedGameId: 'game-123' };
      useAddGameWizardStore.getState().initialize(entryPoint, testGame);

      const state = useAddGameWizardStore.getState();
      expect(state.currentStep).toBe(2);
      expect(state.selectedGame).toEqual(testGame);
    });

    it('should initialize with step 2 for fromSearch entry point', () => {
      const entryPoint: WizardEntryPoint = { type: 'fromSearch', bggId: 13 };
      useAddGameWizardStore.getState().initialize(entryPoint);

      const state = useAddGameWizardStore.getState();
      expect(state.currentStep).toBe(2);
    });

    it('should reset previous state when initializing', () => {
      // Set dirty state first
      useAddGameWizardStore.getState().markDirty();
      expect(useAddGameWizardStore.getState().isDirty).toBe(true);

      // Re-initialize should reset
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      expect(useAddGameWizardStore.getState().isDirty).toBe(false);
    });
  });

  describe('navigation', () => {
    it('should go next from step 1 to 2', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().goNext();
      expect(useAddGameWizardStore.getState().currentStep).toBe(2);
    });

    it('should go next from step 2 to 3', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCurrentStep(2);
      useAddGameWizardStore.getState().goNext();
      expect(useAddGameWizardStore.getState().currentStep).toBe(3);
    });

    it('should not go beyond step 3', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCurrentStep(3);
      useAddGameWizardStore.getState().goNext();
      expect(useAddGameWizardStore.getState().currentStep).toBe(3);
    });

    it('should go back from step 2 to 1 for fromLibrary', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCurrentStep(2);
      useAddGameWizardStore.getState().goBack();
      expect(useAddGameWizardStore.getState().currentStep).toBe(1);
    });

    it('should not go back before step 2 for fromGameCard', () => {
      useAddGameWizardStore.getState().initialize({
        type: 'fromGameCard',
        sharedGameId: 'game-123',
      });
      expect(useAddGameWizardStore.getState().currentStep).toBe(2);
      useAddGameWizardStore.getState().goBack();
      expect(useAddGameWizardStore.getState().currentStep).toBe(2);
    });

    it('should not go back before step 2 for fromSearch', () => {
      useAddGameWizardStore.getState().initialize({
        type: 'fromSearch',
        bggId: 13,
      });
      expect(useAddGameWizardStore.getState().currentStep).toBe(2);
      useAddGameWizardStore.getState().goBack();
      expect(useAddGameWizardStore.getState().currentStep).toBe(2);
    });

    it('should not go back before step 1 for fromLibrary', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().goBack();
      expect(useAddGameWizardStore.getState().currentStep).toBe(1);
    });
  });

  describe('canGoNext', () => {
    it('should allow next from step 1 when game is selected', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setSelectedGame(testGame);
      expect(useAddGameWizardStore.getState().canGoNext()).toBe(true);
    });

    it('should not allow next from step 1 without game selected', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      expect(useAddGameWizardStore.getState().canGoNext()).toBe(false);
    });

    it('should allow next from step 2 (PDF is optional)', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCurrentStep(2);
      expect(useAddGameWizardStore.getState().canGoNext()).toBe(true);
    });

    it('should not allow next from step 3 (final step)', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCurrentStep(3);
      expect(useAddGameWizardStore.getState().canGoNext()).toBe(false);
    });
  });

  describe('state updates', () => {
    it('should set selected game and mark dirty', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setSelectedGame(testGame);

      const state = useAddGameWizardStore.getState();
      expect(state.selectedGame).toEqual(testGame);
      expect(state.isDirty).toBe(true);
    });

    it('should set documents', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      const docs = [{ id: 'pdf-1', fileName: 'rules.pdf', status: 'Ready', documentType: 'base' }];
      useAddGameWizardStore.getState().setDocuments(docs);
      expect(useAddGameWizardStore.getState().documents).toEqual(docs);
    });

    it('should set custom pdf uploaded flag and mark dirty', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      useAddGameWizardStore.getState().setCustomPdfUploaded(true);

      const state = useAddGameWizardStore.getState();
      expect(state.customPdfUploaded).toBe(true);
      expect(state.isDirty).toBe(true);
    });

    it('should set game info and mark dirty', () => {
      useAddGameWizardStore.getState().initialize({ type: 'fromLibrary' });
      const info = { title: 'Catan', minPlayers: 3, maxPlayers: 4 };
      useAddGameWizardStore.getState().setGameInfo(info);

      const state = useAddGameWizardStore.getState();
      expect(state.gameInfo).toEqual(info);
      expect(state.isDirty).toBe(true);
    });
  });

  describe('reset', () => {
    it('should reset all state to initial values', () => {
      useAddGameWizardStore.getState().initialize({
        type: 'fromGameCard',
        sharedGameId: 'game-123',
      }, testGame);
      useAddGameWizardStore.getState().markDirty();

      useAddGameWizardStore.getState().reset();

      const state = useAddGameWizardStore.getState();
      expect(state.currentStep).toBe(1);
      expect(state.entryPoint).toBeNull();
      expect(state.selectedGame).toBeNull();
      expect(state.isDirty).toBe(false);
      expect(state.documents).toEqual([]);
      expect(state.gameInfo).toBeNull();
    });
  });
});
