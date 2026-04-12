'use client';

import type { ReactNode } from 'react';

import { entityHsl, entityLabel } from '../tokens';

import type { MeepleEntityType } from '../types';

/**
 * Flip card back content — entity-colored header + composable sections.
 *
 * Used in the MeepleCard flip flow: front shows the standard card, clicking
 * the flip control reveals this back. Each entity type provides its own
 * sections payload — see the /dev/meeple-card showcase for 8 worked examples
 * matching the acceptance criteria in the design brief:
 *
 * - game      → generic description ('text')
 * - toolkit   → actions for each tool bound to the parent game ('actions')
 * - chat      → history of other chats with the same agent ('list')
 * - kb        → rapid rule summary (rows + text mix)
 * - agent     → most useful info rows
 * - session   → management action list
 * - player    → info rows + social links
 * - tool      → history of past results
 */

export type FlipBackSection =
  | { kind: 'text'; text: string }
  | { kind: 'rows'; title?: string; rows: Array<[string, string]> }
  | {
      kind: 'actions';
      title?: string;
      items: Array<{
        label: string;
        meta?: string;
        icon?: ReactNode;
        disabled?: boolean;
        onClick?: () => void;
      }>;
    }
  | {
      kind: 'list';
      title?: string;
      items: Array<{ title: string; subtitle?: string; meta?: string }>;
    }
  | {
      kind: 'social';
      title?: string;
      links: Array<{ platform: string; handle?: string; icon: ReactNode; href?: string }>;
    };

export interface FlipBackProps {
  entity: MeepleEntityType;
  title: string;
  subtitle?: string;
  sections: FlipBackSection[];
  /** Optional text rendered at the very bottom of the back. */
  footer?: ReactNode;
  className?: string;
}

export function FlipBack({
  entity,
  title,
  subtitle,
  sections,
  footer,
  className = '',
}: FlipBackProps) {
  const accent = entityHsl(entity);

  return (
    <div
      className={`flex h-full flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] ${className}`}
    >
      {/* Entity-colored header with diagonal stripe pattern */}
      <div
        className="relative flex-shrink-0 px-4 py-3"
        style={{
          background: accent,
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 10px, rgba(255,255,255,0.05) 10px, rgba(255,255,255,0.05) 20px)',
        }}
      >
        <h3 className="font-[var(--font-quicksand)] text-[0.95rem] font-bold text-white drop-shadow-sm">
          {title}
        </h3>
        {subtitle && <p className="text-[0.7rem] text-white/85">{subtitle}</p>}
        <span
          aria-hidden
          className="absolute right-3 top-3 rounded-full border border-white/30 bg-white/15 px-2 py-0.5 text-[8px] font-bold uppercase tracking-wide text-white backdrop-blur-sm"
        >
          {entityLabel[entity]}
        </span>
      </div>

      {/* Sections body — scrollable */}
      <div className="flex-1 overflow-y-auto px-3 py-2">
        {sections.map((section, idx) => (
          <FlipBackSectionView key={idx} entity={entity} section={section} />
        ))}
      </div>

      {footer && (
        <div className="flex-shrink-0 border-t border-[var(--mc-border)] px-3 py-1.5 text-[9px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)]">
          {footer}
        </div>
      )}
    </div>
  );
}

interface FlipBackSectionViewProps {
  entity: MeepleEntityType;
  section: FlipBackSection;
}

