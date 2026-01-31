/**
 * Admin Subpage - Server Component Wrapper
 * Issue #1611 Phase 3: SSR Auth Protection Migration
 */

import { redirect } from 'next/navigation';

import { getServerUser } from '@/lib/auth/server';

import { AdminPageClient } from './client';

export default async function AdminSubpage() {
  const user = await getServerUser();
  if (!user) redirect('/login?from=/admin');
  if (user.role.toLowerCase() !== 'admin') redirect('/');
  return <AdminPageClient user={user} />;
}
