/**
 * ResumeBooksList — per-book progress display (Phase E2).
 *
 * Renders a list of "in-progress" books on the play page so users can resume
 * any book they have started. One row per `BookProgress` entry, each row
 * shows the book name + last location, plus a "Riprendi" button that fires
 * `onResume(bookId)`.
 *
 * Empty state ("Nessun libro in corso.") covers the case where no
 * `SessionBookProgress` has been recorded yet for any book.
 *
 * Spec: `docs/superpowers/specs/2026-05-19-gamebook-multi-book-generalization-design.md`.
 */

import { type FC } from 'react';

export interface BookProgress {
  bookId: string;
  bookName: string;
  /**
   * Display label for the last visited paragraph / section in the book
   * (e.g. "§289", "E7", "Chapter 4"). Already formatted by the caller —
   * this component does not interpret the ParagraphScheme.
   */
  lastLocation: string;
  /** ISO 8601 timestamp of the last visit; not currently rendered but kept for parity with backend DTO. */
  lastVisitedAt: string;
}

export interface ResumeBooksListProps {
  progress: BookProgress[];
  onResume: (bookId: string) => void;
}

export const ResumeBooksList: FC<ResumeBooksListProps> = ({ progress, onResume }) => {
  if (progress.length === 0) {
    return (
      <p className="text-muted-foreground text-sm" data-testid="resume-books-list-empty">
        Nessun libro in corso.
      </p>
    );
  }

  return (
    <ul className="flex flex-col gap-2" data-testid="resume-books-list">
      {progress.map(p => (
        <li
          key={p.bookId}
          className="flex items-center justify-between rounded-md border border-border bg-card p-3"
          data-testid={`resume-books-list-row-${p.bookId}`}
        >
          <div>
            <p className="font-medium text-foreground">{p.bookName}</p>
            <p className="text-sm text-muted-foreground">{p.lastLocation}</p>
          </div>
          <button
            type="button"
            onClick={() => onResume(p.bookId)}
            className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
            data-testid={`resume-books-list-button-${p.bookId}`}
          >
            Riprendi
          </button>
        </li>
      ))}
    </ul>
  );
};
