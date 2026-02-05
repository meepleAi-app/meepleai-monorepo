/**
 * Admin Navigation Configuration
 * Issue #3595 - Create admin sidebar navigation
 *
 * Defines navigation structure for admin panel with collapsible sections.
 * Organizes 50+ admin pages into logical groups for better discoverability.
 */

import {
  type LucideIcon,
  LayoutDashboardIcon,
  UsersIcon,
  GamepadIcon,
  ShareIcon,
  HelpCircleIcon,
  BotIcon,
  BrainCircuitIcon,
  CpuIcon,
  BarChartIcon,
  FileTextIcon,
  SettingsIcon,
  SlidersIcon,
  FileIcon,
  GaugeIcon,
  ActivityIcon,
  ClipboardListIcon,
  BellIcon,
  AlertTriangleIcon,
  BellRingIcon,
  ServerIcon,
  DatabaseIcon,
  BoxIcon,
  PackageIcon,
  DownloadIcon,
  MailIcon,
  FlaskConicalIcon,
  WandIcon,
  KeyIcon,
  TerminalIcon,
  FilePlus2Icon,
  CheckSquareIcon,
  Trash2Icon,
  ClockIcon,
  HistoryIcon,
  InboxIcon,
  FileSearchIcon,
} from 'lucide-react';

/**
 * Navigation item for admin sidebar
 */
export interface AdminNavItem {
  /** Route path relative to /admin */
  href: string;
  /** Display label */
  label: string;
  /** Lucide icon component */
  icon: LucideIcon;
  /** Optional badge configuration */
  badge?: {
    /** Badge key for dynamic count lookup */
    key: string;
    /** Default badge variant */
    variant?: 'default' | 'secondary' | 'destructive' | 'outline';
  };
  /** Active state pattern (defaults to exact match) */
  activePattern?: RegExp;
}

/**
 * Navigation section with collapsible items
 */
export interface AdminNavSection {
  /** Section unique identifier */
  id: string;
  /** Section display label */
  label: string;
  /** Section icon */
  icon: LucideIcon;
  /** Navigation items in this section */
  items: AdminNavItem[];
  /** Default expanded state */
  defaultExpanded?: boolean;
}

/**
 * Complete admin navigation structure
 */
