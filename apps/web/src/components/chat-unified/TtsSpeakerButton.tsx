'use client';

import { useCallback } from 'react';

import { Volume2, VolumeX } from 'lucide-react';

import { cn } from '@/lib/utils';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface TtsSpeakerButtonProps {
  /** The message content to speak aloud */
  text: string;
  /** Whether the TTS engine is currently speaking this message */
  isSpeaking: boolean;
  /** Start speaking the provided text */
  onSpeak: (text: string) => void;
  /** Stop speaking */
  onStop: () => void;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function TtsSpeakerButton({ text, isSpeaking, onSpeak, onStop }: TtsSpeakerButtonProps) {
  const handleClick = useCallback(() => {
    if (isSpeaking) {
      onStop();
    } else {
      onSpeak(text);
    }
  }, [isSpeaking, onSpeak, onStop, text]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleClick();
      }
    },
    [handleClick]
  );

  return (
    <button
      type="button"
      role="button"
      tabIndex={0}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      aria-label={isSpeaking ? 'Stop reading' : 'Read aloud'}
      aria-pressed={isSpeaking}
      data-testid={isSpeaking ? 'tts-speaker-stop' : 'tts-speaker-play'}
      className={cn(
        'inline-flex items-center justify-center rounded p-0.5',
        'transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
        isSpeaking
          ? 'text-amber-500 motion-safe:animate-pulse'
          : 'text-muted-foreground/60 hover:text-foreground'
      )}
    >
      {isSpeaking ? (
        <VolumeX className="h-4 w-4 fill-current" aria-hidden="true" />
      ) : (
        <Volume2 className="h-4 w-4" aria-hidden="true" />
      )}
    </button>
  );
}
