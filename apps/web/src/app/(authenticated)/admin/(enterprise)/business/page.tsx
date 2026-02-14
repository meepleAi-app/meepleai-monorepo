/**
 * Enterprise Business Section Page
 * Issue #3689 - Layout Base & Navigation System
 * Issue #3723 - Ledger Dashboard and Visualization
 *
 * Section 6: Business & Analytics
 * Tabs: Usage Stats | Ledger Dashboard | Financial Ledger | Reports
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { FinancialLedgerTab, LedgerDashboardTab } from '@/components/admin/enterprise/financial-ledger';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'business')!;

export default function BusinessPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{
        'ledger-dashboard': <LedgerDashboardTab />,
        'financial-ledger': <FinancialLedgerTab />,
      }}
    />
  );
}
