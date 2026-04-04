import React from 'react';

import { cn } from '@/lib/utils';

type GlowEntity = 'game' | 'player' | 'session' | 'collection' | 'event';

const glowClasses: Record<GlowEntity, string> = {
  game: 'glass-glow-game',
  player: 'glass-glow-player',
  session: 'glass-glow-session',
  collection: 'glass-glow-collection',
  event: 'glass-glow-event',
};

export interface GlassCardProps extends React.HTMLAttributes<HTMLElement> {
  entity?: GlowEntity;
  as?: React.ElementType;
  children: React.ReactNode;
}

export const GlassCard = React.forwardRef<HTMLElement, GlassCardProps>(function GlassCard(
  { entity, as: Component = 'div', className, children, ...props },
  ref
) {
  return (
    <Component
      ref={ref}
      className={cn('glass-card', entity && glowClasses[entity], className)}
      {...props}
    >
      {children}
    </Component>
  );
});
