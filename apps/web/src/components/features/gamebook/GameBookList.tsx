/**
 * GameBookList — the 1..N GameBook "book-manager" surface (issue #2637, SI-6).
 *
 * Renders the real `GameBook` aggregate for a game (community books have
 * `ownerUserId = null`; personal books carry an owner) instead of the retired
 * hardcoded "Press Start + Rules" 2-PDF model. One row per book showing the
 * editable DisplayName, its role badges (decoded from the `roles` bitflag),
 * whether it is a community or personal book, and its KB/physical status.
 *
 * Presentational: the caller owns the fetch (`useGameBooks`) and passes the
 * result down, so the component stays trivially testable and reusable across
 * the detail / (future) onboarding / glossary surfaces. Graceful loading,
 * empty and all-physical states per the demo FIX-2 requirements.
 *
 * Spec: `docs/for-developers/specs/2026-07-01-issue-2619-decomposition-design.md` §5 SI-6.
 */

'use client';

import { type ReactElement } from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import { rolesToNames, type GameBookDto, type GameBookRoleName } from '@/lib/api/gamebook';

export interface GameBookListProps {
  /** The books to render — already fetched by the caller via `useGameBooks`. */
  readonly books: readonly GameBookDto[];
  /** Show the loading skeleton while the caller's query is in flight. */
  readonly isLoading?: boolean;
}

function roleLabelKey(name: GameBookRoleName): string {
  return `gamebook.bookRole.${name.charAt(0).toLowerCase()}${name.slice(1)}`;
}

export function GameBookList({ books, isLoading = false }: GameBookListProps): ReactElement {
  const { t } = useTranslation();

  if (isLoading) {
    return (
      <div data-testid="game-book-list-loading" aria-busy="true" className="flex flex-col gap-2">
        <span className="sr-only">{t('gamebook.bookList.loading')}</span>
        {[0, 1].map(i => (
          <div
            key={i}
            aria-hidden="true"
            className="h-16 animate-pulse rounded-md border border-border bg-muted"
          />
        ))}
      </div>
    );
  }

  if (books.length === 0) {
    return (
      <p data-testid="game-book-list-empty" className="text-sm text-muted-foreground">
        {t('gamebook.bookList.empty')}
      </p>
    );
  }

  const indexedCount = books.filter(b => b.kbSourceDocId !== null).length;

  return (
    <div className="flex flex-col gap-2">
      <p data-testid="game-book-list-summary" className="text-xs font-medium text-muted-foreground">
        <span>{t('gamebook.bookList.count', { count: books.length })}</span>
        {indexedCount > 0 && (
          <span> · {t('gamebook.bookList.indexed', { count: indexedCount })}</span>
        )}
      </p>

      <ul
        data-testid="game-book-list"
        aria-label={t('gamebook.bookList.heading')}
        className="flex flex-col gap-2"
      >
        {books.map(book => (
          <li
            key={book.id}
            data-testid={`game-book-list-row-${book.id}`}
            className="flex flex-col gap-1.5 rounded-md border border-border bg-card p-3"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="font-medium text-foreground">{book.displayName}</span>
              <OriginBadge ownerUserId={book.ownerUserId} />
            </div>

            <div className="flex flex-wrap items-center gap-1.5">
              {rolesToNames(book.roles).map(name => (
                <span
                  key={name}
                  data-slot="game-book-role"
                  className="inline-flex items-center rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground"
                >
                  {t(roleLabelKey(name))}
                </span>
              ))}
              <StatusBadge book={book} />
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

function OriginBadge({ ownerUserId }: { ownerUserId: string | null }): ReactElement {
  const { t } = useTranslation();
  const isCommunity = ownerUserId === null;
  return (
    <span
      data-slot="game-book-origin"
      className={
        'inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-xs font-medium ' +
        (isCommunity
          ? 'bg-[hsl(var(--c-kb)/0.12)] text-[hsl(var(--c-kb))]'
          : 'bg-[hsl(var(--c-player)/0.12)] text-[hsl(var(--c-player))]')
      }
    >
      {isCommunity ? t('gamebook.bookList.originCommunity') : t('gamebook.bookList.originPersonal')}
    </span>
  );
}

function StatusBadge({ book }: { book: GameBookDto }): ReactElement {
  const { t } = useTranslation();
  const label = book.physicalOnly
    ? t('gamebook.bookList.statusPhysical')
    : book.kbSourceDocId !== null
      ? t('gamebook.bookList.statusIndexed')
      : t('gamebook.bookList.statusNotIndexed');
  return (
    <span
      data-slot="game-book-status"
      className="inline-flex items-center rounded-full border border-border px-2 py-0.5 text-xs font-medium text-muted-foreground"
    >
      {label}
    </span>
  );
}
