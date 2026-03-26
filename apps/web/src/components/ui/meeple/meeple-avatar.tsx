/**
 * MeepleAvatar Component - AI Chat Avatar with 5 States
 *
 * Displays an animated meeple character representing the AI assistant
 * with different visual states to communicate AI activity/confidence.
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (lines 358-389)
 * @see docs/04-frontend/improvements/03-brainstorm-ideas.md (#2.1 AI Avatar)
 */

import React from 'react';

import { cn } from '@/lib/utils';

export type MeepleAvatarState = 'idle' | 'thinking' | 'confident' | 'searching' | 'uncertain';
export type MeepleAvatarSize = 'sm' | 'md' | 'lg';

interface MeepleAvatarProps {
  /** Current AI state to display */
  state: MeepleAvatarState;
  /** Size variant */
  size?: MeepleAvatarSize;
  /** Additional CSS classes */
  className?: string;
  /** Custom ARIA label (auto-generated if not provided) */
  ariaLabel?: string;
}

const SIZE_MAP: Record<MeepleAvatarSize, { width: number; height: number; className: string }> = {
  sm: { width: 32, height: 32, className: 'w-8 h-8' },
  md: { width: 40, height: 40, className: 'w-10 h-10' },
  lg: { width: 48, height: 48, className: 'w-12 h-12' },
};

const STATE_LABELS: Record<MeepleAvatarState, string> = {
  idle: 'AI assistant ready',
  thinking: 'AI assistant thinking',
  confident: 'AI assistant confident',
  searching: 'AI assistant searching',
  uncertain: 'AI assistant uncertain',
};

// Predefined gradient IDs for security (prevents XSS via dynamic ID injection)
const GRADIENT_IDS: Record<MeepleAvatarState, string> = {
  idle: 'meeple-gradient-idle',
  thinking: 'meeple-gradient-thinking',
  confident: 'meeple-gradient-confident',
  searching: 'meeple-gradient-searching',
  uncertain: 'meeple-gradient-uncertain',
} as const;

/**
 * MeepleAvatar - Animated AI assistant avatar
 *
 * @example
 * ```tsx
 * // Thinking state during AI processing
 * <MeepleAvatar state="thinking" size="md" />
 *
 * // Confident state for high-confidence answers
 * <MeepleAvatar state="confident" size="lg" />
 * ```
 */
