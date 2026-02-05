'use client';

/**
 * CoinFlip Component (Issue #3345)
 *
 * Animated coin flip with heads/tails result.
 * Features:
 * - 3D flip animation
 * - Sound effects
 * - History of flips
 */

import { useState, useCallback, useRef, useEffect } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { History, Volume2, VolumeX, Coins } from 'lucide-react';

import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from '@/components/ui/overlays/hover-card';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';

import type { CoinFlipResult } from './types';

// ============================================================================
// Types
// ============================================================================

export interface CoinFlipProps {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** Current participant name */
  participantName: string;

  /** Callback when flip is initiated */
  onFlip?: () => Promise<CoinFlipResult>;

  /** History of flips */
  flipHistory?: CoinFlipResult[];

  /** Loading state */
  isLoading?: boolean;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function CoinFlip({
  sessionId: _sessionId,
  participantId: _participantId,
  participantName: _participantName,
  onFlip,
  flipHistory = [],
  isLoading = false,
  className,
}: CoinFlipProps) {
  const [isFlipping, setIsFlipping] = useState(false);
  const [currentResult, setCurrentResult] = useState<'heads' | 'tails' | null>(null);
  const [soundEnabled, setSoundEnabled] = useState(true);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Initialize audio
  useEffect(() => {
    if (typeof window !== 'undefined') {
      audioRef.current = new Audio('/sounds/coin-flip.mp3');
      audioRef.current.volume = 0.5;
    }
  }, []);

  const handleFlip = useCallback(async () => {
    if (isFlipping || isLoading) return;

    setIsFlipping(true);
    setCurrentResult(null);

    // Play sound
    if (soundEnabled && audioRef.current) {
      audioRef.current.currentTime = 0;
      audioRef.current.play().catch(() => {});
    }

    try {
      // If no onFlip callback, do local flip
      if (onFlip) {
        const result = await onFlip();
        // Wait for animation to near completion
        await new Promise((resolve) => setTimeout(resolve, 1500));
        setCurrentResult(result.result);
      } else {
        // Local flip
        await new Promise((resolve) => setTimeout(resolve, 1500));
        setCurrentResult(Math.random() < 0.5 ? 'heads' : 'tails');
      }
    } finally {
      setIsFlipping(false);
    }
  }, [isFlipping, isLoading, onFlip, soundEnabled]);

  const headsCount = flipHistory.filter((f) => f.result === 'heads').length;
  const tailsCount = flipHistory.filter((f) => f.result === 'tails').length;

  return (
    <div className={cn('space-y-6', className)}>
      {/* Coin Display */}
      <div className="flex flex-col items-center justify-center">
        <div
          className="relative w-32 h-32 cursor-pointer perspective-1000"
          onClick={handleFlip}
          style={{ perspective: '1000px' }}
        >
          <motion.div
            className="relative w-full h-full"
            animate={
              isFlipping
                ? {
                    rotateY: [0, 1800],
                    scale: [1, 1.2, 1],
                  }
                : currentResult
                ? { rotateY: currentResult === 'heads' ? 0 : 180 }
                : { rotateY: 0 }
            }
            transition={
              isFlipping
                ? { duration: 1.5, ease: 'easeOut' }
                : { duration: 0.3 }
            }
            style={{ transformStyle: 'preserve-3d' }}
          >
            {/* Heads Side */}
            <div
              className={cn(
                'absolute w-full h-full rounded-full flex items-center justify-center',
                'bg-gradient-to-br from-yellow-400 via-yellow-500 to-yellow-600',
                'border-4 border-yellow-300 shadow-lg',
                'backface-hidden'
              )}
              style={{ backfaceVisibility: 'hidden' }}
            >
              <span className="text-4xl font-bold text-yellow-900">H</span>
            </div>

            {/* Tails Side */}
            <div
              className={cn(
                'absolute w-full h-full rounded-full flex items-center justify-center',
                'bg-gradient-to-br from-amber-400 via-amber-500 to-amber-600',
                'border-4 border-amber-300 shadow-lg',
                'backface-hidden'
              )}
              style={{
                backfaceVisibility: 'hidden',
                transform: 'rotateY(180deg)',
              }}
            >
              <span className="text-4xl font-bold text-amber-900">T</span>
            </div>
          </motion.div>
        </div>

        {/* Result Display */}
        <AnimatePresence mode="wait">
          {currentResult && !isFlipping && (
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -10 }}
              className="mt-4 text-center"
            >
              <span
                className={cn(
                  'text-2xl font-bold capitalize',
                  currentResult === 'heads' ? 'text-yellow-600' : 'text-amber-600'
                )}
              >
                {currentResult}!
              </span>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Controls */}
      <div className="flex items-center justify-center gap-2">
        <Button
          onClick={handleFlip}
          disabled={isFlipping || isLoading}
          className="gap-2"
          size="lg"
        >
          <Coins className="h-5 w-5" />
          {isFlipping ? 'Flipping...' : 'Flip Coin'}
        </Button>

        {/* Sound Toggle */}
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setSoundEnabled(!soundEnabled)}
        >
          {soundEnabled ? (
            <Volume2 className="h-4 w-4" />
          ) : (
            <VolumeX className="h-4 w-4 text-muted-foreground" />
          )}
        </Button>

        {/* History */}
        {flipHistory.length > 0 && (
          <HoverCard>
            <HoverCardTrigger asChild>
              <Button variant="outline" size="icon">
                <History className="h-4 w-4" />
              </Button>
            </HoverCardTrigger>
            <HoverCardContent className="w-64" align="end">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium">Flip History</span>
                  <span className="text-xs text-muted-foreground">
                    H: {headsCount} / T: {tailsCount}
                  </span>
                </div>
                <ScrollArea className="h-40">
                  <div className="space-y-1">
                    {flipHistory.slice(0, 20).map((flip, index) => (
                      <div
                        key={flip.id || index}
                        className="flex items-center justify-between text-sm py-1 px-2 rounded bg-muted/50"
                      >
                        <span className="text-muted-foreground truncate max-w-[100px]">
                          {flip.participantName}
                        </span>
                        <span
                          className={cn(
                            'font-medium capitalize',
                            flip.result === 'heads'
                              ? 'text-yellow-600'
                              : 'text-amber-600'
                          )}
                        >
                          {flip.result}
                        </span>
                      </div>
                    ))}
                  </div>
                </ScrollArea>
              </div>
            </HoverCardContent>
          </HoverCard>
        )}
      </div>

      {/* Stats */}
      {flipHistory.length > 0 && (
        <div className="flex items-center justify-center gap-8 text-sm">
          <div className="text-center">
            <div className="text-2xl font-bold text-yellow-600">{headsCount}</div>
            <div className="text-muted-foreground">Heads</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-amber-600">{tailsCount}</div>
            <div className="text-muted-foreground">Tails</div>
          </div>
          <div className="text-center">
            <div className="text-2xl font-bold text-primary">{flipHistory.length}</div>
            <div className="text-muted-foreground">Total</div>
          </div>
        </div>
      )}
    </div>
  );
}

export default CoinFlip;
