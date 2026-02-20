/**
 * Admin Dashboard Navigation Configuration
 * Unified Top Nav + Contextual Sidebar navigation system
 *
 * 5 main sections: Overview, Users, Shared Games, Agents, Knowledge Base
 * Each section has sidebar items that change contextually.
 */

import {
  type LucideIcon,
  LayoutDashboardIcon,
  UsersIcon,
  ShareIcon,
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
  /** Visual group: 'core' or 'ai' */
  group: 'core' | 'ai';
}

// ─── Navigation Definition ───────────────────────────────────────────────────

export const DASHBOARD_SECTIONS: DashboardSection[] = [
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
  {
    id: 'shared-games',
    label: 'Shared Games',
    icon: ShareIcon,
    baseRoute: '/admin/shared-games',
    description: 'Game catalog, approvals, and categories',
    group: 'core',
    sidebarItems: [
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
    ],
  },
  {
    id: 'agents',
    label: 'Agents',
    icon: BotIcon,
    baseRoute: '/admin/agents',
    description: 'AI agents, models, and analytics',
    group: 'ai',
    sidebarItems: [
      {
        href: '/admin/agents',
        label: 'All Agents',
        icon: BotIcon,
        activePattern: /^\/admin\/agents$/,
      },
      {
        href: '/admin/agents/builder',
        label: 'Agent Builder',
        icon: WrenchIcon,
      },
      {
        href: '/admin/agents/analytics',
        label: 'Analytics',
        icon: BarChartIcon,
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
    ],
  },
  {
    id: 'knowledge-base',
    label: 'Knowledge Base',
    icon: BookOpenIcon,
    baseRoute: '/admin/knowledge-base',
    additionalRoutes: ['/admin/pdfs'],
    description: 'Documents, vectors, and RAG pipeline',
    group: 'ai',
    sidebarItems: [
      {
        href: '/admin/knowledge-base',
        label: 'Overview',
        icon: FileTextIcon,
        activePattern: /^\/admin\/knowledge-base$/,
      },
      {
        href: '/admin/pdfs',
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
        href: '/admin/knowledge-base/embedding',
        label: 'Embedding Service',
        icon: CpuIcon,
      },
      {
        href: '/admin/knowledge-base/upload',
        label: 'Upload & Process',
        icon: UploadIcon,
      },
      {
        href: '/admin/knowledge-base/pipeline',
        label: 'RAG Pipeline',
        icon: BrainCircuitIcon,
      },
      {
        href: '/admin/knowledge-base/settings',
        label: 'Settings',
        icon: SettingsIcon,
      },
    ],
  },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────

/** Get section by ID */
export function getSection(id: string): DashboardSection | undefined {
  return DASHBOARD_SECTIONS.find((s) => s.id === id);
}

/** Get active section based on current pathname */
export function getActiveSection(pathname: string): DashboardSection | undefined {
  // First try exact baseRoute match
  const exact = DASHBOARD_SECTIONS.find((s) => pathname === s.baseRoute);
  if (exact) return exact;

  // Check additionalRoutes (exact or prefix match)
  const byAdditional = DASHBOARD_SECTIONS.find((s) =>
    s.additionalRoutes?.some(
      (r) => pathname === r || pathname.startsWith(r + '/')
    )
  );
  if (byAdditional) return byAdditional;

  // Then try prefix match on baseRoute (longest match wins)
  const sorted = [...DASHBOARD_SECTIONS].sort(
    (a, b) => b.baseRoute.length - a.baseRoute.length
  );
  return sorted.find(
    (s) => pathname === s.baseRoute || pathname.startsWith(s.baseRoute + '/')
  );
}

/** Get sidebar items for a section */
export function getSidebarItems(sectionId: string): DashboardSidebarItem[] {
  return getSection(sectionId)?.sidebarItems ?? [];
}

/** Check if a sidebar item is active */
export function isSidebarItemActive(
  item: DashboardSidebarItem,
  pathname: string
): boolean {
  if (item.activePattern) {
    return item.activePattern.test(pathname);
  }
  return pathname === item.href || pathname.startsWith(item.href + '/');
}

/** Check if a section is active */
export function isSectionActive(
  section: DashboardSection,
  pathname: string
): boolean {
  if (pathname === section.baseRoute || pathname.startsWith(section.baseRoute + '/')) {
    return true;
  }
  return section.additionalRoutes?.some(
    (r) => pathname === r || pathname.startsWith(r + '/')
  ) ?? false;
}

/** Storage key for sidebar collapsed state */
export const ADMIN_DASHBOARD_SIDEBAR_COLLAPSED_KEY = 'admin-dashboard-sidebar-collapsed';
