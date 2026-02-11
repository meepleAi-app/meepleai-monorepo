/**
 * Enterprise Admin Navigation Configuration
 * Issue #3689 - Layout Base & Navigation System
 *
 * Defines the 7-section enterprise admin dashboard navigation
 * with horizontal tab sub-sections per spec.
 */

import {
  type LucideIcon,
  LayoutDashboardIcon,
  BellIcon,
  ZapIcon,
  CoinsIcon,
  DatabaseIcon,
  ServerIcon,
  HardDriveIcon,
  BoxIcon,
  SettingsIcon,
  MailIcon,
  UserIcon,
  ListChecksIcon,
  BotIcon,
  BrainCircuitIcon,
  CpuIcon,
  MessageSquareIcon,
  FileTextIcon,
  UsersIcon,
  LibraryIcon,
  FlagIcon,
  SlidersIcon,
  BarChartIcon,
  WalletIcon,
  ClipboardListIcon,
  CalculatorIcon,
  TrendingUpIcon,
  ActivityIcon,
} from 'lucide-react';

/**
 * Tab definition within an enterprise section
 */
export interface EnterpriseTab {
  /** URL-safe tab identifier (used in ?tab= param) */
  id: string;
  /** Display label */
  label: string;
  /** Tab icon */
  icon: LucideIcon;
}

/**
 * Enterprise dashboard section (vertical sidebar item)
 */
export interface EnterpriseSection {
  /** Section unique identifier */
  id: string;
  /** Display label */
  label: string;
  /** Section icon */
  icon: LucideIcon;
  /** Route segment under /admin/(enterprise)/ */
  route: string;
  /** Description shown in header */
  description: string;
  /** Horizontal tabs within this section */
  tabs: EnterpriseTab[];
}

/**
 * Complete enterprise navigation structure - 7 main sections
 */
export const ENTERPRISE_SECTIONS: EnterpriseSection[] = [
  {
    id: 'overview',
    label: 'Overview',
    icon: LayoutDashboardIcon,
    route: 'overview',
    description: 'Dashboard panoramica con KPI real-time e alert sistema',
    tabs: [
      { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboardIcon },
      { id: 'alerts', label: 'Alerts', icon: BellIcon },
      { id: 'quick-actions', label: 'Quick Actions', icon: ZapIcon },
    ],
  },
  {
    id: 'resources',
    label: 'Resources',
    icon: CoinsIcon,
    route: 'resources',
    description: 'Gestione Token pool, Database, Cache, Vector store',
    tabs: [
      { id: 'tokens', label: 'Tokens', icon: CoinsIcon },
      { id: 'database', label: 'Database', icon: DatabaseIcon },
      { id: 'cache', label: 'Cache', icon: HardDriveIcon },
      { id: 'vectors', label: 'Vectors', icon: BrainCircuitIcon },
      { id: 'services', label: 'Services', icon: ServerIcon },
    ],
  },
  {
    id: 'operations',
    label: 'Operations',
    icon: SettingsIcon,
    route: 'operations',
    description: 'Admin tools, batch jobs, email e impersonation',
    tabs: [
      { id: 'services', label: 'Services', icon: BoxIcon },
      { id: 'cache', label: 'Cache', icon: HardDriveIcon },
      { id: 'email', label: 'Email', icon: MailIcon },
      { id: 'impersonate', label: 'Impersonate', icon: UserIcon },
      { id: 'batch-jobs', label: 'Batch Jobs', icon: ListChecksIcon },
    ],
  },
  {
    id: 'ai-platform',
    label: 'AI Platform',
    icon: BotIcon,
    route: 'ai-platform',
    description: 'AI Lab, agenti, modelli e analytics',
    tabs: [
      { id: 'ai-lab', label: 'AI Lab', icon: BrainCircuitIcon },
      { id: 'agents', label: 'Agents', icon: BotIcon },
      { id: 'models', label: 'Models', icon: CpuIcon },
      { id: 'chat-analytics', label: 'Chat Analytics', icon: MessageSquareIcon },
      { id: 'pdf-analytics', label: 'PDF Analytics', icon: FileTextIcon },
    ],
  },
  {
    id: 'users',
    label: 'Users & Content',
    icon: UsersIcon,
    route: 'users-content',
    description: 'Gestione utenti, libreria condivisa, feature flags',
    tabs: [
      { id: 'users', label: 'Users', icon: UsersIcon },
      { id: 'shared-library', label: 'Shared Library', icon: LibraryIcon },
      { id: 'feature-flags', label: 'Feature Flags', icon: FlagIcon },
      { id: 'user-limits', label: 'User Limits', icon: SlidersIcon },
    ],
  },
  {
    id: 'business',
    label: 'Business',
    icon: BarChartIcon,
    route: 'business',
    description: 'Usage stats, financial ledger e reports',
    tabs: [
      { id: 'usage-stats', label: 'Usage Stats', icon: ActivityIcon },
      { id: 'ledger-dashboard', label: 'Ledger Dashboard', icon: TrendingUpIcon },
      { id: 'financial-ledger', label: 'Financial Ledger', icon: WalletIcon },
      { id: 'reports', label: 'Reports', icon: ClipboardListIcon },
    ],
  },
  {
    id: 'simulations',
    label: 'Simulations',
    icon: CalculatorIcon,
    route: 'simulations',
    description: 'Cost calculator, resource forecast e batch jobs',
    tabs: [
      { id: 'cost-calculator', label: 'Cost Calculator', icon: CalculatorIcon },
      { id: 'resource-forecast', label: 'Resource Forecast', icon: TrendingUpIcon },
      { id: 'batch-jobs', label: 'Batch Jobs', icon: ListChecksIcon },
    ],
  },
];

/**
 * Get section by route segment
 */
export function getEnterpriseSectionByRoute(route: string): EnterpriseSection | undefined {
  return ENTERPRISE_SECTIONS.find((s) => s.route === route);
}

/**
 * Get section by ID
 */
export function getEnterpriseSectionById(id: string): EnterpriseSection | undefined {
  return ENTERPRISE_SECTIONS.find((s) => s.id === id);
}

/**
 * Get default tab for a section
 */
export function getDefaultTab(sectionId: string): string {
  const section = getEnterpriseSectionById(sectionId);
  return section?.tabs[0]?.id ?? 'dashboard';
}
