/**
 * useGameStateKeyboard Hook Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Keyboard shortcuts for game state editor
 * - Ctrl/Cmd + Z: Undo
 * - Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y: Redo
 * - Ctrl/Cmd + S: Save
 * - Input field behavior
 * - Enabled/disabled state
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useGameStateKeyboard } from '../useGameStateKeyboard';

// Mock the game state store
vi.mock('@/lib/stores/game-state-store', () => ({
  useGameStateStore: vi.fn(),
}));

import { useGameStateStore } from '@/lib/stores/game-state-store';

const mockUseGameStateStore = vi.mocked(useGameStateStore);

describe('useGameStateKeyboard - Issue #3026', () => {
  const mockUndo = vi.fn();
  const mockRedo = vi.fn();
  const mockCanUndo = vi.fn();
  const mockCanRedo = vi.fn();
  const mockOnSave = vi.fn();

  // Store original navigator.platform
  const originalPlatform = Object.getOwnPropertyDescriptor(navigator, 'platform');

  beforeEach(() => {
    vi.clearAllMocks();
    mockCanUndo.mockReturnValue(true);
    mockCanRedo.mockReturnValue(true);
    mockUseGameStateStore.mockReturnValue({
      undo: mockUndo,
      redo: mockRedo,
      canUndo: mockCanUndo,
      canRedo: mockCanRedo,
    } as any);
  });

  afterEach(() => {
    // Restore navigator.platform
    if (originalPlatform) {
      Object.defineProperty(navigator, 'platform', originalPlatform);
    }
  });

  const setPlatform = (platform: string) => {
    Object.defineProperty(navigator, 'platform', {
      value: platform,
      writable: true,
      configurable: true,
    });
  };

  const createKeyboardEvent = (
    key: string,
    options: {
      ctrlKey?: boolean;
      metaKey?: boolean;
      shiftKey?: boolean;
      target?: EventTarget | null;
    } = {}
  ): KeyboardEvent => {
    const event = new KeyboardEvent('keydown', {
      key,
      ctrlKey: options.ctrlKey || false,
      metaKey: options.metaKey || false,
      shiftKey: options.shiftKey || false,
      bubbles: true,
      cancelable: true,
    });

    // Mock the target property if provided
    if (options.target) {
      Object.defineProperty(event, 'target', {
        value: options.target,
        writable: false,
      });
    }

    return event;
  };

  describe('Undo (Ctrl/Cmd + Z)', () => {
    it('should call undo on Ctrl+Z (Windows/Linux)', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      window.dispatchEvent(event);

      expect(mockUndo).toHaveBeenCalledTimes(1);
      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should call undo on Cmd+Z (Mac)', () => {
      setPlatform('MacIntel');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { metaKey: true });
      window.dispatchEvent(event);

      expect(mockUndo).toHaveBeenCalledTimes(1);
    });

    it('should not call undo if canUndo returns false', () => {
      setPlatform('Win32');
      mockCanUndo.mockReturnValue(false);

      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockUndo).not.toHaveBeenCalled();
    });

    it('should not call undo without modifier key', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z');
      window.dispatchEvent(event);

      expect(mockUndo).not.toHaveBeenCalled();
    });
  });

  describe('Redo (Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y)', () => {
    it('should call redo on Ctrl+Shift+Z (Windows/Linux)', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true, shiftKey: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      window.dispatchEvent(event);

      expect(mockRedo).toHaveBeenCalledTimes(1);
      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should call redo on Cmd+Shift+Z (Mac)', () => {
      setPlatform('MacIntel');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { metaKey: true, shiftKey: true });
      window.dispatchEvent(event);

      expect(mockRedo).toHaveBeenCalledTimes(1);
    });

    it('should call redo on Ctrl+Y (Windows/Linux)', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('y', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockRedo).toHaveBeenCalledTimes(1);
    });

    it('should call redo on Cmd+Y (Mac)', () => {
      setPlatform('MacIntel');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('y', { metaKey: true });
      window.dispatchEvent(event);

      expect(mockRedo).toHaveBeenCalledTimes(1);
    });

    it('should not call redo if canRedo returns false', () => {
      setPlatform('Win32');
      mockCanRedo.mockReturnValue(false);

      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true, shiftKey: true });
      window.dispatchEvent(event);

      expect(mockRedo).not.toHaveBeenCalled();
    });
  });

  describe('Save (Ctrl/Cmd + S)', () => {
    it('should call onSave on Ctrl+S (Windows/Linux)', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('s', { ctrlKey: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      window.dispatchEvent(event);

      expect(mockOnSave).toHaveBeenCalledTimes(1);
      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should call onSave on Cmd+S (Mac)', () => {
      setPlatform('MacIntel');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('s', { metaKey: true });
      window.dispatchEvent(event);

      expect(mockOnSave).toHaveBeenCalledTimes(1);
    });

    it('should not throw if onSave is not provided', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard());

      const event = createKeyboardEvent('s', { ctrlKey: true });

      expect(() => window.dispatchEvent(event)).not.toThrow();
    });
  });

  describe('Input Field Behavior', () => {
    it('should allow save shortcut in input fields', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const inputElement = document.createElement('input');
      const event = createKeyboardEvent('s', { ctrlKey: true, target: inputElement });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      window.dispatchEvent(event);

      expect(mockOnSave).toHaveBeenCalledTimes(1);
      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should allow save shortcut in textarea fields', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const textareaElement = document.createElement('textarea');
      const event = createKeyboardEvent('s', { ctrlKey: true, target: textareaElement });

      window.dispatchEvent(event);

      expect(mockOnSave).toHaveBeenCalledTimes(1);
    });

    it('should block undo in input fields', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const inputElement = document.createElement('input');
      const event = createKeyboardEvent('z', { ctrlKey: true, target: inputElement });

      window.dispatchEvent(event);

      expect(mockUndo).not.toHaveBeenCalled();
    });

    it('should block redo in textarea fields', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const textareaElement = document.createElement('textarea');
      const event = createKeyboardEvent('y', { ctrlKey: true, target: textareaElement });

      window.dispatchEvent(event);

      expect(mockRedo).not.toHaveBeenCalled();
    });
  });

  describe('Enabled/Disabled State', () => {
    it('should not respond to shortcuts when disabled', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ enabled: false, onSave: mockOnSave }));

      const undoEvent = createKeyboardEvent('z', { ctrlKey: true });
      window.dispatchEvent(undoEvent);

      const redoEvent = createKeyboardEvent('y', { ctrlKey: true });
      window.dispatchEvent(redoEvent);

      const saveEvent = createKeyboardEvent('s', { ctrlKey: true });
      window.dispatchEvent(saveEvent);

      expect(mockUndo).not.toHaveBeenCalled();
      expect(mockRedo).not.toHaveBeenCalled();
      expect(mockOnSave).not.toHaveBeenCalled();
    });

    it('should respond to shortcuts when explicitly enabled', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ enabled: true, onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockUndo).toHaveBeenCalledTimes(1);
    });

    it('should respond to shortcuts by default (enabled is true)', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('z', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockUndo).toHaveBeenCalledTimes(1);
    });
  });

  describe('Cleanup', () => {
    it('should remove event listener on unmount', () => {
      setPlatform('Win32');
      const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener');

      const { unmount } = renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      unmount();

      expect(removeEventListenerSpy).toHaveBeenCalledWith('keydown', expect.any(Function));
    });
  });

  describe('Case Insensitivity', () => {
    it('should handle uppercase Z for undo', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('Z', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockUndo).toHaveBeenCalledTimes(1);
    });

    it('should handle uppercase S for save', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('S', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockOnSave).toHaveBeenCalledTimes(1);
    });

    it('should handle uppercase Y for redo', () => {
      setPlatform('Win32');
      renderHook(() => useGameStateKeyboard({ onSave: mockOnSave }));

      const event = createKeyboardEvent('Y', { ctrlKey: true });
      window.dispatchEvent(event);

      expect(mockRedo).toHaveBeenCalledTimes(1);
    });
  });
});
