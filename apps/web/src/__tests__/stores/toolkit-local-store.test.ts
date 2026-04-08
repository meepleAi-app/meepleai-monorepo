import { describe, it, expect, beforeEach } from 'vitest';

import { createToolkitLocalStore } from '@/stores/toolkit-local-store';

import type { DiaryEvent, LocalNote, LocalPlayer } from '@/components/toolkit-drawer/types';

describe('ToolkitLocalStore', () => {
  let store: ReturnType<typeof createToolkitLocalStore>;

  const makePlayer = (id: string, name: string, color = '#E67E22'): LocalPlayer => ({
    id,
    name,
    color,
  });

  beforeEach(() => {
    store = createToolkitLocalStore('test-game-id');
    store.getState().resetAll();
  });

  describe('Players', () => {
    it('adds a player and initializes empty scores', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      const state = store.getState();
      expect(state.players).toHaveLength(1);
      expect(state.players[0].name).toBe('Marco');
      expect(state.scores['p1']).toEqual({});
    });

    it('removes a player and cleans up their scores', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      store.getState().addPlayer(makePlayer('p2', 'Luca'));
      store.getState().setScore('p1', 'PV', 10);

      store.getState().removePlayer('p1');

      const state = store.getState();
      expect(state.players).toHaveLength(1);
      expect(state.players[0].id).toBe('p2');
      expect(state.scores['p1']).toBeUndefined();
    });

    it('advances turn cyclically', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      store.getState().addPlayer(makePlayer('p2', 'Luca'));

      expect(store.getState().currentTurnIndex).toBe(0);
      store.getState().advanceTurn();
      expect(store.getState().currentTurnIndex).toBe(1);
      store.getState().advanceTurn();
      expect(store.getState().currentTurnIndex).toBe(0);
    });

    it('advanceRound increments round and resets turn', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      store.getState().addPlayer(makePlayer('p2', 'Luca'));
      store.getState().advanceTurn();

      store.getState().advanceRound();
      expect(store.getState().currentRound).toBe(2);
      expect(store.getState().currentTurnIndex).toBe(0);
    });

    it('reorderPlayers applies new order', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      store.getState().addPlayer(makePlayer('p2', 'Luca'));
      store.getState().addPlayer(makePlayer('p3', 'Anna'));

      store.getState().reorderPlayers(['p3', 'p1', 'p2']);

      const names = store.getState().players.map(p => p.name);
      expect(names).toEqual(['Anna', 'Marco', 'Luca']);
    });
  });

  describe('Scores', () => {
    beforeEach(() => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
    });

    it('setScore creates nested category entry', () => {
      store.getState().addScoreCategory('PV');
      store.getState().setScore('p1', 'PV', 12);

      expect(store.getState().scores['p1']['PV']).toBe(12);
    });

    it('addScoreCategory is idempotent', () => {
      store.getState().addScoreCategory('PV');
      store.getState().addScoreCategory('PV');
      expect(store.getState().scoreCategories).toEqual(['PV']);
    });

    it('removeScoreCategory removes from categories and all players', () => {
      store.getState().addScoreCategory('PV');
      store.getState().setScore('p1', 'PV', 10);

      store.getState().removeScoreCategory('PV');

      expect(store.getState().scoreCategories).toEqual([]);
      expect(store.getState().scores['p1']['PV']).toBeUndefined();
    });

    it('resetScores empties all player score maps', () => {
      store.getState().addScoreCategory('PV');
      store.getState().setScore('p1', 'PV', 10);

      store.getState().resetScores();

      expect(store.getState().scores['p1']).toEqual({});
    });
  });

  describe('Notes', () => {
    const makeNote = (
      id: string,
      content: string,
      type: 'shared' | 'private' = 'shared'
    ): LocalNote => ({
      id,
      content,
      type,
      pinned: false,
      createdAt: Date.now(),
      updatedAt: Date.now(),
    });

    it('addNote appends to notes array', () => {
      store.getState().addNote(makeNote('n1', 'Hello'));
      expect(store.getState().notes).toHaveLength(1);
      expect(store.getState().notes[0].content).toBe('Hello');
    });

    it('updateNote changes content and updatedAt', () => {
      store.getState().addNote(makeNote('n1', 'Old'));
      const originalUpdatedAt = store.getState().notes[0].updatedAt;

      // Wait a tick to ensure timestamp differs
      store.getState().updateNote('n1', 'New');

      const updated = store.getState().notes[0];
      expect(updated.content).toBe('New');
      expect(updated.updatedAt).toBeGreaterThanOrEqual(originalUpdatedAt);
    });

    it('deleteNote removes by id', () => {
      store.getState().addNote(makeNote('n1', 'First'));
      store.getState().addNote(makeNote('n2', 'Second'));

      store.getState().deleteNote('n1');

      expect(store.getState().notes).toHaveLength(1);
      expect(store.getState().notes[0].id).toBe('n2');
    });

    it('togglePin flips pinned state', () => {
      store.getState().addNote(makeNote('n1', 'Test'));
      expect(store.getState().notes[0].pinned).toBe(false);

      store.getState().togglePin('n1');
      expect(store.getState().notes[0].pinned).toBe(true);

      store.getState().togglePin('n1');
      expect(store.getState().notes[0].pinned).toBe(false);
    });
  });

  describe('Diary', () => {
    const makeEvent = (id: string, type: DiaryEvent['type'] = 'manual_entry'): DiaryEvent => ({
      id,
      type,
      timestamp: Date.now(),
      payload: {},
    });

    it('logEvent appends to diary', () => {
      store.getState().logEvent(makeEvent('e1', 'dice_roll'));
      store.getState().logEvent(makeEvent('e2', 'score_change'));

      const diary = store.getState().diary;
      expect(diary).toHaveLength(2);
      expect(diary[0].type).toBe('dice_roll');
      expect(diary[1].type).toBe('score_change');
    });

    it('clearDiary empties the diary', () => {
      store.getState().logEvent(makeEvent('e1'));
      store.getState().clearDiary();
      expect(store.getState().diary).toEqual([]);
    });
  });

  describe('Custom Dice Presets', () => {
    it('addCustomPreset adds a new preset', () => {
      store.getState().addCustomPreset({
        name: 'Combat',
        formula: '2d6+1d8',
        source: 'custom',
      });

      expect(store.getState().customDicePresets).toHaveLength(1);
      expect(store.getState().customDicePresets[0].name).toBe('Combat');
    });

    it('addCustomPreset prevents duplicates by name', () => {
      store.getState().addCustomPreset({
        name: 'Combat',
        formula: '2d6',
        source: 'custom',
      });
      store.getState().addCustomPreset({
        name: 'Combat',
        formula: '3d6',
        source: 'custom',
      });

      expect(store.getState().customDicePresets).toHaveLength(1);
    });

    it('removeCustomPreset removes by name', () => {
      store.getState().addCustomPreset({ name: 'A', formula: '1d6', source: 'custom' });
      store.getState().addCustomPreset({ name: 'B', formula: '2d6', source: 'custom' });

      store.getState().removeCustomPreset('A');

      expect(store.getState().customDicePresets).toHaveLength(1);
      expect(store.getState().customDicePresets[0].name).toBe('B');
    });
  });

  describe('resetAll', () => {
    it('resets the entire state to initial values', () => {
      store.getState().addPlayer(makePlayer('p1', 'Marco'));
      store.getState().addScoreCategory('PV');
      store.getState().setScore('p1', 'PV', 10);
      store.getState().advanceRound();

      store.getState().resetAll();

      const state = store.getState();
      expect(state.players).toEqual([]);
      expect(state.scores).toEqual({});
      expect(state.scoreCategories).toEqual([]);
      expect(state.currentRound).toBe(1);
      expect(state.currentTurnIndex).toBe(0);
    });
  });
});
