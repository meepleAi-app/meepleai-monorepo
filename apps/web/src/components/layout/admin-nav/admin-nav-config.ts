import type { ComponentType } from 'react';

import {
  Activity,
  BarChart2,
  Bot,
  BookOpen,
  Database,
  FileSearch,
  Gamepad2,
  Globe,
  LayoutDashboard,
  Mail,
  MonitorCheck,
  Settings,
  Shield,
  Users,
  BellRing,
  UserCheck,
  Wrench,
} from 'lucide-react';

import type { UserRole } from '@/types/auth';

export interface AdminNavItem {
  label: string;
  href: string;
  icon: ComponentType<{ className?: string }>;
  /** Minimum role required to see this item. Defaults to 'admin' when omitted. */
  minRole?: UserRole;
}

export interface AdminNavGroup {
  id: 'A' | 'B' | 'C' | 'D';
  label: string;
  icon: ComponentType<{ className?: string }>;
  items: AdminNavItem[];
}

/**
 * Canonical admin navigation, organised in the 4 groups of the SP5 consolidation
 * spec (A Admin Console / B Power-User Tools / C Platform & Operations /
 * D AI Tooling & Data Quality).
 *
 * Only routes that already exist are listed here. New screens land in waves
 * F3-F6 by extending this array. Paths use the canonical hubs already enforced
 * by next.config.js redirects (Issue #5040).
 */
export const ADMIN_NAV_GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: LayoutDashboard,
    items: [
      { label: 'Dashboard', href: '/admin/overview', icon: LayoutDashboard },
      { label: 'Activity Feed', href: '/admin/overview/activity', icon: Activity },
      { label: 'System Health', href: '/admin/overview/system', icon: MonitorCheck },
      { label: 'Users', href: '/admin/users', icon: Users },
      { label: 'Content', href: '/admin/content', icon: Gamepad2 },
      { label: 'AI / RAG', href: '/admin/ai', icon: Bot },
      { label: 'Knowledge Base', href: '/admin/knowledge-base', icon: BookOpen },
      { label: 'Catalog Ingestion', href: '/admin/catalog-ingestion', icon: Database },
      { label: 'Config', href: '/admin/config', icon: Settings },
      { label: 'Monitor', href: '/admin/monitor', icon: MonitorCheck },
      { label: 'Notifications', href: '/admin/notifications/compose', icon: BellRing },
    ],
  },
  {
    id: 'B',
    label: 'Power-User Tools',
    icon: Wrench,
    items: [
      { label: 'Giochi', href: '/admin/shared-games/all', icon: Gamepad2 },
      { label: 'Email Templates', href: '/admin/content/email-templates', icon: Mail },
      { label: 'Invitations', href: '/admin/users/invitations', icon: Mail },
      { label: 'Ruoli & Permessi', href: '/admin/users/roles', icon: Shield },
      { label: 'Access Requests', href: '/admin/users/access-requests', icon: UserCheck },
    ],
  },
  {
    id: 'C',
    label: 'Platform & Operations',
    icon: Globe,
    items: [
      { label: 'Providers', href: '/admin/providers', icon: Globe },
      { label: 'Analytics', href: '/admin/analytics', icon: BarChart2 },
      {
        label: 'Staging Access',
        href: '/admin/staging-access',
        icon: Shield,
        minRole: 'superadmin',
      },
    ],
  },
  {
    id: 'D',
    label: 'AI Tooling & Data Quality',
    icon: Bot,
    items: [
      { label: 'Agent Definitions', href: '/admin/agents/definitions', icon: Database },
      { label: 'RAG Inspector', href: '/admin/agents/inspector', icon: FileSearch },
      { label: 'RAG Quality', href: '/admin/rag-quality', icon: BarChart2 },
      {
        label: 'Mechanic Extractor',
        href: '/admin/knowledge-base/mechanic-extractor',
        icon: Wrench,
      },
      { label: 'A/B Testing', href: '/admin/agents/ab-testing', icon: BarChart2 },
      { label: 'Agent Usage', href: '/admin/agents/usage', icon: BarChart2 },
    ],
  },
];
