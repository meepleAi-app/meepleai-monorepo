'use client';

/**
 * NotesTab — Notes tab with shared and private sections.
 */

import React, { useState } from 'react';

import { useToolkitDrawer } from '../ToolkitDrawerProvider';
import { NoteCard } from './NoteCard';

import type { LocalNote } from '../types';

// ============================================================================
// Add Note inline form
// ============================================================================

interface AddNoteFormProps {
  type: 'shared' | 'private';
  onAdd: (content: string) => void;
}

function AddNoteForm({ type, onAdd }: AddNoteFormProps) {
  const [content, setContent] = useState('');
  const [open, setOpen] = useState(false);

  const handleSubmit = () => {
    const trimmed = content.trim();
    if (!trimmed) return;
    onAdd(trimmed);
    setContent('');
    setOpen(false);
  };

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-2 w-full rounded-lg border border-dashed border-gray-300 py-2 text-xs text-gray-500 hover:border-[hsl(142,70%,45%)] hover:text-[hsl(142,70%,45%)]"
        data-testid={`add-note-${type}-btn`}
      >
        + Aggiungi nota {type === 'shared' ? 'condivisa' : 'privata'}
      </button>
    );
  }

  return (
    <div className="mt-2 flex flex-col gap-2">
      <textarea
        value={content}
        onChange={e => setContent(e.target.value)}
        autoFocus
        rows={2}
        placeholder={type === 'shared' ? 'Nota visibile a tutti...' : 'Nota visibile solo a te...'}
        className="w-full resize-none rounded-lg border border-gray-300 p-2 text-sm outline-none focus:border-[hsl(142,70%,45%)]"
        data-testid={`add-note-${type}-input`}
      />
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={() => {
            setContent('');
            setOpen(false);
          }}
          className="rounded-lg px-3 py-1 text-xs text-gray-500 hover:bg-gray-100"
        >
          Annulla
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={!content.trim()}
          className="rounded-lg bg-[hsl(142,70%,45%)] px-3 py-1 text-xs font-medium text-white disabled:cursor-not-allowed disabled:bg-gray-200 disabled:text-gray-400"
          data-testid={`add-note-${type}-submit`}
        >
          Aggiungi
        </button>
      </div>
    </div>
  );
}

// ============================================================================
// NotesTab
// ============================================================================

function sortNotes(notes: LocalNote[]): LocalNote[] {
  return [...notes].sort((a, b) => {
    // Pinned first
    if (a.pinned !== b.pinned) return a.pinned ? -1 : 1;
    // Then by updated time desc
    return b.updatedAt - a.updatedAt;
  });
}

export function NotesTab() {
  const { store, logEvent } = useToolkitDrawer();

  const notes = store(s => s.notes);
  const players = store(s => s.players);
  const currentTurnIndex = store(s => s.currentTurnIndex);
  const currentPlayer = players[currentTurnIndex];

  const sharedNotes = sortNotes(notes.filter(n => n.type === 'shared'));
  const privateNotes = sortNotes(
    notes.filter(n => n.type === 'private' && (!currentPlayer || n.playerId === currentPlayer.id))
  );

  const handleAdd = (type: 'shared' | 'private', content: string) => {
    const note: LocalNote = {
      id: crypto.randomUUID(),
      content,
      type,
      playerId: currentPlayer?.id,
      playerName: currentPlayer?.name,
      pinned: false,
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
    store.getState().addNote(note);
    logEvent(
      'note_added',
      { type, preview: content.slice(0, 80) },
      {
        playerId: currentPlayer?.id,
        playerName: currentPlayer?.name,
      }
    );
  };

  const handleUpdate = (noteId: string, content: string) => {
    store.getState().updateNote(noteId, content);
  };

  const handleDelete = (noteId: string) => {
    store.getState().deleteNote(noteId);
  };

  const handleTogglePin = (noteId: string) => {
    store.getState().togglePin(noteId);
  };

  return (
    <div className="flex flex-col gap-4" data-testid="notes-tab">
      {/* Shared notes */}
      <section>
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-400">
          Condivise
        </h3>
        <div className="flex flex-col gap-2">
          {sharedNotes.length === 0 ? (
            <p className="text-xs italic text-gray-400">Nessuna nota condivisa</p>
          ) : (
            sharedNotes.map(note => (
              <NoteCard
                key={note.id}
                note={note}
                onUpdate={handleUpdate}
                onDelete={handleDelete}
                onTogglePin={handleTogglePin}
              />
            ))
          )}
          <AddNoteForm type="shared" onAdd={c => handleAdd('shared', c)} />
        </div>
      </section>

      <hr className="border-gray-200" />

      {/* Private notes */}
      <section>
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-gray-400">
          🔒 Le mie note
        </h3>
        <div className="flex flex-col gap-2">
          {privateNotes.length === 0 ? (
            <p className="text-xs italic text-gray-400">Nessuna nota privata</p>
          ) : (
            privateNotes.map(note => (
              <NoteCard
                key={note.id}
                note={note}
                onUpdate={handleUpdate}
                onDelete={handleDelete}
                onTogglePin={handleTogglePin}
              />
            ))
          )}
          <AddNoteForm type="private" onAdd={c => handleAdd('private', c)} />
        </div>
      </section>
    </div>
  );
}
