'use client';

/**
 * PrivateNotes Component (Issue #3344)
 *
 * Interactive private notes panel for game sessions.
 * Features:
 * - Create and edit private notes
 * - Blur/obscure toggle for partial reveal
 * - Full reveal on demand
 * - Notes persist across session pauses
 */

import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { SessionNote } from './types';

// ============================================================================
// Types
// ============================================================================

export interface PrivateNotesProps {
  /** All visible notes */
  notes: SessionNote[];

  /** Current participant ID */
  participantId: string;

  /** Whether actions are loading */
  isLoading?: boolean;

  /** Save note callback */
  onSave?: (content: string, obscuredText?: string, noteId?: string) => Promise<void>;

  /** Reveal note callback */
  onReveal?: (noteId: string) => Promise<void>;

  /** Hide note callback */
  onHide?: (noteId: string) => Promise<void>;

  /** Delete note callback */
  onDelete?: (noteId: string) => Promise<void>;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Sub-Components
// ============================================================================

interface NoteCardProps {
  note: SessionNote;
  isOwner: boolean;
  onReveal?: () => void;
  onHide?: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  isLoading?: boolean;
}

function NoteCard({ note, isOwner, onReveal, onHide, onEdit, onDelete, isLoading }: NoteCardProps) {
  const [showContent, setShowContent] = useState(false);

  const isObscured = !note.isRevealed && !isOwner && !showContent;

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className={cn(
        'relative p-4 rounded-lg border',
        'bg-card text-card-foreground',
        isOwner ? 'border-primary/50' : 'border-border',
        isLoading && 'opacity-50'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          {isOwner ? (
            <span className="text-xs px-2 py-0.5 rounded-full bg-primary/10 text-primary">
              Your Note
            </span>
          ) : (
            <span className="text-xs px-2 py-0.5 rounded-full bg-muted text-muted-foreground">
              Revealed
            </span>
          )}
          {note.isRevealed && (
            <span className="text-xs text-green-600 dark:text-green-400">Visible to all</span>
          )}
        </div>
        <span className="text-xs text-muted-foreground">
          {new Date(note.updatedAt).toLocaleTimeString()}
        </span>
      </div>

      {/* Content */}
      <div className="relative">
        {isObscured ? (
          <div
            className={cn(
              'p-3 rounded bg-muted/50 min-h-[60px]',
              'flex items-center justify-center'
            )}
          >
            <div className="text-center">
              {note.obscuredText ? (
                <p className="text-sm italic text-muted-foreground">{note.obscuredText}</p>
              ) : (
                <p className="text-sm text-muted-foreground">Content hidden</p>
              )}
            </div>
          </div>
        ) : (
          <motion.div
            initial={{ filter: 'blur(8px)' }}
            animate={{ filter: 'blur(0px)' }}
            transition={{ duration: 0.3 }}
            className="p-3 rounded bg-muted/30 min-h-[60px]"
          >
            <p className="text-sm whitespace-pre-wrap">{note.content || note.obscuredText || '...'}</p>
          </motion.div>
        )}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 mt-3">
        {isOwner && (
          <>
            {note.isRevealed ? (
              <Button
                variant="outline"
                size="sm"
                onClick={onHide}
                disabled={isLoading}
              >
                Hide
              </Button>
            ) : (
              <Button
                variant="default"
                size="sm"
                onClick={onReveal}
                disabled={isLoading}
              >
                Reveal to All
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              onClick={onEdit}
              disabled={isLoading}
            >
              Edit
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="text-destructive hover:text-destructive"
              onClick={onDelete}
              disabled={isLoading}
            >
              Delete
            </Button>
          </>
        )}
        {!isOwner && !note.isRevealed && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setShowContent(!showContent)}
          >
            {showContent ? 'Hide Preview' : 'Show Hint'}
          </Button>
        )}
      </div>
    </motion.div>
  );
}

interface NoteEditorProps {
  initialContent?: string;
  initialObscuredText?: string;
  onSave: (content: string, obscuredText?: string) => void;
  onCancel: () => void;
  isLoading?: boolean;
}

function NoteEditor({
  initialContent = '',
  initialObscuredText = '',
  onSave,
  onCancel,
  isLoading,
}: NoteEditorProps) {
  const [content, setContent] = useState(initialContent);
  const [obscuredText, setObscuredText] = useState(initialObscuredText);

  const handleSave = () => {
    if (content.trim()) {
      onSave(content.trim(), obscuredText.trim() || undefined);
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: 'auto' }}
      exit={{ opacity: 0, height: 0 }}
      className="p-4 rounded-lg border border-primary bg-card"
    >
      <div className="space-y-3">
        <div>
          <label className="text-sm font-medium mb-1 block">Note Content (Private)</label>
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="Write your private note..."
            className={cn(
              'w-full h-24 p-3 rounded-md border resize-none',
              'bg-background text-foreground',
              'focus:outline-none focus:ring-2 focus:ring-primary'
            )}
            disabled={isLoading}
          />
        </div>
        <div>
          <label className="text-sm font-medium mb-1 block">
            Obscured Text (Optional hint shown to others)
          </label>
          <input
            type="text"
            value={obscuredText}
            onChange={(e) => setObscuredText(e.target.value)}
            placeholder="e.g., 'Strategy hint...'"
            className={cn(
              'w-full p-2 rounded-md border',
              'bg-background text-foreground',
              'focus:outline-none focus:ring-2 focus:ring-primary'
            )}
            maxLength={500}
            disabled={isLoading}
          />
          <p className="text-xs text-muted-foreground mt-1">
            This text is shown to others before you reveal the full note
          </p>
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="ghost" size="sm" onClick={onCancel} disabled={isLoading}>
            Cancel
          </Button>
          <Button
            variant="default"
            size="sm"
            onClick={handleSave}
            disabled={isLoading || !content.trim()}
          >
            {isLoading ? 'Saving...' : 'Save Note'}
          </Button>
        </div>
      </div>
    </motion.div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function PrivateNotes({
  notes,
  participantId,
  isLoading = false,
  onSave,
  onReveal,
  onHide,
  onDelete,
  className,
}: PrivateNotesProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);

  const myNotes = notes.filter((n) => n.isOwner);
  const revealedNotes = notes.filter((n) => !n.isOwner && n.isRevealed);

  const handleSave = useCallback(
    async (content: string, obscuredText?: string) => {
      if (!onSave) return;
      await onSave(content, obscuredText, editingNoteId || undefined);
      setIsEditing(false);
      setEditingNoteId(null);
    },
    [onSave, editingNoteId]
  );

  const handleEdit = useCallback((noteId: string) => {
    setEditingNoteId(noteId);
    setIsEditing(true);
  }, []);

  const handleCancel = useCallback(() => {
    setIsEditing(false);
    setEditingNoteId(null);
  }, []);

  const editingNote = editingNoteId ? notes.find((n) => n.id === editingNoteId) : null;

  return (
    <div className={cn('space-y-4', className)}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Private Notes</h3>
        {!isEditing && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => setIsEditing(true)}
            disabled={isLoading}
          >
            + New Note
          </Button>
        )}
      </div>

      {/* Editor */}
      <AnimatePresence>
        {isEditing && (
          <NoteEditor
            initialContent={editingNote?.content || ''}
            initialObscuredText={editingNote?.obscuredText || ''}
            onSave={handleSave}
            onCancel={handleCancel}
            isLoading={isLoading}
          />
        )}
      </AnimatePresence>

      {/* My Notes */}
      {myNotes.length > 0 && (
        <div className="space-y-3">
          <h4 className="text-sm font-medium text-muted-foreground">Your Notes</h4>
          <AnimatePresence mode="popLayout">
            {myNotes.map((note) => (
              <NoteCard
                key={note.id}
                note={note}
                isOwner={true}
                onReveal={() => onReveal?.(note.id)}
                onHide={() => onHide?.(note.id)}
                onEdit={() => handleEdit(note.id)}
                onDelete={() => onDelete?.(note.id)}
                isLoading={isLoading}
              />
            ))}
          </AnimatePresence>
        </div>
      )}

      {/* Revealed Notes from Others */}
      {revealedNotes.length > 0 && (
        <div className="space-y-3">
          <h4 className="text-sm font-medium text-muted-foreground">Revealed by Others</h4>
          <AnimatePresence mode="popLayout">
            {revealedNotes.map((note) => (
              <NoteCard
                key={note.id}
                note={note}
                isOwner={false}
                isLoading={isLoading}
              />
            ))}
          </AnimatePresence>
        </div>
      )}

      {/* Empty State */}
      {notes.length === 0 && !isEditing && (
        <div className="text-center py-8 text-muted-foreground">
          <p>No notes yet</p>
          <p className="text-sm">Create a private note to keep track of your strategy</p>
        </div>
      )}
    </div>
  );
}

export default PrivateNotes;
