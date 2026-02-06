/**
 * Enterprise AI Platform Section Page
 * Issue #3689 - Layout Base & Navigation System
 *
 * Section 4: AI Platform
 * Tabs: AI Lab | Agents | Models | Chat Analytics | PDF Analytics
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'ai-platform')!;

export default function AiPlatformPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{}}
    />
  );
}
