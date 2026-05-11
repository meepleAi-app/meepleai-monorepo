/**
 * AlphaPill — small badge announcing the private Alpha program.
 *
 * Wave A.2 (Issue #589). Mirrors mockup `sp3-join.jsx` lines 56-69.
 * Event-color dot + uppercase mono label, designed to sit above the JoinHero
 * headline. Pure presentational — no a11y role; the surrounding heading
 * provides context.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/lib/color-utils';

export interface AlphaPillProps {
  readonly label: string;
  readonly className?: string;
}

export function AlphaPill({ label, className }: AlphaPillProps): JSX.Element {
  return (
    <span
      data-slot="alpha-pill"
      style={{
        backgroundColor: entityHsl('event', 0.14),
        color: entityHsl('event'),
      }}
      className={clsx(
        'inline-flex items-center gap-1.5',
        'px-2.5 py-1 rounded-full',
        'font-mono text-[10px] font-bold uppercase tracking-[0.08em]',
        className
      )}
    >
      <span
        aria-hidden="true"
        style={{ backgroundColor: entityHsl('event') }}
        className="block h-1.5 w-1.5 rounded-full"
      />
      {label}
    </span>
  );
}
