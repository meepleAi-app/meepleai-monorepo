/**
 * CardNavigationFooter
 *
 * Renders a row of icon-link buttons below the MeepleCard metadata footer.
 * Each button navigates to a related entity page and uses the destination
 * entity's colour for hover effects.
 *
 * @see Issue #4689
 * @see config/entity-navigation.ts for the relationship graph
 */

'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';
import type { ResolvedNavigationLink } from '@/config/entity-navigation';

import { ENTITY_NAV_ICONS } from './navigation-icons';

// Re-export the entity colours so we can look up HSL at render time
// without importing the parent meeple-card module (avoids circular deps).
const NAV_ENTITY_HSL: Record<string, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  document: '210 40% 55%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  custom: '220 70% 50%',
};

interface CardNavigationFooterProps {
  links: ResolvedNavigationLink[];
  className?: string;
}

export function CardNavigationFooter({ links, className }: CardNavigationFooterProps) {
  if (links.length === 0) return null;

  return (
    <nav
      className={cn(
        'flex flex-col',
        'px-2.5 pt-[7px] pb-[9px]',
        'border-t border-border/60',
        'bg-muted/[0.28] dark:bg-muted/20',
        'rounded-b-2xl',
        className,
      )}
      aria-label="Navigate to related entities"
      data-testid="card-navigation-footer"
    >
      {/* "Navigate to" header with decorative lines */}
      <div className="flex items-center justify-center gap-1.5 mb-[5px]">
        <span className="flex-1 max-w-[35px] h-px bg-border/40" />
        <span className="text-[7px] font-bold tracking-[0.12em] uppercase text-muted-foreground/50">
          Navigate to
        </span>
        <span className="flex-1 max-w-[35px] h-px bg-border/40" />
      </div>

      {/* Navigation icon row */}
      <div className="flex justify-center gap-3">
        {links.map((link) => {
          // eslint-disable-next-line security/detect-object-injection -- entity from typed config
          const Icon = ENTITY_NAV_ICONS[link.entity] ?? ENTITY_NAV_ICONS.game;
          // eslint-disable-next-line security/detect-object-injection -- entity from typed config
          const hsl = NAV_ENTITY_HSL[link.entity] ?? NAV_ENTITY_HSL.custom;

          return (
            <Link
              key={`${link.entity}-${link.href}`}
              href={link.href}
              className={cn(
                'group/nav flex flex-col items-center gap-0.5',
                'no-underline transition-all duration-200',
              )}
              title={link.label}
              onClick={(e) => e.stopPropagation()}
            >
              <span
                className={cn(
                  'flex items-center justify-center',
                  'w-7 h-7 rounded-full',
                  'bg-card/60 dark:bg-card/40',
                  'border border-border/30',
                  'backdrop-blur-sm',
                  'transition-all duration-200',
                  'group-hover/nav:scale-110',
                )}
                style={{
                  // Entity-coloured hover glow
                  '--nav-hsl': hsl,
                } as React.CSSProperties}
                // Dynamic hover styles via inline + tailwind arbitrary
                // We rely on a parent hover to set bg/border/shadow
                data-entity={link.entity}
              >
                <Icon className="w-3.5 h-3.5 text-muted-foreground transition-colors duration-200 group-hover/nav:text-[hsl(var(--nav-hsl))]" />
              </span>
              <span className="text-[8px] font-nunito font-bold uppercase tracking-[0.04em] text-muted-foreground/70 transition-colors duration-200 group-hover/nav:text-[hsl(var(--nav-hsl))]">
                {link.label}
              </span>
            </Link>
          );
        })}
      </div>

      {/* Global hover styles for the nav buttons via CSS */}
      <style>{`
        [data-testid="card-navigation-footer"] .group\\/nav:hover [data-entity] {
          background: hsl(var(--nav-hsl) / 0.12);
          border-color: hsl(var(--nav-hsl) / 0.4);
          box-shadow: 0 0 10px hsl(var(--nav-hsl) / 0.2);
        }
      `}</style>
    </nav>
  );
}
