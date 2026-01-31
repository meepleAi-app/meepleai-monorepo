/**
 * BulkActionBar Component (Issue #912)
 *
 * Reusable bulk action bar for admin pages with multi-select functionality.
 * Provides a consistent UI/UX for performing actions on multiple selected items.
 *
 * Features:
 * - Generic TypeScript support for any item type
 * - Configurable action buttons (delete, export, enable, disable, etc.)
 * - Selection counter with progress indicator
 * - Clear selection functionality
 * - Responsive design (mobile + desktop)
 * - Dark mode support
 * - WCAG 2.1 AA accessibility
 * - Empty state handling
 *
 * @example
 * ```tsx
 * <BulkActionBar
 *   selectedCount={selectedKeys.size}
 *   totalCount={keys.length}
 *   actions={[
 *     {
 *       id: 'delete',
 *       label: 'Delete',
 *       icon: Trash2,
 *       variant: 'destructive',
 *       onClick: handleBulkDelete,
 *     },
 *     {
 *       id: 'export',
 *       label: 'Export',
 *       icon: Download,
 *       variant: 'outline',
 *       onClick: handleExport,
 *     }
 *   ]}
 *   onClearSelection={() => setSelectedKeys(new Set())}
 * />
 * ```
 */

import React, { useMemo } from 'react';

