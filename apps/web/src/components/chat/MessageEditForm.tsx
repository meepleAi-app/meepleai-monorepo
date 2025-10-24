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
    <div style={{ marginTop: 8 }}>
      <textarea
        value={editContent}
        onChange={(e) => setEditContent(e.target.value)}
        disabled={isUpdating}
        style={{
          width: '100%',
          minHeight: 80,
          padding: 8,
          fontSize: 14,
          border: '1px solid #cbd5e1',
          borderRadius: 4,
          fontFamily: 'inherit',
          resize: 'vertical'
        }}
        aria-label="Edit message content"
        autoFocus
      />
      <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
        <button
          onClick={() => void saveEdit()}
          disabled={!canSave}
          style={{
            padding: '6px 12px',
            background: canSave ? '#1a73e8' : '#cbd5e1',
            color: 'white',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            fontWeight: 500,
            cursor: canSave ? 'pointer' : 'not-allowed'
          }}
          aria-label="Save edited message"
        >
          {isUpdating ? 'Salvataggio...' : 'Salva'}
        </button>
        <button
          onClick={cancelEdit}
          disabled={isUpdating}
          style={{
            padding: '6px 12px',
            background: '#94a3b8',
            color: 'white',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            fontWeight: 500,
            cursor: isUpdating ? 'not-allowed' : 'pointer',
            opacity: isUpdating ? 0.5 : 1
          }}
          aria-label="Cancel editing"
        >
          Annulla
        </button>
      </div>
    </div>
  );
}
