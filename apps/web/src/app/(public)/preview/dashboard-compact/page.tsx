/**
 * Public Preview: Compact Dashboard
 * For development/testing purposes - no authentication required
 */

import { Metadata } from 'next';

import { PreviewClient } from './client';

export const metadata: Metadata = {
  title: 'Preview: Dashboard Compact | MeepleAI',
  description: 'Preview del dashboard compatto',
};

export default function DashboardCompactPreviewPage() {
  return <PreviewClient />;
}
