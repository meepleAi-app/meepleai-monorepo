'use client';

/**
 * TimerDisplay — Circular countdown ring with color transitions and
 * shake animation in the last 10 seconds.
 *
 * Stateless: receives remaining/total/status as props so it
 * can be tested independently from the store.
 */

import React from 'react';

import { motion } from 'framer-motion';

import type { TimerStatus } from '@/stores/toolkit-timer-store';

// ============================================================================
// Helpers
// ============================================================================

export function formatTime(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function ringColor(progress: number, status: TimerStatus): string {
  if (status === 'ended') return '#ef4444';
  if (progress > 0.5) return 'hsl(142,70%,45%)';
  if (progress > 0.2) return '#f59e0b';
  return '#ef4444';
}

// ============================================================================
// Component
// ============================================================================

interface TimerDisplayProps {
  remaining: number;
  total: number;
  status: TimerStatus;
}

const RADIUS = 52;
const SIZE = 144;
const CENTER = SIZE / 2;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

export function TimerDisplay({ remaining, total, status }: TimerDisplayProps) {
  const progress = total > 0 ? Math.min(1, remaining / total) : 0;
  const color = ringColor(progress, status);
  const dashOffset = CIRCUMFERENCE * (1 - progress);

  const isShaking = status === 'running' && remaining > 0 && remaining <= 10;

  return (
    <motion.div
      className="relative flex items-center justify-center mx-auto"
      style={{ width: SIZE, height: SIZE }}
      data-testid="timer-display-wrapper"
      animate={isShaking ? { x: [-3, 3, -3, 3, -2, 2, -1, 1, 0] } : { x: 0 }}
      transition={
        isShaking ? { duration: 0.5, repeat: Infinity, repeatType: 'loop' } : { duration: 0.1 }
      }
    >
      {/* Circular progress ring (SVG rotated so arc starts at 12 o'clock) */}
      <svg
        className="absolute inset-0"
        width={SIZE}
        height={SIZE}
        viewBox={`0 0 ${SIZE} ${SIZE}`}
        style={{ transform: 'rotate(-90deg)' }}
        aria-hidden="true"
      >
        {/* Track */}
        <circle cx={CENTER} cy={CENTER} r={RADIUS} fill="none" stroke="#e5e7eb" strokeWidth="9" />
        {/* Progress arc */}
        <circle
          cx={CENTER}
          cy={CENTER}
          r={RADIUS}
          fill="none"
          stroke={color}
          strokeWidth="9"
          strokeLinecap="round"
          strokeDasharray={CIRCUMFERENCE}
          strokeDashoffset={dashOffset}
          style={{
            transition: 'stroke-dashoffset 0.45s linear, stroke 0.6s ease',
          }}
        />
      </svg>

      {/* Time label */}
      <div className="z-10 flex flex-col items-center gap-0.5 select-none">
        <span
          className="text-3xl font-bold font-mono tabular-nums leading-none"
          style={{ color, transition: 'color 0.6s ease' }}
          data-testid="timer-display"
        >
          {formatTime(remaining)}
        </span>
        {status === 'ended' && (
          <span className="text-[11px] font-bold tracking-wide text-red-500 animate-pulse">
            Tempo!
          </span>
        )}
        {status === 'paused' && (
          <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wider">
            Pausa
          </span>
        )}
      </div>
    </motion.div>
  );
}
