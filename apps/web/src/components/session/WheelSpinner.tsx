'use client';

/**
 * WheelSpinner Component (Issue #3345)
 *
 * Customizable spinning wheel for random selection.
 * Features:
 * - Add/remove options
 * - Weighted selection
 * - Spin animation
 * - Sound effects
 */

import { useState, useCallback, useRef, useEffect, useMemo } from 'react';
import { motion } from 'framer-motion';
import { Plus, X, RotateCw, Volume2, VolumeX, History, Settings } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from '@/components/ui/overlays/hover-card';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { cn } from '@/lib/utils';

import type { WheelOption, WheelSpinResult } from './types';

// ============================================================================
// Types
// ============================================================================

export interface WheelSpinnerProps {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** Current participant name */
  participantName: string;

  /** Initial options */
  options?: WheelOption[];

  /** Callback when spin is initiated */
  onSpin?: (options: WheelOption[]) => Promise<WheelSpinResult>;

  /** History of spins */
  spinHistory?: WheelSpinResult[];

  /** Loading state */
  isLoading?: boolean;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const WHEEL_COLORS = [
  '#ef4444', // red
  '#f97316', // orange
  '#eab308', // yellow
  '#22c55e', // green
  '#0ea5e9', // sky
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#06b6d4', // cyan
];

const DEFAULT_OPTIONS: WheelOption[] = [
  { id: '1', label: 'Option 1', color: WHEEL_COLORS[0], weight: 1 },
  { id: '2', label: 'Option 2', color: WHEEL_COLORS[1], weight: 1 },
  { id: '3', label: 'Option 3', color: WHEEL_COLORS[2], weight: 1 },
  { id: '4', label: 'Option 4', color: WHEEL_COLORS[3], weight: 1 },
];

// ============================================================================
// Utility Functions
// ============================================================================

function generateId(): string {
  return Math.random().toString(36).substring(2, 9);
}

function getNextColor(currentOptions: WheelOption[]): string {
  const usedColors = new Set(currentOptions.map((o) => o.color));
  const availableColor = WHEEL_COLORS.find((c) => !usedColors.has(c));
  return availableColor || WHEEL_COLORS[currentOptions.length % WHEEL_COLORS.length];
}

// ============================================================================
// Component
// ============================================================================

export function WheelSpinner({
  sessionId,
  participantId,
  participantName,
  options: initialOptions,
  onSpin,
  spinHistory = [],
  isLoading = false,
  className,
}: WheelSpinnerProps) {
  const [options, setOptions] = useState<WheelOption[]>(
    initialOptions || DEFAULT_OPTIONS
  );
  const [isSpinning, setIsSpinning] = useState(false);
  const [rotation, setRotation] = useState(0);
  const [selectedOption, setSelectedOption] = useState<WheelOption | null>(null);
  const [newOptionLabel, setNewOptionLabel] = useState('');
  const [soundEnabled, setSoundEnabled] = useState(true);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Initialize audio
  useEffect(() => {
    if (typeof window !== 'undefined') {
      audioRef.current = new Audio('/sounds/wheel-spin.mp3');
      audioRef.current.volume = 0.5;
    }
  }, []);

  // Calculate wheel segments
  const totalWeight = useMemo(
    () => options.reduce((sum, opt) => sum + opt.weight, 0),
    [options]
  );

  const segments = useMemo(() => {
    let currentAngle = 0;
    return options.map((option) => {
      const angle = (option.weight / totalWeight) * 360;
      const segment = {
        ...option,
        startAngle: currentAngle,
        endAngle: currentAngle + angle,
        midAngle: currentAngle + angle / 2,
      };
      currentAngle += angle;
      return segment;
    });
  }, [options, totalWeight]);

  // Weighted random selection
  const selectRandomOption = useCallback((): WheelOption => {
    const random = Math.random() * totalWeight;
    let cumulative = 0;
    for (const option of options) {
      cumulative += option.weight;
      if (random <= cumulative) {
        return option;
      }
    }
    return options[options.length - 1];
  }, [options, totalWeight]);

  const handleSpin = useCallback(async () => {
    if (isSpinning || isLoading || options.length < 2) return;

    setIsSpinning(true);
    setSelectedOption(null);

    // Play sound
    if (soundEnabled && audioRef.current) {
      audioRef.current.currentTime = 0;
      audioRef.current.play().catch(() => {});
    }

    try {
      let result: WheelOption;

      if (onSpin) {
        const spinResult = await onSpin(options);
        result = spinResult.selectedOption;
      } else {
        result = selectRandomOption();
      }

      // Calculate target rotation
      const segment = segments.find((s) => s.id === result.id);
      if (!segment) return;

      // Spin multiple times plus land on the segment
      const spins = 5 + Math.random() * 3;
      const targetAngle = 360 - segment.midAngle; // Adjust for pointer position at top
      const finalRotation = rotation + spins * 360 + targetAngle;

      setRotation(finalRotation);

      // Wait for animation to complete
      await new Promise((resolve) => setTimeout(resolve, 4000));
      setSelectedOption(result);
    } finally {
      setIsSpinning(false);
    }
  }, [isSpinning, isLoading, options, soundEnabled, onSpin, selectRandomOption, segments, rotation]);

  const handleAddOption = useCallback(() => {
    if (!newOptionLabel.trim()) return;

    const newOption: WheelOption = {
      id: generateId(),
      label: newOptionLabel.trim(),
      color: getNextColor(options),
      weight: 1,
    };

    setOptions([...options, newOption]);
    setNewOptionLabel('');
  }, [newOptionLabel, options]);

  const handleRemoveOption = useCallback(
    (id: string) => {
      if (options.length <= 2) return; // Minimum 2 options
      setOptions(options.filter((o) => o.id !== id));
    },
    [options]
  );

  const wheelSize = 280;
  const center = wheelSize / 2;
  const radius = wheelSize / 2 - 10;

  return (
    <div className={cn('space-y-6', className)}>
      {/* Wheel */}
      <div className="flex flex-col items-center">
        {/* Pointer */}
        <div className="relative z-10 -mb-2">
          <div className="w-0 h-0 border-l-[12px] border-r-[12px] border-t-[20px] border-l-transparent border-r-transparent border-t-primary" />
        </div>

        {/* Wheel SVG */}
        <div className="relative">
          <motion.svg
            width={wheelSize}
            height={wheelSize}
            viewBox={`0 0 ${wheelSize} ${wheelSize}`}
            animate={{ rotate: rotation }}
            transition={{
              duration: isSpinning ? 4 : 0,
              ease: [0.2, 0.8, 0.3, 1],
            }}
            className="drop-shadow-lg cursor-pointer"
            onClick={!isSpinning ? handleSpin : undefined}
          >
            {/* Wheel segments */}
            {segments.map((segment, index) => {
              const startRad = (segment.startAngle - 90) * (Math.PI / 180);
              const endRad = (segment.endAngle - 90) * (Math.PI / 180);

              const x1 = center + radius * Math.cos(startRad);
              const y1 = center + radius * Math.sin(startRad);
              const x2 = center + radius * Math.cos(endRad);
              const y2 = center + radius * Math.sin(endRad);

              const largeArc = segment.endAngle - segment.startAngle > 180 ? 1 : 0;

              const pathD = [
                `M ${center} ${center}`,
                `L ${x1} ${y1}`,
                `A ${radius} ${radius} 0 ${largeArc} 1 ${x2} ${y2}`,
                'Z',
              ].join(' ');

              // Calculate text position
              const textRad = (segment.midAngle - 90) * (Math.PI / 180);
              const textRadius = radius * 0.65;
              const textX = center + textRadius * Math.cos(textRad);
              const textY = center + textRadius * Math.sin(textRad);

              return (
                <g key={segment.id}>
                  <path
                    d={pathD}
                    fill={segment.color}
                    stroke="white"
                    strokeWidth="2"
                  />
                  <text
                    x={textX}
                    y={textY}
                    textAnchor="middle"
                    dominantBaseline="middle"
                    fill="white"
                    fontSize="12"
                    fontWeight="bold"
                    transform={`rotate(${segment.midAngle}, ${textX}, ${textY})`}
                    className="pointer-events-none select-none"
                  >
                    {segment.label.length > 10
                      ? segment.label.substring(0, 10) + '...'
                      : segment.label}
                  </text>
                </g>
              );
            })}

            {/* Center circle */}
            <circle cx={center} cy={center} r="20" fill="white" stroke="#e5e7eb" strokeWidth="2" />
            <circle cx={center} cy={center} r="8" fill="#6b7280" />
          </motion.svg>
        </div>

        {/* Result Display */}
        {selectedOption && !isSpinning && (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            className="mt-4 text-center"
          >
            <div
              className="px-4 py-2 rounded-lg font-bold text-white text-lg"
              style={{ backgroundColor: selectedOption.color }}
            >
              {selectedOption.label}
            </div>
          </motion.div>
        )}
      </div>

      {/* Controls */}
      <div className="flex items-center justify-center gap-2">
        <Button
          onClick={handleSpin}
          disabled={isSpinning || isLoading || options.length < 2}
          className="gap-2"
          size="lg"
        >
          <RotateCw className={cn('h-5 w-5', isSpinning && 'animate-spin')} />
          {isSpinning ? 'Spinning...' : 'Spin Wheel'}
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

        {/* Options Editor */}
        <Dialog>
          <DialogTrigger asChild>
            <Button variant="outline" size="icon">
              <Settings className="h-4 w-4" />
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[300px]">
            <DialogHeader>
              <DialogTitle>Wheel Options</DialogTitle>
            </DialogHeader>
            <div className="space-y-3">
              {/* Add Option */}
              <div className="flex gap-2">
                <Input
                  value={newOptionLabel}
                  onChange={(e) => setNewOptionLabel(e.target.value)}
                  placeholder="New option..."
                  onKeyDown={(e) => e.key === 'Enter' && handleAddOption()}
                  className="flex-1"
                />
                <Button size="icon" onClick={handleAddOption} disabled={!newOptionLabel.trim()}>
                  <Plus className="h-4 w-4" />
                </Button>
              </div>

              {/* Options List */}
              <ScrollArea className="h-40">
                <div className="space-y-1">
                  {options.map((option) => (
                    <div
                      key={option.id}
                      className="flex items-center gap-2 p-1 rounded hover:bg-muted/50"
                    >
                      <div
                        className="w-4 h-4 rounded-full flex-shrink-0"
                        style={{ backgroundColor: option.color }}
                      />
                      <span className="flex-1 text-sm truncate">{option.label}</span>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-6 w-6"
                        onClick={() => handleRemoveOption(option.id)}
                        disabled={options.length <= 2}
                      >
                        <X className="h-3 w-3" />
                      </Button>
                    </div>
                  ))}
                </div>
              </ScrollArea>

              <p className="text-xs text-muted-foreground">
                Minimum 2 options required
              </p>
            </div>
          </DialogContent>
        </Dialog>

        {/* History */}
        {spinHistory.length > 0 && (
          <HoverCard>
            <HoverCardTrigger asChild>
              <Button variant="outline" size="icon">
                <History className="h-4 w-4" />
              </Button>
            </HoverCardTrigger>
            <HoverCardContent className="w-64" align="end">
              <div className="space-y-2">
                <span className="text-sm font-medium">Spin History</span>
                <ScrollArea className="h-40">
                  <div className="space-y-1">
                    {spinHistory.slice(0, 20).map((spin, index) => (
                      <div
                        key={spin.id || index}
                        className="flex items-center justify-between text-sm py-1 px-2 rounded bg-muted/50"
                      >
                        <span className="text-muted-foreground truncate max-w-[80px]">
                          {spin.participantName}
                        </span>
                        <span
                          className="font-medium truncate max-w-[80px]"
                          style={{ color: spin.selectedOption.color }}
                        >
                          {spin.selectedOption.label}
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
    </div>
  );
}

export default WheelSpinner;
