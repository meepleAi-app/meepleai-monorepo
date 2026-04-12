import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card/types';

import type { LucideIcon } from 'lucide-react';

export interface ConnectionPip {
  entityType: MeepleEntityType;
  count: number;
  label: string;
  icon: LucideIcon;
  /** True when count === 0 or connection is unavailable — shows "+" create indicator */
  isEmpty: boolean;
}

export interface ConnectionBarProps {
  connections: ConnectionPip[];
  onPipClick: (pip: ConnectionPip, anchorRect: DOMRect) => void;
  className?: string;
  'data-testid'?: string;
}
