'use client';

import Link from 'next/link';

import { AccentBorder } from '../parts/AccentBorder';
import { Cover } from '../parts/Cover';
import { MetaChips } from '../parts/MetaChips';
import { Rating } from '../parts/Rating';
import { entityHsl } from '../tokens';

import type { MeepleCardProps } from '../types';

/** Full-width entity header card. NavItems render as horizontal chips. */
export function FocusCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    metadata = [],
    navItems = [],
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  return (
    <div
      className={`relative w-full overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)] backdrop-blur-[12px] ${className}`}
      data-entity={entity}
      data-testid={testId}
      onClick={onClick}
    >
      <AccentBorder entity={entity} />

      {/* Hero row: cover + info */}
      <div className="flex gap-4 p-4">
        <div className="h-24 w-24 shrink-0 overflow-hidden rounded-xl">
          <Cover entity={entity} variant="compact" imageUrl={imageUrl} alt={title} />
        </div>

        <div className="flex min-w-0 flex-1 flex-col justify-center gap-1">
          <h2 className="font-[var(--font-quicksand)] text-xl font-bold leading-tight text-[var(--mc-text-primary)]">
            {title}
          </h2>
          {subtitle && <p className="text-sm text-[var(--mc-text-secondary)]">{subtitle}</p>}
          {rating !== undefined && <Rating value={rating} max={ratingMax} />}
          {metadata.length > 0 && <MetaChips metadata={metadata} />}
        </div>
      </div>

      {/* NavItem chip row */}
      {navItems.length > 0 && (
        <div className="flex flex-wrap gap-2 border-t border-[var(--mc-border-light)] px-4 py-3">
          {navItems.map((item, i) => {
            const color = entityHsl(item.entity);
            const chipContent = (
              <>
                <span className="text-[13px] leading-none">{item.icon}</span>
                <span className="text-xs font-semibold">{item.label}</span>
                {item.count !== undefined && item.count > 0 && (
                  <span
                    className="ml-0.5 rounded-full px-1.5 py-0.5 text-[9px] font-bold text-white"
                    style={{ background: color }}
                  >
                    {item.count}
                  </span>
                )}
              </>
            );

            const chipClass =
              'flex items-center gap-1.5 rounded-xl border px-2.5 py-1.5 text-[var(--mc-text-primary)] transition-all duration-200 hover:scale-[1.03] active:scale-95';
            const chipStyle = {
              borderColor: entityHsl(item.entity, 0.3),
              background: entityHsl(item.entity, 0.06),
            };

            if (item.href && !item.disabled) {
              return (
                <Link
                  key={i}
                  href={item.href}
                  className={chipClass}
                  style={chipStyle}
                  onClick={e => {
                    e.stopPropagation();
                    item.onClick?.();
                  }}
                  aria-label={item.label}
                >
                  {chipContent}
                </Link>
              );
            }

            return (
              <button
                key={i}
                type="button"
                onClick={e => {
                  e.stopPropagation();
                  if (!item.disabled) item.onClick?.();
                }}
                disabled={item.disabled}
                className={`${chipClass} ${item.disabled ? 'cursor-not-allowed opacity-40' : ''}`}
                style={chipStyle}
                aria-label={item.label}
              >
                {chipContent}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