import { X, AlertCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

/**
 * Action button configuration for bulk operations
 */
export interface BulkAction {
  /**
   * Unique identifier for the action
   */
  id: string;

  /**
   * Button label text
   */
  label: string;

  /**
   * Lucide icon component
   */
  icon: LucideIcon;

  /**
   * Button variant (default, destructive, outline, secondary, ghost)
   */
  variant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost';

  /**
   * Click handler for the action
   * @param selectedCount - Number of selected items
   */
  onClick: (selectedCount: number) => void;

  /**
   * Whether the action is disabled
   */
  disabled?: boolean;

  /**
   * Custom class name for the button
   */
  className?: string;

  /**
   * Tooltip text (for accessibility)
   */
  tooltip?: string;

  /**
   * Whether to show the selected count in the button label
   * @default true
   */
  showCount?: boolean;
}

export interface BulkActionBarProps {
  /**
   * Number of selected items
   */
  selectedCount: number;

  /**
   * Total number of items available
   */
  totalCount: number;

  /**
   * Array of action configurations
   */
  actions: BulkAction[];

  /**
   * Callback to clear all selections
   */
  onClearSelection: () => void;

  /**
   * Visual variant of the action bar
   * - default: muted background with border
   * - floating: white background with orange border (prominent)
   * @default "default"
   */
  variant?: 'default' | 'floating';

  /**
   * Custom class name for the container
   */
  className?: string;

  /**
   * Label for the selection counter (e.g., "items", "keys", "users")
   * @default "items"
   */
  itemLabel?: string;

  /**
   * Singular form of itemLabel (e.g., "item", "key", "user")
   * @default itemLabel without trailing 's'
   */
  itemLabelSingular?: string;

  /**
   * Whether to show a progress indicator
   * @default true
   */
  showProgress?: boolean;

  /**
   * Whether to show the total count
   * @default true
   */
  showTotal?: boolean;

  /**
   * Custom aria-label for the action bar
   */
  ariaLabel?: string;

  /**
   * Test ID for testing
   */
  testId?: string;
}

/**
 * BulkActionBar Component
 *
 * Reusable component for bulk actions on selected items.
 * Provides a consistent UI across admin pages.
 */
export const BulkActionBar: React.FC<BulkActionBarProps> = ({
  selectedCount,
  totalCount,
  actions,
  onClearSelection,
  variant = 'default',
  className,
  itemLabel = 'items',
  itemLabelSingular,
  showProgress = true,
  showTotal = true,
  ariaLabel,
  testId = 'bulk-action-bar',
}) => {
  // Calculate singular label
  const singularLabel = useMemo(() => {
    if (itemLabelSingular) return itemLabelSingular;
    // Simple pluralization removal (items -> item, keys -> key)
    return itemLabel.endsWith('s') ? itemLabel.slice(0, -1) : itemLabel;
  }, [itemLabel, itemLabelSingular]);

  // Calculate selection percentage
  const selectionPercentage = useMemo(() => {
    if (totalCount === 0) return 0;
    return Math.round((selectedCount / totalCount) * 100);
  }, [selectedCount, totalCount]);

  // Don't render if no items selected
  if (selectedCount === 0) {
    return null;
  }

  const itemLabelText = selectedCount === 1 ? singularLabel : itemLabel;

  return (
    <div
      className={cn(
        'flex items-center justify-between gap-4 p-4 rounded-lg',
        variant === 'floating'
          ? 'border-2 border-orange-500 bg-white'
          : 'border bg-muted/50 border-border',
        'transition-all duration-200',
        'animate-in slide-in-from-top-2 fade-in-0',
        className
      )}
      role="toolbar"
      aria-label={ariaLabel || `Bulk actions for ${selectedCount} selected ${itemLabelText}`}
      data-testid={testId}
    >
      {/* Left Side: Selection Info */}
      <div className="flex items-center gap-3 flex-1 min-w-0">
        {/* Selection Badge */}
        <Badge variant="secondary" className="shrink-0">
          {selectedCount} {showTotal && `/ ${totalCount}`}
        </Badge>

        {/* Selection Text */}
        <div className="flex flex-col gap-1 min-w-0">
          <p className="text-sm font-medium text-foreground">
            {selectedCount} {itemLabelText} selected
          </p>

          {/* Progress Bar */}
          {showProgress && showTotal && (
            <div className="flex items-center gap-2">
              <div className="flex-1 h-1.5 bg-muted-foreground/20 rounded-full overflow-hidden">
                <div
                  className={cn(
                    'h-full transition-all duration-300 rounded-full',
                    selectionPercentage === 100 ? 'bg-primary' : 'bg-primary/80'
                  )}
                  style={{ width: `${selectionPercentage}%` }}
                  role="progressbar"
                  aria-valuenow={selectionPercentage}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-label={`${selectionPercentage}% of items selected`}
                />
              </div>
              <span className="text-xs text-muted-foreground tabular-nums shrink-0">
                {selectionPercentage}%
              </span>
            </div>
          )}
        </div>

        {/* Clear Selection Button */}
        <Button
          variant="ghost"
          size="sm"
          onClick={onClearSelection}
          className="shrink-0 h-8 px-2"
          aria-label="Clear selection"
          data-testid={`${testId}-clear`}
        >
          <X className="h-4 w-4 mr-1" />
          <span className="hidden sm:inline">Clear</span>
        </Button>
      </div>

      {/* Right Side: Action Buttons */}
      <div className="flex items-center gap-2 shrink-0">
        {actions.map(action => {
          const Icon = action.icon;
          const showCountInLabel = action.showCount !== false;
          const buttonLabel = showCountInLabel
            ? `${action.label} (${selectedCount})`
            : action.label;

          return (
            <Button
              key={action.id}
              variant={action.variant || 'default'}
              size="sm"
              onClick={() => action.onClick(selectedCount)}
              disabled={action.disabled}
              className={cn('h-9', action.className)}
              aria-label={action.tooltip || buttonLabel}
              title={action.tooltip}
              data-testid={`${testId}-action-${action.id}`}
            >
              <Icon className="h-4 w-4 mr-2" />
              <span className="hidden sm:inline">{buttonLabel}</span>
              <span className="sm:hidden">{action.label}</span>
            </Button>
          );
        })}
      </div>
    </div>
  );
};

/**
 * EmptyBulkActionBar Component
 *
 * Informational component shown when no items are available for selection.
 * Used for better UX to indicate that bulk actions are available.
 */
export interface EmptyBulkActionBarProps {
  /**
   * Custom message to display
   */
  message?: string;

  /**
   * Item label for the message (e.g., "items", "keys")
   */
  itemLabel?: string;

  /**
   * Custom class name
   */
  className?: string;

  /**
   * Test ID for testing
   */
  testId?: string;
}

export const EmptyBulkActionBar: React.FC<EmptyBulkActionBarProps> = ({
  message,
  itemLabel = 'items',
  className,
  testId = 'empty-bulk-action-bar',
}) => {
  const defaultMessage = `Select ${itemLabel} to perform bulk actions`;

  return (
    <div
      className={cn(
        'flex items-center gap-2 p-3 rounded-lg border border-dashed',
        'bg-muted/30 border-muted-foreground/30',
        'text-muted-foreground text-sm',
        className
      )}
      role="status"
      aria-live="polite"
      data-testid={testId}
    >
      <AlertCircle className="h-4 w-4 shrink-0" />
      <span>{message || defaultMessage}</span>
    </div>
  );
};

BulkActionBar.displayName = 'BulkActionBar';
EmptyBulkActionBar.displayName = 'EmptyBulkActionBar';

export default BulkActionBar;
