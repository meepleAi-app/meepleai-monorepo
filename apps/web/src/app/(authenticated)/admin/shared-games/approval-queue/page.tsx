/**
 * Approval Queue Page - Issue #3537
 *
 * Route: /admin/shared-games/approval-queue
 * Admin page for reviewing and approving games/PDFs submitted by editors.
 */

import { ApprovalQueueClient } from './client';

export const metadata = {
  title: 'Approval Queue | Admin',
  description: 'Review and approve pending game submissions',
};

export default function ApprovalQueuePage() {
  return <ApprovalQueueClient />;
}
