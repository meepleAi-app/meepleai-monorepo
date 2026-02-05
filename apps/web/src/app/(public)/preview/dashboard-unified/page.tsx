import { Metadata } from 'next';

import { UnifiedDashboardPreviewClient } from './client';

export const metadata: Metadata = {
  title: 'Dashboard Unificata - Preview | MeepleAI',
  description: 'Preview della dashboard unificata con toggle Compact/Extended',
};

export default function UnifiedDashboardPreviewPage() {
  return <UnifiedDashboardPreviewClient />;
}
