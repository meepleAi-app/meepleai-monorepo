/**
 * Admin Dashboard - Server Component Wrapper
 *
 * Issue #1611 Phase 2: SSR Auth Protection Migration
 * ADR-015: Server-Side Rendering (SSR) Authentication Protection
 *
 * This is a Server Component that:
 * 1. Validates authentication server-side (zero UI flash)
 * 2. Checks role authorization (admin only)
 * 3. Redirects before render if unauthorized
 * 4. Passes authenticated user to client component
 */

import { getServerUser } from '@/lib/auth/server';
import { redirect } from 'next/navigation';
import { AdminClient } from './admin-client';

export default async function AdminPage() {
  // Server-side authentication check
  const user = await getServerUser();

  // Not authenticated → redirect to login
  if (!user) {
    redirect('/login?from=/admin');
  }

  // Authenticated but not admin → redirect to home
  if (user.role.toLowerCase() !== 'admin') {
    redirect('/');
  }

  // User is authenticated and authorized as admin
  // Pass user to client component for interactive logic
  return <AdminClient user={user} />;
}