export const MeepleAvatar = React.forwardRef<HTMLDivElement, MeepleAvatarProps>(
  ({ state, size = 'md', className, ariaLabel }, ref) => {
    const { width, height, className: sizeClass } = SIZE_MAP[size];
    const label = ariaLabel || STATE_LABELS[state];
    const gradientId = GRADIENT_IDS[state];

    return (
      <div
        ref={ref}
        className={cn(
          'meeple-avatar relative inline-flex items-center justify-center shrink-0',
          sizeClass,
          className
        )}
        role="img"
        aria-label={label}
      >
        <svg
          viewBox="0 0 100 100"
          width={width}
          height={height}
          className={cn('meeple-avatar-svg', `state-${state}`)}
          xmlns="http://www.w3.org/2000/svg"
        >
          {/* Gradient definitions */}
          <defs>
            <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#f97316" /> {/* Primary Orange */}
              <stop offset="100%" stopColor="#ea580c" /> {/* Darker Orange */}
            </linearGradient>

            {/* Glow filter for confident state */}
            <filter id="glow">
              <feGaussianBlur stdDeviation="2" result="coloredBlur" />
              <feMerge>
                <feMergeNode in="coloredBlur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>
          </defs>

          {/* Meeple body - classic board game piece shape */}
          <g className="meeple-body">
            {/* Head */}
            <circle cx="50" cy="25" r="15" fill={`url(#${gradientId})`} />

            {/* Body */}
            <path
              d="M 35 40 Q 35 45, 30 50 L 30 70 Q 30 75, 35 75 L 45 75 L 45 85 Q 45 90, 50 90 Q 55 90, 55 85 L 55 75 L 65 75 Q 70 75, 70 70 L 70 50 Q 65 45, 65 40 Z"
              fill={`url(#${gradientId})`}
            />

            {/* Arms */}
            <ellipse cx="20" cy="50" rx="8" ry="12" fill={`url(#${gradientId})`} />
            <ellipse cx="80" cy="50" rx="8" ry="12" fill={`url(#${gradientId})`} />
          </g>

          {/* State-specific decorations */}
          {state === 'thinking' && (
            <g className="thinking-dots">
              <circle cx="35" cy="30" r="2" fill="#fbbf24" className="dot dot-1">
                <animate
                  attributeName="opacity"
                  values="0.3;1;0.3"
                  dur="1.4s"
                  begin="0s"
                  repeatCount="indefinite"
                />
              </circle>
              <circle cx="50" cy="30" r="2" fill="#fbbf24" className="dot dot-2">
                <animate
                  attributeName="opacity"
                  values="0.3;1;0.3"
                  dur="1.4s"
                  begin="0.2s"
                  repeatCount="indefinite"
                />
              </circle>
              <circle cx="65" cy="30" r="2" fill="#fbbf24" className="dot dot-3">
                <animate
                  attributeName="opacity"
                  values="0.3;1;0.3"
                  dur="1.4s"
                  begin="0.4s"
                  repeatCount="indefinite"
                />
              </circle>
            </g>
          )}

          {state === 'confident' && (
            <g className="confident-sparkles" filter="url(#glow)">
              <circle cx="35" cy="15" r="2" fill="#fbbf24">
                <animate attributeName="opacity" values="0;1;0" dur="2s" repeatCount="indefinite" />
              </circle>
              <circle cx="65" cy="15" r="2" fill="#fbbf24">
                <animate
                  attributeName="opacity"
                  values="0;1;0"
                  dur="2s"
                  begin="0.5s"
                  repeatCount="indefinite"
                />
              </circle>
              <circle cx="25" cy="35" r="1.5" fill="#fbbf24">
                <animate
                  attributeName="opacity"
                  values="0;1;0"
                  dur="2s"
                  begin="1s"
                  repeatCount="indefinite"
                />
              </circle>
              <circle cx="75" cy="35" r="1.5" fill="#fbbf24">
                <animate
                  attributeName="opacity"
                  values="0;1;0"
                  dur="2s"
                  begin="1.5s"
                  repeatCount="indefinite"
                />
              </circle>
            </g>
          )}

          {state === 'searching' && (
            <g className="searching-icon">
              {/* Magnifying glass */}
              <circle cx="70" cy="25" r="6" fill="none" stroke="#fbbf24" strokeWidth="2" />
              <line
                x1="74"
                y1="29"
                x2="78"
                y2="33"
                stroke="#fbbf24"
                strokeWidth="2"
                strokeLinecap="round"
              />
              <animateTransform
                attributeName="transform"
                type="rotate"
                from="0 70 25"
                to="360 70 25"
                dur="3s"
                repeatCount="indefinite"
              />
            </g>
          )}

          {state === 'uncertain' && (
            <g className="uncertain-icon">
              {/* Question mark */}
              <text
                x="70"
                y="32"
                fontSize="16"
                fill="#fbbf24"
                fontWeight="bold"
                textAnchor="middle"
              >
                ?
              </text>
            </g>
          )}

          {/* Idle state - subtle pulse (handled via CSS) */}
        </svg>

        <style jsx>{`
          .meeple-avatar {
            user-select: none;
          }

          .meeple-avatar-svg {
            display: block;
          }

          /* Idle state - subtle pulse */
          .state-idle .meeple-body {
            animation: idle-pulse 3s ease-in-out infinite;
          }

          @keyframes idle-pulse {
            0%,
            100% {
              opacity: 1;
            }
            50% {
              opacity: 0.85;
            }
          }

          /* Thinking state - body subtle bounce */
          .state-thinking .meeple-body {
            animation: thinking-bounce 1.4s ease-in-out infinite;
            transform-origin: center bottom;
          }

          @keyframes thinking-bounce {
            0%,
            100% {
              transform: translateY(0) scale(1);
            }
            50% {
              transform: translateY(-2px) scale(1.02);
            }
          }

          /* Confident state - slight scale up */
          .state-confident .meeple-body {
            transform: scale(1.05);
            filter: url(#glow);
          }

          /* Uncertain state - subtle shake */
          .state-uncertain .meeple-body {
            animation: uncertain-shake 0.5s ease-in-out infinite;
          }

          @keyframes uncertain-shake {
            0%,
            100% {
              transform: translateX(0);
            }
            25% {
              transform: translateX(-1px);
            }
            75% {
              transform: translateX(1px);
            }
          }

          /* Respect prefers-reduced-motion */
          @media (prefers-reduced-motion: reduce) {
            .meeple-avatar-svg * {
              animation: none !important;
              transition: none !important;
            }

            .state-idle .meeple-body,
            .state-thinking .meeple-body,
            .state-uncertain .meeple-body {
              animation: none !important;
            }

            .thinking-dots circle,
            .confident-sparkles circle,
            .searching-icon animateTransform {
              display: none;
            }
          }
        `}</style>
      </div>
    );
  }
);

MeepleAvatar.displayName = 'MeepleAvatar';
