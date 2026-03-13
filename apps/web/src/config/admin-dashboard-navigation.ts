/**
 * Admin Dashboard Navigation Configuration
 * Unified Top Nav + Contextual Sidebar navigation system
 *
 * 6 main sections: Overview, Content, AI, Users, System, Analytics
 * Each section has sidebar items that change contextually.
 */

import {
  type LucideIcon,
  LayoutDashboardIcon,
  UsersIcon,
  BotIcon,
  BookOpenIcon,
  ActivityIcon,
  ServerIcon,
  ShieldIcon,
  ListIcon,
  TagIcon,
  PlusCircleIcon,
  BrainCircuitIcon,
  BarChartIcon,
  CpuIcon,
  MessageSquareIcon,
  WrenchIcon,
  FileTextIcon,
  UploadIcon,
  DatabaseIcon,
  SettingsIcon,
  GitBranchIcon,
  TerminalIcon,
  Settings2Icon,
  MessageSquareCodeIcon,
  ListOrderedIcon,
  TrendingUpIcon,
  MonitorIcon,
  BellIcon,
  HardDriveIcon,
  PlayIcon,
  PackageIcon,
  SlidersIcon,
  FlagIcon,
  GaugeIcon,
  ZapIcon,
  ClipboardListIcon,
  KeyIcon,
  MailIcon,
  ScrollTextIcon,
  HeartPulseIcon,
  BarChart3Icon,
} from 'lucide-react';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface DashboardSidebarItem {
  /** Route path (absolute) */
  href: string;
  /** Display label */
  label: string;
  /** Lucide icon */
  icon: LucideIcon;
  /** Badge key for dynamic count lookup */
  badgeKey?: string;
  /** Active state pattern (defaults to prefix match) */
  activePattern?: RegExp;
}

export interface DashboardSection {
  /** Section unique ID */
  id: string;
  /** Display label for top nav */
  label: string;
  /** Section icon */
  icon: LucideIcon;
  /** Base route prefix for matching */
  baseRoute: string;
  /** Additional route prefixes that belong to this section (outside baseRoute) */
  additionalRoutes?: string[];
  /** Short description */
  description: string;
  /** Sidebar items for this section */
  sidebarItems: DashboardSidebarItem[];
  /** Visual group (kept for backwards compatibility, all sections render flat) */
  group: 'core' | 'ai';
}

// ─── Navigation Definition ───────────────────────────────────────────────────

