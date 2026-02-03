/**
 * Admin AI Usage Dashboard Page - Server Component Wrapper
 * Issue #3080: AI Usage Dashboard - Frontend
 */

import { AiUsageDashboardClient } from './client';

export const metadata = {
  title: 'AI Usage Dashboard | Admin | MeepleAI',
  description: 'Monitor AI costs, usage patterns, and resource allocation',
};

export default function AdminAiUsagePage() {
  return <AiUsageDashboardClient />;
}
