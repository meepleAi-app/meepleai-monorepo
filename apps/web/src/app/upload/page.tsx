/**
 * Upload Page - Server Component Wrapper
 *
 * Issue #1611: SSR Auth Protection Migration
 * ADR-015: Server-Side Rendering (SSR) Authentication Protection
 *
 * This is a Server Component that:
 * 1. Validates authentication server-side (zero UI flash)
 * 2. Checks role authorization (admin/editor only)
 * 3. Redirects before render if unauthorized
 * 4. Passes authenticated user to client component
 */

import { getServerUser } from '@/lib/auth/server';
import { redirect } from 'next/navigation';
import { UploadClient } from './upload-client';

const AUTHORIZED_ROLES = ['admin', 'editor'];

export default async function UploadPage() {
  // Server-side authentication check
  const user = await getServerUser();

  // Not authenticated → redirect to login
  if (!user) {
    redirect('/login?from=/upload');
  }

  // Authenticated but unauthorized role → redirect to home
  if (!AUTHORIZED_ROLES.includes(user.role.toLowerCase())) {
    redirect('/');
  }

  // User is authenticated and authorized
  // Pass user to client component for interactive logic
  return <UploadClient user={user} />;
}
