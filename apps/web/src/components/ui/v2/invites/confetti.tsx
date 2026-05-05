/**
 * Confetti — CSS-only celebratory animation per `accepted-success` state.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 422-448
 * (14 particles via useMemo, randomized left%/delay/duration/color/size/rotate).
 *
 * **Reduced-motion compliance** (critique fix HIGH#3 from spec-panel review):
 * particles disabled via `motion-reduce:hidden` Tailwind class. The confetti is
 * pure decoration — no information is lost when animation is disabled. Users
 * still see the success state (🎲 icon, "Ci sei!" heading, summary card).
 *
 * GPU-only animation: `transform` + `opacity` only (no layout-triggering props
 * like `top` or `width` in keyframes). Falls in 60 FPS budget on mid-range
 * mobile per browser-engine compositor pipeline.
 *
 * Particles randomization seeded once via `useMemo([])` so re-renders during
 * mutation success (e.g. data refetch) do NOT re-trigger animation.
 */

'use client';

import { Fragment, useMemo } from 'react';
import type { CSSProperties, JSX } from 'react';

const ENTITY_TOKENS = ['c-game', 'c-event', 'c-toolkit', 'c-player', 'c-agent'] as const;

interface Particle {
  readonly id: number;
  readonly left: number;
  readonly delay: number;
  readonly duration: number;
  readonly color: (typeof ENTITY_TOKENS)[number];
  readonly size: number;
  readonly rotate: number;
  readonly translateX: number;
}

export interface ConfettiProps {
  readonly count?: number;
}

export function Confetti({ count = 14 }: ConfettiProps): JSX.Element {
  const particles = useMemo<Particle[]>(
    () =>
      Array.from({ length: count }, (_, i) => ({
        id: i,
        left: 5 + Math.random() * 90,
        delay: Math.random() * 0.4,
        duration: 1.4 + Math.random() * 1.0,
        color: ENTITY_TOKENS[i % ENTITY_TOKENS.length],
        size: 6 + Math.random() * 6,
        rotate: Math.random() * 360,
        translateX: (Math.random() - 0.5) * 120,
      })),
    [count]
  );

  return (
    <Fragment>
      <div
        data-testid="invite-confetti"
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 overflow-hidden motion-reduce:hidden"
      >
        {particles.map(p => {
          const style: CSSProperties = {
            position: 'absolute',
            top: '-12px',
            left: `${p.left}%`,
            width: `${p.size}px`,
            height: `${p.size}px`,
            background: `hsl(var(--${p.color}))`,
            borderRadius: p.id % 2 === 0 ? '50%' : '2px',
            transform: `rotate(${p.rotate}deg)`,
            // Custom property used inside keyframe via var(--mai-cx) for horizontal drift.
            ['--mai-cx' as string]: `${p.translateX}px`,
            animation: `mai-invite-confetti ${p.duration}s cubic-bezier(.4,.7,.6,1) ${p.delay}s forwards`,
          };
          return <span key={p.id} style={style} />;
        })}
      </div>
      <style>{`
        @keyframes mai-invite-confetti {
          0%   { transform: translate3d(0, 0, 0) rotate(0deg); opacity: 0; }
          10%  { opacity: 1; }
          100% { transform: translate3d(var(--mai-cx, 0), 280px, 0) rotate(540deg); opacity: 0; }
        }
      `}</style>
    </Fragment>
  );
}
