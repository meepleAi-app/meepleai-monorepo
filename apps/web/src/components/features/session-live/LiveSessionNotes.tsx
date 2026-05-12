/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * LiveSessionNotes — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Notes panel with append-only list and add-note form.
 *
 * Role variants:
 *   Spectator: read-only notes list, add-note form hidden entirely.
 *   Player+Host: full form with visibility toggle (private/shared).
 *
 * Gate C: DIVERGES from MeepleCard — notes panel, not a card pattern.
 *
 * data-slot="live-session-notes" — required by unit tests.
 * data-viewer-role={viewerRole} — role variant assertion in unit tests.
 */

import { type ReactElement, useState } from 'react';

import { PlusCircle } from 'lucide-react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface NoteEntry {
  readonly id: string;
  readonly authorId: string;
  readonly authorName: string;
  readonly content: string;
  readonly visibility: 'private' | 'shared';
  readonly timestamp: string;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface LiveSessionNotesLabels {
  readonly title: string;
  readonly inputAriaLabel: string;
  readonly addAriaLabel: string;
  readonly visibilityPrivate: string;
  readonly visibilityShared: string;
  readonly emptyMessage: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface LiveSessionNotesProps {
  readonly notes: ReadonlyArray<NoteEntry>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onAddNote: (content: string, visibility: 'private' | 'shared') => void;
  readonly labels: LiveSessionNotesLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function LiveSessionNotes({
  notes,
  viewerRole,
  viewerId,
  onAddNote,
  labels,
  className,
}: LiveSessionNotesProps): ReactElement {
  const [inputValue, setInputValue] = useState('');
  const [visibility, setVisibility] = useState<'private' | 'shared'>('shared');

  const isSpectator = viewerRole === 'Spectator';

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = inputValue.trim();
    if (!trimmed) return;
    onAddNote(trimmed, visibility);
    setInputValue('');
  };

  return (
    <section
      data-slot="live-session-notes"
      data-viewer-role={viewerRole}
      aria-label={labels.title}
      className={`flex flex-col gap-3 ${className ?? ''}`}
    >
      <h3 className="shrink-0 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.title}
      </h3>

      {/* Notes list */}
      <div className="flex flex-col gap-2 overflow-y-auto">
        {notes.length === 0 ? (
          <p className="py-4 text-center text-sm text-muted-foreground">{labels.emptyMessage}</p>
        ) : (
          notes.map(note => {
            const isOwn = note.authorId === viewerId;
            const isPrivate = note.visibility === 'private';
            return (
              <article
                key={note.id}
                data-note-id={note.id}
                data-visibility={note.visibility}
                className={`rounded-lg border p-3 text-sm ${
                  isPrivate
                    ? 'border-amber-700/30 bg-amber-900/10'
                    : 'border-border/40 bg-card'
                }`}
              >
                <header className="mb-1 flex items-center justify-between gap-2">
                  <span
                    className={`text-xs font-medium ${isOwn ? 'text-slate-300' : 'text-muted-foreground'}`}
                  >
                    {note.authorName}
                  </span>
                  {isPrivate && (
                    <span className="text-xs text-amber-400/70">{labels.visibilityPrivate}</span>
                  )}
                </header>
                <p className="text-slate-200">{note.content}</p>
              </article>
            );
          })
        )}
      </div>

      {/* Add note form — hidden for Spectator */}
      {!isSpectator && (
        <form onSubmit={handleSubmit} className="flex shrink-0 flex-col gap-2">
          {/* Visibility toggle */}
          <div className="flex gap-2" role="group" aria-label="Visibilità nota">
            <button
              type="button"
              aria-pressed={visibility === 'shared'}
              onClick={() => setVisibility('shared')}
              className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                visibility === 'shared'
                  ? 'bg-slate-600 text-slate-100'
                  : 'text-muted-foreground hover:text-slate-300'
              }`}
            >
              {labels.visibilityShared}
            </button>
            <button
              type="button"
              aria-pressed={visibility === 'private'}
              onClick={() => setVisibility('private')}
              className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                visibility === 'private'
                  ? 'bg-amber-700/60 text-amber-100'
                  : 'text-muted-foreground hover:text-slate-300'
              }`}
            >
              {labels.visibilityPrivate}
            </button>
          </div>

          <div className="flex gap-2">
            <textarea
              value={inputValue}
              onChange={e => setInputValue(e.target.value)}
              aria-label={labels.inputAriaLabel}
              placeholder={labels.inputAriaLabel}
              rows={2}
              className="min-w-0 flex-1 resize-none rounded-lg border border-border/60
                bg-card px-3 py-2 text-sm text-slate-200 placeholder-slate-500
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
            />
            <button
              type="submit"
              aria-label={labels.addAriaLabel}
              disabled={!inputValue.trim()}
              className="flex shrink-0 items-start justify-center rounded-lg border
                border-border/60 bg-card p-2 text-slate-200
                transition-colors hover:bg-slate-600
                disabled:cursor-not-allowed disabled:opacity-40
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
            >
              <PlusCircle className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
