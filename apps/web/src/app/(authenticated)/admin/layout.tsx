/**
 * Admin Layout
 * Issue #3595 - Create admin sidebar navigation
 *
 * Wraps admin pages with AdminSidebar for navigation.
 * Works alongside AuthenticatedLayout to provide:
 * - Sidebar navigation (desktop: sidebar, mobile: drawer)
 * - Consistent admin page structure
 * - Responsive layout with proper spacing
 */

'use client';

import { type ReactNode } from 'react';

import { AdminSidebar } from '@/components/admin/layout/AdminSidebar';
import { cn } from '@/lib/utils';

interface AdminLayoutProps {
  children: ReactNode;
}

export default function AdminLayout({ children }: AdminLayoutProps) {
  return (
    <div className="flex h-[calc(100vh-4rem)] -mt-4">
      {/* Sidebar - includes both desktop sidebar and mobile trigger */}
      <AdminSidebar />

      {/* Main content area */}
      <main
        className={cn(
          'flex-1 overflow-y-auto',
          'px-4 sm:px-6 lg:px-8 py-4'
        )}
      >
        {children}
      </main>
    </div>
  );
}
