/**
 * Admin Pending Typologies Approval Queue Page (Issue #3181)
 *
 * Server component for pending typology approvals.
 * Displays typologies with status=PendingReview for admin review.
 * Part of Epic #3174 (AI Agent System).
 */

import { Metadata } from 'next';

import { PendingTypologiesClient } from './client';

export const metadata: Metadata = {
  title: 'Pending Typologies | Admin | MeepleAI',
  description:
    'Review and approve pending agent typology proposals from editors.',
};

export default function PendingTypologiesPage() {
  return <PendingTypologiesClient />;
}
