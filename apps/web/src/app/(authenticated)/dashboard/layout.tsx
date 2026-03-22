/**
 * Dashboard Layout
 * Registers DashboardContextBar in the global ContextBar.
 */

import { type ReactNode } from 'react';

import { DashboardContextBar } from '@/components/dashboard/DashboardContextBar';
import { ContextBarRegistrar } from '@/components/layout/ContextBar';

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <ContextBarRegistrar>
        <DashboardContextBar />
      </ContextBarRegistrar>
      {children}
    </>
  );
}
