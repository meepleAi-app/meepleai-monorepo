/**
 * useKeyboardShortcuts Hook Tests
 *
 * Tests for global keyboard shortcuts management.
 * @see useKeyboardShortcuts.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { fireEvent } from '@testing-library/dom';

import {
  formatShortcut,
  getDefaultShortcuts,
  useKeyboardShortcuts,
  useMessageInputShortcuts,
  type KeyboardShortcut,
} from '../useKeyboardShortcuts';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
}));

describe('formatShortcut', () => {
  it('should format shortcut with ctrl/meta modifier', () => {
    const shortcut: KeyboardShortcut = {
      key: 'n',
      ctrl: true,
      description: 'New item',
      action: vi.fn(),
    };

    const formatted = formatShortcut(shortcut);
    // Should contain modifier key (Ctrl or Cmd based on platform)
    expect(formatted).toMatch(/N$/);
  });

  it('should format shortcut with shift modifier', () => {
    const shortcut: KeyboardShortcut = {
      key: 'a',
      shift: true,
      description: 'Select all',
      action: vi.fn(),
    };

    const formatted = formatShortcut(shortcut);
    expect(formatted).toContain('A');
  });

  it('should format shortcut with multiple modifiers', () => {
    const shortcut: KeyboardShortcut = {
      key: 's',
      ctrl: true,
      shift: true,
      description: 'Save as',
      action: vi.fn(),
    };

    const formatted = formatShortcut(shortcut);
    expect(formatted).toContain('S');
  });

  it('should format special keys correctly', () => {
    const escapeShortcut: KeyboardShortcut = {
      key: 'Escape',
      description: 'Close',
      action: vi.fn(),
    };

    const enterShortcut: KeyboardShortcut = {
      key: 'Enter',
      description: 'Submit',
      action: vi.fn(),
    };

    const slashShortcut: KeyboardShortcut = {
      key: '/',
      ctrl: true,
      description: 'Search',
      action: vi.fn(),
    };

    expect(formatShortcut(escapeShortcut)).toContain('Esc');
    expect(formatShortcut(enterShortcut)).toContain('↵');
    expect(formatShortcut(slashShortcut)).toContain('/');
  });
});

describe('getDefaultShortcuts', () => {
  it('should return array of default shortcuts', () => {
    const callbacks = {
      onNewChat: vi.fn(),
      onUploadPdf: vi.fn(),
      onFocusSearch: vi.fn(),
      onOpenHelp: vi.fn(),
      onCloseModal: vi.fn(),
    };

    const shortcuts = getDefaultShortcuts(callbacks);

    expect(Array.isArray(shortcuts)).toBe(true);
    expect(shortcuts.length).toBeGreaterThan(0);
  });

  it('should include navigation shortcuts', () => {
    const callbacks = {
      onNewChat: vi.fn(),
      onUploadPdf: vi.fn(),
      onFocusSearch: vi.fn(),
      onOpenHelp: vi.fn(),
      onCloseModal: vi.fn(),
    };

    const shortcuts = getDefaultShortcuts(callbacks);

    const navigationShortcuts = shortcuts.filter(s => s.category === 'navigation');
    expect(navigationShortcuts.length).toBeGreaterThan(0);
  });

  it('should include system shortcuts', () => {
    const callbacks = {
      onNewChat: vi.fn(),
      onUploadPdf: vi.fn(),
      onFocusSearch: vi.fn(),
      onOpenHelp: vi.fn(),
      onCloseModal: vi.fn(),
    };

    const shortcuts = getDefaultShortcuts(callbacks);

    const systemShortcuts = shortcuts.filter(s => s.category === 'system');
    expect(systemShortcuts.length).toBeGreaterThan(0);
  });

  it('should link callbacks to shortcuts', () => {
    const onNewChat = vi.fn();
    const shortcuts = getDefaultShortcuts({
      onNewChat,
      onUploadPdf: vi.fn(),
      onFocusSearch: vi.fn(),
      onOpenHelp: vi.fn(),
      onCloseModal: vi.fn(),
    });

    const newChatShortcut = shortcuts.find(s => s.key === 'n');
    expect(newChatShortcut).toBeDefined();

    newChatShortcut?.action();
    expect(onNewChat).toHaveBeenCalled();
  });
});

describe('useKeyboardShortcuts', () => {
  let addEventListenerSpy: ReturnType<typeof vi.spyOn>;
  let removeEventListenerSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    addEventListenerSpy = vi.spyOn(document, 'addEventListener');
    removeEventListenerSpy = vi.spyOn(document, 'removeEventListener');
  });

  afterEach(() => {
    addEventListenerSpy.mockRestore();
    removeEventListenerSpy.mockRestore();
  });

  it('should add keydown event listener when enabled', () => {
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', description: 'Test', action: vi.fn() },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    expect(addEventListenerSpy).toHaveBeenCalledWith('keydown', expect.any(Function));
  });

  it('should not add event listener when disabled', () => {
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', description: 'Test', action: vi.fn() },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, false));

    expect(addEventListenerSpy).not.toHaveBeenCalledWith('keydown', expect.any(Function));
  });

  it('should remove event listener on unmount', () => {
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', description: 'Test', action: vi.fn() },
    ];

    const { unmount } = renderHook(() => useKeyboardShortcuts(shortcuts, true));

    unmount();

    expect(removeEventListenerSpy).toHaveBeenCalledWith('keydown', expect.any(Function));
  });

  it('should execute shortcut action when key pressed', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', ctrl: true, description: 'Test', action },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    // Simulate Ctrl+A
    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(action).toHaveBeenCalled();
  });

  it('should not execute shortcut when modifiers do not match', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', ctrl: true, description: 'Test', action },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    // Simulate just 'A' without Ctrl
    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: false,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(action).not.toHaveBeenCalled();
  });

  it('should skip disabled shortcuts', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', ctrl: true, description: 'Test', action, enabled: false },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(action).not.toHaveBeenCalled();
  });

  it('should prevent default when preventDefault is true', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', ctrl: true, description: 'Test', action, preventDefault: true },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: true,
      bubbles: true,
      cancelable: true,
    });
    const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

    act(() => {
      document.dispatchEvent(event);
    });

    expect(preventDefaultSpy).toHaveBeenCalled();
  });

  it('should handle meta key (Mac Cmd)', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'n', meta: true, description: 'New', action },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    // Simulate Cmd+N (metaKey)
    const event = new KeyboardEvent('keydown', {
      key: 'n',
      metaKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(action).toHaveBeenCalled();
  });

  it('should allow Escape in input fields', () => {
    const action = vi.fn();
    const shortcuts: KeyboardShortcut[] = [
      { key: 'Escape', description: 'Close', action },
    ];

    // Create an input element
    const input = document.createElement('input');
    document.body.appendChild(input);
    input.focus();

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    // Simulate Escape while in input
    const event = new KeyboardEvent('keydown', {
      key: 'Escape',
      bubbles: true,
    });
    Object.defineProperty(event, 'target', { value: input });

    act(() => {
      document.dispatchEvent(event);
    });

    expect(action).toHaveBeenCalled();

    // Cleanup
    document.body.removeChild(input);
  });

  it('should handle errors in action gracefully', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    const action = vi.fn().mockImplementation(() => {
      throw new Error('Action failed');
    });
    const shortcuts: KeyboardShortcut[] = [
      { key: 'a', ctrl: true, description: 'Test', action },
    ];

    renderHook(() => useKeyboardShortcuts(shortcuts, true));

    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: true,
      bubbles: true,
    });

    // Should not throw
    act(() => {
      fireEvent(document, event);
    });

    expect(action).toHaveBeenCalled();
    consoleSpy.mockRestore();
  });

  it('should update shortcuts when they change', () => {
    const action1 = vi.fn();
    const action2 = vi.fn();

    const { rerender } = renderHook(
      ({ shortcuts }) => useKeyboardShortcuts(shortcuts, true),
      {
        initialProps: {
          shortcuts: [{ key: 'a', ctrl: true, description: 'Test 1', action: action1 }],
        },
      }
    );

    // Update shortcuts
    rerender({
      shortcuts: [{ key: 'a', ctrl: true, description: 'Test 2', action: action2 }],
    });

    const event = new KeyboardEvent('keydown', {
      key: 'a',
      ctrlKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(action1).not.toHaveBeenCalled();
    expect(action2).toHaveBeenCalled();
  });
});

describe('useMessageInputShortcuts', () => {
  it('should trigger send on Ctrl+Enter', () => {
    const onSend = vi.fn();

    renderHook(() => useMessageInputShortcuts(onSend, true));

    const event = new KeyboardEvent('keydown', {
      key: 'Enter',
      ctrlKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(onSend).toHaveBeenCalled();
  });

  it('should trigger send on Cmd+Enter (Mac)', () => {
    const onSend = vi.fn();

    renderHook(() => useMessageInputShortcuts(onSend, true));

    const event = new KeyboardEvent('keydown', {
      key: 'Enter',
      metaKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(onSend).toHaveBeenCalled();
  });

  it('should not trigger send when disabled', () => {
    const onSend = vi.fn();

    renderHook(() => useMessageInputShortcuts(onSend, false));

    const event = new KeyboardEvent('keydown', {
      key: 'Enter',
      ctrlKey: true,
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(onSend).not.toHaveBeenCalled();
  });

  it('should not trigger send on plain Enter', () => {
    const onSend = vi.fn();

    renderHook(() => useMessageInputShortcuts(onSend, true));

    const event = new KeyboardEvent('keydown', {
      key: 'Enter',
      bubbles: true,
    });

    act(() => {
      fireEvent(document, event);
    });

    expect(onSend).not.toHaveBeenCalled();
  });
});
