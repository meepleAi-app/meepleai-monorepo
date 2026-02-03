/**
 * QuickActions Component - Admin Re-export with Config
 *
 * Re-exports the generic ActionGrid from the UI library with admin-specific defaults.
 * The library component contains the full implementation.
 *
 * @module components/admin/QuickActions
 * @see Issue #2925 - Component Library extraction
 * @see Issue #885, #2788 - Original implementation
 */

import {
  UsersIcon,
  AlertTriangleIcon,
  SettingsIcon,
  ZapIcon,
  CheckIcon,
  TrashIcon,
  DownloadIcon,
} from 'lucide-react';

import {
  ActionGrid,
  type ActionGridProps,
  type ActionItem,
  type GradientPreset,
} from '@/components/ui/navigation/action-grid';

// Re-export types for backward compatibility
export type { ActionItem as QuickAction, GradientPreset as GradientKey };

export interface QuickActionsProps extends Omit<ActionGridProps, 'actions'> {
  actions?: ActionItem[];
}

/**
 * Default quick actions for the admin dashboard (Issue #2788)
 * Uses gradient-based styling for modern look
 */
export const defaultQuickActions: ActionItem[] = [
  {
    id: 'approve-games',
    label: 'Approva Giochi',
    href: '/admin/games/pending',
    icon: CheckIcon,
    gradient: 'green-emerald',
  },
  {
    id: 'manage-users',
    label: 'Gestisci Utenti',
    href: '/admin/users',
    icon: UsersIcon,
    gradient: 'blue-indigo',
  },
  {
    id: 'view-alerts',
    label: 'Vedi Alert',
    href: '/admin/alerts',
    icon: AlertTriangleIcon,
    gradient: 'amber-orange',
  },
  {
    id: 'clear-cache',
    label: 'Svuota Cache',
    href: '/admin/cache',
    icon: TrashIcon,
    gradient: 'red-rose',
  },
  {
    id: 'export-data',
    label: 'Esporta Dati',
    href: '/admin/export',
    icon: DownloadIcon,
    gradient: 'purple-violet',
  },
  {
    id: 'configuration',
    label: 'Configurazione',
    href: '/admin/config',
    icon: SettingsIcon,
    gradient: 'stone-stone',
  },
];

/**
 * QuickActions - Admin quick actions grid with default admin settings
 *
 * Wraps ActionGrid with admin-specific defaults:
 * - Default actions for admin dashboard
 * - Zap icon in title
 * - "Quick Actions" title
 */
export function QuickActions({
  actions = defaultQuickActions,
  title = 'Quick Actions',
  titleIcon = ZapIcon,
  ...props
}: QuickActionsProps) {
  return (
    <ActionGrid
      actions={actions}
      title={title}
      titleIcon={titleIcon}
      {...props}
    />
  );
}
