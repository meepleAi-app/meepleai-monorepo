'use client';

import { useCallback } from 'react';

import { Loader2, Mic, MicOff } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { VoiceRecognitionState } from '@/lib/voice/types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface VoiceMicButtonProps {
  state: VoiceRecognitionState;
  onTap: () => void;
  disabled?: boolean;
  /** sm = 32px, md = 40px, lg = 80px (Quick Ask hero button) */
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const SIZE_MAP = {
  sm: { button: 'h-8 w-8', icon: 'h-4 w-4' },
  md: { button: 'h-10 w-10', icon: 'h-5 w-5' },
  lg: { button: 'h-20 w-20', icon: 'h-8 w-8' },
} as const;

const ARIA_LABELS: Record<VoiceRecognitionState | 'disabled', string> = {
  idle: 'Start voice input',
  requesting: 'Requesting microphone access',
  listening: 'Listening - tap to stop',
  processing: 'Processing speech',
  error: 'Voice input error - tap to retry',
  disabled: 'Voice input unavailable',
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function VoiceMicButton({
  state,
  onTap,
  disabled = false,
  size = 'md',
  className,
}: VoiceMicButtonProps) {
  const effectiveState = disabled ? 'disabled' : state;
  const { button: buttonSize, icon: iconSize } = SIZE_MAP[size];

  const handleClick = useCallback(() => {
    if (!disabled) {
      onTap();
    }
  }, [disabled, onTap]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if ((e.key === 'Enter' || e.key === ' ') && !disabled) {
        e.preventDefault();
        onTap();
      }
    },
    [disabled, onTap]
  );

  return (
    <button
      type="button"
      role="button"
      tabIndex={0}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      disabled={disabled}
      aria-label={ARIA_LABELS[effectiveState]}
      aria-pressed={state === 'listening'}
      data-testid={`voice-mic-${effectiveState}`}
      className={cn(
        'relative inline-flex items-center justify-center rounded-full',
        'transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        buttonSize,
        // State-specific styles
        effectiveState === 'idle' &&
          'text-muted-foreground hover:text-foreground hover:bg-muted/50',
        effectiveState === 'requesting' && 'text-amber-500 animate-pulse',
        effectiveState === 'listening' && 'bg-red-500 text-white shadow-md',
        effectiveState === 'processing' && 'text-amber-500',
        effectiveState === 'error' && 'text-red-500 animate-shake',
        effectiveState === 'disabled' && 'text-muted-foreground/50 cursor-not-allowed',
        className
      )}
    >
      {/* Pulsing ring overlay for listening state */}
      {effectiveState === 'listening' && (
        <span
          aria-hidden="true"
          className={cn(
            'absolute inset-0 rounded-full bg-red-500/20',
            'motion-safe:animate-voice-pulse',
            // Reduced motion: static subtle ring instead of animation
            'motion-reduce:bg-red-500/30'
          )}
        />
      )}

      {/* Icon */}
      {effectiveState === 'processing' ? (
        <Loader2 className={cn(iconSize, 'animate-spin')} aria-hidden="true" />
      ) : effectiveState === 'error' ? (
        <MicOff className={iconSize} aria-hidden="true" />
      ) : (
        <Mic
          className={cn(iconSize, effectiveState === 'listening' && 'fill-current')}
          aria-hidden="true"
        />
      )}
    </button>
  );
}
