'use client';

/**
 * NoteCard — Single note with inline edit, pin toggle, and delete.
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import type { LocalNote } from '../types';

export interface NoteCardProps {
  note: LocalNote;
  onUpdate: (noteId: string, content: string) => void;
  onDelete: (noteId: string) => void;
  onTogglePin: (noteId: string) => void;
}

function formatRelative(timestamp: number): string {
  const diffMs = Date.now() - timestamp;
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return 'ora';
  if (diffMin < 60) return `${diffMin}min fa`;
  const diffH = Math.floor(diffMin / 60);
  if (diffH < 24) return `${diffH}h fa`;
  const d = new Date(timestamp);
  return d.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

export function NoteCard({ note, onUpdate, onDelete, onTogglePin }: NoteCardProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(note.content);

  const handleSave = () => {
    const trimmed = draft.trim();
    if (trimmed && trimmed !== note.content) {
      onUpdate(note.id, trimmed);
    }
    setEditing(false);
  };

  const handleCancel = () => {
    setDraft(note.content);
    setEditing(false);
  };

  const handleDelete = () => {
    if (confirm('Eliminare questa nota?')) {
      onDelete(note.id);
    }
  };

  return (
    <div
      className={cn(
        'rounded-xl border bg-white p-3 shadow-sm transition-all',
        note.pinned ? 'border-[hsl(142,70%,45%)]/40 bg-[hsl(142,70%,45%)]/5' : 'border-gray-200'
      )}
      data-testid={`note-card-${note.id}`}
    >
      {editing ? (
        <div className="flex flex-col gap-2">
          <textarea
            value={draft}
            onChange={e => setDraft(e.target.value)}
            autoFocus
            rows={Math.max(2, draft.split('\n').length)}
            className="w-full resize-none rounded-lg border border-gray-300 p-2 text-sm outline-none focus:border-[hsl(142,70%,45%)]"
            data-testid={`note-edit-${note.id}`}
          />
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={handleCancel}
              className="rounded-lg px-3 py-1 text-xs text-gray-500 hover:bg-gray-100"
            >
              Annulla
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="rounded-lg bg-[hsl(142,70%,45%)] px-3 py-1 text-xs font-medium text-white hover:bg-[hsl(142,70%,40%)]"
            >
              Salva
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="flex items-start justify-between gap-2">
            <button
              type="button"
              onClick={() => setEditing(true)}
              className="flex-1 text-left text-sm text-gray-800 whitespace-pre-wrap"
              data-testid={`note-content-${note.id}`}
            >
              {note.pinned && <span className="mr-1">📌</span>}
              {note.content}
            </button>
            <div className="flex shrink-0 items-center gap-1">
              <button
                type="button"
                onClick={() => onTogglePin(note.id)}
                className="rounded p-1 text-xs hover:bg-gray-100"
                title={note.pinned ? 'Rimuovi pin' : 'Pinna'}
                data-testid={`note-pin-${note.id}`}
              >
                {note.pinned ? '📌' : '📍'}
              </button>
              <button
                type="button"
                onClick={handleDelete}
                className="rounded p-1 text-xs hover:bg-red-50"
                title="Elimina"
                data-testid={`note-delete-${note.id}`}
              >
                🗑️
              </button>
            </div>
          </div>
          <div className="mt-1 flex items-center gap-2 text-[10px] text-gray-400">
            {note.playerName && <span>{note.playerName}</span>}
            {note.playerName && <span>·</span>}
            <span>{formatRelative(note.updatedAt)}</span>
          </div>
        </>
      )}
    </div>
  );
}
