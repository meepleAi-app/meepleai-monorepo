/**
 * Enterprise Users & Content Section Page
 * Issue #3689 - Layout Base & Navigation System
 *
 * Section 5: Users & Content
 * Tabs: Users | Shared Library | Feature Flags | User Limits
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'users')!;

export default function UsersContentPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{}}
    />
  );
}
