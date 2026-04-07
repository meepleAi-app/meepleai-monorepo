/**
 * QuickAction type used internally by entity action hooks.
 * This is a legacy compatibility type — components map to MeepleCardAction for the card API.
 */
import type { LucideIcon } from 'lucide-react';

export interface QuickAction {
  icon: LucideIcon;
  label: string;
  onClick: () => void;
  /** When true, action is shown but not clickable */
  disabled?: boolean;
  /** Tooltip shown when disabled */
  disabledTooltip?: string;
  /** When true, action is completely hidden */
  hidden?: boolean;
  /** When true, renders with destructive styling */
  destructive?: boolean;
  /** When true, renders a separator before this action */
  separator?: boolean;
}
