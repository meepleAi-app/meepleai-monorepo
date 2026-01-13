/**
 * Mechanic Pill Component - Issue #2372
 *
 * Displays a game mechanic as a styled pill/chip.
 * Used in game cards, detail views, and list tables.
 */

import { Cog } from 'lucide-react';

import { Badge } from '@/components/ui/badge';

export interface MechanicPillProps {
  /** Mechanic name to display */
  name: string;
  /** Optional size variant */
  size?: 'sm' | 'default';
  /** Optional click handler */
  onClick?: () => void;
  /** Whether the pill is removable (shows X button) */
  removable?: boolean;
  /** Handler for remove action */
  onRemove?: () => void;
}

/**
 * Pill component for displaying game mechanics
 */
export function MechanicPill({
  name,
  size = 'default',
  onClick,
  removable = false,
  onRemove,
}: MechanicPillProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';
  const clickableClass = onClick ? 'cursor-pointer hover:bg-purple-100' : '';

  const handleRemove = (e: React.MouseEvent) => {
    e.stopPropagation();
    onRemove?.();
  };

  return (
    <Badge
      variant="outline"
      className={`gap-1 bg-purple-50 text-purple-700 border-purple-200 ${badgeClass} ${clickableClass}`}
      onClick={onClick}
    >
      <Cog className={iconClass} />
      {name}
      {removable && (
        <button
          type="button"
          onClick={handleRemove}
          className="ml-0.5 rounded-full hover:bg-purple-200 p-0.5 -mr-1"
          aria-label={`Rimuovi meccanica ${name}`}
        >
          <svg className={iconClass} fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </button>
      )}
    </Badge>
  );
}

/**
 * Container for multiple mechanic pills
 */
export interface MechanicPillListProps {
  mechanics: Array<{ id: string; name: string }>;
  size?: 'sm' | 'default';
  maxVisible?: number;
  onMechanicClick?: (mechanicId: string) => void;
  removable?: boolean;
  onRemove?: (mechanicId: string) => void;
}

export function MechanicPillList({
  mechanics,
  size = 'default',
  maxVisible = 3,
  onMechanicClick,
  removable = false,
  onRemove,
}: MechanicPillListProps) {
  const visibleMechanics = mechanics.slice(0, maxVisible);
  const hiddenCount = mechanics.length - maxVisible;

  return (
    <div className="flex flex-wrap gap-1">
      {visibleMechanics.map(mechanic => (
        <MechanicPill
          key={mechanic.id}
          name={mechanic.name}
          size={size}
          onClick={onMechanicClick ? () => onMechanicClick(mechanic.id) : undefined}
          removable={removable}
          onRemove={onRemove ? () => onRemove(mechanic.id) : undefined}
        />
      ))}
      {hiddenCount > 0 && (
        <Badge
          variant="outline"
          className={`text-muted-foreground ${size === 'sm' ? 'text-xs px-1.5 py-0' : ''}`}
        >
          +{hiddenCount}
        </Badge>
      )}
    </div>
  );
}
