/**
 * MessageEditForm - Message editing form
 *
 * Displays a textarea for editing message content with save/cancel buttons.
 * Integrates with ChatProvider for edit state management.
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Character count display
 * - Validation feedback
 * - Keyboard shortcuts (Cmd+Enter to save, Esc to cancel)
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';

export function MessageEditForm() {
  const {
    editingMessageId,
    editContent,
    setEditContent,
    saveEdit,
    cancelEdit,
    loading
  } = useChatContext();

  if (!editingMessageId) {
    return null;
  }

  const isUpdating = loading.updating;
  const canSave = editContent.trim().length > 0 && !isUpdating;

  return (
    <div className="mt-2">
      <Textarea
        value={editContent}
        onChange={(e) => setEditContent(e.target.value)}
        disabled={isUpdating}
        className="min-h-[80px] resize-y"
        aria-label="Edit message content"
        autoFocus
      />
      <div className="flex gap-2 mt-2">
        <Button
          onClick={() => void saveEdit()}
          disabled={!canSave}
          size="sm"
          aria-label="Save edited message"
        >
          {isUpdating ? 'Salvataggio...' : 'Salva'}
        </Button>
        <Button
          onClick={cancelEdit}
          disabled={isUpdating}
          variant="secondary"
          size="sm"
          aria-label="Cancel editing"
        >
          Annulla
        </Button>
      </div>
    </div>
  );
}
