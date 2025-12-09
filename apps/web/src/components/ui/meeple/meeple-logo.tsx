/**
 * MeepleAI Logo Component
 * Custom SVG logo with playful meeple character
 * Supports light/dark theme variants
 */

import React from 'react';

interface MeepleLogoProps {
  variant?: 'full' | 'icon' | 'wordmark';
  size?: 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
  animated?: boolean;
}

type SizeKey = 'sm' | 'md' | 'lg' | 'xl';

const sizeMap: Record<SizeKey, { width: number; height: number; iconSize: number }> = {
  sm: { width: 120, height: 40, iconSize: 32 },
  md: { width: 160, height: 48, iconSize: 40 },
  lg: { width: 200, height: 60, iconSize: 48 },
  xl: { width: 280, height: 84, iconSize: 64 },
};

export function MeepleLogo({
  variant = 'full',
  size = 'md',
  className = '',
  animated = false,
}: MeepleLogoProps) {
  const { width, height, iconSize } = sizeMap[size as SizeKey];

  // Meeple icon (stylized game piece)
  const MeepleIcon = () => (
    <svg
      viewBox="0 0 100 100"
      width={iconSize}
      height={iconSize}
      className={animated ? 'meeple-icon-animated' : ''}
      aria-label="MeepleAI icon"
    >
      {/* Meeple body - inspired by classic board game pieces */}
      <g className="meeple-body">
        {/* Head */}
        <circle cx="50" cy="25" r="15" fill="url(#meepleGradient)" />

        {/* Body */}
        <path
          d="M 35 40 Q 35 45, 30 50 L 30 70 Q 30 75, 35 75 L 45 75 L 45 85 Q 45 90, 50 90 Q 55 90, 55 85 L 55 75 L 65 75 Q 70 75, 70 70 L 70 50 Q 65 45, 65 40 Z"
          fill="url(#meepleGradient)"
        />

        {/* Arms */}
        <ellipse cx="20" cy="50" rx="8" ry="12" fill="url(#meepleGradient)" />
        <ellipse cx="80" cy="50" rx="8" ry="12" fill="url(#meepleGradient)" />
      </g>

      {/* AI spark - small glowing dot */}
      <circle cx="50" cy="25" r="3" fill="#fbbf24" className="ai-spark">
        <animate attributeName="opacity" values="0.3;1;0.3" dur="2s" repeatCount="indefinite" />
      </circle>

      {/* Gradient definitions */}
      <defs>
        <linearGradient id="meepleGradient" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="var(--color-primary-500)" />
          <stop offset="100%" stopColor="var(--color-primary-700)" />
        </linearGradient>
      </defs>
    </svg>
  );

  // Wordmark (MeepleAI text)
  const Wordmark = () => (
    <div className="wordmark" style={{ marginLeft: variant === 'full' ? '12px' : 0 }}>
      <span className="text-display font-bold text-gradient-primary tracking-tight">Meeple</span>
      <span
        className="text-display font-bold tracking-tight"
        style={{ color: 'var(--color-secondary-500)' }}
      >
        AI
      </span>
    </div>
  );

  return (
    <div
      className={`meeple-logo ${className}`}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        width: variant === 'icon' ? iconSize : width,
        height: variant === 'icon' ? iconSize : height,
      }}
    >
      {variant === 'icon' && <MeepleIcon />}
      {variant === 'wordmark' && <Wordmark />}
      {variant === 'full' && (
        <>
          <MeepleIcon />
          <Wordmark />
        </>
      )}

      <style jsx>{`
        .meeple-logo {
          user-select: none;
        }

        .wordmark {
          display: flex;
          align-items: baseline;
          font-size: ${size === 'sm'
            ? '1.25rem'
            : size === 'md'
              ? '1.5rem'
              : size === 'lg'
                ? '2rem'
                : '2.5rem'};
        }

        .text-display {
          font-family: var(--font-display);
        }

        .meeple-icon-animated .meeple-body {
          transform-origin: center;
          animation: meeple-bounce 2s ease-in-out infinite;
        }

        .ai-spark {
          filter: blur(1px);
        }

        @keyframes meeple-bounce {
          0%,
          100% {
            transform: translateY(0) scale(1);
          }
          50% {
            transform: translateY(-4px) scale(1.02);
          }
        }

        .meeple-logo:hover .meeple-body {
          animation: meeple-wiggle 0.5s ease-in-out;
        }

        @keyframes meeple-wiggle {
          0%,
          100% {
            transform: rotate(0deg);
          }
          25% {
            transform: rotate(-5deg);
          }
          75% {
            transform: rotate(5deg);
          }
        }
      `}</style>
    </div>
  );
}
