/**
 * RAG Configuration Store
 *
 * Zustand store for managing RAG strategy configuration state.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type {
  RagConfig,
  GenerationParams,
  RetrievalParams,
  RerankerSettings,
  ModelSelection,
  StrategySpecificSettings,
} from '@/components/rag-dashboard/config/types';
import { DEFAULT_RAG_CONFIG, STRATEGY_PRESETS } from '@/components/rag-dashboard/config/types';
import type { RetrievalStrategyType } from '@/components/rag-dashboard/retrieval-strategies';
import { api } from '@/lib/api';

/**
 * RAG Configuration Store State
 */
interface RagConfigState {
  /**
   * Current configuration.
   */
  config: RagConfig;

  /**
   * Whether configuration has unsaved changes.
   */
  isDirty: boolean;

  /**
   * Whether save operation is in progress.
   */
  isSaving: boolean;

  /**
   * Last save error, if any.
   */
  saveError: string | null;
}

/**
 * RAG Configuration Store Actions
 */
interface RagConfigActions {
  /**
   * Update generation parameters.
   */
  setGenerationParams: (params: Partial<GenerationParams>) => void;

  /**
   * Update retrieval parameters.
   */
  setRetrievalParams: (params: Partial<RetrievalParams>) => void;

  /**
   * Update reranker settings.
   */
  setRerankerSettings: (settings: Partial<RerankerSettings>) => void;

  /**
   * Update model selection.
   */
  setModelSelection: (selection: Partial<ModelSelection>) => void;

  /**
   * Update strategy-specific settings.
   */
  setStrategySpecific: (settings: Partial<StrategySpecificSettings>) => void;

  /**
   * Set active strategy and optionally apply preset.
   */
  setActiveStrategy: (strategy: RetrievalStrategyType, applyPreset?: boolean) => void;

  /**
   * Apply a strategy preset.
   */
  applyPreset: (strategy: RetrievalStrategyType) => void;

  /**
   * Reset all settings to defaults.
   */
  resetToDefaults: () => void;

  /**
   * Save configuration to server.
   */
  saveConfig: () => Promise<void>;

  /**
   * Load user configuration from server.
   */
  loadUserConfig: () => Promise<void>;

  /**
   * Clear save error.
   */
  clearError: () => void;
}

export type RagConfigStore = RagConfigState & RagConfigActions;

/**
 * RAG Configuration Store
 *
 * Persists configuration to localStorage for draft saving.
 */
export const useRagConfigStore = create<RagConfigStore>()(
  persist(
    (set, get) => ({
      // Initial state
      config: DEFAULT_RAG_CONFIG,
      isDirty: false,
      isSaving: false,
      saveError: null,

      // Actions
      setGenerationParams: params =>
        set(state => ({
          config: {
            ...state.config,
            generation: { ...state.config.generation, ...params },
          },
          isDirty: true,
        })),

      setRetrievalParams: params =>
        set(state => ({
          config: {
            ...state.config,
            retrieval: { ...state.config.retrieval, ...params },
          },
          isDirty: true,
        })),

      setRerankerSettings: settings =>
        set(state => ({
          config: {
            ...state.config,
            reranker: { ...state.config.reranker, ...settings },
          },
          isDirty: true,
        })),

      setModelSelection: selection =>
        set(state => ({
          config: {
            ...state.config,
            models: { ...state.config.models, ...selection },
          },
          isDirty: true,
        })),

      setStrategySpecific: settings =>
        set(state => ({
          config: {
            ...state.config,
            strategySpecific: { ...state.config.strategySpecific, ...settings },
          },
          isDirty: true,
        })),

      setActiveStrategy: (strategy, applyPreset = false) => {
        set(state => ({
          config: {
            ...state.config,
            activeStrategy: strategy,
          },
          isDirty: true,
        }));

        if (applyPreset) {
          get().applyPreset(strategy);
        }
      },

      applyPreset: strategy => {
        // Validate strategy key before accessing preset
        if (!Object.hasOwn(STRATEGY_PRESETS, strategy)) return;
        const preset = STRATEGY_PRESETS[strategy];
        if (!preset) return;

        set(state => ({
          config: {
            ...state.config,
            ...preset,
            generation: { ...state.config.generation, ...preset.generation },
            retrieval: { ...state.config.retrieval, ...preset.retrieval },
            reranker: { ...state.config.reranker, ...preset.reranker },
            models: { ...state.config.models, ...preset.models },
            strategySpecific: { ...state.config.strategySpecific, ...preset.strategySpecific },
          },
          isDirty: true,
        }));
      },

      resetToDefaults: () =>
        set({
          config: DEFAULT_RAG_CONFIG,
          isDirty: true,
        }),

      saveConfig: async () => {
        set({ isSaving: true, saveError: null });

        try {
          const { config } = get();
          await api.knowledgeBase.saveRagConfig(config as RagConfig);
          set({ isDirty: false, isSaving: false });
        } catch (error) {
          set({
            saveError: error instanceof Error ? error.message : 'Failed to save configuration',
            isSaving: false,
          });
        }
      },

      loadUserConfig: async () => {
        set({ isSaving: true, saveError: null });

        try {
          const config = await api.knowledgeBase.getRagConfig();
          if (config) {
            set({ config: config as RagConfig, isDirty: false, isSaving: false });
          } else {
            set({ isSaving: false });
          }
        } catch (error) {
          set({
            saveError: error instanceof Error ? error.message : 'Failed to load configuration',
            isSaving: false,
          });
        }
      },

      clearError: () => set({ saveError: null }),
    }),
    {
      name: 'rag-config-storage',
      partialize: state => ({ config: state.config }),
    }
  )
);

/**
 * Hook for accessing RAG configuration.
 */
export function useRagConfig() {
  const store = useRagConfigStore();

  return {
    config: store.config,
    isDirty: store.isDirty,
    isSaving: store.isSaving,
    saveError: store.saveError,
    setGenerationParams: store.setGenerationParams,
    setRetrievalParams: store.setRetrievalParams,
    setRerankerSettings: store.setRerankerSettings,
    setModelSelection: store.setModelSelection,
    setStrategySpecific: store.setStrategySpecific,
    setActiveStrategy: store.setActiveStrategy,
    applyPreset: store.applyPreset,
    resetToDefaults: store.resetToDefaults,
    saveConfig: store.saveConfig,
    loadUserConfig: store.loadUserConfig,
    clearError: store.clearError,
  };
}
