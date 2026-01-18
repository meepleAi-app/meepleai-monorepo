/**
 * Agent Mode Store
 * Issue #2413: Agent Mode Selector Component
 *
 * Zustand store for managing AI agent operation mode selection.
 */

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';

import type { AgentMode } from '@/components/agent';

interface AgentModeStore {
  // Current state
  mode: AgentMode;
  previousMode: AgentMode | null;

  // Metadata
  lastChanged: string | null;

  // Actions
  setMode: (mode: AgentMode) => void;
  resetMode: () => void;

  // Utility getters
  isRulesClarifier: () => boolean;
  isStrategyAdvisor: () => boolean;
  isSetupAssistant: () => boolean;
}

const DEFAULT_MODE: AgentMode = 'RulesClarifier';

const initialState = {
  mode: DEFAULT_MODE,
  previousMode: null,
  lastChanged: null,
};

export const useAgentModeStore = create<AgentModeStore>()(
  devtools(
    persist(
      (set, get) => ({
        ...initialState,

        setMode: (mode: AgentMode) => {
          const currentMode = get().mode;
          set({
            mode,
            previousMode: currentMode,
            lastChanged: new Date().toISOString(),
          });
        },

        resetMode: () => {
          set({
            mode: DEFAULT_MODE,
            previousMode: get().mode,
            lastChanged: new Date().toISOString(),
          });
        },

        // Utility getters
        isRulesClarifier: () => get().mode === 'RulesClarifier',
        isStrategyAdvisor: () => get().mode === 'StrategyAdvisor',
        isSetupAssistant: () => get().mode === 'SetupAssistant',
      }),
      {
        name: 'agent-mode-storage', // localStorage key
        partialize: state => ({
          mode: state.mode,
          lastChanged: state.lastChanged,
        }),
      }
    ),
    { name: 'AgentModeStore' }
  )
);
