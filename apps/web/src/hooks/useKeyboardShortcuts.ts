/**
 * useKeyboardShortcuts Hook
 * Global keyboard shortcuts management for power users (Issue #1100)
 *
 * Provides centralized keyboard shortcut handling with:
 * - Platform detection (Mac vs Windows/Linux)
 * - Conflict prevention
 * - Accessibility support
 * - Visual indicators
 */

import { useEffect, useCallback, useRef } from 'react';

import { useRouter } from 'next/navigation'; // App Router

import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

/**
 * Keyboard shortcut definition
 */
export interface KeyboardShortcut {
  key: string;
  ctrl?: boolean;
  shift?: boolean;
  alt?: boolean;
  meta?: boolean;
  description: string;
  action: () => void;
  preventDefault?: boolean;
  enabled?: boolean;
  category?: 'navigation' | 'actions' | 'editor' | 'system';
}

/**
 * Platform-specific modifier keys
 */
export const isMac =
  typeof window !== 'undefined' && navigator.platform.toUpperCase().indexOf('MAC') >= 0;
export const modKey = isMac ? '⌘' : 'Ctrl';
export const altKey = isMac ? '⌥' : 'Alt';
export const shiftKey = '⇧';

/**
 * Format shortcut for display
 */
export function formatShortcut(shortcut: KeyboardShortcut): string {
  const parts: string[] = [];

  if (shortcut.meta || shortcut.ctrl) {
    parts.push(modKey);
  }
  if (shortcut.shift) {
    parts.push(shiftKey);
  }
  if (shortcut.alt) {
    parts.push(altKey);
  }

  // Special key names
  const keyName =
    shortcut.key === '/'
      ? '/'
      : shortcut.key === 'Enter'
        ? '↵'
        : shortcut.key === 'Escape'
          ? 'Esc'
          : shortcut.key.toUpperCase();

  parts.push(keyName);

  return parts.join(' ');
}

/**
 * Check if keyboard event matches shortcut
 */
function matchesShortcut(event: KeyboardEvent, shortcut: KeyboardShortcut): boolean {
  // Safety checks for undefined/null keys
  if (!event.key || !shortcut.key) return false;

  const key = event.key.toLowerCase();
  const shortcutKey = shortcut.key.toLowerCase();

  // Key match (case-insensitive)
  if (key !== shortcutKey) return false;

  // Modifier keys match
  const shiftMatch = shortcut.shift ? event.shiftKey : !event.shiftKey;
  const altMatch = shortcut.alt ? event.altKey : !event.altKey;

  // Platform-specific modifier handling
  // On Mac: Cmd (meta) is primary, Ctrl is secondary → treat interchangeably
  // On Windows/Linux: Ctrl is primary, Meta (Windows key) is rarely used → treat interchangeably
  const wantsMod = shortcut.ctrl || shortcut.meta;
  const hasMod = event.ctrlKey || event.metaKey;

  const modifierMatch = wantsMod ? hasMod : !event.ctrlKey && !event.metaKey;

  return modifierMatch && shiftMatch && altMatch;
}

/**
 * Default global keyboard shortcuts
 */
export function getDefaultShortcuts(callbacks: {
  onNewChat: () => void;
  onUploadPdf: () => void;
  onFocusSearch: () => void;
  onOpenHelp: () => void;
  onCloseModal: () => void;
}): KeyboardShortcut[] {
  return [
    // Navigation shortcuts
    // NOTE: ctrl + meta = true enables cross-platform support (Cmd on Mac, Ctrl on Windows/Linux)
    {
      key: 'n',
      ctrl: true,
      meta: true,
      description: 'New chat',
      action: callbacks.onNewChat,
      preventDefault: true,
      category: 'navigation',
    },
    {
      key: 'u',
      ctrl: true,
      meta: true,
      description: 'Upload PDF',
      action: callbacks.onUploadPdf,
      preventDefault: true,
      category: 'navigation',
    },
    {
      key: '/',
      ctrl: true,
      meta: true,
      description: 'Focus search',
      action: callbacks.onFocusSearch,
      preventDefault: true,
      category: 'navigation',
    },

    // System shortcuts
    {
      key: '?',
      shift: true, // Shift+? = ?
      description: 'Show keyboard shortcuts help',
      action: callbacks.onOpenHelp,
      preventDefault: true,
      category: 'system',
    },
    {
      key: 'Escape',
      description: 'Close modal or dialog',
      action: callbacks.onCloseModal,
      preventDefault: false, // Let modals handle their own escape
      category: 'system',
    },
  ];
}

