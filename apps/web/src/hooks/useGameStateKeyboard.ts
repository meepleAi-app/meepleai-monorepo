/**
 * useGameStateKeyboard Hook
 * Issue #2406: Game State Editor UI
 *
 * Keyboard shortcuts for game state editor.
 */

'use client';

import { useEffect } from 'react';

import { useGameStateStore } from '@/lib/stores/game-state-store';

interface UseGameStateKeyboardOptions {
  enabled?: boolean;
  onSave?: () => void;
}

/**
 * Keyboard shortcuts:
 * - Ctrl/Cmd + Z: Undo
 * - Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y: Redo
 * - Ctrl/Cmd + S: Save
 */
export function useGameStateKeyboard({ enabled = true, onSave }: UseGameStateKeyboardOptions = {}) {
  const { undo, redo, canUndo, canRedo } = useGameStateStore();

  useEffect(() => {
    if (!enabled) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      const modifier = isMac ? event.metaKey : event.ctrlKey;

      // Ignore if typing in input field
      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
        // Only allow save shortcut in inputs
        if (modifier && event.key.toLowerCase() === 's') {
          event.preventDefault();
          onSave?.();
        }
        return;
      }

      // Undo: Ctrl/Cmd + Z
      if (modifier && event.key.toLowerCase() === 'z' && !event.shiftKey) {
        if (canUndo()) {
          event.preventDefault();
          undo();
        }
        return;
      }

      // Redo: Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y
      if (
        (modifier && event.shiftKey && event.key.toLowerCase() === 'z') ||
        (modifier && event.key.toLowerCase() === 'y')
      ) {
        if (canRedo()) {
          event.preventDefault();
          redo();
        }
        return;
      }

      // Save: Ctrl/Cmd + S
      if (modifier && event.key.toLowerCase() === 's') {
        event.preventDefault();
        onSave?.();
        return;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [enabled, undo, redo, canUndo, canRedo, onSave]);
}
