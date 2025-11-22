/**
 * Tests for useKeyboardShortcuts Hook
 * Issue #1100: Keyboard shortcuts system
 */

import { renderHook, act } from '@testing-library/react';
import {
  useKeyboardShortcuts,
  useMessageInputShortcuts,
  formatShortcut,
  type KeyboardShortcut,
} from '../useKeyboardShortcuts';

// Mock navigator.platform for platform detection
const originalNavigator = global.navigator;

describe('useKeyboardShortcuts', () => {
  beforeEach(() => {
    // Clear all event listeners before each test
    document.removeEventListener('keydown', jest.fn());
  });

  afterEach(() => {
    // Restore original navigator
    Object.defineProperty(global, 'navigator', {
      value: originalNavigator,
      writable: true,
    });
  });

  describe('formatShortcut', () => {
    it('formats simple key shortcut', () => {
      const shortcut: KeyboardShortcut = {
        key: 'n',
        description: 'Test',
        action: jest.fn(),
      };
      const formatted = formatShortcut(shortcut);
      expect(formatted).toMatch(/N/);
    });

    it('formats Cmd+Key on Mac', () => {
      // NOTE: isMac is evaluated at module load time, so we can't mock it after import
      // This test verifies the format based on the actual platform
      const shortcut: KeyboardShortcut = {
        key: 'n',
        ctrl: true,
        meta: true,
        description: 'Test',
        action: jest.fn(),
      };
      const formatted = formatShortcut(shortcut);

      // Should contain either ⌘ (Mac) or Ctrl (Windows/Linux) depending on platform
      const isMac = navigator.platform.toUpperCase().indexOf('MAC') >= 0;
      if (isMac) {
        expect(formatted).toContain('⌘');
      } else {
        expect(formatted).toContain('Ctrl');
      }
      expect(formatted).toContain('N');
    });

    it('formats Ctrl+Key on Windows', () => {
      // Mock Windows platform
      Object.defineProperty(global.navigator, 'platform', {
        value: 'Win32',
        writable: true,
      });

      const shortcut: KeyboardShortcut = {
        key: 'n',
        ctrl: true,
        description: 'Test',
        action: jest.fn(),
      };
      const formatted = formatShortcut(shortcut);
      expect(formatted).toContain('Ctrl');
      expect(formatted).toContain('N');
    });

    it('formats Shift+Key', () => {
      const shortcut: KeyboardShortcut = {
        key: '?',
        shift: true,
        description: 'Test',
        action: jest.fn(),
      };
      const formatted = formatShortcut(shortcut);
      expect(formatted).toContain('⇧');
      expect(formatted).toContain('?');
    });

    it('formats special keys', () => {
      const escapeShortcut: KeyboardShortcut = {
        key: 'Escape',
        description: 'Test',
        action: jest.fn(),
      };
      expect(formatShortcut(escapeShortcut)).toContain('Esc');

      const enterShortcut: KeyboardShortcut = {
        key: 'Enter',
        description: 'Test',
        action: jest.fn(),
      };
      expect(formatShortcut(enterShortcut)).toContain('↵');
    });
  });

  describe('useKeyboardShortcuts hook', () => {
    it('triggers action when matching shortcut is pressed', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          meta: true,
          description: 'New chat',
          action,
          preventDefault: true,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      // Simulate Ctrl+N keypress
      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
      });
      act(() => {
        document.dispatchEvent(event);
      });

      expect(action).toHaveBeenCalledTimes(1);
    });

    it('does not trigger action when shortcut is disabled', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          description: 'New chat',
          action,
          enabled: false,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      // Simulate Ctrl+N keypress
      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
      });
      act(() => {
        document.dispatchEvent(event);
      });

      expect(action).not.toHaveBeenCalled();
    });

    it('does not trigger when hook is disabled', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          description: 'New chat',
          action,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, false));

      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
      });
      act(() => {
        document.dispatchEvent(event);
      });

      expect(action).not.toHaveBeenCalled();
    });

    it('does not trigger shortcuts when typing in input field', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          description: 'New chat',
          action,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      // Create an input element and focus it
      const input = document.createElement('input');
      document.body.appendChild(input);
      input.focus();

      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
      });
      Object.defineProperty(event, 'target', { value: input, writable: false });

      act(() => {
        input.dispatchEvent(event);
      });

      expect(action).not.toHaveBeenCalled();

      document.body.removeChild(input);
    });

    it('allows Escape shortcut even in input fields', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'Escape',
          description: 'Close modal',
          action,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      const input = document.createElement('input');
      document.body.appendChild(input);
      input.focus();

      const event = new KeyboardEvent('keydown', {
        key: 'Escape',
        bubbles: true,
      });
      Object.defineProperty(event, 'target', { value: input, writable: false });

      act(() => {
        input.dispatchEvent(event);
      });

      expect(action).toHaveBeenCalledTimes(1);

      document.body.removeChild(input);
    });

    it('prevents default when preventDefault is true', () => {
      const action = jest.fn();
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          description: 'New chat',
          action,
          preventDefault: true,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
        cancelable: true,
      });
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');
      const stopPropagationSpy = jest.spyOn(event, 'stopPropagation');

      act(() => {
        document.dispatchEvent(event);
      });

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(stopPropagationSpy).toHaveBeenCalled();
      expect(action).toHaveBeenCalledTimes(1);
    });

    it('handles errors in action execution gracefully', () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();
      const errorAction = jest.fn(() => {
        throw new Error('Test error');
      });
      const shortcuts: KeyboardShortcut[] = [
        {
          key: 'n',
          ctrl: true,
          description: 'Error action',
          action: errorAction,
        },
      ];

      renderHook(() => useKeyboardShortcuts(shortcuts, true));

      const event = new KeyboardEvent('keydown', {
        key: 'n',
        ctrlKey: true,
        bubbles: true,
      });

      expect(() => {
        act(() => {
          document.dispatchEvent(event);
        });
      }).not.toThrow();

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Error executing keyboard shortcut:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });
  });

  describe('useMessageInputShortcuts', () => {
    it('triggers send action on Cmd+Enter', () => {
      const onSend = jest.fn();

      renderHook(() => useMessageInputShortcuts(onSend, true));

      const event = new KeyboardEvent('keydown', {
        key: 'Enter',
        ctrlKey: true,
        bubbles: true,
      });

      act(() => {
        document.dispatchEvent(event);
      });

      expect(onSend).toHaveBeenCalledTimes(1);
    });

    it('does not trigger when disabled', () => {
      const onSend = jest.fn();

      renderHook(() => useMessageInputShortcuts(onSend, false));

      const event = new KeyboardEvent('keydown', {
        key: 'Enter',
        ctrlKey: true,
        bubbles: true,
      });

      act(() => {
        document.dispatchEvent(event);
      });

      expect(onSend).not.toHaveBeenCalled();
    });
  });
});
