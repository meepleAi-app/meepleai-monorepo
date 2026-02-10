/**
 * Enterprise Business Section Page
 * Issue #3689 - Layout Base & Navigation System
 *
 * Section 6: Business & Analytics
 * Tabs: Usage Stats | Financial Ledger | Reports
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'business')!;

export default function BusinessPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{}}
    />
  );
}
