'use client';

import { useMemo, useState } from 'react';

import { entityHsl, entityIcon, entityLabel } from '../tokens';

import type { CardStatus, MeepleCardProps, MeepleEntityType } from '../types';

/**
 * EntityTable — tabular view of MeepleCard data.
 *
 * Implements the wireframe from `admin-mockups/meeple-card-visual-test.html`
 * Section 3. Each row is entity-colored on the left border, with columns:
 * Titolo, Tipo, Dettagli, Rating, Stato, Nav.
 *
 * Sorting is uncontrolled (local state) — sortable columns are Titolo, Tipo
 * and Rating. Default sort is Titolo ascending.
 */

export type EntityTableSortColumn = 'title' | 'type' | 'rating';
export type EntityTableSortDirection = 'asc' | 'desc';

export interface EntityTableProps {
  /** Cards to display. Accepts the same shape as MeepleCard. */
  cards: MeepleCardProps[];
  /** Column to sort by (uncontrolled default, or override). */
  defaultSortBy?: EntityTableSortColumn;
  /** Default sort direction. */
  defaultSortDirection?: EntityTableSortDirection;
  /** Optional click handler — falls back to card.onClick when not provided. */
  onRowClick?: (card: MeepleCardProps) => void;
  /** Caption for screen readers (always rendered, visually hidden). */
  caption?: string;
  className?: string;
}

const STATUS_LABELS: Record<CardStatus, string> = {
  owned: 'Posseduto',
  wishlist: 'Wishlist',
  active: 'Attivo',
  idle: 'Inattivo',
  archived: 'Archiviato',
  processing: 'Processing',
  indexed: 'Indicizzato',
  failed: 'Fallito',
  inprogress: 'In corso',
  setup: 'Setup',
  completed: 'Completata',
  paused: 'In pausa',
};

/**
 * Semantic status colors (independent from entity color).
 * Matches the convention in summary mockup + mobile layout.
 */
const STATUS_COLOR: Record<CardStatus, string> = {
  owned: '#166534', // green-800
  wishlist: '#92400e', // amber-800
  active: '#166534',
  idle: '#6b7280', // gray-500
  archived: '#6b7280',
  processing: '#1e40af', // blue-800
  indexed: '#166534',
  failed: '#991b1b', // red-800
  inprogress: '#1e40af',
  setup: '#92400e',
  completed: '#166534',
  paused: '#6b7280',
};

function formatDetails(card: MeepleCardProps): string {
  const parts = (card.metadata ?? []).map(m => m.label).filter(Boolean);
  if (parts.length === 0) return '—';
  return parts.slice(0, 3).join(' · ');
}

function compareNumbers(
  a: number | undefined,
  b: number | undefined,
  dir: EntityTableSortDirection
): number {
  // Undefined values sort last regardless of direction.
  if (a === undefined && b === undefined) return 0;
  if (a === undefined) return 1;
  if (b === undefined) return -1;
  return dir === 'asc' ? a - b : b - a;
}

function compareStrings(a: string, b: string, dir: EntityTableSortDirection): number {
  const cmp = a.localeCompare(b, 'it', { sensitivity: 'base' });
  return dir === 'asc' ? cmp : -cmp;
}

