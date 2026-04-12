'use client';

import type { KeyboardEvent } from 'react';

import Link from 'next/link';

import { entityHsl } from '../tokens';

import type { NavFooterItem } from '../types';

interface NavFooterProps {
  items: NavFooterItem[];
  size?: 'sm' | 'md';
}

export function NavFooter({ items, size = 'sm' }: NavFooterProps) {
  if (items.length === 0) return null;

  const iconSize = size === 'md' ? 'h-8 w-8 text-[15px]' : 'h-7 w-7 text-[13px]';

  return (
    <div className="flex items-center justify-center gap-2 border-t border-[var(--mc-border-light)] bg-[var(--mc-nav-footer-bg)] px-2.5 py-1.5 backdrop-blur-lg">
      {items.map((item, i) => {
        const color = entityHsl(item.entity);
        const glowColor = entityHsl(item.entity, 0.15);
        const borderHover = entityHsl(item.entity, 0.4);

        const handleActivate = () => {
          if (item.disabled) return;
          item.onClick?.();
        };

        const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
          if (item.disabled) return;
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            e.stopPropagation();
            item.onClick?.();
          }
        };

        const innerContent = (
          <>
            <div
              className={`relative flex ${iconSize} items-center justify-center rounded-full border border-[var(--mc-nav-icon-border)] bg-[var(--mc-nav-icon-bg)] transition-all duration-200 group-hover/nav:scale-[1.08] group-hover/nav:border-[var(--nav-hover-border)] group-hover/nav:bg-[var(--nav-hover-bg)] group-hover/nav:shadow-[var(--nav-hover-shadow)] group-active/nav:scale-95 group-active/nav:shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)]`}
              style={
                {
                  '--nav-hover-bg': entityHsl(item.entity, 0.08),
                  '--nav-hover-shadow': `0 2px 8px ${glowColor}`,
                } as React.CSSProperties
              }
            >
              <span className="pointer-events-none flex h-3.5 w-3.5 items-center justify-center [&>svg]:h-full [&>svg]:w-full">
                {item.icon}
              </span>

              {item.count !== undefined && item.count > 0 && (
                <span
                  className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full px-[3px] text-[8px] font-bold text-white shadow-sm"
                  style={{ background: color }}
                >
                  {item.count > 99 ? '99+' : item.count}
                </span>
              )}
            </div>

            {item.showPlus && (
              <button
                type="button"
                onClick={e => {
                  e.stopPropagation();
                  item.onPlusClick?.();
                }}
                className="absolute -bottom-0.5 right-1 flex h-3.5 w-3.5 items-center justify-center rounded-full text-[8px] font-extrabold text-white shadow-sm"
                style={{ background: color }}
                aria-label={`Aggiungi ${item.label}`}
              >
                +
              </button>
            )}

            <span className="text-[7px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)] transition-colors group-hover/nav:text-[var(--mc-text-secondary)]">
              {item.label}
            </span>
          </>
        );

        const commonClassName = `group/nav relative flex flex-col items-center gap-0.5 outline-none focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:ring-[var(--nav-hover-border)] ${
          item.disabled ? 'cursor-not-allowed opacity-45' : 'cursor-pointer'
        }`;
        const commonStyle = { '--nav-hover-border': borderHover } as React.CSSProperties;

        if (item.href && !item.disabled) {
          return (
            <Link
              key={i}
              href={item.href}
              className={commonClassName}
              style={commonStyle}
              aria-label={item.label}
              title={item.label}
              onClick={e => {
                e.stopPropagation();
                item.onClick?.();
              }}
            >
              {innerContent}
            </Link>
          );
        }

        return (
          <div
            key={i}
            role="button"
            tabIndex={item.disabled ? -1 : 0}
            aria-disabled={item.disabled}
            aria-label={item.label}
            title={item.label}
            onClick={e => {
              e.stopPropagation();
              handleActivate();
            }}
            onKeyDown={handleKeyDown}
            className={commonClassName}
            style={commonStyle}
          >
            {innerContent}
          </div>
        );
      })}
    </div>
  );
}
