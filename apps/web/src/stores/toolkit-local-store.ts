'use client';

/**
 * Toolkit Local Store
 *
 * Per-game local Zustand store for the Default Game Toolkit.
 * Persists player state, scores, notes, diary, and custom dice presets
 * to localStorage using the standard MeepleAI middleware stack:
 * devtools + persist + immer.
 *
 * Factory pattern: `createToolkitLocalStore(gameId)` produces a unique
 * store keyed to `meepleai-toolkit-${gameId}`.
 */

import { create } from 'zustand';
import { createJSONStorage, devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type {
  DicePreset,
  DiaryEvent,
  LocalNote,
  LocalPlayer,
} from '@/components/toolkit-drawer/types';

// ============================================================================
// Types
// ============================================================================

export interface ToolkitLocalState {
  // Player management
  players: LocalPlayer[];
  currentTurnIndex: number;
  currentRound: number;

  // Scores
  scores: Record<string, Record<string, number>>; // playerId → category → value
  scoreCategories: string[];

  // Notes
  notes: LocalNote[];

  // Diary
  diary: DiaryEvent[];

  // Custom dice presets
  customDicePresets: DicePreset[];
}

export interface ToolkitLocalActions {
  // Player actions
  addPlayer: (player: LocalPlayer) => void;
  removePlayer: (playerId: string) => void;
  reorderPlayers: (playerIds: string[]) => void;
  setTurn: (index: number) => void;
  advanceTurn: () => void;
  advanceRound: () => void;

  // Score actions
  setScore: (playerId: string, category: string, value: number) => void;
  addScoreCategory: (category: string) => void;
  removeScoreCategory: (category: string) => void;
  resetScores: () => void;

  // Note actions
  addNote: (note: LocalNote) => void;
  updateNote: (noteId: string, content: string) => void;
  deleteNote: (noteId: string) => void;
  togglePin: (noteId: string) => void;

  // Diary actions
  logEvent: (event: DiaryEvent) => void;
  clearDiary: () => void;

  // Dice preset actions
  addCustomPreset: (preset: DicePreset) => void;
  removeCustomPreset: (presetName: string) => void;

  // Reset
  resetAll: () => void;
}

export type ToolkitLocalStore = ToolkitLocalState & ToolkitLocalActions;

// ============================================================================
// Initial State
// ============================================================================

const INITIAL_STATE: ToolkitLocalState = {
  players: [],
  currentTurnIndex: 0,
  currentRound: 1,
  scores: {},
  scoreCategories: [],
  notes: [],
  diary: [],
  customDicePresets: [],
};

// ============================================================================
// Store Factory
// ============================================================================

/**
 * Creates a per-game toolkit store persisted under `meepleai-toolkit-${gameId}`.
 *
 * Each store instance is independent — opening toolkits for different games
 * will not interfere with each other.
 */
export function createToolkitLocalStore(gameId: string) {
  const storageKey = `meepleai-toolkit-${gameId}`;

  return create<ToolkitLocalStore>()(
    devtools(
      persist(
        immer(set => ({
          // ─── Initial State ─────────────────────────────
          ...INITIAL_STATE,

          // ─── Player Actions ────────────────────────────

          addPlayer: player => {
            set(state => {
              state.players.push(player);
              state.scores[player.id] = {};
            });
          },

          removePlayer: playerId => {
            set(state => {
              state.players = state.players.filter(p => p.id !== playerId);
              delete state.scores[playerId];
              // Adjust turn index if needed
              if (state.currentTurnIndex >= state.players.length) {
                state.currentTurnIndex = Math.max(0, state.players.length - 1);
              }
            });
          },

          reorderPlayers: playerIds => {
            set(state => {
              const indexed = new Map(state.players.map(p => [p.id, p]));
              state.players = playerIds
                .map(id => indexed.get(id))
                .filter((p): p is LocalPlayer => p !== undefined);
            });
          },

          setTurn: index => {
            set(state => {
              state.currentTurnIndex = index;
            });
          },

          advanceTurn: () => {
            set(state => {
              if (state.players.length === 0) return;
              state.currentTurnIndex = (state.currentTurnIndex + 1) % state.players.length;
            });
          },

          advanceRound: () => {
            set(state => {
              state.currentRound += 1;
              state.currentTurnIndex = 0;
            });
          },

          // ─── Score Actions ─────────────────────────────

          setScore: (playerId, category, value) => {
            set(state => {
              if (!state.scores[playerId]) {
                state.scores[playerId] = {};
              }
              state.scores[playerId][category] = value;
            });
          },

          addScoreCategory: category => {
            set(state => {
              if (!state.scoreCategories.includes(category)) {
                state.scoreCategories.push(category);
              }
            });
          },

          removeScoreCategory: category => {
            set(state => {
              state.scoreCategories = state.scoreCategories.filter(c => c !== category);
              // Remove the category from all players' scores
              for (const playerId of Object.keys(state.scores)) {
                delete state.scores[playerId][category];
              }
            });
          },

          resetScores: () => {
            set(state => {
              for (const playerId of Object.keys(state.scores)) {
                state.scores[playerId] = {};
              }
            });
          },

          // ─── Note Actions ──────────────────────────────

          addNote: note => {
            set(state => {
              state.notes.push(note);
            });
          },

          updateNote: (noteId, content) => {
            set(state => {
              const note = state.notes.find(n => n.id === noteId);
              if (note) {
                note.content = content;
                note.updatedAt = Date.now();
              }
            });
          },

          deleteNote: noteId => {
            set(state => {
              state.notes = state.notes.filter(n => n.id !== noteId);
            });
          },

          togglePin: noteId => {
            set(state => {
              const note = state.notes.find(n => n.id === noteId);
              if (note) {
                note.pinned = !note.pinned;
              }
            });
          },

          // ─── Diary Actions ─────────────────────────────

          logEvent: event => {
            set(state => {
              state.diary.push(event);
            });
          },

          clearDiary: () => {
            set(state => {
              state.diary = [];
            });
          },

          // ─── Dice Preset Actions ───────────────────────

          addCustomPreset: preset => {
            set(state => {
              // Prevent duplicates by name
              if (!state.customDicePresets.some(p => p.name === preset.name)) {
                state.customDicePresets.push(preset);
              }
            });
          },

          removeCustomPreset: presetName => {
            set(state => {
              state.customDicePresets = state.customDicePresets.filter(p => p.name !== presetName);
            });
          },

          // ─── Reset ─────────────────────────────────────

          resetAll: () => {
            set(() => ({ ...INITIAL_STATE }));
          },
        })),
        {
          name: storageKey,
          storage: createJSONStorage(() =>
            typeof window !== 'undefined'
              ? localStorage
              : {
                  getItem: () => null,
                  setItem: () => {},
                  removeItem: () => {},
                }
          ),
          // Only persist state values, not action functions
          partialize: state => ({
            players: state.players,
            currentTurnIndex: state.currentTurnIndex,
            currentRound: state.currentRound,
            scores: state.scores,
            scoreCategories: state.scoreCategories,
            notes: state.notes,
            diary: state.diary,
            customDicePresets: state.customDicePresets,
          }),
        }
      ),
      {
        name: `ToolkitLocalStore(${gameId})`,
      }
    )
  );
}
