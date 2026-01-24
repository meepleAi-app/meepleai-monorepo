import { AdminShareRequestDetailClient } from './client';

/**
 * Admin Share Request Review Detail Page
 *
 * Server component wrapper for the detailed share request review page.
 * Delegates all logic to client component for interactivity.
 *
 * Route: /admin/share-requests/[id]
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface AdminShareRequestDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function AdminShareRequestDetailPage({
  params,
}: AdminShareRequestDetailPageProps) {
  const { id } = await params;
  return <AdminShareRequestDetailClient id={id} />;
}
