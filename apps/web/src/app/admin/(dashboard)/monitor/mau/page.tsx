/**
 * MAU Monitoring Page - Admin Dashboard
 *
 * Route: /admin/monitor/mau
 * Issue #113: MAU Monitoring Dashboard.
 */

import { type Metadata } from 'next';

import { MauDashboard } from './MauDashboard';

export const metadata: Metadata = {
  title: 'MAU Monitoring',
  description: 'Monthly Active AI Users monitoring dashboard',
};

export default function MauMonitoringPage() {
  return (
    <div className="container py-6">
      <h1 className="mb-6 text-2xl font-bold">MAU Monitoring Dashboard</h1>
      <MauDashboard />
    </div>
  );
}
