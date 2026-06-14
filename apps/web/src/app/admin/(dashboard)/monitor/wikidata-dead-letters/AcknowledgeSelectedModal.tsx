'use client';

import { useEffect, useRef, useState, type KeyboardEvent } from 'react';

import { BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH } from '@/lib/api/admin-wikidata-dead-letters';

interface AcknowledgeSelectedModalProps {
  open: boolean;
  selectedCount: number;
  onConfirm: (note: string | null) => Promise<void> | void;
  onCancel: () => void;
}

/**
 * Issue #1823 Phase F F5 — confirmation modal for bulk-acknowledge.
 * Optional free-text note (max 500 chars, log-only on the BE per DEC-F-4).
 * Pre-empties to `null` instead of empty string so the BE log line shows `note=<none>`.
 * A11y: aria-modal + aria-labelledby; Escape dismisses; opens with focus on textarea.
 */
export function AcknowledgeSelectedModal({
  open,
  selectedCount,
  onConfirm,
  onCancel,
}: AcknowledgeSelectedModalProps) {
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // A11y: when the modal opens, programmatically focus the textarea so screen-reader
  // users land inside the dialog without having to tab through the page background.
  useEffect(() => {
    if (open) {
      textareaRef.current?.focus();
    }
  }, [open]);

  if (!open) return null;

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Escape' && !submitting) {
      e.stopPropagation();
      onCancel();
    }
  };

  const handleConfirm = async () => {
    setSubmitting(true);
    try {
      await onConfirm(note.trim() === '' ? null : note);
      setNote('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="ack-modal-title"
      onKeyDown={handleKeyDown}
      className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm"
    >
      <div className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-lg">
        <h2 id="ack-modal-title" className="text-lg font-semibold text-foreground">
          Acknowledge {selectedCount} dead-letter row{selectedCount === 1 ? '' : 's'}?
        </h2>
        <p className="mt-2 text-sm text-muted-foreground">
          Acknowledged rows are hidden from the default list view and won&apos;t consume scheduler
          retry budget. They are deleted after the 7-day retention sweep regardless.
        </p>

        <label htmlFor="ack-note" className="mt-4 block text-sm font-medium text-foreground">
          Note (optional, log only)
        </label>
        <textarea
          id="ack-note"
          ref={textareaRef}
          aria-label="note"
          maxLength={BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH}
          value={note}
          onChange={e => setNote(e.target.value)}
          rows={3}
          className="mt-1 w-full rounded border border-border bg-background p-2 text-sm text-foreground"
          placeholder="e.g. Commons deleted file"
          disabled={submitting}
        />
        <p className="mt-1 text-xs text-muted-foreground">
          {note.length}/{BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH}
        </p>

        <div className="mt-6 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted"
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            className="rounded bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
            disabled={submitting}
          >
            {submitting ? 'Acknowledging…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  );
}
