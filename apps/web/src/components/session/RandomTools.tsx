'use client';

/**
 * RandomTools Component (Issue #3345)
 *
 * Container component combining Timer, Coin Flip, and Wheel Spinner.
 * Features:
 * - Tab-based navigation between tools
 * - Unified state management
 * - SSE event integration
 */

import { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Clock, Coins, RotateCw } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import { CoinFlip } from './CoinFlip';
import { CountdownTimer } from './CountdownTimer';
import { WheelSpinner } from './WheelSpinner';

import type {
  TimerState,
  CoinFlipResult,
  WheelOption,
  WheelSpinResult,
} from './types';

// ============================================================================
// Types
// ============================================================================

export interface RandomToolsProps {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** Current participant name */
  participantName: string;

  /** Whether current user can control the timer */
  canControlTimer?: boolean;

  /** Timer state (from SSE) */
  timerState?: TimerState;

  /** Coin flip history */
  coinFlipHistory?: CoinFlipResult[];

  /** Wheel spin history */
  wheelSpinHistory?: WheelSpinResult[];

  /** Wheel options (persisted) */
  wheelOptions?: WheelOption[];

  /** Callback when timer is started */
  onTimerStart?: (durationSeconds: number) => Promise<void>;

  /** Callback when timer is paused */
  onTimerPause?: () => Promise<void>;

  /** Callback when timer is resumed */
  onTimerResume?: () => Promise<void>;

  /** Callback when timer is reset */
  onTimerReset?: () => Promise<void>;

  /** Callback when coin is flipped */
  onCoinFlip?: () => Promise<CoinFlipResult>;

  /** Callback when wheel is spun */
  onWheelSpin?: (options: WheelOption[]) => Promise<WheelSpinResult>;

  /** Loading state */
  isLoading?: boolean;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function RandomTools({
  sessionId,
  participantId,
  participantName,
  canControlTimer = true,
  timerState,
  coinFlipHistory = [],
  wheelSpinHistory = [],
  wheelOptions,
  onTimerStart,
  onTimerPause,
  onTimerResume,
  onTimerReset,
  onCoinFlip,
  onWheelSpin,
  isLoading = false,
  className,
}: RandomToolsProps) {
  const [activeTab, setActiveTab] = useState('timer');

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-lg">
          <RotateCw className="h-5 w-5 text-primary" />
          Random Tools
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="timer" className="gap-2">
              <Clock className="h-4 w-4" />
              <span className="hidden sm:inline">Timer</span>
            </TabsTrigger>
            <TabsTrigger value="coin" className="gap-2">
              <Coins className="h-4 w-4" />
              <span className="hidden sm:inline">Coin</span>
            </TabsTrigger>
            <TabsTrigger value="wheel" className="gap-2">
              <RotateCw className="h-4 w-4" />
              <span className="hidden sm:inline">Wheel</span>
            </TabsTrigger>
          </TabsList>

          <div className="mt-6">
            <AnimatePresence mode="wait">
              <TabsContent value="timer" className="mt-0">
                <motion.div
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ duration: 0.2 }}
                >
                  <CountdownTimer
                    timerState={timerState}
                    canControl={canControlTimer}
                    onStart={onTimerStart}
                    onPause={onTimerPause}
                    onResume={onTimerResume}
                    onReset={onTimerReset}
                    isLoading={isLoading}
                  />
                </motion.div>
              </TabsContent>

              <TabsContent value="coin" className="mt-0">
                <motion.div
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ duration: 0.2 }}
                >
                  <CoinFlip
                    sessionId={sessionId}
                    participantId={participantId}
                    participantName={participantName}
                    onFlip={onCoinFlip}
                    flipHistory={coinFlipHistory}
                    isLoading={isLoading}
                  />
                </motion.div>
              </TabsContent>

              <TabsContent value="wheel" className="mt-0">
                <motion.div
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ duration: 0.2 }}
                >
                  <WheelSpinner
                    sessionId={sessionId}
                    participantId={participantId}
                    participantName={participantName}
                    options={wheelOptions}
                    onSpin={onWheelSpin}
                    spinHistory={wheelSpinHistory}
                    isLoading={isLoading}
                  />
                </motion.div>
              </TabsContent>
            </AnimatePresence>
          </div>
        </Tabs>
      </CardContent>
    </Card>
  );
}

export default RandomTools;
