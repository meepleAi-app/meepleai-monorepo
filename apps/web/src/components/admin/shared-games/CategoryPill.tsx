/**
 * Category Pill Component - Issue #2372
 *
 * Displays a game category as a styled pill/chip.
 * Used in game cards, detail views, and list tables.
 */

import { Tag } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

export interface CategoryPillProps {
  /** Category name to display */
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
 * Pill component for displaying game categories
 */
export function CategoryPill({
  name,
  size = 'default',
  onClick,
  removable = false,
  onRemove,
}: CategoryPillProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';
  const clickableClass = onClick ? 'cursor-pointer hover:bg-blue-100' : '';

  const handleRemove = (e: React.MouseEvent) => {
    e.stopPropagation();
    onRemove?.();
  };

  return (
    <Badge
      variant="outline"
      className={`gap-1 bg-blue-50 text-blue-700 border-blue-200 ${badgeClass} ${clickableClass}`}
      onClick={onClick}
    >
      <Tag className={iconClass} />
      {name}
      {removable && (
        <button
          type="button"
          onClick={handleRemove}
          className="ml-0.5 rounded-full hover:bg-blue-200 p-0.5 -mr-1"
          aria-label={`Rimuovi categoria ${name}`}
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
 * Container for multiple category pills
 */
export interface CategoryPillListProps {
  categories: Array<{ id: string; name: string }>;
  size?: 'sm' | 'default';
  maxVisible?: number;
  onCategoryClick?: (categoryId: string) => void;
  removable?: boolean;
  onRemove?: (categoryId: string) => void;
}

export function CategoryPillList({
  categories,
  size = 'default',
  maxVisible = 3,
  onCategoryClick,
  removable = false,
  onRemove,
}: CategoryPillListProps) {
  const visibleCategories = categories.slice(0, maxVisible);
  const hiddenCount = categories.length - maxVisible;

  return (
    <div className="flex flex-wrap gap-1">
      {visibleCategories.map(category => (
        <CategoryPill
          key={category.id}
          name={category.name}
          size={size}
          onClick={onCategoryClick ? () => onCategoryClick(category.id) : undefined}
          removable={removable}
          onRemove={onRemove ? () => onRemove(category.id) : undefined}
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
