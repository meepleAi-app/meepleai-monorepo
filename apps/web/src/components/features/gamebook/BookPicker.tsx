/**
 * BookPicker — radio-group of GameBook options (Phase E1).
 *
 * Used inside the photo-translate form when a game has 2+ narrative books.
 * The user must explicitly pick which book a photo belongs to so the BE
 * scopes per-book progress correctly (see SessionBookProgress + C2 in the
 * gamebook multi-book generalization spec).
 *
 * Contract:
 *   - <= 1 book → render nothing (auto-selected upstream).
 *   - 2+ books → render a horizontal radio group, one button per option.
 *   - On click, fire `onChange(bookId)`.
 *
 * The `roles` field on each option carries the GameBookRole bitflag (Tutorial=1,
 * Setup=2, Narrative=4, Encounter=8, RulesReference=16). Callers typically
 * filter the full book list by role BEFORE passing it in (e.g. only Narrative
 * books for a photo-translate flow).
 */

import { type FC } from 'react';

export interface BookPickerOption {
  id: string;
  displayName: string;
  /**
   * GameBookRole bitflag mirroring the backend enum. Encoded as an `int` over
   * the wire to keep the FE type plain.
   */
  roles: number;
}

export interface BookPickerProps {
  books: BookPickerOption[];
  value: string;
  onChange: (bookId: string) => void;
}

export const BookPicker: FC<BookPickerProps> = ({ books, value, onChange }) => {
  if (books.length <= 1) return null;

  return (
    <div
      role="radiogroup"
      aria-label="Seleziona libro"
      className="flex flex-wrap gap-2"
      data-testid="book-picker"
    >
      {books.map(book => {
        const selected = value === book.id;
        return (
          <button
            key={book.id}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(book.id)}
            className={
              'rounded-md border px-3 py-2 text-sm font-medium transition-colors ' +
              (selected
                ? 'bg-primary text-primary-foreground border-primary'
                : 'bg-muted text-foreground border-border hover:bg-muted/80')
            }
            data-testid={`book-picker-option-${book.id}`}
          >
            {book.displayName}
          </button>
        );
      })}
    </div>
  );
};
