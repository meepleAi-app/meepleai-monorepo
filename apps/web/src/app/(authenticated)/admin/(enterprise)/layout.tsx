/**
 * Enterprise Admin Route Group Layout
 * Issue #3689 - Layout Base & Navigation System
 *
 * This layout wraps all enterprise admin sections.
 * It provides the sidebar + content area structure.
 * Individual section pages provide their own header + tabs.
 */

'use client';

import React, { type ReactNode } from 'react';

import { EnterpriseAdminSidebar } from '@/components/admin/enterprise/EnterpriseAdminSidebar';
import { MobileBottomNav } from '@/components/admin/enterprise/MobileBottomNav';

interface EnterpriseLayoutProps {
  children: ReactNode;
}

export default function EnterpriseLayout({ children }: EnterpriseLayoutProps) {
  return (
    <div className="flex h-[calc(100vh-4rem)] -mt-4">
      {/* Vertical Sidebar */}
      <EnterpriseAdminSidebar />

      {/* Main content area */}
      <main className="flex-1 flex flex-col overflow-hidden">
        {children}
      </main>

      {/* Mobile bottom navigation */}
      <MobileBottomNav />
    </div>
  );
}