export const DASHBOARD_SECTIONS: DashboardSection[] = [
  // ── 1. Overview ──────────────────────────────────────────────────────────
  {
    id: 'overview',
    label: 'Overview',
    icon: LayoutDashboardIcon,
    baseRoute: '/admin/overview',
    description: 'Dashboard, stats, and quick actions',
    group: 'core',
    sidebarItems: [
      {
        href: '/admin/overview',
        label: 'Dashboard',
        icon: LayoutDashboardIcon,
        activePattern: /^\/admin\/overview$/,
      },
      {
        href: '/admin/overview/activity',
        label: 'Activity Feed',
        icon: ActivityIcon,
      },
      {
        href: '/admin/overview/system',
        label: 'System Health',
        icon: ServerIcon,
      },
    ],
  },

  // ── 2. Content (merged: Shared Games + Knowledge Base + Content hub) ─────
  {
    id: 'content',
    label: 'Content',
    icon: FileTextIcon,
    baseRoute: '/admin/shared-games',
    additionalRoutes: [
      '/admin/content',
      '/admin/knowledge-base',
      '/admin/games',
      '/admin/content/email-templates',
    ],
    description: 'Games, documents, vectors, and RAG pipeline',
    group: 'core',
    sidebarItems: [
      // Games
      {
        href: '/admin/shared-games/all',
        label: 'All Games',
        icon: ListIcon,
        activePattern: /^\/admin\/shared-games(\/all)?$/,
      },
      {
        href: '/admin/shared-games/new',
        label: 'Add Game',
        icon: PlusCircleIcon,
      },
      {
        href: '/admin/shared-games/categories',
        label: 'Categories',
        icon: TagIcon,
      },
      {
        href: '/admin/shared-games/import',
        label: 'Import Wizard',
        icon: UploadIcon,
      },
      // Knowledge Base
      {
        href: '/admin/knowledge-base',
        label: 'KB Overview',
        icon: BookOpenIcon,
        activePattern: /^\/admin\/knowledge-base$/,
      },
      {
        href: '/admin/knowledge-base/documents',
        label: 'Documents',
        icon: ListOrderedIcon,
      },
      {
        href: '/admin/knowledge-base/queue',
        label: 'Processing Queue',
        icon: ActivityIcon,
      },
      {
        href: '/admin/knowledge-base/vectors',
        label: 'Vector Collections',
        icon: DatabaseIcon,
      },
      {
        href: '/admin/knowledge-base/upload',
        label: 'Upload & Process',
        icon: UploadIcon,
      },
      // Email Templates (Issue #52)
      {
        href: '/admin/content/email-templates',
        label: 'Email Templates',
        icon: MailIcon,
      },
    ],
  },

  // ── 3. AI (merged: Agents + AI & Agents hub) ────────────────────────────
  {
    id: 'ai',
    label: 'AI',
    icon: BrainCircuitIcon,
    baseRoute: '/admin/agents',
    additionalRoutes: ['/admin/ai'],
    description: 'AI agents, models, RAG, and analytics',
    group: 'ai',
    sidebarItems: [
      {
        href: '/admin/agents',
        label: 'All Agents',
        icon: BotIcon,
        activePattern: /^\/admin\/agents$/,
      },
      {
        href: '/admin/agents/definitions',
        label: 'Definitions',
        icon: ListIcon,
        activePattern: /^\/admin\/agents\/definitions/,
      },
      {
        href: '/admin/agents/builder',
        label: 'Agent Builder',
        icon: WrenchIcon,
      },
      {
        href: '/admin/agents/models',
        label: 'Models & Prompts',
        icon: CpuIcon,
      },
      {
        href: '/admin/agents/chat-history',
        label: 'Chat History',
        icon: MessageSquareIcon,
      },
      {
        href: '/admin/agents/pipeline',
        label: 'Pipeline Explorer',
        icon: GitBranchIcon,
      },
      {
        href: '/admin/agents/debug',
        label: 'Debug Console',
        icon: TerminalIcon,
      },
      {
        href: '/admin/agents/debug-chat',
        label: 'Debug Chat',
        icon: MessageSquareCodeIcon,
      },
      {
        href: '/admin/agents/strategy',
        label: 'Strategy Config',
        icon: Settings2Icon,
      },
      {
        href: '/admin/agents/chat-limits',
        label: 'Chat Limits',
        icon: SettingsIcon,
      },
      {
        href: '/admin/agents/usage',
        label: 'Usage & Costs',
        icon: TrendingUpIcon,
      },
      {
        href: '/admin/agents/analytics',
        label: 'Analytics',
        icon: BarChartIcon,
      },
    ],
  },

  // ── 4. Users ─────────────────────────────────────────────────────────────
  {
    id: 'users',
    label: 'Users',
    icon: UsersIcon,
    baseRoute: '/admin/users',
    description: 'User management, roles, and activity',
    group: 'core',
    sidebarItems: [
      {
        href: '/admin/users',
        label: 'All Users',
        icon: UsersIcon,
        activePattern: /^\/admin\/users$/,
      },
      {
        href: '/admin/users/invitations',
        label: 'Invitations',
        icon: MailIcon,
      },
      {
        href: '/admin/users/roles',
        label: 'Roles & Permissions',
        icon: ShieldIcon,
      },
      {
        href: '/admin/users/activity',
        label: 'Activity Log',
        icon: ActivityIcon,
      },
    ],
  },

  // ── 5. System (merged: Monitor + Configuration) ──────────────────────────
  {
    id: 'system',
    label: 'System',
    icon: MonitorIcon,
    baseRoute: '/admin/monitor',
    additionalRoutes: ['/admin/config'],
    description: 'Monitoring, alerts, cache, and system configuration',
    group: 'core',
    sidebarItems: [
      // Monitor items
      {
        href: '/admin/monitor?tab=alerts',
        label: 'Alerts',
        icon: BellIcon,
        activePattern: /^\/admin\/monitor(\?tab=alerts)?$/,
      },
      {
        href: '/admin/monitor?tab=cache',
        label: 'Cache',
        icon: DatabaseIcon,
      },
      {
        href: '/admin/monitor?tab=infra',
        label: 'Infrastructure',
        icon: HardDriveIcon,
      },
      {
        href: '/admin/monitor?tab=command',
        label: 'Command Center',
        icon: TerminalIcon,
      },
      {
        href: '/admin/monitor?tab=testing',
        label: 'Testing',
        icon: PlayIcon,
      },
      {
        href: '/admin/monitor?tab=export',
        label: 'Bulk Export',
        icon: PackageIcon,
      },
      {
        href: '/admin/monitor?tab=email',
        label: 'Email',
        icon: MailIcon,
      },
      // Operations Console (Issue #126)
      {
        href: '/admin/monitor/operations',
        label: 'Operations',
        icon: ScrollTextIcon,
        activePattern: /^\/admin\/monitor\/operations/,
      },
      // Service Dashboard (Issue #132)
      {
        href: '/admin/monitor/services',
        label: 'Services',
        icon: HeartPulseIcon,
        activePattern: /^\/admin\/monitor\/services/,
      },
      // Grafana Dashboards (Issue #134)
      {
        href: '/admin/monitor/grafana',
        label: 'Grafana',
        icon: BarChart3Icon,
        activePattern: /^\/admin\/monitor\/grafana/,
      },
      // Config items
      {
        href: '/admin/config?tab=general',
        label: 'General Config',
        icon: SlidersIcon,
        activePattern: /^\/admin\/config(\?tab=general)?$/,
      },
      {
        href: '/admin/config?tab=limits',
        label: 'Limits',
        icon: GaugeIcon,
      },
      {
        href: '/admin/config?tab=flags',
        label: 'Feature Flags',
        icon: FlagIcon,
      },
      {
        href: '/admin/config?tab=rate-limits',
        label: 'Rate Limits',
        icon: ZapIcon,
      },
      {
        href: '/admin/config/n8n',
        label: 'n8n Workflows',
        icon: ZapIcon,
      },
    ],
  },

  // ── 6. Analytics ─────────────────────────────────────────────────────────
  {
    id: 'analytics',
    label: 'Analytics',
    icon: BarChartIcon,
    baseRoute: '/admin/analytics',
    description: 'Usage statistics, audit logs, and reports',
    group: 'core',
    sidebarItems: [
      {
        href: '/admin/analytics?tab=overview',
        label: 'Overview',
        icon: BarChartIcon,
        activePattern: /^\/admin\/analytics(\?tab=overview)?$/,
      },
      {
        href: '/admin/analytics?tab=ai-usage',
        label: 'AI Usage',
        icon: CpuIcon,
      },
      {
        href: '/admin/analytics?tab=audit',
        label: 'Audit Log',
        icon: ClipboardListIcon,
      },
      {
        href: '/admin/analytics?tab=reports',
        label: 'Reports',
        icon: FileTextIcon,
      },
      {
        href: '/admin/analytics?tab=api-keys',
        label: 'API Keys',
        icon: KeyIcon,
      },
    ],
  },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Get section by ID */
export function getSection(id: string): DashboardSection | undefined {
  return DASHBOARD_SECTIONS.find(s => s.id === id);
}

/** Get active section based on current pathname */
export function getActiveSection(pathname: string): DashboardSection | undefined {
  // First try exact baseRoute match
  const exact = DASHBOARD_SECTIONS.find(s => pathname === s.baseRoute);
  if (exact) return exact;

  // Check additionalRoutes (exact or prefix match)
  const byAdditional = DASHBOARD_SECTIONS.find(s =>
    s.additionalRoutes?.some(r => pathname === r || pathname.startsWith(r + '/'))
  );
  if (byAdditional) return byAdditional;

  // Then try prefix match on baseRoute (longest match wins)
  const sorted = [...DASHBOARD_SECTIONS].sort((a, b) => b.baseRoute.length - a.baseRoute.length);
  return sorted.find(s => pathname === s.baseRoute || pathname.startsWith(s.baseRoute + '/'));
}

/** Get sidebar items for a section */
export function getSidebarItems(sectionId: string): DashboardSidebarItem[] {
  return getSection(sectionId)?.sidebarItems ?? [];
}

/** Check if a sidebar item is active */
export function isSidebarItemActive(item: DashboardSidebarItem, pathname: string): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  return pathname === item.href || pathname.startsWith(item.href + '/');
}

/** Check if a section is active */
export function isSectionActive(section: DashboardSection, pathname: string): boolean {
  if (pathname === section.baseRoute || pathname.startsWith(section.baseRoute + '/')) {
    return true;
  }
  return (
    section.additionalRoutes?.some(r => pathname === r || pathname.startsWith(r + '/')) ?? false
  );
}

/** Storage key for sidebar collapsed state */
export const ADMIN_DASHBOARD_SIDEBAR_COLLAPSED_KEY = 'admin-dashboard-sidebar-collapsed';
