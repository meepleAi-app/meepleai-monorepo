/**
 * Toolbox Store — Epic #412
 *
 * Zustand store for Toolbox lifecycle, tool management, card deck operations,
 * phase management, and real-time sync via SSE.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type {
  ToolboxDto,
  ToolboxToolDto,
  PhaseDto,
  CardDrawResultDto,
  DrawnCardDto,
  CreateToolboxRequest,
  AddToolRequest,
  UpdateSharedContextRequest,
  AddPhaseRequest,
  CreateCardDeckRequest,
} from '@/lib/api/schemas/toolbox.schemas';

// ============================================================================
// Types
// ============================================================================

export interface DeckState {
  deckId: string;
  name: string;
  totalCards: number;
  remainingInDeck: number;
  drawnCards: DrawnCardDto[];
}

export interface ToolboxStore {
  // State
  toolbox: ToolboxDto | null;
  currentPhase: PhaseDto | null;
  isOffline: boolean;
  isLoading: boolean;
  error: string | null;

  // Card deck state
  cardDecks: Record<string, DeckState>;

  // Tool expand/collapse state
  expandedTools: Set<string>;

  // Actions — Lifecycle
  loadToolbox: (id: string) => Promise<void>;
  loadToolboxByGame: (gameId: string) => Promise<void>;
  createToolbox: (req: CreateToolboxRequest) => Promise<string>;
  setOffline: (offline: boolean) => void;

  // Actions — Tool Management
  addTool: (req: AddToolRequest) => Promise<void>;
  removeTool: (toolId: string) => Promise<void>;
  reorderTools: (orderedIds: string[]) => Promise<void>;
  toggleTool: (toolId: string) => void;

  // Actions — Shared Context
  updateSharedContext: (req: UpdateSharedContextRequest) => Promise<void>;

  // Actions — Card Deck
  createCardDeck: (req: CreateCardDeckRequest) => Promise<void>;
  shuffle: (deckId: string) => Promise<void>;
  draw: (deckId: string, count: number) => Promise<CardDrawResultDto | null>;
  resetDeck: (deckId: string) => Promise<void>;

  // Actions — Phases
  addPhase: (req: AddPhaseRequest) => Promise<void>;
  removePhase: (phaseId: string) => Promise<void>;
  advancePhase: () => Promise<void>;

  // Actions — Mode
  updateMode: (mode: 'Freeform' | 'Phased') => Promise<void>;

  // SSE handlers
  handleToolboxEvent: (event: { type: string; data: unknown }) => void;

  // Reset
  reset: () => void;
}

// ============================================================================
// Initial State
// ============================================================================

const initialState = {
  toolbox: null as ToolboxDto | null,
  currentPhase: null as PhaseDto | null,
  isOffline: false,
  isLoading: false,
  error: null as string | null,
  cardDecks: {} as Record<string, DeckState>,
  expandedTools: new Set<string>(),
};

// ============================================================================
// Store
// ============================================================================

export const useToolboxStore = create<ToolboxStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      // --- Lifecycle ---

      loadToolbox: async (id: string) => {
        set({ isLoading: true, error: null }, false, 'loadToolbox/start');
        try {
          const { api } = await import('@/lib/api');
          const toolbox = await api.toolbox.getToolbox(id);
          const currentPhase = toolbox?.currentPhaseId
            ? (toolbox.phases.find(p => p.id === toolbox.currentPhaseId) ?? null)
            : null;
          set({ toolbox, currentPhase, isLoading: false }, false, 'loadToolbox/success');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to load toolbox';
          set({ error: msg, isLoading: false }, false, 'loadToolbox/error');
        }
      },

      loadToolboxByGame: async (gameId: string) => {
        set({ isLoading: true, error: null }, false, 'loadToolboxByGame/start');
        try {
          const { api } = await import('@/lib/api');
          const toolbox = await api.toolbox.getToolboxByGame(gameId);
          const currentPhase = toolbox?.currentPhaseId
            ? (toolbox.phases.find(p => p.id === toolbox.currentPhaseId) ?? null)
            : null;
          set({ toolbox, currentPhase, isLoading: false }, false, 'loadToolboxByGame/success');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to load toolbox';
          set({ error: msg, isLoading: false }, false, 'loadToolboxByGame/error');
        }
      },

      createToolbox: async (req: CreateToolboxRequest) => {
        set({ isLoading: true, error: null }, false, 'createToolbox/start');
        try {
          const { api } = await import('@/lib/api');
          const toolbox = await api.toolbox.createToolbox(req);
          set({ toolbox, isLoading: false }, false, 'createToolbox/success');
          return toolbox.id;
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to create toolbox';
          set({ error: msg, isLoading: false }, false, 'createToolbox/error');
          throw err;
        }
      },

      setOffline: (offline: boolean) => {
        set({ isOffline: offline }, false, 'setOffline');
      },

      // --- Tool Management ---

      addTool: async (req: AddToolRequest) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const tool = await api.toolbox.addTool(toolbox.id, req);
          set(
            state => ({
              toolbox: state.toolbox
                ? { ...state.toolbox, tools: [...state.toolbox.tools, tool] }
                : null,
            }),
            false,
            'addTool'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to add tool';
          set({ error: msg }, false, 'addTool/error');
        }
      },

      removeTool: async (toolId: string) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          await api.toolbox.removeTool(toolbox.id, toolId);
          set(
            state => ({
              toolbox: state.toolbox
                ? { ...state.toolbox, tools: state.toolbox.tools.filter(t => t.id !== toolId) }
                : null,
            }),
            false,
            'removeTool'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to remove tool';
          set({ error: msg }, false, 'removeTool/error');
        }
      },

      reorderTools: async (orderedIds: string[]) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          await api.toolbox.reorderTools(toolbox.id, orderedIds);
          // Optimistic: reorder local state
          set(
            state => {
              if (!state.toolbox) return state;
              const reordered = orderedIds
                .map((id, i) => {
                  const tool = state.toolbox!.tools.find(t => t.id === id);
                  return tool ? { ...tool, order: i } : null;
                })
                .filter((t): t is ToolboxToolDto => t !== null);
              return { toolbox: { ...state.toolbox, tools: reordered } };
            },
            false,
            'reorderTools'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to reorder';
          set({ error: msg }, false, 'reorderTools/error');
        }
      },

      toggleTool: (toolId: string) => {
        set(
          state => {
            const next = new Set(state.expandedTools);
            if (next.has(toolId)) next.delete(toolId);
            else next.add(toolId);
            return { expandedTools: next };
          },
          false,
          'toggleTool'
        );
      },

      // --- Shared Context ---

      updateSharedContext: async (req: UpdateSharedContextRequest) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const ctx = await api.toolbox.updateSharedContext(toolbox.id, req);
          set(
            state => ({
              toolbox: state.toolbox ? { ...state.toolbox, sharedContext: ctx } : null,
            }),
            false,
            'updateSharedContext'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to update context';
          set({ error: msg }, false, 'updateSharedContext/error');
        }
      },

      // --- Card Deck ---

      createCardDeck: async (req: CreateCardDeckRequest) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const tool = await api.toolbox.createCardDeck(toolbox.id, req);
          set(
            state => ({
              toolbox: state.toolbox
                ? { ...state.toolbox, tools: [...state.toolbox.tools, tool] }
                : null,
            }),
            false,
            'createCardDeck'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to create deck';
          set({ error: msg }, false, 'createCardDeck/error');
        }
      },

      shuffle: async (deckId: string) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          await api.toolbox.shuffleDeck(toolbox.id, deckId);
          // Reset drawn cards on shuffle
          set(
            state => ({
              cardDecks: {
                ...state.cardDecks,
                [deckId]: state.cardDecks[deckId]
                  ? { ...state.cardDecks[deckId], drawnCards: [] }
                  : { deckId, name: '', totalCards: 0, remainingInDeck: 0, drawnCards: [] },
              },
            }),
            false,
            'shuffle'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to shuffle';
          set({ error: msg }, false, 'shuffle/error');
        }
      },

      draw: async (deckId: string, count: number) => {
        const { toolbox } = get();
        if (!toolbox) return null;
        try {
          const { api } = await import('@/lib/api');
          const result = await api.toolbox.drawCards(toolbox.id, deckId, count);
          set(
            state => {
              const existing = state.cardDecks[deckId];
              return {
                cardDecks: {
                  ...state.cardDecks,
                  [deckId]: {
                    deckId,
                    name: existing?.name ?? '',
                    totalCards: existing?.totalCards ?? 0,
                    remainingInDeck: result.remainingInDeck,
                    drawnCards: [...(existing?.drawnCards ?? []), ...result.cards],
                  },
                },
              };
            },
            false,
            'draw'
          );
          return result;
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to draw';
          set({ error: msg }, false, 'draw/error');
          return null;
        }
      },

      resetDeck: async (deckId: string) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          await api.toolbox.resetDeck(toolbox.id, deckId);
          set(
            state => {
              const existing = state.cardDecks[deckId];
              return {
                cardDecks: {
                  ...state.cardDecks,
                  [deckId]: {
                    deckId,
                    name: existing?.name ?? '',
                    totalCards: existing?.totalCards ?? 0,
                    remainingInDeck: existing?.totalCards ?? 0,
                    drawnCards: [],
                  },
                },
              };
            },
            false,
            'resetDeck'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to reset';
          set({ error: msg }, false, 'resetDeck/error');
        }
      },

      // --- Phases ---

      addPhase: async (req: AddPhaseRequest) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const phase = await api.toolbox.addPhase(toolbox.id, req);
          set(
            state => ({
              toolbox: state.toolbox
                ? { ...state.toolbox, phases: [...state.toolbox.phases, phase] }
                : null,
            }),
            false,
            'addPhase'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to add phase';
          set({ error: msg }, false, 'addPhase/error');
        }
      },

      removePhase: async (phaseId: string) => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          await api.toolbox.removePhase(toolbox.id, phaseId);
          set(
            state => ({
              toolbox: state.toolbox
                ? { ...state.toolbox, phases: state.toolbox.phases.filter(p => p.id !== phaseId) }
                : null,
            }),
            false,
            'removePhase'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to remove phase';
          set({ error: msg }, false, 'removePhase/error');
        }
      },

      advancePhase: async () => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const nextPhase = await api.toolbox.advancePhase(toolbox.id);
          set(
            state => ({
              toolbox: state.toolbox ? { ...state.toolbox, currentPhaseId: nextPhase.id } : null,
              currentPhase: nextPhase,
            }),
            false,
            'advancePhase'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to advance phase';
          set({ error: msg }, false, 'advancePhase/error');
        }
      },

      // --- Mode ---

      updateMode: async (mode: 'Freeform' | 'Phased') => {
        const { toolbox } = get();
        if (!toolbox) return;
        try {
          const { api } = await import('@/lib/api');
          const updated = await api.toolbox.updateMode(toolbox.id, mode);
          const currentPhase = updated.currentPhaseId
            ? (updated.phases.find(p => p.id === updated.currentPhaseId) ?? null)
            : null;
          set({ toolbox: updated, currentPhase }, false, 'updateMode');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to update mode';
          set({ error: msg }, false, 'updateMode/error');
        }
      },

      // --- SSE Event Handler ---

      handleToolboxEvent: (event: { type: string; data: unknown }) => {
        // Handle SSE events from other participants
        switch (event.type) {
          case 'CardDrawn':
          case 'DeckShuffled':
          case 'DeckReset':
          case 'PhaseAdvanced':
          case 'SharedContextUpdated':
          case 'ToolAdded':
          case 'ToolRemoved':
            // Reload full toolbox state on any event
            // (can be optimized per-event later)
            get().toolbox && get().loadToolbox(get().toolbox!.id);
            break;
        }
      },

      // --- Reset ---

      reset: () => {
        set({ ...initialState, expandedTools: new Set<string>() }, false, 'reset');
      },
    }),
    { name: 'ToolboxStore' }
  )
);