export const ADMIN_NAVIGATION: AdminNavSection[] = [
  {
    id: 'dashboard',
    label: 'Dashboard',
    icon: LayoutDashboardIcon,
    defaultExpanded: true,
    items: [
      {
        href: '/admin',
        label: 'Overview',
        icon: LayoutDashboardIcon,
      },
      {
        href: '/admin/command-center',
        label: 'Command Center',
        icon: TerminalIcon,
      },
      {
        href: '/admin/redesign',
        label: 'Redesign Preview',
        icon: WandIcon,
      },
    ],
  },
  {
    id: 'users',
    label: 'Users',
    icon: UsersIcon,
    items: [
      {
        href: '/admin/users',
        label: 'User List',
        icon: UsersIcon,
        badge: { key: 'users' },
        activePattern: /^\/admin\/users$/,
      },
      {
        href: '/admin/sessions',
        label: 'Sessions',
        icon: ActivityIcon,
      },
      {
        href: '/admin/api-keys',
        label: 'API Keys',
        icon: KeyIcon,
      },
    ],
  },
  {
    id: 'games',
    label: 'Games',
    icon: GamepadIcon,
    items: [
      {
        href: '/admin/games',
        label: 'Games List',
        icon: GamepadIcon,
      },
      {
        href: '/admin/shared-games',
        label: 'Shared Games',
        icon: ShareIcon,
        activePattern: /^\/admin\/shared-games$/,
      },
      {
        href: '/admin/shared-games/new',
        label: 'Add New Game',
        icon: FilePlus2Icon,
      },
      {
        href: '/admin/shared-games/add-from-bgg',
        label: 'Import from BGG',
        icon: DownloadIcon,
      },
      {
        href: '/admin/shared-games/import',
        label: 'Bulk Import',
        icon: InboxIcon,
      },
      {
        href: '/admin/shared-games/pending-approvals',
        label: 'Pending Approvals',
        icon: CheckSquareIcon,
        badge: { key: 'pending-approvals', variant: 'destructive' },
      },
      {
        href: '/admin/shared-games/approval-queue',
        label: 'Approval Queue',
        icon: ClipboardListIcon,
      },
      {
        href: '/admin/shared-games/pending-deletes',
        label: 'Pending Deletes',
        icon: Trash2Icon,
        badge: { key: 'pending-deletes' },
      },
      {
        href: '/admin/faqs',
        label: 'FAQs',
        icon: HelpCircleIcon,
      },
      {
        href: '/admin/share-requests',
        label: 'Share Requests',
        icon: MailIcon,
        badge: { key: 'share-requests' },
        activePattern: /^\/admin\/share-requests$/,
      },
    ],
  },
  {
    id: 'ai-agents',
    label: 'AI & Agents',
    icon: BotIcon,
    items: [
      {
        href: '/admin/agent-typologies',
        label: 'Agent Typologies',
        icon: BotIcon,
        activePattern: /^\/admin\/agent-typologies$/,
      },
      {
        href: '/admin/agent-typologies/create',
        label: 'Create Typology',
        icon: FilePlus2Icon,
      },
      {
        href: '/admin/agent-typologies/pending',
        label: 'Pending Typologies',
        icon: ClockIcon,
        badge: { key: 'pending-typologies' },
      },
      {
        href: '/admin/agents/test',
        label: 'Agent Testing',
        icon: FlaskConicalIcon,
      },
      {
        href: '/admin/agents/test-history',
        label: 'Test History',
        icon: HistoryIcon,
      },
      {
        href: '/admin/ai-models',
        label: 'AI Models',
        icon: CpuIcon,
      },
      {
        href: '/admin/ai-usage',
        label: 'AI Usage',
        icon: BarChartIcon,
      },
      {
        href: '/admin/prompts',
        label: 'Prompts',
        icon: FileTextIcon,
        activePattern: /^\/admin\/prompts$/,
      },
      {
        href: '/rag',
        label: 'RAG Dashboard',
        icon: BrainCircuitIcon,
      },
      {
        href: '/admin/rag/tier-strategy-config',
        label: 'RAG Tier Config',
        icon: SlidersIcon,
      },
    ],
  },
  {
    id: 'configuration',
    label: 'Configuration',
    icon: SettingsIcon,
    items: [
      {
        href: '/admin/configuration',
        label: 'General Settings',
        icon: SettingsIcon,
        activePattern: /^\/admin\/configuration$/,
      },
      {
        href: '/admin/config/rate-limits',
        label: 'Rate Limits',
        icon: GaugeIcon,
      },
      {
        href: '/admin/configuration/game-library-limits',
        label: 'Game Library Limits',
        icon: GamepadIcon,
      },
      {
        href: '/admin/configuration/pdf-tier-limits',
        label: 'PDF Tier Limits',
        icon: FileIcon,
      },
      {
        href: '/admin/configuration/pdf-upload-limits',
        label: 'PDF Upload Limits',
        icon: FileIcon,
      },
    ],
  },
  {
    id: 'analytics',
    label: 'Analytics',
    icon: BarChartIcon,
    items: [
      {
        href: '/admin/analytics',
        label: 'Dashboard',
        icon: BarChartIcon,
      },
      {
        href: '/admin/reports',
        label: 'Reports',
        icon: ClipboardListIcon,
      },
      {
        href: '/admin/management',
        label: 'Management',
        icon: FileSearchIcon,
      },
    ],
  },
  {
    id: 'alerts',
    label: 'Alerts',
    icon: BellIcon,
    items: [
      {
        href: '/admin/alerts',
        label: 'Active Alerts',
        icon: BellIcon,
        badge: { key: 'alerts', variant: 'destructive' },
        activePattern: /^\/admin\/alerts$/,
      },
      {
        href: '/admin/alert-rules',
        label: 'Alert Rules',
        icon: AlertTriangleIcon,
      },
      {
        href: '/admin/alerts/config',
        label: 'Alert Configuration',
        icon: BellRingIcon,
      },
    ],
  },
  {
    id: 'system',
    label: 'System',
    icon: ServerIcon,
    items: [
      {
        href: '/admin/infrastructure',
        label: 'Infrastructure',
        icon: ServerIcon,
      },
      {
        href: '/admin/cache',
        label: 'Cache',
        icon: DatabaseIcon,
      },
      {
        href: '/admin/services',
        label: 'Services',
        icon: BoxIcon,
      },
      {
        href: '/admin/n8n-templates',
        label: 'n8n Templates',
        icon: PackageIcon,
      },
      {
        href: '/admin/bulk-export',
        label: 'Bulk Export',
        icon: DownloadIcon,
      },
    ],
  },
  {
    id: 'tools',
    label: 'Tools',
    icon: FlaskConicalIcon,
    items: [
      {
        href: '/admin/testing',
        label: 'Testing',
        icon: FlaskConicalIcon,
      },
      {
        href: '/admin/wizard',
        label: 'Setup Wizard',
        icon: WandIcon,
      },
    ],
  },
];

/**
 * Flat list of all admin navigation items for search/lookup
 */
export const ADMIN_NAV_ITEMS_FLAT: AdminNavItem[] = ADMIN_NAVIGATION.flatMap(
  (section) => section.items
);

/**
 * Get section containing a specific href
 */
export function getSectionForHref(href: string): AdminNavSection | undefined {
  return ADMIN_NAVIGATION.find((section) =>
    section.items.some((item) => {
      if (item.activePattern) {
        return item.activePattern.test(href);
      }
      return item.href === href || href.startsWith(item.href + '/');
    })
  );
}

/**
 * Check if a navigation item is active for the given pathname
 */
export function isAdminNavItemActive(item: AdminNavItem, pathname: string): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  // Exact match for root admin, startsWith for others
  if (item.href === '/admin') {
    return pathname === '/admin';
  }
  return pathname === item.href || pathname.startsWith(item.href + '/');
}

/**
 * Get default expanded sections based on current pathname
 */
export function getExpandedSections(pathname: string): string[] {
  const expanded: string[] = [];

  for (const section of ADMIN_NAVIGATION) {
    // Always expand dashboard section
    if (section.defaultExpanded) {
      expanded.push(section.id);
    }

    // Expand section containing current route
    const hasActiveItem = section.items.some((item) =>
      isAdminNavItemActive(item, pathname)
    );
    if (hasActiveItem && !expanded.includes(section.id)) {
      expanded.push(section.id);
    }
  }

  return expanded;
}

/**
 * Storage key for sidebar section expansion state
 */
export const ADMIN_SIDEBAR_SECTIONS_KEY = 'admin-sidebar-sections';

/**
 * Storage key for sidebar collapsed state
 */
export const ADMIN_SIDEBAR_COLLAPSED_KEY = 'admin-sidebar-collapsed';
