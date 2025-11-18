/**
 * KeyboardShortcutsHelp Component
 * Modal displaying all available keyboard shortcuts (Issue #1100)
 *
 * Features:
 * - Categorized shortcuts display
 * - Platform-specific key symbols (Mac vs Windows)
 * - Accessible dialog with ARIA attributes
 * - Keyboard navigation (Esc to close)
 */

import React from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';
import {
  Keyboard,
  MessageSquare,
  Upload,
  Search,
  HelpCircle,
  X as CloseIcon,
} from 'lucide-react';
import {
  formatShortcut,
  isMac,
  type KeyboardShortcut,
} from '@/hooks/useKeyboardShortcuts';

interface KeyboardShortcutsHelpProps {
  isOpen: boolean;
  onClose: () => void;
  shortcuts?: KeyboardShortcut[];
}

/**
 * Icon for shortcut category
 */
function getCategoryIcon(category?: KeyboardShortcut['category']) {
  switch (category) {
    case 'navigation':
      return <Search className="w-4 h-4" />;
    case 'actions':
      return <Upload className="w-4 h-4" />;
    case 'editor':
      return <MessageSquare className="w-4 h-4" />;
    case 'system':
      return <HelpCircle className="w-4 h-4" />;
    default:
      return <Keyboard className="w-4 h-4" />;
  }
}

/**
 * Shortcut item component
 */
function ShortcutItem({ shortcut }: { shortcut: KeyboardShortcut }) {
  return (
    <div className="flex items-center justify-between py-2 px-3 rounded-md hover:bg-accent transition-colors">
      <div className="flex items-center gap-3">
        <div className="text-muted-foreground">
          {getCategoryIcon(shortcut.category)}
        </div>
        <span className="text-sm">{shortcut.description}</span>
      </div>
      <kbd className="pointer-events-none inline-flex h-7 select-none items-center gap-1 rounded border bg-muted px-2 font-mono text-xs font-medium text-muted-foreground opacity-100">
        {formatShortcut(shortcut)}
      </kbd>
    </div>
  );
}

/**
 * Default shortcuts for display
 */
const DEFAULT_SHORTCUTS: KeyboardShortcut[] = [
  // Navigation
  {
    key: 'n',
    ctrl: true,
    meta: true,
    description: 'New chat',
    action: () => {},
    category: 'navigation',
  },
  {
    key: 'u',
    ctrl: true,
    meta: true,
    description: 'Upload PDF',
    action: () => {},
    category: 'navigation',
  },
  {
    key: 'k',
    ctrl: true,
    meta: true,
    description: 'Open command palette',
    action: () => {},
    category: 'navigation',
  },
  {
    key: '/',
    ctrl: true,
    meta: true,
    description: 'Focus search',
    action: () => {},
    category: 'navigation',
  },

  // Editor
  {
    key: 'Enter',
    ctrl: true,
    meta: true,
    description: 'Send message',
    action: () => {},
    category: 'editor',
  },

  // System
  {
    key: '?',
    shift: true,
    description: 'Show keyboard shortcuts',
    action: () => {},
    category: 'system',
  },
  {
    key: 'Escape',
    description: 'Close modal or dialog',
    action: () => {},
    category: 'system',
  },
];

/**
 * Group shortcuts by category
 */
function groupShortcutsByCategory(shortcuts: KeyboardShortcut[]) {
  const groups: Record<string, KeyboardShortcut[]> = {
    navigation: [],
    editor: [],
    actions: [],
    system: [],
  };

  for (const shortcut of shortcuts) {
    const category = shortcut.category || 'actions';
    if (!groups[category]) {
      groups[category] = [];
    }
    groups[category].push(shortcut);
  }

  // Remove empty groups
  return Object.fromEntries(
    Object.entries(groups).filter(([, shortcuts]) => shortcuts.length > 0)
  );
}

/**
 * Category display name
 */
function getCategoryName(category: string): string {
  switch (category) {
    case 'navigation':
      return 'Navigation';
    case 'editor':
      return 'Editor';
    case 'actions':
      return 'Actions';
    case 'system':
      return 'System';
    default:
      return category.charAt(0).toUpperCase() + category.slice(1);
  }
}

/**
 * KeyboardShortcutsHelp Modal Component
 */
export const KeyboardShortcutsHelp: React.FC<KeyboardShortcutsHelpProps> = ({
  isOpen,
  onClose,
  shortcuts = DEFAULT_SHORTCUTS,
}) => {
  const groupedShortcuts = groupShortcutsByCategory(shortcuts);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Keyboard className="w-5 h-5" />
            Keyboard Shortcuts
          </DialogTitle>
          <DialogDescription>
            {isMac ? 'Use ⌘ (Command) key' : 'Use Ctrl key'} with these shortcuts to navigate faster
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 mt-4">
          {Object.entries(groupedShortcuts).map(([category, categoryShortcuts]) => (
            <div key={category}>
              <h3 className="text-sm font-semibold mb-3 flex items-center gap-2">
                {getCategoryIcon(category as KeyboardShortcut['category'])}
                {getCategoryName(category)}
              </h3>
              <div className="space-y-1">
                {categoryShortcuts.map((shortcut, index) => (
                  <ShortcutItem key={`${category}-${index}`} shortcut={shortcut} />
                ))}
              </div>
              {Object.keys(groupedShortcuts).indexOf(category) <
                Object.keys(groupedShortcuts).length - 1 && (
                <Separator className="mt-4" />
              )}
            </div>
          ))}
        </div>

        <div className="mt-6 p-4 bg-muted rounded-lg">
          <div className="flex items-start gap-3">
            <HelpCircle className="w-5 h-5 mt-0.5 text-muted-foreground flex-shrink-0" />
            <div className="text-sm text-muted-foreground space-y-1">
              <p>
                <strong>Tip:</strong> Press <kbd className="px-1.5 py-0.5 text-xs font-semibold border rounded">?</kbd> anytime to open this help dialog.
              </p>
              <p>
                Most shortcuts work with {isMac ? '⌘ Command' : 'Ctrl'} key. Press{' '}
                <kbd className="px-1.5 py-0.5 text-xs font-semibold border rounded">Esc</kbd> to close.
              </p>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
};
