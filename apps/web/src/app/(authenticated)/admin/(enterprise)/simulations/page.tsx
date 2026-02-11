/**
 * Enterprise Simulations Section Page
 * Issue #3689 - Layout Base & Navigation System
 * Issue #3725 - Agent Cost Calculator
 *
 * Section 7: Simulations & Forecasting
 * Tabs: Cost Calculator | Resource Forecast | Batch Jobs
 */

'use client';

import React from 'react';

import { AgentCostCalculatorTab } from '@/components/admin/enterprise/cost-calculator';
import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'simulations')!;

export default function SimulationsPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{
        'cost-calculator': <AgentCostCalculatorTab />,
      }}
    />
  );
}
