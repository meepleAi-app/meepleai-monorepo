'use client';

/**
 * CountdownTimer Component (Issue #3345)
 *
 * Countdown timer with start/pause/reset functionality.
 * Features:
 * - Set duration in seconds/minutes
 * - Start/pause/reset controls
 * - Sound alert on completion
 * - Synced across participants via SSE
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Play, Pause, RotateCcw, Volume2, VolumeX } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

import type { TimerState } from './types';

// ============================================================================
// Types
// ============================================================================

export interface CountdownTimerProps {
  /** Timer state from server */
  timerState?: TimerState;

  /** Whether current user can control the timer */
  canControl?: boolean;

  /** Callback when timer is started */
  onStart?: (durationSeconds: number) => Promise<void>;

  /** Callback when timer is paused */
  onPause?: () => Promise<void>;

  /** Callback when timer is resumed */
  onResume?: () => Promise<void>;

  /** Callback when timer is reset */
  onReset?: () => Promise<void>;

  /** Loading state */
  isLoading?: boolean;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Utility Functions
// ============================================================================

function formatTime(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

function _parseTimeInput(value: string): number {
  const parts = value.split(':');
  if (parts.length === 2) {
    const mins = parseInt(parts[0], 10) || 0;
    const secs = parseInt(parts[1], 10) || 0;
    return mins * 60 + secs;
  }
  return parseInt(value, 10) || 0;
}

// ============================================================================
// Component
// ============================================================================

export function CountdownTimer({
  timerState,
  canControl = true,
  onStart,
  onPause,
  onResume,
  onReset,
  isLoading = false,
  className,
}: CountdownTimerProps) {
  const [inputMinutes, setInputMinutes] = useState(5);
  const [inputSeconds, setInputSeconds] = useState(0);
  const [localRemaining, setLocalRemaining] = useState(0);
  const [soundEnabled, setSoundEnabled] = useState(true);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const intervalRef = useRef<number | null>(null);

  const status = timerState?.status || 'idle';
  const isRunning = status === 'running';
  const isPaused = status === 'paused';
  const isCompleted = status === 'completed';
  const isIdle = status === 'idle';

  // Initialize audio
  useEffect(() => {
    if (typeof window !== 'undefined') {
      audioRef.current = new Audio('/sounds/timer-complete.mp3');
      audioRef.current.volume = 0.5;
    }
    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }
    };
  }, []);

  // Sync local remaining with server state
  useEffect(() => {
    if (timerState) {
      setLocalRemaining(timerState.remainingSeconds);
    }
  }, [timerState, timerState?.remainingSeconds]);

  // Local countdown when running
  useEffect(() => {
    if (isRunning && localRemaining > 0) {
      intervalRef.current = window.setInterval(() => {
        setLocalRemaining((prev) => {
          if (prev <= 1) {
            if (soundEnabled && audioRef.current) {
              audioRef.current.play().catch(() => {
                // Audio autoplay may be blocked
              });
            }
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    } else if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps -- localRemaining updated by interval, not a dependency
  }, [isRunning, soundEnabled]);

  // Play sound on completion
  useEffect(() => {
    if (isCompleted && soundEnabled && audioRef.current) {
      audioRef.current.play().catch(() => {
        // Audio autoplay may be blocked
      });
    }
  }, [isCompleted, soundEnabled]);

  const handleStart = useCallback(async () => {
    const totalSeconds = inputMinutes * 60 + inputSeconds;
    if (totalSeconds > 0 && onStart) {
      await onStart(totalSeconds);
    }
  }, [inputMinutes, inputSeconds, onStart]);

  const handlePause = useCallback(async () => {
    if (onPause) {
      await onPause();
    }
  }, [onPause]);

  const handleResume = useCallback(async () => {
    if (onResume) {
      await onResume();
    }
  }, [onResume]);

  const handleReset = useCallback(async () => {
    if (onReset) {
      await onReset();
    }
    setLocalRemaining(0);
  }, [onReset]);

  const displayTime = isIdle
    ? inputMinutes * 60 + inputSeconds
    : localRemaining;

  const progress = timerState
    ? ((timerState.durationSeconds - localRemaining) / timerState.durationSeconds) * 100
    : 0;

  return (
    <div className={cn('space-y-4', className)}>
      {/* Timer Display */}
      <div className="relative">
        <motion.div
          className={cn(
            'flex items-center justify-center',
            'rounded-xl border-4 p-8',
            'transition-colors duration-300',
            isCompleted && 'border-green-500 bg-green-500/10',
            isRunning && 'border-primary bg-primary/5',
            isPaused && 'border-yellow-500 bg-yellow-500/10',
            isIdle && 'border-muted bg-muted/30'
          )}
        >
          <AnimatePresence mode="wait">
            <motion.span
              key={displayTime}
              initial={{ opacity: 0, scale: 0.8 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.8 }}
              transition={{ duration: 0.2 }}
              className={cn(
                'text-6xl font-mono font-bold tabular-nums',
                isCompleted && 'text-green-600 dark:text-green-400',
                isRunning && localRemaining <= 10 && 'text-red-500 animate-pulse',
                isRunning && localRemaining > 10 && 'text-primary',
                isPaused && 'text-yellow-600 dark:text-yellow-400',
                isIdle && 'text-muted-foreground'
              )}
            >
              {formatTime(displayTime)}
            </motion.span>
          </AnimatePresence>
        </motion.div>

        {/* Progress Bar */}
        {!isIdle && (
          <div className="absolute bottom-0 left-0 right-0 h-1 bg-muted rounded-b-xl overflow-hidden">
            <motion.div
              className="h-full bg-primary"
              initial={{ width: 0 }}
              animate={{ width: `${progress}%` }}
              transition={{ duration: 0.5 }}
            />
          </div>
        )}
      </div>

      {/* Status */}
      {timerState && !isIdle && (
        <div className="text-center text-sm text-muted-foreground">
          {isRunning && `Timer started by ${timerState.startedByName}`}
          {isPaused && 'Timer paused'}
          {isCompleted && 'Time\'s up!'}
        </div>
      )}

      {/* Input Controls (only when idle) */}
      {isIdle && canControl && (
        <div className="flex items-center justify-center gap-2">
          <div className="flex items-center gap-1">
            <Input
              type="number"
              min={0}
              max={99}
              value={inputMinutes}
              onChange={(e) => setInputMinutes(Math.max(0, Math.min(99, parseInt(e.target.value) || 0)))}
              className="w-16 text-center"
              disabled={isLoading}
            />
            <span className="text-muted-foreground">min</span>
          </div>
          <span className="text-xl font-bold">:</span>
          <div className="flex items-center gap-1">
            <Input
              type="number"
              min={0}
              max={59}
              value={inputSeconds}
              onChange={(e) => setInputSeconds(Math.max(0, Math.min(59, parseInt(e.target.value) || 0)))}
              className="w-16 text-center"
              disabled={isLoading}
            />
            <span className="text-muted-foreground">sec</span>
          </div>
        </div>
      )}

      {/* Action Controls */}
      <div className="flex items-center justify-center gap-2">
        {isIdle && canControl && (
          <Button
            onClick={handleStart}
            disabled={isLoading || (inputMinutes === 0 && inputSeconds === 0)}
            className="gap-2"
          >
            <Play className="h-4 w-4" />
            Start Timer
          </Button>
        )}

        {isRunning && canControl && (
          <Button onClick={handlePause} disabled={isLoading} variant="outline" className="gap-2">
            <Pause className="h-4 w-4" />
            Pause
          </Button>
        )}

        {isPaused && canControl && (
          <>
            <Button onClick={handleResume} disabled={isLoading} className="gap-2">
              <Play className="h-4 w-4" />
              Resume
            </Button>
            <Button onClick={handleReset} disabled={isLoading} variant="outline" className="gap-2">
              <RotateCcw className="h-4 w-4" />
              Reset
            </Button>
          </>
        )}

        {isCompleted && canControl && (
          <Button onClick={handleReset} disabled={isLoading} className="gap-2">
            <RotateCcw className="h-4 w-4" />
            Reset Timer
          </Button>
        )}

        {/* Sound Toggle */}
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setSoundEnabled(!soundEnabled)}
          className="ml-2"
        >
          {soundEnabled ? (
            <Volume2 className="h-4 w-4" />
          ) : (
            <VolumeX className="h-4 w-4 text-muted-foreground" />
          )}
        </Button>
      </div>

      {/* Quick Presets */}
      {isIdle && canControl && (
        <div className="flex items-center justify-center gap-2 flex-wrap">
          <span className="text-sm text-muted-foreground">Quick:</span>
          {[1, 2, 5, 10, 15, 30].map((mins) => (
            <Button
              key={mins}
              variant="outline"
              size="sm"
              onClick={() => {
                setInputMinutes(mins);
                setInputSeconds(0);
              }}
              disabled={isLoading}
            >
              {mins}m
            </Button>
          ))}
        </div>
      )}
    </div>
  );
}

export default CountdownTimer;
