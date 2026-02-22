/**
 * TopNavbar Section Configuration
 * Issue #5036 — Navbar Component
 *
 * Defines the 3-section structure: Tool / Discover / Admin
 * Each section has a dropdown with navigation items.
 */

import {
  Activity,
  BarChart2,
  Bell,
  BookOpen,
  Bot,
  Cpu,
  Database,
  FileText,
  Gamepad2,
  Globe,
  History,
  LayoutDashboard,
  MessageSquare,
  Monitor,
  Settings,
  Shield,
  Sliders,
  Users,
} from 'lucide-react';

export interface NavSectionItem {
  label: string;
  href: string;
  icon: typeof BookOpen;
  description?: string;
}

export interface NavSection {
  id: string;
  label: string;
  items: NavSectionItem[];
}

/** Tool section — productive features for authenticated users */
export const TOOL_SECTION: NavSection = {
  id: 'tool',
  label: 'Tool',
  items: [
    {
      label: 'Dashboard',
      href: '/dashboard',
      icon: LayoutDashboard,
      description: 'La tua homepage personale',
    },
    {
      label: 'Library',
      href: '/library',
      icon: BookOpen,
      description: 'I tuoi giochi e collezioni',
    },
    {
      label: 'Agents',
      href: '/agents',
      icon: Bot,
      description: 'I tuoi assistenti AI',
    },
    {
      label: 'Chat',
      href: '/chat',
      icon: MessageSquare,
      description: 'Conversazioni con gli agenti',
    },
    {
      label: 'Sessions',
      href: '/sessions',
      icon: Activity,
      description: 'Sessioni di gioco attive',
    },
    {
      label: 'Play Records',
      href: '/play-records',
      icon: History,
      description: 'Cronologia delle partite',
    },
  ],
};

/** Discover section — community and catalog */
export const DISCOVER_SECTION: NavSection = {
  id: 'discover',
  label: 'Discover',
  items: [
    {
      label: 'Catalogo',
      href: '/discover',
      icon: Gamepad2,
      description: 'Tutti i giochi disponibili',
    },
    {
      label: 'Community',
      href: '/discover?tab=community',
      icon: Globe,
      description: 'Condivisioni dalla community',
    },
    {
      label: 'Proposte',
      href: '/discover?tab=proposals',
      icon: FileText,
      description: 'Proposte di nuovi giochi',
    },
  ],
};

/** Admin section — management (admin/editor only) */
export const ADMIN_SECTION: NavSection = {
  id: 'admin',
  label: 'Admin',
  items: [
    {
      label: 'Users',
      href: '/admin/users',
      icon: Users,
      description: 'Gestione utenti',
    },
    {
      label: 'AI & Agenti',
      href: '/admin/ai',
      icon: Cpu,
      description: 'Lab AI, modelli, prompt',
    },
    {
      label: 'Content',
      href: '/admin/content',
      icon: Database,
      description: 'Giochi, KB, FAQ',
    },
    {
      label: 'Analytics',
      href: '/admin/analytics',
      icon: BarChart2,
      description: 'Usage, audit, report',
    },
    {
      label: 'Config',
      href: '/admin/config',
      icon: Settings,
      description: 'Configurazione sistema',
    },
    {
      label: 'Monitor',
      href: '/admin/monitor',
      icon: Monitor,
      description: 'Infra, alert, cache',
    },
  ],
};

/** Editor section — subset of admin (editor role only) */
export const EDITOR_SECTION: NavSection = {
  id: 'editor',
  label: 'Editor',
  items: [
    {
      label: 'AI & Agenti',
      href: '/admin/ai',
      icon: Cpu,
      description: 'Lab AI, modelli, prompt',
    },
    {
      label: 'Content',
      href: '/admin/content',
      icon: Database,
      description: 'Giochi, KB, FAQ',
    },
  ],
};