/**
 * Hook for global keyboard shortcuts
 */
export function useKeyboardShortcuts(shortcuts: KeyboardShortcut[], enabled = true) {
  const shortcutsRef = useRef<KeyboardShortcut[]>(shortcuts);

  // Update shortcuts ref when they change
  useEffect(() => {
    shortcutsRef.current = shortcuts;
  }, [shortcuts]);

  useEffect(() => {
    if (!enabled) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      // Don't trigger shortcuts when typing in input fields
      const target = event.target as HTMLElement;
      const isInputField =
        target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable;

      for (const shortcut of shortcutsRef.current) {
        // Skip disabled shortcuts
        if (shortcut.enabled === false) continue;

        // Check if shortcut matches
        if (matchesShortcut(event, shortcut)) {
          // Special handling for input fields
          // Allow Escape and search shortcuts (Cmd+/) even in input fields
          const allowInInputField =
            shortcut.key === 'Escape' || shortcut.key === '/' || shortcut.key === '?';

          if (isInputField && !allowInInputField) {
            continue;
          }

          // Prevent default browser behavior if requested
          if (shortcut.preventDefault) {
            event.preventDefault();
            event.stopPropagation();
          }

          // Execute shortcut action
          try {
            shortcut.action();
          } catch (error) {
            logger.error(
              'Error executing keyboard shortcut',
              error instanceof Error ? error : new Error(String(error)),
              createErrorContext('useKeyboardShortcuts', 'executeShortcut', {
                key: shortcut.key,
                description: shortcut.description,
                category: shortcut.category,
              })
            );
          }

          break; // Only execute first matching shortcut
        }
      }
    };

    // Add event listener
    document.addEventListener('keydown', handleKeyDown);

    // Cleanup
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [enabled]);
}

/**
 * Hook for global app-level shortcuts with router integration
 */
export function useGlobalKeyboardShortcuts(
  options: {
    onOpenCommandPalette?: () => void;
    onOpenShortcutsHelp?: () => void;
    onCloseModal?: () => void;
    enabled?: boolean;
  } = {}
) {
  const router = useRouter();
  const { onOpenCommandPalette, onOpenShortcutsHelp, onCloseModal, enabled = true } = options;

  const handleNewChat = useCallback(() => {
    router.push('/chat');
  }, [router]);

  const handleUploadPdf = useCallback(() => {
    router.push('/upload');
  }, [router]);

  const handleFocusSearch = useCallback(() => {
    if (onOpenCommandPalette) {
      onOpenCommandPalette();
    }
  }, [onOpenCommandPalette]);

  const handleOpenHelp = useCallback(() => {
    if (onOpenShortcutsHelp) {
      onOpenShortcutsHelp();
    }
  }, [onOpenShortcutsHelp]);

  const handleCloseModal = useCallback(() => {
    if (onCloseModal) {
      onCloseModal();
    }
  }, [onCloseModal]);

  const shortcuts = getDefaultShortcuts({
    onNewChat: handleNewChat,
    onUploadPdf: handleUploadPdf,
    onFocusSearch: handleFocusSearch,
    onOpenHelp: handleOpenHelp,
    onCloseModal: handleCloseModal,
  });

  useKeyboardShortcuts(shortcuts, enabled);

  return {
    shortcuts,
    isMac,
    modKey,
    formatShortcut,
  };
}

/**
 * Hook for message input shortcuts (Cmd+Enter to send)
 */
export function useMessageInputShortcuts(onSend: () => void, enabled = true) {
  const shortcuts: KeyboardShortcut[] = [
    {
      key: 'Enter',
      ctrl: true,
      meta: true,
      description: 'Send message',
      action: onSend,
      preventDefault: true,
      category: 'editor',
    },
  ];

  useKeyboardShortcuts(shortcuts, enabled);
}