function FlipBackSectionView({ entity, section }: FlipBackSectionViewProps) {
  const accent = entityHsl(entity);

  switch (section.kind) {
    case 'text':
      return (
        <p className="mb-2 text-[0.78rem] leading-relaxed text-[var(--mc-text-secondary)]">
          {section.text}
        </p>
      );

    case 'rows':
      return (
        <div className="mb-2">
          {section.title && <SectionTitle label={section.title} />}
          {section.rows.map(([label, value]) => (
            <div
              key={label}
              className="flex items-center justify-between border-b border-[var(--mc-border-light)] py-1.5 text-[0.7rem] last:border-0"
            >
              <span className="font-semibold text-[var(--mc-text-secondary)]">{label}</span>
              <span className="max-w-[55%] truncate text-right text-[var(--mc-text-primary)]">
                {value}
              </span>
            </div>
          ))}
        </div>
      );

    case 'actions':
      return (
        <div className="mb-2">
          {section.title && <SectionTitle label={section.title} />}
          <ul className="space-y-1">
            {section.items.map(({ label, meta, icon, disabled, onClick }, i) => (
              <li key={`${label}-${i}`}>
                <button
                  type="button"
                  disabled={disabled}
                  onClick={e => {
                    e.stopPropagation();
                    if (!disabled) onClick?.();
                  }}
                  className={`flex w-full items-center gap-2 rounded-md border border-[var(--mc-border-light)] px-2 py-1.5 text-left text-[0.72rem] transition-colors hover:border-[var(--mc-border)] hover:bg-black/[0.03] dark:hover:bg-white/[0.04] ${
                    disabled ? 'cursor-not-allowed opacity-45' : 'cursor-pointer'
                  }`}
                  style={{ borderLeftWidth: 3, borderLeftColor: accent }}
                >
                  {icon && (
                    <span
                      className="flex h-5 w-5 flex-shrink-0 items-center justify-center rounded text-sm"
                      style={{ background: entityHsl(entity, 0.12), color: accent }}
                    >
                      {icon}
                    </span>
                  )}
                  <span className="flex-1 font-semibold text-[var(--mc-text-primary)]">
                    {label}
                  </span>
                  {meta && (
                    <span className="text-[0.65rem] text-[var(--mc-text-muted)]">{meta}</span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        </div>
      );

    case 'list':
      return (
        <div className="mb-2">
          {section.title && <SectionTitle label={section.title} />}
          <ul className="space-y-0.5">
            {section.items.map((item, i) => (
              <li
                key={`${item.title}-${i}`}
                className="flex items-start justify-between gap-2 border-b border-[var(--mc-border-light)] py-1.5 text-[0.7rem] last:border-0"
              >
                <div className="min-w-0 flex-1">
                  <div className="truncate font-semibold text-[var(--mc-text-primary)]">
                    {item.title}
                  </div>
                  {item.subtitle && (
                    <div className="truncate text-[0.65rem] text-[var(--mc-text-muted)]">
                      {item.subtitle}
                    </div>
                  )}
                </div>
                {item.meta && (
                  <span className="flex-shrink-0 text-[0.65rem] font-semibold" style={{ color: accent }}>
                    {item.meta}
                  </span>
                )}
              </li>
            ))}
          </ul>
        </div>
      );

    case 'social':
      return (
        <div className="mb-2">
          {section.title && <SectionTitle label={section.title} />}
          <div className="flex flex-wrap gap-1.5">
            {section.links.map(({ platform, handle, icon, href }, i) => {
              const content = (
                <>
                  <span
                    className="flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full text-sm"
                    style={{ background: entityHsl(entity, 0.12), color: accent }}
                  >
                    {icon}
                  </span>
                  <span className="text-[0.7rem] font-semibold text-[var(--mc-text-primary)]">
                    {platform}
                  </span>
                  {handle && (
                    <span className="text-[0.62rem] text-[var(--mc-text-muted)]">{handle}</span>
                  )}
                </>
              );

              const classes =
                'flex items-center gap-1.5 rounded-full border border-[var(--mc-border-light)] px-2 py-1 transition-colors hover:border-[var(--mc-border)] hover:bg-black/[0.03] dark:hover:bg-white/[0.04]';

              return href ? (
                <a
                  key={`${platform}-${i}`}
                  href={href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={classes}
                  onClick={e => e.stopPropagation()}
                >
                  {content}
                </a>
              ) : (
                <span key={`${platform}-${i}`} className={classes}>
                  {content}
                </span>
              );
            })}
          </div>
        </div>
      );
  }
}

function SectionTitle({ label }: { label: string }) {
  return (
    <div className="mb-1 text-[0.6rem] font-bold uppercase tracking-wider text-[var(--mc-text-muted)]">
      {label}
    </div>
  );
}
