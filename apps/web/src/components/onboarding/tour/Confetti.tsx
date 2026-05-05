import type { CSSProperties, JSX } from 'react';
import { Fragment, useMemo } from 'react';

const COLORS = [
  'hsl(25 95% 58%)',
  'hsl(38 92% 62%)',
  'hsl(240 62% 68%)',
  'hsl(262 68% 68%)',
  'hsl(350 85% 68%)',
  'hsl(142 60% 55%)',
  'hsl(195 76% 58%)',
  'hsl(174 62% 52%)',
];

export interface ConfettiProps {
  readonly count?: number;
}

interface Piece {
  readonly id: number;
  readonly left: number;
  readonly color: string;
  readonly delay: number;
  readonly dur: number;
  readonly size: number;
  readonly rot: number;
  readonly wide: boolean;
}

export function Confetti({ count = 40 }: ConfettiProps): JSX.Element {
  const pieces = useMemo<Piece[]>(
    () =>
      Array.from({ length: count }, (_, i) => ({
        id: i,
        left: 5 + Math.random() * 90,
        color: COLORS[i % COLORS.length],
        delay: Math.random() * 0.9,
        dur: 2.2 + Math.random() * 1.6,
        size: Math.round(6 + Math.random() * 9),
        rot: Math.round(Math.random() * 360),
        wide: Math.random() > 0.5,
      })),
    [count]
  );

  return (
    <Fragment>
      <div
        data-testid="onboarding-confetti"
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 overflow-hidden motion-reduce:hidden"
      >
        {pieces.map(p => {
          const style: CSSProperties = {
            position: 'absolute',
            top: '-10%',
            left: `${p.left}%`,
            width: p.wide ? `${p.size * 1.6}px` : `${p.size}px`,
            height: p.wide ? `${p.size * 0.55}px` : `${p.size}px`,
            borderRadius: p.wide ? '2px' : '50%',
            background: p.color,
            transform: `rotate(${p.rot}deg)`,
            animation: `onboarding-confetti-fall ${p.dur}s ${p.delay}s linear forwards`,
          };
          return <span key={p.id} style={style} />;
        })}
      </div>
      <style>{`
        @keyframes onboarding-confetti-fall {
          0% { transform: translateY(0) rotate(0deg); opacity: 1; }
          100% { transform: translateY(110vh) rotate(720deg); opacity: 0; }
        }
      `}</style>
    </Fragment>
  );
}
