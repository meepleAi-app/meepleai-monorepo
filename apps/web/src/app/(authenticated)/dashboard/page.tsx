/**
 * Old Dashboard Redirect - Issue #4588
 * Epic #4575: Gaming Hub Dashboard - Phase 4
 *
 * Redirects /dashboard to new Gaming Hub homepage at /
 */

import { redirect } from 'next/navigation';

export default function OldDashboardRedirect() {
  redirect('/');
}
