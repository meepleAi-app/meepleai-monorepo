/**
 * FeatureMiniCard — entity-colored mini card for Alpha-program highlights.
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 348-380.
 * Entity-typed accent (kb / agent / session) drives both the icon background
 * (alpha 0.14) and the kicker label color/background (alpha 0.12).
 *
 * Used in JoinHero (3-up grid on desktop, vertical stack on mobile).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import type { EntityType } from '@/components/ui/v2/entity-tokens';
import { entityHsl } from '@/lib/color-utils';

export interface FeatureMiniCardProps {
  readonly entity: EntityType;
  readonly emoji: string;
  readonly title: string;
  readonly description: string;
  readonly className?: string;
}

export function FeatureMiniCard({
  entity,
  emoji,
  title,
  description,
  className,
}: FeatureMiniCardProps): JSX.Element {
  return (
    <div
      data-slot="feature-mini-card"
      data-entity={entity}
      className={clsx(
        'flex h-full flex-col gap-1.5 p-3.5',
        'bg-card rounded-lg border border-border',
        'shadow-xs',
        className
      )}
    >
      <div
        aria-hidden="true"
        style={{ backgroundColor: entityHsl(entity, 0.14) }}
        className="flex h-[30px] w-[30px] items-center justify-center rounded-[9px] text-base"
      >
        {emoji}
      </div>
      <div className="font-display text-[13px] font-bold leading-tight text-foreground">
        {title}
      </div>
      <div className="text-[11px] leading-[1.5] text-[hsl(var(--text-sec))]">{description}</div>
      <div
        style={{
          backgroundColor: entityHsl(entity, 0.12),
          color: entityHsl(entity),
        }}
        className={clsx(
          'mt-auto self-start',
          'inline-flex px-1.5 py-0.5 rounded-sm',
          'font-mono text-[9px] font-bold uppercase tracking-[0.06em]'
        )}
      >
        {entity}
      </div>
    </div>
  );
}
