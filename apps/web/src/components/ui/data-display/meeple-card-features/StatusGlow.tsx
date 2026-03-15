import { memo } from 'react';

import { cn } from '@/lib/utils';

export type GlowState = 'active' | 'complete' | 'idle' | 'error' | 'new';

interface StatusGlowProps {
  state: GlowState;
  entityColor: string; // HSL format: "25 95% 45%"
  className?: string;
}

const GLOW_CONFIG: Record<
  GlowState,
  { animate: boolean; opacity: string; colorOverride?: string; ring?: boolean }
> = {
  active: { animate: true, opacity: 'opacity-100' },
  complete: { animate: false, opacity: 'opacity-100' },
  idle: { animate: false, opacity: 'opacity-0' },
  error: { animate: true, opacity: 'opacity-100', colorOverride: '0 80% 55%' },
  new: { animate: false, opacity: 'opacity-100', ring: true },
};

export const StatusGlow = memo(function StatusGlow({
  state,
  entityColor,
  className,
}: StatusGlowProps) {
  const config = GLOW_CONFIG[state];
  const glowColor = config.colorOverride ?? entityColor;

  return (
    <span
      className={cn(
        'absolute inset-[-1px] rounded-[14px] pointer-events-none z-0',
        config.animate && 'animate-pulse',
        config.opacity,
        config.ring && 'ring-2',
        className
      )}
      style={
        {
          '--glow-color': glowColor,
          boxShadow: state !== 'idle' ? `0 0 20px hsl(${glowColor} / 0.3)` : 'none',
          ...(config.ring ? { '--tw-ring-color': `hsl(${glowColor} / 0.6)` } : {}),
        } as React.CSSProperties
      }
    />
  );
});
