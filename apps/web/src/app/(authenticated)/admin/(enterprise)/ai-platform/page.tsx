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

import { ChatAnalyticsTab } from './components/ChatAnalyticsTab';
import { ModelPerformanceTab } from './components/ModelPerformanceTab';
import { PdfAnalyticsTab } from './components/PdfAnalyticsTab';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'ai-platform')!;

export default function AiPlatformPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{
        'chat-analytics': <ChatAnalyticsTab />,
        models: <ModelPerformanceTab />,
        'pdf-analytics': <PdfAnalyticsTab />,
      }}
    />
  );
}
