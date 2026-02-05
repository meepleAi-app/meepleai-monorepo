/**
 * Compact Dashboard Preview Page
 * Issue #3306 - User Dashboard Hub Core - MVP
 *
 * Preview route for the refined compact dashboard design.
 * Access at: /dashboard/compact
 */

import { Metadata } from 'next';

import { CompactDashboardClient } from './client';

export const metadata: Metadata = {
  title: 'Dashboard | MeepleAI',
  description: 'La tua dashboard MeepleAI - Collezione, partite e AI assistant',
};

export default function CompactDashboardPage() {
  return <CompactDashboardClient />;
}
