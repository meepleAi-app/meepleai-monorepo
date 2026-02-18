/**
 * DiceIcon3D - Isometric 3D Dice Icon
 *
 * SVG component for MeepleCard v2 placeholder imagery.
 * Used when game/entity images are unavailable.
 *
 * @module components/ui/icons/dice-icon-3d
 * @see Issue #4604 - MeepleCard v2 Migration
 *
 * Features:
 * - Isometric 3D perspective (3 visible faces)
 * - Purple/pink gradient coloring
 * - Subtle rotation on hover
 * - Responsive sizing (sm/md/lg)
 * - Fully accessible
 *
 * @example
 * ```tsx
 * // Default size (md = 80px)
 * <DiceIcon3D />
 *
 * // Small placeholder (40px)
 * <DiceIcon3D size="sm" className="opacity-60" />
 *
 * // Large hero (120px)
 * <DiceIcon3D size="lg" className="drop-shadow-2xl" />
 * ```
 */

import React from 'react';

import { cn } from '@/lib/utils';

export interface DiceIcon3DProps {
  /** Size variant */
  size?: 'sm' | 'md' | 'lg';
  /** Additional CSS classes */
  className?: string;
  /** Disable hover rotation */
  static?: boolean;
}

const sizeMap = {
  sm: 40,
  md: 80,
  lg: 120,
} as const;

/**
 * Isometric 3D dice icon with gradient coloring
 */
export function DiceIcon3D({
  size = 'md',
  className,
  static: isStatic = false,
}: DiceIcon3DProps) {
  const dimension = sizeMap[size];

  return (
    <svg
      width={dimension}
      height={dimension}
      viewBox="0 0 100 100"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={cn(
        'transition-transform duration-500 ease-out',
        !isStatic && 'group-hover:rotate-12',
        className
      )}
      aria-hidden="true"
      role="presentation"
    >
      {/* Gradient Definitions */}
      <defs>
        {/* Top face gradient (lightest - purple) */}
        <linearGradient id="dice-top" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="hsl(280, 65%, 75%)" />
          <stop offset="100%" stopColor="hsl(280, 60%, 65%)" />
        </linearGradient>

        {/* Left face gradient (medium - purple/pink) */}
        <linearGradient id="dice-left" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="hsl(280, 60%, 65%)" />
          <stop offset="100%" stopColor="hsl(300, 55%, 55%)" />
        </linearGradient>

        {/* Right face gradient (darkest - pink) */}
        <linearGradient id="dice-right" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="hsl(300, 55%, 55%)" />
          <stop offset="100%" stopColor="hsl(320, 50%, 50%)" />
        </linearGradient>

        {/* Dot shadow */}
        <filter id="dot-shadow">
          <feDropShadow dx="0" dy="1" stdDeviation="1" floodOpacity="0.3" />
        </filter>
      </defs>

      {/* Isometric Dice Body */}
      {/* Top face (facing up-right) */}
      <path
        d="M 50 15 L 80 30 L 50 45 L 20 30 Z"
        fill="url(#dice-top)"
        stroke="rgba(255,255,255,0.2)"
        strokeWidth="0.5"
      />

      {/* Left face (facing left-down) */}
      <path
        d="M 20 30 L 50 45 L 50 85 L 20 70 Z"
        fill="url(#dice-left)"
        stroke="rgba(0,0,0,0.1)"
        strokeWidth="0.5"
      />

      {/* Right face (facing right-down) */}
      <path
        d="M 50 45 L 80 30 L 80 70 L 50 85 Z"
        fill="url(#dice-right)"
        stroke="rgba(0,0,0,0.15)"
        strokeWidth="0.5"
      />

      {/* Dice Dots (pips) */}
      {/* Top face: 5 dots (⚄) */}
      <circle cx="35" cy="28" r="2" fill="white" opacity="0.9" filter="url(#dot-shadow)" />
      <circle cx="50" cy="30" r="2" fill="white" opacity="0.9" filter="url(#dot-shadow)" />
      <circle cx="65" cy="32" r="2" fill="white" opacity="0.9" filter="url(#dot-shadow)" />
      <circle cx="38" cy="36" r="2" fill="white" opacity="0.9" filter="url(#dot-shadow)" />
      <circle cx="62" cy="39" r="2" fill="white" opacity="0.9" filter="url(#dot-shadow)" />

      {/* Left face: 2 dots (⚁) */}
      <circle cx="30" cy="52" r="2.5" fill="white" opacity="0.85" filter="url(#dot-shadow)" />
      <circle cx="40" cy="63" r="2.5" fill="white" opacity="0.85" filter="url(#dot-shadow)" />

      {/* Right face: 4 dots (⚃) */}
      <circle cx="60" cy="50" r="2.5" fill="white" opacity="0.8" filter="url(#dot-shadow)" />
      <circle cx="70" cy="55" r="2.5" fill="white" opacity="0.8" filter="url(#dot-shadow)" />
      <circle cx="60" cy="65" r="2.5" fill="white" opacity="0.8" filter="url(#dot-shadow)" />
      <circle cx="70" cy="70" r="2.5" fill="white" opacity="0.8" filter="url(#dot-shadow)" />

      {/* Subtle edge highlights for depth */}
      <path
        d="M 50 15 L 80 30"
        stroke="rgba(255,255,255,0.3)"
        strokeWidth="1"
        strokeLinecap="round"
      />
      <path
        d="M 50 15 L 20 30"
        stroke="rgba(255,255,255,0.25)"
        strokeWidth="1"
        strokeLinecap="round"
      />
    </svg>
  );
}
