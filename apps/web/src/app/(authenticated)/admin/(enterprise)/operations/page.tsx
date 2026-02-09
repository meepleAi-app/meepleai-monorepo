/**
 * Enterprise Operations Section Page
 * Issue #3689 - Layout Base & Navigation System
 *
 * Section 3: Operations & Admin Tools
 * Tabs: Services | Cache | Email | Impersonate | Batch Jobs
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'operations')!;

export default function OperationsPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{}}
    />
  );
}
