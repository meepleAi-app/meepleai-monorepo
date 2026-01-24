import { AdminShareRequestsClient } from './client';

/**
 * Admin Share Requests Queue Page
 *
 * Server component wrapper for the admin share requests queue.
 * Delegates all logic to client component for interactivity.
 *
 * Route: /admin/share-requests
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

export default function AdminShareRequestsPage() {
  return <AdminShareRequestsClient />;
}
