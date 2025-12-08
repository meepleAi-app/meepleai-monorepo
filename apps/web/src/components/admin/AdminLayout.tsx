/**
 * AdminLayout Component - Issue #874
 *
 * Centralized admin layout with sidebar navigation and header.
 * Provides consistent structure for all admin pages.
 */

'use client';

import { ReactNode } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  LayoutDashboardIcon,
  UsersIcon,
  SettingsIcon,
  BarChartIcon,
  DatabaseIcon,
  FileTextIcon,
  ServerIcon,
  HomeIcon,
} from 'lucide-react';

export interface AdminLayoutProps {
  children: ReactNode;
}

interface NavItem {
  href: string;
  label: string;
  icon: typeof LayoutDashboardIcon;
}

const navigation: NavItem[] = [
  { href: '/admin', label: 'Dashboard', icon: LayoutDashboardIcon },
  { href: '/admin/users', label: 'Users', icon: UsersIcon },
  { href: '/admin/analytics', label: 'Analytics', icon: BarChartIcon },
  { href: '/admin/configuration', label: 'Configuration', icon: SettingsIcon },
  { href: '/admin/cache', label: 'Cache', icon: DatabaseIcon },
  { href: '/admin/prompts', label: 'Prompts', icon: FileTextIcon },
  { href: '/admin/n8n-templates', label: 'N8N Templates', icon: ServerIcon },
];

export function AdminLayout({ children }: AdminLayoutProps) {
  const pathname = usePathname();

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-16">
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-bold text-gray-900">MeepleAI Admin</h1>
            </div>
            <Link
              href="/"
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 hover:text-gray-900 hover:bg-gray-100 rounded-md transition-colors"
            >
              <HomeIcon className="h-4 w-4" />
              Back to Home
            </Link>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-[240px_1fr] gap-8">
          {/* Sidebar Navigation */}
          <aside className="space-y-2">
            <nav aria-label="Admin navigation">
              <ul className="space-y-1">
                {navigation.map(item => {
                  const Icon = item.icon;
                  const isActive = pathname === item.href;

                  return (
                    <li key={item.href}>
                      <Link
                        href={item.href}
                        className={cn(
                          'flex items-center gap-3 px-4 py-2 text-sm font-medium rounded-md transition-colors',
                          isActive
                            ? 'bg-indigo-100 text-indigo-700'
                            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
                        )}
                        aria-current={isActive ? 'page' : undefined}
                      >
                        <Icon className="h-5 w-5" aria-hidden="true" />
                        {item.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </nav>
          </aside>

          {/* Main Content */}
          <main className="flex-1">{children}</main>
        </div>
      </div>
    </div>
  );
}