export function EntityTable({
  cards,
  defaultSortBy = 'title',
  defaultSortDirection = 'asc',
  onRowClick,
  caption = 'Elenco entità',
  className = '',
}: EntityTableProps) {
  const [sortBy, setSortBy] = useState<EntityTableSortColumn>(defaultSortBy);
  const [sortDirection, setSortDirection] =
    useState<EntityTableSortDirection>(defaultSortDirection);

  const sortedCards = useMemo(() => {
    const copy = [...cards];
    copy.sort((a, b) => {
      if (sortBy === 'title') return compareStrings(a.title, b.title, sortDirection);
      if (sortBy === 'type') {
        return compareStrings(entityLabel[a.entity], entityLabel[b.entity], sortDirection);
      }
      if (sortBy === 'rating') return compareNumbers(a.rating, b.rating, sortDirection);
      return 0;
    });
    return copy;
  }, [cards, sortBy, sortDirection]);

  const handleSortClick = (column: EntityTableSortColumn) => {
    if (column === sortBy) {
      setSortDirection(prev => (prev === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(column);
      setSortDirection('asc');
    }
  };

  if (cards.length === 0) {
    return (
      <div
        className={`rounded-2xl border border-dashed border-[var(--mc-border)] bg-[var(--mc-bg-muted)] px-6 py-8 text-center text-sm text-[var(--mc-text-secondary)] ${className}`}
      >
        Nessun elemento da visualizzare.
      </div>
    );
  }

  return (
    <div
      className={`overflow-x-auto rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] ${className}`}
    >
      <table className="w-full border-separate border-spacing-0 text-sm">
        <caption className="sr-only">{caption}</caption>
        <thead className="bg-black/[0.03] dark:bg-white/[0.04]">
          <tr>
            <SortableHeader
              label="Titolo"
              column="title"
              sortBy={sortBy}
              sortDirection={sortDirection}
              onClick={handleSortClick}
            />
            <SortableHeader
              label="Tipo"
              column="type"
              sortBy={sortBy}
              sortDirection={sortDirection}
              onClick={handleSortClick}
            />
            <th
              scope="col"
              className="border-b border-[var(--mc-border)] px-3.5 py-2.5 text-left font-[var(--font-quicksand)] text-[0.72rem] font-semibold uppercase tracking-wider text-[var(--mc-text-muted)]"
            >
              Dettagli
            </th>
            <SortableHeader
              label="Rating"
              column="rating"
              sortBy={sortBy}
              sortDirection={sortDirection}
              onClick={handleSortClick}
            />
            <th
              scope="col"
              className="border-b border-[var(--mc-border)] px-3.5 py-2.5 text-left font-[var(--font-quicksand)] text-[0.72rem] font-semibold uppercase tracking-wider text-[var(--mc-text-muted)]"
            >
              Stato
            </th>
            <th
              scope="col"
              className="border-b border-[var(--mc-border)] px-3.5 py-2.5 text-right font-[var(--font-quicksand)] text-[0.72rem] font-semibold uppercase tracking-wider text-[var(--mc-text-muted)]"
            >
              Nav
            </th>
          </tr>
        </thead>
        <tbody>
          {sortedCards.map((card, i) => (
            <EntityTableRow
              key={card.id ?? `${card.entity}-${card.title}-${i}`}
              card={card}
              onRowClick={onRowClick}
              isLast={i === sortedCards.length - 1}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface SortableHeaderProps {
  label: string;
  column: EntityTableSortColumn;
  sortBy: EntityTableSortColumn;
  sortDirection: EntityTableSortDirection;
  onClick: (column: EntityTableSortColumn) => void;
}

function SortableHeader({ label, column, sortBy, sortDirection, onClick }: SortableHeaderProps) {
  const isActive = sortBy === column;
  const ariaSort = isActive ? (sortDirection === 'asc' ? 'ascending' : 'descending') : 'none';

  return (
    <th
      scope="col"
      aria-sort={ariaSort}
      className="border-b border-[var(--mc-border)] px-3.5 py-2.5 text-left font-[var(--font-quicksand)] text-[0.72rem] font-semibold uppercase tracking-wider text-[var(--mc-text-muted)]"
    >
      <button
        type="button"
        onClick={() => onClick(column)}
        className={`inline-flex items-center gap-1.5 uppercase tracking-wider transition-colors hover:text-[var(--mc-text-primary)] ${
          isActive ? 'text-[var(--mc-text-primary)]' : ''
        }`}
      >
        {label}
        <span
          aria-hidden
          className={`text-[9px] transition-opacity ${isActive ? 'opacity-100' : 'opacity-30'}`}
        >
          {isActive && sortDirection === 'desc' ? '▼' : '▲'}
        </span>
      </button>
    </th>
  );
}

interface EntityTableRowProps {
  card: MeepleCardProps;
  onRowClick?: (card: MeepleCardProps) => void;
  isLast: boolean;
}

function EntityTableRow({ card, onRowClick, isLast }: EntityTableRowProps) {
  const color = entityHsl(card.entity);
  const badgeBg = entityHsl(card.entity, 0.12);
  const borderBottomClass = isLast ? '' : 'border-b border-[var(--mc-border)]';
  const navItems = card.navItems ?? [];

  const handleClick = () => {
    if (onRowClick) onRowClick(card);
    else card.onClick?.();
  };

  const isInteractive = Boolean(onRowClick || card.onClick);

  return (
    <tr
      onClick={isInteractive ? handleClick : undefined}
      className={`group/row border-l-4 transition-colors hover:bg-black/[0.025] dark:hover:bg-white/[0.04] ${
        isInteractive ? 'cursor-pointer' : ''
      }`}
      style={{ borderLeftColor: color }}
      data-entity={card.entity}
    >
      {/* Titolo */}
      <td className={`px-3.5 py-2.5 ${borderBottomClass}`}>
        <div className="flex items-center gap-2.5">
          {card.imageUrl ? (
            // Same convention as Cover.tsx: plain <img> for arbitrary remote URLs.
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={card.imageUrl}
              alt=""
              className="h-8 w-8 flex-shrink-0 rounded-md object-cover"
              loading="lazy"
            />
          ) : (
            <div
              className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md text-sm"
              style={{ background: entityHsl(card.entity, 0.15), color }}
              aria-hidden
            >
              {entityIcon[card.entity]}
            </div>
          )}
          <div className="min-w-0">
            <div className="truncate font-[var(--font-quicksand)] text-[0.85rem] font-bold text-[var(--mc-text-primary)]">
              {card.title}
            </div>
            {card.subtitle && (
              <div className="truncate text-[0.68rem] text-[var(--mc-text-muted)]">
                {card.subtitle}
              </div>
            )}
          </div>
        </div>
      </td>

      {/* Tipo */}
      <td className={`px-3.5 py-2.5 ${borderBottomClass}`}>
        <span
          className="inline-flex rounded px-2 py-0.5 text-[0.62rem] font-bold uppercase tracking-wider"
          style={{ background: badgeBg, color }}
        >
          {entityLabel[card.entity]}
        </span>
      </td>

      {/* Dettagli */}
      <td className={`px-3.5 py-2.5 text-[var(--mc-text-secondary)] ${borderBottomClass}`}>
        {formatDetails(card)}
      </td>

      {/* Rating */}
      <td className={`px-3.5 py-2.5 ${borderBottomClass}`}>
        {card.rating !== undefined ? (
          <span className="inline-flex items-center gap-1 text-[var(--mc-text-primary)]">
            <span aria-hidden className="text-amber-500">
              ★
            </span>
            <span className="font-semibold">{card.rating.toFixed(1)}</span>
            {card.ratingMax && (
              <span className="text-xs text-[var(--mc-text-muted)]">/ {card.ratingMax}</span>
            )}
          </span>
        ) : (
          <span className="text-[var(--mc-text-muted)]" aria-label="Senza rating">
            —
          </span>
        )}
      </td>

      {/* Stato */}
      <td className={`px-3.5 py-2.5 ${borderBottomClass}`}>
        {card.status ? (
          <span
            className="font-semibold text-[0.78rem]"
            style={{ color: STATUS_COLOR[card.status] }}
          >
            {STATUS_LABELS[card.status]}
          </span>
        ) : (
          <span className="text-[var(--mc-text-muted)]">—</span>
        )}
      </td>

      {/* Nav */}
      <td className={`px-3.5 py-2.5 ${borderBottomClass}`}>
        <div className="flex items-center justify-end gap-1.5">
          {navItems.length > 0 ? (
            navItems.map((item, idx) => (
              <TableNavIcon
                key={`${item.label}-${idx}`}
                entity={item.entity}
                icon={item.icon}
                label={item.label}
                count={item.count}
                showPlus={item.showPlus}
                disabled={item.disabled}
                onClick={item.onClick}
              />
            ))
          ) : (
            <span className="text-[var(--mc-text-muted)]">—</span>
          )}
        </div>
      </td>
    </tr>
  );
}

interface TableNavIconProps {
  entity: MeepleEntityType;
  icon: React.ReactNode;
  label: string;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
}

function TableNavIcon({
  entity,
  icon,
  label,
  count,
  showPlus,
  disabled,
  onClick,
}: TableNavIconProps) {
  const color = entityHsl(entity);

  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={e => {
        e.stopPropagation();
        if (!disabled) onClick?.();
      }}
      className={`relative flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full border border-[var(--mc-nav-icon-border)] bg-[var(--mc-nav-icon-bg)] transition-all duration-200 hover:scale-[1.08] hover:shadow-[var(--mc-shadow-sm)] ${
        disabled ? 'cursor-not-allowed opacity-45' : ''
      }`}
      style={
        {
          '--mc-icon-color': color,
          color,
        } as React.CSSProperties
      }
    >
      <span className="pointer-events-none flex h-3 w-3 items-center justify-center [&>svg]:h-full [&>svg]:w-full">
        {icon}
      </span>
      {count !== undefined && count > 0 && (
        <span
          className="absolute -right-1 -top-1 flex h-3.5 min-w-3.5 items-center justify-center rounded-full px-[3px] text-[7px] font-bold text-white shadow-sm"
          style={{ background: color }}
        >
          {count > 99 ? '99+' : count}
        </span>
      )}
      {showPlus && !count && (
        <span
          className="absolute -right-1 -top-1 flex h-3.5 w-3.5 items-center justify-center rounded-full text-[8px] font-extrabold text-white shadow-sm"
          style={{ background: color }}
          aria-hidden
        >
          +
        </span>
      )}
    </button>
  );
}
